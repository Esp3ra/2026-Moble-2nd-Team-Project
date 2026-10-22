"""Member1 physical 015 checks, explicit disposable MySQL only."""
from pathlib import Path
import argparse, importlib.util, os, re, subprocess, sys, tempfile, uuid

ROOT=Path(__file__).resolve().parents[2]
sys.path[:0]=[str(ROOT/'RemoteWork.Server'),str(ROOT/'Tests/TaskQueries.Http.Tests')]
import run_mysql_http_check as owned
from sqlalchemy import create_engine,inspect,text
from sqlalchemy.orm import Session
from remotework import create_app
from remotework.models import Base
from remotework.common import AppError
from remotework.resource_attachment_schema import require_resource_attachment_schema as require, TABLES

COUNT=0
def check(value):
    global COUNT
    assert value
    COUNT+=1

def ddl(engine):
    raw=(ROOT/'RemoteWork.Server/migrations/015_notice_chat_attachments.mysql.sql').read_text(encoding='utf-8-sig')
    script='\n'.join(l for l in raw.splitlines() if not l.lstrip().startswith('--'))
    with engine.begin() as c:
        for statement in script.split(';'):
            if statement.strip():c.exec_driver_sql(statement)

def bad(engine,app):
    with app.app_context(),Session(engine) as db:
        try:require(db,optional=True)
        except AppError as error:check((error.status,error.code)==(503,'RESOURCE_ATTACHMENT_SCHEMA_REQUIRED'))
        else:raise AssertionError('Malformed storage accepted')

def restore_check(e, app, directory):
    """Empty resource tables + synthetic schema1, separate owned restore DB."""
    with e.begin() as c:c.execute(text('INSERT INTO schema_versions(version,applied_at) VALUES (1,UTC_TIMESTAMP(6))'))
    def snapshot(engine):
        with engine.connect() as c:
            names=inspect(c).get_table_names()
            canonical=lambda v:re.sub(r' CHARACTER SET utf8mb4(?= COLLATE utf8mb4_[a-z0-9_]+)','',v)
            return ({n:canonical(c.exec_driver_sql('SHOW CREATE TABLE `'+n+'`').one()[1]) for n in names},
                    {n:list(c.exec_driver_sql('SELECT * FROM `'+n+'`')) for n in names})
    before=snapshot(e)
    q=lambda v:'"'+str(v).replace('\\','\\\\').replace('"','\\"')+'"'
    config=directory/'client.private.cnf'
    config.write_text('[client]\nprotocol=TCP\nhost='+q(e.url.host)+'\nport='+str(e.url.port)+'\nuser='+q(e.url.username)+'\npassword='+q(e.url.password)+'\n',encoding='utf-8')
    binary=Path('C:/Program Files/MySQL/MySQL Server 8.2/bin')
    result=owned.run_owned_client([str(binary/'mysqldump.exe'),'--defaults-extra-file='+str(config),
        '--single-transaction','--no-tablespaces','--set-gtid-purged=OFF','--column-statistics=0',e.url.database],
        stdin=subprocess.DEVNULL,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=30)
    check(result.returncode==0)
    (directory/'schema.private.sql').write_bytes(result.stdout)
    restored_name='remote_work_test_restore_'+uuid.uuid4().hex
    with e.begin() as c:c.exec_driver_sql('CREATE DATABASE '+restored_name+' CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci')
    with (directory/'schema.private.sql').open('rb') as source:
        result=owned.run_owned_client([str(binary/'mysql.exe'),'--defaults-extra-file='+str(config),'--default-character-set=utf8mb4',restored_name],
            stdin=source,capture_output=True,creationflags=subprocess.CREATE_NO_WINDOW,timeout=30)
    check(result.returncode==0)
    restored=create_engine(e.url.set(database=restored_name),hide_parameters=True)
    try:
        check(snapshot(restored)==before)
        with app.app_context(),Session(restored) as db:check(require(db) is True)
    finally:restored.dispose()


def run(url,peer,directory):
    app=create_app(dict(TESTING=True,DATABASE_URL=url,SECRET_KEY='schema-owned-test-'+('x'*32),
        ENABLE_TASK_QUERY_DRAFT=True,ENABLE_COMMON_WRITE_DRAFT=True,ENABLE_RESULTS_DRAFT=True,
        ENABLE_SHARED_ATTACHMENTS_DRAFT=True,ENABLE_NOTICE_DRAFT=True,ENABLE_CHAT_DRAFT=True))
    e=app.extensions['db_engine'];Base.metadata.create_all(e)
    with app.app_context(),Session(e) as db:
        check(require(db,optional=True) is False)
        try:require(db)
        except AppError as error:check(error.status==503)
        else:raise AssertionError('Missing storage accepted')
    ddl(e)
    with app.app_context(),Session(e) as db:check(require(db) is True)
    if peer:
        # Read the fixed member3 provider as a separate module; no source edits.
        spec=importlib.util.spec_from_file_location('remotework.features.member3_review_resource',peer)
        module=importlib.util.module_from_spec(spec);sys.modules[spec.name]=module;spec.loader.exec_module(module)
        with app.app_context(),Session(e) as db:
            app.extensions.pop('resource_attachment_schema_verified',None)
            check(module.require_schema(db) is True)
        fk=next(f for f in inspect(e).get_foreign_keys('result_notice_files') if f['constrained_columns']==['notice_id'])
        with e.begin() as c:
            c.exec_driver_sql('ALTER TABLE result_notice_files DROP FOREIGN KEY `'+fk['name']+'`')
            c.exec_driver_sql('ALTER TABLE result_notice_files ADD CONSTRAINT fk_review_cascade FOREIGN KEY(notice_id) REFERENCES notices(id) ON DELETE CASCADE')
        with app.app_context(),Session(e) as db:
            app.extensions.pop('resource_attachment_schema_verified',None)
            check(module.require_schema(db) is True)  # Original accepts unexpected destructive FK.
        bad(e,app)
    mutations=[
        ('DROP TABLE result_chat_files',),
        ('ALTER TABLE result_resource_uploads DROP CHECK ck_resource_upload_context',),
        ('ALTER TABLE result_resource_uploads ALTER CHECK ck_resource_upload_context NOT ENFORCED',),
        ("ALTER TABLE result_resource_uploads DROP CHECK ck_resource_upload_context",
         'ALTER TABLE result_resource_uploads ADD CONSTRAINT ck_resource_upload_context CHECK (1=1)'),
        ('ALTER TABLE result_resource_uploads DROP CHECK ck_resource_upload_context',
         "ALTER TABLE result_resource_uploads ADD CONSTRAINT ck_resource_upload_context CHECK (((HEX(resource_type)=HEX('notice') AND context_id IS NULL) OR HEX(resource_type)=HEX('chat')) AND context_id IS NOT NULL)"),
        ('ALTER TABLE result_resource_uploads MODIFY resource_type VARCHAR(17) NOT NULL',),
        ("ALTER TABLE result_resource_uploads MODIFY resource_type VARCHAR(16) NOT NULL DEFAULT 'notice'",),
        ('ALTER TABLE result_resource_uploads MODIFY context_id VARCHAR(36) NOT NULL',),
        ('CREATE INDEX ix_test_notice_parent ON result_notice_files(notice_id)',
         'ALTER TABLE result_notice_files DROP PRIMARY KEY'),
    ]
    for statements in mutations:
        with e.begin() as c:
            for name in reversed(list(TABLES)):c.exec_driver_sql('DROP TABLE IF EXISTS `'+name+'`')
        ddl(e)
        with e.begin() as c:
            for sql in statements:c.exec_driver_sql(sql)
        bad(e,app)
    with e.begin() as c:
        for name in reversed(list(TABLES)):c.exec_driver_sql('DROP TABLE IF EXISTS `'+name+'`')
    ddl(e)
    # Every FK is required and both update/delete directions must preserve parents.
    for name in TABLES:
        for original in inspect(e).get_foreign_keys(name):
            fk_name=original['name']
            with e.begin() as c:c.exec_driver_sql('ALTER TABLE `'+name+'` DROP FOREIGN KEY `'+fk_name+'`')
            bad(e,app)
            cols=','.join('`'+n+'`' for n in original['constrained_columns'])
            parents=','.join('`'+n+'`' for n in original['referred_columns'])
            parent='`'+original['referred_table']+'`'
            if original['constrained_columns']==['context_id']:
                # MySQL itself prohibits CASCADE on this CHECK-constrained column.
                with e.begin() as c:c.exec_driver_sql('ALTER TABLE `'+name+'` ADD CONSTRAINT `'+fk_name+'` FOREIGN KEY('+cols+') REFERENCES '+parent+'('+parents+')')
                continue
            with e.begin() as c:c.exec_driver_sql('ALTER TABLE `'+name+'` ADD CONSTRAINT `'+fk_name+'` FOREIGN KEY('+cols+') REFERENCES '+parent+'('+parents+') ON UPDATE CASCADE')
            bad(e,app)
            with e.begin() as c:
                c.exec_driver_sql('ALTER TABLE `'+name+'` DROP FOREIGN KEY `'+fk_name+'`')
                c.exec_driver_sql('ALTER TABLE `'+name+'` ADD CONSTRAINT `'+fk_name+'` FOREIGN KEY('+cols+') REFERENCES '+parent+'('+parents+')')
    with app.app_context(),Session(e) as db:check(require(db) is True)
    # Fixed 015 declarations agree with member3 proposal statement by statement.
    if peer:
        proposal=peer.parents[3]/'docs/sql/notice_chat_attachments.mysql.sql'
        canonical=lambda path:'\n'.join(l.strip() for l in path.read_text(encoding='utf-8-sig').splitlines() if l.strip() and not l.lstrip().startswith('--'))
        check(canonical(proposal)==canonical(ROOT/'RemoteWork.Server/migrations/015_notice_chat_attachments.mysql.sql'))
    restore_check(e,app,directory)
    e.dispose()

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--peer-source',type=Path);args=parser.parse_args()
    tempfile.tempdir='C:/Windows/Temp'
    clean={k:v for k,v in os.environ.items() if not k.startswith(('MYSQL_','REMOTEWORK_')) and k not in ('DATABASE_URL','SECRET_KEY')}
    clean['PYTHON_DOTENV_DISABLED']='1'
    with owned.owned_runtime() as runtime:
        with owned.OwnedMySQL(Path('C:/Program Files/MySQL/MySQL Server 8.2/bin/mysqld.exe'),runtime,clean) as database:
            run(database.url,args.peer_source,runtime)
    print(f'PASS {COUNT} physical schema assertions; original weak FK acceptance distinguished; owned DB/process/runtime cleaned.')
