using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Shell;

public partial class LoginForm : Form
{
    public LoginForm() => InitializeComponent();

    private void ShowWorkspace(PreviewRole role)
    {
        Hide();
        using (var workspace = new MainForm(role)) workspace.ShowDialog();
        Show();
        Activate();
    }
}
