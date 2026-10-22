#nullable disable
namespace RemoteWork.WinForms.Shell
{
    partial class FingerprintLoginForm
    {
        private System.ComponentModel.IContainer components;
        protected override void Dispose(bool disposing)
        {
            if (disposing) { ReleaseLoginView(); components?.Dispose(); }
            base.Dispose(disposing);
        }
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            layoutPage = new TableLayoutPanel(); lblTitle = new Label();
            lblStatus = new Label(); lblDetail = new Label(); cmbDevice = new ComboBox();
            actions = new FlowLayoutPanel(); btnStart = new Button(); btnClose = new Button();
            layoutPage.SuspendLayout(); actions.SuspendLayout(); SuspendLayout();
            layoutPage.Name = "layoutPage"; layoutPage.Dock = DockStyle.Fill;
            layoutPage.Padding = new Padding(24); layoutPage.ColumnCount = 1; layoutPage.RowCount = 5;
            layoutPage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            layoutPage.Controls.Add(lblTitle, 0, 0); layoutPage.Controls.Add(cmbDevice, 0, 1);
            layoutPage.Controls.Add(lblStatus, 0, 2); layoutPage.Controls.Add(lblDetail, 0, 3);
            layoutPage.Controls.Add(actions, 0, 4);
            lblTitle.Name = "lblTitle"; lblTitle.Dock = DockStyle.Fill;
            lblTitle.Font = new Font("맑은 고딕", 18F, FontStyle.Bold); lblTitle.Text = "지문 확인 후 출근·로그인";
            cmbDevice.Name = "cmbDevice"; cmbDevice.Dock = DockStyle.Fill;
            cmbDevice.DropDownStyle = ComboBoxStyle.DropDownList;
            lblStatus.Name = "lblStatus"; lblStatus.Dock = DockStyle.Fill;
            lblStatus.Font = new Font("맑은 고딕", 12F, FontStyle.Bold); lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Text = "지문 장치를 확인해 주세요";
            lblDetail.Name = "lblDetail"; lblDetail.Dock = DockStyle.Fill;
            lblDetail.Padding = new Padding(0, 8, 0, 0);
            lblDetail.Text = "지문 일치 뒤 서버의 출근 저장까지 확인해야 로그인됩니다. 결과 확인 중에는 같은 작업이 유지됩니다.";
            actions.Name = "actions"; actions.Dock = DockStyle.Fill; actions.WrapContents = false;
            actions.Controls.AddRange(new Control[] { btnStart, btnClose });
            btnStart.Name = "btnStart"; btnStart.Text = "지문 확인 후 출근"; btnStart.Size = new Size(210, 38); btnStart.Click += Start_Click;
            btnClose.Name = "btnClose"; btnClose.Text = "닫기"; btnClose.Size = new Size(100, 38); btnClose.Click += Close_Click;
            AutoScaleDimensions = new SizeF(7F, 15F); AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(680, 390); MinimumSize = new Size(640, 390);
            Font = new Font("맑은 고딕", 10F); BackColor = Color.FromArgb(242, 245, 249);
            Text = "출근·로그인 확인"; Name = "FingerprintLoginForm"; StartPosition = FormStartPosition.CenterParent;
            FormClosing += LoginForm_Closing; Controls.Add(layoutPage);
            layoutPage.ResumeLayout(false); actions.ResumeLayout(false); ResumeLayout(false);
        }
        private TableLayoutPanel layoutPage;
        private Label lblTitle, lblStatus, lblDetail;
        private ComboBox cmbDevice;
        private FlowLayoutPanel actions;
        private Button btnStart, btnClose;
    }
}
