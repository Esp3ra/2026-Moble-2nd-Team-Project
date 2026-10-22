using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;

internal static partial class Program
{
    private static readonly Guid ClockChallenge = Guid.Parse("20000000-0000-4000-8000-000000000001");
    private static JsonElement ClockReceipt() => JsonSerializer.SerializeToElement(new
    { state = "completed", workflow_receipt_id = Guid.NewGuid(), work_session_ref = Work, work_date = "2026-10-22" });
    private static async Task PresentWorkday(Form owner, FingerprintWorkdayOperation operation)
    {
        using var form = new FingerprintWorkdayForm(operation, a => a.RunAsync(), autoStart: true);
        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        form.Shown += (_, _) => shown.TrySetResult();
        form.Show(owner); await shown.Task; await form.PendingOperation; form.Dispose();
    }
    private static async Task ClockOutFlow(Form owner, bool lose)
    {
        var server = new ClockServer(); using var api = new ApiClient("http://clock.test", server);
        await api.LoginAsync("synthetic", "synthetic"); var source = new Source(); var monitor = new Monitor();
        int prepares = 0, calls = 0, finishes = 0, shown = 0, uiThread = Environment.CurrentManagedThreadId;
        var device = new ClockDevice(server); FingerprintWorkdayOperation? original = null;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        monitor.StopHold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var host = new LoginWorkdayActivity(owner, api.CaptureSession(), source.Read, Options, monitor,
            automaticallyPoll: false, deviceFactory: _ => device,
            prepareClockOut: async (binding, stop, ct) =>
            {
                prepares++; entered.TrySetResult();
                Check(Environment.CurrentManagedThreadId == uiThread, "Common MainForm preparation is dispatched to UI");
                Check(binding.WorkSessionId == Work && binding.ChallengeId == ClockChallenge && binding.WorkDate == Before.WorkDate,
                    "Prepare gets the server work and verified original challenge");
                await stop();
                Check(!monitor.IsRunning && monitor.Stops == 1, "Monitor is drained before common finalization");
                return new(binding, () => api.CurrentUser is not null, async _ =>
                {
                    calls++; await Task.Yield();
                    if (lose && calls == 1) throw new HttpRequestException("synthetic committed response lost");
                    return ClockReceipt();
                }, () => { finishes++; api.ClearIdentity(); });
            }, presentClockOut: async op =>
            {
                shown++; original ??= op;
                Check(ReferenceEquals(original, op), "Reopened clock-out keeps the same operation");
                await PresentWorkday(owner, op);
            });
        await host.StartAsync(); Check(host.CanClockOut, "Explicit common preparation enables clock-out");
        var pending = host.ShowClockOutAsync(); await entered.Task;
        int reads = source.Reads; await host.CheckAsync(); await host.ShowNoonAsync();
        Check(!pending.IsCompleted && calls == 0 && !host.CanRefresh && !host.CanCheckNoon && source.Reads == reads,
            "Clock-out drain blocks polling/noon and waits before any finalize");
        monitor.StopHold.SetResult(); await pending;
        if (lose)
        {
            Check(finishes == 0 && host.CanClockOut && original?.HasFinalizeAttempt == true, "Lost finalization retains original result owner");
            using (var view = new ActivityPresenceForm(monitor)) { view.Show(owner); view.Close(); }
            Check(prepares == 1 && api.CurrentUser is not null && !monitor.IsRunning, "Borrowed view cannot discard recovery or restart monitoring");
            await host.ShowClockOutAsync();
        }
        Check(prepares == 1 && calls == (lose ? 2 : 1) && shown == (lose ? 2 : 1), "One common owner survives popup reopens");
        Check(device.Captures == 1 && server.OrdinaryFinalizes == 0 && finishes == 1, "No rescan or ordinary finalize; confirmed finish occurs once");
        Check(!host.IsCurrent && api.CurrentUser is null && monitor.Starts == 1 && monitor.Stops == 1, "Confirmed clock-out ends only the owned login and monitor");
    }
    private static async Task ClockOutFailure(Form owner)
    {
        using var api = await Login(); var monitor = new Monitor();
        using var disconnected = new LoginWorkdayActivity(owner, api.CaptureSession(), new Source().Read, Options, monitor, automaticallyPoll: false);
        await disconnected.StartAsync(); await disconnected.ShowClockOutAsync();
        Check(!disconnected.CanClockOut && monitor.IsRunning, "No common connector means no automatic clock-out scan");
        var server = new ClockServer(); using var second = new ApiClient("http://clock.test", server);
        await second.LoginAsync("synthetic", "synthetic"); var source = new Source(); var secondMonitor = new Monitor();
        var device = new ClockDevice(server); int prepares = 0, writes = 0;
        using var host = new LoginWorkdayActivity(owner, second.CaptureSession(), source.Read, Options, secondMonitor,
            deviceFactory: _ => device, automaticallyPoll: false,
            prepareClockOut: async (binding, stop, ct) =>
            {
                prepares++; await stop();
                return new(binding with { OriginalFinalizeId = Guid.NewGuid() }, () => true,
                    _ => { writes++; return Task.FromResult(ClockReceipt()); }, () => throw new VerificationFailure("Wrong owner finished"));
            }, presentClockOut: op => PresentWorkday(owner, op));
        await host.StartAsync(); await host.ShowClockOutAsync();
        Check(writes == 0 && server.OrdinaryFinalizes == 0 && second.CurrentUser is not null, "Wrong common binding is rejected before finalize");
        Check(!secondMonitor.IsRunning && !host.CanRefresh && host.CanClockOut, "Failed preparation cannot silently resume work");
        await host.ShowClockOutAsync();
        Check(prepares == 1 && device.Captures == 1 && writes == 0, "Retry retains the same verified challenge and rejects the same wrong owner");
    }
    private sealed class ClockDevice(ClockServer server) : IIdentityRelayDevice
    {
        public int Captures;
        public async Task<byte[]> CaptureAsync(byte[] bytes, Func<byte[], CancellationToken, Task<byte[]>> approve, CancellationToken ct)
        {
            Captures++; await approve(server.Wire("capture-probe"), ct); await approve(server.Wire("result-probe"), ct);
            return server.Wire("matched-result");
        }
    }
    private sealed class ClockServer : HttpMessageHandler
    {
        public int OrdinaryFinalizes;
        private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        public byte[] Wire(string name)
        {
            using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "identity-v2.json")));
            var vector = fixture.RootElement.GetProperty("vectors").EnumerateArray().Single(v => v.GetProperty("name").GetString() == name);
            var envelope = JsonNode.Parse(vector.GetProperty("envelope_json").GetString()!)!;
            var b = envelope["payload_b64"]!.GetValue<string>();
            var p = JsonNode.Parse(Convert.FromBase64String(b.Replace('-', '+').Replace('_', '/') + new string('=', (4 - b.Length % 4) % 4)))!;
            p["purpose"] = "clock_out"; p["work_session_ref"] = Work.ToString("D"); p["work_date"] = "2026-10-22";
            if (name != "clock-in-request") p["ticket_digest"] = Convert.ToHexString(SHA256.HashData(Wire("clock-in-request"))).ToLowerInvariant();
            envelope["payload_b64"] = B64(Encoding.UTF8.GetBytes(p.ToJsonString()));
            return Encoding.UTF8.GetBytes(envelope.ToJsonString()); // Structural double; real integration checks MAC.
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/auth/login")) return Reply(new LoginResult("synthetic", "Bearer", DateTimeOffset.UtcNow.AddHours(1), User));
            using var doc = JsonDocument.Parse(request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(ct));
            var data = doc.RootElement;
            if (path.EndsWith("/challenges")) return Reply(new { challenge_id = ClockChallenge, client_request_id = data.GetProperty("client_request_id").GetString(),
                state = "issued", expires_at = DateTimeOffset.UtcNow.AddMinutes(1), request_b64 = B64(Wire("clock-in-request")) });
            if (path.EndsWith("/approvals")) return Reply(new { approval_b64 = B64(Wire(data.GetProperty("probe_b64").GetString() == B64(Wire("capture-probe")) ? "capture-approval" : "result-approval")) });
            if (path.EndsWith("/complete") || path.EndsWith(ClockChallenge.ToString("D"))) return Reply(new { challenge_id = ClockChallenge, state = "verified", workflow_state = "pending",
                expires_at = DateTimeOffset.UtcNow.AddMinutes(1), identity_receipt = new { receipt_id = Guid.NewGuid(), purpose = "clock_out", verified_at = DateTimeOffset.UtcNow } });
            if (path.EndsWith("/finalize")) OrdinaryFinalizes++;
            throw new VerificationFailure("Unexpected ordinary clock-out route: " + path);
        }
    }
}
