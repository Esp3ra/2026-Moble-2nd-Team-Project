namespace RemoteWork.WinForms.Features.Tasks.Queries;

/// <summary>
/// Read-only, transport-independent member 2 boundary. HttpTaskQueryService reuses
/// member 1's authenticated client; the server authorizes list and direct detail requests.
/// PreviewRole and requested scope cannot establish permission.
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
