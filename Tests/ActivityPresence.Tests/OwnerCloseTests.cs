using RemoteWork.WinForms.Features.Attendance;

internal static partial class Program
{
    private static async Task OwnerCloseRegression()
    {
        foreach (string path in new[] { "child", "owner", "expiry-early", "expiry-late" })
            foreach (bool delayed in new[] { false, true })
                await ModalClosePath(path, delayed);
        scenarios++;
    }

    private static async Task ModalClosePath(string path, bool delayed)
    {
        string label = $"{path}/{(delayed ? "delayed" : "immediate")}";
        using var owner = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
            Location = new Point(-20000, -20000) };
        var camera = new FakeCamera();
        var light = new FakeLight();
        if (delayed)
        {
            camera.StopCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            light.StopCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        using var child = new ActivityPresenceForm(camera, light)
            { StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        int ownerClosed = 0, modalReturns = 0;
        owner.FormClosed += (_, _) => ownerClosed++;
        owner.Show(host);
        Exception? failure = null;
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var trigger = new System.Windows.Forms.Timer { Interval = 20 };
        trigger.Tick += async (_, _) =>
        {
            trigger.Stop();
            try
            {
                Click(child, "btnStart");
                if (path == "owner") owner.Close();
                else child.Close();
                if (path == "expiry-early") owner.Close();
                if (delayed)
                {
                    Check(!child.IsDisposed && !owner.IsDisposed, label + ": cleanup keeps forms alive without blocking UI");
                    camera.StopCompletion!.TrySetResult();
                    await Task.Delay(30);
                    Check(!child.IsDisposed, label + ": Pi cleanup is awaited as well as camera cleanup");
                    light.StopCompletion!.TrySetResult();
                }
                await Until(() => child.IsDisposed, label + ": modal cleanup completes");
                if (path == "expiry-late") owner.Close();
                if (path == "child")
                    Check(!owner.IsDisposed && owner.Visible && ownerClosed == 0, label + ": normal modal close preserves owner");
                else
                    await Until(() => owner.IsDisposed && ownerClosed == 1, label + ": owner close resumes exactly once");
                await Until(() => modalReturns == 1, label + ": owned modal returns once");
                Check(camera.Disposed && !camera.IsRunning && light.Disposed && !light.Running,
                    label + ": both sources released");
                Check(camera.Stops == 1 && light.Stops == 1, label + ": repeated closing shares one cleanup");
            }
            catch (Exception error) { failure = error; }
            finally
            {
                camera.StopCompletion?.TrySetResult();
                light.StopCompletion?.TrySetResult();
                child.Dispose();
                owner.Dispose();
                finished.TrySetResult();
            }
        };
        trigger.Start();
        child.ShowDialog(owner);
        child.Dispose();
        modalReturns++;
        await finished.Task;
        if (failure is not null) throw new InvalidOperationException(label, failure);
    }
}
