using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Results;

public partial class ReviewsControl : UserControl
{
    private readonly PreviewRole _role;

    public ReviewsControl() : this(PreviewRole.Employee)
    {
    }

    public ReviewsControl(PreviewRole role)
    {
        _role = role;
        InitializeComponent();
        BuildPreview();
    }

    private void BuildPreview()
    {
        bool administrator = _role == PreviewRole.Administrator;
        var list = administrator
            ? UiKit.Grid(
                ["업무 번호", "업무명", "담당자", "제출 버전", "제출일", "검토 상태"],
                ["WRK-2026-002", "월간 운영 현황 정리", "김민수", "v2", "2026-10-21 15:30", "검토 대기"],
                ["WRK-2026-003", "원격 근무 가이드 초안", "박지은", "v1", "2026-10-20 14:00", "반려"],
                ["WRK-2026-001", "요구사항 목록 작성", "김민수", "v1", "2026-10-19 11:20", "승인"])
            : UiKit.Grid(
                ["업무 번호", "업무명", "담당자", "제출 버전", "제출일", "검토 상태"],
                ["WRK-2026-003", "원격 근무 가이드 초안", "박지은", "v1", "2026-10-20 14:00", "반려"],
                ["WRK-2026-006", "가이드 참고 자료 정리", "박지은", "v1", "2026-10-21 15:30", "검토 대기"],
                ["WRK-2026-007", "공지 항목 목록 작성", "박지은", "v1", "2026-10-19 11:20", "승인"]);

        var toolbar = administrator
            ? UiKit.Toolbar(
                UiKit.Combo("전체 검토 상태", "검토 대기", "승인", "반려"),
                UiKit.Input("업무명 또는 담당자 검색"),
                UiKit.Button("검색"),
                UiKit.Button("승인 화면 미리보기", () => UiKit.OpenPreview(this, new ReviewDecisionForm(true)), true),
                UiKit.Button("반려 화면 미리보기", () => UiKit.OpenPreview(this, new ReviewDecisionForm(false))))
            : UiKit.Toolbar(
                UiKit.Combo("전체 검토 상태", "검토 대기", "승인", "반려"),
                UiKit.Button("조회"),
                UiKit.Button("결과 제출 화면 미리보기", () => UiKit.OpenPreview(this, new SubmissionForm()), true));

        var listLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        listLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        listLayout.Controls.Add(toolbar, 0, 0);
        listLayout.Controls.Add(list, 0, 1);

        var detailText = administrator
            ? "[고정 예시] WRK-2026-002 · 월간 운영 현황 정리\r\n담당자: 김민수   |   검토 상태: 검토 대기\r\n제출 버전: v2   |   제출일: 2026-10-21 15:30\r\n\r\n결과 설명\r\n운영 지표와 주간 이슈를 정리했습니다. 1차 검토 의견을 반영해 누락된 항목을 보완했습니다.\r\n\r\n검토 메모 예시\r\n내용과 첨부 문서를 확인한 뒤 승인 또는 반려 화면으로 이동하는 구조입니다."
            : "[고정 예시] WRK-2026-003 · 원격 근무 가이드 초안\r\n담당자: 박지은   |   검토 상태: 반려\r\n제출 버전: v1   |   제출일: 2026-10-20 14:00\r\n\r\n결과 설명\r\n원격 근무 절차와 업무 결과 제출 가이드를 초안으로 정리했습니다.\r\n\r\n반려 사유 예시\r\n제출 기준과 예외 상황 설명이 부족합니다. 검토 의견을 반영한 후 결과를 다시 제출해 주세요.";

        var attachments = UiKit.Grid(
            ["첨부 파일 (예시)", "제출 버전", "크기"],
            [administrator ? "월간_운영현황_v2.pdf" : "원격근무_가이드_v1.pdf", administrator ? "v2" : "v1", "184 KB"],
            ["검토_참고자료.xlsx", "v1", "42 KB"]);

        var versions = administrator
            ? UiKit.Grid(
                ["버전", "제출일", "상태", "제출 설명 (예시)"],
                ["v2", "2026-10-21 15:30", "검토 대기", "검토 의견 반영 및 누락 항목 보완"],
                ["v1", "2026-10-20 14:00", "반려", "초안 최초 제출"])
            : UiKit.Grid(
                ["버전", "제출일", "상태", "제출 설명 (예시)"],
                ["v1", "2026-10-20 14:00", "반려", "초안 최초 제출"]);

        var attachmentLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        attachmentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        attachmentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        attachmentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        attachmentLayout.Controls.Add(attachments, 0, 0);
        attachmentLayout.Controls.Add(UiKit.Toolbar(UiKit.Button("첨부 파일 다운로드")), 0, 1);

        var references = new TabControl { Dock = DockStyle.Fill, Font = new Font("맑은 고딕", 9) };
        var attachmentsTab = new TabPage("첨부 자료") { BackColor = Color.White, Padding = new Padding(8) };
        var versionsTab = new TabPage("제출 버전 이력") { BackColor = Color.White, Padding = new Padding(8) };
        attachmentsTab.Controls.Add(attachmentLayout);
        versionsTab.Controls.Add(versions);
        references.TabPages.Add(attachmentsTab);
        references.TabPages.Add(versionsTab);

        var detailLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var resultCard = UiKit.Card(administrator ? "제출 결과 확인 · 고정 예시" : "내 결과와 반려 사유 · 고정 예시", UiKit.ReadOnlyText(detailText));
        detailLayout.Controls.Add(resultCard, 0, 0);
        detailLayout.Controls.Add(UiKit.Card("첨부 자료와 버전 이력 · 고정 예시", references), 1, 0);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 43));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 57));
        content.Controls.Add(UiKit.Card(administrator ? "검토 대상 목록 · 예시" : "내 결과 제출 목록 · 예시", listLayout), 0, 0);
        content.Controls.Add(detailLayout, 0, 1);

        UiKit.Page(this,
            administrator ? "결과 검토" : "결과 제출 및 확인",
            "화면 골격 미리보기입니다. 목록과 상세는 고정 예시이며 제출·다운로드·승인·반려·검색은 처리되지 않습니다.",
            content);
    }
}
