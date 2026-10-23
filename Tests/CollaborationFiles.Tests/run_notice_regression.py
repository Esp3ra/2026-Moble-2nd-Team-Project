"""Existing notice regressions on owned native MySQL; SQLite cold probes excluded."""
from pathlib import Path
import os,sys
ROOT=Path(__file__).resolve().parents[2]
sys.path[:0]=[str(ROOT/'Tests/TaskQueries.Http.Tests'),str(ROOT/'RemoteWork.Server')]
import run_mysql_http_check as owned
def main():
 env={k:v for k,v in os.environ.items() if not k.startswith(('MYSQL','REMOTEWORK')) and k not in ('DATABASE_URL','SECRET_KEY')}
 env['PYTHON_DOTENV_DISABLED']='1';env['PYTHONIOENCODING']='utf-8';os.environ.clear();os.environ.update(env)
 binary=owned.validate_binary_directory(Path('C:/Program Files/MySQL/MySQL Server 8.2/bin'))
 with owned.owned_runtime() as runtime:
  with owned.OwnedMySQL(binary,runtime,env) as mysql:
   os.environ['MYSQL_TEST_URL']=mysql.url.render_as_string(hide_password=False)
   import pytest
   result=pytest.main(['-q',str(ROOT/'RemoteWork.Server/tests/test_notices.py'),str(ROOT/'RemoteWork.Server/tests/test_web_notices.py'),'-k','not cold_', '--tb=short','-p','no:cacheprovider'])
   os.environ.pop('MYSQL_TEST_URL',None)
  assert mysql.clean
 print('Owned MySQL cleaned; SQLite/shared DB unused.')
 return result
if __name__=='__main__':raise SystemExit(main())
