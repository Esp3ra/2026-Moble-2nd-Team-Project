using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration;

/// <summary>Uses the shared authenticated client; never falls back to sample data.</summary>
public sealed class HttpNoticeService(ApiClient api) : INoticeService, INoticePopupService
{
    private sealed record NoticeData(int Id, string Title, string Content, string Target,
        bool IsPinned, int AuthorId, string Author, DateTimeOffset CreatedAt, string[] AttachmentNames,
        string? PopupKey);
    private sealed record NoticeResult(NoticeData Notice);
    private ApiClient.Session? popupSession;
    private ApiClient.Session? writeSession;
    internal ApiClient.Session AttachmentIdentity()
    {
        var identity = writeSession ??= api.CaptureSession();
        identity.ThrowIfChanged(); return identity;
    }
    private ApiClient.Session PopupIdentity()
    {
        var identity = popupSession ??= api.CaptureSession();
        identity.ThrowIfChanged();
        return identity;
    }

    private sealed record PendingResult(List<NoticeData> Items, int? NextAfterId);

    private static Notice Map(NoticeData? data)
    {
        if (data is null || data.Id <= 0 || data.AuthorId <= 0 ||
            string.IsNullOrWhiteSpace(data.Title) || string.IsNullOrWhiteSpace(data.Content) ||
            string.IsNullOrWhiteSpace(data.Author) || data.CreatedAt == default ||
            !new[] { "전 직원", "기획팀", "개발팀", "디자인팀" }.Contains(data.Target) ||
            data.AttachmentNames is null || data.AttachmentNames.Any(string.IsNullOrWhiteSpace))
            throw new ApiException("공지사항 응답 형식이 올바르지 않습니다.");
        return new Notice
        {
            Id = data.Id, Title = data.Title, Content = data.Content, Target = data.Target,
            IsPinned = data.IsPinned, AuthorId = data.AuthorId, Author = data.Author,
            CreatedAt = data.CreatedAt.LocalDateTime, AttachmentNames = data.AttachmentNames,
            PopupKey = data.PopupKey ?? ""
        };
    }

    public async Task<IReadOnlyList<Notice>> GetNoticesAsync(CancellationToken ct = default)
    {
        var result = new List<Notice>();
        // Read all pages so existing client-side filters/sorts cover the whole list.
        for (int page = 1; ; page++)
        {
            var data = await api.GetAsync<PageResult<NoticeData>>($"notices?page={page}&limit=100", ct);
            if (data.Items is null || data.Page != page || data.Limit != 100 || data.Total < 0 ||
                data.Items.Count > 100)
                throw new ApiException("공지사항 목록 응답 형식이 올바르지 않습니다.");
            result.AddRange(data.Items.Select(Map));
            if (result.Count >= data.Total) return result.DistinctBy(n => n.Id).ToArray();
            if (data.Items.Count == 0)
                throw new ApiException("공지사항 목록을 모두 받지 못했습니다. 다시 조회해 주세요.");
        }
    }

    public async Task<Notice> GetNoticeAsync(int id, CancellationToken ct = default)
    {
        var data = await api.GetAsync<NoticeResult>($"notices/{id}", ct);
        var notice = Map(data.Notice);
        if (notice.Id != id) throw new ApiException("선택한 공지사항과 서버 응답이 일치하지 않습니다.");
        return notice;
    }

    public async Task<Notice> CreateNoticeAsync(CreateNoticeRequest request, CancellationToken ct = default)
    {
        var result = await AttachmentIdentity().PostAsync<NoticeResult>("notices", request, ct);
        return Map(result.Notice);
    }

    public async Task DeleteNoticeAsync(int id, CancellationToken ct = default) =>
        await api.PostAsync<MessageResult>($"notices/{id}/delete", new { }, ct);

    public async Task<PendingNoticePopups> GetPendingPopupsAsync(int afterId = 0, CancellationToken ct = default)
    {
        var identity = PopupIdentity();
        var data = await identity.GetAsync<PendingResult>($"notices/popups/pending?after_id={afterId}", ct);
        identity.ThrowIfChanged();
        if (data.Items is null || data.Items.Count > 20)
            throw new ApiException("공지 팝업 목록 응답이 올바르지 않습니다.");
        var notices = data.Items.Select(Map).ToArray();
        if (notices.Any(n => string.IsNullOrWhiteSpace(n.PopupKey) || n.Id <= afterId || !n.IsPinned) ||
            !notices.Select(n => n.Id).SequenceEqual(notices.Select(n => n.Id).Distinct().OrderBy(id => id)) ||
            (data.NextAfterId is int next && (notices.Length != 20 || next != notices[^1].Id)))
            throw new ApiException("공지 팝업 목록의 순서 또는 식별 정보가 올바르지 않습니다.");
        return new PendingNoticePopups(notices, data.NextAfterId);
    }

    public async Task ConfirmNoticeAsync(Notice notice, CancellationToken ct = default) =>
        await PopupIdentity().PostAsync<MessageResult>($"notices/{notice.Id}/confirm", new { popup_key = notice.PopupKey }, ct);
}
