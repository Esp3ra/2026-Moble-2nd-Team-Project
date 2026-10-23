#nullable disable
namespace RemoteWork.WinForms.Features.Attendance
{
    partial class FingerprintIdentityForm
    {
        private System.ComponentModel.IContainer components;
        protected override void Dispose(bool disposing)
        {
            if (disposing) { ReleaseAttempt(); components?.Dispose(); }
            base.Dispose(disposing);
        }
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            layoutPage = new TableLayoutPanel();
            lblTitle = new Label();
            lblStatus = new Label();
            lblDetail = new Label();
            lblNotice = new Label();
            flowActions = new FlowLayoutPanel();
            btnStart = new Button();
            btnCancel = new Button();
            btnClose = new Button();
            contextTimer = new System.Windows.Forms.Timer(components);
            layoutPage.SuspendLayout(); flowActions.SuspendLayout(); SuspendLayout();
            layoutPage.Name = "layoutPage";
            layoutPage.Dock = DockStyle.Fill;
            layoutPage.Padding = new Padding(24);
            layoutPage.ColumnCount = 1;
            layoutPage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutPage.RowCount = 5;
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            layoutPage.Controls.Add(lblTitle, 0, 0);
            layoutPage.Controls.Add(lblStatus, 0, 1);
            layoutPage.Controls.Add(lblDetail, 0, 2);
            layoutPage.Controls.Add(lblNotice, 0, 3);
            layoutPage.Controls.Add(flowActions, 0, 4);
            lblTitle.Name = "lblTitle"; lblTitle.Dock = DockStyle.Fill;
            lblTitle.Font = new Font("맑은 고딕", 20F, FontStyle.Bold);
            lblTitle.Text = "지문 본인 확인";
            lblStatus.Name = "lblStatus"; lblStatus.Dock = DockStyle.Fill;
            lblStatus.Font = new Font("맑은 고딕", 13F, FontStyle.Bold);
            lblStatus.Text = "지문 연결 준비 전";
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblDetail.Name = "lblDetail"; lblDetail.Dock = DockStyle.Fill;
            lblDetail.Padding = new Padding(0, 10, 0, 0);
            lblDetail.Text = "시작을 누른 뒤 OLED 안내에 따라 손가락을 올려주세요.";
            lblNotice.Name = "lblNotice"; lblNotice.Dock = DockStyle.Fill;
            lblNotice.Text = "본인 확인 결과만 표시합니다. 로그인·출근·오후 업무 전 지문 확인·퇴근의 최종 처리는 담당 화면에서 확인합니다.";
            lblNotice.ForeColor = Color.FromArgb(80, 94, 112);
            flowActions.Name = "flowActions"; flowActions.Dock = DockStyle.Fill;
            flowActions.WrapContents = false;
            flowActions.Controls.AddRange(new Control[] { btnStart, btnCancel, btnClose });
            btnStart.Name = "btnStart"; btnStart.Text = "지문 확인 시작";
            btnStart.Size = new Size(150, 38); btnStart.Click += Start_Click;
            btnCancel.Name = "btnCancel"; btnCancel.Text = "요청 취소";
            btnCancel.Size = new Size(120, 38); btnCancel.Click += Cancel_Click;
            btnClose.Name = "btnClose"; btnClose.Text = "닫기";
            btnClose.Size = new Size(100, 38); btnClose.Click += Close_Click;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(242, 245, 249);
            ClientSize = new Size(660, 400);
            MinimumSize = new Size(600, 390);
            Controls.Add(layoutPage);
            Font = new Font("맑은 고딕", 10F);
            Name = "FingerprintIdentityForm"; Text = "지문 본인 확인";
            StartPosition = FormStartPosition.CenterParent;
            FormClosing += IdentityForm_Closing;
            contextTimer.Interval = 250;
            contextTimer.Tick += ContextTimer_Tick;
            contextTimer.Enabled = true;
            layoutPage.ResumeLayout(false); flowActions.ResumeLayout(false); ResumeLayout(false);
        }
        private TableLayoutPanel layoutPage;
        private Label lblTitle, lblStatus, lblDetail, lblNotice;
        private FlowLayoutPanel flowActions;
        private Button btnStart, btnCancel, btnClose;
        private System.Windows.Forms.Timer contextTimer;
    }
}
