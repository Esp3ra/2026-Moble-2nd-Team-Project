using System.ComponentModel;
using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration
{
    public partial class ImportantNoticeForm : Form
    {
        private readonly Func<CancellationToken, Task>? confirm;
        private readonly CancellationTokenSource lifetime = new();
        private bool confirming;
        private bool lifetimeEnded;

        public ImportantNoticeForm()
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData();
            btnConfirm.Visible = false;
            Disposed += (_, _) =>
            {
                if (lifetimeEnded) return;
                lifetimeEnded = true;
                lifetime.Cancel();
                lifetime.Dispose();
            };
            FormClosing += (_, e) =>
            {
                if (confirming && e.CloseReason == CloseReason.UserClosing) e.Cancel = true;
            };
        }

        public ImportantNoticeForm(Notice notice) : this()
        {
            lblText2.Text = notice.Title;
            txtContent.Text = notice.GetDisplayText();
        }

        public ImportantNoticeForm(Notice notice, Func<CancellationToken, Task> confirm) : this(notice)
        {
            this.confirm = confirm;
            btnConfirm.Visible = true;
            btnClose.Text = "나중에";
        }

        private async void ConfirmNotice_Click(object? sender, EventArgs e)
        {
            if (confirming || confirm is null || IsDisposed) return;
            confirming = true;
            btnConfirm.Enabled = btnClose.Enabled = false;
            btnConfirm.Text = "저장 중…";
            txtConfirmationError.Visible = false;
            try
            {
                await confirm(lifetime.Token);
                if (IsDisposed) return;
                confirming = false;
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (ApiException ex)
            {
                if (!IsDisposed)
                {
                    txtConfirmationError.Text = ex.Message + "\r\n확인 버튼을 눌러 다시 시도할 수 있습니다.";
                    txtConfirmationError.Visible = true;
                }
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed)
                {
                    txtConfirmationError.Text = "접속이 변경되었습니다. 다시 로그인한 후 확인해 주세요.";
                    txtConfirmationError.Visible = true;
                }
            }
            finally
            {
                confirming = false;
                if (!IsDisposed)
                {
                    btnConfirm.Enabled = btnClose.Enabled = true;
                    btnConfirm.Text = "확인";
                }
            }
        }

        private void InitializeSampleData()
        {
        }

        private void ClosePreview_Click(object? sender, EventArgs e)
        {
            Close();
        }

    }
}
