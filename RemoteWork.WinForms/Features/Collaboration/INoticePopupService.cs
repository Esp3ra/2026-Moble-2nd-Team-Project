namespace RemoteWork.WinForms.Features.Collaboration;

public sealed record PendingNoticePopups(IReadOnlyList<Notice> Items, int? NextAfterId);

/// <summary>Separate popup contract so existing notice list/test services remain unchanged.</summary>
public interface INoticePopupService
{
    Task<PendingNoticePopups> GetPendingPopupsAsync(int afterId = 0, CancellationToken ct = default);
    Task ConfirmNoticeAsync(Notice notice, CancellationToken ct = default);
}
