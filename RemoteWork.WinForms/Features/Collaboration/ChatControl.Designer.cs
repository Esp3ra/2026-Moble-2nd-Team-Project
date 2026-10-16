#nullable enable
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Collaboration;

partial class ChatControl
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
        Name = "ChatControl";
        var rooms = UiKit.Stack(
            UiKit.Toolbar(UiKit.Combo("전체 대화방", "개인 대화", "그룹 대화"), UiKit.Button("대화방 만들기")),
            UiKit.Card("대화방 · 샘플", UiKit.Grid(
                new[] { "종류", "대화방", "인원" },
                new[] { "그룹", "기획팀 업무 공유", "4명" },
                new[] { "개인", "김민수 · 샘플", "2명" },
                new[] { "그룹", "개발팀 협업", "3명" })),
            UiKit.Card("참여자 · 샘플", UiKit.ReadOnlyText("이서연 · 기획팀\r\n김민수 · 개발팀\r\n박지우 · 디자인팀\r\n최유진 · 관리자")));

        var conversation = UiKit.Stack(
            UiKit.Card("기획팀 업무 공유 · 샘플 대화", UiKit.ReadOnlyText(
                "이서연 · 샘플   2026.10.13 09:10\r\n오늘 일정과 검토할 업무를 공유합니다.\r\n\r\n" +
                "김민수 · 샘플   2026.10.13 09:15\r\n업무 처리 화면의 기본 구성을 검토하겠습니다.\r\n\r\n" +
                "박지우 · 샘플   2026.10.13 09:18\r\n완료 후 협업 요청 화면에서 내용을 확인하겠습니다.\r\n\r\n" +
                "고정 예시입니다. 대화방 선택과 메시지 수신은 연결되어 있지 않습니다.")),
            UiKit.Card("메시지 작성 · 입력 미리보기", UiKit.Stack(
                UiKit.Input("", multiline: true),
                UiKit.Toolbar(UiKit.Button("파일 첨부"), UiKit.Button("보내기", primary: true),
                    UiKit.Button("협업 요청 화면", () => UiKit.OpenPreview(this, new CollaborationRequestForm()))))),
            UiKit.Label("전송 · 첨부 · 대화방 생성은 준비 중이며 샘플 메시지는 변경되지 않습니다."));

        UiKit.Page(this, "채팅 · 협업", "개인·그룹 대화와 협업 요청 화면을 확인하는 UI 미리보기입니다.",
            UiKit.Split(rooms, conversation, 35));
        ResumeLayout(false);
    }
}
