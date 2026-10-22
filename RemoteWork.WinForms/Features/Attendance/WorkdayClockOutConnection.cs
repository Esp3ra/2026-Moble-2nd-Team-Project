using System.Text.Json;

namespace RemoteWork.WinForms.Features.Attendance;

public sealed record WorkdayClockOutRequest(Guid WorkSessionId, DateOnly WorkDate,
    Guid ChallengeId, Guid OriginalFinalizeId);

/// <summary>Borrowed, login-owned finalization only. The common owner retains its
/// token/drain/result lifetime. Disposing a feature view never disposes that owner.</summary>
public sealed class WorkdayClockOutConnection
{
    private readonly Func<bool> isCurrent;
    private readonly Func<CancellationToken, Task<JsonElement>> complete;
    private readonly Action finish;
    public WorkdayClockOutRequest Request { get; }
    public bool IsCurrent => isCurrent();
    public WorkdayClockOutConnection(WorkdayClockOutRequest request, Func<bool> isCurrent,
        Func<CancellationToken, Task<JsonElement>> complete, Action finish)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.WorkSessionId == Guid.Empty || request.WorkDate == default ||
            request.ChallengeId == Guid.Empty || request.OriginalFinalizeId == Guid.Empty)
            throw new ArgumentException("원 퇴근 작업 식별값이 필요합니다.");
        Request = request;
        this.isCurrent = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
        this.complete = complete ?? throw new ArgumentNullException(nameof(complete));
        this.finish = finish ?? throw new ArgumentNullException(nameof(finish));
    }
    public Task<JsonElement> CompleteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsCurrent) throw new OperationCanceledException("원 퇴근 접속을 확인해 주세요.");
        return complete(ct);
    }
    public void FinishConfirmed()
    {
        if (!IsCurrent) throw new OperationCanceledException("원 퇴근 접속을 확인해 주세요.");
        finish(); // The common owner rechecks its own confirmed receipt and shown login.
    }
}
