using System.Globalization;
using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Features.Tasks.Queries;

namespace RemoteWork.WinForms.Features.Tasks
{
    public partial class TasksControl : UserControl
    {
        private readonly PreviewRole previewRole;
        private readonly ITaskQueryService queryService;
        private CancellationTokenSource? listCancellation;
        private CancellationTokenSource? detailCancellation;
        private long listGeneration;
        private long detailGeneration;
        private bool initializing = true;
        private bool updatingRows;
        private bool detailLoading;
        private bool queriesDisposed;
        private string? selectedTaskId;

        public TasksControl() : this(PreviewRole.Employee) { }

        public TasksControl(PreviewRole role) : this(role, new UnconfiguredTaskQueryService()) { }

        public TasksControl(PreviewRole role, ITaskQueryService queryService)
        {
            ArgumentNullException.ThrowIfNull(queryService);
            previewRole = role;
            this.queryService = queryService;
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            lblTaskScope.Visible = role == PreviewRole.Administrator;
            cmbTaskScope.Visible = role == PreviewRole.Administrator;
            cmbTaskScope.SelectedIndex = 0;
            cmbTaskStatusFilter.SelectedIndex = 0;
            lblTaskQuerySource.Text = queryService.SourceDescription
                + "\r\n업무 처리 창은 조회한 업무와 연결되지 않았습니다.";
            lblTaskQueryStatus.Text = "업무 조회를 기다리고 있습니다.";
            ClearDetail("목록에서 업무를 선택하면 상세 내용이 표시됩니다.");
            UpdateViewScope();
            initializing = false;
        }

        private void UpdateViewScope()
        {
            // PreviewRole only arranges the UI. A future server must authorize each request.
            bool managementView = previewRole == PreviewRole.Administrator && cmbTaskScope.SelectedIndex == 1;
            btnNewTaskPreview.Visible = managementView;
            btnEditTaskPreview.Visible = managementView;
            btnCancelTask.Visible = managementView;
            btnAcceptTask.Visible = !managementView;
            btnReviewRequestPreview.Visible = !managementView;
            btnDeadlineExtensionPreview.Visible = !managementView;
            btnCollaborationRequest.Visible = true;
            btnTaskRequests.Visible = managementView;
            columnGridTasks3.Visible = managementView;
            lblTaskListTitle.Text = managementView ? "관리 업무" : "내 업무";
        }

        /// <summary>
        /// Refreshes the selected list and its first detail. Call on the WinForms UI thread.
        /// The injected boundary owns data access; this control does not authenticate or authorize.
        /// </summary>
        public async Task RefreshAsync()
        {
            if (initializing || queriesDisposed || PreviewActions.IsDesignMode(this)) return;
            long generation = ++listGeneration;
            listCancellation?.Cancel();
            listCancellation?.Dispose();
            listCancellation = new CancellationTokenSource();
            CancellationToken token = listCancellation.Token;
            CancelDetailRequest();
            UpdateViewScope();
            ClearRows();
            ClearDetail("업무 목록을 불러오는 중입니다.");
            lblTaskQueryStatus.Text = "목록 조회 중…";
            gridTasks.Enabled = false;
            btnRetryQuery.Enabled = false;
            var query = new TaskListQuery(
                previewRole == PreviewRole.Administrator && cmbTaskScope.SelectedIndex == 1
                    ? TaskQueryScope.Managed : TaskQueryScope.Own,
                SelectedStatus());

            try
            {
                IReadOnlyList<TaskSummary> summaries = await queryService.GetTasksAsync(query, token);
                if (!IsCurrentList(generation, token)) return;
                ArgumentNullException.ThrowIfNull(summaries);
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
                        gridTasks.CurrentCell = gridTasks.Rows[0].Cells[0];
                        gridTasks.Rows[0].Selected = true;
                    }
                }
                finally
                {
                    updatingRows = false;
                }
                gridTasks.Enabled = true;
                lblTaskQueryStatus.Text = $"조회된 업무 {summaries.Count}개";
                if (summaries.Count == 0)
                    ClearDetail("조회된 업무가 없습니다.");
                else
                    await LoadDetailAsync(summaries[0].TaskId, generation);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
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
                }
            }
        }

        private async Task LoadDetailAsync(string taskId, long parentGeneration)
        {
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
                TaskDetail detail = await queryService.GetTaskAsync(taskId, token);
                if (!IsCurrentDetail(parentGeneration, generation, taskId, token)) return;
                if (detail?.Summary is null || !string.Equals(detail.Summary.TaskId, taskId, StringComparison.Ordinal))
                    throw new TaskQueryException(TaskQueryFailure.Unavailable);
                TaskSummary summary = detail.Summary;
                txtContent.Text = $"{summary.TaskId}\r\n\r\n업무 제목   {summary.Title}\r\n담당자      {summary.AssigneeName}"
                    + $"\r\n업무 상태   {StatusText(summary.Status)}\r\n\r\n시작일      {FormatDate(summary.StartDate)}"
                    + $"\r\n현재 마감일 {FormatDate(summary.DueDate)}\r\n\r\n상세 내용\r\n{detail.Description}";
                lblTaskQueryStatus.Text = "상세 조회 완료";
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception)
            {
                if (!IsCurrentDetail(parentGeneration, generation, taskId, token)) return;
                ClearDetail(FailureText(exception));
                lblTaskQueryStatus.Text = "상세 조회 실패 · " + FailureText(exception);
            }
            finally
            {
                if (IsCurrentDetail(parentGeneration, generation, taskId, token))
                {
                    detailLoading = false;
                    btnRetryQuery.Enabled = true;
                }
            }
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
            !queriesDisposed && !IsDisposed && generation == listGeneration && !token.IsCancellationRequested;

        private bool IsCurrentDetail(long parentGeneration, long generation, string taskId, CancellationToken token) =>
            IsCurrentList(parentGeneration, token) && generation == detailGeneration
            && string.Equals(selectedTaskId, taskId, StringComparison.Ordinal);

        private void ClearRows()
        {
            updatingRows = true;
            try { gridTasks.Rows.Clear(); }
            finally { updatingRows = false; }
        }

        private void ClearDetail(string message)
        {
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
            detailCancellation?.Cancel();
            detailCancellation?.Dispose();
            detailCancellation = null;
        }

        private void CancelPendingQueries()
        {
            if (queriesDisposed) return;
            queriesDisposed = true;
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
            await RefreshAsync();
        }

        private async void gridTasks_SelectionChanged(object? sender, EventArgs e)
        {
            if (initializing || updatingRows || queriesDisposed || PreviewActions.IsDesignMode(this)) return;
            if (gridTasks.SelectedRows.Count == 1 && gridTasks.SelectedRows[0].Tag is TaskSummary summary)
                await LoadDetailAsync(summary.TaskId, listGeneration);
            else
            {
                CancelDetailRequest();
                ClearDetail("목록에서 업무를 선택하면 상세 내용이 표시됩니다.");
                lblTaskQueryStatus.Text = gridTasks.Rows.Count == 0 ? "조회된 업무가 없습니다." : "업무를 선택해 주세요.";
                btnRetryQuery.Enabled = true;
            }
        }

        private void btnNewTaskPreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new TaskEditForm());
        }

        private void btnEditTaskPreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new TaskEditForm(true));
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

        private void btnReviewRequestPreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new ReviewRequestForm(previewRole));
        }

        private void btnDeadlineExtensionPreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new DeadlineExtensionForm(previewRole));
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
