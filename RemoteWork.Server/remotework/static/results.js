"use strict";
(async () => {
  const root = document.getElementById("results-page");
  if (!root) return;
  try {
    if (!globalThis.RemoteWorkFileHash) await import("./file_hash.js");
  } catch (_) { /* Other result actions remain available; file hashing fails closed. */ }
  const $ = id => document.getElementById(id);
  const labels = {not_started:"진행전", in_progress:"진행중", submitted:"제출완료", approved:"승인완료", returned:"반려완료"};
  const reviewLabels = {submitted:"검토 대기", approved:"승인", returned:"반려"};
  const kinds = {created:"생성", assignee_changed:"담당자 변경", status_changed:"상태 변경", submitted:"결과 제출", approved:"승인", returned:"반려",
    updated:"업무 수정", reassigned:"담당자 변경", managers_changed:"관리자 변경", cancelled:"업무 취소",
    deadline_extension_requested:"연장 신청", deadline_extension_approved:"연장 승인", deadline_extension_rejected:"연장 반려", deadline_extension_withdrawn:"연장 철회", other:"기타"};
  const scope = root.dataset.session, csrf = root.dataset.csrf;
  const readOnly = root.dataset.view === "read";
  const controllers = new Set();
  const hashController = new AbortController(); controllers.add(hashController);
  let invalid = false, busy = false, pending = null;
  let pendingFailure = null, canReprepare = false, selectionRevision = 0;
  let tasks = [], task = null, submissions = [], selected = null, generation = 0;
  let files = [], submissionContext = null, decisionContext = null, outcome = "approved";
  const show = (id, text, error = false) => {
    $(id).textContent = text;
    $(id).classList.toggle("error", error);
  };
  const date = value => value ? new Intl.DateTimeFormat("ko-KR", {
    timeZone:"Asia/Seoul", year:"numeric", month:"2-digit", day:"2-digit", hour:"2-digit", minute:"2-digit"
  }).format(new Date(value)) : "—";
  const bytes = size => `${Number(size).toLocaleString()} B`;
  const cell = (row, text, long = false) => {
    const td = document.createElement("td"); td.textContent = text; if (long) td.className = "long"; row.append(td); return td;
  };
  const button = (text, handler) => {
    const b = document.createElement("button"); b.type = "button"; b.textContent = text;
    b.addEventListener("click", handler); return b;
  };
  function stop(message, login = false) {
    invalid = true; pending = null; pendingFailure = null; canReprepare = false; files = [];
    submissionContext = decisionContext = null;
    for (const controller of controllers) controller.abort();
    for (const dialog of root.querySelectorAll("dialog[open]")) dialog.close();
    for (const id of ["tasks","detail","attachments","rounds","history-rows","history-detail"]) $(id)?.replaceChildren();
    $("pending-panel")?.setAttribute("hidden", "");
    for (const input of root.querySelectorAll("button,input,select,textarea")) input.disabled = true;
    show("message", message, true);
    if (login) location.assign("/login");
  }
  async function api(path, body, key, binary = false) {
    if (invalid) throw new Error("접속이 변경되었습니다. 화면을 다시 열어 주세요.");
    const controller = new AbortController(); controllers.add(controller);
    const timeout = setTimeout(() => controller.abort(), 30000);
    try {
      const headers = {"X-Results-Session":scope};
      if (key) headers["Idempotency-Key"] = key;
      const options = {method:body === undefined ? "GET" : "POST", headers, credentials:"same-origin", cache:"no-store", signal:controller.signal};
      if (body !== undefined) {
        const form = new FormData(); form.set("csrf_token", csrf); form.set("body", JSON.stringify(body)); options.body = form;
      }
      const response = await fetch(`/results/data/${path}`, options);
      if (response.status === 401 || (response.redirected && new URL(response.url).pathname === "/login")) {
        stop("로그인이 만료되었습니다.", true); throw new Error("다시 로그인해 주세요.");
      }
      const returnedScope = response.headers.get("X-Results-Session");
      if (returnedScope && returnedScope !== scope) { stop("로그인 접속이 변경되었습니다."); throw new Error("화면을 다시 열어 주세요."); }
      if (binary && response.ok) return response.arrayBuffer();
      const data = await response.json();
      if (!response.ok || data.ok !== true) {
        const error = new Error(data.error?.message || `요청 실패 (${response.status})`);
        error.status = response.status; error.code = data.error?.code;
        if (response.status === 403 || error.code === "RESULTS_SESSION_CHANGED") stop(error.message);
        throw error;
      }
      return data.data;
    } catch (error) {
      if (error.name === "AbortError") throw new Error("응답을 확인하지 못했습니다. 저장 요청이었다면 처리 결과를 확인해 주세요.");
      if (error instanceof SyntaxError) throw new Error("응답 형식을 확인하지 못했습니다. 화면을 새로고침해 주세요.");
      throw error;
    } finally { clearTimeout(timeout); controllers.delete(controller); }
  }
  const action = (operation, target, body, key) => api(`actions/${operation}/${encodeURIComponent(target)}`, body, key);
  const fail = error => { if (!invalid) show("message", error.message || "처리 결과를 확인하지 못했습니다.", true); };
  function lock(value) {
    busy = value;
    for (const id of ["submit-save","decision-save","lookup","retry","reprepare","upload-files","description","reason","view"]) {
      if ($(id)) $(id).disabled = value || Boolean(pending && !["lookup","retry","reprepare"].includes(id));
    }
    for (const id of ["tasks","rounds"]) for (const b of $(id)?.querySelectorAll("button") || []) b.disabled = value || Boolean(pending);
    updateActions();
  }
  function updateActions() {
    if (root.dataset.page !== "results" || readOnly) return;
    const own = !$("view") || $("view").value === "own";
    $("submit-open").hidden = !own;
    $("rework-start").hidden = !own || task?.task_status !== "returned";
    $("rework-start").disabled = busy || Boolean(pending) || !task;
    $("approve-open").hidden = $("return-open").hidden = own;
    $("submit-open").disabled = busy || Boolean(pending) || !task || task.task_status !== "in_progress";
    const ready = !own && task?.task_status === "submitted" && selected?.status === "submitted" && selected === submissions[0];
    $("approve-open").disabled = $("return-open").disabled = busy || Boolean(pending) || !ready;
  }
  async function digest(content) {
    if (typeof globalThis.RemoteWorkFileHash?.sha256 !== "function") throw new Error("파일 검증 기능을 불러오지 못했습니다. 화면을 새로고침한 뒤 다시 시도해 주세요.");
    return globalThis.RemoteWorkFileHash.sha256(content, {signal: hashController.signal});
  }
  async function download(file) {
    try {
      show("message", `${file.name} 다운로드 중…`);
      const content = await api(`attachments/${file.attachment_id}/download`, undefined, undefined, true);
      if (content.byteLength !== file.size || await digest(content) !== file.sha256) throw new Error("첨부파일 크기·해시가 일치하지 않아 다운로드를 중단했습니다.");
      if (invalid) return;
      const url = URL.createObjectURL(new Blob([content], {type:"application/octet-stream"}));
      const link = document.createElement("a"); link.href = url; link.download = file.name; root.append(link); link.click(); link.remove();
      setTimeout(() => URL.revokeObjectURL(url), 1000); show("message", "첨부파일을 다운로드했습니다.");
    } catch (error) { fail(error); }
  }
  function renderFiles(id, attachments) {
    $(id).replaceChildren();
    if (!attachments.length) { const li = document.createElement("li"); li.textContent = "첨부 자료가 없습니다."; $(id).append(li); }
    for (const file of attachments) {
      const li = document.createElement("li"), name = document.createElement("span");
      name.textContent = `${file.name} · ${bytes(file.size)}`; li.append(name, button("다운로드", () => download(file))); $(id).append(li);
    }
  }
  function renderDetail(item) {
    ++selectionRevision;
    selected = item;
    const title = `${task.task_id} · ${task.title}\n담당자: ${task.assignee_name}\n업무 상태: ${labels[task.task_status]} · 마감일: ${task.due_date}`;
    $("detail").textContent = title + (item ? `\n\n${item.round_no}차 제출 · ${reviewLabels[item.status]}\n제출자: ${item.submitted_by_name}\n제출 시각: ${date(item.submitted_at)}\n\n${item.description}\n\n검토 시각: ${date(item.reviewed_at)}\n${item.status === "returned" ? "반려 사유" : "검토 의견"}: ${item.reason || "—"}` : "\n\n아직 제출된 결과물이 없습니다.");
    if (task.task_status === "returned") $("detail").textContent += "\n\n재작업을 시작한 후 다시 제출할 수 있습니다.";
    renderFiles("attachments", item?.attachments || []);
    $("rounds").replaceChildren();
    for (const round of submissions) {
      const row = document.createElement("tr"); row.classList.toggle("selected", item === round);
      cell(row, `${round.round_no}차 제출`); cell(row, date(round.submitted_at)); cell(row, reviewLabels[round.status]);
      cell(row, "").append(button("선택", () => { if (!busy && !pending) renderDetail(round); })); $("rounds").append(row);
    }
    if (!submissions.length) { const row = document.createElement("tr"); const td = cell(row,"제출 이력이 없습니다."); td.colSpan = 4; $("rounds").append(row); }
    updateActions();
  }
  async function choose(item) {
    if (busy || pending) return;
    const serial = ++generation; task = item; submissions = []; selected = null;
    $("detail").textContent = "결과를 불러오고 있습니다."; $("attachments").replaceChildren(); $("rounds").replaceChildren(); updateActions();
    try {
      const [current, data] = await Promise.all([api(`tasks/${item.task_id}`),api(`tasks/${item.task_id}/submissions`)]);
      if (serial !== generation || invalid) return;
      task = current; submissions = data.items;
      tasks = tasks.map(t => t.task_id === task.task_id ? {...t,...task,round_no:submissions[0]?.round_no || null} : t);
      renderDetail(submissions[0] || null); renderTasks(); show("message", "선택한 업무의 결과를 표시했습니다.");
    } catch (error) { if (serial === generation) { task = null; selected = null; $("detail").textContent = "선택한 결과를 표시할 수 없습니다."; updateActions(); fail(error); } }
  }
  function renderTasks() {
    const status = $("status").value, query = $("search").value.trim().toLocaleLowerCase();
    const visible = tasks.filter(t => (!status || t.task_status === status) && `${t.task_id} ${t.title} ${t.assignee_name}`.toLocaleLowerCase().includes(query));
    $("tasks").replaceChildren(); $("list-count").textContent = `조회 ${visible.length}건`;
    for (const item of visible) {
      const row = document.createElement("tr"); row.classList.toggle("selected", item.task_id === task?.task_id);
      cell(row,"").append(button("선택", () => choose(item))); cell(row,item.task_id); cell(row,item.title,true); cell(row,item.assignee_name);
      cell(row,item.round_no ? `${item.round_no}차` : "—"); cell(row,labels[item.task_status]); cell(row,item.due_date); $("tasks").append(row);
    }
    if (!visible.length) { const row = document.createElement("tr"), td = cell(row,"조회 조건에 맞는 업무가 없습니다."); td.colSpan = 7; $("tasks").append(row); }
    if (task && !visible.some(t => t.task_id === task.task_id)) { task = null; selected = null; submissions = []; $("detail").textContent = "업무를 선택해 주세요."; $("attachments").replaceChildren(); $("rounds").replaceChildren(); updateActions(); }
  }
  async function refresh(wanted = task?.task_id) {
    if (pending || busy) return;
    const serial = ++generation;
    task = null; selected = null; submissions = []; updateActions();
    $("detail").textContent = "업무를 선택해 주세요."; $("attachments").replaceChildren(); $("rounds").replaceChildren();
    const view = $("view")?.value || "own";
    $("list-title").textContent = readOnly ? "업무 결과물 · 읽기 전용" : view === "own" ? "내 결과 제출 목록" : "결과물 검토 목록";
    try {
      show("message", "목록을 불러오고 있습니다.");
      const data = readOnly ? {items:[await api(`tasks/${root.dataset.taskId}`)]} : await api(`tasks?scope=${view}`);
      if (serial !== generation || invalid) return;
      tasks = data.items; renderTasks(); show("message", tasks.length ? "업무를 선택해 주세요." : "현재 조회할 업무가 없습니다.");
      const chosen = tasks.find(t => t.task_id === wanted);
      if (chosen && (!$("status").value || chosen.task_status === $("status").value)) await choose(chosen);
      else if (wanted && !chosen) show("message","선택한 업무가 현재 조회 범위에 없습니다. 권한과 조회 범위를 확인해 주세요.");
    } catch (error) { if (serial === generation) { tasks = []; renderTasks(); fail(error); } }
  }
  function renderPendingFiles() {
    $("pending-files").replaceChildren();
    files.forEach((file,index) => {
      const li = document.createElement("li"), span = document.createElement("span"); span.textContent = `${file.name} · ${bytes(file.size)}`;
      li.append(span, button("제거", () => { if (!busy && !pending) { files.splice(index,1); renderPendingFiles(); } })); $("pending-files").append(li);
    });
  }
  async function openSubmission() {
    try {
      if (!task || pending || busy) return;
      const capturedTask = {...task}, previous = submissions[0], id = capturedTask.task_id, serial = generation;
      lock(true);
      const context = await api(`tasks/${id}/context`); if (serial !== generation || invalid) return;
      if (context.task_id !== id) throw new Error("제출할 업무가 변경되었습니다. 다시 조회해 주세요.");
      submissionContext = context; files = []; renderPendingFiles();
      $("description").value = previous?.status === "returned" ? previous.description : "";
      $("upload-files").value = ""; show("upload-message", "");
      $("submission-task").textContent = `${id} · ${capturedTask.title}\n담당자: ${capturedTask.assignee_name} · 마감일: ${context.due_date}`;
      $("submission-dialog").showModal();
    } catch (error) { fail(error); } finally { if (!invalid) lock(false); }
  }
  async function openDecision(value) {
    try {
      if (!selected || pending || busy) return;
      const capturedTask = Object.freeze({...task});
      const captured = Object.freeze({...selected, attachments:Object.freeze(selected.attachments.map(file => Object.freeze({...file})))});
      const id = captured.submission_id, serial = generation, revision = selectionRevision;
      lock(true);
      const context = await api(`submissions/${id}/context`);
      if (serial !== generation || revision !== selectionRevision || invalid) return;
      if (context.task_id !== capturedTask.task_id || context.submission_id !== id || context.round_no !== captured.round_no)
        throw new Error("검토 대상이 변경되었습니다. 다시 조회해 주세요.");
      decisionContext = Object.freeze({...context}); outcome = value; const title = value === "returned" ? "반려" : "승인";
      $("decision-title").textContent = title; $("decision-save").textContent = title;
      $("reason-label").textContent = value === "returned" ? "반려 사유 (필수)" : "승인 의견 (선택)";
      $("reason").required = value === "returned"; $("reason").value = "";
      show("decision-message", "");
      $("decision-task").textContent = `${capturedTask.task_id} · ${capturedTask.title}\n담당자: ${capturedTask.assignee_name} / ${captured.round_no}차 · 검토 대기\n검토자: ${root.dataset.name}`;
      $("decision-result").textContent = `${captured.description}\n제출 시각: ${date(captured.submitted_at)}`;
      renderFiles("decision-files", captured.attachments); $("decision-dialog").showModal();
    } catch (error) { fail(error); } finally { if (!invalid) lock(false); }
  }
  async function upload(file, taskId) {
    const content = await file.arrayBuffer(), sha256 = await digest(content);
    const item = await action("upload-start", taskId, {name:file.name,size:file.size,sha256});
    const data = new Uint8Array(content);
    for (let offset = 0; offset < data.length; offset += 16384) {
      const part = data.subarray(offset,offset + 16384);
      const encoded = btoa(String.fromCharCode(...part));
      await action("upload-chunk", item.attachment_id, {offset,data:encoded});
    }
    const saved = await action("upload-complete", item.attachment_id, {});
    if (saved.sha256 !== sha256 || saved.size !== file.size) throw new Error("업로드 확인값이 다릅니다.");
    return saved.attachment_id;
  }
  async function prepareWrite(operation, target, body, taskId, actionName) {
    const issued = await action("key", target, {action:actionName});
    pending = Object.freeze({operation,target,body:JSON.parse(JSON.stringify(body)),taskId,action:actionName,key:issued.request_key});
    pendingFailure = null; canReprepare = false;
    $("pending-panel").hidden = false; $("retry").hidden = $("reprepare").hidden = true;
    await sendPending();
  }
  async function successWrite(result) {
    const confirmed = pending, id = confirmed.taskId;
    const actionName = {"submission.submitted":"결과 제출", "submission.approved":"승인", "submission.returned":"반려", "task.rework_started":"재작업 시작"}[confirmed.action];
    const round = result?.round_no ? ` · ${result.round_no}차` : "";
    // Identify the confirmed request even after another task or action is selected.
    pending = null; pendingFailure = null; canReprepare = false; $("pending-panel").hidden = true;
    $("submission-dialog").close(); $("decision-dialog").close(); files = []; lock(false);
    show("save-confirmed", `확인된 처리 결과 — 업무 ${id}${round} · ${actionName} 저장 성공. 이후 조회 권한이 바뀌어도 이 처리 결과는 유지됩니다.`);
    $("save-confirmed").hidden = false;
    await refresh(id); if (!$("message").classList.contains("error")) show("message", "서버 저장을 확인하고 목록을 갱신했습니다.");
  }
  async function sendPending() {
    if (!pending) return;
    pendingFailure = null; canReprepare = false; $("reprepare").hidden = true;
    lock(true); show("pending-message", "서버 저장을 확인하고 있습니다.");
    try { const result = await action(pending.operation,pending.target,pending.body,pending.key); await successWrite(result); }
    catch (error) {
      if (invalid) return;
      // Only a definite application rejection plus a later not_recorded receipt
      // permits a new preparation. A timeout/5xx must keep its original request.
      if ([400,409,410,413,422].includes(error.status) && error.code) pendingFailure = error.code;
      show("pending-message", `${error.message} 원본 요청을 보존했습니다. 처리 결과를 먼저 확인해 주세요.`, true);
      const guidance = `${error.message} 이 창을 닫고 상단의 ‘처리 결과 확인’을 눌러 주세요.`;
      if ($("submission-dialog").open) show("upload-message",guidance,true);
      if ($("decision-dialog").open) show("decision-message",guidance,true);
      $("retry").hidden = true; fail(error);
    } finally { if (!invalid) lock(false); }
  }
  async function lookup() {
    if (!pending || busy) return;
    lock(true);
    try {
      const data = await action("lookup",pending.target,{action:pending.action},pending.key);
      if (data.state === "completed") await successWrite(data.result);
      else if (data.state === "not_recorded") {
        canReprepare = Boolean(pendingFailure);
        show("pending-message",canReprepare ? "저장되지 않은 실패 요청입니다. 최신 정보를 조회한 뒤 다시 작성할 수 있습니다." : "저장 기록이 없습니다. 같은 요청을 직접 다시 전송할 수 있습니다.");
        $("reprepare").hidden = !canReprepare; $("retry").hidden = canReprepare;
      }
      else throw new Error("처리 결과 형식을 확인할 수 없습니다.");
    } catch (error) { fail(error); } finally { if (!invalid) lock(false); }
  }
  async function reprepare() {
    if (!pending || busy || !canReprepare || !pendingFailure) return;
    const previous = pending, chosenFiles = [...files];
    pending = null; pendingFailure = null; canReprepare = false;
    $("pending-panel").hidden = true; $("reprepare").hidden = true;
    $("submission-dialog").close(); $("decision-dialog").close(); lock(false);
    await refresh(previous.taskId);
    if (invalid || task?.task_id !== previous.taskId) return;
    if (previous.operation === "submit" && task.task_status === "in_progress") {
      await openSubmission();
      if (!invalid && $("submission-dialog").open) {
        $("description").value = previous.body.description; files = chosenFiles; renderPendingFiles();
        show("upload-message","최신 업무 정보를 확인했습니다. 내용을 확인하고 결과 제출을 다시 눌러 주세요.");
      }
    } else if (previous.operation === "decide" && selected?.submission_id === previous.target && selected.status === "submitted") {
      await openDecision(previous.body.outcome);
      if (!invalid && $("decision-dialog").open) {
        $("reason").value = previous.body.reason;
        show("decision-message","최신 검토 정보를 확인했습니다. 같은 제출물인지 확인한 뒤 처리해 주세요.");
      }
    } else show("message","최신 정보를 조회했습니다. 처리할 업무와 제출물을 다시 확인해 주세요.");
  }
  function startResults() {
    $("filters").addEventListener("submit", e => { e.preventDefault(); if (!pending && !busy) refresh(); });
    $("task-back")?.addEventListener("click", e => {
      if (pending || busy) { e.preventDefault(); show("message", "진행 중인 요청의 처리 결과를 먼저 확인해 주세요.", true); }
    });
    for (const name of ["files","rounds"]) $(name+"-tab").addEventListener("click", () => {
      for (const other of ["files","rounds"]) { $(other+"-tab").setAttribute("aria-selected",String(other === name)); $(other+"-panel").hidden = other !== name; }
    });
    if (readOnly) { refresh(root.dataset.taskId); return; }
    $("view")?.addEventListener("change", () => refresh(null));
    $("submit-open").addEventListener("click", openSubmission);
    $("rework-start").addEventListener("click", async () => {
      if (!task || pending || busy) return;
      lock(true);
      try {
        const id = task.task_id, context = await api(`tasks/${id}/rework-context`);
        await prepareWrite("rework",id,{expected_row_version:context.row_version,expected_assignment_revision:context.assignment_revision},id,"task.rework_started");
      } catch (error) { fail(error); } finally { if (!invalid) lock(false); }
    });
    $("approve-open").addEventListener("click", () => openDecision("approved"));
    $("return-open").addEventListener("click", () => openDecision("returned"));
    $("lookup").addEventListener("click",lookup); $("retry").addEventListener("click", () => { if (!busy) sendPending(); });
    $("reprepare").addEventListener("click",reprepare);
    $("upload-files").addEventListener("change", () => {
      const chosen = [...files,...$("upload-files").files]; $("upload-files").value = "";
      if (chosen.length > 10 || chosen.some(file => file.size > 25*1024*1024)) { show("upload-message","첨부는 파일당 25MiB 이하, 최대 10개입니다.",true); return; }
      files = chosen; renderPendingFiles();
    });
    $("submission-form").addEventListener("submit",async e => {
      e.preventDefault(); if (busy || pending) return;
      const description = $("description").value;
      if (!description.trim()) { show("upload-message","결과 설명을 입력해 주세요.",true); return; }
      lock(true);
      try {
        const ids = [];
        for (const file of files) { show("upload-message", `${file.name} 업로드 중…`); ids.push(await upload(file,submissionContext.task_id)); }
        await prepareWrite("submit",submissionContext.task_id,{expected_row_version:submissionContext.row_version,expected_assignment_revision:submissionContext.assignment_revision,description,attachment_ids:ids},submissionContext.task_id,"submission.submitted");
      } catch (error) { show("upload-message",error.message,true); fail(error); } finally { if (!invalid) lock(false); }
    });
    $("decision-form").addEventListener("submit",async e => {
      e.preventDefault(); if (busy || pending) return;
      const reason = $("reason").value; if (outcome === "returned" && !reason.trim()) { show("message","반려 사유를 입력해 주세요.",true); return; }
      lock(true);
      try { await prepareWrite("decide",decisionContext.submission_id,{expected_row_version:decisionContext.row_version,expected_assignment_revision:decisionContext.assignment_revision,outcome,reason},decisionContext.task_id,`submission.${outcome}`); }
      catch (error) { fail(error); } finally { if (!invalid) lock(false); }
    });
    for (const b of root.querySelectorAll("[data-close]")) b.addEventListener("click", () => { if (!busy) $(b.dataset.close).close(); });
    for (const dialog of root.querySelectorAll("dialog")) dialog.addEventListener("cancel", e => { if (busy) e.preventDefault(); });
    window.addEventListener("beforeunload", e => { if (pending || busy) { e.preventDefault(); e.returnValue = ""; } });
    refresh(root.dataset.taskId || null);
  }
  let events = [], historyTasks = [], cursors = new Map(), historyGeneration = 0;
  function renderHistory() {
    const kind = $("history-kind").value, known = new Set(Object.keys(kinds).filter(k => k !== "other"));
    const data = events.filter(item => !kind || (kind === "other" ? !known.has(item.kind) : item.kind === kind));
    data.sort((a,b) => b.occurred_at.localeCompare(a.occurred_at) || (BigInt(a.event_id) < BigInt(b.event_id) ? 1 : -1));
    $("history-rows").replaceChildren(); $("history-detail").textContent = "기록을 선택해 주세요.";
    for (const item of data) {
      const row = document.createElement("tr");
      cell(row,"").append(button("선택", () => {
        const title = historyTasks.find(t => t.task_id === item.task_id)?.title || "";
        $("history-detail").textContent = `${item.task_id} · ${title}\n수행자: ${item.actor_name}\n변경 시각: ${date(item.occurred_at)}\n변경 유형: ${kinds[item.kind] || "기타"}\n\n${item.summary}`;
      }));
      cell(row,item.task_id); cell(row,item.actor_name); cell(row,date(item.occurred_at)); cell(row,kinds[item.kind] || "기타"); cell(row,item.summary,true); $("history-rows").append(row);
    }
    if (!data.length) { const row=document.createElement("tr"),td=cell(row,"조회 조건에 맞는 이력이 없습니다."); td.colSpan=6; $("history-rows").append(row); }
    const more = [...cursors.values()].some(Boolean); $("history-more").hidden = !more;
    $("history-count").textContent = `표시 ${data.length}건${more ? " · 이전 이력이 더 있습니다." : ""}`;
  }
  async function historyPage(append = false) {
    const serial = ++historyGeneration;
    $("history-more").disabled = true;
    if (!append) { events = []; cursors.clear(); $("history-rows").replaceChildren(); $("history-detail").textContent = "기록을 선택해 주세요."; }
    try {
      const ids = $("history-task").value ? [$("history-task").value] : historyTasks.map(t => t.task_id);
      const collected = [], next = new Map(cursors);
      for (const id of ids) {
        if (append && !cursors.get(id)) continue;
        const query = append ? `?before_event_id=${encodeURIComponent(cursors.get(id))}` : "";
        const data = await api(`tasks/${id}/activity${query}`);
        if (serial !== historyGeneration || invalid) return;
        collected.push(...data.items); next.set(id,data.next_before_event_id);
      }
      const merged = new Map([...events,...collected].map(item => [item.event_id,item])); events = [...merged.values()]; cursors = next;
      renderHistory(); show("message","현재 권한으로 조회한 실제 처리 기록입니다.");
    } catch (error) { if (serial === historyGeneration) { events = []; cursors.clear(); renderHistory(); fail(error); } }
    finally { if (serial === historyGeneration) $("history-more").disabled = false; }
  }
  async function startHistory() {
    $("history-filters").addEventListener("submit",e => { e.preventDefault(); historyPage(); });
    $("history-more").addEventListener("click",() => historyPage(true));
    $("history-kind").addEventListener("change",renderHistory);
    try {
      historyTasks = (await api("history-tasks")).items;
      for (const item of historyTasks) { const option = document.createElement("option"); option.value=item.task_id; option.textContent=`${item.task_id} · ${item.title}`; $("history-task").append(option); }
      await historyPage();
    } catch (error) { fail(error); }
  }
  if (root.dataset.page === "history") startHistory(); else startResults();
})();
