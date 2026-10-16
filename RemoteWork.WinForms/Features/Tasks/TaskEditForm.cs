using System.ComponentModel;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks
{
    public partial class TaskEditForm : Form
    {
        public TaskEditForm() : this(false) { }

        public TaskEditForm(bool editing)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData(editing);
        }

        private void InitializeSampleData(bool editing)
        {
            cmbAssignee.SelectedIndex = 0;
            if (editing)
            {
                Text = "업무 수정";
                lblPageTitle.Text = "업무 수정";
                txtTaskTitle.Text = "주간 업무 자료 정리";
                txtTaskDescription.Text = "주간 업무 자료를 모아 항목별로 정리합니다.";
                cmbAssignee.Text = "김직원";
                txtAttachments.Text = "업무안내.pdf · 240 KB";
                btnConfirm.Text = "수정 확인";
                cmbAssignee.SelectedIndex = 1;
            }
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
