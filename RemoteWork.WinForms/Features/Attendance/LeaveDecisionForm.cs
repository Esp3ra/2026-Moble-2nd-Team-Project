using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Attendance
{
    public partial class LeaveDecisionForm : Form
    {
        public LeaveDecisionForm()
        {
            InitializeComponent();
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
