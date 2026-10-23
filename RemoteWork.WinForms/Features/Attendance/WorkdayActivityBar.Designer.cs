#nullable disable
namespace RemoteWork.WinForms.Features.Attendance
{
    partial class WorkdayActivityBar
    {
        private System.ComponentModel.IContainer components;
        protected override void Dispose(bool disposing)
        {
            if (disposing) { ReleaseBar(); components?.Dispose(); }
            base.Dispose(disposing);
        }
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            layoutStatus = new TableLayoutPanel(); lblWorkday = new Label(); actions = new FlowLayoutPanel();
            btnNoon = new Button(); btnReconnect = new Button(); btnActivity = new Button(); btnClockOut = new Button(); refreshTimer = new System.Windows.Forms.Timer(components);
            layoutStatus.SuspendLayout(); actions.SuspendLayout(); SuspendLayout();
            layoutStatus.Name = "layoutStatus"; layoutStatus.Dock = DockStyle.Fill; layoutStatus.ColumnCount = 1; layoutStatus.RowCount = 2;
            layoutStatus.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutStatus.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F)); layoutStatus.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutStatus.Controls.Add(lblWorkday, 0, 0); layoutStatus.Controls.Add(actions, 0, 1);
            lblWorkday.Name = "lblWorkday"; lblWorkday.Dock = DockStyle.Fill; lblWorkday.Text = "현재 근무 연결 준비 중";
            lblWorkday.TextAlign = ContentAlignment.MiddleLeft; lblWorkday.AutoEllipsis = true;
            actions.Name = "actions"; actions.Dock = DockStyle.Fill; actions.WrapContents = false; actions.Margin = new Padding(0);
            actions.Controls.AddRange(new Control[] { btnNoon, btnActivity, btnReconnect, btnClockOut });
            btnNoon.Name = "btnNoon"; btnNoon.Text = "오후 업무 전 지문 확인"; btnNoon.Size = new Size(185, 30); btnNoon.Enabled = false; btnNoon.Click += Noon_Click;
            btnActivity.Name = "btnActivity"; btnActivity.Text = "활동 상태"; btnActivity.Size = new Size(110, 30); btnActivity.Enabled = false; btnActivity.Click += Activity_Click;
            btnReconnect.Name = "btnReconnect"; btnReconnect.Text = "연결 오류 재시도"; btnReconnect.Size = new Size(145, 30); btnReconnect.Enabled = false; btnReconnect.Visible = false; btnReconnect.Click += Reconnect_Click;
            btnClockOut.Name = "btnClockOut"; btnClockOut.Text = "지문 확인 후 퇴근"; btnClockOut.Size = new Size(150, 30); btnClockOut.Enabled = false; btnClockOut.Click += ClockOut_Click;
            refreshTimer.Interval = 500; refreshTimer.Tick += RefreshTimer_Tick;
            AutoScaleDimensions = new SizeF(7F, 15F); AutoScaleMode = AutoScaleMode.Font; Font = new Font("맑은 고딕", 9F);
            BackColor = Color.FromArgb(242, 245, 250); Padding = new Padding(10, 0, 10, 2); Size = new Size(800, 66);
            Name = "WorkdayActivityBar"; Controls.Add(layoutStatus);
            actions.ResumeLayout(false); layoutStatus.ResumeLayout(false); ResumeLayout(false);
        }
        private TableLayoutPanel layoutStatus;
        private FlowLayoutPanel actions;
        private Label lblWorkday;
        private Button btnNoon, btnActivity, btnReconnect, btnClockOut;
        private System.Windows.Forms.Timer refreshTimer;
    }
}
