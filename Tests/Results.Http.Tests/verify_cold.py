import sys, pathlib
sys.dont_write_bytecode=True
ROOT=pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT.parents[1]/"RemoteWork.Server"))

from remotework import create_app
from remotework.models import Base
app=create_app(dict(SECRET_KEY="test-only-cold-process-"+"x"*40,DATABASE_URL="sqlite://",
    ENABLE_TASK_QUERY_DRAFT=False,ENABLE_COMMON_WRITE_DRAFT=False,ENABLE_RESULTS_DRAFT=False))
assert not any(rule.rule.startswith("/api/v1/results") for rule in app.url_map.iter_rules())
assert not any(name.startswith("result_") for name in Base.metadata.tables)
print("PASS cold/default-off process: no results routes or models, no DB connection or schema initialization.")
