#nullable disable
using System.Drawing;
using System.Windows.Forms;

namespace RemoteWork.WinForms.Shell
{
    partial class LoginForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing) EndLoginView();
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            layoutLogin = new TableLayoutPanel();
            lblCompanyName = new Label();
            lblSystemName = new Label();
            lblLoginTitle = new Label();
            lblLoginId = new Label();
            txtLoginId = new TextBox();
            lblPassword = new Label();
            txtPassword = new TextBox();
            btnLogin = new Button();
            layoutRoleNavigation = new TableLayoutPanel();
            btnEmployeePreview = new Button();
            btnAdministratorPreview = new Button();
            layoutLogin.SuspendLayout();
            layoutRoleNavigation.SuspendLayout();
            SuspendLayout();
            // 
            // layoutLogin
            // 
            layoutLogin.ColumnCount = 1;
            layoutLogin.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutLogin.Controls.Add(lblCompanyName, 0, 0);
            layoutLogin.Controls.Add(lblSystemName, 0, 1);
            layoutLogin.Controls.Add(lblLoginTitle, 0, 3);
            layoutLogin.Controls.Add(lblLoginId, 0, 4);
            layoutLogin.Controls.Add(txtLoginId, 0, 5);
            layoutLogin.Controls.Add(lblPassword, 0, 6);
            layoutLogin.Controls.Add(txtPassword, 0, 7);
            layoutLogin.Controls.Add(btnLogin, 0, 9);
            layoutLogin.Controls.Add(layoutRoleNavigation, 0, 11);
            layoutLogin.Dock = DockStyle.Fill;
            layoutLogin.Location = new Point(0, 0);
            layoutLogin.Margin = new Padding(0);
            layoutLogin.Name = "layoutLogin";
            layoutLogin.Padding = new Padding(42, 32, 42, 30);
            layoutLogin.RowCount = 12;
            layoutLogin.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            layoutLogin.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            layoutLogin.RowStyles.Add(new RowStyle(SizeType.Absolute, 18F));
            layoutLogin.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layoutLogin.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            layoutLogin.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            layoutLogin.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            layoutLogin.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            layoutLogin.RowStyles.Add(new RowStyle(SizeType.Absolute, 20F));
            layoutLogin.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layoutLogin.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutLogin.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layoutLogin.Size = new Size(560, 650);
            layoutLogin.TabIndex = 0;
            // 
            // lblCompanyName
            // 
            lblCompanyName.Dock = DockStyle.Fill;
            lblCompanyName.Font = new Font("맑은 고딕", 20F, FontStyle.Bold);
            lblCompanyName.ForeColor = Color.FromArgb(30, 42, 61);
            lblCompanyName.Location = new Point(42, 32);
            lblCompanyName.Margin = new Padding(0);
            lblCompanyName.Name = "lblCompanyName";
            lblCompanyName.Size = new Size(476, 42);
            lblCompanyName.TabIndex = 0;
            lblCompanyName.Text = "회사명";
            lblCompanyName.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblSystemName
            // 
            lblSystemName.Dock = DockStyle.Fill;
            lblSystemName.Font = new Font("맑은 고딕", 10F);
            lblSystemName.ForeColor = Color.FromArgb(103, 116, 135);
            lblSystemName.Location = new Point(42, 74);
            lblSystemName.Margin = new Padding(0);
            lblSystemName.Name = "lblSystemName";
            lblSystemName.Size = new Size(476, 30);
            lblSystemName.TabIndex = 1;
            lblSystemName.Text = "원격 근무용 업무 처리 확인 시스템";
            lblSystemName.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // lblLoginTitle
            // 
            lblLoginTitle.Dock = DockStyle.Fill;
            lblLoginTitle.Font = new Font("맑은 고딕", 18F, FontStyle.Bold);
            lblLoginTitle.ForeColor = Color.FromArgb(30, 42, 61);
            lblLoginTitle.Location = new Point(42, 122);
            lblLoginTitle.Margin = new Padding(0);
            lblLoginTitle.Name = "lblLoginTitle";
            lblLoginTitle.Size = new Size(476, 48);
            lblLoginTitle.TabIndex = 2;
            lblLoginTitle.Text = "로그인";
            lblLoginTitle.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // lblLoginId
            // 
            lblLoginId.Dock = DockStyle.Fill;
            lblLoginId.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            lblLoginId.ForeColor = Color.FromArgb(30, 42, 61);
            lblLoginId.Location = new Point(42, 170);
            lblLoginId.Margin = new Padding(0);
            lblLoginId.Name = "lblLoginId";
            lblLoginId.Size = new Size(476, 26);
            lblLoginId.TabIndex = 3;
            lblLoginId.Text = "사번 / 아이디";
            lblLoginId.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // txtLoginId
            // 
            txtLoginId.Dock = DockStyle.Fill;
            txtLoginId.Font = new Font("맑은 고딕", 11F);
            txtLoginId.Location = new Point(42, 198);
            txtLoginId.Margin = new Padding(0, 2, 0, 2);
            txtLoginId.MinimumSize = new Size(0, 32);
            txtLoginId.Name = "txtLoginId";
            txtLoginId.Size = new Size(476, 32);
            txtLoginId.TabIndex = 0;
            // 
            // lblPassword
            // 
            lblPassword.Dock = DockStyle.Fill;
            lblPassword.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            lblPassword.ForeColor = Color.FromArgb(30, 42, 61);
            lblPassword.Location = new Point(42, 234);
            lblPassword.Margin = new Padding(0);
            lblPassword.Name = "lblPassword";
            lblPassword.Size = new Size(476, 34);
            lblPassword.TabIndex = 4;
            lblPassword.Text = "비밀번호";
            lblPassword.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // txtPassword
            // 
            txtPassword.Dock = DockStyle.Fill;
            txtPassword.Font = new Font("맑은 고딕", 11F);
            txtPassword.Location = new Point(42, 270);
            txtPassword.Margin = new Padding(0, 2, 0, 2);
            txtPassword.MinimumSize = new Size(0, 32);
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(476, 32);
            txtPassword.TabIndex = 1;
            txtPassword.UseSystemPasswordChar = true;
            // 
            // btnLogin
            // 
            btnLogin.BackColor = Color.FromArgb(52, 101, 224);
            btnLogin.Cursor = Cursors.Hand;
            btnLogin.Dock = DockStyle.Fill;
            btnLogin.FlatAppearance.BorderColor = Color.FromArgb(213, 220, 231);
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.Font = new Font("맑은 고딕", 9F);
            btnLogin.ForeColor = Color.White;
            btnLogin.Location = new Point(42, 326);
            btnLogin.Margin = new Padding(0);
            btnLogin.Name = "btnLogin";
            btnLogin.Size = new Size(476, 44);
            btnLogin.TabIndex = 2;
            btnLogin.Text = "로그인";
            btnLogin.UseVisualStyleBackColor = false;
            // 
            // layoutRoleNavigation
            // 
            layoutRoleNavigation.ColumnCount = 2;
            layoutRoleNavigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layoutRoleNavigation.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            layoutRoleNavigation.Controls.Add(btnEmployeePreview, 0, 0);
            layoutRoleNavigation.Controls.Add(btnAdministratorPreview, 1, 0);
            layoutRoleNavigation.Dock = DockStyle.Fill;
            layoutRoleNavigation.Location = new Point(42, 580);
            layoutRoleNavigation.Margin = new Padding(0);
            layoutRoleNavigation.Name = "layoutRoleNavigation";
            layoutRoleNavigation.RowCount = 1;
            layoutRoleNavigation.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutRoleNavigation.Size = new Size(476, 40);
            layoutRoleNavigation.TabIndex = 5;
            // 
            // btnEmployeePreview
            // 
            btnEmployeePreview.BackColor = Color.White;
            btnEmployeePreview.Cursor = Cursors.Hand;
            btnEmployeePreview.Dock = DockStyle.Fill;
            btnEmployeePreview.FlatAppearance.BorderColor = Color.FromArgb(213, 220, 231);
            btnEmployeePreview.FlatStyle = FlatStyle.Flat;
            btnEmployeePreview.Font = new Font("맑은 고딕", 9F);
            btnEmployeePreview.ForeColor = Color.FromArgb(30, 42, 61);
            btnEmployeePreview.Location = new Point(0, 0);
            btnEmployeePreview.Margin = new Padding(0, 0, 6, 0);
            btnEmployeePreview.Name = "btnEmployeePreview";
            btnEmployeePreview.Size = new Size(232, 40);
            btnEmployeePreview.TabIndex = 4;
            btnEmployeePreview.Text = "직원 화면";
            btnEmployeePreview.UseVisualStyleBackColor = false;
            btnEmployeePreview.Click += btnEmployeePreview_Click;
            // 
            // btnAdministratorPreview
            // 
            btnAdministratorPreview.BackColor = Color.White;
            btnAdministratorPreview.Cursor = Cursors.Hand;
            btnAdministratorPreview.Dock = DockStyle.Fill;
            btnAdministratorPreview.FlatAppearance.BorderColor = Color.FromArgb(213, 220, 231);
            btnAdministratorPreview.FlatStyle = FlatStyle.Flat;
            btnAdministratorPreview.Font = new Font("맑은 고딕", 9F);
            btnAdministratorPreview.ForeColor = Color.FromArgb(30, 42, 61);
            btnAdministratorPreview.Location = new Point(244, 0);
            btnAdministratorPreview.Margin = new Padding(6, 0, 0, 0);
            btnAdministratorPreview.Name = "btnAdministratorPreview";
            btnAdministratorPreview.Size = new Size(232, 40);
            btnAdministratorPreview.TabIndex = 5;
            btnAdministratorPreview.Text = "관리자 화면";
            btnAdministratorPreview.UseVisualStyleBackColor = false;
            btnAdministratorPreview.Click += btnAdministratorPreview_Click;
            // 
            // LoginForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.White;
            ClientSize = new Size(560, 650);
            Controls.Add(layoutLogin);
            Font = new Font("맑은 고딕", 9F);
            MinimumSize = new Size(520, 650);
            Name = "LoginForm";
            StartPosition = FormStartPosition.CenterScreen;
            Text = "원격 근무용 업무 처리 확인 시스템 · 로그인";
            layoutLogin.ResumeLayout(false);
            layoutLogin.PerformLayout();
            layoutRoleNavigation.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel layoutLogin;
        private TableLayoutPanel layoutRoleNavigation;
        private Label lblCompanyName;
        private Label lblSystemName;
        private Label lblLoginTitle;
        private Label lblLoginId;
        private TextBox txtLoginId;
        private Label lblPassword;
        private TextBox txtPassword;
        private Button btnLogin;
        private Button btnEmployeePreview;
        private Button btnAdministratorPreview;
    }
}
