using System.Globalization;
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Attendance;

public sealed record IdentityChallengeTicket(Guid ChallengeId, string State, byte[] Request);

/// <summary>Borrow one captured connection. This adapter neither logs in nor disposes
/// its owner and never installs a token or finalizes an attendance operation.</summary>
public sealed class FingerprintIdentityConnection : IIdentityRelayServer
{
    private readonly IdentityPendingConnection? pending;
    private readonly ApiClient.Session? session;
    public string Purpose { get; }
    public bool IsCurrent => pending?.IsCurrent ?? session!.IsCurrent;
    public CancellationToken Cancellation => pending?.Cancellation ?? session!.Cancellation;

    public FingerprintIdentityConnection(IdentityPendingConnection connection)
    { pending = connection ?? throw new ArgumentNullException(nameof(connection)); Purpose = "clock_in"; }

    public FingerprintIdentityConnection(ApiClient.Session session, string purpose)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        if (purpose is not ("noon_check" or "clock_out")) throw new ArgumentException("출근은 지문 확인 대기 접속이 필요합니다.", nameof(purpose));
        Purpose = purpose;
    }

    public void ThrowIfChanged()
    {
        Cancellation.ThrowIfCancellationRequested();
        if (!IsCurrent) throw new OperationCanceledException("로그인 접속이 변경되거나 본인 확인 시간이 지났습니다.", Cancellation);
    }

    public async Task<IdentityChallengeTicket> IssueAsync(Guid deviceId, Guid requestId, CancellationToken ct)
    {
        Nonempty(deviceId); Nonempty(requestId); ThrowIfChanged();
        var value = pending is not null ? await pending.IssueAsync(deviceId, requestId, ct).ConfigureAwait(false)
            : await session!.PostAsync<JsonElement>("device-identity/challenges", new
            { device_id = deviceId.ToString("D"), purpose = Purpose, client_request_id = requestId.ToString("D") }, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested(); ThrowIfChanged();
        return Ticket(value, requestId);
    }

    public async Task<IdentityChallengeTicket?> LookupAsync(Guid requestId, CancellationToken ct)
    {
        Nonempty(requestId); ThrowIfChanged();
        var value = pending is not null ? await pending.LookupRequestAsync(requestId, ct).ConfigureAwait(false)
            : await session!.GetAsync<JsonElement>("device-identity/requests/" + requestId.ToString("D"), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested(); ThrowIfChanged();
        return Text(value, "state") == "not_recorded" ? null : Ticket(value, requestId);
    }

    public async Task<string> ApproveAsync(Guid challengeId, string probeB64, CancellationToken ct)
    {
        Nonempty(challengeId); Decode(probeB64); ThrowIfChanged();
        var answer = pending is not null ? await pending.ApproveAsync(challengeId, probeB64, ct).ConfigureAwait(false)
            : Text(await session!.PostAsync<JsonElement>(Path(challengeId) + "/approvals", new { probe_b64 = probeB64 }, ct).ConfigureAwait(false), "approval_b64");
        ct.ThrowIfCancellationRequested(); ThrowIfChanged(); Decode(answer);
        return answer;
    }

    public async Task<IdentityStatus> CompleteAsync(Guid challengeId, string resultB64, CancellationToken ct)
    {
        Nonempty(challengeId); Decode(resultB64); ThrowIfChanged();
        var value = pending is not null ? await pending.CompleteAsync(challengeId, resultB64, ct).ConfigureAwait(false)
            : await session!.PostAsync<JsonElement>(Path(challengeId) + "/complete", new { result_b64 = resultB64 }, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested(); ThrowIfChanged();
        return Status(value, challengeId);
    }

    public async Task<IdentityStatus> GetAsync(Guid challengeId, CancellationToken ct)
    {
        Nonempty(challengeId); ThrowIfChanged();
        var value = pending is not null ? await pending.GetAsync(challengeId, ct).ConfigureAwait(false)
            : await session!.GetAsync<JsonElement>(Path(challengeId), ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested(); ThrowIfChanged();
        return Status(value, challengeId);
    }

    public async Task<IdentityStatus> CancelAsync(Guid challengeId, Guid originalCancelId, CancellationToken ct)
    {
        Nonempty(challengeId); Nonempty(originalCancelId); ThrowIfChanged();
        var value = pending is not null ? await pending.CancelAsync(challengeId, originalCancelId, ct).ConfigureAwait(false)
            : await session!.PostAsync<JsonElement>(Path(challengeId) + "/cancel", new { client_request_id = originalCancelId.ToString("D") }, ct).ConfigureAwait(false);
        ct.ThrowIfCancellationRequested(); ThrowIfChanged();
        if (Uuid(value, "challenge_id") != challengeId || Text(value, "state") is not
            ("cancelled" or "verified" or "rejected" or "expired" or "revoked")) throw Invalid();
        // A cancellation can race with verification. Only a fresh server status is authoritative.
        return await GetAsync(challengeId, ct).ConfigureAwait(false);
    }

    private static string Path(Guid id) => "device-identity/challenges/" + id.ToString("D");
    private static void Nonempty(Guid id) { if (id == Guid.Empty) throw new ArgumentException("원래 요청 번호가 필요합니다."); }
    private static IdentityChallengeTicket Ticket(JsonElement value, Guid requestId)
    {
        if (Uuid(value, "client_request_id") != requestId) throw Invalid();
        string state = Text(value, "state");
        if (state is not ("issued" or "verified" or "rejected" or "cancelled" or "expired" or "revoked")) throw Invalid();
        Timestamp(value, "expires_at");
        return new(Uuid(value, "challenge_id"), state, Decode(Text(value, "request_b64")));
    }
    private IdentityStatus Status(JsonElement value, Guid expectedId)
    {
        if (Uuid(value, "challenge_id") != expectedId) throw Invalid();
        Timestamp(value, "expires_at");
        string state = Text(value, "state"), workflow = Text(value, "workflow_state");
        if (!value.TryGetProperty("identity_receipt", out var receipt)) throw Invalid();
        IdentityReceipt? parsed = null;
        if (state == "verified")
        {
            if (workflow is not ("pending" or "completed") || Text(receipt, "purpose") != Purpose) throw Invalid();
            parsed = new(Uuid(receipt, "receipt_id"), Purpose, Timestamp(receipt, "verified_at"));
        }
        else if (state is not ("issued" or "rejected" or "cancelled" or "expired" or "revoked")
            || workflow != "not_started" || receipt.ValueKind != JsonValueKind.Null) throw Invalid();
        return new(expectedId, state, workflow, parsed);
    }
    private static string Text(JsonElement v, string name)
    {
        if (v.ValueKind != JsonValueKind.Object || !v.TryGetProperty(name, out var p)
            || p.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(p.GetString())) throw Invalid();
        return p.GetString()!;
    }
    private static Guid Uuid(JsonElement v, string name)
    {
        string s = Text(v, name);
        if (!Guid.TryParseExact(s, "D", out var id) || id == Guid.Empty || id.ToString("D") != s) throw Invalid();
        return id;
    }
    private static DateTimeOffset Timestamp(JsonElement v, string name)
    {
        string s = Text(v, name);
        if (!(s.EndsWith('Z') || s.EndsWith("+00:00", StringComparison.Ordinal))
            || !DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
            || result.Offset != TimeSpan.Zero || result == default) throw Invalid();
        return result;
    }
    private static byte[] Decode(string value)
    {
        if (value is null || value.Length is < 1 or > 21846
            || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) throw Invalid();
        try
        {
            var bytes = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
            if (bytes.Length is < 1 or > 16384 || Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_') != value) throw Invalid();
            return bytes;
        }
        catch (FormatException) { throw Invalid(); }
    }
    private static InvalidDataException Invalid() => new("지문 본인 확인 응답을 확인할 수 없습니다. 원래 작업 결과부터 확인해 주세요.");
}
