using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Attendance;

public sealed record EnrollmentDevice(string DeviceId, string DisplayName, string KeyEpoch);
public sealed record EnrollmentDevices(List<EnrollmentDevice> Devices);
public sealed record EnrollmentReceipt(string ResultId, string State, string? FailureCode);
public sealed record EnrollmentJob(string EnrollmentId, string OperationRevision, int ReservedSlot,
    string ClaimRevision, string KeyEpoch, string ClientRequestId, string DeviceId, string State,
    string ExpiresAt, bool RecoveryRequired, string? RequestEnvelope, EnrollmentReceipt? Result);
internal sealed record EnrollmentApproval(string ApprovalEnvelope);

public sealed class EnrollmentTarget
{
    public int UserId { get; }
    public ApiClient.Session AdministratorSession { get; }
    public CancellationToken Cancellation { get; }
    private readonly Func<bool> current;
    // The member-1 owner passes the ID from POST UserResult, not the selected list row.
    public EnrollmentTarget(int userId, ApiClient.Session administratorSession,
        Func<bool> isCurrent, CancellationToken cancellation)
    {
        if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
        UserId = userId;
        AdministratorSession = administratorSession;
        current = isCurrent;
        Cancellation = cancellation;
    }
    public void ThrowIfChanged()
    {
        AdministratorSession.ThrowIfChanged();
        Cancellation.ThrowIfCancellationRequested();
        if (!current()) throw new OperationCanceledException("등록 대상이 변경되었습니다.");
    }
}

public sealed class EnrollmentAttempt
{
    public string ClientRequestId { get; } = Guid.NewGuid().ToString();
    public string DeviceId { get; }
    internal readonly EnrollmentTarget Target;
    internal int Running;
    internal bool DeviceStarted;
    internal string? PendingResult;
    public EnrollmentJob? Job { get; internal set; }
    internal EnrollmentAttempt(EnrollmentTarget target, string deviceId) { Target = target; DeviceId = deviceId; }
}

public interface IEnrollmentPiChannel
{
    Task<string> TransferAsync(EnrollmentJob job, bool readOnly,
        Func<JsonElement, CancellationToken, Task<string>> approve,
        IProgress<string>? progress, CancellationToken cancellation);
}

internal static class EnrollmentValidation
{
    internal static void Require(bool condition)
    { if (!condition) throw new InvalidDataException("지문 등록 응답 형식을 확인해 주세요."); }
    internal static void Uuid(string value)
        => Require(Guid.TryParseExact(value, "D", out var id) && id.ToString() == value);
    internal static void Revision(string value)
        => Require(value is not null && Regex.IsMatch(value, "^[1-9][0-9]{0,18}$")
            && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long n) && n > 0);
    internal static byte[] Base64(string value, int maximum = 16384)
    {
        Require(value is not null && value.Length is > 0 && value.Length <= (maximum * 4 + 2) / 3
            && Regex.IsMatch(value, "^[A-Za-z0-9_-]+$"));
        byte[] raw;
        try { raw = Convert.FromBase64String(value!.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4)); }
        catch (FormatException) { throw new InvalidDataException("등록 메시지 인코딩이 올바르지 않습니다."); }
        Require(raw.Length <= maximum && Encode(raw) == value);
        return raw;
    }
    internal static string Encode(byte[] raw) => Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    internal static void Fields(JsonElement value, params string[] names)
    {
        Require(value.ValueKind == JsonValueKind.Object);
        var actual = value.EnumerateObject().Select(p => p.Name).ToArray();
        Require(actual.Length == names.Length && actual.Distinct(StringComparer.Ordinal).Count() == actual.Length
            && actual.Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal)));
    }
    internal static void Job(EnrollmentJob job, EnrollmentAttempt attempt)
    {
        Uuid(job.EnrollmentId); Uuid(job.ClientRequestId); Uuid(job.DeviceId);
        Revision(job.OperationRevision); Revision(job.ClaimRevision); Revision(job.KeyEpoch);
        Require(job.ClientRequestId == attempt.ClientRequestId && job.DeviceId == attempt.DeviceId && job.ReservedSlot >= 0);
        Require(job.State is "issued" or "completed" or "failed" or "recovery_required" or "cancelled" or "expired");
        if (attempt.Job is { } prior)
            Require(prior.EnrollmentId == job.EnrollmentId && prior.OperationRevision == job.OperationRevision
                && prior.ReservedSlot == job.ReservedSlot && prior.ClaimRevision == job.ClaimRevision && prior.KeyEpoch == job.KeyEpoch);
        if (job.RequestEnvelope is not null) Base64(job.RequestEnvelope);
        if (job.Result is not null) { Uuid(job.Result.ResultId); Require(job.Result.State == job.State); }
        if (job.State == "completed") Require(job.Result?.State == "completed");
    }
}

/// <summary>Opaque PC relay: no device keys, MAC generation or account bearer sent to Pi.</summary>
public sealed class FingerprintEnrollmentRelay(EnrollmentTarget target, IEnrollmentPiChannel pi)
{
    private const string Root = "device-enrollments";

    public EnrollmentAttempt CreateAttempt(string deviceId)
    { target.ThrowIfChanged(); EnrollmentValidation.Uuid(deviceId); return new(target, deviceId); }

    public async Task<IReadOnlyList<EnrollmentDevice>> GetDevicesAsync(CancellationToken ct = default)
    {
        target.ThrowIfChanged();
        using var linked = Link(ct);
        var response = await target.AdministratorSession.GetAsync<EnrollmentDevices>(
            $"{Root}/eligible-devices?target_user_id={target.UserId}", linked.Token).ConfigureAwait(false);
        target.ThrowIfChanged();
        linked.Token.ThrowIfCancellationRequested();
        EnrollmentValidation.Require(response.Devices is not null);
        foreach (var device in response.Devices!)
        {
            EnrollmentValidation.Require(device is not null && !string.IsNullOrWhiteSpace(device.DisplayName));
            EnrollmentValidation.Uuid(device!.DeviceId); EnrollmentValidation.Revision(device.KeyEpoch);
        }
        return response.Devices;
    }

    private CancellationTokenSource Link(CancellationToken ct)
        => CancellationTokenSource.CreateLinkedTokenSource(ct, target.Cancellation, target.AdministratorSession.Cancellation);

    public async Task<EnrollmentJob> RunAsync(EnrollmentAttempt attempt, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        if (!ReferenceEquals(attempt.Target, target)) throw new ArgumentException("등록 대상이 다릅니다.");
        if (Interlocked.Exchange(ref attempt.Running, 1) != 0) throw new InvalidOperationException("등록 진행 중입니다.");
        using var linked = Link(ct);
        try
        {
            target.ThrowIfChanged();
            EnrollmentJob job;
            if (attempt.Job is { } known)
                job = await target.AdministratorSession.GetAsync<EnrollmentJob>($"{Root}/{known.EnrollmentId}", linked.Token).ConfigureAwait(false);
            else
            {
                // Same attempt retains this UUID even if the issue response is lost.
                try
                {
                    job = await target.AdministratorSession.PostAsync<EnrollmentJob>(Root, new
                    { client_request_id = attempt.ClientRequestId, target_user_id = target.UserId, device_id = attempt.DeviceId }, linked.Token).ConfigureAwait(false);
                }
                catch (ApiException error) when (error.StatusCode == 0 || error.StatusCode >= 500)
                {
                    job = await target.AdministratorSession.GetAsync<EnrollmentJob>(
                        $"{Root}/by-request/{attempt.ClientRequestId}", linked.Token).ConfigureAwait(false);
                }
            }
            target.ThrowIfChanged();
            linked.Token.ThrowIfCancellationRequested();
            EnrollmentValidation.Job(job, attempt);
            attempt.Job = job;
            if (job.State == "completed") return job;
            if (job.State is not ("issued" or "expired"))
                throw new InvalidOperationException("등록 상태 확인이 필요합니다. 새 지문을 자동 등록하지 않습니다.");
            if (attempt.PendingResult is null)
            {
                bool readOnly = attempt.DeviceStarted || job.State == "expired";
                if (!readOnly && job.RequestEnvelope is null) throw new InvalidDataException("등록 요청이 없습니다.");
                attempt.DeviceStarted = true; // Set BEFORE sending; an uncertain send never triggers automatic rescan.
                progress?.Report(readOnly ? "저장 결과 확인 중" : "지문 등록 준비 중");
                attempt.PendingResult = await pi.TransferAsync(job, readOnly,
                    (challenge, token) => ApproveAsync(job, readOnly, challenge, token), progress, linked.Token).ConfigureAwait(false);
                target.ThrowIfChanged();
                linked.Token.ThrowIfCancellationRequested();
                EnrollmentValidation.Base64(attempt.PendingResult);
            }
            progress?.Report("서버에 계정 연결 확인 중");
            var completed = await target.AdministratorSession.PostAsync<EnrollmentJob>($"{Root}/{job.EnrollmentId}/complete",
                new { operation_revision = job.OperationRevision, result_envelope = attempt.PendingResult }, linked.Token).ConfigureAwait(false);
            target.ThrowIfChanged();
            linked.Token.ThrowIfCancellationRequested();
            EnrollmentValidation.Job(completed, attempt);
            attempt.Job = completed;
            EnrollmentValidation.Require(completed.State == "completed");
            progress?.Report("지문 등록 완료");
            return completed;
        }
        finally { Volatile.Write(ref attempt.Running, 0); }
    }

    private async Task<string> ApproveAsync(EnrollmentJob job, bool readOnly, JsonElement body, CancellationToken ct)
    {
        target.ThrowIfChanged();
        EnrollmentValidation.Fields(body, "operation_revision", "reserved_slot", "claim_revision", "key_epoch", "ticket_digest", "probe_nonce", "scope");
        EnrollmentValidation.Require(body.GetProperty("operation_revision").GetString() == job.OperationRevision
            && body.GetProperty("reserved_slot").GetInt32() == job.ReservedSlot
            && body.GetProperty("claim_revision").GetString() == job.ClaimRevision
            && body.GetProperty("key_epoch").GetString() == job.KeyEpoch
            && body.GetProperty("scope").GetString() == (readOnly ? "result_read" : "sensor_write"));
        var digest = body.GetProperty("ticket_digest").GetString();
        EnrollmentValidation.Require(digest is not null && Regex.IsMatch(digest, "^[0-9a-f]{64}$"));
        int nonceLength = EnrollmentValidation.Base64(body.GetProperty("probe_nonce").GetString()!, 64).Length;
        EnrollmentValidation.Require(nonceLength >= 16);
        var response = await target.AdministratorSession.PostAsync<EnrollmentApproval>($"{Root}/{job.EnrollmentId}/approval", body, ct).ConfigureAwait(false);
        target.ThrowIfChanged();
        ct.ThrowIfCancellationRequested();
        EnrollmentValidation.Base64(response.ApprovalEnvelope);
        return response.ApprovalEnvelope;
    }

    public async Task CancelAsync(EnrollmentAttempt attempt, CancellationToken ct = default)
    {
        target.ThrowIfChanged();
        if (!ReferenceEquals(attempt.Target, target)) throw new ArgumentException("등록 대상이 다릅니다.");
        if (attempt.Job is null) return; // Unknown issue is recovered by the SAME client request ID, never guessed.
        using var linked = Link(ct);
        var value = await target.AdministratorSession.PostAsync<EnrollmentJob>(
            $"{Root}/{attempt.Job.EnrollmentId}/cancel", new { operation_revision = attempt.Job.OperationRevision }, linked.Token).ConfigureAwait(false);
        target.ThrowIfChanged();
        linked.Token.ThrowIfCancellationRequested();
        EnrollmentValidation.Job(value, attempt);
        attempt.Job = value;
    }
}

/// <summary>Member-5 opt-in Pi job transport, separate HttpClient with no account authorization header.</summary>
public sealed class HttpEnrollmentPiChannel : IEnrollmentPiChannel, IDisposable
{
    private readonly HttpClient http;
    private readonly Uri baseUri;
    public HttpEnrollmentPiChannel(string address, HttpMessageHandler? handler = null)
    {
        baseUri = ApiClient.ValidateUrl(address);
        http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false })
        { Timeout = TimeSpan.FromSeconds(12) };
    }
    public async Task<string> TransferAsync(EnrollmentJob job, bool readOnly,
        Func<JsonElement, CancellationToken, Task<string>> approve,
        IProgress<string>? progress, CancellationToken cancellation)
    {
        string relayId = Guid.NewGuid().ToString();
        string capability = EnrollmentValidation.Encode(RandomNumberGenerator.GetBytes(32));
        string path = $"enrollments/jobs/{relayId}";
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        lifetime.CancelAfter(TimeSpan.FromSeconds(150));
        try
        {
            var state = await SendAsync(HttpMethod.Post, "enrollments/jobs", new
            { relay_id = relayId, enrollment_id = job.EnrollmentId, mode = readOnly ? "read_result" : "enroll",
                request_envelope = readOnly ? null : job.RequestEnvelope }, capability, lifetime.Token).ConfigureAwait(false);
            while (true)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                EnrollmentValidation.Require(state.GetProperty("relay_id").GetString() == relayId);
                string? status = state.GetProperty("state").GetString();
                if (status == "succeeded")
                {
                    string raw = state.GetProperty("result_envelope").GetString()!;
                    EnrollmentValidation.Base64(raw);
                    return raw;
                }
                if (status == "failed") throw new InvalidOperationException("라파 등록 상태 확인이 필요합니다. 다시 스캔하지 않고 저장 결과를 확인해 주세요.");
                if (status == "delivered") throw new InvalidOperationException("결과 전달 확인이 필요합니다. 같은 작업의 저장 결과를 다시 확인해 주세요.");
                EnrollmentValidation.Require(status is "running" or "awaiting_approval");
                if (status == "awaiting_approval")
                {
                    var challenge = state.GetProperty("challenge");
                    string approved = await approve(challenge.Clone(), lifetime.Token).ConfigureAwait(false);
                    state = await SendAsync(HttpMethod.Post, path + "/approval", new
                    { probe_nonce = challenge.GetProperty("probe_nonce").GetString(), approval_envelope = approved }, capability, lifetime.Token).ConfigureAwait(false);
                }
                else
                {
                    await Task.Delay(40, lifetime.Token).ConfigureAwait(false);
                    state = await SendAsync(HttpMethod.Get, path, null, capability, lifetime.Token).ConfigureAwait(false);
                }
            }
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or FormatException)
        { throw new InvalidDataException("라파 등록 응답 형식을 확인해 주세요."); }
        finally
        {
            // Ask the Pi to stop even on timeout/view/session cancellation. This is
            // neither a server cancellation nor physical deletion/rollback.
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await SendAsync(HttpMethod.Post, path + "/cancel", new { }, capability, cleanup.Token).ConfigureAwait(false); }
            catch (Exception error) when (error is HttpRequestException or OperationCanceledException or InvalidDataException or ObjectDisposedException or JsonException or KeyNotFoundException) { }
        }
    }
    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object? body, string capability, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        ct = deadline.Token;
        using var request = new HttpRequestMessage(method, new Uri(baseUri, path));
        request.Headers.Add("X-Enrollment-Relay", capability);
        if (body is not null) request.Content = new StringContent(
            JsonSerializer.Serialize(body, ApiClient.Json), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        byte[] bytes = new byte[4096];
        int count;
        while ((count = await stream.ReadAsync(bytes, ct).ConfigureAwait(false)) != 0)
        { EnrollmentValidation.Require(buffer.Length + count <= 65536); buffer.Write(bytes, 0, count); }
        using var document = JsonDocument.Parse(buffer.ToArray());
        EnrollmentValidation.Require(document.RootElement.GetProperty("success").ValueKind == JsonValueKind.True);
        return document.RootElement.GetProperty("data").Clone();
    }
    public void Dispose() => http.Dispose();
}
