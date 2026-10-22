using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Shell;

public partial class MainForm
{
    private LoginWorkdayActivity? workdayActivity;

    /// <summary>Explicit composition only after a real server workday reader and
    /// trusted device/camera configuration have been provided. Default startup stays off.</summary>
    public async Task ConnectWorkdayActivityAsync(
        Func<ApiClient.Session, Func<CancellationToken, Task<WorkdayActivityState>>> readerFactory,
        WorkdayActivityOptions options, IActivityMonitor? monitor = null)
    {
        if (signedInUser is null || closingSession || expired || IsDisposed || !IsHandleCreated || InvokeRequired)
            throw new InvalidOperationException("현재 로그인 화면에서 근무를 연결해 주세요.");
        if (workdayActivity is not null) throw new InvalidOperationException("이 로그인에는 이미 근무가 연결됐습니다.");
        ArgumentNullException.ThrowIfNull(readerFactory);
        var session = AppServices.Api.CaptureSession();
        if (session.User.Id != signedInUser.Id) throw new OperationCanceledException();
        var reader = readerFactory(session); session.ThrowIfChanged();
        workdayActivity = new(this, session, reader, options, monitor);
        components!.Add(workdayActivity);
        var bar = new WorkdayActivityBar { Dock = DockStyle.Fill }; bar.Bind(workdayActivity);
        int row = layoutWorkspace.RowCount++;
        layoutWorkspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
        layoutWorkspace.SetRowSpan(layoutSidebar, layoutWorkspace.RowCount);
        layoutWorkspace.Controls.Add(bar, 1, row);
        await workdayActivity.StartAsync();
    }
}
