using System.Globalization;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.Lifecycle;

// Explicit member 2 adapter. This does not extend member 1 action enums or substitute test data.
public sealed class HttpTaskLifecycleService(ApiClient.Session session) : ITaskLifecycleService
{
    private readonly ApiClient.Session session = session ?? throw new ArgumentNullException(nameof(session));
    public bool IsCurrent => session.IsCurrent;
    public int UserId => session.User.Id;
    public bool IsAdministrator => session.User.Role == "admin";
    public CancellationToken SessionCancellation => session.Cancellation;
    public async Task<TaskLifecycleContext> GetContextAsync(string taskId, CancellationToken ct = default)
    {
        RequireId(taskId);
        try
        {
            var envelope = await session.GetAsync<JsonElement>("tasks/" + taskId + "/lifecycle", ct).ConfigureAwait(false);
            Exact(envelope, "context"); var item = envelope.GetProperty("context");
            Exact(item, "task_id", "row_version", "assignment_revision", "task_status", "start_date", "due_date", "updated_at",
                "assignee_id", "manager_user_ids", "is_cancelled", "cancellation", "pending_extension", "write_enabled");
            string id = Id(item, "task_id"), version = Id(item, "row_version");
            DateOnly start = Date(item, "start_date"), due = Date(item, "due_date");
            if (id != taskId || due < start || item.GetProperty("manager_user_ids").ValueKind != JsonValueKind.Array) throw Invalid();
            int[] managers = item.GetProperty("manager_user_ids").EnumerateArray().Select(IntValue).ToArray();
            if (managers.Distinct().Count() != managers.Length) throw Invalid();
            bool cancelled = Bool(item, "is_cancelled"); TaskCancellation? cancellation = null;
            var cancellationWire = item.GetProperty("cancellation");
            if (cancellationWire.ValueKind != JsonValueKind.Null)
            {
                Exact(cancellationWire, "cancelled_by", "cancelled_at", "reason");
                cancellation = new(Int(cancellationWire, "cancelled_by"), Utc(cancellationWire, "cancelled_at"), Text(cancellationWire, "reason"));
            }
            if (cancelled != (cancellation is not null)) throw Invalid();
            var pendingWire = item.GetProperty("pending_extension");
            TaskExtensionRequest? pending = pendingWire.ValueKind == JsonValueKind.Null ? null : Extension(pendingWire);
            int revision = Int(item, "assignment_revision");
            if (pending is not null && (pending.TaskId != id || pending.AssignmentRevision != revision || pending.Status != TaskExtensionStatus.Pending)) throw Invalid();
            var context = new TaskLifecycleContext(id, version, revision, Status(item, "task_status"), start, due,
                Utc(item, "updated_at"), Int(item, "assignee_id"), Array.AsReadOnly(managers), cancelled,
                cancellation, pending, Bool(item, "write_enabled"));
            session.ThrowIfChanged(); return context;
        }
        catch (ApiException error) { throw Map(error); }
    }
    public async Task<IReadOnlyList<TaskExtensionRequest>> GetExtensionsAsync(TaskQueryScope scope, TaskExtensionStatus? status = null, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(scope) || status is { } value && !Enum.IsDefined(value)) throw new ArgumentException("조회 조건을 확인해 주세요.");
        string path = "tasks/extension-requests?scope=" + (scope == TaskQueryScope.Own ? "own" : "managed");
        if (status is { } filter) path += "&status=" + filter.ToString().ToLowerInvariant();
        try
        {
            var envelope = await session.GetAsync<JsonElement>(path, ct).ConfigureAwait(false); Exact(envelope, "items");
            if (envelope.GetProperty("items").ValueKind != JsonValueKind.Array) throw Invalid();
            var rows = envelope.GetProperty("items").EnumerateArray().Select(Extension).ToArray();
            if (rows.Select(row => row.ExtensionId).Distinct().Count() != rows.Length
                || status is { } wanted && rows.Any(row => row.Status != wanted)
                || scope == TaskQueryScope.Own && rows.Any(row => row.RequestedBy != UserId)) throw Invalid();
            session.ThrowIfChanged(); return Array.AsReadOnly(rows);
        }
        catch (ApiException error) { throw Map(error); }
    }
    public async Task<TaskLifecycleKey> GetRequestKeyAsync(TaskLifecycleRequest request, CancellationToken ct = default)
    {
        Ensure(request); _ = Body(request);
        try
        {
            var item = await session.PostAsync<JsonElement>("write-requests/keys", new { action = TaskLifecycleFormat.Action(request.Kind), target = request.TaskId }, ct).ConfigureAwait(false);
            Exact(item, "request_key", "expires_at");
            var key = new TaskLifecycleKey(request.Kind, request.TaskId, Text(item, "request_key"), Utc(item, "expires_at"));
            ValidateKey(request, key); session.ThrowIfChanged(); return key;
        }
        catch (ApiException error) { throw Map(error); }
    }
    public async Task<TaskLifecycleResult> ChangeAsync(TaskLifecycleRequest request, TaskLifecycleKey key, CancellationToken ct = default)
    {
        Ensure(request); ValidateKey(request, key);
        object body = Body(request);
        string suffix = request.Kind switch
        {
            TaskLifecycleKind.Cancel => "/cancel", TaskLifecycleKind.RequestExtension => "/deadline-extensions",
            TaskLifecycleKind.ApproveExtension => "/deadline-extensions/" + request.ExtensionId + "/approve",
            TaskLifecycleKind.RejectExtension => "/deadline-extensions/" + request.ExtensionId + "/reject",
            _ => "/deadline-extensions/" + request.ExtensionId + "/withdraw"
        };
        try
        {
            var result = ParseResult(request, await session.PostIdempotentAsync<JsonElement>("tasks/" + request.TaskId + suffix, body, key.Value, ct).ConfigureAwait(false));
            session.ThrowIfChanged(); return result;
        }
        catch (ApiException error) { throw Map(error); }
    }
    private static object Body(TaskLifecycleRequest request)
    {
        object body = request.Kind switch
        {
            TaskLifecycleKind.Cancel => new { expected_row_version = request.ExpectedRowVersion, expected_assignment_revision = request.ExpectedAssignmentRevision, reason = request.Reason },
            TaskLifecycleKind.RequestExtension => new { expected_row_version = request.ExpectedRowVersion, expected_assignment_revision = request.ExpectedAssignmentRevision,
                requested_due_date = request.RequestedDueDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), reason = request.Reason },
            TaskLifecycleKind.RejectExtension => new { expected_row_version = request.ExpectedRowVersion, expected_assignment_revision = request.ExpectedAssignmentRevision, reason = request.Reason },
            _ => new { expected_row_version = request.ExpectedRowVersion, expected_assignment_revision = request.ExpectedAssignmentRevision }
        };
        // Match member 1's transport envelope limit as well as the TEXT bound.
        if (JsonSerializer.SerializeToUtf8Bytes(body, ApiClient.Json).Length > 32 * 1024)
            throw new TaskLifecycleException(TaskLifecycleFailure.InvalidInput);
        return body;
    }
    public async Task<TaskLifecycleLookup> GetResultAsync(TaskLifecycleRequest request, TaskLifecycleKey key, CancellationToken ct = default)
    {
        Ensure(request); ValidateKey(request, key);
        try
        {
            var item = await session.PostIdempotentAsync<JsonElement>("write-requests/result",
                new { action = TaskLifecycleFormat.Action(request.Kind), target = request.TaskId }, key.Value, ct).ConfigureAwait(false);
            string state = Text(item, "state");
            TaskLifecycleLookup lookup;
            if (state == "not_recorded")
            {
                Exact(item, "state");
                lookup = new(false);
            }
            else
            {
                Exact(item, "state", "http_status", "result", "expires_at");
                if (state != "completed" || Int(item, "http_status") != 200) throw Invalid();
                _ = Utc(item, "expires_at"); lookup = new(true, ParseResult(request, item.GetProperty("result")));
            }
            session.ThrowIfChanged(); return lookup;
        }
        catch (ApiException error) { throw Map(error); }
    }
    private void Ensure(TaskLifecycleRequest request)
    { ArgumentNullException.ThrowIfNull(request); session.ThrowIfChanged(); if (request.CheckedUserId != UserId) throw new TaskLifecycleException(TaskLifecycleFailure.Forbidden); }
    private static void ValidateKey(TaskLifecycleRequest request, TaskLifecycleKey key)
    {
        if (key.Kind != request.Kind || key.Target != request.TaskId || key.ExpiresAt.Offset != TimeSpan.Zero
            || key.Value is not { Length: >= 32 and <= 512 } || key.Value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw Invalid();
    }
    private static TaskLifecycleResult ParseResult(TaskLifecycleRequest request, JsonElement item)
    {
        Exact(item, "task_id", "row_version", "assignment_revision", "task_status", "updated_at", "extension_id", "requested_due_date", "due_date");
        var result = new TaskLifecycleResult(Id(item, "task_id"), Id(item, "row_version"), Int(item, "assignment_revision"), Status(item, "task_status"),
            Utc(item, "updated_at"), NullableText(item, "extension_id"), NullableDate(item, "requested_due_date"), Date(item, "due_date"));
        if (!TaskLifecycleFormat.MatchesResult(request, result)) throw Invalid(); return result;
    }
    private static TaskExtensionRequest Extension(JsonElement item)
    {
        Exact(item, "extension_id", "task_id", "assignment_revision", "requested_by", "original_due_date", "requested_due_date", "reason", "status", "created_at", "decided_at", "decided_by", "decision_reason");
        TaskExtensionStatus status = Text(item, "status") switch
        {
            "pending" => TaskExtensionStatus.Pending, "approved" => TaskExtensionStatus.Approved, "rejected" => TaskExtensionStatus.Rejected,
            "withdrawn" => TaskExtensionStatus.Withdrawn, "cancelled" => TaskExtensionStatus.Cancelled, _ => throw Invalid()
        };
        var record = new TaskExtensionRequest(Id(item, "extension_id"), Id(item, "task_id"), Int(item, "assignment_revision"), Int(item, "requested_by"),
            Date(item, "original_due_date"), Date(item, "requested_due_date"), Text(item, "reason"), status, Utc(item, "created_at"),
            item.GetProperty("decided_at").ValueKind == JsonValueKind.Null ? null : Utc(item, "decided_at"),
            item.GetProperty("decided_by").ValueKind == JsonValueKind.Null ? null : Int(item, "decided_by"), NullableText(item, "decision_reason"));
        if (record.RequestedDueDate <= record.OriginalDueDate || string.IsNullOrWhiteSpace(record.Reason)
            || (status == TaskExtensionStatus.Pending ? record.DecidedAt is not null || record.DecidedBy is not null || record.DecisionReason is not null
                : record.DecidedAt is null || record.DecidedBy is null)) throw Invalid();
        return record;
    }
    private static void Exact(JsonElement item, params string[] names)
    {
        if (item.ValueKind != JsonValueKind.Object) throw Invalid(); var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in item.EnumerateObject()) if (!names.Contains(field.Name, StringComparer.Ordinal) || !found.Add(field.Name)) throw Invalid();
        if (found.Count != names.Length) throw Invalid();
    }
    private static string Text(JsonElement item, string field) => item.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { } text ? text : throw Invalid();
    private static string? NullableText(JsonElement item, string field) => item.GetProperty(field).ValueKind == JsonValueKind.Null ? null : Text(item, field);
    private static string Id(JsonElement item, string field) { string text = Text(item, field); RequireId(text); return text; }
    private static void RequireId(string text) { if (!TaskLifecycleFormat.IsId(text)) throw Invalid(); }
    private static int Int(JsonElement item, string field) => IntValue(item.GetProperty(field));
    private static int IntValue(JsonElement value) => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number > 0 ? number : throw Invalid();
    private static bool Bool(JsonElement item, string field) => item.GetProperty(field).ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => throw Invalid() };
    private static DateOnly Date(JsonElement item, string field) => DateOnly.TryParseExact(Text(item, field), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : throw Invalid();
    private static DateOnly? NullableDate(JsonElement item, string field) => item.GetProperty(field).ValueKind == JsonValueKind.Null ? null : Date(item, field);
    private static DateTimeOffset Utc(JsonElement item, string field) => DateTimeOffset.TryParseExact(Text(item, field), "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date) ? date : throw Invalid();
    private static TaskDisplayStatus Status(JsonElement item, string field) => Text(item, field) switch
    { "not_started" => TaskDisplayStatus.NotStarted, "in_progress" => TaskDisplayStatus.InProgress, "submitted" => TaskDisplayStatus.Submitted, "approved" => TaskDisplayStatus.Approved, "returned" => TaskDisplayStatus.Returned, _ => throw Invalid() };
    private static TaskLifecycleException Invalid() => new(TaskLifecycleFailure.InvalidResponse);
    private static TaskLifecycleException Map(ApiException error) => new((error.StatusCode, error.Code) switch
    {
        (401, _) => TaskLifecycleFailure.AuthenticationExpired, (403, _) => TaskLifecycleFailure.Forbidden,
        (404, "TASK_NOT_FOUND" or "TASK_EXTENSION_NOT_FOUND") => TaskLifecycleFailure.NotFound,
        (404 or 405, _) => TaskLifecycleFailure.NotConfigured, (400 or 413, _) => TaskLifecycleFailure.InvalidInput,
        (409, "TASK_ASSIGNMENT_CHANGED") => TaskLifecycleFailure.AssignmentChanged, (409, "TASK_VERSION_CONFLICT") => TaskLifecycleFailure.VersionConflict,
        (409, "TASK_STATE_CONFLICT") => TaskLifecycleFailure.StateConflict, (409, "TASK_CANCELLED") => TaskLifecycleFailure.Cancelled,
        (409, "TASK_EXTENSION_NOT_PENDING" or "TASK_EXTENSION_BASE_CHANGED" or "TASK_VERSION_EXHAUSTED") => TaskLifecycleFailure.StateConflict,
        (409, "TASK_EXTENSION_DATE_INVALID" or "TASK_EXTENSION_DECIDER_REQUIRED") => TaskLifecycleFailure.InvalidInput,
        (409, "TASK_EXTENSION_PENDING") => TaskLifecycleFailure.PendingExtension, (409, "IDEMPOTENCY_KEY_REUSED") => TaskLifecycleFailure.KeyReused,
        (409, _) => TaskLifecycleFailure.Conflict, (410, _) => TaskLifecycleFailure.KeyExpired,
        (503, "TASK_LIFECYCLE_SCHEMA_REQUIRED" or "TASK_LIFECYCLE_INTEGRATION_REQUIRED" or "TASK_LIFECYCLE_DISABLED"
            or "WRITE_SCHEMA_REQUIRED" or "WRITE_SUPPORT_DISABLED" or "WRITE_ACTION_NOT_READY") => TaskLifecycleFailure.TechnicalConnectionRequired,
        (503, _) => TaskLifecycleFailure.Unavailable, (>= 200 and < 300, _) => TaskLifecycleFailure.InvalidResponse,
        _ => TaskLifecycleFailure.Unavailable
    }, error);
}
