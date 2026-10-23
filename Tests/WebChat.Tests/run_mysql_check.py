"""Owned MySQL regression suite. Never falls back to SQLite or a user DB."""
from pathlib import Path
import os
import sys

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tests/TaskQueries.Http.Tests'))
import run_mysql_http_check as owned


def main():
    executable=owned.validate_binary_directory(Path('C:/Program Files/MySQL/MySQL Server 8.2/bin'))
    with owned.owned_runtime() as runtime:
        env={k:v for k,v in os.environ.items() if not k.startswith(('MYSQL','REMOTEWORK')) and k not in ('DATABASE_URL','SECRET_KEY')}
        env['PYTHON_DOTENV_DISABLED']='1'
        with owned.OwnedMySQL(executable,runtime,env) as mysql:
            os.environ.clear();os.environ.update(env)
            os.environ['MYSQL_TEST_URL']=mysql.url.render_as_string(hide_password=False)
            sys.path.insert(0,str(ROOT/'RemoteWork.Server'))
            import pytest
            result=pytest.main(['-q',str(ROOT/'RemoteWork.Server/tests/test_web_chat.py'),
                str(ROOT/'RemoteWork.Server/tests/test_chat_storage.py'),str(ROOT/'RemoteWork.Server/tests/test_chat_participants.py'),
                '-k','not cold_process and not explicit_config_false and not explicit_cli',
                '--tb=short','-p','no:cacheprovider'])
            os.environ.pop('MYSQL_TEST_URL',None)
        assert mysql.clean
    print('Owned MySQL stopped; test DB/runtime removed. SQLite and shared DB not used.')
    return result


if __name__=='__main__':raise SystemExit(main())
