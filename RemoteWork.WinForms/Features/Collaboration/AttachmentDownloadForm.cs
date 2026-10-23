namespace RemoteWork.WinForms.Features.Collaboration;

internal sealed class AttachmentDownloadForm : Form
{
    private readonly ListBox list = new() { Dock = DockStyle.Fill, DisplayMember = "Name" };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 60 };
    private readonly Button save = new() { Dock = DockStyle.Bottom, Text = "선택 파일 다운로드", Height = 36, Enabled = false };
    private readonly CancellationTokenSource lifetime = new();
    internal AttachmentDownloadForm(ResourceAttachmentClient client, string kind, string resource)
    {
        Text = "첨부파일 다운로드"; Size = new Size(540, 360); StartPosition = FormStartPosition.CenterParent;
        Controls.Add(list); Controls.Add(status); Controls.Add(save);
        Shown += async (_, _) =>
        {
            try
            {
                status.Text = "첨부 목록 조회 중…";
                var files = await client.ListAsync(kind, resource, lifetime.Token);
                if (IsDisposed) return;
                list.Items.AddRange(files); status.Text = files.Length == 0 ? "연결된 첨부파일이 없습니다." : "저장할 파일을 선택해 주세요.";
                if (files.Length > 0) list.SelectedIndex = 0;
                save.Enabled = files.Length > 0;
            }
            catch (Exception ex) { if (!IsDisposed) status.Text = ex.Message; }
        };
        save.Click += async (_, _) =>
        {
            if (list.SelectedItem is not ResourceAttachment file) return;
            using var dialog = new SaveFileDialog { FileName = file.Name, RestoreDirectory = true };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            save.Enabled = false;
            try
            {
                var bytes = await client.DownloadAsync(kind, resource, file, lifetime.Token);
                string temporary = Path.Combine(Path.GetDirectoryName(dialog.FileName)!, "." + Guid.NewGuid().ToString("N") + ".download");
                try
                {
                    await File.WriteAllBytesAsync(temporary, bytes, lifetime.Token);
                    lifetime.Token.ThrowIfCancellationRequested();
                    File.Move(temporary, dialog.FileName, true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
                if (!IsDisposed) status.Text = "크기·SHA-256 검증 후 다운로드했습니다.";
            }
            catch (Exception ex) { if (!IsDisposed) status.Text = ex.Message; }
            finally { if (!IsDisposed) save.Enabled = true; }
        };
        Disposed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); };
    }
}
