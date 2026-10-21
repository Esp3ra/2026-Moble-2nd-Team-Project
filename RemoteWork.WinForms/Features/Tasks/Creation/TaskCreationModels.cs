using System.Text;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;

namespace RemoteWork.WinForms.Features.Tasks.Creation;

// Keep the checked body unchanged while a request's result is uncertain.
public sealed class TaskCreationRequest
{
    public TaskCreationRequest(string title, string description, DateOnly startDate,
        DateOnly dueDate, int assigneeId, IReadOnlyList<int> managerUserIds,
        IReadOnlyList<string>? attachmentIds = null)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(managerUserIds);
        try
        {
            var strictUtf8 = new UTF8Encoding(false, true);
            strictUtf8.GetByteCount(title);
            strictUtf8.GetByteCount(description);
        }
        catch (EncoderFallbackException error)
        {
            throw new ArgumentException("제목과 설명의 문자 형식을 확인해 주세요.", error);
        }
        int titleLength = 0;
        foreach (var rune in title.EnumerateRunes()) titleLength++;
        if (string.IsNullOrWhiteSpace(title) || titleLength > 200)
            throw new ArgumentException("업무 제목은 공백만 제외하고 유니코드 문자 200개 이내로 입력해 주세요.");
        if (startDate.Year < 1000 || dueDate.Year < 1000 || dueDate < startDate)
            throw new ArgumentException("시작일과 마감일은 1000~9999년 범위이고 마감일은 시작일 이후여야 합니다.");
        if (assigneeId <= 0) throw new ArgumentException("업무 담당자를 선택해 주세요.");
        var managerIds = new List<int>();
        var seen = new HashSet<int>();
        foreach (int id in managerUserIds)
        {
            if (id <= 0 || !seen.Add(id))
                throw new ArgumentException("업무 관리자 ID는 양수이고 중복될 수 없습니다.");
            managerIds.Add(id);
        }
        if (attachmentIds is not null)
        {
            if (attachmentIds.Count > 10)
                throw new ArgumentException("업무 첨부파일은 최대 10개까지 선택할 수 있습니다.", nameof(attachmentIds));
            var copied = new List<string>();
            var seenAttachments = new HashSet<string>(StringComparer.Ordinal);
            foreach (string attachmentId in attachmentIds)
            {
                if (!Guid.TryParseExact(attachmentId, "D", out Guid parsed)
                    || parsed.ToString("D") != attachmentId || !seenAttachments.Add(attachmentId))
                    throw new ArgumentException("첨부 ID는 중복되지 않는 정규 소문자 UUID여야 합니다.", nameof(attachmentIds));
                copied.Add(attachmentId);
            }
            AttachmentIds = copied.AsReadOnly();
        }
        Title = title;
        Description = description;
        StartDate = startDate;
        DueDate = dueDate;
        AssigneeId = assigneeId;
        ManagerUserIds = managerIds.AsReadOnly();
    }

    public string Title { get; }
    public string Description { get; }
    public DateOnly StartDate { get; }
    public DateOnly DueDate { get; }
    public int AssigneeId { get; }
    public IReadOnlyList<int> ManagerUserIds { get; }
    // Null preserves the original six-field request; [] is an explicitly empty
    // final list. Never sort or deduplicate the frozen idempotent request body.
    public IReadOnlyList<string>? AttachmentIds { get; }
}

public sealed class TaskCreationResult
{
    public TaskCreationResult(string taskId, string rowVersion, int assignmentRevision,
        TaskDisplayStatus status, DateTimeOffset updatedAt)
    {
        TaskId = taskId;
        RowVersion = rowVersion;
        AssignmentRevision = assignmentRevision;
        Status = status;
        UpdatedAt = updatedAt;
    }
    public string TaskId { get; }
    public string RowVersion { get; }
    public int AssignmentRevision { get; }
    public TaskDisplayStatus Status { get; }
    public DateTimeOffset UpdatedAt { get; }
}

public sealed class TaskCreationLookup
{
    public TaskCreationLookup(bool recorded, TaskCreationResult? result = null)
    { Recorded = recorded; Result = result; }
    public bool Recorded { get; }
    public TaskCreationResult? Result { get; }
}

public enum TaskCreationFailure
{
    NotConfigured, AuthenticationExpired, Forbidden, InvalidInput,
    AssigneeUnavailable, ManagerUnavailable, KeyReused, KeyExpired,
    Conflict, Unavailable, InvalidResponse, AttachmentUnavailable
}

public sealed class TaskCreationException : Exception
{
    public TaskCreationException(TaskCreationFailure failure, Exception? inner = null)
        : base(MessageFor(failure), inner) { Failure = failure; }
    public TaskCreationFailure Failure { get; }
    public static string MessageFor(TaskCreationFailure failure) => failure switch
    {
        TaskCreationFailure.NotConfigured => "업무 등록 연결이 활성화되지 않았습니다.",
        TaskCreationFailure.AuthenticationExpired => "로그인이 만료되었습니다. 다시 로그인해 주세요.",
        TaskCreationFailure.Forbidden => "현재 로그인에는 업무 등록 또는 선택 첨부의 접근 권한이 없습니다. 현재 권한을 확인해 주세요.",
        TaskCreationFailure.InvalidInput => "업무 입력 내용과 전송 크기를 확인해 주세요.",
        TaskCreationFailure.AssigneeUnavailable => "선택한 담당자를 현재 배정할 수 없습니다. 담당자를 다시 확인해 주세요.",
        TaskCreationFailure.ManagerUnavailable => "선택한 관리자를 현재 지정할 수 없습니다. 관리자를 다시 확인해 주세요.",
        TaskCreationFailure.AttachmentUnavailable => "선택한 첨부파일이 없거나 전송 완료 상태가 아닙니다. 파일 목록을 다시 확인해 주세요. 기존 선택은 유지했습니다.",
        TaskCreationFailure.KeyReused => "요청 키의 내용이 일치하지 않습니다. 새로 등록하지 말고 처리 내역을 확인해 주세요.",
        TaskCreationFailure.KeyExpired => "요청 키 또는 결과 보관 기간이 지났습니다. 새로 등록하지 말고 처리 내역을 확인해 주세요.",
        TaskCreationFailure.Conflict => "등록 요청과 서버 정보가 일치하지 않습니다. 처리 내역을 확인해 주세요.",
        TaskCreationFailure.InvalidResponse => "서버 응답을 확인할 수 없습니다. 등록 결과를 먼저 조회해 주세요.",
        _ => "서버에 연결하지 못했거나 서버 오류가 발생했습니다. 등록 결과를 먼저 조회해 주세요."
    };
}

public interface ITaskCreationService
{
    bool IsCurrent { get; }
    bool CanCreate { get; }
    int UserId { get; }
    CancellationToken SessionCancellation { get; }
    Task<TaskWriteRequestKey> GetRequestKeyAsync(CancellationToken ct = default);
    Task<TaskCreationResult> CreateAsync(TaskCreationRequest request, TaskWriteRequestKey key, CancellationToken ct = default);
    Task<TaskCreationLookup> GetResultAsync(TaskWriteRequestKey key, CancellationToken ct = default);
}

// Preview/read-only providers do not need to offer creation.
public interface ITaskCreationSource
{
    // Borrowed from the authenticated session; closing a page must not dispose it.
    TaskCreationWorkflow? GetCreationWorkflow();
}
