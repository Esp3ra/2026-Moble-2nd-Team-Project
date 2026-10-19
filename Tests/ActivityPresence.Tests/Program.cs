using System.Diagnostics;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json;
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
                Protocol();
                AttendanceEntry();
                Console.WriteLine($"PASS: {checks} assertions; {scenarios} scenarios. Synthetic images, injected camera, real Python stdio. No physical camera/network/DB.");
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
        var form = new ActivityPresenceForm(fake) { StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        form.Show(host); return form;
    }
    private static async Task Until(Func<bool> ready, string label, int timeout = 5000)
    {
        var clock = Stopwatch.StartNew();
        while (!ready() && clock.ElapsedMilliseconds < timeout) await Task.Delay(15);
        Check(ready(), label);
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
        Check(Status(form) == "자리 비움", "Fresh missing hand recovers without attendance write");
        Check(Field<Label>(form, "lblDetails").Text.Contains("2.5"), "Missing duration shown");
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
            "for i in range(30):\n print(json.dumps(dict(version=1,state='HAND_NOT_DETECTED',age_seconds=0,observed_seconds=time.monotonic(),seconds_without_hand=i,jpeg=image)),flush=True)\n" +
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
        { version = 1, state, age_seconds = age, observed_seconds = observed, jpeg = Convert.ToBase64String(jpeg) });
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
}
