using System.Globalization;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration;

internal interface IChatParticipantDirectory
{
    Task<IReadOnlyList<ChatParticipant>> GetParticipantsAsync(CancellationToken ct = default);
}

// The shared ApiClient owns authentication, request cancellation and HTTP errors.
internal sealed class HttpChatParticipantDirectory : IChatParticipantDirectory
{
    private readonly ApiClient.Session session;
    private readonly int userId;
    public HttpChatParticipantDirectory(ApiClient api, int userId)
    {
        session = api.CaptureSession();
        if (session.User.Id != userId) throw new OperationCanceledException("로그인이 변경되었습니다.");
        this.userId = userId;
    }

    private sealed record ParticipantData(int Id, string DisplayName, string Department);

    public async Task<IReadOnlyList<ChatParticipant>> GetParticipantsAsync(CancellationToken ct = default)
    {
        session.ThrowIfChanged();
        var result = new List<ChatParticipant>();
        var ids = new HashSet<int>();
        int? expectedTotal = null;
        for (int page = 1; page <= 1_000_000; page++)
        {
            ct.ThrowIfCancellationRequested();
            var data = await session.GetAsync<PageResult<ParticipantData>>($"chat/participants?page={page}&limit=100", ct);
            session.ThrowIfChanged();
            if (data.Items is null || data.Page != page || data.Limit != 100 || data.Total < 0 ||
                data.Items.Count > 100 || (expectedTotal is not null && expectedTotal != data.Total))
                throw new ApiException("참여자 목록이 변경되었거나 응답이 올바르지 않습니다. 다시 조회해 주세요.");
            expectedTotal = data.Total;
            foreach (var item in data.Items)
            {
                if (item.Id <= 0 || item.Id == userId || !ids.Add(item.Id) ||
                    string.IsNullOrWhiteSpace(item.DisplayName) || item.Department is null)
                    throw new ApiException("참여자 응답 형식이 올바르지 않습니다.");
                result.Add(new ChatParticipant(item.Id.ToString(CultureInfo.InvariantCulture),
                    item.DisplayName, item.Department));
            }
            if (result.Count > data.Total || (result.Count < data.Total && data.Items.Count == 0))
                throw new ApiException("참여자 목록을 모두 받지 못했습니다. 다시 조회해 주세요.");
            if (result.Count == data.Total) return result.AsReadOnly();
        }
        throw new ApiException("참여자 목록의 페이지 범위를 초과했습니다.");
    }
}
