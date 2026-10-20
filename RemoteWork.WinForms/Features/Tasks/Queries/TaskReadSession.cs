using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.Queries;

/// <summary>
/// Borrowed, read-only task access bound to one captured login. A consumer may
/// observe cancellation and currentness, but these are not server permission
/// evidence. This facade does not own or dispose the shared authenticated client.
/// </summary>
public sealed class TaskReadSession : ITaskQueryService
{
    private readonly ApiClient.Session session;
    private readonly HttpTaskQueryService queries;

    internal TaskReadSession(ApiClient.Session session, HttpTaskQueryService queries, string sourceDescription)
    {
        this.session = session;
        this.queries = queries;
        SourceDescription = sourceDescription;
    }

    /// <summary>The origin captured at creation; never follows a subsequent server/account change.</summary>
    public string SourceDescription { get; }
    public bool IsCurrent => session.IsCurrent;
    public CancellationToken Cancellation => session.Cancellation;
    public void ThrowIfChanged() => session.ThrowIfChanged();

    public async Task<IReadOnlyList<TaskSummary>> GetTasksAsync(
        TaskListQuery query, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        session.ThrowIfChanged();
        IReadOnlyList<TaskSummary> result = await queries.GetTasksAsync(query, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        session.ThrowIfChanged();
        return result;
    }

    /// <summary>
    /// Detail is authorized for the current assignee or designated manager. It
    /// does not prove membership in a particular own/managed list scope.
    /// </summary>
    public async Task<TaskDetail> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        session.ThrowIfChanged();
        TaskDetail result = await queries.GetTaskAsync(taskId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        session.ThrowIfChanged();
        return result;
    }
}
