using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Queries;

namespace RemoteWork.WinForms.Features.Tasks.Preparation;

public enum TaskStartKind
{
    Accept,
    Rework
}

public enum TaskStartTargetOutcome
{
    Matched,
    NotCurrentAssignee,
    SourceStatusChanged,
    WrongState
}

// A read snapshot for the preparation screen, not a request DTO or permission to save.
public sealed class TaskStartTarget
{
    internal TaskStartTarget(TaskStartKind kind, string taskId, int userId, int assigneeId,
        string rowVersion, int assignmentRevision, TaskDisplayStatus status, DateTimeOffset updatedAt)
    {
        Kind = kind;
        TaskId = taskId;
        UserId = userId;
        AssigneeId = assigneeId;
        RowVersion = rowVersion;
        AssignmentRevision = assignmentRevision;
        Status = status;
        UpdatedAt = updatedAt;
    }

    public TaskStartKind Kind { get; }
    public string TaskId { get; }
    public int UserId { get; }
    public int AssigneeId { get; }
    public string RowVersion { get; }
    public int AssignmentRevision { get; }
    public TaskDisplayStatus Status { get; }
    public DateTimeOffset UpdatedAt { get; }
}

public sealed record TaskStartTargetCheck(TaskStartTargetOutcome Outcome, TaskStartTarget? Target);

public static class TaskStartTargetPreparation
{
    // Reads only. This does not decide deadline policy, grant server permission,
    // issue a request key, or change any task/assignment/submission state.
    public static TaskStartTargetCheck Check(TaskStartKind kind, TaskDetail source,
        TaskWriteContext context, int userId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(context);
        if (source.Summary is null) throw new ArgumentException("조회한 업무 요약이 필요합니다.", nameof(source));
        if (!Enum.IsDefined(kind)) throw new ArgumentException("수락 또는 재작업 시작 구분이 필요합니다.", nameof(kind));
        if (!Enum.IsDefined(source.Summary.Status)) throw new ArgumentException("조회한 업무 상태를 확인해 주세요.", nameof(source));
        if (!Enum.IsDefined(context.Status)) throw new ArgumentException("현재 업무 상태를 확인해 주세요.", nameof(context));
        if (userId <= 0) throw new ArgumentException("현재 로그인 사용자 ID가 필요합니다.", nameof(userId));
        if (context.AssigneeId <= 0 || context.AssignmentRevision <= 0)
            throw new ArgumentException("현재 담당자와 배정 번호를 확인해 주세요.", nameof(context));
        if (!IsPositiveBigInt(source.Summary.TaskId))
            throw new ArgumentException("조회한 업무 ID는 양의 정규 BIGINT 문자열이어야 합니다.", nameof(source));
        if (!IsPositiveBigInt(context.TaskId) || !IsPositiveBigInt(context.RowVersion))
            throw new ArgumentException("현재 업무 ID와 버전은 양의 정규 BIGINT 문자열이어야 합니다.", nameof(context));
        if (source.Summary.TaskId != context.TaskId)
            throw new ArgumentException("조회한 업무와 현재 확인 대상이 다릅니다.", nameof(context));
        if (context.UpdatedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("현재 업무 변경 시각은 UTC여야 합니다.", nameof(context));

        if (context.AssigneeId != userId)
            return new TaskStartTargetCheck(TaskStartTargetOutcome.NotCurrentAssignee, null);
        if (source.Summary.Status != context.Status)
            return new TaskStartTargetCheck(TaskStartTargetOutcome.SourceStatusChanged, null);
        TaskDisplayStatus expectedStatus = kind == TaskStartKind.Accept
            ? TaskDisplayStatus.NotStarted : TaskDisplayStatus.Returned;
        if (context.Status != expectedStatus)
            return new TaskStartTargetCheck(TaskStartTargetOutcome.WrongState, null);

        var target = new TaskStartTarget(kind, context.TaskId, userId, context.AssigneeId,
            context.RowVersion, context.AssignmentRevision, context.Status, context.UpdatedAt);
        return new TaskStartTargetCheck(TaskStartTargetOutcome.Matched, target);
    }

    private static bool IsPositiveBigInt(string? value)
    {
        if (value is null || value.Length is < 1 or > 19 || value[0] == '0') return false;
        foreach (char digit in value) if (!char.IsAsciiDigit(digit)) return false;
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long number) && number > 0;
    }
}
