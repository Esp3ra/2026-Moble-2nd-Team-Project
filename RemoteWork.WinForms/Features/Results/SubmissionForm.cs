using System.ComponentModel;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Results
{
    public partial class SubmissionForm : Form
    {
        public SubmissionForm() : this(PreviewRole.Employee) { }

        public SubmissionForm(PreviewRole role)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData(role);
        }

        private void InitializeSampleData(PreviewRole role)
        {
            if (role == PreviewRole.Administrator)
            {
                txtTaskSummary.Text = "WRK-2026-010 · 운영 개선 계획";
                txtSubmissionVersion.Text = "이관리자 / v2 제출 예정";
                txtResultDescription.Text = "추진 일정과 담당 구분 설명을 보완했습니다.";
                txtContent4.Text = "이전 제출 v1 · 2026-10-20 14:00 · 반려완료\r\n검토 구분: 상위 검토자 검토 대상\r\n반려 사유: 추진 일정과 담당 구분 설명을 보완해 주세요.";
                gridPendingFiles.Rows.Add(new object[] { "운영개선_계획_v2.pdf", "210 KB", "결과 문서" });
                gridPendingFiles.Rows.Add(new object[] { "운영_참고자료.xlsx", "38 KB", "참고 자료" });
                gridPreviousFiles.Rows.Add(new object[] { "운영개선_계획_v1.pdf", "v1", "2026-10-20 14:00" });
            }
            else
            {
                gridPendingFiles.Rows.Add(new object[] { "원격근무_가이드_v2.pdf", "210 KB", "결과 문서" });
                gridPendingFiles.Rows.Add(new object[] { "예외상황_정리.xlsx", "38 KB", "참고 자료" });
                gridPreviousFiles.Rows.Add(new object[] { "원격근무_가이드_v1.pdf", "v1", "2026-10-20 14:00" });
            }
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
