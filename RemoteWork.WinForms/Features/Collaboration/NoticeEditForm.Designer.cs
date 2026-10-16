#nullable enable
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Collaboration;

partial class NoticeEditForm
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
        Name = "NoticeEditForm";
        var content = UiKit.Stack(
            UiKit.Fields(
                ("공지 제목", UiKit.Input("")),
                ("공지 대상", UiKit.Combo("전 직원 · 샘플", "기획팀 · 샘플", "개발팀 · 샘플", "디자인팀 · 샘플")),
                ("중요도", UiKit.Combo("일반 공지", "중요 공지")),
                ("공지 내용", UiKit.Input("", multiline: true))),
            UiKit.Toolbar(UiKit.Button("첨부파일 선택")),
            UiKit.Label("작성 양식 미리보기입니다. 등록·첨부·대상 지정 결과는 저장되지 않습니다."));
        UiKit.Dialog(this, "공지 작성", "관리자 공지 작성 화면의 외형만 확인합니다.", content,
            UiKit.Button("공지 등록", primary: true));
        ResumeLayout(false);
    }
}
