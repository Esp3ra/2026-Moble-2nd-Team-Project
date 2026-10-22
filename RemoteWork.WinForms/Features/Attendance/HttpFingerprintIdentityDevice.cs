using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Attendance;

public sealed class IdentityPiHttpException : HttpRequestException
{
    public string? Code { get; }
    public IdentityPiHttpException(HttpStatusCode status, string? code)
        : base("라파 본인 확인 요청을 처리하지 못했습니다. 새 스캔으로 자동 전환하지 않습니다.", null, status)
    { Code = code; }
}

/// <summary>Opt-in Pi-only HTTP channel. Uses no account token, key, cookie,
/// redirect or proxy. This is not a server authentication/login client.</summary>
public sealed class HttpFingerprintIdentityDevice : IIdentityRelayDevice, IDisposable
{
    private readonly Uri baseUri;
    private readonly HttpClient http;
    private readonly CancellationTokenSource stopped = new();
    private int running, disposed;
    public HttpFingerprintIdentityDevice(string trustedAddress, HttpMessageHandler? handler = null)
    {
        baseUri = ApiClient.ValidateUrl(trustedAddress);
        http = new HttpClient(handler ?? new HttpClientHandler
        { AllowAutoRedirect = false, UseCookies = false, UseProxy = false }) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<byte[]> CaptureAsync(byte[] requestBytes,
        Func<byte[], CancellationToken, Task<byte[]>> approvalRelay, CancellationToken cancellation)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
            throw new InvalidOperationException("이 장치의 본인 확인 통신이 이미 진행 중입니다.");
        string relayId = Guid.NewGuid().ToString("D");
        string path = "identity/v2/jobs/" + relayId;
        string capability = Encode(RandomNumberGenerator.GetBytes(32));
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation, stopped.Token);
        lifetime.CancelAfter(TimeSpan.FromSeconds(65));
        try
        {
            ArgumentNullException.ThrowIfNull(approvalRelay);
            if (requestBytes is null || requestBytes.Length is < 1 or > 16384) throw Invalid();
            var raw = requestBytes.ToArray();
            string digest = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
            var body = new { relay_id = relayId, request_b64 = Encode(raw) };
            JsonElement state;
            try { state = await Send(HttpMethod.Post, "identity/v2/jobs", body, capability, lifetime.Token).ConfigureAwait(false); }
            catch (Exception error) when (Transient(error) && !lifetime.IsCancellationRequested)
            {
                // An uncertain POST does not justify another relay ID/capture.
                try { state = await Send(HttpMethod.Get, path, null, capability, lifetime.Token).ConfigureAwait(false); }
                catch (IdentityPiHttpException missing) when (missing.StatusCode == HttpStatusCode.NotFound && missing.Code == "IDENTITY_JOB_NOT_FOUND")
                { state = await Send(HttpMethod.Post, "identity/v2/jobs", body, capability, lifetime.Token).ConfigureAwait(false); }
            }
            var approvals = new Dictionary<string, (string Probe, string Approval, long FirstSeen)>(StringComparer.Ordinal);
            while (true)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                ValidateState(state, relayId, digest);
                string status = Text(state, "state");
                if (status == "succeeded") return Decode(Text(state, "result_b64"), 16384);
                if (status == "failed") throw new InvalidOperationException("라파 본인 확인이 중단됐습니다. 같은 작업을 자동으로 다시 스캔하지 않습니다.");
                if (status == "awaiting_approval")
                {
                    string probeId = Text(state, "probe_id"), probe = Text(state, "probe_b64");
                    if (!approvals.TryGetValue(probeId, out var cached))
                    {
                        if (approvals.Count >= 8) throw Invalid();
                        long firstSeen = Stopwatch.GetTimestamp();
                        using var approvalLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                        approvalLifetime.CancelAfter(TimeSpan.FromSeconds(2));
                        var approved = await approvalRelay(Decode(probe, 16384), approvalLifetime.Token).ConfigureAwait(false);
                        approvalLifetime.Token.ThrowIfCancellationRequested();
                        cached = (probe, Encode(approved.ToArray()), firstSeen);
                        Decode(cached.Approval, 16384);
                        approvals.Add(probeId, cached); // Freeze before posting; never regenerate on reply loss.
                    }
                    else if (cached.Probe != probe) throw Invalid();
                    if (Stopwatch.GetElapsedTime(cached.FirstSeen) >= TimeSpan.FromSeconds(2))
                        throw new InvalidOperationException("승인 전송 시간이 지났습니다. 같은 작업을 자동으로 다시 스캔하지 않습니다.");
                    try
                    {
                        state = await Send(HttpMethod.Post, path + "/approval",
                            new { probe_id = probeId, approval_b64 = cached.Approval }, capability, lifetime.Token).ConfigureAwait(false);
                    }
                    catch (Exception error) when (Transient(error) && !lifetime.IsCancellationRequested)
                    { state = await Send(HttpMethod.Get, path, null, capability, lifetime.Token).ConfigureAwait(false); }
                }
                else
                {
                    await Task.Delay(40, lifetime.Token).ConfigureAwait(false);
                    state = await Send(HttpMethod.Get, path, null, capability, lifetime.Token).ConfigureAwait(false);
                }
            }
        }
        finally
        {
            // Only this Pi job, independent of server cancellation/revocation.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await Send(HttpMethod.Post, path + "/cancel", new { }, capability, cleanup.Token).ConfigureAwait(false); }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException or InvalidDataException or ObjectDisposedException) { }
            Interlocked.Exchange(ref running, 0);
        }
    }

    private static bool Transient(Exception error) => error is OperationCanceledException
        || error is HttpRequestException and not IdentityPiHttpException;

    private async Task<JsonElement> Send(HttpMethod method, string path, object? body, string capability, CancellationToken cancellation)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(2));
        using var request = new HttpRequestMessage(method, new Uri(baseUri, path));
        request.Headers.Add("X-Identity-Relay", capability);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        if (response.Content.Headers.ContentLength > 65536) throw Invalid();
        using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        byte[] bytes = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(bytes, deadline.Token).ConfigureAwait(false)) != 0)
        {
            if (buffer.Length + count > 65536) throw Invalid();
            buffer.Write(bytes, 0, count);
        }
        try
        {
            if (!response.IsSuccessStatusCode)
            {
                string? code = null;
                try
                {
                    using var failure = JsonDocument.Parse(buffer.ToArray());
                    code = failure.RootElement.GetProperty("error").GetProperty("code").GetString();
                    if (code is not null && !Regex.IsMatch(code, "\\A[A-Z_]{1,80}\\z")) code = null;
                }
                catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException) { }
                throw new IdentityPiHttpException(response.StatusCode, code);
            }
            if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)) throw Invalid();
            using var doc = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 12 });
            Exact(doc.RootElement, "success", "data");
            if (doc.RootElement.GetProperty("success").ValueKind != JsonValueKind.True) throw Invalid();
            return doc.RootElement.GetProperty("data").Clone();
        }
        catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        { throw Invalid(); }
    }

    private static void ValidateState(JsonElement state, string relayId, string digest)
    {
        try
        {
            Exact(state, "relay_id", "request_digest", "state", "probe_id", "probe_b64", "result_b64", "error_code");
            if (Text(state, "relay_id") != relayId || Text(state, "request_digest") != digest) throw Invalid();
            string status = Text(state, "state");
            if (status == "awaiting_approval")
            {
                string probeId = Text(state, "probe_id");
                if (!Guid.TryParseExact(probeId, "D", out var id) || id.ToString("D") != probeId) throw Invalid();
                Decode(Text(state, "probe_b64"), 16384);
            }
            else if (!Null(state, "probe_id") || !Null(state, "probe_b64")) throw Invalid();
            if (status == "succeeded") Decode(Text(state, "result_b64"), 16384);
            else if (!Null(state, "result_b64")) throw Invalid();
            if (status == "failed")
            {
                if (Text(state, "error_code") is not ("IDENTITY_CAPTURE_CANCELLED" or "IDENTITY_APPROVAL_UNAVAILABLE")) throw Invalid();
            }
            else if (!Null(state, "error_code")) throw Invalid();
            if (status is not ("running" or "awaiting_approval" or "succeeded" or "failed")) throw Invalid();
        }
        catch (Exception error) when (error is FormatException or InvalidOperationException or KeyNotFoundException) { throw Invalid(); }
    }
    private static void Exact(JsonElement value, params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Invalid();
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in value.EnumerateObject()) if (!found.Add(field.Name)) throw Invalid();
        if (!found.SetEquals(fields)) throw Invalid();
    }
    private static bool Null(JsonElement value, string name) => value.GetProperty(name).ValueKind == JsonValueKind.Null;
    private static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? throw Invalid();
    private static string Encode(byte[] raw) => Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string encoded, int maximum)
    {
        if (encoded.Length is < 1 || encoded.Length > (maximum * 4 + 2) / 3 || !Regex.IsMatch(encoded, "\\A[A-Za-z0-9_-]+\\z")) throw Invalid();
        var raw = Convert.FromBase64String(encoded.Replace('-', '+').Replace('_', '/') + new string('=', (4 - encoded.Length % 4) % 4));
        if (raw.Length > maximum || Encode(raw) != encoded) throw Invalid();
        return raw;
    }
    private static InvalidDataException Invalid() => new("라파 본인 확인 응답 형식을 확인해 주세요. 새 스캔으로 자동 전환하지 않습니다.");
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        stopped.Cancel();
        http.Dispose();
    }
}
