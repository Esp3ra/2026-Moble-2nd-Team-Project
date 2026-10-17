using System.ComponentModel;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Collaboration
{
    public partial class NoticeEditForm : Form
    {
        public NoticeEditForm()
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData();
        }

        private void InitializeSampleData()
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

            SelectedAttachmentPaths = dialog.FileNames;
            txtAttachments.Lines = SelectedAttachmentPaths.Select(
                path => System.IO.Path.GetFileName(path)).ToArray();
        }

        private void PublishNotice_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNoticeTitle.Text) ||
                string.IsNullOrWhiteSpace(txtNoticeBody.Text))
            {
                MessageBox.Show(this, "공지사항 제목과 내용을 입력해 주세요.", "공지사항 등록",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            CreatedNotice = new Notice
            {
                Title = txtNoticeTitle.Text.Trim(),
                Content = txtNoticeBody.Text.Trim(),
                Target = cmbNoticeAudience.Text,
                IsPinned = chkPinToTop.Checked,
                AttachmentPaths = SelectedAttachmentPaths.ToArray()
            };
            DialogResult = DialogResult.OK;
            Close();
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
