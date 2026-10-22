/* Owner 2 local screen preparation only. No file bytes, network or persistence.
   The disabled, nameless input also keeps the existing server form unchanged. */
(() => {
  "use strict";
  const maxFiles = 10;
  const maxBytes = 25 * 1024 * 1024;
  document.querySelectorAll("[data-task-file-preparation]").forEach(panel => {
    const form = panel.closest("form");
    const input = panel.querySelector("[data-file-input]");
    const choose = panel.querySelector("[data-file-choose]");
    const list = panel.querySelector("[data-file-list]");
    const clear = panel.querySelector("[data-file-clear]");
    const count = panel.querySelector("[data-file-count]");
    const empty = panel.querySelector("[data-file-empty]");
    const feedback = panel.querySelector("[data-file-feedback]");
    if (!form || !input || !choose || !list || !clear || !count || !empty || !feedback) return;
    let files = [];
    let sequence = 0;

    function sizeLabel(size) {
      if (size < 1024) return `${size} B`;
      if (size < 1024 * 1024) return `${(size / 1024).toFixed(1)} KiB`;
      return `${(size / (1024 * 1024)).toFixed(1)} MiB`;
    }
    function announce(message, error = false, focus = false) {
      feedback.textContent = message;
      feedback.classList.toggle("task-file-feedback-error", error);
      feedback.setAttribute("role", error ? "alert" : "status");
      if (focus) feedback.focus();
    }
    function render() {
      list.replaceChildren();
      files.forEach(file => {
        const row = document.createElement("li");
        row.className = "task-file-row";
        const info = document.createElement("div");
        const name = document.createElement("span");
        name.className = "task-file-name";
        name.textContent = file.name;
        const meta = document.createElement("span");
        meta.className = "task-file-meta";
        meta.textContent = `${sizeLabel(file.size)} · 이 화면에서 선택됨 · 전송 안 됨`;
        info.append(name, meta);
        const remove = document.createElement("button");
        remove.type = "button";
        remove.className = "secondary";
        remove.textContent = "선택에서 지우기";
        remove.setAttribute("aria-label", `${file.name} 선택에서 지우기`);
        remove.addEventListener("click", () => {
          files = files.filter(item => item.key !== file.key);
          render();
          announce("선택 목록에서 지웠습니다. 서버 파일은 변경하지 않았습니다.", false, true);
        });
        row.append(info, remove);
        list.append(row);
      });
      count.textContent = `선택한 파일 ${files.length} / ${maxFiles}개`;
      empty.hidden = files.length > 0;
      clear.disabled = files.length === 0;
    }
    function reset(message) {
      files = [];
      input.value = "";
      render();
      announce(message);
    }
    choose.addEventListener("click", () => input.click());
    input.addEventListener("change", () => {
      const selected = Array.from(input.files || []);
      // Keep only display metadata. Do not retain File objects, paths or bytes.
      input.value = "";
      if (!selected.length) return;
      if (files.length + selected.length > maxFiles) {
        announce("첨부파일은 최대 10개까지 선택할 수 있습니다. 이번 선택은 추가하지 않았습니다.", true, true);
        return;
      }
      if (selected.some(file => typeof file.name !== "string" || !file.name.length
          || !Number.isSafeInteger(file.size) || file.size < 0 || file.size > maxBytes)) {
        announce("파일 이름과 크기를 확인해 주세요. 파일당 25 MiB까지 선택할 수 있으며 이번 선택은 추가하지 않았습니다.", true, true);
        return;
      }
      files.push(...selected.map(file => ({ key: ++sequence, name: file.name, size: file.size })));
      render();
      announce("파일을 선택했습니다. 아직 전송·저장되지 않았습니다. 업무 내용을 확인하려면 선택 목록을 먼저 비워 주세요.");
    });
    clear.addEventListener("click", () => reset("선택 목록을 비웠습니다. 서버 파일은 변경하지 않았습니다."));
    form.addEventListener("submit", event => {
      if (!files.length) return;
      event.preventDefault();
      announce("선택한 파일을 함께 저장할 수 없습니다. 선택 목록을 비운 뒤 첨부 없이 진행해 주세요. 입력한 업무 내용은 유지됩니다.", true, true);
    });
    // Clear local names when leaving, restoring a cached page, or logging out.
    window.addEventListener("pagehide", () => reset("페이지를 떠나 선택 목록을 비웠습니다."));
    window.addEventListener("pageshow", event => {
      if (event.persisted) reset("다시 연 화면의 파일 선택 목록을 비웠습니다. 파일 전송은 아직 연결되지 않았습니다.");
    });
    document.querySelectorAll("aside form").forEach(other => {
      other.addEventListener("submit", () => reset("접속을 종료하여 선택 목록을 비웠습니다."));
    });
    form.addEventListener("reset", () => reset("선택 목록을 비웠습니다."));
    render();
    input.disabled = false;
    choose.disabled = false;
  });
})();
