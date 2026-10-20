using System.Globalization;
using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Creation;
using RemoteWork.WinForms.Features.Tasks.Starting;
using RemoteWork.WinForms.Features.Tasks.Management;
using RemoteWork.WinForms.Features.Results;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks
{
    public partial class TasksControl : UserControl
    {
        private readonly PreviewRole previewRole;
        private readonly ITaskQueryService queryService;
        private ITaskPreparationService? preparationService;
        private TaskCreationWorkflow? creationWorkflow;
        private readonly bool followQueryPreparation;
        private PreviewRole viewRole;
        private TaskQuerySession? viewSession;
        private FeatureUiScope? listUiScope;
        private FeatureUiScope? detailUiScope;
        private FeatureUiScope.Operation? listOperation;
        private FeatureUiScope.Operation? detailOperation;
        private CancellationTokenRegistration sessionRegistration;
        private bool viewSessionInvalidated;
        private bool creationFormOpen;
        private bool startFormOpen;
        private bool submissionFormOpen;
        private bool managementFormOpen;
        private bool lastListQuerySucceeded;
        private string? acknowledgedCreationId;
        private string? createdTaskIdForRefresh;
        private CancellationTokenSource? listCancellation;
        private CancellationTokenSource? detailCancellation;
        private long listGeneration;
        private long detailGeneration;
        private bool initializing = true;
        private bool updatingRows;
        private bool detailLoading;
        private bool queriesDisposed;
        private string? selectedTaskId;
        private string? preferredTaskId;
        private string? detailRetryTaskId;
        private TaskListQuery? currentListQuery;
        private TaskDetail? currentTaskDetail;

        public TasksControl() : this(PreviewRole.Employee) { }

        public TasksControl(PreviewRole role) : this(role, new UnconfiguredTaskQueryService()) { }

        public TasksControl(PreviewRole role, ITaskQueryService queryService)
            : this(role, queryService, (queryService as ITaskPreparationSource)?.CreatePreparationService(), true) { }

        public TasksControl(PreviewRole role, ITaskQueryService queryService, ITaskPreparationService? preparationService)
            : this(role, queryService, preparationService, false) { }

        private TasksControl(PreviewRole role, ITaskQueryService queryService,
            ITaskPreparationService? preparationService, bool followQueryPreparation)
        {
            ArgumentNullException.ThrowIfNull(queryService);
            previewRole = role;
            viewRole = role;
            this.followQueryPreparation = followQueryPreparation;
            this.queryService = queryService;
            this.preparationService = preparationService;
            creationWorkflow = (queryService as ITaskCreationSource)?.GetCreationWorkflow();
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            btnRetryQuery.AccessibleDescription = "상세 조회 실패 시 선택한 업무만 다시 조회합니다. 목록 정보 변경 안내 시 목록 전체를 다시 조회합니다. F5 키는 업무 목록을 다시 조회합니다.";
            new ToolTip(components!).SetToolTip(btnRetryQuery, "F5: 업무 목록 전체 다시 조회");
            lblTaskScope.Visible = role == PreviewRole.Administrator;
            cmbTaskScope.Visible = role == PreviewRole.Administrator;
            cmbTaskScope.SelectedIndex = 0;
            cmbTaskStatusFilter.SelectedIndex = 0;
            lblTaskQuerySource.Text = queryService.SourceDescription
                + (creationWorkflow is null
                    ? "\r\n등록·수정·수락·연장: 입력·대상 확인만 가능하며 저장·전송은 연결 전입니다."
                    : "\r\n신규 등록: 서버 요청 연결 · 수정·수락·연장: 입력 준비만 가능하며 저장은 연결 전입니다.");
            lblTaskQueryStatus.Text = "업무 조회를 기다리고 있습니다.";
            if (queryService is ITaskStartSource)
                lblTaskQuerySource.Text = queryService.SourceDescription
                    + "\r\n신규 등록·수락·재작업: 개발 서버 요청 연결 · 수정·재배정·관리자 변경: 개발 연결 · 취소·연장: 기준 확정 / 기술 연결 준비 중";
            if (queryService is HttpTaskQueryService)
                lblTaskQuerySource.Text += "\r\n결과 제출: 선택 업무의 결과물 폼 연결 · 성공 후 업무 재조회 · 결과 미확인은 결과물 메뉴에서 확인";
            ClearDetail("목록에서 업무를 선택하면 상세 내용이 표시됩니다.");
            UpdateViewScope();
            initializing = false;
        }

        private void UpdateViewScope()
        {
            // The role only arranges the UI. The server authorizes each request.
            bool managementView = viewRole == PreviewRole.Administrator && cmbTaskScope.SelectedIndex == 1;
            lblTaskScope.Visible = viewRole == PreviewRole.Administrator;
            cmbTaskScope.Visible = viewRole == PreviewRole.Administrator;
            btnNewTaskPreview.Visible = managementView;
            btnEditTaskPreview.Visible = managementView;
            btnReassignTask.Visible = managementView && queryService is ITaskManagementSource;
            btnChangeTaskManagers.Visible = managementView && queryService is ITaskManagementSource;
            btnManagementResult.Visible = PendingManagementWorkflow() is not null;
            btnCancelTask.Visible = managementView;
            btnAcceptTask.Visible = !managementView || PendingStartWorkflow() is not null;
            btnSubmitResult.Visible = !managementView && queryService is HttpTaskQueryService;
            btnReviewRequestPreview.Visible = !managementView;
            btnDeadlineExtensionPreview.Visible = !managementView;
            btnCollaborationRequest.Visible = true;
            btnTaskRequests.Visible = managementView;
            columnGridTasks3.Visible = managementView;
            lblTaskListTitle.Text = managementView ? "관리 업무" : "내 업무";
            UpdatePreparationActions();
        }

        private TaskDetail? PreparationDetail()
        {
            if (!HasCurrentQuerySession || detailLoading || currentTaskDetail is not { } detail
                || gridTasks.SelectedRows.Count != 1
                || gridTasks.SelectedRows[0].Tag is not TaskSummary selected
                || !string.Equals(detail.Summary.TaskId, selected.TaskId, StringComparison.Ordinal)
                || !string.Equals(detail.Summary.TaskId, selectedTaskId, StringComparison.Ordinal))
                return null;
            return detail;
        }

        private void UpdatePreparationActions()
        {
            TaskDetail? detail = PreparationDetail();
            bool hasDetail = detail is not null;
            bool managementConnected = queryService is ITaskManagementSource;
            bool managing = viewRole == PreviewRole.Administrator && cmbTaskScope.SelectedIndex == 1;
            bool pendingManagement = PendingManagementWorkflow() is not null;
            bool readyManagement = managing && hasDetail && !managementFormOpen && !pendingManagement;
            btnEditTaskPreview.Enabled = !managementConnected ? hasDetail : readyManagement && detail?.Summary.Status == TaskDisplayStatus.NotStarted;
            btnReassignTask.Enabled = readyManagement && detail?.Summary.Status == TaskDisplayStatus.NotStarted;
            btnChangeTaskManagers.Enabled = readyManagement && detail?.Summary.Status is TaskDisplayStatus.NotStarted or TaskDisplayStatus.InProgress or TaskDisplayStatus.Returned;
            btnManagementResult.Enabled = !managementFormOpen && PendingManagementWorkflow()?.IsCurrent == true;
            btnManagementResult.Visible = PendingManagementWorkflow() is not null;
            bool ownView = viewRole != PreviewRole.Administrator || cmbTaskScope.SelectedIndex == 0;
            btnSubmitResult.Enabled = !submissionFormOpen && ownView
                && viewSession?.Session.IsCurrent == true && currentListQuery?.Scope == TaskQueryScope.Own
                && detail?.Summary.Status == TaskDisplayStatus.InProgress;
            TaskStartWorkflow? pendingStart = PendingStartWorkflow();
            btnAcceptTask.Text = detail?.Summary.Status == TaskDisplayStatus.Returned ? "재작업 시작" : "업무 수락";
            btnAcceptTask.Enabled = ownView && detail?.Summary.Status is TaskDisplayStatus.NotStarted or TaskDisplayStatus.Returned;
            if (pendingStart is not null)
            {
                btnAcceptTask.Text = "수락·재작업 결과 확인";
                btnAcceptTask.Enabled = pendingStart.IsCurrent;
                btnAcceptTask.Visible = true;
            }
            btnDeadlineExtensionPreview.Enabled = hasDetail;
            btnPrepareWrite.Visible = preparationService is not null;
            btnPrepareWrite.Enabled = hasDetail && preparationService?.IsCurrent == true;
            btnNewTaskPreview.Enabled = HasCurrentQuerySession;
        }

        private bool HasCurrentQuerySession => queryService is not HttpTaskQueryService
            || viewSession?.Session.IsCurrent == true;

        // A new login is adopted only by an explicit list refresh. Detail selection
        // and preparation continue to use the session that produced the list.
        private void BindQuerySession()
        {
            if (queryService is not HttpTaskQueryService http) return;
            bool previousIsCurrent = viewSession?.Session.IsCurrent == true;
            sessionRegistration.Dispose();
            listUiScope?.Dispose();
            detailUiScope?.Dispose();
            listOperation = detailOperation = null;
            viewSession = null;
            viewSessionInvalidated = false;
            listUiScope = detailUiScope = null;
            if (!previousIsCurrent)
            {
                preferredTaskId = null;
                createdTaskIdForRefresh = acknowledgedCreationId = null;
            }
            if (followQueryPreparation) preparationService = null;
            creationWorkflow = null;
            TaskQuerySession? captured = http.CaptureViewSession();
            if (captured is null) throw new TaskQueryException(TaskQueryFailure.AuthenticationExpired);
            viewSession = captured;
            if (followQueryPreparation) preparationService = captured.PreparationService;
            creationWorkflow = captured.CreationWorkflow;
            viewRole = captured.Session.User.Role == "admin" ? PreviewRole.Administrator : PreviewRole.Employee;
            listUiScope = new FeatureUiScope(captured.Session, PostToView, () => !queriesDisposed && !IsDisposed);
            detailUiScope = new FeatureUiScope(captured.Session, PostToView, () => !queriesDisposed && !IsDisposed);
            sessionRegistration = captured.Session.Cancellation.Register(() =>
                PostToView(() => InvalidateQuerySession(captured)));
            captured.Session.ThrowIfChanged();
        }

        private void PostToView(Action update)
        {
            // Never synchronously invoke from an ApiClient cancellation callback.
            if (queriesDisposed || IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(update); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        private void InvalidateQuerySession(TaskQuerySession captured)
        {
            if (queriesDisposed || IsDisposed || viewSessionInvalidated || !ReferenceEquals(viewSession, captured)
                || captured.Session.IsCurrent) return;
            viewSessionInvalidated = true;
            ++listGeneration;
            listCancellation?.Cancel();
            listUiScope?.Dispose();
            detailUiScope?.Dispose();
            lastListQuerySucceeded = false;
            createdTaskIdForRefresh = acknowledgedCreationId = null;
            ShowCancelledQuery("접속 변경·인증 만료로 업무");
        }

        /// <summary>
        /// Refreshes the selected list, preserving its selection when the query is unchanged.
        /// Call on the WinForms UI thread.
        /// The injected boundary owns data access; this control does not authenticate or authorize.
        /// </summary>
        public async Task RefreshAsync()
        {
            if (initializing || queriesDisposed || PreviewActions.IsDesignMode(this)) return;
            lastListQuerySucceeded = false;
            // Cancel the previous UI operation before replacing its session binding.
            long generation = ++listGeneration;
            listCancellation?.Cancel();
            listCancellation?.Dispose();
            listCancellation = new CancellationTokenSource();
            CancellationToken token = listCancellation.Token;
            CancelDetailRequest();
            ClearRows();
            ClearDetail("업무 목록을 불러오는 중입니다.");
            lblTaskQueryStatus.Text = "목록 조회 중…";
            gridTasks.Enabled = false;
            btnRetryQuery.Enabled = false;
            bool listReceived = false;
            bool createdTaskVisible = false;
            string? createdTaskId = null;
            try
            {
                BindQuerySession();
                listOperation = listUiScope?.BeginOperation();
                if (listOperation is not null)
                {
                    listCancellation.Dispose();
                    listCancellation = CancellationTokenSource.CreateLinkedTokenSource(listOperation.Cancellation);
                    token = listCancellation.Token;
                }
                var query = new TaskListQuery(
                    viewRole == PreviewRole.Administrator && cmbTaskScope.SelectedIndex == 1
                        ? TaskQueryScope.Managed : TaskQueryScope.Own,
                    SelectedStatus());
                if (query != currentListQuery) preferredTaskId = null;
                currentListQuery = query;
                string? taskIdToRestore = preferredTaskId;
                createdTaskId = createdTaskIdForRefresh;
                createdTaskIdForRefresh = null;
                UpdateViewScope();
                ITaskQueryService queries = viewSession?.QueryService ?? queryService;
                IReadOnlyList<TaskSummary> summaries = await queries.GetTasksAsync(query, token);
                if (!IsCurrentList(generation, token)) return;
                ArgumentNullException.ThrowIfNull(summaries);
                listReceived = true;
                lastListQuerySucceeded = true;
                createdTaskVisible = createdTaskId is not null && summaries.Any(summary => summary.TaskId == createdTaskId);
                updatingRows = true;
                try
                {
                    foreach (TaskSummary summary in summaries)
                    {
                        int row = gridTasks.Rows.Add(summary.TaskId, summary.Title, summary.AssigneeName,
                            StatusText(summary.Status), FormatDate(summary.StartDate), FormatDate(summary.DueDate));
                        gridTasks.Rows[row].Tag = summary;
                    }
                    gridTasks.ClearSelection();
                    gridTasks.CurrentCell = null;
                    if (gridTasks.Rows.Count > 0)
                    {
                        DataGridViewRow selectedRow = gridTasks.Rows.Cast<DataGridViewRow>()
                            .FirstOrDefault(row => row.Tag is TaskSummary summary
                                && string.Equals(summary.TaskId, createdTaskId, StringComparison.Ordinal))
                            ?? gridTasks.Rows.Cast<DataGridViewRow>()
                            .FirstOrDefault(row => row.Tag is TaskSummary summary
                                && string.Equals(summary.TaskId, taskIdToRestore, StringComparison.Ordinal))
                            ?? gridTasks.Rows[0];
                        gridTasks.CurrentCell = selectedRow.Cells[0];
                        selectedRow.Selected = true;
                        preferredTaskId = ((TaskSummary)selectedRow.Tag!).TaskId;
                    }
                }
                finally
                {
                    updatingRows = false;
                }
                gridTasks.Enabled = true;
                lblTaskQueryStatus.Text = $"조회된 업무 {summaries.Count}개";
                if (summaries.Count == 0)
                {
                    preferredTaskId = null;
                    ClearDetail("조회된 업무가 없습니다.");
                }
                else
                    await LoadDetailAsync(preferredTaskId!, generation);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                if (viewSession is { } captured && !captured.Session.IsCurrent)
                    InvalidateQuerySession(captured);
            }
            catch (OperationCanceledException)
            {
                if (viewSession is { } captured && !captured.Session.IsCurrent)
                    InvalidateQuerySession(captured);
                if (IsCurrentList(generation, token)) ShowCancelledQuery("목록");
            }
            catch (Exception exception)
            {
                if (viewSession is { } captured && !captured.Session.IsCurrent)
                    InvalidateQuerySession(captured);
                if (!IsCurrentList(generation, token)) return;
                ClearRows();
                ClearDetail("업무 상세를 표시할 수 없습니다.");
                lblTaskQueryStatus.Text = "목록 조회 실패 · " + FailureText(exception);
            }
            finally
            {
                if (IsCurrentList(generation, token))
                {
                    gridTasks.Enabled = true;
                    btnRetryQuery.Enabled = !detailLoading;
                    if (createdTaskId is not null)
                    {
                        string prefix = "업무 #" + createdTaskId + " 등록 완료 · ";
                        if (listReceived && !createdTaskVisible) prefix += "현재 조회 범위/상태 필터에는 없음 · ";
                        lblTaskQueryStatus.Text = prefix + lblTaskQueryStatus.Text;
                    }
                }
            }
        }

        private async Task LoadDetailAsync(string taskId, long parentGeneration)
        {
            if (!HasCurrentQuerySession)
            {
                await InvalidateChangedSessionAfterSelectionAsync();
                return;
            }
            CancelDetailRequest();
            selectedTaskId = taskId;
            long generation = detailGeneration;
            detailCancellation = new CancellationTokenSource();
            CancellationToken token = detailCancellation.Token;
            detailLoading = true;
            ClearDetail("업무 상세를 불러오는 중입니다.");
            lblTaskQueryStatus.Text = "상세 조회 중…";
            btnRetryQuery.Enabled = false;
            try
            {
                detailOperation = detailUiScope?.BeginOperation();
                if (detailOperation is not null)
                {
                    detailCancellation.Dispose();
                    detailCancellation = CancellationTokenSource.CreateLinkedTokenSource(detailOperation.Cancellation);
                    token = detailCancellation.Token;
                }
                ITaskQueryService queries = viewSession?.QueryService ?? queryService;
                TaskDetail detail = await queries.GetTaskAsync(taskId, token);
                if (!IsCurrentDetail(parentGeneration, generation, taskId, token)) return;
                if (detail?.Summary is null || !string.Equals(detail.Summary.TaskId, taskId, StringComparison.Ordinal))
                    throw new TaskQueryException(TaskQueryFailure.Unavailable);
                TaskSummary summary = detail.Summary;
                currentTaskDetail = detail;
                txtContent.Text = $"{summary.TaskId}\r\n\r\n업무 제목   {summary.Title}\r\n담당자      {summary.AssigneeName}"
                    + $"\r\n업무 상태   {StatusText(summary.Status)}\r\n\r\n시작일      {FormatDate(summary.StartDate)}"
                    + $"\r\n현재 마감일 {FormatDate(summary.DueDate)}\r\n\r\n상세 내용\r\n{detail.Description}";
                bool listSummaryChanged = gridTasks.SelectedRows.Count == 1
                    && gridTasks.SelectedRows[0].Tag is TaskSummary listedSummary
                    && listedSummary != summary;
                lblTaskQueryStatus.Text = listSummaryChanged
                    ? "상세 조회 완료 · 목록 정보 변경 감지 · 목록을 다시 조회해 주세요."
                    : "상세 조회 완료";
                if (listSummaryChanged) btnRetryQuery.Text = "목록 새로고침";
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                await InvalidateChangedSessionAfterSelectionAsync();
            }
            catch (OperationCanceledException)
            {
                // A synchronously cancelled service can complete inside SelectionChanged.
                // Let DataGridView finish changing its current cell before clearing rows.
                await Task.Yield();
                if (viewSession is { } captured && !captured.Session.IsCurrent)
                    InvalidateQuerySession(captured);
                if (IsCurrentDetail(parentGeneration, generation, taskId, token))
                    ShowCancelledQuery("상세");
            }
            catch (Exception exception)
            {
                await InvalidateChangedSessionAfterSelectionAsync();
                if (!IsCurrentDetail(parentGeneration, generation, taskId, token)) return;
                ClearDetail(FailureText(exception));
                lblTaskQueryStatus.Text = "상세 조회 실패 · " + FailureText(exception);
                detailRetryTaskId = taskId;
                btnRetryQuery.Text = "상세 재시도";
            }
            finally
            {
                if (IsCurrentDetail(parentGeneration, generation, taskId, token))
                {
                    detailLoading = false;
                    btnRetryQuery.Enabled = true;
                    UpdatePreparationActions();
                }
            }
        }

        private async Task InvalidateChangedSessionAfterSelectionAsync()
        {
            if (viewSession is not { } captured || captured.Session.IsCurrent) return;
            // SelectionChanged can still be moving CurrentCell. Row removal must
            // run after that stack unwinds, even when cancellation is synchronous.
            await Task.Yield();
            InvalidateQuerySession(captured);
        }

        private TaskDisplayStatus? SelectedStatus() => cmbTaskStatusFilter.SelectedIndex switch
        {
            1 => TaskDisplayStatus.NotStarted,
            2 => TaskDisplayStatus.InProgress,
            3 => TaskDisplayStatus.Submitted,
            4 => TaskDisplayStatus.Approved,
            5 => TaskDisplayStatus.Returned,
            _ => null
        };

        private static string StatusText(TaskDisplayStatus status) => status switch
        {
            TaskDisplayStatus.NotStarted => "진행전",
            TaskDisplayStatus.InProgress => "진행중",
            TaskDisplayStatus.Submitted => "제출완료",
            TaskDisplayStatus.Approved => "승인완료",
            TaskDisplayStatus.Returned => "반려완료",
            _ => throw new TaskQueryException(TaskQueryFailure.Unavailable)
        };

        private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        private static string FailureText(Exception exception) =>
            (exception is TaskQueryException queryException ? queryException.Failure : TaskQueryFailure.Unavailable) switch
            {
                TaskQueryFailure.NotConfigured => "업무 조회 연결이 필요합니다. 공통 서버 조회 계약을 연결해 주세요.",
                TaskQueryFailure.AuthenticationExpired => "인증이 만료되었습니다. 다시 로그인한 뒤 재시도해 주세요.",
                TaskQueryFailure.Forbidden => "업무를 조회할 권한이 없습니다.",
                TaskQueryFailure.NotFound => "선택한 업무를 찾을 수 없습니다.",
                _ => "업무 조회 중 서버 오류가 발생했습니다. 잠시 후 다시 시도해 주세요."
            };

        private bool IsCurrentList(long generation, CancellationToken token) =>
            !queriesDisposed && !IsDisposed && generation == listGeneration && !token.IsCancellationRequested
            && (listOperation?.IsCurrent ?? true);

        private bool IsCurrentDetail(long parentGeneration, long generation, string taskId, CancellationToken token) =>
            IsCurrentList(parentGeneration, token) && generation == detailGeneration
            && (detailOperation?.IsCurrent ?? true)
            && string.Equals(selectedTaskId, taskId, StringComparison.Ordinal);

        private void ClearRows()
        {
            updatingRows = true;
            try { gridTasks.Rows.Clear(); }
            finally { updatingRows = false; }
        }

        private void ClearDetail(string message)
        {
            currentTaskDetail = null;
            UpdatePreparationActions();
            txtContent.Text = message;
            // File lookup belongs to member 3 and has no agreed query contract yet.
            gridTaskAttachments.Rows.Clear();
            lblText4.Text = "업무 첨부파일 · 조회 연결 필요";
        }

        private void CancelDetailRequest()
        {
            ++detailGeneration;
            detailLoading = false;
            selectedTaskId = null;
            currentTaskDetail = null;
            UpdatePreparationActions();
            detailRetryTaskId = null;
            btnRetryQuery.Text = "다시 조회";
            detailCancellation?.Cancel();
            detailCancellation?.Dispose();
            detailCancellation = null;
            detailOperation = null;
        }

        private void ShowCancelledQuery(string operation)
        {
            // The common client can cancel a superseded session without cancelling our local token.
            // Clear that session's visible data; cancellation does not mean a server failure.
            CancelDetailRequest();
            preferredTaskId = null;
            ClearRows();
            ClearDetail("업무 조회가 취소되었습니다. 다시 조회해 주세요.");
            lblTaskQueryStatus.Text = operation + " 조회 취소 · 다시 조회해 주세요.";
            gridTasks.Enabled = true;
            btnRetryQuery.Enabled = true;
        }

        private void CancelPendingQueries()
        {
            if (queriesDisposed) return;
            queriesDisposed = true;
            // The login session owns pending creation, including across menu navigation.
            currentListQuery = null;
            sessionRegistration.Dispose();
            listUiScope?.Dispose();
            detailUiScope?.Dispose();
            preferredTaskId = null;
            ++listGeneration;
            listCancellation?.Cancel();
            listCancellation?.Dispose();
            listCancellation = null;
            CancelDetailRequest();
        }

        private async void TasksControl_Load(object? sender, EventArgs e)
        {
            await RefreshAsync();
        }

        private async void cmbTaskScope_SelectedIndexChanged(object? sender, EventArgs e)
        {
            await RefreshAsync();
        }

        private async void cmbTaskStatusFilter_SelectedIndexChanged(object? sender, EventArgs e)
        {
            await RefreshAsync();
        }

        private async void btnRetryQuery_Click(object? sender, EventArgs e)
        {
            if (detailRetryTaskId is { } retryTaskId
                && string.Equals(selectedTaskId, retryTaskId, StringComparison.Ordinal)
                && gridTasks.SelectedRows.Count == 1
                && gridTasks.SelectedRows[0].Tag is TaskSummary summary
                && string.Equals(summary.TaskId, retryTaskId, StringComparison.Ordinal))
                await LoadDetailAsync(retryTaskId, listGeneration);
            else
                await RefreshAsync();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5 && !initializing && !queriesDisposed && !PreviewActions.IsDesignMode(this))
            {
                RefreshFromKeyboard();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private async void RefreshFromKeyboard()
        {
            await RefreshAsync();
        }

        private async void gridTasks_SelectionChanged(object? sender, EventArgs e)
        {
            if (initializing || updatingRows || queriesDisposed || PreviewActions.IsDesignMode(this)) return;
            if (gridTasks.SelectedRows.Count == 1 && gridTasks.SelectedRows[0].Tag is TaskSummary summary)
            {
                preferredTaskId = summary.TaskId;
                await LoadDetailAsync(summary.TaskId, listGeneration);
            }
            else
            {
                CancelDetailRequest();
                preferredTaskId = null;
                ClearDetail("목록에서 업무를 선택하면 상세 내용이 표시됩니다.");
                lblTaskQueryStatus.Text = gridTasks.Rows.Count == 0 ? "조회된 업무가 없습니다." : "업무를 선택해 주세요.";
                btnRetryQuery.Enabled = true;
            }
        }

        private async void btnNewTaskPreview_Click(object? sender, EventArgs e)
        {
            if (creationFormOpen || queriesDisposed || !HasCurrentQuerySession) return;
            TaskCreationWorkflow? workflow = creationWorkflow;
            if (workflow is null)
            {
                PreviewActions.Open(this, new TaskEditForm(false, preparationService));
                return;
            }
            if (workflow.State == TaskCreationState.Succeeded
                && workflow.CreatedResult?.TaskId == acknowledgedCreationId)
                workflow.ResetAfterSuccess();
            creationFormOpen = true;
            try
            {
                using var form = new TaskEditForm(preparationService, workflow);
                if (form.ShowDialog(FindForm()) == DialogResult.OK && form.CreatedResult is { } result
                    && !queriesDisposed && workflow.IsCurrent)
                    await RefreshAfterCreationAsync(result);
            }
            finally { creationFormOpen = false; }
        }

        private async Task RefreshAfterCreationAsync(TaskCreationResult result)
        {
            if (queriesDisposed || IsDisposed) return;
            // Keep the current scope/filter and old selection when the new row is outside it.
            createdTaskIdForRefresh = result.TaskId;
            await RefreshAsync();
            if (!queriesDisposed && !IsDisposed && creationWorkflow?.IsCurrent == true)
                acknowledgedCreationId = result.TaskId;
        }

        private async void btnEditTaskPreview_Click(object? sender, EventArgs e)
        {
            if (queryService is ITaskManagementSource) { await OpenManagementAsync(TaskManagementKind.Update); return; }
            if (PreparationDetail() is { } detail)
                PreviewActions.Open(this, new TaskEditForm(detail, preparationService));
        }

        private async void btnReassignTask_Click(object? sender, EventArgs e) => await OpenManagementAsync(TaskManagementKind.Reassign);
        private async void btnChangeTaskManagers_Click(object? sender, EventArgs e) => await OpenManagementAsync(TaskManagementKind.ChangeManagers);
        private async void btnManagementResult_Click(object? sender, EventArgs e)
        {
            if (PendingManagementWorkflow() is { } pending) await OpenManagementAsync(null, pending);
        }
        private async Task OpenManagementAsync(TaskManagementKind? kind, TaskManagementWorkflow? recovery = null)
        {
            if (managementFormOpen || queriesDisposed || IsDisposed || !HasCurrentQuerySession
                || viewSession is not { } captured || preparationService?.IsCurrent != true) return;
            TaskDetail? detail = PreparationDetail();
            if (recovery is null && (kind is null || detail is null || currentListQuery?.Scope != TaskQueryScope.Managed
                || PendingManagementWorkflow() is not null)) return;
            TaskManagementWorkflow? workflow;
            try { workflow = recovery ?? CurrentManagementSource()?.GetManagementWorkflow(kind!.Value, detail!.Summary.TaskId); }
            catch (OperationCanceledException) { return; }
            if (workflow is null) return;
            managementFormOpen = true; UpdatePreparationActions();
            try
            {
                using var form = recovery is not null
                    ? new TaskManagementForm(workflow, captured.QueryService, preparationService)
                    : new TaskManagementForm(kind!.Value, detail!, captured.QueryService, preparationService, workflow);
                form.ShowDialog(FindForm());
                if (form.ShouldRefreshList && !queriesDisposed && !IsDisposed && captured.Session.IsCurrent)
                {
                    await RefreshAsync();
                    if (!queriesDisposed && !IsDisposed && captured.Session.IsCurrent && form.ConfirmedResult is { } confirmed)
                        lblTaskQueryStatus.Text = $"업무 변경 성공 확인 · 버전 {confirmed.RowVersion} · " + lblTaskQueryStatus.Text;
                    // A forbidden/empty read after self removal is not rollback.
                    // Only the form's explicit success acknowledgement clears it.
                }
            }
            finally { managementFormOpen = false; if (!queriesDisposed && !IsDisposed) UpdatePreparationActions(); }
        }
        private ITaskManagementSource? CurrentManagementSource() => !HasCurrentQuerySession ? null
            : (viewSession?.QueryService ?? queryService) as ITaskManagementSource;
        private TaskManagementWorkflow? PendingManagementWorkflow()
        {
            if (queriesDisposed) return null;
            try { return CurrentManagementSource()?.GetPendingManagementWorkflow(); }
            catch (OperationCanceledException) { return null; }
        }

        private void PrepareWrite_Click(object? sender, EventArgs e)
        {
            if (PreparationDetail() is { } detail && preparationService?.IsCurrent == true)
                PreviewActions.Open(this, new TaskPreparationForm(preparationService, detail.Summary.TaskId));
        }

        private async void btnAcceptTask_Click(object? sender, EventArgs e)
        {
            if (!btnAcceptTask.Enabled || startFormOpen || queriesDisposed) return;
            TaskStartWorkflow? workflow = PendingStartWorkflow();
            TaskDetail? detail = PreparationDetail();
            if (workflow is null && detail is null) return;
            bool recovering = workflow is not null;
            TaskStartRequest? pending = workflow?.PendingRequest;
            if (recovering && pending is null) { UpdatePreparationActions(); return; }
            TaskStartKind kind = pending?.Kind
                ?? (detail!.Summary.Status == TaskDisplayStatus.Returned ? TaskStartKind.Rework : TaskStartKind.Accept);
            if (workflow is null)
            {
                try { workflow = CurrentStartSource()?.GetStartWorkflow(kind, detail!.Summary.TaskId); }
                catch (OperationCanceledException) { return; }
            }
            startFormOpen = true;
            try
            {
                using var form = recovering ? new TaskAcceptanceForm(workflow!, preparationService)
                    : preparationService is null ? new TaskAcceptanceForm(detail!)
                    : new TaskAcceptanceForm(detail!, kind, preparationService, workflow);
                form.ShowDialog(FindForm());
                if (form.ShouldRefreshList && !queriesDisposed && !IsDisposed && preparationService?.IsCurrent == true
                    && (workflow is null || workflow.IsCurrent))
                {
                    await RefreshAsync();
                    if (lastListQuerySucceeded && workflow?.IsCurrent == true
                        && workflow.State == TaskStartState.Succeeded)
                        workflow.AcknowledgeSuccess();
                }
            }
            finally
            {
                startFormOpen = false;
                if (!queriesDisposed && !IsDisposed) UpdatePreparationActions();
            }
        }

        private ITaskStartSource? CurrentStartSource() => !HasCurrentQuerySession ? null
            : (viewSession?.QueryService ?? queryService) as ITaskStartSource;

        private TaskStartWorkflow? PendingStartWorkflow()
        {
            if (queriesDisposed) return null;
            try { return CurrentStartSource()?.GetPendingStartWorkflow(); }
            catch (OperationCanceledException) { return null; }
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

        private void btnReviewRequestPreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new ReviewRequestForm(previewRole));
        }

        private async void btnSubmitResult_Click(object? sender, EventArgs e)
        {
            TaskDetail? detail = PreparationDetail();
            TaskQuerySession? captured = viewSession;
            if (submissionFormOpen || queriesDisposed || IsDisposed || !btnSubmitResult.Enabled
                || captured?.Session.IsCurrent != true || currentListQuery?.Scope != TaskQueryScope.Own
                || detail?.Summary.Status != TaskDisplayStatus.InProgress) return;
            submissionFormOpen = true;
            UpdatePreparationActions();
            try
            {
                // Member 3 owns the actual write, pending-result recovery and final
                // authorization. Pass the selected identity and this page's login.
                using var form = new SubmissionForm(viewRole, captured.Session, captured.QueryService)
                {
                    Tag = new Dictionary<string, string> { ["TaskId"] = detail.Summary.TaskId }
                };
                if (form.ShowDialog(FindForm()) == DialogResult.OK && !queriesDisposed && !IsDisposed
                    && captured.Session.IsCurrent)
                    await RefreshAsync();
            }
            finally
            {
                submissionFormOpen = false;
                if (!queriesDisposed && !IsDisposed) UpdatePreparationActions();
            }
        }

        private void btnDeadlineExtensionPreview_Click(object? sender, EventArgs e)
        {
            if (PreparationDetail() is { } detail)
                PreviewActions.Open(this, preparationService is null
                    ? new DeadlineExtensionForm(previewRole, detail)
                    : new DeadlineExtensionForm(previewRole, detail, preparationService));
        }

        private void btnCollaborationRequest_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new CollaborationRequestForm());
        }

        private void btnTaskRequests_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new TaskRequestsForm());
        }
    }
}
