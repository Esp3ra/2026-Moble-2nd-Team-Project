using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks
{
    public partial class TaskAcceptanceForm : Form
    {
        public TaskAcceptanceForm() : this(null, true) { }

        public TaskAcceptanceForm(TaskDetail detail)
            : this(detail ?? throw new ArgumentNullException(nameof(detail)), true) { }

        private TaskAcceptanceForm(TaskDetail? detail, bool preparing)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this) || detail is null) return;
            TaskSummary task = detail.Summary;
            txtTaskId.Text = task.TaskId;
            txtTaskTitle.Text = task.Title;
            txtAssignee.Text = task.AssigneeName;
            txtTaskStatus.Text = task.Status switch
            {
                TaskDisplayStatus.NotStarted => "진행전",
                TaskDisplayStatus.InProgress => "진행중",
                TaskDisplayStatus.Submitted => "제출완료",
                TaskDisplayStatus.Approved => "승인완료",
                TaskDisplayStatus.Returned => "반려완료",
                _ => "상태 확인 필요"
            };
            txtStartDate.Text = task.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            txtDeadline.Text = task.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            lblPreparationStatus.Text = "수락 대상 정보 확인 · 수락 처리 규약 확인 필요 · 상태 미변경\r\n조회한 정보만 표시합니다. 서버 전송·배정 수락·상태 변경은 하지 않습니다.";
        }

        private void Close_Click(object? sender, EventArgs e)
        {
            Close();
        }
    }
}
