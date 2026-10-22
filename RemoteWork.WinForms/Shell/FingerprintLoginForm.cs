using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Shell;

/// <summary>Login-only orchestration. Normal token import lives in ApiClient's
/// captured identity scope, never in a button or a device response.</summary>
public partial class FingerprintLoginForm : Form
{
    private readonly ApiClient.IdentityLogin? login;
    private readonly IdentityPendingConnection? pending;
    private readonly EnrollmentEndpoints? endpoints;
    private readonly Func<Uri, IIdentityRelayDevice> deviceFactory;
    private readonly Func<FingerprintIdentityAttempt, Task<IdentityStatus?>> verify;
    private readonly bool autoStart;
    private FingerprintIdentityAttempt? attempt;
    private IIdentityRelayDevice? device;
    private Guid? verifiedChallenge;
    private bool busy, closing, closeReviewed, allowClose, released;
    private readonly CancellationTokenSource lifetime = new();
    public LoginResult? CompletedLogin { get; private set; }
    public Task PendingOperation { get; private set; } = Task.CompletedTask;
    private sealed record Choice(string Id, string Title) { public override string ToString() => Title; }

    public FingerprintLoginForm() : this(null, null, null) { }
    public FingerprintLoginForm(ApiClient.IdentityLogin? login, IdentityPendingConnection? pending,
        EnrollmentEndpoints? endpoints, Func<Uri, IIdentityRelayDevice>? deviceFactory = null,
        Func<FingerprintIdentityAttempt, Task<IdentityStatus?>>? verify = null, bool autoStart = false)
    {
        InitializeComponent();
        this.login = login; this.pending = pending; this.endpoints = endpoints;
        this.deviceFactory = deviceFactory ?? (address => new HttpFingerprintIdentityDevice(address.ToString()));
        this.verify = verify ?? VerifyInDialog;
        this.autoStart = autoStart;
        if (PreviewActions.IsDesignMode(this)) return;
        if (login is null || pending is null || endpoints is null)
        { btnStart.Enabled = false; lblStatus.Text = "로그인 연결 준비 전"; return; }
        int index = 0;
        foreach (string id in endpoints.DeviceIds) cmbDevice.Items.Add(new Choice(id, $"지문 장치 {++index}"));
        if (cmbDevice.Items.Count > 0) cmbDevice.SelectedIndex = 0;
        else { lblStatus.Text = "지문 장치 설정 필요"; btnStart.Enabled = false; }
    }
    private Task<IdentityStatus?> VerifyInDialog(FingerprintIdentityAttempt current)
    {
        using var view = new FingerprintIdentityForm(current, ownsAttempt: false, closeWhenVerified: true, autoStart: true);
        view.ShowDialog(this);
        return Task.FromResult(view.IdentityResult);
    }
    private bool Live => !released && !IsDisposed && !Disposing && login?.IsCurrent == true;
    protected override void OnShown(EventArgs e)
    {
        if (autoStart && cmbDevice.Items.Count == 1) PendingOperation = StartAsync();
        base.OnShown(e);
    }
    private async void Start_Click(object? sender, EventArgs e)
    { PendingOperation = StartAsync(); await PendingOperation; }
    private async Task StartAsync()
    {
        if (!Live || busy || CompletedLogin is not null || cmbDevice.SelectedItem is not Choice choice) return;
        busy = true; btnStart.Enabled = false; cmbDevice.Enabled = false;
        try
        {
            if (verifiedChallenge is null)
            {
                if (attempt is null)
                {
                    device = deviceFactory(endpoints!.Resolve(choice.Id));
                    attempt = new(new FingerprintIdentityConnection(pending!), device, Guid.Parse(choice.Id));
                }
                lblStatus.Text = "지문 본인 확인 중";
                lblDetail.Text = "OLED 안내에 따라 지문을 찍어주세요. 출근 저장 전까지 업무 화면은 열리지 않습니다.";
                var identity = await verify(attempt);
                if (!Live) return;
                if (identity is not { State: "verified", WorkflowState: "pending", IdentityReceipt.Purpose: "clock_in" })
                {
                    lblStatus.Text = attempt.Latest.Title; lblDetail.Text = attempt.Latest.Detail;
                    btnStart.Text = "같은 지문 작업 확인";
                    return;
                }
                verifiedChallenge = identity.ChallengeId;
            }
            lblStatus.Text = "출근 저장·로그인 확인 중";
            lblDetail.Text = "서버의 출근 저장과 정상 로그인 접속을 확인하고 있습니다.";
            var result = await login!.CompleteAsync(verifiedChallenge.Value, lifetime.Token);
            if (!Live) return;
            CompletedLogin = result;
            lblStatus.Text = "출근·로그인 완료";
            DialogResult = DialogResult.OK; Close();
        }
        catch (OperationCanceledException) when (!Live || lifetime.IsCancellationRequested) { }
        catch (Exception error) when (error is ApiException or HttpRequestException or InvalidDataException
            or OperationCanceledException or InvalidOperationException)
        {
            if (!Live) return;
            lblStatus.Text = login!.HasFinalizeAttempt ? "출근 결과 확인 필요" : "지문 연결 확인 필요";
            lblDetail.Text = login.HasFinalizeAttempt
                ? "출근 저장 결과가 아직 확인되지 않았습니다. 같은 출근 결과 확인을 눌러주세요. 지문과 출근을 새로 요청하지 않습니다."
                : "원래 지문 작업을 유지했습니다. 장치 연결과 서버 상태를 확인하고 같은 작업을 확인해 주세요.";
            btnStart.Text = login.HasFinalizeAttempt ? "같은 출근 결과 확인" : "같은 지문 작업 확인";
        }
        finally
        {
            busy = false;
            if (Live && CompletedLogin is null)
                btnStart.Enabled = login!.HasFinalizeAttempt || attempt is null || attempt.Latest.CanRetry;
        }
    }
    private void Close_Click(object? sender, EventArgs e) => Close();
    private async void LoginForm_Closing(object? sender, FormClosingEventArgs e)
    {
        if (CompletedLogin is not null || login is null || !Live || allowClose) return;
        e.Cancel = true;
        if (closing) return;
        closing = true; btnClose.Enabled = btnStart.Enabled = false;
        try
        {
            if (login.HasFinalizeAttempt && !closeReviewed)
            {
                lblStatus.Text = "출근 결과부터 확인해 주세요";
                lblDetail.Text = "서버에 출근 저장 요청을 보냈습니다. 창을 닫아도 출근 기록은 취소되지 않습니다. 결과 확인을 권장하며, 종료하려면 닫기를 한 번 더 눌러주세요.";
                closeReviewed = true;
                return;
            }
            if (attempt is not null) await attempt.CancelAsync();
            await login.RevokeAsync(lifetime.Token);
            allowClose = true; Close();
        }
        catch (Exception error) when (error is ApiException or HttpRequestException or InvalidDataException
            or OperationCanceledException or InvalidOperationException)
        {
            if (!Live) return;
            lblStatus.Text = "접속 종료 확인 필요";
            lblDetail.Text = "서버 종료 응답을 확인하지 못했습니다. 닫기를 다시 누르면 로컬 연결을 종료하며, 서버 취소 완료로 표시하지 않습니다.";
            closeReviewed = true;
            allowClose = true;
        }
        finally
        {
            closing = false;
            if (Live) { btnClose.Enabled = true; btnStart.Enabled = login.HasFinalizeAttempt; }
        }
    }
    private void ReleaseLoginView()
    {
        if (released) return;
        released = true; lifetime.Cancel(); attempt?.Dispose(); (device as IDisposable)?.Dispose();
        // The LoginForm owns the pending scope and its retained finalize recovery lifetime.
    }
}
