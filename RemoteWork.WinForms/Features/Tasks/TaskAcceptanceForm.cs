using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Features.Tasks.Starting;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks
{
    public partial class TaskAcceptanceForm : Form
    {
        private readonly TaskDetail? source;
        private readonly TaskStartKind kind;
        private readonly ITaskPreparationService? preparationService;
        private readonly bool connected;
        private CancellationTokenSource? requestCancellation;
        private CancellationTokenRegistration sessionRegistration;
        private long generation;
        private bool stopped;
        private bool busy;
        private bool accessBlocked;
        private TaskStartTarget? readyTarget;
        private TaskStartTarget? checkedTarget;

        public TaskStartTarget? CheckedTarget => !stopped && !busy && HasCurrentSession
            && (startWorkflow is null || startWorkflow.State == TaskStartState.Editing)
            && checkedTarget?.UserId == preparationService?.UserId ? checkedTarget : null;
        public bool ShouldRefreshList { get; private set; }
        private bool HasCurrentSession => !accessBlocked
            && (startWorkflow is null
                ? preparationService?.IsCurrent == true && !preparationService.SessionCancellation.IsCancellationRequested
                : startWorkflow.IsCurrent && !startWorkflow.SessionCancellation.IsCancellationRequested
                    && (preparationService is null || preparationService.IsCurrent
                        && !preparationService.SessionCancellation.IsCancellationRequested));

        public TaskAcceptanceForm() : this(null, TaskStartKind.Accept, null, false) { }

        public TaskAcceptanceForm(TaskDetail detail)
            : this(detail ?? throw new ArgumentNullException(nameof(detail)),
                detail.Summary.Status == TaskDisplayStatus.Returned ? TaskStartKind.Rework : TaskStartKind.Accept,
                null, false) { }

        public TaskAcceptanceForm(TaskDetail detail, TaskStartKind kind, ITaskPreparationService? prep)
            : this(detail ?? throw new ArgumentNullException(nameof(detail)), kind, prep, true) { }

        public TaskAcceptanceForm(TaskDetail detail, TaskStartKind kind, ITaskPreparationService? prep,
            TaskStartWorkflow? workflow)
            : this(detail ?? throw new ArgumentNullException(nameof(detail)), kind, prep, true, workflow) { }

        public TaskAcceptanceForm(TaskStartWorkflow workflow, ITaskPreparationService? prep)
            : this(null, (workflow ?? throw new ArgumentNullException(nameof(workflow))).PendingRequest?.Kind ?? TaskStartKind.Accept,
                prep, true, workflow, true) { }

        private TaskAcceptanceForm(TaskDetail? detail, TaskStartKind kind,
            ITaskPreparationService? prep, bool connected, TaskStartWorkflow? workflow = null, bool recovery = false)
        {
            this.source = detail;
            this.kind = kind;
            this.preparationService = prep;
            this.connected = connected;
            startWorkflow = workflow;
            recoveryOnly = recovery;
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            if (!connected)
            {
                // Keep the original read-only preview with only its Close button.
                flowActions.Controls.Remove(btnReloadContext);
                flowActions.Controls.Remove(btnConfirmTarget);
                btnReloadContext.Dispose();
                btnConfirmTarget.Dispose();
                layoutDetails.Controls.Remove(lblWriteContext);
                layoutDetails.Controls.Remove(txtWriteContext);
                layoutDetails.Controls.Remove(lblSourceNotice);
                lblWriteContext.Dispose();
                txtWriteContext.Dispose();
                lblSourceNotice.Dispose();
                layoutDetails.RowStyles.RemoveAt(7);
                layoutDetails.RowStyles.RemoveAt(6);
                layoutDetails.RowCount = 6;
            }
            else
            {
                btnReloadContext.Visible = btnConfirmTarget.Visible = true;
                lblWriteContext.Visible = txtWriteContext.Visible = lblSourceNotice.Visible = true;
                lblTaskStatus.Text = "조회 당시 상태";
                lblDeadline.Text = "조회 당시 마감";
                lblAssignee.Text = "조회 당시 담당자";
                txtWriteContext.Text = "최신 정보를 아직 조회하지 않았습니다.";
            }
            InitializeStartMode();
            if (detail is null) return;
            TaskSummary task = detail.Summary;
            Text = lblPageTitle.Text = kind == TaskStartKind.Rework ? "재작업 시작 대상 확인" : "업무 수락 대상 확인";
            txtTaskId.Text = task.TaskId;
            txtTaskTitle.Text = task.Title;
            txtAssignee.Text = task.AssigneeName;
            txtTaskStatus.Text = StatusText(task.Status);
            txtStartDate.Text = task.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            txtDeadline.Text = task.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            lblPreparationStatus.Text = connected
                ? "최신 배정과 상태를 조회할 준비 중입니다. 수락·재작업 시작은 저장하지 않습니다."
                : "조회한 대상 정보만 표시합니다. 서버 전송·배정 수락·재작업 시작·상태 변경은 하지 않습니다.";
            if (startWorkflow is not null)
            {
                Text = lblPageTitle.Text = kind == TaskStartKind.Rework ? "재작업 시작" : "업무 수락";
                lblPreparationStatus.Text = "현재 배정·상태를 조회한 뒤 대상을 확인하고 별도 실행 버튼을 눌러 주세요.";
            }
            SetButtons();
        }

        protected override async void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!connected || stopped) return;
            AttachStartWorkflow();
            if (startWorkflow is not null && (recoveryOnly || startWorkflow.State != TaskStartState.Editing))
            { ShowStartWorkflow(); return; }
            if (preparationService is null)
            {
                Fail(new TaskPreparationException(TaskPreparationFailure.NotConfigured));
                return;
            }
            sessionRegistration = preparationService.SessionCancellation.Register(() =>
            {
                if (!stopped && IsHandleCreated && !IsDisposed)
                    try { BeginInvoke((Action)InvalidateSession); }
                    catch (InvalidOperationException) { }
            });
            await RefreshContextAsync();
        }

        /// <summary>Read the latest target on the WinForms UI thread. No task action is sent.</summary>
        public async Task RefreshContextAsync()
        {
            if (startWorkflow is not null && startWorkflow.State != TaskStartState.Editing)
            { ShowStartWorkflow(); return; }
            if (!connected || stopped || IsDisposed || source is null || accessBlocked) return;
            if (preparationService is null)
            { Fail(new TaskPreparationException(TaskPreparationFailure.NotConfigured)); return; }
            if (!HasCurrentSession) { InvalidateSession(); return; }
            long request = ++generation;
            requestCancellation?.Cancel();
            requestCancellation?.Dispose();
            requestCancellation = new CancellationTokenSource();
            CancellationToken token = requestCancellation.Token;
            readyTarget = checkedTarget = null;
            busy = true;
            txtWriteContext.Text = "현재 배정·상태·버전을 조회하고 있습니다.";
            lblPreparationStatus.Text = "조회 중… 이전 대상 확인값을 지웠습니다. 상태는 변경하지 않습니다.";
            SetButtons();
            try
            {
                TaskWriteContext context = await preparationService.GetContextAsync(source.Summary.TaskId, token);
                if (!IsActive(request, token)) return;
                if (!HasCurrentSession) { InvalidateSession(); return; }
                TaskStartTargetCheck check = TaskStartTargetPreparation.Check(kind, source, context, preparationService.UserId);
                txtWriteContext.Text = $"업무 {context.TaskId} · 현재 담당자 #{context.AssigneeId} · {StatusText(context.Status)}"
                    + $"\r\n업무 버전 {context.RowVersion} · 배정 차수 {context.AssignmentRevision}"
                    + $"\r\n서버 변경 시각 {context.UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)}";
                readyTarget = check.Target;
                if (check.Outcome == TaskStartTargetOutcome.Matched)
                    lblPreparationStatus.Text = startWorkflow is null
                        ? "본인 배정과 대상 상태를 조회했습니다. ‘대상 확인’을 눌러 확인값을 보관할 수 있습니다.\r\n실행 권한·마감 조건은 확정하지 않으며 수락·재작업 시작을 저장하지 않습니다."
                        : "본인 배정과 대상 상태를 조회했습니다. 먼저 ‘대상 확인’을 눌러 주세요.\r\n수락·재작업 실행 시 서버가 현재 권한·버전·한국 시간 기간을 다시 검사합니다.";
                else if (check.Outcome == TaskStartTargetOutcome.NotCurrentAssignee)
                {
                    ShouldRefreshList = true;
                    lblPreparationStatus.Text = "현재 본인에게 배정된 업무가 아닙니다. 창을 닫으면 목록·상세를 다시 조회합니다.\r\n대상을 확인하거나 상태를 변경하지 않았습니다.";
                }
                else if (check.Outcome == TaskStartTargetOutcome.SourceStatusChanged)
                {
                    ShouldRefreshList = true;
                    lblPreparationStatus.Text = "처음 조회한 상태와 현재 상태가 다릅니다. 창을 닫으면 목록·상세를 다시 조회합니다.\r\n변경된 상태를 확인한 후 다시 대상을 선택해 주세요.";
                }
                else
                    lblPreparationStatus.Text = "현재 상태는 선택한 수락·재작업 시작의 확인 대상이 아닙니다. 상태는 변경하지 않았습니다.";
            }
            catch (OperationCanceledException)
            {
                if (!IsActive(request, token)) return;
                if (!preparationService.IsCurrent) InvalidateSession();
                else
                {
                    readyTarget = checkedTarget = null;
                    txtWriteContext.Text = "현재 정보 조회가 취소되었습니다.";
                    lblPreparationStatus.Text = "조회가 취소되었습니다. 최신 정보 다시 조회를 눌러 주세요. 상태는 변경하지 않았습니다.";
                }
            }
            catch (Exception error) { if (IsActive(request, token)) Fail(error); }
            finally
            {
                if (IsActive(request, token)) { busy = false; SetButtons(); }
            }
        }

        private bool IsActive(long request, CancellationToken token) => !stopped && !IsDisposed
            && request == generation && !token.IsCancellationRequested;

        private void SetButtons()
        {
            if (!connected || stopped) return;
            if (startWorkflow is not null) { SetStartButtons(); return; }
            bool current = HasCurrentSession;
            btnReloadContext.Enabled = current && !busy;
            btnConfirmTarget.Enabled = current && !busy && readyTarget is not null;
        }

        private void Fail(Exception error)
        {
            if (stopped || IsDisposed) return;
            readyTarget = checkedTarget = null;
            busy = false;
            txtWriteContext.Text = "현재 정보 확인 실패 · 이전 확인값을 지웠습니다.";
            if (error is TaskPreparationException known)
            {
                if (known.Failure is TaskPreparationFailure.AuthenticationExpired or TaskPreparationFailure.Forbidden)
                    accessBlocked = true;
                if (known.Failure is TaskPreparationFailure.NotFound or TaskPreparationFailure.AssignmentChanged
                    or TaskPreparationFailure.VersionConflict) ShouldRefreshList = true;
                lblPreparationStatus.Text = known.Message + (ShouldRefreshList
                    ? "\r\n창을 닫으면 목록·상세를 다시 조회합니다. 상태는 변경하지 않았습니다."
                    : "\r\n대상 확인과 상태 변경은 하지 않았습니다.");
            }
            else lblPreparationStatus.Text = "현재 업무 정보를 확인할 수 없습니다. 최신 정보를 다시 조회해 주세요.\r\n대상 확인과 상태 변경은 하지 않았습니다.";
            SetButtons();
        }

        private void InvalidateSession()
        {
            if (stopped || IsDisposed) return;
            generation++;
            requestCancellation?.Cancel();
            readyTarget = checkedTarget = null;
            busy = false;
            accessBlocked = true;
            txtWriteContext.Clear();
            if (startWorkflow is not null)
            {
                txtTaskId.Clear(); txtTaskTitle.Clear(); txtAssignee.Clear();
                txtTaskStatus.Clear(); txtStartDate.Clear(); txtDeadline.Clear();
            }
            lblPreparationStatus.Text = startWorkflow is null
                ? "로그인이 만료되었거나 접속이 변경되었습니다. 다시 로그인한 뒤 업무를 조회해 주세요.\r\n수락·재작업 시작과 상태 변경은 하지 않았습니다."
                : startWorkflow.Failure == TaskStartFailure.Forbidden
                    ? "현재 로그인에는 이 업무를 시작할 권한이 없습니다. 이 창의 이전 요청 정보를 지웠습니다.\r\n권한과 서버 처리 내역을 확인한 뒤 다시 로그인해 주세요."
                    : "로그인이 만료되었거나 접속이 변경되어 이전 요청 정보를 지웠습니다.\r\n다시 로그인한 뒤 서버의 업무 상태와 처리 내역을 확인해 주세요.";
            SetButtons();
        }

        private async void ReloadContext_Click(object? sender, EventArgs e)
        {
            if (startWorkflow is not null && startWorkflow.CanCheckResult)
                await RunStartOperationAsync(() => startWorkflow.CheckResultAsync());
            else await RefreshContextAsync();
        }

        private async void ConfirmTarget_Click(object? sender, EventArgs e)
        {
            if (startWorkflow is not null) { await ConfirmStartAsync(); return; }
            if (stopped || busy || !HasCurrentSession || readyTarget is null) return;
            checkedTarget = readyTarget;
            lblPreparationStatus.Text = "대상 확인값을 이 창에 보관했습니다. 수락·재작업 시작을 실행한 것은 아닙니다.\r\n업무 상태·제출 회차는 변경하지 않았으며 서버에 저장하지 않았습니다.";
        }

        private void StopRequests()
        {
            if (stopped) return;
            stopped = true;
            generation++;
            readyTarget = checkedTarget = null;
            sessionRegistration.Unregister();
            requestCancellation?.Cancel();
            requestCancellation?.Dispose();
            requestCancellation = null;
            DetachStartWorkflow();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        { StopRequests(); base.OnFormClosed(e); }

        private static string StatusText(TaskDisplayStatus status) => status switch
        {
            TaskDisplayStatus.NotStarted => "진행전", TaskDisplayStatus.InProgress => "진행중",
            TaskDisplayStatus.Submitted => "제출완료", TaskDisplayStatus.Approved => "승인완료",
            TaskDisplayStatus.Returned => "반려완료", _ => "상태 확인 필요"
        };

        private void Close_Click(object? sender, EventArgs e) => Close();
    }
}
