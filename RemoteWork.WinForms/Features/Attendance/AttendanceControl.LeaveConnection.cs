using System.Globalization;

namespace RemoteWork.WinForms.Features.Attendance;

public partial class AttendanceControl
{
    private LoginLeaveConnection? leaveConnection;
    private bool leaveSubmissionReady;

    public void BindLeaveConnection(LoginLeaveConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (InvokeRequired || IsDisposed || leaveEnded || leaveLoading || leaveConnection is not null
            || !ReferenceEquals(connection.Session, leaveSession)
            || !ReferenceEquals(connection.Workflow, leaveWorkflow) || !connection.IsCurrent)
            throw new InvalidOperationException("같은 로그인과 연차 연결로 화면을 열어 주세요.");
        leaveConnection = connection;
        lblLeavePreparation.Text = "연차 조회를 눌러 설정과 잔여일을 확인해 주세요.";
        UpdateLeaveApplicationButton();
    }

    private void UpdateLeaveApplicationButton()
    {
        // Preview input checking remains available without a real connection.
        // A manager's own missing entitlement/assignment does not gate assigned-row review.
        if (leaveConnection is not null)
            btnLeaveApplicationPreview.Enabled = LeaveLive && !leaveLoading && leaveConnection.IsCurrent && leaveWorkflow.IsConnected && leaveSubmissionReady;
    }

    private async Task RefreshLeaveScreenAsync(CancellationToken ct)
    {
        if (leaveConnection is null) return;
        leaveSubmissionReady = false;
        lblBalance.Text = "잔여 연차 확인 중";
        lblLeavePreparation.Text = "연차 설정 확인 중…";
        UpdateLeaveApplicationButton();
        try
        {
            var state = await leaveConnection.ReadScreenAsync(ct);
            if (!LeaveLive || ct.IsCancellationRequested || !leaveConnection.IsCurrent) return;
            leaveSubmissionReady = state.Preparation is { SchemaReady: true, WriteReady: true };
            lblLeavePreparation.Text = state.Message;
            static string Days(int units) => (units / 2m).ToString("0.#", CultureInfo.CurrentCulture);
            lblBalance.Text = state.Balance is { } balance
                ? $"{balance.Year}년 잔여 {Days(balance.RemainingUnits)}일 · 검토 대기 {Days(balance.ReservedUnits)}일 · 사용 {Days(balance.UsedUnits)}일"
                : "잔여 연차 확인 필요";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested || !LeaveLive || !leaveConnection.IsCurrent) { }
        finally { if (LeaveLive) UpdateLeaveApplicationButton(); }
    }
}
