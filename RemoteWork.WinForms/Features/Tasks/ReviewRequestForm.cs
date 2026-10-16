using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Tasks;

public partial class ReviewRequestForm : Form
{
    public ReviewRequestForm()
    {
        InitializeComponent();
        var fields = UiKit.Fields(
            ("대상 업무", UiKit.ReadOnlyText("TASK-001 · 주간 업무 자료 정리 (샘플)")),
            ("현재 상태", UiKit.ReadOnlyText("진행중 (샘플)")),
            ("현재 마감일", UiKit.ReadOnlyText("2026-10-22 (샘플)")),
            ("검토 요청 내용", UiKit.Input("", true)),
            ("결과물 표시", UiKit.ReadOnlyText("결과 제출 화면 연결 영역\r\n이 창에서 결과물을 제출하거나 업무 상태를 변경하지 않습니다.")));
        UiKit.Dialog(this, "검토 요청",
            "마감 전 검토 요청 입력창의 기본 틀입니다. 요청 내용 필드는 구성 예시입니다.", fields,
            UiKit.Button("요청 확인", primary: true),
            UiKit.Button("취소", () => Close()));
    }
}
