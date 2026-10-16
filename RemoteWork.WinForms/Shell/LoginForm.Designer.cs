#nullable enable
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Shell;

partial class LoginForm
{
    private System.ComponentModel.IContainer? components;

    protected override void Dispose(bool disposing)
    {
        if (disposing) components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        SuspendLayout();
        Text = "원격 근무용 업무 처리 확인 시스템 · 기본 틀";
        ClientSize = new Size(990, 650);
        MinimumSize = new Size(900, 640);
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("맑은 고딕", 9);
        BackColor = UiKit.Background;
        Padding = new Padding(36);

        var intro = new Panel { Dock = DockStyle.Fill, BackColor = UiKit.Navy, Padding = new Padding(30) };
        var introLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1 };
        introLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        introLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        introLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
        introLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        introLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        introLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        var eyebrow = UiKit.Label("REMOTE WORK  /  TEAM PROJECT", 9, true); eyebrow.ForeColor = Color.FromArgb(137, 164, 211);
        var title = UiKit.Label("원격 근무용\n업무 처리 확인 시스템", 22, true); title.ForeColor = Color.White;
        var description = UiKit.Label("업무부터 검토와 협업까지\n팀이 함께 사용할 화면을 구성합니다.", 11); description.ForeColor = Color.FromArgb(192, 206, 229);
        var modules = UiKit.Label("01   업무 · 결과물 · 이력\n02   채팅 · 공지 · 알림\n03   출퇴근 · 연차", 11); modules.ForeColor = Color.FromArgb(192, 206, 229);
        var edition = UiKit.Label("C# WinForms   ·   화면 기본 틀", 9); edition.ForeColor = Color.FromArgb(137, 164, 211);
        introLayout.Controls.Add(eyebrow, 0, 0); introLayout.Controls.Add(title, 0, 1);
        introLayout.Controls.Add(description, 0, 2); introLayout.Controls.Add(modules, 0, 3); introLayout.Controls.Add(edition, 0, 4);
        intro.Controls.Add(introLayout);

        var login = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 6, ColumnCount = 1, Padding = new Padding(28, 38, 0, 12) };
        login.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        login.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        login.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
        login.RowStyles.Add(new RowStyle(SizeType.Absolute, 114));
        login.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        login.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
        login.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        login.Controls.Add(UiKit.Label("로그인 화면", 21, true), 0, 0);
        var hint = UiKit.Label("현재는 화면 미리보기 단계입니다.\n아래 입력 내용은 인증에 사용되지 않습니다.", 10); hint.ForeColor = UiKit.Muted;
        login.Controls.Add(hint, 0, 1);
        var password = UiKit.Input(); password.UseSystemPasswordChar = true;
        login.Controls.Add(UiKit.Fields(("사번 / 아이디", UiKit.Input()), ("비밀번호", password)), 0, 2);
        login.Controls.Add(UiKit.Button("로그인 (준비 중)"), 0, 3);
        login.Controls.Add(UiKit.Stack(
            UiKit.Button("직원 화면 보기", () => ShowWorkspace(PreviewRole.Employee), true),
            UiKit.Button("관리자 화면 보기", () => ShowWorkspace(PreviewRole.Administrator))), 0, 4);
        var warning = UiKit.Label("샘플 역할 선택은 실제 로그인이 아닙니다.\n서버 · DB · 출퇴근 장치가 연결되어 있지 않습니다.", 9); warning.ForeColor = UiKit.Muted;
        login.Controls.Add(warning, 0, 5);
        Controls.Add(UiKit.Split(intro, login, 47));
        ResumeLayout(true);
    }
}
