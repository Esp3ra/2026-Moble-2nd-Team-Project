using System.Globalization;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.Preparation;

/// <summary>W1 preparation/receipt endpoints only. No product mutation route is called.</summary>
public sealed class HttpTaskPreparationService(ApiClient.Session session) : ITaskPreparationService
{
    private readonly ApiClient.Session session = session ?? throw new ArgumentNullException(nameof(session));
    public bool IsCurrent => session.IsCurrent;
    public bool CanSelectCandidates => session.User.Role == "admin";
    public int UserId => session.User.Id;
    public CancellationToken SessionCancellation => session.Cancellation;

    public async Task<TaskCandidatePage> GetCandidatesAsync(TaskCandidateQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!Enum.IsDefined(query.Kind) || query.Search is null || query.Search.EnumerateRunes().Count() > 80
            || query.Page is < 1 or > 1_000_000 || query.Limit is < 1 or > 100)
            throw new ArgumentException("유효한 후보 조회 조건이 필요합니다.", nameof(query));
        if (query.TaskId is not null) ValidateId(query.TaskId);
        string path = "tasks/" + (query.Kind == TaskCandidateKind.Assignee ? "assignment" : "manager")
            + "-candidates?page=" + query.Page.ToString(CultureInfo.InvariantCulture)
            + "&limit=" + query.Limit.ToString(CultureInfo.InvariantCulture)
            + "&q=" + Uri.EscapeDataString(query.Search);
        if (query.TaskId is not null) path += "&task_id=" + query.TaskId;
        try
        {
            var page = await session.GetAsync<CandidatePageWire>(path, ct);
            if (page.Items is null || page.Page != query.Page || page.Limit != query.Limit || page.Total is null or < 0
                || page.Items.Count != Math.Min(query.Limit, Math.Max(0L, (long)page.Total - (long)(query.Page - 1) * query.Limit)))
                throw InvalidResponse();
            var ids = new HashSet<int>();
            var items = new List<TaskUserCandidate>();
            foreach (var item in page.Items)
            {
                if (item?.Id is not > 0 || string.IsNullOrWhiteSpace(item.DisplayName)
                    || string.IsNullOrWhiteSpace(item.SelectionLabel) || !ids.Add(item.Id.Value))
                    throw InvalidResponse();
                items.Add(new(item.Id.Value, item.DisplayName, item.SelectionLabel));
            }
            session.ThrowIfChanged();
            return new(items.AsReadOnly(), page.Page!.Value, page.Limit!.Value, page.Total.Value);
        }
        catch (ApiException error) { throw Map(error); }
    }

    public async Task<TaskWriteContext> GetContextAsync(string taskId, CancellationToken ct = default)
    {
        ValidateId(taskId);
        try
        {
            var response = await session.GetAsync<ContextEnvelope>("tasks/" + taskId + "/write-context", ct);
            var item = response.Context;
            if (item is null || item.TaskId != taskId || !IsId(item.RowVersion) || item.AssigneeId is not > 0
                || item.AssignmentRevision is not > 0 || item.ManagerUserIds is null
                || item.ManagerUserIds.Any(id => id <= 0) || item.ManagerUserIds.Distinct().Count() != item.ManagerUserIds.Count
                || !TryUtc(item.UpdatedAt, out var updatedAt))
                throw InvalidResponse();
            TaskDisplayStatus status = item.TaskStatus switch
            {
                "not_started" => TaskDisplayStatus.NotStarted, "in_progress" => TaskDisplayStatus.InProgress,
                "submitted" => TaskDisplayStatus.Submitted, "approved" => TaskDisplayStatus.Approved,
                "returned" => TaskDisplayStatus.Returned, _ => throw InvalidResponse()
            };
            session.ThrowIfChanged();
            return new(taskId, item.AssigneeId.Value, item.ManagerUserIds.AsReadOnly(), item.RowVersion!,
                item.AssignmentRevision.Value, status, updatedAt);
        }
        catch (ApiException error) { throw Map(error); }
    }

    public async Task<TaskWriteRequestKey> GetRequestKeyAsync(TaskWriteAction action, string? taskId, CancellationToken ct = default)
    {
        string code = ActionCode(action);
        string target = action == TaskWriteAction.Create && taskId is null ? "new" : taskId ?? "";
        if (action == TaskWriteAction.Create && target != "new") throw new ArgumentException("새 업무 대상이 필요합니다.");
        if (action != TaskWriteAction.Create) ValidateId(target);
        try
        {
            var item = await session.PostAsync<KeyWire>("write-requests/keys", new { action = code, target }, ct);
            if (item.RequestKey is null || item.RequestKey.Length is < 32 or > 512
                || item.RequestKey.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.')
                || !TryUtc(item.ExpiresAt, out var expires)) throw InvalidResponse();
            session.ThrowIfChanged();
            return new(action, target, item.RequestKey, expires);
        }
        catch (ApiException error) { throw Map(error); }
    }

    public async Task<TaskWriteResultLookup> GetResultAsync(TaskWriteRequestKey key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        try
        {
            var item = await session.PostIdempotentAsync<ResultWire>("write-requests/result",
                new { action = ActionCode(key.Action), target = key.Target }, key.Value, ct);
            session.ThrowIfChanged();
            if (item.State == "not_recorded")
            {
                if (item.Result is not null || item.HttpStatus is not null || item.ExpiresAt is not null) throw InvalidResponse();
                return new(false);
            }
            if (item.State != "completed" || item.HttpStatus is not (200 or 201)
                || item.Result is not { ValueKind: JsonValueKind.Object } result || !TryUtc(item.ExpiresAt, out var expires))
                throw InvalidResponse();
            return new(true, item.HttpStatus, result.Clone(), expires);
        }
        catch (ApiException error) { throw Map(error); }
    }

    private static string ActionCode(TaskWriteAction action) => action switch
    {
        TaskWriteAction.Create => "task.created", TaskWriteAction.Update => "task.updated",
        TaskWriteAction.Reassign => "task.reassigned", TaskWriteAction.ChangeManagers => "task.managers_changed",
        TaskWriteAction.Accept => "task.accepted", TaskWriteAction.RequestDeadlineExtension => "task.deadline_extension_requested",
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };
    private static bool IsId(string? id) => id is { Length: > 0 and <= 19 } && id[0] != '0'
        && id.All(char.IsAsciiDigit) && long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out long value) && value > 0;
    private static void ValidateId(string id)
    {
        if (!IsId(id)) throw new ArgumentException("양의 BIGINT 문자열이 필요합니다.", nameof(id));
    }
    private static bool TryUtc(string? value, out DateTimeOffset date) => DateTimeOffset.TryParseExact(value,
        "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
    private static TaskPreparationException InvalidResponse() => new(TaskPreparationFailure.Unavailable);
    private static TaskPreparationException Map(ApiException error) => new((error.StatusCode, error.Code) switch
    {
        (401, _) => TaskPreparationFailure.AuthenticationExpired, (403, _) => TaskPreparationFailure.Forbidden,
        (404, "TASK_NOT_FOUND") => TaskPreparationFailure.NotFound, (404, "HTTP_404") => TaskPreparationFailure.NotConfigured,
        (400, _) => TaskPreparationFailure.InvalidInput, (409, "TASK_ASSIGNMENT_CHANGED") => TaskPreparationFailure.AssignmentChanged,
        (409, "TASK_VERSION_CONFLICT") => TaskPreparationFailure.VersionConflict,
        (409, "IDEMPOTENCY_KEY_REUSED") => TaskPreparationFailure.KeyReused,
        (410, "IDEMPOTENCY_KEY_EXPIRED") => TaskPreparationFailure.KeyExpired,
        (503, "WRITE_SCHEMA_REQUIRED" or "WRITE_SUPPORT_DISABLED" or "WRITE_ACTION_NOT_READY") => TaskPreparationFailure.NotConfigured,
        _ => TaskPreparationFailure.Unavailable
    }, error);
    private sealed record CandidateWire(int? Id, string? DisplayName, string? SelectionLabel);
    private sealed record CandidatePageWire(List<CandidateWire?>? Items, int? Page, int? Limit, int? Total);
    private sealed record ContextWire(string? TaskId, int? AssigneeId, List<int>? ManagerUserIds,
        string? RowVersion, int? AssignmentRevision, string? TaskStatus, string? UpdatedAt);
    private sealed record ContextEnvelope(ContextWire? Context);
    private sealed record KeyWire(string? RequestKey, string? ExpiresAt);
    private sealed record ResultWire(string? State, int? HttpStatus, JsonElement? Result, string? ExpiresAt);
}
