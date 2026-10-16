#nullable enable
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Collaboration;

partial class CollaborationRequestForm
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
        Name = "CollaborationRequestForm";
        var content = UiKit.Stack(
            UiKit.Fields(
                ("요청할 업무", UiKit.Combo("화면 구성 검토 · 샘플", "주간 보고서 작성 · 샘플")),
                ("요청 대상", UiKit.Combo("김민수 · 개발팀 · 샘플", "박지우 · 디자인팀 · 샘플", "기획팀 · 샘플")),
                ("요청 제목", UiKit.Input("")),
                ("요청 내용", UiKit.Input("", multiline: true)),
                ("희망 기한", new DateTimePicker { Format = DateTimePickerFormat.Short })),
            UiKit.Label("입력값은 저장·전송되지 않습니다. 대상과 업무 목록은 고정 샘플입니다."));
        UiKit.Dialog(this, "협업 요청", "업무 협업 요청 양식의 외형만 확인합니다.", content,
            UiKit.Button("요청 보내기", primary: true));
        ResumeLayout(false);
    }
}
