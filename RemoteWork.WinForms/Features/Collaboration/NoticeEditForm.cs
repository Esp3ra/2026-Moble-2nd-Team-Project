using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration
{
    public partial class NoticeEditForm : Form
    {
        private readonly INoticeService noticeService;
        private readonly CancellationTokenSource lifetime = new();
        private readonly TextBox saveError = new()
        {
            ReadOnly = true, Multiline = true, Dock = DockStyle.Fill,
            ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(242, 245, 250), ForeColor = Color.DarkRed,
            Visible = false, TabStop = false
        };
        private bool saving;
        private bool unknownSave;
        private readonly List<ResourceAttachment> uploadedFiles = new();
        private ResourceAttachmentClient? attachmentClient;
        private bool lifetimeEnded;

        public NoticeEditForm() : this(new HttpNoticeService(AppServices.Api)) { }

        public NoticeEditForm(INoticeService noticeService)
        {
            this.noticeService = noticeService;
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeNoticeView();
            // Use the existing spare title row; keep the notice-body layout unchanged.
            layoutContent2.Controls.Add(saveError, 0, 1);
            txtNoticeTitle.MaxLength = 200;
            txtNoticeBody.MaxLength = 5000;
            txtAttachments.PlaceholderText = "첨부파일 선택 후 등록 시 전송합니다. 최대 10개 · 파일당 25MiB";
            FormClosing += (_, e) =>
            {
                // Do not suggest that closing cancels an already submitted DB commit.
                if (saving && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
            };
            Disposed += (_, _) =>
            {
                if (lifetimeEnded) return;
                lifetimeEnded = true;
                lifetime.Cancel();
                lifetime.Dispose();
            };
        }

        private void InitializeNoticeView()
        {
            cmbNoticeAudience.SelectedIndex = 0;
        }

        public string[] SelectedAttachmentPaths { get; private set; } = Array.Empty<string>();
        public Notice? CreatedNotice { get; private set; }

        private void SelectAttachment_Click(object? sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "공지사항 첨부파일 선택",
                Filter = "모든 파일 (*.*)|*.*",
                Multiselect = true,
                CheckFileExists = true,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            if (dialog.FileNames.Length > 10 || dialog.FileNames.Any(p => new FileInfo(p).Length > 26214400))
            { MessageBox.Show(this, "최대 10개, 파일당 25MiB까지 선택해 주세요."); return; }
            SelectedAttachmentPaths = dialog.FileNames;
            uploadedFiles.Clear();
            txtAttachments.Lines = SelectedAttachmentPaths.Select(
                path => System.IO.Path.GetFileName(path)).ToArray();
        }

        private async void PublishNotice_Click(object? sender, EventArgs e) => await PublishAsync();

        public async Task PublishAsync()
        {
            if (saving || IsDisposed || unknownSave) return;
            if (string.IsNullOrWhiteSpace(txtNoticeTitle.Text) ||
                string.IsNullOrWhiteSpace(txtNoticeBody.Text))
            {
                MessageBox.Show(this, "공지사항 제목과 내용을 입력해 주세요.", "공지사항 등록",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            saving = true;
            saveError.Visible = false;
            saveError.Clear();
            btnPublishNotice.Enabled = btnClose.Enabled = panelContent.Enabled = false;
            btnPublishNotice.Text = "저장 중…";
            try
            {
                if (SelectedAttachmentPaths.Length > 0)
                {
                    attachmentClient ??= new ResourceAttachmentClient(noticeService is HttpNoticeService http ? http.AttachmentIdentity() : AppServices.Api.CaptureSession());
                    foreach (var path in SelectedAttachmentPaths.Skip(uploadedFiles.Count))
                        uploadedFiles.Add(await attachmentClient.UploadAsync("notice", null, path, lifetime.Token));
                }
                // The legacy notice API has no durable original-result lookup.
                // Freeze an unknown original save; never generate a second notice
                // or discard its possibly linked uploads automatically.
                unknownSave = true;
                var saved = await noticeService.CreateNoticeAsync(new CreateNoticeRequest(
                    txtNoticeTitle.Text.Trim(), txtNoticeBody.Text.Trim(), cmbNoticeAudience.Text,
                    chkPinToTop.Checked, Array.Empty<string>())
                    { AttachmentIds = uploadedFiles.Select(f => f.AttachmentId).ToArray() }, lifetime.Token);
                if (IsDisposed) return;
                unknownSave = false;
                CreatedNotice = saved;
                saving = false;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (ApiException ex)
            {
                if (ex.StatusCode is >= 400 and < 500) unknownSave = false;
                if (!IsDisposed) ShowSaveError(ex.Message);
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed) ShowSaveError("접속이 변경되어 저장 결과를 확인하지 못했습니다. 다시 로그인한 후 목록을 확인해 주세요.");
            }
            catch (Exception ex)
            {
                if (!IsDisposed) ShowSaveError("첨부 또는 등록 처리 실패: " + ex.Message);
            }
            finally
            {
                saving = false;
                if (!IsDisposed)
                {
                    btnPublishNotice.Enabled = panelContent.Enabled = !unknownSave;
                    btnClose.Enabled = true;
                    btnPublishNotice.Text = "공지사항 등록";
                }
            }
        }

        private void ShowSaveError(string message)
        {
            // Keep every input intact and do not return OK on a failed/unknown save.
            saveError.Text = message + "\r\n입력·첨부 ID는 유지됩니다. 저장 결과가 불명확하면 이 창에서 재등록을 막습니다. 목록에서 저장 여부를 먼저 확인해 주세요.";
            saveError.Visible = true;
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

        private void ClosePreview_Click(object? sender, EventArgs e)
        {
            Close();
        }

    }
}
