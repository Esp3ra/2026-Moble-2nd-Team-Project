using System.Globalization;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Features.Tasks.Preparation;

namespace RemoteWork.WinForms.Features.Tasks.Queries;

/// <summary>
/// Member 2's read-only development contract. The shared client owns authentication,
/// transport and session cancellation; this adapter never substitutes sample data.
/// </summary>
public sealed class HttpTaskQueryService(ApiClient api) : ITaskQueryService, ITaskPreparationSource
{
    private readonly ApiClient api = api ?? throw new ArgumentNullException(nameof(api));

    public string SourceDescription => $"업무 조회 서버 · {api.ServerUri}";

    public ITaskPreparationService? CreatePreparationService()
    {
        if (api.CurrentUser is null) return null;
        try { return new HttpTaskPreparationService(api.CaptureSession()); }
        catch (InvalidOperationException) { return null; } // A concurrent logout won the capture.
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

        try
        {
            var result = await api.GetAsync<TaskListResponse>(path, cancellationToken);
            if (result.Items is null) throw InvalidResponse();
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            var summaries = new List<TaskSummary>(result.Items.Count);
            foreach (var item in result.Items)
            {
                TaskSummary summary = MapSummary(item);
                if (query.Status is { } requestedStatus && summary.Status != requestedStatus)
                    throw InvalidResponse();
                if (!identifiers.Add(summary.TaskId)) throw InvalidResponse();
                summaries.Add(summary);
            }
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
            var result = await api.GetAsync<TaskDetailResponse>(
                "tasks/" + Uri.EscapeDataString(taskId), cancellationToken);
            TaskSummary summary = MapSummary(result.Task);
            if (!string.Equals(summary.TaskId, taskId, StringComparison.Ordinal)
                || result.Task!.Description is null)
                throw InvalidResponse();
            return new TaskDetail(summary, result.Task.Description);
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
        return new TaskSummary(item.TaskId, item.Title, item.AssigneeName, status, startDate, dueDate);
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
        string? Description);
}
