using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
using System.Net;
using System.Net.Sockets;
using System.Text;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Ui;

internal static class Program
{
    private static int checks;
    private static int scenarios;
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static byte[] jpeg = null!;
    private static Form host = null!;
    private static string? renderPath;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--pi-check")
        {
            using var client = new LightMotionClient();
            var uri = LightMotionClient.ParseBaseUri(args[1]);
            var first = client.ReadAsync(uri).GetAwaiter().GetResult();
            Thread.Sleep(1000);
            var second = client.ReadAsync(uri).GetAwaiter().GetResult();
            bool fresh = first.Available && second.Available && first.ObservationId != second.ObservationId;
            Console.WriteLine($"Pi GET /motion: available={second.Available}; observation_advanced={fresh}; response_age={second.CurrentAge:F2}s");
            return fresh ? 0 : 1;
        }
        renderPath = args.Length == 2 && args[0] == "--render" ? args[1] : null;
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Control.CheckForIllegalCrossThreadCalls = true;
        using var pixels = new Bitmap(160, 90);
        using (var drawing = Graphics.FromImage(pixels)) drawing.Clear(Color.SteelBlue);
        using var stream = new MemoryStream(); pixels.Save(stream, ImageFormat.Jpeg); jpeg = stream.ToArray();
        int exit = 1;
        using var coordinator = new Form { ShowInTaskbar = false, Location = new Point(-20000, -20000),
            StartPosition = FormStartPosition.Manual, Size = new Size(1100, 760) };
        host = coordinator;
        coordinator.Shown += async (_, _) =>
        {
            try
            {
                await UiFlow();
                await FailureAndClose();
                await Transport();
                PresenceRules();
                await LightHttp();
                await CombinedUi();
                Protocol();
                AttendanceEntry();
                Console.WriteLine($"PASS: {checks} assertions; {scenarios} scenarios. Synthetic images, injected camera, real Python stdio and loopback HTTP. No physical camera/external devices/common server/DB.");
                exit = 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); }
            finally { coordinator.Close(); }
        };
        Application.Run(coordinator);
        return exit;
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); checks++; }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden)!.GetValue(owner)!;
    private static void Poll(ActivityPresenceForm form) => form.GetType().GetMethod("refreshTimer_Tick", Hidden)!.Invoke(form, new object?[] { null, EventArgs.Empty });
    private static string Status(ActivityPresenceForm form) => Field<Label>(form, "lblStatus").Text;
    private static void Click(ActivityPresenceForm form, string button) => Field<Button>(form, button).PerformClick();
    private static ActivityPresenceForm Mount(FakeCamera fake)
    {
        var form = new ActivityPresenceForm(fake, new FakeLight()) { StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        form.Show(host); return form;
    }
    private static async Task Until(Func<bool> ready, string label, int timeout = 5000)
    {
        var clock = Stopwatch.StartNew();
        bool completed;
        while (!(completed = ready()) && clock.ElapsedMilliseconds < timeout) await Task.Delay(15);
        Check(completed, label);
    }

    private static async Task UiFlow()
    {
        var fake = new FakeCamera();
        using var form = Mount(fake);
        Check(fake.Starts == 0 && Status(form) == "카메라 꺼짐", "Opening form never starts capture");
        Field<NumericUpDown>(form, "numRight").Value = 0;
        Click(form, "btnStart");
        Check(fake.Starts == 0 && Status(form) == "카메라 확인 필요", "Reject invalid crop before child starts");
        Field<NumericUpDown>(form, "numRight").Value = 100;
        Click(form, "btnStart");
        Check(fake.Starts == 1 && Status(form) == "확인 대기", "Explicit Start");
        fake.Frame = new("STARTING", Error: "인식 모델 준비 중"); Poll(form);
        Check(Status(form) == "확인 대기" && Field<Label>(form, "lblDetails").Text == "인식 모델 준비 중",
            "Startup stage is shown without a preview");
        Check(!Field<FlowLayoutPanel>(form, "regionOptions").Enabled, "Changing crop requires Stop");
        fake.Frame = new("HAND_DETECTED", Jpeg: jpeg); Poll(form);
        Check(Status(form) == "활동 중" && Field<PictureBox>(form, "preview").Image is not null, "Fresh frame renders");
        if (renderPath is not null)
        {
            using var snapshot = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(snapshot, new Rectangle(Point.Empty, form.Size)); snapshot.Save(renderPath);
        }
        fake.Frame = new("HAND_DETECTED", AgeSeconds: 10, Jpeg: jpeg); Poll(form);
        Check(Status(form) == "카메라 연결 지연" && Field<PictureBox>(form, "preview").Image is null, "Late inference cannot revive preview");
        fake.Frame = new("HAND_DETECTED", Jpeg: jpeg) { ReceivedAt = Stopwatch.GetTimestamp() - Stopwatch.Frequency * 10 }; Poll(form);
        Check(Status(form) == "카메라 연결 지연", "C# queue delay counted");
        fake.Frame = new("HAND_NOT_DETECTED", SecondsWithoutHand: 2.5, Jpeg: jpeg); Poll(form);
        Check(Status(form) == "활동 중", "Missing hand alone does not immediately become away");
        Check(Field<Label>(form, "lblDetails").Text.Contains("자리 비움까지"), "Combined grace countdown shown");
        fake.Frame = new("HAND_DETECTED", Jpeg: new byte[] { 1, 2 }); Poll(form);
        Check(Status(form) == "카메라 확인 필요" && Field<PictureBox>(form, "preview").Image is null, "Invalid JPEG clears image");
        fake.Frame = new("HAND_DETECTED", Jpeg: jpeg); Poll(form);
        Click(form, "btnStop");
        Check(Status(form) == "카메라 꺼짐" && fake.Stops == 1, "Stop releases source");
        fake.Frame = new("HAND_DETECTED", Jpeg: jpeg); Poll(form);
        Check(Status(form) == "카메라 꺼짐", "Late post-stop frame ignored");
        Click(form, "btnStart");
        Check(fake.Starts == 2 && Status(form) == "확인 대기", "Restart clears old frame");
        form.Close(); await Until(() => form.IsDisposed, "Close completes");
        Check(fake.Disposed && !fake.IsRunning, "Form owns source lifetime");
        scenarios++;
    }

    private static async Task FailureAndClose()
    {
        var fake = new FakeCamera();
        using var form = Mount(fake);
        Click(form, "btnStart");
        fake.Frame = new("CAMERA_ERROR", Error: "카메라 영상이 끊겼습니다."); Poll(form);
        Check(Status(form) == "카메라 연결 끊김" && fake.Stops == 1, "Error is distinct from missing hand and stops child");
        Check(Field<Button>(form, "btnStart").Enabled, "Restart enabled after cleanup");
        Click(form, "btnStart");
        fake.StopCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Click(form, "btnStop");
        Check(Status(form) == "카메라 종료 중" && !Field<Button>(form, "btnStart").Enabled, "Cannot overlap captures during stop");
        form.Close();
        Check(!form.IsDisposed, "Close waits for cleanup without blocking UI");
        fake.StopCompletion.SetResult();
        await Until(() => form.IsDisposed, "Close during pending stop");
        scenarios++;
    }

    private static ProcessStartInfo Python(string script)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Tools", "HandPresence", "winforms_bridge.py"))) directory = directory.Parent;
        if (directory is null) throw new DirectoryNotFoundException("repository");
        var start = new ProcessStartInfo(Path.Combine(directory.FullName, "Tools", "HandPresence", ".venv", "Scripts", "python.exe"));
        start.ArgumentList.Add("-u"); start.ArgumentList.Add("-c"); start.ArgumentList.Add(script);
        return start;
    }

    private static async Task Transport()
    {
        // A cold child is essential: importing NumPy in this process or test doubles masked
        // the Windows native import/stdin-reader startup stall in the initial tests.
        var tools = Path.GetDirectoryName(Python("").FileName)!.Replace(".venv\\Scripts", "");
        string coldScript = "import sys,runpy\n" +
            $"sys.path.insert(0,{JsonSerializer.Serialize(tools)})\n" +
            "import hand_presence\nOriginal=hand_presence.CameraWorker\n" +
            "class Camera:\n def isOpened(self): return False\n def release(self): pass\n" +
            "class Detector:\n def close(self): pass\n" +
            "hand_presence.CameraWorker=lambda *args,**kw: Original(*args,**kw,camera_factory=lambda *a:Camera(),detector_factory=Detector)\n" +
            $"runpy.run_path({JsonSerializer.Serialize(Path.Combine(tools, "winforms_bridge.py"))},run_name='__main__')\n";
        using (var cold = new ProcessActivityCamera(_ => Python(coldScript)))
        {
            cold.Start(new());
            ActivityCameraFrame? result = null;
            await Until(() => { result = cold.TakeLatest() ?? result; return result?.State == "CAMERA_ERROR"; },
                "Cold production bridge initializes native libraries before stdin thread; fake camera fails promptly", 8000);
            Check(result!.Error!.Contains("카메라를 열 수 없습니다"), "Cold child returned worker error, not a parser/exit error");
            await cold.StopAsync();
        }
        string script = "import sys,json,time\nsys.stderr.write('x'*100000);sys.stderr.flush()\n" +
            $"image='{Convert.ToBase64String(jpeg)}'\n" +
            "for i in range(30):\n print(json.dumps(dict(version=2,state='HAND_NOT_DETECTED',camera_motion=False,age_seconds=0,observed_seconds=time.monotonic(),seconds_without_hand=i,jpeg=image)),flush=True)\n" +
            "sys.stdin.readline()\n";
        using var source = new ProcessActivityCamera(_ => Python(script));
        Check(!source.IsRunning && source.TakeLatest() is null, "Constructing transport starts no process");
        source.Start(new());
        ActivityCameraFrame? latest = null;
        await Until(() => { latest = source.TakeLatest() ?? latest; return latest?.SecondsWithoutHand == 29; }, "Concurrent stderr drain and latest frame delivery");
        Check(latest!.CurrentAge < 5 && latest.Jpeg!.SequenceEqual(jpeg), "Windows Python/C# clock and JPEG protocol");
        await source.StopAsync();
        Check(!source.IsRunning && source.TakeLatest() is null, "Private child exits on stop without stale response");
        source.Start(new());
        await source.StopAsync();
        Check(!source.IsRunning, "Immediate stop/restart cleans up");
        using var crashed = new ProcessActivityCamera(_ => Python("raise RuntimeError('synthetic crash')"));
        crashed.Start(new());
        ActivityCameraFrame? failure = null;
        await Until(() => { failure = crashed.TakeLatest() ?? failure; return failure?.State == "CAMERA_ERROR"; }, "Unexpected child exit shown as error");
        await crashed.StopAsync();
        using var stalled = new ProcessActivityCamera(_ => Python("import time\ntime.sleep(60)"));
        stalled.Start(new());
        var clock = Stopwatch.StartNew();
        await stalled.StopAsync();
        Check(!stalled.IsRunning && clock.Elapsed.TotalSeconds < 5, "Unresponsive owned child killed after grace period");
        scenarios++;
    }

    private static void Protocol()
    {
        string Json(double observed, string state = "HAND_DETECTED", double age = 0) => JsonSerializer.Serialize(new
        { version = 2, state, camera_motion = false, age_seconds = age, observed_seconds = observed, jpeg = Convert.ToBase64String(jpeg) });
        double now = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        Check(ActivityCameraFrame.Parse(Json(now - 10)).CurrentAge >= 10, "Pipe delay counted from capture QPC, not receipt time");
        Check(ActivityCameraFrame.Parse(Json(now, age: 10)).Jpeg is null, "Old packet does not retain JPEG");
        foreach (var invalid in new[] { Json(now + 100), Json(now, "unknown"), Json(now, age: -1), "{}", "bad json" })
        {
            bool rejected = false;
            try { ActivityCameraFrame.Parse(invalid); } catch { rejected = true; }
            Check(rejected, "Reject malformed protocol");
        }
        scenarios++;
    }

    private static void AttendanceEntry()
    {
        foreach (var role in new[] { PreviewRole.Employee, PreviewRole.Administrator })
        {
            using var page = new AttendanceControl(role);
            page.Dock = DockStyle.Fill;
            host.Controls.Add(page);
            page.PerformLayout();
            var actions = Field<FlowLayoutPanel>(page, "flowActions");
            var button = Field<Button>(page, "btnActivityPresence");
            Check(actions.Controls.Contains(button) && button.Text == "활동 상태 확인", "Button is in existing Designer action row");
            Check(Field<DataGridView>(page, "gridAttendance").Rows.Count == 3, "Attendance samples unchanged");
            Check(Field<Button>(page, "btnLeaveApplicationPreview").Visible == (role == PreviewRole.Employee), "Existing employee leave action preserved");
            Check(Field<Button>(page, "btnLeaveDecision").Visible == (role == PreviewRole.Administrator), "Existing administrator review action preserved");
            bool opened = false;
            using var timer = new System.Windows.Forms.Timer { Interval = 20 };
            timer.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.OfType<ActivityPresenceForm>().FirstOrDefault();
                if (dialog is null) return;
                timer.Stop();
                opened = dialog.Modal && dialog.Owner == host && Status(dialog) == "카메라 꺼짐";
                dialog.Close();
            };
            timer.Start(); button.PerformClick(); timer.Stop();
            Check(opened, "Real attendance handler opens owned modal without starting camera");
            Check(!Application.OpenForms.OfType<ActivityPresenceForm>().Any(), "Modal form disposed after closing");
        }
        scenarios++;
    }

    private static void PresenceRules()
    {
        var tracker = new ActivityPresenceTracker();
        ActivityPresenceView Feed(double t, bool hand = false, bool motion = false,
            string? pulse = null, double? age = null, string? observation = null)
        {
            tracker.ObserveCamera(hand, motion, t);
            tracker.ObserveLight(new(true, observation ?? t.ToString(System.Globalization.CultureInfo.InvariantCulture), pulse, age), t);
            return tracker.Evaluate(t);
        }
        for (int t = 0; t < 30; t++) Check(Feed(t).Status == "활동 중", "All-negative grace holds before 30 seconds");
        Check(Feed(30).Status == "자리 비움", "Exactly 30 seconds becomes away");
        Check(Feed(31, hand: true).Status == "활동 중", "Hand recovers immediately");
        for (int t = 32; t <= 60; t++) Feed(t);
        Check(Feed(61).Status == "자리 비움", "Hand removal uses the last hand observation");
        Check(Feed(62, motion: true).Status == "활동 중", "Camera movement recovers without a hand");
        for (int t = 63; t <= 91; t++) Feed(t);
        Check(Feed(92).Status == "자리 비움", "Camera pulse expires after 30 seconds");
        Check(Feed(93, pulse: "pulse1", age: 0).Status == "활동 중", "Pi movement recovers alone");
        for (int t = 94; t <= 122; t++) Feed(t, pulse: "pulse1", age: t - 93);
        Check(Feed(123, pulse: "pulse1", age: 30).Status == "자리 비움", "Repeated Pi recent result does not add ten seconds");
        Check(Feed(124, pulse: "pulse2", age: 0).Status == "활동 중", "A new Pi event resets grace");
        tracker.ObserveLight(new(false, Error: "disconnected"), 125);
        tracker.ObserveCamera(true, true, 125);
        Check(tracker.Evaluate(125).Status == "확인 필요", "Disconnection dominates even positive camera evidence");
        Check(Feed(126).QuietSeconds == 0, "Recovery begins a new continuous interval");
        for (int t = 127; t <= 155; t++) Feed(t);
        Check(Feed(156).Status == "자리 비움", "Unknown time never counts toward away");
        tracker.Reset();
        for (int t = 0; t <= 4; t++) Feed(t, observation: "frozen");
        Check(tracker.Evaluate(4).Status == "확인 필요", "A frozen observed_at is stale despite repeated successful HTTP");
        tracker.Reset();
        Feed(0);
        for (int t = 1; t <= 29; t++) Feed(t);
        tracker.InvalidateCamera();
        Check(tracker.Evaluate(30).Status == "확인 필요", "Camera failure never turns into away");
        Check(Feed(31).QuietSeconds == 0, "Camera recovery resets quiet time");
        tracker.Reset(); Feed(0);
        for (int t = 1; t <= 29; t++) Feed(t);
        Check(Feed(60).QuietSeconds == 0, "Application/UI pause cannot count as healthy observation");
        tracker.Reset(); Feed(0);
        for (int t = 1; t <= 29; t++) Feed(t);
        tracker.ObserveCamera(false, false, 30, secondsSinceMovement: 1);
        tracker.ObserveLight(new(true, "30"), 30);
        Check(tracker.Evaluate(30).Status == "활동 중", "A camera pulse survives a dropped intermediate UI frame");
        tracker.Reset(); Feed(0);
        for (int t = 1; t <= 29; t++) Feed(t);
        tracker.ObserveCamera(false, false, 30);
        tracker.ObserveLight(new(true, "30") { Continuity = 1 }, 30);
        Check(tracker.Evaluate(30).QuietSeconds == 0, "A dropped intermediate Pi error still breaks continuous quiet time");
        scenarios++;
    }

    private static string MotionBody(string state = "CLEAR", string observed = "2026-10-19T10:00:00.000001+09:00",
        string? motion = null, double? age = null) => JsonSerializer.Serialize(new
        {
            success = true, data = new { available = true, connected = true, state, observed_at = observed,
                last_motion_at = motion, seconds_since_motion = age, recent_motion = true }
        });

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> action) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Task.FromResult(action(request));
    }

    private sealed class PendingHandler : HttpMessageHandler
    {
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, token);
            return new(HttpStatusCode.OK);
        }
    }

    private static async Task LightHttp()
    {
        var uri = LightMotionClient.ParseBaseUri("http://raspberrypi.local:8080");
        using (var client = new LightMotionClient(new Handler(request =>
        {
            Check(request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/motion", "Read-only Pi motion endpoint");
            Check(request.Headers.Authorization is null, "No common user credentials sent to Pi");
            return new(HttpStatusCode.OK) { Content = new StringContent(MotionBody(motion: "2026-10-19T09:59:55+09:00", age: 5)) };
        })))
        {
            var sample = await client.ReadAsync(uri);
            Check(sample.Available && sample.MotionId is not null && sample.SecondsSinceMotion == 5, "Parse timestamp and event age independently of recent_motion");
        }
        foreach (var status in new[] { HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK })
        {
            using var client = new LightMotionClient(new Handler(_ => new(status) { Content = new StringContent(MotionBody(state: "LEVEL_UNKNOWN")) }));
            Check(!(await client.ReadAsync(uri)).Available, "503/unknown level is unavailable, not no movement");
        }
        foreach (var body in new[] { "{}", MotionBody(observed: "2026-10-19"), MotionBody(age: 1),
            MotionBody(motion: "2026-10-19T10:00:00+09:00", age: -1), new string('x', 65537) })
        {
            using var client = new LightMotionClient(new Handler(_ => new(HttpStatusCode.OK) { Content = new StringContent(body) }));
            bool rejected = false; try { await client.ReadAsync(uri); } catch { rejected = true; }
            Check(rejected, "Reject malformed/oversized Pi response");
        }
        foreach (var address in new[] { "file:///test", "http://user:secret@localhost:8080", "http://localhost:8080/path", "http://localhost:8080/?key=x" })
        {
            bool rejected = false; try { LightMotionClient.ParseBaseUri(address); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Reject credential/path configuration");
        }
        using (var source = new PollingLightMotionSource(new LightMotionClient(new Handler(_ =>
            new(HttpStatusCode.OK) { Content = new StringContent(MotionBody()) }))))
        {
            source.Start(uri);
            await Until(() => source.TakeLatest()?.Available == true, "Real poller publishes a sample");
            await source.StopAsync();
            Check(source.TakeLatest() is null, "Stopped poller clears pending sample");
            source.Start(uri);
            await Until(() => source.TakeLatest()?.Available == true, "Stopped poller can restart");
            await source.StopAsync();
        }
        var pending = new PendingHandler();
        using (var source = new PollingLightMotionSource(new LightMotionClient(pending)))
        {
            source.Start(uri);
            await pending.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await source.StopAsync().WaitAsync(TimeSpan.FromSeconds(1));
            Check(source.TakeLatest() is null, "Closing source cancels in-flight HTTP without a late update");
        }
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = Task.Run(async () =>
            {
                using var peer = await listener.AcceptTcpClientAsync();
                using var stream = peer.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                Check(await reader.ReadLineAsync() == "GET /motion HTTP/1.1", "Real TCP request path");
                while (await reader.ReadLineAsync() is { Length: > 0 } header)
                    Check(!header.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase), "No auth over real TCP");
                byte[] body = Encoding.UTF8.GetBytes(MotionBody());
                byte[] headers = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(headers); await stream.WriteAsync(body);
            });
            using var client = new LightMotionClient();
            Check((await client.ReadAsync(new Uri($"http://127.0.0.1:{port}"))).Available, "Actual local HTTP parses Pi response");
            await server;
        }
        finally { listener.Stop(); }
        scenarios++;
    }

    private static async Task CombinedUi()
    {
        double start = Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
        double now = start;
        var camera = new FakeCamera();
        var light = new FakeLight { Factory = () => new(true, now.ToString(System.Globalization.CultureInfo.InvariantCulture))
            { ReceivedAt = (long)(now * Stopwatch.Frequency) } };
        using var form = new ActivityPresenceForm(camera, light, () => now)
            { StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        form.Show(host); Click(form, "btnStart");
        for (int t = 0; t <= 30; t++)
        {
            now = start + t;
            camera.Frame = new("HAND_NOT_DETECTED", Jpeg: jpeg);
            Poll(form);
            Check(Status(form) == (t < 30 ? "활동 중" : "자리 비움"), "Real WinForms combined 30-second boundary");
        }
        now = start + 31;
        camera.Frame = new("HAND_NOT_DETECTED", Jpeg: jpeg) { MotionDetected = true }; Poll(form);
        Check(Status(form) == "활동 중", "Camera motion wired to actual form");
        now = start + 32; light.Next = new(false, Error: "센서 연결 없음");
        camera.Frame = new("HAND_DETECTED", Jpeg: jpeg); Poll(form);
        Check(Status(form) == "확인 필요" && Field<PictureBox>(form, "preview").Image is not null, "Sensor error shown while healthy camera preview remains visible");
        now = start + 33;
        camera.Frame = new("HAND_NOT_DETECTED", Jpeg: jpeg); Poll(form);
        Check(Status(form) == "활동 중", "Fresh light response recovers with a new grace interval");
        Click(form, "btnStop");
        Check(!light.Running && light.Stops == 1, "Stop releases light polling too");
        form.Close(); await Until(() => form.IsDisposed, "Combined form closes");
        Check(light.Disposed, "Combined form disposes Pi source");
        scenarios++;
    }

    private sealed class FakeCamera : IActivityCamera
    {
        public int Starts, Stops;
        public bool Disposed;
        public bool IsRunning { get; private set; }
        public ActivityCameraFrame? Frame;
        public TaskCompletionSource? StopCompletion;
        public void Start(ActivityCameraOptions options) { Starts++; IsRunning = true; Frame = null; }
        public ActivityCameraFrame? TakeLatest() { var frame = Frame; Frame = null; return frame; }
        public async Task StopAsync() { Stops++; if (StopCompletion is not null) await StopCompletion.Task; IsRunning = false; }
        public void Dispose() { Disposed = true; IsRunning = false; Frame = null; }
    }

    private sealed class FakeLight : ILightMotionSource
    {
        public bool Running, Disposed;
        public int Stops;
        public LightMotionSample? Next;
        public Func<LightMotionSample> Factory = () => new(true, Guid.NewGuid().ToString());
        public void Start(Uri uri) { Running = true; Next = null; }
        public LightMotionSample? TakeLatest()
        {
            if (!Running) return null;
            var sample = Next ?? Factory(); Next = null; return sample;
        }
        public Task StopAsync() { Running = false; Stops++; return Task.CompletedTask; }
        public void Dispose() { Running = false; Disposed = true; }
    }
}
