"""Actual owner1 HTTP/DB -> owner4 source/wrapper/scope/panel + MainForm.

Task and journal creation are fixture calls, not owner2 production hooks.
No fake event callback, SQLite or shared user database is used.
"""
import os
from pathlib import Path
import subprocess
from threading import Thread

import pytest
from sqlalchemy import select
from werkzeug.serving import make_server

from remotework import deadline_http as api
from remotework.write_requests import WriteReceipt
from test_task_deadline_facts import deadline_app, deadline
from test_deadline_http import http_app,env,REAL_CLOCK
from test_deadline_events import create


@pytest.mark.parametrize('mode',['normal','lost','list-fail','main'])
def test_actual_binding_http_winforms_confirmation_and_relogin(env,monkeypatch,mode):
    assert env.app.extensions['db_engine'].dialect.name=='mysql'
    monkeypatch.setattr(api,'server_utc',REAL_CLOCK)
    create(env)
    root=Path(__file__).resolve().parents[2]
    dll=root/'Tests/DeadlineConnection.Tests/bin/Release/net8.0-windows/DeadlineConnection.Tests.dll'
    assert dll.is_file(),'Build the binding consumer test first'
    server=make_server('127.0.0.1',0,env.app,threaded=True)
    worker=Thread(target=server.serve_forever,daemon=True);worker.start()
    child_env=os.environ.copy();child_env['DEADLINE_TEST_URL']=f'http://127.0.0.1:{server.server_port}'
    child_env['DEADLINE_LIVE_MODE']=mode
    try:
        result=subprocess.run(['dotnet',str(dll),'--main' if mode=='main' else '--live'],env=child_env,
            capture_output=True,text=True,encoding='utf-8',timeout=45)
        assert result.returncode==0,result.stdout+result.stderr
        print(result.stdout.strip())
        with env.app.extensions['db_engine'].connect() as db:
            rows=list(db.execute(select(api.confirmations)))
            receipts=list(db.execute(select(WriteReceipt)))
            assert len(rows)==len(receipts)==1
            assert rows[0].assignment_revision==1 and rows[0].user_id==env.ids['staff1']
    finally:
        server.shutdown();worker.join(timeout=8);server.server_close()
    assert not worker.is_alive()
