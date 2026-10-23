#nullable disable
namespace RemoteWork.WinForms.Features.Attendance
{
    partial class FingerprintWorkdayForm
    {
        private System.ComponentModel.IContainer components;
        protected override void Dispose(bool disposing)
        {
            if (disposing) { ReleaseView(); components?.Dispose(); }
            base.Dispose(disposing);
        }
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            layoutPage = new TableLayoutPanel(); lblTitle = new Label(); lblStatus = new Label(); lblDetail = new Label();
            actions = new FlowLayoutPanel(); btnConfirm = new Button(); btnCancel = new Button(); btnClose = new Button();
            layoutPage.SuspendLayout(); actions.SuspendLayout(); SuspendLayout();
            layoutPage.Name = "layoutPage"; layoutPage.Dock = DockStyle.Fill; layoutPage.Padding = new Padding(24);
            layoutPage.ColumnCount = 1; layoutPage.RowCount = 4;
            layoutPage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            layoutPage.Controls.Add(lblTitle, 0, 0); layoutPage.Controls.Add(lblStatus, 0, 1);
            layoutPage.Controls.Add(lblDetail, 0, 2); layoutPage.Controls.Add(actions, 0, 3);
            lblTitle.Name = "lblTitle"; lblTitle.Dock = DockStyle.Fill;
            lblTitle.Font = new Font("맑은 고딕", 18F, FontStyle.Bold); lblTitle.Text = "오후 업무 전·퇴근 지문 확인";
            lblStatus.Name = "lblStatus"; lblStatus.Dock = DockStyle.Fill; lblStatus.Text = "지문 확인 준비";
            lblStatus.Font = new Font("맑은 고딕", 12F, FontStyle.Bold);
            lblDetail.Name = "lblDetail"; lblDetail.Dock = DockStyle.Fill; lblDetail.Padding = new Padding(0, 8, 0, 0);
            lblDetail.Text = "현재 로그인과 서버에서 확인한 근무 정보를 연결해야 사용할 수 있습니다.";
            actions.Name = "actions"; actions.Dock = DockStyle.Fill; actions.WrapContents = false;
            actions.Controls.AddRange(new Control[] { btnConfirm, btnCancel, btnClose });
            btnConfirm.Name = "btnConfirm"; btnConfirm.Text = "지문 확인"; btnConfirm.Size = new Size(210, 38); btnConfirm.Click += Confirm_Click;
            btnCancel.Name = "btnCancel"; btnCancel.Text = "요청 취소"; btnCancel.Size = new Size(110, 38); btnCancel.Click += Cancel_Click;
            btnClose.Name = "btnClose"; btnClose.Text = "닫기"; btnClose.Size = new Size(95, 38); btnClose.Click += Close_Click;
            AutoScaleDimensions = new SizeF(7F, 15F); AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(680, 350); MinimumSize = new Size(640, 350);
            Font = new Font("맑은 고딕", 10F); BackColor = Color.FromArgb(242, 245, 249);
            Name = "FingerprintWorkdayForm"; Text = "오후 업무 전·퇴근 지문 확인"; StartPosition = FormStartPosition.CenterParent;
            FormClosing += Workday_Closing; Controls.Add(layoutPage);
            layoutPage.ResumeLayout(false); actions.ResumeLayout(false); ResumeLayout(false);
        }
        private TableLayoutPanel layoutPage;
        private FlowLayoutPanel actions;
        private Label lblTitle, lblStatus, lblDetail;
        private Button btnConfirm, btnCancel, btnClose;
    }
}
