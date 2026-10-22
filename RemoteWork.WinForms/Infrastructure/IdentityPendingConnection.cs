using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace RemoteWork.WinForms.Infrastructure;

/// <summary>Separate RAM-only pending context. Never installs a normal ApiClient identity.</summary>
public sealed class IdentityPendingConnection : IDisposable
{
    private readonly HttpClient http;
    private readonly Uri server;
    private readonly Func<bool> current;
    private readonly CancellationTokenSource lifetime;
    private readonly CancellationToken cancellation;
    private readonly TimeProvider clock;
    private readonly ConcurrentDictionary<Guid, FinalizeAttempt> finalizeAttempts = new();
    private sealed record FinalizeAttempt(Guid RequestId, DateTimeOffset RecoverUntil);
    private string? pendingToken;
    private int ended;
    public string ContextReference { get; private set; } = "";
    public DateTimeOffset ExpiresAt { get; private set; } = DateTimeOffset.MaxValue;
    public CancellationToken Cancellation => cancellation;
    public bool IsCurrent => Volatile.Read(ref ended) == 0 && !cancellation.IsCancellationRequested
        && clock.GetUtcNow() < ExpiresAt && current();

    private IdentityPendingConnection(string serverUrl, Func<bool> current, CancellationToken lifetime,
        HttpMessageHandler? handler, TimeProvider clock)
    {
        server = ApiClient.ValidateUrl(serverUrl);
        this.current = current ?? throw new ArgumentNullException(nameof(current));
        this.clock = clock;
        this.lifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        cancellation = this.lifetime.Token;
        http = new HttpClient(handler ?? new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(12) };
    }

    public static async Task<IdentityPendingConnection> LoginAsync(string serverUrl, string loginId,
        string password, Guid originalRequestId, Func<bool> isCurrent, CancellationToken lifetime,
        CancellationToken cancellationToken = default, HttpMessageHandler? handler = null,
        TimeProvider? timeProvider = null)
    {
        var connection = new IdentityPendingConnection(serverUrl, isCurrent, lifetime, handler,
            timeProvider ?? TimeProvider.System);
        try
        {
            var result = await connection.SendAsync(HttpMethod.Post, "login", new
            { login_id = loginId, password, client_request_id = originalRequestId.ToString("D") },
                cancellationToken, pending: false).ConfigureAwait(false);
            if (result.GetProperty("token_type").GetString() != "IdentityPending"
                || result.GetProperty("state").GetString() != "fingerprint_required"
                || result.TryGetProperty("access_token", out _)) throw Invalid();
            var token = result.GetProperty("pending_token").GetString();
            var reference = result.GetProperty("context_ref").GetString();
            var expiry = result.GetProperty("expires_at").GetDateTimeOffset();
            if (!Opaque(token) || !Opaque(reference) || expiry <= connection.clock.GetUtcNow()
                || expiry > connection.clock.GetUtcNow().AddMinutes(5).AddSeconds(2)) throw Invalid();
            connection.pendingToken = token;
            connection.ContextReference = reference!;
            connection.ExpiresAt = expiry;
            connection.ThrowIfChanged();
            return connection;
        }
        catch { connection.Dispose(); throw; }
    }

    public void ThrowIfChanged()
        => ThrowIfChanged(null);

    private void ThrowIfChanged(Func<bool>? retainedResult)
    {
        if (Volatile.Read(ref ended) != 0 || cancellation.IsCancellationRequested || !current()
            || (clock.GetUtcNow() >= ExpiresAt && retainedResult?.Invoke() != true))
            throw new OperationCanceledException("본인 확인 접속이 변경되거나 종료되었습니다.", cancellation);
    }

    public Task<JsonElement> IssueAsync(Guid deviceId, Guid originalRequestId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, "challenges", new { device_id = deviceId.ToString("D"),
            purpose = "clock_in", client_request_id = originalRequestId.ToString("D") }, ct);
    public Task<JsonElement> LookupRequestAsync(Guid originalRequestId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Get, "requests/" + originalRequestId.ToString("D"), null, ct);
    public Task<JsonElement> GetAsync(Guid challengeId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Get, Path(challengeId), null, ct,
            retainedResult: () => CanRecover(challengeId));
    public async Task<string> ApproveAsync(Guid challengeId, string probeB64, CancellationToken ct = default)
    {
        Bytes(probeB64);
        var result = await SendAsync(HttpMethod.Post, Path(challengeId) + "/approvals",
            new { probe_b64 = probeB64 }, ct, approval: true).ConfigureAwait(false);
        var answer = result.GetProperty("approval_b64").GetString();
        Bytes(answer);
        return answer!;
    }
    public Task<JsonElement> CompleteAsync(Guid challengeId, string resultB64, CancellationToken ct = default)
    {
        Bytes(resultB64);
        return SendAsync(HttpMethod.Post, Path(challengeId) + "/complete", new { result_b64 = resultB64 }, ct);
    }
    public Task<JsonElement> CancelAsync(Guid challengeId, Guid originalCancelId, CancellationToken ct = default)
        => SendAsync(HttpMethod.Post, Path(challengeId) + "/cancel",
            new { client_request_id = originalCancelId.ToString("D") }, ct);
    public Task<JsonElement> FinalizeAsync(Guid challengeId, Guid originalFinalizeId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        // Record before sending: a commit may succeed even when its response is lost.
        // Only the same challenge + original UUID can outlive the activity window.
        ThrowIfChanged(() => CanRecover(challengeId, originalFinalizeId));
        var attempt = finalizeAttempts.GetOrAdd(challengeId,
            _ => new FinalizeAttempt(originalFinalizeId, clock.GetUtcNow().AddHours(24)));
        if (attempt.RequestId != originalFinalizeId)
            throw new InvalidOperationException("최초 확정 요청 번호로 결과를 확인해야 합니다.");
        return SendAsync(HttpMethod.Post, Path(challengeId) + "/finalize",
            new { client_request_id = originalFinalizeId.ToString("D") }, ct,
            retainedResult: () => CanRecover(challengeId, originalFinalizeId));
    }

    private bool CanRecover(Guid challengeId, Guid? requestId = null)
        => finalizeAttempts.TryGetValue(challengeId, out var attempt)
            && (requestId is null || attempt.RequestId == requestId)
            && clock.GetUtcNow() < attempt.RecoverUntil;
    public async Task RevokeAsync(Guid originalRevokeId, CancellationToken ct = default)
    {
        var result = await SendAsync(HttpMethod.Post, "context/revoke",
            new { client_request_id = originalRevokeId.ToString("D") }, ct).ConfigureAwait(false);
        if (result.GetProperty("state").GetString() != "revoked") throw Invalid();
        Dispose();
    }

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body,
        CancellationToken ct, bool pending = true, bool approval = false,
        Func<bool>? retainedResult = null)
    {
        ThrowIfChanged(retainedResult);
        if (pending && pendingToken is null) throw new InvalidOperationException("제한 로그인이 필요합니다.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellation);
        // Approval budget only: never changes ApiClient's shared 12-second timeout.
        linked.CancelAfter(TimeSpan.FromSeconds(approval ? 2 : 12));
        using var request = new HttpRequestMessage(method, new Uri(server, "api/v1/device-identity/" + path));
        if (pending) request.Headers.Authorization = new AuthenticationHeaderValue("IdentityPending", pendingToken);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
        linked.Token.ThrowIfCancellationRequested();
        ThrowIfChanged(retainedResult);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            End();
            throw new ApiException("본인 확인 접속이 만료되었습니다.", 401, "IDENTITY_CONTEXT_REQUIRED");
        }
        using var bytes = new MemoryStream();
        using var stream = await response.Content.ReadAsStreamAsync(linked.Token).ConfigureAwait(false);
        var buffer = new byte[4096];
        int length;
        while ((length = await stream.ReadAsync(buffer, linked.Token).ConfigureAwait(false)) != 0)
        {
            if (bytes.Length + length > 64 * 1024) throw Invalid((int)response.StatusCode);
            bytes.Write(buffer, 0, length);
        }
        linked.Token.ThrowIfCancellationRequested();
        ThrowIfChanged(retainedResult);
        try
        {
            using var doc = JsonDocument.Parse(bytes.ToArray());
            var root = doc.RootElement;
            if (!response.IsSuccessStatusCode || !root.GetProperty("ok").GetBoolean())
            {
                string? code = root.TryGetProperty("error", out var error)
                    && error.TryGetProperty("code", out var value) ? value.GetString() : null;
                throw new ApiException("본인 확인 요청이 처리되지 않았습니다.", (int)response.StatusCode, code);
            }
            var data = root.GetProperty("data");
            if (data.ValueKind != JsonValueKind.Object) throw Invalid((int)response.StatusCode);
            return data.Clone();
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        { throw Invalid((int)response.StatusCode); }
    }

    private static string Path(Guid id) => "challenges/" + id.ToString("D");
    private static bool Opaque(string? value) => value is not null && value.Length is >= 22 and <= 86
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    private static void Bytes(string? value)
    {
        if (value is null || value.Length is < 1 or > 22000
            || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) throw Invalid();
    }
    private static ApiException Invalid(int status = 0) => new("본인 확인 서버 응답 형식이 올바르지 않습니다.", status);
    private void End() { if (Interlocked.Exchange(ref ended, 1) == 0) lifetime.Cancel(); }
    public void Dispose() { End(); pendingToken = null; http.Dispose(); lifetime.Dispose(); }
}
