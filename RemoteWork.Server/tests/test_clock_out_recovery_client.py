"""Actual C# ApiClient + original identity HTTP/MySQL; fixture-only scan/signing."""
from contextlib import contextmanager
from datetime import timedelta
import json
import os
from pathlib import Path
import subprocess
import sys
import threading
from uuid import uuid4

import pytest
from sqlalchemy import select, func
from werkzeug.serving import make_server
from remotework.auth import login_required
from remotework.common import success
from remotework.db import get_db
from remotework.models import User, AuthSession
from remotework.features.identity.attendance import WorkSession, WorkflowReceipt
from remotework.features.identity import routes
from test_identity_service import identity, working, env, issue, post, result, PASSWORD
sys.path.insert(0,str(Path(__file__).resolve().parents[2]/'Tests/TaskQueries.Http.Tests'))
import run_mysql_http_check as owned


@pytest.mark.parametrize('mode',['normal','loss'])
def test_actual_clock_out_recovery_after_normal_session_deleted(working,mode):
    dll=os.environ.get('CLOCK_OUT_CLIENT')
    if not dll:
        pytest.skip('Explicit owned C# client DLL required')
    assert Path(dll).is_file() and Path(dll).name=='ClockOutRecovery.Tests.dll'
    app=working[0]
    work_id=str(uuid4())
    day=(routes.now()+timedelta(hours=9)).date().isoformat()
    with app.app_context():
        db=get_db();user=db.scalar(select(User).where(User.login_id=='staff1'))
        uid=user.id
        db.add(WorkSession(id=work_id,user_id=uid,work_date=day,started_at=routes.now()-timedelta(minutes=5)))
        db.commit()
    @app.get('/api/v1/__test/clock-out')
    @login_required
    def bootstrap():
        from flask import request
        headers={'Authorization':request.headers['Authorization']}
        job,_=issue(working,headers,purpose='clock_out')
        assert post(working,headers,job,'complete',result(working,headers,job)).status_code==200
        return success(dict(work_id=work_id,work_date=day,challenge_id=job['challenge_id']))
    server=make_server('127.0.0.1',0,app,threaded=True)
    worker=threading.Thread(target=server.serve_forever,daemon=True);worker.start()
    try:
        child_env=dict(os.environ)
        child_env.update(CLOCK_OUT_OWNED_TEST='1',CLOCK_OUT_TEST_PASSWORD=PASSWORD)
        child_env.pop('MYSQL_TEST_URL',None);child_env.pop('DATABASE_URL',None)
        completed=owned.run_owned_client(['dotnet',str(Path(dll).resolve()),'--owned-loopback',
            'http://127.0.0.1:'+str(server.server_port)+'/',mode],env=child_env,
            cwd=Path(dll).resolve().parent,stdin=subprocess.DEVNULL,
            capture_output=True,text=True,encoding='utf-8',timeout=35,creationflags=subprocess.CREATE_NO_WINDOW)
        assert completed.returncode==0,completed.stdout+completed.stderr
        summary=json.loads(completed.stdout.strip().splitlines()[-1])
        assert summary['passed'] and summary['finalizes']==(2 if mode=='loss' else 1)
        with app.app_context():
            db=get_db()
            assert db.get(WorkSession,work_id).ended_at is not None
            assert db.scalar(select(func.count()).select_from(WorkflowReceipt))==1
            assert db.scalar(select(func.count()).select_from(AuthSession).where(AuthSession.user_id==uid))==0
    finally:
        server.shutdown();worker.join(5);server.server_close()
        assert not worker.is_alive()
