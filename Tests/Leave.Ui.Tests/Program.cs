using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

internal static class Program
{
    private static int checks;
    private static readonly DateOnly Day = new(2026, 10, 21);
    private static readonly LeaveDraft Draft = new("공가", Day, Day, "테스트 사유");
    private static LeaveRequestView Request(int id = 5, bool assigned = true)
        => new("leave-1", "revision-1", id, "테스트 직원", "시험팀", Draft, "pending", assigned);
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Control.CheckForIllegalCrossThreadCalls = true;
        int result = 1;
        using var loop = new Form { ShowInTaskbar = false, Location = new(-20000, -20000), StartPosition = FormStartPosition.Manual };
        loop.Shown += (_, _) => loop.BeginInvoke((Action)(() =>
        {
            try
            {
                PolicyAndDisconnected(); InvalidPeriod(); Submission(false); Submission(true);
                BadReceipt(); LateSubmission(false); LateSubmission(true);
                ReviewGuards(); Decision("approved"); Decision("rejected"); DecisionRetry(); BadDecisionReceipt();
                ListScope(false); ListScope(true); LateList();
                FilteredList(false); FilteredList(true); EmptyAndFailedList(); InputSummary();
                RejectionDetails(); RejectionScopeAndMissing(); RejectionList();
                if (args.Length == 1) Render(args[0]);
                Console.WriteLine($"PASS Leave UI: {checks} assertions; actual WinForms + synthetic workflow/identity; no real server, database or device.");
                result = 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            finally { loop.Close(); }
        }));
        Application.Run(loop);
        return result;
    }
    private static void Check(bool value, string description)
    { checks++; if (!value) throw new InvalidOperationException(description); }
    private static T Field<T>(object owner, string name) => (T)owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static Task Call(object owner, string name, params object[] args) => (Task)owner.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args)!;
    private static void Complete(Task task)
    {
        var watch = Stopwatch.StartNew();
        while (!task.IsCompleted)
        { Application.DoEvents(); Thread.Sleep(1); if (watch.ElapsedMilliseconds > 5000) throw new TimeoutException(); }
        task.GetAwaiter().GetResult();
    }
    private static ApiClient Login(bool admin = false)
    {
        var api = new ApiClient("http://leave.test", new IdentityHandler(admin));
        Complete(api.LoginAsync("synthetic", "synthetic"));
        return api;
    }
    private static void Fill(LeaveApplicationForm form, string type = "공가")
    {
        Field<ComboBox>(form, "cmbOptions").SelectedItem = type;
        Field<DateTimePicker>(form, "dtpStartDate").Value = Day.ToDateTime(TimeOnly.MinValue);
        Field<DateTimePicker>(form, "dtpEndDate").Value = Day.ToDateTime(TimeOnly.MinValue);
        Field<TextBox>(form, "txtLeaveReason").Text = Draft.Reason;
    }
    private static void PolicyAndDisconnected()
    {
        using var api = Login();
        using var form = new LeaveApplicationForm(api.CaptureSession(), null);
        form.Show();
        foreach (string type in new[] { "연차", "반차(오전)", "반차(오후)", "병가", "공가" })
        {
            Fill(form, type);
            Check((Draft with { Type = type }).DeductsAnnualBalance == (type is not ("병가" or "공가")), "Type charge policy");
            Check(Field<Label>(form, "lblCharge").Text.Contains(type is "병가" or "공가" ? "차감하지" : "차감 대상"), "Visible charge policy");
            Complete(Call(form, "ApplyAsync"));
            Check(!form.SubmissionCompleted && Field<Label>(form, "lblStatus").Text.Contains("저장되지"), "Disconnected never claims persistence");
        }
        Check(Field<TextBox>(form, "txtApplicant").Text.Contains("테스트 본인"), "Applicant from current session");
        using var panel = new AttendanceControl(PreviewRole.Employee, api.CaptureSession(), null);
        Check(Field<DataGridView>(panel, "gridLeaveRequests").Rows.Count == 0, "No fake leave rows");
        Check(!Field<Label>(panel, "lblBalance").Text.Contains("8.0"), "No fake balance");
    }
    private static void InvalidPeriod()
    {
        using var api = Login(); var flow = new Workflow();
        using var form = new LeaveApplicationForm(api.CaptureSession(), flow); Fill(form);
        Field<DateTimePicker>(form, "dtpEndDate").Value = Day.AddDays(-1).ToDateTime(TimeOnly.MinValue);
        Complete(Call(form, "ApplyAsync"));
        Check(flow.Submissions.Count == 0 && Field<Label>(form, "lblStatus").Text.Contains("종료일"), "Invalid period never submits");
    }
    private static void Submission(bool loseReply)
    {
        using var api = Login(); var flow = new Workflow();
        using var form = new LeaveApplicationForm(api.CaptureSession(), flow); form.Show(); Fill(form);
        var first = Call(form, "ApplyAsync"); Complete(Call(form, "ApplyAsync"));
        Check(flow.Submissions.Count == 1, "Duplicate click single flight");
        Check(!Field<TextBox>(form, "txtLeaveReason").Enabled, "Freeze pending payload");
        Guid id = flow.Submissions[0].Id;
        if (loseReply)
        {
            flow.SubmitCompletion.SetException(new HttpRequestException()); Complete(first);
            flow.SubmitCompletion = new(); first = Call(form, "ApplyAsync");
            Check(flow.Submissions.Count == 2 && flow.Submissions.All(x => x.Id == id && x.Draft == Draft), "Retry keeps identity and payload");
        }
        flow.SubmitCompletion.SetResult(new(id, Request())); Complete(first);
        Check(form.SubmissionCompleted, "Matched receipt completes");
        Complete(Call(form, "ApplyAsync"));
        Check(flow.Submissions.Count == (loseReply ? 2 : 1), "Completed does not submit again");
    }
    private static void BadReceipt()
    {
        using var api = Login(); var flow = new Workflow();
        using var form = new LeaveApplicationForm(api.CaptureSession(), flow); Fill(form);
        var task = Call(form, "ApplyAsync");
        flow.SubmitCompletion.SetResult(new(flow.Submissions[0].Id, Request(99))); Complete(task);
        Check(!form.SubmissionCompleted, "Other applicant receipt rejected");
    }
    private static void LateSubmission(bool logout)
    {
        using var api = Login(); var flow = new Workflow();
        using var form = new LeaveApplicationForm(api.CaptureSession(), flow); form.Show(); Fill(form);
        var task = Call(form, "ApplyAsync");
        if (logout) api.ClearIdentity(); else form.Dispose();
        Check(flow.LastCancellation.IsCancellationRequested, "Session/disposal cancels submission");
        flow.SubmitCompletion.SetResult(new(flow.Submissions[0].Id, Request())); Complete(task);
        Check(!form.SubmissionCompleted, "Late submission cannot repaint success");
    }
    private static void ReviewGuards()
    {
        using var employee = Login(); using var admin = Login(true);
        var flow = new Workflow();
        using var wrongRole = new LeaveDecisionForm(employee.CaptureSession(), flow, Request());
        using var unassigned = new LeaveDecisionForm(admin.CaptureSession(), flow, Request(5, false));
        using var noSelection = new LeaveDecisionForm(admin.CaptureSession(), flow, null);
        using var disconnected = new LeaveDecisionForm(admin.CaptureSession(), null, Request());
        foreach (var form in new[] { wrongRole, unassigned, noSelection, disconnected })
        {
            Check(!Field<Button>(form, "btnApprove").Enabled && !Field<Button>(form, "btnReject").Enabled, "Review guard disables both actions");
            Complete(Call(form, "DecideAsync", "approved"));
        }
        Check(flow.Decisions.Count == 0, "No review request outside scope");
    }
    private static void Decision(string decision)
    {
        using var api = Login(true); var flow = new Workflow();
        using var form = new LeaveDecisionForm(api.CaptureSession(), flow, Request()); form.Show();
        Field<TextBox>(form, "txtDecisionReason").Text = "검토 의견";
        var task = Call(form, "DecideAsync", decision); Complete(Call(form, "DecideAsync", decision));
        Check(flow.Decisions.Count == 1 && flow.Decisions[0].Request == Request(), "Only selected request single decision");
        flow.DecisionCompletion.SetResult(new(flow.Decisions[0].Id, Request() with { Status = decision, Revision = "revision-2" })); Complete(task);
        Check(form.DecisionCompleted, "Exact decision receipt completes");
        Check(!Field<Button>(form, "btnApprove").Enabled && !Field<Button>(form, "btnReject").Enabled, "Completed decision locked");
    }
    private static void DecisionRetry()
    {
        using var api = Login(true); var flow = new Workflow();
        using var form = new LeaveDecisionForm(api.CaptureSession(), flow, Request());
        var first = Call(form, "DecideAsync", "approved");
        flow.DecisionCompletion.SetException(new HttpRequestException()); Complete(first);
        Check(!Field<Button>(form, "btnReject").Enabled && Field<TextBox>(form, "txtDecisionReason").ReadOnly, "Uncertain decision prevents opposite and changed reason");
        Complete(Call(form, "DecideAsync", "rejected"));
        Check(flow.Decisions.Count == 1, "No opposite operation after lost response");
        flow.DecisionCompletion = new(); var second = Call(form, "DecideAsync", "approved");
        Check(flow.Decisions[0] == flow.Decisions[1], "Retry retains decision revision, key and payload");
        api.ClearIdentity();
        flow.DecisionCompletion.SetResult(new(flow.Decisions[0].Id, Request() with { Status = "approved", Revision = "r2" })); Complete(second);
        Check(!form.DecisionCompleted, "Late review after logout ignored");
    }
    private static void ListScope(bool admin)
    {
        using var api = Login(admin); var flow = new Workflow();
        using var panel = new AttendanceControl(admin ? PreviewRole.Administrator : PreviewRole.Employee, api.CaptureSession(), flow);
        using var host = new Form(); host.Controls.Add(panel); host.Show(); Application.DoEvents();
        var task = Call(panel, "LoadLeaveAsync");
        flow.ListCompletion.SetResult(new[] { Request(), Request(99, false) }); Complete(task);
        var grid = Field<DataGridView>(panel, "gridLeaveRequests");
        Check(grid.Rows.Count == 1 && ((LeaveRequestView)grid.Rows[0].Tag!).ApplicantId == 5, "Own/assigned list scope");
        Check(grid.SelectedRows.Count == 0 && !Field<Button>(panel, "btnLeaveDecision").Enabled, "No implicit first-row approval");
        if (admin)
        {
            grid.Rows[0].Selected = true;
            Check(Field<Button>(panel, "btnLeaveDecision").Enabled, "Explicit assigned selection can review");
        }
        api.ClearIdentity(); Application.DoEvents();
        Check(grid.Rows.Count == 0 && !Field<Button>(panel, "btnLeaveDecision").Enabled, "Logout clears visible private rows");
    }
    private static void BadDecisionReceipt()
    {
        using var api = Login(true); var flow = new Workflow();
        using var form = new LeaveDecisionForm(api.CaptureSession(), flow, Request());
        var task = Call(form, "DecideAsync", "approved");
        flow.DecisionCompletion.SetResult(new(flow.Decisions[0].Id, Request() with { Id = "other", Status = "approved", Revision = "r2" })); Complete(task);
        Check(!form.DecisionCompleted, "Other request's decision receipt cannot complete selection");
    }
    private static void LateList()
    {
        using var api = Login(); var flow = new Workflow();
        using var panel = new AttendanceControl(PreviewRole.Employee, api.CaptureSession(), flow);
        var task = Call(panel, "LoadLeaveAsync"); api.ClearIdentity();
        flow.ListCompletion.SetResult(new[] { Request() }); Complete(task);
        Check(Field<DataGridView>(panel, "gridLeaveRequests").Rows.Count == 0, "Late list after logout ignored");
    }
    private static void Render(string directory)
    {
        Directory.CreateDirectory(directory);
        using var api = Login(true);
        using var form = new LeaveApplicationForm(api.CaptureSession(), null); form.Show(); Fill(form, "병가"); Application.DoEvents();
        using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
        image.Save(Path.Combine(directory, "leave-application.png"));
        using var review = new LeaveDecisionForm(api.CaptureSession(), null, Request()); review.Show(); Application.DoEvents();
        using var image2 = new Bitmap(review.Width, review.Height); review.DrawToBitmap(image2, new Rectangle(Point.Empty, review.Size));
        image2.Save(Path.Combine(directory, "leave-review.png"));
    }
    private static void FilteredList(bool admin)
    {
        using var api = Login(admin); var flow = new Workflow();
        using var panel = new AttendanceControl(admin ? PreviewRole.Administrator : PreviewRole.Employee, api.CaptureSession(), flow) { LeaveOnly = true };
        using var host = new Form { Size = new(1100, 700) }; host.Controls.Add(panel); host.Show(); Application.DoEvents();
        var task = panel.RefreshLeaveAsync();
        flow.ListCompletion.SetResult(new[] { Request(), Request() with { Id = "approved", Status = "approved" },
            Request() with { Id = "rejected", Status = "rejected" }, Request(99, false) }); Complete(task);
        var filter = Field<ComboBox>(panel, "cmbLeaveStatus"); var grid = Field<DataGridView>(panel, "gridLeaveRequests");
        Check(grid.Rows.Count == 3, "All filter stays inside own/assigned scope");
        filter.SelectedIndex = 2;
        Check(grid.Rows.Count == 1 && ((LeaveRequestView)grid.Rows[0].Tag!).Status == "approved", "Approved local filter");
        grid.Rows[0].Selected = true;
        Check(!Field<Button>(panel, "btnLeaveDecision").Enabled, "Approved row cannot be reviewed again");
        filter.SelectedIndex = 1;
        Check(grid.Rows.Count == 1 && grid.SelectedRows.Count == 0, "Changing filter clears decision selection");
        filter.SelectedIndex = 3;
        Check(grid.Rows.Count == 1 && ((LeaveRequestView)grid.Rows[0].Tag!).Status == "rejected", "Rejected local filter");
        Check(flow.ListCalls == 1, "Filter does not issue another API/workflow query");
        api.ClearIdentity(); Application.DoEvents(); filter.SelectedIndex = 0;
        Check(grid.Rows.Count == 0 && !filter.Enabled, "Filter cannot resurrect rows after session end");
    }
    private static void EmptyAndFailedList()
    {
        using var api = Login(); var flow = new Workflow();
        using var panel = new AttendanceControl(PreviewRole.Employee, api.CaptureSession(), flow);
        var first = panel.RefreshLeaveAsync(); flow.ListCompletion.SetResult(Array.Empty<LeaveRequestView>()); Complete(first);
        Check(Field<Label>(panel, "lblLeaveEmpty").Text.Contains("없습니다"), "Empty successful list has explicit message");
        flow.ListCompletion = new(); var second = panel.RefreshLeaveAsync();
        flow.ListCompletion.SetException(new HttpRequestException("synthetic list error")); Complete(second);
        Field<ComboBox>(panel, "cmbLeaveStatus").SelectedIndex = 2;
        Check(Field<Label>(panel, "lblLeaveEmpty").Text.Contains("불러오지 못"), "Filter cannot turn failed query into empty success");
    }
    private static void InputSummary()
    {
        using var api = Login(); using var form = new LeaveApplicationForm(api.CaptureSession(), null);
        Fill(form, "병가");
        Check(Field<Label>(form, "lblInputSummary").Text.Contains("병가")
            && Field<Label>(form, "lblInputSummary").Text.Contains("2026-10-21"), "Live summary reflects selected type and dates");
        Field<DateTimePicker>(form, "dtpEndDate").Value = Day.AddDays(-1).ToDateTime(TimeOnly.MinValue);
        Check(Field<Label>(form, "lblInputSummary").Text.Contains("종료일"), "Invalid date shown before submission");
        using var admin = Login(true);
        using var review = new LeaveDecisionForm(admin.CaptureSession(), null, Request());
        Check(Field<Label>(review, "lblCharge").Text.Contains("차감하지"), "Review displays sick/official balance policy");
    }
    private static void RejectionDetails()
    {
        using var api = Login(); var flow = new Workflow();
        string reason = "첫 번째 반려 사유\r\n" + new string('가', 800) + "\r\n다른 날짜로 다시 신청해 주세요.";
        var row = Request(5, false) with { Status = "rejected", DecisionReason = reason };
        using var detail = new LeaveDecisionForm(api.CaptureSession(), flow, row, readOnly: true); detail.Show();
        Check(Field<TextBox>(detail, "txtDecisionReason").Text == reason, "Employee sees complete multiline rejection reason");
        Check(Field<TextBox>(detail, "txtRequestReason").Text == row.Draft.Reason, "Application reason is separate from rejection reason");
        Check(Field<TextBox>(detail, "txtDecisionReason").ReadOnly && !Field<Button>(detail, "btnApprove").Enabled
            && !Field<Button>(detail, "btnReject").Enabled, "Details cannot edit or review");
        Complete(Call(detail, "DecideAsync", "approved"));
        Check(flow.Decisions.Count == 0, "Read-only mode cannot submit decisions even with direct invocation");
        api.ClearIdentity(); Application.DoEvents();
        Check(!detail.Visible, "Open rejection details close when login ends");
    }
    private static void RejectionScopeAndMissing()
    {
        using var employee = Login(); using var admin = Login(true);
        var other = Request(99, false) with { Status = "rejected", DecisionReason = "다른 직원의 검토 의견" };
        using var wrongEmployee = new LeaveDecisionForm(employee.CaptureSession(), null, other, readOnly: true);
        using var unassignedAdmin = new LeaveDecisionForm(admin.CaptureSession(), null, other, readOnly: true);
        foreach (var form in new[] { wrongEmployee, unassignedAdmin })
            Check(Field<TextBox>(form, "txtDecisionReason").Text.Length == 0
                && Field<TextBox>(form, "txtRequestReason").Text.Length == 0, "No details outside own/assigned scope");
        using var missing = new LeaveDecisionForm(employee.CaptureSession(), null, Request(5, false) with { Status = "rejected" }, readOnly: true);
        Check(Field<Label>(missing, "lblStatus").Text.Contains("반려 사유가 없습니다")
            && Field<TextBox>(missing, "txtDecisionReason").Text.Length == 0, "Missing rejection reason is explicit, not fabricated from application reason");
    }
    private static void RejectionList()
    {
        using var api = Login(); var flow = new Workflow();
        using var panel = new AttendanceControl(PreviewRole.Employee, api.CaptureSession(), flow);
        using var host = new Form(); host.Controls.Add(panel); host.Show(); Application.DoEvents();
        var row = Request(5, false) with { Status = "rejected", DecisionReason = "서로 다른 사유를 확인해 주세요." };
        var task = panel.RefreshLeaveAsync(); flow.ListCompletion.SetResult(new[] { row }); Complete(task);
        var grid = Field<DataGridView>(panel, "gridLeaveRequests"); grid.Rows[0].Selected = true;
        Check(grid.Rows[0].Cells["columnLeaveDecisionReason"].Value?.ToString() == row.DecisionReason, "Rejection reason reaches the employee list");
        Check(Field<Button>(panel, "btnLeaveDetails").Enabled, "Employee can explicitly open selected details");
        bool openedWithReason = false;
        using var closer = new System.Windows.Forms.Timer { Interval = 20 };
        closer.Tick += (_, _) =>
        {
            var form = Application.OpenForms.OfType<LeaveDecisionForm>().LastOrDefault();
            if (form is null) return;
            openedWithReason = Field<TextBox>(form, "txtDecisionReason").Text == row.DecisionReason
                && Field<TextBox>(form, "txtDecisionReason").ReadOnly;
            closer.Stop(); form.Close();
        };
        closer.Start(); Field<Button>(panel, "btnLeaveDetails").PerformClick(); closer.Stop();
        Check(openedWithReason, "Actual employee details button opens full rejection reason read-only");
        Field<ComboBox>(panel, "cmbLeaveStatus").SelectedIndex = 1;
        Check(!Field<Button>(panel, "btnLeaveDetails").Enabled, "Filter without selected row disables details");
        api.ClearIdentity(); Application.DoEvents();
        Check(!Field<Button>(panel, "btnLeaveDetails").Enabled && grid.Rows.Count == 0, "Ended login cannot reopen private details");
    }
    private sealed class IdentityHandler(bool admin) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data = new LoginResult("synthetic", "Bearer", DateTimeOffset.UtcNow.AddHours(1),
                new UserInfo(admin ? 1 : 5, "synthetic", "테스트 본인", "test", "시험팀", admin ? "admin" : "employee", true)) }, ApiClient.Json), Encoding.UTF8, "application/json") });
    }
    private sealed class Workflow : ILeaveWorkflow
    {
        public bool IsConnected => true;
        public TaskCompletionSource<LeaveSubmissionReceipt> SubmitCompletion = new();
        public TaskCompletionSource<LeaveDecisionReceipt> DecisionCompletion = new();
        public TaskCompletionSource<IReadOnlyList<LeaveRequestView>> ListCompletion = new();
        public List<(LeaveDraft Draft, Guid Id)> Submissions = new();
        public List<(LeaveRequestView Request, string Decision, string Reason, Guid Id)> Decisions = new();
        public CancellationToken LastCancellation;
        public int ListCalls;
        public Task<LeaveSubmissionReceipt> SubmitAsync(LeaveDraft draft, Guid requestId, CancellationToken cancellation)
        { LastCancellation = cancellation; Submissions.Add((draft, requestId)); return SubmitCompletion.Task; }
        public Task<IReadOnlyList<LeaveRequestView>> ListAsync(CancellationToken cancellation) { ListCalls++; return ListCompletion.Task; }
        public Task<LeaveDecisionReceipt> DecideAsync(LeaveRequestView request, string decision, string reason, Guid requestId, CancellationToken cancellation)
        { Decisions.Add((request, decision, reason, requestId)); return DecisionCompletion.Task; }
    }
}
