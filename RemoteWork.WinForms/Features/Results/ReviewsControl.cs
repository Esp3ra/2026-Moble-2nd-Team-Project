using System.ComponentModel;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Results
{
    public partial class ReviewsControl : UserControl
    {
        public ReviewsControl() : this(PreviewRole.Employee) { }

        public ReviewsControl(PreviewRole role)
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData(role);
        }

        private void InitializeSampleData(PreviewRole role)
        {
            bool administrator = role == PreviewRole.Administrator;
            cmbOptions.SelectedIndex = 0;
            if (administrator)
            {
                gridReviews.Rows.Add(new object[] { "WRK-2026-002", "월간 운영 현황 정리", "김민수", "v2", "2026-10-21 15:30", "검토 대기" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-003", "원격 근무 가이드 초안", "박지은", "v1", "2026-10-20 14:00", "반려" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-001", "요구사항 목록 작성", "김민수", "v1", "2026-10-19 11:20", "승인" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-008", "업무 연락 체계 정리", "이직원", "v1", "2026-10-22 10:00", "검토 대기" });
            }
            else
            {
                gridReviews.Rows.Add(new object[] { "WRK-2026-003", "원격 근무 가이드 초안", "박지은", "v1", "2026-10-20 14:00", "반려" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-006", "가이드 참고 자료 정리", "박지은", "v1", "2026-10-21 15:30", "검토 대기" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-007", "공지 항목 목록 작성", "박지은", "v1", "2026-10-19 11:20", "승인" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-009", "가이드 검토 항목 정리", "박지은", "v1", "2026-10-22 10:00", "검토 대기" });
            }
            if (administrator)
            {
                gridAttachments.Rows.Add(new object[] { "월간_운영현황_v2.pdf", "v2", "184 KB" });
                gridAttachments.Rows.Add(new object[] { "검토_참고자료.xlsx", "v1", "42 KB" });
            }
            else
            {
                gridAttachments.Rows.Add(new object[] { "원격근무_가이드_v1.pdf", "v1", "184 KB" });
                gridAttachments.Rows.Add(new object[] { "검토_참고자료.xlsx", "v1", "42 KB" });
            }
            if (administrator)
            {
                gridVersions.Rows.Add(new object[] { "v2", "2026-10-21 15:30", "검토 대기", "검토 의견 반영 및 누락 항목 보완" });
                gridVersions.Rows.Add(new object[] { "v1", "2026-10-20 14:00", "반려", "초안 최초 제출" });
            }
            else
            {
                gridVersions.Rows.Add(new object[] { "v1", "2026-10-20 14:00", "반려", "초안 최초 제출" });
            }
            btnSearch.Visible = administrator;
            btnApprovePreview.Visible = administrator;
            btnRejectPreview.Visible = administrator;
            txtContent.Visible = administrator;
            btnQuery.Visible = !administrator;
            btnSubmissionPreview.Visible = !administrator;
            if (!administrator)
            {
                lblPageTitle.Text = "결과 제출 및 확인";
                lblReviewListTitle.Text = "내 결과 제출 목록";
                lblResultDetailTitle.Text = "내 결과와 반려 사유";
                txtContent2.Text = "WRK-2026-003 · 원격 근무 가이드 초안\r\n담당자: 박지은   |   검토 상태: 반려\r\n제출 버전: v1   |   제출일: 2026-10-20 14:00\r\n\r\n결과 설명\r\n원격 근무 절차와 업무 결과 제출 가이드를 초안으로 정리했습니다.\r\n\r\n반려 사유\r\n제출 기준과 예외 상황 설명이 부족합니다. 검토 의견을 반영한 후 결과를 다시 제출해 주세요.";
            }
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

        private void btnApprovePreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new ReviewDecisionForm(true));
        }

        private void btnRejectPreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new ReviewDecisionForm(false));
        }

        private void btnSubmissionPreview_Click(object? sender, EventArgs e)
        {
            PreviewActions.Open(this, new SubmissionForm());
        }

    }
}
