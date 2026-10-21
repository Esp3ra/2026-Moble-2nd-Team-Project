using System.Globalization;
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration;

public sealed record CollaborationContext(string TaskId, string RowVersion, int AssignmentRevision,
    DateOnly DueDate, DateTimeOffset ServerTime);
public sealed record CollaborationEntry(string RequestId, string TaskId, int AssignmentRevision,
    int RequesterId, int RecipientId, string RequesterName, string TaskTitle, string Title,
    string RequestText, DateOnly DesiredDate, string Status, string RowVersion,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, string? EndReason, bool IsLate)
{
    public override string ToString() => $"[{StatusText(Status)}{(IsLate ? " · 지연" : "")}] {TaskTitle} · {Title}";
    public static string StatusText(string value) => value switch
    {
        "pending" => "응답 대기", "accepted" => "승인", "declined" => "거절",
        "withdrawn" => "철회", "completed" => "협업 완료", "left" => "참여 종료",
        "expired" => "기한 만료", "ended" => "업무 변경으로 종료", _ => "알 수 없음"
    };
}
public sealed record CollaborationHistoryEntry(string EventId, string Status, string? Reason,
    int? ActorId, DateTimeOffset OccurredAt)
{
    public override string ToString() => $"{OccurredAt.ToOffset(TimeSpan.FromHours(9)):yyyy-MM-dd HH:mm:ss} · {CollaborationEntry.StatusText(Status)} · {ReasonText(Reason)}";
    public static string ReasonText(string? reason) => reason switch
    {
        null => "요청 등록", "recipient_response" => "수신자 응답", "withdraw" => "요청자 철회",
        "complete" => "요청자 완료", "leave" => "수신자 참여 종료", "desired_date_elapsed" => "희망 기한 만료",
        "source_task_changed" => "업무 상태·배정·취소 변경", "source_task_interrupted" => "업무 진행 중단",
        _ => "상태 변경"
    };
}
public sealed record CollaborationResult(string RequestId, string Status, string RowVersion, DateTimeOffset UpdatedAt);
public sealed record CollaborationOperation(string Action, string Target, string Path, JsonElement Body, string Key);
public sealed class CollaborationOutcomeUnknownException : Exception
{
    public CollaborationOutcomeUnknownException() : base("처리 결과를 확인하지 못했습니다. 같은 요청의 결과를 확인해 주세요.") { }
}
public interface ICollaborationService
{
    int UserId { get; }
    bool IsCurrent { get; }
    CancellationToken Cancellation { get; }
    Task<IReadOnlyList<CollaborationEntry>> ListAsync(bool sent, CancellationToken ct);
    Task<IReadOnlyList<CollaborationHistoryEntry>> GetHistoryAsync(CollaborationEntry item, CancellationToken ct);
    Task<CollaborationOperation> PrepareCreateAsync(string taskId, int recipientId, string title,
        string text, DateOnly desired, CancellationToken ct);
    Task<CollaborationOperation> PrepareResponseAsync(CollaborationEntry item, string decision, CancellationToken ct);
    Task<CollaborationResult> ExecuteAsync(CollaborationOperation operation, CancellationToken ct);
    Task<CollaborationResult?> ResolveAsync(CollaborationOperation operation, CancellationToken ct);
}

/// <summary>Proposed API contract. Never falls back to sample/local-only delivery.</summary>
public sealed class HttpCollaborationService : ICollaborationService
{
    private readonly ApiClient.Session session;
    public HttpCollaborationService(ApiClient.Session session) => this.session = session;
    public int UserId => session.User.Id;
    public bool IsCurrent => session.IsCurrent;
    public CancellationToken Cancellation => session.Cancellation;
    private sealed record Page(CollaborationEntry[] Items, DateTimeOffset ServerTime);
    private sealed record KeyData(string RequestKey, DateTimeOffset ExpiresAt);
    private sealed record Lookup(string State, int? HttpStatus, CollaborationResult? Result);
    private sealed record HistoryPage(string RequestId, CollaborationHistoryEntry[] Items, DateTimeOffset ServerTime);
    private static readonly HashSet<string> States = ["pending", "accepted", "declined", "withdrawn", "completed", "left", "expired", "ended"];
    private static string BigInt(string value)
    {
        if (string.IsNullOrEmpty(value) || value[0] == '0' || value.Any(c => c is < '0' or > '9')
            || !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long number) || number < 1)
            throw new ApiException("업무 식별자·버전 형식이 올바르지 않습니다.");
        return value;
    }
    private static string Id(string value)
    {
        if (!Guid.TryParseExact(value, "D", out var id) || id.ToString("D") != value)
            throw new ApiException("협업 요청 식별자가 올바르지 않습니다.");
        return value;
    }
    private static CollaborationResult Validate(CollaborationResult value)
    {
        if (value is null || !States.Contains(value.Status) || value.UpdatedAt == default)
            throw new ApiException("협업 응답 형식이 올바르지 않습니다.");
        Id(value.RequestId); BigInt(value.RowVersion); return value;
    }
    public async Task<IReadOnlyList<CollaborationEntry>> ListAsync(bool sent, CancellationToken ct)
    {
        var page = await session.GetAsync<Page>("collaboration-requests?scope=" + (sent ? "sent" : "received"), ct);
        session.ThrowIfChanged();
        if (page?.Items is null || page.ServerTime == default) throw new ApiException("협업 목록 형식이 올바르지 않습니다.");
        var identifiers = new HashSet<string>();
        foreach (var item in page.Items)
        {
            if (item is null || item.AssignmentRevision < 1 || item.RequesterId < 1 || item.RecipientId < 1
                || item.RequesterId == item.RecipientId || (sent ? item.RequesterId : item.RecipientId) != UserId
                || string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.TaskTitle)
                || string.IsNullOrWhiteSpace(item.RequesterName) || string.IsNullOrWhiteSpace(item.RequestText)
                || !identifiers.Add(item.RequestId)) throw new ApiException("협업 목록 형식이 올바르지 않습니다.");
            BigInt(item.TaskId); Validate(new(item.RequestId, item.Status, item.RowVersion, item.UpdatedAt));
        }
        return page.Items;
    }
    public async Task<IReadOnlyList<CollaborationHistoryEntry>> GetHistoryAsync(CollaborationEntry item, CancellationToken ct)
    {
        Id(item.RequestId);
        var page = await session.GetAsync<HistoryPage>($"collaboration-requests/{item.RequestId}/history", ct);
        session.ThrowIfChanged();
        if (page is null || page.RequestId != item.RequestId || page.Items is null || page.ServerTime == default)
            throw new ApiException("협업 이력 형식이 올바르지 않습니다.");
        long previous = 0;
        foreach (var entry in page.Items)
        {
            if (entry is null || !States.Contains(entry.Status) || entry.OccurredAt == default
                || entry.ActorId is not null && entry.ActorId != item.RequesterId && entry.ActorId != item.RecipientId)
                throw new ApiException("협업 이력 형식이 올바르지 않습니다.");
            long current = long.Parse(BigInt(entry.EventId), CultureInfo.InvariantCulture);
            if (current <= previous) throw new ApiException("협업 이력 순서가 올바르지 않습니다.");
            previous = current;
        }
        return page.Items;
    }
    private async Task<CollaborationOperation> PrepareAsync(string action, string target, string path, object body, CancellationToken ct)
    {
        var key = await session.PostAsync<KeyData>("write-requests/keys", new { action, target }, ct);
        session.ThrowIfChanged();
        if (key is null || string.IsNullOrEmpty(key.RequestKey) || key.RequestKey.Length < 32 || key.ExpiresAt == default)
            throw new ApiException("쓰기 요청 키 형식이 올바르지 않습니다.");
        return new(action, target, path, JsonSerializer.SerializeToElement(body, ApiClient.Json), key.RequestKey);
    }
    public async Task<CollaborationOperation> PrepareCreateAsync(string taskId, int recipientId, string title,
        string text, DateOnly desired, CancellationToken ct)
    {
        BigInt(taskId);
        if (recipientId < 1 || recipientId == UserId || string.IsNullOrWhiteSpace(title) || title.Length > 200
            || string.IsNullOrWhiteSpace(text) || text.Length > 4000) throw new ApiException("대상·제목·요청 내용을 확인해 주세요.");
        var context = await session.GetAsync<CollaborationContext>("collaboration-requests/task-context/" + taskId, ct);
        session.ThrowIfChanged();
        if (context is null || context.TaskId != taskId || context.AssignmentRevision < 1 || context.ServerTime == default)
            throw new ApiException("업무 준비 정보 형식이 올바르지 않습니다.");
        BigInt(context.RowVersion);
        var today = DateOnly.FromDateTime(context.ServerTime.ToOffset(TimeSpan.FromHours(9)).DateTime);
        if (desired < today || desired > context.DueDate) throw new ApiException("희망 기한은 서버 기준 오늘부터 업무 마감일 사이여야 합니다.");
        return await PrepareAsync("collaboration.created", taskId, "collaboration-requests", new
        {
            request_id = Guid.NewGuid().ToString("D"), task_id = taskId, recipient_id = recipientId,
            title = title.Trim(), request_text = text.Trim(), desired_date = desired.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            expected_row_version = context.RowVersion, expected_assignment_revision = context.AssignmentRevision
        }, ct);
    }
    public Task<CollaborationOperation> PrepareResponseAsync(CollaborationEntry item, string decision, CancellationToken ct)
    {
        Id(item.RequestId); BigInt(item.RowVersion);
        string state = decision switch { "accept" => "accepted", "decline" => "declined", "withdraw" => "withdrawn",
            "complete" => "completed", "leave" => "left", _ => throw new ArgumentException("지원하지 않는 응답입니다.") };
        return PrepareAsync("collaboration." + state, item.RequestId,
            $"collaboration-requests/{item.RequestId}/actions/{decision}", new { expected_row_version = item.RowVersion }, ct);
    }
    public async Task<CollaborationResult> ExecuteAsync(CollaborationOperation operation, CancellationToken ct)
    {
        try
        {
            var value = await session.PostIdempotentAsync<CollaborationResult>(operation.Path, operation.Body, operation.Key, ct);
            session.ThrowIfChanged();
            return ValidateOperation(operation, value);
        }
        catch (ApiException error) when (error.StatusCode == 0 || error.StatusCode >= 500 || error.StatusCode is >= 200 and < 300)
        { throw new CollaborationOutcomeUnknownException(); }
    }
    public async Task<CollaborationResult?> ResolveAsync(CollaborationOperation operation, CancellationToken ct)
    {
        var lookup = await session.PostIdempotentAsync<Lookup>("write-requests/result",
            new { action = operation.Action, target = operation.Target }, operation.Key, ct);
        session.ThrowIfChanged();
        if (lookup?.State == "not_recorded") return null;
        if (lookup?.State != "completed" || lookup.HttpStatus is not (200 or 201) || lookup.Result is null)
            throw new ApiException("저장 결과 형식이 올바르지 않습니다.");
        return ValidateOperation(operation, lookup.Result);
    }
    private static CollaborationResult ValidateOperation(CollaborationOperation operation, CollaborationResult result)
    {
        Validate(result);
        string expectedId = operation.Action == "collaboration.created"
            ? operation.Body.GetProperty("request_id").GetString()! : operation.Target;
        string expectedStatus = operation.Action == "collaboration.created" ? "pending" : operation.Action.Split('.').Last();
        if (result.RequestId != expectedId || result.Status != expectedStatus)
            throw new ApiException("요청과 저장 결과가 일치하지 않습니다.");
        return result;
    }
}
