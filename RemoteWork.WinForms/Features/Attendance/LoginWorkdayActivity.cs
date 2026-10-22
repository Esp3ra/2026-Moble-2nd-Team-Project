using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Attendance;

// UI projection of member 1's validated WorkdayState, not another HTTP DTO/reader.
public sealed record WorkdayActivityState(Guid WorkSessionId, DateOnly WorkDate,
    DateTimeOffset ServerTime, DateTimeOffset? NoonAt, bool NoonDue);

public sealed record WorkdayActivityOptions(Guid FingerprintDeviceId, Uri PiAddress,
    ActivityCameraOptions Camera, TimeSpan? PollInterval = null);

/// <summary>One shown login owns the reader, a single activity monitor and one
/// retained noon operation. Does not change normal authentication or clock-out.</summary>
public sealed class LoginWorkdayActivity : System.ComponentModel.Component
{
    private readonly Form owner;
    private readonly Func<CancellationToken, Task<WorkdayActivityState>> read;
    private readonly WorkdayActivityOptions options;
    private readonly Func<Uri, IIdentityRelayDevice> deviceFactory;
    private readonly Func<FingerprintWorkdayOperation, Task> presentNoon;
    private readonly SynchronizationContext ui;
    private readonly int uiThread;
    private readonly CancellationTokenSource lifetime;
    private readonly CancellationToken cancellation;
    private readonly CancellationTokenRegistration sessionEnding;
    private readonly System.Windows.Forms.Timer timer;
    private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private FingerprintWorkdayOperation? noonOperation;
    private IIdentityRelayDevice? device;
    private bool reading, showingNoon, started, beforeNoonSeen, noonPrompted, disposed;
    private int ended;
    private Task pendingCheck = Task.CompletedTask;
    public ApiClient.Session Session { get; }
    public IActivityMonitor Monitor { get; }
    public WorkdayActivityState? Workday { get; private set; }
    public bool HasFreshWorkday { get; private set; }
    public bool IsCurrent => Volatile.Read(ref ended) == 0 && !cancellation.IsCancellationRequested && Session.IsCurrent;
    public bool CanCheckNoon => IsCurrent && !showingNoon &&
        (HasFreshWorkday && Workday?.NoonDue == true || noonOperation is { HasFinalizeAttempt: true, Completed: null });
    public string Status { get; private set; } = "현재 근무 연결 준비 중";
    public event Action? Changed;

    public LoginWorkdayActivity(Form owner, ApiClient.Session session,
        Func<CancellationToken, Task<WorkdayActivityState>> read, WorkdayActivityOptions options,
        IActivityMonitor? monitor = null, Func<FingerprintWorkdayOperation, Task>? presentNoon = null,
        Func<Uri, IIdentityRelayDevice>? deviceFactory = null, bool automaticallyPoll = true)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Session = session ?? throw new ArgumentNullException(nameof(session));
        this.read = read ?? throw new ArgumentNullException(nameof(read));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        if (!owner.IsHandleCreated || owner.InvokeRequired || SynchronizationContext.Current is null)
            throw new InvalidOperationException("표시된 로그인 화면의 UI 스레드에서 연결해 주세요.");
        if (options.FingerprintDeviceId == Guid.Empty) throw new ArgumentException("등록 장치를 확인해 주세요.");
        ArgumentNullException.ThrowIfNull(options.Camera);
        ArgumentNullException.ThrowIfNull(options.PiAddress);
        options.Camera.Validate();
        _ = LightMotionClient.ParseBaseUri(options.PiAddress.ToString());
        var interval = options.PollInterval ?? TimeSpan.FromSeconds(15);
        if (interval < TimeSpan.FromSeconds(1) || interval > TimeSpan.FromMinutes(5)) throw new ArgumentOutOfRangeException(nameof(options));
        session.ThrowIfChanged();
        ui = SynchronizationContext.Current; uiThread = Environment.CurrentManagedThreadId;
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(session.Cancellation);
        cancellation = lifetime.Token;
        Monitor = monitor ?? new ActivityMonitor();
        this.deviceFactory = deviceFactory ?? (address => new HttpFingerprintIdentityDevice(address.ToString()));
        this.presentNoon = presentNoon ?? PresentNoon;
        timer = new() { Interval = (int)interval.TotalMilliseconds, Tag = automaticallyPoll };
        timer.Tick += async (_, _) => await CheckAsync();
        owner.FormClosing += OwnerClosing;
        sessionEnding = session.Cancellation.Register(() => { _ = StopAsync(); });
    }

    private Task PresentNoon(FingerprintWorkdayOperation operation)
    {
        using var form = new FingerprintWorkdayForm(operation, autoStart: !operation.HasFinalizeAttempt);
        form.ShowDialog(owner);
        return Task.CompletedTask;
    }
    private void OnUi()
    {
        if (Environment.CurrentManagedThreadId != uiThread) throw new InvalidOperationException("로그인 화면의 UI 스레드에서 실행해 주세요.");
    }
    public async Task StartAsync()
    {
        OnUi();
        if (started) throw new InvalidOperationException("이 로그인에는 이미 근무 감시가 연결됐습니다.");
        started = true;
        await CheckAsync();
        if (IsCurrent && timer.Tag is true) timer.Start();
    }
    public Task CheckAsync(bool allowAutomaticPrompt = true)
    {
        OnUi();
        if (!IsCurrent) return Task.CompletedTask;
        if (reading) return pendingCheck;
        return pendingCheck = CheckCoreAsync(allowAutomaticPrompt);
    }
    private async Task CheckCoreAsync(bool allowAutomaticPrompt)
    {
        reading = true;
        try
        {
            var next = await read(cancellation);
            if (!IsCurrent) return;
            Validate(next);
            if (Workday is { } old && (old.WorkSessionId != next.WorkSessionId || old.WorkDate != next.WorkDate))
            {
                SetStatus("근무 정보가 변경됐습니다. 로그인과 현재 근무를 다시 확인해 주세요.");
                await StopAsync(); return;
            }
            if (Workday is { } previous && (next.ServerTime < previous.ServerTime || previous.NoonAt is not null && next.NoonAt is null))
                throw new InvalidDataException("서버 근무 상태가 이전 기록과 다릅니다.");
            Workday = next; HasFreshWorkday = true;
            if (next.NoonAt is not null) noonPrompted = true;
            else if (next.ServerTime.ToOffset(TimeSpan.FromHours(9)).Hour < 12) beforeNoonSeen = true;
            if (!Monitor.IsRunning)
            {
                var expected = next.WorkSessionId;
                await Monitor.StartAsync(new ActivityMonitorContext(expected.ToString("D"),
                    () => IsCurrent && HasFreshWorkday && Workday?.WorkSessionId == expected, cancellation), options.Camera, options.PiAddress);
                if (!IsCurrent) { await Monitor.StopAsync(); return; }
            }
            SetStatus(next.NoonDue ? "정오 지문 확인이 필요합니다." : next.NoonAt is not null ? "근무 중 · 정오 지문 확인 완료" : "근무 중 · 활동 상태 확인 중");
        }
        catch (Exception e) when (e is ApiException or HttpRequestException or InvalidDataException or InvalidOperationException or OperationCanceledException or ArgumentException)
        {
            HasFreshWorkday = false;
            await Monitor.StopAsync();
            if (IsCurrent) SetStatus(e is ApiException { StatusCode: 404 or 503 }
                ? "근무 연결 준비가 필요합니다. 서버 설정을 확인해 주세요." : "현재 근무 확인 필요 · 다시 확인을 눌러주세요.");
        }
        finally { reading = false; }
        // Initial late connection does not invent a missed-noon/grace policy.
        // Only a before-noon observation followed by server NoonDue opens once.
        if (allowAutomaticPrompt && IsCurrent && HasFreshWorkday && beforeNoonSeen && !noonPrompted
            && Workday?.NoonDue == true && owner.Visible && owner.Enabled && !showingNoon)
            await ShowNoonAsync();
    }
    public async Task ShowNoonAsync()
    {
        OnUi();
        if (!IsCurrent || showingNoon) return;
        showingNoon = true; Changed?.Invoke();
        try
        {
            await CheckAsync(allowAutomaticPrompt: false);
            if (!IsCurrent) return;
            bool recovering = noonOperation is { HasFinalizeAttempt: true, Completed: null };
            if (!recovering && (!HasFreshWorkday || Workday?.NoonDue != true)) return;
            if (noonOperation is { HasFinalizeAttempt: false } old && old.Attempt.Latest.Phase is
                IdentityAttemptPhase.Cancelled or IdentityAttemptPhase.Rejected or IdentityAttemptPhase.Expired)
                ReleaseNoon(); // Only this explicit new user action may replace a known terminal scan.
            if (noonOperation is null)
            {
                var created = deviceFactory(options.PiAddress);
                try
                {
                    noonOperation = new(Session, Workday!.WorkSessionId, Workday.WorkDate, "noon_check", options.FingerprintDeviceId, created);
                    device = created;
                }
                catch { (created as IDisposable)?.Dispose(); throw; }
            }
            noonPrompted = true;
            await presentNoon(noonOperation);
            if (IsCurrent) await CheckAsync(allowAutomaticPrompt: false);
        }
        catch (Exception e) when (e is ApiException or HttpRequestException or InvalidDataException or InvalidOperationException or OperationCanceledException or ArgumentException)
        { if (IsCurrent) SetStatus("정오 지문 결과 확인 필요 · 같은 작업을 다시 확인해 주세요."); }
        finally { showingNoon = false; if (!disposed) Changed?.Invoke(); }
    }
    private static void Validate(WorkdayActivityState state)
    {
        if (state is null || state.WorkSessionId == Guid.Empty || state.WorkDate == default
            || state.ServerTime.Offset != TimeSpan.Zero || state.ServerTime == default
            || DateOnly.FromDateTime(state.ServerTime.ToOffset(TimeSpan.FromHours(9)).DateTime) != state.WorkDate
            || state.NoonAt is { } noon && (noon.Offset != TimeSpan.Zero || noon > state.ServerTime
                || DateOnly.FromDateTime(noon.ToOffset(TimeSpan.FromHours(9)).DateTime) != state.WorkDate)
            || state.NoonDue != (state.NoonAt is null && state.ServerTime.ToOffset(TimeSpan.FromHours(9)).Hour >= 12))
            throw new InvalidDataException("현재 근무 상태를 확인할 수 없습니다.");
    }
    private void SetStatus(string text) { Status = text; if (!disposed) Changed?.Invoke(); }
    private void OwnerClosing(object? sender, FormClosingEventArgs e) { _ = StopAsync(); }
    public Task StopAsync()
    {
        if (Interlocked.Exchange(ref ended, 1) == 0)
        {
            lifetime.Cancel();
            if (Environment.CurrentManagedThreadId == uiThread) _ = FinishStopAsync();
            else ui.Post(_ => { _ = FinishStopAsync(); }, null);
        }
        return stopped.Task;
    }
    private async Task FinishStopAsync()
    {
        try { timer.Stop(); HasFreshWorkday = false; ReleaseNoon(); await Monitor.StopAsync(); }
        catch (Exception e) when (e is not OutOfMemoryException)
        { Monitor.Dispose(); SetStatus("활동 감지 종료 확인이 필요합니다."); }
        finally { stopped.TrySetResult(); if (!disposed) Changed?.Invoke(); }
    }
    private void ReleaseNoon()
    { noonOperation?.Dispose(); noonOperation = null; (device as IDisposable)?.Dispose(); device = null; }
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true; sessionEnding.Unregister(); owner.FormClosing -= OwnerClosing;
            _ = StopAsync(); timer.Dispose(); Monitor.Dispose(); Changed = null;
            _ = stopped.Task.ContinueWith(_ => lifetime.Dispose(), TaskScheduler.Default);
        }
        base.Dispose(disposing);
    }
}
