namespace RemoteWork.WinForms.Infrastructure;

/// <summary>
/// Borrowed handoff for a newly created account, not proof of server enrollment.
/// Only the owning user-management view can create/invalidate this target.
/// </summary>
public sealed class CreatedUserEnrollmentTarget
{
    private readonly CancellationTokenSource lifetime;
    private readonly Func<bool> isViewCurrent;
    private int invalidated;
    public UserInfo User { get; }
    public int UserId => User.Id;
    public ApiClient.Session AdministratorSession { get; }
    public CancellationToken Cancellation { get; }
    public bool IsCurrent => Volatile.Read(ref invalidated) == 0
        && !Cancellation.IsCancellationRequested && AdministratorSession.IsCurrent && isViewCurrent();

    internal CreatedUserEnrollmentTarget(UserInfo user, ApiClient.Session administratorSession, Func<bool> isViewCurrent)
    {
        if (user.Id <= 0) throw new ArgumentException("서버에서 생성한 회원 ID가 필요합니다.", nameof(user));
        User = user;
        AdministratorSession = administratorSession;
        this.isViewCurrent = isViewCurrent;
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(administratorSession.Cancellation);
        Cancellation = lifetime.Token;
    }

    public void ThrowIfChanged()
    {
        if (!IsCurrent) throw new OperationCanceledException("등록 대상 또는 관리자 접속이 변경되었습니다.", Cancellation);
    }

    internal void Invalidate()
    {
        if (Interlocked.Exchange(ref invalidated, 1) != 0) return;
        try { lifetime.Cancel(); }
        finally { lifetime.Dispose(); }
    }
}
