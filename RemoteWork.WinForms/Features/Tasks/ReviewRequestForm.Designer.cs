#nullable disable

namespace RemoteWork.WinForms.Features.Tasks
{
    partial class ReviewRequestForm
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
            layoutContent = new System.Windows.Forms.TableLayoutPanel();
            layoutContent2 = new System.Windows.Forms.TableLayoutPanel();
            lblPageTitle = new System.Windows.Forms.Label();
            panelContent = new System.Windows.Forms.Panel();
            panelContent2 = new System.Windows.Forms.Panel();
            layoutContent3 = new System.Windows.Forms.TableLayoutPanel();
            lblTaskSummary = new System.Windows.Forms.Label();
            txtTaskSummary = new System.Windows.Forms.TextBox();
            lblCurrentStatus = new System.Windows.Forms.Label();
            txtCurrentStatus = new System.Windows.Forms.TextBox();
            lblCurrentDeadline = new System.Windows.Forms.Label();
            txtCurrentDeadline = new System.Windows.Forms.TextBox();
            lblRequestReason = new System.Windows.Forms.Label();
            txtRequestReason = new System.Windows.Forms.TextBox();
            lblResultPreview = new System.Windows.Forms.Label();
            txtResultPreview = new System.Windows.Forms.TextBox();
            flowActions = new System.Windows.Forms.FlowLayoutPanel();
            btnConfirm = new System.Windows.Forms.Button();
            btnCancel = new System.Windows.Forms.Button();
            btnClose = new System.Windows.Forms.Button();
            layoutContent.SuspendLayout();
            layoutContent2.SuspendLayout();
            panelContent.SuspendLayout();
            panelContent2.SuspendLayout();
            layoutContent3.SuspendLayout();
            flowActions.SuspendLayout();
            SuspendLayout();
            //
            // layoutContent
            //
            layoutContent.Text = "";
            layoutContent.Location = new System.Drawing.Point(22, 22);
            layoutContent.Size = new System.Drawing.Size(736, 586);
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
            layoutContent.RowCount = 3;
            layoutContent.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent.Name = "layoutContent";
            layoutContent.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            layoutContent.Controls.Add(layoutContent2, 0, 0);
            layoutContent.Controls.Add(panelContent, 0, 1);
            layoutContent.Controls.Add(flowActions, 0, 2);
            //
            // layoutContent2
            //
            layoutContent2.Text = "";
            layoutContent2.Location = new System.Drawing.Point(0, 0);
            layoutContent2.Size = new System.Drawing.Size(736, 70);
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
            lblPageTitle.Text = "검토 요청";
            lblPageTitle.Location = new System.Drawing.Point(0, 4);
            lblPageTitle.Size = new System.Drawing.Size(134, 28);
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
            panelContent.Size = new System.Drawing.Size(736, 426);
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
            panelContent.Controls.Add(panelContent2);
            //
            // panelContent2
            //
            panelContent2.Text = "";
            panelContent2.Location = new System.Drawing.Point(0, 0);
            panelContent2.Size = new System.Drawing.Size(719, 440);
            panelContent2.MinimumSize = new System.Drawing.Size(0, 440);
            panelContent2.MaximumSize = new System.Drawing.Size(0, 0);
            panelContent2.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            panelContent2.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            panelContent2.Dock = System.Windows.Forms.DockStyle.Top;
            panelContent2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            panelContent2.AutoSize = false;
            panelContent2.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            panelContent2.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            panelContent2.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            panelContent2.TabIndex = 0;
            panelContent2.TabStop = false;
            panelContent2.AutoScroll = true;
            panelContent2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            panelContent2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            panelContent2.Name = "panelContent2";
            panelContent2.Controls.Add(layoutContent3);
            //
            // layoutContent3
            //
            layoutContent3.Text = "";
            layoutContent3.Location = new System.Drawing.Point(0, 0);
            layoutContent3.Size = new System.Drawing.Size(719, 300);
            layoutContent3.MinimumSize = new System.Drawing.Size(0, 0);
            layoutContent3.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent3.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent3.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent3.Dock = System.Windows.Forms.DockStyle.Top;
            layoutContent3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            layoutContent3.AutoSize = true;
            layoutContent3.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            layoutContent3.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            layoutContent3.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            layoutContent3.TabIndex = 0;
            layoutContent3.TabStop = false;
            layoutContent3.AutoScroll = false;
            layoutContent3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            layoutContent3.ColumnCount = 2;
            layoutContent3.RowCount = 5;
            layoutContent3.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent3.Name = "layoutContent3";
            layoutContent3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 116F));
            layoutContent3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 112F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            layoutContent3.Controls.Add(lblTaskSummary, 0, 0);
            layoutContent3.Controls.Add(txtTaskSummary, 1, 0);
            layoutContent3.Controls.Add(lblCurrentStatus, 0, 1);
            layoutContent3.Controls.Add(txtCurrentStatus, 1, 1);
            layoutContent3.Controls.Add(lblCurrentDeadline, 0, 2);
            layoutContent3.Controls.Add(txtCurrentDeadline, 1, 2);
            layoutContent3.Controls.Add(lblRequestReason, 0, 3);
            layoutContent3.Controls.Add(txtRequestReason, 1, 3);
            layoutContent3.Controls.Add(lblResultPreview, 0, 4);
            layoutContent3.Controls.Add(txtResultPreview, 1, 4);
            //
            // lblTaskSummary
            //
            lblTaskSummary.Text = "대상 업무";
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
            lblTaskSummary.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
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
            txtTaskSummary.Text = "TASK-001 · 주간 업무 자료 정리";
            txtTaskSummary.Location = new System.Drawing.Point(119, 3);
            txtTaskSummary.Size = new System.Drawing.Size(597, 28);
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
            // lblCurrentStatus
            //
            lblCurrentStatus.Text = "현재 상태";
            lblCurrentStatus.Location = new System.Drawing.Point(0, 48);
            lblCurrentStatus.Size = new System.Drawing.Size(108, 36);
            lblCurrentStatus.MinimumSize = new System.Drawing.Size(0, 0);
            lblCurrentStatus.MaximumSize = new System.Drawing.Size(0, 0);
            lblCurrentStatus.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblCurrentStatus.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblCurrentStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            lblCurrentStatus.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblCurrentStatus.AutoSize = false;
            lblCurrentStatus.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblCurrentStatus.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblCurrentStatus.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblCurrentStatus.TabIndex = 2;
            lblCurrentStatus.TabStop = false;
            lblCurrentStatus.AutoEllipsis = false;
            lblCurrentStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblCurrentStatus.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblCurrentStatus.UseCompatibleTextRendering = false;
            lblCurrentStatus.Name = "lblCurrentStatus";
            //
            // txtCurrentStatus
            //
            txtCurrentStatus.Text = "진행중";
            txtCurrentStatus.Location = new System.Drawing.Point(119, 47);
            txtCurrentStatus.Size = new System.Drawing.Size(597, 28);
            txtCurrentStatus.MinimumSize = new System.Drawing.Size(0, 28);
            txtCurrentStatus.MaximumSize = new System.Drawing.Size(0, 0);
            txtCurrentStatus.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            txtCurrentStatus.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtCurrentStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            txtCurrentStatus.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtCurrentStatus.AutoSize = true;
            txtCurrentStatus.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtCurrentStatus.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            txtCurrentStatus.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtCurrentStatus.TabIndex = 3;
            txtCurrentStatus.TabStop = false;
            txtCurrentStatus.Multiline = false;
            txtCurrentStatus.ReadOnly = true;
            txtCurrentStatus.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtCurrentStatus.ScrollBars = System.Windows.Forms.ScrollBars.None;
            txtCurrentStatus.UseSystemPasswordChar = false;
            txtCurrentStatus.WordWrap = true;
            txtCurrentStatus.MaxLength = 32767;
            txtCurrentStatus.AcceptsReturn = false;
            txtCurrentStatus.AcceptsTab = false;
            txtCurrentStatus.Name = "txtCurrentStatus";
            //
            // lblCurrentDeadline
            //
            lblCurrentDeadline.Text = "현재 마감일";
            lblCurrentDeadline.Location = new System.Drawing.Point(0, 92);
            lblCurrentDeadline.Size = new System.Drawing.Size(108, 36);
            lblCurrentDeadline.MinimumSize = new System.Drawing.Size(0, 0);
            lblCurrentDeadline.MaximumSize = new System.Drawing.Size(0, 0);
            lblCurrentDeadline.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblCurrentDeadline.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblCurrentDeadline.Dock = System.Windows.Forms.DockStyle.Fill;
            lblCurrentDeadline.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblCurrentDeadline.AutoSize = false;
            lblCurrentDeadline.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblCurrentDeadline.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblCurrentDeadline.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblCurrentDeadline.TabIndex = 4;
            lblCurrentDeadline.TabStop = false;
            lblCurrentDeadline.AutoEllipsis = false;
            lblCurrentDeadline.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblCurrentDeadline.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblCurrentDeadline.UseCompatibleTextRendering = false;
            lblCurrentDeadline.Name = "lblCurrentDeadline";
            //
            // txtCurrentDeadline
            //
            txtCurrentDeadline.Text = "2026-10-22";
            txtCurrentDeadline.Location = new System.Drawing.Point(119, 91);
            txtCurrentDeadline.Size = new System.Drawing.Size(597, 28);
            txtCurrentDeadline.MinimumSize = new System.Drawing.Size(0, 28);
            txtCurrentDeadline.MaximumSize = new System.Drawing.Size(0, 0);
            txtCurrentDeadline.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            txtCurrentDeadline.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtCurrentDeadline.Dock = System.Windows.Forms.DockStyle.Fill;
            txtCurrentDeadline.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtCurrentDeadline.AutoSize = true;
            txtCurrentDeadline.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtCurrentDeadline.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            txtCurrentDeadline.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtCurrentDeadline.TabIndex = 5;
            txtCurrentDeadline.TabStop = false;
            txtCurrentDeadline.Multiline = false;
            txtCurrentDeadline.ReadOnly = true;
            txtCurrentDeadline.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtCurrentDeadline.ScrollBars = System.Windows.Forms.ScrollBars.None;
            txtCurrentDeadline.UseSystemPasswordChar = false;
            txtCurrentDeadline.WordWrap = true;
            txtCurrentDeadline.MaxLength = 32767;
            txtCurrentDeadline.AcceptsReturn = false;
            txtCurrentDeadline.AcceptsTab = false;
            txtCurrentDeadline.Name = "txtCurrentDeadline";
            //
            // lblRequestReason
            //
            lblRequestReason.Text = "검토 요청 내용";
            lblRequestReason.Location = new System.Drawing.Point(0, 136);
            lblRequestReason.Size = new System.Drawing.Size(108, 104);
            lblRequestReason.MinimumSize = new System.Drawing.Size(0, 0);
            lblRequestReason.MaximumSize = new System.Drawing.Size(0, 0);
            lblRequestReason.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblRequestReason.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblRequestReason.Dock = System.Windows.Forms.DockStyle.Fill;
            lblRequestReason.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblRequestReason.AutoSize = false;
            lblRequestReason.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblRequestReason.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblRequestReason.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblRequestReason.TabIndex = 6;
            lblRequestReason.TabStop = false;
            lblRequestReason.AutoEllipsis = false;
            lblRequestReason.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblRequestReason.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblRequestReason.UseCompatibleTextRendering = false;
            lblRequestReason.Name = "lblRequestReason";
            //
            // txtRequestReason
            //
            txtRequestReason.Text = "";
            txtRequestReason.Location = new System.Drawing.Point(116, 136);
            txtRequestReason.Size = new System.Drawing.Size(603, 102);
            txtRequestReason.MinimumSize = new System.Drawing.Size(0, 90);
            txtRequestReason.MaximumSize = new System.Drawing.Size(0, 0);
            txtRequestReason.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            txtRequestReason.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtRequestReason.Dock = System.Windows.Forms.DockStyle.Fill;
            txtRequestReason.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtRequestReason.AutoSize = true;
            txtRequestReason.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtRequestReason.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            txtRequestReason.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtRequestReason.TabIndex = 7;
            txtRequestReason.TabStop = true;
            txtRequestReason.Multiline = true;
            txtRequestReason.ReadOnly = false;
            txtRequestReason.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtRequestReason.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtRequestReason.UseSystemPasswordChar = false;
            txtRequestReason.WordWrap = true;
            txtRequestReason.MaxLength = 32767;
            txtRequestReason.AcceptsReturn = false;
            txtRequestReason.AcceptsTab = false;
            txtRequestReason.Name = "txtRequestReason";
            //
            // lblResultPreview
            //
            lblResultPreview.Text = "결과물 표시";
            lblResultPreview.Location = new System.Drawing.Point(0, 248);
            lblResultPreview.Size = new System.Drawing.Size(108, 48);
            lblResultPreview.MinimumSize = new System.Drawing.Size(0, 0);
            lblResultPreview.MaximumSize = new System.Drawing.Size(0, 0);
            lblResultPreview.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblResultPreview.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblResultPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            lblResultPreview.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblResultPreview.AutoSize = false;
            lblResultPreview.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblResultPreview.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblResultPreview.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblResultPreview.TabIndex = 8;
            lblResultPreview.TabStop = false;
            lblResultPreview.AutoEllipsis = false;
            lblResultPreview.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblResultPreview.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblResultPreview.UseCompatibleTextRendering = false;
            lblResultPreview.Name = "lblResultPreview";
            //
            // txtResultPreview
            //
            txtResultPreview.Text = "결과물은 결과 제출 화면에서 확인합니다.";
            txtResultPreview.Location = new System.Drawing.Point(119, 247);
            txtResultPreview.Size = new System.Drawing.Size(597, 50);
            txtResultPreview.MinimumSize = new System.Drawing.Size(0, 46);
            txtResultPreview.MaximumSize = new System.Drawing.Size(0, 0);
            txtResultPreview.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            txtResultPreview.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtResultPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            txtResultPreview.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtResultPreview.AutoSize = true;
            txtResultPreview.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtResultPreview.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            txtResultPreview.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtResultPreview.TabIndex = 9;
            txtResultPreview.TabStop = false;
            txtResultPreview.Multiline = true;
            txtResultPreview.ReadOnly = true;
            txtResultPreview.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtResultPreview.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtResultPreview.UseSystemPasswordChar = false;
            txtResultPreview.WordWrap = true;
            txtResultPreview.MaxLength = 32767;
            txtResultPreview.AcceptsReturn = false;
            txtResultPreview.AcceptsTab = false;
            txtResultPreview.Name = "txtResultPreview";
            //
            // flowActions
            //
            flowActions.Text = "";
            flowActions.Location = new System.Drawing.Point(0, 536);
            flowActions.Size = new System.Drawing.Size(736, 42);
            flowActions.MinimumSize = new System.Drawing.Size(0, 40);
            flowActions.MaximumSize = new System.Drawing.Size(0, 0);
            flowActions.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            flowActions.Padding = new System.Windows.Forms.Padding(0, 10, 0, 0);
            flowActions.Dock = System.Windows.Forms.DockStyle.Fill;
            flowActions.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            flowActions.AutoSize = true;
            flowActions.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            flowActions.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            flowActions.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            flowActions.TabIndex = 3;
            flowActions.TabStop = false;
            flowActions.AutoScroll = false;
            flowActions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowActions.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            flowActions.WrapContents = true;
            flowActions.Name = "flowActions";
            flowActions.Controls.Add(btnConfirm);
            flowActions.Controls.Add(btnCancel);
            flowActions.Controls.Add(btnClose);
            //
            // btnConfirm
            //
            btnConfirm.Text = "요청 확인";
            btnConfirm.Location = new System.Drawing.Point(633, 10);
            btnConfirm.Size = new System.Drawing.Size(95, 35);
            btnConfirm.MinimumSize = new System.Drawing.Size(88, 34);
            btnConfirm.MaximumSize = new System.Drawing.Size(0, 0);
            btnConfirm.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnConfirm.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnConfirm.Dock = System.Windows.Forms.DockStyle.None;
            btnConfirm.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnConfirm.AutoSize = true;
            btnConfirm.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnConfirm.ForeColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnConfirm.BackColor = System.Drawing.Color.FromArgb(255, 52, 101, 224);
            btnConfirm.TabIndex = 0;
            btnConfirm.TabStop = true;
            btnConfirm.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnConfirm.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnConfirm.UseVisualStyleBackColor = false;
            btnConfirm.DialogResult = System.Windows.Forms.DialogResult.None;
            btnConfirm.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnConfirm.Cursor = System.Windows.Forms.Cursors.Hand;
            btnConfirm.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 52, 101, 224);
            btnConfirm.FlatAppearance.BorderSize = 1;
            btnConfirm.Name = "btnConfirm";
            btnConfirm.Click += new System.EventHandler(Unavailable_Click);
            //
            // btnCancel
            //
            btnCancel.Text = "취소";
            btnCancel.Location = new System.Drawing.Point(537, 10);
            btnCancel.Size = new System.Drawing.Size(88, 35);
            btnCancel.MinimumSize = new System.Drawing.Size(88, 34);
            btnCancel.MaximumSize = new System.Drawing.Size(0, 0);
            btnCancel.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnCancel.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnCancel.Dock = System.Windows.Forms.DockStyle.None;
            btnCancel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnCancel.AutoSize = true;
            btnCancel.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnCancel.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            btnCancel.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnCancel.TabIndex = 1;
            btnCancel.TabStop = true;
            btnCancel.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnCancel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnCancel.UseVisualStyleBackColor = false;
            btnCancel.DialogResult = System.Windows.Forms.DialogResult.None;
            btnCancel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnCancel.Cursor = System.Windows.Forms.Cursors.Hand;
            btnCancel.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 213, 220, 231);
            btnCancel.FlatAppearance.BorderSize = 1;
            btnCancel.Name = "btnCancel";
            btnCancel.Click += new System.EventHandler(ClosePreview_Click);
            //
            // btnClose
            //
            btnClose.Text = "닫기";
            btnClose.Location = new System.Drawing.Point(441, 10);
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
            btnClose.TabIndex = 2;
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
            // ReviewRequestForm
            //
            Text = "검토 요청";
            MinimumSize = new System.Drawing.Size(690, 560);
            MaximumSize = new System.Drawing.Size(0, 0);
            Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            Padding = new System.Windows.Forms.Padding(22, 22, 22, 22);
            AutoSize = false;
            Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            TabIndex = 0;
            TabStop = true;
            ClientSize = new System.Drawing.Size(780, 630);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            MinimizeBox = false;
            MaximizeBox = true;
            Name = "ReviewRequestForm";
            Controls.Add(layoutContent);
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            CancelButton = btnClose;
            flowActions.ResumeLayout(false);
            flowActions.PerformLayout();
            layoutContent3.ResumeLayout(false);
            layoutContent3.PerformLayout();
            panelContent2.ResumeLayout(false);
            panelContent2.PerformLayout();
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
        private System.Windows.Forms.Panel panelContent;
        private System.Windows.Forms.Panel panelContent2;
        private System.Windows.Forms.TableLayoutPanel layoutContent3;
        private System.Windows.Forms.Label lblTaskSummary;
        private System.Windows.Forms.TextBox txtTaskSummary;
        private System.Windows.Forms.Label lblCurrentStatus;
        private System.Windows.Forms.TextBox txtCurrentStatus;
        private System.Windows.Forms.Label lblCurrentDeadline;
        private System.Windows.Forms.TextBox txtCurrentDeadline;
        private System.Windows.Forms.Label lblRequestReason;
        private System.Windows.Forms.TextBox txtRequestReason;
        private System.Windows.Forms.Label lblResultPreview;
        private System.Windows.Forms.TextBox txtResultPreview;
        private System.Windows.Forms.FlowLayoutPanel flowActions;
        private System.Windows.Forms.Button btnConfirm;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnClose;
    }
}
