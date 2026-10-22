using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Attendance;

/// <summary>Opt-in view only. Owner supplies a captured pending/normal connection
/// via an attempt. This view owns that attempt, but not its connection or device.</summary>
public partial class FingerprintIdentityForm : Form
{
    private readonly FingerprintIdentityAttempt? attempt;
    private readonly bool ownsAttempt, closeWhenVerified, autoStart;
    private bool busy, checkingCancel, closing, closeReviewed, released;
    private long displayedSequence = -1;
    private Task cancellationOperation = Task.CompletedTask;
    public IdentityStatus? IdentityResult => attempt?.Result;
    public Task PendingOperation { get; private set; } = Task.CompletedTask;

    public FingerprintIdentityForm() : this(null) { }
    public FingerprintIdentityForm(FingerprintIdentityAttempt? attempt, bool ownsAttempt = true,
        bool closeWhenVerified = false, bool autoStart = false)
    {
        InitializeComponent();
        this.ownsAttempt = ownsAttempt; this.closeWhenVerified = closeWhenVerified; this.autoStart = autoStart;
        if (PreviewActions.IsDesignMode(this)) return;
        this.attempt = attempt;
        if (attempt is null)
        {
            lblStatus.Text = "지문 연결 준비 전";
            lblDetail.Text = "담당 로그인 화면에서 지문 본인 확인 연결을 전달해야 시작할 수 있습니다.";
            btnStart.Enabled = btnCancel.Enabled = false;
        }
        else
        {
            attempt.Changed += AttemptChanged;
            ShowUpdate(attempt.Latest);
        }
    }

    private void AttemptChanged(object? sender, IdentityAttemptUpdate update)
    {
        if (released || IsDisposed || Disposing || !IsHandleCreated) return;
        try { BeginInvoke((Action)(() => { if (!released && !IsDisposed && !Disposing) ShowUpdate(update); })); }
        catch (InvalidOperationException) { }
    }
    protected override void OnShown(EventArgs e)
    {
        if (attempt is not null) ShowUpdate(attempt.Latest);
        if (autoStart && attempt is not null) PendingOperation = StartAsync();
        base.OnShown(e);
    }

    private void ShowUpdate(IdentityAttemptUpdate update)
    {
        if (attempt is not null && !attempt.IsCurrent)
        {
            attempt.RefreshContext();
            update = attempt.Latest;
        }
        if (update.Sequence < displayedSequence) return;
        displayedSequence = update.Sequence;
        lblStatus.Text = update.Title;
        lblDetail.Text = update.Detail;
        btnStart.Text = update.Phase == IdentityAttemptPhase.Ready ? "지문 확인 시작"
            : busy ? "지문 확인 중" : update.CanRetry ? "같은 결과 확인" : "본인 확인 종료";
        btnStart.Enabled = !busy && !checkingCancel && update.CanRetry;
        btnCancel.Text = update.Phase == IdentityAttemptPhase.ReviewRequired ? "취소 확인" : "요청 취소";
        btnCancel.Enabled = !checkingCancel && update.CanCancel;
        if (closeWhenVerified && !busy && !checkingCancel && Visible && update.Phase == IdentityAttemptPhase.Verified)
        { DialogResult = DialogResult.OK; Close(); }
    }
    private async void Start_Click(object? sender, EventArgs e)
    { PendingOperation = StartAsync(); await PendingOperation; }
    private async Task StartAsync()
    {
        if (attempt is null || busy || checkingCancel) return;
        busy = true;
        ShowUpdate(attempt.Latest);
        try { await attempt.RunAsync(); }
        finally { busy = false; if (!released) ShowUpdate(attempt.Latest); }
    }
    private async void Cancel_Click(object? sender, EventArgs e)
    { PendingOperation = CancelAsync(); await PendingOperation; }
    private Task CancelAsync()
    {
        if (attempt is null) return Task.CompletedTask;
        if (checkingCancel) return cancellationOperation;
        checkingCancel = true;
        ShowUpdate(attempt.Latest);
        return cancellationOperation = CancelCoreAsync();
    }
    private async Task CancelCoreAsync()
    {
        try { await attempt!.CancelAsync(); }
        finally { checkingCancel = false; if (!released && attempt is not null) ShowUpdate(attempt.Latest); }
    }
    private void ContextTimer_Tick(object? sender, EventArgs e)
    { if (!released && attempt is not null) { attempt.RefreshContext(); ShowUpdate(attempt.Latest); } }
    private void Close_Click(object? sender, EventArgs e) => Close();
    private async void IdentityForm_Closing(object? sender, FormClosingEventArgs e)
    {
        if (attempt is null || closeReviewed || attempt.Latest.Phase is IdentityAttemptPhase.Ready
            or IdentityAttemptPhase.Verified or IdentityAttemptPhase.Rejected or IdentityAttemptPhase.Cancelled
            or IdentityAttemptPhase.Expired or IdentityAttemptPhase.ContextEnded) return;
        e.Cancel = true;
        if (closing) return;
        closing = true;
        btnClose.Enabled = false;
        try
        {
            PendingOperation = CancelAsync();
            await PendingOperation;
            if (released) return;
            closeReviewed = true;
            if (attempt.Latest.Phase is IdentityAttemptPhase.Cancelled or IdentityAttemptPhase.Verified
                or IdentityAttemptPhase.Rejected or IdentityAttemptPhase.Expired or IdentityAttemptPhase.ContextEnded) Close();
            else lblDetail.Text += " 창을 닫아도 서버 취소 완료로 처리되지 않습니다. 확인 후 닫기를 눌러주세요.";
        }
        finally { closing = false; if (!released) btnClose.Enabled = true; }
    }
    private void ReleaseAttempt()
    {
        if (released) return;
        released = true;
        if (attempt is null) return;
        attempt.Changed -= AttemptChanged;
        if (ownsAttempt) attempt.Dispose();
    }
}
