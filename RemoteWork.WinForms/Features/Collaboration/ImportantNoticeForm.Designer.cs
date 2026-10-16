#nullable enable
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Collaboration;

partial class ImportantNoticeForm
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
        Name = "ImportantNoticeForm";
        var content = UiKit.Stack(
            UiKit.Card("중요 · 보안 점검 안내 · 샘플", UiKit.ReadOnlyText(
                "대상: 전 직원 · 샘플\r\n등록일: 2026.10.13 · 샘플\r\n\r\n" +
                "원격 근무 환경의 보안 점검이 예정되어 있습니다.\r\n업무 일정과 공지 내용을 확인해 주세요.\r\n\r\n" +
                "이 창은 팝업 외형 미리보기입니다. 자동으로 열리지 않습니다.")),
            UiKit.Label("확인 버튼은 준비 중입니다. 읽음 상태나 확인 기록이 저장되지 않습니다."));
        UiKit.Dialog(this, "중요 공지", "중요 공지 팝업 미리보기 · 고정 샘플", content,
            UiKit.Button("공지 확인", primary: true));
        ResumeLayout(false);
    }
}
