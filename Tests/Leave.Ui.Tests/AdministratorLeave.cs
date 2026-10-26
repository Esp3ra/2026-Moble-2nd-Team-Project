using System.Net;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

internal static partial class Program
{
    private static void AdministratorSelfAndHistory()
    {
        using var api = Login(true); var session = api.CaptureSession(); var flow = new Workflow();
        using var connection = new LoginLeaveConnection(session, flow,
            _ => Task.FromResult(Ready), (_, _) => Task.FromResult(Assigned));
        using var panel = new AttendanceControl(PreviewRole.Administrator, session, flow) { LeaveOnly = true };
        using var host = new Form { ClientSize = new(1100, 700) };
        host.Controls.Add(panel); host.Show(); panel.BindLeaveConnection(connection);
        Check(Field<Button>(panel, "btnLeaveApplicationPreview").Visible
            && !Field<Button>(panel, "btnLeaveApplicationPreview").Enabled, "Manager sees application before readiness, disabled until prepared");
        var own = Request(1, false) with { Id = "own" };
        var ownDone = own with { Id = "own-done", Status = "approved" };
        var pending = Request(5) with { Id = "assigned-pending" };
        var approved = Request(5, false) with { Id = "assigned-approved", Status = "approved", CanReadAssigned = true };
        var rejected = Request(6, false) with { Id = "assigned-rejected", Status = "rejected", CanReadAssigned = true, DecisionReason = "지정 담당자 반려 사유" };
        // Even an inconsistent adapter flag must not enable self review.
        var ownForged = own with { Id = "own-forged-action", CanReview = true, CanReadAssigned = true };
        flow.ListCompletion.SetResult([own, ownDone, pending, approved, rejected, ownForged,
            Request(99, false), Request(98, false) with { Id = "unproven-history", Status = "approved", CanReview = true }]);
        Complete(panel.RefreshLeaveAsync());
        var grid = Field<DataGridView>(panel, "gridLeaveRequests");
        Check(grid.Rows.Count == 6, "Manager sees own requests and explicitly assigned history, excluding unproven rows");
        Check(Field<Label>(panel, "lblLeaveTitle").Text.StartsWith("내 신청 · 담당 신청"), "Manager list labels both own and assigned requests");
        Check(Field<Button>(panel, "btnLeaveApplicationPreview").Enabled, "Prepared administrator can open personal application");
        foreach (var row in new[] { own, ownDone, ownForged, approved, rejected })
        {
            grid.ClearSelection();
            grid.Rows.Cast<DataGridViewRow>().Single(r => ((LeaveRequestView)r.Tag!).Id == row.Id).Selected = true;
            Check(Field<Button>(panel, "btnLeaveDetails").Enabled, "Own or authorized completed request remains readable");
            Check(!Field<Button>(panel, "btnLeaveDecision").Enabled, "Own and completed rows never enable review");
            using var detail = new LeaveDecisionForm(session, flow, row, readOnly: true);
            Check(Field<TextBox>(detail, "txtRequestReason").Text == row.Draft.Reason, "Administrator detail retains readable content");
            if (row.Status == "rejected")
                Check(Field<TextBox>(detail, "txtDecisionReason").Text == row.DecisionReason, "Assigned rejected history retains rejection reason");
            Complete(Call(detail, "DecideAsync", "approved"));
        }
        grid.ClearSelection();
        grid.Rows.Cast<DataGridViewRow>().Single(r => ((LeaveRequestView)r.Tag!).Id == pending.Id).Selected = true;
        Check(Field<Button>(panel, "btnLeaveDecision").Enabled, "Other applicant's assigned pending request remains reviewable");
        using var selfReview = new LeaveDecisionForm(session, flow, ownForged);
        Check(!Field<Button>(selfReview, "btnApprove").Enabled && !Field<Button>(selfReview, "btnReject").Enabled,
            "Forged own CanReview flag cannot enable either decision button");
        Complete(Call(selfReview, "DecideAsync", "approved")); Complete(Call(selfReview, "DecideAsync", "rejected"));
        Check(flow.Decisions.Count == 0, "Direct calls cannot review self or read-only history");
        var filter = Field<ComboBox>(panel, "cmbLeaveStatus"); filter.SelectedIndex = 2;
        Check(grid.Rows.Count == 2, "Approved filter retains own and assigned completed history");
        filter.SelectedIndex = 3;
        Check(grid.Rows.Count == 1, "Rejected filter retains assigned completed history");
    }

    private static void AdministratorApplication()
    {
        using var api = Login(true); var flow = new Workflow();
        using var form = new LeaveApplicationForm(api.CaptureSession(), flow); form.Show(); Fill(form);
        var pending = Call(form, "ApplyAsync");
        Check(flow.Submissions.Count == 1, "Administrator can submit own application through the existing workflow");
        flow.SubmitCompletion.SetResult(new(flow.Submissions[0].Id, Request(1, false))); Complete(pending);
        Check(form.SubmissionCompleted, "Administrator own receipt completes application");
    }

    private static void EmployeeCannotReadAssigned()
    {
        using var api = Login(); var flow = new Workflow();
        using var panel = new AttendanceControl(PreviewRole.Employee, api.CaptureSession(), flow);
        var other = Request(99, false) with { Status = "approved", CanReadAssigned = true };
        flow.ListCompletion.SetResult([Request(5, false), other]); Complete(panel.RefreshLeaveAsync());
        Check(Field<DataGridView>(panel, "gridLeaveRequests").Rows.Count == 1, "Assigned-read marker never broadens employee scope");
        using var detail = new LeaveDecisionForm(api.CaptureSession(), flow, other, readOnly: true);
        Check(Field<TextBox>(detail, "txtRequestReason").Text.Length == 0, "Employee cannot read another applicant's marked history");
    }

    private static void LeaveHttpReadScopes()
    {
#if REAL_LEAVE_ADAPTER
        using var handler = new LeaveScopeHandler();
        using var api = new ApiClient("http://leave.test", handler); Complete(api.LoginAsync("synthetic", "synthetic"));
        var flow = new HttpLeaveWorkflow(api.CaptureSession());
        var load = flow.ListAsync(default); Complete(load); var rows = load.Result;
        Check(rows.Count == 4 && handler.Scopes.SequenceEqual(new[] { "own", "review" }), "Administrator adapter queries both authorized scopes");
        Check(!rows.Single(r => r.ApplicantId == 1).CanReadAssigned, "Own scope does not gain assigned marker from unknown JSON field");
        Check(rows.Where(r => r.ApplicantId != 1).All(r => r.CanReadAssigned), "All assigned scope statuses carry read provenance");
        Check(rows.Single(r => r.Status == "approved").CanReadAssigned && !rows.Single(r => r.Status == "approved").CanReview,
            "Approved history separates assigned read from decision permission");
        using var panel = new AttendanceControl(PreviewRole.Administrator, api.CaptureSession(), flow);
        Complete(panel.RefreshLeaveAsync());
        Check(Field<DataGridView>(panel, "gridLeaveRequests").Rows.Count == 4, "Real adapter projection preserves administrator own and completed assigned UI rows");
        handler.WrongOwn = true;
        bool rejected = false;
        try { Complete(flow.ListAsync(default)); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Foreign applicant in own scope remains rejected");
        handler.WrongOwn = false; handler.SelfInReview = true; rejected = false;
        try { Complete(flow.ListAsync(default)); } catch (InvalidDataException) { rejected = true; }
        Check(rejected, "Own applicant in assigned-review scope is rejected");
        using var employeeHandler = new LeaveScopeHandler { Employee = true };
        using var employee = new ApiClient("http://leave.test", employeeHandler); Complete(employee.LoginAsync("synthetic", "synthetic"));
        var employeeLoad = new HttpLeaveWorkflow(employee.CaptureSession()).ListAsync(default); Complete(employeeLoad);
        Check(employeeHandler.Scopes.SequenceEqual(new[] { "own" }) && employeeLoad.Result.All(r => !r.CanReadAssigned),
            "Employee HTTP adapter only requests own scope and ignores forged assigned marker");
#endif
    }

#if REAL_LEAVE_ADAPTER
    private sealed class LeaveScopeHandler : HttpMessageHandler
    {
        public bool WrongOwn, SelfInReview, Employee;
        public readonly List<string> Scopes = [];
        private static object Item(int sequence, int applicant, string status, bool canReview) => new
        {
            request_id = new Guid(sequence, 0, 0, new byte[8]).ToString("D"), revision = "1", applicant_id = applicant,
            applicant_name = "합성 신청인", department = "시험팀", type = "annual", start_date = "2026-10-26", end_date = "2026-10-26",
            reason = "합성 신청 사유", status, charged_units = 2, unit_days = "0.5", can_review = canReview,
            decision_reason = status == "rejected" ? "합성 반려 사유" : "", can_read_assigned = true
        };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            object data;
            if (request.RequestUri!.AbsolutePath.EndsWith("/auth/login"))
                data = new LoginResult("synthetic", "Bearer", DateTimeOffset.UtcNow.AddHours(1),
                    new UserInfo(Employee ? 5 : 1, "synthetic", "합성 본인", "test", "시험팀", Employee ? "employee" : "admin", true));
            else
            {
                if (request.Method != HttpMethod.Get || !request.RequestUri.AbsolutePath.EndsWith("/leave/requests"))
                    throw new InvalidOperationException("Unexpected fake HTTP request");
                var scope = request.RequestUri.Query.Contains("scope=review") ? "review" : "own"; Scopes.Add(scope);
                object[] items = scope == "own"
                    ? [Item(1, WrongOwn ? 99 : Employee ? 5 : 1, "pending", false)]
                    : [Item(2, SelfInReview ? 1 : 5, "pending", true), Item(3, 5, "approved", false), Item(4, 6, "rejected", false)];
                data = new { items, next_cursor = (string?)null };
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data }, ApiClient.Json), Encoding.UTF8, "application/json") });
        }
    }
#endif
}
