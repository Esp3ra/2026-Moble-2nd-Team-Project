namespace RemoteWork.WinForms.Features.Collaboration;

public sealed record CreateNoticeRequest(string Title, string Content, string Target,
    bool IsPinned, string[] AttachmentNames)
{
    public string[] AttachmentIds { get; init; } = Array.Empty<string>();
}

public interface INoticeService
{
    Task<IReadOnlyList<Notice>> GetNoticesAsync(CancellationToken ct = default);
    Task<Notice> GetNoticeAsync(int id, CancellationToken ct = default);
    Task<Notice> CreateNoticeAsync(CreateNoticeRequest request, CancellationToken ct = default);
    Task DeleteNoticeAsync(int id, CancellationToken ct = default);
}
