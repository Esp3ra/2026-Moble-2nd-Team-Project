using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Lifecycle;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks;

public partial class TaskLifecycleForm : Form
{
    private readonly TaskLifecycleKind kind;
    private readonly string? taskId;
    private readonly string? extensionId;
    private readonly ITaskLifecycleService? service;
    private readonly TaskLifecycleWorkflow? workflow;
    private TaskLifecycleContext? context;
    private TaskLifecycleRequest? checkedRequest;
    private CancellationTokenSource? readCancellation;
    private CancellationTokenRegistration registration;
    private long generation;
    private bool stopped, busy, filling;
    public bool ShouldRefreshList { get; private set; }
    public TaskLifecycleResult? ConfirmedResult { get; private set; }

    public TaskLifecycleForm() { InitializeComponent(); SetButtons(); }
    public TaskLifecycleForm(TaskLifecycleKind kind, string taskId, ITaskLifecycleService service,
        TaskLifecycleWorkflow workflow, string? extensionId = null)
    {
        this.kind = kind; this.taskId = taskId; this.service = service; this.workflow = workflow; this.extensionId = extensionId;
        InitializeComponent(); Text = Caption(kind); lblTarget.Text = Text + " · 업무 " + taskId;
        workflow.Changed += WorkflowChanged;
        registration = service.SessionCancellation.Register(() => Post(ShowWorkflow)); SetButtons();
    }
    public TaskLifecycleForm(TaskLifecycleWorkflow workflow, ITaskLifecycleService service)
        : this(workflow.PendingRequest?.Kind ?? TaskLifecycleKind.Cancel,
            workflow.PendingRequest?.TaskId ?? "", service, workflow, workflow.PendingRequest?.ExtensionId) { }
    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (PreviewActions.IsDesignMode(this) || workflow is null) return;
        if (workflow.IsCurrent && workflow.State == TaskLifecycleState.Editing) await ReloadAsync(); else ShowWorkflow();
    }
    public async Task ReloadAsync()
    {
        if (stopped || service?.IsCurrent != true || taskId is null || workflow?.State != TaskLifecycleState.Editing) return;
        long mine = BeginRead(); context = null;
        try
        {
            var current = await service.GetContextAsync(taskId, readCancellation!.Token);
            if (!Current(mine)) return;
            context = current;
            filling = true;
            try
            {
                txtRequestedDueDate.Text = (current.PendingExtension?.RequestedDueDate ?? current.DueDate.AddDays(current.DueDate < DateOnly.MaxValue ? 1 : 0)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                txtReason.Clear();
            }
            finally { filling = false; }
            RenderContext();
            lblStatus.Text = current.WriteEnabled ? "현재 업무를 조회했습니다. 입력·대상을 확인한 뒤 실행해 주세요."
                : TaskLifecycleException.MessageFor(TaskLifecycleFailure.TechnicalConnectionRequired);
        }
        catch (Exception error) { if (Current(mine)) { ClearInput(); lblStatus.Text = ReadError(error); } }
        finally { if (mine == generation && !stopped) { busy = false; SetButtons(); } }
    }
    private async Task CheckInputAsync()
    {
        if (context is null || service?.IsCurrent != true || workflow?.State != TaskLifecycleState.Editing || busy) return;
        var before = context; long mine = BeginRead();
        try
        {
            var current = await service.GetContextAsync(before.TaskId, readCancellation!.Token);
            if (!Current(mine)) return;
            if (current.RowVersion != before.RowVersion || current.AssignmentRevision != before.AssignmentRevision)
            { context = null; throw new ArgumentException("업무·신청이 변경됐습니다. 최신 업무를 다시 조회해 주세요."); }
            context = current;
            TaskExtensionRequest? request = current.PendingExtension;
            if (extensionId is not null && request?.ExtensionId != extensionId)
                throw new ArgumentException("선택한 신청이 더 이상 대기 중이 아닙니다. 요청함을 새로 조회해 주세요.");
            DateOnly? date = kind == TaskLifecycleKind.RequestExtension
                ? DateOnly.TryParseExact(txtRequestedDueDate.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                    ? parsed : throw new ArgumentException("새 마감일을 YYYY-MM-DD 형식으로 입력해 주세요.") : null;
            checkedRequest = TaskLifecycleRequest.Create(kind, current, service.UserId, txtReason.Text, date, request);
            RenderContext(); lblStatus.Text = "입력·대상을 확인했습니다. ‘실행’을 눌러 서버에 요청하세요. 아직 저장하지 않았습니다.";
        }
        catch (Exception error) { if (Current(mine)) { checkedRequest = null; lblStatus.Text = ReadError(error); } }
        finally { if (mine == generation && !stopped) { busy = false; SetButtons(); } }
    }
    private long BeginRead()
    {
        readCancellation?.Cancel(); readCancellation?.Dispose();
        readCancellation = CancellationTokenSource.CreateLinkedTokenSource(service!.SessionCancellation);
        checkedRequest = null; busy = true; SetButtons(); return ++generation;
    }
    private bool Current(long mine) => !stopped && !IsDisposed && mine == generation && service?.IsCurrent == true;
    private async void Confirm_Click(object? sender, EventArgs e)
    {
        if (busy || stopped || workflow?.IsCurrent != true) return;
        if (workflow.CanRetry) await workflow.RetrySameRequestAsync();
        else if (checkedRequest is { } input && workflow.State == TaskLifecycleState.Editing) await workflow.SubmitAsync(input);
        else await CheckInputAsync();
        if (!stopped) ShowWorkflow();
    }
    private async void CheckResult_Click(object? sender, EventArgs e)
    { if (workflow?.CanCheckResult == true && !busy) { await workflow.CheckResultAsync(); if (!stopped) ShowWorkflow(); } }
    private async void Reload_Click(object? sender, EventArgs e) => await ReloadAsync();
    private void Acknowledge_Click(object? sender, EventArgs e)
    {
        if (workflow?.State != TaskLifecycleState.Succeeded || !workflow.IsCurrent) return;
        ConfirmedResult = workflow.Result; ShouldRefreshList = true;
        if (workflow.AcknowledgeSuccess()) { DialogResult = DialogResult.OK; Close(); }
    }
    private void InputEdited(object? sender, EventArgs e)
    { if (filling || stopped) return; checkedRequest = null; if (workflow?.State == TaskLifecycleState.Editing) lblStatus.Text = "입력이 바뀌었습니다. 입력·대상을 다시 확인해 주세요."; SetButtons(); }
    private void Close_Click(object? sender, EventArgs e) => Close();
    private void WorkflowChanged(object? sender, EventArgs e) => Post(ShowWorkflow);
    private void ShowWorkflow()
    {
        if (stopped || IsDisposed || workflow is null) return;
        var snapshot = workflow.Snapshot;
        if (!workflow.IsCurrent)
        {
            ClearInput(); ConfirmedResult = null; ShouldRefreshList = false;
            lblStatus.Text = "접속이 변경되어 이전 입력을 지웠습니다. 현재 로그인에서 다시 조회해 주세요.";
            SetButtons(); return;
        }
        else if (snapshot.State != TaskLifecycleState.Editing || snapshot.Failure is not null)
        { checkedRequest = null; lblStatus.Text = snapshot.Message; if (snapshot.Failure == TaskLifecycleFailure.Forbidden) ClearInput(); }
        if (snapshot.State == TaskLifecycleState.Succeeded && snapshot.Result is { } result)
        {
            ConfirmedResult = result; ShouldRefreshList = true;
            lblStatus.Text = $"서버 처리 성공 확인 · 업무 {result.TaskId} · 버전 {result.RowVersion}\r\n닫으면 목록·요청함을 새로 조회합니다. 취소는 별도 표시이며 5개 업무 상태는 유지됩니다.";
        }
        SetButtons();
    }
    private void SetButtons()
    {
        bool current = !stopped && workflow?.IsCurrent == true;
        bool editing = current && !busy && workflow?.State == TaskLifecycleState.Editing && context is not null;
        txtRequestedDueDate.ReadOnly = !editing || kind != TaskLifecycleKind.RequestExtension;
        txtReason.ReadOnly = !editing || kind is TaskLifecycleKind.ApproveExtension or TaskLifecycleKind.WithdrawExtension;
        lblDate.Visible = txtRequestedDueDate.Visible = kind != TaskLifecycleKind.Cancel;
        lblReason.Text = kind == TaskLifecycleKind.RejectExtension ? "판단 사유 (선택)" : "사유";
        btnReload.Enabled = current && !busy && workflow?.State == TaskLifecycleState.Editing;
        btnConfirm.Text = workflow?.CanRetry == true ? "같은 요청 재전송" : checkedRequest is null ? "입력·대상 확인" : "실행";
        btnConfirm.Enabled = current && !busy && (editing && context?.WriteEnabled == true && !context.IsCancelled || workflow?.CanRetry == true);
        btnCheckResult.Enabled = current && !busy && workflow?.CanCheckResult == true;
        btnAcknowledge.Enabled = current && workflow?.State == TaskLifecycleState.Succeeded;
        btnClose.Enabled = workflow?.IsBusy != true;
    }
    private void RenderContext()
    {
        if (context is null) return;
        txtContext.Text = $"현재 상태: {StatusText(context.Status)} · 마감일: {context.DueDate:yyyy-MM-dd} · 배정: {context.AssignmentRevision}\r\n"
            + (context.IsCancelled ? "취소된 업무 · " + context.Cancellation?.Reason : "업무 관리자: " + string.Join(", ", context.ManagerUserIds.Select(id => "#" + id)))
            + (context.PendingExtension is { } pending ? $"\r\n대기 신청 #{pending.ExtensionId} · 신청자 #{pending.RequestedBy} · 요청 마감 {pending.RequestedDueDate:yyyy-MM-dd}\r\n신청 사유: {pending.Reason}" : "\r\n대기 중인 연장 신청 없음");
    }
    private void ClearInput()
    { context = null; checkedRequest = null; filling = true; try { txtReason.Clear(); txtRequestedDueDate.Clear(); txtContext.Text = "현재 업무 확인 필요"; } finally { filling = false; } }
    private static string ReadError(Exception error) => error is ArgumentException or TaskLifecycleException ? error.Message
        : error is OperationCanceledException ? "업무 조회를 취소했습니다. 다시 확인해 주세요." : "업무 조회 중 오류가 발생했습니다. 서버 연결을 확인해 주세요.";
    internal static string Caption(TaskLifecycleKind kind) => kind switch
    { TaskLifecycleKind.Cancel => "업무 취소", TaskLifecycleKind.RequestExtension => "마감 연장 신청", TaskLifecycleKind.ApproveExtension => "마감 연장 승인", TaskLifecycleKind.RejectExtension => "마감 연장 반려", _ => "마감 연장 철회" };
    private static string StatusText(Queries.TaskDisplayStatus status) => status switch
    { Queries.TaskDisplayStatus.NotStarted => "진행전", Queries.TaskDisplayStatus.InProgress => "진행중", Queries.TaskDisplayStatus.Submitted => "제출완료", Queries.TaskDisplayStatus.Approved => "승인완료", _ => "반려완료" };
    private void Post(Action action)
    {
        if (stopped || IsDisposed || !IsHandleCreated) return;
        try { if (InvokeRequired) BeginInvoke((Action)(() => { if (!stopped && !IsDisposed) action(); })); else action(); } catch (InvalidOperationException) { }
    }
    private void StopOwnedQueries()
    {
        if (stopped) return; stopped = true; ++generation; checkedRequest = null;
        readCancellation?.Cancel(); readCancellation?.Dispose(); registration.Unregister();
        if (workflow is not null) workflow.Changed -= WorkflowChanged;
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    { if (workflow?.IsBusy == true && workflow.IsCurrent && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; lblStatus.Text = "요청 처리 중입니다. 응답·결과 확인 안내를 기다려 주세요."; } base.OnFormClosing(e); }
}
