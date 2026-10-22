using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Attachments;
using RemoteWork.WinForms.Features.Tasks.Management;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks;

public partial class TaskManagementForm : Form
{
    private readonly TaskManagementKind kind;
    private readonly ITaskQueryService? queries;
    private readonly ITaskPreparationService? preparation;
    private readonly TaskManagementWorkflow? workflow;
    private readonly string? taskId;
    private TaskDetail? source;
    private TaskWriteContext? context;
    private TaskManagementRequest? checkedRequest;
    private int selectedAssignee;
    private IReadOnlyList<int> selectedManagers = Array.Empty<int>();
    private CancellationTokenSource? cancellation;
    private CancellationTokenRegistration sessionRegistration;
    private long generation;
    private bool stopped, busy, filling;
    public bool ShouldRefreshList { get; private set; }
    public TaskManagementResult? ConfirmedResult { get; private set; }

    public TaskManagementForm() { InitializeComponent(); SetButtons(); }
    public TaskManagementForm(TaskManagementKind kind, TaskDetail detail, ITaskQueryService queries,
        ITaskPreparationService preparation, TaskManagementWorkflow workflow)
    {
        this.kind = kind; source = detail; taskId = detail.Summary.TaskId;
        this.queries = queries; this.preparation = preparation; this.workflow = workflow;
        InitializeComponent(); InitializeWorkflow();
    }
    public TaskManagementForm(TaskManagementWorkflow workflow, ITaskQueryService? queries = null,
        ITaskPreparationService? preparation = null)
    {
        this.workflow = workflow; this.queries = queries; this.preparation = preparation;
        taskId = workflow.PendingRequest?.TaskId;
        kind = workflow.PendingRequest?.Kind ?? TaskManagementKind.Update;
        InitializeComponent(); InitializeWorkflow();
    }
    private void InitializeWorkflow()
    {
        Text = kind switch { TaskManagementKind.Update => "업무 수정", TaskManagementKind.Reassign => "담당자 변경", _ => "관리자 변경" };
        lblTarget.Text = Text + " · 업무 " + (source?.Summary.TaskId ?? workflow?.PendingRequest?.TaskId ?? "확인 필요")
            + "\r\n입력·대상 확인과 실행은 별도이며 최종 현재 권한·상태는 서버가 판단합니다.";
        workflow!.Changed += WorkflowChanged;
        if (workflow.Attachments is { } attachments) attachments.Changed += AttachmentsChanged;
        sessionRegistration = workflow.SessionCancellation.Register(() => Post(ShowWorkflow));
        ShowWorkflow();
    }
    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (PreviewActions.IsDesignMode(this) || workflow is null) return;
        if (workflow.State == TaskManagementState.Editing && source is not null) await ReloadAsync();
        else ShowWorkflow();
    }
    public async Task ReloadAsync(bool resetAttachments = false)
    {
        if (stopped || workflow?.IsCurrent != true || queries is null || preparation is null
            || workflow.State != TaskManagementState.Editing) return;
        if (taskId is null) return;
        if (resetAttachments && workflow.Attachments is { HasChanges: true } selected)
        {
            if (!ConfirmAttachmentReset() || !selected.ResetWithoutDelete()) return;
        }
        long mine = BeginRead();
        try
        {
            // The public detail DTO has no version. Fence it between two current
            // contexts so an intervening edit cannot bind old text to a new version.
            TaskWriteContext before = await preparation.GetContextAsync(taskId, cancellation!.Token);
            TaskDetail detail = await queries.GetTaskAsync(taskId, cancellation.Token);
            TaskWriteContext current = await preparation.GetContextAsync(taskId, cancellation.Token);
            if (!Current(mine)) return;
            if (before.TaskId != taskId || current.TaskId != taskId || before.RowVersion != current.RowVersion
                || before.AssignmentRevision != current.AssignmentRevision || before.Status != current.Status
                || before.AssigneeId != current.AssigneeId
                || !before.ManagerUserIds.ToHashSet().SetEquals(current.ManagerUserIds))
                throw new ArgumentException("조회 중 업무가 변경됐습니다. 최신 업무를 다시 조회해 주세요.");
            if (!preparation.CanSelectCandidates || !current.ManagerUserIds.Contains(preparation.UserId)
                || detail.Summary.TaskId != current.TaskId || detail.Summary.Status != current.Status || !Allowed(current.Status))
                throw new ArgumentException("현재 지정 관리자와 변경 가능한 상태를 다시 확인해 주세요.");
            if (workflow.Attachments is { Loaded: true } attachments && !attachments.MatchesContext(current))
            {
                if (attachments.HasChanges && !ConfirmAttachmentReset())
                { lblStatus.Text = "기존 첨부 선택을 유지했습니다. 새 업무 기준으로 바꾸려면 최신 조회에서 선택 초기화를 확인해 주세요."; return; }
                if (!attachments.ResetWithoutDelete()) return;
            }
            source = detail; context = current; selectedAssignee = current.AssigneeId;
            selectedManagers = Array.AsReadOnly(current.ManagerUserIds.ToArray());
            filling = true;
            try
            {
                txtTitle.Text = detail.Summary.Title; txtDescription.Text = detail.Description;
                txtStartDate.Text = detail.Summary.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                txtDueDate.Text = detail.Summary.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            finally { filling = false; }
            RenderSelection(); lblStatus.Text = "현재 업무를 읽었습니다. 변경 입력을 확인한 뒤 별도로 실행해 주세요.";
            RenderAttachments();
        }
        catch (OperationCanceledException) { if (Current(mine)) lblStatus.Text = "업무 확인이 취소됐습니다. 다시 조회해 주세요."; }
        catch (Exception error) { BlockRevokedRead(error, mine); if (Current(mine)) { ForgetInput(); lblStatus.Text = ReadError(error); } }
        finally { if (mine == generation && !stopped) { busy = false; SetButtons(); } }
    }
    private bool Allowed(TaskDisplayStatus status) => kind == TaskManagementKind.ChangeManagers
        ? status is TaskDisplayStatus.NotStarted or TaskDisplayStatus.InProgress or TaskDisplayStatus.Returned
        : status == TaskDisplayStatus.NotStarted;
    private long BeginRead()
    {
        cancellation?.Cancel(); cancellation?.Dispose();
        cancellation = CancellationTokenSource.CreateLinkedTokenSource(workflow!.SessionCancellation);
        checkedRequest = null; busy = true; SetButtons(); return ++generation;
    }
    private bool Current(long mine) => !stopped && !IsDisposed && mine == generation && workflow?.IsCurrent == true;
    private async Task CheckInputAsync()
    {
        if (source is null || context is null || preparation is null || busy || workflow?.State != TaskManagementState.Editing) return;
        long mine = BeginRead();
        try
        {
            var latest = await preparation.GetContextAsync(source.Summary.TaskId, cancellation!.Token);
            if (!Current(mine)) return;
            if (latest.TaskId != context.TaskId || latest.RowVersion != context.RowVersion
                || latest.AssignmentRevision != context.AssignmentRevision || latest.Status != context.Status
                || !preparation.CanSelectCandidates) throw new ArgumentException("업무가 변경됐습니다. 최신 업무를 다시 조회해 주세요.");
            if (kind == TaskManagementKind.Update && workflow.Attachments is { } attachments
                && (!attachments.IsReady || (attachments.Loaded && !attachments.MatchesContext(latest))))
                throw new ArgumentException("첨부 전송·삭제 결과와 원래 업무 버전을 확인한 뒤 다시 진행해 주세요.");
            checkedRequest = kind switch
            {
                TaskManagementKind.Update => TaskManagementRequest.Update(source, latest, preparation.UserId,
                    txtTitle.Text, txtDescription.Text, Date(txtStartDate.Text), Date(txtDueDate.Text), workflow.Attachments?.CompletedIds),
                TaskManagementKind.Reassign => TaskManagementRequest.Reassign(source, latest, preparation.UserId, selectedAssignee),
                _ => TaskManagementRequest.ChangeManagers(source, latest, preparation.UserId, selectedManagers)
            };
            lblStatus.Text = "입력·대상을 확인했습니다. ‘변경 실행’을 눌러 서버에 요청하세요. 아직 저장하지 않았습니다.";
        }
        catch (Exception error) { BlockRevokedRead(error, mine); if (Current(mine)) { checkedRequest = null; lblStatus.Text = ReadError(error); } }
        finally { if (mine == generation && !stopped) { busy = false; SetButtons(); } }
    }
    private static DateOnly Date(string value) => DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var parsed) ? parsed : throw new ArgumentException("날짜를 YYYY-MM-DD 형식으로 입력해 주세요.");
    private async void Confirm_Click(object? sender, EventArgs e)
    {
        if (busy || stopped || workflow?.IsCurrent != true) return;
        if (workflow.CanRetry) await workflow.RetrySameRequestAsync();
        else if (checkedRequest is { } request && workflow.State == TaskManagementState.Editing)
            await workflow.SubmitAsync(request);
        else await CheckInputAsync();
        if (!stopped) ShowWorkflow();
    }
    private async void CheckResult_Click(object? sender, EventArgs e)
    { if (!busy && workflow?.CanCheckResult == true) { await workflow.CheckResultAsync(); if (!stopped) ShowWorkflow(); } }
    private async void Reload_Click(object? sender, EventArgs e) => await ReloadAsync(resetAttachments: true);
    protected virtual bool ConfirmAttachmentReset() => MessageBox.Show(this,
        "최신 업무를 다시 읽기 위해 현재 첨부 선택을 초기화할까요?\r\n새 파일 선택과 기존 파일 제외를 되돌립니다. 서버 임시 파일은 자동 삭제하지 않습니다.",
        "첨부 선택 초기화", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    protected virtual void ShowAttachmentPicker(TaskManagementAttachmentSelection attachments)
    { using var picker = new TaskManagementAttachmentsForm(attachments); picker.ShowDialog(this); }

    private async void Attachments_Click(object? sender, EventArgs e)
    {
        if (busy || stopped || kind != TaskManagementKind.Update || workflow?.IsCurrent != true
            || workflow.Attachments is not { } attachments || attachments.IsBusy) return;
        if (!attachments.Loaded)
        {
            if (workflow.State != TaskManagementState.Editing || context is null || preparation is null) return;
            long mine = BeginRead();
            try
            {
                TaskWriteContext before = await preparation.GetContextAsync(context.TaskId, cancellation!.Token);
                if (!Current(mine)) return;
                if (!SameContext(context, before)) throw new ArgumentException("업무가 변경됐습니다. 최신 업무를 다시 조회해 주세요.");
                var files = await attachments.ReadFilesAsync(cancellation.Token);
                TaskWriteContext after = await preparation.GetContextAsync(context.TaskId, cancellation.Token);
                if (!Current(mine)) return;
                if (!SameContext(before, after) || !attachments.BindSnapshot(after, files))
                    throw new ArgumentException("첨부 조회 중 업무가 변경됐습니다. 최신 업무를 다시 조회해 주세요.");
            }
            catch (Exception error)
            {
                BlockRevokedRead(error, mine);
                if (Current(mine)) lblStatus.Text = "첨부 목록을 확인하지 못했습니다. 기존 첨부를 유지한 본문 수정은 가능합니다.\r\n" + ReadError(error);
                return;
            }
            finally { if (mine == generation && !stopped) { busy = false; RenderAttachments(); SetButtons(); } }
        }
        if (!stopped && workflow.IsCurrent && attachments.Loaded)
        {
            ShowAttachmentPicker(attachments);
            if (!stopped) { RenderAttachments(); SetButtons(); }
        }
    }
    private static bool SameContext(TaskWriteContext first, TaskWriteContext second) => first.TaskId == second.TaskId
        && first.RowVersion == second.RowVersion && first.AssignmentRevision == second.AssignmentRevision
        && first.Status == second.Status && first.AssigneeId == second.AssigneeId
        && first.ManagerUserIds.ToHashSet().SetEquals(second.ManagerUserIds);

    private void AttachmentsChanged(object? sender, EventArgs e) => Post(() =>
    {
        if (workflow?.State == TaskManagementState.Editing) checkedRequest = null;
        RenderAttachments(); SetButtons();
    });
    private void RenderAttachments()
    {
        lblAttachments.Visible = kind == TaskManagementKind.Update;
        var attachments = workflow?.Attachments;
        lblAttachments.Text = attachments?.IsCurrent != true ? "첨부 선택 연결이 없습니다. 기존 업무 첨부는 유지합니다."
            : workflow?.State == TaskManagementState.Succeeded ? workflow.PendingRequest?.AttachmentIds is { } saved
                ? $"서버에서 최종 첨부 {saved.Count}개 저장을 확인했습니다."
                : "업무 변경 성공을 확인했습니다. 첨부 목록은 변경하지 않았습니다."
            : !attachments.Loaded ? "기존 첨부 유지 · ‘첨부 확인·변경’에서 목록을 확인하고 수정할 수 있습니다."
            : attachments.IsBusy ? "첨부 파일을 처리하고 있습니다. 업무 변경은 아직 저장하지 않았습니다."
            : !attachments.IsReady ? "첨부 전송·삭제 결과 확인이 필요합니다. 파일 화면에서 선택을 정리해 주세요."
            : attachments.CompletedIds is { } ids ? $"변경할 최종 첨부 {ids.Count}개 · ‘변경 실행’을 눌러야 업무에 반영됩니다."
            : $"현재 첨부 {attachments.Items.Count}개 유지 · 첨부 변경 없음";
    }
    private void ChooseCandidates_Click(object? sender, EventArgs e)
    {
        if (busy || stopped || source is null || context is null || preparation?.IsCurrent != true) return;
        using var picker = new TaskPreparationForm(preparation, source.Summary.TaskId);
        if (picker.ShowDialog(this) != DialogResult.OK || workflow?.IsCurrent != true || stopped
            || picker.Selection is not { } selection) return;
        checkedRequest = null;
        if (selection.Context?.RowVersion != context.RowVersion || selection.Context?.AssignmentRevision != context.AssignmentRevision
            || selection.Context?.TaskId != context.TaskId || selection.Context?.Status != context.Status)
        { context = null; lblStatus.Text = "선택 중 업무가 변경됐습니다. 최신 업무를 다시 조회해 주세요."; SetButtons(); return; }
        if (kind == TaskManagementKind.Reassign)
            selectedAssignee = selection.Assignee?.Id ?? context.AssigneeId;
        else selectedManagers = Array.AsReadOnly(selection.ManagerUserIds.ToArray());
        RenderSelection();
        lblStatus.Text = kind == TaskManagementKind.Reassign
            ? "이번 변경은 선택한 담당자만 반영합니다. 입력·대상을 확인해 주세요."
            : "이번 변경은 선택한 업무 관리자만 반영합니다. 입력·대상을 확인해 주세요.";
        SetButtons();
    }
    private void InputEdited(object? sender, EventArgs e)
    {
        if (filling || stopped) return; checkedRequest = null;
        if (workflow?.State == TaskManagementState.Editing) lblStatus.Text = "입력이 바뀌었습니다. 입력·대상을 다시 확인해 주세요.";
        SetButtons();
    }
    private void Acknowledge_Click(object? sender, EventArgs e)
    {
        if (workflow?.State != TaskManagementState.Succeeded || !workflow.IsCurrent) return;
        ConfirmedResult = workflow.Result; ShouldRefreshList = true;
        if (workflow.AcknowledgeSuccess()) { DialogResult = DialogResult.OK; Close(); }
    }
    private void Close_Click(object? sender, EventArgs e) => Close();
    private void WorkflowChanged(object? sender, EventArgs e) => Post(ShowWorkflow);
    private void Post(Action action)
    {
        if (stopped || IsDisposed || !IsHandleCreated) return;
        try { if (InvokeRequired) BeginInvoke((Action)(() => { if (!stopped && !IsDisposed) action(); })); else action(); }
        catch (InvalidOperationException) { }
    }
    private void ShowWorkflow()
    {
        if (stopped || IsDisposed || workflow is null) return;
        var snapshot = workflow.Snapshot;
        if (snapshot.State == TaskManagementState.Editing && txtRecovery.TextLength > 0)
        {
            ClearRecoveryDisplay();
            if (context is not null) RenderSelection();
            else lblSelection.Text = "최신 업무를 조회한 뒤 변경 대상을 확인해 주세요.";
        }
        if (!workflow.IsCurrent)
        {
            ForgetInput(); ConfirmedResult = null; ShouldRefreshList = false;
            lblStatus.Text = "접속이 변경되어 이전 입력을 지웠습니다. 현재 로그인에서 업무를 다시 조회해 주세요.";
            SetButtons(); return;
        }
        else if (snapshot.State != TaskManagementState.Editing || snapshot.Failure is not null)
        {
            checkedRequest = null; lblStatus.Text = snapshot.Message;
            if (snapshot.Failure == TaskManagementFailure.Forbidden) ForgetInput();
            else if (snapshot.State != TaskManagementState.Editing && snapshot.PendingRequest is { } pending)
            {
                if (pending.Kind == TaskManagementKind.Update)
                {
                    filling = true;
                    try
                    {
                        txtTitle.Text = pending.Title; txtDescription.Text = pending.Description;
                        txtStartDate.Text = pending.StartDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        txtDueDate.Text = pending.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    }
                    finally { filling = false; }
                }
                else RenderRecovery(pending);
            }
        }
        if (snapshot.State == TaskManagementState.Succeeded && snapshot.Result is { } result)
        {
            ConfirmedResult = result; ShouldRefreshList = true;
            lblStatus.Text = $"서버 변경 성공을 확인했습니다. 업무 {result.TaskId} · 버전 {result.RowVersion} · 배정 {result.AssignmentRevision}\r\n"
                + "닫으면 최신 업무를 다시 조회합니다. 관리자에서 자신을 제외했다면 이후 조회·결과 확인은 거절될 수 있으며 저장 취소가 아닙니다.";
        }
        SetButtons();
    }
    private void RenderRecovery(TaskManagementRequest pending)
    {
        string action = pending.Kind == TaskManagementKind.Reassign ? "담당자 변경" : "관리자 변경";
        string recipients = pending.Kind == TaskManagementKind.Reassign ? "사용자 #" + pending.AssigneeId
            : string.Join("\r\n", pending.ManagerUserIds.Select(id => "사용자 #" + id));
        txtRecovery.Text = $"원래 요청 · 업무 {pending.TaskId}\r\n동작: {action}\r\n"
            + "표시명은 원래 요청에 없어 확인할 수 없습니다.\r\n\r\n변경할 대상:\r\n" + recipients;
        fields.Visible = false; fieldsHost.AutoScrollPosition = Point.Empty;
        txtRecovery.Visible = true; txtRecovery.BringToFront();
        lblSelection.Text = "재전송할 원래 변경 대상을 읽기 전용으로 표시합니다.";
    }
    private void ClearRecoveryDisplay()
    {
        txtRecovery.Clear(); txtRecovery.Visible = false;
        fields.Visible = true; fields.BringToFront();
    }
    private void SetButtons()
    {
        RenderAttachments();
        bool current = !stopped && workflow?.IsCurrent == true;
        bool attachmentBusy = workflow?.AttachmentsBusy == true;
        bool editing = current && !busy && !attachmentBusy && workflow?.State == TaskManagementState.Editing && context is not null;
        bool editable = editing && kind == TaskManagementKind.Update;
        txtTitle.ReadOnly = txtDescription.ReadOnly = txtStartDate.ReadOnly = txtDueDate.ReadOnly = !editable;
        btnChooseCandidates.Visible = kind != TaskManagementKind.Update;
        btnChooseCandidates.Text = kind == TaskManagementKind.Reassign ? "담당자 선택" : "관리자 선택";
        btnChooseCandidates.Enabled = editing;
        btnAttachments.Visible = kind == TaskManagementKind.Update;
        btnAttachments.Enabled = current && !busy && !attachmentBusy && workflow?.IsBusy != true && workflow?.Attachments is { } attachments
            && (attachments.Loaded || editing);
        btnReload.Enabled = current && !busy && !attachmentBusy && workflow?.State == TaskManagementState.Editing && queries is not null && preparation is not null;
        btnConfirm.Text = workflow?.CanRetry == true ? "같은 요청 재전송" : checkedRequest is null ? "입력·대상 확인" : "변경 실행";
        btnConfirm.Enabled = current && !busy && !attachmentBusy && (editing || workflow?.CanRetry == true);
        btnCheckResult.Enabled = current && !busy && workflow?.CanCheckResult == true;
        btnAcknowledge.Enabled = current && workflow?.State == TaskManagementState.Succeeded;
        btnClose.Enabled = workflow?.IsBusy != true && !attachmentBusy;
    }
    private void RenderSelection() => lblSelection.Text = kind switch
    {
        TaskManagementKind.Reassign => $"변경할 담당자 #{selectedAssignee}\r\n이번 변경은 담당자만 반영합니다. 현재 관리자 목록은 그대로 유지합니다.",
        TaskManagementKind.ChangeManagers => $"변경할 업무 관리자 {string.Join(", ", selectedManagers.Select(id => "#" + id))}\r\n"
            + "이번 변경은 업무 관리자만 반영합니다. 활성 관리자 1명 이상이며 다른 관리자가 남으면 최초 등록자·본인도 제외할 수 있습니다.",
        _ => "업무 제목·내용·시작일·마감일과 선택한 첨부 변경을 반영합니다. 입력·대상을 확인한 뒤 별도로 실행해 주세요."
    };
    private void ForgetInput()
    {
        ClearRecoveryDisplay();
        source = null; context = null; checkedRequest = null; selectedAssignee = 0; selectedManagers = Array.Empty<int>();
        filling = true;
        try { txtTitle.Clear(); txtDescription.Clear(); txtStartDate.Clear(); txtDueDate.Clear(); lblSelection.Text = "이전 선택을 지웠습니다."; }
        finally { filling = false; }
    }
    private static string ReadError(Exception error) => error switch
    {
        ArgumentException => error.Message, TaskPreparationException => error.Message,
        ApiException { StatusCode: 401 } => "로그인이 만료되었습니다. 다시 로그인해 주세요.",
        ApiException { StatusCode: 403 } => "현재 업무 첨부를 사용할 권한이 없습니다.",
        ApiException { StatusCode: 503 } => "첨부 기능 또는 저장소 연결을 확인해 주세요.",
        ApiException => "첨부 요청을 확인하지 못했습니다. " + error.Message,
        TaskQueryException => "업무 조회 권한·접속·서버 상태를 확인하고 다시 조회해 주세요.",
        OperationCanceledException => "현재 업무 확인을 취소했습니다. 다시 조회해 주세요.",
        _ => "업무 확인 중 오류가 발생했습니다. 입력·대상을 다시 확인해 주세요."
    };
    private void BlockRevokedRead(Exception error, long mine)
    {
        if (!Current(mine)) return;
        bool revoked = error is ApiException { StatusCode: 401 or 403 }
            or TaskPreparationException { Failure: TaskPreparationFailure.AuthenticationExpired or TaskPreparationFailure.Forbidden }
            or TaskQueryException { Failure: TaskQueryFailure.AuthenticationExpired or TaskQueryFailure.Forbidden };
        if (revoked && workflow?.State == TaskManagementState.Editing) workflow.BlockAttachmentSession();
    }
    private void StopOwnedQueries()
    {
        if (stopped) return; stopped = true; ++generation; checkedRequest = null;
        cancellation?.Cancel(); cancellation?.Dispose(); sessionRegistration.Unregister();
        if (workflow is not null) workflow.Changed -= WorkflowChanged;
        if (workflow?.Attachments is { } attachments) attachments.Changed -= AttachmentsChanged;
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if ((workflow?.IsBusy == true || workflow?.AttachmentsBusy == true) && workflow.IsCurrent && e.CloseReason == CloseReason.UserClosing)
        { e.Cancel = true; lblStatus.Text = "요청 처리 중입니다. 응답·처리 결과 확인 안내를 기다려 주세요."; }
        base.OnFormClosing(e);
    }
}
