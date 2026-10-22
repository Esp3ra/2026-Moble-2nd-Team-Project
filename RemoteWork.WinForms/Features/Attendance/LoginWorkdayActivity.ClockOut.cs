using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Attendance;

public sealed partial class LoginWorkdayActivity
{
    private readonly Func<WorkdayClockOutRequest, Func<Task>, CancellationToken, Task<WorkdayClockOutConnection>>? prepareClockOut;
    private readonly Func<FingerprintWorkdayOperation, Task> presentClockOut;
    private FingerprintWorkdayOperation? clockOutOperation;
    private IIdentityRelayDevice? clockOutDevice;
    private bool showingClockOut, clockOutPaused;
    public bool CanRefresh => IsCurrent && !clockOutPaused && !showingClockOut;
    public bool CanClockOut => IsCurrent && prepareClockOut is not null && !showingNoon && !showingClockOut &&
        (HasFreshWorkday && Workday is not null || clockOutOperation?.IsCurrent == true);

    public async Task ShowClockOutAsync()
    {
        OnUi();
        if (!CanClockOut) return;
        showingClockOut = true; Changed?.Invoke();
        try
        {
            if (!clockOutPaused) await CheckAsync(allowAutomaticPrompt: false);
            if (!IsCurrent || !clockOutPaused && (!HasFreshWorkday || Workday is null)) return;
            if (clockOutOperation is { HasFinalizeAttempt: false } old && old.Attempt.Latest.Phase is
                IdentityAttemptPhase.Cancelled or IdentityAttemptPhase.Rejected or IdentityAttemptPhase.Expired)
                ReleaseClockOut();
            if (clockOutOperation is null)
            {
                var created = deviceFactory(options.PiAddress);
                try
                {
                    clockOutOperation = new(Session, Workday!.WorkSessionId, Workday.WorkDate, "clock_out",
                        options.FingerprintDeviceId, created, PrepareOnUiAsync);
                    clockOutDevice = created;
                }
                catch { (created as IDisposable)?.Dispose(); throw; }
            }
            await presentClockOut(clockOutOperation);
            if (!IsCurrent) return;
            if (clockOutOperation.Completed is not null)
            {
                SetStatus("퇴근 저장 확인 완료 · 접속 종료 중");
                clockOutOperation.ClockOutConnection!.FinishConfirmed();
            }
            else if (clockOutPaused) SetStatus("퇴근 결과 확인 필요 · 같은 퇴근 결과를 확인해 주세요.");
            else await CheckAsync(allowAutomaticPrompt: false);
        }
        catch (Exception error) when (error is ApiException or HttpRequestException or InvalidDataException
            or InvalidOperationException or OperationCanceledException or ArgumentException)
        { if (IsCurrent) SetStatus("퇴근 결과 확인 필요 · 새 스캔 대신 같은 작업을 확인해 주세요."); }
        finally { showingClockOut = false; if (!disposed) Changed?.Invoke(); }
    }

    private Task<WorkdayClockOutConnection> PrepareOnUiAsync(WorkdayClockOutRequest request, CancellationToken ct)
    {
        var result = new TaskCompletionSource<WorkdayClockOutConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
        int dispatchState = 0;
        var registration = ct.Register(() =>
        { if (Interlocked.CompareExchange(ref dispatchState, 2, 0) == 0) result.TrySetCanceled(ct); });
        async void Prepare(object? _)
        {
            try
            {
                if (Interlocked.CompareExchange(ref dispatchState, 1, 0) != 0) return;
                ct.ThrowIfCancellationRequested();
                if (!IsCurrent || disposed) throw new OperationCanceledException(ct);
                result.TrySetResult(await prepareClockOut!(request, PauseForClockOutAsync, ct));
            }
            catch (OperationCanceledException) { result.TrySetCanceled(ct); }
            catch (Exception error) { result.TrySetException(error); }
            finally { registration.Dispose(); }
        }
        if (Environment.CurrentManagedThreadId == uiThread) Prepare(null);
        else ui.Post(Prepare, null);
        return result.Task;
    }

    private async Task PauseForClockOutAsync()
    {
        OnUi();
        if (!IsCurrent) throw new OperationCanceledException(cancellation);
        clockOutPaused = true; HasFreshWorkday = false; timer.Stop();
        SetStatus("퇴근 저장 준비 · 활동 감지와 근무 조회 종료 중");
        await Monitor.StopAsync();
        await pendingCheck.WaitAsync(cancellation);
        if (!IsCurrent) throw new OperationCanceledException(cancellation);
    }
    private void ReleaseClockOut()
    {
        clockOutOperation?.Dispose(); clockOutOperation = null;
        (clockOutDevice as IDisposable)?.Dispose(); clockOutDevice = null;
        // Common finalization stays with MainForm, never a borrowed feature view.
    }
}
