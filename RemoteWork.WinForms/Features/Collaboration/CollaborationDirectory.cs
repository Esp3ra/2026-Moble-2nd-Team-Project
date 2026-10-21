using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration;

public sealed record CollaborationRecipient(int UserId, string Name, string Department)
{
    public override string ToString() => $"{Name} · {Department} (ID {UserId})";
}

/// <summary>Existing authenticated reads only. No request saving or new permission.</summary>
public interface ICollaborationDirectory
{
    int CurrentUserId { get; }
    bool IsCurrent { get; }
    CancellationToken Cancellation { get; }
    Task<IReadOnlyList<TaskSummary>> GetOwnTasksAsync(CancellationToken ct);
    Task<IReadOnlyList<CollaborationRecipient>> GetRecipientsAsync(CancellationToken ct);
}

internal sealed class HttpCollaborationDirectory : ICollaborationDirectory
{
    private readonly ApiClient.Session identity;
    private readonly TaskReadSession tasks;
    private readonly HttpChatParticipantDirectory users;
    public HttpCollaborationDirectory(ApiClient api)
    {
        identity = api.CaptureSession();
        tasks = new HttpTaskQueryService(api).CaptureReadSession();
        users = new HttpChatParticipantDirectory(api, identity.User.Id);
        identity.ThrowIfChanged();
    }
    public int CurrentUserId => identity.User.Id;
    public bool IsCurrent => identity.IsCurrent && tasks.IsCurrent;
    public CancellationToken Cancellation => identity.Cancellation;
    public Task<IReadOnlyList<TaskSummary>> GetOwnTasksAsync(CancellationToken ct) =>
        tasks.GetTasksAsync(new(TaskQueryScope.Own, TaskDisplayStatus.InProgress), ct);
    public async Task<IReadOnlyList<CollaborationRecipient>> GetRecipientsAsync(CancellationToken ct)
    {
        identity.ThrowIfChanged();
        var people = await users.GetParticipantsAsync(ct).ConfigureAwait(false);
        identity.ThrowIfChanged();
        return people.Select(p => new CollaborationRecipient(
            int.Parse(p.Id, CultureInfo.InvariantCulture), p.Name, p.Department)).ToArray();
    }
}
