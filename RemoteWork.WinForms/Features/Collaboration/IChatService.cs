namespace RemoteWork.WinForms.Features.Collaboration;

// 화면은 저장 방식 대신 이 계약에 의존합니다. 서버 연결 시 구현체를 교체합니다.
internal interface IChatService
{
    ChatParticipant CurrentUser { get; }
    Task<IReadOnlyList<ChatRoom>> GetRoomsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatParticipant>> GetAvailableParticipantsAsync(CancellationToken cancellationToken = default);
    Task<ChatRoom> CreateRoomAsync(CreateChatRoomRequest request, CancellationToken cancellationToken = default);
    Task DeleteRoomAsync(string roomId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(string roomId, CancellationToken cancellationToken = default);
    Task<ChatMessage> SendMessageAsync(string roomId, string content, CancellationToken cancellationToken = default);
}
