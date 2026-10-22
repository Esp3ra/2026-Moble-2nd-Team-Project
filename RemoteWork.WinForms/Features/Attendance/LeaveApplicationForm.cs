using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Attendance
{
    public partial class LeaveApplicationForm : Form
    {
        private readonly ApiClient.Session? session;
        private readonly ILeaveWorkflow workflow = new UnconnectedLeaveWorkflow();
        private readonly CancellationTokenSource lifetime = new();
        private CancellationTokenRegistration sessionEnding;
        private bool busy, finished, uncertain, ended;
        private Guid? requestId;
        private LeaveDraft? frozenDraft;
        public bool SubmissionCompleted { get; private set; }
        public Task PendingOperation { get; private set; } = Task.CompletedTask;

        public LeaveApplicationForm() : this(null, null) { }

        public LeaveApplicationForm(ApiClient.Session? session, ILeaveWorkflow? workflow)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            this.session = session ?? LeaveInput.Current();
            this.workflow = workflow ?? new UnconnectedLeaveWorkflow();
            txtApplicant.Text = LeaveInput.Applicant(this.session);
            dtpStartDate.Value = dtpEndDate.Value = DateTime.Today;
            cmbOptions.SelectedIndex = 0;
            cmbOptions.SelectedIndexChanged += (_, _) => UpdateChargeNotice();
            lblStatus.Text = this.workflow.IsConnected
                ? "기간과 사유를 확인하고 신청해 주세요. 일수·잔여 연차는 서버에서 확인합니다."
                : "서버 연결 전입니다. 입력 내용을 확인할 수 있으며 신청은 저장되지 않습니다.";
            btnApplyLeave.Text = this.workflow.IsConnected ? "신청" : "입력 확인";
            btnApplyLeave.Enabled = LeaveInput.Live(this.session);
            UpdateChargeNotice();
            if (this.session is not null)
                sessionEnding = this.session.Cancellation.Register(EndSession);
            FormClosed += (_, _) => EndLifetime();
        }

        private void EndSession()
        {
            if (ended) return;
            lifetime.Cancel();
            try { if (IsHandleCreated) BeginInvoke((Action)(() => { if (!IsDisposed) Close(); })); }
            catch (InvalidOperationException) { }
        }

        private void EndLifetime()
        {
            if (ended) return;
            ended = true;
            sessionEnding.Unregister();
            lifetime.Cancel();
        }

        private void UpdateChargeNotice()
        {
            lblCharge.Text = Draft().DeductsAnnualBalance
                ? "연차 잔여일 차감 대상 · 실제 차감 일수는 서버에서 계산합니다."
                : "병가·공가는 연차 잔여일을 차감하지 않습니다.";
        }

        private bool Live => !ended && !IsDisposed && !Disposing && LeaveInput.Live(session);
        private LeaveDraft Draft() => new(cmbOptions.SelectedItem as string ?? "",
            DateOnly.FromDateTime(dtpStartDate.Value), DateOnly.FromDateTime(dtpEndDate.Value),
            txtLeaveReason.Text.Trim());
        private void SetInput(bool enabled)
        {
            cmbOptions.Enabled = dtpStartDate.Enabled = dtpEndDate.Enabled = txtLeaveReason.Enabled = enabled;
        }

        private async void ApplyLeave_Click(object? sender, EventArgs e)
        { PendingOperation = ApplyAsync(); await PendingOperation; }

        private async Task ApplyAsync()
        {
            if (!Live || busy || finished) return;
            var draft = Draft();
            string? error = LeaveInput.Error(draft);
            if (error is not null) { lblStatus.Text = error; return; }
            if (!workflow.IsConnected)
            {
                lblStatus.Text = "입력 형식 확인 완료. 서버 미연결로 신청은 저장되지 않았습니다. 일수·잔여 연차·중복 기간은 서버 연결 후 확인합니다.";
                return;
            }
            if (frozenDraft is not null && !LeaveInput.Same(frozenDraft, draft))
            { lblStatus.Text = "처리 중인 신청 내용이 달라졌습니다. 이전 신청 결과부터 확인해 주세요."; return; }
            requestId ??= Guid.NewGuid();
            frozenDraft ??= draft;
            busy = true;
            SetInput(false);
            btnApplyLeave.Enabled = false;
            lblStatus.Text = uncertain ? "같은 신청의 처리 결과 확인 중…" : "연차 신청 중…";
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, session!.Cancellation);
            try
            {
                var result = await workflow.SubmitAsync(frozenDraft, requestId.Value, linked.Token);
                if (!Live || linked.IsCancellationRequested) return;
                if (result?.Request is null || result.RequestId != requestId || result.Request.ApplicantId != session.User.Id
                    || result.Request.Status != "pending" || string.IsNullOrWhiteSpace(result.Request.Id)
                    || string.IsNullOrWhiteSpace(result.Request.Revision)
                    || !LeaveInput.Same(result.Request.Draft, frozenDraft))
                    throw new InvalidDataException();
                SubmissionCompleted = finished = true;
                lblStatus.Text = "연차 신청 완료 · 검토 대기";
                btnApplyLeave.Text = "신청 완료";
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
            catch (Exception ex) when (ex is ApiException or HttpRequestException or InvalidDataException or InvalidOperationException or OperationCanceledException)
            {
                if (!Live) return;
                uncertain = true;
                lblStatus.Text = "신청 결과 확인 필요. 신청 내용과 요청 번호를 유지했습니다. 새 신청으로 자동 전환하지 않습니다.";
                btnApplyLeave.Text = "같은 신청 확인";
            }
            finally
            {
                busy = false;
                if (Live) btnApplyLeave.Enabled = !finished;
            }
        }

        private void ClosePreview_Click(object? sender, EventArgs e)
        {
            Close();
        }

    }
}
