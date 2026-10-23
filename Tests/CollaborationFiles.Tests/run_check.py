"""Member 4 product adapters + C#/browser over TCP, owned MySQL/raw DDL only."""
from pathlib import Path
import argparse,os,subprocess,sys,threading,json,uuid,base64,hashlib
from urllib.request import Request,urlopen
from urllib.error import HTTPError
ROOT=Path(__file__).resolve().parents[2]
sys.path[:0]=[str(ROOT/'Tests/TaskQueries.Http.Tests'),str(ROOT/'RemoteWork.Server')]
import run_mysql_http_check as owned

def run(args,mysql,runtime,env):
 from remotework import create_app
 from remotework.auth import create_user
 from remotework.db import init_schema,transaction
 from remotework.models import Base,User,AuditEvent,AuthSession,LoginFailure,ResourceGrant,SchemaVersion,AdminGuard
 from remotework.features.chat.models import ChatRoom,ChatMember,ChatMessage
 from remotework.features import shared_resource_attachments as resource
 from sqlalchemy import select,func
 from werkzeug.serving import make_server
 config=dict(DATABASE_URL=mysql.url,SECRET_KEY='synthetic-files-test-'+ 'x'*40,TESTING=False,
   ENABLE_NOTICE_DRAFT=True,ENABLE_CHAT_DRAFT=True,ENABLE_RESULTS_DRAFT=True,ENABLE_TASK_QUERY_DRAFT=True,ENABLE_COMMON_WRITE_DRAFT=True,
   ENABLE_SHARED_ATTACHMENTS_DRAFT=True,RESULTS_CONTEXT_MODE='local-development',RESULTS_STORAGE_ROOT=str(runtime/'files'))
 app=create_app(config)
 Base.metadata.create_all(app.extensions['db_engine'],tables=[m.__table__ for m in (User,AuthSession,LoginFailure,AuditEvent,ResourceGrant,SchemaVersion,AdminGuard)])
 with app.app_context(),transaction() as db:db.add_all([SchemaVersion(version=1),AdminGuard(id=1)])
 # Existing common tables + literal numbered migrations; no create_all shortcuts
 # for the file refs whose SQL ownership/review remains member 1/3.
 paths=['002_task_queries.mysql.sql','003_notices.mysql.sql','004_write_receipts.mysql.sql','005_chat_storage.mysql.sql','006_results.mysql.sql','013_shared_attachments.mysql.sql']
 with app.extensions['db_engine'].begin() as db:
  for path in [ROOT/'RemoteWork.Server/migrations'/n for n in paths]+[ROOT/'docs/sql/notice_chat_attachments.mysql.sql']:
   sql='\n'.join(x for x in path.read_text(encoding='utf-8-sig').splitlines() if not x.lstrip().startswith('--'))
   for statement in sql.split(';'):
    if statement.strip():db.exec_driver_sql(statement)
 ids={};room=str(uuid.uuid4())
 with app.app_context(),transaction() as db:
  for login,password,name,number,dept,role in [('admin1','Admin-test-2026!','관리자','A1','운영팀','admin'),('staff1','Staff-test-2026!','직원','E1','개발팀','employee'),('peer1','Peer-test-2026!','외부부서','E2','디자인팀','employee')]:
   u=create_user(db,dict(login_id=login,password=password,display_name=name,employee_no=number,department=dept,role=role));ids[login]=u.id
  db.add(ChatRoom(id=room,title='첨부 검증방',is_group=False,created_by=ids['admin1']));db.flush()
  for login in ['admin1','staff1']:db.add(ChatMember(room_id=room,user_id=ids[login],display_name=login,department='개발팀'))
 server=make_server('127.0.0.1',0,app,threaded=True);thread=threading.Thread(target=server.serve_forever,daemon=True);thread.start()
 base=f'http://127.0.0.1:{server.server_port}';tokens={};count=0
 def call(who,path,body=None,status=200,method=None):
  nonlocal count
  headers={'Content-Type':'application/json'}
  if who:headers['Authorization']='Bearer '+tokens[who]
  req=Request(base+'/api/v1/'+path,headers=headers,method=method or ('POST' if body is not None else 'GET'),data=None if body is None else json.dumps(body).encode())
  try:
   with urlopen(req,timeout=20) as r:code,data=r.status,json.load(r)
  except HTTPError as e:code,data=e.code,json.load(e)
  assert code==status,(path,code,status,data);count+=1;return data.get('data',{})
 try:
  for login,password in [('admin1','Admin-test-2026!'),('staff1','Staff-test-2026!'),('peer1','Peer-test-2026!')]:
   tokens[login]=call(None,'auth/login',dict(login_id=login,password=password))['access_token']
  def upload(who,kind,context,content):
   p=f'results/shared/resources/{kind}/uploads';f=call(who,p+'/start',dict(name='<한글>+.txt',size=len(content),sha256=hashlib.sha256(content).hexdigest(),context_id=context),201)
   for offset in range(0,len(content),16384):call(who,p+'/'+f['attachment_id']+'/chunks',dict(offset=offset,data=base64.b64encode(content[offset:offset+16384]).decode()))
   call(who,p+'/'+f['attachment_id']+'/complete',{});return f
  prefix='results/shared/resources/';f=upload('admin1','notice',None,b'notice payload')
  # Fail AFTER the product helper has flushed refs, at the original audit.
  # Resource row and refs must both roll back, preserving the ready temp ID.
  from unittest.mock import patch
  with patch('remotework.features.notices.routes.audit',side_effect=RuntimeError('synthetic audit rollback')):
   call('admin1','notices',dict(title='rollback after ref',content='x',target='전 직원',attachment_ids=[f['attachment_id']]),500)
  call('admin1',prefix+'notice/uploads/'+f['attachment_id'])
  with app.app_context(),transaction() as db:
   assert db.scalar(select(func.count()).select_from(resource.NoticeFile))==0;count+=1
  notice=call('admin1','notices',dict(title='HTTP 첨부',content='등록',target='개발팀',is_pinned=False,attachment_ids=[f['attachment_id']]),201)['notice']
  nid=notice['id'];call('staff1',f'{prefix}notice/{nid}/files')
  call('peer1',f'{prefix}notice/{nid}/files',status=403)
  call('staff1',f'{prefix}notice/uploads/{f["attachment_id"]}',status=403)
  call('admin1',f'notices/{nid}/delete',{},409)
  # A rejected mixed-owner/duplicate/unfinished attachment leaves no notice/audit.
  with app.app_context(),transaction() as db:before=db.scalar(select(func.count()).select_from(__import__('remotework.features.notices.models',fromlist=['Notice']).Notice))
  call('admin1','notices',dict(title='rollback',content='x',target='전 직원',attachment_ids=[str(uuid.uuid4())]),404)
  call('admin1','notices',dict(title='duplicate',content='x',target='전 직원',attachment_ids=[f['attachment_id']]*2),400)
  with app.app_context(),transaction() as db:assert db.scalar(select(func.count()).select_from(__import__('remotework.features.notices.models',fromlist=['Notice']).Notice))==before;count+=1
  cf=upload('admin1','chat',room,b'chat payload');mid=str(uuid.uuid4());body=dict(message_id=mid,content='첨부 메시지',attachment_ids=[cf['attachment_id']])
  path=f'chat/rooms/{room}/messages'
  with patch('remotework.features.chat.storage.audit',side_effect=RuntimeError('synthetic audit rollback')):call('admin1',path,body,500)
  with app.app_context(),transaction() as db:
   assert db.scalar(select(ChatMessage).where(ChatMessage.message_id==mid)) is None
   assert db.get(resource.ChatFile,(mid,cf['attachment_id'])) is None;count+=1
  call('admin1',path,body,201);call('admin1',path,body)
  call('admin1',path,body|{'attachment_ids':[]},409)
  call('staff1',f'{prefix}chat/{mid}/files');call('peer1',f'{prefix}chat/{mid}/files',status=404)
  call('admin1','notices',dict(title='cross kind',content='x',target='전 직원',attachment_ids=[cf['attachment_id']]),403)
  # Different room metadata/UUID is not a source read grant; exact resource path.
  other=str(uuid.uuid4());call('staff1',f'{prefix}chat/{other}/files/{cf["attachment_id"]}/chunks',status=404)
  # Permission recheck after file IO: revoke membership in provider's read window.
  import builtins
  original_open=Path.open
  def open_and_hide(path,*a,**kw):
   result=original_open(path,*a,**kw)
   if path.name==cf['attachment_id'] and a and a[0]=='rb':
    with app.app_context(),transaction() as db:
     from remotework.models import utcnow
     db.get(ChatMember,(room,ids['staff1'])).hidden_at=utcnow()
   return result
  with patch.object(Path,'open',open_and_hide):call('staff1',f'{prefix}chat/{mid}/files/{cf["attachment_id"]}/chunks',status=404)
  with app.app_context(),transaction() as db:db.get(ChatMember,(room,ids['staff1'])).hidden_at=None
  testenv=env|{'FILES_TEST_URL':base,'FILES_TEST_DIR':str(runtime),'FILES_TEST_ROOM':room}
  result=subprocess.run(['dotnet','run','--project',str(ROOT/'Tests/CollaborationFiles.Tests'),'-c','Release','--no-build'],env=testenv,cwd=ROOT)
  assert result.returncode==0,'C# product integration failed'
  result=subprocess.run(['dotnet','run','--project',str(ROOT/'Tests/CollaborationFiles.Tests'),'-c','Release','--no-build','--','--partial'],env=testenv,cwd=ROOT)
  assert result.returncode==0,'R78 actual C# control selection regression failed'
  if args.node:
   result=subprocess.run([str(args.node),str(ROOT/'Tests/CollaborationFiles.Tests/browser_check.cjs')],env=testenv|{'NODE_PATH':str(args.node_modules),'FILES_TEST_BROWSER':str(args.browser),'FILES_TEST_REPORT':str(args.report_dir)},cwd=ROOT,timeout=180)
   assert result.returncode==0,'browser integration failed'
  call('admin1',f'chat/rooms/{room}/hide',{})
  call('admin1',f'{prefix}chat/{mid}/files',status=404);call('staff1',f'{prefix}chat/{mid}/files')
  with app.app_context(),transaction() as db:
   assert db.scalar(select(func.count()).select_from(ChatMessage).where(ChatMessage.message_id==mid))==1
   assert db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action=='chat.message_sent',AuditEvent.resource_id==mid))==1
   assert db.get(resource.ChatFile,(mid,cf['attachment_id'])) is not None
  print(f'PASS {count} actual HTTP/MySQL member4 authority/atomicity/replay/hide checks; C#/browser counts separate')
 finally:
  server.shutdown();server.server_close();thread.join(10);app.extensions['db_engine'].dispose();assert not thread.is_alive()

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--node',type=Path);parser.add_argument('--node-modules',type=Path);parser.add_argument('--browser',type=Path);parser.add_argument('--report-dir',type=Path);args=parser.parse_args()
 env={k:v for k,v in os.environ.items() if not k.startswith(('MYSQL','REMOTEWORK')) and k not in ('DATABASE_URL','SECRET_KEY')};env['PYTHON_DOTENV_DISABLED']='1';env['PYTHONIOENCODING']='utf-8';os.environ.clear();os.environ.update(env)
 binary=owned.validate_binary_directory(Path('C:/Program Files/MySQL/MySQL Server 8.2/bin'))
 with owned.owned_runtime() as runtime:
  with owned.OwnedMySQL(binary,runtime,env) as mysql:run(args,mysql,runtime,env)
  assert mysql.clean
 print('Owned MySQL/HTTP/files cleaned; no shared DB writes or SQLite.')
if __name__=='__main__':main()
