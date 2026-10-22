using RemoteWork.WinForms.Features.DeadlineAlerts;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Shell;

public partial class MainForm
{
    private LoginDeadlineNotifications? deadlineNotifications;

    /// <summary>Call on the shown login's UI thread only after a real server source is provided.</summary>
    public async Task ConnectDeadlineAlertsAsync(Func<ApiClient.Session, IDeadlineAlertSource> sourceFactory)
    {
        if (signedInUser is null || closingSession || expired || IsDisposed || !IsHandleCreated || InvokeRequired)
            throw new InvalidOperationException("현재 로그인 화면에서 연결해 주세요.");
        if (deadlineNotifications is not null)
            throw new InvalidOperationException("이 로그인에는 마감 알림이 이미 연결되었습니다.");
        ArgumentNullException.ThrowIfNull(sourceFactory);
        var session = AppServices.Api.CaptureSession();
        if (session.User.Id != signedInUser.Id) throw new OperationCanceledException();
        var source = sourceFactory(session);
        session.ThrowIfChanged();
        deadlineNotifications = new(this, session, source, CaptureDeadlineTaskNavigator);
        components!.Add(deadlineNotifications);
        await deadlineNotifications.StartAsync();
    }

    private Func<string, Task<bool>> CaptureDeadlineTaskNavigator()
    {
        if (closingSession || expired || IsDisposed || InvokeRequired || signedInUser is null)
            throw new OperationCanceledException();
        if (currentPage is not TasksControl tasks || tasks.IsDisposed)
        {
            tasks = new TasksControl(previewRole, new HttpTaskQueryService(AppServices.Api));
            ShowPage(tasks, btnTasks);
        }
        tasks.CreateControl();
        return tasks.CaptureOwnTaskNavigator();
    }
}
