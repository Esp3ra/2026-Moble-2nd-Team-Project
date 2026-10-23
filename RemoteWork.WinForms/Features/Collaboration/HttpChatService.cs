using System.Globalization;
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration;

// The authenticated path persists rooms and messages on the configured server DB.
internal sealed class HttpChatService : IChatService
{
    private readonly ApiClient.Session session;
    private readonly int userId;
    private readonly IChatParticipantDirectory directory;
    private readonly object pendingLock = new();
    private (string Key, string Id)? pendingRoom;
    private readonly Dictionary<string, (string Content, string Id)> pendingMessages = new();
    internal bool HasPendingMessage(string room) { lock (pendingLock) return pendingMessages.ContainsKey(room); }
    internal ResourceAttachmentClient Attachments => attachmentClient ??= new ResourceAttachmentClient(Identity());
    private ResourceAttachmentClient? attachmentClient;
    public ChatParticipant CurrentUser { get; }
    internal bool IsSessionCurrent => session.IsCurrent;

    private sealed record ParticipantData(int Id, string Name, string Department);
    private sealed record RoomData(string Id, string Name, bool? IsGroup, ParticipantData[] Participants);
    private sealed record MessageData(Guid Id, string RoomId, int SenderId, string SenderName,
        string Content, DateTimeOffset SentAt);
    private sealed record RoomResult(RoomData Room);
    private sealed record MessageResultData(MessageData Message);
    private sealed record CursorPage<T>(T[] Items, string? NextCursor);

    public HttpChatService(ApiClient api, UserInfo user)
    {
        session = api.CaptureSession();
        if (session.User.Id != user.Id) throw new OperationCanceledException("로그인이 변경되었습니다.");
        userId = user.Id;
        CurrentUser = new ChatParticipant(user.Id.ToString(CultureInfo.InvariantCulture), user.DisplayName, user.Department);
        directory = new HttpChatParticipantDirectory(api, user.Id);
        session.ThrowIfChanged();
    }

    private ApiClient.Session Identity()
    {
        session.ThrowIfChanged();
        return session;
    }

    private static void SameIdentity(ApiClient.Session identity) => identity.ThrowIfChanged();

    private static string RoomId(string id)
    {
        if (!Guid.TryParse(id, out var parsed) || parsed.ToString("D") != id)
            throw new ApiException("대화방 식별자가 올바르지 않습니다.");
        return id;
    }

    private ChatRoom Map(RoomData? room)
    {
        if (room is null || string.IsNullOrWhiteSpace(room.Name) || room.IsGroup is null || room.Participants is null
            || room.Participants.Length < (room.IsGroup == true ? 3 : 2)
            || (room.IsGroup == false && room.Participants.Length != 2)
            || room.Participants.Any(p => p is null)
            || !room.Participants.Any(p => p.Id == userId)
            || room.Participants.Select(p => p.Id).Distinct().Count() != room.Participants.Length
            || room.Participants.Any(p => p.Id <= 0 || string.IsNullOrWhiteSpace(p.Name) || p.Department is null))
            throw new ApiException("대화방 응답 형식이 올바르지 않습니다.");
        return new ChatRoom(RoomId(room.Id), room.Name, room.IsGroup.Value,
            Array.AsReadOnly(room.Participants.Select(p => new ChatParticipant(
                p.Id.ToString(CultureInfo.InvariantCulture), p.Name, p.Department)).ToArray()));
    }

    private static ChatMessage Map(MessageData? message, string roomId)
    {
        if (message is null || message.Id == Guid.Empty || message.RoomId != roomId || message.SenderId <= 0
            || string.IsNullOrWhiteSpace(message.SenderName) || string.IsNullOrWhiteSpace(message.Content)
            || !ChatText.WithinLimit(message.Content, 2000) || message.SentAt == default)
            throw new ApiException("메시지 응답 형식이 올바르지 않습니다.");
        return new ChatMessage(message.Id, roomId, message.SenderId.ToString(CultureInfo.InvariantCulture),
            message.SenderName, message.Content, message.SentAt);
    }

    public Task<IReadOnlyList<ChatParticipant>> GetAvailableParticipantsAsync(CancellationToken cancellationToken = default)
        => directory.GetParticipantsAsync(cancellationToken);

    public async Task<IReadOnlyList<ChatRoom>> GetRoomsAsync(CancellationToken cancellationToken = default)
    {
        var identity = Identity();
        var rooms = new List<ChatRoom>();
        string? cursor = null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = "chat/rooms?limit=100" + (cursor is null ? "" : "&after=" + cursor);
            var page = await identity.GetAsync<CursorPage<RoomData>>(path, cancellationToken);
            SameIdentity(identity);
            if (page.Items is null || page.Items.Length > 100)
                throw new ApiException("대화방 목록 응답이 올바르지 않습니다.");
            string? previous = cursor;
            foreach (var item in page.Items)
            {
                var room = Map(item);
                if (!ids.Add(room.Id) || (previous is not null && string.CompareOrdinal(previous, room.Id) >= 0))
                    throw new ApiException("대화방 목록이 변경되었습니다. 다시 조회해 주세요.");
                rooms.Add(room);
                previous = room.Id;
            }
            if (page.NextCursor is null) return rooms.AsReadOnly();
            if (page.Items.Length != 100 || previous != page.NextCursor)
                throw new ApiException("대화방 목록을 모두 받지 못했습니다.");
            cursor = RoomId(page.NextCursor);
        }
    }

    public async Task<ChatRoom> CreateRoomAsync(CreateChatRoomRequest request, CancellationToken cancellationToken = default)
    {
        var identity = Identity();
        if (request.ParticipantIds is null || request.ParticipantIds.Any(id => !int.TryParse(id, NumberStyles.None,
                CultureInfo.InvariantCulture, out int n) || n <= 0 || n == userId))
            throw new ArgumentException("참여자를 다시 선택해 주세요.");
        int[] ids = request.ParticipantIds.Select(id => int.Parse(id, CultureInfo.InvariantCulture)).Order().ToArray();
        string title = request.Title.Trim();
        string key = JsonSerializer.Serialize(new { title, request.IsGroup, ids });
        string id;
        lock (pendingLock)
        {
            if (pendingRoom is null || pendingRoom.Value.Key != key)
                pendingRoom = (key, Guid.NewGuid().ToString("D"));
            id = pendingRoom.Value.Id;
        }
        try
        {
            var response = await identity.PostAsync<RoomResult>("chat/rooms", new { room_id = id, title,
                is_group = request.IsGroup, participant_ids = ids }, cancellationToken);
            SameIdentity(identity);
            var room = Map(response.Room);
            if (room.Id != id || room.Name != title || room.IsGroup != request.IsGroup
                || !room.Participants.Select(p => int.Parse(p.Id, CultureInfo.InvariantCulture)).Order()
                    .SequenceEqual(ids.Append(userId).Order()))
                throw new ApiException("저장된 대화방이 요청 내용과 다릅니다. 목록을 다시 확인해 주세요.");
            lock (pendingLock) if (pendingRoom?.Id == id) pendingRoom = null;
            return room;
        }
        catch (ApiException ex) when (ex.StatusCode >= 400 && ex.StatusCode < 500)
        {
            lock (pendingLock) if (pendingRoom?.Id == id) pendingRoom = null;
            throw;
        }
    }

    public async Task DeleteRoomAsync(string roomId, CancellationToken cancellationToken = default)
    {
        var identity = Identity();
        await identity.PostAsync<MessageResult>($"chat/rooms/{RoomId(roomId)}/hide", new { }, cancellationToken);
        SameIdentity(identity);
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string roomId, CancellationToken cancellationToken = default)
    {
        RoomId(roomId);
        var identity = Identity();
        var messages = new List<ChatMessage>();
        var ids = new HashSet<Guid>();
        string cursor = "0";
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await identity.GetAsync<CursorPage<MessageData>>($"chat/rooms/{roomId}/messages?limit=100&after={cursor}", cancellationToken);
            SameIdentity(identity);
            if (page.Items is null || page.Items.Length > 100)
                throw new ApiException("메시지 목록 응답이 올바르지 않습니다.");
            foreach (var item in page.Items)
            {
                var message = Map(item, roomId);
                if (!ids.Add(message.Id)) throw new ApiException("중복된 메시지 응답입니다. 다시 조회해 주세요.");
                messages.Add(message);
            }
            if (page.NextCursor is null) return messages.AsReadOnly();
            if (page.Items.Length != 100 || !long.TryParse(page.NextCursor, NumberStyles.None, CultureInfo.InvariantCulture, out long next)
                || next <= long.Parse(cursor, CultureInfo.InvariantCulture))
                throw new ApiException("메시지 목록을 모두 받지 못했습니다.");
            cursor = page.NextCursor;
        }
    }

    public Task<ChatMessage> SendMessageAsync(string roomId, string content, CancellationToken cancellationToken = default)
        => SendMessageWithFilesAsync(roomId, content, Array.Empty<string>(), cancellationToken);

    internal async Task<ChatMessage> SendMessageWithFilesAsync(string roomId, string content, string[] attachmentIds, CancellationToken cancellationToken = default)
    {
        RoomId(roomId);
        var identity = Identity();
        string text = content.Trim();
        if (text.Length == 0 || !ChatText.WithinLimit(text, 2000)) throw new ArgumentException("메시지는 1~2000자로 입력해 주세요.");
        string requestContent = JsonSerializer.Serialize(new { text, attachmentIds });
        string id;
        lock (pendingLock)
        {
            if (pendingMessages.TryGetValue(roomId, out var old) && old.Content != requestContent)
                throw new ApiException("이전 메시지 결과가 불명확합니다. 원래 내용·첨부로 결과를 확인해 주세요.");
            if (!pendingMessages.TryGetValue(roomId, out var pending))
                pendingMessages[roomId] = (requestContent, Guid.NewGuid().ToString("D"));
            id = pendingMessages[roomId].Id;
        }
        try
        {
            object body = attachmentIds.Length == 0 ? new { message_id = id, content = text }
                : new { message_id = id, content = text, attachment_ids = attachmentIds };
            var response = await identity.PostAsync<MessageResultData>($"chat/rooms/{roomId}/messages", body, cancellationToken);
            SameIdentity(identity);
            var message = Map(response.Message, roomId);
            if (message.Id.ToString("D") != id || message.SenderId != CurrentUser.Id || message.Content != text)
                throw new ApiException("저장된 메시지가 요청 내용과 다릅니다. 대화를 다시 확인해 주세요.");
            lock (pendingLock) if (pendingMessages.TryGetValue(roomId, out var pending) && pending.Id == id) pendingMessages.Remove(roomId);
            return message;
        }
        catch (ApiException ex) when (ex.StatusCode == 400)
        {
            lock (pendingLock) if (pendingMessages.TryGetValue(roomId, out var pending) && pending.Id == id) pendingMessages.Remove(roomId);
            throw;
        }
    }
}
