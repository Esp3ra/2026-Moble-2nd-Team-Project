#nullable disable

namespace RemoteWork.WinForms.Features.Results
{
    partial class SubmissionForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle headerStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle cellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle headerStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle cellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            layoutContent = new System.Windows.Forms.TableLayoutPanel();
            layoutContent2 = new System.Windows.Forms.TableLayoutPanel();
            lblPageTitle = new System.Windows.Forms.Label();
            layoutSubmissionStatus = new System.Windows.Forms.TableLayoutPanel();
            txtSubmissionStatus = new System.Windows.Forms.TextBox();
            btnRetryTask = new System.Windows.Forms.Button();
            panelContent = new System.Windows.Forms.Panel();
            layoutContent3 = new System.Windows.Forms.TableLayoutPanel();
            panelContent2 = new System.Windows.Forms.Panel();
            layoutContent4 = new System.Windows.Forms.TableLayoutPanel();
            lblText2 = new System.Windows.Forms.Label();
            panelContent3 = new System.Windows.Forms.Panel();
            layoutContent5 = new System.Windows.Forms.TableLayoutPanel();
            lblTaskSummary = new System.Windows.Forms.Label();
            txtTaskSummary = new System.Windows.Forms.TextBox();
            lblSubmissionVersion = new System.Windows.Forms.Label();
            txtSubmissionVersion = new System.Windows.Forms.TextBox();
            lblResultDescription = new System.Windows.Forms.Label();
            txtResultDescription = new System.Windows.Forms.TextBox();
            panelContent4 = new System.Windows.Forms.Panel();
            layoutContent6 = new System.Windows.Forms.TableLayoutPanel();
            lblText6 = new System.Windows.Forms.Label();
            tabDetails = new System.Windows.Forms.TabControl();
            tabPage = new System.Windows.Forms.TabPage();
            layoutContent7 = new System.Windows.Forms.TableLayoutPanel();
            gridPendingFiles = new System.Windows.Forms.DataGridView();
            columnGridPendingFiles1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridPendingFiles2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridPendingFiles3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            flowActions = new System.Windows.Forms.FlowLayoutPanel();
            btnAttachFile = new System.Windows.Forms.Button();
            btnRemoveAttachment = new System.Windows.Forms.Button();
            tabPage2 = new System.Windows.Forms.TabPage();
            layoutContent8 = new System.Windows.Forms.TableLayoutPanel();
            txtContent4 = new System.Windows.Forms.TextBox();
            gridPreviousFiles = new System.Windows.Forms.DataGridView();
            columnGridPreviousFiles1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridPreviousFiles2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridPreviousFiles3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            flowActions2 = new System.Windows.Forms.FlowLayoutPanel();
            btnSubmit = new System.Windows.Forms.Button();
            btnClose = new System.Windows.Forms.Button();
            layoutContent.SuspendLayout();
            layoutContent2.SuspendLayout();
            panelContent.SuspendLayout();
            layoutContent3.SuspendLayout();
            panelContent2.SuspendLayout();
            layoutContent4.SuspendLayout();
            panelContent3.SuspendLayout();
            layoutContent5.SuspendLayout();
            panelContent4.SuspendLayout();
            layoutContent6.SuspendLayout();
            tabDetails.SuspendLayout();
            tabPage.SuspendLayout();
            layoutContent7.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridPendingFiles).BeginInit();
            flowActions.SuspendLayout();
            tabPage2.SuspendLayout();
            layoutContent8.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridPreviousFiles).BeginInit();
            flowActions2.SuspendLayout();
            SuspendLayout();
            //
            // layoutContent
            //
            layoutContent.Text = "";
            layoutContent.Location = new System.Drawing.Point(22, 22);
            layoutContent.Size = new System.Drawing.Size(956, 636);
            layoutContent.MinimumSize = new System.Drawing.Size(0, 0);
            layoutContent.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            layoutContent.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent.Dock = System.Windows.Forms.DockStyle.Fill;
            layoutContent.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            layoutContent.AutoSize = false;
            layoutContent.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            layoutContent.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            layoutContent.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            layoutContent.TabIndex = 0;
            layoutContent.TabStop = false;
            layoutContent.AutoScroll = false;
            layoutContent.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent.ColumnCount = 1;
            layoutContent.RowCount = 4;
            layoutContent.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent.Name = "layoutContent";
            layoutContent.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 82F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layoutContent.Controls.Add(layoutContent2, 0, 0);
            layoutContent.Controls.Add(layoutSubmissionStatus, 0, 1);
            layoutContent.Controls.Add(panelContent, 0, 2);
            layoutContent.Controls.Add(flowActions2, 0, 3);
            // Always visible outside the attachment/history tabs and content scroll.
            layoutSubmissionStatus.Name = "layoutSubmissionStatus";
            layoutSubmissionStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            layoutSubmissionStatus.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            layoutSubmissionStatus.ColumnCount = 2;
            layoutSubmissionStatus.RowCount = 1;
            layoutSubmissionStatus.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutSubmissionStatus.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            layoutSubmissionStatus.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            txtSubmissionStatus.Name = "txtSubmissionStatus";
            txtSubmissionStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            txtSubmissionStatus.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            txtSubmissionStatus.Multiline = true;
            txtSubmissionStatus.ReadOnly = true;
            txtSubmissionStatus.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtSubmissionStatus.BackColor = System.Drawing.Color.White;
            txtSubmissionStatus.AccessibleName = "결과 제출 상태 및 오류 안내";
            txtSubmissionStatus.AccessibleDescription = "현재 조회 상태, 입력 잠금 사유 또는 제출 결과입니다.";
            txtSubmissionStatus.TabIndex = 0;
            btnRetryTask.Name = "btnRetryTask";
            btnRetryTask.Text = "업무 다시 확인";
            btnRetryTask.AutoSize = true;
            btnRetryTask.MinimumSize = new System.Drawing.Size(120, 36);
            btnRetryTask.Padding = new System.Windows.Forms.Padding(8, 4, 8, 4);
            btnRetryTask.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
            btnRetryTask.Margin = new System.Windows.Forms.Padding(0);
            btnRetryTask.TabIndex = 1;
            btnRetryTask.AccessibleDescription = "현재 업무 정보를 다시 조회합니다. 결과 제출이나 파일 전송을 실행하지 않습니다.";
            btnRetryTask.Click += new System.EventHandler(RetryTask_Click);
            layoutSubmissionStatus.Controls.Add(txtSubmissionStatus, 0, 0);
            layoutSubmissionStatus.Controls.Add(btnRetryTask, 1, 0);
            //
            // layoutContent2
            //
            layoutContent2.Text = "";
            layoutContent2.Location = new System.Drawing.Point(0, 0);
            layoutContent2.Size = new System.Drawing.Size(956, 70);
            layoutContent2.MinimumSize = new System.Drawing.Size(0, 0);
            layoutContent2.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent2.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent2.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent2.Dock = System.Windows.Forms.DockStyle.Fill;
            layoutContent2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            layoutContent2.AutoSize = false;
            layoutContent2.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            layoutContent2.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            layoutContent2.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            layoutContent2.TabIndex = 0;
            layoutContent2.TabStop = false;
            layoutContent2.AutoScroll = false;
            layoutContent2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent2.ColumnCount = 1;
            layoutContent2.RowCount = 1;
            layoutContent2.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent2.Name = "layoutContent2";
            layoutContent2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            layoutContent2.Controls.Add(lblPageTitle, 0, 0);
            //
            // lblPageTitle
            //
            lblPageTitle.Text = "업무 결과 제출";
            lblPageTitle.Location = new System.Drawing.Point(0, 4);
            lblPageTitle.Size = new System.Drawing.Size(197, 28);
            lblPageTitle.MinimumSize = new System.Drawing.Size(0, 0);
            lblPageTitle.MaximumSize = new System.Drawing.Size(0, 0);
            lblPageTitle.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblPageTitle.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblPageTitle.Dock = System.Windows.Forms.DockStyle.None;
            lblPageTitle.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblPageTitle.AutoSize = true;
            lblPageTitle.Font = new System.Drawing.Font("맑은 고딕", 20F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblPageTitle.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblPageTitle.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblPageTitle.TabIndex = 0;
            lblPageTitle.TabStop = false;
            lblPageTitle.AutoEllipsis = false;
            lblPageTitle.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            lblPageTitle.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblPageTitle.UseCompatibleTextRendering = false;
            lblPageTitle.Name = "lblPageTitle";
            //
            // panelContent
            //
            panelContent.Text = "";
            panelContent.Location = new System.Drawing.Point(0, 110);
            panelContent.Size = new System.Drawing.Size(956, 476);
            panelContent.MinimumSize = new System.Drawing.Size(0, 0);
            panelContent.MaximumSize = new System.Drawing.Size(0, 0);
            panelContent.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            panelContent.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            panelContent.Dock = System.Windows.Forms.DockStyle.Fill;
            panelContent.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            panelContent.AutoSize = false;
            panelContent.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            panelContent.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            panelContent.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            panelContent.TabIndex = 2;
            panelContent.TabStop = false;
            panelContent.AutoScroll = true;
            panelContent.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            panelContent.BorderStyle = System.Windows.Forms.BorderStyle.None;
            panelContent.Name = "panelContent";
            panelContent.Controls.Add(layoutContent3);
            //
            // layoutContent3
            //
            layoutContent3.Text = "";
            layoutContent3.Location = new System.Drawing.Point(0, 0);
            layoutContent3.Size = new System.Drawing.Size(956, 476);
            layoutContent3.MinimumSize = new System.Drawing.Size(0, 440);
            layoutContent3.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent3.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent3.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent3.Dock = System.Windows.Forms.DockStyle.Top;
            layoutContent3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            layoutContent3.AutoSize = false;
            layoutContent3.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            layoutContent3.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            layoutContent3.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            layoutContent3.TabIndex = 0;
            layoutContent3.TabStop = false;
            layoutContent3.AutoScroll = false;
            layoutContent3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent3.ColumnCount = 2;
            layoutContent3.RowCount = 1;
            layoutContent3.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent3.Name = "layoutContent3";
            layoutContent3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 52F));
            layoutContent3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 48F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.Controls.Add(panelContent2, 0, 0);
            layoutContent3.Controls.Add(panelContent4, 1, 0);
            //
            // panelContent2
            //
            panelContent2.Text = "";
            panelContent2.Location = new System.Drawing.Point(0, 0);
            panelContent2.Size = new System.Drawing.Size(487, 466);
            panelContent2.MinimumSize = new System.Drawing.Size(0, 120);
            panelContent2.MaximumSize = new System.Drawing.Size(0, 0);
            panelContent2.Margin = new System.Windows.Forms.Padding(0, 0, 10, 10);
            panelContent2.Padding = new System.Windows.Forms.Padding(16, 16, 16, 16);
            panelContent2.Dock = System.Windows.Forms.DockStyle.Fill;
            panelContent2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            panelContent2.AutoSize = false;
            panelContent2.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            panelContent2.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            panelContent2.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            panelContent2.TabIndex = 0;
            panelContent2.TabStop = false;
            panelContent2.AutoScroll = false;
            panelContent2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            panelContent2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panelContent2.Name = "panelContent2";
            panelContent2.Controls.Add(layoutContent4);
            //
            // layoutContent4
            //
            layoutContent4.Text = "";
            layoutContent4.Location = new System.Drawing.Point(16, 16);
            layoutContent4.Size = new System.Drawing.Size(453, 432);
            layoutContent4.MinimumSize = new System.Drawing.Size(0, 0);
            layoutContent4.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent4.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            layoutContent4.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent4.Dock = System.Windows.Forms.DockStyle.Fill;
            layoutContent4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            layoutContent4.AutoSize = false;
            layoutContent4.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            layoutContent4.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            layoutContent4.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            layoutContent4.TabIndex = 0;
            layoutContent4.TabStop = false;
            layoutContent4.AutoScroll = false;
            layoutContent4.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent4.ColumnCount = 1;
            layoutContent4.RowCount = 2;
            layoutContent4.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent4.Name = "layoutContent4";
            layoutContent4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent4.Controls.Add(lblText2, 0, 0);
            layoutContent4.Controls.Add(panelContent3, 0, 1);
            //
            // lblText2
            //
            lblText2.Text = "제출 내용";
            lblText2.Location = new System.Drawing.Point(0, 4);
            lblText2.Size = new System.Drawing.Size(70, 19);
            lblText2.MinimumSize = new System.Drawing.Size(0, 0);
            lblText2.MaximumSize = new System.Drawing.Size(0, 0);
            lblText2.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblText2.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblText2.Dock = System.Windows.Forms.DockStyle.None;
            lblText2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblText2.AutoSize = true;
            lblText2.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblText2.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblText2.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            lblText2.TabIndex = 0;
            lblText2.TabStop = false;
            lblText2.AutoEllipsis = false;
            lblText2.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            lblText2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblText2.UseCompatibleTextRendering = false;
            lblText2.Name = "lblText2";
            //
            // panelContent3
            //
            panelContent3.Text = "";
            panelContent3.Location = new System.Drawing.Point(0, 34);
            panelContent3.Size = new System.Drawing.Size(453, 398);
            panelContent3.MinimumSize = new System.Drawing.Size(0, 0);
            panelContent3.MaximumSize = new System.Drawing.Size(0, 0);
            panelContent3.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            panelContent3.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            panelContent3.Dock = System.Windows.Forms.DockStyle.Fill;
            panelContent3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            panelContent3.AutoSize = false;
            panelContent3.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            panelContent3.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            panelContent3.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            panelContent3.TabIndex = 1;
            panelContent3.TabStop = false;
            panelContent3.AutoScroll = true;
            panelContent3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            panelContent3.BorderStyle = System.Windows.Forms.BorderStyle.None;
            panelContent3.Name = "panelContent3";
            panelContent3.Controls.Add(layoutContent5);
            //
            // layoutContent5
            //
            layoutContent5.Text = "";
            layoutContent5.Location = new System.Drawing.Point(0, 0);
            layoutContent5.Size = new System.Drawing.Size(453, 200);
            layoutContent5.MinimumSize = new System.Drawing.Size(0, 0);
            layoutContent5.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent5.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent5.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent5.Dock = System.Windows.Forms.DockStyle.Top;
            layoutContent5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            layoutContent5.AutoSize = true;
            layoutContent5.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            layoutContent5.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            layoutContent5.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            layoutContent5.TabIndex = 0;
            layoutContent5.TabStop = false;
            layoutContent5.AutoScroll = false;
            layoutContent5.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            layoutContent5.ColumnCount = 2;
            layoutContent5.RowCount = 3;
            layoutContent5.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent5.Name = "layoutContent5";
            layoutContent5.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 116F));
            layoutContent5.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent5.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent5.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent5.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 112F));
            layoutContent5.Controls.Add(lblTaskSummary, 0, 0);
            layoutContent5.Controls.Add(txtTaskSummary, 1, 0);
            layoutContent5.Controls.Add(lblSubmissionVersion, 0, 1);
            layoutContent5.Controls.Add(txtSubmissionVersion, 1, 1);
            layoutContent5.Controls.Add(lblResultDescription, 0, 2);
            layoutContent5.Controls.Add(txtResultDescription, 1, 2);
            //
            // lblTaskSummary
            //
            lblTaskSummary.Text = "업무";
            lblTaskSummary.Location = new System.Drawing.Point(0, 4);
            lblTaskSummary.Size = new System.Drawing.Size(108, 36);
            lblTaskSummary.MinimumSize = new System.Drawing.Size(0, 0);
            lblTaskSummary.MaximumSize = new System.Drawing.Size(0, 0);
            lblTaskSummary.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblTaskSummary.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblTaskSummary.Dock = System.Windows.Forms.DockStyle.Fill;
            lblTaskSummary.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblTaskSummary.AutoSize = false;
            lblTaskSummary.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblTaskSummary.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblTaskSummary.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            lblTaskSummary.TabIndex = 0;
            lblTaskSummary.TabStop = false;
            lblTaskSummary.AutoEllipsis = false;
            lblTaskSummary.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblTaskSummary.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblTaskSummary.UseCompatibleTextRendering = false;
            lblTaskSummary.Name = "lblTaskSummary";
            //
            // txtTaskSummary
            //
            txtTaskSummary.Text = "WRK-2026-003 · 원격 근무 가이드 초안";
            txtTaskSummary.Location = new System.Drawing.Point(119, 3);
            txtTaskSummary.Size = new System.Drawing.Size(331, 28);
            txtTaskSummary.MinimumSize = new System.Drawing.Size(0, 28);
            txtTaskSummary.MaximumSize = new System.Drawing.Size(0, 0);
            txtTaskSummary.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            txtTaskSummary.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtTaskSummary.Dock = System.Windows.Forms.DockStyle.Fill;
            txtTaskSummary.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtTaskSummary.AutoSize = true;
            txtTaskSummary.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtTaskSummary.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            txtTaskSummary.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtTaskSummary.TabIndex = 1;
            txtTaskSummary.TabStop = false;
            txtTaskSummary.Multiline = false;
            txtTaskSummary.ReadOnly = true;
            txtTaskSummary.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtTaskSummary.ScrollBars = System.Windows.Forms.ScrollBars.None;
            txtTaskSummary.UseSystemPasswordChar = false;
            txtTaskSummary.WordWrap = true;
            txtTaskSummary.MaxLength = 32767;
            txtTaskSummary.AcceptsReturn = false;
            txtTaskSummary.AcceptsTab = false;
            txtTaskSummary.Name = "txtTaskSummary";
            //
            // lblSubmissionVersion
            //
            lblSubmissionVersion.Text = "담당자 / 제출 버전";
            lblSubmissionVersion.Location = new System.Drawing.Point(0, 48);
            lblSubmissionVersion.Size = new System.Drawing.Size(108, 36);
            lblSubmissionVersion.MinimumSize = new System.Drawing.Size(0, 0);
            lblSubmissionVersion.MaximumSize = new System.Drawing.Size(0, 0);
            lblSubmissionVersion.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblSubmissionVersion.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblSubmissionVersion.Dock = System.Windows.Forms.DockStyle.Fill;
            lblSubmissionVersion.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblSubmissionVersion.AutoSize = false;
            lblSubmissionVersion.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblSubmissionVersion.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblSubmissionVersion.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            lblSubmissionVersion.TabIndex = 2;
            lblSubmissionVersion.TabStop = false;
            lblSubmissionVersion.AutoEllipsis = false;
            lblSubmissionVersion.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblSubmissionVersion.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblSubmissionVersion.UseCompatibleTextRendering = false;
            lblSubmissionVersion.Name = "lblSubmissionVersion";
            //
            // txtSubmissionVersion
            //
            txtSubmissionVersion.Text = "김직원 / v2 제출 예정";
            txtSubmissionVersion.Location = new System.Drawing.Point(119, 47);
            txtSubmissionVersion.Size = new System.Drawing.Size(331, 28);
            txtSubmissionVersion.MinimumSize = new System.Drawing.Size(0, 28);
            txtSubmissionVersion.MaximumSize = new System.Drawing.Size(0, 0);
            txtSubmissionVersion.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            txtSubmissionVersion.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtSubmissionVersion.Dock = System.Windows.Forms.DockStyle.Fill;
            txtSubmissionVersion.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtSubmissionVersion.AutoSize = true;
            txtSubmissionVersion.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtSubmissionVersion.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            txtSubmissionVersion.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtSubmissionVersion.TabIndex = 3;
            txtSubmissionVersion.TabStop = false;
            txtSubmissionVersion.Multiline = false;
            txtSubmissionVersion.ReadOnly = true;
            txtSubmissionVersion.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtSubmissionVersion.ScrollBars = System.Windows.Forms.ScrollBars.None;
            txtSubmissionVersion.UseSystemPasswordChar = false;
            txtSubmissionVersion.WordWrap = true;
            txtSubmissionVersion.MaxLength = 32767;
            txtSubmissionVersion.AcceptsReturn = false;
            txtSubmissionVersion.AcceptsTab = false;
            txtSubmissionVersion.Name = "txtSubmissionVersion";
            //
            // lblResultDescription
            //
            lblResultDescription.Text = "결과 설명";
            lblResultDescription.Location = new System.Drawing.Point(0, 92);
            lblResultDescription.Size = new System.Drawing.Size(108, 104);
            lblResultDescription.MinimumSize = new System.Drawing.Size(0, 0);
            lblResultDescription.MaximumSize = new System.Drawing.Size(0, 0);
            lblResultDescription.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblResultDescription.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblResultDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            lblResultDescription.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblResultDescription.AutoSize = false;
            lblResultDescription.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblResultDescription.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblResultDescription.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            lblResultDescription.TabIndex = 4;
            lblResultDescription.TabStop = false;
            lblResultDescription.AutoEllipsis = false;
            lblResultDescription.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblResultDescription.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblResultDescription.UseCompatibleTextRendering = false;
            lblResultDescription.Name = "lblResultDescription";
            //
            // txtResultDescription
            //
            txtResultDescription.Text = "검토 의견을 반영하고 제출 기준과 예외 상황 설명을 보완했습니다.";
            txtResultDescription.Location = new System.Drawing.Point(116, 92);
            txtResultDescription.Size = new System.Drawing.Size(337, 102);
            txtResultDescription.MinimumSize = new System.Drawing.Size(0, 90);
            txtResultDescription.MaximumSize = new System.Drawing.Size(0, 0);
            txtResultDescription.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            txtResultDescription.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtResultDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            txtResultDescription.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtResultDescription.AutoSize = true;
            txtResultDescription.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtResultDescription.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            txtResultDescription.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtResultDescription.TabIndex = 5;
            txtResultDescription.TabStop = true;
            txtResultDescription.Multiline = true;
            txtResultDescription.ReadOnly = false;
            txtResultDescription.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtResultDescription.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtResultDescription.UseSystemPasswordChar = false;
            txtResultDescription.WordWrap = true;
            txtResultDescription.MaxLength = 32767;
            txtResultDescription.AcceptsReturn = true;
            txtResultDescription.AcceptsTab = false;
            txtResultDescription.Name = "txtResultDescription";
            //
            // panelContent4
            //
            panelContent4.Text = "";
            panelContent4.Location = new System.Drawing.Point(497, 0);
            panelContent4.Size = new System.Drawing.Size(449, 466);
            panelContent4.MinimumSize = new System.Drawing.Size(0, 120);
            panelContent4.MaximumSize = new System.Drawing.Size(0, 0);
            panelContent4.Margin = new System.Windows.Forms.Padding(0, 0, 10, 10);
            panelContent4.Padding = new System.Windows.Forms.Padding(16, 16, 16, 16);
            panelContent4.Dock = System.Windows.Forms.DockStyle.Fill;
            panelContent4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            panelContent4.AutoSize = false;
            panelContent4.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            panelContent4.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            panelContent4.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            panelContent4.TabIndex = 1;
            panelContent4.TabStop = false;
            panelContent4.AutoScroll = false;
            panelContent4.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            panelContent4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panelContent4.Name = "panelContent4";
            panelContent4.Controls.Add(layoutContent6);
            //
            // layoutContent6
            //
            layoutContent6.Text = "";
            layoutContent6.Location = new System.Drawing.Point(16, 16);
            layoutContent6.Size = new System.Drawing.Size(415, 432);
            layoutContent6.MinimumSize = new System.Drawing.Size(0, 0);
            layoutContent6.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent6.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            layoutContent6.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent6.Dock = System.Windows.Forms.DockStyle.Fill;
            layoutContent6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            layoutContent6.AutoSize = false;
            layoutContent6.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            layoutContent6.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            layoutContent6.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            layoutContent6.TabIndex = 0;
            layoutContent6.TabStop = false;
            layoutContent6.AutoScroll = false;
            layoutContent6.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent6.ColumnCount = 1;
            layoutContent6.RowCount = 2;
            layoutContent6.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent6.Name = "layoutContent6";
            layoutContent6.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            layoutContent6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent6.Controls.Add(lblText6, 0, 0);
            layoutContent6.Controls.Add(tabDetails, 0, 1);
            //
            // lblText6
            //
            lblText6.Text = "첨부 자료와 이전 제출물";
            lblText6.Location = new System.Drawing.Point(0, 4);
            lblText6.Size = new System.Drawing.Size(206, 19);
            lblText6.MinimumSize = new System.Drawing.Size(0, 0);
            lblText6.MaximumSize = new System.Drawing.Size(0, 0);
            lblText6.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblText6.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblText6.Dock = System.Windows.Forms.DockStyle.None;
            lblText6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblText6.AutoSize = true;
            lblText6.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblText6.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblText6.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            lblText6.TabIndex = 0;
            lblText6.TabStop = false;
            lblText6.AutoEllipsis = false;
            lblText6.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            lblText6.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblText6.UseCompatibleTextRendering = false;
            lblText6.Name = "lblText6";
            //
            // tabDetails
            //
            tabDetails.Text = "";
            tabDetails.Location = new System.Drawing.Point(3, 37);
            tabDetails.Size = new System.Drawing.Size(409, 392);
            tabDetails.MinimumSize = new System.Drawing.Size(0, 0);
            tabDetails.MaximumSize = new System.Drawing.Size(0, 0);
            tabDetails.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            tabDetails.Padding = new System.Drawing.Point(6, 3);
            tabDetails.Dock = System.Windows.Forms.DockStyle.Fill;
            tabDetails.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            tabDetails.AutoSize = false;
            tabDetails.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            tabDetails.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            tabDetails.BackColor = System.Drawing.Color.FromArgb(255, 240, 240, 240);
            tabDetails.TabIndex = 1;
            tabDetails.TabStop = true;
            tabDetails.SizeMode = System.Windows.Forms.TabSizeMode.Normal;
            tabDetails.Alignment = System.Windows.Forms.TabAlignment.Top;
            tabDetails.Name = "tabDetails";
            tabDetails.Controls.Add(tabPage);
            tabDetails.Controls.Add(tabPage2);
            //
            // tabPage
            //
            tabPage.Text = "첨부 예정";
            tabPage.Location = new System.Drawing.Point(4, 24);
            tabPage.Size = new System.Drawing.Size(401, 364);
            tabPage.MinimumSize = new System.Drawing.Size(0, 0);
            tabPage.MaximumSize = new System.Drawing.Size(0, 0);
            tabPage.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            tabPage.Padding = new System.Windows.Forms.Padding(8, 8, 8, 8);
            tabPage.Dock = System.Windows.Forms.DockStyle.None;
            tabPage.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            tabPage.AutoSize = false;
            tabPage.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            tabPage.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            tabPage.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            tabPage.TabIndex = 0;
            tabPage.TabStop = false;
            tabPage.UseVisualStyleBackColor = false;
            tabPage.BorderStyle = System.Windows.Forms.BorderStyle.None;
            tabPage.Name = "tabPage";
            tabPage.Controls.Add(layoutContent7);
            //
            // layoutContent7
            //
            layoutContent7.Text = "";
            layoutContent7.Location = new System.Drawing.Point(8, 8);
            layoutContent7.Size = new System.Drawing.Size(385, 348);
            layoutContent7.MinimumSize = new System.Drawing.Size(0, 0);
            layoutContent7.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent7.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            layoutContent7.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent7.Dock = System.Windows.Forms.DockStyle.Fill;
            layoutContent7.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            layoutContent7.AutoSize = false;
            layoutContent7.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            layoutContent7.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            layoutContent7.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            layoutContent7.TabIndex = 0;
            layoutContent7.TabStop = false;
            layoutContent7.AutoScroll = false;
            layoutContent7.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent7.ColumnCount = 1;
            layoutContent7.RowCount = 2;
            layoutContent7.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent7.Name = "layoutContent7";
            layoutContent7.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent7.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent7.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent7.Controls.Add(gridPendingFiles, 0, 0);
            layoutContent7.Controls.Add(flowActions, 0, 1);
            //
            // gridPendingFiles
            //
            gridPendingFiles.Text = "";
            gridPendingFiles.Location = new System.Drawing.Point(3, 3);
            gridPendingFiles.Size = new System.Drawing.Size(379, 298);
            gridPendingFiles.MinimumSize = new System.Drawing.Size(0, 140);
            gridPendingFiles.MaximumSize = new System.Drawing.Size(0, 0);
            gridPendingFiles.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            gridPendingFiles.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            gridPendingFiles.Dock = System.Windows.Forms.DockStyle.Fill;
            gridPendingFiles.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            gridPendingFiles.AutoSize = false;
            gridPendingFiles.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            gridPendingFiles.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            gridPendingFiles.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            gridPendingFiles.TabIndex = 0;
            gridPendingFiles.TabStop = true;
            gridPendingFiles.ReadOnly = true;
            gridPendingFiles.AllowUserToAddRows = false;
            gridPendingFiles.AllowUserToDeleteRows = false;
            gridPendingFiles.AllowUserToOrderColumns = false;
            gridPendingFiles.AllowUserToResizeRows = false;
            gridPendingFiles.RowHeadersVisible = false;
            gridPendingFiles.MultiSelect = false;
            gridPendingFiles.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            gridPendingFiles.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            gridPendingFiles.BackgroundColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            gridPendingFiles.BorderStyle = System.Windows.Forms.BorderStyle.None;
            gridPendingFiles.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            gridPendingFiles.GridColor = System.Drawing.Color.FromArgb(255, 235, 239, 246);
            gridPendingFiles.EnableHeadersVisualStyles = false;
            gridPendingFiles.ColumnHeadersHeight = 36;
            gridPendingFiles.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            gridPendingFiles.RowTemplate.Height = 38;
            gridPendingFiles.Name = "gridPendingFiles";
            columnGridPendingFiles1.Name = "columnGridPendingFiles1";
            columnGridPendingFiles1.HeaderText = "첨부 예정 파일";
            columnGridPendingFiles1.MinimumWidth = 168;
            columnGridPendingFiles1.Width = 168;
            columnGridPendingFiles1.FillWeight = 100F;
            columnGridPendingFiles1.ReadOnly = true;
            columnGridPendingFiles1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridPendingFiles1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridPendingFiles2.Name = "columnGridPendingFiles2";
            columnGridPendingFiles2.HeaderText = "크기";
            columnGridPendingFiles2.MinimumWidth = 82;
            columnGridPendingFiles2.Width = 106;
            columnGridPendingFiles2.FillWeight = 100F;
            columnGridPendingFiles2.ReadOnly = true;
            columnGridPendingFiles2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridPendingFiles2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridPendingFiles3.Name = "columnGridPendingFiles3";
            columnGridPendingFiles3.HeaderText = "구분";
            columnGridPendingFiles3.MinimumWidth = 82;
            columnGridPendingFiles3.Width = 105;
            columnGridPendingFiles3.FillWeight = 100F;
            columnGridPendingFiles3.ReadOnly = true;
            columnGridPendingFiles3.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridPendingFiles3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            headerStyle1.BackColor = System.Drawing.Color.FromArgb(255, 243, 246, 251);
            headerStyle1.ForeColor = System.Drawing.Color.FromArgb(255, 103, 116, 135);
            headerStyle1.SelectionBackColor = System.Drawing.Color.Empty;
            headerStyle1.SelectionForeColor = System.Drawing.Color.Empty;
            headerStyle1.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            headerStyle1.Padding = new System.Windows.Forms.Padding(6, 6, 6, 6);
            headerStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.NotSet;
            headerStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.NotSet;
            headerStyle1.NullValue = "";
            cellStyle1.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            cellStyle1.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            cellStyle1.SelectionBackColor = System.Drawing.Color.FromArgb(255, 231, 239, 255);
            cellStyle1.SelectionForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            cellStyle1.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            cellStyle1.Padding = new System.Windows.Forms.Padding(6, 6, 6, 6);
            cellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            cellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            cellStyle1.NullValue = "";
            gridPendingFiles.ColumnHeadersDefaultCellStyle = headerStyle1;
            gridPendingFiles.DefaultCellStyle = cellStyle1;
            gridPendingFiles.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { columnGridPendingFiles1, columnGridPendingFiles2, columnGridPendingFiles3 });
            //
            // flowActions
            //
            flowActions.Text = "";
            flowActions.Location = new System.Drawing.Point(0, 304);
            flowActions.Size = new System.Drawing.Size(385, 40);
            flowActions.MinimumSize = new System.Drawing.Size(0, 40);
            flowActions.MaximumSize = new System.Drawing.Size(0, 0);
            flowActions.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            flowActions.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            flowActions.Dock = System.Windows.Forms.DockStyle.Fill;
            flowActions.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            flowActions.AutoSize = true;
            flowActions.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            flowActions.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            flowActions.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            flowActions.TabIndex = 1;
            flowActions.TabStop = false;
            flowActions.AutoScroll = false;
            flowActions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowActions.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            flowActions.WrapContents = true;
            flowActions.Name = "flowActions";
            flowActions.Controls.Add(btnAttachFile);
            flowActions.Controls.Add(btnRemoveAttachment);
            //
            // btnAttachFile
            //
            btnAttachFile.Text = "파일 첨부";
            btnAttachFile.Location = new System.Drawing.Point(0, 0);
            btnAttachFile.Size = new System.Drawing.Size(95, 35);
            btnAttachFile.MinimumSize = new System.Drawing.Size(88, 34);
            btnAttachFile.MaximumSize = new System.Drawing.Size(0, 0);
            btnAttachFile.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnAttachFile.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnAttachFile.Dock = System.Windows.Forms.DockStyle.None;
            btnAttachFile.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnAttachFile.AutoSize = true;
            btnAttachFile.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnAttachFile.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            btnAttachFile.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnAttachFile.TabIndex = 0;
            btnAttachFile.TabStop = true;
            btnAttachFile.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnAttachFile.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnAttachFile.UseVisualStyleBackColor = false;
            btnAttachFile.DialogResult = System.Windows.Forms.DialogResult.None;
            btnAttachFile.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnAttachFile.Cursor = System.Windows.Forms.Cursors.Hand;
            btnAttachFile.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 213, 220, 231);
            btnAttachFile.FlatAppearance.BorderSize = 1;
            btnAttachFile.Name = "btnAttachFile";
            btnAttachFile.Click += new System.EventHandler(Unavailable_Click);
            //
            // btnRemoveAttachment
            //
            btnRemoveAttachment.Text = "선택 첨부 제거";
            btnRemoveAttachment.Location = new System.Drawing.Point(103, 0);
            btnRemoveAttachment.Size = new System.Drawing.Size(123, 35);
            btnRemoveAttachment.MinimumSize = new System.Drawing.Size(88, 34);
            btnRemoveAttachment.MaximumSize = new System.Drawing.Size(0, 0);
            btnRemoveAttachment.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnRemoveAttachment.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnRemoveAttachment.Dock = System.Windows.Forms.DockStyle.None;
            btnRemoveAttachment.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnRemoveAttachment.AutoSize = true;
            btnRemoveAttachment.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnRemoveAttachment.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            btnRemoveAttachment.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnRemoveAttachment.TabIndex = 1;
            btnRemoveAttachment.TabStop = true;
            btnRemoveAttachment.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnRemoveAttachment.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnRemoveAttachment.UseVisualStyleBackColor = false;
            btnRemoveAttachment.DialogResult = System.Windows.Forms.DialogResult.None;
            btnRemoveAttachment.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnRemoveAttachment.Cursor = System.Windows.Forms.Cursors.Hand;
            btnRemoveAttachment.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 213, 220, 231);
            btnRemoveAttachment.FlatAppearance.BorderSize = 1;
            btnRemoveAttachment.Name = "btnRemoveAttachment";
            btnRemoveAttachment.Click += new System.EventHandler(Unavailable_Click);
            //
            // tabPage2
            //
            tabPage2.Text = "이전 제출물";
            tabPage2.Location = new System.Drawing.Point(4, 24);
            tabPage2.Size = new System.Drawing.Size(401, 364);
            tabPage2.MinimumSize = new System.Drawing.Size(0, 0);
            tabPage2.MaximumSize = new System.Drawing.Size(0, 0);
            tabPage2.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            tabPage2.Padding = new System.Windows.Forms.Padding(8, 8, 8, 8);
            tabPage2.Dock = System.Windows.Forms.DockStyle.None;
            tabPage2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            tabPage2.AutoSize = false;
            tabPage2.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            tabPage2.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            tabPage2.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            tabPage2.TabIndex = 1;
            tabPage2.TabStop = false;
            tabPage2.UseVisualStyleBackColor = false;
            tabPage2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            tabPage2.Name = "tabPage2";
            tabPage2.Controls.Add(layoutContent8);
            //
            // layoutContent8
            //
            layoutContent8.Text = "";
            layoutContent8.Location = new System.Drawing.Point(8, 8);
            layoutContent8.Size = new System.Drawing.Size(385, 348);
            layoutContent8.MinimumSize = new System.Drawing.Size(0, 0);
            layoutContent8.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent8.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            layoutContent8.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent8.Dock = System.Windows.Forms.DockStyle.Fill;
            layoutContent8.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            layoutContent8.AutoSize = false;
            layoutContent8.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            layoutContent8.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            layoutContent8.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            layoutContent8.TabIndex = 0;
            layoutContent8.TabStop = false;
            layoutContent8.AutoScroll = false;
            layoutContent8.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent8.ColumnCount = 1;
            layoutContent8.RowCount = 2;
            layoutContent8.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent8.Name = "layoutContent8";
            layoutContent8.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent8.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 112F));
            layoutContent8.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent8.Controls.Add(txtContent4, 0, 0);
            layoutContent8.Controls.Add(gridPreviousFiles, 0, 1);
            //
            // txtContent4
            //
            txtContent4.Text = "이전 제출 v1 · 2026-10-20 14:00 · 반려완료\r\n반려 사유: 제출 기준과 예외 상황 설명을 보완해 주세요.";
            txtContent4.Location = new System.Drawing.Point(3, 3);
            txtContent4.Size = new System.Drawing.Size(379, 106);
            txtContent4.MinimumSize = new System.Drawing.Size(0, 56);
            txtContent4.MaximumSize = new System.Drawing.Size(0, 0);
            txtContent4.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            txtContent4.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtContent4.Dock = System.Windows.Forms.DockStyle.Fill;
            txtContent4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtContent4.AutoSize = true;
            txtContent4.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtContent4.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            txtContent4.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtContent4.TabIndex = 0;
            txtContent4.TabStop = false;
            txtContent4.Multiline = true;
            txtContent4.ReadOnly = true;
            txtContent4.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtContent4.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtContent4.UseSystemPasswordChar = false;
            txtContent4.WordWrap = true;
            txtContent4.MaxLength = 32767;
            txtContent4.AcceptsReturn = false;
            txtContent4.AcceptsTab = false;
            txtContent4.Name = "txtContent4";
            //
            // gridPreviousFiles
            //
            gridPreviousFiles.Text = "";
            gridPreviousFiles.Location = new System.Drawing.Point(3, 115);
            gridPreviousFiles.Size = new System.Drawing.Size(379, 230);
            gridPreviousFiles.MinimumSize = new System.Drawing.Size(0, 140);
            gridPreviousFiles.MaximumSize = new System.Drawing.Size(0, 0);
            gridPreviousFiles.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            gridPreviousFiles.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            gridPreviousFiles.Dock = System.Windows.Forms.DockStyle.Fill;
            gridPreviousFiles.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            gridPreviousFiles.AutoSize = false;
            gridPreviousFiles.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            gridPreviousFiles.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            gridPreviousFiles.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            gridPreviousFiles.TabIndex = 1;
            gridPreviousFiles.TabStop = true;
            gridPreviousFiles.ReadOnly = true;
            gridPreviousFiles.AllowUserToAddRows = false;
            gridPreviousFiles.AllowUserToDeleteRows = false;
            gridPreviousFiles.AllowUserToOrderColumns = false;
            gridPreviousFiles.AllowUserToResizeRows = false;
            gridPreviousFiles.RowHeadersVisible = false;
            gridPreviousFiles.MultiSelect = false;
            gridPreviousFiles.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            gridPreviousFiles.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            gridPreviousFiles.BackgroundColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            gridPreviousFiles.BorderStyle = System.Windows.Forms.BorderStyle.None;
            gridPreviousFiles.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            gridPreviousFiles.GridColor = System.Drawing.Color.FromArgb(255, 235, 239, 246);
            gridPreviousFiles.EnableHeadersVisualStyles = false;
            gridPreviousFiles.ColumnHeadersHeight = 36;
            gridPreviousFiles.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            gridPreviousFiles.RowTemplate.Height = 38;
            gridPreviousFiles.Name = "gridPreviousFiles";
            columnGridPreviousFiles1.Name = "columnGridPreviousFiles1";
            columnGridPreviousFiles1.HeaderText = "이전 첨부 파일";
            columnGridPreviousFiles1.MinimumWidth = 168;
            columnGridPreviousFiles1.Width = 168;
            columnGridPreviousFiles1.FillWeight = 100F;
            columnGridPreviousFiles1.ReadOnly = true;
            columnGridPreviousFiles1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridPreviousFiles1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridPreviousFiles2.Name = "columnGridPreviousFiles2";
            columnGridPreviousFiles2.HeaderText = "버전";
            columnGridPreviousFiles2.MinimumWidth = 82;
            columnGridPreviousFiles2.Width = 100;
            columnGridPreviousFiles2.FillWeight = 100F;
            columnGridPreviousFiles2.ReadOnly = true;
            columnGridPreviousFiles2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridPreviousFiles2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridPreviousFiles3.Name = "columnGridPreviousFiles3";
            columnGridPreviousFiles3.HeaderText = "제출일";
            columnGridPreviousFiles3.MinimumWidth = 106;
            columnGridPreviousFiles3.Width = 106;
            columnGridPreviousFiles3.FillWeight = 100F;
            columnGridPreviousFiles3.ReadOnly = true;
            columnGridPreviousFiles3.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridPreviousFiles3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            headerStyle2.BackColor = System.Drawing.Color.FromArgb(255, 243, 246, 251);
            headerStyle2.ForeColor = System.Drawing.Color.FromArgb(255, 103, 116, 135);
            headerStyle2.SelectionBackColor = System.Drawing.Color.Empty;
            headerStyle2.SelectionForeColor = System.Drawing.Color.Empty;
            headerStyle2.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            headerStyle2.Padding = new System.Windows.Forms.Padding(6, 6, 6, 6);
            headerStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.NotSet;
            headerStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.NotSet;
            headerStyle2.NullValue = "";
            cellStyle2.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            cellStyle2.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            cellStyle2.SelectionBackColor = System.Drawing.Color.FromArgb(255, 231, 239, 255);
            cellStyle2.SelectionForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            cellStyle2.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            cellStyle2.Padding = new System.Windows.Forms.Padding(6, 6, 6, 6);
            cellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            cellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            cellStyle2.NullValue = "";
            gridPreviousFiles.ColumnHeadersDefaultCellStyle = headerStyle2;
            gridPreviousFiles.DefaultCellStyle = cellStyle2;
            gridPreviousFiles.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { columnGridPreviousFiles1, columnGridPreviousFiles2, columnGridPreviousFiles3 });
            //
            // flowActions2
            //
            flowActions2.Text = "";
            flowActions2.Location = new System.Drawing.Point(0, 586);
            flowActions2.Size = new System.Drawing.Size(956, 42);
            flowActions2.MinimumSize = new System.Drawing.Size(0, 40);
            flowActions2.MaximumSize = new System.Drawing.Size(0, 0);
            flowActions2.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            flowActions2.Padding = new System.Windows.Forms.Padding(0, 10, 0, 0);
            flowActions2.Dock = System.Windows.Forms.DockStyle.Fill;
            flowActions2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            flowActions2.AutoSize = true;
            flowActions2.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            flowActions2.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            flowActions2.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            flowActions2.TabIndex = 3;
            flowActions2.TabStop = false;
            flowActions2.AutoScroll = false;
            flowActions2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowActions2.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            flowActions2.WrapContents = true;
            flowActions2.Name = "flowActions2";
            flowActions2.Controls.Add(btnSubmit);
            flowActions2.Controls.Add(btnClose);
            //
            // btnSubmit
            //
            btnSubmit.Text = "결과 제출";
            btnSubmit.Location = new System.Drawing.Point(853, 10);
            btnSubmit.Size = new System.Drawing.Size(95, 35);
            btnSubmit.MinimumSize = new System.Drawing.Size(88, 34);
            btnSubmit.MaximumSize = new System.Drawing.Size(0, 0);
            btnSubmit.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnSubmit.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnSubmit.Dock = System.Windows.Forms.DockStyle.None;
            btnSubmit.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnSubmit.AutoSize = true;
            btnSubmit.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnSubmit.ForeColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnSubmit.BackColor = System.Drawing.Color.FromArgb(255, 52, 101, 224);
            btnSubmit.TabIndex = 0;
            btnSubmit.TabStop = true;
            btnSubmit.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnSubmit.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnSubmit.UseVisualStyleBackColor = false;
            btnSubmit.DialogResult = System.Windows.Forms.DialogResult.None;
            btnSubmit.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnSubmit.Cursor = System.Windows.Forms.Cursors.Hand;
            btnSubmit.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 52, 101, 224);
            btnSubmit.FlatAppearance.BorderSize = 1;
            btnSubmit.Name = "btnSubmit";
            btnSubmit.Click += new System.EventHandler(Unavailable_Click);
            //
            // btnClose
            //
            btnClose.Text = "닫기";
            btnClose.Location = new System.Drawing.Point(757, 10);
            btnClose.Size = new System.Drawing.Size(88, 35);
            btnClose.MinimumSize = new System.Drawing.Size(88, 34);
            btnClose.MaximumSize = new System.Drawing.Size(0, 0);
            btnClose.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnClose.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnClose.Dock = System.Windows.Forms.DockStyle.None;
            btnClose.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnClose.AutoSize = true;
            btnClose.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnClose.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            btnClose.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnClose.TabIndex = 1;
            btnClose.TabStop = true;
            btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnClose.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnClose.UseVisualStyleBackColor = false;
            btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnClose.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnClose.Cursor = System.Windows.Forms.Cursors.Hand;
            btnClose.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 213, 220, 231);
            btnClose.FlatAppearance.BorderSize = 1;
            btnClose.Name = "btnClose";
            btnClose.Click += new System.EventHandler(ClosePreview_Click);
            //
            // SubmissionForm
            //
            Text = "업무 결과 제출";
            MinimumSize = new System.Drawing.Size(950, 650);
            MaximumSize = new System.Drawing.Size(0, 0);
            Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            Padding = new System.Windows.Forms.Padding(22, 22, 22, 22);
            AutoSize = false;
            Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            TabIndex = 0;
            TabStop = true;
            ClientSize = new System.Drawing.Size(1000, 680);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;
            Name = "SubmissionForm";
            Controls.Add(layoutContent);
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            CancelButton = btnClose;
            flowActions2.ResumeLayout(false);
            flowActions2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)gridPreviousFiles).EndInit();
            layoutContent8.ResumeLayout(false);
            layoutContent8.PerformLayout();
            tabPage2.ResumeLayout(false);
            tabPage2.PerformLayout();
            flowActions.ResumeLayout(false);
            flowActions.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)gridPendingFiles).EndInit();
            layoutContent7.ResumeLayout(false);
            layoutContent7.PerformLayout();
            tabPage.ResumeLayout(false);
            tabPage.PerformLayout();
            tabDetails.ResumeLayout(false);
            tabDetails.PerformLayout();
            layoutContent6.ResumeLayout(false);
            layoutContent6.PerformLayout();
            panelContent4.ResumeLayout(false);
            panelContent4.PerformLayout();
            layoutContent5.ResumeLayout(false);
            layoutContent5.PerformLayout();
            panelContent3.ResumeLayout(false);
            panelContent3.PerformLayout();
            layoutContent4.ResumeLayout(false);
            layoutContent4.PerformLayout();
            panelContent2.ResumeLayout(false);
            panelContent2.PerformLayout();
            layoutContent3.ResumeLayout(false);
            layoutContent3.PerformLayout();
            panelContent.ResumeLayout(false);
            panelContent.PerformLayout();
            layoutContent2.ResumeLayout(false);
            layoutContent2.PerformLayout();
            layoutContent.ResumeLayout(false);
            layoutContent.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel layoutContent;
        private System.Windows.Forms.TableLayoutPanel layoutContent2;
        private System.Windows.Forms.Label lblPageTitle;
        private System.Windows.Forms.TableLayoutPanel layoutSubmissionStatus;
        private System.Windows.Forms.TextBox txtSubmissionStatus;
        private System.Windows.Forms.Button btnRetryTask;
        private System.Windows.Forms.Panel panelContent;
        private System.Windows.Forms.TableLayoutPanel layoutContent3;
        private System.Windows.Forms.Panel panelContent2;
        private System.Windows.Forms.TableLayoutPanel layoutContent4;
        private System.Windows.Forms.Label lblText2;
        private System.Windows.Forms.Panel panelContent3;
        private System.Windows.Forms.TableLayoutPanel layoutContent5;
        private System.Windows.Forms.Label lblTaskSummary;
        private System.Windows.Forms.TextBox txtTaskSummary;
        private System.Windows.Forms.Label lblSubmissionVersion;
        private System.Windows.Forms.TextBox txtSubmissionVersion;
        private System.Windows.Forms.Label lblResultDescription;
        private System.Windows.Forms.TextBox txtResultDescription;
        private System.Windows.Forms.Panel panelContent4;
        private System.Windows.Forms.TableLayoutPanel layoutContent6;
        private System.Windows.Forms.Label lblText6;
        private System.Windows.Forms.TabControl tabDetails;
        private System.Windows.Forms.TabPage tabPage;
        private System.Windows.Forms.TableLayoutPanel layoutContent7;
        private System.Windows.Forms.DataGridView gridPendingFiles;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridPendingFiles1;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridPendingFiles2;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridPendingFiles3;
        private System.Windows.Forms.FlowLayoutPanel flowActions;
        private System.Windows.Forms.Button btnAttachFile;
        private System.Windows.Forms.Button btnRemoveAttachment;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TableLayoutPanel layoutContent8;
        private System.Windows.Forms.TextBox txtContent4;
        private System.Windows.Forms.DataGridView gridPreviousFiles;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridPreviousFiles1;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridPreviousFiles2;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridPreviousFiles3;
        private System.Windows.Forms.FlowLayoutPanel flowActions2;
        private System.Windows.Forms.Button btnSubmit;
        private System.Windows.Forms.Button btnClose;
    }
}
