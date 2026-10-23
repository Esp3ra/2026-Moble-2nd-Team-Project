using System.Net;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;

internal static partial class Program
{
    private static async Task WindowAndReconnect(Form owner)
    {
        using var api = await Login(); var source = new Source(); var monitor = new Monitor(); int prompts = 0;
        using var host = new LoginWorkdayActivity(owner, api.CaptureSession(), source.Read, Options, monitor,
            _ => { prompts++; return Task.CompletedTask; }, _ => new NoSensor(), automaticallyPoll: false);
        using var bar = new WorkdayActivityBar(); owner.Controls.Add(bar); bar.Show(); bar.Bind(host);
        Check(typeof(WorkdayActivityBar).GetField("btnRefresh", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) is null,
            "Ordinary work-refresh button is removed from the Designer");
        var reconnect = Field<Button>(bar, "btnReconnect");
        Check(!reconnect.Visible && !host.CanReconnect, "A new connection never shows the error-only action");
        await host.StartAsync();
        Check(!reconnect.Visible && !host.CanCheckNoon, "Normal pre-window work has no refresh/reconnect or fingerprint action");
        source.State = Before with { ServerTime = DateTimeOffset.Parse("2026-10-22T01:59:59Z") };
        await host.CheckAsync(); Check(prompts == 0 && !host.CanCheckNoon, "10:59:59 does not start afternoon fingerprint");
        source.State = Due; await host.CheckAsync();
        Check(prompts == 1 && host.CanCheckNoon && Field<Button>(bar, "btnNoon").Text == "오후 업무 전 지문 확인",
            "11:00 starts one clearly named afternoon fingerprint prompt");
        source.State = Due with { ServerTime = DateTimeOffset.Parse("2026-10-22T02:59:59Z") };
        await host.CheckAsync(); Check(prompts == 1 && host.CanCheckNoon, "11:59:59 stays eligible without another automatic prompt");
        source.State = Due with { ServerTime = DateTimeOffset.Parse("2026-10-22T03:00:00Z"), NoonDue = false };
        await host.CheckAsync(); await host.ShowNoonAsync();
        Check(prompts == 1 && !host.CanCheckNoon && host.Status.Contains("완료 기록 없음") && !host.Status.Contains("확인 완료"),
            "12:00 closes new scans and never turns a missed window into completion");
        source.Error = new HttpRequestException("synthetic disconnected"); await host.CheckAsync();
        Check(reconnect.Visible && reconnect.Enabled && host.CanReconnect && !monitor.IsRunning,
            "Only a real connection error exposes the dedicated reconnect action");
        int reads = source.Reads; source.Error = null; reconnect.PerformClick();
        Check(source.Reads == reads + 1 && !reconnect.Visible && !host.CanReconnect && monitor.IsRunning,
            "Error reconnect restores the same work monitor and hides itself after success");
        await host.StopAsync();
        await NoonReceiptRecoveryAfterWindow(owner);
    }

    private static async Task NoonReceiptRecoveryAfterWindow(Form owner)
    {
        var wire = new ClockServer("noon_check"); var server = new NoonRecoveryServer(wire);
        using var api = new ApiClient("http://noon.test", server); await api.LoginAsync("synthetic", "synthetic");
        var source = new Source { State = Due }; var monitor = new Monitor(); var device = new ClockDevice(wire);
        FingerprintWorkdayOperation? original = null; int shown = 0;
        using var host = new LoginWorkdayActivity(owner, api.CaptureSession(), source.Read, Options, monitor,
            async operation => { original ??= operation; shown++; Check(ReferenceEquals(original, operation), "Recovery owns the same noon operation"); await PresentWorkday(owner, operation); },
            _ => device, automaticallyPoll: false);
        await host.StartAsync();
        Check(original?.HasFinalizeAttempt == true && original.Completed is null && host.CanCheckNoon,
            "A committed response loss keeps the original receipt recovery available");
        source.State = Due with { ServerTime = DateTimeOffset.Parse("2026-10-22T03:00:00Z"), NoonAt = Due.ServerTime, NoonDue = false };
        await host.CheckAsync();
        using var bar = new WorkdayActivityBar(); owner.Controls.Add(bar); bar.Show(); bar.Bind(host);
        Check(host.CanCheckNoon && Field<Button>(bar, "btnNoon").Text == "지문 저장 결과 확인",
            "After 12:00 the surviving action explicitly recovers the saved result");
        await host.ShowNoonAsync();
        Check(shown == 2 && device.Captures == 1 && server.FinalizeIds.Count == 2
            && server.FinalizeIds.Distinct().Single() == original!.OriginalFinalizeId && original.Completed is not null,
            "Outside-window recovery uses one fingerprint and the original finalize UUID");
        Check(!host.CanCheckNoon && monitor.Starts == 1 && monitor.IsRunning && api.CurrentUser is not null,
            "Recovered afternoon attendance preserves ordinary login and the same monitor");
        await host.StopAsync();
    }

    private sealed class NoonRecoveryServer(ClockServer wire) : DelegatingHandler(wire)
    {
        public List<Guid> FinalizeIds { get; } = [];
        private readonly Guid receipt = Guid.NewGuid();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (!request.RequestUri!.AbsolutePath.EndsWith("/finalize")) return await base.SendAsync(request, ct);
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            FinalizeIds.Add(doc.RootElement.GetProperty("client_request_id").GetGuid());
            if (FinalizeIds.Count == 1) throw new HttpRequestException("Synthetic committed afternoon response lost");
            return Reply(new { state = "completed", workflow_receipt_id = receipt, work_session_ref = Work, work_date = "2026-10-22" });
        }
    }
}
