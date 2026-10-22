/* Owner 2 cookie attachment consumer. No Bearer tokens or persisted drafts. */
(() => {
  "use strict";
  const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
  const hash = /^[0-9a-f]{64}$/;
  const maxBytes = 25 * 1024 * 1024, chunkBytes = 16384;
  document.querySelectorAll("[data-task-attachments]").forEach(panel => {
    let config;
    try { config = JSON.parse(panel.dataset.config); } catch (_) { return; }
    if (!config || !hash.test(config.session) || !["create", "edit", "read"].includes(config.mode)) return;
    const feedback = panel.querySelector("[data-attachment-feedback]");
    const list = panel.querySelector("[data-attachment-list]");
    const count = panel.querySelector("[data-attachment-count]");
    const refresh = panel.querySelector("[data-attachment-refresh]");
    const form = panel.closest("form"), input = panel.querySelector("[data-attachment-input]");
    const choose = panel.querySelector("[data-attachment-choose]");
    const upload = panel.querySelector("[data-attachment-upload]");
    const idsField = panel.querySelector("[data-attachment-ids]");
    const displayField = panel.querySelector("[data-attachment-display]");
    const editable = config.mode !== "read";
    const canHash = !!(globalThis.crypto && globalThis.crypto.subtle);
    if (!feedback || !list || !count || !refresh || (editable && (!form || !input || !choose || !upload || !idsField || !displayField))) return;
    let alive = true, busy = false, loaded = !config.taskId, explicit = config.explicit === true, serial = 0;
    const display = restoredDisplay();
    let rows = (config.ids || []).map(id => {
      const hint = display.get(id);
      return {key: ++serial, id, name: id, ...hint, state: "selected", linked: false,
        restored: !!hint, temporary: hint?.temporary === true};
    });
    const controller = new AbortController();
    function say(message, error = false) {
      feedback.textContent = message;
      feedback.classList.toggle("task-file-feedback-error", error);
      feedback.setAttribute("role", error ? "alert" : "status");
      if (error) feedback.focus();
    }
    function current() { if (!alive) throw new Error("이전 화면은 사용할 수 없습니다. 다시 열어 주세요."); }
    function retire(message) {
      alive = false; controller.abort(); rows = []; loaded = false;
      if (input) input.value = "";
      if (idsField) { idsField.value = "[]"; idsField.disabled = true; }
      if (displayField) { displayField.value = ""; displayField.disabled = true; }
      render(); say(message, true);
    }
    function endpoint(kind, id) {
      if (id && !uuid.test(id)) throw new Error("파일 식별자를 확인하지 못했습니다.");
      const raw = config.urls[kind];
      if (typeof raw !== "string") throw new Error("현재 파일 연결을 사용할 수 없습니다.");
      const url = new URL(id ? raw.replace("__id__", id) : raw, location.origin);
      if (url.origin !== location.origin || !url.pathname.startsWith("/results/data/shared/")) throw new Error("파일 연결 주소가 올바르지 않습니다.");
      return url;
    }
    async function request(url, body) {
      current();
      const options = {method: body === undefined ? "GET" : "POST", credentials: "same-origin",
        redirect: "error", cache: "no-store", signal: controller.signal,
        headers: {"X-Results-Session": config.session}};
      if (body !== undefined) {
        const csrf = form && form.querySelector('[name="csrf_token"]');
        if (!csrf) throw new Error("로그인 화면을 다시 확인해 주세요.");
        options.body = new FormData(); options.body.append("csrf_token", csrf.value);
        options.body.append("body", JSON.stringify(body));
      }
      const response = await fetch(url.toString(), options); current();
      if (response.status === 401 || response.headers.get("X-Results-Session") !== config.session) {
        retire("로그인 접속이 끝났거나 변경되었습니다. 업무 화면을 다시 열어 주세요."); current();
      }
      const value = await response.json(); current();
      if (!response.ok || value.ok !== true) {
        const code = value.error && value.error.code || "RESPONSE_ERROR";
        if (response.status === 403 || code === "RESULTS_SESSION_CHANGED") {
          retire("현재 첨부 접근 권한 또는 로그인 접속을 확인할 수 없습니다. 업무 화면을 다시 열어 주세요."); current();
        }
        throw new Error(`${value.error && value.error.message || "첨부 요청을 확인하지 못했습니다."} (${code}${value.request_id ? " / " + value.request_id : ""})`);
      }
      return value.data;
    }
    function metadata(item) {
      if (!item || !uuid.test(item.attachment_id) || typeof item.name !== "string" || !item.name.length ||
          !Number.isSafeInteger(item.size) || item.size < 0 || item.size > maxBytes || !hash.test(item.sha256)) {
        throw new Error("파일 정보의 형식·크기·해시를 확인하지 못했습니다.");
      }
      return item;
    }
    function restoredDisplay() {
      // These are past display hints, never proof of ownership or current linkage.
      const result = new Map(), items = config.display;
      if (items === undefined || items === null) return result;
      if (!Array.isArray(items) || items.length > 10) return result;
      try {
        for (const item of items) {
          metadata(item);
          if (Object.keys(item).sort().join(",") !== "attachment_id,name,sha256,size,temporary"
              || typeof item.temporary !== "boolean" || !(config.ids || []).includes(item.attachment_id)
              || result.has(item.attachment_id)) return new Map();
          result.set(item.attachment_id, {name: item.name, size: item.size, sha256: item.sha256, temporary: item.temporary});
        }
      } catch (_) { return new Map(); }
      return result;
    }
    function same(a, b) { return a.attachment_id === b.attachment_id && a.name === b.name && a.size === b.size && a.sha256 === b.sha256; }
    async function readList() {
      const result = await request(endpoint("list"));
      if (!result || !Array.isArray(result.items) || result.items.length > 10) throw new Error("업무 첨부 목록 형식이 올바르지 않습니다.");
      const items = result.items.map(metadata);
      if (new Set(items.map(x => x.attachment_id)).size !== items.length) throw new Error("중복된 업무 첨부 목록입니다.");
      return items;
    }
    function render() {
      list.replaceChildren();
      rows.forEach(row => {
        const li = document.createElement("li"), info = document.createElement("div"); li.className = "task-file-row";
        const name = document.createElement("span"); name.className = "task-file-name"; name.textContent = row.name;
        const meta = document.createElement("span"); meta.className = "task-file-meta";
        const labels = {local: "선택됨 · 전송 전", uploading: "전송 확인 중", ready: "전송 완료 · 업무 저장 전",
          linked: "현재 업무 첨부", selected: "선택 ID · 저장 시 재검사", uncertain: "전송 결과 확인 필요 · 같은 파일로 이어서 확인",
          start_unknown: "전송 시작 결과 불명 · 자동 재시작 안 함", discard_unknown: "삭제 결과 확인 필요"};
        const stateLabel = row.restored && row.state === "selected"
          ? "이전 업로드 정보 · 저장·삭제 시 재검사" : labels[row.state];
        meta.textContent = `${row.size === undefined ? "" : row.size + " B · "}${stateLabel}`;
        info.append(name, meta); li.append(info);
        function button(label, action, needsHash = false) {
          const b = document.createElement("button"); b.type = "button"; b.className = "secondary";
          b.textContent = label; b.disabled = !alive || busy || (needsHash && !canHash);
          b.addEventListener("click", () => { if (alive && !busy) action(); }); li.append(b);
        }
        if (row.linked) button("다운로드", () => run(() => download(row)), true);
        if (editable) {
          if (row.temporary && !row.linked && row.id) button(row.state === "discard_unknown" ? "임시 삭제 다시 확인" : "임시 파일 삭제", () => {
            if (window.confirm("선택한 임시 파일의 삭제를 요청할까요? 현재 소유·연결 상태는 서버에서 다시 검사합니다. 업무 저장은 실행하지 않습니다.")) run(() => discard(row));
          });
          else button(row.id ? "업무에서 제외" : "선택에서 제외", () => {
            if (row.state === "start_unknown" && !window.confirm("시작 결과가 불명확해 서버 임시 파일이 남아 있을 수 있습니다. 화면 선택만 지울까요?")) return;
            rows = rows.filter(item => item.key !== row.key); explicit = true; render();
            say("선택에서 제외했습니다. 업무 첨부 변경은 내용 확인·실행 후 적용됩니다.");
          });
          if (row.state === "discard_unknown") button("선택에서만 제외", () => {
            if (!window.confirm("서버 삭제 여부를 확정할 수 없습니다. 서버 파일을 남겨 둔 채 이 화면의 선택에서만 제외할까요?")) return;
            rows = rows.filter(item => item.key !== row.key); explicit = true; render();
            say("화면 선택에서 제외했습니다. 서버 삭제 성공을 확인한 것은 아닙니다.");
          });
        }
        list.append(li);
      });
      count.textContent = `첨부 ${rows.length} / 10개`;
      refresh.disabled = !alive || busy || !config.taskId;
      if (editable) {
        input.disabled = !alive || busy || !loaded || !canHash;
        choose.disabled = input.disabled;
        upload.disabled = !alive || busy || !loaded || !canHash || !rows.some(row => row.file && ["local", "uncertain"].includes(row.state));
        idsField.disabled = !explicit || !alive;
        const selected = rows.filter(row => row.id && ["ready", "linked", "selected"].includes(row.state));
        idsField.value = JSON.stringify(selected.map(row => row.id));
        const items = [];
        for (const row of selected) {
          try {
            const item = metadata({attachment_id: row.id, name: row.name, size: row.size, sha256: row.sha256});
            items.push({...item, temporary: row.temporary === true && !row.linked});
          } catch (_) { /* A selected ID alone must not invent display or deletion information. */ }
        }
        displayField.disabled = !explicit || !alive;
        displayField.value = alive ? JSON.stringify({mode: config.mode, task_id: config.taskId, items}) : "";
      }
    }
    async function run(operation) {
      if (!alive || busy) return;
      busy = true; render();
      try { await operation(); current(); }
      catch (error) { if (alive) say(error.message || "응답을 확인하지 못했습니다. 자동으로 재전송하지 않습니다.", true); }
      finally { busy = false; render(); }
    }
    async function refreshList() {
      if (!config.taskId) { loaded = true; say(canHash ? "파일을 선택한 뒤 명시적으로 업로드해 주세요." : "이 브라우저 연결에서는 파일 검증을 사용할 수 없습니다. HTTPS 또는 localhost로 접속해 주세요.", !canHash); return; }
      loaded = false;
      if (!explicit || !editable) rows = [];
      const items = await readList(); current();
      if (!explicit || !editable) rows = items.map(item => ({...item, key: ++serial, id: item.attachment_id, state: "linked", linked: true}));
      else rows.forEach(row => {
        const item = items.find(item => item.attachment_id === row.id);
        if (item) {
          Object.assign(row, item); row.linked = true; row.temporary = false; row.restored = false;
          if (row.state === "selected") row.state = "linked";
        }
        else { row.linked = false; if (row.state === "linked") row.state = "selected"; }
      });
      loaded = true;
      say((items.length ? "현재 업무 첨부를 확인했습니다." : "현재 업무 첨부가 없습니다.") +
        (editable && explicit ? " 입력 중인 최종 선택은 유지합니다." : ""));
      if (!canHash) say("파일 전송·다운로드 검증에는 HTTPS 또는 localhost 접속이 필요합니다. 기존 첨부 조회와 유지는 가능합니다.", true);
    }
    const digest = async bytes => Array.from(new Uint8Array(await crypto.subtle.digest("SHA-256", bytes))).map(x => x.toString(16).padStart(2, "0")).join("");
    async function uploadRow(row) {
      row.state = "uploading"; render();
      let started = !!row.id;
      try {
        const bytes = new Uint8Array(await row.file.arrayBuffer()); current();
        if (bytes.length !== row.size) throw new Error("선택 파일의 크기가 달라졌습니다.");
        const sha256 = await digest(bytes); current();
        if (row.sha256 && row.sha256 !== sha256) throw new Error("선택 파일 내용이 달라졌습니다.");
        row.sha256 = sha256;
        if (!row.id) {
          const body = {name: row.name, size: row.size, sha256};
          if (config.taskId) body.task_id = config.taskId;
          started = true;
          const result = await request(endpoint("start"), body);
          if (result && uuid.test(result.attachment_id)) { row.id = result.attachment_id; row.temporary = true; row.offset = 0; }
          const data = metadata(result);
          if (data.name !== row.name || data.size !== row.size || data.sha256 !== sha256) throw new Error("시작한 파일 정보가 선택 파일과 다릅니다.");
          row.offset = 0;
        }
        while (row.offset < bytes.length) {
          const chunk = bytes.subarray(row.offset, Math.min(row.offset + chunkBytes, bytes.length));
          const result = await request(endpoint("chunk", row.id), {offset: row.offset, data: btoa(String.fromCharCode(...chunk))});
          if (!result || result.attachment_id !== row.id || result.next_offset !== row.offset + chunk.length) throw new Error("전송 위치를 확인하지 못했습니다. 같은 파일로 다시 확인해 주세요.");
          row.offset = result.next_offset;
        }
        const data = metadata(await request(endpoint("complete", row.id), {}));
        if (data.attachment_id !== row.id || data.name !== row.name || data.size !== row.size || data.sha256 !== sha256) throw new Error("완료 파일 정보가 선택 파일과 다릅니다.");
        row.state = "ready"; row.file = null; explicit = true;
      } catch (error) {
        row.state = row.id ? "uncertain" : started ? "start_unknown" : "local";
        throw error;
      }
    }
    async function discard(row) {
      try {
        const result = await request(endpoint("discard", row.id), {});
        if (!result || result.attachment_id !== row.id) throw new Error("임시 파일 삭제 결과를 확인하지 못했습니다.");
        rows = rows.filter(item => item.key !== row.key); explicit = true;
        say("미연결 임시 파일 삭제를 확인했습니다. 업무 내용은 저장하지 않았습니다.");
      } catch (error) { row.state = "discard_unknown"; throw error; }
    }
    async function download(row) {
      const initial = (await readList()).find(item => item.attachment_id === row.id);
      if (!initial) throw new Error("현재 업무에서 파일을 찾을 수 없습니다. 목록을 다시 확인해 주세요.");
      const bytes = new Uint8Array(initial.size); let offset = 0, finished = false;
      while (!finished) {
        const url = endpoint("download", row.id); url.searchParams.set("offset", String(offset));
        const part = await request(url);
        if (!part || part.attachment_id !== row.id || part.offset !== offset || typeof part.data !== "string") throw new Error("다운로드 위치를 확인하지 못했습니다.");
        const raw = atob(part.data), length = Math.min(chunkBytes, initial.size - offset);
        if (btoa(raw) !== part.data || raw.length !== length || part.next_offset !== offset + length || part.eof !== (offset + length === initial.size)) throw new Error("다운로드 크기·완료 표시가 올바르지 않습니다.");
        bytes.set(Uint8Array.from(raw, c => c.charCodeAt(0)), offset); offset += length; finished = part.eof;
      }
      if (await digest(bytes) !== initial.sha256) throw new Error("파일 해시가 달라 다운로드를 중단했습니다.");
      current();
      const final = (await readList()).find(item => item.attachment_id === row.id);
      if (!final || !same(initial, final)) throw new Error("파일의 현재 업무 연결 또는 정보가 달라 저장하지 않았습니다.");
      current();
      const url = URL.createObjectURL(new Blob([bytes]));
      const link = document.createElement("a"); link.href = url;
      link.download = initial.name.replace(/[\\/\x00-\x1f]/g, "_");
      try { link.click(); } finally { setTimeout(() => URL.revokeObjectURL(url), 1000); }
      say("현재 업무 연결과 전체 크기·해시를 확인한 파일의 다운로드를 시작했습니다.");
    }
    refresh.addEventListener("click", () => run(refreshList));
    if (editable) {
      choose.addEventListener("click", () => input.click());
      input.addEventListener("change", () => {
        const files = Array.from(input.files || []); input.value = "";
        if (!alive || busy || !loaded || !files.length) return;
        if (rows.length + files.length > 10 || files.some(f => !f.name || !Number.isSafeInteger(f.size) || f.size < 0 || f.size > maxBytes)) {
          say("최대 10개, 파일당 25 MiB까지 선택할 수 있습니다. 이번 선택은 추가하지 않았습니다.", true); return;
        }
        rows.push(...files.map(file => ({key: ++serial, name: file.name, size: file.size, file, state: "local"})));
        render(); say("파일을 선택했습니다. 업로드 버튼을 눌러 전송한 뒤 업무 내용을 확인해 주세요.");
      });
      upload.addEventListener("click", () => run(async () => {
        for (const row of rows.filter(row => row.file && ["local", "uncertain"].includes(row.state))) await uploadRow(row);
        say("파일 전송을 확인했습니다. 업무 내용 확인·실행을 마쳐야 첨부가 저장됩니다.");
      }));
      form.addEventListener("submit", event => {
        if (!alive || busy || rows.some(row => !["ready", "linked", "selected"].includes(row.state))) {
          event.preventDefault(); say("파일 전송·삭제 결과를 먼저 확인하거나 선택을 정리해 주세요. 업무 입력은 유지합니다.", true); return;
        }
        // Freeze UI without disabling the serialized final-list field.
        busy = true; render();
      });
      form.addEventListener("reset", event => { event.preventDefault(); say("첨부와 입력을 초기화하려면 업무 화면을 다시 열어 주세요. 서버 임시 파일은 자동 삭제하지 않습니다.", true); });
    }
    document.querySelectorAll("aside form").forEach(other => other.addEventListener("submit", () => retire("접속을 종료했습니다. 파일을 자동 삭제하지 않습니다.")));
    window.addEventListener("pagehide", () => retire("이전 화면의 파일 선택을 정리했습니다. 업무 화면을 다시 열어 주세요."));
    window.addEventListener("pageshow", event => { if (event.persisted) retire("이전 화면은 다시 사용할 수 없습니다. 새로고침 후 업무 정보를 확인해 주세요."); });
    render(); run(refreshList);
  });
})();
