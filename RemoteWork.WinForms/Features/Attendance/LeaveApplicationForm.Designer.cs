#nullable enable
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Attendance;

partial class LeaveApplicationForm
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
        Name = "LeaveApplicationForm";
        var content = UiKit.Stack(
            UiKit.Fields(
                ("신청자", UiKit.ReadOnlyText("이서연 · 기획팀 · 샘플")),
                ("신청 유형", UiKit.Combo("연차")),
                ("시작일", new DateTimePicker { Format = DateTimePickerFormat.Short, Value = new DateTime(2026, 10, 16) }),
                ("종료일", new DateTimePicker { Format = DateTimePickerFormat.Short, Value = new DateTime(2026, 10, 16) }),
                ("신청 사유", UiKit.Input("", multiline: true))),
            UiKit.Label("기간과 사유 입력은 양식 확인용입니다. 연차 신청 처리는 준비 중입니다."));
        UiKit.Dialog(this, "연차 신청", "연차 신청 양식의 외형만 확인합니다.", content,
            UiKit.Button("연차 신청", primary: true));
        ResumeLayout(false);
    }
}
