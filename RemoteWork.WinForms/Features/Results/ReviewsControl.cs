using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using System.Numerics;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Results
{
    public partial class ReviewsControl : UserControl
    {
        private readonly PreviewRole previewRole;
        private bool previewReady;
        private bool updating;

        // 2번의 조회 서비스와 1번의 공통 접속을 그대로 사용합니다.
        private readonly ITaskQueryService? taskQueries;
        private readonly ApiClient.Session? loginSession;
        private CancellationTokenSource? listCancellation;
        private CancellationTokenSource? detailCancellation;
        private CancellationTokenRegistration sessionRegistration;
        private int listRequest;
        private int detailRequest;
        private bool loading;
        private TaskDetail? selectedTask;
        private ResultsApi? results;
        private List<ResultSubmission> submissions = new();
        private bool downloading;
        private readonly CancellationTokenSource viewClosed = new();
        private ResultSubmission? SelectedSubmission => gridVersions.CurrentRow?.Tag as ResultSubmission;
        public event EventHandler? ResultsSaved;

        private bool HasCurrentTask =>
            !loading &&
            loginSession?.IsCurrent == true &&
            selectedTask != null &&
            gridReviews.CurrentRow?.Tag is TaskSummary summary &&
            summary == selectedTask.Summary;

        private bool IsReviewing =>
            previewRole == PreviewRole.Administrator &&
            cmbResultView.SelectedIndex == 1;

        public ReviewsControl() : this(PreviewRole.Employee) { }

        public ReviewsControl(PreviewRole role)
        {
            previewRole = role;

            InitializeComponent();

            if (PreviewActions.IsDesignMode(this)) return;

            if (AppServices.Api.CurrentUser != null)
            {
                try
                {
                    loginSession = AppServices.Api.CaptureSession();
                    taskQueries = new HttpTaskQueryService(AppServices.Api);
                    results = new ResultsApi(loginSession, ResultsConnection.Files);
                }
                catch (InvalidOperationException)
                {
                    // 생성 도중 로그아웃되면 조회 연결을 만들지 않습니다.
                }
            }

            InitializeInteractions();

            Load += (_, _) =>
            {
                if (loginSession != null)
                {
                    sessionRegistration = loginSession.Cancellation.Register(() =>
                    {
                        if (IsDisposed || Disposing || !IsHandleCreated) return;

                        try
                        {
                            BeginInvoke(new Action(() =>
                            {
                                if (IsDisposed || Disposing) return;
                                CancelQueries();
                                ClearQueryView("접속이 변경되었습니다. 다시 로그인해 주세요.");
                            }));
                        }
                        catch (InvalidOperationException) { }
                    });
                }

                RefreshAfterSave();
            };

            Disposed += (_, _) =>
            {
                viewClosed.Cancel();
                CancelQueries();
                sessionRegistration.Dispose();
                listCancellation?.Dispose();
                detailCancellation?.Dispose();
                listCancellation = null;
                detailCancellation = null;
            };

            cmbResultView.Visible =
                role == PreviewRole.Administrator;

            previewReady = true;
            ShowScopePreview();
        }

        private void ShowScopePreview()
        {
            bool administrator =
                previewRole == PreviewRole.Administrator;

            bool reviewing =
                administrator &&
                cmbResultView.SelectedIndex == 1;

            updating = true;

            cmbOptions.SelectedIndex = 0;

            gridReviews.Rows.Clear();
            gridAttachments.Rows.Clear();
            gridVersions.Rows.Clear();

            btnSearch.Visible = reviewing;
            btnApprovePreview.Visible = reviewing;
            btnRejectPreview.Visible = reviewing;
            txtContent.Visible = true;

            btnQuery.Visible = !reviewing;
            btnSubmissionPreview.Visible = !reviewing;

            lblPageTitle.Text =
                reviewing ? "결과물 검토" : "내 결과물";

            lblReviewListTitle.Text =
                reviewing ? "검토 대상 목록" : "내 결과물 목록";

            lblResultDetailTitle.Text =
                reviewing ? "제출 결과 확인" : "내 결과와 반려 사유";

            txtContent2.Clear();

            updating = false;

            if (IsHandleCreated)
                RefreshAfterSave();
        }

        private void InitializeInteractions()
        {
            lblText4.Text = "첨부 자료와 이력";
            columnGridReviews4.HeaderText = "제출 회차";
            columnGridAttachments2.HeaderText = "제출 회차";
            tabPage2.Text = "제출 이력";
            columnGridVersions1.HeaderText = "이력";

            gridAttachments.CurrentCellChanged += (_, _) => UpdateActionState();
            gridReviews.CellFormatting += FormatRound;
            gridVersions.CellFormatting += FormatRound;
            gridAttachments.CellFormatting += FormatRound;

            txtContent.Clear();
            txtContent.PlaceholderText = "업무명 또는 담당자 검색";

            gridReviews.CurrentCellChanged += (_, _) =>
            {
                if (!updating) ShowSelectedReview();
            };

            gridVersions.CurrentCellChanged += (_, _) =>
            {
                if (!updating) ShowSelectedVersion();
            };

            cmbOptions.SelectedIndexChanged += (_, _) =>
            {
                if (!updating) FilterReviews();
            };

            txtContent.KeyDown += (_, e) =>
            {
                if (e.KeyCode != Keys.Enter) return;

                e.SuppressKeyPress = true;
                RefreshAfterSave();
            };
        }

        private void FilterReviews()
        {
            string keyword =
                txtContent.Visible
                    ? txtContent.Text.Trim()
                    : "";

            string status =
                cmbOptions.SelectedIndex > 0
                    ? cmbOptions.Text
                    : "";

            string selectedId =
                CellText(gridReviews.CurrentRow, 0);

            updating = true;

            try
            {
                gridReviews.CurrentCell = null;

                foreach (DataGridViewRow row in gridReviews.Rows)
                {
                    row.Visible =
                        (keyword.Length == 0 ||
                         (CellText(row, 1) + " " + CellText(row, 2))
                            .Contains(
                                keyword,
                                StringComparison.OrdinalIgnoreCase)) &&
                        (status.Length == 0 ||
                         CellText(row, 5) == status);
                }

                var rows = gridReviews.Rows
                    .Cast<DataGridViewRow>()
                    .Where(row => row.Visible)
                    .ToList();

                var target =
                    rows.FirstOrDefault(row =>
                        CellText(row, 0) == selectedId)
                    ?? rows.FirstOrDefault();

                if (target != null)
                    gridReviews.CurrentCell = target.Cells[0];
            }
            finally
            {
                updating = false;
            }

            ShowSelectedReview();
        }

        private async void ShowSelectedReview()
        {
            int request = ++detailRequest;
            detailCancellation?.Cancel();
            detailCancellation?.Dispose();
            detailCancellation = null;
            selectedTask = null;
            submissions.Clear();
            updating = true;
            gridVersions.Rows.Clear();
            gridAttachments.Rows.Clear();
            updating = false;
            UpdateActionState();
            var row = gridReviews.CurrentRow;
            if (row == null) { txtContent2.Text = "조회된 업무가 없습니다."; return; }
            if (loading || taskQueries == null || loginSession?.IsCurrent != true)
            { txtContent2.Text = "로그인과 업무 조회 연결을 확인해 주세요."; return; }

            string id = CellText(row, 0);
            bool reviewing = IsReviewing;
            detailCancellation = CancellationTokenSource.CreateLinkedTokenSource(loginSession.Cancellation);
            var token = detailCancellation.Token;
            txtContent2.Text = "업무 상세를 불러오는 중입니다.";
            try
            {
                TaskDetail detail = reviewing
                    ? await results!.ReviewTaskAsync(id, token)
                    : await taskQueries.GetTaskAsync(id, token);
                if (detail.Summary.TaskId != id)
                    throw new System.IO.InvalidDataException("선택한 업무와 조회 결과가 다릅니다.");
                var history = await results!.SubmissionsAsync(id, token);
                await ApplyUiAsync(() =>
                {
                    if (!CanDisplay(token) || request != detailRequest ||
                        CellText(gridReviews.CurrentRow, 0) != id) return;
                    selectedTask = detail;
                    submissions = history;
                    updating = true;
                    try
                    {
                        foreach (var item in history)
                        {
                            int index = gridVersions.Rows.Add(item.RoundNo, Kst(item.SubmittedAt),
                                ResultStatus(item.Status), item.Description);
                            gridVersions.Rows[index].Tag = item;
                        }
                        if (history.Count > 0)
                        {
                            row.Cells[3].Value = history[0].RoundNo;
                            row.Cells[4].Value = Kst(history[0].SubmittedAt);
                            gridVersions.CurrentCell = gridVersions.Rows[0].Cells[0];
                        }
                    }
                    finally { updating = false; }
                    ShowSelectedVersion();
                }, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) when (error is TaskQueryException or ApiException || IsDataError(error))
            {
                await ApplyUiAsync(() =>
                {
                    if (request == detailRequest) ClearSelectedView(QueryError(error));
                }, token);
            }
        }

        private void ShowSelectedVersion()
        {
            UpdateActionState();
            gridAttachments.Rows.Clear();

            if (gridReviews.CurrentRow == null)
            {
                txtContent2.Text = "조회된 업무가 없습니다.";
                return;
            }

            if (selectedTask == null) return;

            TaskSummary task = selectedTask.Summary;

            txtContent2.Text =
                $"{task.TaskId} · {task.Title}\r\n" +
                $"담당자: {task.AssigneeName}\r\n" +
                $"업무 상태: {DisplayStatus(task.Status)}\r\n" +
                $"시작일: {task.StartDate:yyyy-MM-dd}   |   " +
                $"마감일: {task.DueDate:yyyy-MM-dd}\r\n\r\n" +
                $"업무 설명\r\n{selectedTask.Description}\r\n\r\n" +
                (submissions.Count == 0 ? "제출 이력이 없습니다." : "제출 이력에서 회차를 선택해 주세요.");


            if (SelectedSubmission is ResultSubmission selected)
            {
                txtContent2.AppendText($"\r\n\r\n{selected.RoundNo}차 · {ResultStatus(selected.Status)}" +
                    $"\r\n제출 시각: {Kst(selected.SubmittedAt)}\r\n결과 설명\r\n{selected.Description}" +
                    $"\r\n\r\n검토 의견 / 반려 사유\r\n{selected.Reason}");
                foreach (var file in selected.Attachments)
                {
                    int index = gridAttachments.Rows.Add(file.Name, selected.RoundNo,
                        $"{file.Size / 1024.0:N1} KB");
                    gridAttachments.Rows[index].Tag = file;
                }
                if (selected.Attachments.Count > 0 && results?.CanTransferFiles != true)
                    txtContent2.AppendText("\r\n\r\n첨부 전송은 1번 공통 Session 파일 전송 연결이 필요합니다.");
            }
            UpdateActionState();

            if (!HasCurrentTask)
            {
                txtContent2.AppendText(
                    "\r\n\r\n목록 조회 후 업무 정보가 변경되었습니다. 조회 버튼으로 목록을 다시 불러와 주세요.");
            }

            if (IsReviewing)
            {
                txtContent2.AppendText("\r\n\r\n현재 검토자로 연결된 업무입니다. 저장 시 서버가 권한을 다시 확인합니다.");
            }
            else if (task.Status == TaskDisplayStatus.Returned)
            {
                txtContent2.AppendText(
                    "\r\n\r\n업무 화면에서 재작업 시작을 먼저 실행한 뒤 재제출해 주세요.");
            }
        }

        private bool CanReviewSelected(string? action = null) =>
            HasCurrentTask && IsReviewing && SelectedSubmission is ResultSubmission selected &&
            submissions.FirstOrDefault()?.SubmissionId == selected.SubmissionId &&
            selected.SubmittedBy != loginSession!.User.Id &&
            ((action == null && (results?.Pending("submission.approved", selected.SubmissionId) != null ||
                                results?.Pending("submission.returned", selected.SubmissionId) != null)) ||
             (action != null && results?.Pending(action, selected.SubmissionId) != null) ||
             (selected.Status == "submitted" &&
              selectedTask!.Summary.Status == TaskDisplayStatus.Submitted &&
              results?.Pending("submission.approved", selected.SubmissionId) == null &&
              results?.Pending("submission.returned", selected.SubmissionId) == null));

        private void UpdateActionState()
        {
            btnSubmissionPreview.Enabled = CanSubmitSelectedTask();
            btnSubmissionPreview.Text = selectedTask != null &&
                results?.Pending("submission.submitted", selectedTask.Summary.TaskId) != null
                    ? "제출 결과 확인" : "결과 제출";
            btnApprovePreview.Enabled = CanReviewSelected("submission.approved");
            btnRejectPreview.Enabled = CanReviewSelected("submission.returned");
            btnDownloadAttachment.Enabled = !downloading && results?.CanTransferFiles == true &&
                HasCurrentTask && gridAttachments.CurrentRow?.Tag is ResultAttachment;
        }

        private bool CanSubmitSelectedTask() =>
            HasCurrentTask && !IsReviewing &&
            (results?.Pending("submission.submitted", selectedTask!.Summary.TaskId) != null ||
             (selectedTask!.Summary.Status == TaskDisplayStatus.InProgress &&
              DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(
                  TimeSpan.FromHours(9)).DateTime) <= selectedTask.Summary.DueDate));

        private async Task DownloadAttachmentAsync()
        {
            if (results == null || gridAttachments.CurrentRow?.Tag is not ResultAttachment file) return;
            if (file.Name != System.IO.Path.GetFileName(file.Name))
                throw new System.IO.InvalidDataException("첨부 이름을 확인해 주세요.");
            using var dialog = new SaveFileDialog
            {
                FileName = file.Name, Filter = "모든 파일 (*.*)|*.*", OverwritePrompt = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            downloading = true;
            UpdateActionState();
            try { await results.DownloadAsync(file, dialog.FileName, loginSession!.Cancellation); }
            finally
            {
                downloading = false;
                if (!IsDisposed && !Disposing) UpdateActionState();
            }
        }

        private static string Kst(DateTimeOffset time) =>
            time.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd HH:mm");
        private static string ResultStatus(string status) => status switch
        {
            "submitted" => "제출완료", "approved" => "승인완료", "returned" => "반려완료",
            _ => throw new System.IO.InvalidDataException("제출 상태를 확인해 주세요.")
        };

        private void FormatRound(
            object? sender,
            DataGridViewCellFormattingEventArgs e)
        {
            bool roundColumn =
                (sender == gridReviews && e.ColumnIndex == 3) ||
                (sender == gridVersions && e.ColumnIndex == 0) ||
                (sender == gridAttachments && e.ColumnIndex == 1);

            if (!roundColumn || e.Value == null) return;

            e.Value = RoundText(e.Value.ToString() ?? "");
            e.FormattingApplied = true;
        }

        private static string RoundText(string value)
        {
            string text = value.Trim().TrimStart('v', 'V');

            return BigInteger.TryParse(text, out BigInteger number) && number > 0
                ? $"{number}차"
                : "—";
        }

        private static string CellText(
            DataGridViewRow? row,
            int index) =>
            row?.Cells[index].Value?.ToString() ?? "";

        private static bool IsDataError(Exception ex) =>
            ex is System.IO.InvalidDataException
                or System.IO.IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or FormatException;

        private void cmbResultView_SelectedIndexChanged(
            object? sender,
            EventArgs e)
        {
            if (previewReady &&
                !PreviewActions.IsDesignMode(this))
            {
                ShowScopePreview();
            }
        }

        private async void Unavailable_Click(object? sender, EventArgs e)
        {
            try
            {
                if (sender == btnDownloadAttachment)
                    await DownloadAttachmentAsync();
                else
                    await RefreshAsync();
            }
            catch (Exception error) when (error is ApiException or OperationCanceledException ||
                IsDataError(error))
            {
                if (!IsDisposed && !Disposing)
                    MessageBox.Show(this, error.Message, "처리 확인");
            }
        }

        private void btnApprovePreview_Click(
            object? sender,
            EventArgs e) =>
            OpenResultForm(
                () => new ReviewDecisionForm(true, results!, selectedTask!.Summary, SelectedSubmission!),
                true);

        private void btnRejectPreview_Click(
            object? sender,
            EventArgs e) =>
            OpenResultForm(
                () => new ReviewDecisionForm(false, results!, selectedTask!.Summary, SelectedSubmission!),
                true);

        private void btnSubmissionPreview_Click(
            object? sender,
            EventArgs e) =>
            OpenResultForm(
                () => new SubmissionForm(previewRole, loginSession!, taskQueries!),
                false);

        private async void OpenResultForm(
            Func<Form> createForm,
            bool review)
        {
            var row = gridReviews.CurrentRow;

            if (row == null)
            {
                MessageBox.Show(
                    this,
                    "업무를 선택해 주세요.",
                    "선택 확인");

                return;
            }

            if (!HasCurrentTask)
            {
                MessageBox.Show(
                    this,
                    "업무 상세를 조회한 뒤 다시 실행해 주세요.",
                    "업무 조회 확인");
                return;
            }

            if (review ? !CanReviewSelected() : !CanSubmitSelectedTask())
            {
                MessageBox.Show(this, review
                    ? "최신 검토 대기 제출물을 선택해 주세요."
                    : "마감일 안의 진행중 업무를 선택해 주세요. 반려 후에는 재작업 시작이 필요합니다.");
                return;
            }

            using var form = createForm();

            // 제목·이름·화면 상태를 권한이나 제출 회차로 사용하지 않습니다.
            form.Tag = new Dictionary<string, string>
            {
                ["TaskId"] = selectedTask!.Summary.TaskId
            };

            if (form.ShowDialog(this) == DialogResult.OK && loginSession?.IsCurrent == true)
            {
                ResultsSaved?.Invoke(this, EventArgs.Empty);
                await RefreshAsync();
            }
        }

        private async void RefreshAfterSave()
        {
            await RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            if (!previewReady || IsDisposed || Disposing) return;
            string selectedId = CellText(gridReviews.CurrentRow, 0);
            bool reviewing = IsReviewing;
            int request = ++listRequest;
            detailRequest++;
            listCancellation?.Cancel();
            listCancellation?.Dispose();
            listCancellation = null;
            detailCancellation?.Cancel();
            loading = true;
            ClearQueryView("업무 목록을 불러오는 중입니다.");
            if (taskQueries == null || loginSession?.IsCurrent != true)
            {
                loading = false;
                ClearQueryView("로그인 후 업무 조회를 사용할 수 있습니다.");
                return;
            }
            listCancellation = CancellationTokenSource.CreateLinkedTokenSource(loginSession.Cancellation);
            var token = listCancellation.Token;
            try
            {
                IReadOnlyList<TaskSummary> tasks = reviewing
                    ? await results!.ReviewTasksAsync(token)
                    : await taskQueries.GetTasksAsync(new TaskListQuery(TaskQueryScope.Own), token);
                var rows = new List<(TaskSummary Task, ResultSubmission? Last, string Error)>();
                foreach (var task in tasks)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        var history = await results!.SubmissionsAsync(task.TaskId, token);
                        rows.Add((task, history.FirstOrDefault(), ""));
                    }
                    catch (ApiException error)
                    {
                        token.ThrowIfCancellationRequested();
                        rows.Add((task, null, "제출 정보 조회 실패: " + error.Message));
                    }
                }
                await ApplyUiAsync(() =>
                {
                    if (!CanDisplay(token) || request != listRequest) return;
                    updating = true;
                    try
                    {
                        foreach (var item in rows)
                        {
                            int index = gridReviews.Rows.Add(item.Task.TaskId, item.Task.Title,
                                item.Task.AssigneeName, item.Last?.RoundNo ?? "",
                                item.Last == null ? "" : Kst(item.Last.SubmittedAt),
                                DisplayStatus(item.Task.Status));
                            gridReviews.Rows[index].Tag = item.Task;
                            gridReviews.Rows[index].ErrorText = item.Error;
                        }
                        var selected = gridReviews.Rows.Cast<DataGridViewRow>()
                            .FirstOrDefault(r => CellText(r, 0) == selectedId);
                        if (selected != null) gridReviews.CurrentCell = selected.Cells[0];
                    }
                    finally { updating = false; }
                    loading = false;
                    FilterReviews();
                }, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) when (error is TaskQueryException or ApiException || IsDataError(error))
            {
                await ApplyUiAsync(() =>
                {
                    if (request != listRequest) return;
                    loading = false;
                    ClearQueryView(QueryError(error));
                }, token);
            }
        }

        private async Task ApplyUiAsync(Action action, CancellationToken token)
        {
            if (IsDisposed || Disposing || viewClosed.IsCancellationRequested ||
                token.IsCancellationRequested || !IsHandleCreated) return;
            if (!InvokeRequired) { action(); return; }
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var cancelled = token.Register(() => completion.TrySetResult(false));
            using var closed = viewClosed.Token.Register(() => completion.TrySetResult(false));
            try
            {
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed || Disposing || token.IsCancellationRequested ||
                        viewClosed.IsCancellationRequested)
                    { completion.TrySetResult(false); return; }
                    try { action(); completion.TrySetResult(true); }
                    catch (Exception error) { completion.TrySetException(error); }
                }));
            }
            catch (InvalidOperationException) { completion.TrySetResult(false); }
            await completion.Task.ConfigureAwait(false);
        }

        private bool CanDisplay(CancellationToken token) =>
            !IsDisposed &&
            !Disposing &&
            !token.IsCancellationRequested &&
            loginSession?.IsCurrent == true;

        private void CancelQueries()
        {
            listRequest++;
            detailRequest++;
            listCancellation?.Cancel();
            detailCancellation?.Cancel();
            selectedTask = null;
        }

        private void ClearQueryView(string message)
        {
            updating = true;
            selectedTask = null;
            submissions.Clear();
            gridReviews.Rows.Clear();
            gridVersions.Rows.Clear();
            gridAttachments.Rows.Clear();
            updating = false;
            txtContent2.Text = message;
            UpdateActionState();
            btnDownloadAttachment.Enabled = false;
        }

        private void ClearSelectedView(string message)
        {
            updating = true;
            selectedTask = null;
            submissions.Clear();
            gridVersions.Rows.Clear();
            gridAttachments.Rows.Clear();
            updating = false;
            txtContent2.Text = message;
            UpdateActionState();
            btnDownloadAttachment.Enabled = false;
        }

        private static string DisplayStatus(TaskDisplayStatus status) =>
            status switch
            {
                TaskDisplayStatus.NotStarted => "진행전",
                TaskDisplayStatus.InProgress => "진행중",
                TaskDisplayStatus.Submitted => "제출완료",
                TaskDisplayStatus.Approved => "승인완료",
                TaskDisplayStatus.Returned => "반려완료",
                _ => throw new InvalidOperationException("알 수 없는 업무 상태입니다.")
            };

        private static string QueryError(Exception ex) =>
            ex is TaskQueryException error
                ? error.Failure switch
                {
                    TaskQueryFailure.NotConfigured =>
                        "업무 조회 API가 연결되지 않았습니다. 서버의 업무 조회 설정을 확인해 주세요.",
                    TaskQueryFailure.AuthenticationExpired =>
                        "로그인이 만료되었습니다. 다시 로그인해 주세요.",
                    TaskQueryFailure.Forbidden =>
                        "이 업무를 조회할 권한이 없습니다.",
                    TaskQueryFailure.NotFound =>
                        "선택한 업무를 찾을 수 없습니다. 목록을 다시 조회해 주세요.",
                    _ => "업무를 가져오지 못했습니다. 서버 연결을 확인하고 조회 버튼으로 다시 시도해 주세요."
                }
                : ex.Message;
    }
}

// 별도 ResultsApi.cs 파일 없이 세 화면에서 사용하는 공통 통신 코드입니다.
namespace RemoteWork.WinForms.Features.Results
{
    // 기존 공통 Session의 JSON GET/POST로 파일 조각을 인증·전송합니다.
    public interface IResultsFileTransport
    {
        Task<ResultAttachment> UploadAsync(ApiClient.Session session, string path,
            string name, Stream content, CancellationToken token);
        Task<Stream> DownloadAsync(ApiClient.Session session, string path,
            CancellationToken token);
    }

    public static class ResultsConnection
    {
        public static IResultsFileTransport? Files { get; set; } = new ResultsFileTransport();
    }

    public sealed record ResultItems<T>(List<T> Items);
    public sealed record ResultAttachment(string AttachmentId, string Name, long Size, string Sha256);
    public sealed record ResultSubmission(string SubmissionId, string TaskId, int AssignmentRevision,
        string RoundNo, int SubmittedBy, string SubmittedByName, string Description, string Status,
        DateTimeOffset SubmittedAt, int? ReviewerId, string Reason, DateTimeOffset? ReviewedAt,
        List<ResultAttachment> Attachments);
    public sealed record ResultContext(string TaskId, string Title, int AssigneeId, string RowVersion,
        int AssignmentRevision, string TaskStatus, DateOnly DueDate, int ReviewerId,
        bool ExtensionPending, string NextRoundNo);
    public sealed record DecisionContext(string TaskId, string SubmissionId, string RowVersion,
        int AssignmentRevision, string RoundNo, string TaskStatus);
    public sealed record ResultSaved(string TaskId, string SubmissionId, string RoundNo,
        string RowVersion, int AssignmentRevision, string TaskStatus, DateTimeOffset UpdatedAt,
        string? DecisionId = null);
    public sealed record ResultHistoryItem(string EventId, string TaskId, string SubmissionId,
        int ActorId, string ActorName, string Kind, string Summary, DateTimeOffset OccurredAt);
    public sealed record ResultHistoryPage(List<ResultHistoryItem> Items, string? NextBeforeEventId);
    public sealed record SubmitResultBody(string ExpectedRowVersion, int ExpectedAssignmentRevision,
        string Description, string[] AttachmentIds);
    public sealed record DecideResultBody(string ExpectedRowVersion, int ExpectedAssignmentRevision,
        string Outcome, string Reason);
    internal sealed record ResultTask(string TaskId, string Title, string AssigneeName,
        string TaskStatus, DateOnly StartDate, DateOnly DueDate);
    internal sealed record ResultKey(string RequestKey, DateTimeOffset ExpiresAt);
    internal sealed record ResultLookup(string State, int? HttpStatus, JsonElement? Result);

    public sealed class PendingResultWrite
    {
        public required string Action { get; init; }
        public required string Target { get; init; }
        public required string Path { get; init; }
        public required string TaskId { get; init; }
        public required JsonElement Body { get; init; }
        public ResultAttachment[] Files { get; init; } = [];
        public object? Draft { get; set; }
        internal ResultKey? Key { get; set; }
        public string Stage { get; internal set; } = "new";
        internal int LastFailureStatus { get; set; }
        public bool Locked => Stage != "new";
    }

    public sealed class ResultsApi
    {
        public const long MaxFileBytes = 25L * 1024 * 1024;
        public ApiClient.Session Session { get; }
        private readonly IResultsFileTransport? files;
        public bool CanTransferFiles => files != null;

        // 동일 로그인 접속에서 창/메뉴를 다시 열어도 불명확 요청을 보존합니다.
        // 로그아웃·동일 계정 재로그인 때 폐기하며 디스크에는 저장하지 않습니다.
        private sealed class SessionWrites
        {
            public readonly ConcurrentDictionary<string, PendingResultWrite> Items = new();
            public int Busy;
        }
        private static readonly ConcurrentDictionary<CancellationToken, SessionWrites> Writes = new();

        public ResultsApi(ApiClient.Session session, IResultsFileTransport? fileTransport = null)
        {
            Session = session;
            files = fileTransport;
            session.ThrowIfChanged();
            if (Writes.TryAdd(session.Cancellation, new SessionWrites()))
            {
                session.Cancellation.Register(() =>
                {
                    if (Writes.TryRemove(session.Cancellation, out var old))
                        old.Items.Clear();
                });
            }
            if (!session.IsCurrent)
            {
                Writes.TryRemove(session.Cancellation, out _);
                session.ThrowIfChanged();
            }
        }

        private SessionWrites Store
        {
            get
            {
                Session.ThrowIfChanged();
                return Writes.TryGetValue(Session.Cancellation, out var value)
                    ? value : throw new OperationCanceledException();
            }
        }

        private static string Slot(string action, string target) => action + ":" + target;
        public PendingResultWrite? Pending(string action, string target) =>
            Session.IsCurrent && Writes.TryGetValue(Session.Cancellation, out var store)
                ? store.Items.GetValueOrDefault(Slot(action, target)) : null;

        public PendingResultWrite Begin(string action, string target, string path, string taskId,
            object body, IEnumerable<ResultAttachment>? uploaded = null)
        {
            var operation = new PendingResultWrite
            {
                Action = action, Target = Id(target), Path = path, TaskId = Id(taskId),
                Body = JsonSerializer.SerializeToElement(body, ApiClient.Json),
                Files = uploaded?.ToArray() ?? []
            };
            if (System.Text.Encoding.UTF8.GetByteCount(operation.Body.GetRawText()) > 32768)
                throw new InvalidDataException("공통 요청 크기 32KiB를 넘었습니다. 설명 또는 의견을 줄여 주세요.");
            var existing = Store.Items.GetOrAdd(Slot(action, target), operation);
            if (existing.Body.GetRawText() != operation.Body.GetRawText() ||
                existing.TaskId != operation.TaskId || existing.Path != operation.Path)
                throw new InvalidOperationException("같은 대상의 기존 요청이 있습니다. 창을 다시 열어 처리 결과를 확인해 주세요.");
            return existing;
        }

        public void Forget(PendingResultWrite operation) =>
            Store.Items.TryRemove(Slot(operation.Action, operation.Target), out _);

        public async Task<ResultSaved?> RunAsync(PendingResultWrite operation, CancellationToken token)
        {
            SessionWrites store = Store;
            if (Interlocked.Exchange(ref store.Busy, 1) != 0)
                throw new InvalidOperationException("다른 결과물 저장 처리가 진행 중입니다.");
            try
            {
                if (!ReferenceEquals(Pending(operation.Action, operation.Target), operation))
                    throw new InvalidOperationException("현재 접속의 요청을 다시 확인해 주세요.");
                if (operation.Stage == "completed") operation.Stage = "check";
                if (operation.Stage == "check")
                {
                    var lookup = await Session.PostIdempotentAsync<ResultLookup>(
                        "write-requests/result", new { action = operation.Action, target = operation.Target },
                        operation.Key!.RequestKey, token);
                    Session.ThrowIfChanged();
                    if (lookup.State == "not_recorded")
                    {
                        operation.Stage = operation.Key.ExpiresAt <= DateTimeOffset.UtcNow ? "expired" :
                            operation.LastFailureStatus is >= 400 and <= 599 ? "reprepare" : "retry";
                        return null;
                    }
                    int expectedStatus = operation.Action == "submission.submitted" ? 201 : 200;
                    if (lookup.State != "completed" || lookup.HttpStatus != expectedStatus ||
                        lookup.Result is null)
                        throw new InvalidDataException("저장 결과 응답을 확인할 수 없습니다.");
                    ResultSaved saved = lookup.Result.Value.Deserialize<ResultSaved>(ApiClient.Json)
                        ?? throw new InvalidDataException("저장 결과가 비어 있습니다.");
                    ValidateSaved(operation, saved);
                    operation.Stage = "completed";
                    return saved;
                }
                if (operation.Stage == "reprepare")
                    throw new InvalidOperationException("업무를 다시 확인한 뒤 새 요청을 준비해 주세요.");
                if (operation.Stage == "expired")
                    throw new InvalidOperationException("요청 키가 만료되었습니다. 현재 제출 기록 확인이 필요합니다.");
                operation.Key ??= await Session.PostAsync<ResultKey>(
                    "write-requests/keys", new { action = operation.Action, target = operation.Target }, token);
                Session.ThrowIfChanged();
                if (operation.Key.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    operation.Stage = "check";
                    return null;
                }
                // POST 직전부터 응답 불명확으로 간주합니다. 자동 재전송하지 않습니다.
                operation.Stage = "check";
                ResultSaved result;
                try
                {
                    result = await Session.PostIdempotentAsync<ResultSaved>(
                        operation.Path, operation.Body, operation.Key.RequestKey, token);
                }
                catch (ApiException error)
                {
                    operation.LastFailureStatus = error.StatusCode;
                    throw;
                }
                Session.ThrowIfChanged();
                ValidateSaved(operation, result);
                operation.Stage = "completed";
                return result;
            }
            catch (ApiException error) when (error.StatusCode == 410)
            {
                operation.Stage = "expired";
                throw;
            }
            finally { Volatile.Write(ref store.Busy, 0); }
        }

        private static void ValidateSaved(PendingResultWrite operation, ResultSaved saved)
        {
            string expectedState = operation.Action.Split('.')[1];
            long expectedVersion = long.Parse(
                operation.Body.GetProperty("expected_row_version").GetString()!,
                System.Globalization.CultureInfo.InvariantCulture);
            int expectedAssignment = operation.Body.GetProperty("expected_assignment_revision").GetInt32();
            if (saved.TaskId != operation.TaskId || Id(saved.SubmissionId).Length == 0 ||
                Id(saved.RoundNo).Length == 0 || Id(saved.RowVersion).Length == 0 ||
                saved.TaskStatus != expectedState || saved.AssignmentRevision != expectedAssignment ||
                long.Parse(saved.RowVersion, System.Globalization.CultureInfo.InvariantCulture) != checked(expectedVersion + 1) ||
                (expectedState != "submitted" && (saved.SubmissionId != operation.Target ||
                    string.IsNullOrEmpty(saved.DecisionId))) ||
                saved.UpdatedAt.Offset != TimeSpan.Zero)
                throw new InvalidDataException("저장 결과의 업무·버전·상태가 일치하지 않습니다.");
        }

        internal static string Id(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 19 || value[0] == '0' ||
                value.Any(c => c is < '0' or > '9') || !long.TryParse(value, out long number) || number < 1)
                throw new InvalidDataException("서버 식별자 형식을 확인해 주세요.");
            return value;
        }

        private static TaskSummary Summary(ResultTask row) => new(
            Id(row.TaskId), row.Title, row.AssigneeName, row.TaskStatus switch
            {
                "not_started" => TaskDisplayStatus.NotStarted,
                "in_progress" => TaskDisplayStatus.InProgress,
                "submitted" => TaskDisplayStatus.Submitted,
                "approved" => TaskDisplayStatus.Approved,
                "returned" => TaskDisplayStatus.Returned,
                _ => throw new InvalidDataException("알 수 없는 업무 상태입니다.")
            }, row.StartDate, row.DueDate);

        public async Task<IReadOnlyList<TaskSummary>> ReviewTasksAsync(CancellationToken token) =>
            (await Session.GetAsync<ResultItems<ResultTask>>("results/review-tasks", token))
                .Items.Select(Summary).ToArray();
        public async Task<IReadOnlyList<TaskSummary>> HistoryTasksAsync(CancellationToken token) =>
            (await Session.GetAsync<ResultItems<ResultTask>>("results/history-tasks", token))
                .Items.Select(Summary).ToArray();
        public async Task<TaskDetail> ReviewTaskAsync(string id, CancellationToken token) =>
            new(Summary(await Session.GetAsync<ResultTask>("results/tasks/" + Id(id), token)), "");

        public async Task<List<ResultSubmission>> SubmissionsAsync(string id, CancellationToken token)
        {
            var items = await Session.GetAsync<ResultItems<ResultSubmission>>(
                "results/tasks/" + Id(id) + "/submissions", token);
            if (items.Items == null || items.Items.Any(s => s == null || s.TaskId != id || s.Attachments == null))
                throw new InvalidDataException("제출 목록의 업무가 일치하지 않습니다.");
            foreach (var item in items.Items)
            {
                Id(item.SubmissionId);
                Id(item.RoundNo);
                if (item.Status is not ("submitted" or "approved" or "returned"))
                    throw new InvalidDataException("제출 상태를 확인해 주세요.");
            }
            return items.Items;
        }
        public Task<ResultContext> ContextAsync(string id, CancellationToken token) =>
            Session.GetAsync<ResultContext>("results/tasks/" + Id(id) + "/context", token);
        public Task<DecisionContext> DecisionContextAsync(string id, CancellationToken token) =>
            Session.GetAsync<DecisionContext>("results/submissions/" + Id(id) + "/context", token);
        public async Task<ResultHistoryPage> HistoryAsync(string id, string? before, CancellationToken token,
            bool includeTaskEvents = false)
        {
            var page = await Session.GetAsync<ResultHistoryPage>("results/tasks/" + Id(id) +
                (includeTaskEvents ? "/activity" : "/history") +
                (before == null ? "" : "?before_event_id=" + Id(before)), token);
            if (page.Items == null || page.Items.Any(e => e == null || e.TaskId != id))
                throw new InvalidDataException("이력 응답 형식을 확인해 주세요.");
            foreach (var item in page.Items) Id(item.EventId);
            if (page.NextBeforeEventId != null) Id(page.NextBeforeEventId);
            return page;
        }

        public async Task<ResultAttachment> UploadAsync(string taskId, string path, CancellationToken token)
        {
            if (files == null)
                throw new InvalidOperationException("1번 공통 Session의 파일 전송 연결이 필요합니다.");
            await using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                65536, useAsync: true);
            if (source.Length > MaxFileBytes)
                throw new InvalidDataException("파일 하나는 25MiB 이하여야 합니다.");
            string hash = Convert.ToHexString(await SHA256.HashDataAsync(source, token)).ToLowerInvariant();
            long length = source.Length;
            source.Position = 0;
            var result = await files.UploadAsync(Session, "results/tasks/" + Id(taskId) + "/uploads",
                Path.GetFileName(path), source, token);
            Session.ThrowIfChanged();
            if (!Guid.TryParseExact(result.AttachmentId, "D", out _) || result.Size != length ||
                result.Name != Path.GetFileName(path) || result.Sha256 != hash)
                throw new InvalidDataException("업로드된 파일의 크기·해시가 일치하지 않습니다.");
            return result;
        }

        public async Task DownloadAsync(ResultAttachment file, string destination, CancellationToken token)
        {
            if (files == null)
                throw new InvalidOperationException("1번 공통 Session의 파일 전송 연결이 필요합니다.");
            if (!Guid.TryParseExact(file.AttachmentId, "D", out _) || file.Size is < 0 or > MaxFileBytes)
                throw new InvalidDataException("첨부 정보를 확인해 주세요.");
            string fullPath = Path.GetFullPath(destination);
            string tempPath = Path.Combine(Path.GetDirectoryName(fullPath)!, ".result-" + Guid.NewGuid() + ".partial");
            try
            {
                await using (var input = await files.DownloadAsync(Session,
                    "results/attachments/" + file.AttachmentId + "/download", token))
                await using (var output = new FileStream(tempPath, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 65536, useAsync: true))
                using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
                {
                    byte[] buffer = new byte[65536];
                    long length = 0;
                    int count;
                    while ((count = await input.ReadAsync(buffer, token)) > 0)
                    {
                        length += count;
                        if (length > file.Size)
                            throw new InvalidDataException("다운로드 크기가 첨부 정보와 다릅니다.");
                        hash.AppendData(buffer, 0, count);
                        await output.WriteAsync(buffer.AsMemory(0, count), token);
                    }
                    if (length != file.Size ||
                        Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant() != file.Sha256)
                        throw new InvalidDataException("다운로드 크기·해시 검증에 실패했습니다.");
                }
                Session.ThrowIfChanged();
                token.ThrowIfCancellationRequested();
                File.Move(tempPath, fullPath, overwrite: true);
            }
            finally { if (File.Exists(tempPath)) File.Delete(tempPath); }
        }

        public static string PendingText(PendingResultWrite operation) => operation.Stage switch
        {
            "check" => "응답만으로 저장 여부를 확정할 수 없습니다. 버튼을 눌러 처리 결과를 확인해 주세요.",
            "retry" => "서버에 기록이 없음을 확인했습니다. 버튼을 다시 누르면 같은 키·내용으로 재전송합니다.",
            "expired" => "키가 만료되었습니다. 현재 제출 기록을 먼저 확인해야 합니다.",
            "reprepare" => "저장되지 않은 실패 요청입니다. 버튼을 눌러 업무를 다시 확인한 뒤 입력을 수정해 주세요.",
            _ => "입력 내용을 확인한 뒤 처리해 주세요."
        };
    }



    internal sealed class ResultsFileTransport : IResultsFileTransport
    {
        private const int ChunkBytes = 16384;
        private sealed record UploadProgress(string AttachmentId, long NextOffset);
        private sealed record FileChunk(string AttachmentId, long Offset, long NextOffset, bool Eof, string Data);

        public async Task<ResultAttachment> UploadAsync(ApiClient.Session session, string path,
            string name, Stream content, CancellationToken token)
        {
            if (!content.CanSeek) throw new InvalidOperationException("파일 스트림을 확인해 주세요.");
            long start = content.Position;
            long size = content.Length - start;
            if (size is < 0 or > ResultsApi.MaxFileBytes)
                throw new InvalidDataException("파일 하나는 25MiB 이하여야 합니다.");
            string hash = Convert.ToHexString(await SHA256.HashDataAsync(content, token)).ToLowerInvariant();
            content.Position = start;
            var upload = await session.PostAsync<ResultAttachment>(path + "/start",
                new { name, size, sha256 = hash }, token);
            if (!Guid.TryParseExact(upload.AttachmentId, "D", out _) ||
                upload.Name != name || upload.Size != size || upload.Sha256 != hash)
                throw new InvalidDataException("업로드 준비 정보가 일치하지 않습니다.");
            byte[] buffer = new byte[ChunkBytes];
            long offset = 0;
            while (offset < size)
            {
                int expected = (int)Math.Min(ChunkBytes, size - offset);
                int count = 0;
                while (count < expected)
                {
                    int read = await content.ReadAsync(buffer.AsMemory(count, expected - count), token);
                    if (read == 0) throw new InvalidDataException("전송 중 파일 크기가 변경되었습니다.");
                    count += read;
                }
                var progress = await session.PostAsync<UploadProgress>(
                    "results/uploads/" + upload.AttachmentId + "/chunks",
                    new { offset, data = Convert.ToBase64String(buffer, 0, count) }, token);
                session.ThrowIfChanged();
                if (progress.AttachmentId != upload.AttachmentId || progress.NextOffset != offset + count)
                    throw new InvalidDataException("파일 전송 위치가 일치하지 않습니다.");
                offset += count;
            }
            return await session.PostAsync<ResultAttachment>(
                "results/uploads/" + upload.AttachmentId + "/complete", new { }, token);
        }

        public async Task<Stream> DownloadAsync(ApiClient.Session session, string path, CancellationToken token)
        {
            if (!path.EndsWith("/download", StringComparison.Ordinal))
                throw new InvalidDataException("첨부 다운로드 경로를 확인해 주세요.");
            string chunkPath = path[..^"/download".Length] + "/chunks";
            string attachmentId = path.Split('/')[2];
            var output = new MemoryStream();
            try
            {
                long offset = 0;
                while (true)
                {
                    var chunk = await session.GetAsync<FileChunk>(chunkPath + "?offset=" +
                        offset.ToString(System.Globalization.CultureInfo.InvariantCulture), token);
                    session.ThrowIfChanged();
                    byte[] bytes;
                    try { bytes = Convert.FromBase64String(chunk.Data); }
                    catch (FormatException) { throw new InvalidDataException("파일 조각 형식이 올바르지 않습니다."); }
                    if (chunk.AttachmentId != attachmentId || chunk.Offset != offset ||
                        bytes.Length > ChunkBytes || chunk.NextOffset != offset + bytes.Length ||
                        (!chunk.Eof && bytes.Length == 0) || chunk.NextOffset > ResultsApi.MaxFileBytes)
                        throw new InvalidDataException("파일 조각의 위치·크기가 다릅니다.");
                    await output.WriteAsync(bytes, token);
                    offset = chunk.NextOffset;
                    if (chunk.Eof) break;
                }
                output.Position = 0;
                return output;
            }
            catch { await output.DisposeAsync(); throw; }
        }
    }
}
