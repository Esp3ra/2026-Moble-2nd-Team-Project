using System.Runtime.InteropServices;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration;

public partial class ChatControl
{
    private const int ReceiveInterval = 1000;
    private readonly System.Windows.Forms.Timer receiveTimer = new() { Interval = ReceiveInterval };
    private CancellationTokenSource? receiveRequest;
    private bool autoReceiveEnabled, autoReceiveLoaded, receiverDisposed, receiveBusy, receiveFailed;

    private void ConfigureAutoReceive(bool enabled)
    {
        autoReceiveEnabled = enabled;
        receiveTimer.Tick += ReceiveTimer_Tick;
        VisibleChanged += (_, _) => UpdateAutoReceiveState();
        Disposed += (_, _) => DisposeReceiver();
    }

    private bool CurrentReceiveSession => chatService is not HttpChatService http || http.IsSessionCurrent;

    private void UpdateAutoReceiveState()
    {
        if (receiverDisposed) return;
        if (autoReceiveEnabled && autoReceiveLoaded && Visible && CurrentReceiveSession)
            receiveTimer.Start();
        else
        {
            receiveTimer.Stop();
            CancelReceiveRequest();
        }
    }

    private void CancelReceiveRequest() => receiveRequest?.Cancel();

    private void InvalidateReceive()
    {
        ++selectionVersion;
        CancelReceiveRequest();
    }

    private void DisposeReceiver()
    {
        if (receiverDisposed) return;
        receiverDisposed = true;
        receiveTimer.Stop();
        receiveTimer.Dispose();
        CancelReceiveRequest();
        lifetime.Cancel();
        lifetime.Dispose();
    }

    private async void ReceiveTimer_Tick(object? sender, EventArgs e) => await ReceiveOnceAsync();

    private bool CanApplyReceive(int version, string? roomId, int filter) =>
        !receiverDisposed && !IsDisposed && !Disposing && Visible && CurrentReceiveSession
        && version == selectionVersion && roomId == selectedRoom?.Id && filter == cmbOptions.SelectedIndex
        && !loadingRooms && !loadingMessages && !sending && !creatingRoom && !deletingRoom;

    // One request chain per visible chat screen. Reuse the existing read APIs;
    // server authentication/authorization and payload contracts are unchanged.
    private async Task ReceiveOnceAsync()
    {
        if (!autoReceiveEnabled || !autoReceiveLoaded || receiverDisposed || !Visible || receiveBusy
            || loadingRooms || loadingMessages || sending || creatingRoom || deletingRoom) return;
        if (!CurrentReceiveSession) { UpdateAutoReceiveState(); return; }
        receiveBusy = true;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        receiveRequest = request;
        int version = selectionVersion, filter = cmbOptions.SelectedIndex;
        string? previousId = selectedRoom?.Id;
        try
        {
            var latestRooms = await chatService.GetRoomsAsync(request.Token);
            request.Token.ThrowIfCancellationRequested();
            if (!CanApplyReceive(version, previousId, filter)) return;
            var visibleRooms = latestRooms.Where(r => filter == 0 || (filter == 1 ? !r.IsGroup : r.IsGroup)).ToArray();
            var nextRoom = visibleRooms.FirstOrDefault(r => r.Id == previousId) ?? visibleRooms.FirstOrDefault();
            var history = nextRoom is null ? Array.Empty<ChatMessage>()
                : await chatService.GetMessagesAsync(nextRoom.Id, request.Token);
            request.Token.ThrowIfCancellationRequested();
            if (!CanApplyReceive(version, previousId, filter)) return;

            bool changedRoom = previousId != nextRoom?.Id;
            if (!SameRooms(rooms, latestRooms))
            {
                rooms = latestRooms;
                PopulateRooms(nextRoom?.Id);
            }
            if (changedRoom)
            {
                if (previousId is not null)
                {
                    if (latestRooms.Any(r => r.Id == previousId)) drafts[previousId] = txtContent3.Text;
                    else drafts.Remove(previousId); // Hidden on another client: do not restore its draft.
                }
                ++selectionVersion;
                txtContent3.Text = nextRoom is null ? "" : drafts.GetValueOrDefault(nextRoom.Id, "");
            }
            selectedRoom = nextRoom;
            lblText4.Text = nextRoom?.Name ?? "대화방을 선택해 주세요";
            txtContent.Text = nextRoom is null ? "" : string.Join(Environment.NewLine,
                nextRoom.Participants.Select(p => $"{p.Name} · {p.Department}"));
            RenderReceivedMessages(history, preservePosition: !changedRoom);
            UpdateInputState();
            if (receiveFailed) ShowStatus("자동 수신 연결이 복구됐습니다.");
            receiveFailed = false;
            receiveTimer.Interval = ReceiveInterval;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested || !CurrentReceiveSession) { }
        catch (ApiException ex) when (ex.StatusCode == 401)
        {
            if (!receiverDisposed) receiveTimer.Stop();
            if (!receiverDisposed && !IsDisposed && !Disposing) ShowStatus("로그인이 만료되어 자동 수신을 중지했습니다.");
        }
        catch (Exception ex)
        {
            if (CanApplyReceive(version, previousId, filter))
            {
                receiveFailed = true;
                receiveTimer.Interval = Math.Min(receiveTimer.Interval * 2, 10000);
                ShowStatus($"자동 수신 연결 실패 · {receiveTimer.Interval / 1000}초 후 재시도: {ex.Message}");
            }
        }
        finally
        {
            if (ReferenceEquals(receiveRequest, request)) receiveRequest = null;
            receiveBusy = false;
            if (!receiverDisposed && !CurrentReceiveSession) UpdateAutoReceiveState();
        }
    }

    private static bool SameRooms(IReadOnlyList<ChatRoom> left, IReadOnlyList<ChatRoom> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            pair.First.Id == pair.Second.Id && pair.First.Name == pair.Second.Name
            && pair.First.IsGroup == pair.Second.IsGroup
            && pair.First.Participants.SequenceEqual(pair.Second.Participants));

    [StructLayout(LayoutKind.Sequential)]
    private struct ScrollInfo
    {
        public uint Size, Mask;
        public int Minimum, Maximum;
        public uint Page;
        public int Position, Track;
    }

    [DllImport("user32.dll", EntryPoint = "GetScrollInfo")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadScrollInfo(nint handle, int bar, ref ScrollInfo info);

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint TextMessage(nint handle, uint message, nint wParam, nint lParam);

    private void RenderReceivedMessages(IReadOnlyList<ChatMessage> messages, bool preservePosition)
    {
        string text = string.Join(Environment.NewLine + Environment.NewLine, messages.Select(message =>
            $"{message.SenderName}   {message.SentAt.ToLocalTime():yyyy.MM.dd HH:mm:ss}\r\n{message.Content}"));
        if (txtContent2.Text == text) return; // Do not reset selection/scroll every second.
        var info = new ScrollInfo { Size = (uint)Marshal.SizeOf<ScrollInfo>(), Mask = 7 };
        bool atBottom = !ReadScrollInfo(txtContent2.Handle, 1, ref info)
            || (long)info.Position + info.Page >= (long)info.Maximum + 1;
        bool follow = !preservePosition || txtContent2.TextLength == 0 || (atBottom && txtContent2.SelectionLength == 0);
        int firstLine = (int)TextMessage(txtContent2.Handle, 0xCE, 0, 0);
        int selection = txtContent2.SelectionStart, length = txtContent2.SelectionLength;
        txtContent2.Text = text;
        if (follow)
        {
            txtContent2.SelectionStart = txtContent2.TextLength;
            txtContent2.ScrollToCaret();
        }
        else
        {
            txtContent2.Select(Math.Min(selection, text.Length), Math.Min(length, Math.Max(0, text.Length - selection)));
            int current = (int)TextMessage(txtContent2.Handle, 0xCE, 0, 0);
            TextMessage(txtContent2.Handle, 0xB6, 0, firstLine - current);
        }
    }
}
