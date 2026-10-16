using System.ComponentModel;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks
{
    public partial class DeadlineExtensionForm : Form
    {
        public DeadlineExtensionForm() : this(PreviewRole.Employee) { }

        public DeadlineExtensionForm(PreviewRole role)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData(role);
        }

        private void InitializeSampleData(PreviewRole role)
        {
            if (role == PreviewRole.Administrator)
                txtTaskSummary.Text = "TASK-A001 · 부서 운영 계획 작성";
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
