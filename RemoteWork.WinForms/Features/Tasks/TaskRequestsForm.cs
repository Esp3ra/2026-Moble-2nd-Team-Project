using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks
{
    public partial class TaskRequestsForm : Form
    {
        public TaskRequestsForm()
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData();
        }

        private void InitializeSampleData()
        {
            gridRequests.Rows.Add(new object[] { "마감 연장", "TASK-001 · 주간 업무 자료 정리", "김직원", "2026-10-20 10:00", "확인 대기" });
            gridRequests.Rows.Add(new object[] { "검토 요청", "TASK-001 · 주간 업무 자료 정리", "김직원", "2026-10-20 10:30", "확인 대기" });
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
