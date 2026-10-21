"""Owned MySQL + real HTTP shared-file provider checks.

/_shared_test routes are explicit consumer fixtures, NOT member 2 product code.
No user DB, product settings, schema or sample data is used.
"""
import base64, hashlib, json, os, pathlib, secrets, sys, tempfile, threading
from datetime import date, timedelta
from urllib.request import Request, urlopen
from urllib.error import HTTPError
from concurrent.futures import ThreadPoolExecutor
from unittest.mock import patch

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parents[2]
sys.path[:0] = [str(ROOT/'RemoteWork.Server'), str(ROOT/'Tests/TaskQueries.Http.Tests')]
import run_mysql_http_check as owned
from sqlalchemy import select, func
from werkzeug.security import generate_password_hash
from waitress import create_server
from flask import g, request
from remotework import create_app
from remotework.auth import authenticated_transaction, login_required
from remotework.common import AppError, payload, success
from remotework.db import get_db, transaction
from remotework.models import Base, User, AdminGuard, SchemaVersion, utcnow
from remotework.write_contract import ExpectedVersions
from remotework.features import results, shared_attachments as shared
from remotework.features.tasks.models import TaskRecord, TaskAssignment, TaskManager
from remotework.features.tasks.lifecycle_models import TaskCancellation

COUNT = 0


def check(value, label):
    global COUNT
    if not value: raise AssertionError(label)
    COUNT += 1


def run(url, directory):
    app = create_app(dict(TESTING=False, DATABASE_URL=url, SECRET_KEY=secrets.token_hex(32),
        ENABLE_TASK_QUERY_DRAFT=True, ENABLE_COMMON_WRITE_DRAFT=True,
        ENABLE_TASK_CREATE_DRAFT=True, ENABLE_TASK_START_DRAFT=True,
        ENABLE_RESULTS_DRAFT=True, ENABLE_SHARED_ATTACHMENTS_DRAFT=True,
        RESULTS_CONTEXT_MODE='local-development', RESULTS_STORAGE_ROOT=str(directory/'files')))
    engine = app.extensions['db_engine']
    # Apply the actual review SQL in this owned DB only, after prerequisite tables.
    Base.metadata.create_all(engine, tables=[t for t in Base.metadata.sorted_tables if t not in shared.TABLES])
    password=secrets.token_urlsafe(24)
    ids={}
    with app.app_context(), transaction() as db:
        db.add_all([AdminGuard(id=1),SchemaVersion(version=1)])
        for login,role in [('manager','admin'),('worker','employee'),('other','admin'),('outsider','employee')]:
            user=User(login_id=login,password_hash=generate_password_hash(password,method='pbkdf2:sha256'),
                employee_no='TEST-'+login,display_name='TEST-'+login,department='TEST',role=role,is_active=True)
            db.add(user);db.flush();ids[login]=user.id
        db.add(results.DepartmentReviewer(department='TEST',reviewer_id=ids['manager']))
        for number in (100,200,300,500,600):
            db.add(TaskRecord(task_id=number,title='TEST shared '+str(number),description='TEST isolated',
                task_status='in_progress',start_date=date.today()-timedelta(days=2),due_date=date.today()+timedelta(days=3),
                current_assignment_revision=1,row_version=1,created_by=ids['manager'],created_at=utcnow(),updated_at=utcnow()))
            db.flush()
            db.add(TaskAssignment(task_id=number,revision=1,assignee_id=ids['worker'],assigned_by=ids['manager'],
                assigned_at=utcnow()-timedelta(days=1),accepted_at=utcnow()-timedelta(hours=1)))
            db.add(TaskManager(task_id=number,user_id=ids['other'] if number==300 else ids['manager']))

    snapshot_gates = {}

    @app.before_request
    def wait_after_auth_snapshot():
        # Test-only scheduling after real load_identity, before the shared guard.
        label = request.headers.get('X-Test-Snapshot')
        if label not in snapshot_gates:
            return
        gate = snapshot_gates[label]
        db = get_db()
        query = select(shared.SharedLink).where(shared.SharedLink.attachment_id == gate['file'])
        gate['cached'] = list(db.scalars(query))
        gate['before'] = sorted(link.task_id for link in gate['cached'])
        gate['ready'].set()
        if not gate['resume'].wait(8):
            raise AppError(503,'TEST_GATE_TIMEOUT','TEST snapshot scheduling failed')
        gate['after'] = sorted(link.task_id for link in db.scalars(query))

    @app.post('/api/v1/_shared_test/task/<int:task_id>/<operation>')
    @login_required
    def consumer(task_id,operation):
        body=payload()
        with authenticated_transaction() as db:
            expected=ExpectedVersions.from_body(body)
            if operation=='replace':
                files=shared.replace_task_files(db,task_id,body['attachment_ids'],expected=expected)
            elif operation=='release':
                shared.release_task_files(db,task_id,expected=expected);files=[]
            elif operation=='cancel':
                shared.release_task_files(db,task_id,expected=expected)
                db.add(TaskCancellation(task_id=task_id,cancelled_by=g.user.id,reason='TEST isolated cancellation',cancelled_at=utcnow()))
                files=[]
            else: raise AppError(400,'TEST_BAD_OPERATION','TEST only')
            if body.get('rollback'):raise AppError(409,'TEST_ROLLBACK','TEST intentional rollback')
            return success({'items':files})

    @app.post('/api/v1/_shared_test/create')
    @login_required
    def create_consumer():
        body=payload()
        with authenticated_transaction(roles=('admin',)) as db:
            number=body['task_id']
            db.add(TaskRecord(task_id=number,title='TEST atomic creation',description='TEST only',task_status='not_started',
                start_date=date.today(),due_date=date.today()+timedelta(days=2),current_assignment_revision=1,
                row_version=1,created_by=g.user.id,created_at=utcnow(),updated_at=utcnow()))
            db.flush()
            db.add(TaskAssignment(task_id=number,revision=1,assignee_id=ids['worker'],assigned_by=g.user.id,assigned_at=utcnow()))
            db.add(TaskManager(task_id=number,user_id=g.user.id));db.flush()
            shared.replace_task_files(db,number,body['attachment_ids'],expected=ExpectedVersions(1,1))
            if body.get('rollback'):raise AppError(409,'TEST_ROLLBACK','TEST intentional rollback')
        return success({'task_id':str(number)},201)

    server=create_server(app,host='127.0.0.1',port=0,threads=6)
    thread=threading.Thread(target=server.run,daemon=True);thread.start()
    base='http://127.0.0.1:'+str(server.effective_port)
    tokens={}
    def call(who,path,body=None,method=None,status=200,key=None,snapshot=None):
        headers={} if who is None else {'Authorization':'Bearer '+tokens[who]}
        if snapshot:headers['X-Test-Snapshot']=snapshot
        if key:headers['Idempotency-Key']=key
        data=None
        if body is not None:data=json.dumps(body).encode();headers['Content-Type']='application/json'
        req=Request(base+path,data=data,headers=headers,method=method)
        try:
            with urlopen(req,timeout=10) as r:actual=r.status;raw=r.read();mime=r.headers.get('Content-Type','')
        except HTTPError as r:actual=r.code;raw=r.read();mime=r.headers.get('Content-Type','')
        check(actual==status,path+' status '+str(actual))
        value=json.loads(raw) if 'json' in mime else raw
        return value.get('data') if isinstance(value,dict) else value
    def api(who,path,body=None,**kwargs):return call(who,'/api/v1/'+path,body,**kwargs)
    def upload(who,content=b'TEST shared bytes',name='TEST.any'):
        item=api(who,'results/shared/uploads/start',{'name':name,'size':len(content),'sha256':hashlib.sha256(content).hexdigest()},status=201)
        for offset in range(0,len(content),results.CHUNK_BYTES):
            api(who,'results/shared/uploads/'+item['attachment_id']+'/chunks',
                {'offset':offset,'data':base64.b64encode(content[offset:offset+results.CHUNK_BYTES]).decode()})
        api(who,'results/shared/uploads/'+item['attachment_id']+'/complete',{})
        return item['attachment_id']
    def change(who,task,files=None,operation='replace',status=200,rollback=False,version='1',snapshot=None):
        return call(who,f'/api/v1/_shared_test/task/{task}/{operation}',{'expected_row_version':version,
            'expected_assignment_revision':1,'attachment_ids':files or [],'rollback':rollback},status=status,snapshot=snapshot)
    def download(who,file,status=200):return api(who,'results/shared/files/'+file+'/download',status=status)
    def delayed(file, action, concurrent):
        label=secrets.token_hex(8)
        gate={'file':file,'ready':threading.Event(),'resume':threading.Event()}
        snapshot_gates[label]=gate
        try:
            with ThreadPoolExecutor(1) as pool:
                future=pool.submit(action,label)
                try:
                    check(gate['ready'].wait(8),'real authenticated request paused before write guard')
                    concurrent()
                finally:
                    gate['resume'].set()
                future.result(timeout=12)
            check(gate['before']==gate['after'],'ordinary RR read still uses the pre-commit snapshot')
            return gate['before']
        finally:
            gate['resume'].set()
            snapshot_gates.pop(label,None)
    try:
        for login in ids:
            reply=api(None,'auth/login',{'login_id':login,'password':password})
            tokens[login]=reply['access_token']
        meta={'name':'TEST.any','size':0,'sha256':hashlib.sha256(b'').hexdigest()}
        api('manager','results/shared/uploads/start',meta,status=503)
        sql=(ROOT/'RemoteWork.Server/migrations/013_shared_attachments.mysql.sql').read_text(encoding='utf-8')
        sql='\n'.join(line for line in sql.splitlines() if not line.lstrip().startswith('--'))
        with engine.begin() as conn:
            for statement in sql.split(';'):
                if statement.strip():conn.exec_driver_sql(statement)
        check(True,'actual review SQL applied only to owned DB')
        # R52-03: B commits AFTER A's authenticated RR snapshot, BEFORE A locks.
        race_start=COUNT
        race_bytes=b'TEST R52-03 current reference'
        race_file=upload('manager',race_bytes)
        change('manager',500,[race_file])
        before=delayed(race_file,
            lambda label:change('manager',500,operation='release',snapshot=label),
            lambda:change('manager',600,[race_file]))
        check(before==[500],'R52-03 snapshot originally contains only A')
        with app.app_context():
            db=get_db()
            check(list(db.scalars(select(shared.SharedLink.task_id).where(shared.SharedLink.attachment_id==race_file)))==[600],
                'R52-03 only newly committed B reference remains')
            check(db.get(shared.SharedFile,race_file).state=='ready','R52-03 B reference keeps file ready')
            shared.cleanup_shared_files()
        check(download('worker',race_file)==race_bytes,'R52-03 B download returns original bytes after cleanup')
        check((directory/'files/shared'/race_file).is_file(),'R52-03 physical bytes retained')
        change('manager',600,operation='release')
        check(not (directory/'files/shared'/race_file).exists(),'R52-03 last reference still cleans bytes')
        # Both task mutation queries must see a replacement committed after auth.
        for operation in ('release','replace'):
            for rollback in (False,True):
                old=upload('manager',b'TEST snapshot old')
                new=upload('manager',b'TEST snapshot new')
                change('manager',500,[old]);change('manager',600,[old])
                delayed(old,
                    lambda label:change('manager',500,operation=operation,rollback=rollback,
                        status=409 if rollback else 200,snapshot=label),
                    lambda:change('manager',500,[new]))
                current=api('manager','results/shared/tasks/500')['items']
                check([item['attachment_id'] for item in current]==([new] if rollback else []),
                    operation+' sees current replacement and preserves TX rollback')
                check(download('worker',old)==b'TEST snapshot old','old B reference survives current mutation')
                if rollback:
                    check(download('worker',new)==b'TEST snapshot new','rollback retains replacement bytes')
                    change('manager',500,operation='release')
                check(not (directory/'files/shared'/new).exists(),'last replacement reference cleanup retained')
                change('manager',600,operation='release')
        # A stale "unbound" snapshot cannot authorize deleting a now-bound upload.
        bound=upload('manager',b'TEST binding after snapshot')
        check(delayed(bound,
            lambda label:call('manager','/api/v1/results/shared/uploads/'+bound,method='DELETE',status=403,snapshot=label),
            lambda:change('manager',500,[bound]))==[], 'upload snapshot initially had no references')
        check(download('worker',bound)==b'TEST binding after snapshot','current binding prevents uploader deletion')
        change('manager',500,operation='release')
        print(f'PASS {COUNT-race_start} R52-03/current-reference regression checks against owned MySQL/HTTP.')
        api('manager','results/shared/uploads/start',{**meta,'size':results.MAX_FILE_BYTES+1},status=413)
        api('manager','results/shared/uploads/start',{**meta,'name':'../bad'},status=400)
        api('manager','results/shared/uploads/start',{**meta,'sha256':'bad'},status=400)
        api('worker','results/shared/uploads/start',meta,status=403)
        content=b'TEST shared A B\0'*1600
        file=upload('manager',content)
        download('manager',file,status=404)
        change('other',100,[file],status=403)
        change('manager',100,[file],version='2',status=409)
        change('manager',100,[file,file],status=400)
        change('manager',100,[file]*11,status=400)
        change('manager',100,[file])
        check(download('manager',file)==content,'manager download bytes')
        check(download('worker',file)==content,'current employee download bytes')
        download('outsider',file,status=403)
        # File ID possession and the uploader label are not cross-task permission.
        change('other',300,[file],status=403)
        reviewer_file=upload('other',b'TEST reviewer-only access')
        change('other',300,[reviewer_file])
        check(download('manager',reviewer_file)==b'TEST reviewer-only access','designated reviewer reads without task-manager membership')
        change('manager',300,[reviewer_file],status=403)
        change('manager',200,[file])
        change('manager',100,operation='release')
        check(download('worker',file)==content,'B survives A reference removal')
        check((directory/'files/shared'/file).is_file(),'B physical file retained')
        replacement=upload('manager',b'TEST replacement')
        change('manager',200,[replacement],rollback=True,status=409)
        check(download('worker',file)==content,'rollback preserves old reference and bytes')
        # Keep a submission reference before replacing/removing the task attachment.
        change('manager',100,[file])
        ctx=api('worker','results/tasks/100/context')
        key=api('worker','write-requests/keys',{'action':'submission.submitted','target':'100'},status=201)['request_key']
        body={'expected_row_version':ctx['row_version'],'expected_assignment_revision':1,'description':'TEST shared first round','attachment_ids':[file]}
        first=api('worker','results/tasks/100/submissions',body,status=201,key=key)
        again=api('worker','results/tasks/100/submissions',body,status=201,key=key)
        check(first==again,'existing receipt replay does not duplicate shared reference')
        listed=api('worker','results/tasks/100/submissions')['items']
        check(listed[0]['attachments'][0]['attachment_id']==file and listed[0]['round_no']=='1','first round contains shared file')
        check(api('worker','results/attachments/'+file+'/download')==content,'legacy results download accepts shared reference')
        chunk=api('worker','results/attachments/'+file+'/chunks?offset=0')
        check(base64.b64decode(chunk['data'])==content[:results.CHUNK_BYTES],'legacy chunk bytes reused')
        judged=api('manager',f"results/submissions/{first['submission_id']}/context")
        decision_key=api('manager','write-requests/keys',{'action':'submission.returned','target':first['submission_id']},status=201)['request_key']
        returned=api('manager',f"results/submissions/{first['submission_id']}/decisions",{
            'expected_row_version':judged['row_version'],'expected_assignment_revision':1,'outcome':'returned','reason':'TEST improve'},key=decision_key)
        rework_key=api('worker','write-requests/keys',{'action':'task.rework_started','target':'100'},status=201)['request_key']
        progressed=api('worker','tasks/100/rework-start',{'expected_row_version':returned['row_version'],'expected_assignment_revision':1},key=rework_key)
        second_key=api('worker','write-requests/keys',{'action':'submission.submitted','target':'100'},status=201)['request_key']
        second=api('worker','results/tasks/100/submissions',{**body,'expected_row_version':progressed['row_version'],
            'description':'TEST second shared round'},status=201,key=second_key)
        history=api('worker','results/tasks/100/submissions')['items']
        check([x['round_no'] for x in history]==['2','1'],'newest round first; first round retained')
        check(all(x['attachments'][0]['attachment_id']==file for x in history),'same file retains independent first and second submission links')
        current_version=second['row_version']
        change('manager',100,operation='release',version=current_version)
        change('manager',200,[replacement])
        check(download('worker',file)==content,'old submission attachment survives task references removal')
        # Submission links cannot be edited by an external request.
        with app.test_request_context():
            with app.app_context():
                g.user=get_db().get(User,ids['worker']);g.write_transaction_active=True
                try:shared.attach_new_submission(get_db(),get_db().get(results.Submission,int(first['submission_id'])),[replacement])
                except AppError as error:check(error.code=='SUBMISSION_IMMUTABLE','old submitted attachments immutable')
                else:raise AssertionError('old submission was mutable')
        change('manager',200,operation='release')
        download('worker',replacement,status=404)
        check(not (directory/'files/shared'/replacement).exists(),'last task reference removal deletes physical file')
        temporary=upload('manager',b'TEST temporary')
        api('other','results/shared/uploads/'+temporary,method='DELETE',status=403)
        api('manager','results/shared/uploads/'+temporary,method='DELETE')
        check(not (directory/'files/shared'/temporary).exists(),'explicit registration cancellation cleans unused upload')
        draft=upload('manager',b'TEST atomic registration')
        call('manager','/api/v1/_shared_test/create',{'task_id':400,'attachment_ids':[draft],'rollback':True},status=409)
        with app.app_context():check(get_db().get(TaskRecord,400) is None,'failed task creation rolls back task and references')
        call('manager','/api/v1/_shared_test/create',{'task_id':400,'attachment_ids':[draft]},status=201)
        api('manager','results/shared/uploads/'+draft,method='DELETE',status=403)
        check(download('worker',draft)==b'TEST atomic registration','registration and file confirmation same TX')
        with app.app_context(),transaction() as db:
            db.add(TaskAssignment(task_id=400,revision=2,assignee_id=ids['outsider'],assigned_by=ids['manager'],assigned_at=utcnow()))
            db.flush();task=db.get(TaskRecord,400);task.current_assignment_revision=2;task.row_version=2
            db.delete(db.get(TaskManager,(400,ids['manager'])));db.add(TaskManager(task_id=400,user_id=ids['other']))
        download('worker',draft,status=403)
        # The old manager is also the department reviewer: remove that independent role too.
        with app.app_context(),transaction() as db:db.get(results.DepartmentReviewer,'TEST').reviewer_id=ids['other']
        download('manager',draft,status=403)
        check(download('outsider',draft)==b'TEST atomic registration','new assignee receives file access')
        check(download('other',draft)==b'TEST atomic registration','new manager receives file access')
        retry_file=upload('manager',b'TEST retry deletion');change('manager',200,[retry_file])
        original_unlink=pathlib.Path.unlink
        def busy_file(path,*args,**kwargs):
            if path.name==retry_file:raise PermissionError('TEST owned file busy')
            return original_unlink(path,*args,**kwargs)
        with patch.object(pathlib.Path,'unlink',busy_file):
            change('manager',200,operation='release')
        with app.app_context():
            check(get_db().get(shared.SharedFile,retry_file).state=='deleting','failed physical delete remains retryable')
        change('manager',200,[retry_file],status=404)
        with app.app_context():check(shared.cleanup_shared_files()>=1,'retry removes failed deletion')
        check(not (directory/'files/shared'/retry_file).exists(),'retry file bytes removed')
        corrupt=api('manager','results/shared/uploads/start',{**meta,'size':1,'sha256':'0'*64},status=201)['attachment_id']
        api('manager',f'results/shared/uploads/{corrupt}/chunks',{'offset':0,'data':base64.b64encode(b'x').decode()})
        api('manager',f'results/shared/uploads/{corrupt}/complete',{},status=409)
        change('manager',200,[corrupt],status=409)
        api('manager',f'results/shared/uploads/{corrupt}',method='DELETE')
        empty=upload('manager',b'')
        with app.app_context(),transaction() as db:db.get(shared.SharedFile,empty).expires_at=utcnow()-timedelta(days=1)
        with app.app_context():check(shared.cleanup_shared_files()>=1,'expired unreferenced upload cleaned')
        # A/B release and B read are independent requests against the actual HTTP server.
        race=upload('manager',b'TEST race');change('manager',200,[race]);change('manager',100,[race],version=current_version)
        barrier=threading.Barrier(2)
        def release_a():barrier.wait();return change('manager',100,operation='release',version=current_version)
        def read_b():barrier.wait();return download('worker',race)
        with ThreadPoolExecutor(2) as pool:
            a=pool.submit(release_a);b=pool.submit(read_b);a.result();check(b.result()==b'TEST race','concurrent A release preserves B download')
        check(download('worker',race)==b'TEST race','B still reads after concurrent release')
        # Existing-task uploads recheck cancellation before and after file IO.
        changing=api('worker','results/shared/uploads/start',{**meta,'name':'TEST mid-cancel','size':32768,
            'sha256':hashlib.sha256(b'x'*32768).hexdigest(),'task_id':'200'},status=201)['attachment_id']
        part={'offset':0,'data':base64.b64encode(b'x'*16384).decode()}
        api('worker',f'results/shared/uploads/{changing}/chunks',part)
        change('manager',200,operation='cancel')
        api('worker',f'results/shared/uploads/{changing}/chunks',{**part,'offset':16384},status=409)
        api('worker',f'results/shared/uploads/{changing}/complete',{},status=409)
        api('manager','results/shared/tasks/200',status=409)
        change('manager',200,[file],status=409)
        change('manager',100,operation='cancel',version=current_version)
        check(download('worker',file)==content,'cancelled task retains historical submitted files')
        with app.app_context(),transaction() as db:db.get(User,ids['manager']).role='employee'
        download('manager',file,status=403)
        with app.app_context(),transaction() as db:db.get(User,ids['manager']).role='admin'
        with app.app_context():check(get_db().scalar(select(func.count()).select_from(SchemaVersion))==1,'common schema version unchanged')
        print(f'PASS {COUNT} shared attachment MySQL + real loopback HTTP checks. Task consumer routes are explicit test fixtures, not member2 product integration.')
    finally:
        server.close();thread.join(timeout=5);engine.dispose()


if __name__=='__main__':
    tempfile.tempdir='C:/Windows/Temp'
    clean={k:v for k,v in os.environ.items() if not k.startswith('MYSQL_') and k!='DATABASE_URL'}
    with owned.owned_runtime() as runtime:
        with owned.OwnedMySQL(pathlib.Path(os.environ.get('RESULTS_MYSQLD','C:/Program Files/MySQL/MySQL Server 8.2/bin/mysqld.exe')),runtime,clean) as database:
            run(database.url,runtime)
    print('Owned shared-file HTTP/MySQL/files cleaned; user DB unchanged.')
