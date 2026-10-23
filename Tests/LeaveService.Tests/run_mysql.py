"""Execute original leave API tests in owned native MySQL; never user DB."""
from pathlib import Path
import os,subprocess,sys
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tests/TaskQueries.Http.Tests'))
import run_mysql_http_check as owned

def main():
    binary=owned.validate_binary_directory(Path('C:/Program Files/MySQL/MySQL Server 8.2/bin'))
    env={k:v for k,v in os.environ.items() if not k.startswith(('MYSQL','REMOTEWORK')) and k not in ('DATABASE_URL','SECRET_KEY')}
    env.update(PYTHON_DOTENV_DISABLED='1',PYTHONIOENCODING='utf-8',PYTHONPATH=str(ROOT/'RemoteWork.Server'))
    with owned.owned_runtime() as runtime:
        with owned.OwnedMySQL(binary,runtime,env) as mysql:
            result=subprocess.run([sys.executable,'-m','pytest','-q',str(ROOT/'RemoteWork.Server/tests/test_leave_service.py'),
                '--tb=short','-p','no:cacheprovider'],cwd=ROOT,env=env|{'MYSQL_TEST_URL':mysql.url.render_as_string(hide_password=False)})
        assert mysql.clean
    print('Owned MySQL/process/runtime cleaned; no shared DB or SQLite',flush=True)
    return result.returncode

if __name__=='__main__':raise SystemExit(main())
