using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private const string Device = "123e4567-e89b-42d3-a456-426614174000";
    private const string JobId = "31593412-67fd-40d2-878b-868d15f99a11";
    private const string Opaque = "cHVibGljLXN5bnRoZXRpYw";
    private static int checks;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
    private static IEnumerable<Control> Controls(Control owner)
    {
        foreach (Control child in owner.Controls)
        { yield return child; foreach (var descendant in Controls(child)) yield return descendant; }
    }
    private static void Pump(Func<bool> done)
    {
        var timer = Stopwatch.StartNew();
        while (!done()) { Application.DoEvents(); Thread.Sleep(5); if (timer.Elapsed.TotalSeconds > 8) throw new TimeoutException("UI check"); }
        Application.DoEvents();
    }
    [STAThread]
    private static void Main()
    {
        Control.CheckForIllegalCrossThreadCalls = true;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        CheckFlow(false); CheckFlow(true); CheckLogoutClosesView(); CheckDuplicateFinger();
        Console.WriteLine($"Fingerprint enrollment UI: {checks} checks PASS (real WinForms + synthetic HTTP; no devices).");
    }
    private static void CheckFlow(bool closeEarly)
    {
        var server = new Server(); using var api = new ApiClient("http://127.0.0.1:5000", server);
        api.LoginAsync("admin", "synthetic").GetAwaiter().GetResult();
        using var cancellation = new CancellationTokenSource();
        var target = new EnrollmentTarget(88, api.CaptureSession(), () => !cancellation.IsCancellationRequested, cancellation.Token);
        var pi = new Pi(); using var form = new FingerprintEnrollmentForm(target, pi) { ShowInTaskbar = false, Opacity = 0 };
        form.Show();
        var start = Controls(form).OfType<Button>().Single(b => b.Text == "지문 등록");
        Pump(() => start.Enabled);
        Check(Controls(form).OfType<Label>().Any(l => l.Text.Contains("88")), "fixed created-user target visible");
        start.PerformClick(); Pump(() => pi.Calls == 1);
        Check(!start.Enabled, "duplicate start disabled");
        Check(!Controls(form).OfType<ComboBox>().Single().Enabled, "device pinned after start");
        if (closeEarly)
        {
            cancellation.Cancel(); form.Close(); pi.Ready.SetResult(Opaque);
            Pump(() => pi.Returned);
            for (int i = 0; i < 15; i++) { Application.DoEvents(); Thread.Sleep(5); }
            Check(server.Completes == 0 && !form.RegistrationCompleted, "closed/changed target suppresses late completion");
        }
        else
        {
            Check(!form.RegistrationCompleted, "sensor response alone not completed");
            pi.Ready.SetResult(Opaque);
            Pump(() => form.RegistrationCompleted);
            Check(server.Completes == 1, "server commit required");
            Check(Controls(form).OfType<Label>().Any(l => l.Text == "지문 등록 완료"), "completed status shown");
            Check(!start.Enabled, "completed operation cannot restart");
            form.Close();
        }
    }
    private sealed class Pi : IEnrollmentPiChannel
    {
        public int Calls;
        public volatile bool Returned;
        public TaskCompletionSource<string> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<string> TransferAsync(EnrollmentJob job, bool readOnly,
            Func<JsonElement, CancellationToken, Task<string>> approve, IProgress<string>? progress, CancellationToken cancellation)
        { Calls++; var value = await Ready.Task.ConfigureAwait(false); Returned = true; return value; }
    }
    private static void CheckDuplicateFinger()
    {
        var server = new Server(); using var api = new ApiClient("http://127.0.0.1:5000", server);
        api.LoginAsync("admin", "synthetic").GetAwaiter().GetResult();
        var pi = new Pi();
        using var form = new FingerprintEnrollmentForm(new EnrollmentTarget(88, api.CaptureSession(), () => true, CancellationToken.None), pi)
        { ShowInTaskbar = false, Opacity = 0 };
        form.Show();
        var start = Controls(form).OfType<Button>().Single(b => b.Text == "지문 등록");
        Pump(() => start.Enabled); start.PerformClick(); Pump(() => pi.Calls == 1);
        pi.Ready.SetException(new EnrollmentPiFailureException("already_registered"));
        Pump(() => start.Text == "현재 작업 확인 필요");
        Check(Controls(form).OfType<Label>().Any(l => l.Text.Contains("이미 등록된 지문")), "duplicate failure is visible in the real form");
        Check(!start.Enabled && !Controls(form).OfType<ComboBox>().Single().Enabled, "duplicate failure cannot start a fresh scan or switch devices");
        start.PerformClick(); Application.DoEvents();
        Check(pi.Calls == 1 && server.Completes == 0 && !form.RegistrationCompleted, "failure never completes or rescans");
        form.Close();
    }
    private static void CheckLogoutClosesView()
    {
        var server = new Server(); using var api = new ApiClient("http://127.0.0.1:5000", server);
        api.LoginAsync("admin", "synthetic").GetAwaiter().GetResult();
        var target = new EnrollmentTarget(88, api.CaptureSession(), () => true, CancellationToken.None);
        var pi = new Pi(); using var form = new FingerprintEnrollmentForm(target, pi) { ShowInTaskbar = false, Opacity = 0 };
        form.Show();
        var start = Controls(form).OfType<Button>().Single(b => b.Text == "지문 등록");
        Pump(() => start.Enabled);
        start.PerformClick(); Pump(() => pi.Calls == 1);
        api.ClearIdentity(); Pump(() => form.IsDisposed);
        pi.Ready.SetResult(Opaque); Pump(() => pi.Returned);
        for (int i = 0; i < 15; i++) { Application.DoEvents(); Thread.Sleep(5); }
        Check(server.Completes == 0 && !form.RegistrationCompleted, "logout closes registration view and rejects late result");
    }
    private sealed class Server : HttpMessageHandler
    {
        private string clientId = "";
        private string? physicalRaw;
        public int Completes;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath;
            object data;
            if (path.EndsWith("auth/login")) data = new { access_token = "public-synthetic", token_type = "Bearer", expires_at = DateTimeOffset.UtcNow.AddHours(1),
                user = new UserInfo(1, "admin", "가상 관리자", "QA", "QA", "admin", true) };
            else if (path.Contains("eligible-devices")) data = new EnrollmentDevices([new EnrollmentDevice(Device, "가상 장치", "1")]);
            else
            {
                using var body = JsonDocument.Parse(request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(cancellationToken));
                if (path.EndsWith("/device-enrollments")) clientId = body.RootElement.GetProperty("client_request_id").GetString()!;
                if (path.EndsWith("/physical-result"))
                {
                    if (request.Method == HttpMethod.Post) physicalRaw = body.RootElement.GetProperty("result_envelope").GetString();
                    data = new { enrollment_id = JobId, operation_revision = "1", reserved_slot = 7, claim_revision = "1", key_epoch = "1",
                        contract = "enrollment-mysql-v1", phase = physicalRaw is null ? "not_recorded" : "result_recorded",
                        result_envelope = physicalRaw, result_id = physicalRaw is null ? null : "af7de985-1df9-4e65-a310-dd86fa4c6862",
                        outcome = physicalRaw is null ? null : "sensor_stored", failure_code = (string?)null,
                        intent_at = physicalRaw is null ? null : "2026-10-20T01:01:00Z",
                        reported_at = physicalRaw is null ? null : "2026-10-20T01:01:01Z", may_store = false, approval_envelope = (string?)null };
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data }, ApiClient.Json), Encoding.UTF8, "application/json") };
                }
                if (path.EndsWith("/complete")) Completes++;
                data = new EnrollmentJob(JobId, "1", 7, "1", "1", clientId, Device, Completes > 0 ? "completed" : "issued",
                    "2026-10-20T01:03:00Z", false, Opaque, Completes > 0 ? new EnrollmentReceipt("af7de985-1df9-4e65-a310-dd86fa4c6862", "completed", null) : null);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data }, ApiClient.Json), Encoding.UTF8, "application/json") };
        }
    }
}
