using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks;

public partial class TaskPreparationForm : Form
{
    private readonly ITaskPreparationService? service;
    private readonly string? taskId;
    private CancellationTokenSource? requestCancellation;
    private CancellationTokenRegistration sessionRegistration;
    private long generation;
    private bool stopped;
    private bool busy;
    private bool initialized;
    private bool pageReady;
    private bool contextReady;
    private bool manage;
    private int page = 1;
    private int total;
    private TaskWriteContext? context;
    private TaskUserCandidate? assignee;
    private readonly SortedDictionary<int, string> managers = new();
    public TaskPreparationSelection? Selection { get; private set; }

    public TaskPreparationForm() : this(null, null) { }
    public TaskPreparationForm(ITaskPreparationService? service, string? taskId)
    {
        this.service = service;
        this.taskId = taskId;
        InitializeComponent();
        if (PreviewActions.IsDesignMode(this)) return;
        cmbKind.SelectedIndex = 0;
        initialized = true;
        SetState();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (service is null) { Fail(new TaskPreparationException(TaskPreparationFailure.NotConfigured)); return; }
        sessionRegistration = service.SessionCancellation.Register(() =>
        {
            if (!stopped && IsHandleCreated && !IsDisposed)
                try { BeginInvoke((Action)(() => { if (!stopped) CancelSession(); })); }
                catch (InvalidOperationException) { /* The owned window is closing. */ }
        });
        await RefreshContextAsync();
    }

    public async Task RefreshContextAsync()
    {
        if (service is null || stopped) return;
        var (request, token) = BeginRequest();
        ClearSelection();
        contextReady = false;
        manage = false;
        lblContext.Text = "현재 업무 정보를 확인하고 있습니다.";
        try
        {
            if (taskId is not null) context = await service.GetContextAsync(taskId, token);
            if (!Current(request, token)) return;
            contextReady = true;
            manage = service.CanSelectCandidates && (context is null || context.ManagerUserIds.Contains(service.UserId));
            assignee = context is null ? null : new(context.AssigneeId, "현재 담당자", "현재 담당자 · #" + context.AssigneeId);
            if (context is not null)
                foreach (int id in context.ManagerUserIds) managers.Add(id, "업무 관리자 · #" + id);
            if (taskId == null && manage)
                managers.Add(service.UserId, "등록자 · #" + service.UserId);
            lblContext.Text = context is null ? "새 업무의 담당자·업무 관리자 선택 준비입니다. 아직 업무가 생성되지 않았습니다.\r\n관리자는 1명 이상이며 등록자는 자동 포함됩니다. 최초 등록에서는 등록자를 제외할 수 없습니다.\r\n업무 관리자는 제출 검토자와 별개입니다."
                : $"업무 {context.TaskId} · 현재 담당자 #{context.AssigneeId}\r\n업무 버전 {context.RowVersion} · 배정 차수 {context.AssignmentRevision} · 현재 상태 {StatusText(context.Status)}\r\n업무 관리자는 제출 검토자와 별개이며, 조회 결과는 최종 저장 허용을 뜻하지 않습니다.";
            RenderSelection();
            busy = false;
            SetState();
            if (manage) await LoadCandidatesAsync(1);
            else lblStatus.Text = "현재 업무의 연결 정보를 조회했습니다. 담당자·관리자 변경 선택은 제공하지 않습니다.";
        }
        catch (OperationCanceledException) { if (Current(request, token)) CancelSession(); }
        catch (Exception error) { if (Current(request, token)) Fail(error); }
        finally { FinishRequest(request); }
    }

    public async Task LoadCandidatesAsync(int requestedPage)
    {
        if (service is null || stopped || !contextReady || !manage) return;
        var (request, token) = BeginRequest();
        string search = txtSearch.Text;
        var kind = cmbKind.SelectedIndex == 1 ? TaskCandidateKind.Manager : TaskCandidateKind.Assignee;
        try
        {
            var result = await service.GetCandidatesAsync(new(kind, taskId, search, requestedPage), token);
            if (!Current(request, token)) return;
            page = result.Page;
            total = result.Total;
            foreach (var candidate in result.Items)
            {
                int index = gridCandidates.Rows.Add(candidate.Id, candidate.DisplayName, candidate.SelectionLabel);
                gridCandidates.Rows[index].Tag = candidate;
            }
            gridCandidates.ClearSelection();
            gridCandidates.CurrentCell = null;
            pageReady = true;
            lblPage.Text = $"{page}페이지 · 총 {total}명";
            lblStatus.Text = total == 0 ? "조회된 후보가 없습니다. 기존 선택은 아래에서 확인할 수 있습니다."
                : "후보를 선택한 뒤 ‘선택 반영’을 누르세요. 업무 관리자 선택은 페이지를 이동해도 유지됩니다.";
        }
        catch (OperationCanceledException) { if (Current(request, token)) CancelSession(); }
        catch (Exception error) { if (Current(request, token)) Fail(error); }
        finally { FinishRequest(request); }
    }

    private (long Request, CancellationToken Token) BeginRequest()
    {
        Selection = null;
        generation++;
        requestCancellation?.Cancel();
        requestCancellation?.Dispose();
        requestCancellation = new CancellationTokenSource();
        busy = true;
        pageReady = false;
        gridCandidates.Rows.Clear();
        lblPage.Text = "조회 중…";
        lblStatus.Text = "준비 정보를 불러오는 중입니다.";
        SetState();
        return (generation, requestCancellation.Token);
    }
    private bool Current(long request, CancellationToken token) => !stopped && !IsDisposed
        && request == generation && !token.IsCancellationRequested && service?.IsCurrent == true;
    private void FinishRequest(long request)
    {
        if (request != generation || stopped) return;
        if (service?.IsCurrent != true) { CancelSession(); return; }
        busy = false;
        SetState();
    }
    private void SetState()
    {
        bool current = !stopped && service?.IsCurrent == true;
        bool edit = current && !busy && manage && contextReady;
        cmbKind.Enabled = txtSearch.Enabled = btnSearch.Enabled = edit;
        btnRefreshContext.Enabled = current && !busy;
        gridCandidates.Enabled = edit && pageReady;
        btnPrevious.Enabled = edit && pageReady && page > 1;
        btnNext.Enabled = edit && pageReady && (long)page * 20 < total;
        btnChoose.Enabled = edit && pageReady && gridCandidates.SelectedRows.Count == 1;
        SelectionItem? selected = listSelected.SelectedItem as SelectionItem;
        btnRemove.Enabled = edit && selected != null && !IsRequiredCreator(selected);
        btnConfirm.Enabled = edit && pageReady && HasRequiredManagers();
    }
    private bool IsRequiredCreator(SelectionItem item)
    {
        if (taskId != null || service == null) return false;
        return item.Manager && item.Id == service.UserId;
    }
    private bool HasRequiredManagers()
    {
        // This policy applies to initial creation. Later manager changes need their own policy.
        if (taskId != null) return true;
        if (service == null || !service.CanSelectCandidates) return false;
        return managers.Count >= 1 && managers.ContainsKey(service.UserId);
    }
    private void ClearSelection()
    {
        context = null;
        assignee = null;
        managers.Clear();
        Selection = null;
        listSelected.Items.Clear();
    }
    private void CancelSession()
    {
        generation++;
        requestCancellation?.Cancel();
        busy = false;
        pageReady = contextReady = manage = false;
        gridCandidates.Rows.Clear();
        ClearSelection();
        lblContext.Text = "로그인 접속이 변경되어 이전 준비 정보를 지웠습니다.";
        lblPage.Text = "";
        lblStatus.Text = "창을 닫고 현재 로그인에서 업무를 다시 조회해 주세요.";
        SetState();
    }
    private void Fail(Exception error)
    {
        pageReady = false;
        lblPage.Text = "조회 실패";
        gridCandidates.Rows.Clear();
        if (error is TaskPreparationException { Failure: TaskPreparationFailure.AuthenticationExpired or TaskPreparationFailure.Forbidden or TaskPreparationFailure.NotFound })
        { ClearSelection(); contextReady = manage = false; lblContext.Text = "이전 준비 정보를 지웠습니다. 정보를 다시 조회해 주세요."; }
        lblStatus.Text = error is TaskPreparationException known ? known.Message
            : error is ArgumentException ? "검색어와 업무 식별자를 확인해 주세요." : "준비 정보를 불러오지 못했습니다. 다시 조회해 주세요.";
        SetState();
    }
    private void RenderSelection()
    {
        listSelected.Items.Clear();
        if (assignee is not null) listSelected.Items.Add(new SelectionItem(false, assignee.Id, "담당자: " + assignee.SelectionLabel));
        foreach (KeyValuePair<int, string> item in managers)
        {
            string label = "업무 관리자: " + item.Value;
            if (taskId == null && service != null && item.Key == service.UserId)
                label += " (등록자 · 필수)";
            listSelected.Items.Add(new SelectionItem(true, item.Key, label));
        }
        SetState();
    }
    private void ChooseClick(object? sender, EventArgs e)
    {
        if (!btnChoose.Enabled || gridCandidates.SelectedRows[0].Tag is not TaskUserCandidate selected) return;
        if (cmbKind.SelectedIndex == 1) managers[selected.Id] = selected.SelectionLabel;
        else assignee = selected;
        RenderSelection();
    }
    private void RemoveClick(object? sender, EventArgs e)
    {
        if (!btnRemove.Enabled || listSelected.SelectedItem is not SelectionItem selected) return;
        if (IsRequiredCreator(selected)) return;
        if (selected.Manager) managers.Remove(selected.Id); else assignee = null;
        RenderSelection();
    }
    private void ConfirmClick(object? sender, EventArgs e)
    {
        if (!btnConfirm.Enabled || service?.IsCurrent != true) return;
        if (!HasRequiredManagers())
        {
            lblStatus.Text = "등록자를 포함한 업무 관리자 1명 이상이 필요합니다. 정보를 다시 조회해 주세요.";
            SetState();
            return;
        }
        Selection = new(context, assignee, Array.AsReadOnly(managers.Keys.ToArray()));
        DialogResult = DialogResult.OK; // Local selection only; the calling form remains unsaved.
        Close();
    }
    private void CandidateChanged(object? sender, EventArgs e) => SetState();
    private async void SearchClick(object? sender, EventArgs e) => await LoadCandidatesAsync(1);
    private async void KindChanged(object? sender, EventArgs e) { if (initialized && contextReady && manage) await LoadCandidatesAsync(1); }
    private async void PreviousClick(object? sender, EventArgs e) => await LoadCandidatesAsync(page - 1);
    private async void NextClick(object? sender, EventArgs e) => await LoadCandidatesAsync(page + 1);
    private async void RefreshContextClick(object? sender, EventArgs e) => await RefreshContextAsync();
    private async void SearchKeyDown(object? sender, KeyEventArgs e)
    { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await LoadCandidatesAsync(1); } }
    private void StopRequests()
    {
        if (stopped) return;
        stopped = true;
        generation++;
        sessionRegistration.Dispose();
        requestCancellation?.Cancel();
        requestCancellation?.Dispose();
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    { StopRequests(); base.OnFormClosed(e); }
    private static string StatusText(Queries.TaskDisplayStatus status) => status switch
    {
        Queries.TaskDisplayStatus.NotStarted => "진행전", Queries.TaskDisplayStatus.InProgress => "진행중",
        Queries.TaskDisplayStatus.Submitted => "제출완료", Queries.TaskDisplayStatus.Approved => "승인완료",
        Queries.TaskDisplayStatus.Returned => "반려완료", _ => "확인 필요"
    };
    private sealed record SelectionItem(bool Manager, int Id, string Label)
    { public override string ToString() => Label; }
}
