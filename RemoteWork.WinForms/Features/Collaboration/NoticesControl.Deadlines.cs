using RemoteWork.WinForms.Features.DeadlineAlerts;

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
        layoutContent8.Controls.Add(deadlinePanel,0,1);
    }
    public void BindDeadlineAlerts(DeadlineAlertScope? loginScope) => deadlinePanel?.Bind(loginScope);
    private void BindDeadlineAlertsFromOwner()
    {
        var scope = DeadlineAlertScope.Find(FindForm());
        if (scope is not null) BindDeadlineAlerts(scope);
    }
}
