using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Attendance;

/// <summary>Designer-editable noon/clock-out view. Borrows the operation so closing
/// cannot lose its original result/UUID. A login/workday owner must retain it.</summary>
public partial class FingerprintWorkdayForm : Form
{
    private readonly FingerprintWorkdayOperation? operation;
    private readonly Func<FingerprintIdentityAttempt, Task> verify;
    private readonly bool autoStart;
    private readonly CancellationTokenSource lifetime = new();
    private bool busy, closing, reviewedClose, allowClose, released;
    public WorkdayIdentityReceipt? CompletedReceipt { get; private set; }
    public Task PendingOperation { get; private set; } = Task.CompletedTask;

    public FingerprintWorkdayForm() : this(null) { }
    public FingerprintWorkdayForm(FingerprintWorkdayOperation? operation,
        Func<FingerprintIdentityAttempt, Task>? verify = null, bool autoStart = false)
    {
        InitializeComponent();
        this.operation = operation;
        this.autoStart = autoStart;
        this.verify = verify ?? VerifyInDialog;
        if (PreviewActions.IsDesignMode(this)) return;
        if (operation is null)
        { btnConfirm.Enabled = btnCancel.Enabled = false; lblStatus.Text = "현재 근무 연결 전"; return; }
        bool noon = operation.Purpose == "noon_check";
        Text = lblTitle.Text = noon ? "오후 업무 전 지문 확인" : "지문 확인 후 퇴근";
        btnConfirm.Text = noon ? "오후 업무 전 지문 확인" : "지문 확인 후 퇴근";
        lblDetail.Text = noon ? "한국 시간 11:00 이상 12:00 미만에 OLED 안내에 따라 지문을 찍어주세요. 서버 저장까지 확인해야 완료됩니다."
            : "지문 일치 후 서버의 퇴근 저장을 확인합니다. 창을 닫는 것만으로 퇴근 처리되지 않습니다.";
        ShowRetainedState();
    }
    private Task VerifyInDialog(FingerprintIdentityAttempt attempt)
    {
        using var form = new FingerprintIdentityForm(attempt, ownsAttempt: false, closeWhenVerified: true, autoStart: true);
        form.ShowDialog(this);
        return Task.CompletedTask;
    }
    private bool Live => !released && !IsDisposed && !Disposing && operation?.IsCurrent == true;
    protected override void OnShown(EventArgs e)
    {
        if (autoStart && Live) PendingOperation = ConfirmAsync();
        base.OnShown(e);
    }
    private void ShowRetainedState()
    {
        if (operation?.HasFinalizeAttempt == true && operation.Completed is null)
        { lblStatus.Text = "저장 결과 확인 필요"; btnConfirm.Text = "같은 저장 결과 확인"; btnCancel.Enabled = false; }
    }
    private async void Confirm_Click(object? sender, EventArgs e)
    { PendingOperation = ConfirmAsync(); await PendingOperation; }
    private async Task ConfirmAsync()
    {
        if (!Live || busy || closing || CompletedReceipt is not null) return;
        busy = true; btnConfirm.Enabled = btnCancel.Enabled = false;
        try
        {
            if (!operation!.HasFinalizeAttempt && operation.Completed is null)
            {
                await verify(operation.Attempt);
                if (!Live) return;
                if (operation.Attempt.Result is not { State: "verified" })
                { lblStatus.Text = operation.Attempt.Latest.Title; lblDetail.Text = operation.Attempt.Latest.Detail; return; }
            }
            lblStatus.Text = operation.Purpose == "noon_check" ? "오후 업무 전 지문 확인 저장 중" : "퇴근 저장 확인 중";
            lblDetail.Text = "응답이 끊겨도 원래 작업 결과를 확인합니다. 지문과 저장을 새로 요청하지 않습니다.";
            btnClose.Enabled = false;
            var receipt = await operation.CompleteAsync(lifetime.Token);
            if (!Live) return;
            CompletedReceipt = receipt;
            lblStatus.Text = operation.Purpose == "noon_check" ? "오후 업무 전 지문 확인 완료" : "퇴근 저장 완료";
            lblDetail.Text = operation.Purpose == "noon_check" ? "서버에서 현재 근무의 오후 업무 전 지문 확인 저장을 확인했습니다."
                : "서버에 퇴근 기록이 저장되었습니다.";
            btnClose.Text = "확인";
        }
        catch (Exception error) when (error is RemoteWork.WinForms.Infrastructure.ApiException or HttpRequestException
            or InvalidDataException or InvalidOperationException or OperationCanceledException)
        {
            if (!Live) return;
            lblStatus.Text = operation!.HasFinalizeAttempt ? "저장 결과 확인 필요" : "지문 확인 필요";
            lblDetail.Text = operation.HasFinalizeAttempt
                ? "서버 저장 결과가 아직 확인되지 않았습니다. 같은 저장 결과 확인을 눌러주세요. 창 닫기는 저장 취소가 아닙니다."
                : operation.Attempt.Latest.Detail;
        }
        finally
        {
            busy = false;
            if (!released && !IsDisposed)
            {
                btnClose.Enabled = true;
                btnConfirm.Enabled = Live && CompletedReceipt is null && (operation!.HasFinalizeAttempt || operation.Attempt.Latest.CanRetry);
                btnCancel.Enabled = Live && CompletedReceipt is null && !operation!.HasFinalizeAttempt;
                ShowRetainedState();
            }
        }
    }
    private async void Cancel_Click(object? sender, EventArgs e)
    {
        if (!Live || busy || closing || operation!.HasFinalizeAttempt) return;
        busy = true; btnCancel.Enabled = btnConfirm.Enabled = false;
        try
        {
            await operation.CancelAsync(lifetime.Token);
            if (Live) { lblStatus.Text = operation.Attempt.Latest.Title; lblDetail.Text = operation.Attempt.Latest.Detail; }
        }
        catch (Exception error) when (error is RemoteWork.WinForms.Infrastructure.ApiException or HttpRequestException
            or InvalidDataException or InvalidOperationException or OperationCanceledException)
        { if (Live) { lblStatus.Text = "취소 확인 필요"; lblDetail.Text = "서버 취소를 확인하지 못했습니다. 기존 요청을 유지합니다."; } }
        finally
        {
            busy = false;
            if (Live)
            {
                btnClose.Enabled = true;
                btnCancel.Enabled = operation!.Attempt.Latest.CanCancel;
                btnConfirm.Enabled = operation.Attempt.Latest.CanRetry;
            }
        }
    }
    private void Close_Click(object? sender, EventArgs e) => Close();
    private void Workday_Closing(object? sender, FormClosingEventArgs e)
    {
        if (allowClose || !Live || CompletedReceipt is not null) return;
        if (busy) { e.Cancel = true; return; }
        if (operation!.HasFinalizeAttempt && !reviewedClose)
        {
            e.Cancel = true; reviewedClose = true;
            lblStatus.Text = "저장 결과를 먼저 확인해 주세요";
            lblDetail.Text = "닫아도 저장 요청은 취소되지 않습니다. 같은 결과 확인을 권장합니다. 닫기를 다시 누르면 이 창만 닫힙니다.";
            return;
        }
        closing = true; allowClose = true;
        // Owner keeps the same operation across views. No logout, rollback or new scan.
    }
    private void ReleaseView()
    { if (released) return; released = true; lifetime.Cancel(); }
}
