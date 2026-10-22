using System.Globalization;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.Management;

public sealed class HttpTaskManagementService(ApiClient.Session session) : ITaskManagementService
{
    private readonly ApiClient.Session session = session ?? throw new ArgumentNullException(nameof(session));
    private readonly HttpTaskPreparationService preparation = new(session);
    public bool IsCurrent => session.IsCurrent;
    public int UserId => session.User.Id;
    public CancellationToken SessionCancellation => session.Cancellation;

    public async Task<TaskWriteRequestKey> GetRequestKeyAsync(TaskManagementRequest request, CancellationToken ct = default)
    {
        Ensure(request);
        try
        {
            var key = await preparation.GetRequestKeyAsync(TaskManagementFormat.Action(request.Kind), request.TaskId, ct).ConfigureAwait(false);
            ValidateKey(request, key); session.ThrowIfChanged(); return key;
        }
        catch (TaskPreparationException error) { throw Map(error); }
    }
    public async Task<TaskManagementResult> ChangeAsync(TaskManagementRequest request, TaskWriteRequestKey key, CancellationToken ct = default)
    {
        Ensure(request); ValidateKey(request, key);
        object body = request.Kind switch
        {
            TaskManagementKind.Update => UpdateBody(request),
            TaskManagementKind.Reassign => new { expected_row_version = request.ExpectedRowVersion,
                expected_assignment_revision = request.ExpectedAssignmentRevision, assignee_id = request.AssigneeId!.Value },
            TaskManagementKind.ChangeManagers => new { expected_row_version = request.ExpectedRowVersion,
                expected_assignment_revision = request.ExpectedAssignmentRevision, manager_user_ids = request.ManagerUserIds },
            _ => throw new ArgumentException("업무 변경 구분을 확인해 주세요.")
        };
        string path = "tasks/" + request.TaskId;
        try
        {
            JsonElement item = request.Kind == TaskManagementKind.Reassign
                ? await session.PostIdempotentAsync<JsonElement>(path + "/reassign", body, key.Value, ct).ConfigureAwait(false)
                : await session.PatchIdempotentAsync<JsonElement>(path + (request.Kind == TaskManagementKind.ChangeManagers ? "/managers" : ""), body, key.Value, ct).ConfigureAwait(false);
            TaskManagementResult result = Parse(request, item);
            session.ThrowIfChanged(); return result;
        }
        catch (ApiException error) { throw Map(error); }
    }
    private static object UpdateBody(TaskManagementRequest request)
    {
        var body = new Dictionary<string, object?>
        {
            ["expected_row_version"] = request.ExpectedRowVersion,
            ["expected_assignment_revision"] = request.ExpectedAssignmentRevision,
            ["title"] = request.Title,
            ["description"] = request.Description,
            ["start_date"] = request.StartDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["due_date"] = request.DueDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        };
        if (request.AttachmentIds is not null) body["attachment_ids"] = request.AttachmentIds;
        return body;
    }
    public async Task<TaskManagementLookup> GetResultAsync(TaskManagementRequest request, TaskWriteRequestKey key, CancellationToken ct = default)
    {
        Ensure(request); ValidateKey(request, key);
        try
        {
            TaskWriteResultLookup lookup = await preparation.GetResultAsync(key, ct).ConfigureAwait(false);
            session.ThrowIfChanged();
            if (!lookup.Recorded) return new(false);
            if (lookup.HttpStatus != 200 || lookup.Result is null) throw InvalidResponse();
            return new(true, Parse(request, lookup.Result.Value));
        }
        catch (TaskPreparationException error) { throw Map(error); }
    }
    private void Ensure(TaskManagementRequest request)
    {
        ArgumentNullException.ThrowIfNull(request); session.ThrowIfChanged();
        if (request.CheckedUserId != UserId || session.User.Role != "admin") throw new TaskManagementException(TaskManagementFailure.Forbidden);
    }
    private static void ValidateKey(TaskManagementRequest request, TaskWriteRequestKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Target != request.TaskId || key.Action != TaskManagementFormat.Action(request.Kind)
            || key.ExpiresAt.Offset != TimeSpan.Zero || key.Value is not { Length: >= 32 and <= 512 }
            || key.Value.Any(value => !char.IsAsciiLetterOrDigit(value) && value is not '-' and not '_' and not '.'))
            throw new ArgumentException("이 업무 변경의 서버 요청 키가 필요합니다.");
    }
    private static TaskManagementResult Parse(TaskManagementRequest request, JsonElement item)
    {
        string[] fields = ["task_id", "row_version", "assignment_revision", "task_status", "updated_at"];
        if (item.ValueKind != JsonValueKind.Object) throw InvalidResponse();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in item.EnumerateObject())
            if (!fields.Contains(field.Name, StringComparer.Ordinal) || !names.Add(field.Name)) throw InvalidResponse();
        if (names.Count != fields.Length) throw InvalidResponse();
        string? Text(string name) => item.GetProperty(name).ValueKind == JsonValueKind.String ? item.GetProperty(name).GetString() : null;
        string? taskId = Text("task_id"), version = Text("row_version");
        if (!TaskManagementFormat.IsId(taskId) || !TaskManagementFormat.IsId(version)
            || item.GetProperty("assignment_revision").ValueKind != JsonValueKind.Number
            || !item.GetProperty("assignment_revision").TryGetInt32(out int revision) || revision <= 0
            || !DateTimeOffset.TryParseExact(Text("updated_at"), "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var updated)) throw InvalidResponse();
        TaskDisplayStatus status = Text("task_status") switch
        {
            "not_started" => TaskDisplayStatus.NotStarted, "in_progress" => TaskDisplayStatus.InProgress,
            "submitted" => TaskDisplayStatus.Submitted, "approved" => TaskDisplayStatus.Approved,
            "returned" => TaskDisplayStatus.Returned, _ => throw InvalidResponse()
        };
        var result = new TaskManagementResult(taskId!, version!, revision, status, updated);
        if (!TaskManagementFormat.MatchesResult(request, result)) throw InvalidResponse();
        return result;
    }
    private static TaskManagementException InvalidResponse() => new(TaskManagementFailure.InvalidResponse);
    private static TaskManagementException Map(TaskPreparationException error) => new(error.Failure switch
    {
        TaskPreparationFailure.AuthenticationExpired => TaskManagementFailure.AuthenticationExpired,
        TaskPreparationFailure.Forbidden => TaskManagementFailure.Forbidden, TaskPreparationFailure.NotFound => TaskManagementFailure.NotFound,
        TaskPreparationFailure.NotConfigured => TaskManagementFailure.NotConfigured, TaskPreparationFailure.InvalidInput => TaskManagementFailure.InvalidInput,
        TaskPreparationFailure.AssignmentChanged => TaskManagementFailure.AssignmentChanged,
        TaskPreparationFailure.VersionConflict => TaskManagementFailure.VersionConflict,
        TaskPreparationFailure.KeyReused => TaskManagementFailure.KeyReused, TaskPreparationFailure.KeyExpired => TaskManagementFailure.KeyExpired,
        _ => TaskManagementFailure.Unavailable
    }, error);
    private static TaskManagementException Map(ApiException error) => new((error.StatusCode, error.Code) switch
    {
        (401, _) => TaskManagementFailure.AuthenticationExpired, (403, _) => TaskManagementFailure.Forbidden,
        (404, "ATTACHMENT_NOT_FOUND") or (409, "ATTACHMENT_UNAVAILABLE") => TaskManagementFailure.AttachmentUnavailable,
        (404, "TASK_NOT_FOUND") => TaskManagementFailure.NotFound, (404 or 405, _) => TaskManagementFailure.NotConfigured,
        (400 or 413, _) => TaskManagementFailure.InvalidInput,
        (409, "TASK_ASSIGNMENT_CHANGED") => TaskManagementFailure.AssignmentChanged,
        (409, "TASK_VERSION_CONFLICT") => TaskManagementFailure.VersionConflict,
        (409, "TASK_STATE_CONFLICT") => TaskManagementFailure.StateConflict,
        (409, "TASK_MANAGEMENT_DATA_INCONSISTENT") => TaskManagementFailure.DataInconsistent,
        (409, "TASK_VERSION_EXHAUSTED") => TaskManagementFailure.VersionExhausted,
        (409, "TASK_ASSIGNMENT_EXHAUSTED") => TaskManagementFailure.AssignmentExhausted,
        (409, "TASK_DUE_DATE_IN_PAST") => TaskManagementFailure.DueDateInPast,
        (409, "TASK_CHANGE_NOT_NEEDED") => TaskManagementFailure.NoChange,
        (409, "IDEMPOTENCY_KEY_REUSED") => TaskManagementFailure.KeyReused,
        (409, _) => TaskManagementFailure.Conflict, (410, _) => TaskManagementFailure.KeyExpired,
        (503, "WRITE_SCHEMA_REQUIRED" or "WRITE_SUPPORT_DISABLED" or "WRITE_ACTION_NOT_READY"
            or "TASK_MANAGEMENT_DISABLED" or "TASK_RESULTS_SCHEMA_REQUIRED" or "TASK_ATTACHMENT_REPLACEMENT_NOT_READY"
            or "SHARED_ATTACHMENT_SCHEMA_REQUIRED") => TaskManagementFailure.NotConfigured,
        (>= 200 and < 300, _) => TaskManagementFailure.InvalidResponse, _ => TaskManagementFailure.Unavailable
    }, error);
}
