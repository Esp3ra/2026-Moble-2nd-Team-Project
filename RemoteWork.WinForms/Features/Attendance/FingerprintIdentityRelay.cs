using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RemoteWork.WinForms.Features.Attendance;

// Internal transport seams for PR62 RW-ID-v2. No HTTP routes/auth scheme or
// LoginForm wiring is installed here. Implementations must bind to one captured
// current IdentityPending/normal context; a display UserInfo is insufficient.
public sealed record IdentityReceipt(Guid ReceiptId, string Purpose, DateTimeOffset VerifiedAt);
public sealed record IdentityStatus(Guid ChallengeId, string State, string WorkflowState, IdentityReceipt? IdentityReceipt);
public interface IIdentityRelayServer
{
    Task<string> ApproveAsync(Guid challengeId, string probeB64, CancellationToken cancellation);
    Task<IdentityStatus> CompleteAsync(Guid challengeId, string resultB64, CancellationToken cancellation);
    Task<IdentityStatus> GetAsync(Guid challengeId, CancellationToken cancellation);
}
public interface IIdentityRelayDevice
{
    Task<byte[]> CaptureAsync(byte[] requestBytes,
        Func<byte[], CancellationToken, Task<byte[]>> approvalRelay, CancellationToken cancellation);
}

/// <summary>One issued challenge, one sensor invocation, exact RAM-only bytes.
/// A verified return is ONLY identity verification; never login/attendance completion.
/// Server finalize, token issuance, cancellation/revocation and issuance recovery
/// remain separate service/owner responsibilities. No keys or credentials live here.
/// </summary>
public sealed class FingerprintIdentityRelay : IDisposable
{
    private readonly Guid challengeId;
    private readonly string deviceId, purpose, ticketDigest;
    private readonly byte[] request;
    private readonly Func<bool> contextCurrent;
    private readonly CancellationToken contextCancellation;
    private readonly IIdentityRelayServer server;
    private readonly IIdentityRelayDevice device;
    private readonly CancellationTokenSource lifetime = new();
    private int running, disposed;
    private bool captureStarted, localMatched;
    private string? frozenResult;
    public bool HasPendingResult => frozenResult is not null;

    public FingerprintIdentityRelay(Guid challengeId, string deviceId, string purpose, byte[] request,
        Func<bool> contextCurrent, CancellationToken contextCancellation,
        IIdentityRelayServer server, IIdentityRelayDevice device)
    {
        if (challengeId == Guid.Empty || !Guid.TryParseExact(deviceId, "D", out var id) || id.ToString("D") != deviceId
            || purpose is not ("clock_in" or "noon_check" or "clock_out")) throw Invalid();
        this.challengeId = challengeId; this.deviceId = deviceId; this.purpose = purpose;
        this.request = request.ToArray();
        this.contextCurrent = contextCurrent ?? throw new ArgumentNullException(nameof(contextCurrent));
        this.contextCancellation = contextCancellation;
        this.server = server ?? throw new ArgumentNullException(nameof(server));
        this.device = device ?? throw new ArgumentNullException(nameof(device));
        var payload = ReadWire(this.request, "id_request");
        if (Text(payload, "purpose") != purpose) throw Invalid();
        ticketDigest = Convert.ToHexString(SHA256.HashData(this.request)).ToLowerInvariant();
    }

    public Task<IdentityStatus> RunAsync(CancellationToken cancellation = default) => Execute(false, cancellation);
    public Task<IdentityStatus> ResumeCompletionAsync(CancellationToken cancellation = default) => Execute(true, cancellation);

    private async Task<IdentityStatus> Execute(bool resume, CancellationToken cancellation)
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
            throw new InvalidOperationException("본인 확인이 이미 진행 중입니다.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(contextCancellation, lifetime.Token, cancellation);
        try
        {
            Current(linked.Token);
            if (resume || captureStarted)
            {
                if (frozenResult is null) throw new InvalidOperationException("이전 결과를 재구성하거나 자동으로 다시 스캔할 수 없습니다.");
                var status = await server.GetAsync(challengeId, linked.Token);
                Current(linked.Token);
                CheckStatus(status, allowIssued: true);
                if (status.State != "issued") return status;
            }
            else
            {
                captureStarted = true;
                var result = await device.CaptureAsync(request.ToArray(), async (probe, deviceCancellation) =>
                {
                    using var approvalLifetime = CancellationTokenSource.CreateLinkedTokenSource(linked.Token, deviceCancellation);
                    Current(approvalLifetime.Token);
                    var originalProbe = probe.ToArray();
                    CheckBinding(ReadWire(originalProbe, "id_probe"));
                    var approval = await server.ApproveAsync(challengeId, Encode(originalProbe), approvalLifetime.Token);
                    Current(approvalLifetime.Token);
                    var bytes = Decode(approval, 16384);
                    CheckBinding(ReadWire(bytes, "id_approval"));
                    // Pi verifies MAC, fresh nonce, scope, elapsed time and current approval.
                    return bytes;
                }, linked.Token);
                Current(linked.Token);
                var frozen = result.ToArray();
                var payload = ReadWire(frozen, "id_result");
                CheckBinding(payload);
                string outcome = Text(payload, "outcome");
                if (outcome is not ("matched" or "no_match" or "busy" or "timeout" or "cancelled" or "sensor_error")) throw Invalid();
                localMatched = outcome == "matched";
                frozenResult = Encode(frozen); // Freeze BEFORE complete; never parse/re-serialize the signed bytes.
            }
            string completion = frozenResult ?? throw Invalid();
            Current(linked.Token);
            var receipt = await server.CompleteAsync(challengeId, completion, linked.Token);
            Current(linked.Token);
            CheckStatus(receipt, allowIssued: false);
            return receipt;
        }
        finally { Interlocked.Exchange(ref running, 0); }
    }

    private void Current(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (Volatile.Read(ref disposed) != 0 || !contextCurrent())
            throw new OperationCanceledException("이전 본인 확인 요청을 취소했습니다.", cancellation);
    }

    private void CheckStatus(IdentityStatus status, bool allowIssued)
    {
        if (status is null || status.ChallengeId != challengeId) throw Invalid();
        if (status.State == "verified")
        {
            if (!localMatched || status.WorkflowState != "pending" || status.IdentityReceipt is not { } receipt
                || receipt.ReceiptId == Guid.Empty || receipt.Purpose != purpose) throw Invalid();
        }
        else if (status.State is "rejected" or "cancelled" or "expired" or "revoked" || allowIssued && status.State == "issued")
        {
            if (status.IdentityReceipt is not null || status.WorkflowState != "not_started") throw Invalid();
        }
        else throw Invalid();
    }

    private void CheckBinding(JsonElement payload)
    { if (Text(payload, "ticket_digest") != ticketDigest) throw Invalid(); }

    private JsonElement ReadWire(byte[] raw, string kind)
    {
        // Structural checks only. A relay PC does NOT authenticate the signature.
        try
        {
            if (raw.Length is < 1 or > 16384) throw Invalid();
            using var doc = JsonDocument.Parse(raw, new JsonDocumentOptions { MaxDepth = 16 });
            var root = doc.RootElement;
            NoDuplicates(root);
            var names = root.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            if (!names.SetEquals(new[] { "v", "kind", "device_id", "key_id", "payload_b64", "mac_b64" })
                || root.GetProperty("v").GetInt32() != 2 || Text(root, "kind") != kind
                || Text(root, "device_id") != deviceId || !Regex.IsMatch(Text(root, "key_id"), "\\A[A-Za-z0-9_-]{1,64}\\z")
                || Decode(Text(root, "mac_b64"), 32).Length != 32) throw Invalid();
            using var payload = JsonDocument.Parse(Decode(Text(root, "payload_b64"), 8192), new JsonDocumentOptions { MaxDepth = 16 });
            NoDuplicates(payload.RootElement);
            if (Text(payload.RootElement, "challenge_id") != challengeId.ToString("D")) throw Invalid();
            return payload.RootElement.Clone();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        { throw Invalid(); }
    }
    private static void NoDuplicates(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Invalid();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in value.EnumerateObject()) if (!names.Add(p.Name)) throw Invalid();
    }
    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? throw Invalid();
    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string encoded, int maximum)
    {
        if (encoded is null || encoded.Length is < 1 || encoded.Length > (maximum * 4 + 2) / 3
            || !Regex.IsMatch(encoded, "\\A[A-Za-z0-9_-]+\\z")) throw Invalid();
        var raw = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/') + new string('=', (4 - encoded.Length % 4) % 4));
        if (raw.Length > maximum || Encode(raw) != encoded) throw Invalid();
        return raw;
    }
    private static InvalidDataException Invalid() => new("본인 확인 응답이 올바르지 않습니다. 새 스캔으로 자동 전환하지 않습니다.");
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetime.Cancel();
        frozenResult = null;
    }
}
