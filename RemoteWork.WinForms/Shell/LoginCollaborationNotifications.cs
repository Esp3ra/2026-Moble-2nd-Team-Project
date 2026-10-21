using System.ComponentModel;
using System.Globalization;
using RemoteWork.WinForms.Features.Collaboration;

namespace RemoteWork.WinForms.Shell;

/// <summary>Login window owns start/stop. Menu dialogs borrow the one scope.</summary>
public sealed class LoginCollaborationNotifications : Component
{
    private readonly Form owner;
    private readonly CancellationTokenRegistration identityEnded;
    private bool stopped;
    public CollaborationNotificationScope Scope { get; }

    public LoginCollaborationNotifications(Form owner, ICollaborationService service, TimeSpan interval)
    {
        if (!service.IsCurrent) throw new OperationCanceledException("현재 로그인이 필요합니다.");
        this.owner = owner;
        Scope = new(owner, service, interval);
        owner.Shown += Start;
        owner.FormClosing += Stop;
        owner.Disposed += OwnerDisposed;
        identityEnded = service.Cancellation.Register(() =>
        {
            if (owner.IsDisposed || !owner.IsHandleCreated) return;
            try { owner.BeginInvoke((Action)Dispose); }
            catch (InvalidOperationException) { }
        });
    }

    // No silent 10-second production policy: an explicit client setting is
    // required. Missing/invalid settings keep automatic reception disconnected.
    public static TimeSpan? ParseInterval(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) &&
        seconds is >= 1 and <= 3600 ? TimeSpan.FromSeconds(seconds) : null;

    private async void Start(object? sender, EventArgs e)
    {
        if (!stopped) await Scope.StartAsync();
    }
    private void Stop(object? sender, FormClosingEventArgs e) => Dispose();
    private void OwnerDisposed(object? sender, EventArgs e) => Dispose();
    protected override void Dispose(bool disposing)
    {
        if (disposing && !stopped)
        {
            stopped = true;
            owner.Shown -= Start;
            owner.FormClosing -= Stop;
            owner.Disposed -= OwnerDisposed;
            identityEnded.Dispose();
            Scope.Dispose();
        }
        base.Dispose(disposing);
    }
}
