namespace RemoteWork.WinForms.Features.Tasks.Queries.TestDoubles;

/// <summary>
/// Explicit in-memory read fixture. Own/Managed select supplied sets, not authenticated identities.
/// Filtering this fixture does not test or enforce server permissions. No data is mutated.
/// </summary>
public sealed class TaskQueryTestDouble : ITaskQueryService
{
    private readonly TaskDetail[] ownTasks;
    private readonly TaskDetail[] managedTasks;
    private readonly IReadOnlyDictionary<string, TaskDetail> details;

    public string SourceDescription => "테스트 대역 · 실제 서버 연결 아님";

    public TaskQueryTestDouble(IEnumerable<TaskDetail> tasks) : this(tasks, tasks) { }

    public TaskQueryTestDouble(IEnumerable<TaskDetail> ownTasks, IEnumerable<TaskDetail> managedTasks)
    {
        ArgumentNullException.ThrowIfNull(ownTasks);
        ArgumentNullException.ThrowIfNull(managedTasks);
        this.ownTasks = ownTasks.ToArray();
        this.managedTasks = managedTasks.ToArray();
        var byId = new Dictionary<string, TaskDetail>(StringComparer.Ordinal);
        foreach (var detail in this.ownTasks.Concat(this.managedTasks))
        {
            ArgumentNullException.ThrowIfNull(detail);
            ArgumentNullException.ThrowIfNull(detail.Summary);
            ArgumentException.ThrowIfNullOrWhiteSpace(detail.Summary.TaskId);
            if (byId.TryGetValue(detail.Summary.TaskId, out var previous) && previous != detail)
                throw new ArgumentException("같은 업무 ID에 서로 다른 테스트 상세를 지정할 수 없습니다.");
            byId[detail.Summary.TaskId] = detail;
        }
        details = byId;
    }

    public Task<IReadOnlyList<TaskSummary>> GetTasksAsync(
        TaskListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        var source = query.Scope switch
        {
            TaskQueryScope.Own => ownTasks,
            TaskQueryScope.Managed => managedTasks,
            _ => throw new ArgumentOutOfRangeException(nameof(query))
        };
        IReadOnlyList<TaskSummary> result = source
            .Select(detail => detail.Summary)
            .Where(summary => query.Status is null || summary.Status == query.Status)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<TaskDetail> GetTaskAsync(
        string taskId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        cancellationToken.ThrowIfCancellationRequested();
        return details.TryGetValue(taskId, out var detail)
            ? Task.FromResult(detail)
            : Task.FromException<TaskDetail>(new TaskQueryException(TaskQueryFailure.NotFound));
    }
}
