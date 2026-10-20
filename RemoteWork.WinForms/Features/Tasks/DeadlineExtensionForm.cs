using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks
{
    public partial class DeadlineExtensionForm : Form
    {
        private readonly TaskDetail? sourceDetail;
        private readonly ITaskPreparationService? preparationService;
        private readonly bool connected;
        private CancellationTokenRegistration sessionRegistration;
        private bool preparationStopped;
        private bool preparationInvalidated;
        private bool initializing = true;
        private DeadlineExtensionInputDraft? checkedInput;
        private bool HasCurrentPreparation => !preparationStopped && !preparationInvalidated && !IsDisposed
            && (!connected || (preparationService?.IsCurrent == true
                && !preparationService.SessionCancellation.IsCancellationRequested));

        // A local policy/input check only. Current assignee, pending request and decision-maker
        // must be checked atomically by the future server implementation.
        public DeadlineExtensionInputDraft? CheckedInput
        {
            get
            {
                if (!HasCurrentPreparation)
                { checkedInput = null; return null; }
                // Programmatic checkbox changes may not raise ValueChanged.
                if (checkedInput is not null
                    && (sourceDetail is null || !dtpRequestedDeadline.Checked
                        || checkedInput.TaskId != sourceDetail.Summary.TaskId
                        || checkedInput.OriginalDueDate != sourceDetail.Summary.DueDate
                        || checkedInput.RequestedDueDate != DateOnly.FromDateTime(dtpRequestedDeadline.Value)
                        || checkedInput.Reason != txtExtensionReason.Text
                        || !DeadlineExtensionPolicy.AllowsRequest(sourceDetail.Summary.Status)
                        || DeadlineExtensionPolicy.DateError(checkedInput.OriginalDueDate,
                            checkedInput.RequestedDueDate, DeadlineExtensionPolicy.KoreanDate(DateTimeOffset.UtcNow)) is not null))
                    checkedInput = null;
                return checkedInput;
            }
            private set => checkedInput = value;
        }

        public DeadlineExtensionForm() : this(PreviewRole.Employee) { }

        public DeadlineExtensionForm(PreviewRole role) : this(role, null, null, false) { }

        public DeadlineExtensionForm(PreviewRole role, TaskDetail detail)
            : this(role, detail ?? throw new ArgumentNullException(nameof(detail)), null, false) { }

        public DeadlineExtensionForm(PreviewRole role, TaskDetail detail, ITaskPreparationService preparationService)
            : this(role, detail ?? throw new ArgumentNullException(nameof(detail)),
                preparationService ?? throw new ArgumentNullException(nameof(preparationService)), true) { }

        private DeadlineExtensionForm(PreviewRole role, TaskDetail? detail, ITaskPreparationService? preparationService, bool connected)
        {
            sourceDetail = detail;
            this.preparationService = preparationService;
            this.connected = connected;
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            Text = "마감 연장 입력 준비";
            txtTaskSummary.Text = detail is null ? "대상 업무 선택 필요"
                : $"{detail.Summary.TaskId} · {detail.Summary.Title}";
            txtCurrentDeadline.Text = detail?.Summary.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "—";
            txtExtensionReason.Clear();
            dtpRequestedDeadline.Checked = false;
            bool allowedState = detail is not null && DeadlineExtensionPolicy.AllowsRequest(detail.Summary.Status);
            dtpRequestedDeadline.Enabled = allowedState;
            txtExtensionReason.Enabled = allowedState;
            btnConfirm.Enabled = allowedState;
            initializing = false;
            ResetPreparationStatus();
            UpdateDateNotice();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!connected || preparationStopped || preparationService is null) return;
            // Borrow only the captured session lifetime. No service request is made here.
            sessionRegistration = preparationService.SessionCancellation.Register(() =>
            {
                if (!preparationStopped && IsHandleCreated && !IsDisposed)
                    try { BeginInvoke((Action)InvalidatePreparation); }
                    catch (InvalidOperationException) { }
            });
            if (!HasCurrentPreparation) InvalidatePreparation();
        }

        private void InvalidatePreparation()
        {
            if (preparationStopped || preparationInvalidated || IsDisposed) return;
            preparationInvalidated = true;
            checkedInput = null;
            inputErrors.Clear();
            txtTaskSummary.Clear();
            txtCurrentDeadline.Clear();
            txtExtensionReason.Clear();
            txtExtensionNotice.Clear();
            dtpRequestedDeadline.Checked = false;
            dtpRequestedDeadline.CustomFormat = "'—'";
            dtpRequestedDeadline.Enabled = txtExtensionReason.Enabled = btnConfirm.Enabled = false;
            lblPreparationStatus.ForeColor = Color.FromArgb(30, 42, 61);
            lblPreparationStatus.Text = "로그인이 만료되었거나 접속이 변경되어 이전 업무 입력을 지웠습니다.\r\n창을 닫고 현재 로그인에서 업무를 다시 조회해 주세요. 연장 요청은 접수하지 않았습니다.";
        }

        private void StopPreparation()
        {
            if (preparationStopped) return;
            preparationStopped = true;
            checkedInput = null;
            sessionRegistration.Unregister();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        { StopPreparation(); base.OnFormClosed(e); }

        private void InputChanged(object? sender, EventArgs e)
        {
            CheckedInput = null;
            if (initializing) return;
            if (preparationInvalidated || preparationStopped) return;
            if (!HasCurrentPreparation) { InvalidatePreparation(); return; }
            inputErrors.Clear();
            ResetPreparationStatus();
            UpdateDateNotice();
        }

        private void ResetPreparationStatus()
        {
            lblPreparationStatus.ForeColor = Color.FromArgb(30, 42, 61);
            lblPreparationStatus.Text = sourceDetail is null
                ? "목록에서 업무 상세를 조회한 뒤 연장 대상을 선택해 주세요. 요청이 접수되지 않습니다."
                : !DeadlineExtensionPolicy.AllowsRequest(sourceDetail.Summary.Status)
                    ? "진행중·반려완료에서만 연장을 신청할 수 있습니다. 진행전은 관리자의 일반 날짜 수정을 이용합니다.\r\n현재 화면은 입력 준비이며 요청 접수·마감 변경은 하지 않습니다."
                    : "진행중·반려완료는 마감 전·후 신청 가능 · 신청자 이외의 현재 업무 관리자가 판단합니다.\r\n현재 화면은 입력 준비이며 요청 접수·마감 변경은 하지 않습니다.";
        }

        private void UpdateDateNotice()
        {
            txtExtensionNotice.Text = "현재 마감일: " + txtCurrentDeadline.Text
                + "\r\n희망 마감일: " + (dtpRequestedDeadline.Checked
                    ? DateOnly.FromDateTime(dtpRequestedDeadline.Value).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : "선택 필요")
                + "\r\n기준: 현재 기한보다 뒤 · 승인 당일 한국 날짜 이상 · 승인해도 업무 상태 유지";
        }

        private void ConfirmInput_Click(object? sender, EventArgs e)
        {
            CheckedInput = null;
            inputErrors.Clear();
            if (!HasCurrentPreparation)
            { InvalidatePreparation(); return; }
            if (sourceDetail is null || !btnConfirm.Enabled) return;
            Control? firstInvalid = null;
            if (!dtpRequestedDeadline.Checked)
            {
                inputErrors.SetError(dtpRequestedDeadline, "희망 마감일을 선택해 주세요.");
                firstInvalid = dtpRequestedDeadline;
            }
            else if (DeadlineExtensionPolicy.DateError(sourceDetail.Summary.DueDate,
                DateOnly.FromDateTime(dtpRequestedDeadline.Value),
                DeadlineExtensionPolicy.KoreanDate(DateTimeOffset.UtcNow)) is { } dateError)
            {
                inputErrors.SetError(dtpRequestedDeadline, dateError);
                firstInvalid = dtpRequestedDeadline;
            }
            if (string.IsNullOrWhiteSpace(txtExtensionReason.Text))
            {
                inputErrors.SetError(txtExtensionReason, "연장 사유를 입력해 주세요.");
                firstInvalid ??= txtExtensionReason;
            }
            if (firstInvalid is not null)
            {
                lblPreparationStatus.ForeColor = Color.Firebrick;
                lblPreparationStatus.Text = inputErrors.GetError(firstInvalid) + "\r\n요청 접수·마감 변경은 하지 않았습니다.";
                firstInvalid.Focus();
                return;
            }

            CheckedInput = new DeadlineExtensionInputDraft(
                sourceDetail.Summary.TaskId,
                sourceDetail.Summary.DueDate,
                DateOnly.FromDateTime(dtpRequestedDeadline.Value),
                txtExtensionReason.Text);
            lblPreparationStatus.ForeColor = Color.FromArgb(30, 42, 61);
            lblPreparationStatus.Text = "로컬 상태·날짜·사유 확인 · 접수·마감 변경 안 됨\r\n서버 연결 시 현재 담당자·대기 신청 중복·판단 가능한 다른 관리자와 최신 날짜/버전을 다시 검사합니다.";
        }

        private void ClosePreview_Click(object? sender, EventArgs e)
        {
            Close();
        }

    }
}
