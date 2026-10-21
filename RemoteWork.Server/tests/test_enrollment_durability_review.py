"""R55-01/02/03 regressions on an owned MySQL, including cold OFF and RR races."""
from concurrent.futures import ThreadPoolExecutor
import json
import os
from pathlib import Path
import subprocess
import sys
import threading

import pytest
from sqlalchemy import event, func, select

from remotework import create_app
from remotework.db import get_db
from remotework.features.enrollment.models import Job, Mapping
from remotework.features.enrollment.durability import PhysicalReceipt
from remotework import enrollment_protocol_draft as wire
from test_device_enrollment import env, issue, result, complete, count, ROOT
from test_enrollment_durability import durable, post, report, intent_body


def off_app(e):
    app = create_app(dict(e[0].config) | {'ENABLE_ENROLLMENT_DURABILITY_DRAFT':False})
    return app, app.test_client(), *e[2:]


def cold_off(e, job, raw):
    payload = {'url':str(e[0].extensions['db_engine'].url.render_as_string(hide_password=False)),
        'headers':e[2], 'job':job, 'raw':raw, 'now':e[4][0].isoformat()}
    code = r'''
import json,sys
from datetime import datetime
from remotework import create_app
v=json.load(sys.stdin)
app=create_app({'TESTING':True,'SECRET_KEY':'test-enrollment-'+'a'*32,'DATABASE_URL':v['url'],
 'ENABLE_DEVICE_ENROLLMENT_DRAFT':True,'ENABLE_ENROLLMENT_DURABILITY_DRAFT':False,
 'ENROLLMENT_CLOCK':lambda:datetime.fromisoformat(v['now']),
 'ENROLLMENT_DEVICE_KEYS':{v['job']['device_id']:{'key_id':'test-public-key','key_epoch':1,
 'request_key':bytes(range(32)),'result_key':bytes(range(32,64))}}})
assert 'remotework.features.enrollment.durability' not in sys.modules
from remotework import enrollment_protocol_draft as wire
client=app.test_client();job=v['job'];root='/api/v1/device-enrollments/'+job['enrollment_id']
body={f:job[f] for f in wire.COMMON-{'enrollment_id'}}|{'scope':'sensor_write',
 'ticket_digest':wire.ticket_digest(wire.b64decode(job['request_envelope'])), 'probe_nonce':wire.b64encode(b'q'*32)}
write=client.post(root+'/approval',headers=v['headers'],json=body)
done=client.post(root+'/complete',headers=v['headers'],json={'operation_revision':job['operation_revision'],'result_envelope':v['raw']})
assert not any('/physical-result' in str(r) for r in app.url_map.iter_rules())
assert 'archive-enrollment-result' not in app.cli.commands
print(json.dumps({'write':write.status_code,'complete':done.status_code}))
app.extensions['db_engine'].dispose()
'''
    child_env = os.environ.copy()
    for name in list(child_env):
        if name.startswith('REMOTEWORK_'):
            child_env.pop(name)
    child_env['PYTHON_DOTENV_DISABLED']='1'
    completed = subprocess.run([sys.executable,'-c',code],input=json.dumps(payload),capture_output=True,
        text=True,env=child_env,timeout=30)
    assert completed.returncode == 0, 'Owned cold OFF check failed; private output omitted'
    return json.loads(completed.stdout)


def test_r55_01_intent_survives_hot_and_cold_off_without_legacy_write_or_unrecorded_complete(durable):
    job,_ = issue(durable);raw=result(job)
    assert post(durable,job,'/storage-intent',intent_body(job)).json['data']['may_store'] is True
    legacy = off_app(durable)
    try:
        assert post(legacy,job,'/approval',intent_body(job)|{'scope':'sensor_write'}).status_code == 409
        assert complete(legacy,job,raw).status_code == 409
        assert cold_off(durable,job,raw) == {'write':409,'complete':409}
        assert count(durable[0],Mapping) == 0
        assert report(durable,job,raw).status_code == 200
        assert cold_off(durable,job,raw) == {'write':409,'complete':200}
        assert complete(legacy,job,raw).status_code == 200
        assert cold_off(durable,job,raw) == {'write':409,'complete':200}
        assert count(durable[0],Mapping) == 1
    finally:
        legacy[0].extensions['db_engine'].dispose()


def test_r55_02_capture_waiting_on_guard_sees_newly_committed_intent(durable):
    job,_=issue(durable);engine=durable[0].extensions['db_engine']
    waiting,release=threading.Event(),threading.Event()
    def pause(connection,cursor,statement,params,context,many):
        if threading.current_thread().name.startswith('capture-race') and 'FROM admin_guard' in statement and 'FOR UPDATE' in statement:
            waiting.set()
            assert release.wait(15), 'Intent writer did not release capture reader'
    event.listen(engine,'before_cursor_execute',pause)
    try:
        with ThreadPoolExecutor(1,thread_name_prefix='capture-race') as pool:
            pending=pool.submit(lambda:post(durable,job,'/approval',intent_body(job)|{'scope':'sensor_capture'}).status_code)
            try:
                assert waiting.wait(15), 'Capture did not reach guard after login snapshot'
                stored=post(durable,job,'/storage-intent',intent_body(job))
                assert stored.status_code==200 and stored.json['data']['may_store'] is True
            finally:
                release.set()
            assert pending.result(timeout=15)==409
        assert count(durable[0],Mapping)==0
    finally:
        release.set();event.remove(engine,'before_cursor_execute',pause)


@pytest.mark.parametrize('outcome', ['sensor_stored','failed'])
@pytest.mark.parametrize('transport', ['http','operator'])
def test_r55_03_first_blob_cannot_conflict_with_terminal_hash_but_identical_import_works(durable,tmp_path,outcome,transport):
    job,_=issue(durable)
    original=result(job,outcome=outcome,failure=None if outcome=='sensor_stored' else 'duplicate_finger')
    legacy=off_app(durable)
    try:
        first=complete(legacy,job,original)
        assert first.status_code==200
        assert count(durable[0],PhysicalReceipt)==0
        conflicting=result(job,outcome='failed',failure='duplicate_finger')
        def send(raw):
            if transport=='http':return report(durable,job,raw).status_code
            path=tmp_path/'result.private.json';path.write_bytes(wire.b64decode(raw))
            response=durable[0].test_cli_runner().invoke(args=['archive-enrollment-result','--login-id','admin1',
                '--enrollment-id',job['enrollment_id'],'--result-file',str(path)])
            return 200 if response.exit_code==0 else 409
        assert send(conflicting)==409
        assert count(durable[0],PhysicalReceipt)==0
        assert send(original)==200 and send(original)==200
        assert complete(durable,job,original).json['data']==first.json['data']
        with durable[0].app_context():
            db=get_db();physical=db.get(PhysicalReceipt,job['enrollment_id']);terminal=db.get(Job,job['enrollment_id'])
            assert physical.result_hash==terminal.result_hash and physical.result_envelope==wire.b64decode(original)
            assert db.scalar(select(func.count()).select_from(Mapping)) == (1 if outcome=='sensor_stored' else 0)
    finally:
        legacy[0].extensions['db_engine'].dispose()
