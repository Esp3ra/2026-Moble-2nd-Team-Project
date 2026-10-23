using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

namespace RemoteWork.ConnectedWorkspace;

public sealed record WorkspaceConnectionReport(string[] Notices);

public static class WorkspaceConnector
{
    public static async Task<WorkspaceConnectionReport> ConnectAsync(MainForm main, WorkspaceProfile profile,
        Func<ApiClient.Session, LoginLeaveConnection>? leaveFactory = null,
        Func<MainForm, WorkdayActivityOptions, Task>? workdayConnect = null)
    {
        if (AppServices.Api.ServerUri != profile.Server) throw new InvalidOperationException("설정한 서버가 현재 로그인 서버와 다릅니다.");
        var session = AppServices.Api.CaptureSession();
        var notices = new List<string>();
        async Task Attach(string name, Func<Task> attach)
        {
            try { session.ThrowIfChanged(); await attach(); session.ThrowIfChanged(); }
            catch (OperationCanceledException) when (!session.IsCurrent || main.IsDisposed) { }
            catch (Exception ex) when (ex is ApiException or HttpRequestException or InvalidDataException or InvalidOperationException or OperationCanceledException)
            { if (session.IsCurrent && !main.IsDisposed) notices.Add(name + " 연결 준비를 확인해 주세요. 다른 기능의 연결은 계속합니다."); }
        }
        Task leave = Task.CompletedTask, workday = Task.CompletedTask;
        if (profile.LeaveEnabled)
        {
            var factory = leaveFactory ?? CommonLeaveFactory();
            if (factory is null) notices.Add("연차 연결은 아직 준비되지 않았습니다. 지문·근무 연결은 별도로 진행합니다.");
            else leave = Attach("연차", () => main.ConnectLeaveWorkflowAsync(factory));
        }
        if (profile.Workday is { } options)
            workday = Attach("지문·현재 근무", () => workdayConnect is null
                ? main.ConnectWorkdayActivityAsync(options) : workdayConnect(main, options));
        await Task.WhenAll(leave, workday);
        return new(notices.ToArray());
    }

    private static Func<ApiClient.Session, LoginLeaveConnection>? CommonLeaveFactory()
    {
#if COMMON_LEAVE
        return session =>
        {
            var adapter = new HttpLeaveWorkflow(session);
            return new(session, adapter, async ct =>
            {
                var r = await adapter.ReadinessAsync(ct); return new(r.SchemaReady, r.WriteReady, r.Pending, r.Year, r.ServerTime);
            }, async (year, ct) =>
            {
                var b = await adapter.BalanceAsync(year, ct); return new(b.Year, b.GrantedUnits, b.ReservedUnits, b.UsedUnits, b.RemainingUnits);
            });
        };
#else
        return null;
#endif
    }
}
