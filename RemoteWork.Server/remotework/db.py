from contextlib import contextmanager
from flask import current_app, g
from sqlalchemy import create_engine, event, select
from sqlalchemy.orm import sessionmaker
from .models import Base, SchemaVersion, AdminGuard

SCHEMA_VERSION = 1


def init_app(app):
    url = app.config["DATABASE_URL"]
    options = {"pool_pre_ping": True, "hide_parameters": True}
    if str(url).startswith("sqlite"):
        options["connect_args"] = {"check_same_thread": False}
    else:
        options["connect_args"] = {"connect_timeout": 5, "read_timeout": 10, "write_timeout": 10}
    engine = create_engine(url, **options)
    if engine.dialect.name == "sqlite":
        @event.listens_for(engine, "connect")
        def foreign_keys(conn, _):
            conn.execute("PRAGMA foreign_keys=ON")
    app.extensions["db_engine"] = engine
    app.extensions["db_factory"] = sessionmaker(bind=engine, expire_on_commit=False)

    @app.teardown_appcontext
    def close_db(_error):
        db = g.pop("db", None)
        if db is not None:
            db.close()  # Rolls back any uncommitted operation.


def get_db():
    if "db" not in g:
        g.db = current_app.extensions["db_factory"]()
    return g.db


@contextmanager
def transaction():
    """One mutation + its audit/grants, committed or rolled back together."""
    db = get_db()
    try:
        yield db
        db.commit()
    except Exception:
        db.rollback()
        raise


def init_schema():
    engine = current_app.extensions["db_engine"]
    Base.metadata.create_all(engine)
    with transaction() as db:
        existing = db.get(SchemaVersion, SCHEMA_VERSION)
        if existing is None:
            db.add(SchemaVersion(version=SCHEMA_VERSION))
        if db.get(AdminGuard, 1) is None:
            db.add(AdminGuard(id=1))


def schema_status(required_versions=(SCHEMA_VERSION,)):
    """Recorded migration evidence, not a physical-DDL or compatibility proof."""
    required = sorted(set(required_versions))
    if not required or any(type(v) is not int or v < 1 for v in required):
        raise ValueError("Explicit positive required schema versions are necessary")
    recorded = list(get_db().scalars(select(SchemaVersion.version).order_by(SchemaVersion.version)))
    return {"required_versions": required, "recorded_versions": recorded,
            "missing_versions": sorted(set(required) - set(recorded))}


def check_schema(required_versions=(SCHEMA_VERSION,)):
    if schema_status(required_versions)["missing_versions"]:
        from .common import AppError
        raise AppError(503, "DATABASE_UNAVAILABLE", "데이터베이스가 초기화되지 않았습니다. 서버의 초기화 절차를 확인해 주세요.")
