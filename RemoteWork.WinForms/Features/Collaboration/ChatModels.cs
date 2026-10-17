namespace RemoteWork.WinForms.Features.Collaboration;

internal sealed record ChatParticipant(string Id, string Name, string Department);

internal sealed record ChatRoom(string Id, string Name, bool IsGroup,
    IReadOnlyList<ChatParticipant> Participants);

// 서버 연결 시에도 같은 요청 데이터를 전달할 수 있도록 화면과 분리합니다.
internal sealed record CreateChatRoomRequest(string Title, bool IsGroup,
    IReadOnlyList<string> ParticipantIds);

internal sealed record ChatMessage(Guid Id, string RoomId, string SenderId,
    string SenderName, string Content, DateTimeOffset SentAt);
