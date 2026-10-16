using System.ComponentModel;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Results
{
    public partial class SubmissionForm : Form
    {
        public SubmissionForm()
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData();
        }

        private void InitializeSampleData()
        {
            gridPendingFiles.Rows.Add(new object[] { "원격근무_가이드_v2.pdf", "210 KB", "결과 문서" });
            gridPendingFiles.Rows.Add(new object[] { "예외상황_정리.xlsx", "38 KB", "참고 자료" });
            gridPreviousFiles.Rows.Add(new object[] { "원격근무_가이드_v1.pdf", "v1", "2026-10-20 14:00" });
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
