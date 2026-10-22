using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private static int checks;
    private static readonly Guid Challenge = Guid.Parse("20000000-0000-4000-8000-000000000001");
    private static readonly Guid DeviceId = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly JsonDocument Fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "identity-v2.json")));
    private static byte[] Vector(string name) => Encoding.UTF8.GetBytes(Fixture.RootElement.GetProperty("vectors").EnumerateArray()
        .Single(x => x.GetProperty("name").GetString() == name).GetProperty("envelope_json").GetString()!);
    private static string B64(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static void Check(bool condition, string description)
    { checks++; if (!condition) throw new InvalidOperationException(description); }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static HttpResponseMessage Reply(object data) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data }, ApiClient.Json), Encoding.UTF8, "application/json") };

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize(); Control.CheckForIllegalCrossThreadCalls = true;
        int code = 1;
        using var loop = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
        loop.Shown += async (_, _) =>
        {
            try
            {
                await Matched(); await LostIssue(); await UnknownIssueCancellation(); await ServerUnavailable();
                await LostComplete(false); await LostComplete(true);
                await LostDevice(); await CancelWhileCapturing(); await LostCancel(); await CancelRace();
                await LateContext(false); await LateContext(true); await Timeout(); await CallerCancelled();
                await BadResponses(); await NormalConnection(); await ContextAfterSuccess();
                await Ui(loop, args); await UiCloseAndDisconnect(loop);
                Console.WriteLine($"PASS identity connection/UI: {checks} assertions; captured C# connections + synthetic HTTP handler/Pi + real WinForms loop; no live server/device/DB/login activation.");
                code = 0;
            }
            catch (Exception e) { Console.Error.WriteLine(e); }
            finally { loop.Close(); }
        };
        Application.Run(loop); return code;
    }

    private sealed class Harness : IDisposable
    {
        public bool Current = true;
        public readonly Server Server = new();
        public readonly Device Device = new();
        public IdentityPendingConnection Pending = null!;
        public FingerprintIdentityConnection Connection = null!;
        public FingerprintIdentityAttempt Attempt = null!;
        public static async Task<Harness> Create()
        {
            var h = new Harness();
            h.Pending = await IdentityPendingConnection.LoginAsync("http://identity.test", "synthetic", "synthetic",
                Guid.NewGuid(), () => h.Current, CancellationToken.None, handler: h.Server);
            h.Connection = new(h.Pending);
            h.Attempt = new(h.Connection, h.Device, DeviceId);
            return h;
        }
        public void Dispose() { Attempt.Dispose(); Pending.Dispose(); }
    }
    private static async Task Matched()
    {
        using var h = await Harness.Create();
        var phases = new List<IdentityAttemptPhase>(); h.Attempt.Changed += (_, u) => phases.Add(u.Phase);
        await h.Attempt.RunAsync(); await h.Attempt.RunAsync();
        Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.Verified, "Server verification shown");
        Check(h.Attempt.Result?.WorkflowState == "pending", "No automatic finalize or completed attendance");
        Check(phases.Contains(IdentityAttemptPhase.WaitingForFinger) && phases.Contains(IdentityAttemptPhase.SendingResult), "Waiting and sending guidance");
        Check(h.Device.Captures == 1 && h.Server.Completes.Count == 1 && h.Server.Issues.Count == 1, "Single capture; terminal retry is a no-op");
        Check(h.Device.Request!.SequenceEqual(Vector("clock-in-request")), "Original request bytes");
        Check(h.Server.Probes.SequenceEqual(new[] { B64(Vector("capture-probe")), B64(Vector("result-probe")) }), "Exact probe strings to server");
        Check(h.Server.Completes.Single() == B64(Vector("matched-result")), "Exact result bytes");
        Check(h.Server.Schemes.All(x => x == "IdentityPending"), "Only pending scheme, no normal bearer installed");
        Check(h.Server.Finalizes == 0 && h.Server.Revokes == 0, "Connection/UI never finalize or revoke owner context");
        h.Attempt.Dispose(); Check(h.Pending.IsCurrent, "Attempt does not dispose borrowed pending connection");
    }
    private static async Task LostIssue()
    {
        using var h = await Harness.Create(); h.Server.LoseIssue = true;
        await h.Attempt.RunAsync();
        Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.ConnectionLost && h.Device.Captures == 0, "Uncertain issue performs no device I/O");
        await h.Attempt.RunAsync();
        Check(h.Server.Lookups == 1 && h.Server.Issues.Count == 1 && h.Device.Captures == 1, "Lookup original issue before capture");
        Check(h.Server.Issues.Single() == h.Attempt.OriginalRequestId, "Issue UUID frozen before send");
    }
    private static async Task LostComplete(bool committed)
    {
        using var h = await Harness.Create(); h.Server.LoseComplete = true; h.Server.CommitOnLostComplete = committed;
        await h.Attempt.RunAsync(); await h.Attempt.RunAsync();
        Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.Verified, "Recover exact previous result");
        Check(h.Device.Captures == 1 && h.Server.Gets == 1, "Result recovery queries before resend, no rescan");
        Check(h.Server.Completes.Count == (committed ? 1 : 2) && h.Server.Completes.All(x => x == B64(Vector("matched-result"))), "Identical result after uncertain complete");
    }
    private static async Task UnknownIssueCancellation()
    {
        using var h = await Harness.Create(); h.Server.LoseIssue = true; h.Server.StoreLostIssue = false;
        await h.Attempt.RunAsync(); await h.Attempt.CancelAsync();
        Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.ReviewRequired && !h.Attempt.Latest.CanRetry,
            "Unknown issue is not proof of server cancellation");
        Check(h.Server.Issues.Count == 1 && h.Device.Captures == 0 && h.Server.CancelIds.Count == 0,
            "Cancel lookup never issues a new challenge or touches sensor");
        h.Server.RequestStored = true; // Simulate a delayed original server commit.
        await h.Attempt.CancelAsync();
        Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.Cancelled && h.Server.Lookups == 2,
            "Find and cancel delayed original request");
    }
    private static async Task ServerUnavailable()
    {
        foreach (int status in new[] { 404, 503, 403 })
        {
            using var h = await Harness.Create(); h.Server.FailIssueStatus = status;
            await h.Attempt.RunAsync();
            Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.ReviewRequired && h.Device.Captures == 0,
                "Server feature/authorization failure cannot start sensor");
            Check(h.Attempt.Latest.Title == (status == 403 ? "계정·장치 연결 확인 필요" : "서버 지문 기능 확인 필요"),
                "Distinguish API configuration from network failure");
        }
        using var invalid = await Harness.Create(); invalid.Server.WrongIssue = true;
        await invalid.Attempt.RunAsync();
        Check(invalid.Attempt.Latest.Phase == IdentityAttemptPhase.ReviewRequired && invalid.Device.Captures == 0,
            "Wrong issued request ID blocked before sensor capture");
    }
    private static async Task LostDevice()
    {
        using var h = await Harness.Create(); h.Device.Fail = new HttpRequestException();
        await h.Attempt.RunAsync(); await h.Attempt.RunAsync(); await h.Attempt.RunAsync();
        Check(h.Device.Captures == 1 && h.Server.Gets == 2 && h.Server.Completes.Count == 0, "No reconstruction or recapture after missing device result");
        Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.ReviewRequired, "Unresolved server issue is not success");
    }
    private static async Task CancelWhileCapturing()
    {
        using var h = await Harness.Create(); h.Device.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = h.Attempt.RunAsync(); await h.Device.Entered.Task;
        await h.Attempt.RunAsync();
        Check(h.Device.Captures == 1, "Concurrent UI starts do not duplicate capture");
        Check(h.Attempt.Latest.CanCancel, "Cancel stays available during sensor wait");
        await h.Attempt.CancelAsync(); await first;
        Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.Cancelled && h.Server.CancelIds.Count == 1, "Await Pi stop then server-confirmed cancellation");
        await h.Attempt.RunAsync(); Check(h.Device.Captures == 1, "Cancellation cannot become a new scan");
    }
    private static async Task LostCancel()
    {
        using var h = await Harness.Create(); h.Device.Fail = new HttpRequestException();
        await h.Attempt.RunAsync(); h.Server.LoseCancel = true;
        await h.Attempt.CancelAsync();
        Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.ReviewRequired && !h.Attempt.Latest.CanRetry, "Lost cancellation response is not cancellation success");
        await h.Attempt.CancelAsync();
        Check(h.Server.CancelIds.Count == 2 && h.Server.CancelIds.All(id => id == h.Attempt.OriginalCancelId), "Cancellation retries original UUID");
        Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.Cancelled, "Repeated cancellation confirmed by GET");
    }
    private static async Task CancelRace()
    {
        using var h = await Harness.Create(); h.Server.CompleteGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = h.Attempt.RunAsync(); await h.Server.CompleteEntered.Task;
        var cancel = h.Attempt.CancelAsync(); h.Server.CompleteGate.SetResult(true);
        await Task.WhenAll(first, cancel);
        Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.Verified, "Server verified wins race, never mislabel as cancelled");
        Check(h.Attempt.Result?.WorkflowState == "pending" && h.Server.Finalizes == 0, "Race still cannot finalize login");
    }
    private static async Task LateContext(bool dispose)
    {
        using var h = await Harness.Create(); h.Device.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously); h.Device.IgnoreCancellation = true;
        var task = h.Attempt.RunAsync(); await h.Device.Entered.Task;
        if (dispose) h.Attempt.Dispose(); else h.Current = false;
        h.Device.Gate.SetResult(true); await task;
        Check(h.Server.Completes.Count == 0, "Late device completion cannot write under stale/disposed context");
        if (!dispose) Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.ContextEnded, "Stale identity guidance");
    }
    private static async Task Timeout()
    {
        using var h = await Harness.Create(); h.Device.Fail = new OperationCanceledException();
        await h.Attempt.RunAsync();
        Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.TimedOut && h.Attempt.Latest.CanRetry, "Timeout has explicit guidance, not absence or success");
    }
    private static async Task CallerCancelled()
    {
        using var h = await Harness.Create(); h.Device.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource(); var task = h.Attempt.RunAsync(stop.Token);
        await h.Device.Entered.Task; stop.Cancel(); await task;
        Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.ReviewRequired && h.Server.CancelIds.Count == 0, "Local cancellation alone is not server cancellation");
    }
    private static async Task BadResponses()
    {
        foreach (string bad in new[] { "purpose", "receipt", "date", "challenge", "workflow", "null_receipt" })
        {
            using var h = await Harness.Create(); h.Server.Bad = bad;
            await h.Attempt.RunAsync();
            Check(h.Attempt.Latest.Phase == IdentityAttemptPhase.ReviewRequired && h.Attempt.Result is null, "Reject malformed/mismatched response: " + bad);
        }
    }
    private static async Task NormalConnection()
    {
        using var server = new Server(); using var api = new ApiClient("http://identity.test", server);
        await api.LoginAsync("synthetic", "synthetic"); var session = api.CaptureSession();
        foreach (string purpose in new[] { "noon_check", "clock_out" })
        {
            var connection = new FingerprintIdentityConnection(session, purpose);
            var id = Guid.NewGuid(); var issued = await connection.IssueAsync(DeviceId, id, default);
            Check(issued.ChallengeId == Challenge && server.LastPurpose == purpose, "Normal context sends declared purpose: " + purpose);
        }
        Check(server.Schemes.All(x => x == "Bearer"), "No pending auth downgrade on normal context");
        bool refused = false; try { _ = new FingerprintIdentityConnection(session, "clock_in"); } catch (ArgumentException) { refused = true; }
        Check(refused, "Clock-in requires pending connection");
        api.ClearIdentity(); int before = server.Issues.Count;
        try { await new FingerprintIdentityConnection(session, "clock_out").IssueAsync(DeviceId, Guid.NewGuid(), default); }
        catch (OperationCanceledException) { }
        Check(server.Issues.Count == before, "Stale normal session blocked before HTTP");
    }
    private static async Task ContextAfterSuccess()
    {
        using var h = await Harness.Create(); await h.Attempt.RunAsync();
        h.Pending.Dispose();
        Check(h.Attempt.Result is null && h.Attempt.Latest.Phase == IdentityAttemptPhase.ContextEnded, "Owner context cancellation immediately hides previous receipt");
    }
    private static async Task Ui(Form owner, string[] args)
    {
        using (var empty = new FingerprintIdentityForm())
            Check(!Field<Button>(empty, "btnStart").Enabled && !Field<Button>(empty, "btnCancel").Enabled, "Designer default cannot start without owner context");
        using var h = await Harness.Create(); h.Device.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var form = new FingerprintIdentityForm(h.Attempt) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
        form.Show(owner); Field<Button>(form, "btnStart").PerformClick(); var start = form.PendingOperation;
        await h.Device.Entered.Task; await Task.Delay(25);
        Check(Field<Label>(form, "lblStatus").Text == "지문 입력 대기" && Field<Button>(form, "btnCancel").Enabled, "Real form shows waiting and active cancel");
        if (args.Length == 1)
        {
            Directory.CreateDirectory(args[0]);
            using var image = new Bitmap(form.Width, form.Height); form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
            image.Save(Path.Combine(args[0], "fingerprint-waiting.png"));
        }
        Field<Button>(form, "btnCancel").PerformClick(); await form.PendingOperation; await start; await Task.Delay(25);
        Check(Field<Label>(form, "lblStatus").Text == "지문 요청 취소 완료" && !Field<Button>(form, "btnStart").Enabled, "UI cancellation text is server-confirmed");
        form.Close(); Check(h.Pending.IsCurrent, "Closing view keeps owner connection alive");
    }
    private static async Task UiCloseAndDisconnect(Form owner)
    {
        using (var h = await Harness.Create())
        using (var form = new FingerprintIdentityForm(h.Attempt) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) })
        {
            h.Device.Gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            form.Show(owner); Field<Button>(form, "btnStart").PerformClick(); var start = form.PendingOperation;
            await h.Device.Entered.Task; form.Close();
            await form.PendingOperation; await start; await Task.Delay(25);
            Check(!form.Visible && h.Server.CancelIds.Count == 1 && h.Server.State == "cancelled", "Window close awaits same-operation server cancellation");
        }
        using (var h = await Harness.Create())
        using (var form = new FingerprintIdentityForm(h.Attempt) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) })
        {
            h.Device.Fail = new HttpRequestException();
            form.Show(owner); Field<Button>(form, "btnStart").PerformClick(); await form.PendingOperation; await Task.Delay(25);
            Check(Field<Label>(form, "lblStatus").Text == "지문 연결 확인 필요" && Field<Button>(form, "btnStart").Text == "같은 결과 확인", "Disconnected guidance offers same-result recovery");
            h.Current = false; await Task.Delay(350);
            Check(Field<Label>(form, "lblStatus").Text == "접속 확인 필요" && !Field<Button>(form, "btnStart").Enabled, "Timer rejects stale queued UI and predicate-only context expiry");
            form.Close();
        }
    }

    private sealed class Device : IIdentityRelayDevice
    {
        public int Captures;
        public byte[]? Request;
        public Exception? Fail;
        public bool IgnoreCancellation;
        public TaskCompletionSource<bool>? Gate;
        public readonly TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<byte[]> CaptureAsync(byte[] request, Func<byte[], CancellationToken, Task<byte[]>> approve, CancellationToken ct)
        {
            Captures++; Request = request.ToArray(); Entered.TrySetResult(true);
            if (Gate is not null) { if (IgnoreCancellation) await Gate.Task; else await Gate.Task.WaitAsync(ct); }
            if (Fail is not null) throw Fail;
            if (IgnoreCancellation) return Vector("matched-result");
            Check((await approve(Vector("capture-probe"), ct)).SequenceEqual(Vector("capture-approval")), "Capture approval bytes preserved");
            Check((await approve(Vector("result-probe"), ct)).SequenceEqual(Vector("result-approval")), "Result approval bytes preserved");
            return Vector("matched-result");
        }
    }
    private sealed class Server : HttpMessageHandler
    {
        public readonly List<Guid> Issues = [], CancelIds = [];
        public readonly List<string> Probes = [], Completes = [], Schemes = [];
        public int Gets, Lookups, Finalizes, Revokes, FailIssueStatus;
        public string State = "issued", LastPurpose = "clock_in", Bad = "";
        public bool LoseIssue, LoseComplete, CommitOnLostComplete, LoseCancel;
        public bool RequestStored, WrongIssue, StoreLostIssue = true;
        public TaskCompletionSource<bool>? CompleteGate;
        public readonly TaskCompletionSource<bool> CompleteEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private object Ticket(Guid id) => new { challenge_id = Challenge.ToString("D"), client_request_id = WrongIssue ? Guid.NewGuid().ToString("D") : id.ToString("D"), state = State,
            expires_at = DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"), request_b64 = B64(Vector("clock-in-request")) };
        private object Status() => new
        {
            challenge_id = Bad == "challenge" ? Guid.NewGuid().ToString("D") : Challenge.ToString("D"), state = State,
            expires_at = DateTimeOffset.UtcNow.AddMinutes(1).ToString("O"),
            workflow_state = Bad == "workflow" ? "completed" : State == "verified" ? "pending" : "not_started",
            identity_receipt = State != "verified" || Bad == "null_receipt" ? null : new
            {
                receipt_id = Bad == "receipt" ? Guid.Empty.ToString("D") : "40000000-0000-4000-8000-000000000001",
                purpose = Bad == "purpose" ? "clock_out" : LastPurpose,
                verified_at = Bad == "date" ? "2026-10-22T10:00:00+09:00" : DateTimeOffset.UtcNow.ToString("O")
            }
        };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.AbsolutePath;
            using var body = JsonDocument.Parse(request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(ct));
            var v = body.RootElement;
            if (path.EndsWith("/auth/login")) return Reply(new { access_token = "synthetic-normal", token_type = "Bearer",
                expires_at = DateTimeOffset.UtcNow.AddHours(1), user = new UserInfo(5, "synthetic", "테스트", "test", "시험팀", "employee", true) });
            if (path.EndsWith("/login")) return Reply(new { pending_token = new string('p', 43), token_type = "IdentityPending",
                context_ref = new string('c', 43), state = "fingerprint_required", expires_at = DateTimeOffset.UtcNow.AddMinutes(4) });
            Schemes.Add(request.Headers.Authorization?.Scheme ?? "none");
            if (path.EndsWith("/challenges"))
            {
                var id = Guid.Parse(v.GetProperty("client_request_id").GetString()!); Issues.Add(id); LastPurpose = v.GetProperty("purpose").GetString()!;
                if (FailIssueStatus != 0) return new HttpResponseMessage((HttpStatusCode)FailIssueStatus)
                { Content = new StringContent(JsonSerializer.Serialize(new { ok = false, error = new { code = "IDENTITY_WORKFLOW_NOT_READY", message = "synthetic" } }), Encoding.UTF8, "application/json") };
                RequestStored = !LoseIssue || StoreLostIssue;
                if (LoseIssue) { LoseIssue = false; throw new HttpRequestException(); }
                return Reply(Ticket(id));
            }
            if (path.Contains("/requests/")) { Lookups++; return Reply(RequestStored ? Ticket(Guid.Parse(path.Split('/')[^1])) : new { state = "not_recorded" }); }
            if (path.EndsWith("/approvals"))
            {
                string probe = v.GetProperty("probe_b64").GetString()!; Probes.Add(probe);
                return Reply(new { approval_b64 = B64(Vector(probe == B64(Vector("capture-probe")) ? "capture-approval" : "result-approval")) });
            }
            if (path.EndsWith("/complete"))
            {
                Completes.Add(v.GetProperty("result_b64").GetString()!); CompleteEntered.TrySetResult(true);
                if (CompleteGate is not null) await CompleteGate.Task; // Deliberately ignore CT to expose late-response races.
                if (LoseComplete)
                { LoseComplete = false; if (CommitOnLostComplete) State = "verified"; throw new HttpRequestException(); }
                State = "verified"; return Reply(Status());
            }
            if (path.EndsWith("/cancel"))
            {
                CancelIds.Add(Guid.Parse(v.GetProperty("client_request_id").GetString()!));
                if (State == "issued") State = "cancelled";
                if (LoseCancel) { LoseCancel = false; throw new HttpRequestException(); }
                return Reply(new { challenge_id = Challenge, state = State });
            }
            if (request.Method == HttpMethod.Get) { Gets++; return Reply(Status()); }
            if (path.EndsWith("/finalize")) Finalizes++;
            if (path.EndsWith("/revoke")) Revokes++;
            throw new InvalidOperationException("Unexpected identity test route");
        }
    }
}
