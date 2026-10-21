"""Actual member 2/3 HTTP+MySQL integration, only in an owned disposable DB.

No TEST_DOUBLE_ACK, product .env, product account or user database is used.
The versioned integration ACK below is installed ONLY in this isolated app
to execute its real services; no shared deployment acknowledgment is installed.
"""
import sys, os, pathlib, tempfile, json, secrets, hashlib, base64, threading
from datetime import timedelta, timezone
from concurrent.futures import ThreadPoolExecutor
from urllib.request import Request, urlopen
from urllib.error import HTTPError
from unittest.mock import patch

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent
sys.path[:0] = [str(ROOT.parents[1] / 'RemoteWork.Server'), str(ROOT.parent / 'TaskQueries.Http.Tests')]
import run_mysql_http_check as owned
from remotework import create_app
from remotework.auth import create_user
from remotework.db import transaction
from remotework.models import Base, User, AdminGuard, SchemaVersion, AuditEvent, utcnow
from remotework.features import results
from remotework.features.tasks.lifecycle_models import TaskCancellation, TaskDeadlineExtension
from remotework.features.tasks.lifecycle_support import INTEGRATED_ACK
from remotework.features.tasks.models import TaskRecord
from remotework.write_requests import WriteReceipt
from sqlalchemy import select, func
from waitress import create_server

COUNT = 0
def check(value, label):
    global COUNT
    if not value: raise AssertionError(label)
    COUNT += 1

def run(database_url, storage):
    app = create_app(dict(TESTING=True, SECRET_KEY=secrets.token_hex(32), DATABASE_URL=database_url,
        ENABLE_TASK_QUERY_DRAFT=True, ENABLE_COMMON_WRITE_DRAFT=True, ENABLE_TASK_CREATE_DRAFT=True,
        ENABLE_TASK_START_DRAFT=True, ENABLE_TASK_MANAGEMENT_DRAFT=True, ENABLE_TASK_LIFECYCLE_DRAFT=True,
        ENABLE_RESULTS_DRAFT=True, RESULTS_CONTEXT_MODE='local-development', RESULTS_STORAGE_ROOT=str(storage)))
    engine = app.extensions['db_engine']
    try:
        excluded = set(results.RESULT_TABLES) | {TaskCancellation.__table__, TaskDeadlineExtension.__table__}
        Base.metadata.create_all(engine, tables=[t for t in Base.metadata.sorted_tables if t not in excluded])
        for filename in ('006_results.mysql.sql', '009_task_lifecycle.mysql.sql'):
            sql = (ROOT.parents[1] / 'RemoteWork.Server/migrations' / filename).read_text(encoding='utf-8')
            sql = '\n'.join(line for line in sql.splitlines() if not line.lstrip().startswith('--'))
            with engine.begin() as db:
                for statement in sql.split(';'):
                    if statement.strip(): db.exec_driver_sql(statement)
        password = secrets.token_urlsafe(24)
        ids = {}
        with app.app_context(), transaction() as db:
            db.add_all([AdminGuard(id=1), SchemaVersion(version=1)])
            for name, role in [('manager','admin'),('worker','employee'),('other','employee'),('upper','admin')]:
                user = create_user(db, dict(login_id=name,password=password,display_name='TEST-'+name,
                    employee_no='TEST-'+name,department='TEST',role=role))
                ids[name] = user.id
            db.add(results.DepartmentReviewer(department='TEST',reviewer_id=ids['manager']))
            db.add(results.UpperReviewer(submitter_id=ids['manager'],reviewer_id=ids['upper']))
        server = create_server(app, host='127.0.0.1', port=0, threads=6)
        thread = threading.Thread(target=server.run,daemon=True);thread.start()
        url = 'http://127.0.0.1:' + str(server.effective_port)
        tokens = {}
        def call(who, path, body=None, key=None, status=200, raw=None, content_type=None, method=None):
            headers = {} if who is None else {'Authorization':'Bearer '+tokens[who]}
            if key: headers['Idempotency-Key'] = key
            data = raw
            if body is not None: data=json.dumps(body).encode();headers['Content-Type']='application/json'
            if content_type: headers['Content-Type']=content_type
            req=Request(url+'/api/v1/'+path,headers=headers,data=data,method=method)
            try:
                with urlopen(req,timeout=10) as response: actual=response.status; value=response.read()
            except HTTPError as response: actual=response.code;value=response.read()
            if path.endswith('/download') and actual==200: result=value
            else: result=json.loads(value)
            check(actual in status if isinstance(status,tuple) else actual==status, f'{path}: expected {status}, received {actual}; code '+str(result.get('error',{}).get('code') if isinstance(result,dict) else 'binary'))
            if isinstance(status,tuple):return actual,result
            return result.get('data') if isinstance(result,dict) and result.get('ok') else result
        for who in ids:
            tokens[who]=call(None,'auth/login',{'login_id':who,'password':password})['access_token']
        def key(who, action, target):
            return call(who,'write-requests/keys',{'action':action,'target':str(target)},status=201)['request_key']
        def detail(target): return call('worker','tasks/'+str(target))['task']
        def versions(target):
            row=call('manager',f'tasks/{target}/write-context')['context'];return {'expected_row_version':row['row_version'],'expected_assignment_revision':row['assignment_revision']}
        today=utcnow().replace(tzinfo=timezone.utc).astimezone(timezone(timedelta(hours=9))).date()
        def task(accept=True):
            body={'title':'TEST integration task','description':'TEST preserved','start_date':(today-timedelta(days=1)).isoformat(),
                'due_date':(today+timedelta(days=2)).isoformat(),'assignee_id':ids['worker'],'manager_user_ids':[ids['manager']]}
            target=call('manager','tasks',body,key('manager','task.created','new'),status=201)['task_id']
            if accept:call('worker',f'tasks/{target}/accept',versions(target),key('worker','task.accepted',target))
            return target
        def cancel(target, body=None, request_key=None, status=200):
            return call('manager',f'tasks/{target}/cancel',body or {**versions(target),'reason':'TEST cancel'},
                        request_key or key('manager','task.cancelled',target),status=status)
        def submit(target, attachments=None, status=201, request_key=None, body=None):
            body=body or {**versions(target),'description':'TEST real result','attachment_ids':attachments or []}
            request_key=request_key or key('worker','submission.submitted',target)
            return call('worker',f'results/tasks/{target}/submissions',body,request_key,status=status)
        def legacy(target,status=201):
            boundary='TEST-owned-boundary'
            raw=(f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="TEST.any"\r\nContent-Type: application/octet-stream\r\n\r\nTEST bytes\r\n--{boundary}--\r\n').encode()
            return call('worker',f'results/tasks/{target}/uploads',raw=raw,content_type='multipart/form-data; boundary='+boundary,status=status)
        def start(target):
            return call('worker',f'results/tasks/{target}/uploads/start',{'name':'TEST.any','size':3,'sha256':hashlib.sha256(b'ABC').hexdigest()},status=201)['attachment_id']
        def chunk(attachment,status=200):
            return call('worker',f'results/uploads/{attachment}/chunks',{'offset':0,'data':base64.b64encode(b'ABC').decode()},status=status)
        def complete(attachment,status=200): return call('worker',f'results/uploads/{attachment}/complete',{},status=status)
        def extension(target, suffix='', outcome=None):
            if not suffix:
                return call('worker',f'tasks/{target}/deadline-extensions',{**versions(target),'reason':'TEST extend','requested_due_date':(today+timedelta(days=5)).isoformat()},key('worker','task.deadline_extension_requested',target))
            who='worker' if outcome=='withdrawn' else 'manager'
            body=versions(target)
            if outcome=='rejected':body['reason']='TEST rejected'
            return call(who,f'tasks/{target}/deadline-extensions/{suffix}',body,key(who,'task.deadline_extension_'+outcome,target))
        try:
            # Without the deployment ACK even this full app remains closed.
            target=task();body={**versions(target),'reason':'TEST cancel'}
            call('manager','write-requests/keys',{'action':'task.cancelled','target':target},status=503)
            call('manager',f'tasks/{target}/cancel',body,status=503)
            app.extensions['task_lifecycle_integration_ack']=dict(INTEGRATED_ACK)
            cancel(target)
            check(call('worker',f'results/tasks/{target}/context',status=409)['error']['code']=='TASK_CANCELLED','cancelled context blocked')
            submit(target,status=409);legacy(target,status=409)
            call('worker',f'results/tasks/{target}/uploads/start',{'name':'TEST.any','size':0,'sha256':hashlib.sha256(b'').hexdigest()},status=409)

            # Real pending source overrides even a conflicting local gate.
            target=task()
            with app.app_context(),transaction() as db:db.add(results.SubmissionGate(task_id=int(target),extension_pending=False))
            pending=extension(target)
            check(call('worker',f'results/tasks/{target}/context',status=409)['error']['code']=='DEADLINE_EXTENSION_PENDING','actual pending blocks despite local false')
            submit(target,status=409);legacy(target,status=409)
            approved=extension(target,pending['extension_id']+'/approve','approved')
            context=call('worker',f'results/tasks/{target}/context')
            check(context['due_date']==approved['due_date']==(today+timedelta(days=5)).isoformat(),'real approved due date consumed')
            with app.app_context(),transaction() as db:db.get(results.SubmissionGate,int(target)).extension_pending=True
            check(call('worker',f'results/tasks/{target}/context')['extension_pending'] is False,'local true does not override real resolved request')
            submit(target)

            target=task()
            with app.app_context(),transaction() as db:
                row=db.get(TaskRecord,int(target));row.due_date=today-timedelta(days=1);row.row_version+=1
            check(call('worker',f'results/tasks/{target}/context',status=409)['error']['code']=='SUBMISSION_AFTER_DUE_DATE','actual past deadline blocks direct submit')
            pending=extension(target)
            extension(target,pending['extension_id']+'/approve','approved')
            call('worker',f'results/tasks/{target}/context')
            submit(target)

            # Guard covers every upload stage, including an IO-time cancellation.
            for stage in ('chunk','complete','legacy'):
                target=task();attachment=None
                if stage!='legacy':attachment=start(target)
                if stage=='complete':chunk(attachment)
                fired=[];original_open=pathlib.Path.open
                def intercepted(path,*args,**kwargs):
                    result=original_open(path,*args,**kwargs)
                    mode=args[0] if args else kwargs.get('mode','r')
                    if path.parent==storage and mode=='xb' and not fired:
                        fired.append(True);cancel(target)
                    return result
                with patch.object(pathlib.Path,'open',intercepted):
                    if stage=='chunk':chunk(attachment,status=409)
                    elif stage=='complete':complete(attachment,status=409)
                    else:legacy(target,status=409)
                check(bool(fired),stage+' cancellation actually occurred between IO guards')
                with app.app_context(),transaction() as db:
                    check(db.scalar(select(func.count()).select_from(results.Attachment).where(results.Attachment.task_id==int(target),results.Attachment.state=='ready'))==0,stage+' no ready attachment committed after cancel')
                    check(db.scalar(select(func.count()).select_from(results.Submission).where(results.Submission.task_id==int(target)))==0,stage+' no submission created')

            # New chunks/completion are also blocked when cancellation was earlier.
            target=task();attachment=start(target);cancel(target);chunk(attachment,status=409);complete(attachment,status=409)

            for stage in ('chunk','complete'):
                target=task();attachment=start(target)
                if stage=='complete':chunk(attachment)
                extension(target)
                result=chunk(attachment,status=409) if stage=='chunk' else complete(attachment,status=409)
                check(result['error']['code']=='DEADLINE_EXTENSION_PENDING',stage+' actual pending blocks an existing upload')

            # Past bytes/results and successful receipt replay survive cancellation.
            target=task();file=legacy(target)
            body={**versions(target),'description':'TEST preserved past result','attachment_ids':[file['attachment_id']]}
            submit_key=key('worker','submission.submitted',target)
            saved=submit(target,body=body,request_key=submit_key)
            review_body={**versions(target),'outcome':'returned','reason':'TEST revise'}
            decision_key=key('manager','submission.returned',saved['submission_id'])
            call('manager',f'results/submissions/{saved["submission_id"]}/decisions',review_body,decision_key)
            cancel(target)
            check(call('worker',f'results/tasks/{target}/submissions')['items'][0]['description']==body['description'],'cancel preserves previous result read')
            check(call('worker',f'results/attachments/{file["attachment_id"]}/download')==b'TEST bytes','cancel preserves permitted historical download')
            replay=submit(target,body=body,request_key=submit_key)
            check(replay['submission_id']==saved['submission_id'],'cancel does not invalidate existing successful submit receipt')
            call('manager',f'results/submissions/{saved["submission_id"]}/decisions',review_body,decision_key)
            call('manager',f'results/submissions/{saved["submission_id"]}/context',status=409)
            call('manager',f'results/submissions/{saved["submission_id"]}/decisions',{**versions(target),'outcome':'approved','reason':''},key('manager','submission.approved',saved['submission_id']),status=409)
            call('other',f'results/attachments/{file["attachment_id"]}/download',status=403)

            # Persisted cancellation remains authoritative after routes are disabled.
            app.config['ENABLE_TASK_LIFECYCLE_DRAFT']=False
            submit(target,status=409)
            submit(target,body=body,request_key=submit_key)
            call('worker',f'results/tasks/{target}/submissions')
            app.config['ENABLE_TASK_LIFECYCLE_DRAFT']=True

            # Real simultaneous writes share the same guard/version, not two stores.
            for operation in ('cancel','extension'):
                target=task();v=versions(target);barrier=threading.Barrier(2)
                submit_body={**v,'description':'TEST race','attachment_ids':[]}
                k1=key('worker','submission.submitted',target)
                action='task.cancelled' if operation=='cancel' else 'task.deadline_extension_requested'
                k2=key('manager' if operation=='cancel' else 'worker',action,target)
                def post_submit():
                    barrier.wait()
                    return call('worker',f'results/tasks/{target}/submissions',submit_body,k1,status=(201,409))
                def post_other():
                    barrier.wait();who='manager' if operation=='cancel' else 'worker'
                    suffix='cancel' if operation=='cancel' else 'deadline-extensions'
                    body={**v,'reason':'TEST race'}
                    if operation=='extension':body['requested_due_date']=(today+timedelta(days=5)).isoformat()
                    return call(who,f'tasks/{target}/{suffix}',body,k2,status=(200,409))
                with ThreadPoolExecutor(max_workers=2) as pool:
                    a=pool.submit(post_submit);b=pool.submit(post_other);one,two=a.result(),b.result()
                check((one[0]==201)!=(two[0]==200),operation+' exactly one concurrent write committed')
                loser=one[1] if one[0]==409 else two[1]
                check(loser['error']['code'] in {'TASK_VERSION_CONFLICT','TASK_CANCELLED','TASK_STATE_CONFLICT','DEADLINE_EXTENSION_PENDING'},operation+' losing response is an explicit business conflict')
                with app.app_context(),transaction() as db:
                    submissions=db.scalar(select(func.count()).select_from(results.Submission).where(results.Submission.task_id==int(target)))
                    other_count=db.scalar(select(func.count()).select_from(TaskCancellation if operation=='cancel' else TaskDeadlineExtension).where((TaskCancellation if operation=='cancel' else TaskDeadlineExtension).task_id==int(target)))
                    check(submissions+other_count==1,operation+' DB effects are mutually exclusive')

            # Actual new actions produce readable labels without a second event store.
            target=task()
            for action,suffix,outcome in [('requested','',None),('withdrawn','withdraw','withdrawn'),('requested','',None),('rejected','reject','rejected'),('requested','',None),('approved','approve','approved')]:
                if not suffix:pending=extension(target)
                else:extension(target,pending['extension_id']+'/'+suffix,outcome)
            cancel(target)
            items=call('manager',f'results/tasks/{target}/activity')['items']
            kinds={x['kind'] for x in items}
            check({'cancelled','deadline_extension_requested','deadline_extension_approved','deadline_extension_rejected','deadline_extension_withdrawn'}<=kinds,'actual lifecycle actions have translated history kinds')
            check(any('희망 마감일' in x['summary'] for x in items),'requested due date shown in history')
            check(any('마감일:' in x['summary'] and '→' in x['summary'] for x in items),'approved before/after due date shown')

            target=task(accept=False)
            call('manager',f'tasks/{target}',{**versions(target),'title':'TEST edited title','description':'TEST changed',
                'start_date':(today-timedelta(days=2)).isoformat(),'due_date':(today+timedelta(days=3)).isoformat()},
                key('manager','task.updated',target),method='PATCH')
            call('manager',f'tasks/{target}/managers',{**versions(target),'manager_user_ids':[ids['manager'],ids['upper']]},
                key('manager','task.managers_changed',target),method='PATCH')
            call('manager',f'tasks/{target}/reassign',{**versions(target),'assignee_id':ids['other']},key('manager','task.reassigned',target))
            items=call('manager',f'results/tasks/{target}/activity')['items']
            check({'updated','reassigned','managers_changed'}<={x['kind'] for x in items},'all three actual management action kinds translated')
            check(any('변경 항목' in x['summary'] and '마감일:' in x['summary'] for x in items),'actual update fields/dates displayed')
            check(any('관리자 ID:' in x['summary'] and '→' in x['summary'] for x in items),'actual manager before/after IDs displayed')
            check(any('담당자 ID:' in x['summary'] and '→' in x['summary'] for x in items),'actual assignment before/after IDs displayed')

            target=task()
            with app.app_context(),transaction() as db:
                before_receipts=db.scalar(select(func.count()).select_from(WriteReceipt))
            def fail_result_event(*args):raise RuntimeError('TEST rollback after domain writes')
            with patch.object(results,'_event',fail_result_event):submit(target,status=500)
            with app.app_context(),transaction() as db:
                check(db.get(TaskRecord,int(target)).task_status=='in_progress','result failure rolls back task state')
                check(db.scalar(select(func.count()).select_from(results.Submission).where(results.Submission.task_id==int(target)))==0,'result failure rolls back submission')
                check(db.scalar(select(func.count()).select_from(WriteReceipt))==before_receipts,'result failure rolls back receipt')

            # Partial schema is an unavailable source, never interpreted as no pending.
            target=task()
            with engine.begin() as db:db.exec_driver_sql('RENAME TABLE task_deadline_extensions TO TEST_hidden_extensions')
            try:call('worker',f'results/tasks/{target}/context',status=503)
            finally:
                with engine.begin() as db:db.exec_driver_sql('RENAME TABLE TEST_hidden_extensions TO task_deadline_extensions')
            check('task_lifecycle_integration_ack' in app.extensions,'ACK installed only in this isolated app')
            print(f'PASS {COUNT} real member2/member3 MySQL+loopback HTTP checks; no deployment ACK installed outside this test.')
        finally:
            server.close();thread.join(timeout=3)
    finally:engine.dispose()

if __name__=='__main__':
    tempfile.tempdir=r'C:/Windows/Temp'
    clean={k:v for k,v in os.environ.items() if not k.startswith('MYSQL_') and k!='DATABASE_URL'}
    with owned.owned_runtime() as runtime:
        with owned.OwnedMySQL(pathlib.Path(os.environ.get('RESULTS_MYSQLD',r'C:/Program Files/MySQL/MySQL Server 8.2/bin/mysqld.exe')),runtime,clean) as database:
            run(database.url,runtime/'files')
    print('Owned integration HTTP/MySQL/files cleaned; user DB unchanged.')
