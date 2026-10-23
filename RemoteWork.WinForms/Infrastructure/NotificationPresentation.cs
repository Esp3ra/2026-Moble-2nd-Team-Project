using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Features.DeadlineAlerts;

namespace RemoteWork.WinForms.Infrastructure;

/// <summary>UI-thread presentation only; does not grant rights or consume notices.</summary>
public static class NotificationPresentation
{
    public static bool CanPresent(Form owner) => !owner.IsDisposed && !owner.Disposing &&
        owner.Visible && owner.Enabled && owner.WindowState != FormWindowState.Minimized &&
        !Application.OpenForms.Cast<Form>().Any(form => form != owner && form.Visible &&
            (form.Modal || form is ImportantNoticeForm or CollaborationResponseForm or DeadlineAlertForm or DeadlineAlertInboxForm));
}
