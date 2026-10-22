using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;

internal static class Program
{
    private static int checks;
    private static readonly JsonDocument Fixture = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "identity-v2.json")));
    private static readonly Guid Challenge = Guid.Parse("20000000-0000-4000-8000-000000000001");
    private static readonly string DeviceId = Fixture.RootElement.GetProperty("device_id").GetString()!;
    private static byte[] Vector(string name) => Encoding.UTF8.GetBytes(Fixture.RootElement.GetProperty("vectors").EnumerateArray()
        .Single(x => x.GetProperty("name").GetString() == name).GetProperty("envelope_json").GetString()!);
    private static string B64(byte[] raw) => Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string raw) => Convert.FromBase64String(raw.Replace('-', '+').Replace('_', '/') + new string('=', (4 - raw.Length % 4) % 4));
    private static IdentityStatus Verified() => new(Challenge, "verified", "pending", new(Guid.Parse("60000000-0000-4000-8000-000000000001"), "clock_in", DateTimeOffset.Parse("2026-10-21T08:00:15Z")));
    private static void Check(bool condition, string description)
    { checks++; if (!condition) throw new InvalidOperationException(description); }
    private static async Task Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { Check(true, typeof(T).Name); return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
    private static async Task<int> Main()
    {
        try
        {
            Vectors(); await Matched(); await Retry(false); await Retry(true);
            await SingleFlight(); await LostCapture(); await LateCapture(false); await LateCapture(true);
            await LateApproval(); await LateCompletion(); await WrongReceipt(); await FailedFinger(); await WrongWire();
            Console.WriteLine($"PASS identity relay: {checks} assertions; synthetic wire/transport/current-context fixtures; no keys, real server, device, DB, login or attendance effects.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void Vectors()
    {
        foreach (var v in Fixture.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var raw = Encoding.UTF8.GetBytes(v.GetProperty("envelope_json").GetString()!);
            Check(Decode(B64(raw)).SequenceEqual(raw), "All 13 envelope bytes survive base64 transport");
            Check(Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant() == v.GetProperty("digest").GetString(), "Full original bytes digest");
        }
    }
    private sealed class Harness : IDisposable
    {
        public bool Current = true;
        public readonly CancellationTokenSource Context = new();
        public readonly Server Server = new();
        public readonly Device Device;
        public readonly FingerprintIdentityRelay Relay;
        public readonly byte[] Request;
        public Harness()
        {
            Device = new Device(); Request = Vector("clock-in-request");
            Relay = new(Challenge, DeviceId, "clock_in", Request, () => Current, Context.Token, Server, Device);
        }
        public void Dispose() { Relay.Dispose(); Context.Dispose(); }
    }
    private static async Task Matched()
    {
        using var h = new Harness();
        h.Request[0] = 0; // Constructor must have captured a private copy.
        var result = await h.Relay.RunAsync();
        Check(result.State == "verified" && result.WorkflowState == "pending", "Identity only, no workflow completion");
        Check(h.Device.Captures == 1 && h.Server.Approvals.Count == 2 && h.Server.Results.Count == 1, "One capture, distinct scope round trips, one completion");
        Check(Decode(h.Server.Results[0]).SequenceEqual(Vector("matched-result")), "Original signed result exactly relayed");
        Check(h.Device.Approvals[0].SequenceEqual(Vector("capture-approval")) && h.Device.Approvals[1].SequenceEqual(Vector("result-approval")), "Original approvals exactly relayed");
    }
    private static async Task Retry(bool committed)
    {
        using var h = new Harness(); h.Server.LoseFirst = true;
        h.Server.Lookup = committed ? Verified() : new(Challenge, "issued", "not_started", null);
        await Throws<HttpRequestException>(() => h.Relay.RunAsync());
        Check(h.Relay.HasPendingResult, "Freeze exact result before completion request");
        h.Device.Result.AsSpan().Fill(0); // Late mutation must not change frozen completion bytes.
        var status = await h.Relay.ResumeCompletionAsync();
        Check(h.Server.Gets == 1 && h.Device.Captures == 1, "Recovery queries first and never recaptures");
        Check(status.State == "verified", "Server authoritative verification returned");
        Check(h.Server.Results.Count == (committed ? 1 : 2), "Already committed lookup avoids extra complete");
        Check(h.Server.Results.All(r => r == B64(Vector("matched-result"))), "Uncommitted retry uses same exact bytes/result ID");
    }
    private static async Task SingleFlight()
    {
        using var h = new Harness(); h.Device.Gate = new();
        var first = h.Relay.RunAsync();
        await Throws<InvalidOperationException>(() => h.Relay.RunAsync());
        Check(h.Device.Captures == 1, "Concurrent start has single capture");
        h.Device.Gate.SetResult(true); await first;
    }
    private static async Task LostCapture()
    {
        using var h = new Harness(); h.Device.Fail = true;
        await Throws<HttpRequestException>(() => h.Relay.RunAsync());
        await Throws<InvalidOperationException>(() => h.Relay.RunAsync());
        Check(h.Device.Captures == 1 && h.Server.Results.Count == 0, "Lost device result cannot be reconstructed or recaptured");
    }
    private static async Task LateCapture(bool dispose)
    {
        using var h = new Harness(); h.Device.Gate = new(); h.Device.SkipApprovals = true;
        var pending = h.Relay.RunAsync();
        if (dispose) h.Relay.Dispose(); else { h.Current = false; h.Context.Cancel(); }
        h.Device.Gate.SetResult(true);
        await Throws<OperationCanceledException>(() => pending);
        Check(h.Server.Results.Count == 0, "Late capture after disposal/context replacement never completes");
    }
    private static async Task LateApproval()
    {
        using var h = new Harness(); h.Server.ApprovalGate = new();
        var pending = h.Relay.RunAsync(); h.Current = false;
        h.Server.ApprovalGate.SetResult(true);
        await Throws<OperationCanceledException>(() => pending);
        Check(h.Device.Approvals.Count == 0 && h.Server.Results.Count == 0, "Late approval cannot return to Pi or complete");
    }
    private static async Task LateCompletion()
    {
        using var h = new Harness(); h.Server.AfterComplete = () => h.Current = false;
        await Throws<OperationCanceledException>(() => h.Relay.RunAsync());
        Check(h.Server.Results.Count == 1, "Cannot undo server commit, but no late success returned");
    }
    private static async Task WrongReceipt()
    {
        using var h = new Harness(); h.Server.Response = Verified() with { ChallengeId = Guid.NewGuid() };
        await Throws<InvalidDataException>(() => h.Relay.RunAsync());
        using var other = new Harness(); other.Server.Response = Verified() with { WorkflowState = "completed" };
        await Throws<InvalidDataException>(() => other.Relay.RunAsync());
        using var purpose = new Harness(); purpose.Server.Response = Verified() with { IdentityReceipt = Verified().IdentityReceipt! with { Purpose = "clock_out" } };
        await Throws<InvalidDataException>(() => purpose.Relay.RunAsync());
    }
    private static async Task FailedFinger()
    {
        using var h = new Harness(); h.Device.Result = Vector("no_match-result");
        h.Server.Response = new(Challenge, "rejected", "not_started", null);
        Check((await h.Relay.RunAsync()).State == "rejected", "No match never opens normal session");
        using var invalid = new Harness(); invalid.Device.Result = Vector("no_match-result");
        await Throws<InvalidDataException>(() => invalid.Relay.RunAsync());
    }
    private static async Task WrongWire()
    {
        using var h = new Harness(); h.Device.Result = Vector("clock-in-request");
        await Throws<InvalidDataException>(() => h.Relay.RunAsync());
        Check(h.Server.Results.Count == 0, "Wrong kind never submitted");
        using var changed = new Harness();
        changed.Device.Result = ReplacePayload(Vector("matched-result"), "ticket_digest", new string('0', 64));
        await Throws<InvalidDataException>(() => changed.Relay.RunAsync());
        Check(changed.Server.Results.Count == 0, "Wrong request digest never submitted");
    }
    private static byte[] ReplacePayload(byte[] raw, string name, object value)
    {
        var envelope = JsonSerializer.Deserialize<Dictionary<string, object>>(raw)!;
        using var doc = JsonDocument.Parse(raw);
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(Decode(doc.RootElement.GetProperty("payload_b64").GetString()!))!;
        payload[name] = value; envelope["payload_b64"] = B64(JsonSerializer.SerializeToUtf8Bytes(payload));
        return JsonSerializer.SerializeToUtf8Bytes(envelope); // Invalid MAC; transport structural test only.
    }
    private sealed class Server : IIdentityRelayServer
    {
        public List<string> Approvals = new(), Results = new();
        public int Gets;
        public bool LoseFirst;
        public TaskCompletionSource<bool>? ApprovalGate;
        public Action? AfterComplete;
        public IdentityStatus Response = Verified(), Lookup = Verified();
        public async Task<string> ApproveAsync(Guid id, string probeB64, CancellationToken cancellation)
        {
            Check(id == Challenge, "Approval target challenge");
            Approvals.Add(probeB64);
            if (ApprovalGate is not null) await ApprovalGate.Task; // Deliberately ignores cancellation to test caller guards.
            string scope = Approvals.Count == 1 ? "capture" : "result";
            Check(Decode(probeB64).SequenceEqual(Vector(scope + "-probe")), "Original probe bytes exactly wrapped");
            return B64(Vector(scope + "-approval"));
        }
        public Task<IdentityStatus> CompleteAsync(Guid id, string resultB64, CancellationToken cancellation)
        {
            Check(id == Challenge, "Completion target challenge"); Results.Add(resultB64); AfterComplete?.Invoke();
            if (LoseFirst && Results.Count == 1) throw new HttpRequestException("Synthetic lost reply");
            return Task.FromResult(Response);
        }
        public Task<IdentityStatus> GetAsync(Guid id, CancellationToken cancellation)
        { Check(id == Challenge, "Lookup target challenge"); Gets++; return Task.FromResult(Lookup); }
    }
    private sealed class Device : IIdentityRelayDevice
    {
        public int Captures;
        public bool Fail, SkipApprovals;
        public byte[] Result = Vector("matched-result");
        public TaskCompletionSource<bool>? Gate;
        public List<byte[]> Approvals = new();
        public async Task<byte[]> CaptureAsync(byte[] request, Func<byte[], CancellationToken, Task<byte[]>> approvals, CancellationToken cancellation)
        {
            Captures++; Check(request.SequenceEqual(Vector("clock-in-request")), "Original request immutable across caller mutation");
            if (Gate is not null) await Gate.Task;
            if (Fail) throw new HttpRequestException("Synthetic device reply lost");
            if (!SkipApprovals)
            {
                Approvals.Add(await approvals(Vector("capture-probe"), cancellation));
                Approvals.Add(await approvals(Vector("result-probe"), cancellation));
            }
            return Result;
        }
    }
}
