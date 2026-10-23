// Real two-context browser + Flask/MySQL. All accounts and messages are synthetic.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');

(async () => {
  const browser = await chromium.launch({ headless: true, ...(process.env.WEB_CHAT_BROWSER ? { executablePath: process.env.WEB_CHAT_BROWSER } : {}) });
  const base = process.env.WEB_CHAT_TEST_URL;
  assert.match(base || '', /^http:\/\/127\.0\.0\.1:\d+$/);
  const output = process.env.WEB_CHAT_TEST_REPORT;
  let checks = 0;
  const passed = name => { checks++; console.log('PASS', name); };
  const contexts = [];
  const pages = [];
  const errors = [];
  try {
    for (const [login, password] of [['staff1', 'Staff-test-2026!'], ['admin1', 'Admin-test-2026!']]) {
      const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } }); contexts.push(context);
      const page = await context.newPage(); pages.push(page);
      page.on('pageerror', e => errors.push(e.message));
      await page.goto(base + '/login');
      await page.locator('[name=login_id]').fill(login);
      await page.locator('[name=password]').fill(password);
      await page.locator('button[type=submit]').click();
      await page.goto(base + '/chat');
      await page.locator('#chat-status').filter({ hasText: '자동 수신 연결됨' }).waitFor();
    }
    const [a,b] = pages;
    passed('two separate cookie logins; chat scripts load under CSP');
    await a.locator('.chat-room-button').filter({hasText:'페이지 검증'}).click();
    await a.locator('#chat-messages p').filter({hasText:'이전 메시지 100'}).waitFor();
    assert.equal(await a.locator('#chat-messages p').count(),101);
    passed('existing history over 100 messages consumes the real string cursor');
    await a.locator('#chat-create').click();
    await a.locator('#chat-people input').first().waitFor();
    await a.locator('#chat-new-title').fill('웹 채팅 검증');
    await a.locator('#chat-people label').filter({ hasText: '관리자 · 운영팀' }).locator('input').check();
    await a.locator('#chat-room-submit').click();
    await a.locator('#chat-dialog').waitFor({ state: 'hidden' });
    await b.locator('.chat-room-button').filter({ hasText: '웹 채팅 검증' }).waitFor();
    await b.locator('.chat-room-button').filter({ hasText: '웹 채팅 검증' }).click();
    await b.locator('#chat-members').filter({ hasText: '직원' }).waitFor();
    passed('UI creates a DB room; second user receives invitation automatically');
    await a.locator('#chat-content').fill('첫 메시지'); await a.locator('#chat-content').press('Enter');
    await b.locator('#chat-messages p').filter({ hasText: '첫 메시지' }).waitFor();
    passed('Enter sends and the other browser receives without reselecting');
    await b.locator('#chat-content').fill('답장'); await b.locator('#chat-content').press('Shift+Enter');
    await b.locator('#chat-content').pressSequentially('두 번째 줄');
    await b.locator('#chat-send-button').click();
    await a.locator('#chat-messages p').filter({ hasText: '두 번째 줄' }).waitFor();
    assert.equal(await a.locator('#chat-messages p').last().textContent(), '답장\n두 번째 줄');
    passed('bidirectional message and Shift+Enter newline');
    await a.locator('#chat-content').fill('아직 안 보낸 초안');
    await b.locator('#chat-content').fill('수신 중 초안 보존'); await b.locator('#chat-send-button').click();
    await a.locator('#chat-messages p').filter({ hasText: '수신 중 초안 보존' }).waitFor();
    assert.equal(await a.locator('#chat-content').inputValue(), '아직 안 보낸 초안');
    passed('poll does not replace the unsent draft');
    await a.locator('#chat-content').fill('<img src=x onerror=alert(1)>'); await a.locator('#chat-send-button').click();
    await b.locator('#chat-messages p').filter({ hasText: '<img src=x' }).waitFor();
    assert.equal(await b.locator('#chat-messages img').count(), 0);
    passed('HTML payload is displayed as plain text');
    await a.screenshot({ path: path.join(output,'chat-desktop.png'), fullPage: true });
    await a.setViewportSize({ width: 390, height: 844 });
    assert.ok(await a.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth));
    await a.screenshot({ path: path.join(output,'chat-mobile.png'), fullPage: true });
    await a.setViewportSize({ width:1440,height:1000 });
    passed('desktop/mobile layout has no page-level horizontal overflow');

    let release, entered; const held = new Promise(resolve => { release=resolve; });
    const started = new Promise(resolve => { entered=resolve; }); let delayed=false;
    await a.route('**/chat/data/rooms/*/messages?*',async route => {
      if (!delayed) {
        delayed=true; const response=await route.fetch(); entered(); await held;
        try { await route.fulfill({response}); } catch { /* Aborted old selection is expected. */ }
      } else await route.continue();
    });
    await a.locator('#chat-refresh').click(); await started;
    await a.locator('.chat-room-button').filter({hasText:'페이지 검증'}).click(); release();
    await a.locator('#chat-messages p').filter({hasText:'이전 메시지 100'}).waitFor();
    assert.equal(await a.locator('#chat-room-title').textContent(),'페이지 검증');
    assert.equal(await a.locator('#chat-messages p').filter({hasText:'첫 메시지'}).count(),0);
    await a.unroute('**/chat/data/rooms/*/messages?*');
    await a.locator('.chat-room-button').filter({hasText:'웹 채팅 검증'}).click();
    await a.locator('#chat-messages p').filter({hasText:'첫 메시지'}).waitFor();
    passed('late read from the previous selected room cannot replace the current room');
    await a.locator('#chat-content').fill('가'.repeat(2001)); await a.locator('#chat-send-button').click();
    await a.locator('#chat-status').filter({ hasText: '1~2,000자' }).waitFor();
    passed('2001 code points rejected before send');
    await a.locator('#chat-content').fill('😀'.repeat(2000)); await a.locator('#chat-send-button').click();
    await b.locator('#chat-messages p').filter({ hasText: '😀'.repeat(2000) }).waitFor();
    passed('2000 emoji persist and arrive in the second browser');

    let drop = 'before'; const sent = [];
    await a.route('**/chat/data/rooms/*/messages', async route => {
      if (route.request().method() === 'POST') {
        const form = new URLSearchParams(route.request().postData()); const payload = JSON.parse(form.get('payload'));
        if (payload.content === '동일 요청 재시도') {
          sent.push(payload);
          if (drop === 'before') { drop = 'done'; await route.abort('failed'); return; }
        }
      }
      await route.continue();
    });
    await a.locator('#chat-content').fill('동일 요청 재시도'); await a.locator('#chat-send-button').click();
    await a.locator('#chat-send-button').filter({ hasText: '동일 요청 재전송' }).waitFor();
    await a.locator('#chat-send-button').click();
    await b.locator('#chat-messages p').filter({ hasText: '동일 요청 재시도' }).waitFor();
    assert.equal(sent.length,2); assert.deepEqual(sent[0],sent[1]);
    assert.equal(await b.locator('#chat-messages p').filter({ hasText: '동일 요청 재시도' }).count(),1);
    passed('lost uncommitted response: explicit retry keeps exact UUID/body and stores once');
    await a.unroute('**/chat/data/rooms/*/messages');

    let committedDrops = 0;
    await a.route('**/chat/data/rooms/*/messages', async route => {
      if (route.request().method() === 'POST') {
        const body = JSON.parse(new URLSearchParams(route.request().postData()).get('payload'));
        if (body.content === '저장 후 응답 유실') {
          committedDrops++; await route.fetch(); await route.abort('failed'); return;
        }
      }
      await route.continue();
    });
    await a.locator('#chat-content').fill('저장 후 응답 유실'); await a.locator('#chat-send-button').click();
    await a.locator('#chat-content').waitFor({ state: 'visible' });
    await a.waitForFunction(() => document.getElementById('chat-content').value === '');
    await b.locator('#chat-messages p').filter({ hasText: '저장 후 응답 유실' }).waitFor();
    assert.equal(committedDrops,1);
    passed('committed response lost: read recovery clears pending with no automatic POST');
    await a.unroute('**/chat/data/rooms/*/messages');

    await a.reload();
    await a.locator('.chat-room-button').filter({ hasText: '웹 채팅 검증' }).click();
    await a.locator('#chat-messages p').filter({ hasText: '첫 메시지' }).waitFor();
    passed('reload reads the same persisted conversation');

    a.once('dialog', dialog => dialog.accept()); await a.locator('#chat-hide').click();
    await a.locator('.chat-room-button').filter({ hasText: '웹 채팅 검증' }).waitFor({state:'hidden'});
    assert.equal(await b.locator('.chat-room-button').filter({ hasText: '웹 채팅 검증' }).count(),1);
    assert.ok((await b.locator('#chat-messages p').count())>=7);
    passed('hide removes only this user room; other user retains all history');

    await a.locator('#chat-create').click(); await a.locator('#chat-people input').first().waitFor();
    await a.locator('#chat-new-title').fill('부서 간 그룹'); await a.locator('#chat-kind').selectOption('group');
    await a.locator('#chat-department').selectOption('디자인팀');
    await a.locator('#chat-people label:visible input').check();
    await a.locator('#chat-department').selectOption('운영팀');
    await a.locator('#chat-people label:visible input').check();
    await a.locator('#chat-room-submit').click(); await a.locator('#chat-dialog').waitFor({state:'hidden'});
    await a.locator('#chat-members').filter({hasText:'디자인팀'}).waitFor();
    assert.equal(await a.locator('#chat-members li').count(),3);
    await a.locator('#chat-filter').selectOption('personal');
    assert.equal(await a.locator('.chat-room-button').filter({hasText:'부서 간 그룹'}).count(),0);
    await a.locator('#chat-filter').selectOption('group');
    await a.locator('.chat-room-button').filter({hasText:'부서 간 그룹'}).waitFor();
    passed('DB employee department filter; multi-user group and room-kind filter');

    // Same account, new login cookie in the same browser context: old page must stop.
    const otherTab=await contexts[1].newPage(); await otherTab.goto(base+'/login');
    await otherTab.locator('[name=login_id]').fill('admin1'); await otherTab.locator('[name=password]').fill('Admin-test-2026!');
    await otherTab.locator('button[type=submit]').click();
    await b.locator('#chat-status').filter({hasText:'로그인이 만료되거나 변경'}).waitFor({timeout:15000});
    assert.equal(await b.locator('#chat-messages p').count(),0);
    assert.equal(await b.locator('#chat-send-button').isDisabled(),true);
    passed('same-account relogin invalidates old tab scope and clears private content');
    assert.deepEqual(errors,[]); passed('no browser script errors');
    fs.writeFileSync(path.join(output,'browser-results.json'),JSON.stringify({checks,errors,actualBrowser:true,actualHttp:true,actualMySQL:true,twoPhysicalPCs:false},null,2));
    console.log(`PASS: ${checks} real browser/HTTP/MySQL scenarios`);
  } finally {
    for (const context of contexts) await context.close();
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode=1; });
