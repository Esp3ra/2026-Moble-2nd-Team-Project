using RemoteWork.WinForms.Features.Tasks.Lifecycle;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks;

public partial class TaskExtensionRequestsForm : Form
{
    private readonly ITaskLifecycleService? service;
    private readonly ITaskLifecycleSource? source;
    private CancellationTokenSource? cancellation;
    private CancellationTokenRegistration registration;
    private long generation;
    private bool stopped, busy, opening;
    public bool ShouldRefreshList { get; private set; }
    public TaskExtensionRequestsForm() { InitializeComponent(); cmbScope.SelectedIndex = 0; cmbStatus.SelectedIndex = 0; UpdateActions(); }
    public TaskExtensionRequestsForm(ITaskLifecycleService service, ITaskLifecycleSource source, TaskQueryScope scope) : this()
    {
        this.service = service; this.source = source;
        cmbScope.SelectedIndex = scope == TaskQueryScope.Managed ? 1 : 0;
        cmbScope.Enabled = service.IsAdministrator;
        registration = service.SessionCancellation.Register(() => Post(Disconnect)); UpdateActions();
    }
    protected override async void OnShown(EventArgs e)
    { base.OnShown(e); if (!PreviewActions.IsDesignMode(this) && service is not null) { if (service.IsCurrent) await ReloadAsync(); else Disconnect(); } }
    public async Task ReloadAsync()
    {
        if (service?.IsCurrent != true || stopped || opening) return;
        cancellation?.Cancel(); cancellation?.Dispose(); cancellation = CancellationTokenSource.CreateLinkedTokenSource(service.SessionCancellation);
        long mine = ++generation; busy = true; gridRequests.Rows.Clear(); txtDetail.Text = "신청을 선택하면 사유·판단 내용이 표시됩니다.";
        lblStatus.Text = "마감 연장 신청 조회 중…"; UpdateActions();
        try
        {
            TaskExtensionStatus? status = cmbStatus.SelectedIndex switch
            { 1 => TaskExtensionStatus.Pending, 2 => TaskExtensionStatus.Approved, 3 => TaskExtensionStatus.Rejected, 4 => TaskExtensionStatus.Withdrawn, 5 => TaskExtensionStatus.Cancelled, _ => null };
            var rows = await service.GetExtensionsAsync(cmbScope.SelectedIndex == 1 ? TaskQueryScope.Managed : TaskQueryScope.Own, status, cancellation.Token);
            if (!Current(mine)) return;
            foreach (var row in rows)
            {
                int index = gridRequests.Rows.Add(row.ExtensionId, row.TaskId, "#" + row.RequestedBy,
                    row.OriginalDueDate.ToString("yyyy-MM-dd"), row.RequestedDueDate.ToString("yyyy-MM-dd"), StatusText(row.Status));
                gridRequests.Rows[index].Tag = row;
            }
            SelectionChanged(this, EventArgs.Empty);
            lblStatus.Text = rows.Count == 0 ? "조회된 마감 연장 신청이 없습니다." : $"마감 연장 신청 {rows.Count}건 · 최종 현재 권한은 실행 전 서버에서 다시 검사합니다.";
        }
        catch (Exception error)
        { if (Current(mine)) { gridRequests.Rows.Clear(); lblStatus.Text = error is TaskLifecycleException ? error.Message : error is OperationCanceledException ? "조회가 취소됐습니다. 다시 조회해 주세요." : "연장 신청을 조회할 수 없습니다. 서버 연결을 확인해 주세요."; } }
        finally { if (mine == generation && !stopped) { busy = false; UpdateActions(); } }
    }
    private bool Current(long mine) => !stopped && !IsDisposed && mine == generation && service?.IsCurrent == true;
    private TaskExtensionRequest? Selection => gridRequests.SelectedRows.Count == 1 ? gridRequests.SelectedRows[0].Tag as TaskExtensionRequest : null;
    private void SelectionChanged(object? sender, EventArgs e)
    {
        txtDetail.Text = Selection is { } row ? $"업무 {row.TaskId} · 배정 {row.AssignmentRevision} · 신청자 #{row.RequestedBy}\r\n신청 사유: {row.Reason}\r\n판단 사유: {row.DecisionReason ?? "없음"}"
            : "신청을 선택하면 사유·판단 내용이 표시됩니다.";
        UpdateActions();
    }
    private async void Refresh_Click(object? sender, EventArgs e) => await ReloadAsync();
    private void FilterChanged(object? sender, EventArgs e)
    {
        if (service is null || stopped) return;
        ++generation; cancellation?.Cancel(); busy = false; gridRequests.Rows.Clear();
        lblStatus.Text = "조회 조건이 변경됐습니다. 선택 조건 조회를 눌러 주세요.";
        txtDetail.Text = "신청을 다시 조회해 주세요."; UpdateActions();
    }
    private async void Approve_Click(object? sender, EventArgs e) => await OpenActionAsync(TaskLifecycleKind.ApproveExtension);
    private async void Reject_Click(object? sender, EventArgs e) => await OpenActionAsync(TaskLifecycleKind.RejectExtension);
    private async void Withdraw_Click(object? sender, EventArgs e) => await OpenActionAsync(TaskLifecycleKind.WithdrawExtension);
    private async void Result_Click(object? sender, EventArgs e) => await OpenActionAsync(null);
    private async Task OpenActionAsync(TaskLifecycleKind? kind)
    {
        if (busy || opening || service?.IsCurrent != true || source is null) return;
        TaskLifecycleWorkflow? workflow;
        try
        {
            var pending = source.GetPendingLifecycleWorkflow();
            if (kind is null || pending is not null) { workflow = pending; kind = null; }
            else if (Selection is { Status: TaskExtensionStatus.Pending } selected)
                workflow = source.GetLifecycleWorkflow(kind.Value, selected.TaskId, selected.ExtensionId);
            else return;
        }
        catch (OperationCanceledException) { return; }
        if (workflow is null) return;
        opening = true; UpdateActions();
        try
        {
            using var form = workflow.PendingRequest is not null ? new TaskLifecycleForm(workflow, service)
                : new TaskLifecycleForm(kind!.Value, Selection!.TaskId, service, workflow, Selection.ExtensionId);
            form.ShowDialog(this);
            if (form.ShouldRefreshList && service.IsCurrent && !stopped)
            { ShouldRefreshList = true; opening = false; await ReloadAsync(); }
        }
        finally { opening = false; if (!stopped) UpdateActions(); }
    }
    private void UpdateActions()
    {
        bool current = !stopped && service?.IsCurrent == true && !busy && !opening;
        var row = Selection; bool pending = current && row?.Status == TaskExtensionStatus.Pending;
        try { if (source?.GetPendingLifecycleWorkflow() is not null) pending = false; } catch (OperationCanceledException) { pending = false; }
        btnApprove.Enabled = btnReject.Enabled = pending && cmbScope.SelectedIndex == 1 && service!.IsAdministrator && row!.RequestedBy != service.UserId;
        btnWithdraw.Enabled = pending && row!.RequestedBy == service!.UserId;
        btnRefresh.Enabled = current; cmbStatus.Enabled = current; cmbScope.Enabled = current && service!.IsAdministrator;
        try { btnResult.Enabled = current && source?.GetPendingLifecycleWorkflow() is not null; } catch (OperationCanceledException) { btnResult.Enabled = false; }
    }
    private void Disconnect()
    {
        if (stopped) return; ++generation; cancellation?.Cancel(); busy = false;
        gridRequests.Rows.Clear(); txtDetail.Text = "이전 접속의 신청을 지웠습니다.";
        lblStatus.Text = "접속이 변경되었습니다. 현재 로그인에서 요청함을 다시 열어 주세요."; ShouldRefreshList = false; UpdateActions();
    }
    private void Post(Action action)
    { if (stopped || IsDisposed || !IsHandleCreated) return; try { if (InvokeRequired) BeginInvoke((Action)(() => { if (!stopped && !IsDisposed) action(); })); else action(); } catch (InvalidOperationException) { } }
    private void StopOwnedQueries()
    { if (stopped) return; stopped = true; ++generation; cancellation?.Cancel(); cancellation?.Dispose(); registration.Unregister(); }
    private static string StatusText(TaskExtensionStatus status) => status switch
    { TaskExtensionStatus.Pending => "대기", TaskExtensionStatus.Approved => "승인", TaskExtensionStatus.Rejected => "반려", TaskExtensionStatus.Withdrawn => "철회", _ => "업무 취소로 종료" };
}
