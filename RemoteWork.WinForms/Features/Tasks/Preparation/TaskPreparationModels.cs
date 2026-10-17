using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks.Queries;

namespace RemoteWork.WinForms.Features.Tasks.Preparation;

public enum TaskCandidateKind { Assignee, Manager }
public sealed record TaskCandidateQuery(TaskCandidateKind Kind, string? TaskId = null,
    string Search = "", int Page = 1, int Limit = 20);
public sealed record TaskUserCandidate(int Id, string DisplayName, string SelectionLabel);
public sealed record TaskCandidatePage(IReadOnlyList<TaskUserCandidate> Items, int Page, int Limit, int Total);
public sealed record TaskWriteContext(string TaskId, int AssigneeId, IReadOnlyList<int> ManagerUserIds,
    string RowVersion, int AssignmentRevision, TaskDisplayStatus Status, DateTimeOffset UpdatedAt);

// These selections prepare a future action. They are not a server mutation DTO or permission.
public sealed record TaskPreparationSelection(TaskWriteContext? Context, TaskUserCandidate? Assignee,
    IReadOnlyList<int> ManagerUserIds);
public enum TaskWriteAction { Create, Update, Reassign, ChangeManagers, Accept, RequestDeadlineExtension }

// Deliberately not a record: its generated ToString must not disclose a request key.
public sealed class TaskWriteRequestKey(TaskWriteAction action, string target, string value, DateTimeOffset expiresAt)
{
    public TaskWriteAction Action { get; } = action;
    public string Target { get; } = target;
    public string Value { get; } = value;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public override string ToString() => "쓰기 요청 키 (메모리 보관)";
}
public sealed record TaskWriteResultLookup(bool Recorded, int? HttpStatus = null,
    JsonElement? Result = null, DateTimeOffset? ExpiresAt = null);

public enum TaskPreparationFailure
{
    NotConfigured, AuthenticationExpired, Forbidden, NotFound, InvalidInput,
    AssignmentChanged, VersionConflict, KeyReused, KeyExpired, Unavailable
}
public sealed class TaskPreparationException(TaskPreparationFailure failure, Exception? inner = null)
    : Exception(MessageFor(failure), inner)
{
    public TaskPreparationFailure Failure { get; } = failure;
    public static string MessageFor(TaskPreparationFailure failure) => failure switch
    {
        TaskPreparationFailure.NotConfigured => "업무 준비 정보 연결이 필요합니다.",
        TaskPreparationFailure.AuthenticationExpired => "로그인이 만료되었습니다. 다시 로그인해 주세요.",
        TaskPreparationFailure.Forbidden => "현재 업무의 준비 정보를 조회할 권한이 없습니다.",
        TaskPreparationFailure.NotFound => "업무를 찾을 수 없습니다. 목록을 다시 조회해 주세요.",
        TaskPreparationFailure.InvalidInput => "조회 조건을 확인해 주세요.",
        TaskPreparationFailure.AssignmentChanged => "담당 배정이 변경되었습니다. 업무 정보를 다시 조회해 주세요.",
        TaskPreparationFailure.VersionConflict => "업무가 변경되었습니다. 업무 정보를 다시 조회해 주세요.",
        TaskPreparationFailure.KeyReused => "같은 요청 키의 내용이 다릅니다. 처리 결과를 먼저 확인해 주세요.",
        TaskPreparationFailure.KeyExpired => "요청 키 또는 결과 보관 기간이 지났습니다. 현재 업무를 먼저 확인해 주세요.",
        _ => "준비 정보를 불러오지 못했습니다. 다시 조회해 주세요."
    };
}

public interface ITaskPreparationService
{
    bool IsCurrent { get; }
    bool CanSelectCandidates { get; }
    int UserId { get; }
    CancellationToken SessionCancellation { get; }
    Task<TaskCandidatePage> GetCandidatesAsync(TaskCandidateQuery query, CancellationToken ct = default);
    Task<TaskWriteContext> GetContextAsync(string taskId, CancellationToken ct = default);
    Task<TaskWriteRequestKey> GetRequestKeyAsync(TaskWriteAction action, string? taskId, CancellationToken ct = default);
    Task<TaskWriteResultLookup> GetResultAsync(TaskWriteRequestKey key, CancellationToken ct = default);
}

// An optional companion to the unchanged read interface; preview/test readers need not implement it.
public interface ITaskPreparationSource
{
    ITaskPreparationService? CreatePreparationService();
}
