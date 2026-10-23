"""Notice-only raw017/API/cookie recovery checks in an owned disposable MySQL."""
from pathlib import Path
import os
import argparse
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tests/TaskQueries.Http.Tests'))
import run_mysql_http_check as owned


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--legacy', action='store_true', help='Run existing notice API/web regressions in a separate owned DB')
    args=parser.parse_args()
    binary=owned.validate_binary_directory(Path('C:/Program Files/MySQL/MySQL Server 8.2/bin'))
    env={
        k:v for k,v in os.environ.items() if not k.startswith(('MYSQL','REMOTEWORK')) and k not in ('DATABASE_URL','SECRET_KEY')}
    dependency=os.environ.get('NOTICE_TEST_PYTHONPATH','')
    env.update(PYTHON_DOTENV_DISABLED='1',PYTHONIOENCODING='utf-8',
        PYTHONPATH=os.pathsep.join(filter(None,(str(ROOT/'RemoteWork.Server'),dependency))))
    with owned.owned_runtime() as runtime:
        with owned.OwnedMySQL(binary,runtime,env) as mysql:
            tests=['test_notices.py','test_notice_review.py','test_web_notices.py'] if args.legacy else ['test_notice_operations.py']
            result=subprocess.run([sys.executable,'-m','pytest','-q',
                *[str(ROOT/'RemoteWork.Server/tests'/name) for name in tests],
                '--tb=short','-p','no:cacheprovider'],cwd=ROOT,
                env=env|{'MYSQL_TEST_URL':mysql.url.render_as_string(hide_password=False)})
        assert mysql.clean
    print('Owned notice test MySQL/process/runtime cleaned; shared database untouched',flush=True)
    return result.returncode


if __name__=='__main__':raise SystemExit(main())
