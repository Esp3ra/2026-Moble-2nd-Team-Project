using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Collaboration
{
    public partial class ChatControl : UserControl
    {
        private readonly IChatService chatService;
        private readonly CancellationTokenSource lifetime = new();
        private readonly Dictionary<string, string> drafts = new();
        private IReadOnlyList<ChatRoom> rooms = Array.Empty<ChatRoom>();
        private ChatRoom? selectedRoom;
        private bool updatingRooms;
        private bool loadingRooms;
        private bool loadingMessages;
        private bool sending;
        private bool creatingRoom;
        private bool deletingRoom;
        private int selectionVersion;

        // Visual Studio 디자이너에서도 사용할 수 있는 기본 생성자입니다.
        public ChatControl() : this(new InMemoryChatService(new ChatParticipant("EMP-001", "김직원", "개발팀"))) { }

        internal ChatControl(IChatService chatService)
        {
            this.chatService = chatService;
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeDeleteMenu();
            txtContent.Clear();
            txtContent2.Clear();
            lblText4.Text = "대화방을 선택해 주세요";
            cmbOptions.SelectedIndex = 0;
            ShowStatus("대화방을 불러오는 중입니다.");
            UpdateInputState();
            Load += ChatControl_Load;
            Disposed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
        }

        private async void ChatControl_Load(object? sender, EventArgs e)
        {
            // Load가 다시 발생해도 이미 조회한 목록을 중복으로 채우지 않습니다.
            Load -= ChatControl_Load;
            loadingRooms = true;
            UpdateInputState();
            try
            {
                rooms = await chatService.GetRoomsAsync(lifetime.Token);
                if (IsDisposed || Disposing) return;
                loadingRooms = false;
                PopulateRooms();
                await ShowSelectedRoomAsync();
            }
            catch (OperationCanceledException) when (IsDisposed || Disposing) { }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing) ShowStatus($"대화방 조회 실패: {ex.Message}");
            }
            finally
            {
                loadingRooms = false;
                if (!IsDisposed && !Disposing) UpdateInputState();
            }
        }

        private void PopulateRooms(string? preferredRoomId = null)
        {
            string? previousId = preferredRoomId ?? selectedRoom?.Id;
            updatingRooms = true;
            try
            {
                gridRooms.Rows.Clear();
                foreach (var room in rooms)
                {
                    if (cmbOptions.SelectedIndex == 1 && room.IsGroup) continue;
                    if (cmbOptions.SelectedIndex == 2 && !room.IsGroup) continue;
                    int index = gridRooms.Rows.Add(room.IsGroup ? "그룹" : "개인", room.Name, $"{room.Participants.Count}명");
                    gridRooms.Rows[index].Tag = room;
                }
                if (gridRooms.Rows.Count > 0)
                {
                    var row = gridRooms.Rows.Cast<DataGridViewRow>()
                        .FirstOrDefault(r => (r.Tag as ChatRoom)?.Id == previousId) ?? gridRooms.Rows[0];
                    gridRooms.CurrentCell = row.Cells[1];
                }
            }
            finally { updatingRooms = false; }
        }

        private async void RoomFilter_Changed(object? sender, EventArgs e)
        {
            if (updatingRooms || loadingRooms || sending || creatingRoom || deletingRoom || rooms.Count == 0) return;
            PopulateRooms();
            await ShowSelectedRoomAsync();
        }

        private async void RoomSelection_Changed(object? sender, EventArgs e)
        {
            if (updatingRooms || loadingRooms || sending || creatingRoom || deletingRoom) return;
            await ShowSelectedRoomAsync();
        }

        private async Task ShowSelectedRoomAsync()
        {
            if (selectedRoom is not null) drafts[selectedRoom.Id] = txtContent3.Text;
            selectedRoom = gridRooms.CurrentRow?.Tag as ChatRoom;
            int version = ++selectionVersion;
            txtContent2.Clear();
            if (selectedRoom is null)
            {
                loadingMessages = false;
                txtContent.Clear();
                txtContent3.Clear();
                lblText4.Text = "대화방을 선택해 주세요";
                UpdateInputState();
                return;
            }
            var room = selectedRoom;
            lblText4.Text = room.Name;
            txtContent.Text = string.Join(Environment.NewLine, room.Participants.Select(p => $"{p.Name} · {p.Department}"));
            txtContent3.Text = drafts.GetValueOrDefault(room.Id, "");
            loadingMessages = true;
            UpdateInputState();
            try
            {
                var history = await chatService.GetMessagesAsync(room.Id, lifetime.Token);
                // 서버 조회가 늦게 끝나더라도 이전 방의 내용을 새 방에 덮어쓰지 않습니다.
                if (IsDisposed || Disposing || version != selectionVersion) return;
                RenderMessages(history);
                ShowStatus("Enter 전송 · Shift+Enter 줄바꿈");
            }
            catch (OperationCanceledException) when (IsDisposed || Disposing) { }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing && version == selectionVersion)
                    ShowStatus($"대화 조회 실패: {ex.Message}");
            }
            finally
            {
                if (!IsDisposed && !Disposing && version == selectionVersion)
                {
                    loadingMessages = false;
                    UpdateInputState();
                }
            }
        }

        private async void SendMessage_Click(object? sender, EventArgs e) => await SendMessageAsync();

        private void InitializeDeleteMenu()
        {
            var menu = new ContextMenuStrip(components);
            var deleteItem = new ToolStripMenuItem(chatService is HttpChatService ? "내 목록에서 삭제" : "삭제");
            ChatRoom? rightClickedRoom = null;
            menu.Items.Add(deleteItem);

            gridRooms.CellMouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 0
                    || loadingRooms || sending || creatingRoom || deletingRoom) return;
                var row = gridRooms.Rows[e.RowIndex];
                if (row.Tag is not ChatRoom room) return;

                gridRooms.ClearSelection();
                gridRooms.CurrentCell = row.Cells[e.ColumnIndex];
                row.Selected = true;
                // 이름이 같은 방이 있어도 우클릭한 방의 ID로만 삭제합니다.
                rightClickedRoom = room;
                menu.Show(Cursor.Position);
            };

            menu.Opening += (_, e) =>
                e.Cancel = rightClickedRoom is null || loadingRooms || sending || creatingRoom || deletingRoom;
            deleteItem.Click += async (_, _) =>
            {
                if (rightClickedRoom is { } room) await DeleteRoomAsync(room);
            };
        }

        private async Task DeleteRoomAsync(ChatRoom room)
        {
            if (loadingRooms || sending || creatingRoom || deletingRoom) return;
            int rowIndex = gridRooms.CurrentRow?.Index ?? 0;
            var remainingVisibleRooms = gridRooms.Rows.Cast<DataGridViewRow>()
                .Select(row => row.Tag).OfType<ChatRoom>().Where(candidate => candidate.Id != room.Id).ToArray();
            string? nextRoomId = remainingVisibleRooms
                .ElementAtOrDefault(Math.Min(rowIndex, Math.Max(0, remainingVisibleRooms.Length - 1)))?.Id;

            deletingRoom = true;
            // 이미 진행 중인 대화 조회가 삭제 후 화면에 다시 나타나지 않게 합니다.
            ++selectionVersion;
            loadingMessages = false;
            UpdateInputState();
            ShowStatus($"'{room.Name}' 대화방을 삭제하는 중입니다.");
            try
            {
                await chatService.DeleteRoomAsync(room.Id, lifetime.Token);
                if (IsDisposed || Disposing) return;
                rooms = rooms.Where(candidate => candidate.Id != room.Id).ToArray();
                drafts.Remove(room.Id);
                if (selectedRoom?.Id == room.Id)
                {
                    // 다음 방을 표시할 때 삭제한 방의 초안을 다시 저장하지 않습니다.
                    selectedRoom = null;
                    txtContent3.Clear();
                }
                PopulateRooms(nextRoomId);
                await ShowSelectedRoomAsync();
                if (!IsDisposed && !Disposing)
                    ShowStatus(chatService is HttpChatService
                        ? $"'{room.Name}' 방을 내 목록에서만 삭제했습니다. 다른 참여자의 대화는 유지됩니다."
                        : $"'{room.Name}' 대화방과 대화를 삭제했습니다.");
            }
            catch (OperationCanceledException) when (IsDisposed || Disposing) { }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing)
                {
                    // 저장소 삭제에 실패했으면 기존 방과 입력 내용을 유지합니다.
                    await ShowSelectedRoomAsync();
                    if (!IsDisposed && !Disposing) ShowStatus($"대화방 삭제 실패: {ex.Message}");
                }
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                {
                    deletingRoom = false;
                    UpdateInputState();
                }
            }
        }

        private async void CreateRoom_Click(object? sender, EventArgs e)
        {
            if (creatingRoom || loadingRooms || sending || deletingRoom) return;
            creatingRoom = true;
            UpdateInputState();
            bool created = false;
            try
            {
                using var dialog = new CreateChatRoomForm(chatService);
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK || dialog.CreatedRoom is not { } room) return;
                created = true;
                rooms = await chatService.GetRoomsAsync(lifetime.Token);
                if (IsDisposed || Disposing) return;
                // 현재 필터가 개인/그룹 중 하나여도 새 방을 즉시 확인할 수 있게 합니다.
                updatingRooms = true;
                try { cmbOptions.SelectedIndex = 0; }
                finally { updatingRooms = false; }
                PopulateRooms(room.Id);
                await ShowSelectedRoomAsync();
                if (!IsDisposed && !Disposing) ShowStatus(chatService is HttpChatService
                    ? $"'{room.Name}' 대화방을 서버에 저장했습니다."
                    : $"'{room.Name}' 대화방을 임시로 만들었습니다.");
            }
            catch (OperationCanceledException) when (IsDisposed || Disposing) { }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing)
                    ShowStatus(created ? $"대화방은 생성됐지만 목록 갱신에 실패했습니다. 채팅 메뉴를 다시 열어 주세요: {ex.Message}"
                        : $"대화방 만들기 실패: {ex.Message}");
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                {
                    creatingRoom = false;
                    UpdateInputState();
                }
            }
        }

        private async void MessageInput_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                await SendMessageAsync();
            }
        }

        private void MessageInput_TextChanged(object? sender, EventArgs e) => UpdateInputState();

        private async Task SendMessageAsync()
        {
            if (selectedRoom is null || loadingRooms || loadingMessages || sending || creatingRoom || deletingRoom) return;
            if (string.IsNullOrWhiteSpace(txtContent3.Text))
            {
                ShowStatus("메시지를 입력해 주세요.");
                return;
            }
            string roomId = selectedRoom.Id;
            string content = txtContent3.Text;
            bool saved = false;
            sending = true;
            UpdateInputState();
            try
            {
                await chatService.SendMessageAsync(roomId, content, lifetime.Token);
                saved = true;
                if (IsDisposed || Disposing) return;
                txtContent3.Clear();
                drafts.Remove(roomId);
                var history = await chatService.GetMessagesAsync(roomId, lifetime.Token);
                if (IsDisposed || Disposing) return;
                RenderMessages(history);
                ShowStatus(chatService is HttpChatService ? "메시지를 서버에 저장했습니다." : "이 화면의 임시 대화에 추가했습니다.");
            }
            catch (OperationCanceledException) when (IsDisposed || Disposing) { }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing)
                    ShowStatus(saved ? $"메시지는 추가되었지만 목록 갱신에 실패했습니다. 방을 다시 선택해 주세요: {ex.Message}"
                        : $"메시지 추가 실패: {ex.Message}");
            }
            finally
            {
                if (!IsDisposed && !Disposing)
                {
                    sending = false;
                    UpdateInputState();
                    txtContent3.Focus();
                }
            }
        }

        private void RenderMessages(IReadOnlyList<ChatMessage> messages)
        {
            txtContent2.Text = string.Join(Environment.NewLine + Environment.NewLine,
                messages.Select(message => $"{message.SenderName}   {message.SentAt.ToLocalTime():yyyy.MM.dd HH:mm:ss}\r\n{message.Content}"));
            txtContent2.SelectionStart = txtContent2.TextLength;
            txtContent2.ScrollToCaret();
        }

        private void UpdateInputState()
        {
            bool canCompose = selectedRoom is not null && !loadingRooms && !loadingMessages && !sending && !creatingRoom && !deletingRoom;
            txtContent3.Enabled = canCompose;
            btnSendMessage.Enabled = canCompose && !string.IsNullOrWhiteSpace(txtContent3.Text);
            gridRooms.Enabled = cmbOptions.Enabled = !loadingRooms && !sending && !creatingRoom && !deletingRoom;
            btnCreateRoom.Enabled = !loadingRooms && !sending && !creatingRoom && !deletingRoom;
        }

        private void ShowStatus(string text)
        {
            string source = chatService is HttpChatService ? "서버 DB 저장" : "대화방·메시지 임시 저장";
            lblChatStatus.Text = $"{source} · 작성자: {chatService.CurrentUser.Name} | {text}";
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

    }
}
