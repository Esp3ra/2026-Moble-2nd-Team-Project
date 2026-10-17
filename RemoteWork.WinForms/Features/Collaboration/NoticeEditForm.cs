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
            txtAttachments.PlaceholderText = "���ϸ��� ����˴ϴ�. ���ε塤�ٿ�ε�� ���� ������� �ʾҽ��ϴ�.";
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
                Title = "�������� ÷������ ����",
                Filter = "��� ���� (*.*)|*.*",
                Multiselect = true,
                CheckFileExists = true,
                RestoreDirectory = true
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            SelectedAttachmentPaths = dialog.FileNames;
            txtAttachments.Lines = SelectedAttachmentPaths.Select(
                path => System.IO.Path.GetFileName(path)).ToArray();
        }

        private async void PublishNotice_Click(object? sender, EventArgs e) => await PublishAsync();

        public async Task PublishAsync()
        {
            if (saving || IsDisposed) return;
            if (string.IsNullOrWhiteSpace(txtNoticeTitle.Text) ||
                string.IsNullOrWhiteSpace(txtNoticeBody.Text))
            {
                MessageBox.Show(this, "�������� ����� ������ �Է��� �ּ���.", "�������� ���",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            saving = true;
            saveError.Visible = false;
            saveError.Clear();
            btnPublishNotice.Enabled = btnClose.Enabled = panelContent.Enabled = false;
            btnPublishNotice.Text = "���� �ߡ�";
            try
            {
                var saved = await noticeService.CreateNoticeAsync(new CreateNoticeRequest(
                    txtNoticeTitle.Text.Trim(), txtNoticeBody.Text.Trim(), cmbNoticeAudience.Text,
                    chkPinToTop.Checked, SelectedAttachmentPaths.Select(Path.GetFileName)
                        .Select(name => name ?? "").ToArray()), lifetime.Token);
                if (IsDisposed) return;
                CreatedNotice = saved;
                saving = false;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (ApiException ex)
            {
                if (!IsDisposed) ShowSaveError(ex.Message);
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed) ShowSaveError("������ ����Ǿ� ���� ����� Ȯ������ ���߽��ϴ�. �ٽ� �α����� �� ����� Ȯ���� �ּ���.");
            }
            finally
            {
                saving = false;
                if (!IsDisposed)
                {
                    btnPublishNotice.Enabled = btnClose.Enabled = panelContent.Enabled = true;
                    btnPublishNotice.Text = "�������� ���";
                }
            }
        }

        private void ShowSaveError(string message)
        {
            // Keep every input intact and do not return OK on a failed/unknown save.
            saveError.Text = message + "\r\n�Է� ������ �����˴ϴ�. ��� ������� ���� ���� ��Ͽ��� ���� ���θ� Ȯ���� �ּ���.";
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
