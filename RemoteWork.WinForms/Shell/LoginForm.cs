using System.Diagnostics;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Shell;

public partial class LoginForm : Form
{
    public LoginForm()
    {
        InitializeComponent();
        if (PreviewActions.IsDesignMode(this)) return;
        lblLoginId.Text = "아이디";
        txtLoginId.MaxLength = 50;
        txtPassword.MaxLength = 128;
        btnLogin.Click += Login_Click;
        btnEmployeePreview.Text = "서버 연결 설정";
        btnAdministratorPreview.Text = "웹 로그인 열기";
        AcceptButton = btnLogin;
    }

    private async void Login_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtLoginId.Text) || string.IsNullOrEmpty(txtPassword.Text))
        {
            MessageBox.Show(this, "아이디와 비밀번호를 입력해 주세요.", "로그인");
            return;
        }
        SetBusy(true);
        try
        {
            var login = await AppServices.Api.LoginAsync(txtLoginId.Text.Trim(), txtPassword.Text);
            txtPassword.Clear();
            if (IsDisposed) { AppServices.Api.ClearIdentity(); return; }
            Hide();
            using (var workspace = new MainForm(login.User)) workspace.ShowDialog(this);
            if (!IsDisposed) { Show(); Activate(); txtPassword.Focus(); }
        }
        catch (OperationCanceledException) { /* A newer login or server setting superseded this request. */ }
        catch (ApiException ex) { if (!IsDisposed) MessageBox.Show(this, ex.Message, "로그인 확인", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        finally { if (!IsDisposed) { txtPassword.Clear(); SetBusy(false); } }
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
        try { Process.Start(new ProcessStartInfo(new Uri(AppServices.Api.ServerUri, "login").ToString()) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { MessageBox.Show(this, "브라우저에서 다음 주소를 열어 주세요.\n" + new Uri(AppServices.Api.ServerUri, "login"), "웹 로그인"); }
    }
}
