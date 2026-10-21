using System.ComponentModel;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration;

/// <summary>Owner explicitly chooses polling interval; shared MainForm wiring needs member 1 review.</summary>
public sealed class CollaborationNotificationCoordinator : Component
{
    private readonly Form owner;
    private readonly ICollaborationService service;
    private readonly System.Windows.Forms.Timer timer;
    private readonly CancellationTokenSource lifetime;
    private readonly HashSet<string> displayed = [];
    private CollaborationResponseForm? popup;
    private bool checking, stopped;
    public event Action<string>? StatusChanged;
    public CollaborationNotificationCoordinator(Form owner, ICollaborationService service, TimeSpan interval)
    {
        this.owner = owner; this.service = service;
        if (interval.TotalMilliseconds is < 1000 or > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(interval));
        timer = new() { Interval = (int)interval.TotalMilliseconds };
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(service.Cancellation);
        timer.Tick += async (_, _) => await CheckAsync();
        owner.FormClosed += OwnerClosed;
    }
    private bool Current => !stopped && !owner.IsDisposed && !owner.Disposing && service.IsCurrent && !lifetime.IsCancellationRequested;
    private bool CanPresent => Current && popup is null && owner.Visible &&
        owner.WindowState != FormWindowState.Minimized &&
        !Application.OpenForms.Cast<Form>().Any(f => f.Visible &&
            (f is ImportantNoticeForm || f is CollaborationResponseForm));
    public async Task StartAsync() { if (Current) { timer.Start(); await CheckAsync(); } }
    public async Task CheckAsync()
    {
        if (!Current) { timer.Stop(); popup?.Close(); return; }
        if (checking || !CanPresent) return;
        checking = true;
        try
        {
            var ct = lifetime.Token;
            var received = await service.ListAsync(false, ct);
            if (!Current) return;
            var sent = await service.ListAsync(true, ct);
            if (!Current) return;
            StatusChanged?.Invoke("");
            var candidate = received.Where(x => x.Status == "pending")
                .Concat(sent.Where(x => x.Status is "accepted" or "declined" or "left" or "expired" or "ended"))
                .FirstOrDefault(x => !displayed.Contains(x.RequestId + ":" + x.RowVersion));
            // UI state can change while either list request is awaited (or a
            // status subscriber runs). Recheck immediately before claiming the
            // candidate; yielding must not consume its displayed-version key.
            if (candidate is null || !CanPresent) return;
            displayed.Add(candidate.RequestId + ":" + candidate.RowVersion);
            popup = new CollaborationResponseForm(service, candidate);
            var active = popup;
            active.FormClosed += (_, _) => { if (popup == active) popup = null; active.Dispose(); };
            active.Show(owner);
        }
        catch (OperationCanceledException) { }
        catch (ApiException ex)
        {
            if (Current)
            {
                StatusChanged?.Invoke("협업 알림 연결 실패: " + ex.Message);
                if (ex.StatusCode is 401 or 403 or 404 or 405) timer.Stop();
            }
        }
        finally { checking = false; }
    }
    private void OwnerClosed(object? sender, FormClosedEventArgs e) => Dispose();
    protected override void Dispose(bool disposing)
    {
        if (disposing && !stopped)
        {
            stopped = true; owner.FormClosed -= OwnerClosed;
            timer.Stop(); timer.Dispose(); lifetime.Cancel(); popup?.Close(); lifetime.Dispose();
        }
        base.Dispose(disposing);
    }
}
