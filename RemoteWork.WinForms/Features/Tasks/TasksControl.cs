using System.ComponentModel;
using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Features.Collaboration;

namespace RemoteWork.WinForms.Features.Tasks
{
    public partial class TasksControl : UserControl
    {
        public TasksControl() : this(PreviewRole.Employee) { }

        public TasksControl(PreviewRole role)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData(role);
        }

        private void InitializeSampleData(PreviewRole role)
        {
            bool administrator = role == PreviewRole.Administrator;
            if (administrator)
            {
                gridTasks.Rows.Add(new object[] { "TASK-001", "주간 업무 자료 정리", "김직원", "진행중", "진행중", "2026-10-15", "2026-10-22" });
                gridTasks.Rows.Add(new object[] { "TASK-002", "회의 결과 보고서 작성", "김직원", "대기", "진행전", "2026-10-16", "2026-10-23" });
                gridTasks.Rows.Add(new object[] { "TASK-003", "프로젝트 검토 자료 준비", "이직원", "제출완", "진행완료", "2026-10-14", "2026-10-21" });
                gridTasks.Rows.Add(new object[] { "TASK-004", "팀 자료 목록 정리", "박직원", "반려", "진행중", "2026-10-13", "2026-10-20" });
                gridTasks.Rows.Add(new object[] { "TASK-005", "월간 업무 결과 기록", "김직원", "승인완", "진행완료", "2026-10-12", "2026-10-19" });
            }
            else
            {
                gridTasks.Rows.Add(new object[] { "TASK-001", "주간 업무 자료 정리", "김직원", "진행중", "진행중", "2026-10-15", "2026-10-22" });
                gridTasks.Rows.Add(new object[] { "TASK-002", "회의 결과 보고서 작성", "김직원", "대기", "진행전", "2026-10-16", "2026-10-23" });
                gridTasks.Rows.Add(new object[] { "TASK-005", "월간 업무 결과 기록", "김직원", "승인완", "진행완료", "2026-10-12", "2026-10-19" });
            }
            gridTaskAttachments.Rows.Add(new object[] { "업무안내.pdf", "240 KB", "관리자" });
            btnNewTaskPreview.Visible = administrator;
            btnEditTaskPreview.Visible = administrator;
            btnCancelTask.Visible = administrator;
            btnAcceptTask.Visible = !administrator;
            btnReviewRequestPreview.Visible = !administrator;
            btnDeadlineExtensionPreview.Visible = !administrator;
            btnCollaborationRequest.Visible = true;
            btnTaskRequests.Visible = administrator;
            columnGridTasks3.Visible = administrator;
            if (!administrator)
            {
                lblTaskListTitle.Text = "내 업무";
            }
        }

        private void btnNewTaskPreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new TaskEditForm());
        }

        private void btnEditTaskPreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new TaskEditForm(true));
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

        private void btnReviewRequestPreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new ReviewRequestForm());
        }

        private void btnDeadlineExtensionPreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new DeadlineExtensionForm());
        }

        private void btnCollaborationRequest_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new CollaborationRequestForm());
        }

        private void btnTaskRequests_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new TaskRequestsForm());
        }

    }
}
