using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;
using RemoteWork.WinForms.Ui;

internal static partial class Program
{
    private static async Task ClockOutLoopback(string[] args)
    {
#if CLOCKOUT_PEER
        if (args.Length != 6 || args[5] is not ("normal" or "loss")) throw new ArgumentException("Owned clock-out fixture required");
        var server = ApiClient.ValidateUrl(args[1]); var pi = ApiClient.ValidateUrl(args[2]);
        if (new[] { server, pi }.Any(u => u.Scheme != "http" || u.Host != "127.0.0.1")) throw new InvalidOperationException("Owned loopback only");
        var api = AppServices.Api; string previous = api.ServerUri.ToString(); api.SetServer(server.ToString());
        try
        {
            var login = await api.LoginAsync(Environment.GetEnvironmentVariable("MEMBER5_TEST_LOGIN_ID")!, Environment.GetEnvironmentVariable("MEMBER5_TEST_PASSWORD")!);
            using var main = new MainForm(login.User) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
            main.Show(); var monitor = new Monitor(); int prepares = 0, shown = 0; FingerprintWorkdayOperation? original = null;
            await main.ConnectWorkdayActivityAsync(new(Guid.Parse(args[3]), pi, new()), monitor,
                prepareClockOut: async (request, stop, ct) =>
                {
                    prepares++;
                    var common = await main.PrepareClockOutFinalizationAsync(request.WorkSessionId, request.WorkDate,
                        request.ChallengeId, request.OriginalFinalizeId, stop, ct);
                    var actual = new WorkdayClockOutRequest(common.WorkSessionId, common.WorkDate, common.ChallengeId, common.OriginalFinalizeId);
                    return new(actual, () => common.IsCurrent, common.CompleteAsync, () => main.FinishConfirmedClockOut(common));
                }, presentClockOut: async op =>
                {
                    shown++; original ??= op; Check(ReferenceEquals(original, op), "Actual common owner retains feature operation across views");
                    await PresentWorkday(main, op);
                    Check(args[5] == "loss" && shown == 1 ? op.HasFinalizeAttempt && op.Completed is null : op.Completed is not null,
                        "Actual clock-out form reports only the authoritative saved result");
                });
            var host = Field<LoginWorkdayActivity>(main, "workdayActivity");
            Check(host.Workday?.WorkSessionId == Guid.Parse(args[4]) && monitor.Starts == 1, "Merged reader wires actual MainForm to existing server work");
            await host.ShowClockOutAsync();
            if (args[5] == "loss")
            {
                Check(api.CurrentUser is not null && !main.IsDisposed && host.CanClockOut && !host.CanRefresh && !monitor.IsRunning,
                    "Uncertain committed clock-out preserves original login only for result recovery");
                bool refused = false;
                try { await api.CaptureSession().GetAsync<IdentityResult>("auth/me"); } catch (OperationCanceledException) { refused = true; }
                Check(refused && host.IsCurrent, "Ordinary auth check is blocked without expiring original recovery");
                await host.ShowClockOutAsync();
            }
            await host.StopAsync();
            Check(original?.Completed is not null && api.CurrentUser is null && !main.Visible && prepares == 1,
                "Confirmed original clock-out ends actual MainForm once");
            Check(monitor.Starts == 1 && monitor.Stops == 1, "Single monitor drains before finalization without restart");
            Console.WriteLine(JsonSerializer.Serialize(new { passed = true, mode = args[5], shown, prepares, checks,
                monitor_starts = monitor.Starts, monitor_stops = monitor.Stops }));
        }
        finally { api.ClearIdentity(); api.SetServer(previous); }
#else
        await Task.CompletedTask;
        throw new InvalidOperationException("Run only in the reviewed PR73 combined export with ClockOutPeer=true");
#endif
    }
}
