using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Attendance
{
    public partial class AttendanceControl : UserControl
    {
        public AttendanceControl() : this(PreviewRole.Employee) { }

        public AttendanceControl(PreviewRole role)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData(role);
        }

        private void InitializeSampleData(PreviewRole role)
        {
            bool administrator = role == PreviewRole.Administrator;
            cmbOptions.SelectedIndex = 0;
            columnAttendanceEmployee.Visible = administrator;
            columnAttendanceDepartment.Visible = administrator;
            columnLeaveEmployee.Visible = administrator;
            columnLeaveDepartment.Visible = administrator;
            btnLeaveApplicationPreview.Visible = !administrator;
            btnLeaveDecision.Visible = administrator;
            lblBalance.Text = administrator ? "검토 대기 3건" : "남은 연차 8.0일";
            lblAttendanceTitle.Text = administrator ? "직원 출퇴근 기록" : "출퇴근 기록";
            lblLeaveTitle.Text = administrator ? "직원 연차 신청 목록" : "연차 신청 내역";

            if (administrator)
            {
                gridAttendance.Rows.Add("이서연", "기획팀", "2026.10.16", "09:03", "―", "근무 중", "원격");
                gridAttendance.Rows.Add("김민수", "개발팀", "2026.10.16", "08:57", "―", "근무 중", "원격");
                gridAttendance.Rows.Add("박지우", "디자인팀", "2026.10.16", "09:00", "―", "근무 중", "원격");
                gridLeaveRequests.Rows.Add("이서연", "기획팀", "연차", "2026.10.16", "개인 일정", "검토 대기");
                gridLeaveRequests.Rows.Add("김민수", "개발팀", "반차(오후)", "2026.10.20", "개인 일정", "검토 대기");
                gridLeaveRequests.Rows.Add("박지우", "디자인팀", "병가", "2026.10.21", "진료", "검토 대기");
            }
            else
            {
                gridAttendance.Rows.Add("이서연", "기획팀", "2026.10.16", "09:03", "―", "근무 중", "원격");
                gridAttendance.Rows.Add("이서연", "기획팀", "2026.10.15", "08:57", "18:02", "정상", "원격");
                gridAttendance.Rows.Add("이서연", "기획팀", "2026.10.08", "―", "―", "연차", "―");
                gridLeaveRequests.Rows.Add("이서연", "기획팀", "연차", "2026.10.08", "개인 일정", "접수");
                gridLeaveRequests.Rows.Add("이서연", "기획팀", "반차(오후)", "2026.10.20", "개인 일정", "검토 대기");
            }
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

        private void btnLeaveApplicationPreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new LeaveApplicationForm());
        }

        private void btnLeaveDecision_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new LeaveDecisionForm());
        }

    }
}
