namespace RemoteWork.WinForms.Features.Tasks.Queries;

/// <summary>
/// Read-only member 2 boundary. No HTTP endpoint, user ID type or authentication contract is assumed.
/// A future adapter must use member 1's authenticated context; the server must authorize both
/// list and direct detail requests. PreviewRole and requested scope cannot establish permission.
/// </summary>
public interface ITaskQueryService
{
    /// <summary>Visible origin of the data. Test doubles must clearly identify themselves.</summary>
    string SourceDescription { get; }

    Task<IReadOnlyList<TaskSummary>> GetTasksAsync(
        TaskListQuery query, CancellationToken cancellationToken = default);

    Task<TaskDetail> GetTaskAsync(
        string taskId, CancellationToken cancellationToken = default);
}
