#nullable disable
namespace RemoteWork.LeavePreview
{
    partial class LeavePreviewForm
    {
        private System.ComponentModel.IContainer components;
        protected override void Dispose(bool disposing)
        {
            if (disposing) { ReleasePreview(); components?.Dispose(); }
            base.Dispose(disposing);
        }
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            layoutPage = new TableLayoutPanel(); lblPreview = new Label(); tabs = new TabControl();
            employeeTab = new TabPage(); reviewerTab = new TabPage(); employeeHost = new Panel(); reviewerHost = new Panel();
            layoutPage.SuspendLayout(); tabs.SuspendLayout(); employeeTab.SuspendLayout(); reviewerTab.SuspendLayout(); SuspendLayout();
            layoutPage.Name = "layoutPage"; layoutPage.Dock = DockStyle.Fill; layoutPage.ColumnCount = 1; layoutPage.RowCount = 2;
            layoutPage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutPage.Controls.Add(lblPreview, 0, 0); layoutPage.Controls.Add(tabs, 0, 1);
            lblPreview.Name = "lblPreview"; lblPreview.Dock = DockStyle.Fill; lblPreview.Padding = new Padding(18, 12, 12, 8);
            lblPreview.BackColor = Color.FromArgb(255, 244, 216); lblPreview.ForeColor = Color.FromArgb(87, 64, 18);
            lblPreview.Text = "연차 화면 체험 · 실제 신청·승인은 저장되지 않습니다.\r\n직원 탭에서 신청한 뒤 관리자 탭에서 검토해 보세요. 예시 데이터는 이 창을 닫으면 사라지며 잔여 연차 계산은 연결 전입니다.";
            tabs.Name = "tabs"; tabs.Dock = DockStyle.Fill; tabs.Controls.Add(employeeTab); tabs.Controls.Add(reviewerTab);
            employeeTab.Name = "employeeTab"; employeeTab.Text = "1. 직원 · 신청과 내역"; employeeTab.Controls.Add(employeeHost);
            reviewerTab.Name = "reviewerTab"; reviewerTab.Text = "2. 담당 관리자 · 검토"; reviewerTab.Controls.Add(reviewerHost);
            employeeHost.Name = "employeeHost"; employeeHost.Dock = DockStyle.Fill;
            reviewerHost.Name = "reviewerHost"; reviewerHost.Dock = DockStyle.Fill;
            AutoScaleDimensions = new SizeF(7F, 15F); AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1160, 760); MinimumSize = new Size(980, 680);
            Font = new Font("맑은 고딕", 9F); Text = "연차 화면 체험 · 반려 사유 확인 · 실제 저장 없음";
            Name = "LeavePreviewForm"; StartPosition = FormStartPosition.CenterScreen;
            Controls.Add(layoutPage);
            reviewerTab.ResumeLayout(false); employeeTab.ResumeLayout(false); tabs.ResumeLayout(false); layoutPage.ResumeLayout(false); ResumeLayout(false);
        }
        private TableLayoutPanel layoutPage;
        private Label lblPreview;
        private TabControl tabs;
        private TabPage employeeTab, reviewerTab;
        private Panel employeeHost, reviewerHost;
    }
}
