namespace RemoteWork.WinForms.Features.Collaboration;

// 방과 메시지는 메모리에 보관합니다. 실제 로그인 경로의 초대 목록만 서버에서 조회합니다.
internal sealed class InMemoryChatService : IChatService
{
    public const int MaxMessageLength = 2000;
    public const int MaxRoomTitleLength = 60;
    private readonly object sync = new();
    private readonly List<ChatRoom> rooms;
    private IReadOnlyList<ChatParticipant> availableParticipants;
    private readonly IChatParticipantDirectory? participantDirectory;
    private readonly Dictionary<string, List<ChatMessage>> messages;

    public ChatParticipant CurrentUser { get; }

    public InMemoryChatService(ChatParticipant currentUser)
    {
        CurrentUser = currentUser;
        var lee = new ChatParticipant("sample-lee", "이서연", "기획팀");
        var kim = new ChatParticipant("sample-kim", "김민수", "개발팀");
        var park = new ChatParticipant("sample-park", "박지우", "디자인팀");
        var choi = new ChatParticipant("sample-choi", "최유진", "관리자");
        availableParticipants = Array.AsReadOnly(new[] { lee, kim, park, choi });
        rooms = new List<ChatRoom>
        {
            new ChatRoom("planning", "기획팀 업무 공유", true, Array.AsReadOnly(new[] { currentUser, lee, kim, park })),
            new ChatRoom("private-kim", "김민수", false, Array.AsReadOnly(new[] { currentUser, kim })),
            new ChatRoom("development", "개발팀 협업", true, Array.AsReadOnly(new[] { currentUser, kim, choi }))
        };
        messages = rooms.ToDictionary(room => room.Id, _ => new List<ChatMessage>());
        AddSample("planning", lee, "오늘 일정과 검토할 업무를 공유합니다.", 9, 10);
        AddSample("planning", kim, "업무 처리 화면의 기본 구성을 검토하겠습니다.", 9, 15);
        AddSample("planning", park, "완료 후 검토 내용을 공유하겠습니다.", 9, 18);
        AddSample("private-kim", kim, "안녕하세요. 업무 관련 내용을 이 대화방에서 확인해 주세요.", 10, 0);
        AddSample("development", kim, "개발 진행 상황과 확인할 내용을 공유해 주세요.", 10, 30);
    }

    // Authenticated screens use real candidates and start with no sample rooms.
    public InMemoryChatService(ChatParticipant currentUser, IChatParticipantDirectory participantDirectory)
    {
        CurrentUser = currentUser;
        this.participantDirectory = participantDirectory;
        availableParticipants = Array.Empty<ChatParticipant>();
        rooms = new List<ChatRoom>();
        messages = new Dictionary<string, List<ChatMessage>>();
    }

    public Task<IReadOnlyList<ChatRoom>> GetRoomsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
            return Task.FromResult<IReadOnlyList<ChatRoom>>(Array.AsReadOnly(rooms.ToArray()));
    }

    public async Task<IReadOnlyList<ChatParticipant>> GetAvailableParticipantsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (participantDirectory is null) return availableParticipants;
        var loaded = await participantDirectory.GetParticipantsAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            availableParticipants = Array.AsReadOnly(loaded.ToArray());
            return availableParticipants;
        }
    }

    public async Task<ChatRoom> CreateRoomAsync(CreateChatRoomRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > MaxRoomTitleLength
            || request.Title.Any(char.IsControl))
            throw new ArgumentException($"대화방 제목은 줄바꿈 없이 1~{MaxRoomTitleLength}자로 입력해 주세요.");
        if (request.ParticipantIds is null)
            throw new ArgumentException("참여자를 선택해 주세요.");
        string[] ids = request.ParticipantIds.Distinct(StringComparer.Ordinal).ToArray();
        if ((!request.IsGroup && ids.Length != 1) || (request.IsGroup && ids.Length < 2))
            throw new ArgumentException(request.IsGroup ? "그룹 대화는 상대방을 2명 이상 선택해 주세요."
                : "개인 대화는 상대방을 1명 선택해 주세요.");

        // Recheck active candidates before creating the still-local room.
        if (participantDirectory is not null)
            await GetAvailableParticipantsAsync(cancellationToken);
        var participants = new List<ChatParticipant> { CurrentUser };
        foreach (string id in ids)
        {
            var participant = availableParticipants.FirstOrDefault(p => p.Id == id);
            if (participant is null) throw new ArgumentException("선택할 수 없는 참여자가 포함되어 있습니다.");
            participants.Add(participant);
        }

        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var room = new ChatRoom($"room-{Guid.NewGuid():N}", request.Title.Trim(), request.IsGroup,
                participants.AsReadOnly());
            rooms.Add(room);
            messages.Add(room.Id, new List<ChatMessage>());
            return room;
        }
    }

    public Task DeleteRoomAsync(string roomId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var room = rooms.FirstOrDefault(candidate => candidate.Id == roomId);
            if (room is null) throw new ArgumentException("존재하지 않는 대화방입니다.", nameof(roomId));
            // 방과 대화를 함께 정리하여 삭제한 방의 기록이 남지 않게 합니다.
            rooms.Remove(room);
            messages.Remove(roomId);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string roomId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (sync)
        {
            if (!messages.TryGetValue(roomId, out var history))
                throw new ArgumentException("존재하지 않는 대화방입니다.", nameof(roomId));
            // 복사본을 반환하여 화면에서 저장소의 목록을 직접 바꾸지 못하게 합니다.
            return Task.FromResult<IReadOnlyList<ChatMessage>>(Array.AsReadOnly(history.ToArray()));
        }
    }

    public Task<ChatMessage> SendMessageAsync(string roomId, string content, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("메시지를 입력해 주세요.", nameof(content));
        if (content.Length > MaxMessageLength)
            throw new ArgumentException($"메시지는 {MaxMessageLength:N0}자까지 입력할 수 있습니다.", nameof(content));
        lock (sync)
        {
            if (!messages.TryGetValue(roomId, out var history))
                throw new ArgumentException("존재하지 않는 대화방입니다.", nameof(roomId));
            var message = new ChatMessage(Guid.NewGuid(), roomId, CurrentUser.Id, CurrentUser.Name,
                content.Trim(), DateTimeOffset.Now);
            history.Add(message);
            return Task.FromResult(message);
        }
    }

    private void AddSample(string roomId, ChatParticipant sender, string content, int hour, int minute)
    {
        messages[roomId].Add(new ChatMessage(Guid.NewGuid(), roomId, sender.Id, sender.Name,
            content, new DateTimeOffset(2026, 10, 13, hour, minute, 0, TimeSpan.FromHours(9))));
    }
}
