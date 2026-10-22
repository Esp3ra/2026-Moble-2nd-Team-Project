using System.Globalization;
using System.Text;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;

namespace RemoteWork.WinForms.Features.Tasks.Management;

public enum TaskManagementKind { Update, Reassign, ChangeManagers }

// Built only from a separately checked target and copied local input. It remains
// a concurrency snapshot: the server decides current permission and state.
public sealed class TaskManagementRequest
{
    private TaskManagementRequest(TaskManagementKind kind, TaskDetail detail, TaskWriteContext context, int userId,
        string? title = null, string? description = null, DateOnly? start = null, DateOnly? due = null,
        int? assignee = null, IReadOnlyList<int>? managers = null, IReadOnlyList<string>? attachmentIds = null)
    {
        ArgumentNullException.ThrowIfNull(detail); ArgumentNullException.ThrowIfNull(context);
        bool allowed = kind == TaskManagementKind.ChangeManagers
            ? context.Status is TaskDisplayStatus.NotStarted or TaskDisplayStatus.InProgress or TaskDisplayStatus.Returned
            : context.Status == TaskDisplayStatus.NotStarted;
        if (!allowed || userId <= 0 || !context.ManagerUserIds.Contains(userId)
            || detail.Summary.TaskId != context.TaskId || detail.Summary.Status != context.Status
            || !TaskManagementFormat.IsId(context.TaskId) || !TaskManagementFormat.IsId(context.RowVersion)
            || context.AssignmentRevision <= 0 || context.UpdatedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("현재 관리 업무의 상태·권한·버전을 다시 확인해 주세요.");
        Kind = kind; TaskId = context.TaskId; ExpectedRowVersion = context.RowVersion;
        ExpectedAssignmentRevision = context.AssignmentRevision; ExpectedStatus = context.Status;
        CheckedUserId = userId; Title = title; Description = description; StartDate = start; DueDate = due;
        AssigneeId = assignee; ManagerUserIds = managers ?? Array.Empty<int>();
        AttachmentIds = attachmentIds;
    }
    public static TaskManagementRequest Update(TaskDetail detail, TaskWriteContext context, int userId,
        string title, string description, DateOnly start, DateOnly due, IReadOnlyList<string>? attachmentIds = null)
    {
        ArgumentNullException.ThrowIfNull(description);
        if (string.IsNullOrWhiteSpace(title) || title.EnumerateRunes().Count() > 200 || start.Year < 1000
            || due.Year < 1000 || due < start) throw new ArgumentException("제목·시작일·마감일을 확인해 주세요.");
        var utf8 = new UTF8Encoding(false, true);
        try { utf8.GetBytes(title); utf8.GetBytes(description); }
        catch (EncoderFallbackException) { throw new ArgumentException("문자 형식을 확인해 주세요."); }
        IReadOnlyList<string>? copiedIds = null;
        if (attachmentIds is not null)
        {
            if (attachmentIds.Count > 10)
                throw new ArgumentException("업무 첨부파일은 최대 10개까지 선택할 수 있습니다.", nameof(attachmentIds));
            var copied = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in attachmentIds)
            {
                if (!Guid.TryParseExact(id, "D", out Guid parsed) || parsed.ToString("D") != id || !seen.Add(id))
                    throw new ArgumentException("첨부 ID는 중복되지 않는 정규 소문자 UUID여야 합니다.", nameof(attachmentIds));
                copied.Add(id);
            }
            copiedIds = copied.AsReadOnly();
        }
        return new(TaskManagementKind.Update, detail, context, userId, title, description, start, due,
            attachmentIds: copiedIds);
    }
    public static TaskManagementRequest Reassign(TaskDetail detail, TaskWriteContext context, int userId, int assigneeId)
    {
        if (assigneeId <= 0) throw new ArgumentException("현재 활성 담당자를 선택해 주세요.");
        return new(TaskManagementKind.Reassign, detail, context, userId, assignee: assigneeId);
    }
    public static TaskManagementRequest ChangeManagers(TaskDetail detail, TaskWriteContext context, int userId, IEnumerable<int> managerIds)
    {
        ArgumentNullException.ThrowIfNull(managerIds);
        int[] ids = managerIds.ToArray();
        if (ids.Length == 0 || ids.Any(id => id <= 0) || ids.Distinct().Count() != ids.Length)
            throw new ArgumentException("업무 관리자를 한 명 이상 선택해 주세요.");
        return new(TaskManagementKind.ChangeManagers, detail, context, userId,
            managers: Array.AsReadOnly(ids.Order().ToArray()));
    }
    public TaskManagementKind Kind { get; }
    public string TaskId { get; }
    public string ExpectedRowVersion { get; }
    public int ExpectedAssignmentRevision { get; }
    public TaskDisplayStatus ExpectedStatus { get; }
    public string? Title { get; }
    public string? Description { get; }
    public DateOnly? StartDate { get; }
    public DateOnly? DueDate { get; }
    public int? AssigneeId { get; }
    public IReadOnlyList<int> ManagerUserIds { get; }
    // Null leaves existing references unchanged; [] explicitly removes this
    // task's references. Preserve the frozen original order for W1 replay.
    public IReadOnlyList<string>? AttachmentIds { get; }
    internal int CheckedUserId { get; }
}

public sealed record TaskManagementResult(string TaskId, string RowVersion, int AssignmentRevision,
    TaskDisplayStatus Status, DateTimeOffset UpdatedAt);
public sealed record TaskManagementLookup(bool Recorded, TaskManagementResult? Result = null);
public enum TaskManagementFailure
{
    NotConfigured, AuthenticationExpired, Forbidden, NotFound, InvalidInput, AssignmentChanged,
    VersionConflict, StateConflict, DataInconsistent, VersionExhausted, AssignmentExhausted,
    DueDateInPast, NoChange, KeyReused, KeyExpired, Conflict, Unavailable, InvalidResponse, AttachmentUnavailable
}
public sealed class TaskManagementException(TaskManagementFailure failure, Exception? inner = null)
    : Exception(MessageFor(failure), inner)
{
    public TaskManagementFailure Failure { get; } = failure;
    public static string MessageFor(TaskManagementFailure failure) => failure switch
    {
        TaskManagementFailure.AuthenticationExpired => "접속이 변경되었습니다. 현재 로그인에서 다시 조회해 주세요.",
        TaskManagementFailure.Forbidden => "현재 업무 관리 권한을 확인할 수 없습니다. 이전 요청이 반영됐을 수도 있으므로 처리 내역을 확인해 주세요.",
        TaskManagementFailure.NotConfigured => "업무 변경 서버 기능·테이블 연결이 준비되지 않았습니다.",
        TaskManagementFailure.NotFound => "업무를 찾을 수 없습니다. 목록을 다시 조회해 주세요.",
        TaskManagementFailure.InvalidInput => "입력과 현재 대상 업무를 다시 확인해 주세요.",
        TaskManagementFailure.AssignmentChanged => "담당 배정이 변경되었습니다. 최신 업무를 다시 조회해 주세요.",
        TaskManagementFailure.VersionConflict => "업무 버전이 변경되었습니다. 최신 업무를 다시 조회해 주세요.",
        TaskManagementFailure.StateConflict => "현재 업무 상태에서는 이 변경을 할 수 없습니다.",
        TaskManagementFailure.DataInconsistent => "업무와 배정·결과 기록의 정합성 점검이 필요합니다.",
        TaskManagementFailure.VersionExhausted or TaskManagementFailure.AssignmentExhausted => "업무·배정 번호 상한에 도달했습니다. 관리자 점검이 필요합니다.",
        TaskManagementFailure.DueDateInPast => "새 마감일은 한국 시간 오늘 이후로 지정해 주세요.",
        TaskManagementFailure.NoChange => "현재 저장된 값과 같습니다. 변경할 내용을 다시 확인해 주세요.",
        TaskManagementFailure.AttachmentUnavailable => "선택한 첨부파일이 없거나 전송 완료 상태가 아닙니다. 파일 목록을 다시 확인해 주세요. 기존 선택은 유지했습니다.",
        TaskManagementFailure.KeyExpired => "요청 키·결과 보관 기간이 지났습니다. 처리 내역을 확인해 주세요.",
        TaskManagementFailure.KeyReused or TaskManagementFailure.Conflict => "요청 정보가 일치하지 않습니다. 새 요청을 보내기 전에 처리 내역을 확인해 주세요.",
        _ => "업무 변경 결과를 확인할 수 없습니다. 같은 요청의 처리 결과를 먼저 확인해 주세요."
    };
}
public interface ITaskManagementService
{
    bool IsCurrent { get; }
    int UserId { get; }
    CancellationToken SessionCancellation { get; }
    Task<TaskWriteRequestKey> GetRequestKeyAsync(TaskManagementRequest request, CancellationToken ct = default);
    Task<TaskManagementResult> ChangeAsync(TaskManagementRequest request, TaskWriteRequestKey key, CancellationToken ct = default);
    Task<TaskManagementLookup> GetResultAsync(TaskManagementRequest request, TaskWriteRequestKey key, CancellationToken ct = default);
}
public interface ITaskManagementSource
{
    TaskManagementWorkflow? GetManagementWorkflow(TaskManagementKind kind, string taskId);
    TaskManagementWorkflow? GetPendingManagementWorkflow();
}
internal static class TaskManagementFormat
{
    internal static bool IsId(string? value) => value is { Length: > 0 and <= 19 } && value[0] != '0'
        && value.All(char.IsAsciiDigit) && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long number) && number > 0;
    internal static TaskWriteAction Action(TaskManagementKind kind) => kind switch
    {
        TaskManagementKind.Update => TaskWriteAction.Update, TaskManagementKind.Reassign => TaskWriteAction.Reassign,
        TaskManagementKind.ChangeManagers => TaskWriteAction.ChangeManagers,
        _ => throw new ArgumentException("업무 변경 구분을 확인해 주세요.")
    };
    internal static bool MatchesResult(TaskManagementRequest request, TaskManagementResult? result)
    {
        long expected = long.Parse(request.ExpectedRowVersion, CultureInfo.InvariantCulture);
        long revision = request.ExpectedAssignmentRevision + (request.Kind == TaskManagementKind.Reassign ? 1L : 0L);
        return result is not null && result.TaskId == request.TaskId && IsId(result.RowVersion)
            && expected < long.MaxValue && long.Parse(result.RowVersion, CultureInfo.InvariantCulture) == expected + 1
            && revision <= int.MaxValue && result.AssignmentRevision == revision && result.Status == request.ExpectedStatus
            && result.UpdatedAt.Offset == TimeSpan.Zero;
    }
}
