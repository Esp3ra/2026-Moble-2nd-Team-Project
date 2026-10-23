using System.Text;
using System.Text.Json;
using RemoteWork.ConnectedWorkspace;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;
using RemoteWork.WinForms.Ui;

internal static partial class Program
{
    private static void StartupProfile()
    {
        var server = new Uri("http://127.0.0.1:5000/");
        var endpoints = EnrollmentEndpoints.Parse(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            version = 1, server_url = server.ToString(), allow_insecure_http = true,
            devices = new Dictionary<string, object> { [DeviceId.ToString("D")] = new { pi_base_url = "http://pi.invalid:8080/" } }
        })), server);
        string valid = JsonSerializer.Serialize(new
        {
            version = 1, server_url = server.ToString(), leave_enabled = true,
            workday = new { device_id = DeviceId, camera = new { index = 0, backend = "DSHOW", left = 0, top = 35, right = 100, bottom = 100 } }
        });
        var profile = WorkspaceProfile.Parse(Encoding.UTF8.GetBytes(valid), server, endpoints);
        Check(profile.Workday?.FingerprintDeviceId == DeviceId && profile.LeaveEnabled, "Both independent startup tracks parsed");
        Check(profile.Workday!.PiAddress.ToString() == "http://pi.invalid:8080/", "Pi topology reused, not invented");
        foreach (var bad in new[]
        {
            valid.Replace("\"version\":1", "\"version\":true"),
            valid.Replace("\"version\":1", "\"version\":99999999999999999999"),
            valid.Replace("\"server_url\":\"http://127.0.0.1:5000/\"", "\"server_url\":null"),
            valid.Replace("\"version\":1", "\"version\":1,\"version\":1"),
            valid.Replace("127.0.0.1:5000", "127.0.0.1:5001"),
            valid.Replace(DeviceId.ToString(), Guid.NewGuid().ToString()),
            valid.Replace("\"top\":35", "\"top\":101"),
            valid.Replace("DSHOW", "unknown")
        })
        {
            bool denied = false;
            try { WorkspaceProfile.Parse(Encoding.UTF8.GetBytes(bad), server, endpoints); }
            catch (InvalidDataException) { denied = true; }
            Check(denied, "Invalid or wrong-server startup profile rejected before login/camera");
        }
        // Actual filesystem Load used before login, rather than Parse with an
        // already-created topology. Test-owned paths never read user settings.
        string directory = Path.Combine(Path.GetTempPath(), "workday-startup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string workspace = Path.Combine(directory, "workspace.json"), topology = Path.Combine(directory, "endpoints.json");
            var command = typeof(WorkspaceProfile).Assembly.GetType("RemoteWork.ConnectedWorkspace.Program")!
                .GetMethod("Run", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
            int CheckStartup()
            {
                string previousServer = AppServices.Api.ServerUri.ToString();
                try
                {
                    AppServices.Api.SetServer(server.ToString());
                    return (int)command.Invoke(null, new object[] { new[] { "--check-config" },
                        (Func<Uri, WorkspaceProfile>)(current => WorkspaceProfile.Load(current, workspace, topology)) })!;
                }
                finally { AppServices.Api.SetServer(previousServer); }
            }
            string leaveOnly = JsonSerializer.Serialize(new { version = 1, server_url = server.ToString(), leave_enabled = true, workday = (object?)null });
            File.WriteAllText(workspace, leaveOnly);
            Check(!File.Exists(topology), "Owned leave-only PC has no Pi topology");
            var loaded = WorkspaceProfile.Load(server, workspace, topology);
            Check(loaded.LeaveEnabled && loaded.Workday is null, "Actual pre-login Load supports leave-only without Pi settings");
            Check(CheckStartup() == 0, "Explicit check-config command accepts leave-only without Pi topology");
            File.WriteAllText(topology, "invalid Pi JSON");
            loaded = WorkspaceProfile.Load(server, workspace, topology);
            Check(loaded.LeaveEnabled && loaded.Workday is null, "Leave-only Load never parses an unrelated invalid Pi file");
            Check(CheckStartup() == 0, "Explicit check-config command ignores malformed Pi topology for leave-only");

            File.WriteAllText(workspace, valid);
            foreach (var contents in new string?[] { null, "invalid Pi JSON", JsonSerializer.Serialize(new
            {
                version = 1, server_url = "http://127.0.0.1:5001/", allow_insecure_http = true,
                devices = new Dictionary<string, object> { [DeviceId.ToString("D")] = new { pi_base_url = "http://pi.invalid:8080/" } }
            }) })
            {
                if (contents is null) File.Delete(topology); else File.WriteAllText(topology, contents);
                bool denied = false;
                try { WorkspaceProfile.Load(server, workspace, topology); }
                catch (InvalidDataException) { denied = true; }
                Check(denied, "Requested workday still rejects missing, malformed or wrong-server Pi settings");
                Check(CheckStartup() == 1, "Explicit check-config rejects required bad Pi topology before login");
            }
            File.WriteAllText(topology, JsonSerializer.Serialize(new
            {
                version = 1, server_url = server.ToString(), allow_insecure_http = true,
                devices = new Dictionary<string, object> { [DeviceId.ToString("D")] = new { pi_base_url = "http://pi.invalid:8080/" } }
            }));
            loaded = WorkspaceProfile.Load(server, workspace, topology);
            Check(loaded.LeaveEnabled && loaded.Workday?.FingerprintDeviceId == DeviceId, "Actual Load keeps both tracks when topology is valid");
            Check(loaded.Workday!.PiAddress == new Uri("http://pi.invalid:8080/"), "Actual Load resolves the selected Pi unchanged");
            Check(CheckStartup() == 0, "Explicit check-config accepts valid combined topology");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static async Task IndependentStartup(bool leaveFails, bool workdayFails)
    {
        using var server = new Server(); var api = AppServices.Api; string previous = api.ServerUri.ToString();
        api.SetServer(server.Url);
        try
        {
            var login = await api.LoginAsync("synthetic", "synthetic");
            using var main = new MainForm(login.User) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
            main.Show(); Field<System.Windows.Forms.Timer>(main, "identityTimer").Stop();
            var profile = new WorkspaceProfile(new Uri(server.Url), Options, true);
            var monitor = new Monitor(); var workflow = new StartupLeave(); int leaveCalls = 0, workdayCalls = 0;
            var report = await WorkspaceConnector.ConnectAsync(main, profile, session =>
            {
                leaveCalls++;
                if (leaveFails) throw new InvalidOperationException("synthetic missing leave provider");
                return new(session, workflow,
                    _ => Task.FromResult(new LeavePreparationState(true, false, ["LEAVE_POLICY_REQUIRED"], 2026, Before.ServerTime)),
                    (_, _) => Task.FromException<LeaveBalanceState>(new InvalidOperationException()));
            }, async (owner, options) =>
            {
                workdayCalls++;
                if (workdayFails) throw new InvalidOperationException("synthetic device not ready");
                await owner.ConnectWorkdayActivityAsync(options, monitor);
            });
            Check(leaveCalls == 1 && workdayCalls == 1, "Both tracks attempted once even if the other fails");
            Check(report.Notices.Length == (leaveFails || workdayFails ? 1 : 0), "Only failed track reports setup notice");
            Check(monitor.Starts == (workdayFails ? 0 : 1), "Confirmed server work alone starts monitor");
            var connection = Field<LoginLeaveConnection?>(main, "leaveConnection");
            Check((connection is not null) == !leaveFails, "Leave owner installed independently");
            if (!leaveFails)
            {
                Field<Button>(main, "btnAttendance").PerformClick();
                var page = (AttendanceControl)Field<UserControl>(main, "currentPage");
                await page.RefreshLeaveAsync();
                Check(!Field<Button>(page, "btnLeaveApplicationPreview").Enabled, "Policy-off stays blocked in connected startup");
                Check(ReferenceEquals(Field<ILeaveWorkflow>(page, "leaveWorkflow"), workflow), "Startup menus borrow original leave workflow");
                if (!workdayFails) Check(ReferenceEquals(Field<IActivityMonitor>(page, "activityMonitor"), monitor), "Both tracks share existing monitor, not another camera");
            }
            api.ClearIdentity(); await Task.Delay(15);
            Check(connection?.IsCurrent != true && !monitor.IsRunning, "Login end terminates both owned tracks");
        }
        finally { api.ClearIdentity(); api.SetServer(previous); }
    }

    private sealed class StartupLeave : ILeaveWorkflow
    {
        public bool IsConnected => true;
        public Task<IReadOnlyList<LeaveRequestView>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<LeaveRequestView>>([]);
        public Task<LeaveSubmissionReceipt> SubmitAsync(LeaveDraft draft, Guid id, CancellationToken ct) => throw new InvalidOperationException("No synthetic writes");
        public Task<LeaveDecisionReceipt> DecideAsync(LeaveRequestView row, string decision, string reason, Guid id, CancellationToken ct) => throw new InvalidOperationException("No synthetic writes");
    }
}
