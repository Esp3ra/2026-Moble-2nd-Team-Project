#nullable enable
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Shell;

partial class MainForm
{
    private System.ComponentModel.IContainer? components;
    private Panel contentPanel = null!;
    private FlowLayoutPanel menuPanel = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        SuspendLayout();
        Text = "원격 근무용 업무 처리 확인 시스템 · 화면 미리보기";
        ClientSize = new Size(1320, 820); MinimumSize = new Size(1140, 740);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font; Font = new Font("맑은 고딕", 9);
        BackColor = UiKit.Background;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Color.White,
            ColumnCount = 3, RowCount = 1, Padding = new Padding(26, 17, 18, 10), Margin = Padding.Empty };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 132));
        header.Controls.Add(UiKit.Label("원격 업무 워크스페이스", 17, true), 0, 0);
        var identity = UiKit.Label(previewRole == PreviewRole.Administrator
            ? "샘플 관리자  /  관리자 화면\n서버 미연결 · 인증 없음" : "샘플 직원  /  직원 화면\n서버 미연결 · 인증 없음", 9);
        identity.ForeColor = UiKit.Muted; header.Controls.Add(identity, 1, 0);
        header.Controls.Add(UiKit.Button("시작 화면", Close), 2, 0);
        root.Controls.Add(header, 0, 0);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210)); body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var sidebar = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = UiKit.Navy,
            ColumnCount = 1, RowCount = 3, Padding = new Padding(14, 26, 10, 16), Margin = Padding.Empty };
        sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); sidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        var caption = UiKit.Label("WORKSPACE", 9, true); caption.ForeColor = Color.FromArgb(126, 152, 193);
        caption.Padding = new Padding(12, 0, 0, 0); sidebar.Controls.Add(caption, 0, 0);
        menuPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, Margin = Padding.Empty };
        sidebar.Controls.Add(menuPanel, 0, 1);
        var note = UiKit.Label("화면 기본 틀\n\n샘플 데이터만 표시\n업무 처리 기능 준비 중", 9); note.ForeColor = Color.FromArgb(126, 152, 193);
        note.Padding = new Padding(12, 0, 0, 0); sidebar.Controls.Add(note, 0, 2);
        contentPanel = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty, BackColor = UiKit.Background };
        body.Controls.Add(sidebar, 0, 0); body.Controls.Add(contentPanel, 1, 0); root.Controls.Add(body, 0, 1);
        var status = UiKit.Label("   기본 틀 미리보기   |   메뉴 이동 · 입력 창 열기/닫기만 연결   |   저장 · 통신 · 장치 동작 없음", 9);
        status.AutoSize = false; status.Dock = DockStyle.Fill; status.BackColor = Color.White;
        status.TextAlign = ContentAlignment.MiddleLeft; status.ForeColor = UiKit.Muted; status.Margin = Padding.Empty;
        root.Controls.Add(status, 0, 2); Controls.Add(root);
        ResumeLayout(true);
    }
}
