using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;

internal static partial class Program
{
    private static async Task Loopback(Form owner, string[] args)
    {
        if (args.Length != 6 || args[5] is not ("normal" or "loss")) throw new ArgumentException("Owned workday test arguments required");
        var server = ApiClient.ValidateUrl(args[1]); var pi = ApiClient.ValidateUrl(args[2]);
        if (new[] { server, pi }.Any(u => u.Scheme != "http" || u.Host != "127.0.0.1")) throw new InvalidOperationException("Owned loopback only");
        var direct = new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false, UseProxy = false };
        using var api = new ApiClient(server.ToString(), args[5] == "loss" ? new LostFinalize(direct) : direct);
        await api.LoginAsync(Environment.GetEnvironmentVariable("MEMBER5_TEST_LOGIN_ID")!, Environment.GetEnvironmentVariable("MEMBER5_TEST_PASSWORD")!);
        var session = api.CaptureSession(); var reader = new WorkdayStateConnection(session); var monitor = new Monitor();
        int shown = 0; FingerprintWorkdayOperation? original = null;
        using var host = new LoginWorkdayActivity(owner, session,
            async ct => { var s = await reader.ReadAsync(ct); return new(s.WorkSessionId, s.WorkDate, s.ServerTime, s.NoonAt, s.NoonDue); },
            new(Guid.Parse(args[3]), pi, new()), monitor, async operation =>
            {
                shown++; original ??= operation;
                Check(ReferenceEquals(original, operation), "Real recovery retains original noon operation");
                using var form = new FingerprintWorkdayForm(operation, a => a.RunAsync(), autoStart: true);
                var shownForm = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                form.Shown += (_, _) => shownForm.TrySetResult();
                form.Show(owner); await shownForm.Task; await form.PendingOperation;
                if (args[5] == "loss" && shown == 1)
                    Check(form.CompletedReceipt is null && operation.HasFinalizeAttempt, "Real lost noon response stays unknown; " +
                        Field<Label>(form, "lblStatus").Text + "; " + operation.Attempt.Latest.Detail);
                else Check(form.CompletedReceipt is not null, "Real server noon receipt confirmed; " +
                    Field<Label>(form, "lblStatus").Text + "; " + operation.Attempt.Latest.Detail);
                form.Dispose(); // Borrowed operation is retained by host, not this view.
            }, automaticallyPoll: false);
        await host.StartAsync();
        Check(host.Workday?.WorkSessionId == Guid.Parse(args[4]) && !host.Workday.NoonDue && monitor.Starts == 1,
            "Actual reader starts one monitor for server-confirmed pre-noon work");
        await host.CheckAsync();
        Check(shown == 1, "Actual server due transition opens one confirmation");
        if (args[5] == "loss")
        { Check(host.CanCheckNoon, "Retained result recovery allowed even after server reports noon saved"); await host.ShowNoonAsync(); }
        Check(original?.Completed is not null && monitor.Starts == 1 && host.Workday?.NoonAt is not null && !host.CanCheckNoon,
            "One completed noon result without duplicate monitoring");
        await host.StopAsync(); Check(monitor.Stops == 1, "Owned host drains monitor");
        Console.WriteLine(JsonSerializer.Serialize(new { passed = true, mode = args[5], checks, shown, monitor_starts = monitor.Starts, monitor_stops = monitor.Stops }));
    }
    private sealed class LostFinalize(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private bool lost;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            if (!lost && request.RequestUri!.AbsolutePath.EndsWith("/finalize") && response.IsSuccessStatusCode)
            { lost = true; await response.Content.ReadAsByteArrayAsync(ct); response.Dispose(); throw new HttpRequestException("Synthetic discarded noon finalize response"); }
            return response;
        }
    }
}
