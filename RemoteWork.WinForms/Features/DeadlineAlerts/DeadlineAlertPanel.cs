namespace RemoteWork.WinForms.Features.DeadlineAlerts;

/// <summary>A menu view borrows the login scope. No polling or fake/sample items.</summary>
public sealed class DeadlineAlertPanel : UserControl
{
    private DeadlineAlertScope? scope;
    private readonly Label status = new() { Dock = DockStyle.Fill, Text = "마감 알림 서버 연결 준비 중입니다.", AutoEllipsis = true };
    private readonly Button open = new() { Dock = DockStyle.Bottom, Text = "미확인 알림 보기", Height = 32, Enabled = false };
    public DeadlineAlertPanel()
    {
        Controls.Add(status); Controls.Add(open);
        open.Click += (_, _) =>
        {
            if (scope is not { IsCurrent: true }) return;
            var inbox = new DeadlineAlertInboxForm(scope);
            inbox.FormClosed += (_, _) => inbox.Dispose();
            inbox.Show(FindForm());
        };
        Disposed += (_, _) => { if (scope is not null) scope.Changed -= RefreshStatus; };
    }
    public void Bind(DeadlineAlertScope? borrowed)
    {
        if (scope is not null) scope.Changed -= RefreshStatus;
        scope = borrowed;
        if (scope is not null) scope.Changed += RefreshStatus;
        RefreshStatus();
    }
    private void RefreshStatus()
    {
        if (IsDisposed) return;
        status.Text = scope?.Status ?? "마감 알림 서버 연결 준비 중입니다.";
        open.Enabled = scope is { IsCurrent: true };
    }
}
