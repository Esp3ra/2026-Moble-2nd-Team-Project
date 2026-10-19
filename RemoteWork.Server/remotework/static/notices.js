// Only prevent repeated clicks in this page; no automatic write retries.
document.querySelectorAll('form[data-notice-write]').forEach(form => {
  form.addEventListener('submit', event => {
    if (form.dataset.submitting === '1') { event.preventDefault(); return; }
    form.dataset.submitting = '1';
    const button = form.querySelector('button[type="submit"]');
    if (button) { button.dataset.label = button.textContent; button.disabled = true; button.textContent = '처리 중…'; }
  });
});
window.addEventListener('pageshow', () => {
  document.querySelectorAll('form[data-notice-write]').forEach(form => {
    delete form.dataset.submitting;
    form.querySelectorAll('button[data-label]').forEach(button => {
      button.disabled = false; button.textContent = button.dataset.label;
    });
  });
});

// Row clicks only choose the ID; the query form is the only detail action.
const choice = document.getElementById('notice-choice');
document.querySelectorAll('tr[data-notice-id]').forEach(row => {
  const choose = () => {
    if (!choice) return;
    choice.value = row.dataset.noticeId;
    document.querySelectorAll('tr[data-notice-id]').forEach(other => {
      const selected = other === row;
      other.classList.toggle('notice-selected', selected);
      other.setAttribute('aria-selected', String(selected));
    });
  };
  row.addEventListener('click', event => {
    if (event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
    event.preventDefault(); choose();
  });
  row.addEventListener('keydown', event => {
    if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); choose(); }
  });
});
