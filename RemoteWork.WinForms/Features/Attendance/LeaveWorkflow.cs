using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Attendance;

// Internal UI seam, not an agreed HTTP DTO, database schema or leave policy.
public sealed record LeaveDraft(string Type, DateOnly StartDate, DateOnly EndDate, string Reason)
{
    // User-confirmed policy: sick/official leave does not consume annual leave.
    // The server must calculate chargeable days, holidays and balances atomically.
    public bool DeductsAnnualBalance => Type is "연차" or "반차(오전)" or "반차(오후)";
}
public sealed record LeaveRequestView(string Id, string Revision, int ApplicantId, string ApplicantName,
    string Department, LeaveDraft Draft, string Status, bool CanReview = false, string? DecisionReason = null,
    bool CanReadAssigned = false);
public sealed record LeaveSubmissionReceipt(Guid RequestId, LeaveRequestView Request);
public sealed record LeaveDecisionReceipt(Guid RequestId, LeaveRequestView Request);

/// <summary>Must be bound to the same captured administrator/employee session.
/// List must return only the current user's own requests or the administrator's assigned applicants.
/// CanReview comes from the server for this session, never from a user-editable field.
/// CanReadAssigned records a server-authorized assigned-list response, including completed history;
/// it never authorizes a decision. Pending CanReview rows remain readable for existing adapters.
/// The server must recheck assignment on every decision and enforce atomic/idempotent writes.
/// This UI seam does not define the HTTP contract or local persistence.
/// </summary>
public interface ILeaveWorkflow
{
    bool IsConnected { get; }
    Task<LeaveSubmissionReceipt> SubmitAsync(LeaveDraft draft, Guid requestId, CancellationToken cancellation);
    Task<IReadOnlyList<LeaveRequestView>> ListAsync(CancellationToken cancellation);
    Task<LeaveDecisionReceipt> DecideAsync(LeaveRequestView request, string decision, string reason,
        Guid requestId, CancellationToken cancellation);
}

public sealed class UnconnectedLeaveWorkflow : ILeaveWorkflow
{
    public bool IsConnected => false;
    private static InvalidOperationException Missing() => new("연차 서버가 아직 연결되지 않았습니다. 신청·승인 내역은 저장되지 않았습니다.");
    public Task<LeaveSubmissionReceipt> SubmitAsync(LeaveDraft draft, Guid requestId, CancellationToken cancellation)
        => Task.FromException<LeaveSubmissionReceipt>(Missing());
    public Task<IReadOnlyList<LeaveRequestView>> ListAsync(CancellationToken cancellation)
        => Task.FromException<IReadOnlyList<LeaveRequestView>>(Missing());
    public Task<LeaveDecisionReceipt> DecideAsync(LeaveRequestView request, string decision, string reason,
        Guid requestId, CancellationToken cancellation)
        => Task.FromException<LeaveDecisionReceipt>(Missing());
}

internal static class LeaveInput
{
    // All five types are requested; sick and official leave do not reduce annual balance.
    public static readonly string[] Types = ["연차", "반차(오전)", "반차(오후)", "병가", "공가"];
    public const int TextLimit = 1000; // UI transport limit proposal; not a server policy.
    public static string? Error(LeaveDraft draft)
    {
        if (!Types.Contains(draft.Type, StringComparer.Ordinal)) return "신청 유형을 선택해 주세요.";
        if (draft.EndDate < draft.StartDate) return "종료일은 시작일보다 빠를 수 없습니다.";
        if (draft.Reason.Length > TextLimit) return "사유는 1,000자 이내로 입력해 주세요.";
        return null;
    }

    public static ApiClient.Session? Current()
    {
        try { return AppServices.Api.CaptureSession(); }
        catch (InvalidOperationException) { return null; }
    }

    public static bool Live(ApiClient.Session? session) => session?.IsCurrent == true && session.User.IsActive;
    public static bool Administrator(ApiClient.Session? session) => Live(session) && session!.User.Role == "admin";
    public static bool CanRead(ApiClient.Session? session, LeaveRequestView? request)
        => Live(session) && request is not null && (request.ApplicantId == session!.User.Id
            || Administrator(session) && (request.CanReadAssigned || request is { CanReview: true, Status: "pending" }));
    public static bool CanReview(ApiClient.Session? session, LeaveRequestView? request)
        => Administrator(session) && request is { CanReview: true, Status: "pending" }
            && request.ApplicantId != session!.User.Id;
    public static string Applicant(ApiClient.Session? session)
        => Live(session) ? $"{session!.User.DisplayName} · {session.User.Department}" : "로그인이 필요합니다.";
    public static bool Same(LeaveDraft left, LeaveDraft right) => left == right;
}
