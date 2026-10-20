using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RemoteWork.WinForms.Features.Attendance;

/// <summary>One server-confirmed work session, bound by its owner to one login lifetime.</summary>
public sealed class ActivityMonitorContext(string workSessionId, Func<bool> isCurrent, CancellationToken cancellation = default)
{
    public string WorkSessionId { get; } = !string.IsNullOrWhiteSpace(workSessionId) && workSessionId.Length <= 128
        ? workSessionId : throw new ArgumentException("근무 세션을 확인해 주세요.", nameof(workSessionId));
    internal Func<bool> IsCurrent { get; } = isCurrent ?? throw new ArgumentNullException(nameof(isCurrent));
    internal CancellationToken Cancellation { get; } = cancellation;
}

public enum ActivityMonitorPhase { Stopped, Starting, Running, Faulted }

public sealed record ActivityMonitorUpdate(Guid MonitorId, long Generation, long Sequence,
    string? WorkSessionId, ActivityMonitorPhase Phase, string Status, string Detail,
    ActivityCameraFrame? Frame = null);

public interface IActivityMonitor : IDisposable
{
    Guid Id { get; }
    bool IsRunning { get; }
    bool IsStopping { get; }
    ActivityMonitorUpdate Latest { get; }
    event Action<ActivityMonitorUpdate>? Changed;
    Task StartAsync(ActivityMonitorContext context, ActivityCameraOptions camera, Uri piAddress);
    Task StopAsync();
    bool IsCurrent(ActivityMonitorUpdate update);
}

/// <summary>Owns camera/Pi input and local presence independently of any Form. No attendance writes.</summary>
public sealed class ActivityMonitor : IActivityMonitor
{
    private sealed class Run(ActivityMonitorContext context, ActivityCameraOptions options, Uri uri, long generation, double startedAt)
    {
        public readonly ActivityMonitorContext Context = context;
        public readonly ActivityCameraOptions Options = options;
        public readonly Uri Uri = uri;
        public readonly long Generation = generation;
        public readonly double StartedAt = startedAt;
        public readonly ActivityPresenceTracker Presence = new();
        public readonly CancellationTokenSource Cancel = new();
        public CancellationTokenRegistration Registration;
        public Task Pump = Task.CompletedTask;
        public ActivityCameraFrame? Frame;
        public bool FrameUsable;
        public bool Stopping;
    }

    private readonly object gate = new();
    private readonly IActivityCamera camera;
    private readonly ILightMotionSource light;
    private readonly Func<double> now;
    private readonly bool automatic;
    private readonly TimeSpan stopBudget;
    private Run? run;
    private Task stop = Task.CompletedTask;
    private long generation, sequence;
    private bool disposed, broken;
    private ActivityMonitorUpdate latest;
    public Guid Id { get; } = Guid.NewGuid();
    public event Action<ActivityMonitorUpdate>? Changed;

    public ActivityMonitor() : this(new ProcessActivityCamera(), new PollingLightMotionSource()) { }
    public ActivityMonitor(IActivityCamera camera, ILightMotionSource light)
        : this(camera, light, () => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, true) { }

    internal ActivityMonitor(IActivityCamera camera, ILightMotionSource light, Func<double> now,
        bool automatic, TimeSpan? stopBudget = null)
    {
        this.camera = camera;
        this.light = light;
        this.now = now;
        this.automatic = automatic;
        this.stopBudget = stopBudget ?? TimeSpan.FromSeconds(4);
        latest = new(Id, 0, 0, null, ActivityMonitorPhase.Stopped, "카메라 꺼짐", "카메라 시작을 누르면 확인합니다.");
    }

    public ActivityMonitorUpdate Latest { get { lock (gate) return latest; } }
    public bool IsRunning { get { lock (gate) return run is { Stopping: false }; } }
    public bool IsStopping { get { lock (gate) return !stop.IsCompleted; } }

    public async Task StartAsync(ActivityMonitorContext context, ActivityCameraOptions options, Uri piAddress)
    {
        ArgumentNullException.ThrowIfNull(context);
        options.Validate();
        piAddress = LightMotionClient.ParseBaseUri(piAddress.ToString());
        context.Cancellation.ThrowIfCancellationRequested();
        if (!context.IsCurrent()) throw new OperationCanceledException("로그인 접속이 변경되었습니다.");
        ActivityMonitorUpdate update;
        bool ownsRun = false;
        try
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (broken) throw new InvalidOperationException("장치 정리가 끝나지 않았습니다. 감지 서비스를 다시 준비해 주세요.");
                if (!stop.IsCompleted) throw new InvalidOperationException("장치를 정리하고 있습니다.");
                if (run is { Stopping: false } existing)
                {
                    if (ReferenceEquals(context, existing.Context) && options == existing.Options && piAddress == existing.Uri) return;
                    throw new InvalidOperationException("다른 근무 세션의 감지가 실행 중입니다.");
                }
                context.Cancellation.ThrowIfCancellationRequested();
                var current = new Run(context, options, piAddress, ++generation, now());
                run = current;
                ownsRun = true;
                update = Set(current, ActivityMonitorPhase.Starting, "확인 대기", "카메라를 준비하고 있습니다.");
                camera.Start(options);
                light.Start(piAddress);
                // The callback invalidates synchronously and never waits for UI or cleanup.
                current.Registration = context.Cancellation.UnsafeRegister(state => { _ = StopAsync(); }, null);
                if (automatic && !current.Stopping) current.Pump = Task.Run(() => PumpAsync(current));
            }
        }
        catch
        {
            if (ownsRun) await StopAsync().ConfigureAwait(false);
            throw;
        }
        if (IsCurrent(update)) Changed?.Invoke(update);
    }

    private async Task PumpAsync(Run current)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            while (await timer.WaitForNextTickAsync(current.Cancel.Token).ConfigureAwait(false))
            {
                lock (gate) if (run != current || current.Stopping) return;
                Refresh();
            }
        }
        catch (OperationCanceledException) when (current.Cancel.IsCancellationRequested) { }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            lock (gate)
            {
                if (run != current || current.Stopping) return;
                Set(current, ActivityMonitorPhase.Faulted, "확인 필요", "감지 처리를 계속할 수 없습니다. 장치 연결을 확인해 주세요.");
            }
            _ = StopCore(preserveFault: true);
        }
    }

    // Manual check windows/tests may drive the same input pipeline on their own clock.
    internal ActivityMonitorUpdate Refresh()
    {
        ActivityMonitorUpdate update;
        bool invalid = false, fault = false;
        Run? candidate;
        lock (gate) candidate = run;
        // CaptureSession.IsCurrent has its own lock. Never call owner code
        // while holding the monitor lock (authentication cancellation can re-enter).
        bool sessionCurrent = candidate is not null && !candidate.Context.Cancellation.IsCancellationRequested && candidate.Context.IsCurrent();
        lock (gate)
        {
            if (run is not { Stopping: false } current || current != candidate) return latest;
            if (current.Context.Cancellation.IsCancellationRequested || !sessionCurrent)
            {
                invalid = true;
                update = latest;
            }
            else
            {
                update = Read(current);
                fault = update.Phase == ActivityMonitorPhase.Faulted;
            }
        }
        if (invalid || fault)
        {
            _ = StopCore(preserveFault: fault);
            return Latest;
        }
        if (IsCurrent(update)) Changed?.Invoke(update);
        return update;
    }

    private ActivityMonitorUpdate Read(Run current)
    {
        if (light.TakeLatest() is { } sample)
            current.Presence.ObserveLight(sample, sample.ReceivedAt / (double)Stopwatch.Frequency);
        var frame = camera.TakeLatest();
        double observed = now(); // Read after inputs, including injected test clocks.
        if (frame is not null)
        {
            current.Frame = frame;
            current.FrameUsable = false;
            if (frame.State == "STARTING")
            {
                current.Presence.InvalidateCamera();
                return Set(current, ActivityMonitorPhase.Starting, "확인 대기", frame.Error ?? "카메라를 준비하고 있습니다.");
            }
            if (frame.State == "CAMERA_ERROR")
            {
                current.Presence.InvalidateCamera();
                return Set(current, ActivityMonitorPhase.Faulted,
                    frame.Error?.Contains("영상이 끊겼") == true ? "카메라 연결 끊김" : "카메라 확인 필요",
                    frame.Error ?? "카메라 연결을 확인해 주세요.");
            }
            if (frame.State == "STALE" || frame.CurrentAge > 5) return Stale(current);
            try
            {
                if (frame.Jpeg is null || frame.MotionDetected is null) throw new InvalidDataException();
                using var stream = new MemoryStream(frame.Jpeg, writable: false);
                using var decoded = Image.FromStream(stream);
                current.FrameUsable = true;
                current.Presence.ObserveCamera(frame.State == "HAND_DETECTED", frame.MotionDetected.Value,
                    observed - frame.CurrentAge, frame.SecondsSinceHand, frame.SecondsSinceMovement);
            }
            catch (Exception error) when (error is ArgumentException or ExternalException or InvalidDataException)
            {
                current.Presence.InvalidateCamera();
                return Set(current, ActivityMonitorPhase.Running, "카메라 확인 필요",
                    "미리보기 영상을 표시할 수 없습니다. 중지 후 다시 시작해 주세요.");
            }
        }
        else if (current.Frame is { } previous ? previous.CurrentAge > 5 : observed - current.StartedAt > 5)
            return Stale(current);
        if (current.FrameUsable && current.Frame is { State: "HAND_DETECTED" or "HAND_NOT_DETECTED" })
        {
            var view = current.Presence.Evaluate(observed);
            return Set(current, ActivityMonitorPhase.Running, view.Status, view.Detail, current.Frame);
        }
        return latest;
    }

    private ActivityMonitorUpdate Stale(Run current)
    {
        current.Presence.InvalidateCamera();
        return Set(current, ActivityMonitorPhase.Running, "카메라 연결 지연", current.Frame?.State == "STARTING"
            ? $"{current.Frame.Error}. 카메라 번호와 다른 카메라 프로그램의 실행 여부를 확인해 주세요."
            : "최신 영상을 기다리고 있습니다.");
    }

    private ActivityMonitorUpdate Set(Run current, ActivityMonitorPhase phase, string status, string detail, ActivityCameraFrame? frame = null)
        => latest = new(Id, current.Generation, ++sequence, current.Context.WorkSessionId, phase, status, detail, frame);

    public bool IsCurrent(ActivityMonitorUpdate update)
    {
        Run? candidate;
        lock (gate) candidate = run;
        bool sessionCurrent = candidate is not null && !candidate.Context.Cancellation.IsCancellationRequested && candidate.Context.IsCurrent();
        lock (gate)
            return sessionCurrent && !disposed && run is { Stopping: false } current && current == candidate && current.Generation == generation &&
                update.MonitorId == Id && update.Generation == generation && update.Sequence == latest.Sequence &&
                update.WorkSessionId == current.Context.WorkSessionId && !current.Context.Cancellation.IsCancellationRequested;
    }

    public Task StopAsync() => StopCore(preserveFault: false);

    private Task StopCore(bool preserveFault)
    {
        lock (gate)
        {
            if (run is not { Stopping: false } current) return stop;
            current.Stopping = true;
            ++generation; // Invalidate all queued views before starting asynchronous cleanup.
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            stop = completion.Task;
            current.Registration.Unregister();
            current.Cancel.Cancel();
            latest = latest with { Generation = generation, Sequence = ++sequence, Frame = null,
                Phase = preserveFault ? ActivityMonitorPhase.Faulted : ActivityMonitorPhase.Stopped,
                Status = preserveFault ? latest.Status : "카메라 꺼짐",
                Detail = preserveFault ? latest.Detail : "카메라 시작을 누르면 확인합니다." };
            _ = FinishStopAsync(current, completion);
            return stop;
        }
    }

    private async Task FinishStopAsync(Run current, TaskCompletionSource completion)
    {
        Task pending = Task.CompletedTask;
        try
        {
            pending = Task.WhenAll(camera.StopAsync(), light.StopAsync(), current.Pump);
            await pending.WaitAsync(stopBudget).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            lock (gate)
            {
                broken = true;
                latest = latest with { Phase = ActivityMonitorPhase.Faulted, Status = "카메라 확인 필요",
                    Detail = "장치 정리를 완료하지 못했습니다. 감지 서비스를 다시 준비해 주세요.", Frame = null };
            }
            camera.Dispose(); light.Dispose();
            _ = pending.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        }
        finally
        {
            current.Registration.Unregister();
            current.Cancel.Dispose();
            completion.TrySetResult();
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            _ = StopAsync();
            camera.Dispose(); light.Dispose();
            Changed = null;
        }
    }
}

/// <summary>Posts to a view without allowing queued callbacks to resurrect an old login/run.</summary>
public sealed class ActivityMonitorSubscription : IDisposable
{
    private readonly IActivityMonitor monitor;
    private readonly Action<ActivityMonitorUpdate> handler;
    private int disposed;

    public ActivityMonitorSubscription(IActivityMonitor monitor, SynchronizationContext ui,
        Func<bool> viewAlive, Action<ActivityMonitorUpdate> render)
    {
        this.monitor = monitor;
        handler = update =>
        {
            if (Volatile.Read(ref disposed) != 0 || !viewAlive() || !monitor.IsCurrent(update)) return;
            try { ui.Post(_ =>
            {
                if (Volatile.Read(ref disposed) == 0 && viewAlive() && monitor.IsCurrent(update)) render(update);
            }, null); }
            catch (Exception error) when (error is ObjectDisposedException or InvalidOperationException or System.ComponentModel.InvalidAsynchronousStateException)
            { Dispose(); }
        };
        monitor.Changed += handler;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0) monitor.Changed -= handler;
    }
}
