namespace RemoteWork.WinForms.Features.Tasks.Queries;

/// <summary>The normal startup boundary until the common server adapter is agreed and connected.</summary>
public sealed class UnconfiguredTaskQueryService : ITaskQueryService
{
    public string SourceDescription => "업무 조회 연결 필요 · 실제 서버 연결 없음";

    public Task<IReadOnlyList<TaskSummary>> GetTasksAsync(
        TaskListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<IReadOnlyList<TaskSummary>>(
            new TaskQueryException(TaskQueryFailure.NotConfigured));
    }

    public Task<TaskDetail> GetTaskAsync(
        string taskId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromException<TaskDetail>(new TaskQueryException(TaskQueryFailure.NotConfigured));
    }
}
