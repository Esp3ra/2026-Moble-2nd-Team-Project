using System.ComponentModel;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Collaboration
{
    public partial class NoticesControl : UserControl
    {
        public NoticesControl() : this(PreviewRole.Employee) { }

        public NoticesControl(PreviewRole role)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData(role);
        }

        private void InitializeSampleData(PreviewRole role)
        {
            bool administrator = role == PreviewRole.Administrator;
            cmbOptions2.SelectedIndex = 0;
            gridNotices.Rows.Add(new object[] { "전 직원", "보안 점검 안내", "관리자", "2026.10.13" });
            gridNotices.Rows.Add(new object[] { "기획팀", "주간 업무 공유", "관리자", "2026.10.12" });
            gridNotices.Rows.Add(new object[] { "개발팀", "개발 일정 안내", "관리자", "2026.10.11" });
            btnNewNoticePreview.Visible = administrator;
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

        private void btnNewNoticePreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new NoticeEditForm());
        }

    }
}
