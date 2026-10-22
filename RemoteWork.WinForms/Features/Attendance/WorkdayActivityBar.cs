namespace RemoteWork.WinForms.Features.Attendance;

public partial class WorkdayActivityBar : UserControl
{
    private LoginWorkdayActivity? activity;
    public WorkdayActivityBar() { InitializeComponent(); }
    public void Bind(LoginWorkdayActivity host)
    {
        if (activity is not null) throw new InvalidOperationException("이미 근무 상태가 연결됐습니다.");
        activity = host; host.Changed += Render; refreshTimer.Start(); Render();
    }
    private void Render()
    {
        if (IsDisposed || activity is null) return;
        lblWorkday.Text = activity.Status + (activity.IsCurrent && activity.Monitor.IsRunning ? " · " + activity.Monitor.Latest.Status : "");
        btnNoon.Enabled = activity.CanCheckNoon;
        btnRefresh.Enabled = activity.IsCurrent;
        btnActivity.Enabled = activity.IsCurrent && activity.Workday is not null;
    }
    private async void Noon_Click(object? sender, EventArgs e) { if (activity is not null) await activity.ShowNoonAsync(); }
    private async void Refresh_Click(object? sender, EventArgs e) { if (activity is not null) await activity.CheckAsync(); }
    private void Activity_Click(object? sender, EventArgs e)
    {
        if (activity?.IsCurrent != true) return;
        using var view = new ActivityPresenceForm(activity.Monitor); view.ShowDialog(FindForm());
    }
    private void ReleaseBar() { if (activity is not null) activity.Changed -= Render; }
    private void RefreshTimer_Tick(object? sender, EventArgs e) => Render();
}
