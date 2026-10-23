namespace RemoteWork.WinForms.Features.Collaboration;

public sealed record CreateNoticeRequest(string Title, string Content, string Target,
    bool IsPinned, string[] AttachmentNames)
{
    public Guid OperationId { get; init; } = Guid.NewGuid();
    public string[] AttachmentIds { get; init; } = Array.Empty<string>();
}

public sealed record NoticeCreationReceipt(int NoticeId, Guid OperationId, DateTimeOffset CreatedAt);

/// <summary>A repeated invocation explicitly checks the original save before retrying it.</summary>
public interface IRecoverableNoticeService
{
    Task<NoticeCreationReceipt> CreateOrRecoverNoticeAsync(CreateNoticeRequest request, CancellationToken ct = default);
}

public interface INoticeService
{
    Task<IReadOnlyList<Notice>> GetNoticesAsync(CancellationToken ct = default);
    Task<Notice> GetNoticeAsync(int id, CancellationToken ct = default);
    Task<Notice> CreateNoticeAsync(CreateNoticeRequest request, CancellationToken ct = default);
    Task DeleteNoticeAsync(int id, CancellationToken ct = default);
}
