"""Owned MySQL/real HTTP provider checks; member 4 adapters are TEST fixtures.

Never connects to user DB/settings or installs a member 4 product adapter.
"""
import base64
import hashlib
import json
import os
from pathlib import Path
import re
import secrets
import sys
import tempfile
import threading
import uuid
from urllib.error import HTTPError
from urllib.request import Request, urlopen, build_opener, HTTPCookieProcessor
from http.cookiejar import CookieJar
from unittest.mock import patch

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
sys.path[:0] = [str(ROOT/'RemoteWork.Server'), str(ROOT/'Tests/TaskQueries.Http.Tests')]
import run_mysql_http_check as owned
from flask import g
from sqlalchemy import select, func
from waitress import create_server
from werkzeug.security import generate_password_hash
from remotework import create_app
from remotework.auth import authenticated_transaction, login_required
from remotework.common import AppError, payload, success
from remotework.db import transaction, get_db
from remotework.models import Base, User, AdminGuard, SchemaVersion, utcnow
from remotework.features import shared_attachments as shared, shared_resource_attachments as resource
from remotework.features.chat.models import ChatRoom, ChatMember, ChatMessage
from remotework.features.notices.models import Notice

COUNT = 0


def check(value, label):
    global COUNT
    if not value:
        raise AssertionError(label)
    COUNT += 1


def run(url, directory):
    app = create_app(dict(TESTING=False, DATABASE_URL=url, SECRET_KEY=secrets.token_hex(32),
        ENABLE_TASK_QUERY_DRAFT=True, ENABLE_COMMON_WRITE_DRAFT=True, ENABLE_RESULTS_DRAFT=True,
        ENABLE_SHARED_ATTACHMENTS_DRAFT=True, ENABLE_NOTICE_DRAFT=True, ENABLE_CHAT_DRAFT=True,
        RESULTS_CONTEXT_MODE='local-development', RESULTS_STORAGE_ROOT=str(directory/'files')))
    engine = app.extensions['db_engine']
    Base.metadata.create_all(engine)
    # Apply the actual proposed SQL only in this disposable database.
    with engine.begin() as conn:
        for statement in (ROOT/'docs/sql/notice_chat_attachments.mysql.sql').read_text(encoding='utf-8').split(';'):
            statement = '\n'.join(line for line in statement.splitlines() if not line.startswith('--')).strip()
            if statement:
                conn.exec_driver_sql(statement)
    password = secrets.token_urlsafe(24)
    ids = {}
    room, other_room, message, other_message = [str(uuid.uuid4()) for _ in range(4)]
    with app.app_context(), transaction() as db:
        db.add_all([AdminGuard(id=1), SchemaVersion(version=1)])
        for login, role, department in [('manager', 'admin', '개발팀'), ('worker', 'employee', '개발팀'),
                                        ('outsider', 'employee', '디자인팀'), ('otheradmin', 'admin', '개발팀')]:
            user = User(login_id=login, password_hash=generate_password_hash(password, method='pbkdf2:sha256'),
                employee_no='TEST-'+login, display_name='TEST-'+login, department=department, role=role, is_active=True)
            db.add(user); db.flush(); ids[login] = user.id
        for notice_id in (10, 20):
            db.add(Notice(id=notice_id, title='TEST notice', content='TEST only', target='개발팀',
                is_pinned=False, author_id=ids['manager'], author='TEST', created_at=utcnow()))
        for room_id, message_id in [(room, message), (other_room, other_message)]:
            db.add(ChatRoom(id=room_id, title='TEST room', is_group=True,
                created_by=ids['worker'], created_at=utcnow())); db.flush()
            db.add_all(ChatMember(room_id=room_id, user_id=ids[who], display_name='TEST', department='개발팀')
                       for who in ('manager', 'worker'))
            db.add(ChatMessage(message_id=message_id, room_id=room_id, sender_id=ids['worker'],
                sender_name='TEST', content='TEST message', sent_at=utcnow()))

    def membership(db, room_id):
        member = db.scalar(select(ChatMember).where(ChatMember.room_id == room_id,
            ChatMember.user_id == g.user.id).with_for_update().execution_options(populate_existing=True))
        if member is None or member.hidden_at is not None:
            raise AppError(403, 'FORBIDDEN', 'TEST current member required')
        return True

    def notice_upload(db, context):
        return g.user.role == 'admin'

    def notice_access(db, notice_id, operation):
        notice = db.scalar(select(Notice).where(Notice.id == notice_id)
                          .with_for_update().execution_options(populate_existing=True))
        if notice is None:
            raise AppError(404, 'NOT_FOUND', 'TEST notice missing')
        if ((operation != 'read' and g.user.role != 'admin') or
                (operation == 'read' and g.user.role != 'admin' and notice.target not in ('전 직원', g.user.department))):
            raise AppError(403, 'FORBIDDEN', 'TEST notice audience required')
        return resource.ResourceAccess()

    def chat_access(db, message_id, operation):
        item = db.scalar(select(ChatMessage).where(ChatMessage.message_id == message_id)
                         .with_for_update().execution_options(populate_existing=True))
        if item is None:
            raise AppError(404, 'NOT_FOUND', 'TEST message missing')
        membership(db, item.room_id)
        if operation != 'read' and item.sender_id != g.user.id:
            raise AppError(403, 'FORBIDDEN', 'TEST message writer required')
        return resource.ResourceAccess(item.room_id)

    resource.register_adapter(app, 'notice', authorize_upload=notice_upload, authorize_resource=notice_access)
    resource.register_adapter(app, 'chat', authorize_upload=membership, authorize_resource=chat_access)

    @app.post('/api/v1/_resource_test/<kind>/<target>/<operation>')
    @login_required
    def consumer(kind, target, operation):
        body = payload()
        with authenticated_transaction() as db:
            if operation == 'replace':
                items = resource.replace_resource_files(db, kind, target, body['attachment_ids'])
            elif operation == 'release':
                resource.release_resource_files(db, kind, target); items = []
            else:
                raise AppError(400, 'INVALID_INPUT', 'TEST operation')
            if body.get('rollback'):
                raise AppError(409, 'TEST_ROLLBACK', 'TEST intentional rollback')
        return success({'items': items})

    @app.post('/api/v1/_resource_test/new-notice')
    @login_required
    def atomic_notice():
        body = payload()
        with authenticated_transaction(roles=('admin',)) as db:
            db.add(Notice(id=30, title='TEST atomic', content='TEST', target='개발팀', is_pinned=False,
                author_id=g.user.id, author='TEST', created_at=utcnow())); db.flush()
            resource.replace_resource_files(db, 'notice', 30, body['attachment_ids'])
            raise AppError(409, 'TEST_ROLLBACK', 'TEST resource + refs roll back together')

    server = create_server(app, host='127.0.0.1', port=0, threads=6)
    closing = threading.Event()
    def serve():
        try:
            server.run()
        except OSError as error:
            if not (closing.is_set() and getattr(error, 'winerror', None) == 10038):
                raise
    thread = threading.Thread(target=serve, daemon=True); thread.start()
    base = 'http://127.0.0.1:'+str(server.effective_port)
    tokens = {}

    def call(who, path, body=None, method=None, status=200, headers=None, opener=None):
        values = {} if who is None else {'Authorization': 'Bearer '+tokens[who]}
        values.update(headers or {})
        data = body
        if isinstance(body, dict):
            data = json.dumps(body).encode(); values['Content-Type'] = 'application/json'
        req = Request(base+path, data=data, headers=values, method=method)
        try:
            with (opener.open(req, timeout=10) if opener else urlopen(req, timeout=10)) as r:
                actual, raw, mime = r.status, r.read(), r.headers.get('Content-Type', '')
        except HTTPError as r:
            actual, raw, mime = r.code, r.read(), r.headers.get('Content-Type', '')
        check(actual == status, path+' status '+str(actual)+' expected '+str(status))
        result = json.loads(raw) if 'json' in mime else raw
        return result.get('data', result) if isinstance(result, dict) else result

    prefix = '/api/v1/results/shared/resources/'

    def upload(who, kind='notice', context=None, content=b'TEST resource bytes'):
        item = call(who, prefix+kind+'/uploads/start', dict(context_id=context, name='TEST.any',
                    size=len(content), sha256=hashlib.sha256(content).hexdigest()), status=201)
        path = prefix+kind+'/uploads/'+item['attachment_id']
        for offset in range(0, len(content), 16384):
            part = dict(offset=offset, data=base64.b64encode(content[offset:offset+16384]).decode())
            call(who, path+'/chunks', part)
            call(who, path+'/chunks', part)  # Same-byte retry is safe.
        status = call(who, path)
        check(status['state'] == 'uploading', 'status GET does not complete upload')
        call(who, path+'/complete', {})
        call(who, path+'/complete', {})
        return item['attachment_id']

    def change(kind, target, files=None, who='manager', operation='replace', rollback=False, status=200):
        return call(who, '/api/v1/_resource_test/'+kind+'/'+str(target)+'/'+operation,
                    dict(attachment_ids=files or [], rollback=rollback), status=status)

    def download(kind, target, attachment, who='worker', status=200):
        return call(who, prefix+kind+'/'+str(target)+'/files/'+attachment+'/download', status=status)

    try:
        for who in ids:
            tokens[who] = call(None, '/api/v1/auth/login', dict(login_id=who, password=password))['access_token']
        file = upload('manager')
        call('worker', prefix+'notice/10/files')
        download('notice', 10, file, status=404)
        change('notice', 10, [file], rollback=True, status=409)
        check(call('worker', prefix+'notice/10/files')['items'] == [], 'failed reference change rolls back')
        call('manager', '/api/v1/_resource_test/new-notice', {'attachment_ids': [file]}, status=409)
        with app.app_context():
            check(get_db().get(Notice, 30) is None, 'new notice and file references roll back together')
        change('notice', 10, [file])
        change('notice', 10, [file])
        with app.app_context():
            check(get_db().scalar(select(func.count()).select_from(resource.NoticeFile)) == 1,
                  'same final list does not duplicate a reference')
        check(download('notice', 10, file) == b'TEST resource bytes', 'notice audience downloads exact bytes')
        download('notice', 10, file, who='outsider', status=403)
        change('notice', 20, [file])
        # Actual committed target revocation between the two download guards.
        original_open = Path.open
        changed = [False]
        def revoke_during_io(path, *args, **kwargs):
            handle = original_open(path, *args, **kwargs)
            if path == directory/'files/shared'/file and args and args[0] == 'rb' and not changed[0]:
                changed[0] = True
                with app.app_context(), transaction() as db:
                    db.get(Notice, 20).target = '디자인팀'
            return handle
        with patch.object(Path, 'open', revoke_during_io):
            download('notice', 20, file, status=403)
        check(changed[0], 'actual notice audience change during IO blocks final bytes')
        with app.app_context(), transaction() as db:
            db.get(Notice, 20).target = '개발팀'
        changed[0] = False
        with patch.object(Path, 'open', revoke_during_io):
            call('worker', prefix+'notice/20/files/'+file+'/chunks?offset=0', status=403)
        check(changed[0], 'chunk IO also rechecks current audience')
        with app.app_context(), transaction() as db:
            db.get(Notice, 20).target = '개발팀'
        change('notice', 10, operation='release')
        download('notice', 10, file, status=404)
        check(download('notice', 20, file) == b'TEST resource bytes', 'removing A preserves B')
        call('manager', '/api/v1/results/shared/files/'+file+'/download', status=404)
        call('manager', '/api/v1/results/shared/uploads/'+file+'/complete', {}, status=403)
        call('manager', prefix+'notice/uploads/'+file, status=403)
        change('notice', 20, [file, file], status=400)
        change('notice', 20, [file]*11, status=400)
        call('manager', prefix+'notice/2147483648/files', status=400)
        call('manager', prefix+'notice/01/files', status=400)
        call('manager', prefix+'chat/1/files', status=400)
        # Current target change revokes an old notice reader without deleting B.
        with app.app_context(), transaction() as db:
            db.get(Notice, 20).target = '디자인팀'
        download('notice', 20, file, status=403)
        check(download('notice', 20, file, who='outsider') == b'TEST resource bytes', 'new audience can read')
        change('notice', 20, operation='release', rollback=True, status=409)
        check(download('notice', 20, file, who='outsider') == b'TEST resource bytes', 'release rollback preserves bytes')
        chat_file = upload('worker', 'chat', room, content=b'TEST chat')
        change('chat', message, [chat_file], who='worker')
        check(download('chat', message, chat_file, who='manager') == b'TEST chat', 'current participant downloads')
        download('chat', message, chat_file, who='otheradmin', status=403)
        change('chat', other_message, [chat_file], who='worker', status=403)
        change('notice', 10, [chat_file], status=403)
        with app.app_context(), transaction() as db:
            db.get(ChatMember, (room, ids['worker'])).hidden_at = utcnow()
        download('chat', message, chat_file, status=403)
        check(download('chat', message, chat_file, who='manager') == b'TEST chat', 'personal hide does not delete peer file')
        chunk = call('manager', prefix+'chat/'+message+'/files/'+chat_file+'/chunks?offset=0')
        check(base64.b64decode(chunk['data']) == b'TEST chat', 'resource chunks use original file checks')
        call('manager', prefix+'chat/'+message+'/files/'+chat_file+'/chunks?offset=00', status=400)
        meta = dict(name='TEST', size=1, sha256=hashlib.sha256(b'x').hexdigest(), context_id=None)
        call('worker', prefix+'notice/uploads/start', meta, status=403)
        call('manager', prefix+'notice/uploads/start', {**meta, 'size': 26214401}, status=413)
        call('manager', prefix+'notice/uploads/start', {**meta, 'name': '../bad'}, status=400)
        call('manager', prefix+'notice/uploads/start', {**meta, 'sha256': 'X'*64}, status=400)
        partial = call('manager', prefix+'notice/uploads/start', meta, status=201)['attachment_id']
        partial_path = prefix+'notice/uploads/'+partial
        call('manager', partial_path+'/complete', {}, status=409)
        call('manager', partial_path+'/chunks', dict(offset=0, data=base64.b64encode(b'x').decode()))
        call('manager', partial_path+'/chunks', dict(offset=0, data=base64.b64encode(b'y').decode()), status=409)
        call('manager', partial_path+'/complete', {})
        call('manager', partial_path, headers={'X-Results-Session': 'not-a-web-session'})
        # An API request still requires its own real API identity.
        call(None, partial_path, status=401)
        with app.app_context(), transaction() as db:
            db.get(User, ids['manager']).is_active = False
        call('manager', partial_path, status=401)
        with app.app_context(), transaction() as db:
            db.get(User, ids['manager']).is_active = True
        call('manager', partial_path, method='DELETE')
        temporary = upload('manager', content=b'')
        call('worker', prefix+'notice/uploads/'+temporary, status=403)
        with app.app_context(), transaction() as db:
            db.get(shared.SharedFile, temporary).expires_at = utcnow()-__import__('datetime').timedelta(seconds=1)
        call('manager', prefix+'notice/uploads/'+temporary+'/complete', {}, status=409)
        with app.app_context():
            shared.cleanup_shared_files()
            check(get_db().get(shared.SharedFile, temporary) is None, 'expired unreferenced resource file cleaned')
            check(get_db().get(resource.ResourceUpload, temporary) is None, 'scope removed before file FK delete')
        # Cookie adapter requires current web session binding and the existing CSRF.
        from urllib.parse import urlencode
        web = build_opener(HTTPCookieProcessor(CookieJar()))
        page = call(None, '/login', opener=web).decode()
        csrf = re.search(r'name="csrf_token" value="([^"]+)"', page).group(1)
        call(None, '/login', urlencode(dict(csrf_token=csrf, login_id='manager', password=password)).encode(),
             headers={'Content-Type': 'application/x-www-form-urlencoded'}, opener=web)
        page = call(None, '/results', opener=web).decode()
        csrf = re.search(r'data-csrf="([^"]+)"', page)
        if csrf is None:
            csrf = re.search(r'name="csrf-token" content="([^"]+)"', page)
        scope = re.search(r'data-session="([^"]+)"', page)
        if scope is None:
            scope = re.search(r'data-session-scope="([^"]+)"', page)
        check(csrf is not None and scope is not None, 'real page supplies CSRF and session scope')
        csrf, scope = csrf.group(1), scope.group(1)
        headers = {'Content-Type': 'application/x-www-form-urlencoded', 'X-Results-Session': scope}
        path = '/results/data/shared/resources/notice/uploads/start'
        form = urlencode(dict(csrf_token=csrf, body=json.dumps(meta))).encode()
        web_item = call(None, path, form, headers=headers, opener=web, status=201)
        call(None, path, form, headers={**headers, 'X-Results-Session': 'old'}, opener=web, status=409)
        call(None, path, urlencode(dict(csrf_token='wrong', body=json.dumps(meta))).encode(),
             headers=headers, opener=web, status=400)
        call('manager', path, form, headers=headers, status=400)  # Bearer cannot replace the cookie/CSRF session.
        web_id = web_item['attachment_id']
        status = call(None, '/results/data/shared/resources/notice/uploads/'+web_id,
                      headers={'X-Results-Session': scope}, opener=web)
        check(status['state'] == 'uploading', 'web status GET never calls complete')
        # Current cookie/CSRF without its authentication cookie cannot borrow Bearer.
        jar = next(h.cookiejar for h in web.handlers if isinstance(h, HTTPCookieProcessor))
        auth_cookies = [cookie for cookie in jar if cookie.name == 'rw_session']
        for cookie in auth_cookies:
            jar.clear(cookie.domain, cookie.path, cookie.name)
        call('manager', path, form, headers=headers, opener=web, status=401)
        for cookie in auth_cookies:
            jar.set_cookie(cookie)
        call(None, '/results/data/shared/resources/notice/uploads/'+web_id+'/discard',
             urlencode(dict(csrf_token=csrf, body='{}')).encode(), headers=headers, opener=web)
        # Missing adapters fail closed, without treating an admin role as a grant.
        with app.app_context():
            saved = app.extensions['resource_attachment_adapters'].pop('notice')
        call('manager', prefix+'notice/10/files', status=503)
        app.extensions['resource_attachment_adapters']['notice'] = saved
        # Unknown callback return values must never grant resource access.
        app.extensions['resource_attachment_adapters']['notice'] = (notice_upload, lambda *args: None)
        call('manager', prefix+'notice/10/files', status=403)
        app.extensions['resource_attachment_adapters']['notice'] = saved
        with app.app_context(), transaction() as db:
            db.get(shared.SharedFile, file).expires_at = utcnow()-__import__('datetime').timedelta(days=1)
        with app.app_context():
            shared.cleanup_shared_files()
        check(download('notice', 20, file, who='outsider') == b'TEST resource bytes',
              'expired temporary TTL never removes a live notice reference')
        # Final unlink removes only that resource's bytes; other live links survive.
        change('notice', 20, operation='release')
        with app.app_context():
            check(get_db().get(shared.SharedFile, file) is None, 'last notice unlink cleans physical reference metadata')
            check(get_db().get(shared.SharedFile, chat_file) is not None, 'chat reference survives notice cleanup')
            check(get_db().scalar(select(func.count()).select_from(SchemaVersion)) == 1, 'common schema1 unchanged')
        print(f'PASS {COUNT} owned MySQL/real HTTP resource-file checks; member4 authority/consumer routes are TEST fixtures.')
    finally:
        closing.set(); server.close(); thread.join(timeout=5); engine.dispose()


if __name__ == '__main__':
    tempfile.tempdir = 'C:/Windows/Temp'
    clean = {k:v for k,v in os.environ.items() if not k.startswith('MYSQL_') and k != 'DATABASE_URL'}
    with owned.owned_runtime() as runtime:
        with owned.OwnedMySQL(Path(os.environ.get('RESULTS_MYSQLD', 'C:/Program Files/MySQL/MySQL Server 8.2/bin/mysqld.exe')), runtime, clean) as database:
            run(database.url, runtime)
    print('Owned resource HTTP/MySQL/files cleaned; user DB unchanged.')
