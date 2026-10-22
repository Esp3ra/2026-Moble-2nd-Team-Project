using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

internal static class Program
{
    private static int checks;
    private static string? renderFolder;
    private static readonly Guid Challenge = Guid.Parse("20000000-0000-4000-8000-000000000001");
    private static readonly Guid DeviceId = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid Work = Guid.Parse("70000000-0000-4000-8000-000000000001");
    private static readonly JsonDocument Fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "identity-v2.json")));
    private static byte[] Vector(string name) => Encoding.UTF8.GetBytes(Fixture.RootElement.GetProperty("vectors").EnumerateArray()
        .Single(x => x.GetProperty("name").GetString() == name).GetProperty("envelope_json").GetString()!);
    private static string B64(byte[] v) => Convert.ToBase64String(v).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static void Check(bool ok, string description) { checks++; if (!ok) throw new InvalidOperationException(description); }
    private static async Task Refused(Func<Task> action)
    {
        try { await action(); } catch (Exception e) when (e is OperationCanceledException or ApiException or InvalidDataException or ObjectDisposedException or HttpRequestException)
        { Check(true, "Rejected unsafe login transition"); return; }
        throw new InvalidOperationException("Unsafe transition was accepted");
    }
    private static T Field<T>(object owner, string name) => (T)(owner is LoginForm ? typeof(LoginForm) : owner.GetType())
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static HttpResponseMessage Reply(object data) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data }, ApiClient.Json), Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Error(int status, string code) => new((HttpStatusCode)status)
    { Content = new StringContent(JsonSerializer.Serialize(new { ok = false, error = new { code, message = "synthetic error" } }), Encoding.UTF8, "application/json") };

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize(); Control.CheckForIllegalCrossThreadCalls = true;
        if (args.Length == 2 && args[0] == "--render") { renderFolder = args[1]; Directory.CreateDirectory(renderFolder); }
        int code = 1;
        using var loop = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
        loop.Shown += async (_, _) =>
        {
            try
            {
                if (args.Length > 0 && args[0] == "--loopback-login") await LoopbackLogin(loop, args);
                else
                {
                    await TokenGate(); await Recovery(); await ChangedReceipt(); await RevokeAfterExpiry();
                    await Late(false); await Late(true); await EarlyDispose(); await InvalidResponses();
                    await LoginUi(loop, false, false); await LoginUi(loop, true, false); await LoginUi(loop, true, true);
                    await NoFallback(loop); await ClosedLogin(loop); await SameTokenGeneration();
                    Console.WriteLine($"PASS identity login UI: {checks} assertions; real WinForms/ApiClient/pending/relay with controlled HTTP/device; no live server, DB, sensor or configuration changes.");
                }
                code = 0;
            }
            catch (Exception e) { Console.Error.WriteLine(e); }
            finally { loop.Close(); }
        };
        Application.Run(loop); return code;
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class Host : IDisposable
    {
        public readonly Server Server = new();
        public readonly ApiClient Api;
        public readonly ApiClient.IdentityLogin Scope;
        public readonly Clock Clock = new();
        public IdentityPendingConnection Pending = null!;
        public Host()
        {
            Api = new("http://login.test", new Handler(Server));
            Scope = Api.BeginIdentityLogin(new Handler(Server), Clock);
            Server.IdentityInstalled = () => Api.CurrentUser is not null;
        }
        public async Task Open() => Pending = await Scope.OpenAsync("synthetic", "synthetic");
        public void Dispose() { Scope.Dispose(); Api.Dispose(); }
    }
    private static async Task TokenGate()
    {
        using var h = new Host(); await h.Open();
        Check(h.Api.CurrentUser is null, "Pending is never a normal login");
        await Refused(() => h.Scope.CompleteAsync(Challenge));
        Check(h.Api.CurrentUser is null && h.Server.WorkSaves == 0, "UI verification cannot replace server verification");
        h.Server.Verified = true;
        var login = await h.Scope.CompleteAsync(Challenge);
        Check(h.Server.WorkSaves == 1 && h.Api.CurrentUser?.Id == 5 && h.Scope.IsCurrent, "Server commit then normal identity import");
        Check(!h.Server.InstalledDuringMe && h.Server.MeAuth == "Bearer", "auth/me is checked before installing the normal token");
        Check(h.Server.PendingSchemes.All(x => x == "IdentityPending"), "Pending transport stays separate");
        await h.Scope.CompleteAsync(Challenge);
        Check(h.Server.FinalizeIds.Count == 2 && h.Server.WorkSaves == 1, "Completed repeat uses accepted RAM result, no duplicate write");
        await Refused(() => h.Scope.CompleteAsync(Guid.NewGuid()));
        h.Scope.Dispose(); Check(h.Api.CurrentUser?.Id == login.User.Id, "Disposing login scope preserves accepted normal login");
    }
    private static async Task Recovery()
    {
        using var h = new Host(); await h.Open(); h.Server.Verified = true; h.Server.LoseFinalize = true;
        await Refused(() => h.Scope.CompleteAsync(Challenge));
        Check(h.Api.CurrentUser is null && h.Server.WorkSaves == 1, "Lost finalize response cannot open workspace");
        h.Clock.Now = h.Clock.Now.AddMinutes(6);
        Check(!h.Pending.IsCurrent && h.Scope.IsCurrent, "Retain result owner beyond activity expiry");
        await h.Scope.CompleteAsync(Challenge);
        Check(h.Server.FinalizeIds.Count == 2 && h.Server.FinalizeIds.Distinct().Single() == h.Scope.OriginalFinalizeId, "Same finalize UUID after five-minute expiry");
        Check(h.Server.WorkSaves == 1 && h.Api.CurrentUser?.Id == 5, "Original encrypted-result replay imports one session");
    }
    private static async Task ChangedReceipt()
    {
        using var h = new Host(); await h.Open(); h.Server.Verified = true; h.Server.RejectMe = true;
        await Refused(() => h.Scope.CompleteAsync(Challenge)); Check(h.Api.CurrentUser is null, "401 provisional identity not installed");
        h.Server.RejectMe = false; h.Server.Token = new string('x', 43);
        await Refused(() => h.Scope.CompleteAsync(Challenge));
        Check(h.Api.CurrentUser is null, "Changed replay token rejected");
        int before = h.Server.FinalizeIds.Count;
        await Refused(() => h.Scope.CompleteAsync(Guid.NewGuid()));
        Check(h.Server.FinalizeIds.Count == before, "Cannot switch challenge during result recovery");
    }
    private static async Task RevokeAfterExpiry()
    {
        using var h = new Host(); await h.Open(); h.Server.Verified = true; h.Server.LoseFinalize = true;
        await Refused(() => h.Scope.CompleteAsync(Challenge)); h.Clock.Now = h.Clock.Now.AddMinutes(6);
        await h.Scope.RevokeAsync();
        Check(h.Server.Revokes == 1 && h.Api.CurrentUser is null, "Explicit revoke of retained context after activity expiry");
        Check(h.Server.WorkSaves == 1, "Revoke is not an attendance rollback");
    }
    private static async Task Late(bool afterMe)
    {
        using var h = new Host(); await h.Open(); h.Server.Verified = true;
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (afterMe) h.Server.MeGate = gate; else h.Server.FinalizeGate = gate;
        var task = h.Scope.CompleteAsync(Challenge);
        await (afterMe ? h.Server.MeEntered.Task : h.Server.FinalizeEntered.Task);
        h.Api.SetServer("http://new-login.test");
        h.Server.FingerprintRequired = false; h.Server.NormalUser = new(6, "second", "다른 접속", "T6", "시험", "employee", true);
        await h.Api.LoginAsync("second", "synthetic");
        gate.SetResult(true); await Refused(async () => await task);
        Check(h.Api.CurrentUser?.Id == 6, "Late finalize/auth-me cannot replace a newer login");
        h.Scope.Dispose(); Check(h.Api.CurrentUser?.Id == 6, "Stale owner disposal does not clear newer login");
    }
    private static async Task EarlyDispose()
    {
        using var h = new Host(); h.Server.PendingGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var opening = h.Open(); await h.Server.PendingEntered.Task;
        h.Scope.Dispose(); h.Server.PendingGate.SetResult(true); await Refused(async () => await opening);
        Check(h.Api.CurrentUser is null, "Disposed view cannot finish a pending login");
    }
    private static async Task InvalidResponses()
    {
        foreach (string bad in new[] { "state", "token_type", "work", "day", "expiry", "user" })
        {
            using var h = new Host(); await h.Open(); h.Server.Verified = true; h.Server.Bad = bad;
            await Refused(() => h.Scope.CompleteAsync(Challenge));
            Check(h.Api.CurrentUser is null, "Reject invalid finalize/identity: " + bad);
        }
    }
    private static EnrollmentEndpoints Endpoints(Uri server) => EnrollmentEndpoints.Parse(JsonSerializer.SerializeToUtf8Bytes(new
    {
        version = 1, server_url = server.ToString(), allow_insecure_http = true,
        devices = new Dictionary<string, object> { [DeviceId.ToString("D")] = new { pi_base_url = "http://pi.test:8080" } }
    }), server);
    private static async Task LoginUi(Form owner, bool required, bool lose)
    {
        var server = new Server { FingerprintRequired = required, LoseFinalize = lose };
        using var api = new ApiClient("http://login.test", new Handler(server));
        server.IdentityInstalled = () => api.CurrentUser is not null;
        int workspaces = 0, fingerprintViews = 0, captures = 0;
        using var form = new TestLogin(api, (scope, pending) =>
        {
            fingerprintViews++;
            var sensor = new Device { Captured = () => captures++ };
            using var view = new FingerprintLoginForm(scope, pending, Endpoints(scope.ServerUri), _ => sensor,
                async attempt => { await attempt.RunAsync(); return attempt.Result; })
            { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
            view.Shown += async (_, _) =>
            {
                if (required && !lose) Render(view, "login-before-scan.png");
                Field<Button>(view, "btnStart").PerformClick(); await view.PendingOperation;
                if (lose)
                {
                    Check(workspaces == 0 && api.CurrentUser is null, "No workspace on uncertain clock-in");
                    Check(Field<Label>(view, "lblStatus").Text == "출근 결과 확인 필요", "Uncertain finalization guidance");
                    Render(view, "login-recover-result.png");
                    Field<Button>(view, "btnStart").PerformClick(); await view.PendingOperation;
                }
            };
            view.ShowDialog(owner); return view.CompletedLogin;
        }, _ => { Check(api.CurrentUser is not null, "Workspace after token installation only"); workspaces++; },
            ct => api.BeginIdentityLogin(new Handler(server), lifetime: ct));
        form.Show(owner); Fill(form); Field<Button>(form, "btnLogin").PerformClick();
        await form.PendingLogin;
        Check(form.Errors.Count == 0 && workspaces == 1, "Actual LoginForm completes exactly once");
        Check(fingerprintViews == (required ? 1 : 0) && captures == (required ? 1 : 0), "Server OFF/resume preserves normal login; first work scans once");
        Check(server.WorkSaves == (required ? 1 : 0), "One server clock-in, no UI-only attendance success");
        if (lose) Check(server.FinalizeIds.Distinct().Count() == 1 && captures == 1, "UI recovery does not reopen scanner");
        form.Close();
    }
    private static async Task NoFallback(Form owner)
    {
        foreach (int status in new[] { 403, 404, 503 })
        {
            var server = new Server { NormalError = status };
            using var api = new ApiClient("http://login.test", new Handler(server)); int opened = 0;
            using var form = new TestLogin(api, (_, _) => { opened++; return null; }, _ => opened++, ct => api.BeginIdentityLogin(new Handler(server), lifetime: ct));
            form.Show(owner); Fill(form); Field<Button>(form, "btnLogin").PerformClick(); await form.PendingLogin;
            Check(opened == 0 && server.PendingLogins == 0 && api.CurrentUser is null && form.Errors.Count == 1,
                "No pending/normal bypass on non-requirement status");
            form.Close();
        }
    }
    private static async Task ClosedLogin(Form owner)
    {
        var server = new Server { FingerprintRequired = false, NormalGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var api = new ApiClient("http://login.test", new Handler(server)); int opened = 0;
        using var form = new TestLogin(api, (_, _) => null, _ => opened++, ct => api.BeginIdentityLogin(new Handler(server), lifetime: ct));
        form.Show(owner); Fill(form); Field<Button>(form, "btnLogin").PerformClick(); var task = form.PendingLogin;
        await server.NormalEntered.Task; form.Close(); server.NormalGate.SetResult(true); await task;
        Check(opened == 0 && api.CurrentUser is null, "Closed LoginForm cannot install late password-login response");
    }
    private static void Fill(LoginForm form)
    { Field<TextBox>(form, "txtLoginId").Text = "synthetic"; Field<TextBox>(form, "txtPassword").Text = "synthetic"; }
    private static async Task SameTokenGeneration()
    {
        var server = new Server { FingerprintRequired = false };
        using var api = new ApiClient("http://login.test", new Handler(server));
        var first = await api.LoginAsync("synthetic", "synthetic");
        var second = await api.LoginAsync("synthetic", "synthetic"); // The fixture deliberately returns an identical token.
        var current = typeof(ApiClient).GetMethod("IsLoginCurrent", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var clear = typeof(ApiClient).GetMethod("ClearLoginIfCurrent", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Check(!(bool)current.Invoke(api, new object[] { first })! && (bool)current.Invoke(api, new object[] { second })!, "Same token still has a different captured login generation");
        clear.Invoke(api, new object[] { first }); Check(api.CurrentUser?.Id == 5, "Old view cannot clear a newer identical-token login");
        clear.Invoke(api, new object[] { second }); Check(api.CurrentUser is null, "Current view clears only its own accepted login");
    }
    private static void Render(Form form, string name)
    {
        if (renderFolder is null) return;
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(renderFolder, name));
    }
    private static async Task LoopbackLogin(Form owner, string[] args)
    {
        if (args.Length != 5 || args[4] is not ("matched" or "unregistered" or "matched-loss")) throw new ArgumentException("Explicit owned test endpoints/mode required");
        var server = ApiClient.ValidateUrl(args[1]); var pi = ApiClient.ValidateUrl(args[2]);
        if (new[] { server, pi }.Any(u => u.Scheme != "http" || u.Host != "127.0.0.1")) throw new InvalidOperationException("Owned loopback only");
        string deviceId = Guid.Parse(args[3]).ToString("D");
        string loginId = Environment.GetEnvironmentVariable("MEMBER5_TEST_LOGIN_ID") ?? throw new InvalidOperationException("Synthetic login required");
        string password = Environment.GetEnvironmentVariable("MEMBER5_TEST_PASSWORD") ?? throw new InvalidOperationException("Synthetic password required");
        bool match = args[4] != "unregistered", loss = args[4] == "matched-loss";
        static HttpMessageHandler Direct() => new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false, UseProxy = false };
        using var api = new ApiClient(server.ToString(), Direct());
        int workspaces = 0, dialogs = 0; Exception? callbackFailure = null;
        using var form = new TestLogin(api, (scope, pending) =>
        {
            var topology = EnrollmentEndpoints.Parse(JsonSerializer.SerializeToUtf8Bytes(new { version = 1, server_url = server.ToString(),
                allow_insecure_http = true, devices = new Dictionary<string, object> { [deviceId] = new { pi_base_url = pi.ToString() } } }), server);
            FingerprintLoginForm? outer = null;
            using var view = outer = new FingerprintLoginForm(scope, pending, topology, verify: attempt =>
            {
                dialogs++;
                using var inner = new FingerprintIdentityForm(attempt, ownsAttempt: false, closeWhenVerified: true, autoStart: true)
                { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
                inner.Shown += async (_, _) =>
                {
                    try
                    {
                        await inner.PendingOperation;
                        if (inner.Visible) inner.Close();
                    }
                    catch (Exception e) { callbackFailure = e; inner.Dispose(); }
                };
                inner.ShowDialog(outer); return Task.FromResult(inner.IdentityResult);
            }, autoStart: true) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
            view.Shown += async (_, _) =>
            {
                try
                {
                    await view.PendingOperation;
                    if (loss && view.CompletedLogin is null)
                    {
                        Check(api.CurrentUser is null && Field<Label>(view, "lblStatus").Text == "출근 결과 확인 필요", "Real lost commit stays outside workspace");
                        Field<Button>(view, "btnStart").PerformClick(); await view.PendingOperation;
                    }
                    if (!match)
                    {
                        Check(api.CurrentUser is null && view.CompletedLogin is null, "Real no_match cannot install login");
                        view.Close(); await view.PendingOperation;
                    }
                    else Check(view.CompletedLogin is not null, "Real server commit and auth/me completed");
                }
                catch (Exception e) { callbackFailure = e; view.Dispose(); }
            };
            view.ShowDialog(owner); return view.CompletedLogin;
        }, _ => workspaces++, ct => api.BeginIdentityLogin(loss ? new LoseFinalize(Direct()) : Direct(), lifetime: ct));
        form.Show(owner); Field<TextBox>(form, "txtLoginId").Text = loginId; Field<TextBox>(form, "txtPassword").Text = password;
        Field<Button>(form, "btnLogin").PerformClick(); await form.PendingLogin;
        if (callbackFailure is not null) throw callbackFailure;
        Check(form.Errors.Count == 0 && dialogs == 1 && workspaces == (match ? 1 : 0), "Real LoginForm/scanner/result/finalize workspace gate");
        if (match)
        {
            Check(api.CurrentUser?.LoginId == loginId, "Imported real auth/me identity");
            using var resumed = new ApiClient(server.ToString(), Direct());
            Check((await resumed.LoginAsync(loginId, password)).User.Id == api.CurrentUser!.Id, "Same-day open work resumes normal login without another scan");
        }
        form.Close();
        Console.WriteLine(JsonSerializer.Serialize(new { mode = args[4], checks, workspace_count = workspaces, scanner_dialogs = dialogs, passed = true }));
    }
    private sealed class LoseFinalize(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private bool lost;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            if (!lost && request.RequestUri!.AbsolutePath.EndsWith("/finalize") && response.IsSuccessStatusCode)
            { lost = true; await response.Content.ReadAsByteArrayAsync(ct); response.Dispose(); throw new HttpRequestException("Synthetic discarded finalize response"); }
            return response;
        }
    }
    private sealed class TestLogin : LoginForm
    {
        public readonly List<string> Errors = [];
        public TestLogin(ApiClient api, Func<ApiClient.IdentityLogin, IdentityPendingConnection, LoginResult?> fingerprint,
            Action<LoginResult> open, Func<CancellationToken, ApiClient.IdentityLogin> create) : base(api, fingerprint, open, create)
        { ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; Location = new(-20000, -20000); }
        protected override void ShowLoginError(string message) => Errors.Add(message);
    }
    private sealed class Device : IIdentityRelayDevice
    {
        public Action? Captured;
        public async Task<byte[]> CaptureAsync(byte[] request, Func<byte[], CancellationToken, Task<byte[]>> approvals, CancellationToken ct)
        {
            Captured?.Invoke(); Check(request.SequenceEqual(Vector("clock-in-request")), "Original request reaches sensor seam");
            Check((await approvals(Vector("capture-probe"), ct)).SequenceEqual(Vector("capture-approval")), "Original capture approval");
            Check((await approvals(Vector("result-probe"), ct)).SequenceEqual(Vector("result-approval")), "Original result approval");
            return Vector("matched-result");
        }
    }
    private sealed class Handler(Server server) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => server.Send(request, ct); }
    private sealed class Server
    {
        public bool FingerprintRequired = true, Verified, LoseFinalize, RejectMe, InstalledDuringMe;
        public string Token = new('f', 43), Bad = "", MeAuth = "";
        public int WorkSaves, Revokes, PendingLogins, NormalError;
        public UserInfo NormalUser = new(5, "synthetic", "테스트", "T5", "시험팀", "employee", true);
        public readonly DateTimeOffset Expiry = DateTimeOffset.UtcNow.AddHours(1);
        public readonly List<Guid> FinalizeIds = [];
        public readonly List<string> PendingSchemes = [];
        public Func<bool>? IdentityInstalled;
        public TaskCompletionSource<bool>? FinalizeGate, MeGate, PendingGate, NormalGate;
        public readonly TaskCompletionSource<bool> FinalizeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously),
            MeEntered = new(TaskCreationOptions.RunContinuationsAsynchronously), PendingEntered = new(TaskCreationOptions.RunContinuationsAsynchronously),
            NormalEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool cancelled;
        private object Status() => new { challenge_id = Challenge, state = cancelled ? "cancelled" : Verified ? "verified" : "issued",
            expires_at = DateTimeOffset.UtcNow.AddMinutes(1), workflow_state = Verified ? "pending" : "not_started",
            identity_receipt = !Verified ? null : new { receipt_id = "40000000-0000-4000-8000-000000000001", purpose = "clock_in", verified_at = DateTimeOffset.UtcNow } };
        public async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.AbsolutePath;
            using var body = JsonDocument.Parse(request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(ct)); var v = body.RootElement;
            if (path.EndsWith("/auth/login"))
            {
                NormalEntered.TrySetResult(true); if (NormalGate is not null) await NormalGate.Task;
                if (NormalError != 0) return Error(NormalError, "SYNTHETIC_ERROR");
                if (FingerprintRequired && WorkSaves == 0) return Error(409, "IDENTITY_FINGERPRINT_REQUIRED");
                return Reply(new LoginResult(new string('n', 43), "Bearer", Expiry, NormalUser));
            }
            if (path.EndsWith("/device-identity/login"))
            {
                PendingLogins++; PendingEntered.TrySetResult(true); if (PendingGate is not null) await PendingGate.Task;
                return Reply(new { pending_token = new string('p', 43), token_type = "IdentityPending", context_ref = new string('c', 43),
                    state = "fingerprint_required", expires_at = DateTimeOffset.UtcNow.AddMinutes(4) });
            }
            if (path.EndsWith("/auth/me"))
            {
                MeAuth = request.Headers.Authorization!.Scheme; InstalledDuringMe |= IdentityInstalled?.Invoke() == true;
                MeEntered.TrySetResult(true); if (MeGate is not null) await MeGate.Task;
                if (RejectMe) return Error(401, "AUTH_REQUIRED");
                return Reply(new IdentityResult(Bad == "user" ? NormalUser with { LoginId = "other" } : NormalUser, Expiry));
            }
            PendingSchemes.Add(request.Headers.Authorization?.Scheme ?? "none");
            if (path.EndsWith("/challenges")) return Reply(new { challenge_id = Challenge, client_request_id = v.GetProperty("client_request_id").GetString(),
                state = "issued", expires_at = DateTimeOffset.UtcNow.AddMinutes(1), request_b64 = B64(Vector("clock-in-request")) });
            if (path.EndsWith("/approvals")) return Reply(new { approval_b64 = B64(Vector(v.GetProperty("probe_b64").GetString() == B64(Vector("capture-probe")) ? "capture-approval" : "result-approval")) });
            if (path.EndsWith("/complete")) { Verified = true; return Reply(Status()); }
            if (path.EndsWith("/finalize"))
            {
                FinalizeIds.Add(Guid.Parse(v.GetProperty("client_request_id").GetString()!));
                if (!Verified) return Error(409, "IDENTITY_TERMINAL");
                FinalizeEntered.TrySetResult(true); if (FinalizeGate is not null) await FinalizeGate.Task;
                if (WorkSaves == 0) WorkSaves++;
                if (LoseFinalize) { LoseFinalize = false; throw new HttpRequestException("synthetic lost response"); }
                return Reply(new { state = Bad == "state" ? "verified" : "completed",
                    workflow_receipt_id = "80000000-0000-4000-8000-000000000001", work_session_ref = Bad == "work" ? Guid.Empty : Work,
                    work_date = Bad == "day" ? "yesterday" : "2026-10-22", access_token = Token,
                    token_type = Bad == "token_type" ? "IdentityPending" : "Bearer", expires_at = Bad == "expiry" ? DateTimeOffset.UtcNow.AddHours(-1) : Expiry });
            }
            if (path.EndsWith("/revoke")) { Revokes++; return Reply(new { state = "revoked" }); }
            if (path.EndsWith("/cancel")) { cancelled = true; return Reply(new { challenge_id = Challenge, state = "cancelled" }); }
            if (request.Method == HttpMethod.Get) return Reply(Status());
            throw new InvalidOperationException("Unexpected test route");
        }
    }
}
