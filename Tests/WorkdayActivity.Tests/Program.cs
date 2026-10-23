using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;
using RemoteWork.WinForms.Ui;

internal static partial class Program
{
    private static int checks;
    private static readonly Guid Work = Guid.Parse("70000000-0000-4000-8000-000000000001");
    private static readonly Guid DeviceId = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly WorkdayActivityOptions Options = new(DeviceId, new Uri("http://pi.invalid:8080"), new());
    private static WorkdayActivityState Before => new(Work, new(2026, 10, 22), DateTimeOffset.Parse("2026-10-22T01:59:00Z"), null, false);
    private static WorkdayActivityState Due => Before with { ServerTime = Before.ServerTime.AddMinutes(1), NoonDue = true };
    private sealed class VerificationFailure(string message) : Exception(message);
    private static void Check(bool ok, string message) { checks++; if (!ok) throw new VerificationFailure(message); }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static HttpResponseMessage Reply(object data) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data }, ApiClient.Json), Encoding.UTF8, "application/json") };
    private static readonly UserInfo User = new(5, "synthetic", "시험 관리자", "TEST", "시험팀", "admin", true);
    [STAThread]
    private static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.SetError(new StreamWriter(Console.OpenStandardError(), new UTF8Encoding(false)) { AutoFlush = true });
        ApplicationConfiguration.Initialize(); Control.CheckForIllegalCrossThreadCalls = true;
        int code = 1;
        using var owner = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
        owner.Shown += async (_, _) =>
        {
            try
            {
                if (args.FirstOrDefault() == "--loopback-workday-activity") await Loopback(owner, args);
                else if (args.FirstOrDefault() == "--loopback-clockout-activity") await ClockOutLoopback(args);
                else
                {
                    await NoonAndBorrowedViews(owner); await LateAndOtherPc(owner); await WindowAndReconnect(owner); await ErrorsAndConcurrency(owner);
                    await EndedAndChangedWork(owner); await InvalidSource(owner); await ClosedWorkdayKeepsGeneralLogin(owner);
                    await MainOwnership(); await ClosedMainKeepsGeneralMenus(); await NormalCloseDrains();
                    StartupProfile(); await IndependentStartup(false, false); await IndependentStartup(true, false); await IndependentStartup(false, true);
                    await ClockOutFlow(owner, false); await ClockOutFlow(owner, true); await ClockOutFailure(owner);
                    Console.WriteLine($"PASS workday activity: {checks} assertions; real C# forms/captured sessions, synthetic reader/monitor/device; no production changes.");
                }
                code = 0;
            }
            catch (Exception e) { Console.Error.WriteLine(e); }
            finally { owner.Close(); }
        };
        Application.Run(owner); return code;
    }
    private sealed class Identity : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(Reply(new LoginResult(new string('t', 43), "Bearer", DateTimeOffset.UtcNow.AddHours(1), User)));
    }
    private static async Task<ApiClient> Login()
    {
        var api = new ApiClient("http://session.invalid", new Identity());
        await api.LoginAsync("synthetic", "synthetic"); return api;
    }
    private sealed class Source
    {
        public WorkdayActivityState State = Before;
        public int Reads; public Exception? Error;
        public TaskCompletionSource<WorkdayActivityState>? Hold;
        public Task<WorkdayActivityState> Read(CancellationToken ct)
        { Reads++; if (Error is not null) return Task.FromException<WorkdayActivityState>(Error); return Hold?.Task ?? Task.FromResult(State); }
    }
    private sealed class NoSensor : IIdentityRelayDevice
    {
        public Task<byte[]> CaptureAsync(byte[] b, Func<byte[], CancellationToken, Task<byte[]>> approval, CancellationToken ct)
            => throw new InvalidOperationException("Unit scheduler must not scan");
    }
    private sealed class Monitor : IActivityMonitor
    {
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsRunning { get; private set; }
        public bool IsStopping => false;
        public int Starts, Stops, Disposals; public ActivityMonitorContext? Context;
        public TaskCompletionSource? StopHold;
        public ActivityMonitorUpdate Latest { get; private set; } = new(Guid.Empty, 0, 0, null, ActivityMonitorPhase.Stopped, "검사 감지 꺼짐", "대역");
        public event Action<ActivityMonitorUpdate>? Changed;
        public Task StartAsync(ActivityMonitorContext context, ActivityCameraOptions camera, Uri uri)
        {
            if (IsRunning) throw new InvalidOperationException("duplicate camera");
            Starts++; Context = context; IsRunning = true;
            Latest = new(Id, Starts, Starts, context.WorkSessionId, ActivityMonitorPhase.Running, "활동중", "시험 대역");
            Changed?.Invoke(Latest); return Task.CompletedTask;
        }
        public Task StopAsync() { if (IsRunning) Stops++; IsRunning = false; return StopHold?.Task ?? Task.CompletedTask; }
        public bool IsCurrent(ActivityMonitorUpdate u) => IsRunning && u == Latest;
        public void Dispose() { Disposals++; IsRunning = false; }
    }
    private static async Task NoonAndBorrowedViews(Form owner)
    {
        using var api = await Login(); var source = new Source(); var monitor = new Monitor();
        var shown = new List<FingerprintWorkdayOperation>();
        using var host = new LoginWorkdayActivity(owner, api.CaptureSession(), source.Read, Options, monitor,
            op => { shown.Add(op); return Task.CompletedTask; }, _ => new NoSensor(), automaticallyPoll: false);
        await host.StartAsync(); await host.CheckAsync();
        Check(monitor.Starts == 1 && monitor.Context!.WorkSessionId == Work.ToString("D"), "Only server-confirmed work starts one monitor");
        source.State = Due; await host.CheckAsync(); await host.CheckAsync();
        Check(shown.Count == 1 && host.CanCheckNoon, "Before-noon to server-due opens once, still requires real confirmation");
        await host.ShowNoonAsync();
        Check(shown.Count == 2 && ReferenceEquals(shown[0], shown[1]), "Closing/reopening borrows the original operation");
        using (var activity = new ActivityPresenceForm(host.Monitor)) { activity.Show(owner); activity.Close(); }
        Check(monitor.IsRunning && monitor.Stops == 0, "Closing borrowed status view does not stop work monitoring");
        source.State = Due with { NoonAt = Due.ServerTime, NoonDue = false }; await host.CheckAsync();
        Check(!host.CanCheckNoon && host.Status.Contains("완료"), "Server noon receipt state removes request");
        await host.ShowNoonAsync(); Check(shown.Count == 2, "Another-PC/current noon completion prevents new sensor operation");
        await host.StopAsync(); Check(monitor.Stops == 1 && !host.IsCurrent, "Normal stop drains one monitor");
    }
    private static async Task LateAndOtherPc(Form owner)
    {
        using var api = await Login(); var source = new Source { State = Due }; int shown = 0;
        using var host = new LoginWorkdayActivity(owner, api.CaptureSession(), source.Read, Options, new Monitor(),
            _ => { shown++; return Task.CompletedTask; }, _ => new NoSensor(), automaticallyPoll: false);
        await host.StartAsync(); await host.CheckAsync();
        Check(shown == 1 && host.CanCheckNoon, "Initial login inside the confirmed window prompts once");
        await host.ShowNoonAsync(); Check(shown == 2, "An unfinished in-window check reopens its retained operation");
    }
    private static async Task ErrorsAndConcurrency(Form owner)
    {
        using var api = await Login(); var source = new Source(); var monitor = new Monitor();
        using var host = new LoginWorkdayActivity(owner, api.CaptureSession(), source.Read, Options, monitor, automaticallyPoll: false);
        await host.StartAsync(); source.Error = new ApiException("synthetic offline", 503);
        await host.CheckAsync();
        Check(!host.HasFreshWorkday && !monitor.IsRunning && host.Status.Contains("준비"), "Unavailable source stops monitoring instead of fabricating work");
        source.Error = null; await host.CheckAsync();
        Check(monitor.Starts == 2 && monitor.Stops == 1, "Same confirmed work resumes after completed stop");
        source.Hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int old = source.Reads; var a = host.CheckAsync(); var b = host.CheckAsync();
        Check(source.Reads == old + 1 && ReferenceEquals(a, b), "Concurrent refresh shares one original read");
        source.Hold.SetResult(Before); await Task.WhenAll(a, b);
    }
    private static async Task EndedAndChangedWork(Form owner)
    {
        using var api = await Login(); var source = new Source(); var monitor = new Monitor();
        using var host = new LoginWorkdayActivity(owner, api.CaptureSession(), source.Read, Options, monitor, automaticallyPoll: false);
        await host.StartAsync(); source.State = Before with { WorkSessionId = Guid.NewGuid() }; await host.CheckAsync();
        Check(!host.IsCurrent && !monitor.IsRunning && monitor.Starts == 1, "Different work cannot silently reuse the old monitor");
        using var secondApi = await Login(); var secondSource = new Source(); var secondMonitor = new Monitor();
        using var second = new LoginWorkdayActivity(owner, secondApi.CaptureSession(), secondSource.Read, Options, secondMonitor, automaticallyPoll: false);
        await second.StartAsync(); secondSource.Hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = second.CheckAsync(); await secondApi.LoginAsync("synthetic", "synthetic");
        await second.StopAsync(); secondSource.Hold.SetResult(Due); await pending;
        Check(!second.IsCurrent && secondMonitor.Stops == 1 && secondMonitor.Starts == 1 && secondApi.CurrentUser is not null,
            "Late old read after same-account login neither restarts camera nor clears new login");
    }
    private static async Task ClosedWorkdayKeepsGeneralLogin(Form owner)
    {
        foreach (bool alreadyMonitoring in new[] { false, true })
        {
            using var api = await Login(); var session = api.CaptureSession();
            var source = new Source(); var monitor = new Monitor(); int devices = 0, prompts = 0;
            var closed = new ApiException("synthetic closed work", 409, "ATTENDANCE_DAY_CLOSED");
            if (!alreadyMonitoring) source.Error = closed;
            using var host = new LoginWorkdayActivity(owner, session, source.Read, Options, monitor,
                _ => { prompts++; return Task.CompletedTask; }, _ => { devices++; return new NoSensor(); }, automaticallyPoll: false);
            await host.StartAsync();
            if (alreadyMonitoring) { source.Error = closed; await host.CheckAsync(); }
            Check(host.Status == "퇴근 완료 · 일반 접속 중" && host.IsWorkdayClosed,
                "Exact closed-work response is an ordinary post-clock-out login, not a connection error");
            Check(session.IsCurrent && host.IsCurrent && api.CurrentUser?.Id == User.Id,
                "Closed work keeps the same authenticated general session");
            Check(!host.HasFreshWorkday && !monitor.IsRunning && !host.CanRefresh && !host.CanCheckNoon && !host.CanClockOut,
                "Closed work cannot restart monitoring, refresh or offer new noon/clock-out work");
            Check(monitor.Starts == (alreadyMonitoring ? 1 : 0) && monitor.Stops == (alreadyMonitoring ? 1 : 0),
                "Initially closed work never opens camera; another-PC clock-out drains the existing monitor once");
            int reads = source.Reads; source.Error = null; source.State = Before;
            await host.CheckAsync(); await host.ShowNoonAsync(); await host.ShowClockOutAsync();
            Check(source.Reads == reads && !monitor.IsRunning && devices == 0 && prompts == 0,
                "Later old open data cannot resume closed work or send a new scan in the same login");
        }
        using var otherApi = await Login(); var otherSource = new Source
            { Error = new ApiException("other conflict", 409, "SYNTHETIC_OTHER_CONFLICT") };
        using var other = new LoginWorkdayActivity(owner, otherApi.CaptureSession(), otherSource.Read, Options,
            new Monitor(), automaticallyPoll: false);
        await other.StartAsync();
        Check(!other.IsWorkdayClosed && other.CanRefresh && !other.HasFreshWorkday,
            "Other 409 errors are not mislabeled as a confirmed clock-out");
    }
    private static async Task MainOwnership()
    {
        using var server = new Server(); var api = AppServices.Api; string previous = api.ServerUri.ToString();
        api.SetServer(server.Url);
        try
        {
            var login = await api.LoginAsync("synthetic", "synthetic");
            using var main = new MainForm(login.User) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
            main.Show(); var monitor = new Monitor(); ApiClient.Session? captured = null;
            await main.ConnectWorkdayActivityAsync(session => { captured = session; return Reader(session, server); }, Options, monitor);
            Check(captured?.IsCurrent == true && monitor.Starts == 1, "Actual MainForm owns one captured work monitor");
            Field<Button>(main, "btnAttendance").PerformClick();
            var page = Field<AttendanceControl>(main, "currentPage");
            Check(ReferenceEquals(Field<IActivityMonitor>(page, "activityMonitor"), monitor), "Attendance page borrows the MainForm monitor");
            Field<Button>(main, "btnChat").PerformClick();
            Check(page.IsDisposed && monitor.IsRunning && monitor.Stops == 0, "Menu disposal preserves work monitoring");
            Field<Button>(main, "btnAttendance").PerformClick();
            Check(ReferenceEquals(Field<IActivityMonitor>(Field<AttendanceControl>(main, "currentPage"), "activityMonitor"), monitor), "Returning menu keeps same monitor");
            bool refused = false;
            try { await main.ConnectWorkdayActivityAsync(_ => _ => Task.FromResult(Before), Options, new Monitor()); }
            catch (InvalidOperationException) { refused = true; }
            Check(refused, "Duplicate MainForm connection rejected before another camera");
            var host = Field<LoginWorkdayActivity>(main, "workdayActivity");
            await Task.Run(() => api.ClearIdentity()); await host.StopAsync();
            Check(monitor.Stops == 1 && !host.IsCurrent, "Background login end drains monitor on UI without deadlock");
            main.Dispose();
            Check(monitor.Disposals > 0, "MainForm disposal releases monitor resources");
            using var preview = new MainForm(); preview.Show(); bool factoryCalled = false;
            try { await preview.ConnectWorkdayActivityAsync(_ => { factoryCalled = true; return _ => Task.FromResult(Before); }, Options); }
            catch (InvalidOperationException) { }
            Check(!factoryCalled, "Preview MainForm cannot instantiate work source or camera"); preview.Dispose();
        }
        finally { api.ClearIdentity(); api.SetServer(previous); }
    }
    private static async Task InvalidSource(Form owner)
    {
        foreach (bool off in new[] { false, true })
        {
            using var api = await Login(); var monitor = new Monitor();
            var source = new Source { State = Before with { NoonDue = true }, Error = off ? new ApiException("off", 404) : null };
            using var host = new LoginWorkdayActivity(owner, api.CaptureSession(), source.Read, Options, monitor, automaticallyPoll: false);
            await host.StartAsync();
            Check(monitor.Starts == 0 && !host.HasFreshWorkday && !host.CanCheckNoon, "OFF or inconsistent noon state cannot start camera or authorize scan");
        }
    }
    private static async Task ClosedMainKeepsGeneralMenus()
    {
        using var server = new Server { WorkdayClosed = true };
        var api = AppServices.Api; string previous = api.ServerUri.ToString(); api.SetServer(server.Url);
        try
        {
            var login = await api.LoginAsync("synthetic", "synthetic"); var session = api.CaptureSession();
            using var main = new MainForm(login.User) { ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
            main.Show(); var monitor = new Monitor();
            await main.ConnectWorkdayActivityAsync(Options, monitor);
            var host = Field<LoginWorkdayActivity>(main, "workdayActivity");
            Check(host.IsWorkdayClosed && host.Status == "퇴근 완료 · 일반 접속 중" && session.IsCurrent,
                "Actual MainForm and shared HTTP reader preserve a normal login on closed-work 409");
            Field<Button>(main, "btnAttendance").PerformClick();
            Check(Field<AttendanceControl>(main, "currentPage") is not null && monitor.Starts == 0,
                "General attendance/leave menu remains available without starting a closed work camera");
            Field<Button>(main, "btnChat").PerformClick();
            Check(session.IsCurrent && server.Logouts == 0 && !monitor.IsRunning,
                "General navigation does not log out, reopen attendance or activate the monitor");
            await host.StopAsync(); main.Close();
        }
        finally { api.ClearIdentity(); api.SetServer(previous); }
    }
    private static async Task NormalCloseDrains()
    {
        using var server = new Server(); var api = AppServices.Api; string previous = api.ServerUri.ToString();
        api.SetServer(server.Url);
        try
        {
            var login = await api.LoginAsync("synthetic", "synthetic");
            using var main = new MainForm(login.User) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
            main.Show(); var monitor = new Monitor { StopHold = new(TaskCreationOptions.RunContinuationsAsynchronously) };
            await main.ConnectWorkdayActivityAsync(session => Reader(session, server), Options, monitor);
            main.Close();
            Check(server.Logouts == 0 && !main.IsDisposed && monitor.Stops == 1, "Normal close waits for monitor drain before logout");
            monitor.StopHold.SetResult();
            var until = DateTime.UtcNow.AddSeconds(5);
            while (!main.IsDisposed && DateTime.UtcNow < until) await Task.Delay(10);
            Check(main.IsDisposed && server.Logouts == 1, "Original normal logout completes after owned monitor drain");
        }
        finally { api.ClearIdentity(); api.SetServer(previous); }
    }
    private static Func<CancellationToken, Task<WorkdayActivityState>> Reader(ApiClient.Session session, Server server)
    {
        var reader = new WorkdayStateConnection(session);
        return async ct => { var s = await reader.ReadAsync(ct); return new(s.WorkSessionId, s.WorkDate, s.ServerTime, s.NoonAt, s.NoonDue); };
    }
    private sealed class Server : IDisposable
    {
        private readonly HttpListener listener = new(); private readonly Task run;
        public string Url { get; }
        public int Logouts;
        public bool WorkdayClosed;
        public Server()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); int port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
            Url = $"http://127.0.0.1:{port}/"; listener.Prefixes.Add(Url); listener.Start(); run = Task.Run(Serve);
        }
        private async Task Serve()
        {
            try
            {
                while (listener.IsListening)
                {
                    var c = await listener.GetContextAsync(); object data;
                    string path = c.Request.Url!.AbsolutePath;
                    if (path.EndsWith("/workday") && WorkdayClosed)
                    {
                        byte[] error = JsonSerializer.SerializeToUtf8Bytes(new { ok = false,
                            error = new { code = "ATTENDANCE_DAY_CLOSED", message = "synthetic closed work" } }, ApiClient.Json);
                        c.Response.StatusCode = 409; c.Response.ContentType = "application/json";
                        c.Response.ContentLength64 = error.Length;
                        await c.Response.OutputStream.WriteAsync(error); c.Response.Close(); continue;
                    }
                    if (path.EndsWith("/auth/login")) data = new LoginResult(new string('x', 43), "Bearer", DateTimeOffset.UtcNow.AddHours(1), User);
                    else if (path.EndsWith("/auth/me")) data = new IdentityResult(User, DateTimeOffset.UtcNow.AddHours(1));
                    else if (path.EndsWith("/workday")) data = new { server_time = "2026-10-22T01:59:00Z", work_session_ref = Work, work_date = "2026-10-22",
                        state = "open", started_at = "2026-10-22T01:00:00Z", noon_at = (string?)null, noon_due = false };
                    else if (path.EndsWith("/auth/logout")) { Interlocked.Increment(ref Logouts); data = new MessageResult("signed out"); }
                    else data = new { items = Array.Empty<object>() };
                    byte[] body = JsonSerializer.SerializeToUtf8Bytes(new { ok = true, data }, ApiClient.Json);
                    c.Response.ContentType = "application/json"; c.Response.ContentLength64 = body.Length;
                    await c.Response.OutputStream.WriteAsync(body); c.Response.Close();
                }
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException) { }
        }
        public void Dispose() { listener.Stop(); listener.Close(); run.GetAwaiter().GetResult(); }
    }
}
