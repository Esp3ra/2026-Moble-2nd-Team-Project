using System.Globalization;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.Creation;

// Opt-in member 2 development contract. The server remains the authorization authority.
public sealed class HttpTaskCreationService : ITaskCreationService
{
    private readonly ApiClient.Session session;
    private readonly HttpTaskPreparationService preparation;

    public HttpTaskCreationService(ApiClient.Session session)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        preparation = new HttpTaskPreparationService(session);
    }

    public bool IsCurrent => session.IsCurrent;
    public bool CanCreate => session.User.Role == "admin";
    public int UserId => session.User.Id;
    public CancellationToken SessionCancellation => session.Cancellation;

    public async Task<TaskWriteRequestKey> GetRequestKeyAsync(CancellationToken ct = default)
    {
        EnsureSession();
        try
        {
            var key = await preparation.GetRequestKeyAsync(TaskWriteAction.Create, null, ct).ConfigureAwait(false);
            session.ThrowIfChanged();
            ValidateKey(key);
            return key;
        }
        catch (TaskPreparationException error) { throw MapPreparation(error); }
    }

    public async Task<TaskCreationResult> CreateAsync(TaskCreationRequest request,
        TaskWriteRequestKey key, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateKey(key);
        EnsureSession();
        if (!request.ManagerUserIds.Contains(UserId))
            throw new TaskCreationException(TaskCreationFailure.InvalidInput);
        var body = new
        {
            title = request.Title,
            description = request.Description,
            start_date = request.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            due_date = request.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            assignee_id = request.AssigneeId,
            manager_user_ids = request.ManagerUserIds
        };
        // This is the common HTTP body limit, not an additional description policy.
        if (Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(body, ApiClient.Json)) > 32 * 1024)
            throw new TaskCreationException(TaskCreationFailure.InvalidInput);
        try
        {
            var response = await session.PostIdempotentAsync<JsonElement>("tasks", body, key.Value, ct).ConfigureAwait(false);
            var result = ParseResult(response);
            session.ThrowIfChanged();
            return result;
        }
        catch (ApiException error) { throw MapApi(error); }
    }

    public async Task<TaskCreationLookup> GetResultAsync(TaskWriteRequestKey key, CancellationToken ct = default)
    {
        ValidateKey(key);
        EnsureSession();
        try
        {
            var lookup = await preparation.GetResultAsync(key, ct).ConfigureAwait(false);
            session.ThrowIfChanged();
            if (!lookup.Recorded) return new TaskCreationLookup(false);
            // ApiClient exposes successful data only. A retained receipt additionally
            // exposes the original status, which must be 201 for this creation contract.
            if (lookup.HttpStatus != 201 || lookup.Result is null)
                throw new TaskCreationException(TaskCreationFailure.InvalidResponse);
            return new TaskCreationLookup(true, ParseResult(lookup.Result.Value));
        }
        catch (TaskPreparationException error) { throw MapPreparation(error); }
    }

    private void EnsureSession()
    {
        session.ThrowIfChanged();
        if (!CanCreate) throw new TaskCreationException(TaskCreationFailure.Forbidden);
    }

    private static void ValidateKey(TaskWriteRequestKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Action != TaskWriteAction.Create || key.Target != "new" || key.Value is null
            || key.Value.Length is < 32 or > 512)
            throw new ArgumentException("새 업무 등록용 요청 키가 필요합니다.", nameof(key));
        foreach (char character in key.Value)
            if (!char.IsAsciiLetterOrDigit(character) && character != '-' && character != '_' && character != '.')
                throw new ArgumentException("새 업무 등록용 요청 키가 필요합니다.", nameof(key));
    }

    private static TaskCreationResult ParseResult(JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object) throw InvalidResponse();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in item.EnumerateObject())
        {
            if (field.Name is not ("task_id" or "row_version" or "assignment_revision" or "task_status" or "updated_at")
                || !names.Add(field.Name)) throw InvalidResponse();
        }
        if (names.Count != 5) throw InvalidResponse();
        string? id = ReadString(item, "task_id");
        string? rowVersion = ReadString(item, "row_version");
        string? status = ReadString(item, "task_status");
        string? updated = ReadString(item, "updated_at");
        var revision = item.GetProperty("assignment_revision");
        if (!IsPositiveBigInt(id) || rowVersion != "1" || status != "not_started"
            || revision.ValueKind != JsonValueKind.Number || !revision.TryGetInt32(out int assignmentRevision)
            || assignmentRevision != 1
            || !DateTimeOffset.TryParseExact(updated, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var updatedAt))
            throw InvalidResponse();
        return new TaskCreationResult(id!, rowVersion, assignmentRevision, TaskDisplayStatus.NotStarted, updatedAt);
    }

    private static string? ReadString(JsonElement item, string name)
    {
        var field = item.GetProperty(name);
        return field.ValueKind == JsonValueKind.String ? field.GetString() : null;
    }

    private static bool IsPositiveBigInt(string? value)
    {
        if (value is null || value.Length is < 1 or > 19 || value[0] == '0') return false;
        foreach (char character in value) if (!char.IsAsciiDigit(character)) return false;
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long id) && id > 0;
    }

    private static TaskCreationException InvalidResponse() => new(TaskCreationFailure.InvalidResponse);

    private static TaskCreationException MapApi(ApiException error)
    {
        TaskCreationFailure failure = (error.StatusCode, error.Code) switch
        {
            (401, _) => TaskCreationFailure.AuthenticationExpired,
            (403, _) => TaskCreationFailure.Forbidden,
            (400 or 413, _) => TaskCreationFailure.InvalidInput,
            (404 or 405, _) => TaskCreationFailure.NotConfigured,
            (409, "ASSIGNEE_UNAVAILABLE") => TaskCreationFailure.AssigneeUnavailable,
            (409, "TASK_MANAGER_UNAVAILABLE") => TaskCreationFailure.ManagerUnavailable,
            (409, "IDEMPOTENCY_KEY_REUSED") => TaskCreationFailure.KeyReused,
            (410, _) => TaskCreationFailure.KeyExpired,
            (409, _) => TaskCreationFailure.Conflict,
            (>= 200 and < 300, _) => TaskCreationFailure.InvalidResponse,
            _ => TaskCreationFailure.Unavailable
        };
        return new TaskCreationException(failure, error);
    }

    private static TaskCreationException MapPreparation(TaskPreparationException error)
    {
        TaskCreationFailure failure = error.Failure switch
        {
            TaskPreparationFailure.NotConfigured => TaskCreationFailure.NotConfigured,
            TaskPreparationFailure.AuthenticationExpired => TaskCreationFailure.AuthenticationExpired,
            TaskPreparationFailure.Forbidden => TaskCreationFailure.Forbidden,
            TaskPreparationFailure.InvalidInput => TaskCreationFailure.InvalidInput,
            TaskPreparationFailure.KeyReused => TaskCreationFailure.KeyReused,
            TaskPreparationFailure.KeyExpired => TaskCreationFailure.KeyExpired,
            TaskPreparationFailure.VersionConflict or TaskPreparationFailure.AssignmentChanged => TaskCreationFailure.Conflict,
            _ => TaskCreationFailure.Unavailable
        };
        return new TaskCreationException(failure, error);
    }
}
