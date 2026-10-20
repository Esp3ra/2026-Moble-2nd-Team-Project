import sys, os, pathlib, tempfile
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT))
sys.path.insert(0,str(ROOT.parent/"TaskQueries.Http.Tests"))
import verify_server
import run_mysql_http_check as owned
# MySQL native initialization needs an ASCII path; the helper checks exact ownership before cleanup.
tempfile.tempdir=r"C:/Windows/Temp"
clean_env={k:v for k,v in os.environ.items() if not k.startswith("MYSQL_") and k!="DATABASE_URL"}
with owned.owned_runtime() as runtime:
    with owned.OwnedMySQL(pathlib.Path(os.environ.get("RESULTS_MYSQLD",r"C:/Program Files/MySQL/MySQL Server 8.2/bin/mysqld.exe")),runtime,clean_env) as database:
        try:
            verify_server.run(database.url,runtime/"files",run_client="--client" in sys.argv)
        finally:
            if hasattr(verify_server,"ENGINE_FOR_CLEANUP"):
                verify_server.ENGINE_FOR_CLEANUP.dispose()
print("Owned MySQL database, process and verified temporary runtime cleaned.")
