using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.LeavePreview;

public partial class LeavePreviewForm : Form
{
    private readonly ApiClient employee = PreviewStore.CreateIdentity(false), reviewer = PreviewStore.CreateIdentity(true);
    private readonly PreviewStore store = new();
    private AttendanceControl? employeePage, reviewerPage;
    private bool released;
    public Task Ready { get; private set; } = Task.CompletedTask;

    public LeavePreviewForm()
    {
        InitializeComponent();
        tabs.SelectedIndexChanged += async (_, _) => await RefreshSelectedAsync();
    }
    protected override void OnShown(EventArgs e)
    { Ready = InitializePreviewAsync(); base.OnShown(e); }
    private async Task InitializePreviewAsync()
    {
        await employee.LoginAsync("preview-employee", "preview-only");
        await reviewer.LoginAsync("preview-reviewer", "preview-only");
        if (released) return;
        var employeeSession = employee.CaptureSession(); var reviewerSession = reviewer.CaptureSession();
        employeePage = new(PreviewRole.Employee, employeeSession, store.For(employeeSession)) { Dock = DockStyle.Fill, LeaveOnly = true, PreviewOnly = true };
        reviewerPage = new(PreviewRole.Administrator, reviewerSession, store.For(reviewerSession)) { Dock = DockStyle.Fill, LeaveOnly = true, PreviewOnly = true };
        employeeHost.Controls.Add(employeePage); reviewerHost.Controls.Add(reviewerPage);
        await RefreshSelectedAsync();
    }
    private async Task RefreshSelectedAsync()
    {
        if (released) return;
        var page = tabs.SelectedIndex == 0 ? employeePage : reviewerPage;
        if (page is not null) await page.RefreshLeaveAsync();
    }
    private void ReleasePreview()
    {
        if (released) return;
        released = true;
        employeePage?.Dispose(); reviewerPage?.Dispose(); employee.Dispose(); reviewer.Dispose();
    }
}
