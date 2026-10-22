using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Attendance
{
    public partial class LeaveDecisionForm : Form
    {
        private readonly ApiClient.Session? session;
        private readonly ILeaveWorkflow workflow = new UnconnectedLeaveWorkflow();
        private readonly LeaveRequestView? request;
        private readonly CancellationTokenSource lifetime = new();
        private CancellationTokenRegistration sessionEnding;
        private bool busy, ended, uncertain;
        private Guid? requestId;
        private string? frozenDecision, frozenReason;
        public bool DecisionCompleted { get; private set; }
        public Task PendingOperation { get; private set; } = Task.CompletedTask;

        public LeaveDecisionForm() : this(null, null, null) { }

        public LeaveDecisionForm(ApiClient.Session? session, ILeaveWorkflow? workflow, LeaveRequestView? request)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            this.session = session ?? LeaveInput.Current();
            this.workflow = workflow ?? new UnconnectedLeaveWorkflow();
            this.request = request;
            txtApplicant.Text = request is null ? "" : $"{request.ApplicantName} · {request.Department}";
            txtLeaveType.Text = request?.Draft.Type ?? "";
            txtPeriod.Text = request is null ? "" : $"{request.Draft.StartDate:yyyy-MM-dd} ~ {request.Draft.EndDate:yyyy-MM-dd}";
            txtRequestReason.Text = request?.Draft.Reason ?? "";
            lblCharge.Text = request is null ? "신청 내역을 선택하면 연차 차감 여부를 확인할 수 있습니다."
                : request.Draft.DeductsAnnualBalance ? "연차 잔여일 차감 대상 · 실제 차감 일수는 서버에서 확인합니다."
                : "병가·공가는 연차 잔여일을 차감하지 않습니다.";
            lblStatus.Text = !LeaveInput.Administrator(this.session) ? "관리자 로그인이 필요합니다."
                : request is null ? "지정된 직원의 신청을 목록에서 선택해 주세요."
                : !request.CanReview ? "검토 권한이 확인된 신청만 처리할 수 있습니다."
                : !this.workflow.IsConnected ? "서버 연결 전입니다. 승인·반려는 저장되지 않습니다."
                : request.Status != "pending" ? "이미 처리된 신청입니다. 목록을 새로 확인해 주세요."
                : "신청 내용을 확인해 주세요. 병가·공가는 연차 잔여일을 차감하지 않습니다.";
            SetButtons();
            if (this.session is not null) sessionEnding = this.session.Cancellation.Register(EndSession);
            FormClosed += (_, _) => EndLifetime();
        }

        private bool Live => !ended && !IsDisposed && !Disposing && LeaveInput.Administrator(session);
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
        private void SetButtons()
        {
            bool ready = Live && workflow.IsConnected && request is { CanReview: true, Status: "pending" }
                && !busy && !DecisionCompleted;
            btnApprove.Enabled = ready && (!uncertain || frozenDecision == "approved");
            btnReject.Enabled = ready && (!uncertain || frozenDecision == "rejected");
            txtDecisionReason.ReadOnly = busy || uncertain || DecisionCompleted || !ready;
        }
        private async void Approve_Click(object? sender, EventArgs e)
        { PendingOperation = DecideAsync("approved"); await PendingOperation; }
        private async void Reject_Click(object? sender, EventArgs e)
        { PendingOperation = DecideAsync("rejected"); await PendingOperation; }

        private async Task DecideAsync(string decision)
        {
            if (decision is not ("approved" or "rejected")) return;
            if (!Live || busy || DecisionCompleted || !workflow.IsConnected
                || request is not { CanReview: true, Status: "pending" }) return;
            string reason = txtDecisionReason.Text.Trim();
            if (reason.Length > LeaveInput.TextLimit) { lblStatus.Text = "검토 의견은 1,000자 이내로 입력해 주세요."; return; }
            if (frozenDecision is not null && (frozenDecision != decision || frozenReason != reason))
            { lblStatus.Text = "이전 처리 결과부터 확인해 주세요. 처리 내용은 유지됩니다."; return; }
            requestId ??= Guid.NewGuid();
            frozenDecision ??= decision;
            frozenReason ??= reason;
            busy = true;
            SetButtons();
            lblStatus.Text = uncertain ? "같은 처리 결과 확인 중…" : "검토 결과 저장 중…";
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, session!.Cancellation);
            try
            {
                var result = await workflow.DecideAsync(request, decision, frozenReason, requestId.Value, linked.Token);
                if (!Live || linked.IsCancellationRequested) return;
                if (result?.Request is null || result.RequestId != requestId || result.Request.Id != request.Id
                    || result.Request.ApplicantId != request.ApplicantId || result.Request.Status != decision
                    || string.IsNullOrWhiteSpace(result.Request.Revision)
                    || !LeaveInput.Same(result.Request.Draft, request.Draft)) throw new InvalidDataException();
                DecisionCompleted = true;
                lblStatus.Text = decision == "approved" ? "승인 완료" : "반려 완료";
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
            catch (Exception ex) when (ex is ApiException or HttpRequestException or InvalidDataException or InvalidOperationException or OperationCanceledException)
            {
                if (!Live) return;
                uncertain = true;
                lblStatus.Text = "처리 결과 확인 필요. 같은 요청 번호와 내용을 유지합니다. 목록의 최신 결과를 확인해 주세요.";
                if (decision == "approved") btnApprove.Text = "승인 결과 확인";
                else btnReject.Text = "반려 결과 확인";
            }
            finally { busy = false; if (Live) SetButtons(); }
        }

        private void ClosePreview_Click(object? sender, EventArgs e)
        {
            Close();
        }
    }
}
