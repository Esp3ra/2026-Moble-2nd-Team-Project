using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Shell;

public sealed class ConnectionSettingsForm : Form
{
    private readonly TextBox address = new() { Dock = DockStyle.Fill };
    private readonly Button test = new() { Text = "연결 확인", AutoSize = true };
    private readonly Button save = new() { Text = "저장", AutoSize = true };
    private readonly Label status = new() { AutoSize = true, MaximumSize = new Size(450, 0) };

    public ConnectionSettingsForm()
    {
        Text = "서버 연결 설정"; ClientSize = new Size(520, 280); MinimumSize = new Size(520, 320);
        StartPosition = FormStartPosition.CenterParent; Font = new Font("맑은 고딕", 9F);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(24), ColumnCount = 1, RowCount = 5 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.Controls.Add(new Label { Text = "서버 주소를 입력하세요.\n다른 PC에서는 서버 PC의 주소를 사용합니다.", AutoSize = true }, 0, 0);
        address.Text = AppServices.Api.ServerUri.ToString().TrimEnd('/');
        layout.Controls.Add(address, 0, 1);
        layout.Controls.Add(test, 0, 2); layout.Controls.Add(status, 0, 3); layout.Controls.Add(save, 0, 4);
        Controls.Add(layout);
        test.Click += async (_, _) =>
        {
            test.Enabled = save.Enabled = false;
            try
            {
                using var api = new ApiClient(address.Text);
                await api.GetAsync<HealthResult>("health");
                if (!IsDisposed) status.Text = "서버와 데이터베이스 연결을 확인했습니다.";
            }
            catch (Exception ex) when (ex is ApiException or ArgumentException)
            { if (!IsDisposed) status.Text = ex.Message; }
            finally { if (!IsDisposed) test.Enabled = save.Enabled = true; }
        };
        save.Click += (_, _) =>
        {
            try { AppServices.SaveServerUrl(address.Text); DialogResult = DialogResult.OK; Close(); }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            { status.Text = ex is ArgumentException ? ex.Message : "설정을 저장할 수 없습니다. 폴더 접근 권한을 확인해 주세요."; }
        };
    }
}
