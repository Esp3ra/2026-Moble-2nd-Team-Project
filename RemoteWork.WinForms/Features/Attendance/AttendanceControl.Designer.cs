#nullable enable
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Attendance;

partial class AttendanceControl
{
    private System.ComponentModel.IContainer? components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        SuspendLayout();
        Name = "AttendanceControl";
        var history = UiKit.Stack(
            UiKit.Toolbar(UiKit.Combo("2026년 10월 · 샘플", "2026년 9월 · 샘플"), UiKit.Button("조회"),
                UiKit.Button("연차 신청 화면", () => UiKit.OpenPreview(this, new LeaveApplicationForm()), primary: true)),
            UiKit.Card("출퇴근 기록 · 샘플", UiKit.Grid(
                new[] { "일자", "출근", "퇴근", "상태", "근무 형태" },
                new[] { "2026.10.13", "09:03", "―", "근무 중 · 샘플", "원격" },
                new[] { "2026.10.12", "08:57", "18:02", "정상 · 샘플", "원격" },
                new[] { "2026.10.08", "―", "―", "연차 · 샘플", "―" })),
            UiKit.Card("연차 신청 내역 · 샘플", UiKit.Grid(
                new[] { "유형", "기간", "사유", "상태" },
                new[] { "연차", "2026.10.08", "개인 일정 · 샘플", "접수 예시" },
                new[] { "연차", "2026.10.16", "개인 일정 · 샘플", "신청 예시" })),
            UiKit.Label("출퇴근·연차 내역은 고정 예시입니다. 실제 출근·퇴근 처리와 기록 조회는 연결되지 않았습니다."));

        var devices = UiKit.Stack(
            UiKit.Card("출퇴근 기기 · 외형 미리보기", UiKit.Stack(
                UiKit.Label("UNCONNECTED", size: 15, bold: true),
                UiKit.ReadOnlyText("기기 상태: 연결되지 않음\r\n직원: 이서연 · 샘플\r\n기기 ID: DEVICE-SAMPLE\r\n통신·센서·인증 데이터 없음"))),
            UiKit.Card("지문 · 얼굴 인증 영역", UiKit.ReadOnlyText(
                "[ 지문 센서 미리보기 ]\r\n지문 인식: 미연결\r\n\r\n" +
                "[ 얼굴 인식 미리보기 ]\r\n카메라 화면: 미연결\r\n\r\n" +
                "센서와 카메라는 실행되지 않습니다.")),
            UiKit.Card("OLED 표시 영역 · 샘플", UiKit.ReadOnlyText(
                "REMOTE WORK\r\nDEVICE: UNCONNECTED\r\nDISPLAY PREVIEW\r\n\r\n고정 문구로 표시 배치만 확인합니다.")));

        UiKit.Page(this, "출퇴근 · 연차", "출퇴근 조회와 연차 신청 양식, 별도 출퇴근 기기 화면의 외형을 확인합니다.",
            UiKit.Split(history, devices, 66));
        ResumeLayout(false);
    }
}
