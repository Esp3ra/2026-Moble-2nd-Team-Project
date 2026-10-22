// Existing UUID chat protocol. Poll reads only; every write needs a user action.
export function requestId(crypto = globalThis.crypto) {
  if (!crypto?.getRandomValues) throw new Error('이 브라우저에서 안전한 요청 ID를 만들 수 없습니다.');
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 15) | 64; bytes[8] = (bytes[8] & 63) | 128;
  const h = Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('');
  return `${h.slice(0,8)}-${h.slice(8,12)}-${h.slice(12,16)}-${h.slice(16,20)}-${h.slice(20)}`;
}

export async function readAll(load) {
  const result = [], seen = new Set(); let after = null;
  for (let page = 0; page < 1000; page++) {
    const data = await load(after);
    if (!Array.isArray(data.items) || !('next_cursor' in data)) throw new Error('대화 조회 응답을 확인해 주세요.');
    result.push(...data.items);
    if (data.next_cursor === null) return result;
    if (typeof data.next_cursor !== 'string' || !data.next_cursor || seen.has(data.next_cursor)) throw new Error('조회 커서가 올바르지 않습니다.');
    seen.add(data.next_cursor); after = data.next_cursor;
  }
  throw new Error('조회량이 많아 전체 대화를 불러오지 못했습니다. 관리자에게 문의해 주세요.');
}

export class ChatPage {
  constructor(root) {
    this.root = root; this.base = root.dataset.base; this.directory = root.dataset.directory;
    this.scope = root.dataset.session; this.csrf = root.dataset.csrf;
    this.rooms = []; this.people = []; this.selected = null; this.roomAttempt = null;
    this.drafts = new Map(); this.pending = new Map(); this.epoch = 0; this.delay = 1000;
    this.alive = true; this.busy = false; this.reading = false; this.timer = null; this.controller = null;
    this.$ = id => document.getElementById(`chat-${id}`);
  }
  status(message, error = false) { this.$('status').textContent = message; this.$('status').classList.toggle('chat-error', error); }
  roomStatus(message, error = false) { this.$('room-status').textContent = message; this.$('room-status').classList.toggle('chat-error', error); }
  async api(path, body = undefined, signal = undefined) {
    if (!this.alive) throw new DOMException('화면 종료', 'AbortError');
    const options = { credentials: 'same-origin', cache: 'no-store', redirect: 'error', signal,
      headers: { 'Accept': 'application/json', 'X-Chat-Session': this.scope } };
    if (body !== undefined) {
      options.method = 'POST';
      options.body = new URLSearchParams({ csrf_token: this.csrf, payload: JSON.stringify(body) });
    }
    const response = await fetch(path, options);
    let envelope;
    try { envelope = await response.json(); }
    catch { throw new Error('서버 응답을 확인하지 못했습니다. 전송한 내용은 다시 조회해 주세요.'); }
    if (!response.ok || envelope.ok !== true) {
      const message = envelope.error?.message || '요청을 처리하지 못했습니다.';
      if (response.status === 401 || response.status === 403) this.stop(true);
      const error = new Error(`${message}${envelope.request_id ? ` 요청 번호: ${envelope.request_id}` : ''}`);
      error.status = response.status; error.code = envelope.error?.code; throw error;
    }
    if (!this.alive) throw new DOMException('화면 종료', 'AbortError');
    return envelope.data;
  }
  stop(clear = false) {
    this.alive = false; this.invalidate();
    if (clear) {
      this.rooms = []; this.pending.clear(); this.drafts.clear(); this.roomAttempt = null;
      this.$('rooms').replaceChildren(); this.$('members').replaceChildren(); this.$('messages').replaceChildren();
      this.$('people').replaceChildren(); this.$('content').value = ''; this.$('new-title').value = '';
      if (this.$('dialog').open) this.$('dialog').close();
      this.status('로그인이 만료되거나 변경되었습니다. 화면을 새로 열어 주세요.', true);
      this.controls();
    }
  }
  invalidate() { this.epoch++; this.controller?.abort(); clearTimeout(this.timer); }
  schedule() {
    clearTimeout(this.timer);
    if (this.alive && !document.hidden) this.timer = setTimeout(() => this.refresh(), this.delay);
  }
  controls() {
    const usable = this.alive && !this.busy, chosen = usable && !!this.selected;
    const pending = this.pending.get(this.selected);
    this.$('content').disabled = !chosen;
    this.$('content').readOnly = !!pending;
    this.$('send-button').disabled = !chosen;
    this.$('send-button').textContent = pending ? '동일 요청 재전송' : '보내기';
    this.$('result').hidden = !pending; this.$('result').disabled = !usable;
    this.$('hide').disabled = !chosen || !!pending;
    for (const id of ['create', 'filter', 'refresh']) this.$(id).disabled = !usable;
    for (const button of this.$('rooms').querySelectorAll('button')) button.disabled = !usable;
  }
  renderRooms() {
    const filter = this.$('filter').value;
    const list = this.rooms.filter(r => filter === 'all' || r.is_group === (filter === 'group'));
    const signature = JSON.stringify([list, this.selected]);
    if (this.$('rooms').dataset.signature === signature) return;
    this.$('rooms').dataset.signature = signature;
    this.$('rooms').replaceChildren(...list.map(room => {
      const tr = document.createElement('tr'); tr.classList.toggle('chat-selected', room.id === this.selected);
      const kind = document.createElement('td'); kind.textContent = room.is_group ? '그룹' : '개인';
      const title = document.createElement('td'), button = document.createElement('button');
      button.type = 'button'; button.className = 'chat-room-button'; button.textContent = room.name;
      button.setAttribute('aria-pressed', String(room.id === this.selected));
      button.addEventListener('click', () => this.choose(room.id)); title.append(button);
      const count = document.createElement('td'); count.textContent = `${room.participants.length}명`;
      tr.append(kind, title, count); return tr;
    }));
    this.$('empty').hidden = list.length !== 0;
  }
  renderRoom(messages) {
    const room = this.rooms.find(r => r.id === this.selected);
    this.$('room-title').textContent = room?.name || '대화방을 선택해 주세요';
    const members = room?.participants || [], signature = JSON.stringify(members);
    if (this.$('members').dataset.signature !== signature) {
      this.$('members').dataset.signature = signature;
      this.$('members').replaceChildren(...members.map(person => {
        const li = document.createElement('li'); li.textContent = `${person.name} · ${person.department || '부서 없음'}`; return li;
      }));
    }
    const box = this.$('messages'), msgSignature = JSON.stringify([this.selected, messages]);
    if (box.dataset.signature === msgSignature) return;
    const sameRoom = box.dataset.room === (this.selected || '');
    const atBottom = box.scrollHeight - box.clientHeight - box.scrollTop < 40;
    const top = box.scrollTop;
    const known = sameRoom ? new Set(Array.from(box.children, n => n.dataset.message)) : new Set();
    const existingIds = sameRoom ? Array.from(box.children, n => n.dataset.message) : [];
    const appendOnly = existingIds.every((id, index) => id === messages[index]?.id);
    if (!appendOnly || !sameRoom) box.replaceChildren();
    for (const message of messages) {
      if (appendOnly && sameRoom && known.has(message.id)) continue;
      const item = document.createElement('article'); item.className = 'chat-message'; item.dataset.message = message.id;
      const heading = document.createElement('header');
      const date = new Date(message.sent_at);
      heading.textContent = `${message.sender_name} · ${Number.isNaN(date.getTime()) ? message.sent_at : date.toLocaleString('ko-KR', { timeZone: 'Asia/Seoul', hour12: false })}`;
      const text = document.createElement('p'); text.textContent = message.content;
      item.append(heading, text); box.append(item);
    }
    box.dataset.signature = msgSignature; box.dataset.room = this.selected || '';
    box.scrollTop = !sameRoom || atBottom ? box.scrollHeight : top;
  }
  async refresh() {
    if (!this.alive || document.hidden || this.busy || this.reading) { this.schedule(); return; }
    this.reading = true; const generation = this.epoch, selected = this.selected;
    this.controller = new AbortController();
    const signal = this.controller.signal;
    try {
      const rooms = await readAll(after => this.api(`${this.base}?limit=100${after ? `&after=${encodeURIComponent(after)}` : ''}`, undefined, signal));
      const exists = selected && rooms.some(r => r.id === selected);
      const messages = exists ? await readAll(after => this.api(`${this.base}/${selected}/messages?limit=100${after ? `&after=${encodeURIComponent(after)}` : ''}`, undefined, signal)) : [];
      if (!this.alive || document.hidden || generation !== this.epoch) return;
      this.rooms = rooms;
      if (selected && !exists) { this.selected = null; this.$('content').value = ''; }
      const attempt = this.pending.get(selected);
      if (attempt && messages.some(m => m.id === attempt.message_id)) this.sent(selected, attempt);
      this.renderRooms(); this.renderRoom(messages); this.delay = 1000;
      this.status(attempt && this.pending.has(selected) ? '전송 결과가 아직 확인되지 않았습니다. 동일 요청 재전송 또는 결과 확인을 선택해 주세요.' : '서버 DB 저장 · 새 메시지 자동 수신 연결됨');
    } catch (error) {
      if (error.name !== 'AbortError' && this.alive && generation === this.epoch) {
        this.delay = Math.min(this.delay * 2, 10000);
        this.status(`${error.message} · ${this.delay / 1000}초 후 다시 조회합니다.`, true);
      }
    } finally { this.reading = false; this.controls(); this.schedule(); }
  }
  choose(id) {
    if (this.busy || !this.alive) return;
    if (this.selected) this.drafts.set(this.selected, this.$('content').value);
    this.invalidate(); this.selected = id;
    this.$('content').value = this.pending.get(id)?.content || this.drafts.get(id) || '';
    this.renderRooms(); this.renderRoom([]); this.controls(); this.refresh();
  }
  sent(room, attempt) {
    if (this.pending.get(room) !== attempt) return;
    this.pending.delete(room); this.drafts.delete(room);
    if (this.selected === room) this.$('content').value = '';
  }
  async send() {
    if (!this.alive || this.busy || !this.selected) return;
    const room = this.selected, content = this.$('content').value.trim();
    if (!content || Array.from(content).length > 2000) { this.status('메시지는 1~2,000자까지 입력해 주세요.', true); return; }
    let attempt = this.pending.get(room);
    if (!attempt) {
      try { attempt = { message_id: requestId(), content }; }
      catch (error) { this.status(error.message, true); return; }
      this.pending.set(room, attempt);
    }
    this.invalidate(); this.busy = true; this.controls();
    try {
      const data = await this.api(`${this.base}/${room}/messages`, attempt);
      if (data.message?.id !== attempt.message_id || data.message?.content !== attempt.content) throw new Error('전송 결과를 확인하지 못했습니다.');
      this.sent(room, attempt); this.status('메시지를 저장했습니다.');
    } catch (error) {
      if (error.status === 400) this.pending.delete(room); // Definite pre-write input rejection: allow editing.
      if (this.alive) this.status(`${error.message} 입력 내용을 유지합니다.${this.pending.has(room) ? ' 원래 요청 ID도 유지합니다.' : ''}`, true);
    }
    finally { this.busy = false; this.controls(); this.refresh(); }
  }
  async loadPeople() {
    if (this.roomAttempt || this.busy || !this.alive) return;
    this.$('room-submit').disabled = true; this.roomStatus('직원을 불러오는 중입니다.');
    try {
      let page = 1, total = null; const people = [], seen = new Set();
      while (true) {
        const data = await this.api(`${this.directory}?page=${page}&limit=100`);
        if (!Number.isInteger(data.total) || !Array.isArray(data.items) || total !== null && data.total !== total) throw new Error('직원 목록이 변경되었습니다. 다시 조회해 주세요.');
        total = data.total;
        for (const person of data.items) {
          if (seen.has(person.id)) throw new Error('중복된 직원 정보입니다. 다시 조회해 주세요.');
          seen.add(person.id); people.push(person);
        }
        if (people.length === total) break;
        if (!data.items.length || people.length > total || ++page > 1000) throw new Error('직원 목록을 모두 조회하지 못했습니다.');
      }
      this.people = people;
      this.$('department').replaceChildren(new Option('전체 부서', ''), ...Array.from(new Set(people.map(p => p.department))).sort().map(d => new Option(d || '부서 없음', d || '\u0001')));
      this.$('people').replaceChildren(...people.map(person => {
        const label = document.createElement('label'), input = document.createElement('input');
        label.dataset.department = person.department || '\u0001'; input.type = 'checkbox'; input.value = String(person.id);
        const text = document.createElement('span'); text.textContent = `${person.display_name} · ${person.department || '부서 없음'} (#${person.id})`;
        label.append(input, text); return label;
      }));
      this.roomStatus(people.length ? '초대할 사용자를 선택해 주세요. 본인은 자동으로 포함됩니다.' : '초대할 활성 사용자가 없습니다.');
    } catch (error) { if (this.alive) this.roomStatus(error.message, true); }
    finally { this.$('room-submit').disabled = !this.alive || !this.people.length; }
  }
  async createRoom() {
    if (!this.alive || this.busy) return;
    if (!this.roomAttempt) {
      const ids = Array.from(this.$('people').querySelectorAll('input:checked'), input => Number(input.value));
      const title = this.$('new-title').value.trim(), group = this.$('kind').value === 'group';
      if (!title || Array.from(title).length > 60 || /[\x00-\x1f\x7f]/.test(title) || (group ? ids.length < 2 : ids.length !== 1)) {
        this.roomStatus('이름은 1~60자, 개인은 상대방 1명, 그룹은 2명 이상을 선택해 주세요.', true); return;
      }
      try { this.roomAttempt = { room_id: requestId(), title, is_group: group, participant_ids: ids }; }
      catch (error) { this.roomStatus(error.message, true); return; }
    }
    this.invalidate(); this.busy = true; this.controls();
    for (const input of this.$('room-form').querySelectorAll('input,select,button')) input.disabled = true;
    const attempt = this.roomAttempt;
    try {
      const data = await this.api(this.base, attempt);
      if (data.room?.id !== attempt.room_id) throw new Error('대화방 저장 결과를 확인하지 못했습니다.');
      this.roomAttempt = null; this.$('dialog').close(); this.selected = data.room.id;
      this.rooms = [...this.rooms.filter(r => r.id !== data.room.id), data.room];
      this.$('content').value = ''; this.renderRooms(); this.renderRoom([]); this.status('대화방을 만들었습니다.');
    } catch (error) {
      if (error.status === 400 || error.code === 'CHAT_PARTICIPANT_UNAVAILABLE') this.roomAttempt = null;
      if (this.alive) this.roomStatus(`${error.message} ${this.roomAttempt ? '원래 요청을 유지합니다. 만들기를 누르면 동일 요청으로 재시도합니다.' : '입력 내용을 확인한 후 다시 시도해 주세요.'}`, true);
    }
    finally {
      this.busy = false;
      for (const input of this.$('room-form').querySelectorAll('input,select,button')) input.disabled = !this.alive || !!this.roomAttempt;
      this.$('room-submit').disabled = !this.alive; this.$('room-close').disabled = !this.alive;
      this.controls(); this.refresh();
    }
  }
  async hide() {
    if (!this.alive || this.busy || !this.selected || this.pending.has(this.selected)) return;
    if (!window.confirm('내 대화방 목록에서만 삭제합니다. 다른 참여자의 대화 기록은 유지됩니다.')) return;
    this.invalidate(); this.busy = true; this.controls();
    try {
      await this.api(`${this.base}/${this.selected}/hide`, {});
      this.drafts.delete(this.selected); this.selected = null; this.$('content').value = '';
      this.renderRoom([]); this.status('내 목록에서만 삭제했습니다.');
    } catch (error) { if (this.alive) this.status(error.message, true); }
    finally { this.busy = false; this.controls(); this.refresh(); }
  }
  start() {
    this.$('send').addEventListener('submit', event => { event.preventDefault(); this.send(); });
    this.$('content').addEventListener('keydown', event => {
      if (event.key === 'Enter' && !event.shiftKey && !event.isComposing && event.keyCode !== 229) { event.preventDefault(); this.send(); }
    });
    this.$('refresh').addEventListener('click', () => this.refresh());
    this.$('result').addEventListener('click', () => this.refresh());
    this.$('filter').addEventListener('change', () => {
      const room = this.rooms.find(r => r.id === this.selected), value = this.$('filter').value;
      if (room && value !== 'all' && room.is_group !== (value === 'group')) {
        this.drafts.set(this.selected, this.$('content').value); this.invalidate(); this.selected = null;
        this.$('content').value = ''; this.renderRoom([]);
      }
      this.renderRooms(); this.controls(); this.schedule();
    });
    this.$('hide').addEventListener('click', () => this.hide());
    this.$('create').addEventListener('click', () => {
      this.$('dialog').showModal();
      if (!this.roomAttempt) { this.$('new-title').value = ''; this.loadPeople(); }
    });
    this.$('room-close').addEventListener('click', () => { if (!this.busy) this.$('dialog').close(); });
    this.$('dialog').addEventListener('cancel', event => { if (this.busy) event.preventDefault(); });
    this.$('room-reload').addEventListener('click', () => this.loadPeople());
    this.$('department').addEventListener('change', () => {
      for (const label of this.$('people').children) label.hidden = !!this.$('department').value && label.dataset.department !== this.$('department').value;
    });
    this.$('room-form').addEventListener('submit', event => { event.preventDefault(); this.createRoom(); });
    document.addEventListener('visibilitychange', () => { this.invalidate(); if (!document.hidden) this.refresh(); });
    window.addEventListener('pagehide', () => this.stop());
    window.addEventListener('pageshow', event => { if (event.persisted) window.location.reload(); });
    this.refresh(); return this;
  }
}

if (typeof document !== 'undefined') {
  const root = document.getElementById('chat-app');
  if (root) new ChatPage(root).start();
}
