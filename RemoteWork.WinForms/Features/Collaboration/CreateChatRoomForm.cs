using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Collaboration;

public partial class CreateChatRoomForm : Form
{
    private readonly IChatService chatService;
    private readonly CancellationTokenSource lifetime = new();
    private bool loading = true;
    private bool saving;
    private bool updatingChecks;
    private bool lifetimeEnded;
    internal ChatRoom? CreatedRoom { get; private set; }

    // 기본 생성자를 유지해 Visual Studio 디자이너에서 열 수 있습니다.
    public CreateChatRoomForm() : this(new InMemoryChatService(new ChatParticipant("EMP-001", "김직원", "개발팀"))) { }

    internal CreateChatRoomForm(IChatService chatService)
    {
        this.chatService = chatService;
        InitializeComponent();
        if (PreviewActions.IsDesignMode(this)) return;
        lblCurrentUser.Text = $"{chatService.CurrentUser.Name} · {chatService.CurrentUser.Department} (본인, 자동 포함)";
        UpdateSelectionState();
        Load += LoadParticipantsAsync;
        FormClosing += ConfirmClosing;
        Disposed += (_, _) =>
        {
            if (lifetimeEnded) return;
            lifetimeEnded = true;
            lifetime.Cancel();
            lifetime.Dispose();
        };
    }

    private async void LoadParticipantsAsync(object? sender, EventArgs e)
    {
        Load -= LoadParticipantsAsync;
        try
        {
            var participants = await chatService.GetAvailableParticipantsAsync(lifetime.Token);
            if (IsDisposed || Disposing) return;
            foreach (var participant in participants.Where(p => p.Id != chatService.CurrentUser.Id)
                .OrderBy(p => p.Department).ThenBy(p => p.Name))
                lstParticipants.Items.Add(new ParticipantItem(participant));
            lblError.Text = lstParticipants.Items.Count == 0 ? "선택할 수 있는 참여자가 없습니다." : "";
        }
        catch (OperationCanceledException) when (IsDisposed || Disposing) { }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing) lblError.Text = $"참여자를 불러오지 못했습니다: {ex.Message}";
        }
        finally
        {
            if (!IsDisposed && !Disposing)
            {
                loading = false;
                UpdateSelectionState();
            }
        }
    }

    private void RoomType_Changed(object? sender, EventArgs e)
    {
        if (rdoPrivate.Checked && lstParticipants.CheckedIndices.Count > 1)
        {
            updatingChecks = true;
            try
            {
                foreach (int index in lstParticipants.CheckedIndices.Cast<int>().Skip(1).ToArray())
                    lstParticipants.SetItemChecked(index, false);
            }
            finally { updatingChecks = false; }
        }
        lblError.Text = "";
        UpdateSelectionState();
    }

    private void Participants_ItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (updatingChecks) return;
        // 개인 대화는 다른 사람을 체크하면 기존 선택을 해제합니다.
        if (rdoPrivate.Checked && e.NewValue == CheckState.Checked)
        {
            updatingChecks = true;
            try
            {
                foreach (int index in lstParticipants.CheckedIndices.Cast<int>().Where(i => i != e.Index).ToArray())
                    lstParticipants.SetItemChecked(index, false);
            }
            finally { updatingChecks = false; }
        }
        lblError.Text = "";
        // ItemCheck는 값 변경 전에 발생하므로 변경 예정 값을 함께 계산합니다.
        UpdateSelectionState(e.Index, e.NewValue);
    }

    private void Title_TextChanged(object? sender, EventArgs e)
    {
        lblError.Text = "";
        UpdateSelectionState();
    }

    private void UpdateSelectionState(int changingIndex = -1, CheckState changingState = CheckState.Unchecked)
    {
        int count = Enumerable.Range(0, lstParticipants.Items.Count).Count(i =>
            i == changingIndex ? changingState == CheckState.Checked : lstParticipants.GetItemChecked(i));
        lblSelection.Text = $"상대방 {count}명 선택 · 본인 포함 {count + 1}명";
        lblParticipantHint.Text = rdoPrivate.Checked
            ? "개인: 상대방 1명을 선택합니다. 본인을 포함해 총 2명입니다."
            : "그룹: 상대방 2명 이상을 선택합니다. 본인은 자동 포함됩니다.";
        bool validCount = rdoPrivate.Checked ? count == 1 : count >= 2;
        btnCreate.Enabled = !loading && !saving && validCount && !string.IsNullOrWhiteSpace(txtTitle.Text)
            && !txtTitle.Text.Any(char.IsControl);
        txtTitle.Enabled = rdoPrivate.Enabled = rdoGroup.Enabled = lstParticipants.Enabled = !loading && !saving;
        btnCancel.Enabled = !saving;
    }

    private async void Create_Click(object? sender, EventArgs e)
    {
        if (loading || saving || !btnCreate.Enabled) return;
        var ids = lstParticipants.CheckedItems.Cast<ParticipantItem>().Select(item => item.Participant.Id).ToArray();
        var request = new CreateChatRoomRequest(txtTitle.Text.Trim(), rdoGroup.Checked, ids);
        saving = true;
        lblError.Text = "대화방을 만드는 중입니다…";
        UpdateSelectionState();
        try
        {
            var room = await chatService.CreateRoomAsync(request, lifetime.Token);
            if (IsDisposed || Disposing) return;
            CreatedRoom = room;
            saving = false;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (OperationCanceledException) when (IsDisposed || Disposing) { }
        catch (Exception ex)
        {
            if (!IsDisposed && !Disposing) lblError.Text = ex.Message;
        }
        finally
        {
            if (!IsDisposed && !Disposing)
            {
                saving = false;
                UpdateSelectionState();
            }
        }
    }

    private void ConfirmClosing(object? sender, FormClosingEventArgs e)
    {
        // 서버 구현으로 바꿨을 때 생성 중 닫아서 결과를 놓치지 않도록 합니다.
        if (saving)
        {
            e.Cancel = true;
            lblError.Text = "대화방 생성이 완료될 때까지 기다려 주세요.";
        }
    }

    private sealed record ParticipantItem(ChatParticipant Participant)
    {
        public override string ToString() => $"{Participant.Name} · {Participant.Department} (#{Participant.Id})";
    }
}
