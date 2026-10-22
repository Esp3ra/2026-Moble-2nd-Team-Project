using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Attendance
{
    public partial class AttendanceControl : UserControl
    {
        private readonly IActivityMonitor? activityMonitor;
        private readonly ILeaveWorkflow leaveWorkflow = new UnconnectedLeaveWorkflow();
        private readonly ApiClient.Session? leaveSession;
        private readonly CancellationTokenSource leaveLifetime = new();
        private CancellationTokenRegistration leaveSessionEnding;
        private bool leaveLoading, leaveEnded;
        public Task PendingLeaveLoad { get; private set; } = Task.CompletedTask;

        public AttendanceControl(PreviewRole role, IActivityMonitor activityMonitor) : this(role)
        { this.activityMonitor = activityMonitor ?? throw new ArgumentNullException(nameof(activityMonitor)); }

        public AttendanceControl() : this(PreviewRole.Employee) { }

        public AttendanceControl(PreviewRole role) : this(role, null, null) { }

        public AttendanceControl(PreviewRole role, ApiClient.Session? session, ILeaveWorkflow? workflow)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            leaveSession = session ?? LeaveInput.Current();
            leaveWorkflow = workflow ?? new UnconnectedLeaveWorkflow();
            InitializeSampleData(role);
            if (leaveSession is not null) leaveSessionEnding = leaveSession.Cancellation.Register(EndLeaveSession);
            gridLeaveRequests.SelectionChanged += (_, _) => UpdateReviewButton();
            UpdateReviewButton();
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
            cmbOptions.Visible = false; // Old sample month filter is not a server filter.
            btnQuery.Text = "연차 조회";
            lblBalance.Text = "잔여 연차 조회 전";
            lblAttendanceTitle.Text = administrator ? "직원 출퇴근 기록 · 예시" : "출퇴근 기록 · 예시";
            lblLeaveTitle.Text = leaveWorkflow.IsConnected
                ? (administrator ? "지정된 직원의 연차 신청 · 조회 전" : "내 연차 신청 · 조회 전")
                : "연차 서버 연결 전 · 저장된 신청을 아직 조회하지 않았습니다.";

            if (administrator)
            {
                gridAttendance.Rows.Add("이서연", "기획팀", "2026.10.16", "09:03", "―", "근무 중", "원격");
                gridAttendance.Rows.Add("김민수", "개발팀", "2026.10.16", "08:57", "―", "근무 중", "원격");
                gridAttendance.Rows.Add("박지우", "디자인팀", "2026.10.16", "09:00", "―", "근무 중", "원격");
            }
            else
            {
                gridAttendance.Rows.Add("김직원", "개발팀", "2026.10.16", "09:03", "―", "근무 중", "원격");
                gridAttendance.Rows.Add("김직원", "개발팀", "2026.10.15", "08:57", "18:02", "정상", "원격");
                gridAttendance.Rows.Add("김직원", "개발팀", "2026.10.08", "―", "―", "연차", "―");
            }
        }

        private bool LeaveLive => !leaveEnded && !IsDisposed && !Disposing && LeaveInput.Live(leaveSession);
        private void EndLeaveSession()
        {
            if (leaveEnded) return;
            leaveLifetime.Cancel();
            try
            {
                if (IsHandleCreated) BeginInvoke((Action)(() =>
                {
                    if (IsDisposed || Disposing) return;
                    gridLeaveRequests.Rows.Clear();
                    lblLeaveTitle.Text = "로그인이 필요합니다. 이전 신청 목록을 닫았습니다.";
                    lblBalance.Text = "잔여 연차 조회 전";
                    btnQuery.Enabled = btnLeaveDecision.Enabled = btnLeaveApplicationPreview.Enabled = false;
                }));
            }
            catch (InvalidOperationException) { }
        }
        private void EndLeaveLifetime()
        {
            if (leaveEnded) return;
            leaveEnded = true;
            leaveSessionEnding.Unregister();
            leaveLifetime.Cancel();
        }
        private LeaveRequestView? SelectedLeave => gridLeaveRequests.SelectedRows.Count == 1
            ? gridLeaveRequests.SelectedRows[0].Tag as LeaveRequestView : null;
        private void UpdateReviewButton() => btnLeaveDecision.Enabled = LeaveLive && !leaveLoading
            && leaveWorkflow.IsConnected && LeaveInput.Administrator(leaveSession)
            && SelectedLeave is { CanReview: true, Status: "pending" };
        private async void QueryLeave_Click(object? sender, EventArgs e)
        { PendingLeaveLoad = LoadLeaveAsync(); await PendingLeaveLoad; }
        private async Task LoadLeaveAsync()
        {
            if (!LeaveLive || leaveLoading) return;
            gridLeaveRequests.Rows.Clear();
            if (!leaveWorkflow.IsConnected)
            { lblLeaveTitle.Text = "연차 서버 연결 전 · 신청 내역은 조회되지 않았습니다."; return; }
            leaveLoading = true;
            btnQuery.Enabled = false;
            UpdateReviewButton();
            lblLeaveTitle.Text = "연차 신청 조회 중…";
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(leaveLifetime.Token, leaveSession!.Cancellation);
            try
            {
                var rows = await leaveWorkflow.ListAsync(linked.Token);
                if (!LeaveLive || linked.IsCancellationRequested) return;
                // An adapter must already enforce this on the server. Do not display accidental cross-scope rows.
                var scoped = rows.Where(r => LeaveInput.Administrator(leaveSession)
                    ? r.CanReview : r.ApplicantId == leaveSession.User.Id).ToArray();
                foreach (var r in scoped)
                {
                    int index = gridLeaveRequests.Rows.Add(r.ApplicantName, r.Department, r.Draft.Type,
                        $"{r.Draft.StartDate:yyyy-MM-dd} ~ {r.Draft.EndDate:yyyy-MM-dd}", r.Draft.Reason,
                        r.Status switch { "pending" => "검토 대기", "approved" => "승인", "rejected" => "반려", _ => "확인 필요" });
                    gridLeaveRequests.Rows[index].Tag = r;
                }
                gridLeaveRequests.ClearSelection();
                gridLeaveRequests.CurrentCell = null;
                lblLeaveTitle.Text = LeaveInput.Administrator(leaveSession)
                    ? $"지정된 직원의 연차 신청 {scoped.Length}건" : $"내 연차 신청 {scoped.Length}건";
            }
            catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
            catch (Exception ex) when (ex is ApiException or HttpRequestException or InvalidDataException or InvalidOperationException or OperationCanceledException)
            { if (LeaveLive) lblLeaveTitle.Text = "연차 조회 실패 · 이전 목록으로 처리하지 않습니다. 다시 조회해 주세요."; }
            finally
            {
                leaveLoading = false;
                if (LeaveLive) { btnQuery.Enabled = true; UpdateReviewButton(); }
            }
        }

        private void btnLeaveApplicationPreview_Click(object? sender, EventArgs e)
        {
            using var form = new LeaveApplicationForm(leaveSession, leaveWorkflow);
            form.ShowDialog(this);
            if (form.SubmissionCompleted && LeaveLive) { PendingLeaveLoad = LoadLeaveAsync(); }
        }

        private void btnLeaveDecision_Click(object? sender, EventArgs e)
        {
            if (!btnLeaveDecision.Enabled || SelectedLeave is not { } selected) return;
            using var form = new LeaveDecisionForm(leaveSession, leaveWorkflow, selected);
            form.ShowDialog(this);
            if (form.DecisionCompleted && LeaveLive) { PendingLeaveLoad = LoadLeaveAsync(); }
        }

        private void btnActivityPresence_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, activityMonitor is null ? new ActivityPresenceForm() : new ActivityPresenceForm(activityMonitor));
        }

    }
}
