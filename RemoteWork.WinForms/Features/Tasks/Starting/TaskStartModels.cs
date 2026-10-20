using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;

namespace RemoteWork.WinForms.Features.Tasks.Starting;

// The immutable request can only be made after the read detail/context check.
// It is still a concurrency snapshot; the server decides current permission/date/state.
public sealed class TaskStartRequest
{
    private TaskStartRequest(TaskStartTarget target)
    {
        Kind = target.Kind;
        TaskId = target.TaskId;
        ExpectedRowVersion = target.RowVersion;
        ExpectedAssignmentRevision = target.AssignmentRevision;
        CheckedUserId = target.UserId;
    }

    public static TaskStartRequest FromCheckedTarget(TaskStartTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!Enum.IsDefined(target.Kind) || !TaskStartFormat.IsPositiveBigInt(target.TaskId)
            || !TaskStartFormat.IsPositiveBigInt(target.RowVersion) || target.AssignmentRevision <= 0
            || target.UserId <= 0 || target.AssigneeId != target.UserId
            || target.Status != (target.Kind == TaskStartKind.Accept ? TaskDisplayStatus.NotStarted : TaskDisplayStatus.Returned)
            || target.UpdatedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("현재 본인 업무를 다시 확인해 주세요.", nameof(target));
        return new TaskStartRequest(target);
    }

    public TaskStartKind Kind { get; }
    public string TaskId { get; }
    public string ExpectedRowVersion { get; }
    public int ExpectedAssignmentRevision { get; }
    internal int CheckedUserId { get; }
}

public sealed class TaskStartResult(string taskId, string rowVersion, int assignmentRevision,
    TaskDisplayStatus status, DateTimeOffset updatedAt)
{
    public string TaskId { get; } = taskId;
    public string RowVersion { get; } = rowVersion;
    public int AssignmentRevision { get; } = assignmentRevision;
    public TaskDisplayStatus Status { get; } = status;
    public DateTimeOffset UpdatedAt { get; } = updatedAt;
}

// Do not use a record: generated ToString must not disclose the private request key.
public sealed class TaskStartKey(TaskStartKind kind, string target, string value, DateTimeOffset expiresAt)
{
    public TaskStartKind Kind { get; } = kind;
    public string Target { get; } = target;
    public string Value { get; } = value;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public override string ToString() => "업무 시작 요청 키 (메모리 보관)";
}

public sealed record TaskStartLookup(bool Recorded, TaskStartResult? Result = null);

public enum TaskStartFailure
{
    NotConfigured, AuthenticationExpired, Forbidden, NotFound, InvalidInput,
    AssignmentChanged, VersionConflict, StateConflict, DataInconsistent, VersionExhausted,
    BeforeStartDate, AfterDueDate, KeyReused, KeyExpired, Conflict, Unavailable, InvalidResponse
}

public sealed class TaskStartException(TaskStartFailure failure, Exception? inner = null)
    : Exception(MessageFor(failure), inner)
{
    public TaskStartFailure Failure { get; } = failure;
    public static string MessageFor(TaskStartFailure failure) => failure switch
    {
        TaskStartFailure.NotConfigured => "업무 수락·재작업 시작 연결이 준비되지 않았습니다.",
        TaskStartFailure.AuthenticationExpired => "로그인이 만료되었습니다. 다시 로그인해 주세요.",
        TaskStartFailure.Forbidden => "현재 로그인에는 이 업무를 시작할 권한이 없습니다. 다시 로그인해 주세요.",
        TaskStartFailure.NotFound => "업무를 찾을 수 없습니다. 목록을 다시 조회해 주세요.",
        TaskStartFailure.InvalidInput => "현재 본인 업무와 요청 내용을 다시 확인해 주세요.",
        TaskStartFailure.AssignmentChanged => "담당 배정이 변경되었습니다. 업무 정보를 다시 조회해 주세요.",
        TaskStartFailure.VersionConflict => "업무가 변경되었습니다. 업무 정보를 다시 조회해 주세요.",
        TaskStartFailure.StateConflict => "현재 상태에서는 시작할 수 없습니다. 업무 정보를 다시 조회해 주세요.",
        TaskStartFailure.DataInconsistent => "수락 기록과 업무 상태가 일치하지 않습니다. 관리자 점검이 필요합니다.",
        TaskStartFailure.VersionExhausted => "업무 버전 상한에 도달했습니다. 관리자 점검이 필요합니다.",
        TaskStartFailure.BeforeStartDate => "한국 시간 기준 시작일 전에는 시작할 수 없습니다.",
        TaskStartFailure.AfterDueDate => "한국 시간 기준 마감일이 지나 시작할 수 없습니다.",
        TaskStartFailure.KeyReused => "요청 키의 내용이 일치하지 않습니다. 새 요청을 보내지 말고 처리 내역을 확인해 주세요.",
        TaskStartFailure.KeyExpired => "요청 키 또는 결과 보관 기간이 지났습니다. 처리 내역을 확인해 주세요.",
        TaskStartFailure.Conflict => "요청과 서버 정보가 일치하지 않습니다. 처리 내역을 확인해 주세요.",
        TaskStartFailure.InvalidResponse => "서버 응답을 확인할 수 없습니다. 처리 결과를 먼저 확인해 주세요.",
        _ => "서버 연결 또는 처리 오류가 발생했습니다. 처리 결과를 먼저 확인해 주세요."
    };
}

public interface ITaskStartService
{
    bool IsCurrent { get; }
    int UserId { get; }
    CancellationToken SessionCancellation { get; }
    Task<TaskStartKey> GetRequestKeyAsync(TaskStartRequest request, CancellationToken ct = default);
    Task<TaskStartResult> StartAsync(TaskStartRequest request, TaskStartKey key, CancellationToken ct = default);
    Task<TaskStartLookup> GetResultAsync(TaskStartRequest request, TaskStartKey key, CancellationToken ct = default);
}

public interface ITaskStartSource
{
    // Borrowed from the login session; forms must unsubscribe, not dispose it.
    TaskStartWorkflow? GetStartWorkflow(TaskStartKind kind, string taskId);
    TaskStartWorkflow? GetPendingStartWorkflow();
}

internal static class TaskStartFormat
{
    internal static bool IsPositiveBigInt(string? value) => value is { Length: > 0 and <= 19 }
        && value[0] != '0' && value.All(char.IsAsciiDigit)
        && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long number) && number > 0;

    internal static bool MatchesResult(TaskStartRequest request, TaskStartResult? result)
    {
        if (result is null || result.TaskId != request.TaskId || !IsPositiveBigInt(result.RowVersion)
            || result.AssignmentRevision != request.ExpectedAssignmentRevision
            || result.Status != TaskDisplayStatus.InProgress || result.UpdatedAt.Offset != TimeSpan.Zero)
            return false;
        long expected = long.Parse(request.ExpectedRowVersion, CultureInfo.InvariantCulture);
        return expected < long.MaxValue && long.Parse(result.RowVersion, CultureInfo.InvariantCulture) == expected + 1;
    }
}
