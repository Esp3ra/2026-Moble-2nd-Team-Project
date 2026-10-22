#nullable disable
using System.Drawing;
using System.Windows.Forms;

namespace RemoteWork.WinForms.Features.Attendance
{
    partial class LeaveApplicationForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing) EndLifetime();
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            layoutPage = new TableLayoutPanel();
            layoutHeading = new TableLayoutPanel();
            lblPageTitle = new Label();
            panelFields = new Panel();
            layoutFields = new TableLayoutPanel();
            lblApplicant = new Label();
            txtApplicant = new TextBox();
            lblLeaveType = new Label();
            cmbOptions = new ComboBox();
            lblStartDate = new Label();
            dtpStartDate = new DateTimePicker();
            lblEndDate = new Label();
            dtpEndDate = new DateTimePicker();
            lblLeaveReason = new Label();
            txtLeaveReason = new TextBox();
            flowActions = new FlowLayoutPanel();
            btnApplyLeave = new Button();
            btnClose = new Button();
            lblStatus = new Label();
            lblCharge = new Label();
            lblInputSummary = new Label();
            layoutPage.SuspendLayout();
            layoutHeading.SuspendLayout();
            panelFields.SuspendLayout();
            layoutFields.SuspendLayout();
            flowActions.SuspendLayout();
            SuspendLayout();
            // layoutPage
            layoutPage.ColumnCount = 1;
            layoutPage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutPage.RowCount = 6;
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            layoutPage.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            layoutPage.Dock = DockStyle.Fill;
            layoutPage.Margin = new Padding(0);
            layoutPage.Name = "layoutPage";
            layoutPage.Controls.Add(layoutHeading, 0, 0);
            layoutPage.Controls.Add(panelFields, 0, 1);
            layoutPage.Controls.Add(lblInputSummary, 0, 2);
            layoutPage.Controls.Add(lblCharge, 0, 3);
            layoutPage.Controls.Add(lblStatus, 0, 4);
            layoutPage.Controls.Add(flowActions, 0, 5);
            lblInputSummary.Name = "lblInputSummary";
            lblInputSummary.Dock = DockStyle.Fill;
            lblInputSummary.TextAlign = ContentAlignment.MiddleLeft;
            lblInputSummary.Text = "신청할 유형·기간·사유를 확인해 주세요.";
            lblCharge.Name = "lblCharge";
            lblCharge.Dock = DockStyle.Fill;
            lblCharge.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Name = "lblStatus";
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.AutoEllipsis = true;
            lblStatus.Padding = new Padding(0, 8, 0, 0);
            lblStatus.Text = "서버 연결 전입니다.";
            // layoutHeading
            layoutHeading.ColumnCount = 1;
            layoutHeading.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutHeading.RowCount = 1;
            layoutHeading.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutHeading.Dock = DockStyle.Fill;
            layoutHeading.Margin = new Padding(0);
            layoutHeading.Name = "layoutHeading";
            layoutHeading.Controls.Add(lblPageTitle, 0, 0);
            lblPageTitle.AutoSize = true;
            lblPageTitle.Font = new Font("맑은 고딕", 20F, FontStyle.Bold);
            lblPageTitle.Text = "연차 신청";
            lblPageTitle.Name = "lblPageTitle";
            lblPageTitle.Margin = new Padding(0, 0, 0, 4);
            // panelFields
            panelFields.BackColor = Color.White;
            panelFields.BorderStyle = BorderStyle.FixedSingle;
            panelFields.Padding = new Padding(20);
            panelFields.Dock = DockStyle.Fill;
            panelFields.Margin = new Padding(0);
            panelFields.Name = "panelFields";
            panelFields.Controls.Add(layoutFields);
            layoutFields.ColumnCount = 2;
            layoutFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));
            layoutFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutFields.RowCount = 5;
            layoutFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layoutFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layoutFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layoutFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
            layoutFields.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutFields.Dock = DockStyle.Fill;
            layoutFields.Margin = new Padding(0);
            layoutFields.Name = "layoutFields";
            layoutFields.Controls.Add(lblApplicant, 0, 0);
            layoutFields.Controls.Add(txtApplicant, 1, 0);
            layoutFields.Controls.Add(lblLeaveType, 0, 1);
            layoutFields.Controls.Add(cmbOptions, 1, 1);
            layoutFields.Controls.Add(lblStartDate, 0, 2);
            layoutFields.Controls.Add(dtpStartDate, 1, 2);
            layoutFields.Controls.Add(lblEndDate, 0, 3);
            layoutFields.Controls.Add(dtpEndDate, 1, 3);
            layoutFields.Controls.Add(lblLeaveReason, 0, 4);
            layoutFields.Controls.Add(txtLeaveReason, 1, 4);
            lblApplicant.Text = "신청자";
            lblApplicant.Dock = DockStyle.Fill;
            lblApplicant.TextAlign = ContentAlignment.MiddleLeft;
            lblApplicant.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            lblApplicant.Name = "lblApplicant";
            txtApplicant.Text = "";
            txtApplicant.ReadOnly = true;
            txtApplicant.Dock = DockStyle.Fill;
            txtApplicant.Margin = new Padding(0, 10, 0, 8);
            txtApplicant.BorderStyle = BorderStyle.FixedSingle;
            txtApplicant.Name = "txtApplicant";
            lblLeaveType.Text = "신청 유형";
            lblLeaveType.Dock = DockStyle.Fill;
            lblLeaveType.TextAlign = ContentAlignment.MiddleLeft;
            lblLeaveType.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            lblLeaveType.Name = "lblLeaveType";
            cmbOptions.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbOptions.Items.AddRange(new object[] { "연차", "반차(오전)", "반차(오후)", "병가", "공가" });
            cmbOptions.Dock = DockStyle.Fill;
            cmbOptions.Margin = new Padding(0, 10, 0, 8);
            cmbOptions.Name = "cmbOptions";
            lblStartDate.Text = "시작일";
            lblStartDate.Dock = DockStyle.Fill;
            lblStartDate.TextAlign = ContentAlignment.MiddleLeft;
            lblStartDate.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            lblStartDate.Name = "lblStartDate";
            dtpStartDate.Format = DateTimePickerFormat.Custom;
            dtpStartDate.CustomFormat = "yyyy-MM-dd";
            dtpStartDate.Value = new System.DateTime(2026, 10, 16);
            dtpStartDate.Dock = DockStyle.Fill;
            dtpStartDate.Margin = new Padding(0, 10, 0, 8);
            dtpStartDate.Name = "dtpStartDate";
            lblEndDate.Text = "종료일";
            lblEndDate.Dock = DockStyle.Fill;
            lblEndDate.TextAlign = ContentAlignment.MiddleLeft;
            lblEndDate.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            lblEndDate.Name = "lblEndDate";
            dtpEndDate.Format = DateTimePickerFormat.Custom;
            dtpEndDate.CustomFormat = "yyyy-MM-dd";
            dtpEndDate.Value = new System.DateTime(2026, 10, 16);
            dtpEndDate.Dock = DockStyle.Fill;
            dtpEndDate.Margin = new Padding(0, 10, 0, 8);
            dtpEndDate.Name = "dtpEndDate";
            lblLeaveReason.Text = "신청 사유";
            lblLeaveReason.Dock = DockStyle.Fill;
            lblLeaveReason.TextAlign = ContentAlignment.TopLeft;
            lblLeaveReason.Padding = new Padding(0, 10, 0, 0);
            lblLeaveReason.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            lblLeaveReason.Name = "lblLeaveReason";
            txtLeaveReason.Multiline = true;
            txtLeaveReason.Dock = DockStyle.Fill;
            txtLeaveReason.ScrollBars = ScrollBars.Vertical;
            txtLeaveReason.BorderStyle = BorderStyle.FixedSingle;
            txtLeaveReason.Margin = new Padding(0, 10, 0, 0);
            txtLeaveReason.Name = "txtLeaveReason";
            txtLeaveReason.MaxLength = 1000;
            // flowActions
            flowActions.Dock = DockStyle.Fill;
            flowActions.FlowDirection = FlowDirection.RightToLeft;
            flowActions.Padding = new Padding(0, 16, 0, 0);
            flowActions.Margin = new Padding(0);
            flowActions.Name = "flowActions";
            flowActions.Controls.Add(btnApplyLeave);
            flowActions.Controls.Add(btnClose);
            btnApplyLeave.Text = "신청";
            btnApplyLeave.Size = new Size(100, 36);
            btnApplyLeave.FlatStyle = FlatStyle.Flat;
            btnApplyLeave.FlatAppearance.BorderColor = Color.FromArgb(52, 101, 224);
            btnApplyLeave.BackColor = Color.FromArgb(52, 101, 224);
            btnApplyLeave.ForeColor = Color.White;
            btnApplyLeave.Margin = new Padding(10, 0, 0, 0);
            btnApplyLeave.Name = "btnApplyLeave";
            btnApplyLeave.Click += new System.EventHandler(ApplyLeave_Click);
            btnClose.Text = "닫기";
            btnClose.Size = new Size(100, 36);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.FlatAppearance.BorderColor = Color.FromArgb(213, 220, 231);
            btnClose.BackColor = Color.White;
            btnClose.DialogResult = DialogResult.Cancel;
            btnClose.Margin = new Padding(10, 0, 0, 0);
            btnClose.Name = "btnClose";
            btnClose.Click += new System.EventHandler(ClosePreview_Click);
            // LeaveApplicationForm
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Font = new Font("맑은 고딕", 9F);
            BackColor = Color.FromArgb(242, 245, 250);
            ForeColor = Color.FromArgb(30, 42, 61);
            ClientSize = new Size(780, 660);
            MinimumSize = new Size(690, 630);
            Padding = new Padding(22);
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            MinimizeBox = false;
            CancelButton = btnClose;
            Text = "연차 신청";
            Name = "LeaveApplicationForm";
            Controls.Add(layoutPage);
            flowActions.ResumeLayout(false);
            layoutFields.ResumeLayout(false);
            layoutFields.PerformLayout();
            panelFields.ResumeLayout(false);
            layoutHeading.ResumeLayout(false);
            layoutHeading.PerformLayout();
            layoutPage.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private TableLayoutPanel layoutPage;
        private TableLayoutPanel layoutHeading;
        private Label lblPageTitle;
        private Panel panelFields;
        private TableLayoutPanel layoutFields;
        private Label lblApplicant;
        private TextBox txtApplicant;
        private Label lblLeaveType;
        private ComboBox cmbOptions;
        private Label lblStartDate;
        private DateTimePicker dtpStartDate;
        private Label lblEndDate;
        private DateTimePicker dtpEndDate;
        private Label lblLeaveReason;
        private TextBox txtLeaveReason;
        private FlowLayoutPanel flowActions;
        private Button btnApplyLeave;
        private Button btnClose;
        private Label lblStatus;
        private Label lblCharge;
        private Label lblInputSummary;
    }
}
