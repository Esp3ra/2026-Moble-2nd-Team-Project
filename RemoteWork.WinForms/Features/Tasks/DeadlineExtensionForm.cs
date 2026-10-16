using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks;

public partial class DeadlineExtensionForm : Form
{
    public DeadlineExtensionForm()
    {
        InitializeComponent();
        var requestedDeadline = new DateTimePicker
        {
            Dock = DockStyle.Fill,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "yyyy-MM-dd",
            Value = new DateTime(2026, 10, 26),
            Margin = new Padding(0, 4, 0, 4),
        };
        var fields = UiKit.Fields(
            ("대상 업무", UiKit.ReadOnlyText("TASK-001 · 주간 업무 자료 정리 (샘플)")),
            ("현재 마감일", UiKit.ReadOnlyText("2026-10-22 (샘플)")),
            ("요청 마감일", requestedDeadline),
            ("연장 사유", UiKit.Input("", true)),
            ("변경 반영 안내", UiKit.ReadOnlyText("마감 연장과 알림 반영은 이후 연결할 영역입니다.\r\n현재 마감일과 요청 상태는 변경되지 않습니다.")));
        UiKit.Dialog(this, "마감 연장 요청",
            "연장 요청 입력 항목의 배치용 창입니다. 요청과 마감일 변경은 동작하지 않습니다.", fields,
            UiKit.Button("요청 확인", primary: true),
            UiKit.Button("취소", () => Close()));
    }
}
