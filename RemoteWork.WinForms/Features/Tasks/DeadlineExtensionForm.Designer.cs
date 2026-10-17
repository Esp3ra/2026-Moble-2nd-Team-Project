#nullable disable

namespace RemoteWork.WinForms.Features.Tasks
{
    partial class DeadlineExtensionForm
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
            lblCurrentDeadline = new System.Windows.Forms.Label();
            txtCurrentDeadline = new System.Windows.Forms.TextBox();
            lblRequestedDeadline = new System.Windows.Forms.Label();
            dtpRequestedDeadline = new System.Windows.Forms.DateTimePicker();
            lblExtensionReason = new System.Windows.Forms.Label();
            txtExtensionReason = new System.Windows.Forms.TextBox();
            lblExtensionNotice = new System.Windows.Forms.Label();
            txtExtensionNotice = new System.Windows.Forms.TextBox();
            flowActions = new System.Windows.Forms.FlowLayoutPanel();
            btnConfirm = new System.Windows.Forms.Button();
            btnCancel = new System.Windows.Forms.Button();
            btnClose = new System.Windows.Forms.Button();
            lblPreparationStatus = new System.Windows.Forms.Label();
            inputErrors = new System.Windows.Forms.ErrorProvider(components);
            ((System.ComponentModel.ISupportInitialize)inputErrors).BeginInit();
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
            layoutContent.RowCount = 4;
            layoutContent.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent.Name = "layoutContent";
            layoutContent.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            layoutContent.Controls.Add(layoutContent2, 0, 0);
            layoutContent.Controls.Add(panelContent, 0, 1);
            layoutContent.Controls.Add(lblPreparationStatus, 0, 2);
            layoutContent.Controls.Add(flowActions, 0, 3);
            //
            // lblPreparationStatus
            //
            lblPreparationStatus.AutoSize = false;
            lblPreparationStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            lblPreparationStatus.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            lblPreparationStatus.Name = "lblPreparationStatus";
            lblPreparationStatus.Text = "로컬 입력 준비 · 요청 접수·마감 변경은 하지 않습니다.";
            lblPreparationStatus.TabIndex = 3;
            //
            // inputErrors
            //
            inputErrors.BlinkStyle = System.Windows.Forms.ErrorBlinkStyle.NeverBlink;
            inputErrors.ContainerControl = this;
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
            lblPageTitle.Text = "마감 연장 요청";
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
            layoutContent3.Controls.Add(lblCurrentDeadline, 0, 1);
            layoutContent3.Controls.Add(txtCurrentDeadline, 1, 1);
            layoutContent3.Controls.Add(lblRequestedDeadline, 0, 2);
            layoutContent3.Controls.Add(dtpRequestedDeadline, 1, 2);
            layoutContent3.Controls.Add(lblExtensionReason, 0, 3);
            layoutContent3.Controls.Add(txtExtensionReason, 1, 3);
            layoutContent3.Controls.Add(lblExtensionNotice, 0, 4);
            layoutContent3.Controls.Add(txtExtensionNotice, 1, 4);
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
            txtTaskSummary.Text = "대상 업무 선택 필요";
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
            // lblCurrentDeadline
            //
            lblCurrentDeadline.Text = "현재 마감일";
            lblCurrentDeadline.Location = new System.Drawing.Point(0, 48);
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
            lblCurrentDeadline.TabIndex = 2;
            lblCurrentDeadline.TabStop = false;
            lblCurrentDeadline.AutoEllipsis = false;
            lblCurrentDeadline.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblCurrentDeadline.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblCurrentDeadline.UseCompatibleTextRendering = false;
            lblCurrentDeadline.Name = "lblCurrentDeadline";
            //
            // txtCurrentDeadline
            //
            txtCurrentDeadline.Text = "—";
            txtCurrentDeadline.Location = new System.Drawing.Point(119, 47);
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
            txtCurrentDeadline.TabIndex = 3;
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
            // lblRequestedDeadline
            //
            lblRequestedDeadline.Text = "요청 마감일";
            lblRequestedDeadline.Location = new System.Drawing.Point(0, 92);
            lblRequestedDeadline.Size = new System.Drawing.Size(108, 36);
            lblRequestedDeadline.MinimumSize = new System.Drawing.Size(0, 0);
            lblRequestedDeadline.MaximumSize = new System.Drawing.Size(0, 0);
            lblRequestedDeadline.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblRequestedDeadline.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblRequestedDeadline.Dock = System.Windows.Forms.DockStyle.Fill;
            lblRequestedDeadline.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblRequestedDeadline.AutoSize = false;
            lblRequestedDeadline.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblRequestedDeadline.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblRequestedDeadline.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblRequestedDeadline.TabIndex = 4;
            lblRequestedDeadline.TabStop = false;
            lblRequestedDeadline.AutoEllipsis = false;
            lblRequestedDeadline.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblRequestedDeadline.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblRequestedDeadline.UseCompatibleTextRendering = false;
            lblRequestedDeadline.Name = "lblRequestedDeadline";
            //
            // dtpRequestedDeadline
            //
            dtpRequestedDeadline.Location = new System.Drawing.Point(116, 92);
            dtpRequestedDeadline.Size = new System.Drawing.Size(603, 23);
            dtpRequestedDeadline.MinimumSize = new System.Drawing.Size(0, 0);
            dtpRequestedDeadline.MaximumSize = new System.Drawing.Size(0, 0);
            dtpRequestedDeadline.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            dtpRequestedDeadline.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            dtpRequestedDeadline.Dock = System.Windows.Forms.DockStyle.Fill;
            dtpRequestedDeadline.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            dtpRequestedDeadline.AutoSize = false;
            dtpRequestedDeadline.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            dtpRequestedDeadline.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            dtpRequestedDeadline.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            dtpRequestedDeadline.TabIndex = 5;
            dtpRequestedDeadline.TabStop = true;
            dtpRequestedDeadline.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            dtpRequestedDeadline.CustomFormat = " yyyy-MM-dd";
            dtpRequestedDeadline.ShowCheckBox = true;
            dtpRequestedDeadline.Checked = false;
            dtpRequestedDeadline.MinDate = new System.DateTime(552877920000000000L);
            dtpRequestedDeadline.MaxDate = new System.DateTime(3155062752000000000L);
            dtpRequestedDeadline.Name = "dtpRequestedDeadline";
            dtpRequestedDeadline.ValueChanged += new System.EventHandler(InputChanged);
            //
            // lblExtensionReason
            //
            lblExtensionReason.Text = "연장 사유";
            lblExtensionReason.Location = new System.Drawing.Point(0, 136);
            lblExtensionReason.Size = new System.Drawing.Size(108, 104);
            lblExtensionReason.MinimumSize = new System.Drawing.Size(0, 0);
            lblExtensionReason.MaximumSize = new System.Drawing.Size(0, 0);
            lblExtensionReason.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblExtensionReason.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblExtensionReason.Dock = System.Windows.Forms.DockStyle.Fill;
            lblExtensionReason.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblExtensionReason.AutoSize = false;
            lblExtensionReason.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblExtensionReason.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblExtensionReason.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblExtensionReason.TabIndex = 6;
            lblExtensionReason.TabStop = false;
            lblExtensionReason.AutoEllipsis = false;
            lblExtensionReason.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblExtensionReason.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblExtensionReason.UseCompatibleTextRendering = false;
            lblExtensionReason.Name = "lblExtensionReason";
            //
            // txtExtensionReason
            //
            txtExtensionReason.Text = "";
            txtExtensionReason.Location = new System.Drawing.Point(116, 136);
            txtExtensionReason.Size = new System.Drawing.Size(603, 102);
            txtExtensionReason.MinimumSize = new System.Drawing.Size(0, 90);
            txtExtensionReason.MaximumSize = new System.Drawing.Size(0, 0);
            txtExtensionReason.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            txtExtensionReason.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtExtensionReason.Dock = System.Windows.Forms.DockStyle.Fill;
            txtExtensionReason.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtExtensionReason.AutoSize = true;
            txtExtensionReason.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtExtensionReason.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            txtExtensionReason.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtExtensionReason.TabIndex = 7;
            txtExtensionReason.TabStop = true;
            txtExtensionReason.Multiline = true;
            txtExtensionReason.ReadOnly = false;
            txtExtensionReason.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtExtensionReason.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtExtensionReason.UseSystemPasswordChar = false;
            txtExtensionReason.WordWrap = true;
            txtExtensionReason.MaxLength = 32767;
            txtExtensionReason.AcceptsReturn = false;
            txtExtensionReason.AcceptsTab = false;
            txtExtensionReason.Name = "txtExtensionReason";
            txtExtensionReason.TextChanged += new System.EventHandler(InputChanged);
            //
            // lblExtensionNotice
            //
            lblExtensionNotice.Text = "변경 반영 안내";
            lblExtensionNotice.Location = new System.Drawing.Point(0, 248);
            lblExtensionNotice.Size = new System.Drawing.Size(108, 48);
            lblExtensionNotice.MinimumSize = new System.Drawing.Size(0, 0);
            lblExtensionNotice.MaximumSize = new System.Drawing.Size(0, 0);
            lblExtensionNotice.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblExtensionNotice.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblExtensionNotice.Dock = System.Windows.Forms.DockStyle.Fill;
            lblExtensionNotice.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblExtensionNotice.AutoSize = false;
            lblExtensionNotice.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblExtensionNotice.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblExtensionNotice.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblExtensionNotice.TabIndex = 8;
            lblExtensionNotice.TabStop = false;
            lblExtensionNotice.AutoEllipsis = false;
            lblExtensionNotice.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblExtensionNotice.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblExtensionNotice.UseCompatibleTextRendering = false;
            lblExtensionNotice.Name = "lblExtensionNotice";
            //
            // txtExtensionNotice
            //
            txtExtensionNotice.Text = "현재 마감일: —\r\n희망 마감일: 선택 필요";
            txtExtensionNotice.Location = new System.Drawing.Point(119, 247);
            txtExtensionNotice.Size = new System.Drawing.Size(597, 50);
            txtExtensionNotice.MinimumSize = new System.Drawing.Size(0, 46);
            txtExtensionNotice.MaximumSize = new System.Drawing.Size(0, 0);
            txtExtensionNotice.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            txtExtensionNotice.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtExtensionNotice.Dock = System.Windows.Forms.DockStyle.Fill;
            txtExtensionNotice.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtExtensionNotice.AutoSize = true;
            txtExtensionNotice.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtExtensionNotice.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            txtExtensionNotice.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtExtensionNotice.TabIndex = 9;
            txtExtensionNotice.TabStop = false;
            txtExtensionNotice.Multiline = true;
            txtExtensionNotice.ReadOnly = true;
            txtExtensionNotice.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtExtensionNotice.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtExtensionNotice.UseSystemPasswordChar = false;
            txtExtensionNotice.WordWrap = true;
            txtExtensionNotice.MaxLength = 32767;
            txtExtensionNotice.AcceptsReturn = false;
            txtExtensionNotice.AcceptsTab = false;
            txtExtensionNotice.Name = "txtExtensionNotice";
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
            btnConfirm.Text = "입력 확인";
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
            btnConfirm.Click += new System.EventHandler(ConfirmInput_Click);
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
            // DeadlineExtensionForm
            //
            Text = "마감 연장 요청";
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
            Name = "DeadlineExtensionForm";
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
            ((System.ComponentModel.ISupportInitialize)inputErrors).EndInit();
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
        private System.Windows.Forms.Label lblCurrentDeadline;
        private System.Windows.Forms.TextBox txtCurrentDeadline;
        private System.Windows.Forms.Label lblRequestedDeadline;
        private System.Windows.Forms.DateTimePicker dtpRequestedDeadline;
        private System.Windows.Forms.Label lblExtensionReason;
        private System.Windows.Forms.TextBox txtExtensionReason;
        private System.Windows.Forms.Label lblExtensionNotice;
        private System.Windows.Forms.TextBox txtExtensionNotice;
        private System.Windows.Forms.FlowLayoutPanel flowActions;
        private System.Windows.Forms.Button btnConfirm;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Label lblPreparationStatus;
        private System.Windows.Forms.ErrorProvider inputErrors;
    }
}
