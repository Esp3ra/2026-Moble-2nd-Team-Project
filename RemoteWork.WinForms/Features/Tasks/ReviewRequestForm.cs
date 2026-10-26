using RemoteWork.WinForms.Features.Tasks.ReviewRequests;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks
{
    public partial class ReviewRequestForm : Form
    {
        private readonly TaskReviewRequestsClient? client;
        private readonly string? taskId;
        private readonly CancellationTokenSource lifetime = new();
        private CancellationTokenRegistration registration;
        private ReviewRequestContext? context;
        private bool busy;
        private bool lifetimeDisposed;
        public bool Saved { get; private set; }
        public ReviewRequestForm() : this(PreviewRole.Employee) { }
        public ReviewRequestForm(PreviewRole role)
        {
            InitializeComponent();
            txtTaskSummary.Clear(); txtCurrentStatus.Clear(); txtCurrentDeadline.Clear();
            txtRequestReason.Clear(); txtResultPreview.Clear(); txtRequestReason.MaxLength = 4000;
            lblStatus.Text = "제출완료된 결과에 별도 검토 요청을 보냅니다. 업무 상태와 제출 회차는 바뀌지 않습니다.";
            btnConfirm.Enabled = btnCancel.Enabled = false; txtRequestReason.ReadOnly = true;
            Disposed += (_, _) =>
            {
                if (lifetimeDisposed) return;
                lifetimeDisposed = true;
                lifetime.Cancel(); registration.Dispose(); lifetime.Dispose();
            };
        }
        public ReviewRequestForm(TaskReviewRequestsClient client, string taskId) : this(PreviewRole.Employee)
        { this.client = client; this.taskId = taskId; }
        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (client is null || PreviewActions.IsDesignMode(this)) return;
            registration = client.Session.Cancellation.Register(() =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                try { BeginInvoke((Action)(() => { if (!IsDisposed) Disconnect(); })); }
                catch (InvalidOperationException) { }
            });
            UpdateActions();
            if (client.Pending is not null) RenderPending(); else await ReloadAsync();
        }
        private bool Current => !IsDisposed && !Disposing && !lifetime.IsCancellationRequested && client?.Session.IsCurrent == true && !client.IsBlocked;
        public async Task ReloadAsync()
        {
            if (!Current || busy || client!.Pending is not null || taskId is null) return;
            busy = true; context = null; UpdateActions(); lblStatus.Text = "제출 결과와 현재 지정 검토자를 확인하고 있습니다.";
            try
            {
                var value = await client.ContextAsync(taskId, lifetime.Token);
                if (!Current) return;
                context = value;
                txtTaskSummary.Text = value.TaskId + " · " + value.Title;
                txtCurrentStatus.Text = "제출완료 · " + value.RoundNo + "차 · 검토자: " + value.ReviewerName;
                txtCurrentDeadline.Text = value.DueDate.ToString("yyyy-MM-dd");
                txtResultPreview.Text = value.Description;
                lblStatus.Text = value.Request?.State == "pending"
                    ? "이 제출 회차에는 확인 대기 중인 요청이 있습니다. 검토 요청함에서 확인할 수 있습니다."
                    : "요청 내용을 입력한 뒤 ‘검토 요청 보내기’를 누르세요. 결과를 다시 제출하거나 승인하는 동작이 아닙니다.";
                if (value.Request?.State == "pending") txtRequestReason.Text = value.Request.Reason;
            }
            catch (Exception error) { if (Current) lblStatus.Text = TaskReviewRequestsClient.ErrorText(error); }
            finally { busy = false; if (!IsDisposed) UpdateActions(); }
        }
        private async void Confirm_Click(object? sender, EventArgs e)
        {
            if (!Current || busy) return;
            if (client!.Pending?.Stage == "completed") { Saved = true; client.ClearConfirmed(); DialogResult = DialogResult.OK; Close(); return; }
            busy = true; UpdateActions();
            try
            {
                if (client.Pending is null)
                {
                    if (context is null || context.Request?.State == "pending") return;
                    client.Prepare(context, txtRequestReason.Text);
                }
                await client.SendAsync(lifetime.Token);
                if (Current) RenderPending();
            }
            catch (Exception error) { if (Current) { RenderPending(); lblStatus.Text = TaskReviewRequestsClient.ErrorText(error); } }
            finally { busy = false; if (!IsDisposed) UpdateActions(); }
        }
        private async void Refresh_Click(object? sender, EventArgs e)
        {
            bool blockedRecovery = client?.CanCheckBlockedResult == true && client.Session.IsCurrent && !IsDisposed;
            if ((!Current && !blockedRecovery) || busy) return;
            if (blockedRecovery)
            {
                busy = true; UpdateActions();
                try { await client!.CheckAsync(lifetime.Token); if (Current) RenderPending(); }
                catch (Exception error) { if (!IsDisposed) lblStatus.Text = TaskReviewRequestsClient.ErrorText(error); }
                finally { busy = false; if (!IsDisposed) UpdateActions(); }
                return;
            }
            if (client!.Pending is null) { await ReloadAsync(); return; }
            if (client.Pending.Stage == "failed") { client.ClearConfirmed(); await ReloadAsync(); return; }
            if (client.Pending.Stage != "check") return;
            busy = true; UpdateActions();
            try { await client.CheckAsync(lifetime.Token); if (Current) RenderPending(); }
            catch (Exception error) { if (Current) lblStatus.Text = TaskReviewRequestsClient.ErrorText(error); }
            finally { busy = false; if (!IsDisposed) UpdateActions(); }
        }
        private void RenderPending()
        {
            if (client?.Pending is not { } op) return;
            txtTaskSummary.Text = "업무 " + op.TaskId;
            txtCurrentStatus.Text = op.RoundNo + "차 · " + (op.Action == "task.review_requested" ? "검토 요청" : "요청 확인");
            txtRequestReason.Text = op.Reason;
            lblStatus.Text = op.Stage switch
            {
                "completed" => "서버 저장 완료 · 요청 #" + op.Saved!.RequestId + " · " + TaskReviewRequestsClient.StateText(op.Saved.RequestState),
                "check" => "처리 결과가 아직 확인되지 않았습니다. ‘처리 결과 확인’을 누르세요. 자동 재전송하지 않습니다.",
                "not_recorded" => "서버에 처리 기록이 없습니다. 같은 내용과 요청 번호로 명시적으로 다시 보낼 수 있습니다.",
                "failed" => "요청이 거절됐습니다. 최신 정보를 조회한 뒤 내용을 다시 확인해 주세요.",
                _ => "원래 요청 내용으로 전송을 다시 시도할 수 있습니다."
            };
            if (op.Stage == "completed") Saved = true;
            UpdateActions();
        }
        private void UpdateActions()
        {
            if (client?.IsBlocked == true)
            {
                context = null; txtTaskSummary.Clear(); txtCurrentStatus.Clear(); txtCurrentDeadline.Clear();
                txtRequestReason.Clear(); txtResultPreview.Clear();
                lblStatus.Text = client.CanCheckBlockedResult
                    ? "검토 요청 권한이 변경되어 이전 내용을 숨겼습니다. 원래 요청의 처리 결과만 다시 확인할 수 있습니다."
                    : "검토 요청 권한이 변경되어 이전 내용을 지웠습니다. 다시 로그인해 주세요.";
            }
            var pending = client?.Pending;
            bool ready = Current && !busy && client?.IsBusy != true;
            txtRequestReason.ReadOnly = !ready || pending is not null || context is null || context.Request?.State == "pending";
            btnConfirm.Text = pending?.Stage switch { "completed" => "확인 후 닫기", "not_recorded" => "같은 요청 다시 보내기",
                _ => pending?.Action == "task.review_acknowledged" ? "요청 확인 처리" : "검토 요청 보내기" };
            btnConfirm.Enabled = ready && (pending is null ? context is not null && context.Request?.State != "pending" : pending.Stage is "ready" or "not_recorded" or "completed");
            btnCancel.Text = pending?.Stage == "check" ? "처리 결과 확인" : "최신 정보 조회";
            btnCancel.Enabled = ready && (pending is null || pending.Stage is "check" or "failed");
            if (client?.CanCheckBlockedResult == true && client.Session.IsCurrent)
            { btnCancel.Text = "기존 요청 처리 결과 확인"; btnCancel.Enabled = !busy && !client.IsBusy; }
        }
        private void Disconnect()
        {
            context = null; txtTaskSummary.Clear(); txtCurrentStatus.Clear(); txtCurrentDeadline.Clear();
            txtRequestReason.Clear(); txtResultPreview.Clear(); lblStatus.Text = "접속이 변경되었습니다. 다시 로그인해 주세요.";
            UpdateActions();
        }
        private void ClosePreview_Click(object? sender, EventArgs e) => Close();
    }
}
