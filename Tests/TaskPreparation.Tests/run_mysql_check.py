"""Read-only W1 preparation through actual WinForms/HTTP on a newly owned MySQL.

Uses the existing owned-process fixture; never connects to configured/shared DBs.
"""
from pathlib import Path
from datetime import date
from threading import Thread
import argparse
import json
import os
import secrets
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tests/TaskQueries.Http.Tests'))
import run_mysql_http_check as owned


def run(args):
    if not args.allow_disposable_mysql:
        raise owned.shared.RunFailure('explicit --allow-disposable-mysql is required')
    if args.mysql_bin_dir is None:
        raise owned.shared.RunFailure('explicit --mysql-bin-dir is required')
    executable = owned.validate_binary_directory(args.mysql_bin_dir)
    dll = (args.client_dll or Path(__file__).parent / 'bin/Release/net8.0-windows/TaskPreparation.Tests.dll').resolve()
    if not dll.is_file():
        raise owned.shared.RunFailure('build TaskPreparation.Tests Release first')
    os.environ['PYTHON_DOTENV_DISABLED'] = '1'
    sys.path.insert(0, str(ROOT / 'RemoteWork.Server'))
    from remotework import create_app
    from remotework.auth import create_user
    from remotework.db import init_schema, get_db, transaction
    from remotework.models import User
    from remotework.features.tasks.models import TaskRecord, TaskAssignment, TaskManager
    from remotework.write_requests import WriteReceipt
    from sqlalchemy import select, func
    from waitress import create_server, wasyncore

    with owned.owned_runtime() as runtime:
        env = owned.shared.isolated_environment(runtime)
        env.pop('DATABASE_URL', None)
        with owned.OwnedMySQL(executable, runtime, env) as mysql:
            app = create_app({'TESTING': True, 'SECRET_KEY': secrets.token_hex(32),
                              'DATABASE_URL': mysql.url, 'ENABLE_COMMON_WRITE_DRAFT': True,
                              'ENABLE_TASK_QUERY_DRAFT': True, 'ENABLE_NOTICE_DRAFT': False,
                              'WRITE_REQUEST_TTL_SECONDS': 86400})
            passwords = {name: secrets.token_urlsafe(24) for name in ('admin', 'staff', 'other')}
            with app.app_context():
                init_schema()
                with transaction() as db:
                    users = {}
                    for name in passwords:
                        users[name] = create_user(db, dict(login_id=name, password=passwords[name],
                            display_name='동명이인' if name != 'admin' else '관리자', employee_no=name,
                            department='검증팀', role='admin' if name == 'admin' else 'employee'))
                    for task_id, assignee in ((101, 'staff'), (102, 'other')):
                        db.add(TaskRecord(task_id=task_id, title='조회 전용 검증', description='', task_status='not_started',
                            start_date=date(2026, 10, 17), due_date=date(2026, 10, 20),
                            current_assignment_revision=1, created_by=users['admin'].id))
                        db.flush()
                        db.add(TaskAssignment(task_id=task_id, revision=1, assignee_id=users[assignee].id,
                                              assigned_by=users['admin'].id))
                        db.add(TaskManager(task_id=task_id, user_id=users['admin'].id))
            def snapshot():
                with app.app_context():
                    db = get_db()
                    return {table.name: db.execute(select(*table.columns).order_by(*table.primary_key.columns)).all()
                            for table in (TaskRecord.__table__, TaskAssignment.__table__, TaskManager.__table__)}
            before = snapshot()
            sockets = {}
            server = create_server(app, host='127.0.0.1', port=0, threads=4, map=sockets, asyncore_loop_timeout=.1)
            thread = Thread(target=server.run, daemon=True)
            thread.start()
            try:
                settings = dict(url=f'http://127.0.0.1:{server.effective_port}',
                    admin_password=passwords['admin'], staff_password=passwords['staff'])
                # The prebuilt DLL runs in this dotnet process; run() owns timeout/kill/wait.
                child = subprocess.run(['dotnet', str(dll), '--live'], input=json.dumps(settings),
                    capture_output=True, text=True, encoding='utf-8', env=env, timeout=60,
                    creationflags=subprocess.CREATE_NO_WINDOW)
                if child.returncode:
                    raise owned.shared.RunFailure('C# preparation live checks failed (child output omitted)')
                print(child.stdout.strip(), flush=True)
                assert snapshot() == before, 'Preparation unexpectedly changed task relationships'
                with app.app_context():
                    assert get_db().scalar(select(func.count()).select_from(WriteReceipt)) == 0
                print('PASS database preservation: task/assignment/manager rows unchanged; no write receipts.', flush=True)
            finally:
                server.task_dispatcher.shutdown()
                wasyncore.close_all(map=sockets)
                thread.join(timeout=10)
                app.extensions['db_engine'].dispose()
                if thread.is_alive():
                    raise owned.shared.RunFailure('owned HTTP server did not stop')
        assert mysql.clean
    assert not runtime.exists()
    print('CLEANUP: owned HTTP/MySQL stopped; owned database and temporary runtime removed.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--allow-disposable-mysql', action='store_true')
    parser.add_argument('--mysql-bin-dir', type=Path)
    parser.add_argument('--client-dll', type=Path)
    try:
        run(parser.parse_args())
    except Exception as error:
        print('FAIL: ' + type(error).__name__ + '; credentials and child output omitted.', file=sys.stderr)
        sys.exit(1)
