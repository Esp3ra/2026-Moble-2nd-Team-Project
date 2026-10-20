using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Results;

public partial class ReviewDecisionForm : Form
{
    private readonly bool approve;
    private ResultsApi? results;
    private TaskSummary? task;
    private ResultSubmission? submission;
    private DecisionContext? context;
    private readonly CancellationTokenSource closed = new();
    private CancellationTokenRegistration registration;
    private bool busy;
    private bool cleaned;
    private bool blocked;
    private string ActionName => approve ? "submission.approved" : "submission.returned";

    public ReviewDecisionForm() : this(true) { }
    public ReviewDecisionForm(bool approve)
    {
        this.approve = approve;
        InitializeComponent();
        if (PreviewActions.IsDesignMode(this)) return;
        InitializeSampleData(approve);
        Disposed += (_, _) =>
        {
            if (cleaned) return;
            cleaned = true;
            closed.Cancel();
            registration.Dispose();
            closed.Dispose();
        };
    }

    internal ReviewDecisionForm(bool approve, ResultsApi api,
        TaskSummary task, ResultSubmission submission) : this(approve)
    {
        results = api;
        this.task = task;
        this.submission = submission;
    }

    private void InitializeSampleData(bool approve)
    {
        Text = lblPageTitle.Text = approve ? "결과 승인" : "결과 반려";
        lblText2.Text = approve ? "승인 내용" : "반려 내용";
        lblDecisionReason.Text = approve ? "승인 의견" : "반려 사유";
        lblSubmissionVersion.Text = "담당자 / 제출 회차";
        btnDecision.Text = approve ? "승인" : "반려";
        txtTaskSummary.Clear();
        txtSubmissionVersion.Clear();
        txtReviewer.Clear();
        txtDecisionReason.Clear();
        txtContent5.Clear();
        btnDecision.Enabled = false;
        btnDecision.DialogResult = DialogResult.None;
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (PreviewActions.IsDesignMode(this)) return;
        try
        {
            if (results == null || task == null || submission == null)
                throw new InvalidOperationException("리뷰컨트롤에서 실제 제출물을 선택해 주세요.");
            results.Session.ThrowIfChanged();
            registration = results.Session.Cancellation.Register(() =>
            {
                if (!IsHandleCreated || IsDisposed || Disposing) return;
                try { BeginInvoke(new Action(Block)); }
                catch (InvalidOperationException) { }
            });
            txtTaskSummary.Text = task.TaskId + " · " + task.Title;
            txtSubmissionVersion.Text = submission.SubmittedByName + " / " + submission.RoundNo + "차";
            txtReviewer.Text = results.Session.User.DisplayName + " (ID " + results.Session.User.Id + ")";
            txtContent5.Text = submission.Description + "\r\n\r\n첨부\r\n" +
                string.Join("\r\n", submission.Attachments.Select(f => f.Name));
            var pending = results.Pending(ActionName, submission.SubmissionId);
            if (pending == null)
                context = await results.DecisionContextAsync(submission.SubmissionId, closed.Token);
            else
            {
                txtDecisionReason.Text = pending.Body.GetProperty("reason").GetString();
                txtContent5.AppendText("\r\n\r\n" + ResultsApi.PendingText(pending));
            }
            results.Session.ThrowIfChanged();
        }
        catch (Exception error) when (Expected(error))
        {
            ShowError(error);
        }
        finally { if (!IsDisposed && !Disposing) UpdateButtons(); }
    }

    private void UpdateButtons()
    {
        var pending = results?.Session.IsCurrent == true && submission != null
            ? results.Pending(ActionName, submission.SubmissionId) : null;
        bool ready = !busy && !blocked && results?.Session.IsCurrent == true &&
            (context != null || pending != null);
        btnDecision.Enabled = ready && pending?.Stage != "expired";
        txtDecisionReason.ReadOnly = !ready || pending != null;
        btnDecision.Text = pending?.Stage switch
        {
            "check" or "completed" => "처리 결과 확인",
            "retry" => "같은 요청 재전송",
            "reprepare" => "제출물 다시 확인",
            "expired" => "요청 만료",
            _ => approve ? "승인" : "반려"
        };
    }

    private async void Unavailable_Click(object? sender, EventArgs e)
    {
        if (busy || blocked || results == null || submission == null || task == null) return;
        busy = true;
        UpdateButtons();
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(
                closed.Token, results.Session.Cancellation);
            var pending = results.Pending(ActionName, submission.SubmissionId);
            if (pending?.Stage == "reprepare")
            {
                results.Forget(pending);
                context = await results.DecisionContextAsync(submission.SubmissionId, linked.Token);
                txtContent5.AppendText("\r\n현재 제출물을 다시 확인했습니다. 의견 확인 후 처리해 주세요.");
                return;
            }
            if (pending == null)
            {
                if (!approve && string.IsNullOrWhiteSpace(txtDecisionReason.Text))
                    throw new InvalidDataException("반려 사유를 입력해 주세요.");
                context = await results.DecisionContextAsync(submission.SubmissionId, linked.Token);
                if (context.TaskId != task.TaskId || context.SubmissionId != submission.SubmissionId ||
                    context.TaskStatus != "submitted")
                    throw new InvalidDataException("검토 대상이 변경되었습니다.");
                pending = results.Begin(ActionName, submission.SubmissionId,
                    "results/submissions/" + ResultsApi.Id(submission.SubmissionId) + "/decisions",
                    task.TaskId, new DecideResultBody(context.RowVersion, context.AssignmentRevision,
                        approve ? "approved" : "returned", txtDecisionReason.Text));
            }
            var saved = await results.RunAsync(pending, linked.Token);
            if (saved == null)
                txtContent5.AppendText("\r\n\r\n" + ResultsApi.PendingText(pending));
            else if (!IsDisposed && !Disposing && results.Session.IsCurrent)
            {
                results.Forget(pending);
                DialogResult = DialogResult.OK;
                Close();
            }
        }
        catch (Exception error) when (Expected(error)) { ShowError(error); }
        finally
        {
            busy = false;
            if (!IsDisposed && !Disposing) UpdateButtons();
        }
    }

    private static bool Expected(Exception error) =>
        error is ApiException or IOException or InvalidDataException or InvalidOperationException or ArgumentException
            or OperationCanceledException;

    private void ShowError(Exception error)
    {
        if (IsDisposed || Disposing) return;
        if (results?.Session.IsCurrent != true || error is ApiException { StatusCode: 401 or 403 })
            Block();
        var pending = results?.Session.IsCurrent == true && submission != null
            ? results.Pending(ActionName, submission.SubmissionId) : null;
        txtContent5.Text = error.Message + (pending == null ? "" : "\r\n" + ResultsApi.PendingText(pending));
    }

    private void Block()
    {
        if (IsDisposed || Disposing) return;
        blocked = true;
        context = null;
        txtTaskSummary.Clear();
        txtSubmissionVersion.Clear();
        txtReviewer.Clear();
        txtDecisionReason.Clear();
        txtContent5.Text = "접속 또는 현재 검토 권한을 다시 확인해 주세요.";
        UpdateButtons();
    }

    private void ClosePreview_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel) return;
        if (DialogResult != DialogResult.OK) DialogResult = DialogResult.Cancel;
        closed.Cancel();
    }
}
