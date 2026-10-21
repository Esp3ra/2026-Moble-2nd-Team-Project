using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Features.Results;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Shell
{
    public partial class MainForm : Form
    {
        private readonly PreviewRole previewRole;
        // Keep the current user chat service when navigating between pages.
        private readonly IChatService? chatService;
        private UserControl? currentPage;
        private ImportantNoticeCoordinator? noticeCoordinator;
        private LoginCollaborationNotifications? collaborationNotifications;
        private readonly System.Windows.Forms.Timer identityTimer = new() { Interval = 30000 };
        private readonly UserInfo? signedInUser;
        private bool allowClose;
        private bool closingSession;
        private bool refreshing;
        private bool expired;

        public MainForm() : this(PreviewRole.Employee) { }

        public MainForm(UserInfo user) : this(user.Role == "admin" ? PreviewRole.Administrator : PreviewRole.Employee)
        {
            signedInUser = user;
            chatService = new HttpChatService(AppServices.Api, user);
            lblPreviewIdentity.Text = $"{user.DisplayName} | {(user.Role == "admin" ? "관리자" : "직원")}\n{user.Department}\n사번 {user.EmployeeNo}";
            btnBackToLogin.Text = "로그아웃";
            AppServices.Api.AuthenticationExpired += AuthenticationExpired;
            FormClosing += SignOutOnClosing;
            components!.Add(new LifecycleCleanup(() => AppServices.Api.AuthenticationExpired -= AuthenticationExpired));
            identityTimer.Tick += RefreshIdentity;
            identityTimer.Start();
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(8), WrapContents = true, Margin = new Padding(0), BackColor = Color.White };
            var password = new Button { Text = "비밀번호 변경", AutoSize = true };
            password.Click += (_, _) => { using var form = new PasswordChangeForm(); if (form.ShowDialog(this) == DialogResult.OK) { allowClose = true; Close(); } };
            footer.Controls.Add(password);
            if (user.Role == "admin")
            {
                var users = new Button { Text = "사용자 관리", AutoSize = true };
                users.Click += (_, _) => { using var form = new UserManagementForm(); form.ShowDialog(this); };
                footer.Controls.Add(users);
            }
            var note = new Label { AutoSize = true, MaximumSize = new Size(460, 0), Text = "로그인 연결됨 · 업무 조회 연결 상태는 업무 화면에서 확인하세요.", Padding = new Padding(5, 7, 0, 0) };
            footer.Controls.Add(note);
            var collaborationStatus = new Label { AutoSize = true, Text = "협업 알림 미연결", Padding = new Padding(5, 7, 0, 0) };
            footer.Controls.Add(collaborationStatus);
            var collaborationInterval = LoginCollaborationNotifications.ParseInterval(
                Environment.GetEnvironmentVariable("REMOTEWORK_COLLABORATION_POLL_SECONDS"));
            if (collaborationInterval is TimeSpan interval)
            {
                collaborationNotifications = new(this,
                    new HttpCollaborationService(AppServices.Api.CaptureSession()), interval);
                components!.Add(collaborationNotifications);
                collaborationStatus.Text = "협업 알림 준비 중";
                collaborationNotifications.Scope.StatusChanged += status =>
                {
                    if (!IsDisposed && !closingSession && !expired)
                        collaborationStatus.Text = string.IsNullOrEmpty(status) ? "협업 알림 연결됨" : status;
                };
            }
            layoutWorkspace.RowCount = 3;
            layoutWorkspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            layoutWorkspace.SetRowSpan(layoutSidebar, 3);
            layoutWorkspace.Controls.Add(footer, 1, 2);
            if (user.Role == "employee")
            {
                noticeCoordinator = new ImportantNoticeCoordinator(this, AppServices.Api,
                    new HttpNoticeService(AppServices.Api), user.Id);
                components!.Add(noticeCoordinator);
                var popupTip = new ToolTip(components!);
                note.Text = "로그인 연결됨 · 중요 공지 수신 준비 중";
                noticeCoordinator.StatusChanged += status =>
                {
                    if (IsDisposed || closingSession || expired) return;
                    note.Text = "로그인 연결됨 · " + status;
                    popupTip.SetToolTip(note, noticeCoordinator.LastError ?? "전 직원·본인 부서의 새 중요 공지를 표시합니다.");
                };
            }
        }

        public MainForm(PreviewRole role)
        {
            InitializeComponent();
            components ??= new System.ComponentModel.Container();
            components.Add(identityTimer);
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
            ShowPage(signedInUser is null
                ? new TasksControl(previewRole)
                : new TasksControl(previewRole, new HttpTaskQueryService(AppServices.Api)), btnTasks);
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
            if (signedInUser is not null && signedInUser.Role != "admin") return;
            ShowPage(new HistoryControl(), btnHistory);
        }

        private async void RefreshIdentity(object? sender, EventArgs e)
        {
            if (refreshing || closingSession || IsDisposed) return;
            refreshing = true;
            try
            {
                var identity = await AppServices.Api.RefreshIdentityAsync();
                if (IsDisposed) return;
                var user = identity.User;
                lblIdentityCaption.Text = "사원 정보 · 서버 연결됨";
                lblPreviewIdentity.Text = $"{user.DisplayName} | {(user.Role == "admin" ? "관리자" : "직원")}\n{user.Department}\n사번 {user.EmployeeNo}";
            }
            catch (OperationCanceledException) { /* Logout/relogin superseded this refresh. */ }
            catch (ApiException ex) { if (!IsDisposed && ex.StatusCode != 401) lblIdentityCaption.Text = "서버 연결 끊김 · 재확인 중"; }
            finally { refreshing = false; }
        }

        private void AuthenticationExpired(object? sender, EventArgs e)
        {
            if (IsDisposed || expired || closingSession) return;
            expired = true;
            collaborationNotifications?.Dispose();
            noticeCoordinator?.Dispose();
            identityTimer.Stop();
            BeginInvoke(() =>
            {
                if (IsDisposed) return;
                foreach (Form owned in OwnedForms) owned.Close();
                MessageBox.Show(this, "로그인이 만료되었거나 계정 정보가 변경되었습니다. 다시 로그인해 주세요.", "로그인 필요");
                allowClose = true;
                Close();
            });
        }

        private async void SignOutOnClosing(object? sender, FormClosingEventArgs e)
        {
            if (allowClose) return;
            e.Cancel = true;
            if (closingSession) return;
            closingSession = true;
            collaborationNotifications?.Dispose();
            noticeCoordinator?.Dispose();
            identityTimer.Stop();
            try { await AppServices.Api.LogoutAsync(); }
            catch (ApiException ex)
            {
                if (ex.StatusCode != 401) MessageBox.Show(this,
                    "이 PC에서는 로그아웃했습니다. 서버에 연결할 수 없어 서버의 접속 종료는 확인하지 못했습니다. 접속은 만료 시 자동 종료됩니다.", "연결 확인");
            }
            finally { allowClose = true; if (!IsDisposed) Close(); }
        }

        private sealed class LifecycleCleanup(Action cleanup) : System.ComponentModel.Component
        {
            protected override void Dispose(bool disposing)
            {
                if (disposing) cleanup();
                base.Dispose(disposing);
            }
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
