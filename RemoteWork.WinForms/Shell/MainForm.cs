using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Features.Results;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Shell;

public partial class MainForm : Form
{
    private readonly PreviewRole previewRole;
    private readonly List<Button> menuButtons = new();
    private UserControl? currentPage;

    public MainForm() : this(PreviewRole.Employee) { }

    public MainForm(PreviewRole role)
    {
        previewRole = role;
        InitializeComponent();
        AddMenu("01   업무", () => new TasksControl(previewRole));
        AddMenu("02   결과물 · 검토", () => new ReviewsControl(previewRole));
        AddMenu("03   채팅 · 협업", () => new ChatControl());
        AddMenu("04   공지 · 알림", () => new NoticesControl(previewRole));
        AddMenu("05   출퇴근 · 연차", () => new AttendanceControl());
        AddMenu("06   업무 이력", () => new HistoryControl());
        ShowPage(() => new TasksControl(previewRole), menuButtons[0]);
    }

    private void AddMenu(string title, Func<UserControl> pageFactory)
    {
        var button = new Button
        {
            Text = title, Width = 184, Height = 46, FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0),
            BackColor = UiKit.Navy, ForeColor = Color.FromArgb(193, 206, 225),
            Font = new Font("맑은 고딕", 10), Margin = new Padding(0, 0, 0, 8), Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => ShowPage(pageFactory, button);
        menuButtons.Add(button); menuPanel.Controls.Add(button);
    }

    private void ShowPage(Func<UserControl> pageFactory, Button selected)
    {
        contentPanel.Controls.Clear(); currentPage?.Dispose();
        currentPage = pageFactory(); currentPage.Dock = DockStyle.Fill;
        contentPanel.Controls.Add(currentPage);
        foreach (var item in menuButtons)
        {
            item.BackColor = item == selected ? UiKit.Accent : UiKit.Navy;
            item.ForeColor = item == selected ? Color.White : Color.FromArgb(193, 206, 225);
        }
    }
}
