using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

internal static class EnrollmentConnectionChecks
{
    private const string A = "123e4567-e89b-42d3-a456-426614174000";
    private const string B = "223e4567-e89b-42d3-a456-426614174000";
    private const string Api = "http://enrollment.test:5000/";
    private const string Opaque = "cHVibGljLXN5bnRoZXRpYw";
    private const string JobId = "31593412-67fd-40d2-878b-868d15f99a11";
    private static bool requireMySql;
    private static readonly UserInfo Admin = new(1, "admin-test", "QA admin", "QA", "QA", "admin", true);
    private static readonly UserInfo Created = new(88, "created-test", "QA member", "QA88", "QA", "employee", true);
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action, string message)
    { try { action(); } catch (InvalidDataException) { checks++; return; } throw new InvalidOperationException(message); }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static Task Call(object owner, string name) => (Task)owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, null)!;
    private static void Pump(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        { Application.DoEvents(); Thread.Sleep(2); if (watch.Elapsed.TotalSeconds > 12) throw new TimeoutException("Enrollment UI did not settle"); }
        Application.DoEvents();
    }
    private static void Complete(Task task) { Pump(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static string Config(string devices = "", string server = Api, bool allowHttp = true)
        => JsonSerializer.Serialize(new { version = 1, server_url = server, allow_insecure_http = allowHttp,
            devices = string.IsNullOrEmpty(devices) ? new Dictionary<string, object>
            { [A] = new { pi_base_url = "https://pi-a.test:5443/" }, [B] = new { pi_base_url = "https://pi-b.test:5443/" } } : null });
    private static EnrollmentEndpoints Parse(string text, string server = Api)
        => EnrollmentEndpoints.Parse(Encoding.UTF8.GetBytes(text), new Uri(server));

    public static void Run(bool requireMySqlEnrollment)
    {
        requireMySql = requireMySqlEnrollment;
        MappingGate();
        Configuration(); Flow("success"); Flow("missing"); Flow("closed"); Flow("logout");
        InvalidServer(); PreflightAndForeignJob();
        foreach (string mode in new[] { "switch", "same", "503", "other409", "wrong400", "lost", "reissue409", "afterjob", "afterpi" }) RejectedDeviceSelection(mode);
        Console.WriteLine($"PASS enrollment connection: {checks} checks; mode={(requireMySql ? "required enrollment-mysql-v1" : "baseline/physical-result-aware")}; real user-management/registration WinForms + controlled HTTP/channel, no real device/MySQL.");
    }

    private static void Configuration()
    {
        string config = Config();
        var parsed = Parse(config);
        Check(parsed.Resolve(A).Host == "pi-a.test" && parsed.Resolve(B).Host == "pi-b.test", "Exact device mapping, no first-device fallback");
        Reject(() => parsed.Resolve(Guid.NewGuid().ToString()), "Unknown device must fail closed");
        foreach (string bad in new[]
        {
            config.Replace("\"version\":1", "\"version\":2"),
            config.Replace("\"version\":1", "\"version\":1,\"version\":1"),
            config.Replace("\"devices\":{", "\"devices\":{\"" + A + "\":{\"pi_base_url\":\"https://duplicate.test/\"},"),
            config.Replace(A, A.ToUpperInvariant()),
            config.Replace("https://pi-b.test:5443/", "https://name:secret@pi-b.test/"),
            config.Replace("https://pi-b.test:5443/", "https://pi-b.test/prefix"),
            config.Replace("https://pi-b.test:5443/", "https://pi-b.test/?key=value"),
            config.Replace("https://pi-b.test:5443/", "https://pi-b.test/#fragment"),
            config.Replace("https://pi-b.test:5443/", "file:///C:/synthetic"),
            config.Replace("\"allow_insecure_http\":true", "\"allow_insecure_http\":\"true\""),
            config.Replace("\"version\":1", "\"version\":1,\"token\":\"synthetic\""),
            Config(server: "http://other-server.test:5000/"),
            Config(allowHttp: false), "{}", "null", "[]", new string(' ', 65537)
        }) Reject(() => Parse(bad), "Invalid configuration accepted: " + bad[..Math.Min(60, bad.Length)]);
        Check(Parse(Config(server: "https://api.test/", allowHttp: false), "https://api.test:443").Resolve(B).Scheme == "https", "Canonical default-port server binding");
        string temp = Path.Combine(Path.GetTempPath(), "rw-endpoints-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            Reject(() => EnrollmentEndpoints.Load(new Uri(Api), temp), "Missing file must not fallback");
            File.WriteAllText(temp, config);
            Check(EnrollmentEndpoints.Load(new Uri(Api), temp).Resolve(B).Host == "pi-b.test", "Explicit local file loading");
            File.WriteAllText(temp, "malformed");
            Reject(() => EnrollmentEndpoints.Load(new Uri(Api), temp), "Malformed file must not fallback");
        }
        finally { File.Delete(temp); }
    }

    private static UserManagementForm Prepare(ApiClient api, ICreatedUserEnrollmentLauncher launcher)
    {
        Complete(api.LoginAsync("admin-test", "synthetic-password"));
        var form = new UserManagementForm(api, launcher) { ShowInTaskbar = false };
        form.Show(); Pump(() => !Field<bool>(form, "loading"));
        Check(!Field<Button>(form, "enroll").Enabled, "Selecting old account does not allow new enrollment");
        form.GetType().GetMethod("ClearEditor", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
        Field<TextBox>(form, "login").Text = Created.LoginId;
        Field<TextBox>(form, "name").Text = Created.DisplayName;
        Field<TextBox>(form, "password").Text = "synthetic-password";
        Complete(Call(form, "Save"));
        Check(form.EnrollmentTarget?.UserId == 88 && Field<Button>(form, "enroll").Enabled, "Explicit button enabled only for confirmed created response ID");
        return form;
    }

    private static void Flow(string mode)
    {
        using var server = new Server(requireMySql); using var api = new ApiClient(Api, server);
        var pi = new Pi(); var addresses = new List<Uri>();
        string config = mode == "missing" ? Config().Replace("\"" + B + "\":{\"pi_base_url\":\"https://pi-b.test:5443/\"}", "")
            .Replace(",}", "}") : Config();
        var launcher = new CreatedUserEnrollmentLauncher(uri => Parse(config, uri.AbsoluteUri), address => { addresses.Add(address); return pi; });
        using var owner = Prepare(api, launcher);
        Check(server.Issues == 0 && pi.Calls == 0, "Account creation itself never starts enrollment");
        FingerprintEnrollmentForm? popup = null;
        int stage = 0;
        Exception? failure = null;
        var watch = Stopwatch.StartNew();
        using var driver = new System.Windows.Forms.Timer { Interval = 10 };
        driver.Tick += (_, _) =>
        {
            try
            {
                if (watch.Elapsed.TotalSeconds > 10) throw new TimeoutException("Modal registration driver");
                popup ??= Application.OpenForms.OfType<FingerprintEnrollmentForm>().SingleOrDefault();
                if (popup is null || popup.IsDisposed) return;
                var start = Field<Button>(popup, "start"); var devices = Field<ComboBox>(popup, "devices");
                if (stage == 0 && start.Enabled)
                {
                    Check(Controls(popup).OfType<Label>().Any(label => label.Text.Contains("88")), "Popup uses creation response ID, not first/admin row");
                    devices.SelectedIndex = 1; stage = 1; start.PerformClick();
                }
                else if (stage == 1 && mode == "missing" && start.Enabled)
                {
                    Check(server.Issues == 0 && pi.Calls == 0 && addresses.Count == 0 && !popup.RegistrationStarted,
                        "Missing selected endpoint rejects BEFORE issuing server job/claim");
                    Check(devices.Enabled, "A preflight failure allows explicit correction before any attempt");
                    devices.SelectedIndex = 0; stage = 2; start.PerformClick();
                }
                else if ((stage == 1 || stage == 2) && pi.Calls == 1)
                {
                    stage = 3;
                    Check(server.Target == Created.Id && server.Device == (mode == "missing" ? A : B), "Issued target and explicit selected device preserved");
                    Check(addresses.Single().Host == (mode == "missing" ? "pi-a.test" : "pi-b.test"), "Selected device gets its exact configured channel");
                    Check(!popup.RegistrationCompleted && !start.Enabled && !devices.Enabled, "No sensor-only completion; duplicate start/device switch disabled");
                    if (requireMySql)
                        Check(server.PhysicalReads == 1 && server.PhysicalWrites == 0,
                            "Required MySQL flow reads physical status BEFORE Pi; no legacy fallback");
                    if (mode == "logout") Task.Run(api.ClearIdentity);
                    else if (mode == "closed") popup.Close();
                    else pi.Ready.SetResult(Opaque);
                }
                else if (stage == 3 && popup.RegistrationCompleted) { stage = 4; popup.Close(); }
            }
            catch (Exception error) { failure = error; driver.Stop(); popup?.Close(); pi.Ready.TrySetResult(Opaque); }
        };
        driver.Start();
        Field<Button>(owner, "enroll").PerformClick();
        if (mode is "closed" or "logout")
        {
            // Modal Close hides the view; using/Dispose happens after pending work.
            Pump(() => popup is { Visible: false } || failure is not null);
            Check(!pi.Disposed && Field<bool>(owner, "enrolling"), "Closing popup waits for PendingOperation BEFORE transport disposal");
            Check(!Field<Button>(owner, "enroll").Enabled, "Second registration cannot start while previous transport drains");
            pi.Ready.TrySetResult(Opaque);
        }
        Pump(() => !Field<bool>(owner, "enrolling")); driver.Stop();
        if (failure is not null) throw failure;
        Check(pi.Disposed, "Caller disposes borrowed Pi channel after pending operation");
        if (mode is "success" or "missing")
        {
            Check(server.Completes == 1 && popup!.RegistrationCompleted, "Server confirmation is required for completed UI");
            if (requireMySql || server.PhysicalReads > 0)
                Check(server.PhysicalWrites == 1 && server.RecordedRaw == Opaque,
                    "Exact Pi result is recorded once BEFORE complete");
            Check(Field<Label>(owner, "status").Text.StartsWith("지문 등록 완료"), "Owning form publishes current confirmed result");
            Check(!Field<Button>(owner, "enroll").Enabled, "Completed operation cannot start a new enrollment");
        }
        else
        {
            Check(server.Completes == 0 && !popup!.RegistrationCompleted, "Late result after close/logout cannot complete server mapping");
            Check(server.PhysicalWrites == 0, "Late result after close/logout cannot record physical result");
            Check(!Field<Button>(owner, "enroll").Enabled, "Uncertain operation/session cannot silently create another attempt");
        }
        owner.Close();
    }

    private static void InvalidServer()
    {
        using var server = new Server(); using var api = new ApiClient(Api, server);
        using var owner = Prepare(api, new CreatedUserEnrollmentLauncher(uri => Parse(Config(server: "https://other.test/"), uri.AbsoluteUri)));
        Field<Button>(owner, "enroll").PerformClick();
        Pump(() => !Field<bool>(owner, "enrolling"));
        Check(server.Issues == 0 && server.Eligible == 0, "Server/config mismatch rejected before popup or issue");
        Check(Field<Button>(owner, "enroll").Enabled && owner.EnrollmentTarget?.IsCurrent == true, "Pre-open config error preserves creation and permits correction");
        owner.Close();
    }

    private static void PreflightAndForeignJob()
    {
        using var server = new Server(); using var api = new ApiClient(Api, server);
        using var owner = Prepare(api, new CreatedUserEnrollmentLauncher());
        var target = owner.EnrollmentTarget!; var pi = new Pi();
        using var channel = new EnrollmentDeviceChannel(target, Parse(Config()), _ => pi);
        channel.ValidateDevice(B);
        Reject(() => channel.ValidateDevice(A), "An issued device cannot switch to another address");
        try
        {
            Complete(channel.TransferAsync(server.Job(A), false, (_, _) => Task.FromResult(Opaque), null, CancellationToken.None));
            throw new Exception("Foreign job reached configured channel");
        }
        catch (InvalidDataException) { checks++; }
        Check(pi.Calls == 0, "Foreign device job rejected without transport");
        Check(!channel.ReleaseUnissuedDevice(A), "Wrong-device release cannot clear the selected binding");
        pi.Ready.SetResult(Opaque);
        Complete(channel.TransferAsync(server.Job(B), false, (_, _) => Task.FromResult(Opaque), null, CancellationToken.None));
        Check(pi.Calls == 1 && !channel.ReleaseUnissuedDevice(B), "Transport-started binding cannot be released as a pre-issue rejection");
        owner.Close();
    }

    private static void RejectedDeviceSelection(string mode)
    {
        using var server = new Server(true) { RejectOrdinary = mode is not ("afterjob" or "afterpi"),
            IssueStatus = mode == "503" ? 503 : mode == "wrong400" ? 400 : 409,
            IssueCode = mode == "other409" ? "OTHER_CONFLICT" : "ENROLLMENT_NO_FREE_CLAIM",
            LostIssue = mode == "lost", FailReissue = mode == "reissue409", FailPhysical = mode == "afterjob", FailReport = mode == "afterpi" };
        server.RecoveryDevice = mode == "same" ? A : B;
        using var api = new ApiClient(Api, server);
        var channels = new List<Pi>(); var addresses = new List<Uri>();
        var launcher = new CreatedUserEnrollmentLauncher(uri => Parse(Config(), uri.AbsoluteUri), address =>
        { addresses.Add(address); var pi = new Pi(); pi.Ready.SetResult(Opaque); channels.Add(pi); return pi; });
        using var owner = Prepare(api, launcher);
        FingerprintEnrollmentForm? popup = null; Exception? failure = null; int stage = 0;
        var watch = Stopwatch.StartNew();
        using var driver = new System.Windows.Forms.Timer { Interval = 10 };
        driver.Tick += (_, _) =>
        {
            try
            {
                if (watch.Elapsed.TotalSeconds > 10) throw new TimeoutException("Device rejection/reselection " + mode);
                popup ??= Application.OpenForms.OfType<FingerprintEnrollmentForm>().SingleOrDefault();
                if (popup is null || popup.IsDisposed) return;
                var devices = Field<ComboBox>(popup, "devices"); var start = Field<Button>(popup, "start");
                if (stage == 0 && devices.Items.Count == 2 && start.Enabled)
                { devices.SelectedIndex = 0; stage = 1; start.PerformClick(); }
                else if (stage == 1 && server.Issues == 1 && !Field<bool>(popup, "busy"))
                {
                    if (mode is not ("switch" or "same" or "reissue409"))
                    {
                        Check(popup.RegistrationStarted && !devices.Enabled, "Uncertain/post-job attempt stays bound: " + mode);
                        Check(channels.Count == 1 && !channels[0].Disposed && server.Reissues == 0,
                            "No channel release/rebind or reissue after " + mode);
                        Check(channels[0].Calls == (mode == "afterpi" ? 1 : 0), "Expected physical boundary: " + mode);
                        stage = 9; popup.Close(); return;
                    }
                    Check(!popup.RegistrationStarted && devices.Enabled && channels[0].Disposed,
                        "Exact pre-issue rejection releases both attempt and prepared channel");
                    devices.SelectedIndex = mode == "same" ? 0 : 1;
                    Field<CheckBox>(popup, "recoveryMode").Checked = true;
                    Field<TextBox>(popup, "recoveryId").Text = Server.RecoveryId;
                    Check(addresses.Count == 1 && server.Reissues == 0 && channels.Sum(p => p.Calls) == 0,
                        "Explicit selection alone does not create another channel/job/scan");
                    stage = 2; Field<Button>(popup, "checkRecovery").PerformClick();
                }
                else if (stage == 2 && !Field<bool>(popup, "busy") && start.Enabled)
                {
                    Check(addresses.Count == 1 && server.Reissues == 0, "Recovery GET alone does not bind or reissue");
                    stage = 3; start.PerformClick();
                }
                else if (stage == 3 && !Field<bool>(popup, "busy") && server.Reissues == 1)
                {
                    Check(addresses.Count == 2 && addresses[1].Host == (mode == "same" ? "pi-a.test" : "pi-b.test"),
                        "Explicit recovery binds the exact newly selected configured endpoint");
                    Check(server.Target == 88 && owner.EnrollmentTarget?.UserId == 88,
                        "Rebinding preserves same created member and original administrator session");
                    Check(channels[0].Calls == 0 && channels[1].Calls == (mode == "reissue409" ? 0 : 1),
                        "Old channel never receives new-device work; no automatic extra scan");
                    Check(!channels[1].Disposed && popup.RegistrationStarted,
                        "Issued/reissue attempt remains bound until view releases transport");
                    Check(popup.RegistrationCompleted == (mode != "reissue409") && server.Completes == (mode == "reissue409" ? 0 : 1),
                        "Completed only after exact physical result and common-server complete");
                    stage = 9; popup.Close();
                }
            }
            catch (Exception error) { failure = error; driver.Stop(); popup?.Close(); }
        };
        driver.Start(); Field<Button>(owner, "enroll").PerformClick(); driver.Stop();
        if (failure is not null) throw failure;
        Check(stage == 9 && channels.All(p => p.Disposed), "Launcher cleans all prepared channels after pending work: " + mode);
        owner.Close();
    }

    private static IEnumerable<Control> Controls(Control root)
    { foreach (Control child in root.Controls) { yield return child; foreach (var descendant in Controls(child)) yield return descendant; } }

    // These are fixture guards, not tests of a real server or MySQL persistence.
    private static void MappingGate()
    {
        using var server = new Server(true) { Issues = 1, Device = A, Client = Guid.NewGuid().ToString() };
        using var client = new HttpClient(server) { BaseAddress = new Uri(Api) };
        client.DefaultRequestHeaders.Authorization = new("Bearer", "synthetic-token");
        HttpStatusCode Send(string suffix, string raw)
        {
            using var content = new StringContent(JsonSerializer.Serialize(new { operation_revision = "1", result_envelope = raw }), Encoding.UTF8, "application/json");
            var task = client.PostAsync($"api/v1/device-enrollments/{JobId}/{suffix}", content);
            Complete(task); using var response = task.GetAwaiter().GetResult(); return response.StatusCode;
        }
        Check(Send("complete", Opaque) == HttpStatusCode.Conflict && server.Completes == 0,
            "Strict fixture refuses completion without recorded bytes");
        Check(Send("physical-result", Opaque) == HttpStatusCode.OK, "Fixture accepts first physical result");
        Check(Send("physical-result", Opaque) == HttpStatusCode.OK && server.PhysicalWrites == 1,
            "Fixture preserves identical replay without another stored effect");
        Check(Send("physical-result", "b3RoZXI") == HttpStatusCode.Conflict && server.RecordedRaw == Opaque,
            "Fixture refuses overwrite by different bytes");
        Check(Send("complete", "b3RoZXI") == HttpStatusCode.Conflict && server.Completes == 0,
            "Fixture refuses completion with different bytes");
        Check(Send("complete", Opaque) == HttpStatusCode.OK && server.Completes == 1,
            "Fixture permits completion only after exact result storage");
    }
    private sealed class Pi : IEnrollmentPiChannel, IDisposable
    {
        public int Calls; public bool Disposed;
        public readonly TaskCompletionSource<string> Ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<string> TransferAsync(EnrollmentJob job, bool readOnly,
            Func<JsonElement, CancellationToken, Task<string>> approve, IProgress<string>? progress, CancellationToken cancellation)
        { Interlocked.Increment(ref Calls); return await Ready.Task.ConfigureAwait(false); }
        public void Dispose() => Disposed = true;
    }
    private sealed class Server(bool requirePhysical = false) : HttpMessageHandler
    {
        public const string RecoveryId = "453f13b4-aecb-4aec-9a7b-8ae64e7dd100";
        public bool RejectOrdinary, LostIssue, FailReissue, FailPhysical, FailReport;
        public int IssueStatus = 409, Reissues; public string IssueCode = "ENROLLMENT_NO_FREE_CLAIM", RecoveryDevice = B;
        public int Issues, Completes, Eligible, Target; public string Device = "", Client = "";
        public int PhysicalReads, PhysicalWrites;
        public string? RecordedRaw { get; private set; }
        public EnrollmentJob Job(string device) => new(JobId, "1", 7, Reissues > 0 ? "2" : "1", "1", Client,
            device, Completes > 0 ? "completed" : "issued", "2026-10-20T01:03:00Z", false, Opaque,
            Completes > 0 ? new EnrollmentReceipt("af7de985-1df9-4e65-a310-dd86fa4c6862", "completed", null) : null);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            string path = request.RequestUri!.AbsolutePath; object data;
            if (path.EndsWith("auth/login")) data = new LoginResult("synthetic-token", "Bearer", DateTimeOffset.UtcNow.AddHours(1), Admin);
            else
            {
                Check(request.Headers.Authorization?.Parameter == "synthetic-token", "All common-server requests use captured administrator bearer");
                if (path.EndsWith("/users") && request.Method == HttpMethod.Get) data = new PageResult<UserInfo>([Admin], 1, 50, 100);
                else if (path.EndsWith("/users") && request.Method == HttpMethod.Post) data = new UserResult(Created);
                else if (path.EndsWith("/eligible-devices"))
                {
                    Check(request.RequestUri.Query == "?target_user_id=88", "Eligible query uses created response ID");
                    Eligible++; data = new EnrollmentDevices([new(A, "QA device A", "1"), new(B, "QA device B", "1")]);
                }
                else if (path.EndsWith("/device-enrollments"))
                {
                    using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
                    var body = document.RootElement;
                    Client = body.GetProperty("client_request_id").GetString()!;
                    Target = body.GetProperty("target_user_id").GetInt32(); Device = body.GetProperty("device_id").GetString()!;
                    Issues++;
                    if (LostIssue) throw new HttpRequestException("Synthetic lost issue response");
                    if (RejectOrdinary) return Error(IssueStatus, IssueCode);
                    data = Job(Device);
                }
                else if (path.Contains("/by-request/")) return Error(503, "SYNTHETIC_LOOKUP_UNAVAILABLE");
                else if (path.EndsWith("/recoveries/" + RecoveryId)) data = new { recovery = RecoveryValue() };
                else if (path.EndsWith("/recoveries/" + RecoveryId + "/reissue"))
                {
                    using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
                    var body = document.RootElement;
                    Check(body.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "client_request_id", "target_user_id" }), "Reissue exact body");
                    Client = body.GetProperty("client_request_id").GetString()!;
                    Target = body.GetProperty("target_user_id").GetInt32(); Device = RecoveryDevice; Reissues++;
                    if (FailReissue) return Error(409, "ENROLLMENT_NO_FREE_CLAIM");
                    data = new { recovery = RecoveryValue(), job = Job(Device) };
                }
                else if (path.EndsWith("/physical-result"))
                {
                    Check(path == $"/api/v1/device-enrollments/{JobId}/physical-result" && Issues == 1,
                        "Physical route belongs to the issued job");
                    if (FailPhysical || FailReport && request.Method == HttpMethod.Post) return Error(409, "ENROLLMENT_NO_FREE_CLAIM");
                    if (request.Method == HttpMethod.Get) PhysicalReads++;
                    else
                    {
                        Check(request.Method == HttpMethod.Post, "Physical result uses GET/POST only");
                        string raw = await ResultBody(request, cancellation);
                        if (RecordedRaw is not null && RecordedRaw != raw) return Conflict();
                        if (RecordedRaw is null) { RecordedRaw = raw; PhysicalWrites++; }
                    }
                    data = new
                    {
                        enrollment_id = JobId, operation_revision = "1", reserved_slot = 7,
                        claim_revision = Reissues > 0 ? "2" : "1", key_epoch = "1", contract = "enrollment-mysql-v1",
                        phase = RecordedRaw is null ? "not_recorded" : "result_recorded", result_envelope = RecordedRaw,
                        result_id = RecordedRaw is null ? null : "af7de985-1df9-4e65-a310-dd86fa4c6862",
                        outcome = RecordedRaw is null ? null : "sensor_stored", failure_code = (string?)null,
                        intent_at = RecordedRaw is null ? null : "2026-10-20T01:01:00Z",
                        reported_at = RecordedRaw is null ? null : "2026-10-20T01:01:01Z",
                        may_store = false, approval_envelope = (string?)null
                    };
                }
                else if (path.EndsWith("/complete"))
                {
                    Check(path == $"/api/v1/device-enrollments/{JobId}/complete" && Issues == 1
                        && request.Method == HttpMethod.Post, "Complete uses the issued job and POST");
                    string raw = await ResultBody(request, cancellation);
                    if ((requirePhysical || PhysicalReads > 0 || RecordedRaw is not null) && RecordedRaw != raw)
                        return Conflict();
                    Completes++; data = Job(Device);
                }
                else throw new InvalidOperationException("Unexpected common-server route: " + path);
            }
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data }, ApiClient.Json), Encoding.UTF8, "application/json") };
        }
        private static HttpResponseMessage Conflict() => new(HttpStatusCode.Conflict)
        { Content = new StringContent("{\"ok\":false,\"error\":{\"code\":\"FIXTURE_RESULT_CONFLICT\",\"message\":\"Exact physical result must be recorded first\"}}", Encoding.UTF8, "application/json") };
        private object RecoveryValue() => new { recovery_id = RecoveryId, source_enrollment_id = "7cb7e37e-0661-432d-9bfb-5037483d64a3", device_id = RecoveryDevice,
            reserved_slot = 7, source_operation_revision = "1", source_claim_revision = "1", state = Reissues > 0 && !FailReissue ? "reissued" : "closed_awaiting_explicit_reissue",
            new_enrollment_id = Reissues > 0 && !FailReissue ? JobId : null, evidence_kind = "operator_observation_not_signed_result",
            closed_at = "2026-10-21T00:01:00Z", reissued_at = Reissues > 0 && !FailReissue ? "2026-10-21T00:02:00Z" : null };
        private static HttpResponseMessage Error(int status, string code) => new((HttpStatusCode)status)
        { Content = new StringContent(JsonSerializer.Serialize(new { ok = false, error = new { code, message = "Synthetic controlled rejection" }, request_id = "synthetic-rebind" }), Encoding.UTF8, "application/json") };
        private static async Task<string> ResultBody(HttpRequestMessage request, CancellationToken cancellation)
        {
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
            var body = document.RootElement;
            Check(body.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "operation_revision", "result_envelope" }),
                "Result request has exactly revision and original bytes");
            Check(body.GetProperty("operation_revision").GetString() == "1", "Result uses current operation revision");
            return body.GetProperty("result_envelope").GetString()!;
        }
    }
}
