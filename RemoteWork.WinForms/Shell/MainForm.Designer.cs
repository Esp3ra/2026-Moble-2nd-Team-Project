#nullable disable
using System.Drawing;
using System.Windows.Forms;

namespace RemoteWork.WinForms.Shell
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            layoutWorkspace = new TableLayoutPanel();
            layoutSidebar = new TableLayoutPanel();
            panelNavigation = new FlowLayoutPanel();
            layoutIdentity = new TableLayoutPanel();
            lblSidebarTitle = new Label();
            lblIdentityCaption = new Label();
            lblPreviewIdentity = new Label();
            btnNotices = new Button();
            btnTasks = new Button();
            btnChat = new Button();
            btnReviews = new Button();
            btnHistory = new Button();
            btnAttendance = new Button();
            layoutHeader = new TableLayoutPanel();
            lblWorkspaceTitle = new Label();
            btnBackToLogin = new Button();
            contentPanel = new Panel();
            layoutWorkspace.SuspendLayout();
            layoutSidebar.SuspendLayout();
            panelNavigation.SuspendLayout();
            layoutIdentity.SuspendLayout();
            layoutHeader.SuspendLayout();
            contentPanel.SuspendLayout();
            SuspendLayout();
            // layoutWorkspace
            layoutWorkspace.Name = "layoutWorkspace";
            layoutWorkspace.Dock = DockStyle.Fill;
            layoutWorkspace.Margin = new Padding(0);
            layoutWorkspace.Padding = new Padding(0);
            layoutWorkspace.ColumnCount = 2;
            layoutWorkspace.RowCount = 2;
            layoutWorkspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));
            layoutWorkspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutWorkspace.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
            layoutWorkspace.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            // layoutSidebar
            layoutSidebar.Name = "layoutSidebar";
            layoutSidebar.Dock = DockStyle.Fill;
            layoutSidebar.Margin = new Padding(0);
            layoutSidebar.Padding = new Padding(12, 0, 12, 0);
            layoutSidebar.ColumnCount = 1;
            layoutSidebar.RowCount = 3;
            layoutSidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutSidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            layoutSidebar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutSidebar.RowStyles.Add(new RowStyle(SizeType.Absolute, 128F));
            // layoutSidebar
            layoutSidebar.BackColor = Color.FromArgb(23, 38, 63);
            // lblSidebarTitle
            lblSidebarTitle.Text = "원격 업무 관리";
            lblSidebarTitle.Name = "lblSidebarTitle";
            lblSidebarTitle.Dock = DockStyle.Fill;
            lblSidebarTitle.AutoSize = false;
            lblSidebarTitle.Font = new Font("맑은 고딕", 14F, FontStyle.Bold);
            lblSidebarTitle.ForeColor = Color.FromArgb(255, 255, 255);
            lblSidebarTitle.TextAlign = ContentAlignment.MiddleLeft;
            lblSidebarTitle.Margin = new Padding(0);
            // lblSidebarTitle
            lblSidebarTitle.Padding = new Padding(10, 0, 0, 0);
            // panelNavigation
            panelNavigation.Name = "panelNavigation";
            panelNavigation.Dock = DockStyle.Fill;
            panelNavigation.FlowDirection = FlowDirection.TopDown;
            panelNavigation.WrapContents = false;
            panelNavigation.AutoScroll = false;
            panelNavigation.Margin = new Padding(0);
            panelNavigation.Padding = new Padding(0);
            // btnNotices
            btnNotices.Text = "01   공지";
            btnNotices.Name = "btnNotices";
            btnNotices.Dock = DockStyle.Fill;
            btnNotices.FlatStyle = FlatStyle.Flat;
            btnNotices.Font = new Font("맑은 고딕", 9F);
            btnNotices.Cursor = Cursors.Hand;
            btnNotices.BackColor = Color.White;
            btnNotices.ForeColor = Color.FromArgb(30, 42, 61);
            btnNotices.UseVisualStyleBackColor = false;
            btnNotices.Margin = new Padding(0);
            btnNotices.TabStop = true;
            btnNotices.FlatAppearance.BorderColor = Color.FromArgb(213, 220, 231);
            btnNotices.Click += btnNotices_Click;
            // btnNotices
            btnNotices.Dock = DockStyle.None;
            btnNotices.Size = new Size(184, 46);
            btnNotices.BackColor = Color.FromArgb(23, 38, 63);
            btnNotices.ForeColor = Color.FromArgb(218, 226, 237);
            btnNotices.TextAlign = ContentAlignment.MiddleLeft;
            btnNotices.Padding = new Padding(14, 0, 0, 0);
            btnNotices.Margin = new Padding(0, 0, 0, 8);
            btnNotices.FlatAppearance.BorderSize = 0;
            panelNavigation.Controls.Add(btnNotices);
            // btnTasks
            btnTasks.Text = "02   업무";
            btnTasks.Name = "btnTasks";
            btnTasks.Dock = DockStyle.Fill;
            btnTasks.FlatStyle = FlatStyle.Flat;
            btnTasks.Font = new Font("맑은 고딕", 9F);
            btnTasks.Cursor = Cursors.Hand;
            btnTasks.BackColor = Color.White;
            btnTasks.ForeColor = Color.FromArgb(30, 42, 61);
            btnTasks.UseVisualStyleBackColor = false;
            btnTasks.Margin = new Padding(0);
            btnTasks.TabStop = true;
            btnTasks.FlatAppearance.BorderColor = Color.FromArgb(213, 220, 231);
            btnTasks.Click += btnTasks_Click;
            // btnTasks
            btnTasks.Dock = DockStyle.None;
            btnTasks.Size = new Size(184, 46);
            btnTasks.BackColor = Color.FromArgb(23, 38, 63);
            btnTasks.ForeColor = Color.FromArgb(218, 226, 237);
            btnTasks.TextAlign = ContentAlignment.MiddleLeft;
            btnTasks.Padding = new Padding(14, 0, 0, 0);
            btnTasks.Margin = new Padding(0, 0, 0, 8);
            btnTasks.FlatAppearance.BorderSize = 0;
            panelNavigation.Controls.Add(btnTasks);
            // btnChat
            btnChat.Text = "03   채팅";
            btnChat.Name = "btnChat";
            btnChat.Dock = DockStyle.Fill;
            btnChat.FlatStyle = FlatStyle.Flat;
            btnChat.Font = new Font("맑은 고딕", 9F);
            btnChat.Cursor = Cursors.Hand;
            btnChat.BackColor = Color.White;
            btnChat.ForeColor = Color.FromArgb(30, 42, 61);
            btnChat.UseVisualStyleBackColor = false;
            btnChat.Margin = new Padding(0);
            btnChat.TabStop = true;
            btnChat.FlatAppearance.BorderColor = Color.FromArgb(213, 220, 231);
            btnChat.Click += btnChat_Click;
            // btnChat
            btnChat.Dock = DockStyle.None;
            btnChat.Size = new Size(184, 46);
            btnChat.BackColor = Color.FromArgb(23, 38, 63);
            btnChat.ForeColor = Color.FromArgb(218, 226, 237);
            btnChat.TextAlign = ContentAlignment.MiddleLeft;
            btnChat.Padding = new Padding(14, 0, 0, 0);
            btnChat.Margin = new Padding(0, 0, 0, 8);
            btnChat.FlatAppearance.BorderSize = 0;
            panelNavigation.Controls.Add(btnChat);
            // btnReviews
            btnReviews.Text = "04   결과물";
            btnReviews.Name = "btnReviews";
            btnReviews.Dock = DockStyle.Fill;
            btnReviews.FlatStyle = FlatStyle.Flat;
            btnReviews.Font = new Font("맑은 고딕", 9F);
            btnReviews.Cursor = Cursors.Hand;
            btnReviews.BackColor = Color.White;
            btnReviews.ForeColor = Color.FromArgb(30, 42, 61);
            btnReviews.UseVisualStyleBackColor = false;
            btnReviews.Margin = new Padding(0);
            btnReviews.TabStop = true;
            btnReviews.FlatAppearance.BorderColor = Color.FromArgb(213, 220, 231);
            btnReviews.Click += btnReviews_Click;
            // btnReviews
            btnReviews.Dock = DockStyle.None;
            btnReviews.Size = new Size(184, 46);
            btnReviews.BackColor = Color.FromArgb(23, 38, 63);
            btnReviews.ForeColor = Color.FromArgb(218, 226, 237);
            btnReviews.TextAlign = ContentAlignment.MiddleLeft;
            btnReviews.Padding = new Padding(14, 0, 0, 0);
            btnReviews.Margin = new Padding(0, 0, 0, 8);
            btnReviews.FlatAppearance.BorderSize = 0;
            panelNavigation.Controls.Add(btnReviews);
            // btnHistory
            btnHistory.Text = "05   업무 이력";
            btnHistory.Name = "btnHistory";
            btnHistory.Dock = DockStyle.Fill;
            btnHistory.FlatStyle = FlatStyle.Flat;
            btnHistory.Font = new Font("맑은 고딕", 9F);
            btnHistory.Cursor = Cursors.Hand;
            btnHistory.BackColor = Color.White;
            btnHistory.ForeColor = Color.FromArgb(30, 42, 61);
            btnHistory.UseVisualStyleBackColor = false;
            btnHistory.Margin = new Padding(0);
            btnHistory.TabStop = true;
            btnHistory.FlatAppearance.BorderColor = Color.FromArgb(213, 220, 231);
            btnHistory.Click += btnHistory_Click;
            // btnHistory
            btnHistory.Dock = DockStyle.None;
            btnHistory.Size = new Size(184, 46);
            btnHistory.BackColor = Color.FromArgb(23, 38, 63);
            btnHistory.ForeColor = Color.FromArgb(218, 226, 237);
            btnHistory.TextAlign = ContentAlignment.MiddleLeft;
            btnHistory.Padding = new Padding(14, 0, 0, 0);
            btnHistory.Margin = new Padding(0, 0, 0, 8);
            btnHistory.FlatAppearance.BorderSize = 0;
            panelNavigation.Controls.Add(btnHistory);
            // btnAttendance
            btnAttendance.Text = "06   출퇴근 · 연차";
            btnAttendance.Name = "btnAttendance";
            btnAttendance.Dock = DockStyle.Fill;
            btnAttendance.FlatStyle = FlatStyle.Flat;
            btnAttendance.Font = new Font("맑은 고딕", 9F);
            btnAttendance.Cursor = Cursors.Hand;
            btnAttendance.BackColor = Color.White;
            btnAttendance.ForeColor = Color.FromArgb(30, 42, 61);
            btnAttendance.UseVisualStyleBackColor = false;
            btnAttendance.Margin = new Padding(0);
            btnAttendance.TabStop = true;
            btnAttendance.FlatAppearance.BorderColor = Color.FromArgb(213, 220, 231);
            btnAttendance.Click += btnAttendance_Click;
            // btnAttendance
            btnAttendance.Dock = DockStyle.None;
            btnAttendance.Size = new Size(184, 46);
            btnAttendance.BackColor = Color.FromArgb(23, 38, 63);
            btnAttendance.ForeColor = Color.FromArgb(218, 226, 237);
            btnAttendance.TextAlign = ContentAlignment.MiddleLeft;
            btnAttendance.Padding = new Padding(14, 0, 0, 0);
            btnAttendance.Margin = new Padding(0, 0, 0, 8);
            btnAttendance.FlatAppearance.BorderSize = 0;
            panelNavigation.Controls.Add(btnAttendance);
            // layoutIdentity
            layoutIdentity.Name = "layoutIdentity";
            layoutIdentity.Dock = DockStyle.Fill;
            layoutIdentity.Margin = new Padding(0);
            layoutIdentity.Padding = new Padding(10, 8, 8, 16);
            layoutIdentity.ColumnCount = 1;
            layoutIdentity.RowCount = 2;
            layoutIdentity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutIdentity.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            layoutIdentity.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            // lblIdentityCaption
            lblIdentityCaption.Text = "사원 정보";
            lblIdentityCaption.Name = "lblIdentityCaption";
            lblIdentityCaption.Dock = DockStyle.Fill;
            lblIdentityCaption.AutoSize = false;
            lblIdentityCaption.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            lblIdentityCaption.ForeColor = Color.FromArgb(157, 176, 200);
            lblIdentityCaption.TextAlign = ContentAlignment.MiddleLeft;
            lblIdentityCaption.Margin = new Padding(0);
            // lblPreviewIdentity
            lblPreviewIdentity.Text = "이관리자 | 관리자\n운영관리팀\n사번 ADM-001";
            lblPreviewIdentity.Name = "lblPreviewIdentity";
            lblPreviewIdentity.Dock = DockStyle.Fill;
            lblPreviewIdentity.AutoSize = false;
            lblPreviewIdentity.Font = new Font("맑은 고딕", 9F, FontStyle.Regular);
            lblPreviewIdentity.ForeColor = Color.FromArgb(228, 235, 244);
            lblPreviewIdentity.TextAlign = ContentAlignment.MiddleLeft;
            lblPreviewIdentity.Margin = new Padding(0);
            layoutIdentity.Controls.Add(lblIdentityCaption, 0, 0);
            layoutIdentity.Controls.Add(lblPreviewIdentity, 0, 1);
            layoutSidebar.Controls.Add(lblSidebarTitle, 0, 0);
            layoutSidebar.Controls.Add(panelNavigation, 0, 1);
            layoutSidebar.Controls.Add(layoutIdentity, 0, 2);
            // layoutHeader
            layoutHeader.Name = "layoutHeader";
            layoutHeader.Dock = DockStyle.Fill;
            layoutHeader.Margin = new Padding(0);
            layoutHeader.Padding = new Padding(28, 16, 28, 16);
            layoutHeader.ColumnCount = 2;
            layoutHeader.RowCount = 1;
            layoutHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
            layoutHeader.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            // layoutHeader
            layoutHeader.BackColor = Color.White;
            // lblWorkspaceTitle
            lblWorkspaceTitle.Text = "원격 근무 업무 관리";
            lblWorkspaceTitle.Name = "lblWorkspaceTitle";
            lblWorkspaceTitle.Dock = DockStyle.Fill;
            lblWorkspaceTitle.AutoSize = false;
            lblWorkspaceTitle.Font = new Font("맑은 고딕", 17F, FontStyle.Bold);
            lblWorkspaceTitle.ForeColor = Color.FromArgb(30, 42, 61);
            lblWorkspaceTitle.TextAlign = ContentAlignment.MiddleLeft;
            lblWorkspaceTitle.Margin = new Padding(0);
            // btnBackToLogin
            btnBackToLogin.Text = "시작 화면";
            btnBackToLogin.Name = "btnBackToLogin";
            btnBackToLogin.Dock = DockStyle.Fill;
            btnBackToLogin.FlatStyle = FlatStyle.Flat;
            btnBackToLogin.Font = new Font("맑은 고딕", 9F);
            btnBackToLogin.Cursor = Cursors.Hand;
            btnBackToLogin.BackColor = Color.White;
            btnBackToLogin.ForeColor = Color.FromArgb(30, 42, 61);
            btnBackToLogin.UseVisualStyleBackColor = false;
            btnBackToLogin.Margin = new Padding(0);
            btnBackToLogin.TabStop = true;
            btnBackToLogin.FlatAppearance.BorderColor = Color.FromArgb(213, 220, 231);
            btnBackToLogin.Click += ClosePreview_Click;
            layoutHeader.Controls.Add(lblWorkspaceTitle, 0, 0);
            layoutHeader.Controls.Add(btnBackToLogin, 1, 0);
            // contentPanel
            contentPanel.Name = "contentPanel";
            contentPanel.Dock = DockStyle.Fill;
            contentPanel.BackColor = Color.FromArgb(242, 245, 250);
            contentPanel.Margin = new Padding(0);
            layoutWorkspace.Controls.Add(layoutSidebar, 0, 0);
            layoutWorkspace.SetRowSpan(layoutSidebar, 2);
            layoutWorkspace.Controls.Add(layoutHeader, 1, 0);
            layoutWorkspace.Controls.Add(contentPanel, 1, 1);
            Controls.Add(layoutWorkspace);
            // MainForm
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("맑은 고딕", 9F);
            ClientSize = new Size(1320, 820);
            MinimumSize = new Size(1120, 740);
            BackColor = Color.FromArgb(242, 245, 250);
            Name = "MainForm";
            Text = "원격 근무용 업무 처리 확인 시스템";
            StartPosition = FormStartPosition.CenterScreen;
            contentPanel.ResumeLayout(false);
            contentPanel.PerformLayout();
            layoutHeader.ResumeLayout(false);
            layoutHeader.PerformLayout();
            layoutIdentity.ResumeLayout(false);
            layoutIdentity.PerformLayout();
            panelNavigation.ResumeLayout(false);
            panelNavigation.PerformLayout();
            layoutSidebar.ResumeLayout(false);
            layoutSidebar.PerformLayout();
            layoutWorkspace.ResumeLayout(false);
            layoutWorkspace.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TableLayoutPanel layoutWorkspace;
        private TableLayoutPanel layoutSidebar;
        private FlowLayoutPanel panelNavigation;
        private TableLayoutPanel layoutIdentity;
        private Label lblSidebarTitle;
        private Label lblIdentityCaption;
        private Label lblPreviewIdentity;
        private Button btnNotices;
        private Button btnTasks;
        private Button btnChat;
        private Button btnReviews;
        private Button btnHistory;
        private Button btnAttendance;
        private TableLayoutPanel layoutHeader;
        private Label lblWorkspaceTitle;
        private Button btnBackToLogin;
        private Panel contentPanel;
    }
}
