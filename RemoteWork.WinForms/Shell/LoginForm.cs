using System.Diagnostics;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;
using RemoteWork.ConnectedWorkspace;

namespace RemoteWork.WinForms.Shell;

public partial class LoginForm : Form
{
    private readonly ApiClient? api;
    private readonly Func<ApiClient.IdentityLogin, IdentityPendingConnection, LoginResult?>? fingerprintLogin;
    private readonly Action<LoginResult>? openWorkspace;
    private readonly Func<CancellationToken, ApiClient.IdentityLogin>? createIdentityLogin;
    private readonly CancellationTokenSource loginLifetime = new();
    private bool loginBusy, ended;
    public Task PendingLogin { get; private set; } = Task.CompletedTask;
    public LoginForm() : this(null) { }
    public LoginForm(ApiClient? api,
        Func<ApiClient.IdentityLogin, IdentityPendingConnection, LoginResult?>? fingerprintLogin = null,
        Action<LoginResult>? openWorkspace = null,
        Func<CancellationToken, ApiClient.IdentityLogin>? createIdentityLogin = null)
    {
        InitializeComponent();
        if (PreviewActions.IsDesignMode(this)) return;
        if (api is not null && openWorkspace is null)
            throw new ArgumentException("별도 접속을 전달할 때는 같은 접속의 업무 화면 연결도 필요합니다.", nameof(openWorkspace));
        this.api = api ?? AppServices.Api;
        this.fingerprintLogin = fingerprintLogin ?? OpenFingerprintLogin;
        this.openWorkspace = openWorkspace ?? (login => { using var workspace = WorkspaceStartup.CreateMainForm(login); workspace.ShowDialog(this); });
        this.createIdentityLogin = createIdentityLogin ?? (ct => this.api.BeginIdentityLogin(lifetime: ct));
        lblLoginId.Text = "아이디";
        txtLoginId.MaxLength = 50;
        txtPassword.MaxLength = 128;
        btnLogin.Click += Login_Click;
        btnEmployeePreview.Text = "서버 연결 설정";
        btnAdministratorPreview.Text = "웹 로그인 열기";
        AcceptButton = btnLogin;
    }

    private async void Login_Click(object? sender, EventArgs e)
    { PendingLogin = LoginAsync(); await PendingLogin; }

    private async Task LoginAsync()
    {
        if (loginBusy || ended) return;
        if (string.IsNullOrWhiteSpace(txtLoginId.Text) || string.IsNullOrEmpty(txtPassword.Text))
        {
            ShowLoginError("아이디와 비밀번호를 입력해 주세요.");
            return;
        }
        loginBusy = true; SetBusy(true);
        try
        {
            LoginResult? login;
            try { login = await api!.LoginAsync(txtLoginId.Text.Trim(), txtPassword.Text, loginLifetime.Token); }
            catch (ApiException error) when (error.StatusCode == 409 && error.Code == "IDENTITY_FINGERPRINT_REQUIRED")
            {
                // Only the server's explicit first-clock-in requirement opens this flow.
                using var scope = createIdentityLogin!(loginLifetime.Token);
                var pending = await scope.OpenAsync(txtLoginId.Text.Trim(), txtPassword.Text, loginLifetime.Token);
                if (!IsDisposed) txtPassword.Clear();
                if (ended || IsDisposed || !scope.IsCurrent) return;
                login = fingerprintLogin!(scope, pending);
            }
            if (!IsDisposed) txtPassword.Clear();
            if (login is null) return;
            if (ended || IsDisposed) { api!.ClearLoginIfCurrent(login); return; }
            if (!api!.IsLoginCurrent(login)) return;
            Hide();
            openWorkspace!(login);
            if (!IsDisposed) { Show(); Activate(); txtPassword.Focus(); }
        }
        catch (OperationCanceledException) { /* A newer login or server setting superseded this request. */ }
        catch (ApiException ex) { if (!ended && !IsDisposed) ShowLoginError(ex.Message); }
        catch (Exception ex) when (ex is InvalidDataException or HttpRequestException)
        { if (!ended && !IsDisposed) ShowLoginError("지문 로그인 연결을 확인해 주세요. 서버와 장치별 주소 설정이 필요합니다."); }
        finally { loginBusy = false; if (!IsDisposed) { txtPassword.Clear(); SetBusy(false); } }
    }

    protected virtual void ShowLoginError(string message)
        => MessageBox.Show(this, message, "로그인 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private LoginResult? OpenFingerprintLogin(ApiClient.IdentityLogin scope, IdentityPendingConnection pending)
    {
        var endpoints = EnrollmentEndpoints.Load(scope.ServerUri);
        using var view = new FingerprintLoginForm(scope, pending, endpoints, autoStart: true);
        view.ShowDialog(this);
        return view.CompletedLogin;
    }

    private void EndLoginView()
    {
        if (ended) return;
        ended = true; loginLifetime.Cancel();
    }

    private void SetBusy(bool busy)
    {
        btnLogin.Enabled = btnEmployeePreview.Enabled = btnAdministratorPreview.Enabled = !busy;
        txtLoginId.Enabled = txtPassword.Enabled = !busy;
        btnLogin.Text = busy ? "로그인 확인 중…" : "로그인";
    }

    private void btnEmployeePreview_Click(object? sender, EventArgs e)
    {
        using var settings = new ConnectionSettingsForm();
        settings.ShowDialog(this);
    }

    private void btnAdministratorPreview_Click(object? sender, EventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(new Uri(api!.ServerUri, "login").ToString()) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { MessageBox.Show(this, "브라우저에서 다음 주소를 열어 주세요.\n" + new Uri(api!.ServerUri, "login"), "웹 로그인"); }
    }
}
