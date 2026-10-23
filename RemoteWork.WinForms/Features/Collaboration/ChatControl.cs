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
        private readonly Dictionary<string, ChatAttachmentSelection> roomFiles = new();
        private readonly HashSet<string> unknownSends = new();
        private readonly HashSet<string> fileErrors = new();
        private IReadOnlyList<ChatMessage> shownMessages = Array.Empty<ChatMessage>();
        private readonly Button attachmentDownload = new() { Text = "첨부 다운로드", AutoSize = true, Height = 28, Margin = new Padding(0) };
        private bool creatingRoom;
        private bool deletingRoom;
        private int selectionVersion;

        // Visual Studio 디자이너에서도 사용할 수 있는 기본 생성자입니다.
        public ChatControl() : this(new InMemoryChatService(new ChatParticipant("EMP-001", "김직원", "개발팀"))) { }

        internal ChatControl(IChatService chatService) : this(chatService, chatService is HttpChatService) { }

        // Explicit override is used only by isolated UI tests; preview stays local.
        internal ChatControl(IChatService chatService, bool autoReceive)
        {
            this.chatService = chatService;
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            ConfigureAutoReceive(autoReceive);
            InitializeDeleteMenu();
            btnAttachFile.Click -= Unavailable_Click;
            btnAttachFile.Click += AttachFiles_Click;
            var selectionMenu = new ContextMenuStrip(components!);
            btnAttachFile.ContextMenuStrip = selectionMenu;
            selectionMenu.Opening += (_, e) =>
            {
                selectionMenu.Items.Clear();
                if (selectedRoom is null || sending || unknownSends.Contains(selectedRoom.Id) || !roomFiles.TryGetValue(selectedRoom.Id, out var selection)) { e.Cancel = true; return; }
                string room = selectedRoom.Id;
                foreach (var entry in selection.Entries)
                {
                    var remove = new ToolStripMenuItem($"{(entry.File is null ? "전송 대기" : "완료")} · {Path.GetFileName(entry.Path)} — 선택에서 제외");
                    remove.Click += (_, _) =>
                    {
                        RemoveSelectedFile(room, entry.Path);
                    };
                    selectionMenu.Items.Add(remove);
                }
                e.Cancel = selectionMenu.Items.Count == 0;
            };
            var fileMenu = new ContextMenuStrip(components!);
            fileMenu.Opening += (_, e) =>
            {
                fileMenu.Items.Clear();
                if (chatService is not HttpChatService http || selectedRoom is null) { e.Cancel = true; return; }
                foreach (var message in shownMessages.Reverse())
                {
                    var item = new ToolStripMenuItem($"{message.SenderName}: {message.Content[..Math.Min(24, message.Content.Length)]} · 첨부파일");
                    item.Click += (_, _) => { using var dialog = new AttachmentDownloadForm(http.Attachments, "chat", message.Id.ToString("D")); dialog.ShowDialog(FindForm()); };
                    fileMenu.Items.Add(item);
                }
                e.Cancel = fileMenu.Items.Count == 0;
            };
            txtContent2.ContextMenuStrip = fileMenu;
            var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0) };
            header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            layoutContent8.Controls.Remove(lblText4); lblText4.AutoSize = false; lblText4.Dock = DockStyle.Fill; lblText4.AutoEllipsis = true;
            header.Controls.Add(lblText4, 0, 0); header.Controls.Add(attachmentDownload, 1, 0); layoutContent8.Controls.Add(header, 0, 0);
            attachmentDownload.Click += (_, _) => fileMenu.Show(attachmentDownload, new Point(0, attachmentDownload.Height));
            txtContent.Clear();
            txtContent2.Clear();
            lblText4.Text = "대화방을 선택해 주세요";
            cmbOptions.SelectedIndex = 0;
            ShowStatus("대화방을 불러오는 중입니다.");
            UpdateInputState();
            Load += ChatControl_Load;

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
                if (!IsDisposed && !Disposing)
                {
                    UpdateInputState();
                    autoReceiveLoaded = true;
                    UpdateAutoReceiveState();
                }
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
            CancelReceiveRequest();
            if (selectedRoom is not null) drafts[selectedRoom.Id] = txtContent3.Text;
            selectedRoom = gridRooms.CurrentRow?.Tag as ChatRoom;
            int version = ++selectionVersion;
            txtContent2.Clear();
            shownMessages = Array.Empty<ChatMessage>(); attachmentDownload.Enabled = false;
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
            var menu = new ContextMenuStrip(components!);
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
            // Already in-flight manual or automatic responses must not restore this room.
            InvalidateReceive();
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
            InvalidateReceive();
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
            if (selectedRoom is null || loadingRooms || loadingMessages || sending || creatingRoom || deletingRoom || HasUnfinishedAttachments(selectedRoom.Id)) return;
            if (string.IsNullOrWhiteSpace(txtContent3.Text))
            {
                ShowStatus("메시지를 입력해 주세요.");
                return;
            }
            string roomId = selectedRoom.Id;
            string content = txtContent3.Text;
            bool saved = false;
            sending = true;
            InvalidateReceive();
            UpdateInputState();
            try
            {
                if (chatService is HttpChatService http)
                    await http.SendMessageWithFilesAsync(roomId, content,
                        roomFiles.GetValueOrDefault(roomId, new()).Completed.Select(f => f.AttachmentId).ToArray(), lifetime.Token);
                else await chatService.SendMessageAsync(roomId, content, lifetime.Token);
                unknownSends.Remove(roomId);
                if (chatService is HttpChatService sentHttp) sentHttp.Attachments.ForgetLinkedUploads(roomFiles.GetValueOrDefault(roomId, new()).Completed.Select(f => f.AttachmentId));
                roomFiles.Remove(roomId);
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
                if (!saved && (ex is not RemoteWork.WinForms.Infrastructure.ApiException apiError || (apiError.StatusCode == 0 || apiError.StatusCode >= 500)))
                    unknownSends.Add(roomId);
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
            shownMessages = messages;
            attachmentDownload.Enabled = chatService is HttpChatService && shownMessages.Count > 0;
            txtContent2.Text = string.Join(Environment.NewLine + Environment.NewLine,
                messages.Select(message => $"{message.SenderName}   {message.SentAt.ToLocalTime():yyyy.MM.dd HH:mm:ss}\r\n{message.Content}"));
            txtContent2.SelectionStart = txtContent2.TextLength;
            txtContent2.ScrollToCaret();
        }

        private void UpdateInputState()
        {
            bool canCompose = selectedRoom is not null && !loadingRooms && !loadingMessages && !sending && !creatingRoom && !deletingRoom;
            txtContent3.Enabled = canCompose && (selectedRoom is null || !unknownSends.Contains(selectedRoom.Id));
            btnAttachFile.Enabled = canCompose && chatService is HttpChatService && (selectedRoom is null || CanEditAttachments(selectedRoom.Id));
            btnAttachFile.Text = selectedRoom is not null && roomFiles.TryGetValue(selectedRoom.Id, out var files) ? $"파일 첨부 ({files.Completed.Length}/{files.Entries.Count})" : "파일 첨부";
            btnSendMessage.Enabled = canCompose && !string.IsNullOrWhiteSpace(txtContent3.Text)
                && ChatText.WithinLimit(txtContent3.Text.Trim(), 2000) && (selectedRoom is null || !HasUnfinishedAttachments(selectedRoom.Id));
            gridRooms.Enabled = cmbOptions.Enabled = !loadingRooms && !sending && !creatingRoom && !deletingRoom;
            btnCreateRoom.Enabled = !loadingRooms && !sending && !creatingRoom && !deletingRoom;
        }

        private void ShowStatus(string text)
        {
            string source = chatService is HttpChatService ? "서버 DB 저장" : "대화방·메시지 임시 저장";
            lblChatStatus.Text = $"{source} · 작성자: {chatService.CurrentUser.Name} | {text}";
        }

        private bool HasUnfinishedAttachments(string room) => fileErrors.Contains(room) || (roomFiles.TryGetValue(room, out var state) && state.HasPending);
        private bool CanEditAttachments(string room) => !sending && !unknownSends.Contains(room) && (chatService is not HttpChatService http || !http.HasPendingMessage(room));
        private bool RemoveSelectedFile(string room, string path)
        {
            if (!CanEditAttachments(room) || !roomFiles.TryGetValue(room, out var selection)) return false;
            selection.Remove(path);
            if (!selection.HasPending) fileErrors.Remove(room);
            UpdateInputState(); return true;
        }

        private async void AttachFiles_Click(object? sender, EventArgs e)
        {
            if (selectedRoom is null || !CanEditAttachments(selectedRoom.Id) || chatService is not HttpChatService http) return;
            using var dialog = new OpenFileDialog { Multiselect = true, CheckFileExists = true, RestoreDirectory = true };
            if (dialog.ShowDialog(FindForm()) != DialogResult.OK) return;
            string room = selectedRoom.Id;
            await UploadSelectedFilesAsync(room, dialog.FileNames,
                (path, ct) => http.Attachments.UploadAsync("chat", room, path, ct));
        }

        // Same product operation is called by the picker and isolated controls
        // tests; the test does not need to automate a Windows file dialog.
        private async Task UploadSelectedFilesAsync(string room, string[] paths,
            Func<string, CancellationToken, Task<ResourceAttachment>> upload)
        {
            var files = roomFiles.GetValueOrDefault(room, new());
            roomFiles[room] = files;
            sending = true; InvalidateReceive(); UpdateInputState();
            try
            {
                await files.UploadAsync(paths, upload, lifetime.Token);
                fileErrors.Remove(room);
                if (!IsDisposed) ShowStatus(files.HasPending
                    ? "남은 첨부: " + string.Join(" · ", files.Entries.Where(e => e.File is null).Select(e => Path.GetFileName(e.Path))) + " · 모두 전송하거나 선택에서 제외해 주세요."
                    : "첨부 전송 완료. 보내기를 누르면 메시지에 연결됩니다.");
            }
            catch (Exception ex) { fileErrors.Add(room); if (!IsDisposed) ShowStatus("첨부 실패: " + ex.Message + " 원래 선택 전체를 유지합니다. 미완료 파일을 다시 선택하거나 첨부 선택 메뉴에서 제외해 주세요."); }
            finally { sending = false; if (!IsDisposed) UpdateInputState(); }
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

    }
}
