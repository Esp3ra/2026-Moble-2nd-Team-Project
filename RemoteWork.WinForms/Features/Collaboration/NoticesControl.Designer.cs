#nullable enable
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Collaboration;

partial class NoticesControl
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
        Name = "NoticesControl";
        var filters = new List<Control>
        {
            UiKit.Combo("전체 공지", "중요 공지", "일반 공지"),
            UiKit.Combo("전체 대상", "전 직원", "기획팀", "개발팀"),
            UiKit.Button("조회")
        };
        if (_previewRole == PreviewRole.Administrator)
            filters.Add(UiKit.Button("공지 작성 화면", () => UiKit.OpenPreview(this, new NoticeEditForm()), primary: true));

        var notices = UiKit.Stack(
            UiKit.Toolbar(filters.ToArray()),
            UiKit.Card("공지 목록 · 샘플", UiKit.Grid(
                new[] { "구분", "제목", "대상", "등록일" },
                new[] { "중요", "보안 점검 안내 · 샘플", "전 직원", "2026.10.13" },
                new[] { "일반", "주간 업무 공유 · 샘플", "기획팀", "2026.10.12" },
                new[] { "일반", "개발 일정 안내 · 샘플", "개발팀", "2026.10.11" })),
            UiKit.Label("목록과 상세는 고정 샘플이며 선택·조회 동작은 연결되지 않았습니다."));

        var details = UiKit.Stack(
            UiKit.Card("보안 점검 안내 · 샘플", UiKit.ReadOnlyText(
                "중요 공지    대상: 전 직원\r\n작성자: 관리자 · 샘플    등록일: 2026.10.13\r\n\r\n" +
                "원격 근무 환경의 보안 점검이 예정되어 있습니다.\r\n담당 업무와 일정을 확인해 주세요.\r\n\r\n" +
                "이 공지 내용은 화면 배치 확인을 위한 예시입니다.")),
            UiKit.Toolbar(UiKit.Button("중요 공지 팝업 미리보기", () => UiKit.OpenPreview(this, new ImportantNoticeForm())),
                UiKit.Button("공지 확인")),
            UiKit.Card("업무 마감 알림 · 정적 샘플", UiKit.ReadOnlyText(
                "업무: 주간 보고서 작성 · 샘플\r\n마감: 2026.10.14 18:00 · 샘플\r\n상태: 마감 예정 · 샘플\r\n\r\n" +
                "시간 감지·자동 알림·확인 기록은 연결되지 않았습니다.")),
            UiKit.Label(_previewRole == PreviewRole.Administrator
                ? "관리자 화면 미리보기 · 공지 작성 양식을 열 수 있습니다."
                : "직원 화면 미리보기 · 공지 열람 화면의 배치를 확인합니다."));

        UiKit.Page(this, "공지사항", "대상별 공지 목록, 중요 공지와 마감 알림의 배치를 확인합니다.",
            UiKit.Split(notices, details, 56));
        ResumeLayout(false);
    }
}
