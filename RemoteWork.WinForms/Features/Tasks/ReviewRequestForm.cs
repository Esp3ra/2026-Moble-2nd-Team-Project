using System.ComponentModel;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks
{
    public partial class ReviewRequestForm : Form
    {
        public ReviewRequestForm() : this(PreviewRole.Employee) { }

        public ReviewRequestForm(PreviewRole role)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData(role);
        }

        private void InitializeSampleData(PreviewRole role)
        {
            if (role == PreviewRole.Administrator)
                txtTaskSummary.Text = "TASK-A003 · 월간 운영 현황 보고";
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
