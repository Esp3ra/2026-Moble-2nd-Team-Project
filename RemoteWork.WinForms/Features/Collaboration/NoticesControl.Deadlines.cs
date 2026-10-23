using RemoteWork.WinForms.Features.DeadlineAlerts;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

namespace RemoteWork.WinForms.Features.Collaboration;

public partial class NoticesControl
{
    private DeadlineAlertPanel? deadlinePanel;
    private void InitializeDeadlineView()
    {
        // Replace the old fictitious weekly-report sample with a borrowed view.
        layoutContent8.Controls.Remove(txtContent2);
        txtContent2.Dispose();
        deadlinePanel = new DeadlineAlertPanel { Dock = DockStyle.Fill };
        deadlinePanel.RefreshRequested += RefreshDeadlineAlertsAsync;
        layoutContent8.Controls.Add(deadlinePanel,0,1);
    }
    public void BindDeadlineAlerts(DeadlineAlertScope? loginScope) => deadlinePanel?.Bind(loginScope);
    private void BindDeadlineAlertsFromOwner()
    {
        var scope = DeadlineAlertScope.Find(FindForm());
        if (scope is not null) BindDeadlineAlerts(scope);
    }
    private bool connectingDeadline;
    private ApiClient.Session? deadlineSession;
    private async Task RefreshDeadlineAlertsAsync()
    {
        if (connectingDeadline || IsDisposed || lifetimeEnded || FindForm() is not MainForm main || AppServices.Api.CurrentUser is null) return;
        connectingDeadline = true;
        HttpDeadlineAlertSource? candidate = null;
        try
        {
            var scope = DeadlineAlertScope.Find(main);
            if (scope is null)
            {
                var session = deadlineSession ??= AppServices.Api.CaptureSession();
                session.ThrowIfChanged();
                candidate = new HttpDeadlineAlertSource(session);
                // A real full response, not a flag/health200, proves this source is ready.
                await candidate.ReadAsync(lifetime.Token);
                if (IsDisposed || lifetimeEnded) return;
                session.ThrowIfChanged();
                scope = DeadlineAlertScope.Find(main);
                if (scope is null)
                {
                    var owned = candidate;
                    await main.ConnectDeadlineAlertsAsync(_ => owned);
                    main.Disposed += (_, _) => owned.Dispose();
                    candidate = null; // Owner lifetime, never disposed by a menu.
                    scope = DeadlineAlertScope.Find(main);
                }
            }
            if (IsDisposed || lifetimeEnded) return;
            BindDeadlineAlerts(scope);
            if (scope is not null) await scope.CheckAsync();
        }
        catch (Exception ex) when (ex is ApiException or InvalidDataException or OperationCanceledException or HttpRequestException or InvalidOperationException)
        {
            if (!IsDisposed && !lifetimeEnded)
            {
                var existing = DeadlineAlertScope.Find(main);
                if (existing is not null) BindDeadlineAlerts(existing);
                else deadlinePanel?.ShowConnectionError(ex.Message);
            }
        }
        finally { candidate?.Dispose(); connectingDeadline = false; }
    }
}
