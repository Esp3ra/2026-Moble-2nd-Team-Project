using System.Globalization;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Creation;
using RemoteWork.WinForms.Features.Tasks.Starting;
using RemoteWork.WinForms.Features.Tasks.Management;
using RemoteWork.WinForms.Features.Tasks.Lifecycle;

namespace RemoteWork.WinForms.Features.Tasks.Queries;

/// <summary>
/// Member 2's read-only development contract. The shared client owns authentication,
/// transport and session cancellation; this adapter never substitutes sample data.
/// </summary>
public sealed class HttpTaskQueryService : ITaskQueryService, ITaskPreparationSource, ITaskCreationSource, ITaskStartSource, ITaskManagementSource, ITaskLifecycleSource
{
    private readonly ApiClient api;
    private readonly ApiClient.Session? session;

    public HttpTaskQueryService(ApiClient api) : this(api, null) { }

    private HttpTaskQueryService(ApiClient api, ApiClient.Session? session)
    {
        this.api = api ?? throw new ArgumentNullException(nameof(api));
        this.session = session;
    }

    public string SourceDescription => $"업무 조회 서버 · {api.ServerUri}";

    /// <summary>
    /// Borrow task reads for this login only. Call explicitly after login;
    /// an old reader cannot attach itself to a later account or server.
    /// This does not start polling or provide any write capabilities.
    /// </summary>
    public TaskReadSession CaptureReadSession()
    {
        ApiClient.Session captured = session ?? api.CaptureSession();
        captured.ThrowIfChanged();
        string source = SourceDescription;
        var queries = new HttpTaskQueryService(api, captured);
        captured.ThrowIfChanged();
        return new TaskReadSession(captured, queries, source);
    }

    // A page captures all its optional task services together. The public adapter
    // remains reusable after login, while this view's adapter cannot move to it.
    internal TaskQuerySession? CaptureViewSession()
    {
        session?.ThrowIfChanged();
        ApiClient.Session captured;
        try { captured = session ?? api.CaptureSession(); }
        catch (InvalidOperationException) { return null; }
        var query = new HttpTaskQueryService(api, captured);
        var preparation = new HttpTaskPreparationService(captured);
        TaskCreationWorkflow? creation = query.GetCreationWorkflow();
        captured.ThrowIfChanged();
        return new(captured, query, preparation, creation);
    }

    public ITaskPreparationService? CreatePreparationService()
    {
        if (session is not null)
        {
            session.ThrowIfChanged();
            var preparation = new HttpTaskPreparationService(session);
            session.ThrowIfChanged();
            return preparation;
        }
        if (api.CurrentUser is null) return null;
        try { return new HttpTaskPreparationService(api.CaptureSession()); }
        catch (InvalidOperationException) { return null; } // A concurrent logout won the capture.
    }

    public TaskCreationWorkflow? GetCreationWorkflow() =>
        InCurrentSession(() => TaskCreationSessionStore.Get(api));

    public TaskStartWorkflow? GetStartWorkflow(TaskStartKind kind, string taskId) =>
        InCurrentSession(() => TaskStartSessionStore.Get(api, kind, taskId));

    public TaskStartWorkflow? GetPendingStartWorkflow() =>
        InCurrentSession(() => TaskStartSessionStore.FindPending(api));

    public TaskManagementWorkflow? GetManagementWorkflow(TaskManagementKind kind, string taskId) =>
        InCurrentSession(() => TaskManagementSessionStore.Get(api, kind, taskId));

    public TaskManagementWorkflow? GetPendingManagementWorkflow() =>
        InCurrentSession(() => TaskManagementSessionStore.FindPending(api));

    public ITaskLifecycleService? CreateLifecycleService()
    {
        if (session is not null) { session.ThrowIfChanged(); return new HttpTaskLifecycleService(session); }
        try { return new HttpTaskLifecycleService(api.CaptureSession()); }
        catch (InvalidOperationException) { return null; }
    }
    public TaskLifecycleWorkflow? GetLifecycleWorkflow(TaskLifecycleKind kind, string taskId, string? extensionId = null) =>
        InCurrentSession(() => TaskLifecycleSessionStore.Get(api, kind, taskId, extensionId));
    public TaskLifecycleWorkflow? GetPendingLifecycleWorkflow() =>
        InCurrentSession(() => TaskLifecycleSessionStore.FindPending(api));

    private T InCurrentSession<T>(Func<T> get)
    {
        session?.ThrowIfChanged();
        T value = get();
        session?.ThrowIfChanged();
        return value;
    }

    private ApiClient.Session? CaptureRequestSession()
    {
        if (session is not null)
        {
            session.ThrowIfChanged();
            return session;
        }
        // Direct anonymous callers remain useful for explicit HTTP doubles. A
        // logged-in request must keep the identity it started with throughout DTO mapping.
        if (api.CurrentUser is null) return null;
        try { return api.CaptureSession(); }
        catch (InvalidOperationException)
        { throw new OperationCanceledException("이전 접속의 업무 조회를 취소했습니다."); }
    }

    public async Task<IReadOnlyList<TaskSummary>> GetTasksAsync(
        TaskListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        string scope = query.Scope switch
        {
            TaskQueryScope.Own => "own",
            TaskQueryScope.Managed => "managed",
            _ => throw new ArgumentOutOfRangeException(nameof(query))
        };
        string path = "tasks?scope=" + scope;
        if (query.Status is { } status) path += "&status=" + StatusCode(status);
        if (query.IncludeCancelled) path += "&include_cancelled=true";

        try
        {
            ApiClient.Session? requestSession = CaptureRequestSession();
            var result = requestSession is null
                ? await api.GetAsync<TaskListResponse>(path, cancellationToken)
                : await requestSession.GetAsync<TaskListResponse>(path, cancellationToken);
            if (result.Items is null) throw InvalidResponse();
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            var summaries = new List<TaskSummary>(result.Items.Count);
            foreach (var item in result.Items)
            {
                TaskSummary summary = MapSummary(item);
                if (query.Status is { } requestedStatus && summary.Status != requestedStatus)
                    throw InvalidResponse();
                if (!query.IncludeCancelled && summary.IsCancelled) throw InvalidResponse();
                if (!identifiers.Add(summary.TaskId)) throw InvalidResponse();
                summaries.Add(summary);
            }
            requestSession?.ThrowIfChanged();
            return summaries;
        }
        catch (ApiException error) { throw MapFailure(error); }
    }

    public async Task<TaskDetail> GetTaskAsync(
        string taskId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            ApiClient.Session? requestSession = CaptureRequestSession();
            string path = "tasks/" + Uri.EscapeDataString(taskId);
            var result = requestSession is null
                ? await api.GetAsync<TaskDetailResponse>(path, cancellationToken)
                : await requestSession.GetAsync<TaskDetailResponse>(path, cancellationToken);
            TaskSummary summary = MapSummary(result.Task);
            if (!string.Equals(summary.TaskId, taskId, StringComparison.Ordinal)
                || result.Task!.Description is null)
                throw InvalidResponse();
            var detail = new TaskDetail(summary, result.Task.Description);
            requestSession?.ThrowIfChanged();
            return detail;
        }
        catch (ApiException error) { throw MapFailure(error); }
    }

    private static TaskSummary MapSummary(TaskResponse? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.TaskId)
            || string.IsNullOrWhiteSpace(item.Title) || item.AssigneeId is not > 0
            || string.IsNullOrWhiteSpace(item.AssigneeName)
            || !DateOnly.TryParseExact(item.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var startDate)
            || !DateOnly.TryParseExact(item.DueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dueDate) || dueDate < startDate)
            throw InvalidResponse();

        TaskDisplayStatus status = item.TaskStatus switch
        {
            "not_started" => TaskDisplayStatus.NotStarted,
            "in_progress" => TaskDisplayStatus.InProgress,
            "submitted" => TaskDisplayStatus.Submitted,
            "approved" => TaskDisplayStatus.Approved,
            "returned" => TaskDisplayStatus.Returned,
            _ => throw InvalidResponse()
        };
        return new TaskSummary(item.TaskId, item.Title, item.AssigneeName, status, startDate, dueDate, item.IsCancelled);
    }

    private static string StatusCode(TaskDisplayStatus status) => status switch
    {
        TaskDisplayStatus.NotStarted => "not_started",
        TaskDisplayStatus.InProgress => "in_progress",
        TaskDisplayStatus.Submitted => "submitted",
        TaskDisplayStatus.Approved => "approved",
        TaskDisplayStatus.Returned => "returned",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    private static TaskQueryException MapFailure(ApiException error) => new(error.StatusCode switch
    {
        401 => TaskQueryFailure.AuthenticationExpired,
        403 => TaskQueryFailure.Forbidden,
        404 when error.Code == "TASK_NOT_FOUND" => TaskQueryFailure.NotFound,
        404 when error.Code == "HTTP_404" => TaskQueryFailure.NotConfigured,
        _ => TaskQueryFailure.Unavailable
    }, innerException: error);

    private static TaskQueryException InvalidResponse() => new(TaskQueryFailure.Unavailable);

    // Nullable wire fields are checked explicitly before constructing non-null UI models.
    private sealed record TaskListResponse(List<TaskResponse?>? Items);
    private sealed record TaskDetailResponse(TaskResponse? Task);
    private sealed record TaskResponse(string? TaskId, string? Title, int? AssigneeId,
        string? AssigneeName, string? TaskStatus, string? StartDate, string? DueDate,
        string? Description, bool IsCancelled = false);
}
