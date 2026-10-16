using System.ComponentModel;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Results
{
    public partial class HistoryControl : UserControl
    {
        public HistoryControl()
        {
            InitializeComponent();
            if (PreviewActions.IsDesignMode(this)) return;
            InitializeSampleData();
        }

        private void InitializeSampleData()
        {
            cmbOptions.SelectedIndex = 0;
            cmbOptions2.SelectedIndex = 0;
            gridHistory.Rows.Add(new object[] { "WRK-2026-002", "이서연 · 관리자", "2026-10-22 10:30", "승인", "제출 v2 검토 승인" });
            gridHistory.Rows.Add(new object[] { "WRK-2026-002", "김민수 · 직원", "2026-10-21 15:30", "결과 제출", "v2 재제출 · 검토 의견 반영" });
            gridHistory.Rows.Add(new object[] { "WRK-2026-002", "이서연 · 관리자", "2026-10-20 16:10", "반려", "누락된 운영 지표 보완 요청" });
            gridHistory.Rows.Add(new object[] { "WRK-2026-002", "김민수 · 직원", "2026-10-20 14:00", "결과 제출", "v1 최초 제출 · PDF 1개 첨부" });
            gridHistory.Rows.Add(new object[] { "WRK-2026-002", "김민수 · 직원", "2026-10-20 13:40", "진행률 변경", "진행률 60% → 100%" });
            gridHistory.Rows.Add(new object[] { "WRK-2026-002", "김민수 · 직원", "2026-10-18 09:10", "상태 변경", "대기 → 진행 중" });
            gridHistory.Rows.Add(new object[] { "WRK-2026-002", "이서연 · 관리자", "2026-10-17 11:00", "담당자 변경", "미배정 → 김민수" });
            gridHistory.Rows.Add(new object[] { "WRK-2026-002", "이서연 · 관리자", "2026-10-17 10:00", "생성", "월간 운영 현황 정리 업무 생성" });
        }

        private void Unavailable_Click(object? sender, EventArgs e)
        {
            PreviewActions.ShowUnavailable(this);
        }

    }
}
