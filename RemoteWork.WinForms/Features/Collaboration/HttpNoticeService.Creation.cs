using System.Diagnostics;
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration;

public sealed partial class HttpNoticeService
{
    private readonly SemaphoreSlim creationLane = new(1, 1);
    private readonly Dictionary<Guid, Creation> creations = [];
    private sealed class Creation(object body)
    {
        public readonly object Body = body;
        public readonly string Fingerprint = JsonSerializer.Serialize(body, ApiClient.Json);
        public readonly long Started = Stopwatch.GetTimestamp();
        public string? Key;
        public bool Sent;
        public NoticeCreationReceipt? Receipt;
    }
    private sealed record CreationKey(string RequestKey, DateTimeOffset ExpiresAt);
    private sealed record CreationLookup(string State, int? HttpStatus, NoticeCreationReceipt? Result, DateTimeOffset? ExpiresAt);

    public async Task<NoticeCreationReceipt> CreateOrRecoverNoticeAsync(CreateNoticeRequest request, CancellationToken ct = default)
    {
        if (request.OperationId == Guid.Empty) throw new ArgumentException("공지 원 요청 번호가 필요합니다.");
        var identity = AttachmentIdentity();
        var id = request.OperationId;
        var target = id.ToString("D");
        const string action = "notice.created";
        // Copy the arrays so caller mutations cannot alter a pending original request.
        object body = new { operation_id = target, request.Title, request.Content, request.Target,
            request.IsPinned, AttachmentNames = request.AttachmentNames.ToArray(), AttachmentIds = request.AttachmentIds.ToArray() };
        await creationLane.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            identity.ThrowIfChanged(); ct.ThrowIfCancellationRequested();
            if (!creations.TryGetValue(id, out var op))
            {
                if (creations.Count >= 64) throw new InvalidOperationException("기존 공지 저장 결과를 먼저 확인해 주세요. 현재 접속의 등록 한도입니다.");
                creations[id] = op = new(body);
            }
            if (op.Fingerprint != JsonSerializer.Serialize(body, ApiClient.Json))
                throw new InvalidOperationException("저장 결과 확인 중에는 원 요청 번호와 입력·첨부를 유지해 주세요.");
            if (Stopwatch.GetElapsedTime(op.Started) >= TimeSpan.FromHours(24))
                throw new ApiException("공지 원 요청의 결과 확인 기간이 지났습니다. 목록과 관리자 기록을 확인해 주세요.", 410, "NOTICE_OPERATION_EXPIRED");
            if (op.Receipt is not null) return op.Receipt;
            if (op.Sent)
            {
                var found = await identity.GetAsync<CreationLookup>("notices/operations/" + target, ct).ConfigureAwait(false);
                identity.ThrowIfChanged(); ct.ThrowIfCancellationRequested();
                if (found.State == "completed") return op.Receipt = ValidateCreationLookup(found, id);
                if (found.State != "not_recorded" || op.Key is null) throw InvalidCreation();
                var common = await identity.PostIdempotentAsync<CreationLookup>("write-requests/result", new { action, target }, op.Key, ct).ConfigureAwait(false);
                identity.ThrowIfChanged(); ct.ThrowIfCancellationRequested();
                if (common.State == "completed") return op.Receipt = ValidateCreationLookup(common, id);
                if (common.State != "not_recorded") throw InvalidCreation();
            }
            if (op.Key is null)
            {
                var issued = await identity.PostAsync<CreationKey>("write-requests/keys", new { action, target }, ct).ConfigureAwait(false);
                identity.ThrowIfChanged(); ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(issued.RequestKey) || issued.ExpiresAt == default || issued.ExpiresAt.Offset != TimeSpan.Zero)
                    throw InvalidCreation();
                op.Key = issued.RequestKey;
            }
            identity.ThrowIfChanged(); ct.ThrowIfCancellationRequested();
            op.Sent = true;
            var receipt = await identity.PostIdempotentAsync<NoticeCreationReceipt>("notices", op.Body, op.Key, ct).ConfigureAwait(false);
            identity.ThrowIfChanged(); ct.ThrowIfCancellationRequested();
            return op.Receipt = ValidateCreation(receipt, id);
        }
        finally { creationLane.Release(); }
    }

    private static NoticeCreationReceipt ValidateCreationLookup(CreationLookup lookup, Guid id)
    {
        if (lookup.HttpStatus != 201 || lookup.ExpiresAt is null || lookup.ExpiresAt == default(DateTimeOffset)
            || lookup.ExpiresAt.Value.Offset != TimeSpan.Zero) throw InvalidCreation();
        return ValidateCreation(lookup.Result, id);
    }
    private static NoticeCreationReceipt ValidateCreation(NoticeCreationReceipt? receipt, Guid id)
    {
        if (receipt is null || receipt.NoticeId <= 0 || receipt.OperationId != id
            || receipt.CreatedAt == default || receipt.CreatedAt.Offset != TimeSpan.Zero) throw InvalidCreation();
        return receipt;
    }
    private static ApiException InvalidCreation() => new("공지 저장 결과의 원 요청 번호·응답 형식을 확인할 수 없습니다. 같은 요청의 결과를 다시 확인해 주세요.");
}
