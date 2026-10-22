using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Shell;

public partial class MainForm
{
    private ApiClient.ClockOutFinalization? clockOutFinalization;
    private bool preparingClockOut;

    /// <summary>Explicit owner seam after the feature's verified clock-out scan.
    /// The feature supplies its owned monitor/poll stop-and-wait callback.
    /// Closing a borrowed popup does not dispose this login-owned recovery.</summary>
    public async Task<ApiClient.ClockOutFinalization> PrepareClockOutFinalizationAsync(
        Guid workSessionId, DateOnly workDate, Guid challengeId, Guid originalFinalizeId,
        Func<Task> stopWorkdayAndWait, CancellationToken ct = default)
    {
        if (InvokeRequired || IsDisposed || !IsHandleCreated || signedInUser is null || expired
            || closingSession || preparingClockOut || clockOutFinalization is { IsCurrent: true })
            throw new InvalidOperationException("현재 로그인 화면에서 하나의 퇴근 결과를 연결해 주세요.");
        ArgumentNullException.ThrowIfNull(stopWorkdayAndWait);
        var session = AppServices.Api.CaptureSession();
        if (session.User.Id != signedInUser.Id) throw new OperationCanceledException();
        preparingClockOut = true;
        try
        {
            await stopWorkdayAndWait();
            ct.ThrowIfCancellationRequested(); session.ThrowIfChanged();
            var connection = await session.BeginClockOutFinalizationAsync(workSessionId, workDate,
                challengeId, originalFinalizeId, ct);
            try
            {
                if (IsDisposed || expired || closingSession) throw new OperationCanceledException();
                session.ThrowIfChanged();
                clockOutFinalization = connection;
                components!.Add(new LifecycleCleanup(connection.Dispose));
                return connection;
            }
            catch { connection.Dispose(); throw; }
        }
        finally { preparingClockOut = false; }
    }

    /// <summary>Only a server-validated original receipt can end this shown login.
    /// Ordinary Close/Logout retain their original behavior and are not clock-out.</summary>
    public void FinishConfirmedClockOut(ApiClient.ClockOutFinalization connection)
    {
        if (InvokeRequired || IsDisposed || !ReferenceEquals(clockOutFinalization, connection)
            || !connection.HasConfirmedReceipt || !connection.IsCurrent)
            throw new InvalidOperationException("이 로그인에 연결한 원 퇴근 저장 결과가 필요합니다.");
        closingSession = true;
        identityTimer.Stop();
        deadlineNotifications?.Dispose();
        collaborationNotifications?.Dispose();
        noticeCoordinator?.Dispose();
        connection.EndSession();
        allowClose = true;
        Close();
    }
}
