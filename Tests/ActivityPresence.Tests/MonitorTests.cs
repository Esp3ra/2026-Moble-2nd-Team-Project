using System.Diagnostics;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Ui;

internal static partial class Program
{
    private sealed class QueuedUi : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? Value)> queue = new();
        public int Pending { get { lock (queue) return queue.Count; } }
        public override void Post(SendOrPostCallback callback, object? value) { lock (queue) queue.Enqueue((callback, value)); }
        public void Drain()
        {
            while (true)
            {
                (SendOrPostCallback Callback, object? Value) item;
                lock (queue) { if (queue.Count == 0) return; item = queue.Dequeue(); }
                item.Callback(item.Value);
            }
        }
    }

    private sealed class ContinuousCamera : IActivityCamera
    {
        public int Starts, Stops, Reads;
        public bool Disposed;
        public bool IsRunning { get; private set; }
        public void Start(ActivityCameraOptions options) { Starts++; IsRunning = true; }
        public ActivityCameraFrame? TakeLatest()
        { Interlocked.Increment(ref Reads); return IsRunning ? new("HAND_DETECTED", Jpeg: jpeg) : null; }
        public Task StopAsync() { Stops++; IsRunning = false; return Task.CompletedTask; }
        public void Dispose() { Disposed = true; IsRunning = false; }
    }

    private sealed class ClosedUi : SynchronizationContext
    {
        public override void Post(SendOrPostCallback callback, object? value) => throw new InvalidOperationException("Synthetic UI shutdown");
    }

    private static async Task MonitorLifecycle()
    {
        var uri = new Uri("http://raspberrypi.local:8080");
        var camera = new ContinuousCamera();
        var light = new FakeLight();
        using (var monitor = new ActivityMonitor(camera, light))
        {
            Check(camera.Starts == 0 && !monitor.IsRunning, "Monitor construction performs no I/O");
            bool identityCurrent = true;
            var context = new ActivityMonitorContext("fixture-work", () => identityCurrent);
            await monitor.StartAsync(context, new(), uri);
            await monitor.StartAsync(context, new(), uri);
            Check(camera.Starts == 1, "Duplicate start for the same context/options is idempotent");
            bool rejected = false;
            try { await monitor.StartAsync(new("fixture-work", () => true), new(), uri); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && monitor.IsRunning && camera.Stops == 0, "Overlapping login context rejected without stopping current capture");
            await Until(() => monitor.Latest.Phase == ActivityMonitorPhase.Running, "Monitor receives evidence without any activity Form");
            Check(monitor.Latest.Status == "활동 중", "Background monitor combines camera and Pi signals");
            int reads = camera.Reads;
            using (var page = new AttendanceControl(PreviewRole.Employee, monitor))
            {
                host.Controls.Add(page); page.Dispose();
                await Until(() => camera.Reads > reads, "Disposing a menu page does not stop background input");
            }
            using (var view = new ActivityPresenceForm(monitor))
            {
                view.Location = new Point(-20000, -20000); view.Show(host);
                await Until(() => Status(view) == "활동 중", "Shared monitor view shows current state");
                Check(!Field<Button>(view, "btnStart").Enabled && !Field<Button>(view, "btnStop").Enabled,
                    "Shared view does not independently start or stop work monitoring");
                view.Close(); await Until(() => view.IsDisposed, "Shared view closes without owning cleanup");
            }
            Check(monitor.IsRunning && camera.Starts == 1 && camera.Stops == 0 && !camera.Disposed,
                "Closing shared view preserves owner-managed monitor");
            using (var reopened = new ActivityPresenceForm(monitor))
            {
                reopened.Location = new Point(-20000, -20000); reopened.Show(host);
                await Until(() => Status(reopened) == "활동 중", "Reopened view uses the same running instance");
                reopened.Close(); await Until(() => reopened.IsDisposed, "Reopened view unsubscribes");
            }
            identityCurrent = false;
            await Until(() => !monitor.IsRunning && camera.Stops == 1 && light.Stops == 1,
                "Invalid login context stops background monitoring without UI intervention");
            await monitor.StopAsync();
            Check(monitor.Latest.Frame is null, "Invalidation clears cached preview");
        }
        Check(camera.Disposed && light.Disposed, "Owner disposal releases both sources");

        var fake = new FakeCamera();
        var fakeLight = new FakeLight();
        using (var monitor = new ActivityMonitor(fake, fakeLight, () => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, false))
        {
            bool current = true, alive = true;
            int rendered = 0;
            var ui = new QueuedUi();
            using var subscription = new ActivityMonitorSubscription(monitor, ui, () => alive, _ => rendered++);
            var context = new ActivityMonitorContext("same-work", () => current);
            await monitor.StartAsync(context, new(), uri);
            fake.Frame = new("HAND_DETECTED", Jpeg: jpeg); monitor.Refresh();
            Check(ui.Pending >= 1 && rendered == 0, "Monitor notifications use Post, never synchronous UI invocation");
            var previous = monitor.Latest;
            await monitor.StopAsync();
            await monitor.StartAsync(new("same-work", () => current), new(), uri);
            fake.Frame = new("HAND_DETECTED", Jpeg: jpeg); monitor.Refresh();
            ui.Drain();
            Check(rendered == 1 && !monitor.IsCurrent(previous), "Queued old generation cannot overwrite same-work relogin");
            fake.Frame = new("HAND_DETECTED", Jpeg: jpeg); monitor.Refresh();
            current = false; ui.Drain();
            Check(rendered == 1, "Session validity rechecked inside already-posted callback");
            int before = ui.Pending;
            fake.Frame = new("HAND_DETECTED", Jpeg: jpeg); monitor.Refresh();
            Check(ui.Pending == before && !monitor.IsRunning, "Invalid session never posts a new view callback");
            await monitor.StopAsync();
            current = true;
            await monitor.StartAsync(new("new-work", () => current), new(), uri);
            fake.Frame = new("HAND_DETECTED", Jpeg: jpeg); monitor.Refresh();
            alive = false; ui.Drain();
            Check(rendered == 1, "Closed/disposed view rejects already-posted callback");
            alive = true;
            fake.Frame = new("HAND_DETECTED", Jpeg: jpeg); monitor.Refresh();
            subscription.Dispose(); ui.Drain();
            Check(rendered == 1, "Unsubscribe invalidates queued callbacks too");
            await monitor.StopAsync();
        }

        fake = new FakeCamera { StopCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        fakeLight = new FakeLight { StopCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using (var monitor = new ActivityMonitor(fake, fakeLight, () => 0, false))
        using (var cancellation = new CancellationTokenSource())
        {
            await monitor.StartAsync(new("cancelled-work", () => true, cancellation.Token), new(), uri);
            var snapshot = monitor.Latest;
            cancellation.Cancel();
            var stopping = monitor.StopAsync();
            Check(!monitor.IsRunning && !monitor.IsCurrent(snapshot) && ReferenceEquals(stopping, monitor.StopAsync()),
                "Cancellation immediately invalidates generation and shares one cleanup Task");
            Check(fake.Stops == 1 && fakeLight.Stops == 1 && !stopping.IsCompleted, "Cleanup is asynchronous and called once per source");
            bool overlapRejected = false;
            try { await monitor.StartAsync(new("pending-stop", () => true), new(), uri); }
            catch (InvalidOperationException) { overlapRejected = true; }
            Check(overlapRejected && monitor.IsStopping && fake.Starts == 1,
                "A new login cannot start before both previous sources are cleaned up");
            fake.StopCompletion.TrySetResult(); await Task.Delay(20);
            Check(!stopping.IsCompleted, "Monitor waits for Pi cleanup after camera is done");
            fakeLight.StopCompletion.TrySetResult(); await stopping;
        }
        fake = new FakeCamera(); fakeLight = new FakeLight();
        using (var monitor = new ActivityMonitor(fake, fakeLight, () => 0, false))
        {
            bool probe = false;
            bool Current() => !probe || Task.Run(() => monitor.Latest).Wait(TimeSpan.FromSeconds(1));
            await monitor.StartAsync(new("lock-order", Current), new(), uri);
            probe = true;
            Check(monitor.IsCurrent(monitor.Latest), "Owner session check does not hold the monitor lock across common-code callbacks");
            await monitor.StopAsync();
        }
        fake = new FakeCamera(); fakeLight = new FakeLight();
        using (var monitor = new ActivityMonitor(fake, fakeLight, () => 0, false))
        using (var subscription = new ActivityMonitorSubscription(monitor, new ClosedUi(), () => true, _ => throw new InvalidOperationException()))
        {
            await monitor.StartAsync(new("closed-ui", () => true), new(), uri);
            fake.Frame = new("HAND_DETECTED", Jpeg: jpeg); monitor.Refresh();
            Check(monitor.IsRunning && fake.Stops == 0, "A closing UI dispatcher cannot stop the owner-managed worker");
            await monitor.StopAsync();
        }
        fake = new FakeCamera { StopCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        fakeLight = new FakeLight();
        using (var monitor = new ActivityMonitor(fake, fakeLight, () => 0, false, TimeSpan.FromMilliseconds(60)))
        {
            await monitor.StartAsync(new("bounded-work", () => true), new(), uri);
            await monitor.StopAsync().WaitAsync(TimeSpan.FromSeconds(2));
            Check(fake.Disposed && fakeLight.Disposed && !monitor.IsRunning, "Bounded cleanup disposes unresponsive owned sources");
            Check(monitor.Latest.Phase == ActivityMonitorPhase.Faulted && monitor.Latest.Frame is null,
                "Cleanup timeout is reported as an error, never a successful stop");
            bool rejected = false;
            try { await monitor.StartAsync(new("new-work", () => true), new(), uri); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "A timed-out source cannot be reused for a new login");
            fake.StopCompletion.TrySetResult();
        }
        scenarios++;
    }
}
