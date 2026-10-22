"use strict";
// Run the actual owner 2 browser source with explicit DOM and HTTP doubles.
// This checks transport/session/intent boundaries, not real server acceptance.
const assert = require("node:assert/strict"), fs = require("node:fs");
const path = require("node:path"), vm = require("node:vm"), crypto = require("node:crypto");
const source = fs.readFileSync(path.join(__dirname, "../../RemoteWork.Server/remotework/static/task_attachments.js"), "utf8");
let checks = 0, scenarios = 0;
function check(value, message) { assert.ok(value, message); checks++; }
const hash = bytes => crypto.createHash("sha256").update(bytes).digest("hex");
const session = "a".repeat(64), id1 = "11111111-1111-1111-1111-111111111111";
const id2 = "22222222-2222-2222-2222-222222222222";
const settle = async () => { for (let i = 0; i < 12; i++) await new Promise(resolve => setImmediate(resolve)); };
function deferred() { let resolve, reject; const promise = new Promise((a, b) => { resolve = a; reject = b; }); return {promise, resolve, reject}; }

class Element {
  constructor(tag = "div") {
    this.tagName = tag.toUpperCase(); this.children = []; this.listeners = new Map();
    this.attributes = {}; this.dataset = {}; this.disabled = false; this.value = "";
    this.files = []; this.focused = false; this._text = ""; this.classes = new Set();
    this.classList = {toggle: (name, enabled) => enabled ? this.classes.add(name) : this.classes.delete(name)};
  }
  set textContent(value) { this._text = String(value); this.children = []; }
  get textContent() { return this._text + this.children.map(child => child.textContent).join(""); }
  set innerHTML(_) { throw new Error("Untrusted metadata must not become HTML"); }
  append(...children) { this.children.push(...children); }
  replaceChildren(...children) { this._text = ""; this.children = children; }
  addEventListener(type, fn) { if (!this.listeners.has(type)) this.listeners.set(type, []); this.listeners.get(type).push(fn); }
  setAttribute(name, value) { this.attributes[name] = String(value); }
  focus() { this.focused = true; }
  emit(type, detail = {}) {
    const event = {target: this, defaultPrevented: false, preventDefault() { this.defaultPrevented = true; }, ...detail};
    event.promises = (this.listeners.get(type) || []).map(fn => fn(event)).filter(value => value && typeof value.then === "function");
    return event;
  }
  click() { if (!this.disabled) return this.emit("click"); }
}
class FormDataDouble {
  constructor() { this.fields = new Map(); }
  append(key, value) { assert.ok(!this.fields.has(key)); this.fields.set(key, value); }
}
function response(data, {status = 200, scope = session, code = "FAIL"} = {}) {
  return {ok: status < 400, status, headers: {get: name => name === "X-Results-Session" ? scope : null},
    async json() { return status < 400 ? {ok: true, data, request_id: "test-only"}
      : {ok: false, error: {code, message: "명시 HTTP 대역 오류"}, request_id: "test-only"}; }};
}
function meta(id, name, bytes) { return {attachment_id: id, name, size: bytes.length, sha256: hash(bytes)}; }
function server() {
  const files = new Map(), calls = []; let ids = 0;
  const state = {files, calls, listed: [], interceptor: null};
  state.fetch = async (raw, options) => {
    const url = new URL(raw), body = options.body ? JSON.parse(options.body.fields.get("body")) : undefined;
    const call = {url, options, body}; calls.push(call);
    assert.equal(url.origin, "http://fixture.invalid");
    assert.equal(options.credentials, "same-origin"); assert.equal(options.redirect, "error");
    assert.equal(options.cache, "no-store"); assert.equal(options.headers["X-Results-Session"], session);
    assert.ok(!("Authorization" in options.headers));
    if (body !== undefined) {
      assert.equal(options.method, "POST"); assert.equal(options.body.fields.get("csrf_token"), "fixture-csrf");
      assert.deepEqual([...options.body.fields.keys()], ["csrf_token", "body"]);
    }
    if (state.interceptor) {
      const intercepted = await state.interceptor(call);
      if (intercepted !== undefined) return intercepted;
    }
    if (url.pathname.endsWith("/uploads/start")) {
      const id = ++ids === 1 ? id1 : id2;
      const info = {attachment_id: id, name: body.name, size: body.size, sha256: body.sha256};
      files.set(id, {info, bytes: Buffer.alloc(body.size), chunks: new Map()});
      return response(info, {status: 201});
    }
    const upload = /\/uploads\/([^/]+)\/(chunks|complete|discard)$/.exec(url.pathname);
    if (upload) {
      const [, id, op] = upload, file = files.get(id);
      if (!file) return response(null, {status: 404, code: "ATTACHMENT_NOT_FOUND"});
      if (op === "chunks") {
        const bytes = Buffer.from(body.data, "base64");
        const old = file.chunks.get(body.offset);
        if (old) assert.deepEqual(old, bytes, "retry keeps exact bytes at the original offset");
        file.chunks.set(body.offset, bytes); bytes.copy(file.bytes, body.offset);
        return response({attachment_id: id, next_offset: body.offset + bytes.length});
      }
      if (op === "complete") {
        assert.equal(hash(file.bytes), file.info.sha256);
        return response(file.info);
      }
      files.delete(id); return response({attachment_id: id});
    }
    const chunk = /\/tasks\/1\/files\/([^/]+)\/chunks$/.exec(url.pathname);
    if (chunk) {
      const file = files.get(chunk[1]), offset = Number(url.searchParams.get("offset"));
      const bytes = file.bytes.subarray(offset, offset + 16384);
      return response({attachment_id: chunk[1], offset, data: bytes.toString("base64"),
        next_offset: offset + bytes.length, eof: offset + bytes.length === file.bytes.length});
    }
    if (url.pathname === "/results/data/shared/tasks/1") return response({items: state.listed});
    throw new Error("Unexpected HTTP double route " + url.pathname);
  };
  return state;
}
function harness({mode = "create", explicit = false, ids = null, service = server(), configPatch = {}} = {}) {
  const panel = new Element("section"), form = new Element("form"), window = new Element("window");
  const logout = new Element("form"), nodes = {}, created = [], blobs = [], confirmations = [];
  const config = {mode, explicit, ids, taskId: mode === "create" ? null : "1", session,
    urls: {start: "/results/data/shared/uploads/start", chunk: "/results/data/shared/uploads/__id__/chunks",
      complete: "/results/data/shared/uploads/__id__/complete", discard: "/results/data/shared/uploads/__id__/discard",
      list: "/results/data/shared/tasks/1", download: "/results/data/shared/tasks/1/files/__id__/chunks"}, ...configPatch};
  for (const name of ["feedback", "list", "count", "refresh", "input", "choose", "upload", "ids"]) nodes[name] = new Element();
  nodes.choose.disabled = nodes.upload.disabled = nodes.input.disabled = nodes.refresh.disabled = true;
  const csrf = new Element("input"); csrf.value = "fixture-csrf";
  panel.dataset.config = JSON.stringify(config);
  panel.closest = () => mode === "read" ? null : form;
  panel.querySelector = selector => { const found = /^\[data-attachment-(.+)\]$/.exec(selector); return found ? nodes[found[1]] : null; };
  form.querySelector = selector => selector === '[name="csrf_token"]' ? csrf : null;
  const document = {querySelectorAll: selector => selector === "[data-task-attachments]" ? [panel]
    : selector === "aside form" ? [logout] : [],
    createElement: tag => { const e = new Element(tag); created.push(e); return e; }};
  let confirmResult = true;
  window.confirm = message => { confirmations.push(message); return confirmResult; };
  class TestURL extends URL {}
  TestURL.createObjectURL = blob => { blobs.push(blob); return "blob:fixture"; };
  TestURL.revokeObjectURL = () => {};
  const storage = new Proxy({}, {get: () => { throw new Error("Browser persistence is forbidden"); }});
  const context = {document, window, console, location: {origin: "http://fixture.invalid"}, URL: TestURL,
    AbortController, FormData: FormDataDouble, fetch: service.fetch, Uint8Array, Blob,
    btoa: value => Buffer.from(value, "binary").toString("base64"),
    atob: value => Buffer.from(value, "base64").toString("binary"),
    crypto: {subtle: {async digest(_, bytes) { return Uint8Array.from(Buffer.from(hash(Buffer.from(bytes)), "hex")).buffer; }}},
    setTimeout: callback => { callback(); return 1; }, localStorage: storage, sessionStorage: storage, indexedDB: storage};
  vm.runInNewContext(source, context, {filename: "task_attachments.js"});
  const h = {nodes, form, logout, window, service, blobs, created, confirmations,
    setConfirm: value => { confirmResult = value; },
    choose(files) { nodes.input.files = files; nodes.input.value = "fakepath"; nodes.input.emit("change"); },
    file(name, bytes) { let reads = 0; const data = Buffer.from(bytes); return {name, size: data.length,
      async arrayBuffer() { reads++; return Uint8Array.from(data).buffer; }, get reads() { return reads; }}; },
    rowCount: () => nodes.list.children.length,
    rowButton(index, label) { return nodes.list.children[index].children.find(item => item.tagName === "BUTTON" && item.textContent === label); },
    async click(button) { const event = button.click(); if (event) await Promise.all(event.promises); await settle(); }};
  return h;
}
async function scenario(name, fn) { await fn(); scenarios++; console.log("PASS " + name); }

(async () => {
  await scenario("local selection remains separate from explicit upload and form execution", async () => {
    const h = harness(); await settle();
    check(!h.nodes.choose.disabled && h.service.calls.length === 0, "create screen has no initial network or upload");
    const file = h.file('<img src=x onerror="fail">.txt', Buffer.from("test"));
    h.choose([file]); check(h.rowCount() === 1 && file.reads === 0, "selection reads metadata only");
    check(h.nodes.list.textContent.includes(file.name), "untrusted filename rendered as text");
    check(h.form.emit("submit").defaultPrevented, "unuploaded selection blocks preparation/search");
    check(h.nodes.input.value === "", "native selection is cleared without a multipart form field");
    await h.click(h.rowButton(0, "선택에서 제외"));
    check(h.rowCount() === 0 && h.service.calls.length === 0, "local exclusion causes no deletion");
  });
  await scenario("upload chunks and final IDs keep cookie CSRF session and exact bytes", async () => {
    const h = harness(); await settle();
    const bytes = Buffer.alloc(16384 + 9, 251), file = h.file("plus-base64.bin", bytes);
    h.choose([file]); await h.click(h.nodes.upload);
    check(h.service.calls.length === 4, "start, two chunks, and complete only");
    check(h.service.calls.filter(call => call.url.pathname.endsWith("/chunks")).length === 2, "16 KiB chunks");
    check(h.service.calls[1].body.data.includes("+"), "base64 plus characters preserved in JSON FormData");
    check(h.nodes.list.textContent.includes("전송 완료"), "completion shown only after validated metadata");
    check(h.nodes.ids.value === JSON.stringify([id1]) && !h.nodes.ids.disabled, "only completed ID serialized");
    check(!h.form.emit("submit").defaultPrevented, "explicit preparation can use completed final ID");
    check(!h.nodes.ids.disabled && h.nodes.choose.disabled, "form freezes UI while preserving serialized IDs");
    check(h.service.calls.every(call => !call.url.pathname.includes("/tasks/write")), "upload never submits the task");
  });
  await scenario("lost chunk acknowledgement retries same ID offset and bytes only on click", async () => {
    const service = server(), h = harness({service}); await settle();
    let lost = false;
    service.interceptor = async call => {
      if (!lost && call.url.pathname.endsWith("/chunks")) {
        lost = true; const file = service.files.get(id1), data = Buffer.from(call.body.data, "base64");
        file.chunks.set(call.body.offset, data); data.copy(file.bytes, call.body.offset);
        throw new Error("Explicit lost chunk response");
      }
    };
    h.choose([h.file("chunk.bin", Buffer.alloc(17000, 17))]); await h.click(h.nodes.upload);
    check(h.nodes.list.textContent.includes("전송 결과 확인 필요"), "uncertain chunk outcome retained");
    check(h.form.emit("submit").defaultPrevented, "uncertain upload blocks task preparation");
    const count = service.calls.length; await settle(); check(service.calls.length === count, "no automatic retry");
    await h.click(h.nodes.upload);
    check(service.calls.filter(call => call.url.pathname.endsWith("/start")).length === 1, "retry does not mint another upload");
    const repeated = service.calls.filter(call => call.url.pathname.endsWith("/chunks") && call.body.offset === 0);
    check(repeated.length === 2 && repeated[0].body.data === repeated[1].body.data, "same offset and original base64 retried");
    check(h.nodes.ids.value === JSON.stringify([id1]) && h.nodes.list.textContent.includes("전송 완료"), "retry completes original upload");
  });
  await scenario("unknown start and lost completion never create replacement uploads", async () => {
    const h = harness(); await settle();
    h.service.interceptor = async call => { if (call.url.pathname.endsWith("/start")) throw new Error("Unknown start"); };
    h.choose([h.file("unknown.txt", Buffer.from("a"))]); await h.click(h.nodes.upload);
    check(h.nodes.list.textContent.includes("전송 시작 결과 불명") && h.nodes.upload.disabled, "unknown start cannot restart automatically");
    check(h.form.emit("submit").defaultPrevented && h.service.calls.length === 1, "unknown start blocks form and extra request");
    h.setConfirm(false); await h.click(h.rowButton(0, "선택에서 제외"));
    check(h.rowCount() === 1, "uncertain local exclusion needs explicit confirmation");
    h.setConfirm(true); await h.click(h.rowButton(0, "선택에서 제외"));
    check(h.rowCount() === 0 && h.service.calls.length === 1, "local exclusion never claims server deletion");
    const complete = harness(); await settle(); let lost = false;
    complete.service.interceptor = async call => {
      if (!lost && call.url.pathname.endsWith("/complete")) { lost = true; throw new Error("Unknown complete"); }
    };
    complete.choose([complete.file("done.txt", Buffer.from("complete"))]); await complete.click(complete.nodes.upload);
    const prior = complete.service.calls.length; await complete.click(complete.nodes.upload);
    check(complete.service.calls.length === prior + 1 && complete.service.calls.at(-1).url.pathname.endsWith("/complete"), "completion retry reuses original upload without new chunks/start");
    check(complete.nodes.list.textContent.includes("전송 완료"), "same upload completion recovered");
  });
  await scenario("late responses after leaving cannot revive rows or send subsequent chunks", async () => {
    for (const event of ["pagehide", "logout"]) {
      const h = harness(); await settle(); const pending = deferred();
      h.service.interceptor = () => pending.promise;
      h.choose([h.file("private.txt", Buffer.from("late"))]);
      const upload = h.nodes.upload.click(); await settle();
      if (event === "logout") h.logout.emit("submit"); else h.window.emit("pagehide");
      pending.resolve(response(meta(id1, "private.txt", Buffer.from("late"))));
      await Promise.all(upload.promises); await settle();
      check(h.rowCount() === 0 && h.nodes.ids.disabled && h.nodes.choose.disabled, "retired screen stays empty: " + event);
      check(h.service.calls.length === 1 && h.service.calls[0].options.signal.aborted, "no chunks after stale start response: " + event);
      check(!h.nodes.feedback.textContent.includes("전송 완료"), "no success from late response: " + event);
    }
  });
  await scenario("expired permissions or replaced session retire the original screen", async () => {
    for (const failure of [{status: 401}, {status: 403}, {scope: "b".repeat(64)},
                           {status: 409, code: "RESULTS_SESSION_CHANGED"}]) {
      const h = harness(); await settle();
      h.service.interceptor = async () => response(null, failure);
      h.choose([h.file("secret.txt", Buffer.from("private"))]); await h.click(h.nodes.upload);
      check(h.rowCount() === 0 && h.nodes.ids.disabled && h.nodes.choose.disabled, "auth/scope failure retires current selection");
      check(h.service.calls.length === 1 && h.form.emit("submit").defaultPrevented, "retired session never uploads or submits again");
    }
  });
  await scenario("temporary deletion is explicit and uncertainty never claims success", async () => {
    const h = harness(); await settle();
    h.choose([h.file("temporary.txt", Buffer.from("temporary"))]); await h.click(h.nodes.upload);
    const before = h.service.calls.length;
    h.setConfirm(false); await h.click(h.rowButton(0, "임시 파일 삭제"));
    check(h.service.calls.length === before && h.rowCount() === 1, "cancelled confirmation sends no discard");
    h.setConfirm(true); await h.click(h.rowButton(0, "임시 파일 삭제"));
    check(h.rowCount() === 0 && h.service.calls.at(-1).url.pathname.endsWith("/discard"), "explicit discard removes only confirmed upload");
    const uncertain = harness(); await settle();
    uncertain.choose([uncertain.file("uncertain.txt", Buffer.from("x"))]); await uncertain.click(uncertain.nodes.upload);
    let first = true;
    uncertain.service.interceptor = async call => {
      if (call.url.pathname.endsWith("/discard") && first) { first = false; uncertain.service.files.delete(id1); throw new Error("Lost discard"); }
    };
    await uncertain.click(uncertain.rowButton(0, "임시 파일 삭제"));
    check(uncertain.nodes.list.textContent.includes("삭제 결과 확인 필요"), "lost discard stays uncertain");
    await uncertain.click(uncertain.rowButton(0, "임시 삭제 다시 확인"));
    check(uncertain.nodes.list.textContent.includes("삭제 결과 확인 필요"), "404 does not invent successful deletion");
    check(uncertain.form.emit("submit").defaultPrevented, "uncertain discard blocks task preparation");
    const exclude = uncertain.rowButton(0, "선택에서만 제외");
    check(!!exclude, "explicit local exclusion is available when server outcome cannot be established");
    const count = uncertain.service.calls.length;
    await uncertain.click(exclude);
    check(uncertain.rowCount() === 0 && uncertain.service.calls.length === count, "local exclusion causes no repeated server mutation");
  });
  await scenario("read download verifies size hash and current task membership before offering bytes", async () => {
    for (const size of [0, 17001]) {
      const service = server(), bytes = Buffer.alloc(size, 252), info = meta(id1, "../report.txt", bytes);
      service.files.set(id1, {info, bytes}); service.listed = [info];
      const h = harness({mode: "read", service}); await settle();
      await h.click(h.rowButton(0, "다운로드"));
      check(h.blobs.length === 1 && h.blobs[0].size === size, "verified download offered exact size: " + size);
      const anchor = h.created.find(element => element.tagName === "A");
      check(anchor.download === ".._report.txt", "download filename removes path separators");
      check(service.calls.filter(call => call.url.pathname.endsWith("/tasks/1")).length === 3, "initial, pre-transfer, and pre-save current task list reads");
      check(service.calls.every(call => call.options.method === "GET"), "read screen issues no mutations");
    }
    for (const failure of ["membership", "hash"]) {
      const service = server(), bytes = Buffer.from("protected"), info = meta(id1, "protected.txt", bytes);
      service.files.set(id1, {info, bytes}); service.listed = [info];
      const h = harness({mode: "read", service}); await settle();
      let reads = 0;
      service.interceptor = async call => {
        if (call.url.pathname.endsWith("/tasks/1") && ++reads === 2 && failure === "membership") return response({items: []});
        if (call.url.pathname.endsWith("/chunks") && failure === "hash") return response({attachment_id: id1, offset: 0,
          next_offset: bytes.length, eof: true, data: Buffer.alloc(bytes.length, 0).toString("base64")});
      };
      await h.click(h.rowButton(0, "다운로드"));
      check(h.blobs.length === 0 && h.nodes.feedback.attributes.role === "alert", "failed final membership/hash never offers bytes: " + failure);
    }
  });
  await scenario("explicit edit selection survives refresh without silently adding current files", async () => {
    const service = server(), a = meta(id1, "A.txt", Buffer.from("A")), b = meta(id2, "B.txt", Buffer.from("B"));
    service.listed = [a];
    const h = harness({mode: "edit", service}); await settle();
    check(h.nodes.ids.disabled && h.rowCount() === 1, "initial edit omits final-list field to preserve existing attachments");
    await h.click(h.rowButton(0, "업무에서 제외"));
    check(!h.nodes.ids.disabled && h.nodes.ids.value === "[]", "explicit empty list means remove task references on later task execution");
    service.listed = [a, b]; await h.click(h.nodes.refresh);
    check(h.rowCount() === 0 && h.nodes.ids.value === "[]", "refresh never replaces explicit final selection");
    check(service.calls.every(call => call.options.method === "GET"), "edit exclusion does not delete files or mutate tasks");
  });
  await scenario("invalid selection and malformed transport responses cannot become stored IDs", async () => {
    const h = harness(); await settle();
    h.choose([h.file("existing.txt", Buffer.from("a"))]);
    h.choose([h.file("valid.txt", Buffer.from("b")), {name: "too-large", size: 25 * 1024 * 1024 + 1}]);
    check(h.rowCount() === 1 && h.nodes.feedback.attributes.role === "alert", "invalid batch has no partial selection effect");
    h.choose(Array.from({length: 10}, (_, index) => h.file("file" + index, Buffer.from("a"))));
    check(h.rowCount() === 1, "over-ten batch leaves prior selection intact");
    h.choose([{name: "exact-max", size: 25 * 1024 * 1024}]);
    check(h.rowCount() === 2 && h.service.calls.length === 0, "exact 25 MiB accepts metadata without starting upload");
    for (const failure of ["chunk-offset", "complete-metadata"]) {
      const target = harness(); await settle();
      target.service.interceptor = async call => {
        if (failure === "chunk-offset" && call.url.pathname.endsWith("/chunks")) {
          return response({attachment_id: id1, next_offset: 999});
        }
        if (failure === "complete-metadata" && call.url.pathname.endsWith("/complete")) {
          return response(meta(id1, "wrong-file.txt", Buffer.from("safe")));
        }
      };
      target.choose([target.file("expected.txt", Buffer.from("safe"))]); await target.click(target.nodes.upload);
      check(target.nodes.list.textContent.includes("전송 결과 확인 필요"), "bad response stays uncertain: " + failure);
      check(target.nodes.ids.value === "[]" && target.form.emit("submit").defaultPrevented, "bad response cannot enter task final IDs: " + failure);
    }
  });
  await scenario("delayed download after screen retirement never offers a stale file", async () => {
    const service = server(), bytes = Buffer.from("deferred"), info = meta(id1, "late-download.txt", bytes);
    service.files.set(id1, {info, bytes}); service.listed = [info];
    const h = harness({mode: "read", service}); await settle();
    const pending = deferred(); let started;
    service.interceptor = async call => {
      if (call.url.pathname.endsWith("/chunks")) { started = true; return pending.promise; }
    };
    const event = h.rowButton(0, "다운로드").click(); await settle();
    check(started && h.blobs.length === 0, "download is waiting on original task chunk");
    h.window.emit("pagehide");
    pending.resolve(response({attachment_id: id1, offset: 0, next_offset: bytes.length, eof: true, data: bytes.toString("base64")}));
    await Promise.all(event.promises); await settle();
    check(h.blobs.length === 0 && h.rowCount() === 0, "late download response creates no object URL and revives no rows");
    check(h.nodes.feedback.textContent.includes("이전 화면"), "late completion cannot overwrite retirement guidance");
  });
  console.log("PASS TaskWeb attachment transport: " + checks + " checks in " + scenarios + " scenarios; actual JS with explicit DOM/HTTP doubles.");
})().catch(error => { console.error(error.stack); process.exitCode = 1; });
