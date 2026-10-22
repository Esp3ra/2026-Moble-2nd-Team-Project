using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;

internal static partial class Program
{
    private static readonly DateOnly WorkDay = new(2026, 10, 22);
    private static async Task WorkdayChecks(Form owner)
    {
        foreach (string purpose in new[] { "noon_check", "clock_out" })
        {
            var server = new WorkServer(purpose);
            using var api = new ApiClient("http://work.test", server);
            await api.LoginAsync("synthetic", "synthetic");
            var device = new WorkDevice(server);
            using var operation = new FingerprintWorkdayOperation(api.CaptureSession(), Work, WorkDay, purpose, DeviceId, device);
            await Refused(() => operation.CompleteAsync());
            Check(server.FinalizeIds.Count == 0, "No finalize before actual attempt verification");
            await operation.Attempt.RunAsync();
            Check(operation.Attempt.Result?.State == "verified" && device.Captures == 1, "Verified actual relay for work purpose");
            server.Lose = true;
            await Refused(() => operation.CompleteAsync());
            Check(operation.HasFinalizeAttempt && operation.Completed is null && server.Saves == 1, "Lost saved result remains unknown");
            using var view = new FingerprintWorkdayForm(operation, _ => throw new InvalidOperationException("Must not rescan saved result"));
            view.Show(owner);
            Check(Field<Button>(view, "btnConfirm").Text == "같은 저장 결과 확인", "Reopened view retains result recovery");
            Field<Button>(view, "btnConfirm").PerformClick(); await view.PendingOperation;
            Check(view.CompletedReceipt?.WorkSessionId == Work && view.CompletedReceipt.Purpose == purpose, "Only matching saved work receipt completes UI");
            Check(server.FinalizeIds.Count == 2 && server.FinalizeIds.Distinct().Single() == operation.OriginalFinalizeId
                && server.Saves == 1 && device.Captures == 1, "One scan/write and original finalize UUID after loss");
            Check(api.CurrentUser is not null, "View does not mutate normal login; host closes clock-out separately");
            int before = server.FinalizeIds.Count;
            await operation.CompleteAsync(); Check(server.FinalizeIds.Count == before, "Completed repeats use retained validated receipt");
            view.Close();
        }
        foreach (string bad in new[] { "work", "day", "state", "receipt", "token" })
        {
            var server = new WorkServer("clock_out") { Bad = bad };
            using var api = new ApiClient("http://work.test", server); await api.LoginAsync("synthetic", "synthetic");
            using var operation = new FingerprintWorkdayOperation(api.CaptureSession(), Work, WorkDay, "clock_out", DeviceId, new WorkDevice(server));
            await operation.Attempt.RunAsync(); await Refused(() => operation.CompleteAsync());
            Check(operation.Completed is null && operation.HasFinalizeAttempt, "Invalid receipt stays recoverable: " + bad);
        }
        foreach (bool day in new[] { false, true })
        {
            var server = new WorkServer("noon_check");
            using var api = new ApiClient("http://work.test", server); await api.LoginAsync("synthetic", "synthetic");
            var device = new WorkDevice(server);
            using var operation = new FingerprintWorkdayOperation(api.CaptureSession(), day ? Work : Guid.NewGuid(),
                day ? WorkDay.AddDays(-1) : WorkDay, "noon_check", DeviceId, device);
            await operation.Attempt.RunAsync();
            Check(device.Captures == 0 && operation.Attempt.Result is null, "Stale work/day blocked before sensor");
        }
        await WorkdayLate(owner); await WorkdayClose(owner); await WorkdayCancellation();
    }
    private static async Task WorkdayLate(Form owner)
    {
        var server = new WorkServer("clock_out");
        using var api = new ApiClient("http://work.test", server); await api.LoginAsync("synthetic", "synthetic");
        using var operation = new FingerprintWorkdayOperation(api.CaptureSession(), Work, WorkDay, "clock_out", DeviceId, new WorkDevice(server));
        await operation.Attempt.RunAsync();
        server.Hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var done = operation.CompleteAsync(); await server.Entered.Task;
        await api.LoginAsync("synthetic", "synthetic"); server.Hold.SetResult(true);
        await Refused(() => done);
        Check(operation.Completed is null && api.CurrentUser is not null, "Old clock-out result does not close a newer login");
    }
    private static async Task WorkdayClose(Form owner)
    {
        var server = new WorkServer("clock_out");
        using var api = new ApiClient("http://work.test", server); await api.LoginAsync("synthetic", "synthetic");
        using var operation = new FingerprintWorkdayOperation(api.CaptureSession(), Work, WorkDay, "clock_out", DeviceId, new WorkDevice(server));
        await operation.Attempt.RunAsync();
        server.Hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var view = new FingerprintWorkdayForm(operation, _ => Task.CompletedTask); view.Show(owner);
        Field<Button>(view, "btnConfirm").PerformClick(); await server.Entered.Task;
        view.Close();
        Check(view.Visible && !Field<Button>(view, "btnClose").Enabled, "Close cannot race clock-out commit");
        server.Hold.SetResult(true); await view.PendingOperation;
        Check(view.CompletedReceipt is not null, "Receipt received before close");
        Render(view, "clock-out-complete.png"); view.Close();
        var lossServer = new WorkServer("noon_check") { Lose = true };
        using var second = new ApiClient("http://work.test", lossServer); await second.LoginAsync("synthetic", "synthetic");
        using var pending = new FingerprintWorkdayOperation(second.CaptureSession(), Work, WorkDay, "noon_check", DeviceId, new WorkDevice(lossServer));
        using var lost = new FingerprintWorkdayForm(pending, a => a.RunAsync()); lost.Show(owner);
        Field<Button>(lost, "btnConfirm").PerformClick(); await lost.PendingOperation;
        lost.Close(); Check(lost.Visible, "First close explains unknown saved result");
        Render(lost, "noon-result-review.png"); lost.Close();
        Check(pending.IsCurrent && pending.HasFinalizeAttempt, "Closing borrowed view retains operation for same result recovery");
        await pending.CompleteAsync(); Check(lossServer.Saves == 1, "Reopen recovery does not duplicate noon write");
    }
    private static async Task WorkdayCancellation()
    {
        var server = new WorkServer("noon_check");
        using var api = new ApiClient("http://work.test", server); await api.LoginAsync("synthetic", "synthetic");
        using var operation = new FingerprintWorkdayOperation(api.CaptureSession(), Work, WorkDay, "noon_check", DeviceId, new WorkDevice(server));
        await operation.CancelAsync();
        Check(operation.Attempt.Latest.Phase == IdentityAttemptPhase.Cancelled && server.FinalizeIds.Count == 0, "Pre-request cancellation writes no attendance");
        var device = new WorkDevice(server);
        using var saved = new FingerprintWorkdayOperation(api.CaptureSession(), Work, WorkDay, "noon_check", DeviceId, device);
        await saved.Attempt.RunAsync(); server.Lose = true;
        await Refused(() => saved.CompleteAsync());
        bool blocked = false;
        try { await saved.CancelAsync(); } catch (InvalidOperationException) { blocked = true; }
        Check(blocked && server.CancelCalls == 0, "No cancel/rollback after possible commit");
    }
    private sealed class WorkDevice(WorkServer server) : IIdentityRelayDevice
    {
        public int Captures;
        public async Task<byte[]> CaptureAsync(byte[] requestBytes, Func<byte[], CancellationToken, Task<byte[]>> approvalRelay, CancellationToken ct)
        {
            Captures++; await approvalRelay(server.Wire("capture-probe"), ct); await approvalRelay(server.Wire("result-probe"), ct);
            return server.Wire("matched-result");
        }
    }
    private sealed class WorkServer(string purpose) : HttpMessageHandler
    {
        public bool Lose; public string Bad = ""; public int Saves, CancelCalls;
        public List<Guid> FinalizeIds = [];
        public TaskCompletionSource<bool>? Hold;
        public TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public byte[] Wire(string name)
        {
            var e = JsonNode.Parse(Vector(name))!.AsObject();
            string b = e["payload_b64"]!.GetValue<string>();
            var p = JsonNode.Parse(Convert.FromBase64String(b.Replace('-', '+').Replace('_', '/') + new string('=', (4 - b.Length % 4) % 4)))!;
            p["purpose"] = purpose; p["work_session_ref"] = Work.ToString("D"); p["work_date"] = "2026-10-22";
            if (name != "clock-in-request") p["ticket_digest"] = Convert.ToHexString(SHA256.HashData(Wire("clock-in-request"))).ToLowerInvariant();
            e["payload_b64"] = B64(Encoding.UTF8.GetBytes(p.ToJsonString()));
            // Intentionally structural test double: only real server/Pi integration authenticates MACs.
            return Encoding.UTF8.GetBytes(e.ToJsonString());
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            using var doc = JsonDocument.Parse(request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(ct));
            var data = doc.RootElement;
            if (path.EndsWith("/auth/login")) return Reply(new LoginResult(new string('w', 43), "Bearer", DateTimeOffset.UtcNow.AddHours(1),
                new UserInfo(5, "synthetic", "테스트", "T5", "시험팀", "employee", true)));
            Check(request.Headers.Authorization?.Scheme == "Bearer", "Work operations use captured normal session");
            if (path.EndsWith("/challenges")) return Reply(new { challenge_id = Challenge, client_request_id = data.GetProperty("client_request_id").GetString(),
                state = "issued", expires_at = DateTimeOffset.UtcNow.AddMinutes(1), request_b64 = B64(Wire("clock-in-request")) });
            if (path.EndsWith("/approvals"))
            {
                bool capture = data.GetProperty("probe_b64").GetString() == B64(Wire("capture-probe"));
                return Reply(new { approval_b64 = B64(Wire(capture ? "capture-approval" : "result-approval")) });
            }
            if (path.EndsWith("/complete")) return Reply(new { challenge_id = Challenge, state = "verified", workflow_state = "pending",
                expires_at = DateTimeOffset.UtcNow.AddMinutes(1), identity_receipt = new { receipt_id = Guid.NewGuid(), purpose, verified_at = DateTimeOffset.UtcNow } });
            if (path.EndsWith("/finalize"))
            {
                FinalizeIds.Add(data.GetProperty("client_request_id").GetGuid()); Entered.TrySetResult(true);
                if (Hold is not null) await Hold.Task;
                Saves = 1;
                if (Lose) { Lose = false; throw new HttpRequestException("Synthetic lost saved work response"); }
                var result = new JsonObject { ["state"] = Bad == "state" ? "verified" : "completed",
                    ["workflow_receipt_id"] = (Bad == "receipt" ? Guid.Empty : Guid.Parse("80000000-0000-4000-8000-000000000001")).ToString("D"),
                    ["work_session_ref"] = (Bad == "work" ? Guid.NewGuid() : Work).ToString("D"), ["work_date"] = Bad == "day" ? "2026-10-21" : "2026-10-22" };
                if (Bad == "token") result["access_token"] = "unexpected";
                return Reply(result);
            }
            if (path.EndsWith("/cancel")) CancelCalls++;
            throw new InvalidOperationException("Unexpected workday test route");
        }
    }
    private static async Task LoopbackWorkday(Form owner, string[] args)
    {
        if (args.Length != 8 || args[4] is not ("noon_check" or "clock_out") || args[7] is not ("normal" or "loss")) throw new ArgumentException("Explicit test workday required");
        var server = ApiClient.ValidateUrl(args[1]); var pi = ApiClient.ValidateUrl(args[2]);
        if (new[] { server, pi }.Any(u => u.Scheme != "http" || u.Host != "127.0.0.1")) throw new InvalidOperationException("Owned loopback only");
        var direct = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false, UseProxy = false };
        using var api = new ApiClient(server.ToString(), args[7] == "loss" ? new LoseFinalize(direct) : direct);
        await api.LoginAsync(Environment.GetEnvironmentVariable("MEMBER5_TEST_LOGIN_ID")!, Environment.GetEnvironmentVariable("MEMBER5_TEST_PASSWORD")!);
        using var device = new HttpFingerprintIdentityDevice(pi.ToString());
        using var operation = new FingerprintWorkdayOperation(api.CaptureSession(), Guid.Parse(args[5]), DateOnly.ParseExact(args[6], "yyyy-MM-dd"), args[4], Guid.Parse(args[3]), device);
        int scanner = 0;
        using var view = new FingerprintWorkdayForm(operation, async a => { scanner++; await a.RunAsync(); });
        view.Show(owner); Field<Button>(view, "btnConfirm").PerformClick(); await view.PendingOperation;
        if (args[7] == "loss")
        {
            Check(view.CompletedReceipt is null && operation.HasFinalizeAttempt, "Real work response loss is not success");
            Field<Button>(view, "btnConfirm").PerformClick(); await view.PendingOperation;
        }
        Check(view.CompletedReceipt?.WorkSessionId == Guid.Parse(args[5]) && scanner == 1, "Real server work receipt after one scanner operation");
        view.Close();
        Console.WriteLine(JsonSerializer.Serialize(new { passed = true, purpose = args[4], mode = args[7], scanner_dialogs = scanner, checks }));
    }
}
