using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

internal static partial class Program
{
    // These native calls address only a dialog owned by this test's MainForm on
    // this UI thread. They never dismiss windows belonging to another program.
    private delegate bool WindowVisitor(IntPtr window, IntPtr state);
    private const uint WmClose = 0x0010;
    [DllImport("user32.dll")] private static extern bool EnumThreadWindows(uint thread, WindowVisitor visitor, IntPtr state);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder name, int length);
    [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);

    private static IntPtr OwnedMessage(IntPtr owner)
    {
        IntPtr found = IntPtr.Zero;
        EnumThreadWindows(GetCurrentThreadId(), (window, _) =>
        {
            var name = new StringBuilder(64);
            GetClassName(window, name, name.Capacity);
            if (GetWindow(window, 4) == owner && name.ToString() == "#32770") found = window;
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private static async Task AuthenticationExpiryUi()
    {
        foreach (bool delayed in new[] { false, true })
            foreach (bool dismissEarly in new[] { false, true })
                await AuthenticationExpiryCase(delayed, dismissEarly);
        scenarios++;
    }

    private static async Task AuthenticationExpiryCase(bool delayed, bool dismissEarly)
    {
        string label = $"real login UI/401/{(delayed ? "delayed" : "immediate")}/{(dismissEarly ? "early" : "late")} notice";
        await using var server = new AuthFixture();
        var api = AppServices.Api;
        Check(api.CurrentUser is null, label + ": isolated session starts signed out");
        var previousUri = api.ServerUri;
        api.SetServer(server.Uri.ToString());
        using var login = new LoginForm { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
            Location = new Point(-20000, -20000) };
        var camera = new FakeCamera();
        var light = new FakeLight();
        if (delayed)
        {
            camera.StopCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            light.StopCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        using var child = new ActivityPresenceForm(camera, light)
            { StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        MainForm? main = null;
        int returnedToLogin = 0, mainClosed = 0, expiryEvents = 0;
        EventHandler expired = (_, _) => expiryEvents++;
        api.AuthenticationExpired += expired;
        try
        {
            login.Show(host);
            login.VisibleChanged += (_, _) => { if (login.Visible) returnedToLogin++; };
            Field<TextBox>(login, "txtLoginId").Text = "fixture-user";
            Field<TextBox>(login, "txtPassword").Text = "fixture-password";
            Field<Button>(login, "btnLogin").PerformClick();
            await Until(() => (main = Application.OpenForms.OfType<MainForm>().FirstOrDefault()) is not null,
                label + ": LoginForm opens MainForm through HTTP fixture");
            main!.Location = new Point(-20000, -20000);
            main.FormClosed += (_, _) => mainClosed++;
            IntPtr mainHandle = main.Handle;
            Exception? failure = null;
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var trigger = new System.Windows.Forms.Timer { Interval = 20 };
            trigger.Tick += async (_, _) =>
            {
                trigger.Stop();
                IntPtr notice = IntPtr.Zero;
                try
                {
                    Click(child, "btnStart");
                    try { await api.RefreshIdentityAsync(); throw new InvalidOperationException("Expected fixture 401"); }
                    catch (ApiException error) { Check(error.StatusCode == 401, label + $": real ApiClient receives 401 (actual {error.StatusCode})"); }
                    await Until(() => (notice = OwnedMessage(mainHandle)) != IntPtr.Zero,
                        label + ": product authentication handler shows expiry notice");
                    if (!dismissEarly)
                    {
                        camera.StopCompletion?.TrySetResult();
                        light.StopCompletion?.TrySetResult();
                        // A nested native notice can keep ShowDialog's caller
                        // on the stack even after cleanup/Close have completed.
                        await Until(() => child.DialogResult != DialogResult.None && !camera.IsRunning && !light.Running,
                            label + ": cleanup finishes before dismissing notice");
                    }
                    Check(OwnedMessage(mainHandle) == notice && PostMessage(notice, WmClose, IntPtr.Zero, IntPtr.Zero),
                        label + ": request own expiry notice close");
                    await Until(() => !IsWindow(notice), label + ": expiry notice actually closed");
                    if (delayed && dismissEarly)
                    {
                        await Task.Delay(60);
                        Check(!main.IsDisposed && !child.IsDisposed, label + ": pending cleanup defers main close");
                    }
                    camera.StopCompletion?.TrySetResult();
                    light.StopCompletion?.TrySetResult();
                    await Until(() => main.IsDisposed && login.Visible && Field<Button>(login, "btnLogin").Enabled,
                        label + ": product login screen restored");
                    Check(mainClosed == 1 && returnedToLogin == 1 && expiryEvents == 1,
                        label + ": one expiry, one main close, one login return");
                    Check(api.CurrentUser is null && camera.Disposed && light.Disposed && !camera.IsRunning && !light.Running,
                        label + ": session and both sources released");
                    Check(camera.Stops == 1 && light.Stops == 1, label + ": no duplicate cleanup");
                }
                catch (Exception error) { failure = error; }
                finally
                {
                    if (notice != IntPtr.Zero && OwnedMessage(mainHandle) == notice)
                        PostMessage(notice, WmClose, IntPtr.Zero, IntPtr.Zero);
                    camera.StopCompletion?.TrySetResult(); light.StopCompletion?.TrySetResult();
                    child.Dispose(); main.Dispose();
                    finished.TrySetResult();
                }
            };
            trigger.Start();
            child.ShowDialog(main);
            child.Dispose();
            await finished.Task;
            if (failure is not null) throw new InvalidOperationException(label, failure);
            Check(server.LoginCount == 1 && server.IdentityCount == 1, label + ": expected fixture requests only");
        }
        finally
        {
            api.AuthenticationExpired -= expired;
            main?.Dispose();
            api.ClearIdentity();
            api.SetServer(previousUri.ToString());
        }
    }

    private sealed class AuthFixture : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly Task serving;
        public Uri Uri { get; }
        public int LoginCount, IdentityCount;
        public AuthFixture()
        {
            listener.Start();
            Uri = new($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
            serving = Task.Run(Serve);
        }
        private async Task Serve()
        {
            try
            {
                while (!stop.IsCancellationRequested)
                {
                    using var peer = await listener.AcceptTcpClientAsync(stop.Token);
                    using var stream = peer.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
                    string request = await reader.ReadLineAsync(stop.Token) ?? "";
                    int length = 0;
                    while (await reader.ReadLineAsync(stop.Token) is { Length: > 0 } header)
                        if (header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase)) length = int.Parse(header[15..]);
                    // Only ASCII fixture credentials are sent; consume the body without logging it.
                    var body = new char[length];
                    if (length > 0) await reader.ReadBlockAsync(body.AsMemory(), stop.Token);
                    object payload;
                    int status = 200;
                    if (request.StartsWith("POST /api/v1/auth/login ", StringComparison.Ordinal))
                    {
                        LoginCount++;
                        payload = new { ok = true, data = new { access_token = "fixture-only-token", token_type = "bearer",
                            expires_at = DateTimeOffset.UtcNow.AddMinutes(5), user = new UserInfo(1, "fixture-user", "Fixture", "TEST-1", "개발팀", "employee", true) } };
                    }
                    else if (request.StartsWith("GET /api/v1/auth/me ", StringComparison.Ordinal))
                    {
                        IdentityCount++; status = 401;
                        payload = new { ok = false, error = new { code = "SESSION_EXPIRED", message = "fixture expiry" } };
                    }
                    else if (request.StartsWith("GET /api/v1/notices?", StringComparison.Ordinal))
                        payload = new { ok = true, data = new { items = Array.Empty<object>(), page = 1, limit = 100, total = 0 } };
                    else if (request == "GET /api/v1/notices/popups/pending?after_id=0 HTTP/1.1")
                    {
                        // Only the explicit popup-disabled contract is supported.
                        // All other unknown routes must still fail this fixture.
                        status = 404;
                        payload = new { ok = false, error = new { code = "NOT_FOUND", message = "fixture popup disabled" } };
                    }
                    else throw new InvalidOperationException("Unexpected authentication fixture route");
                    byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload, ApiClient.Json);
                    byte[] headers = Encoding.ASCII.GetBytes($"HTTP/1.1 {status} {(status == 200 ? "OK" : status == 404 ? "Not Found" : "Unauthorized")}\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
                    // Session expiry can cancel an in-flight notice read. The
                    // fixture must keep accepting the following auth request.
                    try { await stream.WriteAsync(headers, stop.Token); await stream.WriteAsync(bytes, stop.Token); }
                    catch (IOException) { }
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
        public async ValueTask DisposeAsync()
        {
            stop.Cancel(); listener.Stop();
            try { await serving; } finally { stop.Dispose(); }
        }
    }

    private static async Task<int> ReadOwnedPid(string path)
    {
        var elapsed = Stopwatch.StartNew();
        while (elapsed.ElapsedMilliseconds < 5000)
        {
            try
            {
                // File creation is visible before the Python writer has closed
                // its handle. Retry sharing/empty-write races, not cleanup itself.
                string value = await File.ReadAllTextAsync(path);
                if (int.TryParse(value, out int pid) && pid > 0)
                {
                    Check(true, "Owned unresponsive Python publishes a readable PID");
                    return pid;
                }
            }
            catch (IOException) { /* The owned writer may still hold the file. */ }
            await Task.Delay(15);
        }
        throw new InvalidOperationException("Owned unresponsive Python did not publish a readable PID within 5 seconds");
    }

    private static async Task ForcedOwnerClose()
    {
        // Deterministically hold the same exclusive writer handle that caused
        // the reviewer failure, then publish a PID without touching a process.
        string probePath = Path.Combine(Path.GetTempPath(), $"activity-pid-probe-{Guid.NewGuid():N}.pid");
        try
        {
            Task<int> reading;
            using (var writer = new FileStream(probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                reading = ReadOwnedPid(probePath);
                await Task.Delay(60);
                Check(!reading.IsCompleted, "PID reader waits while the owned writer holds the file");
                await writer.WriteAsync(Encoding.UTF8.GetBytes(Environment.ProcessId.ToString()));
            }
            Check(await reading == Environment.ProcessId, "PID reader recovers after the writer closes");
        }
        finally { File.Delete(probePath); }
        foreach (bool parentClose in new[] { false, true })
        {
            string pidPath = Path.Combine(Path.GetTempPath(), $"activity-close-{Guid.NewGuid():N}.pid");
            using var owner = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
                Location = new Point(-20000, -20000) };
            using var camera = new ProcessActivityCamera(_ => Python(
                $"import os,time\nfrom pathlib import Path\nPath({JsonSerializer.Serialize(pidPath)}).write_text(str(os.getpid()))\ntime.sleep(60)"));
            var light = new FakeLight();
            using var child = new ActivityPresenceForm(camera, light)
                { StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
            owner.Show(host);
            Exception? failure = null;
            var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var trigger = new System.Windows.Forms.Timer { Interval = 20 };
            trigger.Tick += async (_, _) =>
            {
                trigger.Stop();
                try
                {
                    Click(child, "btnStart");
                    int pid = await ReadOwnedPid(pidPath);
                    using var process = Process.GetProcessById(pid);
                    var elapsed = Stopwatch.StartNew();
                    // Reproduce expiry's child.Close -> owner.Close ordering too.
                    child.Close();
                    if (parentClose) owner.Close();
                    await Until(() => child.IsDisposed && (!parentClose || owner.IsDisposed), "Forced cleanup completes modal/owner", 6000);
                    await Until(() => process.HasExited, "Only owned unresponsive process exits after grace period");
                    Check(elapsed.Elapsed.TotalSeconds >= 1.5 && elapsed.Elapsed.TotalSeconds < 6,
                        "Owner close preserves camera grace period before forced exit");
                    Check(parentClose || (!owner.IsDisposed && owner.Visible), "Forced normal modal close preserves owner");
                    Check(!camera.IsRunning && light.Disposed && !light.Running, "Forced cleanup releases camera and Pi source");
                }
                catch (Exception error) { failure = error; }
                finally { child.Dispose(); owner.Dispose(); finished.TrySetResult(); }
            };
            try
            {
                trigger.Start(); child.ShowDialog(owner); child.Dispose();
                await finished.Task;
                if (failure is not null) throw new InvalidOperationException("Forced modal/owner close", failure);
            }
            finally { File.Delete(pidPath); }
        }
        scenarios++;
    }
}
