using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks;

public partial class TasksControl : UserControl
{
    public TasksControl() : this(PreviewRole.Employee) { }

    public TasksControl(PreviewRole role)
    {
        InitializeComponent();

        var actions = role == PreviewRole.Administrator
            ? UiKit.Toolbar(
                UiKit.Button("업무 등록 창 보기", () => UiKit.OpenPreview(this, new TaskEditForm()), true),
                UiKit.Button("수정 창 보기", () => UiKit.OpenPreview(this, new TaskEditForm(true))),
                UiKit.Button("업무 취소"))
            : UiKit.Toolbar(
                UiKit.Button("업무 수락"),
                UiKit.Button("검토 요청 창 보기", () => UiKit.OpenPreview(this, new ReviewRequestForm()), true),
                UiKit.Button("마감 연장 창 보기", () => UiKit.OpenPreview(this, new DeadlineExtensionForm())));

        // Each role receives its own fixed sample table; no filtering or state change occurs.
        var columns = role == PreviewRole.Administrator
            ? new[] { "업무 번호", "업무 제목", "담당자", "상태", "우선순위", "시작일", "마감일" }
            : new[] { "업무 번호", "업무 제목", "상태", "우선순위", "시작일", "마감일" };
        var rows = role == PreviewRole.Administrator
            ? new[]
            {
                new[] { "TASK-001", "주간 업무 자료 정리", "김직원", "진행중", "높음", "2026-10-15", "2026-10-22" },
                new[] { "TASK-002", "회의 결과 보고서 작성", "김직원", "대기", "보통", "2026-10-16", "2026-10-23" },
                new[] { "TASK-003", "프로젝트 검토 자료 준비", "이직원", "제출완", "높음", "2026-10-14", "2026-10-21" },
                new[] { "TASK-004", "팀 자료 목록 정리", "박직원", "반려", "보통", "2026-10-13", "2026-10-20" },
                new[] { "TASK-005", "월간 업무 결과 기록", "김직원", "승인완", "낮음", "2026-10-12", "2026-10-19" }
            }
            : new[]
            {
                new[] { "TASK-001", "주간 업무 자료 정리", "진행중", "높음", "2026-10-15", "2026-10-22" },
                new[] { "TASK-002", "회의 결과 보고서 작성", "대기", "보통", "2026-10-16", "2026-10-23" },
                new[] { "TASK-005", "월간 업무 결과 기록", "승인완", "낮음", "2026-10-12", "2026-10-19" }
            };
        var tasks = UiKit.Grid(columns, rows);

        var details = UiKit.ReadOnlyText(
            "[고정 샘플 상세 · TASK-001]\r\n\r\n" +
            "업무 제목   주간 업무 자료 정리\r\n" +
            "담당자      김직원\r\n" +
            "우선순위    높음\r\n" +
            "현재 상태   진행중\r\n" +
            "진행률      40% (샘플 표시)\r\n\r\n" +
            "시작일      2026-10-15\r\n" +
            "현재 마감일 2026-10-22\r\n" +
            "변경 마감일 — (변경 내역 표시 영역)\r\n\r\n" +
            "상세 내용\r\n주간 업무 자료를 모아 항목별로 정리합니다.\r\n\r\n" +
            "표에서 선택한 행은 상세와 연동되지 않습니다.\r\n" +
            "상태·진행률·날짜는 미리 배치한 샘플 값입니다.");
        var attachments = UiKit.Grid(
            new[] { "첨부파일 (샘플)", "크기", "등록자" },
            new[] { "업무안내_샘플.pdf", "240 KB", "관리자" });
        var detailLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
        };
        detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 218));
        detailLayout.Controls.Add(UiKit.Card("업무 상세 · 고정 샘플", details), 0, 0);
        detailLayout.Controls.Add(UiKit.Card("업무 첨부파일", attachments), 0, 1);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.Controls.Add(actions, 0, 0);
        content.Controls.Add(UiKit.Split(UiKit.Card(role == PreviewRole.Administrator ? "전체 업무 · 샘플" : "내 업무 · 샘플", tasks), detailLayout, 60), 0, 1);

        UiKit.Page(this, "업무 관리", "화면 구성 확인용 샘플입니다. 업무와 상태는 저장하거나 변경하지 않습니다.", content);
    }
}
