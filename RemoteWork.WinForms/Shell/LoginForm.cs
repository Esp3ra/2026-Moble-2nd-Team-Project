using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Shell
{
    public partial class LoginForm : Form
    {
        public LoginForm()
        {
            InitializeComponent();
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

        private void btnEmployeePreview_Click(object? sender, EventArgs e)
        {
            ShowWorkspace(PreviewRole.Employee);
        }

        private void btnAdministratorPreview_Click(object? sender, EventArgs e)
        {
            ShowWorkspace(PreviewRole.Administrator);
        }

        private void ShowWorkspace(PreviewRole role)
        {
            Hide();
            using (var workspace = new MainForm(role)) workspace.ShowDialog();
            Show();
            Activate();
        }

    }
}
