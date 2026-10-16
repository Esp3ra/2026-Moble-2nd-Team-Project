using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Results;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Features.Attendance;

namespace RemoteWork.WinForms.Shell
{
    public partial class MainForm : Form
    {
        private readonly PreviewRole previewRole;
        // Keep the local chat store when navigating between pages.
        private readonly IChatService? chatService;
        private UserControl? currentPage;

        public MainForm() : this(PreviewRole.Employee) { }

        public MainForm(PreviewRole role)
        {
            InitializeComponent();
            previewRole = role;
            if (PreviewActions.IsDesignMode(this)) return;
            chatService = new InMemoryChatService(role == PreviewRole.Administrator
                ? new ChatParticipant("ADM-001", "이관리자", "운영관리팀")
                : new ChatParticipant("EMP-001", "김직원", "개발팀"));
            InitializeSampleData(role);
            ShowPage(new NoticesControl(previewRole), btnNotices);
        }

        private void InitializeSampleData(PreviewRole role)
        {
            bool administrator = role == PreviewRole.Administrator;
            lblPreviewIdentity.Text = administrator
                ? "이관리자 | 관리자\n운영관리팀\n사번 ADM-001"
                : "김직원 | 사원\n개발팀\n사번 EMP-001";
            btnHistory.Visible = administrator;
            btnReviews.Text = "04   결과물";
            btnAttendance.Text = administrator ? "06   출퇴근 · 연차" : "05   출퇴근 · 연차";
        }

        private void ClosePreview_Click(object? sender, EventArgs e)
        {
            Close();
        }

        private void btnTasks_Click(object? sender, EventArgs e)
        {
            ShowPage(new TasksControl(previewRole), btnTasks);
        }

        private void btnReviews_Click(object? sender, EventArgs e)
        {
            ShowPage(new ReviewsControl(previewRole), btnReviews);
        }

        private void btnChat_Click(object? sender, EventArgs e)
        {
            if (chatService is not null) ShowPage(new ChatControl(chatService), btnChat);
        }

        private void btnNotices_Click(object? sender, EventArgs e)
        {
            ShowPage(new NoticesControl(previewRole), btnNotices);
        }

        private void btnAttendance_Click(object? sender, EventArgs e)
        {
            ShowPage(new AttendanceControl(previewRole), btnAttendance);
        }

        private void btnHistory_Click(object? sender, EventArgs e)
        {
            ShowPage(new HistoryControl(), btnHistory);
        }

        private void ShowPage(UserControl page, Button selected)
        {
            contentPanel.Controls.Clear();
            currentPage?.Dispose();
            currentPage = page;
            currentPage.Dock = DockStyle.Fill;
            contentPanel.Controls.Add(currentPage);
            Button[] menus = { btnTasks, btnReviews, btnChat, btnNotices, btnAttendance, btnHistory };
            foreach (var button in menus)
            {
                button.BackColor = button == selected ? Color.FromArgb(52, 101, 224) : Color.FromArgb(23, 38, 63);
                button.ForeColor = button == selected ? Color.White : Color.FromArgb(193, 206, 225);
            }
        }

    }
}
