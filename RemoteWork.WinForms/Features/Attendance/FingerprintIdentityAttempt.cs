using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Attendance;

public enum IdentityAttemptPhase
{
    Ready, Requesting, WaitingForFinger, SendingResult, Verified, Rejected,
    TimedOut, ConnectionLost, CheckingCancellation, Cancelled, Expired, ContextEnded, ReviewRequired
}

public sealed record IdentityAttemptUpdate(long Sequence, IdentityAttemptPhase Phase,
    string Title, string Detail, bool CanRetry, bool CanCancel);

/// <summary>One explicit UI attempt. Keeps issue/cancel IDs and exact relay bytes in
/// RAM. A retry queries the same attempt, never starts another sensor capture.
/// Caller owns the captured connection and device; no finalize/login side effects.</summary>
public sealed class FingerprintIdentityAttempt : IDisposable, IIdentityRelayServer
{
    private readonly FingerprintIdentityConnection connection;
    private readonly IIdentityRelayDevice device;
    private readonly Guid deviceId;
    private readonly SemaphoreSlim gate = new(1);
    private readonly CancellationTokenSource lifetime = new(), captureStop = new();
    private readonly CancellationToken lifetimeToken;
    private readonly object updateLock = new();
    private readonly CancellationTokenRegistration contextEnding;
    private FingerprintIdentityRelay? relay;
    private IdentityChallengeTicket? ticket;
    private bool issueStarted, captureStarted, terminal;
    private int cancelRequested, disposed;
    private long sequence;
    public Guid OriginalRequestId { get; } = Guid.NewGuid();
    public Guid OriginalCancelId { get; } = Guid.NewGuid();
    private IdentityStatus? result;
    public IdentityStatus? Result => IsCurrent ? result : null;
    public bool IsCurrent => Volatile.Read(ref disposed) == 0 && connection.IsCurrent;
    public IdentityAttemptUpdate Latest { get; private set; } = new(0, IdentityAttemptPhase.Ready,
        "지문 본인 확인", "시작을 누른 뒤 OLED 안내에 따라 손가락을 올려주세요.", true, true);
    public event EventHandler<IdentityAttemptUpdate>? Changed;

    public FingerprintIdentityAttempt(FingerprintIdentityConnection connection, IIdentityRelayDevice device, Guid deviceId)
    {
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.device = device ?? throw new ArgumentNullException(nameof(device));
        if (deviceId == Guid.Empty) throw new ArgumentException("등록된 장치 번호가 필요합니다.", nameof(deviceId));
        this.deviceId = deviceId;
        lifetimeToken = lifetime.Token;
        contextEnding = connection.Cancellation.Register(RefreshContext);
    }

    public void RefreshContext()
    {
        if (Volatile.Read(ref disposed) != 0 || connection.IsCurrent) return;
        captureStop.Cancel();
        if (Latest.Phase != IdentityAttemptPhase.ContextEnded)
            Update(IdentityAttemptPhase.ContextEnded, "접속 확인 필요", "로그인 접속이 바뀌었거나 본인 확인 시간이 지났습니다. 이전 요청 결과를 현재 계정에 적용하지 않습니다.");
    }

    public async Task RunAsync(CancellationToken cancellation = default)
    {
        if (!await gate.WaitAsync(0, cancellation).ConfigureAwait(false)) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(connection.Cancellation,
            lifetimeToken, captureStop.Token, cancellation);
        try
        {
            Current(linked.Token);
            if (terminal || Volatile.Read(ref cancelRequested) != 0) return;
            if (ticket is null)
            {
                Update(IdentityAttemptPhase.Requesting, "지문 요청 확인 중", "같은 요청 번호로 서버와 연결하고 있습니다.", cancel: true);
                if (issueStarted) ticket = await connection.LookupAsync(OriginalRequestId, linked.Token).ConfigureAwait(false);
                if (ticket is null)
                {
                    issueStarted = true; // Freeze the UUID before an uncertain HTTP write.
                    ticket = await connection.IssueAsync(deviceId, OriginalRequestId, linked.Token).ConfigureAwait(false);
                }
                Current(linked.Token);
            }
            if (ticket.State != "issued" || captureStarted && relay?.HasPendingResult != true)
            {
                var status = await connection.GetAsync(ticket.ChallengeId, linked.Token).ConfigureAwait(false);
                Current(linked.Token); ShowStatus(status);
                return;
            }
            relay ??= new(ticket.ChallengeId, deviceId.ToString("D"), connection.Purpose, ticket.Request,
                () => connection.IsCurrent && Volatile.Read(ref cancelRequested) == 0,
                connection.Cancellation, this, device);
            bool resume = captureStarted;
            captureStarted = true;
            Update(resume ? IdentityAttemptPhase.SendingResult : IdentityAttemptPhase.WaitingForFinger,
                resume ? "같은 지문 결과 확인 중" : "지문 입력 대기",
                resume ? "센서를 다시 읽지 않고 서버에 원래 결과를 확인합니다."
                    : "OLED 안내에 따라 손가락을 떼고 다시 올려주세요. 지문 등록이 아닌 본인 확인입니다.", cancel: true);
            var result = resume ? await relay.ResumeCompletionAsync(linked.Token).ConfigureAwait(false)
                : await relay.RunAsync(linked.Token).ConfigureAwait(false);
            Current(linked.Token); ShowStatus(result);
        }
        catch (Exception error) when (Expected(error)) { ShowError(error, cancellation); }
        finally { gate.Release(); }
    }

    public async Task CancelAsync(CancellationToken cancellation = default)
    {
        if (Volatile.Read(ref disposed) != 0) return;
        Interlocked.Exchange(ref cancelRequested, 1);
        captureStop.Cancel(); // Stop Pi I/O immediately, before waiting for the active UI call.
        await gate.WaitAsync(cancellation).ConfigureAwait(false);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(connection.Cancellation, lifetimeToken, cancellation);
        try
        {
            Current(linked.Token);
            if (terminal) return;
            Update(IdentityAttemptPhase.CheckingCancellation, "취소 결과 확인 중", "장치 입력을 중지하고 서버의 같은 작업을 확인합니다.");
            if (!issueStarted)
            {
                terminal = true;
                Update(IdentityAttemptPhase.Cancelled, "지문 요청 취소", "서버 요청을 보내기 전에 취소했습니다.");
                return;
            }
            ticket ??= await connection.LookupAsync(OriginalRequestId, linked.Token).ConfigureAwait(false);
            Current(linked.Token);
            if (ticket is null)
            {
                Update(IdentityAttemptPhase.ReviewRequired, "취소 결과 확인 필요",
                    "원래 요청이 아직 조회되지 않습니다. 취소 확인을 다시 눌러주세요. 새 지문 요청은 보내지 않습니다.", false, true);
                return;
            }
            var status = await connection.CancelAsync(ticket.ChallengeId, OriginalCancelId, linked.Token).ConfigureAwait(false);
            Current(linked.Token); ShowStatus(status);
        }
        catch (Exception error) when (Expected(error)) { ShowError(error, cancellation); }
        finally { gate.Release(); }
    }

    private void Current(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
        connection.ThrowIfChanged();
    }
    private void ShowStatus(IdentityStatus status)
    {
        if (!IsCurrent) { RefreshContext(); return; }
        result = status;
        terminal = status.State != "issued";
        switch (status.State)
        {
            case "verified":
                Update(IdentityAttemptPhase.Verified, "지문 본인 확인 완료",
                    "서버가 지문 일치를 확인했습니다. 로그인·출퇴근 완료는 별도 처리 결과를 확인해야 합니다."); break;
            case "cancelled": case "revoked":
                Update(IdentityAttemptPhase.Cancelled, "지문 요청 취소 완료", "서버에서 작업이 종료된 것을 확인했습니다."); break;
            case "expired":
                Update(IdentityAttemptPhase.Expired, "지문 요청 시간 만료", "이 요청은 종료됐습니다. 새 요청은 담당 화면에서 다시 시작해 주세요."); break;
            case "rejected":
                Update(IdentityAttemptPhase.Rejected, "지문 본인 확인 실패", "서버가 본인 확인을 승인하지 않았습니다. 지문 등록 상태와 OLED 안내를 확인해 주세요."); break;
            default:
                Update(IdentityAttemptPhase.ReviewRequired, "원래 작업 확인 필요",
                    "서버 작업이 아직 진행 상태입니다. 같은 작업의 결과 확인 또는 취소만 할 수 있습니다. 센서를 자동으로 다시 읽지 않습니다.", Volatile.Read(ref cancelRequested) == 0, true); break;
        }
    }
    private void ShowError(Exception error, CancellationToken caller)
    {
        if (Volatile.Read(ref disposed) != 0) return;
        if (!connection.IsCurrent || connection.Cancellation.IsCancellationRequested)
            Update(IdentityAttemptPhase.ContextEnded, "접속 확인 필요", "로그인 접속이 바뀌었거나 본인 확인 시간이 지났습니다. 이전 요청 결과를 현재 계정에 적용하지 않습니다.");
        else if (Volatile.Read(ref cancelRequested) != 0)
            Update(IdentityAttemptPhase.ReviewRequired, "취소 결과 확인 필요", "장치 입력은 중지했습니다. 서버의 취소 완료는 아직 확인되지 않았습니다. 취소 확인을 다시 눌러주세요.", false, true);
        else if (error is OperationCanceledException && !caller.IsCancellationRequested
            || error is IdentityPiHttpException { StatusCode: System.Net.HttpStatusCode.RequestTimeout })
            Update(IdentityAttemptPhase.TimedOut, "지문 응답 시간 초과", "원래 작업 결과부터 확인해 주세요. 결과 확인은 지문을 다시 읽지 않습니다.", true, true);
        else if (error is ApiException { StatusCode: 404 or 503 })
            Update(IdentityAttemptPhase.ReviewRequired, "서버 지문 기능 확인 필요", "서버의 지문 기능 또는 업무 연결이 준비되지 않았습니다. 담당자에게 실행 버전과 설정 확인을 요청해 주세요. 같은 요청은 유지됩니다.", true, true);
        else if (error is ApiException { StatusCode: 403 })
            Update(IdentityAttemptPhase.ReviewRequired, "계정·장치 연결 확인 필요", "서버가 이 계정과 장치의 본인 확인을 허용하지 않았습니다. 등록 연결과 권한을 담당자에게 확인해 주세요.", true, true);
        else if (error is HttpRequestException or ApiException)
            Update(IdentityAttemptPhase.ConnectionLost, "지문 연결 확인 필요", "서버 또는 라파와 연결하지 못했습니다. 주소·네트워크를 확인한 뒤 같은 작업 결과를 확인해 주세요.", true, true);
        else
            Update(IdentityAttemptPhase.ReviewRequired, "지문 결과 확인 필요", "원래 작업을 유지했습니다. 같은 결과를 확인하거나 취소해 주세요. 지문을 자동으로 다시 읽지 않습니다.", true, true);
    }
    private static bool Expected(Exception error) => error is ApiException or HttpRequestException
        or OperationCanceledException or InvalidDataException or InvalidOperationException or ObjectDisposedException;
    private void Update(IdentityAttemptPhase phase, string title, string detail, bool retry = false, bool cancel = false)
    {
        IdentityAttemptUpdate value;
        bool contextCurrent = connection.IsCurrent; // Never take the common identity lock under our update lock.
        lock (updateLock)
        {
            if (Volatile.Read(ref disposed) != 0) return;
            if (!contextCurrent)
            {
                phase = IdentityAttemptPhase.ContextEnded; title = "접속 확인 필요";
                detail = "로그인 접속이 바뀌었거나 본인 확인 시간이 지났습니다. 이전 요청 결과를 현재 계정에 적용하지 않습니다.";
                retry = cancel = false;
            }
            Latest = value = new(++sequence, phase, title, detail, retry, cancel);
        }
        Changed?.Invoke(this, value);
    }
    Task<string> IIdentityRelayServer.ApproveAsync(Guid id, string probe, CancellationToken ct)
        => connection.ApproveAsync(id, probe, ct);
    Task<IdentityStatus> IIdentityRelayServer.GetAsync(Guid id, CancellationToken ct)
        => connection.GetAsync(id, ct);
    Task<IdentityStatus> IIdentityRelayServer.CompleteAsync(Guid id, string result, CancellationToken ct)
    {
        Update(IdentityAttemptPhase.SendingResult, "지문 결과 확인 중", "라파의 원래 결과를 서버에 보내 확인하고 있습니다.", cancel: true);
        return connection.CompleteAsync(id, result, ct);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        contextEnding.Unregister();
        lifetime.Cancel(); captureStop.Cancel(); relay?.Dispose();
        Changed = null;
        // Owner keeps its connection and device. Remote cancellation needs an awaited CancelAsync.
    }
}
