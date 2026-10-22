using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Ui;

internal static partial class Program
{
    private static readonly DateTimeOffset ServerNow = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    private static LeavePreparationState Ready => new(true, true, [], 2026, ServerNow);
    private static LeaveBalanceState Assigned => new(2026, 16, 1, 2, 13);

    private static void ConnectedLeaveScreens()
    {
        using var api = Login(); var session = api.CaptureSession(); var flow = new Workflow();
        var preparation = Ready; var balance = Assigned; bool failBalance = false, failPreparation = false;
        int queriedYear = 0;
        using var connection = new LoginLeaveConnection(session, flow,
            _ => failPreparation ? Task.FromException<LeavePreparationState>(new HttpRequestException()) : Task.FromResult(preparation),
            (year, _) => { queriedYear = year; return failBalance ? Task.FromException<LeaveBalanceState>(new HttpRequestException()) : Task.FromResult(balance); });
        using var panel = new AttendanceControl(PreviewRole.Employee, session, flow) { LeaveOnly = true };
        using var host = new Form(); host.Controls.Add(panel); host.Show();
        panel.BindLeaveConnection(connection);
        Check(!Field<Button>(panel, "btnLeaveApplicationPreview").Enabled, "No application before readiness read");
        flow.ListCompletion.SetResult([Request(5, false)]);
        Complete(panel.RefreshLeaveAsync());
        Check(queriedYear == 2026, "Balance uses server readiness year");
        var label = Field<Label>(panel, "lblBalance");
        Check(label.Text.Contains("6.5일") && label.Text.Contains("0.5일") && label.Text.Contains("1일"), "Half-day units project into real remaining/reserved/used days");
        Check(Field<Button>(panel, "btnLeaveApplicationPreview").Enabled, "Ready employee may open application");
        Check(Field<Label>(panel, "lblLeavePreparation").Text.Contains("연결됨"), "Ready state visible independently of list");
        preparation = Ready with { WriteReady = false, Pending = ["LEAVE_POLICY_REQUIRED", "LEAVE_REVIEWER_REQUIRED"] };
        failBalance = true; Complete(panel.RefreshLeaveAsync());
        Check(!Field<Button>(panel, "btnLeaveApplicationPreview").Enabled, "Missing preparation disables new submission");
        Check(label.Text == "잔여 연차 확인 필요", "Failed balance clears previous value instead of showing zero");
        Check(Field<Label>(panel, "lblLeavePreparation").Text.Contains("연차 정책 확인")
            && Field<Label>(panel, "lblLeavePreparation").Text.Contains("검토 담당자 지정"), "Pending settings translated without inventing approval");
        Field<ComboBox>(panel, "cmbLeaveStatus").SelectedIndex = 1;
        Check(Field<Label>(panel, "lblLeavePreparation").Text.Contains("연차 정책 확인"), "Filtering never erases preparation notice");
        failPreparation = true; Complete(panel.RefreshLeaveAsync());
        Check(Field<DataGridView>(panel, "gridLeaveRequests").Rows.Count == 1, "Preparation failure does not suppress readable own list");
        Check(!Field<Button>(panel, "btnLeaveApplicationPreview").Enabled && label.Text == "잔여 연차 확인 필요", "Preparation failure never leaves stale write readiness");
        failPreparation = failBalance = false; preparation = Ready; balance = Assigned with { Year = 2025 };
        Complete(panel.RefreshLeaveAsync());
        Check(label.Text == "잔여 연차 확인 필요", "Mismatched year is not displayed");
        balance = Assigned with { RemainingUnits = 15 };
        Complete(panel.RefreshLeaveAsync());
        Check(label.Text == "잔여 연차 확인 필요", "Invalid balance arithmetic is not displayed");
        preparation = Ready with { Pending = ["LEAVE_POLICY_REQUIRED"] };
        Complete(panel.RefreshLeaveAsync());
        Check(!Field<Button>(panel, "btnLeaveApplicationPreview").Enabled, "Contradictory ready/pending state rejected");
    }

    private static void ManagerPreparation()
    {
        using var api = Login(true); var session = api.CaptureSession(); var flow = new Workflow();
        using var connection = new LoginLeaveConnection(session, flow,
            _ => Task.FromResult(Ready with { WriteReady = false, Pending = ["LEAVE_ENTITLEMENT_REQUIRED", "LEAVE_REVIEWER_REQUIRED"] }),
            (_, _) => Task.FromException<LeaveBalanceState>(new HttpRequestException()));
        using var panel = new AttendanceControl(PreviewRole.Administrator, session, flow);
        using var host = new Form(); host.Controls.Add(panel); host.Show(); panel.BindLeaveConnection(connection);
        flow.ListCompletion.SetResult([Request(5, true), Request(99, false)]); Complete(panel.RefreshLeaveAsync());
        var grid = Field<DataGridView>(panel, "gridLeaveRequests");
        Check(grid.Rows.Count == 1, "Manager sees only assigned records with own preparation missing");
        grid.Rows[0].Selected = true;
        Check(Field<Button>(panel, "btnLeaveDecision").Enabled, "Manager missing personal grant/reviewer can still review assigned worker");
        Check(Field<Button>(panel, "btnLeaveDetails").Enabled, "Assigned detail remains available");
        api.ClearIdentity(); Application.DoEvents();
        Check(grid.Rows.Count == 0 && !Field<Button>(panel, "btnLeaveDecision").Enabled, "Ended manager session clears private rows and actions");
        Check(Field<Label>(panel, "lblLeavePreparation").Text.Contains("종료"), "Preparation also cleared on logout");
    }

    private static void LatePreparation()
    {
        using var api = Login(); var session = api.CaptureSession(); var flow = new Workflow();
        var pending = new TaskCompletionSource<LeavePreparationState>(); CancellationToken observed = default;
        using var connection = new LoginLeaveConnection(session, flow,
            ct => { observed = ct; return pending.Task; }, (_, _) => Task.FromResult(Assigned));
        using var panel = new AttendanceControl(PreviewRole.Employee, session, flow);
        using var host = new Form(); host.Controls.Add(panel); host.Show(); panel.BindLeaveConnection(connection);
        flow.ListCompletion.SetResult([]); var load = panel.RefreshLeaveAsync();
        api.ClearIdentity(); Application.DoEvents(); pending.SetResult(Ready); Complete(load);
        Check(observed.IsCancellationRequested && !connection.IsCurrent, "Logout cancels in-flight preparation");
        Check(Field<Label>(panel, "lblBalance").Text == "잔여 연차 조회 전", "Late readiness does not restore ended-login balance");
        Check(!Field<Button>(panel, "btnLeaveApplicationPreview").Enabled, "Late readiness cannot reenable ended login");
    }

    private static void LeaveConnectionLifetime()
    {
        using var api = Login(); var session = api.CaptureSession(); var flow = new Workflow();
        using var connection = new LoginLeaveConnection(session, flow,
            _ => Task.FromResult(Ready), (_, _) => Task.FromResult(Assigned));
        using (var first = new AttendanceControl(PreviewRole.Employee, session, flow)) first.BindLeaveConnection(connection);
        Check(connection.IsCurrent, "Page disposal does not end login-scoped connection");
        using var second = new AttendanceControl(PreviewRole.Employee, session, flow);
        second.BindLeaveConnection(connection); flow.ListCompletion.SetResult([]); Complete(second.RefreshLeaveAsync());
        Check(ReferenceEquals(Field<ILeaveWorkflow>(second, "leaveWorkflow"), flow), "Menu reentry borrows original workflow with original intent cache");
        using var wrong = new AttendanceControl(PreviewRole.Employee, api.CaptureSession(), new Workflow());
        bool rejected = false;
        try { wrong.BindLeaveConnection(connection); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "Different captured session/workflow cannot borrow connection");
        connection.Dispose(); Check(!connection.IsCurrent && session.IsCurrent, "Owner disposal ends only leave connection, not identity");
    }

    private static void RenderConnection(string directory)
    {
        using var api = Login(); var session = api.CaptureSession(); var flow = new Workflow();
        using var connection = new LoginLeaveConnection(session, flow, _ => Task.FromResult(Ready), (_, _) => Task.FromResult(Assigned));
        using var panel = new AttendanceControl(PreviewRole.Employee, session, flow) { Dock = DockStyle.Fill, LeaveOnly = true };
        using var host = new Form { ClientSize = new(1100, 660), Text = "연차 연결 · 합성 데이터 화면 검사" };
        host.Controls.Add(panel); host.Show(); panel.BindLeaveConnection(connection);
        flow.ListCompletion.SetResult([Request(5, false) with { Status = "rejected", DecisionReason = "담당자 확인 후 일정을 조정해 주세요." }]);
        Complete(panel.RefreshLeaveAsync()); Application.DoEvents();
        using var image = new Bitmap(host.Width, host.Height); host.DrawToBitmap(image, new Rectangle(Point.Empty, host.Size));
        image.Save(Path.Combine(directory, "leave-connection.png"));
    }
}
