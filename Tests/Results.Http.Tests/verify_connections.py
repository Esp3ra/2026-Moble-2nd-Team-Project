"""Member 3 T3-1..5 checks in a new owned MySQL only.

The /_connections_test consumer is a TEST fixture, not member 2 product code.
Cookies/CSRF, provider callbacks and production handlers are used unchanged.
"""
import base64
from datetime import date, timedelta
import hashlib
import html
import json
import os
from pathlib import Path
import re
import secrets
import sys
import tempfile
from unittest.mock import patch
from urllib.parse import parse_qs, urlsplit

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
sys.path[:0] = [str(ROOT / 'RemoteWork.Server'), str(ROOT / 'Tests/TaskQueries.Http.Tests')]
import run_mysql_http_check as owned
from flask import g, request, current_app
from sqlalchemy import select, func
from werkzeug.security import generate_password_hash
from remotework import create_app
from remotework.auth import authenticated_transaction, login_required
from remotework.common import AppError, success
from remotework.db import get_db, transaction
from remotework.models import Base, User, AuthSession, AdminGuard, SchemaVersion, AuditEvent, utcnow
from remotework.write_contract import ExpectedVersions
from remotework.write_requests import register_write_action, issue_key, perform_once, WriteResult
from remotework.features import results, results_web as web, shared_attachments as shared
from remotework.features.tasks.models import TaskRecord, TaskAssignment, TaskManager
from remotework.features.tasks.lifecycle_models import TaskCancellation

COUNT = 0


def check(value, label):
    global COUNT
    if not value:
        raise AssertionError(label)
    COUNT += 1


def run(url, runtime):
    app = create_app(dict(TESTING=True, SECRET_KEY=secrets.token_hex(32), DATABASE_URL=url,
        ENABLE_TASK_QUERY_DRAFT=True, ENABLE_COMMON_WRITE_DRAFT=True,
        ENABLE_TASK_CREATE_DRAFT=True, ENABLE_TASK_START_DRAFT=True,
        ENABLE_RESULTS_DRAFT=True, ENABLE_SHARED_ATTACHMENTS_DRAFT=True,
        RESULTS_CONTEXT_MODE='local-development', RESULTS_STORAGE_ROOT=str(runtime / 'files'), COOKIE_SECURE=False))
    engine = app.extensions['db_engine']
    Base.metadata.create_all(engine)
    password = secrets.token_urlsafe(24)
    ids = {}
    with app.app_context(), transaction() as db:
        db.add_all([AdminGuard(id=1), SchemaVersion(version=1)])
        for name, role in [('manager','admin'), ('reviewer','admin'), ('worker','employee'), ('outsider','employee'), ('other','admin')]:
            user = User(login_id=name, password_hash=generate_password_hash(password, method='pbkdf2:sha256'),
                employee_no='TEST-' + name, display_name='TEST-' + name, department='TEST', role=role, is_active=True)
            db.add(user); db.flush(); ids[name] = user.id
        db.add(results.DepartmentReviewer(department='TEST', reviewer_id=ids['reviewer']))
        for number in [100, 200, 300, 9007199254740993]:
            task = TaskRecord(task_id=number, title='TEST task', description='TEST', task_status='in_progress',
                start_date=date.today()-timedelta(days=2), due_date=date.today()+timedelta(days=5),
                row_version=1, current_assignment_revision=1, created_by=ids['manager'], created_at=utcnow(), updated_at=utcnow())
            db.add(task); db.flush()
            db.add(TaskAssignment(task_id=number, revision=1, assignee_id=ids['worker'], assigned_by=ids['manager'],
                assigned_at=utcnow()-timedelta(days=1), accepted_at=utcnow()-timedelta(hours=1)))
            db.add(TaskManager(task_id=number, user_id=ids['manager']))

    def authorize(db, target):
        task, assignment, managers = results._current(db, int(target))
        if g.user.role != 'admin' or g.user.id not in managers:
            raise AppError(403, 'FORBIDDEN', 'TEST current manager required')
        return True

    register_write_action(app, action='test.attachments_changed', authorize=authorize,
        result_fields={'task_id','row_version','assignment_revision'})

    @app.get('/_connections_test/context/<target>')
    @login_required
    def context(target):
        provider = current_app.extensions['results_task_web']
        link = provider['build_url'](target, view=request.args.get('view','read'), scope=request.args.get('scope','managed'),
            status=request.args.get('status'), include_cancelled=request.args.get('include_cancelled') == 'true')
        return success(dict(url=link, session=provider['session_scope']()))

    @app.post('/_connections_test/keys/<target>')
    @login_required
    def keys(target):
        with authenticated_transaction() as db:
            authorize(db, target)
            data = issue_key(action='test.attachments_changed', target=target)
        return success(data, 201)

    @app.get('/_connections_test/ids/<target>')
    @login_required
    def read_ids(target):
        with authenticated_transaction() as db:
            values = shared.read_task_attachment_ids(db, target,
                expected=ExpectedVersions(row_version=int(request.args.get('version','1')), assignment_revision=1))
        return success(dict(attachment_ids=list(values)))

    @app.post('/_connections_test/replace/<target>')
    @login_required
    def replace(target):
        body = web.form_body()
        def mutate(db):
            expected = ExpectedVersions(row_version=int(body['expected_row_version']), assignment_revision=1)
            before = shared.read_task_attachment_ids(db, target, expected=expected)
            # This fixture records the proposed minimal audit. Member 2 remains
            # responsible for consuming it in its real update writer.
            shared.selection(db, body['attachment_ids'])
            if set(before) == set(body['attachment_ids']):
                raise AppError(409, 'TASK_CHANGE_NOT_NEEDED', 'TEST no change')
            shared.replace_task_files(db, target, body['attachment_ids'], expected=expected)
            if body.get('rollback'):
                raise AppError(409, 'TEST_ROLLBACK', 'TEST fail after replacement')
            task = db.get(TaskRecord, int(target)); task.row_version += 1
            db.add(AuditEvent(actor_id=g.user.id, action='task.updated', resource_type='task', resource_id=target,
                details_json=json.dumps(dict(before={}, after={}, changed_fields=['attachment_ids'],
                    before_attachment_ids=sorted(before), attachment_ids=sorted(body['attachment_ids'])))))
            return WriteResult(dict(task_id=target, row_version=str(task.row_version), assignment_revision=1))
        with authenticated_transaction() as db:
            result = perform_once(db, key=request.headers.get('Idempotency-Key'), action='test.attachments_changed',
                target=target, body=body, authorize=authorize, mutate=mutate)
        return success(result.data, result.status)

    clients, csrf, scopes = {}, {}, {}
    def login(name):
        client = app.test_client(); client.get('/login')
        with client.session_transaction() as session: token = session['csrf_token']
        reply = client.post('/login', data=dict(csrf_token=token, login_id=name, password=password))
        check(reply.status_code == 302, 'cookie login ' + name)
        page = client.get('/results'); text = page.get_data(as_text=True)
        scopes[name] = re.search(r'data-session="([a-f0-9]+)"', text).group(1)
        with client.session_transaction() as session: csrf[name] = session['csrf_token']
        clients[name] = client
    for name in ids: login(name)

    def call(name, path, body=None, status=200, key=None):
        headers = {'X-Results-Session': scopes[name]}
        if key: headers['Idempotency-Key'] = key
        reply = (clients[name].get(path, headers=headers) if body is None else
                 clients[name].post(path, headers=headers, data={'csrf_token':csrf[name], 'body':json.dumps(body)}))
        check(reply.status_code == status, f'{path}: expected {status}, got {reply.status_code} {reply.get_data().decode("utf-8",errors="replace")[:160] if not reply.is_json else reply.json.get("error",{})}')
        return reply

    base = '/results/data/shared'
    def upload(name='manager', content=b'TEST bytes\x00\xff', task=None):
        metadata = dict(name='TEST.any', size=len(content), sha256=hashlib.sha256(content).hexdigest())
        if task is not None: metadata['task_id'] = task
        item = call(name, base+'/uploads/start', metadata, 201).json['data']
        for offset in range(0, len(content), 16384):
            call(name, base+'/uploads/'+item['attachment_id']+'/chunks', dict(offset=offset,
                data=base64.b64encode(content[offset:offset+16384]).decode()))
        check(call(name, base+'/uploads/'+item['attachment_id']+'/complete', {}).json['data'] == item, 'upload metadata unchanged')
        return item
    def change(target, attachment_ids, version=1, status=200, rollback=False, key=None):
        if key is None: key = call('manager','/_connections_test/keys/'+target, {}, 201).json['data']['request_key']
        body = dict(expected_row_version=str(version), attachment_ids=attachment_ids, rollback=rollback)
        return call('manager','/_connections_test/replace/'+target, body, status, key), key, body

    # Read mode is a real authorized page, without any write UI. A manager is
    # deliberately NOT the actual reviewer in this fixture.
    for name in ['manager','worker','reviewer']:
        page = call(name, '/results?task_id=100&view=read')
        text = page.get_data(as_text=True)
        check('data-view="read"' in text and not any('id="'+field+'"' in text for field in
            ['submit-open','approve-open','return-open','rework-start','submission-dialog','decision-dialog']), 'read mode omits every write action '+name)
        call(name, '/results/data/tasks/100/submissions')
    call('outsider','/results?task_id=100&view=read',status=403)
    for query in ['view=read','task_id=0100&view=read','task_id=100&view=read&return_url=https://example.org','task_id=100&view=read&task_id=200']:
        call('manager','/results?'+query,status=400)
    ctx = call('manager','/_connections_test/context/100?status=submitted&include_cancelled=true').json['data']
    check(ctx['session'] == scopes['manager'] and 'Bearer' not in json.dumps(ctx), 'public scope has no Bearer')
    page = call('manager',ctx['url']); text = page.get_data(as_text=True)
    back = html.unescape(re.search('id="task-back" href="([^"]+)"',text).group(1))
    reply = call('manager',back,status=302)
    target = urlsplit(reply.location)
    check(target.path == '/tasks/100' and parse_qs(target.query) == dict(scope=['managed'], status=['submitted'], include_cancelled=['true']), 'exact internal detail and filters')
    check(call('manager',reply.location).status_code == 200, 'return performs current task detail GET')
    big = call('worker','/_connections_test/context/9007199254740993?scope=own&view=own').json['data']['url']
    check('9007199254740993' in big and 'view=own' in big, 'canonical BIGINT preserved without JS number')
    for state in ['not_started','in_progress','submitted','approved','returned']:
        link = call('worker','/_connections_test/context/100?scope=own&view=own&status='+state).json['data']['url']
        check('back_context=' in link, 'all five status values retained '+state)
    call('manager','/_connections_test/context/100?scope=managed&view=own',status=400)
    call('outsider','/_connections_test/context/100?scope=own',status=403)
    call('worker',ctx['url'],status=400)
    token = parse_qs(urlsplit(ctx['url']).query)['back_context'][0]
    call('manager','/results?task_id=200&view=read&back_context='+token,status=400)
    call('manager','/results?task_id=100&view=read&back_context='+token+'bad',status=400)
    with patch.object(web, 'utcnow', return_value=utcnow()+timedelta(days=366)):
        call('manager',ctx['url'],status=400)
    second_tab = call('manager','/_connections_test/context/200').json['data']['url']
    call('manager',second_tab); call('manager',ctx['url'])

    # Shared upload uses cookies/CSRF/same page scope, exact JSON and limits.
    call('worker',base+'/uploads/start',dict(name='x',size=0,sha256=hashlib.sha256(b'').hexdigest()),403)
    call('manager',base+'/uploads/start',dict(name='x',size=26214401,sha256='0'*64),413)
    call('manager',base+'/uploads/start',dict(name='x',size=0,sha256='BAD'),400)
    reply=clients['manager'].post(base+'/uploads/start',headers={'X-Results-Session':scopes['manager']},data={'body':'{}'})
    check(reply.status_code == 400 and reply.json['error']['code'] == 'CSRF_FAILED', 'CSRF retained')
    reply=clients['manager'].post(base+'/uploads/start',headers={'X-Results-Session':'old'},data={'csrf_token':csrf['manager'],'body':'{}'})
    check(reply.status_code == 409 and reply.json['error']['code'] == 'RESULTS_SESSION_CHANGED', 'old page scope denied')
    reply=clients['manager'].post(base+'/uploads/start',headers={'X-Results-Session':scopes['manager']},data={'csrf_token':csrf['manager'],'body':'{"name":"a","name":"b"}'})
    check(reply.status_code == 400, 'duplicate JSON fields denied')
    first = upload(); file_id = first['attachment_id']; file_url = f'{base}/tasks/100/files/{file_id}'
    reply, key, body = change('100',[file_id])
    check(reply.json['data']['row_version'] == '2', 'one successful mutation increments version once')
    call('manager','/_connections_test/replace/100',body,key=key)
    with app.app_context():
        db=get_db()
        check(db.get(TaskRecord,100).row_version == 2, 'receipt replay does not mutate twice')
        check(db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action=='task.updated')) == 1, 'one audit on replay')
    call('manager',base+'/tasks/100')
    check(call('manager','/_connections_test/ids/100?version=2').json['data']['attachment_ids']==[file_id], 'public reader returns only sorted IDs')
    call('worker','/_connections_test/ids/100?version=2',status=403)
    with patch.object(shared,'require_schema',side_effect=AppError(503,'SHARED_ATTACHMENT_SCHEMA_REQUIRED','TEST schema unavailable')):
        call('manager','/_connections_test/ids/100?version=2',status=503)
        call('worker','/_connections_test/ids/100?version=2',status=403)
        call('manager','/_connections_test/ids/100?version=1',status=409)
    check(call('worker',file_url+'/download').data == b'TEST bytes\x00\xff', 'task-scoped actual download bytes')
    chunk=call('worker',file_url+'/chunks?offset=0').json['data']
    check(base64.b64decode(chunk['data']) == b'TEST bytes\x00\xff' and chunk['eof'], '16KiB-compatible chunks response')
    call('worker',file_url+'/chunks?offset='+'9'*5000,status=400)
    call('worker',file_url+'/chunks?offset=0&offset=1',status=400)
    call('outsider',file_url+'/download',status=403)
    call('manager',f'{base}/tasks/300/files/{file_id}/download',status=404)
    call('manager',base+'/uploads/'+file_id+'/discard',{},403)
    change('100',[file_id],version=2,status=409)
    change('100',[file_id,file_id],version=2,status=400)
    change('100',None,version=2,status=400)
    change('100',[str(__import__('uuid').uuid4()) for _ in range(11)],version=2,status=400)
    change('100',[],version=1,status=409)
    rollback_file=upload(content=b'TEST rollback')
    change('100',[rollback_file['attachment_id']],version=2,status=409,rollback=True)
    check(call('worker',file_url+'/download').data == b'TEST bytes\x00\xff', 'rollback preserves original reference and bytes')
    call('manager',base+'/uploads/'+rollback_file['attachment_id']+'/discard',{})
    change('200',[file_id]); change('100',[],version=2)
    call('manager',file_url+'/download',status=404)
    check(call('manager',f'{base}/tasks/200/files/{file_id}/download').data == b'TEST bytes\x00\xff', 'A unlink preserves B reference')
    activity=call('manager','/results/data/tasks/100/activity').json['data']['items']
    check(any('첨부 자료: 1개 → 0개' in event['summary'] for event in activity), 'minimal audit shown in Korean for desktop and web')
    check(not any(file_id in event['summary'] or 'TEST.any' in event['summary'] for event in activity), 'audit summary excludes filename/path/body')
    # Successful receipt replay never re-enters first-mutation file validation.
    call('manager','/_connections_test/replace/100',body,key=key)
    expired=upload(content=b'TEST expired')
    with app.app_context(), transaction() as db:
        db.get(shared.SharedFile,expired['attachment_id']).expires_at=utcnow()-timedelta(days=1)
    change('300',[expired['attachment_id']],status=403)
    call('manager',base+'/uploads/'+expired['attachment_id']+'/complete',{},409)
    with app.test_request_context('/'):
        try: shared.read_task_attachment_ids(get_db(),'100',expected=ExpectedVersions(row_version=3,assignment_revision=1))
        except RuntimeError: check(True,'helper refuses unguarded DB session')
        else: check(False,'unguarded helper accepted')

    # Actual result save, pending receipt replay and manager/reviewer separation.
    submit_context=call('worker','/results/data/tasks/100/context').json['data']
    submission_body=dict(expected_row_version=submit_context['row_version'],expected_assignment_revision=1,description='TEST result',attachment_ids=[])
    result_key=call('worker','/results/data/actions/key/100',{'action':'submission.submitted'},201).json['data']['request_key']
    saved=call('worker','/results/data/actions/submit/100',submission_body,201,result_key).json['data']
    call('worker','/results/data/actions/submit/100',submission_body,201,result_key)
    call('manager','/results/data/submissions/'+saved['submission_id']+'/context',status=403)
    check(call('worker','/results/data/actions/lookup/100',{'action':'submission.submitted'},key=result_key).json['data']['state']=='completed','lost-save receipt uses original key')
    worker_context=call('worker','/_connections_test/context/100?scope=own&view=own').json['data']['url']
    worker_page=call('worker',worker_context).get_data(as_text=True)
    worker_back=html.unescape(re.search('id="task-back" href="([^"]+)"',worker_page).group(1))
    with app.app_context(), transaction() as db:
        db.get(TaskAssignment,(100,1)).assignee_id=ids['outsider']
    denied=call('worker',worker_back,status=403)
    check('저장 성공은 유지' in denied.get_data(as_text=True),'detail403 never becomes saved-result failure')
    with app.app_context(): check(get_db().get(results.Submission,int(saved['submission_id'])) is not None,'save survives lost task read rights')

    # Mid-IO authority removal: another authenticated request commits before
    # the production handler's second guard. B remains readable afterward.
    path=runtime/'files/shared'/file_id
    original_open=Path.open
    changed=[False]
    def revoke_during_io(value,*args,**kwargs):
        handle=original_open(value,*args,**kwargs)
        if value == path and args and args[0]=='rb' and not changed[0]:
            changed[0]=True
            with app.app_context(), transaction() as db:
                db.get(TaskAssignment,(200,1)).assignee_id=ids['outsider']
        return handle
    with patch.object(Path,'open',revoke_during_io):
        call('worker',f'{base}/tasks/200/files/{file_id}/download',status=403)
    check(changed[0], 'relationship actually changed during download IO')
    call('outsider',f'{base}/tasks/200/files/{file_id}/download')
    with app.app_context(), transaction() as db:
        db.get(TaskAssignment,(200,1)).assignee_id=ids['worker']
    changed[0]=False
    with patch.object(Path,'open',revoke_during_io):
        call('worker',f'{base}/tasks/200/files/{file_id}/chunks?offset=0',status=403)
    check(changed[0], 'chunk download also rechecks after IO')
    with app.app_context(), transaction() as db:
        db.get(TaskAssignment,(200,1)).assignee_id=ids['worker']
    change('300',[file_id])
    changed[0]=False
    def unlink_during_io(value,*args,**kwargs):
        handle=original_open(value,*args,**kwargs)
        if value == path and args and args[0]=='rb' and not changed[0]:
            changed[0]=True
            with app.app_context(), transaction() as db:
                db.delete(db.get(shared.SharedLink,(file_id,'task',200)))
        return handle
    with patch.object(Path,'open',unlink_during_io):
        call('worker',f'{base}/tasks/200/files/{file_id}/download',status=404)
    check(changed[0], 'A reference actually removed during IO')
    check(call('worker',f'{base}/tasks/300/files/{file_id}/download').data==b'TEST bytes\x00\xff','B readable after A mid-IO unlink')
    path.write_bytes(b'corrupted')
    call('worker',f'{base}/tasks/300/files/{file_id}/download',status=409)
    path.write_bytes(b'TEST bytes\x00\xff')
    uploading=call('worker',base+'/uploads/start',dict(name='TEST cancel.any',size=32768,
        sha256=hashlib.sha256(b'x'*32768).hexdigest(),task_id='300'),201).json['data']['attachment_id']
    part=dict(offset=0,data=base64.b64encode(b'x'*16384).decode())
    call('worker',base+'/uploads/'+uploading+'/chunks',part)
    with app.app_context(), transaction() as db:
        db.add(TaskCancellation(task_id=300,cancelled_by=ids['manager'],reason='TEST cancelled during upload',cancelled_at=utcnow()))
    call('worker',base+'/uploads/'+uploading+'/chunks',{**part,'offset':16384},409)
    call('worker',base+'/uploads/'+uploading+'/complete',{},409)
    # Same account re-login makes old scope and signed navigation unusable.
    old_scope=scopes['manager']; login('manager')
    call('manager',ctx['url'],status=400)
    reply=clients['manager'].get(base+'/tasks/200',headers={'X-Results-Session':old_scope})
    check(reply.status_code==409, 'same account new session rejects old upload page')
    provider=app.extensions.pop('results_shared_attachments')
    call('manager',base+'/tasks/200',status=503)
    app.extensions['results_shared_attachments']=provider
    with app.app_context():
        check(get_db().scalar(select(func.count()).select_from(SchemaVersion))==1,'global schema version unchanged')
    engine.dispose()
    print(f'PASS {COUNT} new connection checks: owned MySQL, real cookie/CSRF handlers, W1 consumer fixture, IO revocation. Member2 product integration remains separate.')


if __name__ == '__main__':
    tempfile.tempdir='C:/Windows/Temp'
    clean={k:v for k,v in os.environ.items() if not k.startswith('MYSQL_') and k!='DATABASE_URL'}
    with owned.owned_runtime() as runtime:
        with owned.OwnedMySQL(Path(os.environ.get('RESULTS_MYSQLD','C:/Program Files/MySQL/MySQL Server 8.2/bin/mysqld.exe')),runtime,clean) as database:
            run(database.url,runtime)
    print('Owned test database/files/process cleaned; user DB and running server unchanged.')
