using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Shell;

public partial class MainForm
{
    private LoginLeaveConnection? leaveConnection;

    /// <summary>Explicit composition with member 1's reviewed adapter. No server policy,
    /// account assignment or default startup is enabled by this method.</summary>
    public Task ConnectLeaveWorkflowAsync(Func<ApiClient.Session, LoginLeaveConnection> factory)
    {
        if (signedInUser is null || closingSession || expired || IsDisposed || !IsHandleCreated || InvokeRequired)
            throw new InvalidOperationException("현재 로그인 화면에서 연차를 연결해 주세요.");
        if (leaveConnection is not null) throw new InvalidOperationException("이 로그인에는 이미 연차가 연결됐습니다.");
        ArgumentNullException.ThrowIfNull(factory);
        var session = AppServices.Api.CaptureSession();
        if (session.User.Id != signedInUser.Id) throw new OperationCanceledException();
        var connection = factory(session) ?? throw new InvalidOperationException("연차 연결을 확인해 주세요.");
        try
        {
            session.ThrowIfChanged();
            if (!ReferenceEquals(connection.Session, session) || !connection.IsCurrent)
                throw new InvalidOperationException("같은 로그인으로 연차를 연결해 주세요.");
            leaveConnection = connection;
            components!.Add(connection);
        }
        catch { connection.Dispose(); throw; }
        return Task.CompletedTask;
    }

    private AttendanceControl CreateAttendancePage()
    {
        var connection = leaveConnection;
        if (connection is not null && !connection.IsCurrent) throw new OperationCanceledException();
        var session = connection?.Session ?? workdayActivity?.Session;
        var monitor = workdayActivity is { IsCurrent: true } ? workdayActivity.Monitor : null;
        var page = session is null ? new AttendanceControl(previewRole)
            : monitor is null ? new AttendanceControl(previewRole, session, connection?.Workflow)
            : new AttendanceControl(previewRole, session, connection?.Workflow, monitor);
        if (connection is not null) page.BindLeaveConnection(connection);
        return page;
    }
}
