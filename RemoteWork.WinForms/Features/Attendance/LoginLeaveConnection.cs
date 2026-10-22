using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Attendance;

// UI projections, not another HTTP contract or policy configuration.
public sealed record LeavePreparationState(bool SchemaReady, bool WriteReady, string[] Pending,
    int Year, DateTimeOffset ServerTime);
public sealed record LeaveBalanceState(int Year, int GrantedUnits, int ReservedUnits, int UsedUnits, int RemainingUnits);
public sealed record LeaveScreenState(LeavePreparationState? Preparation, LeaveBalanceState? Balance, string Message);

/// <summary>One captured login owns one workflow, including its original-intent cache.
/// Pages borrow this connection; disposing a page never ends the login or activity monitor.
/// The common adapter validates its HTTP DTOs before projecting them here.</summary>
public sealed class LoginLeaveConnection : System.ComponentModel.Component
{
    private readonly Func<CancellationToken, Task<LeavePreparationState>> readiness;
    private readonly Func<int, CancellationToken, Task<LeaveBalanceState>> balance;
    private readonly CancellationTokenSource lifetime;
    private readonly CancellationToken cancellation;
    private bool disposed;
    public ApiClient.Session Session { get; }
    public ILeaveWorkflow Workflow { get; }
    public bool IsCurrent => !disposed && !cancellation.IsCancellationRequested && LeaveInput.Live(Session);

    public LoginLeaveConnection(ApiClient.Session session, ILeaveWorkflow workflow,
        Func<CancellationToken, Task<LeavePreparationState>> readiness,
        Func<int, CancellationToken, Task<LeaveBalanceState>> balance)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
        Workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
        this.readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
        this.balance = balance ?? throw new ArgumentNullException(nameof(balance));
        session.ThrowIfChanged();
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(session.Cancellation);
        cancellation = lifetime.Token;
    }

    public async Task<LeaveScreenState> ReadScreenAsync(CancellationToken ct = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, ct);
        void Current()
        {
            linked.Token.ThrowIfCancellationRequested();
            Session.ThrowIfChanged();
            if (!IsCurrent) throw new OperationCanceledException();
        }
        Current();
        LeavePreparationState preparation;
        try
        {
            preparation = await readiness(linked.Token).ConfigureAwait(false);
            Current();
            if (preparation is null || preparation.Year is < 2000 or > 9998 || preparation.Pending is null
                || preparation.ServerTime.Offset != TimeSpan.Zero
                || preparation.WriteReady && (!preparation.SchemaReady || preparation.Pending.Length != 0))
                throw new InvalidDataException();
        }
        catch (Exception ex) when (ReadFailure(ex) && !linked.IsCancellationRequested && IsCurrent)
        { return new(null, null, "연차 설정을 확인하지 못했습니다. 다시 조회해 주세요."); }

        LeaveBalanceState? value = null;
        try
        {
            value = await balance(preparation.Year, linked.Token).ConfigureAwait(false);
            Current();
            if (value is null || value.Year != preparation.Year || value.GrantedUnits is < 0 or > 732
                || value.ReservedUnits < 0 || value.UsedUnits < 0 || value.RemainingUnits < 0
                || (long)value.RemainingUnits != (long)value.GrantedUnits - value.ReservedUnits - value.UsedUnits)
                throw new InvalidDataException();
        }
        catch (Exception ex) when (ReadFailure(ex) && !linked.IsCancellationRequested && IsCurrent)
        { value = null; }
        Current();
        string message = preparation.WriteReady ? "연차 신청 연결됨 · 처리 결과는 서버에서 확인합니다."
            : "신청 준비 중 · " + PendingMessage(preparation);
        if (value is null) message += " 잔여일은 확인되지 않았습니다.";
        return new(preparation, value, message);
    }

    private static bool ReadFailure(Exception ex) => ex is ApiException or HttpRequestException
        or InvalidDataException or InvalidOperationException or OperationCanceledException;
    private static string PendingMessage(LeavePreparationState preparation)
    {
        if (!preparation.SchemaReady) return "서버 준비가 필요합니다.";
        var reasons = preparation.Pending.Select(code => code switch
        {
            "LEAVE_POLICY_REQUIRED" => "연차 정책 확인",
            "LEAVE_CALENDAR_REQUIRED" => "휴일 정보 확인",
            "LEAVE_ENTITLEMENT_REQUIRED" => "개인 연차 배정 확인",
            "LEAVE_REVIEWER_REQUIRED" => "검토 담당자 지정",
            _ => "서버 설정 확인"
        }).Distinct().ToArray();
        return reasons.Length == 0 ? "서버 설정 확인이 필요합니다." : string.Join(" · ", reasons) + "이 필요합니다.";
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed) { disposed = true; lifetime.Cancel(); lifetime.Dispose(); }
        base.Dispose(disposing);
    }
}
