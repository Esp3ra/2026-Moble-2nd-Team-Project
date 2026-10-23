"""Raw005 + owned MySQL + real Flask TCP + Chromium; never the user's server."""
from pathlib import Path
import argparse
import os
import subprocess
import sys
import threading

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tests/TaskQueries.Http.Tests'))
import run_mysql_http_check as owned


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--node',type=Path,required=True)
    parser.add_argument('--node-modules',type=Path,required=True)
    parser.add_argument('--report-dir',type=Path,required=True)
    parser.add_argument('--browser',type=Path)
    args=parser.parse_args()
    assert all(p.is_absolute() for p in [args.node,args.node_modules,args.report_dir])
    args.report_dir.mkdir(parents=True,exist_ok=True)
    executable=owned.validate_binary_directory(Path('C:/Program Files/MySQL/MySQL Server 8.2/bin'))
    with owned.owned_runtime() as runtime:
        env={k:v for k,v in os.environ.items() if not k.startswith(('MYSQL','REMOTEWORK')) and k not in ('DATABASE_URL','SECRET_KEY')}
        env['PYTHON_DOTENV_DISABLED']='1';os.environ.clear();os.environ.update(env)
        with owned.OwnedMySQL(executable,runtime,env) as mysql:
            sys.path.insert(0,str(ROOT/'RemoteWork.Server'))
            from remotework import create_app
            from remotework.auth import create_user
            from remotework.db import init_schema,transaction
            from werkzeug.serving import make_server
            config={'DATABASE_URL':mysql.url,'SECRET_KEY':'synthetic-browser-chat-'+ 'x'*40,'TESTING':True,'ENABLE_CHAT_DRAFT':False}
            common=create_app(config)
            try:
                with common.app_context():
                    init_schema()
                    with transaction() as db:
                        for login,password,name,number,department,role in [
                            ('admin1','Admin-test-2026!','관리자','A1','운영팀','admin'),
                            ('staff1','Staff-test-2026!','직원','E1','개발팀','employee'),
                            ('peer1','Peer-test-2026!','동료','E2','디자인팀','employee')]:
                            create_user(db,dict(login_id=login,password=password,display_name=name,employee_no=number,department=department,role=role))
                sql='\n'.join(line for line in (ROOT/'RemoteWork.Server/migrations/005_chat_storage.mysql.sql').read_text(encoding='utf-8-sig').splitlines() if not line.lstrip().startswith('--'))
                with common.extensions['db_engine'].begin() as db:
                    for statement in sql.split(';'):
                        if statement.strip():db.exec_driver_sql(statement)
            finally:common.extensions['db_engine'].dispose()
            config['ENABLE_CHAT_DRAFT']=True;app=create_app(config)
            # Explicit test fixture for the real >100-message web cursor path.
            import uuid
            from sqlalchemy import select
            from remotework.models import User
            from remotework.features.chat.models import ChatRoom,ChatMember,ChatMessage
            with app.app_context(),transaction() as db:
                staff=db.scalar(select(User).where(User.login_id=='staff1'))
                admin=db.scalar(select(User).where(User.login_id=='admin1'))
                room_id=str(uuid.uuid4())
                db.add(ChatRoom(id=room_id,title='페이지 검증',is_group=False,created_by=staff.id));db.flush()
                for user in [staff,admin]:db.add(ChatMember(room_id=room_id,user_id=user.id,display_name=user.display_name,department=user.department))
                db.add_all([ChatMessage(message_id=str(uuid.uuid4()),room_id=room_id,sender_id=staff.id,sender_name=staff.display_name,content=f'이전 메시지 {i}') for i in range(101)])
            server=make_server('127.0.0.1',0,app,threaded=True);thread=threading.Thread(target=server.serve_forever,daemon=True)
            thread.start()
            try:
                test_env=env|{'WEB_CHAT_TEST_URL':f'http://127.0.0.1:{server.server_port}','WEB_CHAT_TEST_REPORT':str(args.report_dir),'NODE_PATH':str(args.node_modules)}
                if args.browser:
                    assert args.browser.is_absolute() and args.browser.is_file()
                    test_env['WEB_CHAT_BROWSER']=str(args.browser)
                result=subprocess.run([str(args.node),str(ROOT/'Tests/WebChat.Tests/browser_check.cjs')],cwd=ROOT,env=test_env,timeout=180)
            finally:
                server.shutdown();server.server_close();thread.join(timeout=10);app.extensions['db_engine'].dispose()
                assert not thread.is_alive()
        assert mysql.clean
    print('Owned server/MySQL stopped and temporary DB/runtime removed; raw005 unchanged; shared server/SQLite unused.')
    return result.returncode


if __name__=='__main__':raise SystemExit(main())
