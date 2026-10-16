using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Results;

public partial class SubmissionForm : Form
{
    public SubmissionForm()
    {
        InitializeComponent();
        BuildPreview();
    }

    private void BuildPreview()
    {
        var description = UiKit.Input("[예시] 검토 의견을 반영하고 제출 기준과 예외 상황 설명을 보완했습니다.", true);
        var fields = UiKit.Fields(
            ("업무", UiKit.ReadOnlyText("WRK-2026-003 · 원격 근무 가이드 초안")),
            ("담당자 / 제출 버전", UiKit.ReadOnlyText("박지은 / v2 제출 예정 (예시)")),
            ("결과 설명", description));

        var attachments = UiKit.Grid(
            ["첨부 예정 파일 (예시)", "크기", "구분"],
            ["원격근무_가이드_v2.pdf", "210 KB", "결과 문서"],
            ["예외상황_정리.xlsx", "38 KB", "참고 자료"]);

        var priorAttachments = UiKit.Grid(
            ["이전 첨부 파일 (예시)", "버전", "제출일"],
            ["원격근무_가이드_v1.pdf", "v1", "2026-10-20 14:00"]);

        var versionHistory = UiKit.ReadOnlyText(
            "[예시] 이전 제출 v1 · 2026-10-20 14:00 · 반려\r\n반려 사유: 제출 기준과 예외 상황 설명을 보완해 주세요.\r\n재제출 시 이전 결과와 첨부 자료를 확인하는 영역입니다.");

        var attachmentLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        attachmentLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        attachmentLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        attachmentLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        attachmentLayout.Controls.Add(attachments, 0, 0);
        attachmentLayout.Controls.Add(UiKit.Toolbar(UiKit.Button("파일 첨부"), UiKit.Button("선택 첨부 제거")), 0, 1);

        var previousLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        previousLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        previousLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
        previousLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        previousLayout.Controls.Add(versionHistory, 0, 0);
        previousLayout.Controls.Add(priorAttachments, 0, 1);

        var references = new TabControl { Dock = DockStyle.Fill, Font = new Font("맑은 고딕", 9) };
        var plannedTab = new TabPage("첨부 예정") { BackColor = Color.White, Padding = new Padding(8) };
        var previousTab = new TabPage("이전 제출물") { BackColor = Color.White, Padding = new Padding(8) };
        plannedTab.Controls.Add(attachmentLayout);
        previousTab.Controls.Add(previousLayout);
        references.TabPages.Add(plannedTab);
        references.TabPages.Add(previousTab);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var submissionCard = UiKit.Card("제출 내용", fields);
        content.Controls.Add(submissionCard, 0, 0);
        content.Controls.Add(UiKit.Card("첨부 자료와 이전 제출물 · 예시", references), 1, 0);

        UiKit.Dialog(this, "업무 결과 제출", "화면 골격 미리보기 · 입력과 첨부 항목의 위치만 확인할 수 있습니다. 제출·첨부·제거 기능은 준비 중입니다.", content,
            UiKit.Button("결과 제출", primary: true));
        ClientSize = new Size(1000, 680);
        MinimumSize = new Size(950, 650);
    }
}
