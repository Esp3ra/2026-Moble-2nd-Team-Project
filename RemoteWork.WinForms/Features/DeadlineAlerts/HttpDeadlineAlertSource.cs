using System.Globalization;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.DeadlineAlerts;

/// <summary>
/// Member1 PR70 binding-aware HTTP consumer. Event UUID and confirmation
/// target are distinct. No schema creation or fabricated event/baseline.
/// </summary>
public sealed class HttpDeadlineAlertSource : IDeadlineAlertSource, IDisposable
{
    private const string Action = "deadline_alert.confirmed";
    private readonly ApiClient.Session session;
    private readonly CancellationTokenSource lifetime;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Dictionary<string, Operation> pending = new(StringComparer.Ordinal);
    private readonly HashSet<string> retryable = new(StringComparer.Ordinal);
    // Keys, pending operations and proofs are scoped by the server target,
    // never by the event UUID alone. A reassignment is not a new deadline event.
    private readonly Dictionary<string, (string TaskId, int Revision)> bindings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> eventTasks = new(StringComparer.Ordinal);
    private sealed record ConfirmationProof(string EventId, string TaskId, int Revision, DateTimeOffset ConfirmedAt);
    private readonly Dictionary<string, ConfirmationProof> succeeded = new(StringComparer.Ordinal);
    private Dictionary<string, DeadlineAlert> current = new(StringComparer.Ordinal);
    private sealed record Operation(string Target, string EventId, string TaskId, string RowVersion, int AssignmentRevision, string Key)
    {
        public object Body => new { task_id = TaskId, expected_row_version = RowVersion, expected_assignment_revision = AssignmentRevision };
    }
    public HttpDeadlineAlertSource(ApiClient.Session session)
    {
        session.ThrowIfChanged();
        this.session = session;
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(session.Cancellation);
    }
    public int UserId => session.User.Id;
    public bool IsCurrent => session.IsCurrent && !lifetime.IsCancellationRequested;
    public CancellationToken Cancellation => lifetime.Token;

    private async Task<T> Serial<T>(Func<CancellationToken, Task<T>> call, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, Cancellation);
        await gate.WaitAsync(linked.Token);
        try
        {
            session.ThrowIfChanged(); linked.Token.ThrowIfCancellationRequested();
            T value = await call(linked.Token);
            session.ThrowIfChanged(); linked.Token.ThrowIfCancellationRequested();
            return value;
        }
        finally { gate.Release(); }
    }
    private static InvalidDataException Invalid() => new("마감 알림 서버 응답 형식이 올바르지 않습니다.");
    private static void Shape(JsonElement value, params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Invalid();
        var names = value.EnumerateObject().Select(p => p.Name).ToArray();
        if (names.Length != fields.Length || !names.ToHashSet(StringComparer.Ordinal).SetEquals(fields)) throw Invalid();
    }
    private static string Text(JsonElement value, string name) =>
        value.GetProperty(name).ValueKind == JsonValueKind.String ? value.GetProperty(name).GetString()! : throw Invalid();
    private static int Integer(JsonElement value, string name) =>
        value.GetProperty(name).ValueKind == JsonValueKind.Number && value.GetProperty(name).TryGetInt32(out var n) && n > 0 ? n : throw Invalid();
    private static DateTimeOffset Time(string text) => text.EndsWith('Z') &&
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var stamp) && stamp != default && stamp.Offset == TimeSpan.Zero
        ? stamp : throw Invalid();
    private static string Id(string id) => id.Length is >= 1 and <= 80 &&
        id.All(c => char.IsAsciiLetterOrDigit(c) || c is ':' or '_' or '-') ? id : throw Invalid();
    private static bool Boolean(JsonElement value, string name) => value.GetProperty(name).ValueKind switch
    { JsonValueKind.True => true, JsonValueKind.False => false, _ => throw Invalid() };

    private async Task<DeadlineAlertSnapshot> ReadCore(string path, CancellationToken ct)
    {
        var page = await session.GetAsync<JsonElement>(path, ct);
        Shape(page, "server_time", "items");
        var stamp = Time(Text(page, "server_time"));
        var array = page.GetProperty("items");
        if (array.ValueKind != JsonValueKind.Array) throw Invalid();
        List<DeadlineAlert> items = [];
        var pageEvents = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in array.EnumerateArray())
        {
            Shape(item, "event_id", "target", "task_id", "title", "assignee_id", "status", "cancelled", "due_date", "row_version", "assignment_revision", "confirmed_at");
            var status = Text(item, "status") switch
            {
                "not_started" => TaskDisplayStatus.NotStarted, "in_progress" => TaskDisplayStatus.InProgress,
                "submitted" => TaskDisplayStatus.Submitted, "approved" => TaskDisplayStatus.Approved,
                "returned" => TaskDisplayStatus.Returned, _ => throw Invalid()
            };
            if (!DateOnly.TryParseExact(Text(item, "due_date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var due) || due.Year < 1000) throw Invalid();
            var confirmation = item.GetProperty("confirmed_at");
            DateTimeOffset? confirmedAt = null;
            if (confirmation.ValueKind != JsonValueKind.Null &&
                (confirmation.ValueKind != JsonValueKind.String || Time(confirmation.GetString()!) > stamp)) throw Invalid();
            if (confirmation.ValueKind == JsonValueKind.String) confirmedAt = Time(confirmation.GetString()!);
            var eventId = EventId(Text(item, "event_id"));
            var target = Target(Text(item, "target"));
            var entry = new DeadlineAlert(target, Text(item, "task_id"), Text(item, "row_version"),
                Integer(item, "assignment_revision"), UserId, Integer(item, "assignee_id"), Text(item, "title"), due,
                status, Boolean(item, "cancelled"), confirmation.ValueKind != JsonValueKind.Null) { EventId = eventId };
            if (target != $"d1:{entry.TaskId}:{eventId}:{entry.AssignmentRevision.ToString(CultureInfo.InvariantCulture)}") throw Invalid();
            if (eventTasks.TryGetValue(eventId, out var originalTask) && originalTask != entry.TaskId ||
                pageEvents.TryGetValue(eventId, out var pageTask) && pageTask != entry.TaskId) throw Invalid();
            pageEvents[eventId] = entry.TaskId;
            if (bindings.TryGetValue(entry.EventToken, out var binding) &&
                binding != (entry.TaskId, entry.AssignmentRevision)) throw Invalid();
            if (entry.CurrentAssigneeId != UserId || succeeded.TryGetValue(entry.EventToken, out var proof) &&
                (!entry.IsConfirmed || proof.ConfirmedAt != confirmedAt)) throw Invalid();
            items.Add(entry);
        }
        var snapshot = new DeadlineAlertSnapshot(stamp, items);
        DeadlineAlertPolicy.Validate(snapshot, UserId);
        foreach (var item in items) bindings[item.EventToken] = (item.TaskId, item.AssignmentRevision);
        foreach (var pair in pageEvents) eventTasks[pair.Key] = pair.Value;
        current = items.ToDictionary(i => i.EventToken, StringComparer.Ordinal);
        return snapshot;
    }
    public Task<DeadlineAlertSnapshot> ReadAsync(CancellationToken ct) => Serial(c => ReadCore("deadline-alerts", c), ct);
    public Task<DeadlineAlertSnapshot> RecheckAsync(string eventToken, CancellationToken ct) =>
        Serial(c => ReadCore("deadline-alerts/" + Uri.EscapeDataString(Target(eventToken)), c), ct);

    private static string EventId(string value) => Guid.TryParseExact(value, "D", out var id) &&
        id.ToString("D") == value ? value : throw Invalid();
    private static string Target(string value)
    {
        Id(value);
        var parts = value.Split(':');
        if (parts.Length != 4 || parts[0] != "d1" ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var task) || task < 1 || task.ToString(CultureInfo.InvariantCulture) != parts[1] ||
            !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var revision) || revision < 1 || revision.ToString(CultureInfo.InvariantCulture) != parts[3]) throw Invalid();
        EventId(parts[2]);
        return value;
    }
    private void Receipt(JsonElement value, Operation operation)
    {
        Shape(value, "task_id", "event_id", "assignment_revision", "confirmed_at");
        if (Text(value, "task_id") != operation.TaskId || Text(value, "event_id") != operation.EventId ||
            Integer(value, "assignment_revision") != operation.AssignmentRevision) throw Invalid();
        var proof = new ConfirmationProof(operation.EventId, operation.TaskId, operation.AssignmentRevision, Time(Text(value, "confirmed_at")));
        if (succeeded.TryGetValue(operation.Target, out var old) && old != proof) throw Invalid();
        succeeded[operation.Target] = proof;
    }
    private async Task<DeadlineConfirmation> AfterSuccess(CancellationToken ct)
    {
        try { return new(DeadlineConfirmationState.Confirmed, await ReadCore("deadline-alerts", ct)); }
        catch (ApiException e) when (e.StatusCode is not (401 or 403)) { return new(DeadlineConfirmationState.Confirmed); }
        catch (InvalidDataException) { return new(DeadlineConfirmationState.Confirmed); }
    }
    public Task<DeadlineConfirmation> ConfirmAsync(string eventToken, CancellationToken ct) => Serial(async c =>
    {
        Target(eventToken);
        if (succeeded.ContainsKey(eventToken)) return await AfterSuccess(c);
        Operation operation;
        if (pending.TryGetValue(eventToken, out var original))
        {
            if (!retryable.Remove(eventToken)) throw new ApiException("원래 확인 요청의 결과를 먼저 조회해 주세요.", 409, "DEADLINE_ORIGINAL_RESULT_REQUIRED");
            operation = original; // Explicit retry after verified not_recorded: exact original key/body.
        }
        else
        {
            if (!current.TryGetValue(eventToken, out var item)) throw new ApiException("현재 전체 알림 목록을 먼저 조회해 주세요.", 409, "DEADLINE_SNAPSHOT_REQUIRED");
            var key = await session.PostAsync<JsonElement>("write-requests/keys", new { action = Action, target = eventToken }, c);
            Shape(key, "request_key", "expires_at"); Time(Text(key, "expires_at"));
            var requestKey = Text(key, "request_key");
            if (requestKey.Length is < 32 or > 512 || requestKey.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch is not '-' and not '_' and not '.')) throw Invalid();
            operation = new Operation(eventToken, item.EventId ?? throw Invalid(), item.TaskId, item.RowVersion, item.AssignmentRevision, requestKey);
            pending.Add(eventToken, operation); // Retain before any business POST.
        }
        try
        {
            var result = await session.PostIdempotentAsync<JsonElement>("deadline-alerts/" + Uri.EscapeDataString(eventToken) + "/confirm", operation.Body, operation.Key, c);
            Receipt(result, operation);
        }
        catch (ApiException e) when (e.StatusCode == 409 && e.Code == "DEADLINE_EVENT_CHANGED")
        { return new DeadlineConfirmation(DeadlineConfirmationState.Changed, await ReadCore("deadline-alerts", c)); }
        catch (ApiException e) when (e.StatusCode == 0 || e.StatusCode >= 500 || e.StatusCode is >= 200 and < 300)
        { return new DeadlineConfirmation(DeadlineConfirmationState.Unknown, await ReadCore("deadline-alerts", c)); }
        catch (InvalidDataException)
        { return new DeadlineConfirmation(DeadlineConfirmationState.Unknown, await ReadCore("deadline-alerts", c)); }
        return await AfterSuccess(c);
    }, ct);

    public Task<DeadlineConfirmation> ResolveConfirmationAsync(string eventToken, CancellationToken ct) => Serial(async c =>
    {
        Target(eventToken);
        if (succeeded.ContainsKey(eventToken)) return await AfterSuccess(c);
        if (!pending.TryGetValue(eventToken, out var operation)) throw new ApiException("조회할 원래 확인 요청이 없습니다.", 409, "DEADLINE_OPERATION_REQUIRED");
        var lookup = await session.PostIdempotentAsync<JsonElement>("write-requests/result",
            new { action = Action, target = eventToken }, operation.Key, c);
        if (lookup.ValueKind != JsonValueKind.Object) throw Invalid();
        if (Text(lookup, "state") == "not_recorded")
        {
            Shape(lookup, "state");
            var snapshot = await ReadCore("deadline-alerts", c);
            retryable.Add(eventToken); // Preserve the original operation. Never POST during lookup.
            return new DeadlineConfirmation(DeadlineConfirmationState.NotRecorded, snapshot);
        }
        Shape(lookup, "state", "http_status", "result", "expires_at");
        if (Text(lookup, "state") != "completed" || Integer(lookup, "http_status") != 200) throw Invalid();
        Time(Text(lookup, "expires_at"));
        Receipt(lookup.GetProperty("result"), operation);
        return await AfterSuccess(c);
    }, ct);
    public void Dispose() { lifetime.Cancel(); }
}
