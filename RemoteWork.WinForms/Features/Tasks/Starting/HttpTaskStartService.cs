using System.Globalization;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.Starting;

// Member 2 endpoints through the existing member 1 captured HTTP session.
public sealed class HttpTaskStartService(ApiClient.Session session) : ITaskStartService
{
    private readonly ApiClient.Session session = session ?? throw new ArgumentNullException(nameof(session));
    public bool IsCurrent => session.IsCurrent;
    public int UserId => session.User.Id;
    public CancellationToken SessionCancellation => session.Cancellation;

    public async Task<TaskStartKey> GetRequestKeyAsync(TaskStartRequest request, CancellationToken ct = default)
    {
        EnsureRequest(request);
        try
        {
            var item = await session.PostAsync<JsonElement>("write-requests/keys",
                new { action = ActionCode(request.Kind), target = request.TaskId }, ct).ConfigureAwait(false);
            RequireFields(item, "request_key", "expires_at");
            string? value = ReadString(item, "request_key");
            if (!IsKey(value) || !TryUtc(ReadString(item, "expires_at"), out var expires)) throw InvalidResponse();
            session.ThrowIfChanged();
            return new(request.Kind, request.TaskId, value!, expires);
        }
        catch (ApiException error) { throw Map(error); }
    }

    public async Task<TaskStartResult> StartAsync(TaskStartRequest request, TaskStartKey key, CancellationToken ct = default)
    {
        EnsureRequest(request);
        ValidateKey(request, key);
        try
        {
            var item = await session.PostIdempotentAsync<JsonElement>("tasks/" + request.TaskId + "/"
                + (request.Kind == TaskStartKind.Accept ? "accept" : "rework-start"), new
                {
                    expected_row_version = request.ExpectedRowVersion,
                    expected_assignment_revision = request.ExpectedAssignmentRevision
                }, key.Value, ct).ConfigureAwait(false);
            var result = ParseResult(request, item);
            session.ThrowIfChanged();
            return result;
        }
        catch (ApiException error) { throw Map(error); }
    }

    public async Task<TaskStartLookup> GetResultAsync(TaskStartRequest request, TaskStartKey key, CancellationToken ct = default)
    {
        EnsureRequest(request);
        ValidateKey(request, key);
        try
        {
            var item = await session.PostIdempotentAsync<JsonElement>("write-requests/result",
                new { action = ActionCode(request.Kind), target = request.TaskId }, key.Value, ct).ConfigureAwait(false);
            if (item.ValueKind != JsonValueKind.Object) throw InvalidResponse();
            // Check state before selecting its exact allowed shape; duplicates always fail.
            string? state = ReadOptionalString(item, "state");
            if (state == "not_recorded")
            {
                RequireFields(item, "state");
                session.ThrowIfChanged();
                return new(false);
            }
            RequireFields(item, "state", "http_status", "result", "expires_at");
            if (state != "completed" || item.GetProperty("http_status").ValueKind != JsonValueKind.Number
                || !item.GetProperty("http_status").TryGetInt32(out int status) || status != 200
                || !TryUtc(ReadString(item, "expires_at"), out _)) throw InvalidResponse();
            var result = ParseResult(request, item.GetProperty("result"));
            session.ThrowIfChanged();
            return new(true, result);
        }
        catch (ApiException error) { throw Map(error); }
    }

    private void EnsureRequest(TaskStartRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        session.ThrowIfChanged();
        if (request.CheckedUserId != UserId) throw new TaskStartException(TaskStartFailure.Forbidden);
    }

    private static void ValidateKey(TaskStartRequest request, TaskStartKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Kind != request.Kind || key.Target != request.TaskId || !IsKey(key.Value)
            || key.ExpiresAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("이 업무 시작의 서버 요청 키가 필요합니다.", nameof(key));
        // Local expiry must not prevent lookup of a still retained successful receipt.
    }

    private static TaskStartResult ParseResult(TaskStartRequest request, JsonElement item)
    {
        RequireFields(item, "task_id", "row_version", "assignment_revision", "task_status", "updated_at");
        string? taskId = ReadString(item, "task_id"), version = ReadString(item, "row_version");
        var revision = item.GetProperty("assignment_revision");
        if (!TaskStartFormat.IsPositiveBigInt(taskId) || !TaskStartFormat.IsPositiveBigInt(version)
            || revision.ValueKind != JsonValueKind.Number || !revision.TryGetInt32(out int assignmentRevision)
            || assignmentRevision <= 0 || ReadString(item, "task_status") != "in_progress"
            || !TryUtc(ReadString(item, "updated_at"), out var updated)) throw InvalidResponse();
        var result = new TaskStartResult(taskId!, version!, assignmentRevision, TaskDisplayStatus.InProgress, updated);
        if (!TaskStartFormat.MatchesResult(request, result)) throw InvalidResponse();
        return result;
    }

    private static void RequireFields(JsonElement item, params string[] expected)
    {
        if (item.ValueKind != JsonValueKind.Object) throw InvalidResponse();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in item.EnumerateObject())
            if (!expected.Contains(field.Name, StringComparer.Ordinal) || !names.Add(field.Name)) throw InvalidResponse();
        if (names.Count != expected.Length) throw InvalidResponse();
    }

    private static string? ReadString(JsonElement item, string name) =>
        item.GetProperty(name).ValueKind == JsonValueKind.String ? item.GetProperty(name).GetString() : null;
    private static string? ReadOptionalString(JsonElement item, string name) =>
        item.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() : null;
    private static bool IsKey(string? value) => value is { Length: >= 32 and <= 512 }
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');
    private static bool TryUtc(string? value, out DateTimeOffset parsed) => DateTimeOffset.TryParseExact(value,
        "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed);
    private static string ActionCode(TaskStartKind kind) => kind switch
    {
        TaskStartKind.Accept => "task.accepted", TaskStartKind.Rework => "task.rework_started",
        _ => throw new ArgumentException("업무 시작 구분이 필요합니다.", nameof(kind))
    };
    private static TaskStartException InvalidResponse() => new(TaskStartFailure.InvalidResponse);
    private static TaskStartException Map(ApiException error) => new((error.StatusCode, error.Code) switch
    {
        (401, _) => TaskStartFailure.AuthenticationExpired, (403, _) => TaskStartFailure.Forbidden,
        (404, "TASK_NOT_FOUND") => TaskStartFailure.NotFound,
        (404 or 405, _) => TaskStartFailure.NotConfigured,
        (400 or 413, _) => TaskStartFailure.InvalidInput,
        (409, "TASK_ASSIGNMENT_CHANGED") => TaskStartFailure.AssignmentChanged,
        (409, "TASK_VERSION_CONFLICT") => TaskStartFailure.VersionConflict,
        (409, "TASK_STATE_CONFLICT") => TaskStartFailure.StateConflict,
        (409, "TASK_START_DATA_INCONSISTENT") => TaskStartFailure.DataInconsistent,
        (409, "TASK_VERSION_EXHAUSTED") => TaskStartFailure.VersionExhausted,
        (409, "TASK_START_BEFORE_START_DATE") => TaskStartFailure.BeforeStartDate,
        (409, "TASK_START_AFTER_DUE_DATE") => TaskStartFailure.AfterDueDate,
        (409, "IDEMPOTENCY_KEY_REUSED") => TaskStartFailure.KeyReused,
        (409, _) => TaskStartFailure.Conflict,
        (410, _) => TaskStartFailure.KeyExpired,
        (503, "WRITE_SCHEMA_REQUIRED" or "WRITE_SUPPORT_DISABLED" or "WRITE_ACTION_NOT_READY"
            or "TASK_START_DISABLED" or "TASK_START_SCHEMA_REQUIRED") => TaskStartFailure.NotConfigured,
        (>= 200 and < 300, _) => TaskStartFailure.InvalidResponse,
        _ => TaskStartFailure.Unavailable
    }, error);
}
