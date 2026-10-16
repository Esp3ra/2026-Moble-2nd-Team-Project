using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Results
{
    public partial class ReviewsControl : UserControl
    {
        private readonly PreviewRole previewRole;
        private bool previewReady;

        public ReviewsControl() : this(PreviewRole.Employee) { }

        public ReviewsControl(PreviewRole role)
        {
            previewRole = role;
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            cmbResultView.Visible = role == PreviewRole.Administrator;
            previewReady = true;
            ShowScopePreview();
        }

        private void ShowScopePreview()
        {
            bool administrator = previewRole == PreviewRole.Administrator;
            bool reviewing = administrator && cmbResultView.SelectedIndex == 1;
            cmbOptions.SelectedIndex = 0;
            gridReviews.Rows.Clear();
            gridAttachments.Rows.Clear();
            gridVersions.Rows.Clear();
            if (reviewing)
            {
                gridReviews.Rows.Add(new object[] { "WRK-2026-002", "월간 운영 현황 정리", "김민수", "v2", "2026-10-21 15:30", "제출완료" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-003", "원격 근무 가이드 초안", "김직원", "v1", "2026-10-20 14:00", "반려완료" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-001", "요구사항 목록 작성", "김민수", "v1", "2026-10-19 11:20", "승인완료" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-008", "업무 연락 체계 정리", "이직원", "v1", "2026-10-22 10:00", "제출완료" });
                gridAttachments.Rows.Add(new object[] { "월간_운영현황_v2.pdf", "v2", "184 KB" });
                gridAttachments.Rows.Add(new object[] { "검토_참고자료.xlsx", "v1", "42 KB" });
                gridVersions.Rows.Add(new object[] { "v2", "2026-10-21 15:30", "제출완료", "검토 의견 반영 및 누락 항목 보완" });
                gridVersions.Rows.Add(new object[] { "v1", "2026-10-20 14:00", "반려완료", "초안 최초 제출" });
                txtContent2.Text = "WRK-2026-002 · 월간 운영 현황 정리\r\n담당자: 김민수   |   업무 상태: 제출완료\r\n제출 버전: v2   |   제출일: 2026-10-21 15:30\r\n\r\n결과 설명\r\n운영 지표와 주간 이슈를 정리했습니다. 1차 검토 의견을 반영해 누락된 항목을 보완했습니다.\r\n\r\n검토 메모\r\n운영 지표와 첨부 문서를 확인해 주세요.";
            }
            else if (administrator)
            {
                gridReviews.Rows.Add(new object[] { "WRK-2026-010", "운영 개선 계획", "이관리자", "v1", "2026-10-20 14:00", "반려완료" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-011", "일정 점검 보고서", "이관리자", "v1", "2026-10-21 15:30", "제출완료" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-012", "팀 업무 분장표", "이관리자", "v1", "2026-10-19 11:20", "승인완료" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-013", "운영 점검 자료", "이관리자", "—", "—", "진행중" });
                gridAttachments.Rows.Add(new object[] { "운영개선_계획_v1.pdf", "v1", "184 KB" });
                gridAttachments.Rows.Add(new object[] { "운영_참고자료.xlsx", "v1", "42 KB" });
                gridVersions.Rows.Add(new object[] { "v1", "2026-10-20 14:00", "반려완료", "운영 개선 계획 최초 제출" });
                txtContent2.Text = "WRK-2026-010 · 운영 개선 계획\r\n담당자: 이관리자   |   업무 상태: 반려완료\r\n제출 버전: v1   |   제출일: 2026-10-20 14:00\r\n검토 구분: 상위 검토자 검토 대상\r\n\r\n결과 설명\r\n운영 개선 항목과 추진 일정을 정리했습니다.\r\n\r\n반려 사유\r\n추진 일정과 담당 구분 설명을 보완해 주세요.";
            }
            else
            {
                gridReviews.Rows.Add(new object[] { "WRK-2026-003", "원격 근무 가이드 초안", "김직원", "v1", "2026-10-20 14:00", "반려완료" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-006", "가이드 참고 자료 정리", "김직원", "v1", "2026-10-21 15:30", "제출완료" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-007", "공지사항 항목 목록 작성", "김직원", "v1", "2026-10-19 11:20", "승인완료" });
                gridReviews.Rows.Add(new object[] { "WRK-2026-009", "가이드 검토 항목 정리", "김직원", "—", "—", "진행중" });
                gridAttachments.Rows.Add(new object[] { "원격근무_가이드_v1.pdf", "v1", "184 KB" });
                gridAttachments.Rows.Add(new object[] { "검토_참고자료.xlsx", "v1", "42 KB" });
                gridVersions.Rows.Add(new object[] { "v1", "2026-10-20 14:00", "반려완료", "초안 최초 제출" });
                txtContent2.Text = "WRK-2026-003 · 원격 근무 가이드 초안\r\n담당자: 김직원   |   업무 상태: 반려완료\r\n제출 버전: v1   |   제출일: 2026-10-20 14:00\r\n\r\n결과 설명\r\n원격 근무 절차와 업무 결과 제출 가이드를 초안으로 정리했습니다.\r\n\r\n반려 사유\r\n제출 기준과 예외 상황 설명이 부족합니다. 검토 의견을 반영한 후 결과를 다시 제출해 주세요.";
            }
            btnSearch.Visible = reviewing;
            btnApprovePreview.Visible = reviewing;
            btnRejectPreview.Visible = reviewing;
            txtContent.Visible = reviewing;
            btnQuery.Visible = !reviewing;
            btnSubmissionPreview.Visible = !reviewing;
            lblPageTitle.Text = reviewing ? "결과물 검토" : "내 결과물";
            lblReviewListTitle.Text = reviewing ? "검토 대상 목록" : "내 결과물 목록";
            lblResultDetailTitle.Text = reviewing ? "제출 결과 확인" : "내 결과와 반려 사유";
        }

        private void cmbResultView_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (previewReady && !PreviewActions.IsDesignMode(this)) ShowScopePreview();
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
            PreviewActions.Open(this, new SubmissionForm(previewRole));
        }
    }
}
