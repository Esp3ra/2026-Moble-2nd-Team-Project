using System.ComponentModel;
using System.IO;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Results
{
    public partial class SubmissionForm : Form
    {
        // 1번 LOG에서 전달한 개발 기준입니다.
        private const long MaxAttachmentBytes = 25L * 1024 * 1024;
        private const int MaxAttachmentCount = 10;

        private readonly List<PendingAttachment> pendingFiles = new();
        private readonly CancellationTokenSource formCancellation = new();
        private CancellationTokenRegistration sessionRegistration;
        private ApiClient.Session? session;
        private ITaskQueryService? taskQueries;
        private ITaskPreparationService? taskPreparation;
        private ResultsApi? results;
        private ResultContext? submissionContext;
        private string taskId = "";
        private bool busy;
        private bool accessBlocked;
        private bool lifetimeDisposed;

        private sealed class SubmissionAccessException(string message)
            : Exception(message);

        private sealed record PendingAttachment(
            string Path,
            string Name,
            long Size,
            DateTime LastWriteTimeUtc,
            ResultAttachment? Uploaded = null);

        public SubmissionForm() : this(PreviewRole.Employee) { }

        public SubmissionForm(PreviewRole role)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;

            InitializeSampleData(role);

            Disposed += (_, _) =>
            {
                if (lifetimeDisposed) return;
                lifetimeDisposed = true;
                formCancellation.Cancel();
                sessionRegistration.Dispose();
                formCancellation.Dispose();
                pendingFiles.Clear();
            };
        }

        // 리뷰컨트롤에서 캡처한 동일한 로그인 접속과 조회 서비스를 받습니다.
        internal SubmissionForm(
            PreviewRole role,
            ApiClient.Session currentSession,
            ITaskQueryService queries) : this(role)
        {
            session = currentSession;
            taskQueries = queries;
            taskPreparation = new HttpTaskPreparationService(currentSession);
        }

        private void InitializeSampleData(PreviewRole role)
        {
            // 기존 메서드 이름은 유지하되 고정 예시는 생성하지 않습니다.
            lblSubmissionVersion.Text = "담당자 / 제출 회차";
            columnGridPreviousFiles2.HeaderText = "제출 회차";

            txtTaskSummary.Clear();
            txtSubmissionVersion.Clear();
            txtResultDescription.Clear();
            txtContent4.Text = "선택한 업무 정보를 기다리고 있습니다.";
            txtSubmissionStatus.Text = "선택한 업무의 제출 가능 여부를 확인하면 결과 설명을 입력할 수 있습니다.";
            txtResultDescription.PlaceholderText = "제출할 결과 설명을 입력하세요.";

            gridPendingFiles.Rows.Clear();
            gridPreviousFiles.Rows.Clear();
            btnSubmit.DialogResult = DialogResult.None;

            UpdateButtons();
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            if (PreviewActions.IsDesignMode(this)) return;

            try
            {
                if (Tag is not IDictionary<string, string> context ||
                    !context.TryGetValue("TaskId", out string? id) ||
                    string.IsNullOrWhiteSpace(id))
                {
                    BlockAccess("결과물 목록에서 업무를 선택한 뒤 열어 주세요.");
                    return;
                }

                taskId = id;

                // 화면의 역할이나 이름으로 제출 권한을 판단하지 않습니다.
                session ??= AppServices.Api.CaptureSession();
                taskQueries ??= new HttpTaskQueryService(AppServices.Api);
                taskPreparation ??= new HttpTaskPreparationService(session);
                results ??= new ResultsApi(session, ResultsConnection.Files);

                sessionRegistration = session.Cancellation.Register(() =>
                {
                    if (IsDisposed || Disposing || !IsHandleCreated) return;

                    try
                    {
                        BeginInvoke(new Action(() =>
                        {
                            if (!IsDisposed && !Disposing)
                                BlockAccess("접속이 변경되었습니다. 다시 로그인해 주세요.");
                        }));
                    }
                    catch (InvalidOperationException) { }
                });

                busy = true;
                SetStatus("업무·배정·마감·검토자 정보를 확인하고 있습니다. 확인 중에는 입력과 제출이 잠깁니다.");
                UpdateButtons();
                var pending = results.Pending("submission.submitted", taskId);
                if (pending != null)
                    RestorePending(pending);
                else
                    await ReadCurrentTaskAsync();
            }
            catch (OperationCanceledException)
            {
                if (CanShowMessage())
                    BlockAccess("업무 확인이 취소되었습니다. 다시 로그인하거나 창을 다시 열어 주세요.");
            }
            catch (Exception ex) when (IsExpectedError(ex))
            {
                if (CanShowMessage()) ShowError(ex);
            }
            finally
            {
                busy = false;
                if (CanShowMessage()) UpdateButtons();
            }
        }

        private async Task ReadCurrentTaskAsync()
        {
            DemandSession();
            submissionContext = null;
            UpdateButtons();

            if (taskQueries == null || taskPreparation == null)
                throw new InvalidOperationException("업무 조회 연결을 확인해 주세요.");

            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                formCancellation.Token, session!.Cancellation);

            CancellationToken token = cancellation.Token;

            TaskDetail detail = await taskQueries.GetTaskAsync(taskId, token);
            session.ThrowIfChanged();
            token.ThrowIfCancellationRequested();

            TaskWriteContext context =
                await taskPreparation.GetContextAsync(taskId, token);

            session.ThrowIfChanged();
            token.ThrowIfCancellationRequested();

            if (detail.Summary.TaskId != taskId ||
                context.TaskId != taskId ||
                detail.Summary.Status != context.Status)
            {
                throw new InvalidDataException(
                    "조회 중 업무 정보가 변경되었습니다. 결과물 목록을 다시 조회해 주세요.");
            }

            if (context.AssigneeId != session.User.Id)
            {
                throw new SubmissionAccessException(
                    "현재 로그인한 사용자에게 배정된 업무만 제출할 수 있습니다.");
            }

            // Display only after the captured login's current assignment is
            // confirmed. Result-provider failures must not leave an unexplained
            // blank form, and this display is not permission to submit.
            txtTaskSummary.Text = $"{taskId} · {detail.Summary.Title}";
            txtSubmissionVersion.Text = $"{detail.Summary.AssigneeName} / 제출 회차 확인 중";

            if (context.Status != TaskDisplayStatus.InProgress)
                throw new InvalidOperationException(StateMessage(context.Status));

            ValidateDeadline(detail.Summary.DueDate, DateTimeOffset.UtcNow);
            if (results == null)
                throw new InvalidOperationException("결과물 서버 연결이 필요합니다.");
            ResultContext ready = await results.ContextAsync(taskId, token);
            session.ThrowIfChanged();
            token.ThrowIfCancellationRequested();
            if (ready.TaskId != taskId || ready.AssigneeId != session.User.Id ||
                ready.RowVersion != context.RowVersion ||
                ready.AssignmentRevision != context.AssignmentRevision ||
                ready.TaskStatus != "in_progress" || ready.DueDate != detail.Summary.DueDate ||
                ready.ExtensionPending || ready.ReviewerId < 1 || ready.ReviewerId == session.User.Id)
                throw new InvalidDataException("업무·배정·검토자·연장 상태를 다시 확인해 주세요.");
            ResultsApi.Id(ready.NextRoundNo);
            var previous = await results.SubmissionsAsync(taskId, token);
            session.ThrowIfChanged();
            token.ThrowIfCancellationRequested();
            submissionContext = ready;
            txtTaskSummary.Text = $"{taskId} · {detail.Summary.Title}";
            txtSubmissionVersion.Text = $"{detail.Summary.AssigneeName} / {ready.NextRoundNo}차";
            gridPreviousFiles.Rows.Clear();
            foreach (var item in previous)
                foreach (var file in item.Attachments)
                    gridPreviousFiles.Rows.Add(file.Name, item.RoundNo + "차",
                        item.SubmittedAt.ToOffset(TimeSpan.FromHours(9)).ToString("yyyy-MM-dd HH:mm"));
            var last = previous.FirstOrDefault();
            txtContent4.Text = $"마감일: {ready.DueDate:yyyy-MM-dd}\r\n검토자 ID: {ready.ReviewerId}\r\n" +
                (last == null ? "이전 제출 없음" : $"이전 {last.RoundNo}차 · {last.Status}\r\n검토 의견: {last.Reason}");
            SetStatus($"제출 준비 완료 · {ready.NextRoundNo}차 · 마감일 {ready.DueDate:yyyy-MM-dd}\r\n결과 설명을 입력하고 ‘결과 제출’을 누르세요.");
        }

        private void RestorePending(PendingResultWrite operation)
        {
            txtTaskSummary.Text = "업무 " + taskId;
            txtSubmissionVersion.Text = "기존 요청 처리 결과 확인";
            txtResultDescription.Text = operation.Body.GetProperty("description").GetString();
            pendingFiles.Clear();
            if (operation.Draft is PendingAttachment[] draft)
                pendingFiles.AddRange(draft);
            gridPendingFiles.Rows.Clear();
            foreach (var file in operation.Files)
                gridPendingFiles.Rows.Add(file.Name, $"{file.Size / 1024.0:N1} KB", "서버 임시 첨부");
            txtContent4.Text = ResultsApi.PendingText(operation);
            SetStatus(txtContent4.Text);
        }

        private static void ValidateDeadline(
            DateOnly dueDate,
            DateTimeOffset now)
        {
            // 현재 업무 마감일의 한국 시간 당일이 끝날 때까지입니다.
            DateOnly today = DateOnly.FromDateTime(
                now.ToOffset(TimeSpan.FromHours(9)).DateTime);

            if (today > dueDate)
            {
                throw new InvalidOperationException(
                    $"마감일({dueDate:yyyy-MM-dd})이 지나 제출할 수 없습니다. " +
                    "연장 승인이 완료되고 업무 마감일이 갱신된 뒤 다시 확인해 주세요.");
            }

            // 이 날짜 검사는 연장 승인 여부를 대신하지 않습니다.
            // 연장 신청 대기 여부와 최종 마감은 서버 저장 직전에 다시 검사해야 합니다.
        }

        private static string StateMessage(TaskDisplayStatus status) =>
            status switch
            {
                TaskDisplayStatus.NotStarted =>
                    "업무 화면에서 업무 수락을 완료한 뒤 제출해 주세요.",
                TaskDisplayStatus.Returned =>
                    "업무 화면에서 재작업 시작을 완료한 뒤 재제출해 주세요.",
                TaskDisplayStatus.Submitted =>
                    "이미 제출완료 상태입니다. 검토 결과를 확인해 주세요.",
                TaskDisplayStatus.Approved =>
                    "이미 승인완료된 업무입니다.",
                _ => "현재 업무 상태에서는 제출할 수 없습니다."
            };

        private void UpdateButtons()
        {
            var pending = session?.IsCurrent == true && taskId.Length > 0
                ? results?.Pending("submission.submitted", taskId) : null;
            bool connected = !busy && !accessBlocked && session?.IsCurrent == true && taskId.Length > 0;
            bool canEdit = connected && pending == null && submissionContext != null;
            txtResultDescription.ReadOnly = !canEdit;
            btnAttachFile.Enabled = canEdit;
            btnRemoveAttachment.Enabled = canEdit && pendingFiles.Count > 0;
            btnSubmit.Enabled = connected && pending?.Stage != "expired" &&
                (submissionContext != null || pending != null);
            // Never replace an ambiguous write with a fresh context/read path.
            btnRetryTask.Enabled = connected && pending == null;
            btnSubmit.Text = pending?.Stage switch
            {
                "check" or "completed" => "처리 결과 확인",
                "retry" => "같은 요청 재전송",
                "reprepare" => "업무 다시 확인",
                "expired" => "요청 만료",
                _ => "결과 제출"
            };
        }

        private void DemandSession()
        {
            if (accessBlocked || session == null || !session.IsCurrent)
                throw new SubmissionAccessException("다시 로그인한 뒤 창을 열어 주세요.");

            formCancellation.Token.ThrowIfCancellationRequested();
        }

        private static PendingAttachment ReadAttachment(string path)
        {
            var info = new FileInfo(Path.GetFullPath(path));

            using var stream = new FileStream(
                info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);

            long size = stream.Length;

            if (size > MaxAttachmentBytes)
            {
                throw new InvalidDataException(
                    $"{info.Name}: 파일 1개는 25MiB 이하여야 합니다.");
            }

            info.Refresh();

            return new PendingAttachment(
                info.FullName, info.Name, size, info.LastWriteTimeUtc);
        }

        private void AddAttachments(IEnumerable<string> paths)
        {
            DemandSession();

            var knownPaths = new HashSet<string>(
                pendingFiles.Select(file => file.Path),
                StringComparer.OrdinalIgnoreCase);

            var additions = new List<PendingAttachment>();

            foreach (string path in paths)
            {
                string fullPath = Path.GetFullPath(path);
                if (!knownPaths.Add(fullPath)) continue;

                if (pendingFiles.Count + additions.Count >= MaxAttachmentCount)
                    throw new InvalidDataException("첨부파일은 최대 10개까지 선택할 수 있습니다.");

                additions.Add(ReadAttachment(fullPath));
            }

            DemandSession();

            // 선택한 묶음 전체를 확인한 뒤 목록에 추가합니다.
            // 원본 파일을 복사·이동·삭제하거나 서버에 업로드하지 않습니다.
            pendingFiles.AddRange(additions);
            ShowPendingFiles();
        }

        private void ShowPendingFiles()
        {
            gridPendingFiles.Rows.Clear();

            foreach (PendingAttachment file in pendingFiles)
            {
                int index = gridPendingFiles.Rows.Add(
                    file.Name,
                    $"{file.Size / 1024.0:N1} KB",
                    "선택한 파일");

                gridPendingFiles.Rows[index].Tag = file;
            }

            UpdateButtons();
        }

        private void RemoveAttachment()
        {
            if (gridPendingFiles.CurrentRow?.Tag is PendingAttachment file)
            {
                pendingFiles.Remove(file);
                ShowPendingFiles();
            }
        }

        private void ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(txtResultDescription.Text))
                throw new InvalidDataException("결과 설명을 입력해 주세요.");

            if (pendingFiles.Count > MaxAttachmentCount)
                throw new InvalidDataException("첨부파일은 최대 10개까지 선택할 수 있습니다.");

            foreach (PendingAttachment original in pendingFiles)
            {
                PendingAttachment current = ReadAttachment(original.Path);

                if (current.Size != original.Size ||
                    current.LastWriteTimeUtc != original.LastWriteTimeUtc)
                {
                    throw new InvalidDataException(
                        $"{original.Name}: 선택한 뒤 파일이 변경되었습니다. 제거 후 다시 첨부해 주세요.");
                }
            }
        }

        private async Task<ResultSaved?> CheckSubmissionAsync()
        {
            DemandSession();
            if (results == null) throw new InvalidOperationException("결과물 서버 연결이 필요합니다.");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                formCancellation.Token, session!.Cancellation);
            var pending = results.Pending("submission.submitted", taskId);
            if (pending?.Stage == "reprepare")
            {
                results.Forget(pending);
                for (int i = 0; i < pendingFiles.Count; i++)
                    pendingFiles[i] = pendingFiles[i] with { Uploaded = null };
                ShowPendingFiles();
                submissionContext = null;
                await ReadCurrentTaskAsync();
                txtContent4.AppendText("\r\n현재 업무를 다시 확인했습니다. 입력 확인 후 제출해 주세요.");
                SetStatus(txtContent4.Text);
                return null;
            }
            if (pending == null)
            {
                ValidateInputs();
                await ReadCurrentTaskAsync();
                for (int i = 0; i < pendingFiles.Count; i++)
                {
                    var file = pendingFiles[i];
                    if (file.Uploaded == null)
                    {
                        var uploaded = await results.UploadAsync(taskId, file.Path, linked.Token);
                        pendingFiles[i] = file with { Uploaded = uploaded };
                    }
                }
                ValidateInputs();
                var uploadedFiles = pendingFiles.Select(f => f.Uploaded!).ToArray();
                pending = results.Begin("submission.submitted", taskId,
                    "results/tasks/" + ResultsApi.Id(taskId) + "/submissions", taskId,
                    new SubmitResultBody(submissionContext!.RowVersion,
                        submissionContext.AssignmentRevision, txtResultDescription.Text,
                        uploadedFiles.Select(f => f.AttachmentId).ToArray()), uploadedFiles);
                pending.Draft = pendingFiles.ToArray();
            }
            ResultSaved? saved = await results.RunAsync(pending, linked.Token);
            if (saved == null)
            {
                txtContent4.Text = ResultsApi.PendingText(pending);
                SetStatus(txtContent4.Text);
            }
            else
                results.Forget(pending);
            return saved;
        }

        private async void Unavailable_Click(object? sender, EventArgs e)
        {
            if (busy || IsDisposed || Disposing) return;

            try
            {
                DemandSession();

                if (sender == btnAttachFile)
                {
                    using var dialog = new OpenFileDialog
                    {
                        Multiselect = true,
                        CheckFileExists = true,
                        Filter = "모든 파일 (*.*)|*.*",
                        Title = "첨부파일 선택 · 파일당 25MiB, 최대 10개"
                    };

                    if (dialog.ShowDialog(this) == DialogResult.OK)
                        AddAttachments(dialog.FileNames);
                }
                else if (sender == btnRemoveAttachment)
                {
                    RemoveAttachment();
                }
                else if (sender == btnSubmit)
                {
                    busy = true;
                    SetStatus("현재 업무와 입력 내용을 확인하고 제출 요청을 처리하고 있습니다.");
                    UpdateButtons();
                    ResultSaved? saved = await CheckSubmissionAsync();
                    if (saved != null && CanShowMessage() && session?.IsCurrent == true)
                    {
                        DialogResult = DialogResult.OK;
                        Close();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                if (CanShowMessage())
                    BlockAccess("요청이 취소되었습니다. 다시 로그인하거나 창을 다시 열어 주세요.");
            }
            catch (Exception ex) when (IsExpectedError(ex))
            {
                if (CanShowMessage())
                {
                    ShowError(ex);
                    MessageBox.Show(this, txtContent4.Text, "처리 확인");
                }
            }
            finally
            {
                busy = false;
                if (CanShowMessage()) UpdateButtons();
            }
        }

        private static bool IsExpectedError(Exception ex) =>
            ex is TaskQueryException
                or SubmissionAccessException
                or InvalidDataException
                or TaskPreparationException
                or ApiException
                or IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or ArgumentException;

        private void ShowError(Exception ex)
        {
            bool denied =
                session?.IsCurrent != true ||
                ex is SubmissionAccessException ||
                ex is ApiException { StatusCode: 401 or 403 } ||
                ex is TaskQueryException
                {
                    Failure: TaskQueryFailure.Forbidden
                        or TaskQueryFailure.AuthenticationExpired
                } ||
                ex is TaskPreparationException
                {
                    Failure: TaskPreparationFailure.Forbidden
                        or TaskPreparationFailure.AuthenticationExpired
                };

            string message = ex is TaskQueryException queryError
                ? queryError.Failure switch
                {
                    TaskQueryFailure.NotConfigured => "업무 조회 API 연결이 필요합니다.",
                    TaskQueryFailure.AuthenticationExpired => "다시 로그인해 주세요.",
                    TaskQueryFailure.Forbidden => "현재 업무를 조회할 권한이 없습니다.",
                    TaskQueryFailure.NotFound => "업무를 찾을 수 없습니다. 목록을 다시 조회해 주세요.",
                    _ => "업무 조회에 실패했습니다. 입력은 유지되며 다시 확인할 수 있습니다."
                }
                : ex.Message;
            if (ex is ApiException { Code: { Length: > 0 } code })
                message += "\r\n오류 코드: " + code;

            if (denied)
                BlockAccess(message);
            else
            {
                var pending = taskId.Length > 0 ? results?.Pending("submission.submitted", taskId) : null;
                txtContent4.Text = message + (pending == null ? "" : "\r\n" + ResultsApi.PendingText(pending));
                SetStatus(txtContent4.Text + (pending == null
                    ? submissionContext == null
                        ? "\r\n원인을 확인한 뒤 ‘업무 다시 확인’을 누르세요. 확인 전에는 제출할 수 없습니다."
                        : "\r\n입력 내용을 확인한 뒤 다시 제출해 주세요."
                    : ""), error: true);
            }
        }

        private void BlockAccess(string message)
        {
            accessBlocked = true;
            submissionContext = null;
            taskId = "";
            pendingFiles.Clear();
            txtTaskSummary.Clear();
            txtSubmissionVersion.Clear();
            txtResultDescription.Clear();
            gridPendingFiles.Rows.Clear();
            gridPreviousFiles.Rows.Clear();
            txtContent4.Text = message;
            SetStatus(message, error: true);
            UpdateButtons();
        }

        private void SetStatus(string message, bool error = false)
        {
            txtSubmissionStatus.Text = message.ReplaceLineEndings(Environment.NewLine);
            txtSubmissionStatus.ForeColor = error ? Color.Firebrick : Color.FromArgb(30, 42, 61);
            txtSubmissionStatus.SelectionStart = 0;
            txtSubmissionStatus.SelectionLength = 0;
            txtSubmissionStatus.ScrollToCaret();
        }

        private async void RetryTask_Click(object? sender, EventArgs e)
        {
            if (busy || accessBlocked || !CanShowMessage() || session?.IsCurrent != true
                || taskId.Length == 0 || results?.Pending("submission.submitted", taskId) != null) return;
            busy = true;
            SetStatus("업무·배정·마감·검토자 정보를 다시 확인하고 있습니다. 입력한 설명과 선택 파일은 유지합니다.");
            UpdateButtons();
            try { await ReadCurrentTaskAsync(); }
            catch (OperationCanceledException)
            { if (CanShowMessage()) BlockAccess("업무 확인이 취소되었습니다. 다시 로그인하거나 창을 다시 열어 주세요."); }
            catch (Exception ex) when (IsExpectedError(ex))
            { if (CanShowMessage()) ShowError(ex); }
            finally { busy = false; if (CanShowMessage()) UpdateButtons(); }
        }

        private bool CanShowMessage() => !IsDisposed && !Disposing;

        private void ClosePreview_Click(object? sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.Cancel) return;

            if (DialogResult != DialogResult.OK)
                DialogResult = DialogResult.Cancel;
            formCancellation.Cancel();
        }
    }
}
