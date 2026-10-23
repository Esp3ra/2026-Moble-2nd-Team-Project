#if REAL_LEAVE_ADAPTER
using System.Reflection;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;
using RemoteWork.WinForms.Ui;

internal static partial class Program
{
    private static LoginLeaveConnection Connect(ApiClient.Session session, HttpLeaveWorkflow workflow)
        => new(session, workflow, async ct =>
        {
            var state = await workflow.ReadinessAsync(ct);
            return new(state.SchemaReady, state.WriteReady, state.Pending, state.Year, state.ServerTime);
        }, async (year, ct) =>
        {
            var state = await workflow.BalanceAsync(year, ct);
            return new(state.Year, state.GrantedUnits, state.ReservedUnits, state.UsedUnits, state.RemainingUnits);
        });

    private static void RealLeave(string[] args)
    {
        if (args.Length != 4 || !Uri.TryCreate(args[1], UriKind.Absolute, out var uri) || !uri.IsLoopback)
            throw new ArgumentException("Owned loopback fixture required.");
        string mode = args[2], decision = args[3];
        string password = Environment.GetEnvironmentVariable("MEMBER5_TEST_PASSWORD") ?? throw new InvalidOperationException();
        string originalServer = AppServices.Api.ServerUri.ToString();
        MainForm? main = null;
        try
        {
            AppServices.Api.SetServer(uri.ToString());
            var login = AppServices.Api.LoginAsync("worker", password); Complete(login);
            main = new MainForm(login.Result.User); main.Show();
            Field<System.Windows.Forms.Timer>(main, "identityTimer").Stop();
            LoginLeaveConnection? connection = null; HttpLeaveWorkflow? flow = null; int created = 0;
            Complete(main.ConnectLeaveWorkflowAsync(session =>
            {
                created++; flow = new(session); return connection = Connect(session, flow);
            }));
            bool duplicateRejected = false;
            try { Complete(main.ConnectLeaveWorkflowAsync(session => throw new Exception("must not replace"))); }
            catch (InvalidOperationException) { duplicateRejected = true; }
            Check(duplicateRejected && created == 1, "MainForm owns exactly one leave adapter per captured login");
            Field<Button>(main, "btnAttendance").PerformClick();
            var page = (AttendanceControl)Field<UserControl>(main, "currentPage");
            Complete(page.RefreshLeaveAsync());
            Check(Field<Label>(page, "lblBalance").Text.Contains("잔여 8일"), "Actual MySQL balance reaches Designer label");
            if (mode == "policy-off")
            {
                Check(!Field<Button>(page, "btnLeaveApplicationPreview").Enabled, "Actual policy-off blocks new application UI");
                Check(Field<Label>(page, "lblLeavePreparation").Text.Contains("연차 정책 확인"), "Actual pending policy visible");
            }
            else
            {
                Check(Field<Button>(page, "btnLeaveApplicationPreview").Enabled, "Actual preparation unlocks explicit application");
                using var application = new LeaveApplicationForm(connection!.Session, flow); application.Show(main);
                Fill(application, "반차(오전)");
                Field<DateTimePicker>(application, "dtpStartDate").Value = new DateTime(2026, 10, 22);
                Field<DateTimePicker>(application, "dtpEndDate").Value = new DateTime(2026, 10, 22);
                Complete(Call(application, "ApplyAsync"));
                Guid original = Field<Guid?>(application, "requestId")!.Value;
                if (mode == "loss")
                {
                    Check(!application.SubmissionCompleted, "Discarded committed response remains uncertain in actual form");
                    Complete(Call(application, "ApplyAsync"));
                    Check(Field<Guid?>(application, "requestId") == original, "Actual retry keeps original intent UUID");
                }
                Check(application.SubmissionCompleted, "Actual server receipt completes application form"); application.Close();
                Field<Button>(main, "btnNotices").PerformClick();
                Check(page.IsDisposed && connection.IsCurrent, "Menu navigation disposes only page, not original adapter owner");
                Field<Button>(main, "btnAttendance").PerformClick(); page = (AttendanceControl)Field<UserControl>(main, "currentPage");
                Check(ReferenceEquals(Field<ILeaveWorkflow>(page, "leaveWorkflow"), flow) && created == 1, "Menu reentry retains actual adapter and cache");
                Complete(page.RefreshLeaveAsync());
                Check(Field<DataGridView>(page, "gridLeaveRequests").Rows.Count == 1, "Single MySQL request visible after reentry");

                using var boss = new ApiClient(uri.ToString()); Complete(boss.LoginAsync("boss", password));
                var managerSession = boss.CaptureSession(); var managerFlow = new HttpLeaveWorkflow(managerSession);
                using var managerConnection = Connect(managerSession, managerFlow);
                using var managerPage = new AttendanceControl(PreviewRole.Administrator, managerSession, managerFlow);
                using var managerHost = new Form(); managerHost.Controls.Add(managerPage); managerHost.Show();
                managerPage.BindLeaveConnection(managerConnection); Complete(managerPage.RefreshLeaveAsync());
                var grid = Field<DataGridView>(managerPage, "gridLeaveRequests");
                Check(grid.Rows.Count == 1, "Actual designated manager receives pending request"); grid.Rows[0].Selected = true;
                Check(Field<Button>(managerPage, "btnLeaveDecision").Enabled, "Manager own missing entitlement does not block assigned review");
                var request = (LeaveRequestView)grid.Rows[0].Tag!;
                using var review = new LeaveDecisionForm(managerSession, managerFlow, request); review.Show(managerHost);
                Field<TextBox>(review, "txtDecisionReason").Text = "검토 사유 · 신청 사유와 별도";
                Complete(Call(review, "DecideAsync", decision));
                Guid reviewId = Field<Guid?>(review, "requestId")!.Value;
                if (mode == "loss")
                {
                    Check(!review.DecisionCompleted, "Discarded committed decision is uncertain");
                    Complete(Call(review, "DecideAsync", decision));
                    Check(Field<Guid?>(review, "requestId") == reviewId, "Decision retry keeps original UUID and revision");
                }
                Check(review.DecisionCompleted, "Actual decision receipt completes form"); review.Close();
                Complete(page.RefreshLeaveAsync());
                var rows = Field<DataGridView>(page, "gridLeaveRequests");
                Check(rows.Rows.Count == 1 && ((LeaveRequestView)rows.Rows[0].Tag!).Status == decision, "Employee sees final server status");
                Check(rows.Rows[0].Cells["columnLeaveDecisionReason"].Value?.ToString() == "검토 사유 · 신청 사유와 별도", "Actual decision reason reaches separate column");
                string expected = decision == "approved" ? "잔여 7.5일" : "잔여 8일";
                Check(Field<Label>(page, "lblBalance").Text.Contains(expected), "Approval consumes or rejection restores actual half-day units");
            }
            AppServices.Api.ClearIdentity(); Application.DoEvents();
            Check(!connection!.IsCurrent, "Actual captured connection ends with login");
            Console.WriteLine(JsonSerializer.Serialize(new { passed = true, checks, mode, decision, created }));
        }
        finally
        {
            AppServices.Api.ClearIdentity();
            // Test-only owner disposal; no logout request or product configuration file write.
            main?.Dispose(); AppServices.Api.SetServer(originalServer);
        }
    }
}
#endif
