using RemoteWork.WinForms.Features.Tasks.ReviewRequests;

namespace RemoteWork.WinForms.Features.Tasks;

public partial class TaskReviewRequestsForm : Form
{
    private readonly TaskReviewRequestsClient? client;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenRegistration registration;
    private bool busy;
    private bool loading;
    private bool lifetimeDisposed;
    public TaskReviewRequestsForm() { InitializeComponent(); }
    public TaskReviewRequestsForm(TaskReviewRequestsClient client, bool reviewing) : this()
    {
        this.client = client;
        cmbScope.SelectedIndex = reviewing && client.Session.User.Role == "admin" ? 1 : 0;
        cmbScope.Enabled = client.Session.User.Role == "admin";
        Disposed += (_, _) =>
        {
            if (lifetimeDisposed) return;
            lifetimeDisposed = true;
            lifetime.Cancel(); registration.Dispose(); lifetime.Dispose();
        };
    }
    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (client is null) return;
        registration = client.Session.Cancellation.Register(() =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)(() => { if (!IsDisposed) { gridRequests.Rows.Clear(); txtDetail.Clear(); lblStatus.Text = "접속이 변경되었습니다. 다시 로그인해 주세요."; UpdateActions(); } })); }
            catch (InvalidOperationException) { }
        });
        UpdateActions(); await ReloadAsync();
    }
    private bool Current => !IsDisposed && !Disposing && client?.Session.IsCurrent == true && !client.IsBlocked && !lifetime.IsCancellationRequested;
    private ReviewRequestItem? Selected => gridRequests.SelectedRows.Count == 1 ? gridRequests.SelectedRows[0].Tag as ReviewRequestItem : null;
    public async Task ReloadAsync()
    {
        if (!Current || busy) return;
        busy = true; loading = true; gridRequests.Rows.Clear(); txtDetail.Clear(); UpdateActions();
        lblStatus.Text = "검토 요청을 조회하고 있습니다.";
        try
        {
            var items = await client!.ListAsync(cmbScope.SelectedIndex == 1, lifetime.Token);
            if (!Current) return;
            foreach (var item in items)
            {
                int row = gridRequests.Rows.Add(item.RequestId, item.Title, item.RoundNo + "차", item.RequesterName,
                    item.ReviewerName, TaskReviewRequestsClient.StateText(item.State), item.CreatedAt.ToOffset(TimeSpan.FromHours(9)).ToString("MM-dd HH:mm"));
                gridRequests.Rows[row].Tag = item;
            }
            lblStatus.Text = items.Count == 0 ? "조회된 검토 요청이 없습니다." : $"검토 요청 {items.Count}건 · 요청 확인은 결과물 승인·반려와 별개입니다.";
        }
        catch (Exception error) { if (Current) lblStatus.Text = TaskReviewRequestsClient.ErrorText(error); }
        finally { busy = false; loading = false; if (!IsDisposed) { SelectionChanged(this, EventArgs.Empty); UpdateActions(); } }
    }
    private void SelectionChanged(object? sender, EventArgs e)
    {
        if (loading) return;
        txtDetail.Text = Selected is { } row ? $"업무 {row.TaskId} · 제출 {row.RoundNo}차\r\n요청자: {row.RequesterName} · 검토자: {row.ReviewerName}\r\n{row.Reason}\r\n\r\n상태: {TaskReviewRequestsClient.StateText(row.State)}"
            + (row.AcknowledgedAt is { } at ? $" · 확인 시각 {at.ToOffset(TimeSpan.FromHours(9)):yyyy-MM-dd HH:mm}" : "")
            : "요청을 선택하면 내용이 표시됩니다.";
        UpdateActions();
    }
    private async void Refresh_Click(object? sender, EventArgs e) => await ReloadAsync();
    private void ScopeChanged(object? sender, EventArgs e)
    {
        if (client is null || busy) return;
        gridRequests.Rows.Clear(); txtDetail.Clear(); lblStatus.Text = "조회 범위가 변경됐습니다. 조회를 눌러 주세요."; UpdateActions();
    }
    private async void Acknowledge_Click(object? sender, EventArgs e)
    {
        if (!Current || busy || Selected is not { } row || client!.Pending is not null || cmbScope.SelectedIndex != 1) return;
        busy = true; UpdateActions();
        try
        {
            client.PrepareAcknowledge(row);
            await client.SendAsync(lifetime.Token);
            if (!Current) return;
            if (client.Pending?.Stage == "completed") client.ClearConfirmed();
            busy = false; await ReloadAsync();
        }
        catch (Exception error) { if (Current) lblStatus.Text = TaskReviewRequestsClient.ErrorText(error); }
        finally { busy = false; if (!IsDisposed) UpdateActions(); }
    }
    private async void Result_Click(object? sender, EventArgs e)
    {
        bool blockedRecovery = client?.CanCheckBlockedResult == true && client.Session.IsCurrent && !IsDisposed;
        if ((!Current && !blockedRecovery) || busy || (client!.Pending is null && !blockedRecovery)) return;
        busy = true; UpdateActions();
        try { using var dialog = new ReviewRequestForm(client, client.Pending?.TaskId ?? ""); dialog.ShowDialog(this); }
        finally { busy = false; if (Current) await ReloadAsync(); }
    }
    private void UpdateActions()
    {
        if (client?.IsBlocked == true)
        {
            loading = true; gridRequests.Rows.Clear(); loading = false; txtDetail.Clear();
            lblStatus.Text = "검토 요청 권한이 변경되어 이전 내용을 지웠습니다. 다시 로그인해 주세요.";
        }
        bool ready = Current && !busy && client?.IsBusy != true;
        btnRefresh.Enabled = ready;
        cmbScope.Enabled = ready && client!.Session.User.Role == "admin";
        btnAcknowledge.Enabled = ready && cmbScope.SelectedIndex == 1 && client!.Pending is null
            && Selected is { State: "pending" } row && row.ReviewerId == client.Session.User.Id && row.RequestedBy != client.Session.User.Id;
        btnResult.Enabled = ready && client!.Pending is not null;
        if (client?.CanCheckBlockedResult == true && client.Session.IsCurrent)
            btnResult.Enabled = !busy && !client.IsBusy;
        gridRequests.Enabled = ready;
    }
}
