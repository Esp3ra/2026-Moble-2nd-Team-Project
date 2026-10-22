using System.Globalization;
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Attendance;

public sealed record WorkdayIdentityReceipt(Guid ReceiptId, Guid WorkSessionId, DateOnly WorkDate, string Purpose);

/// <summary>One normal-session noon/clock-out operation. The owner must supply a
/// server-confirmed work ID/date; neither a displayed user nor a local PC flag is evidence.
/// Owns its attempt, borrows the normal session and device. No login/logout or monitor writes.</summary>
public sealed class FingerprintWorkdayOperation : IDisposable
{
    private readonly ApiClient.Session session;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim gate = new(1);
    private Guid? verifiedChallenge;
    private bool disposed;
    public string Purpose { get; }
    public Guid WorkSessionId { get; }
    public DateOnly WorkDate { get; }
    public Guid OriginalFinalizeId { get; } = Guid.NewGuid();
    public FingerprintIdentityAttempt Attempt { get; }
    public bool IsCurrent => !disposed && !lifetime.IsCancellationRequested && session.IsCurrent;
    public bool HasFinalizeAttempt { get; private set; }
    public WorkdayIdentityReceipt? Completed { get; private set; }

    public FingerprintWorkdayOperation(ApiClient.Session session, Guid workSessionId, DateOnly workDate,
        string purpose, Guid deviceId, IIdentityRelayDevice device)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        session.ThrowIfChanged();
        if (purpose is not ("noon_check" or "clock_out")) throw new ArgumentException("정오 또는 퇴근 확인을 선택해 주세요.", nameof(purpose));
        if (workSessionId == Guid.Empty || workDate == default) throw new ArgumentException("서버의 현재 근무 정보를 확인해 주세요.");
        ArgumentNullException.ThrowIfNull(device);
        Purpose = purpose; WorkSessionId = workSessionId; WorkDate = workDate;
        Attempt = new(new FingerprintIdentityConnection(session, purpose), new BoundDevice(this, device), deviceId);
    }

    private void Current(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); lifetime.Token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(disposed, this); session.ThrowIfChanged();
    }

    /// <summary>Only after the real attempt's verified result. Repeats send the
    /// identical finalize UUID and challenge; they never invoke the sensor again.</summary>
    public async Task<WorkdayIdentityReceipt> CompleteAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Current(ct);
            if (Completed is not null) return Completed;
            if (verifiedChallenge is null)
            {
                var status = Attempt.Result;
                if (status is not { State: "verified", WorkflowState: "pending", IdentityReceipt: not null }
                    || status.IdentityReceipt.Purpose != Purpose || status.ChallengeId == Guid.Empty) throw Invalid();
                CheckRequest(Attempt.IssuedRequest ?? throw Invalid());
                verifiedChallenge = status.ChallengeId;
            }
            // Freeze before I/O: a lost response may have already committed clock-out
            // and removed the normal server session. The existing endpoint accepts only
            // this original context's retained result. Do not send auth/logout to recover.
            HasFinalizeAttempt = true;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token);
            var answer = await session.PostAsync<JsonElement>(
                "device-identity/challenges/" + verifiedChallenge.Value.ToString("D") + "/finalize",
                new { client_request_id = OriginalFinalizeId.ToString("D") }, linked.Token).ConfigureAwait(false);
            Current(ct);
            var receipt = Parse(answer);
            Completed = receipt;
            return receipt;
        }
        finally { gate.Release(); }
    }

    public async Task CancelAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Current(ct);
            if (HasFinalizeAttempt) throw new InvalidOperationException("저장 요청 뒤에는 같은 결과를 확인해야 합니다. 취소로 기록을 되돌리지 않습니다.");
            await Attempt.CancelAsync().ConfigureAwait(false);
            Current(ct);
        }
        finally { gate.Release(); }
    }

    private WorkdayIdentityReceipt Parse(JsonElement answer)
    {
        try
        {
            if (answer.GetProperty("state").GetString() != "completed"
                || answer.TryGetProperty("access_token", out _) || answer.TryGetProperty("pending_token", out _)) throw Invalid();
            var work = Id(answer, "work_session_ref");
            var receipt = Id(answer, "workflow_receipt_id");
            if (!DateOnly.TryParseExact(answer.GetProperty("work_date").GetString(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                || work != WorkSessionId || date != WorkDate) throw Invalid();
            return new(receipt, work, date, Purpose);
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw Invalid(); }
    }

    private static Guid Id(JsonElement value, string name)
    {
        string? s = value.GetProperty(name).GetString();
        if (!Guid.TryParseExact(s, "D", out var id) || id == Guid.Empty || id.ToString("D") != s) throw Invalid();
        return id;
    }
    private static InvalidDataException Invalid() => new("현재 근무의 저장 결과를 확인할 수 없습니다. 같은 작업 결과를 다시 확인해 주세요.");

    private void CheckRequest(byte[] requestBytes)
    {
        try
        {
            using var envelope = JsonDocument.Parse(requestBytes);
            string b64 = envelope.RootElement.GetProperty("payload_b64").GetString()!;
            using var payload = JsonDocument.Parse(Convert.FromBase64String(b64.Replace('-', '+').Replace('_', '/')
                + new string('=', (4 - b64.Length % 4) % 4)));
            var p = payload.RootElement;
            if (Id(p, "work_session_ref") != WorkSessionId || p.GetProperty("purpose").GetString() != Purpose
                || p.GetProperty("work_date").GetString() != WorkDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) throw Invalid();
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or ArgumentNullException)
        { throw Invalid(); }
    }

    private sealed class BoundDevice(FingerprintWorkdayOperation owner, IIdentityRelayDevice inner) : IIdentityRelayDevice
    {
        public Task<byte[]> CaptureAsync(byte[] requestBytes, Func<byte[], CancellationToken, Task<byte[]>> approvalRelay, CancellationToken ct)
        {
            owner.Current(ct);
            // Structural comparison only; server/Pi still verify the MAC. Check before
            // the first physical capture so stale work context cannot consume a scan.
            owner.CheckRequest(requestBytes);
            return inner.CaptureAsync(requestBytes, approvalRelay, ct);
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; lifetime.Cancel(); Attempt.Dispose();
        // Local cleanup only. A committed attendance record is never rolled back here.
    }
}
