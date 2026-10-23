// Member 3's scoped cookie transport. No Bearer, browser persistence or auto POST retries.
// SHA-256 also runs on the agreed LAN HTTP origin where SubtleCrypto is absent.
export function sha256(bytes) {
  const k = [0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,0xf40e3585,0x106aa070,0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2];
  const h = [0x6a09e667,0xbb67ae85,0x3c6ef372,0xa54ff53a,0x510e527f,0x9b05688c,0x1f83d9ab,0x5be0cd19];
  const data = new Uint8Array(Math.ceil((bytes.length + 9) / 64) * 64); data.set(bytes); data[bytes.length] = 128;
  const view = new DataView(data.buffer), bits = bytes.length * 8;
  view.setUint32(data.length - 8, Math.floor(bits / 4294967296)); view.setUint32(data.length - 4, bits >>> 0);
  const r = (x,n) => (x >>> n) | (x << (32-n)), w = new Uint32Array(64);
  for (let block = 0; block < data.length; block += 64) {
    for (let i=0;i<16;i++) w[i] = view.getUint32(block + i*4);
    for (let i=16;i<64;i++) { const x=w[i-15], y=w[i-2]; w[i] = (w[i-16]+(r(x,7)^r(x,18)^(x>>>3))+w[i-7]+(r(y,17)^r(y,19)^(y>>>10)))>>>0; }
    let [a,b,c,d,e,f,g,t] = h;
    for (let i=0;i<64;i++) { const p=(t+(r(e,6)^r(e,11)^r(e,25))+((e&f)^(~e&g))+k[i]+w[i])>>>0;
      const q=((r(a,2)^r(a,13)^r(a,22))+((a&b)^(a&c)^(b&c)))>>>0;
      t=g;g=f;f=e;e=(d+p)>>>0;d=c;c=b;b=a;a=(p+q)>>>0; }
    [a,b,c,d,e,f,g,t].forEach((v,i)=>h[i]=(h[i]+v)>>>0);
  }
  return h.map(v=>v.toString(16).padStart(8,'0')).join('');
}

export class ResourceFiles {
  constructor(root) { this.scope = root.dataset.attachmentSession; this.csrf = root.dataset.csrf; this.pending = new Map(); }
  async api(path, body) {
    const options = { credentials: 'same-origin', cache: 'no-store', redirect: 'error', headers: { 'X-Results-Session': this.scope } };
    // URLSearchParams percent-encodes '+'/'/' in base64 and can exceed the
    // existing 32KiB common cap. Multipart fields preserve the 16KiB protocol.
    if (body !== undefined) { options.method='POST'; options.body=new FormData(); options.body.set('csrf_token',this.csrf); options.body.set('body',JSON.stringify(body)); }
    const response = await fetch('/results/data/shared/resources/'+path,options);
    let data; try { data=await response.json(); } catch { throw new Error('첨부 응답을 확인하지 못했습니다. 입력·첨부를 유지합니다.'); }
    if (!response.ok || !data.ok) { const error=new Error(data.error?.message || '첨부 전송 실패'); error.status=response.status; throw error; }
    return data.data;
  }
  async upload(kind, context, file) {
    if (file.size > 26214400) throw new Error('파일 하나는 25MiB 이하여야 합니다.');
    const bytes = new Uint8Array(await file.arrayBuffer()), hash=sha256(bytes), root=`${kind}/uploads`;
    const key=JSON.stringify([kind,context,file.name,file.size,hash]);
    let meta=this.pending.get(key);
    if (!meta) { meta=await this.api(root+'/start',{name:file.name,size:bytes.length,sha256:hash,context_id:context}); this.pending.set(key,meta); }
    if (meta.size!==bytes.length || meta.sha256!==hash || meta.name!==file.name) throw new Error('업로드 정보가 일치하지 않습니다.');
    const path=`${root}/${meta.attachment_id}`, state=await this.api(path);
    if (state.state==='ready') return meta;
    if (state.state!=='uploading' || !Array.isArray(state.received_offsets)) throw new Error('첨부 전송 상태를 확인해 주세요.');
    const received=new Set(state.received_offsets);
    for(let offset=0;offset<bytes.length;offset+=16384) {
      if(received.has(offset)) continue;
      await this.api(path+'/chunks',{offset,data:btoa(String.fromCharCode(...bytes.subarray(offset,offset+16384)))});
    }
    await this.api(path+'/complete',{}); return meta;
  }
  list(kind,resource) { return this.api(`${kind}/${encodeURIComponent(resource)}/files`).then(d=>d.items); }
  async download(kind,resource,file) {
    if (!Number.isInteger(file.size) || file.size<0 || file.size>26214400 || !/^[a-f0-9]{64}$/.test(file.sha256)) throw new Error('첨부 정보를 확인해 주세요.');
    const bytes=new Uint8Array(file.size); let offset=0;
    while(true) {
      const part=await this.api(`${kind}/${encodeURIComponent(resource)}/files/${file.attachment_id}/chunks?offset=${offset}`);
      const content=Uint8Array.from(atob(part.data),c=>c.charCodeAt(0));
      if(part.attachment_id!==file.attachment_id || part.offset!==offset || content.length!==Math.min(16384,file.size-offset) || part.next_offset!==offset+content.length || part.eof!==(part.next_offset===file.size)) throw new Error('첨부 조각이 일치하지 않습니다.');
      bytes.set(content,offset);offset=part.next_offset;if(part.eof)break;
    }
    if(sha256(bytes)!==file.sha256) throw new Error('SHA-256 검증에 실패했습니다. 파일을 저장하지 않았습니다.');
    const url=URL.createObjectURL(new Blob([bytes])), link=document.createElement('a');
    link.href=url;link.download=file.name;link.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
  }
  async renderDownloads(container,kind,resource) {
    container.textContent='첨부 목록 조회 중…';
    try {
      const files=await this.list(kind,resource);container.replaceChildren();
      if(!files.length) container.textContent='연결된 첨부파일이 없습니다.';
      for(const file of files) {
        const button=document.createElement('button');button.type='button';button.textContent=`${file.name} (${file.size.toLocaleString()} bytes) ↓`;
        button.addEventListener('click',async()=>{button.disabled=true;try{await this.download(kind,resource,file);}catch(e){alert(e.message);}finally{button.disabled=false;}});
        container.append(button);
      }
    } catch(e) {container.textContent=e.message;}
  }
}

if(typeof document!=='undefined') {
  const form=document.querySelector('form[data-notice-attachments]');
  if(form && !form.hasAttribute('data-notice-recovery')) {
    const transport=new ResourceFiles(form), picker=form.querySelector('[data-file-picker]'), status=form.querySelector('[data-file-status]'), field=form.querySelector('[name=attachment_ids]');
    const uploaded=[], unresolved=new Set();let uploading=false,unknown=false,uploadError=false;
    const identity=f=>JSON.stringify([f.name,f.size,f.lastModified]);
    picker.addEventListener('change',async()=>{
      if(uploading||unknown)return;
      const files=Array.from(picker.files);if(uploaded.length+files.length>10){status.textContent='최대 10개까지 첨부해 주세요.';return;}
      for(const file of files)unresolved.add(identity(file));
      uploading=true;form.querySelector('button[type=submit]').disabled=true;picker.disabled=true;
      try {
        for(const file of files) {status.textContent=`${file.name} 전송 중…`;const meta=await transport.upload('notice',null,file);if(!uploaded.some(x=>x.attachment_id===meta.attachment_id))uploaded.push(meta);field.value=JSON.stringify(uploaded.map(f=>f.attachment_id));unresolved.delete(identity(file));}
        uploadError=unresolved.size>0;status.textContent=uploaded.map(f=>f.name).join(' · ')+(uploadError?' · 실패한 원래 파일을 다시 선택해 주세요.':'');
      } catch(e){uploadError=true;status.textContent=e.message+' 같은 파일을 다시 선택하면 전송 상태를 확인합니다. 첨부 전송 완료 전에는 등록할 수 없습니다.';}
      finally{uploading=false;picker.disabled=false;picker.value='';form.querySelector('button[type=submit]').disabled=uploadError;}
    });
    form.addEventListener('submit',async event=>{
      event.preventDefault();if(uploading||unknown||uploadError)return;
      unknown=true;const button=form.querySelector('button[type=submit]');button.disabled=true;picker.disabled=true;
      try {
        const response=await fetch(form.action,{method:'POST',credentials:'same-origin',body:new FormData(form)});
        if(response.redirected && response.url.includes('/notices')) {location.assign(response.url);return;}
        if(response.status>=400 && response.status<500){unknown=false;status.textContent='등록 입력·권한을 확인해 주세요. 입력과 첨부는 유지합니다.';}
        else status.textContent='등록 결과를 확인하지 못했습니다. 중복 등록을 막았습니다. 목록에서 먼저 확인해 주세요.';
      }catch{status.textContent='통신 오류로 등록 결과가 불명확합니다. 첨부를 유지합니다. 목록에서 먼저 확인해 주세요.';}
      finally{button.disabled=unknown;picker.disabled=unknown;}
    });
  }
  for(const box of document.querySelectorAll('[data-notice-downloads]')) new ResourceFiles(box).renderDownloads(box,'notice',box.dataset.noticeDownloads);
}
