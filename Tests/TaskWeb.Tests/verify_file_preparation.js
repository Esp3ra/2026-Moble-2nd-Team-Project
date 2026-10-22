"use strict";
// Execute the original owner2 browser source with an explicit DOM double.
// Every network, byte-reading, and persistence entry point is forbidden here.
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const source = fs.readFileSync(path.join(__dirname, "../../RemoteWork.Server/remotework/static/task_file_preparation.js"), "utf8");
let checks = 0, groups = 0;
function check(value, message) { assert.ok(value, message); checks++; }

class Element {
  constructor(tag = "div") {
    this.tagName = tag.toUpperCase(); this.children = []; this.listeners = new Map();
    this.attributes = {}; this.disabled = false; this.hidden = false; this.value = "";
    this.files = []; this.className = ""; this.type = ""; this.focused = false; this._text = ""; this.clicks = 0;
    this.classes = new Set();
    this.classList = { toggle: (name, enabled) => enabled ? this.classes.add(name) : this.classes.delete(name) };
  }
  set textContent(value) { this._text = String(value); this.children = []; }
  get textContent() { return this._text + this.children.map(child => child.textContent).join(""); }
  set innerHTML(_) { throw new Error("Rendering file metadata through HTML is forbidden"); }
  append(...children) { this.children.push(...children); }
  replaceChildren(...children) { this._text = ""; this.children = children; }
  addEventListener(type, callback) {
    if (!this.listeners.has(type)) this.listeners.set(type, []);
    this.listeners.get(type).push(callback);
  }
  setAttribute(name, value) { this.attributes[name] = String(value); }
  focus() { this.focused = true; }
  click() { this.clicks++; if (!this.disabled) return this.emit("click"); }
  emit(type, detail = {}) {
    const event = { target: this, defaultPrevented: false,
      preventDefault() { this.defaultPrevented = true; }, ...detail };
    for (const listener of this.listeners.get(type) || []) listener(event);
    return event;
  }
}

function harness({ missing, formPresent = true, panelPresent = true } = {}) {
  const form = new Element("form"), logout = new Element("form"), window = new Element("window");
  const panel = new Element("section"), nodes = {};
  for (const [name, tag] of Object.entries({ input: "input", choose: "button", list: "ul", clear: "button", count: "p", empty: "p", feedback: "p" })) {
    nodes[name] = new Element(tag);
  }
  nodes.input.disabled = nodes.choose.disabled = nodes.clear.disabled = true;
  panel.closest = selector => selector === "form" && formPresent ? form : null;
  panel.querySelector = selector => {
    const match = /^\[data-file-(.*)\]$/.exec(selector);
    return match && match[1] !== missing ? nodes[match[1]] : null;
  };
  const forbidden = [];
  function forbid(name) { return () => { forbidden.push(name); throw new Error("Unexpected " + name); }; }
  const storage = new Proxy({}, { get: (_, name) => forbid("storage." + String(name)) });
  const document = {
    createElement: tag => new Element(tag),
    querySelectorAll: selector => selector === "[data-task-file-preparation]" ? (panelPresent ? [panel] : [])
      : selector === "aside form" ? [logout] : []
  };
  vm.runInNewContext(source, { document, window, console,
    fetch: forbid("fetch"), XMLHttpRequest: forbid("XMLHttpRequest"), WebSocket: forbid("WebSocket"),
    FileReader: forbid("FileReader"), Blob: forbid("Blob"), FormData: forbid("FormData"),
    localStorage: storage, sessionStorage: storage, indexedDB: storage,
    navigator: { sendBeacon: forbid("sendBeacon") }, crypto: { subtle: { digest: forbid("digest") } },
    URL: { createObjectURL: forbid("createObjectURL") },
  }, { filename: "task_file_preparation.js" });
  function file(name, size) {
    return { name, size, arrayBuffer: forbid("arrayBuffer"), text: forbid("file.text"),
      stream: forbid("stream"), slice: forbid("slice"),
      get webkitRelativePath() { forbidden.push("path"); throw new Error("File path access forbidden"); } };
  }
  function choose(files) {
    nodes.input.files = files;
    nodes.input.value = "C:\\fakepath\\only-browser-display";
    nodes.input.emit("change");
  }
  return { form, logout, window, panel, nodes, file, choose, forbidden,
    rowCount: () => nodes.list.children.length,
    remove: index => nodes.list.children[index].children[1].emit("click") };
}

function scenario(name, fn) { fn(); groups++; console.log("PASS " + name); }

scenario("empty screen and progressive enhancement", () => {
  const h = harness();
  check(!h.nodes.input.disabled, "complete local guard enables the file chooser");
  check(!h.nodes.choose.disabled && h.nodes.input.clicks === 0, "chooser opens only after an explicit user action");
  h.nodes.choose.click();
  check(h.nodes.input.clicks === 1, "visible choose button opens the native file input once");
  check(h.nodes.clear.disabled && !h.nodes.empty.hidden, "empty screen keeps clear unavailable");
  check(h.nodes.count.textContent.includes("0 / 10"), "empty count is explicit");
  check(!h.form.emit("submit").defaultPrevented, "no files leaves the existing form submission unchanged");
  check(h.forbidden.length === 0, "initialization has no network or persistence");
  for (const missing of ["input", "choose", "list", "clear", "count", "empty", "feedback"]) {
    const incomplete = harness({ missing });
    check(incomplete.nodes.input.disabled, "incomplete panel stays unavailable: " + missing);
    check(incomplete.nodes.choose.disabled, "incomplete panel keeps choose button unavailable: " + missing);
  }
  check(harness({ formPresent: false }).nodes.input.disabled, "unbound panel stays unavailable");
  check(harness({ panelPresent: false }).nodes.input.disabled, "unrelated page stays unchanged");
});

scenario("local metadata only and exact 25 MiB boundary", () => {
  const h = harness(), original = h.file("empty.txt", 0), maximum = h.file("maximum.bin", 25 * 1024 * 1024);
  h.choose([original, maximum]);
  check(h.rowCount() === 2, "zero-byte and exact 25 MiB files are accepted");
  check(h.nodes.input.value === "", "native FileList selection is cleared immediately");
  check(h.nodes.count.textContent.includes("2 / 10"), "count uses retained metadata");
  check(h.nodes.list.textContent.includes("0 B") && h.nodes.list.textContent.includes("25.0 MiB"), "sizes are displayed without reading bytes");
  check(h.nodes.list.textContent.includes("전송 안 됨"), "row does not claim upload completion");
  check(!h.nodes.clear.disabled && h.nodes.empty.hidden, "nonempty actions and empty state match");
  original.name = "changed.txt"; original.size = 123;
  h.choose([]);
  check(h.nodes.list.textContent.includes("empty.txt") && !h.nodes.list.textContent.includes("changed.txt"), "metadata is an immutable copy");
  check(h.rowCount() === 2, "cancelling a later chooser keeps earlier selection");
  check(h.forbidden.length === 0, "selection does not read bytes, path, storage or network");
});

scenario("untrusted names are text and removal identifies the selected row", () => {
  const h = harness(), name = '<img src=x onerror="throw 1"> & 한글.txt';
  h.choose([h.file(name, 1024), h.file(name, 2048)]);
  check(h.rowCount() === 2, "same names are not silently deduplicated");
  check(h.nodes.list.children[0].children[0].children[0].textContent === name, "filename is literal text");
  check(h.nodes.list.children[0].children[1].attributes["aria-label"].includes(name), "remove action identifies its file");
  check(h.nodes.list.children[0].children[1].type === "button", "removal cannot submit a form");
  h.remove(0);
  check(h.rowCount() === 1 && h.nodes.list.textContent.includes("2.0 KiB"), "first duplicate removal preserves the second file");
  check(h.nodes.feedback.textContent.includes("서버 파일은 변경하지 않았습니다"), "local removal is distinct from server deletion");
  check(h.nodes.feedback.focused, "keyboard focus survives row removal");
  check(h.forbidden.length === 0, "removal sends no server request");
});

scenario("invalid selection batches have no partial effect", () => {
  const h = harness(); h.choose([h.file("existing.txt", 1)]);
  for (const rejected of [h.file("too-large.bin", 25 * 1024 * 1024 + 1), h.file("", 1),
    h.file("negative", -1), h.file("fraction", 1.5), h.file("unknown", NaN), h.file(99, 1)]) {
    h.choose([h.file("should-not-be-added.txt", 2), rejected]);
    check(h.rowCount() === 1 && h.nodes.list.textContent.includes("existing.txt"), "bad batch preserves only earlier selection");
    check(!h.nodes.list.textContent.includes("should-not-be-added"), "valid prefix of rejected batch is not added");
    check(h.nodes.feedback.attributes.role === "alert" && h.nodes.feedback.focused, "rejected batch is announced accessibly");
  }
  h.choose(Array.from({ length: 10 }, (_, i) => h.file("extra" + i, i)));
  check(h.rowCount() === 1, "oversized count batch is atomic");
  h.choose(Array.from({ length: 9 }, (_, i) => h.file("fits" + i, i)));
  check(h.rowCount() === 10, "ten selections are allowed");
  h.choose([h.file("eleventh", 1)]);
  check(h.rowCount() === 10 && !h.nodes.list.textContent.includes("eleventh"), "eleventh file is rejected");
  check(h.forbidden.length === 0, "invalid metadata is never uploaded");
});

scenario("preparation and candidate search cannot silently omit chosen files", () => {
  const h = harness(); h.choose([h.file("pending.txt", 1)]);
  for (const value of ["prepare", "search", undefined]) {
    const event = h.form.emit("submit", { submitter: value ? { name: "op", value } : null });
    check(event.defaultPrevented, "submission is blocked while local files remain: " + value);
    check(h.nodes.feedback.textContent.includes("선택 목록을 비운 뒤 첨부 없이"), "explicit no-attachment continuation guidance");
  }
  check(h.rowCount() === 1, "blocked form leaves the selection intact");
  h.nodes.clear.emit("click");
  check(h.rowCount() === 0 && h.nodes.clear.disabled, "explicit clear releases the local selection");
  check(!h.form.emit("submit", { submitter: { value: "prepare" } }).defaultPrevented, "existing preparation works after explicit clear");
  check(h.forbidden.length === 0, "screen guard sends no write or upload");
});

scenario("navigation, cached restore and logout do not retain file names", () => {
  for (const mode of ["pagehide", "cached-pageshow", "logout", "reset"]) {
    const h = harness(); h.choose([h.file("private-name.txt", 1)]);
    if (mode === "pagehide") h.window.emit("pagehide");
    if (mode === "cached-pageshow") h.window.emit("pageshow", { persisted: true });
    if (mode === "logout") check(!h.logout.emit("submit").defaultPrevented, "logout itself remains available");
    if (mode === "reset") h.form.emit("reset");
    check(h.rowCount() === 0 && !h.nodes.list.textContent.includes("private-name"), "names cleared on " + mode);
    check(h.nodes.input.value === "" && h.nodes.clear.disabled, "input and buttons cleared on " + mode);
    check(h.forbidden.length === 0, "lifetime clear causes no automatic deletion: " + mode);
  }
  const h = harness(); h.choose([h.file("current.txt", 1)]); h.window.emit("pageshow", { persisted: false });
  check(h.rowCount() === 1, "ordinary page event does not discard current local choice");
});

console.log(`PASS TaskWeb local file preparation: ${checks} checks in ${groups} scenarios; original JS with explicit DOM only.`);
