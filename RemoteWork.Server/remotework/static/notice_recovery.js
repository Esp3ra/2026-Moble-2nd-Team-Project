// Notice-only original-request UI. Never persist bodies/keys or auto repeat a POST.
import { ResourceFiles } from './resource_attachments.js';

function bind(form) {
  const transport=new ResourceFiles(form), picker=form.querySelector('[data-file-picker]');
  const status=form.querySelector('[data-file-status]'), field=form.querySelector('[name=attachment_ids]');
  const publish=form.querySelector('[value=publish]'), check=form.querySelector('[value=check]');
  const ids=JSON.parse(field.value), uploaded=new Map(), unresolved=new Set();
  let busy=false, locked=form.dataset.operationLocked==='1', original=null;
  const identity=file=>JSON.stringify([file.name,file.size,file.lastModified]);
  const show=()=>{status.textContent=ids.map(id=>uploaded.get(id)?.name||id).join(' · ');};
  for(const id of ids) transport.api(`notice/uploads/${encodeURIComponent(id)}`).then(meta=>{uploaded.set(id,meta);show();}).catch(()=>{show();});
  const freeze=()=>{
    locked=true;publish.disabled=true;picker.disabled=true;
    form.querySelectorAll('input:not([type=hidden]),select,textarea').forEach(item=>{
      if(item.tagName==='SELECT'||item.type==='checkbox'||item.type==='file')item.disabled=true;else item.readOnly=true;
    });
  };
  picker.addEventListener('change',async()=>{
    if(busy||locked)return;
    const files=Array.from(picker.files);if(ids.length+files.length>10){status.textContent='최대 10개까지 첨부해 주세요.';return;}
    for(const file of files)unresolved.add(identity(file));
    busy=true;publish.disabled=check.disabled=picker.disabled=true;
    try {
      for(const file of files){const meta=await transport.upload('notice',null,file);if(!ids.includes(meta.attachment_id))ids.push(meta.attachment_id);uploaded.set(meta.attachment_id,meta);field.value=JSON.stringify(ids);unresolved.delete(identity(file));}
      show();
    }catch(error){status.textContent=error.message+' 원래 미완료 파일을 다시 선택하거나 명시적으로 새 작성 화면에서 시작해 주세요.';}
    finally{busy=false;picker.disabled=false;picker.value='';publish.disabled=unresolved.size>0;check.disabled=false;}
  });
  form.addEventListener('submit',async event=>{
    event.preventDefault();if(busy)return;
    const action=event.submitter?.value||'publish';
    if(unresolved.size){status.textContent='원래 선택한 첨부 전송을 완료한 뒤 처리 결과를 확인해 주세요.';return;}
    if(action==='publish'&&(publish.disabled||unresolved.size))return;
    const data=original ? new FormData() : new FormData(form);
    if(original)for(const [name,value] of original.entries())data.append(name,value);
    data.set('notice_action',action);
    if(action==='publish')original=data;
    busy=true;publish.disabled=check.disabled=picker.disabled=true;
    try {
      const response=await fetch(form.action,{method:'POST',credentials:'same-origin',body:data});
      if(response.redirected&&new URL(response.url).origin===location.origin&&new URL(response.url).pathname==='/notices'){location.assign(response.url);return;}
      const page=new DOMParser().parseFromString(await response.text(),'text/html');
      const replacement=page.querySelector('.notice-editor');
      if(!replacement||!replacement.querySelector('[data-notice-recovery]'))throw new Error('원 요청 응답을 확인하지 못했습니다.');
      form.closest('.notice-editor').replaceWith(replacement);bind(replacement.querySelector('[data-notice-recovery]'));
    }catch(error){freeze();check.disabled=false;status.textContent=error.message+' 입력·첨부·원 요청을 유지합니다. 처리 결과 확인을 눌러 주세요.';}
    finally{busy=false;}
  });
}

const form=document.querySelector('[data-notice-recovery]');if(form)bind(form);
