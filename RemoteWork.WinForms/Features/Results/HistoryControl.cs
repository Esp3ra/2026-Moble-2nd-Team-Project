using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Results;

public partial class HistoryControl : UserControl
{
    public HistoryControl()
    {
        InitializeComponent();
        BuildPreview();
    }

    private void BuildPreview()
    {
        var filters = UiKit.Toolbar(
            UiKit.Combo("전체 업무", "WRK-2026-002 · 월간 운영 현황 정리"),
            UiKit.Combo("전체 변경 유형", "생성", "담당자 변경", "상태 변경", "진행률 변경", "결과 제출", "반려", "승인"),
            UiKit.Button("조회"));

        var history = UiKit.Grid(
            ["업무 번호", "수행자", "변경 시각", "변경 유형", "변경 내용 (예시)"],
            ["WRK-2026-002", "이서연 · 관리자", "2026-10-22 10:30", "승인", "제출 v2 검토 승인"],
            ["WRK-2026-002", "김민수 · 직원", "2026-10-21 15:30", "결과 제출", "v2 재제출 · 검토 의견 반영"],
            ["WRK-2026-002", "이서연 · 관리자", "2026-10-20 16:10", "반려", "누락된 운영 지표 보완 요청"],
            ["WRK-2026-002", "김민수 · 직원", "2026-10-20 14:00", "결과 제출", "v1 최초 제출 · PDF 1개 첨부"],
            ["WRK-2026-002", "김민수 · 직원", "2026-10-20 13:40", "진행률 변경", "진행률 60% → 100%"],
            ["WRK-2026-002", "김민수 · 직원", "2026-10-18 09:10", "상태 변경", "대기 → 진행 중"],
            ["WRK-2026-002", "이서연 · 관리자", "2026-10-17 11:00", "담당자 변경", "미배정 → 김민수"],
            ["WRK-2026-002", "이서연 · 관리자", "2026-10-17 10:00", "생성", "월간 운영 현황 정리 업무 생성"]);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        content.Controls.Add(filters, 0, 0);
        content.Controls.Add(UiKit.Card("업무 변경 이력 · 고정 예시 8건", history), 0, 1);
        content.Controls.Add(UiKit.Card("이력 상세 영역 · 예시", UiKit.ReadOnlyText(
            "WRK-2026-002 · 월간 운영 현황 정리\r\n수행자: 이서연 (관리자)   |   변경 시각: 2026-10-22 10:30   |   유형: 승인\r\n변경 내용: v2 제출 결과를 검토하고 승인한 예시입니다. 이 화면은 이력 위치와 표시 항목을 확인하는 골격입니다.")), 0, 2);

        UiKit.Page(this, "업무 이력", "누가, 언제, 무엇을 바꿨는지 표시할 화면입니다. 모든 기록은 고정 예시이며 조회와 기록 저장은 작동하지 않습니다.", content);
    }
}
