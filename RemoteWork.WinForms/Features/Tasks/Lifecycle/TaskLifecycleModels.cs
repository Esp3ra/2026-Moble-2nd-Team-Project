using System.Globalization;
using System.Text;
using RemoteWork.WinForms.Features.Tasks.Queries;

namespace RemoteWork.WinForms.Features.Tasks.Lifecycle;

public enum TaskLifecycleKind { Cancel, RequestExtension, ApproveExtension, RejectExtension, WithdrawExtension }
public enum TaskExtensionStatus { Pending, Approved, Rejected, Withdrawn, Cancelled }
public sealed record TaskCancellation(int CancelledBy, DateTimeOffset CancelledAt, string Reason);
public sealed record TaskExtensionRequest(string ExtensionId, string TaskId, int AssignmentRevision,
    int RequestedBy, DateOnly OriginalDueDate, DateOnly RequestedDueDate, string Reason,
    TaskExtensionStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt,
    int? DecidedBy, string? DecisionReason);
public sealed record TaskLifecycleContext(string TaskId, string RowVersion, int AssignmentRevision,
    TaskDisplayStatus Status, DateOnly StartDate, DateOnly DueDate, DateTimeOffset UpdatedAt,
    int AssigneeId, IReadOnlyList<int> ManagerUserIds, bool IsCancelled,
    TaskCancellation? Cancellation, TaskExtensionRequest? PendingExtension, bool WriteEnabled);

// A copied read snapshot and immutable input; final permission and date checks are server-side.
public sealed class TaskLifecycleRequest
{
    private TaskLifecycleRequest(TaskLifecycleKind kind, TaskLifecycleContext context, int userId,
        string reason, DateOnly? requestedDate, TaskExtensionRequest? extension)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Enum.IsDefined(kind) || !TaskLifecycleFormat.IsId(context.TaskId)
            || !TaskLifecycleFormat.IsId(context.RowVersion) || context.AssignmentRevision <= 0
            || userId <= 0 || context.IsCancelled || context.UpdatedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("현재 업무·배정·취소 여부를 다시 확인해 주세요.");
        if (!context.WriteEnabled) throw new TaskLifecycleException(TaskLifecycleFailure.TechnicalConnectionRequired);
        bool open = context.Status is TaskDisplayStatus.InProgress or TaskDisplayStatus.Returned;
        bool manager = context.ManagerUserIds.Contains(userId);
        if (kind == TaskLifecycleKind.Cancel)
        {
            if (!manager || context.Status is not (TaskDisplayStatus.NotStarted or TaskDisplayStatus.InProgress or TaskDisplayStatus.Returned)
                || string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("지정 관리자와 취소 가능한 상태·사유를 확인해 주세요.");
        }
        else if (kind == TaskLifecycleKind.RequestExtension)
        {
            if (!open || context.AssigneeId != userId || context.PendingExtension is not null || !context.ManagerUserIds.Any(id => id != userId)
                || requestedDate is null || requestedDate <= context.DueDate || string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("현재 담당자·진행 상태·중복 신청·새 마감일·사유를 확인해 주세요.");
        }
        else
        {
            if (!open || extension is null || !TaskLifecycleFormat.IsId(extension.ExtensionId) || extension.Status != TaskExtensionStatus.Pending
                || extension.TaskId != context.TaskId || extension.AssignmentRevision != context.AssignmentRevision
                || context.PendingExtension?.ExtensionId != extension.ExtensionId)
                throw new ArgumentException("현재 배정의 대기 중 연장 신청을 다시 확인해 주세요.");
            if (kind == TaskLifecycleKind.WithdrawExtension ? extension.RequestedBy != userId || context.AssigneeId != userId
                : !manager || extension.RequestedBy == userId)
                throw new ArgumentException("본인 신청 철회 또는 자기 신청이 아닌 지정 관리자 판단만 가능합니다.");
            if (kind == TaskLifecycleKind.ApproveExtension && extension.RequestedDueDate <= context.DueDate)
                throw new ArgumentException("요청 마감일은 현재 마감일보다 뒤여야 합니다.");
            requestedDate = extension.RequestedDueDate;
        }
        Kind = kind; TaskId = context.TaskId; ExpectedRowVersion = context.RowVersion;
        ExpectedAssignmentRevision = context.AssignmentRevision; ExpectedStatus = context.Status;
        CheckedUserId = userId; Reason = reason; RequestedDueDate = requestedDate;
        ExtensionId = kind is TaskLifecycleKind.Cancel or TaskLifecycleKind.RequestExtension ? null : extension?.ExtensionId;
        OriginalDueDate = context.DueDate;
    }
    public static TaskLifecycleRequest Create(TaskLifecycleKind kind, TaskLifecycleContext context,
        int userId, string reason = "", DateOnly? requestedDueDate = null,
        TaskExtensionRequest? extension = null, DateOnly? koreaToday = null)
    {
        ArgumentNullException.ThrowIfNull(reason);
        DateOnly today = koreaToday ?? DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).DateTime);
        DateOnly? date = kind == TaskLifecycleKind.RequestExtension ? requestedDueDate : extension?.RequestedDueDate;
        if (kind is TaskLifecycleKind.RequestExtension or TaskLifecycleKind.ApproveExtension && (date is null || date < today))
            throw new ArgumentException("새 마감일은 한국 시간 오늘 또는 그 이후여야 합니다.");
        try
        { if (new UTF8Encoding(false, true).GetByteCount(reason) > 60_000) throw new ArgumentException("사유는 UTF-8 기준 60,000바이트 이내로 입력해 주세요."); }
        catch (EncoderFallbackException) { throw new ArgumentException("사유의 문자 형식을 확인해 주세요."); }
        return new(kind, context, userId, reason, requestedDueDate, extension);
    }
    public TaskLifecycleKind Kind { get; }
    public string TaskId { get; }
    public string ExpectedRowVersion { get; }
    public int ExpectedAssignmentRevision { get; }
    public TaskDisplayStatus ExpectedStatus { get; }
    public string Reason { get; }
    public DateOnly? RequestedDueDate { get; }
    public DateOnly OriginalDueDate { get; }
    public string? ExtensionId { get; }
    internal int CheckedUserId { get; }
}
public sealed record TaskLifecycleResult(string TaskId, string RowVersion, int AssignmentRevision,
    TaskDisplayStatus Status, DateTimeOffset UpdatedAt, string? ExtensionId, DateOnly? RequestedDueDate, DateOnly DueDate);
public sealed record TaskLifecycleKey(TaskLifecycleKind Kind, string Target, string Value, DateTimeOffset ExpiresAt);
public sealed record TaskLifecycleLookup(bool Recorded, TaskLifecycleResult? Result = null);
public enum TaskLifecycleFailure
{
    NotConfigured, TechnicalConnectionRequired, AuthenticationExpired, Forbidden, NotFound, InvalidInput,
    AssignmentChanged, VersionConflict, StateConflict, Cancelled, PendingExtension, KeyReused, KeyExpired,
    Conflict, Unavailable, InvalidResponse
}
public sealed class TaskLifecycleException(TaskLifecycleFailure failure, Exception? inner = null) : Exception(MessageFor(failure), inner)
{
    public TaskLifecycleFailure Failure { get; } = failure;
    public static string MessageFor(TaskLifecycleFailure failure) => failure switch
    {
        TaskLifecycleFailure.TechnicalConnectionRequired or TaskLifecycleFailure.NotConfigured => "기술 연결 필요: 공통 요청 등록·DB 및 제출 차단 연결 확인 전에는 저장하지 않습니다.",
        TaskLifecycleFailure.AuthenticationExpired => "접속이 변경되었습니다. 현재 로그인에서 다시 조회해 주세요.",
        TaskLifecycleFailure.Forbidden => "현재 권한이 없습니다. 불확실한 이전 요청은 지정 관리자가 처리 내역을 확인해야 합니다.",
        TaskLifecycleFailure.NotFound => "업무 또는 연장 신청을 찾을 수 없습니다.",
        TaskLifecycleFailure.InvalidInput => "입력·날짜·대상 신청을 다시 확인해 주세요.",
        TaskLifecycleFailure.AssignmentChanged or TaskLifecycleFailure.VersionConflict => "업무 또는 배정이 변경됐습니다. 최신 정보를 조회해 주세요.",
        TaskLifecycleFailure.StateConflict or TaskLifecycleFailure.Cancelled => "현재 상태 또는 취소된 업무에서는 처리할 수 없습니다.",
        TaskLifecycleFailure.PendingExtension => "현재 배정에 대기 중인 연장 신청이 있습니다.",
        TaskLifecycleFailure.KeyExpired => "요청 키·결과 보관 기간이 지났습니다. 처리 내역 확인이 필요합니다.",
        TaskLifecycleFailure.KeyReused or TaskLifecycleFailure.Conflict => "요청 정보가 일치하지 않습니다. 처리 내역 확인이 필요합니다.",
        _ => "처리 결과를 확인할 수 없습니다. 같은 요청의 결과를 먼저 확인해 주세요."
    };
}
public interface ITaskLifecycleService
{
    bool IsCurrent { get; }
    int UserId { get; }
    bool IsAdministrator { get; }
    CancellationToken SessionCancellation { get; }
    Task<TaskLifecycleContext> GetContextAsync(string taskId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskExtensionRequest>> GetExtensionsAsync(TaskQueryScope scope, TaskExtensionStatus? status = null, CancellationToken ct = default);
    Task<TaskLifecycleKey> GetRequestKeyAsync(TaskLifecycleRequest request, CancellationToken ct = default);
    Task<TaskLifecycleResult> ChangeAsync(TaskLifecycleRequest request, TaskLifecycleKey key, CancellationToken ct = default);
    Task<TaskLifecycleLookup> GetResultAsync(TaskLifecycleRequest request, TaskLifecycleKey key, CancellationToken ct = default);
}
public interface ITaskLifecycleSource
{
    ITaskLifecycleService? CreateLifecycleService();
    TaskLifecycleWorkflow? GetLifecycleWorkflow(TaskLifecycleKind kind, string taskId, string? extensionId = null);
    TaskLifecycleWorkflow? GetPendingLifecycleWorkflow();
}
internal static class TaskLifecycleFormat
{
    internal static bool IsId(string? text) => text is { Length: > 0 and <= 19 } && text[0] != '0'
        && text.All(char.IsAsciiDigit) && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0;
    internal static string Action(TaskLifecycleKind kind) => kind switch
    {
        TaskLifecycleKind.Cancel => "task.cancelled", TaskLifecycleKind.RequestExtension => "task.deadline_extension_requested",
        TaskLifecycleKind.ApproveExtension => "task.deadline_extension_approved", TaskLifecycleKind.RejectExtension => "task.deadline_extension_rejected",
        TaskLifecycleKind.WithdrawExtension => "task.deadline_extension_withdrawn", _ => throw new ArgumentException("처리 구분을 확인해 주세요.")
    };
    internal static bool MatchesResult(TaskLifecycleRequest request, TaskLifecycleResult? result) => result is not null
        && result.TaskId == request.TaskId && IsId(result.RowVersion)
        && long.Parse(request.ExpectedRowVersion, CultureInfo.InvariantCulture) < long.MaxValue
        && long.Parse(result.RowVersion, CultureInfo.InvariantCulture) == long.Parse(request.ExpectedRowVersion, CultureInfo.InvariantCulture) + 1
        && result.AssignmentRevision == request.ExpectedAssignmentRevision && result.Status == request.ExpectedStatus
        && result.UpdatedAt.Offset == TimeSpan.Zero
        && (request.Kind == TaskLifecycleKind.Cancel ? result.ExtensionId is null && result.RequestedDueDate is null
            : IsId(result.ExtensionId) && (request.ExtensionId is null || result.ExtensionId == request.ExtensionId)
                && result.RequestedDueDate == request.RequestedDueDate)
        && result.DueDate == (request.Kind == TaskLifecycleKind.ApproveExtension ? request.RequestedDueDate : request.OriginalDueDate);
}
