"""Native owned MySQL only; each module gets its own empty DB/RAW DDL."""
from pathlib import Path
import os,subprocess,sys
import argparse
ROOT=Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/'Tests/TaskQueries.Http.Tests'))
import run_mysql_http_check as owned

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--consumer-only',action='store_true');args=parser.parse_args()
    binary=owned.validate_binary_directory(Path('C:/Program Files/MySQL/MySQL Server 8.2/bin'))
    env={k:v for k,v in os.environ.items() if not k.startswith(('MYSQL','REMOTEWORK')) and k not in ('DATABASE_URL','SECRET_KEY')}
    env['PYTHON_DOTENV_DISABLED']='1';env['PYTHONIOENCODING']='utf-8'
    env['PYTHONPATH']=str(ROOT/'RemoteWork.Server')+os.pathsep+str(ROOT/'RemoteWork.Server/tests')
    for name in (['test_deadline_binding_consumer.py'] if args.consumer_only else ['test_deadline_http.py','test_deadline_binding_consumer.py']):
        with owned.owned_runtime() as runtime:
            with owned.OwnedMySQL(binary,runtime,env) as mysql:
                result=subprocess.run([sys.executable,'-m','pytest','-q','-s',str(ROOT/'RemoteWork.Server/tests'/name),
                    '--tb=short','-p','no:cacheprovider'],cwd=ROOT,env=env|{'MYSQL_TEST_URL':mysql.url.render_as_string(hide_password=False)})
            assert mysql.clean
        print('Owned MySQL/server runtime cleaned:',name,flush=True)
        if result.returncode:return result.returncode
    return 0

if __name__=='__main__':raise SystemExit(main())
