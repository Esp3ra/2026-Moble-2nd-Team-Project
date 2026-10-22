using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks.Attachments;

/// <summary>The login-owned selection outlives this borrowed modal window.</summary>
public partial class TaskCreationAttachmentsForm : Form
{
    private readonly TaskCreationAttachmentSelection? selection;
    private readonly CancellationTokenSource lifetime = new();
    private bool stopped, operating, refreshing;

    public TaskCreationAttachmentsForm() : this(null) { }

    public TaskCreationAttachmentsForm(TaskCreationAttachmentSelection? selection)
    {
        this.selection = selection;
        InitializeComponent();
        if (PreviewActions.IsDesignMode(this)) return;
        if (selection is not null) selection.Changed += SelectionChanged;
        RefreshSelection();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        RefreshSelection();
    }

    private TaskCreationAttachmentItem? SelectedItem => selection?.IsCurrent == true
        && gridFiles.CurrentRow?.Tag is Guid key ? selection.Items.FirstOrDefault(item => item.Key == key) : null;

    private bool CanChange => !stopped && !operating && selection?.CanChange == true;

    private void SelectionChanged(object? sender, EventArgs e)
    {
        if (stopped || IsDisposed || !IsHandleCreated) return;
        try
        {
            if (InvokeRequired) BeginInvoke((Action)RefreshSelection);
            else RefreshSelection();
        }
        catch (InvalidOperationException) { /* The borrowed dialog is closing. */ }
    }

    private void RefreshSelection()
    {
        if (stopped || IsDisposed) return;
        Guid? key = gridFiles.CurrentRow?.Tag as Guid?;
        refreshing = true;
        try
        {
            gridFiles.Rows.Clear();
            if (selection?.IsCurrent == true)
            {
                foreach (var item in selection.Items)
                {
                    int index = gridFiles.Rows.Add(item.Name, FormatSize(item.Size), StateLabel(item.State));
                    gridFiles.Rows[index].Tag = item.Key;
                    if (item.Key == key) gridFiles.CurrentCell = gridFiles.Rows[index].Cells[0];
                }
            }
        }
        finally { refreshing = false; }
        UpdateActions();
    }

    private static string FormatSize(long size) => size < 1024 ? $"{size:N0} B"
        : size < 1024 * 1024 ? $"{size / 1024d:N1} KiB" : $"{size / (1024d * 1024):N1} MiB";

    private static string StateLabel(TaskCreationUploadState state) => state switch
    {
        TaskCreationUploadState.Ready => "선택 완료",
        TaskCreationUploadState.Sending => "업로드 중",
        TaskCreationUploadState.NeedsRetry => "전송 확인 필요",
        TaskCreationUploadState.StartUncertain => "시작 결과 불명",
        TaskCreationUploadState.Completed => "업로드 완료",
        TaskCreationUploadState.Rejected => "업로드 거절",
        TaskCreationUploadState.Disconnected => "접속 종료",
        TaskCreationUploadState.Disposed => "선택 해제",
        TaskCreationUploadState.Discarding => "삭제 중",
        TaskCreationUploadState.DiscardUncertain => "삭제 결과 불명",
        TaskCreationUploadState.DiscardRejected => "삭제 거절",
        TaskCreationUploadState.Discarded => "삭제 확인",
        _ => "상태 확인 필요"
    };

    private void UpdateActions()
    {
        if (stopped || IsDisposed) return;
        var item = SelectedItem;
        bool editable = CanChange;
        btnAdd.Enabled = editable && selection!.Items.Count < 10;
        btnUpload.Enabled = editable && item?.CanUpload == true;
        btnRetry.Enabled = editable && item?.CanRetry == true;
        btnDelete.Enabled = editable && item?.CanDiscard == true;
        btnRemove.Enabled = editable && item?.CanRemove == true;
        btnClose.Enabled = selection?.IsCurrent != true || (!operating && !selection.IsBusy);
        gridFiles.Enabled = !operating && selection?.IsBusy != true;
        lblCount.Text = selection?.IsCurrent == true ? $"선택 파일 {selection.Items.Count} / 10개" : "선택 파일 없음";
        txtState.Text = selection is null ? "첨부 연결이 준비되지 않았습니다. 업무 등록 화면에서 다시 열어 주세요."
            : !selection.IsCurrent ? "로그인 접속이 변경되어 이전 파일 선택을 표시하지 않습니다. 창을 닫고 다시 로그인해 주세요."
            : operating || selection.IsBusy ? "선택한 파일을 처리하고 있습니다. 응답을 확인할 때까지 기다려 주세요."
            : !selection.CanChange && !selection.IsBusy ? selection.Message
            : item is null || selection.Message == item.Message ? selection.Message : selection.Message + "\r\n" + item.Message;
        btnDelete.Text = item?.State == TaskCreationUploadState.DiscardUncertain ? "삭제 다시 확인" : "임시 파일 삭제";
    }

    private void GridFiles_SelectionChanged(object? sender, EventArgs e)
    { if (!refreshing) UpdateActions(); }

    protected virtual string? ChooseFile()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "업무에 첨부할 파일 선택", Multiselect = false,
            CheckFileExists = true, CheckPathExists = true, RestoreDirectory = true,
            Filter = "모든 파일 (*.*)|*.*"
        };
        return dialog.ShowDialog(this) == DialogResult.OK ? dialog.FileName : null;
    }

    protected virtual bool ConfirmFileDeletion(TaskCreationAttachmentItem item) =>
        MessageBox.Show(this, $"‘{item.Name}’ 임시 파일을 서버에서 삭제할까요?\r\n업무 등록에 연결하지 않은 본인 파일만 삭제할 수 있습니다.",
            "임시 첨부 삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    protected virtual bool ConfirmLocalRemoval(TaskCreationAttachmentItem item) =>
        MessageBox.Show(this, $"‘{item.Name}’을 선택 목록에서 지울까요?\r\n"
            + (item.State == TaskCreationUploadState.StartUncertain
                ? "시작 결과를 몰라 서버의 임시 파일을 삭제할 수 없습니다. 서버 파일 삭제는 요청하지 않습니다."
                : item.State == TaskCreationUploadState.DiscardRejected
                    ? "삭제가 거절되어 서버 파일이 남아 있을 수 있습니다. 선택 목록만 지우며 삭제 요청을 다시 보내지 않습니다."
                    : "이 작업은 선택 목록만 정리하며 서버에 삭제 요청을 보내지 않습니다."),
            "선택 목록 정리", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) == DialogResult.Yes;

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (!CanChange || selection is null) return;
        operating = true;
        string? errorMessage = null;
        UpdateActions();
        try { await action(lifetime.Token); }
        catch (OperationCanceledException) when (stopped || IsDisposed || !selection.IsCurrent) { }
        catch (Exception error)
        {
            if (!stopped && !IsDisposed)
                errorMessage = error is ArgumentException ? error.Message : "파일 처리 결과를 확인해 주세요. 같은 요청을 자동으로 다시 보내지 않습니다.";
        }
        finally
        {
            operating = false;
            if (!stopped && !IsDisposed)
            {
                RefreshSelection();
                if (selection.IsCurrent && errorMessage is not null) txtState.Text = errorMessage;
            }
        }
    }

    private async void Add_Click(object? sender, EventArgs e)
    {
        if (!CanChange || selection is null) return;
        string? path = ChooseFile();
        if (path is not null && CanChange)
            await RunAsync(ct => selection.AddFileAsync(path, ct));
    }

    private async void Upload_Click(object? sender, EventArgs e)
    {
        if (!CanChange || selection is null || SelectedItem is not { CanUpload: true } item) return;
        await RunAsync(ct => selection.UploadAsync(item.Key, ct));
    }

    private async void Retry_Click(object? sender, EventArgs e)
    {
        if (!CanChange || selection is null || SelectedItem is not { CanRetry: true } item) return;
        await RunAsync(ct => selection.RetryAsync(item.Key, ct));
    }

    private async void Delete_Click(object? sender, EventArgs e)
    {
        if (!CanChange || selection is null || SelectedItem is not { CanDiscard: true } item) return;
        // Capture the shown file before the modal confirmation; a changed selection
        // or delayed callback must never redirect DELETE to a different file.
        Guid target = item.Key;
        if (!ConfirmFileDeletion(item) || !CanChange
            || selection.Items.FirstOrDefault(x => x.Key == target)?.CanDiscard != true) return;
        await RunAsync(ct => selection.DiscardAsync(target, ct));
    }

    private void Remove_Click(object? sender, EventArgs e)
    {
        if (!CanChange || selection is null || SelectedItem is not { CanRemove: true } item) return;
        Guid target = item.Key;
        if (!ConfirmLocalRemoval(item) || !CanChange
            || selection.Items.FirstOrDefault(x => x.Key == target)?.CanRemove != true) return;
        selection.RemoveLocal(target);
        RefreshSelection();
    }

    private void Close_Click(object? sender, EventArgs e) => Close();

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (selection?.IsCurrent == true && (operating || selection.IsBusy) && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            txtState.Text = "파일 처리 중입니다. 응답 또는 결과 확인 상태가 될 때까지 기다려 주세요.";
        }
        base.OnFormClosing(e);
    }

    private void Stop()
    {
        if (stopped) return;
        stopped = true;
        if (selection is not null) selection.Changed -= SelectionChanged;
        lifetime.Cancel();
        lifetime.Dispose();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    { Stop(); base.OnFormClosed(e); }
}
