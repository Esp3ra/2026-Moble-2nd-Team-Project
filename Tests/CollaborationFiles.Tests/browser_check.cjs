const {chromium}=require('playwright'),assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
(async()=>{
 const base=process.env.FILES_TEST_URL,output=process.env.FILES_TEST_REPORT;fs.mkdirSync(output,{recursive:true});
 const browser=await chromium.launch({headless:true,executablePath:process.env.FILES_TEST_BROWSER});let checks=0;const errors=[],contexts=[];
 const pass=label=>{checks++;console.log('PASS',label);};
 try{
  const pages=[];
  for(const [id,pw] of [['admin1','Admin-test-2026!'],['staff1','Staff-test-2026!']]){
   const context=await browser.newContext({acceptDownloads:true,viewport:{width:1440,height:1000}});contexts.push(context);
   const page=await context.newPage();pages.push(page);page.on('pageerror',e=>errors.push(e.message));
   await page.goto(base+'/login');await page.locator('[name=login_id]').fill(id);await page.locator('[name=password]').fill(pw);await page.locator('button[type=submit]').click();
  }
  const [admin,staff]=pages;
  await admin.goto(base+'/notices/new');
  const vectors=[0,1,3,55,56,63,64,65,127,128,16384,49153,1000000].map(size=>crypto.randomBytes(size));
  for(const bytes of vectors){const actual=await admin.evaluate(async values=>(await import('/static/resource_attachments.js')).sha256(new Uint8Array(values)),Array.from(bytes));assert.equal(actual,crypto.createHash('sha256').update(bytes).digest('hex'));}
  pass('LAN-compatible SHA-256 matches Node crypto across padding/chunk/1MiB vectors');
  const content=Buffer.alloc(49153,251),filename='웹 한글 + 자료.txt';
  await admin.locator('#title').fill('브라우저 공지 첨부');await admin.locator('#content').fill('웹 실제 파일 전송');
  await admin.locator('#notice-files').setInputFiles({name:filename,mimeType:'text/plain',buffer:content});
  await admin.locator('[data-file-status]').filter({hasText:filename}).waitFor();
  await admin.locator('form[data-notice-attachments] button[type=submit]').click();await admin.waitForURL(/\/notices\?choice=/);pass('notice browser upload chunks + same-TX create');
  const id=new URL(admin.url()).searchParams.get('choice');await staff.goto(base+`/notices?selected=${id}`);
  const button=staff.locator('[data-notice-downloads] button').filter({hasText:filename});await button.waitFor();
  const download=await Promise.all([staff.waitForEvent('download'),button.click()]);const saved=path.join(output,'notice-download.txt');await download[0].saveAs(saved);assert.deepEqual(fs.readFileSync(saved),content);pass('other cookie session notice download validates exact bytes');
  await staff.reload();await staff.locator('[data-notice-downloads] button').filter({hasText:filename}).waitFor();pass('notice attachment survives reload');
  await admin.goto(base+'/chat');await staff.goto(base+'/chat');
  for(const page of pages){await page.locator('.chat-room-button').filter({hasText:'첨부 검증방'}).click();await page.locator('#chat-content:not([disabled])').waitFor();}
  // C# message/file from preceding actual client test is visible in the web.
  const fromClient=staff.locator('.chat-message').filter({hasText:'C# 채팅 첨부'});await fromClient.locator('button').first().click();
  await fromClient.locator('button').filter({hasText:'한글 + 자료.txt'}).waitFor();pass('web consumes a desktop-created message attachment');
  await admin.locator('#chat-file-picker').setInputFiles({name:filename,mimeType:'text/plain',buffer:content});
  await admin.locator('#chat-file-status').filter({hasText:filename}).waitFor();await admin.locator('#chat-content').fill('웹 채팅 첨부 전송');
  await admin.locator('#chat-send-button').click();const item=staff.locator('.chat-message').filter({hasText:'웹 채팅 첨부 전송'});await item.waitFor();await item.locator('button').first().click();
  const chatButton=item.locator('button').filter({hasText:filename});await chatButton.waitFor();const cd=await Promise.all([staff.waitForEvent('download'),chatButton.click()]);const savedChat=path.join(output,'chat-download.txt');await cd[0].saveAs(savedChat);assert.deepEqual(fs.readFileSync(savedChat),content);pass('web chat upload/send/poll and other participant download');
  // R78-01: original handler, real browser + transport/MySQL, injected loss
  // of B's committed chunk response. C has not started when B fails.
  const abc=['A','B','C'].map(n=>({name:`R78 ${n}.txt`,mimeType:'text/plain',buffer:Buffer.from(`R78 ${n} bytes`)}));
  let bId=null,failB=true;const starts=new Map(),partialPosts=[];
  await admin.route('**/results/data/shared/resources/chat/uploads/**',async route=>{
    const req=route.request(),url=new URL(req.url());
    if(req.method()==='POST'&&url.pathname.endsWith('/start')){
      const response=await route.fetch(),data=await response.json();const name=data.data.name;
      starts.set(name,(starts.get(name)||0)+1);if(name==='R78 B.txt')bId=data.data.attachment_id;
      await route.fulfill({response});
    }else if(failB&&bId&&url.pathname.endsWith(`/${bId}/chunks`)){
      failB=false;await route.fetch();await route.abort('failed');
    }else await route.continue();
  });
  await admin.route('**/chat/data/rooms/*/messages',async route=>{
    if(route.request().method()==='POST')partialPosts.push(new URLSearchParams(route.request().postData()).get('payload'));
    await route.continue();
  });
  await admin.locator('#chat-content').fill('R78 ABC 복구');await admin.locator('#chat-file-picker').setInputFiles(abc);
  await admin.waitForFunction(()=>document.querySelector('#chat-attach').disabled===false&&document.querySelector('#chat-file-status').textContent.includes('R78 C.txt'));
  assert.equal(starts.get('R78 A.txt'),1);assert.equal(starts.get('R78 B.txt'),1);assert.equal(starts.has('R78 C.txt'),false);
  assert.equal(await admin.locator('#chat-send-button').isDisabled(),true);
  await admin.locator('#chat-file-picker').setInputFiles(abc[1]);
  await admin.waitForFunction(()=>document.querySelector('#chat-attach').disabled===false&&document.querySelector('#chat-file-status').textContent.includes('R78 B.txt (완료)'));
  assert.equal(await admin.locator('#chat-send-button').isDisabled(),true);assert.equal(starts.has('R78 C.txt'),false);assert.equal(starts.get('R78 B.txt'),1);
  await admin.locator('#chat-content').press('Enter');assert.equal(partialPosts.length,0);
  pass('R78 A success/B failure/C untried then B-only retry keeps C and blocks send/Enter');
  await admin.locator('#chat-file-picker').setInputFiles(abc[2]);
  await admin.waitForFunction(()=>document.querySelector('#chat-send-button').disabled===false);
  await admin.locator('#chat-send-button').click();await admin.waitForFunction(()=>document.querySelector('#chat-content').value==='');
  assert.equal(partialPosts.length,1);const original=JSON.parse(partialPosts[0]);assert.equal(original.attachment_ids.length,3);assert.equal(new Set(original.attachment_ids).size,3);
  const recovered=staff.locator('.chat-message').filter({hasText:'R78 ABC 복구'});await recovered.waitFor();await recovered.locator('button').first().click();
  await recovered.locator('button').filter({hasText:'R78 C.txt'}).waitFor();assert.equal(await recovered.locator('button').count(),4);
  pass('R78 C completion sends original ABC once and real DB references are visible to recipient');
  await admin.unroute('**/results/data/shared/resources/chat/uploads/**');await admin.unroute('**/chat/data/rooms/*/messages');
  // Explicit user removal, rather than a retry, may remove an unfinished file.
  await admin.route('**/results/data/shared/resources/chat/uploads/**/chunks',route=>route.abort('failed'));
  await admin.locator('#chat-file-picker').setInputFiles({name:'R78 제거.txt',mimeType:'text/plain',buffer:Buffer.from('explicit removal')});
  await admin.waitForFunction(()=>document.querySelector('#chat-attach').disabled===false&&document.querySelector('#chat-file-status').textContent.includes('R78 제거.txt'));
  assert.equal(await admin.locator('#chat-send-button').isDisabled(),true);
  await admin.locator('#chat-file-status span').filter({hasText:'R78 제거.txt'}).locator('button').click();
  await admin.locator('#chat-content').fill('선택 제거 후 입력');assert.equal(await admin.locator('#chat-send-button').isDisabled(),false);
  await admin.locator('#chat-content').fill('');await admin.unroute('**/results/data/shared/resources/chat/uploads/**/chunks');
  pass('R78 explicit selection removal restores compose without deleting server files');
  // Lose only the original message success response; polling must recover that
  // UUID and its file set, without an extra business POST or orphan reupload.
  await admin.locator('#chat-file-picker').setInputFiles({name:'응답유실.txt',mimeType:'text/plain',buffer:Buffer.from('lost attachment')});await admin.locator('#chat-file-status').filter({hasText:'응답유실.txt'}).waitFor();
  let posts=0;await admin.route('**/chat/data/rooms/*/messages',async route=>{if(route.request().method()==='POST'){posts++;await route.fetch();await route.abort('failed');}else await route.continue();});
  await admin.locator('#chat-content').fill('첨부 응답 유실');await admin.locator('#chat-send-button').click();await admin.locator('#chat-content').filter({hasText:''}).waitFor();
  await staff.locator('.chat-message').filter({hasText:'첨부 응답 유실'}).waitFor();await admin.waitForFunction(()=>document.querySelector('#chat-content').value==='');assert.equal(posts,1);pass('lost committed message response recovered by reads; one UUID/one POST');await admin.unroute('**/chat/data/rooms/*/messages');
  // Preserve other participant file even when sender hides their own list.
  admin.once('dialog',dialog=>dialog.accept());await admin.locator('#chat-hide').click();await admin.locator('#chat-room-title').filter({hasText:'대화방을 선택'}).waitFor();
  await item.locator('button').first().click();await chatButton.waitFor();pass('personal hide retains recipient access');
  await staff.screenshot({path:path.join(output,'chat-files-desktop.png'),fullPage:true});await staff.setViewportSize({width:390,height:844});await staff.screenshot({path:path.join(output,'chat-files-mobile.png'),fullPage:true});
  assert.equal(await staff.evaluate(()=>document.documentElement.scrollWidth<=innerWidth+1),true);assert.deepEqual(errors,[]);pass('desktop/mobile and no page script errors');
  await admin.goto(base+`/notices/${id}/delete`);
  await admin.locator('form[data-notice-write] button[type=submit]').click();
  await admin.waitForURL(/\/notices$/);
  pass('D3 actual cookie/CSRF delete form commits attached notice deletion');
  const gone=await staff.goto(base+`/notices?selected=${id}`);
  assert.equal(gone.status(),200);
  assert.equal(await staff.locator(`tr[data-notice-id="${id}"]`).count(),0);
  assert.equal(await staff.locator('[data-notice-downloads] button').count(),0);
  pass('D3 deleted attached notice no longer accessible to recipient');
  fs.writeFileSync(path.join(output,'results.json'),JSON.stringify({passed:checks,pageErrors:errors},null,2));
 }finally{for(const context of contexts)await context.close();await browser.close();}
})().catch(e=>{console.error(e);process.exitCode=1;});
