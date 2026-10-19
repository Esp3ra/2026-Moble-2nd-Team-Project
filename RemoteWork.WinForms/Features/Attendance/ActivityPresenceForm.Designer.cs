#nullable disable
using System.Drawing;
using System.Windows.Forms;

namespace RemoteWork.WinForms.Features.Attendance
{
    partial class ActivityPresenceForm
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing) { ReleaseCamera(); components?.Dispose(); }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            layout = new TableLayoutPanel();
            lblHeading = new Label(); lblGuide = new Label();
            controlRow = new FlowLayoutPanel(); cameraOptions = new FlowLayoutPanel();
            piOptions = new FlowLayoutPanel(); lblPiAddress = new Label(); txtPiAddress = new TextBox();
            lblCamera = new Label(); cmbCamera = new ComboBox(); cmbBackend = new ComboBox();
            btnStart = new Button(); btnStop = new Button(); regionOptions = new FlowLayoutPanel();
            lblLeft = new Label(); lblTop = new Label(); lblRight = new Label(); lblBottom = new Label();
            numLeft = new NumericUpDown(); numTop = new NumericUpDown();
            numRight = new NumericUpDown(); numBottom = new NumericUpDown();
            lblStatus = new Label(); lblDetails = new Label();
            previewPanel = new Panel(); preview = new PictureBox(); lblEmpty = new Label(); lblFooter = new Label();
            refreshTimer = new System.Windows.Forms.Timer(components);
            layout.SuspendLayout(); controlRow.SuspendLayout(); cameraOptions.SuspendLayout();
            regionOptions.SuspendLayout(); previewPanel.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)preview).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numLeft).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numTop).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numRight).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numBottom).BeginInit();
            SuspendLayout();
            layout.Name = "layout"; layout.Dock = DockStyle.Fill; layout.Padding = new Padding(20);
            layout.ColumnCount = 1; layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowCount = 9;
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layout.Controls.Add(lblHeading, 0, 0); layout.Controls.Add(lblGuide, 0, 1);
            layout.Controls.Add(controlRow, 0, 2); layout.Controls.Add(piOptions, 0, 3);
            layout.Controls.Add(regionOptions, 0, 4);
            layout.Controls.Add(lblStatus, 0, 5); layout.Controls.Add(lblDetails, 0, 6);
            layout.Controls.Add(previewPanel, 0, 7); layout.Controls.Add(lblFooter, 0, 8);
            lblHeading.Name = "lblHeading"; lblHeading.Text = "활동 상태 확인";
            lblHeading.AutoSize = true; lblHeading.Font = new Font("맑은 고딕", 20F, FontStyle.Bold);
            lblGuide.Name = "lblGuide"; lblGuide.Dock = DockStyle.Fill;
            lblGuide.Text = "카메라는 손과 책상 쪽으로 고정해 주세요. 얼굴과 배경은 촬영 각도로 제외합니다.";
            controlRow.Name = "controlRow"; controlRow.Dock = DockStyle.Fill; controlRow.WrapContents = false;
            controlRow.Controls.Add(cameraOptions); controlRow.Controls.Add(btnStart); controlRow.Controls.Add(btnStop);
            cameraOptions.Name = "cameraOptions"; cameraOptions.AutoSize = true;
            cameraOptions.WrapContents = false; cameraOptions.Margin = new Padding(0, 0, 16, 0);
            cameraOptions.Controls.Add(lblCamera); cameraOptions.Controls.Add(cmbCamera); cameraOptions.Controls.Add(cmbBackend);
            lblCamera.Text = "카메라 번호"; lblCamera.AutoSize = true; lblCamera.Margin = new Padding(0, 7, 8, 0);
            cmbCamera.Name = "cmbCamera"; cmbCamera.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbCamera.Items.AddRange(new object[] { "0", "1", "2", "3", "4" }); cmbCamera.Width = 52;
            cmbBackend.Name = "cmbBackend"; cmbBackend.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbBackend.Items.AddRange(new object[] { "DSHOW", "MSMF" }); cmbBackend.Width = 92;
            btnStart.Name = "btnStart"; btnStart.Text = "카메라 시작"; btnStart.Size = new Size(124, 34);
            btnStart.BackColor = Color.FromArgb(52, 101, 224); btnStart.ForeColor = Color.White;
            btnStart.FlatStyle = FlatStyle.Flat; btnStart.Click += btnStart_Click;
            btnStop.Name = "btnStop"; btnStop.Text = "중지"; btnStop.Size = new Size(80, 34);
            btnStop.Enabled = false; btnStop.Click += btnStop_Click;
            piOptions.Name = "piOptions"; piOptions.Dock = DockStyle.Fill; piOptions.WrapContents = false;
            piOptions.Controls.Add(lblPiAddress); piOptions.Controls.Add(txtPiAddress);
            lblPiAddress.Name = "lblPiAddress"; lblPiAddress.Text = "라파 주소"; lblPiAddress.AutoSize = true;
            lblPiAddress.Margin = new Padding(0, 6, 10, 0);
            txtPiAddress.Name = "txtPiAddress"; txtPiAddress.Width = 370;
            txtPiAddress.Text = "http://raspberrypi.local:8080";
            regionOptions.Name = "regionOptions"; regionOptions.Dock = DockStyle.Fill; regionOptions.WrapContents = false;
            regionOptions.Controls.Add(lblLeft); regionOptions.Controls.Add(numLeft);
            regionOptions.Controls.Add(lblTop); regionOptions.Controls.Add(numTop);
            regionOptions.Controls.Add(lblRight); regionOptions.Controls.Add(numRight);
            regionOptions.Controls.Add(lblBottom); regionOptions.Controls.Add(numBottom);
            lblLeft.Text = "왼쪽 (%)"; lblTop.Text = "위쪽 (%)"; lblRight.Text = "오른쪽 (%)"; lblBottom.Text = "아래쪽 (%)";
            lblLeft.AutoSize = true; lblTop.AutoSize = true; lblRight.AutoSize = true; lblBottom.AutoSize = true;
            lblLeft.Margin = new Padding(0, 5, 4, 0); lblTop.Margin = new Padding(10, 5, 4, 0);
            lblRight.Margin = new Padding(10, 5, 4, 0); lblBottom.Margin = new Padding(10, 5, 4, 0);
            numLeft.Name = "numLeft"; numTop.Name = "numTop"; numRight.Name = "numRight"; numBottom.Name = "numBottom";
            numLeft.Width = 55; numTop.Width = 55; numRight.Width = 55; numBottom.Width = 55;
            numLeft.Maximum = 100; numTop.Maximum = 100; numRight.Maximum = 100; numBottom.Maximum = 100;
            numTop.Value = 35; numRight.Value = 100; numBottom.Value = 100;
            lblStatus.Name = "lblStatus"; lblStatus.Text = "카메라 꺼짐"; lblStatus.Dock = DockStyle.Fill;
            lblStatus.Font = new Font("맑은 고딕", 15F, FontStyle.Bold);
            lblDetails.Name = "lblDetails"; lblDetails.Text = "카메라 시작을 누르면 확인합니다."; lblDetails.Dock = DockStyle.Fill;
            previewPanel.Name = "previewPanel"; previewPanel.Dock = DockStyle.Fill; previewPanel.BackColor = Color.FromArgb(23, 33, 46);
            preview.Name = "preview"; preview.Dock = DockStyle.Fill; preview.SizeMode = PictureBoxSizeMode.Zoom;
            lblEmpty.Name = "lblEmpty"; lblEmpty.Text = "카메라 꺼짐"; lblEmpty.Dock = DockStyle.Fill;
            lblEmpty.ForeColor = Color.White; lblEmpty.TextAlign = ContentAlignment.MiddleCenter;
            lblEmpty.Font = new Font("맑은 고딕", 15F);
            previewPanel.Controls.Add(preview); previewPanel.Controls.Add(lblEmpty);
            lblFooter.Name = "lblFooter"; lblFooter.Dock = DockStyle.Fill; lblFooter.Padding = new Padding(0, 10, 0, 0);
            lblFooter.Text = "손·화면 움직임·조도 변화 모두 30초 없으면 자리 비움 · 연결 문제는 확인 필요\r\n영상은 저장/외부 전송하지 않으며, 출퇴근 기록에 자동 반영되지 않습니다.";
            refreshTimer.Interval = 100; refreshTimer.Tick += refreshTimer_Tick;
            AutoScaleDimensions = new SizeF(7F, 15F); AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("맑은 고딕", 9F); BackColor = Color.FromArgb(242, 245, 250); ForeColor = Color.FromArgb(30, 42, 61);
            ClientSize = new Size(880, 810); MinimumSize = new Size(800, 770);
            StartPosition = FormStartPosition.CenterParent; Text = "활동 상태 확인"; Name = "ActivityPresenceForm";
            Controls.Add(layout); Shown += ActivityPresenceForm_Shown; FormClosing += ActivityPresenceForm_FormClosing;
            ((System.ComponentModel.ISupportInitialize)numLeft).EndInit(); ((System.ComponentModel.ISupportInitialize)numTop).EndInit();
            ((System.ComponentModel.ISupportInitialize)numRight).EndInit(); ((System.ComponentModel.ISupportInitialize)numBottom).EndInit();
            ((System.ComponentModel.ISupportInitialize)preview).EndInit(); previewPanel.ResumeLayout(false);
            regionOptions.ResumeLayout(false); regionOptions.PerformLayout(); cameraOptions.ResumeLayout(false); cameraOptions.PerformLayout();
            controlRow.ResumeLayout(false); controlRow.PerformLayout(); layout.ResumeLayout(false); layout.PerformLayout(); ResumeLayout(false);
        }

        private TableLayoutPanel layout;
        private FlowLayoutPanel controlRow, cameraOptions, regionOptions;
        private FlowLayoutPanel piOptions;
        private Label lblPiAddress;
        private TextBox txtPiAddress;
        private Label lblHeading, lblGuide, lblCamera, lblLeft, lblTop, lblRight, lblBottom;
        private Label lblStatus, lblDetails, lblEmpty, lblFooter;
        private ComboBox cmbCamera, cmbBackend;
        private NumericUpDown numLeft, numTop, numRight, numBottom;
        private Button btnStart, btnStop;
        private Panel previewPanel;
        private PictureBox preview;
        private System.Windows.Forms.Timer refreshTimer;
    }
}
