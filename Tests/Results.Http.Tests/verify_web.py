"""Real cookie/CSRF result flow in a separate owned MySQL instance only."""
import sys, os, json, re, pathlib, tempfile, hashlib, base64
from datetime import date, timedelta

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT))
sys.path.insert(0,str(ROOT.parent/'TaskQueries.Http.Tests'))
import verify_server as regression
import run_mysql_http_check as owned
from sqlalchemy import select, func
from remotework.db import transaction, get_db
from remotework.models import User, utcnow, AuthSession, AuditEvent
from remotework.features import results as results
from remotework.features.tasks.models import TaskRecord, TaskAssignment, TaskManager

COUNT = 0


def check(value, name):
    global COUNT
    if not value: raise AssertionError(name)
    COUNT += 1


def run_web(app, password, storage):
    ids = {}
    with app.app_context(), transaction() as db:
        for user in db.scalars(select(User)):
            ids[user.login_id] = user.id
        for n, owner in ((100, 'worker'),(101, 'manager'),(102, 'worker'),(103, 'worker')):
            task = TaskRecord(task_id=n,title=f'TEST 웹 업무 {n}',description='TEST isolated browser flow',
                task_status='in_progress',start_date=date.today()-timedelta(days=2),
                due_date=date.today()+timedelta(days=4),current_assignment_revision=1,
                row_version=1,created_by=ids['manager'],created_at=utcnow(),updated_at=utcnow())
            db.add(task); db.flush()
            db.add(TaskAssignment(task_id=n,revision=1,assignee_id=ids[owner],assigned_by=ids['manager'],
                assigned_at=utcnow()-timedelta(days=1),accepted_at=utcnow()-timedelta(seconds=30)))
            db.add(TaskManager(task_id=n,user_id=ids['manager']))
    clients = {}; headers = {}; csrf = {}
    for login in ('worker','manager','upper','outsider'):
        client = app.test_client()
        client.get('/login')
        with client.session_transaction() as session: first = session['csrf_token']
        reply = client.post('/login',data={'csrf_token':first,'login_id':login,'password':password})
        check(reply.status_code==302,'actual cookie login '+login)
        page = client.get('/results')
        html = page.get_data(as_text=True)
        check(page.status_code==200 and 'results-page' in html,'real result template '+login)
        token = re.search('data-session="([a-f0-9]+)"',html).group(1)
        headers[login] = {'X-Results-Session':token}
        with client.session_transaction() as session: csrf[login]=session['csrf_token']
        clients[login]=client

    def call(login, path, body=None, key=None, status=200):
        header={**headers[login]}
        if key: header['Idempotency-Key']=key
        if body is None: reply=clients[login].get('/results/data/'+path,headers=header)
        else: reply=clients[login].post('/results/data/'+path,data={'csrf_token':csrf[login],'body':json.dumps(body)},headers=header)
        check(reply.status_code==status,f'{login} {path}: expected {status}, got {reply.status_code}')
        if status<300: return reply.json['data']
        check(reply.json['ok'] is False,'common-shaped web error '+path)
        return reply.json

    def action(login,op,target,body,key=None,status=200): return call(login,f'actions/{op}/{target}',body,key,status)
    def key(login,action_name,target): return action(login,'key',target,{'action':action_name},status=201)['request_key']
    def submit(login,target,description,attachments=()):
        context=call(login,f'tasks/{target}/context')
        body={'expected_row_version':context['row_version'],'expected_assignment_revision':context['assignment_revision'],
              'description':description,'attachment_ids':list(attachments)}
        request_key=key(login,'submission.submitted',target)
        saved=action(login,'submit',target,body,request_key,201)
        replay=action(login,'submit',target,body,request_key,201)
        check(replay==saved,'web same-key replay')
        receipt=action(login,'lookup',target,{'action':'submission.submitted'},request_key)
        check(receipt['state']=='completed' and receipt['result']==saved,'web receipt recovery')
        return saved
    def decide(login,item,outcome,reason):
        context=call(login,f'submissions/{item["submission_id"]}/context')
        body={'expected_row_version':context['row_version'],'expected_assignment_revision':context['assignment_revision'],
              'outcome':outcome,'reason':reason}
        request_key=key(login,'submission.'+outcome,item['submission_id'])
        return action(login,'decide',item['submission_id'],body,request_key)

    check(clients['worker'].get('/features/results').location.endswith('/results'),'existing result menu route')
    check(clients['manager'].get('/results/history').status_code==200,'admin history template')
    check(clients['worker'].get('/results/history').status_code==403,'staff history denied')
    check(clients['worker'].get('/api/v1/results/history-tasks').status_code==401,'web cookie not API Bearer')
    reply=clients['worker'].post('/results/data/actions/key/100',data={'body':'{}'},headers=headers['worker'])
    check(reply.status_code==400 and reply.json['error']['code']=='CSRF_FAILED','common CSRF retained')
    check(clients['worker'].get('/results/data/tasks',headers={'X-Results-Session':'wrong'}).status_code==409,'old page denied')
    call('worker','tasks?scope=review',status=403)
    for wrong in ([],None,{'action':[]},{'action':'task.created'},{'action':'submission.submitted','extra':1}):
        # A list/null is invalid input rather than another business contract.
        reply=clients['worker'].post('/results/data/actions/key/100',data={'csrf_token':csrf['worker'],'body':json.dumps(wrong)},headers=headers['worker'])
        check(reply.status_code==400,'invalid key body blocked')
    reply=clients['worker'].post('/results/data/actions/key/100',data={'csrf_token':csrf['worker'],'body':'{"action":"submission.submitted","action":"submission.approved"}'},headers=headers['worker'])
    check(reply.status_code==400,'duplicate JSON rejected')
    call('outsider','tasks/100/submissions',status=403)
    call('worker','tasks/101/submissions',status=403)
    own=call('worker','tasks?scope=own')['items']; check(any(t['task_id']=='100' and t['round_no'] is None for t in own),'actual own list empty round')
    reviewed=call('manager','tasks?scope=review')['items']; check(any(t['task_id']=='100' for t in reviewed),'explicit reviewer list')

    content=b'WEB actual bytes\x00\xff\n'+('한글<script>alert(1)</script>').encode()
    digest=hashlib.sha256(content).hexdigest()
    upload=action('worker','upload-start','100',{'name':'TEST 자료.any','size':len(content),'sha256':digest},status=201)
    action('worker','upload-chunk',upload['attachment_id'],{'offset':0,'data':base64.b64encode(content).decode()})
    completed=action('worker','upload-complete',upload['attachment_id'],{})
    check(completed['sha256']==digest,'web actual upload hash')
    first=submit('worker','100','TEST <img src=x onerror=alert(1)> 첫 결과',[upload['attachment_id']])
    check(first['round_no']=='1','first success round one')
    context=call('manager',f'submissions/{first["submission_id"]}/context')
    blank={'expected_row_version':context['row_version'],'expected_assignment_revision':context['assignment_revision'],'outcome':'returned','reason':'   '}
    action('manager','decide',first['submission_id'],blank,key('manager','submission.returned',first['submission_id']),400)
    decide('manager',first,'returned','TEST 웹 반려: 누락 보완')
    call('worker','tasks/100/context',status=409)
    # Member 2 owns rework. Exercise its existing API; don't implement another web transition.
    bearer=app.test_client().post('/api/v1/auth/login',json={'login_id':'worker','password':password}).json['data']['access_token']
    api_client=app.test_client(); api_header={'Authorization':'Bearer '+bearer}
    def rework():
        with app.app_context(): current=get_db().get(TaskRecord,100); version=str(current.row_version)
        issued=api_client.post('/api/v1/write-requests/keys',json={'action':'task.rework_started','target':'100'},headers=api_header)
        check(issued.status_code==201,'existing member2 rework key')
        reply=api_client.post('/api/v1/tasks/100/rework-start',json={'expected_row_version':version,'expected_assignment_revision':1},
            headers={**api_header,'Idempotency-Key':issued.json['data']['request_key']})
        check(reply.status_code==200,'existing member2 rework unchanged')
    rework()
    second=submit('worker','100','TEST 수정 결과')
    items=call('worker','tasks/100/submissions')['items']
    check([x['round_no'] for x in items]==['2','1'],'previous submissions retained')
    check(items[1]['attachments'][0]['sha256']==digest and items[1]['reason']=='TEST 웹 반려: 누락 보완','old file and rejection retained')
    reply=clients['worker'].get('/results/data/attachments/'+upload['attachment_id']+'/download',headers=headers['worker'])
    check(reply.status_code==200 and reply.data==content,'web actual byte download')
    call('outsider','attachments/'+upload['attachment_id']+'/download',status=403)
    for round_no in range(3,7):
        decide('manager',second,'returned',f'TEST {round_no}차 수정 필요')
        current=call('worker','tasks/100/rework-context')
        request_key=key('worker','task.rework_started','100')
        body={'expected_row_version':current['row_version'],'expected_assignment_revision':current['assignment_revision']}
        action('worker','rework','100',body,request_key)
        check(action('worker','lookup','100',{'action':'task.rework_started'},request_key)['state']=='completed','web rework receipt')
        second=submit('worker','100',f'TEST {round_no}차 결과')
        check(second['round_no']==str(round_no),'unlimited successful resubmit '+str(round_no))
    decide('manager',second,'approved','TEST 웹 승인')
    check(call('worker','tasks/100')['task_status']=='approved','web saved task state')
    call('manager',f'submissions/{second["submission_id"]}/context',status=409)
    own_admin=submit('manager','101','TEST 관리자 본인 제출')
    call('manager',f'submissions/{own_admin["submission_id"]}/context',status=403)
    decide('upper',own_admin,'approved','TEST 상위 승인')
    items=call('manager','tasks/100/activity')['items']
    check(all(x in {item['kind'] for item in items} for x in ('submitted','returned','approved')),'real activity submit/review history')
    call('worker','tasks/100/activity',status=403)
    with app.app_context(),transaction() as db:
        current=db.get(TaskRecord,102);current.due_date=date.today()-timedelta(days=1)
        db.add(results.SubmissionGate(task_id=103,extension_pending=True))
    call('worker','tasks/102/context',status=409);call('worker','tasks/103/context',status=409)
    with app.app_context(),transaction() as db:
        db.get(TaskRecord,102).due_date=date.today()+timedelta(days=3)
        db.get(results.SubmissionGate,103).extension_pending=False
    call('worker','tasks/102/context');call('worker','tasks/103/context')
    zero=action('worker','upload-start','103',{'name':'TEST empty.unknown','size':0,'sha256':hashlib.sha256(b'').hexdigest()},status=201)
    check(action('worker','upload-complete',zero['attachment_id'],{})['size']==0,'zero-byte all-format upload')
    current=call('worker','tasks/103/context')
    lost_key=key('worker','submission.submitted','103')
    lost_body={'expected_row_version':current['row_version'],'expected_assignment_revision':current['assignment_revision'],
        'description':'TEST lost web response','attachment_ids':[zero['attachment_id']]}
    action('worker','submit','103',lost_body,lost_key,502)
    recovered=action('worker','lookup','103',{'action':'submission.submitted'},lost_key)
    check(recovered['state']=='completed' and recovered['result']['round_no']=='1','web lost success recovered')
    action('worker','submit','103',lost_body,lost_key,201)
    check(len(call('worker','tasks/103/submissions')['items'])==1,'web lost-response retry does not increment round')
    with app.app_context(),transaction() as db:
        for n in range(105):
            db.add(AuditEvent(actor_id=ids['manager'],action='task.status_changed',resource_type='task',resource_id='100',
                             details_json=json.dumps({'test_fixture':n}),created_at=utcnow()))
    page=call('manager','tasks/100/activity')
    check(len(page['items'])==100 and page['next_before_event_id'],'actual cursor page is not whole history')
    older=call('manager','tasks/100/activity?before_event_id='+page['next_before_event_id'])
    check(len(older['items'])>0 and not ({x['event_id'] for x in page['items']} & {x['event_id'] for x in older['items']}),'cursor older page without duplicates')
    # A new same-account login rotates CSRF and page scope; the old page cannot write.
    client=clients['worker'];old_scope=headers['worker'].copy();old_csrf=csrf['worker']
    client.post('/logout',data={'csrf_token':old_csrf})
    client.get('/login')
    with client.session_transaction() as session: login_csrf=session['csrf_token']
    client.post('/login',data={'csrf_token':login_csrf,'login_id':'worker','password':password})
    check(client.get('/results/data/tasks',headers=old_scope).status_code==409,'same account new session old page denied')
    with client.session_transaction() as session: current_csrf=session['csrf_token']
    check(client.post('/results/data/actions/key/102',headers=old_scope,data={'csrf_token':current_csrf,'body':'{"action":"submission.submitted"}'}).status_code==409,'old session cannot mint key')
    print(f'PASS {COUNT} web checks: real cookie/CSRF/MySQL/shared result writes/files/history; member2 rework API reused separately.')
    if '--preview' in sys.argv:
        from waitress import create_server
        import threading,time
        server=create_server(app,host='127.0.0.1',port=0,threads=4)
        thread=threading.Thread(target=server.run,daemon=True);thread.start()
        preview=ROOT.parents[2]/'web-preview.private.json'
        preview.write_text(json.dumps({'url':'http://127.0.0.1:'+str(server.effective_port),'password':password}),encoding='utf-8')
        print('OWNED WEB PREVIEW READY '+str(server.effective_port),flush=True)
        stop=preview.with_suffix('.stop')
        try:
            until=time.monotonic()+900
            while time.monotonic()<until and not stop.exists(): time.sleep(.3)
        finally:
            server.close();thread.join(timeout=3);preview.unlink(missing_ok=True);stop.unlink(missing_ok=True)


if __name__=='__main__':
    original_create=regression.create_app
    def web_loss_factory(*args,**kwargs):
        from flask import request,jsonify
        app=original_create(*args,**kwargs)
        app.jinja_env.auto_reload=True
        lost=set()
        @app.after_request
        def lose_one_committed_response(response):
            if '/results/data/actions/submit/' in request.path and request.method=='POST' and response.status_code==201:
                description=json.loads(request.form.get('body','{}')).get('description')
                if description in ('TEST lost web response','TEST 브라우저 응답 유실') and description not in lost:
                    lost.add(description)
                    reply=jsonify(ok=False,error={'code':'TEST_RESPONSE_LOST','message':'TEST 응답 유실: 처리 결과를 확인해 주세요.'})
                    reply.status_code=502
                    return reply
            return response
        return app
    regression.create_app=web_loss_factory
    original=regression.run_csharp
    def combined(app,password,storage):
        if '--skip-csharp' not in sys.argv:
            original(app,password,storage)
        run_web(app,password,storage)
    regression.run_csharp=combined
    tempfile.tempdir=r'C:/Windows/Temp'
    clean={k:v for k,v in os.environ.items() if not k.startswith('MYSQL_') and k!='DATABASE_URL'}
    with owned.owned_runtime() as runtime:
        with owned.OwnedMySQL(pathlib.Path(os.environ.get('RESULTS_MYSQLD',r'C:/Program Files/MySQL/MySQL Server 8.2/bin/mysqld.exe')),runtime,clean) as database:
            try: regression.run(database.url,runtime/'files',run_client=True)
            finally:
                if hasattr(regression,'ENGINE_FOR_CLEANUP'):regression.ENGINE_FOR_CLEANUP.dispose()
    print('Owned test MySQL/preview cleaned; user DB unchanged.')
