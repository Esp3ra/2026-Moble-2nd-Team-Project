"""Real member4 notice delete routes, real member3 public provider, owned MySQL."""
from pathlib import Path
from unittest.mock import patch
from sqlalchemy import select,func
from sqlalchemy.orm import Session
from remotework.db import transaction
from remotework.features.notices.models import Notice
from remotework.models import AuditEvent
from remotework.features import shared_resource_attachments as resource
from remotework.features import shared_attachments as shared

def run(app,call,upload,runtime):
 checks=0
 def check(ok,label):
  nonlocal checks
  assert ok,label
  checks+=1
 def create(ids,title='D3 real route'):
  return call('admin1','notices',dict(title=title,content='same TX',target='전 직원',attachment_ids=ids),201)['notice']['id']
 def file_state(fid):
  with app.app_context(),transaction() as db:
   f=db.get(shared.SharedFile,fid)
   return None if f is None else f.state
 def verify_present(nid,fid):
  with app.app_context(),transaction() as db:
   check(db.get(Notice,nid) is not None,'notice rollback')
   check(db.get(resource.NoticeFile,(nid,fid)) is not None,'ref rollback')
   check(db.scalar(select(func.count()).select_from(AuditEvent).where(AuditEvent.action=='notice.deleted',AuditEvent.resource_id==str(nid)))==0,'audit rollback')
  check(file_state(fid)=='ready','file ready after rollback')
 f=upload('admin1','notice',None,b'D3 independent synthetic bytes');fid=f['attachment_id'];a=create([fid]);b=create([fid],'B preserved')
 with app.app_context():path=shared.storage()/fid
 with patch('remotework.features.notices.routes.audit',side_effect=RuntimeError('D3 injected audit failure')):
  call('admin1',f'notices/{a}/delete',{},500)
 verify_present(a,fid);check(path.exists(),'bytes retained after audit failure')
 original=Session.commit
 def failing_commit(db):
  # SQLAlchemy flush can empty deleted; notice.deleted audit remains in identity_map.
  from flask import request
  if request.path.endswith(f'/notices/{a}/delete') and not getattr(__import__('flask').g,'d3_commit_failed',False):
   __import__('flask').g.d3_commit_failed=True
   raise RuntimeError('D3 injected commit failure')
  return original(db)
 with patch.object(Session,'commit',failing_commit):call('admin1',f'notices/{a}/delete',{},500)
 verify_present(a,fid);check(path.exists(),'bytes retained after commit failure')
 # Missing public reader / feature OFF must not bypass real attachment evidence.
 with patch.object(resource,'read_resource_attachment_ids',None):call('admin1',f'notices/{a}/delete',{},503)
 verify_present(a,fid)
 provider=app.extensions.pop('results_resource_attachments')
 try:call('admin1',f'notices/{a}/delete',{},503)
 finally:app.extensions['results_resource_attachments']=provider
 verify_present(a,fid)
 call('staff1',f'notices/{a}/delete',{},403)
 call('admin1',f'notices/{a}/delete',{})
 call('admin1',f'notices/{a}',status=404)
 call('staff1',f'results/shared/resources/notice/{b}/files')
 check(path.read_bytes()==b'D3 independent synthetic bytes','A delete keeps B file bytes')
 check(file_state(fid)=='ready','A delete keeps B ready')
 call('admin1',f'notices/{b}/delete',{})
 check(not path.exists() and file_state(fid) is None,'last ref physical cleanup after commit')
 # A post-commit filesystem failure leaves DB success and blocked bytes for retry.
 f=upload('admin1','notice',None,b'D3 locked bytes');fid=f['attachment_id'];a=create([fid])
 with app.app_context():path=shared.storage()/fid
 unlink=Path.unlink
 def locked(p,*args,**kw):
  if p.name==fid:raise PermissionError('D3 injected file lock')
  return unlink(p,*args,**kw)
 with patch.object(Path,'unlink',locked):call('admin1',f'notices/{a}/delete',{})
 call('admin1',f'notices/{a}',status=404)
 check(path.exists() and file_state(fid)=='deleting','DB success plus retryable deleting bytes')
 with app.app_context():removed=app.extensions['results_shared_attachments'].cleanup_shared_files()
 check(removed>=1 and not path.exists() and file_state(fid) is None,'explicit cleanup retry removes last file')
 # No provider is required for legacy filename-only notices without real-file evidence.
 a=create([],'legacy filename only');provider=app.extensions.pop('results_resource_attachments')
 try:call('admin1',f'notices/{a}/delete',{})
 finally:app.extensions['results_resource_attachments']=provider
 check(True,'legacy notice without real attachments still deletes while feature OFF')
 print(f'PASS {checks} D3 actual member4 route/provider/HTTP/MySQL assertions (injected audit/commit/IO faults)',flush=True)
