using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks;

public partial class TaskEditForm : Form
{
    public TaskEditForm() : this(false) { }

    public TaskEditForm(bool editing)
    {
        InitializeComponent();
        var startDate = SampleDate(2026, 10, 15);
        var deadline = SampleDate(2026, 10, 22);
        var fields = UiKit.Fields(
            ("업무 제목", UiKit.Input(editing ? "주간 업무 자료 정리" : "")),
            ("상세 내용", UiKit.Input(editing ? "주간 업무 자료를 모아 항목별로 정리합니다." : "", true)),
            ("담당자", UiKit.Combo(editing
                ? new[] { "김직원 (샘플)", "이직원 (샘플)", "박직원 (샘플)" }
                : new[] { "담당자 선택", "김직원 (샘플)", "이직원 (샘플)", "박직원 (샘플)" })),
            ("우선순위", UiKit.Combo("보통 (샘플 값)", "높음 (샘플 값)", "낮음 (샘플 값)")),
            ("시작일", startDate),
            ("마감일", deadline),
            ("첨부파일", UiKit.ReadOnlyText(editing ? "업무안내_샘플.pdf · 240 KB\r\n실제 파일은 연결되어 있지 않습니다." : "첨부파일 표시 영역\r\n실제 파일은 선택하거나 업로드하지 않습니다.")));
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty,
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.Controls.Add(fields, 0, 0);
        content.Controls.Add(UiKit.Toolbar(UiKit.Button("첨부파일 선택")), 0, 1);

        UiKit.Dialog(this, editing ? "업무 수정" : "업무 등록",
            "입력 항목 배치용 창입니다. 확인 버튼은 업무를 저장하거나 변경하지 않습니다.", content,
            UiKit.Button(editing ? "수정 확인" : "등록 확인", primary: true),
            UiKit.Button("취소", () => Close()));
    }

    private static DateTimePicker SampleDate(int year, int month, int day) => new()
    {
        Dock = DockStyle.Fill,
        Format = DateTimePickerFormat.Custom,
        CustomFormat = "yyyy-MM-dd",
        Value = new DateTime(year, month, day),
        Margin = new Padding(0, 4, 0, 4),
    };
}
