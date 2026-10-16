using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Features.Collaboration;

namespace RemoteWork.WinForms.Features.Tasks
{
    public partial class TasksControl : UserControl
    {
        private readonly PreviewRole previewRole;

        public TasksControl() : this(PreviewRole.Employee) { }

        public TasksControl(PreviewRole role)
        {
            previewRole = role;
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData();
        }

        private void InitializeSampleData()
        {
            bool administrator = previewRole == PreviewRole.Administrator;
            lblTaskScope.Visible = administrator;
            cmbTaskScope.Visible = administrator;
            cmbTaskScope.SelectedIndex = 0;
            cmbTaskStatusFilter.SelectedIndex = 0;
            gridTaskAttachments.Rows.Add(new object[] { "업무안내.pdf", "240 KB", "관리자" });
            ShowTaskPreview();
        }

        private void ShowTaskPreview()
        {
            // 화면 구분에 맞는 고정 예시만 표시합니다. 업무를 수락하거나 변경하지 않습니다.
            bool administrator = previewRole == PreviewRole.Administrator;
            bool managementView = administrator && cmbTaskScope.SelectedIndex == 1;
            gridTasks.Rows.Clear();
            if (managementView)
            {
                gridTasks.Rows.Add(new object[] { "TASK-001", "주간 업무 자료 정리", "김직원", "진행전", "2026-10-15", "2026-10-22" });
                gridTasks.Rows.Add(new object[] { "TASK-002", "회의 결과 보고서 작성", "이직원", "진행중", "2026-10-16", "2026-10-23" });
                gridTasks.Rows.Add(new object[] { "TASK-003", "프로젝트 검토 자료 준비", "박직원", "제출완료", "2026-10-14", "2026-10-21" });
                gridTasks.Rows.Add(new object[] { "TASK-004", "팀 자료 목록 정리", "김직원", "승인완료", "2026-10-13", "2026-10-20" });
                gridTasks.Rows.Add(new object[] { "TASK-005", "월간 업무 결과 기록", "이직원", "반려완료", "2026-10-12", "2026-10-19" });
            }
            else
            {
                string assignee = administrator ? "이관리자" : "김직원";
                string prefix = administrator ? "TASK-A" : "TASK-";
                gridTasks.Rows.Add(new object[] { prefix + "001", administrator ? "부서 운영 계획 작성" : "주간 업무 자료 정리", assignee, "진행전", "2026-10-15", "2026-10-22" });
                gridTasks.Rows.Add(new object[] { prefix + "002", administrator ? "팀 회의 안건 정리" : "회의 결과 보고서 작성", assignee, "진행중", "2026-10-16", "2026-10-23" });
                gridTasks.Rows.Add(new object[] { prefix + "003", administrator ? "월간 운영 현황 보고" : "프로젝트 검토 자료 준비", assignee, "제출완료", "2026-10-14", "2026-10-21" });
                gridTasks.Rows.Add(new object[] { prefix + "004", administrator ? "분기 운영 지표 정리" : "팀 자료 목록 정리", assignee, "승인완료", "2026-10-13", "2026-10-20" });
                gridTasks.Rows.Add(new object[] { prefix + "005", administrator ? "차기 운영안 보완" : "월간 업무 결과 기록", assignee, "반려완료", "2026-10-12", "2026-10-19" });
            }
            btnNewTaskPreview.Visible = managementView;
            btnEditTaskPreview.Visible = managementView;
            btnCancelTask.Visible = managementView;
            btnAcceptTask.Visible = !managementView;
            btnReviewRequestPreview.Visible = !managementView;
            btnDeadlineExtensionPreview.Visible = !managementView;
            btnCollaborationRequest.Visible = true;
            btnTaskRequests.Visible = managementView;
            columnGridTasks3.Visible = managementView;
            lblTaskListTitle.Text = managementView ? "관리 업무" : "내 업무";

            bool ownAdministrator = administrator && !managementView;
            string taskId = ownAdministrator ? "TASK-A001" : "TASK-001";
            string title = ownAdministrator ? "부서 운영 계획 작성" : "주간 업무 자료 정리";
            string owner = ownAdministrator ? "이관리자" : "김직원";
            txtContent.Text = $"{taskId}\r\n\r\n업무 제목   {title}\r\n담당자      {owner}\r\n업무 상태   진행전\r\n\r\n시작일      2026-10-15\r\n현재 마감일 2026-10-22\r\n변경 마감일 —\r\n\r\n상세 내용\r\n{(ownAdministrator ? "부서 운영 계획과 검토 자료를 정리합니다." : "주간 업무 자료를 모아 항목별로 정리합니다.")}";
        }

        private void cmbTaskScope_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (!PreviewActions.IsDesignMode(this)) ShowTaskPreview();
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
            PreviewActions.Open(this, new ReviewRequestForm(previewRole));
        }

        private void btnDeadlineExtensionPreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new DeadlineExtensionForm(previewRole));
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
