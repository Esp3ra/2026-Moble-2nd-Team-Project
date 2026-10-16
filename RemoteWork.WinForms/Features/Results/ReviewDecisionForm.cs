using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Results;

public partial class ReviewDecisionForm : Form
{
    private readonly bool _approve;

    public ReviewDecisionForm() : this(true)
    {
    }

    public ReviewDecisionForm(bool approve = true)
    {
        _approve = approve;
        InitializeComponent();
        BuildPreview();
    }

    private void BuildPreview()
    {
        var fields = UiKit.Fields(
            ("검토 업무", UiKit.ReadOnlyText("WRK-2026-002 · 월간 운영 현황 정리")),
            ("담당자 / 제출 버전", UiKit.ReadOnlyText("김민수 / v2 · 검토 대기 (예시)")),
            ("검토자", UiKit.ReadOnlyText("이서연 · 관리자 (예시)")),
            (_approve ? "승인 의견" : "반려 사유",
                UiKit.Input(_approve
                    ? "[예시] 검토 의견이 반영되었으며 결과 자료를 확인했습니다."
                    : "[예시] 누락된 운영 지표와 예외 상황 설명을 보완해 주세요.", true)));

        var result = UiKit.ReadOnlyText(
            "[고정 예시] 결과 설명\r\n운영 지표와 주간 이슈를 정리했습니다. 1차 검토 의견을 반영해 누락된 항목을 보완했습니다.\r\n\r\n첨부 파일\r\n월간_운영현황_v2.pdf · 184 KB\r\n검토_참고자료.xlsx · 42 KB\r\n\r\n이전 제출\r\nv1 · 2026-10-20 14:00 · 반려\r\n사유: 누락된 운영 지표 보완 요청");

        var content = UiKit.Split(UiKit.Card(_approve ? "승인 내용" : "반려 내용", fields), UiKit.Card("제출 결과 · 예시", result), 57);
        UiKit.Dialog(this,
            _approve ? "결과 승인" : "결과 반려",
            _approve
                ? "관리자 승인 화면 미리보기입니다. 승인 버튼을 눌러도 업무 상태나 이력은 변경되지 않습니다."
                : "관리자 반려 화면 미리보기입니다. 반려 사유 입력 위치를 확인하며 실제 반려나 알림은 처리되지 않습니다.",
            content, UiKit.Button(_approve ? "승인" : "반려", primary: true));
        ClientSize = new Size(960, 710);
        MinimumSize = new Size(900, 650);
    }
}
