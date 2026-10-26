using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.ReviewRequests;

public sealed record ReviewRequestItem(string RequestId, string TaskId, string Title, string SubmissionId,
    string RoundNo, int AssignmentRevision, int RequestedBy, string RequesterName, int ReviewerId,
    string ReviewerName, string Reason, string State, DateTimeOffset CreatedAt,
    DateTimeOffset? AcknowledgedAt, int? AcknowledgedBy, string RowVersion, string TaskStatus);
public sealed record ReviewRequestContext(string TaskId, string Title, string TaskStatus, DateOnly DueDate,
    string RowVersion, int AssignmentRevision, string SubmissionId, string RoundNo, string Description,
    int ReviewerId, string ReviewerName, ReviewRequestItem? Request);
public sealed record ReviewRequestSaved(string RequestId, string TaskId, string SubmissionId, string RoundNo,
    string RequestState, DateTimeOffset CreatedAt, DateTimeOffset? AcknowledgedAt);

public sealed class ReviewRequestOperation
{
    internal ReviewRequestOperation(string action, string taskId, string submissionId, string roundNo,
        string path, object body, string reason, string? requestId = null)
    {
        Action = action; TaskId = taskId; SubmissionId = submissionId; RoundNo = roundNo;
        Path = path; Body = JsonSerializer.SerializeToElement(body, ApiClient.Json); Reason = reason; RequestId = requestId;
    }
    public string Action { get; }
    public string TaskId { get; }
    public string SubmissionId { get; }
    public string RoundNo { get; }
    public string Reason { get; }
    public string? RequestId { get; }
    internal string Path { get; }
    internal JsonElement Body { get; }
    internal string? Key { get; set; }
    public string Stage { get; internal set; } = "ready";
    public ReviewRequestSaved? Saved { get; internal set; }
}

// One pending write per login, retained across page/dialog reopening. The common
// Session owns authentication and HTTP; no token or user data is persisted here.
public sealed class TaskReviewRequestsClient
{
    private static readonly ConditionalWeakTable<ApiClient, Holder> stores = new();
    private sealed class Holder { internal TaskReviewRequestsClient? Client; }
    public static TaskReviewRequestsClient For(ApiClient api)
    {
        var holder = stores.GetValue(api, _ => new Holder());
        lock (holder)
        {
            if (holder.Client?.Session.IsCurrent != true)
                holder.Client = new TaskReviewRequestsClient(api.CaptureSession());
            return holder.Client;
        }
    }
    public TaskReviewRequestsClient(ApiClient.Session session) { Session = session; }
    public ApiClient.Session Session { get; }
    public bool IsBusy { get; private set; }
    public bool IsBlocked { get; private set; }
    private ReviewRequestOperation? pending;
    public ReviewRequestOperation? Pending { get => IsBlocked ? null : pending; private set => pending = value; }
    public bool CanCheckBlockedResult => IsBlocked && pending?.Stage == "check" && pending.Key is not null;
    private sealed record ContextEnvelope(ReviewRequestContext Context);
    private sealed record ItemsEnvelope(List<ReviewRequestItem> Items);
    private sealed record KeyEnvelope(string RequestKey, DateTimeOffset ExpiresAt);

    public async Task<ReviewRequestContext> ContextAsync(string taskId, CancellationToken ct)
    {
        DemandSession();
        Id(taskId);
        ContextEnvelope envelope;
        try { envelope = await Session.GetAsync<ContextEnvelope>("tasks/" + taskId + "/review-request-context", ct); }
        catch (ApiException error) when (error.StatusCode == 403) { IsBlocked = true; throw; }
        var item = envelope.Context ?? throw Invalid();
        if (item.TaskId != taskId || item.TaskStatus != "submitted" || item.AssignmentRevision < 1
            || item.ReviewerId < 1 || item.ReviewerId == Session.User.Id || item.Title is null
            || item.Description is null || item.ReviewerName is null || item.DueDate == default) throw Invalid();
        Id(item.RowVersion); Id(item.SubmissionId); Id(item.RoundNo);
        if (item.Request is { } existing)
        {
            Validate(existing);
            if (existing.TaskId != taskId || existing.SubmissionId != item.SubmissionId
                || existing.AssignmentRevision != item.AssignmentRevision || existing.RequestedBy != Session.User.Id) throw Invalid();
        }
        Session.ThrowIfChanged(); return item;
    }
    public async Task<IReadOnlyList<ReviewRequestItem>> ListAsync(bool reviewing, CancellationToken ct)
    {
        DemandSession();
        ItemsEnvelope response;
        try { response = await Session.GetAsync<ItemsEnvelope>("tasks/review-requests?scope=" + (reviewing ? "reviewing" : "own"), ct); }
        catch (ApiException error) when (error.StatusCode == 403) { IsBlocked = true; throw; }
        if (response.Items is null) throw Invalid();
        foreach (var row in response.Items)
        {
            Validate(row);
            if (reviewing ? row.ReviewerId != Session.User.Id : row.RequestedBy != Session.User.Id) throw Invalid();
        }
        if (response.Items.Select(x => x.RequestId).Distinct().Count() != response.Items.Count) throw Invalid();
        Session.ThrowIfChanged(); return response.Items.AsReadOnly();
    }
    public ReviewRequestOperation Prepare(ReviewRequestContext context, string reason)
    {
        DemandNew();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 4000)
            throw new ArgumentException("검토 요청 내용은 공백을 제외하고 4,000자 이내로 입력해 주세요.");
        var operation = new ReviewRequestOperation("task.review_requested", context.TaskId, context.SubmissionId, context.RoundNo,
            "tasks/" + Id(context.TaskId) + "/review-requests", new { expected_row_version = Id(context.RowVersion),
                expected_assignment_revision = context.AssignmentRevision, submission_id = Id(context.SubmissionId), reason }, reason);
        if (JsonSerializer.SerializeToUtf8Bytes(operation.Body).Length > 32 * 1024)
            throw new ArgumentException("검토 요청 내용이 전송 한도를 초과했습니다.");
        Pending = operation; return operation;
    }
    public ReviewRequestOperation PrepareAcknowledge(ReviewRequestItem item)
    {
        DemandNew(); Validate(item);
        if (item.State != "pending" || item.ReviewerId != Session.User.Id || item.RequestedBy == Session.User.Id)
            throw new InvalidOperationException("현재 지정 검토자의 대기 요청만 확인할 수 있습니다.");
        Pending = new ReviewRequestOperation("task.review_acknowledged", item.TaskId, item.SubmissionId, item.RoundNo,
            "tasks/" + item.TaskId + "/review-requests/" + item.RequestId + "/acknowledge",
            new { expected_row_version = item.RowVersion, expected_assignment_revision = item.AssignmentRevision }, item.Reason, item.RequestId);
        return Pending;
    }
    private void DemandNew()
    {
        DemandSession();
        if (IsBusy || Pending is not null) throw new InvalidOperationException("기존 요청 처리 결과를 먼저 확인해 주세요.");
    }
    public void ClearConfirmed()
    {
        DemandSession();
        if (IsBusy || Pending?.Stage is not ("completed" or "failed"))
            throw new InvalidOperationException("미확인 요청은 지울 수 없습니다. 처리 결과를 먼저 확인해 주세요.");
        Pending = null;
    }
    public async Task SendAsync(CancellationToken ct)
    {
        DemandSession();
        var op = Pending ?? throw new InvalidOperationException("확인할 요청이 없습니다.");
        if (IsBusy || op.Stage is not ("ready" or "not_recorded"))
            throw new InvalidOperationException("처리 결과를 먼저 확인해 주세요.");
        IsBusy = true;
        try
        {
            if (op.Key is null)
            {
                var key = await Session.PostAsync<KeyEnvelope>("write-requests/keys", new { action = op.Action, target = op.TaskId }, ct);
                if (key.RequestKey is not { Length: >= 32 and <= 512 }
                    || key.RequestKey.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.')
                    || key.ExpiresAt == default || key.ExpiresAt.Offset != TimeSpan.Zero) throw Invalid();
                op.Key = key.RequestKey;
            }
            Session.ThrowIfChanged(); ct.ThrowIfCancellationRequested();
            op.Stage = "check"; // Never automatically repeat a write with an unknown outcome.
            var result = await Session.PostIdempotentAsync<ReviewRequestSaved>(op.Path, op.Body, op.Key, ct);
            Session.ThrowIfChanged(); ValidateSaved(op, result); op.Saved = result; op.Stage = "completed";
        }
        catch (ApiException error) when (error.StatusCode is 400 or 403 or 404 or 409 or 413 or 415 or 422)
        { op.Stage = "failed"; if (error.StatusCode == 403) IsBlocked = true; throw; }
        finally { IsBusy = false; }
    }
    public async Task CheckAsync(CancellationToken ct)
    {
        Session.ThrowIfChanged();
        if (IsBlocked && !CanCheckBlockedResult) DemandSession();
        var op = pending ?? throw new InvalidOperationException("확인할 요청이 없습니다.");
        if (IsBusy || op.Key is null || op.Stage != "check") throw new InvalidOperationException("결과 확인 대상이 없습니다.");
        IsBusy = true;
        try
        {
            var answer = await Session.PostIdempotentAsync<JsonElement>("write-requests/result",
                new { action = op.Action, target = op.TaskId }, op.Key, ct);
            Session.ThrowIfChanged();
            string? state = answer.GetProperty("state").GetString();
            if (state == "not_recorded") { op.Stage = "not_recorded"; IsBlocked = false; return; }
            if (state != "completed" || answer.GetProperty("http_status").GetInt32() != (op.Action == "task.review_requested" ? 201 : 200)) throw Invalid();
            var saved = answer.GetProperty("result").Deserialize<ReviewRequestSaved>(ApiClient.Json) ?? throw Invalid();
            ValidateSaved(op, saved); op.Saved = saved; op.Stage = "completed"; IsBlocked = false;
        }
        catch (ApiException error) when (error.StatusCode == 403) { IsBlocked = true; throw; }
        finally { IsBusy = false; }
    }
    private void DemandSession()
    {
        Session.ThrowIfChanged();
        if (IsBlocked) throw new InvalidOperationException("검토 요청 권한이 변경되었습니다. 다시 로그인해 주세요.");
    }
    private static void ValidateSaved(ReviewRequestOperation op, ReviewRequestSaved saved)
    {
        Id(saved.RequestId);
        if (saved.TaskId != op.TaskId || saved.SubmissionId != op.SubmissionId || saved.RoundNo != op.RoundNo
            || (op.RequestId is not null && saved.RequestId != op.RequestId)
            || saved.RequestState != (op.Action == "task.review_requested" ? "pending" : "acknowledged")
            || saved.CreatedAt == default || saved.CreatedAt.Offset != TimeSpan.Zero
            || (saved.AcknowledgedAt is { } at && (at == default || at.Offset != TimeSpan.Zero))
            || (saved.RequestState == "acknowledged") != (saved.AcknowledgedAt is not null)) throw Invalid();
    }
    private static void Validate(ReviewRequestItem row)
    {
        if (row is null) throw Invalid();
        Id(row.RequestId); Id(row.TaskId); Id(row.SubmissionId); Id(row.RoundNo); Id(row.RowVersion);
        if (row.AssignmentRevision < 1 || row.RequestedBy < 1 || row.ReviewerId < 1 || row.Reason is null
            || row.Title is null || row.RequesterName is null || row.ReviewerName is null
            || row.State is not ("pending" or "acknowledged" or "resolved")
            || row.TaskStatus is not ("not_started" or "in_progress" or "submitted" or "returned" or "approved")
            || row.CreatedAt == default || row.CreatedAt.Offset != TimeSpan.Zero
            || (row.AcknowledgedAt is null) != (row.AcknowledgedBy is null)) throw Invalid();
    }
    private static string Id(string value)
    {
        if (string.IsNullOrEmpty(value) || value[0] == '0' || value.Any(c => c < '0' || c > '9')
            || !long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long n) || n < 1) throw Invalid();
        return value;
    }
    private static InvalidDataException Invalid() => new("검토 요청 응답 형식을 확인해 주세요.");
    public static string ErrorText(Exception error) => error switch
    {
        ApiException api => api.Message + " (" + api.Code + ")",
        OperationCanceledException => "접속이 변경되었거나 조회가 취소되었습니다. 다시 로그인해 주세요.",
        HttpRequestException => "서버에 연결하지 못했습니다. 전송한 요청은 처리 결과를 확인해 주세요.",
        ArgumentException or InvalidOperationException or InvalidDataException => error.Message,
        _ => "서버 응답을 확인하지 못했습니다. 처리 결과 확인 후 다시 진행해 주세요."
    };
    public static string StateText(string state) => state switch
    { "pending" => "확인 대기", "acknowledged" => "요청 확인", "resolved" => "검토 종료", _ => "상태 확인 필요" };
}
