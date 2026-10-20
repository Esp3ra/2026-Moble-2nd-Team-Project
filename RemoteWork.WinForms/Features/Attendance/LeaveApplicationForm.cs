using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Attendance
{
    public partial class LeaveApplicationForm : Form
    {
        public LeaveApplicationForm()
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData();
        }

        private void InitializeSampleData()
        {
            cmbOptions.SelectedIndex = 0;
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

        private void ClosePreview_Click(object? sender, EventArgs e)
        {
            Close();
        }

    }
}
