from datetime import date
from flask import g
import pytest
from sqlalchemy import delete, select

from remotework.auth import create_user
from remotework.db import get_db, transaction
from remotework.features.tasks.models import TaskAssignment, TaskManager, TaskRecord
from remotework.models import User
from remotework.task_write_support import install
from remotework.write_requests import bp


@pytest.fixture
def preparation(app):
    app.config.update(ENABLE_COMMON_WRITE_DRAFT=True, ENABLE_TASK_QUERY_DRAFT=True, WRITE_REQUEST_TTL_SECONDS=86400)
    app.register_blueprint(bp)
    install(app)
    with app.app_context(), transaction() as db:
        users={u.login_id:u for u in db.scalars(select(User))}
        outsider=create_user(db,{'login_id':'otheradmin','password':'Other-admin-2026!',
            'display_name':'외부 관리자','employee_no':'A002','department':'개발팀','role':'admin'})
        other=create_user(db,{'login_id':'otherstaff','password':'Other-staff-2026!',
            'display_name':'직원','employee_no':'E002','department':'개발팀','role':'employee'})
        task=TaskRecord(task_id=9007199254740993,title='준비 검증',description='',task_status='not_started',
            start_date=date(2026,10,17),due_date=date(2026,10,20),current_assignment_revision=1,
            created_by=users['admin1'].id)
        db.add(task)
        db.flush()
        db.add(TaskAssignment(task_id=task.task_id,revision=1,assignee_id=users['staff1'].id,assigned_by=users['admin1'].id))
        db.add(TaskManager(task_id=task.task_id,user_id=users['admin1'].id))
        return app,task.task_id,users['staff1'].id,users['admin1'].id


def test_real_product_preparation_routes_policy_and_minimal_fields(preparation,login):
    app,task_id,staff_id,admin_id=preparation
    admin,_=login('admin1','Admin-test-2026!')
    staff,_=login()
    outsider,_=login('otheradmin','Other-admin-2026!')
    with app.test_client() as client:
        path=f'/api/v1/tasks/{task_id}/write-context'
        for headers in (admin,staff):
            r=client.get(path,headers=headers)
            assert r.status_code==200,r.json
            context=r.json['data']['context']
            assert context['task_id']==str(task_id) and context['row_version']=='1'
            assert context['assignment_revision']==1 and context['assignee_id']==staff_id
            assert context['manager_user_ids']==[admin_id]
        assert client.get(path,headers=outsider).status_code==403
        for name in ('assignment','manager'):
            route=f'/api/v1/tasks/{name}-candidates'
            assert client.get(route,headers=staff).status_code==403
            assert client.get(route+'?task_id='+str(task_id),headers=outsider).status_code==403
            r=client.get(route+'?task_id='+str(task_id),headers=admin)
            assert r.status_code==200,r.json
            assert all(set(item)=={'id','display_name','selection_label'} for item in r.json['data']['items'])
            # Active administrators can select candidates for a new task.
            assert client.get(route,headers=outsider).status_code==200
            for bad in ('?limit=0','?page=1&page=2','?q=x&q=y','?task_id=01','?unexpected=1'):
                assert client.get(route+bad,headers=admin).status_code==400
        key=client.post('/api/v1/write-requests/keys',headers=staff,
            json={'action':'task.accepted','target':str(task_id)})
        assert key.status_code==201
        assert client.post('/api/v1/write-requests/keys',headers=outsider,
            json={'action':'task.updated','target':str(task_id)}).status_code==403
        assert client.post('/api/v1/write-requests/keys',headers=staff,
            json={'action':'task.deadline_extension_approved','target':str(task_id)}).status_code==503
        # No task mutation was installed by common support.
        assert client.post(f'/api/v1/tasks/{task_id}/accept',headers=staff,json={}).status_code==404


def test_reassignment_and_manager_revocation_close_context_and_keys(preparation,login):
    app,task_id,staff_id,admin_id=preparation
    staff,_=login()
    admin,_=login('admin1','Admin-test-2026!')
    with app.app_context(),transaction() as db:
        other=db.scalar(select(User).where(User.login_id=='otherstaff'))
        db.add(TaskAssignment(task_id=task_id,revision=2,assignee_id=other.id,assigned_by=admin_id))
        db.get(TaskRecord,task_id).current_assignment_revision=2
        db.execute(delete(TaskManager).where(TaskManager.task_id==task_id))
    with app.test_client() as client:
        for headers,action in ((staff,'task.accepted'),(admin,'task.updated')):
            assert client.get(f'/api/v1/tasks/{task_id}/write-context',headers=headers).status_code==403
            assert client.post('/api/v1/write-requests/keys',headers=headers,
                json={'action':action,'target':str(task_id)}).status_code==403


def test_real_csharp_http_to_mysql_preparation_and_single_effect(preparation):
    import json, os, subprocess
    from pathlib import Path
    from threading import Thread
    from sqlalchemy import func
    from waitress import create_server
    from waitress import wasyncore
    from remotework.auth import authenticated_transaction, login_required
    from remotework.common import audit, payload, success
    from remotework.task_write_support import authorize_task
    from remotework.write_contract import ExpectedVersions
    from remotework.write_requests import WriteReceipt, WriteResult, perform_once
    from remotework.models import AuditEvent, utcnow
    from flask import request
    app,task_id,_,_=preparation
    if app.extensions['db_engine'].dialect.name!='mysql':
        pytest.skip('Actual C# HTTP test requires isolated MySQL')
    root=Path(__file__).resolve().parents[2]
    dll=Path(os.getenv('WRITE_SUPPORT_HTTP_CLIENT_DLL',root/'Tests/WriteSupport.Http.Tests/bin/Release/net8.0/WriteSupport.Http.Tests.dll'))
    if not dll.is_file():
        pytest.skip('Build Tests/WriteSupport.Http.Tests Release first')
    @app.post('/api/v1/_write_support_probe')
    @login_required
    def probe():
        body=payload()
        with authenticated_transaction() as db:
            def change(session):
                task=session.get(TaskRecord,task_id)
                ExpectedVersions.from_body(body).check(row_version=task.row_version,
                    assignment_revision=task.current_assignment_revision)
                task.title=body['title']
                task.row_version+=1
                task.updated_at=utcnow()
                audit('test.http_once','task',task_id)
                return WriteResult({'task_id':str(task_id),'row_version':str(task.row_version)})
            result=perform_once(db,key=request.headers.get('Idempotency-Key'),action='task.accepted',
                target=str(task_id),body=body,authorize=lambda session,target:authorize_task(session,target),mutate=change)
        return success(result.data,result.status)
    sockets={}
    server=create_server(app,host='127.0.0.1',port=0,threads=4,map=sockets,asyncore_loop_timeout=.1)
    thread=Thread(target=server.run,daemon=True)
    thread.start()
    try:
        settings={'server_url':f'http://127.0.0.1:{server.effective_port}',
                  'admin_password':'Admin-test-2026!','staff_password':'Staff-test-2026!','task_id':str(task_id)}
        child=subprocess.run(['dotnet',str(dll)],input=json.dumps(settings),capture_output=True,
                             encoding='utf-8',timeout=45)
        assert child.returncode==0, child.stdout+'\n'+child.stderr
        print(child.stdout.strip())
        with app.app_context():
            db=get_db()
            assert db.get(TaskRecord,task_id).row_version==2
            assert db.scalar(select(func.count()).select_from(WriteReceipt))==1
            assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action=='test.http_once'))==1
    finally:
        server.task_dispatcher.shutdown()
        wasyncore.close_all(map=sockets)
        thread.join(timeout=5)
        assert not thread.is_alive()


@pytest.mark.parametrize('revoked',['assignment','manager'])
def test_mysql_relation_revoked_while_preparation_waits(preparation,login,revoked):
    from concurrent.futures import ThreadPoolExecutor
    from threading import Event,current_thread
    from sqlalchemy import event
    app,task_id,_,admin_id=preparation
    engine=app.extensions['db_engine']
    if engine.dialect.name!='mysql':
        pytest.skip('Actual RR snapshot/current locking read required')
    headers,_=login() if revoked=='assignment' else login('admin1','Admin-test-2026!')
    reached,resume=Event(),Event()
    def pause(conn,cursor,statement,parameters,context,executemany):
        if current_thread().name.startswith('waiting-context') and 'FROM admin_guard' in statement:
            reached.set()
            assert resume.wait(20)
    def query():
        with app.test_client() as client:
            return client.get(f'/api/v1/tasks/{task_id}/write-context',headers=headers).status_code
    event.listen(engine,'before_cursor_execute',pause)
    try:
        with ThreadPoolExecutor(max_workers=1,thread_name_prefix='waiting-context') as pool:
            future=pool.submit(query)
            try:
                assert reached.wait(15)
                # Independent committed fixture mutation after older auth snapshot.
                with app.app_context(),transaction() as db:
                    if revoked=='assignment':
                        other=db.scalar(select(User).where(User.login_id=='otherstaff'))
                        db.add(TaskAssignment(task_id=task_id,revision=2,assignee_id=other.id,assigned_by=admin_id))
                        db.get(TaskRecord,task_id).current_assignment_revision=2
                    else:
                        db.execute(delete(TaskManager).where(TaskManager.task_id==task_id))
            finally:
                resume.set()
            assert future.result(timeout=20)==403
    finally:
        event.remove(engine,'before_cursor_execute',pause)

