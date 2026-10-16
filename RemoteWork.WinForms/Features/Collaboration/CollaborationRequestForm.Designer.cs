#nullable disable

namespace RemoteWork.WinForms.Features.Collaboration
{
    partial class CollaborationRequestForm
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
            layoutContent3 = new System.Windows.Forms.TableLayoutPanel();
            panelContent2 = new System.Windows.Forms.Panel();
            layoutContent4 = new System.Windows.Forms.TableLayoutPanel();
            lblText2 = new System.Windows.Forms.Label();
            cmbOptions = new System.Windows.Forms.ComboBox();
            lblText3 = new System.Windows.Forms.Label();
            cmbOptions2 = new System.Windows.Forms.ComboBox();
            lblText4 = new System.Windows.Forms.Label();
            txtContent = new System.Windows.Forms.TextBox();
            lblCollaborationReason = new System.Windows.Forms.Label();
            txtCollaborationReason = new System.Windows.Forms.TextBox();
            lblText6 = new System.Windows.Forms.Label();
            dtpDate = new System.Windows.Forms.DateTimePicker();
            flowActions = new System.Windows.Forms.FlowLayoutPanel();
            btnSendRequest = new System.Windows.Forms.Button();
            btnClose = new System.Windows.Forms.Button();
            layoutContent.SuspendLayout();
            layoutContent2.SuspendLayout();
            panelContent.SuspendLayout();
            layoutContent3.SuspendLayout();
            panelContent2.SuspendLayout();
            layoutContent4.SuspendLayout();
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
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
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
            layoutContent2.Size = new System.Drawing.Size(736, 44);
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
            layoutContent2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent2.Controls.Add(lblPageTitle, 0, 0);
            //
            // lblPageTitle
            //
            lblPageTitle.Text = "협업 요청";
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
            panelContent.Controls.Add(layoutContent3);
            //
            // layoutContent3
            //
            layoutContent3.Text = "";
            layoutContent3.Location = new System.Drawing.Point(0, 0);
            layoutContent3.Size = new System.Drawing.Size(719, 440);
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
            layoutContent3.AutoScroll = true;
            layoutContent3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent3.ColumnCount = 1;
            layoutContent3.RowCount = 1;
            layoutContent3.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent3.Name = "layoutContent3";
            layoutContent3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.Controls.Add(panelContent2, 0, 0);
            //
            // panelContent2
            //
            panelContent2.Text = "";
            panelContent2.Location = new System.Drawing.Point(0, 0);
            panelContent2.Size = new System.Drawing.Size(719, 413);
            panelContent2.MinimumSize = new System.Drawing.Size(0, 0);
            panelContent2.MaximumSize = new System.Drawing.Size(0, 0);
            panelContent2.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            panelContent2.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            panelContent2.Dock = System.Windows.Forms.DockStyle.Fill;
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
            panelContent2.Controls.Add(layoutContent4);
            //
            // layoutContent4
            //
            layoutContent4.Text = "";
            layoutContent4.Location = new System.Drawing.Point(0, 0);
            layoutContent4.Size = new System.Drawing.Size(719, 288);
            layoutContent4.MinimumSize = new System.Drawing.Size(0, 0);
            layoutContent4.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent4.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent4.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent4.Dock = System.Windows.Forms.DockStyle.Top;
            layoutContent4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            layoutContent4.AutoSize = true;
            layoutContent4.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            layoutContent4.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            layoutContent4.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            layoutContent4.TabIndex = 0;
            layoutContent4.TabStop = false;
            layoutContent4.AutoScroll = false;
            layoutContent4.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            layoutContent4.ColumnCount = 2;
            layoutContent4.RowCount = 5;
            layoutContent4.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent4.Name = "layoutContent4";
            layoutContent4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 116F));
            layoutContent4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 112F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent4.Controls.Add(lblText2, 0, 0);
            layoutContent4.Controls.Add(cmbOptions, 1, 0);
            layoutContent4.Controls.Add(lblText3, 0, 1);
            layoutContent4.Controls.Add(cmbOptions2, 1, 1);
            layoutContent4.Controls.Add(lblText4, 0, 2);
            layoutContent4.Controls.Add(txtContent, 1, 2);
            layoutContent4.Controls.Add(lblCollaborationReason, 0, 3);
            layoutContent4.Controls.Add(txtCollaborationReason, 1, 3);
            layoutContent4.Controls.Add(lblText6, 0, 4);
            layoutContent4.Controls.Add(dtpDate, 1, 4);
            //
            // lblText2
            //
            lblText2.Text = "요청할 업무";
            lblText2.Location = new System.Drawing.Point(0, 4);
            lblText2.Size = new System.Drawing.Size(108, 36);
            lblText2.MinimumSize = new System.Drawing.Size(0, 0);
            lblText2.MaximumSize = new System.Drawing.Size(0, 0);
            lblText2.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblText2.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblText2.Dock = System.Windows.Forms.DockStyle.Fill;
            lblText2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblText2.AutoSize = false;
            lblText2.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblText2.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblText2.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblText2.TabIndex = 0;
            lblText2.TabStop = false;
            lblText2.AutoEllipsis = false;
            lblText2.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblText2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblText2.UseCompatibleTextRendering = false;
            lblText2.Name = "lblText2";
            //
            // cmbOptions
            //
            cmbOptions.Text = "화면 구성 검토";
            cmbOptions.Location = new System.Drawing.Point(116, 4);
            cmbOptions.Size = new System.Drawing.Size(603, 26);
            cmbOptions.MinimumSize = new System.Drawing.Size(0, 0);
            cmbOptions.MaximumSize = new System.Drawing.Size(0, 0);
            cmbOptions.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            cmbOptions.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            cmbOptions.Dock = System.Windows.Forms.DockStyle.Fill;
            cmbOptions.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            cmbOptions.AutoSize = false;
            cmbOptions.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            cmbOptions.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            cmbOptions.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            cmbOptions.TabIndex = 1;
            cmbOptions.TabStop = true;
            cmbOptions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbOptions.IntegralHeight = true;
            cmbOptions.MaxDropDownItems = 8;
            cmbOptions.Name = "cmbOptions";
            cmbOptions.Items.AddRange(new object[] { "화면 구성 검토", "주간 보고서 작성" });
            //
            // lblText3
            //
            lblText3.Text = "요청 대상";
            lblText3.Location = new System.Drawing.Point(0, 48);
            lblText3.Size = new System.Drawing.Size(108, 36);
            lblText3.MinimumSize = new System.Drawing.Size(0, 0);
            lblText3.MaximumSize = new System.Drawing.Size(0, 0);
            lblText3.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblText3.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblText3.Dock = System.Windows.Forms.DockStyle.Fill;
            lblText3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblText3.AutoSize = false;
            lblText3.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblText3.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblText3.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblText3.TabIndex = 2;
            lblText3.TabStop = false;
            lblText3.AutoEllipsis = false;
            lblText3.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblText3.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblText3.UseCompatibleTextRendering = false;
            lblText3.Name = "lblText3";
            //
            // cmbOptions2
            //
            cmbOptions2.Text = "김민수 · 개발팀";
            cmbOptions2.Location = new System.Drawing.Point(116, 48);
            cmbOptions2.Size = new System.Drawing.Size(603, 26);
            cmbOptions2.MinimumSize = new System.Drawing.Size(0, 0);
            cmbOptions2.MaximumSize = new System.Drawing.Size(0, 0);
            cmbOptions2.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            cmbOptions2.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            cmbOptions2.Dock = System.Windows.Forms.DockStyle.Fill;
            cmbOptions2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            cmbOptions2.AutoSize = false;
            cmbOptions2.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            cmbOptions2.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            cmbOptions2.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            cmbOptions2.TabIndex = 3;
            cmbOptions2.TabStop = true;
            cmbOptions2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbOptions2.IntegralHeight = true;
            cmbOptions2.MaxDropDownItems = 8;
            cmbOptions2.Name = "cmbOptions2";
            cmbOptions2.Items.AddRange(new object[] { "김민수 · 개발팀", "박지우 · 디자인팀", "기획팀" });
            //
            // lblText4
            //
            lblText4.Text = "요청 제목";
            lblText4.Location = new System.Drawing.Point(0, 92);
            lblText4.Size = new System.Drawing.Size(108, 36);
            lblText4.MinimumSize = new System.Drawing.Size(0, 0);
            lblText4.MaximumSize = new System.Drawing.Size(0, 0);
            lblText4.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblText4.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblText4.Dock = System.Windows.Forms.DockStyle.Fill;
            lblText4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblText4.AutoSize = false;
            lblText4.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblText4.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblText4.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblText4.TabIndex = 4;
            lblText4.TabStop = false;
            lblText4.AutoEllipsis = false;
            lblText4.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblText4.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblText4.UseCompatibleTextRendering = false;
            lblText4.Name = "lblText4";
            //
            // txtContent
            //
            txtContent.Text = "";
            txtContent.Location = new System.Drawing.Point(116, 92);
            txtContent.Size = new System.Drawing.Size(603, 28);
            txtContent.MinimumSize = new System.Drawing.Size(0, 28);
            txtContent.MaximumSize = new System.Drawing.Size(0, 0);
            txtContent.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            txtContent.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtContent.Dock = System.Windows.Forms.DockStyle.Fill;
            txtContent.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtContent.AutoSize = true;
            txtContent.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtContent.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            txtContent.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtContent.TabIndex = 5;
            txtContent.TabStop = true;
            txtContent.Multiline = false;
            txtContent.ReadOnly = false;
            txtContent.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtContent.ScrollBars = System.Windows.Forms.ScrollBars.None;
            txtContent.UseSystemPasswordChar = false;
            txtContent.WordWrap = true;
            txtContent.MaxLength = 32767;
            txtContent.AcceptsReturn = false;
            txtContent.AcceptsTab = false;
            txtContent.Name = "txtContent";
            //
            // lblCollaborationReason
            //
            lblCollaborationReason.Text = "요청 내용";
            lblCollaborationReason.Location = new System.Drawing.Point(0, 136);
            lblCollaborationReason.Size = new System.Drawing.Size(108, 104);
            lblCollaborationReason.MinimumSize = new System.Drawing.Size(0, 0);
            lblCollaborationReason.MaximumSize = new System.Drawing.Size(0, 0);
            lblCollaborationReason.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblCollaborationReason.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblCollaborationReason.Dock = System.Windows.Forms.DockStyle.Fill;
            lblCollaborationReason.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblCollaborationReason.AutoSize = false;
            lblCollaborationReason.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblCollaborationReason.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblCollaborationReason.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblCollaborationReason.TabIndex = 6;
            lblCollaborationReason.TabStop = false;
            lblCollaborationReason.AutoEllipsis = false;
            lblCollaborationReason.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblCollaborationReason.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblCollaborationReason.UseCompatibleTextRendering = false;
            lblCollaborationReason.Name = "lblCollaborationReason";
            //
            // txtCollaborationReason
            //
            txtCollaborationReason.Text = "";
            txtCollaborationReason.Location = new System.Drawing.Point(116, 136);
            txtCollaborationReason.Size = new System.Drawing.Size(603, 102);
            txtCollaborationReason.MinimumSize = new System.Drawing.Size(0, 90);
            txtCollaborationReason.MaximumSize = new System.Drawing.Size(0, 0);
            txtCollaborationReason.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            txtCollaborationReason.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtCollaborationReason.Dock = System.Windows.Forms.DockStyle.Fill;
            txtCollaborationReason.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtCollaborationReason.AutoSize = true;
            txtCollaborationReason.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtCollaborationReason.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            txtCollaborationReason.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtCollaborationReason.TabIndex = 7;
            txtCollaborationReason.TabStop = true;
            txtCollaborationReason.Multiline = true;
            txtCollaborationReason.ReadOnly = false;
            txtCollaborationReason.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtCollaborationReason.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtCollaborationReason.UseSystemPasswordChar = false;
            txtCollaborationReason.WordWrap = true;
            txtCollaborationReason.MaxLength = 32767;
            txtCollaborationReason.AcceptsReturn = false;
            txtCollaborationReason.AcceptsTab = false;
            txtCollaborationReason.Name = "txtCollaborationReason";
            //
            // lblText6
            //
            lblText6.Text = "희망 기한";
            lblText6.Location = new System.Drawing.Point(0, 248);
            lblText6.Size = new System.Drawing.Size(108, 36);
            lblText6.MinimumSize = new System.Drawing.Size(0, 0);
            lblText6.MaximumSize = new System.Drawing.Size(0, 0);
            lblText6.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblText6.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblText6.Dock = System.Windows.Forms.DockStyle.Fill;
            lblText6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblText6.AutoSize = false;
            lblText6.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblText6.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblText6.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblText6.TabIndex = 8;
            lblText6.TabStop = false;
            lblText6.AutoEllipsis = false;
            lblText6.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblText6.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblText6.UseCompatibleTextRendering = false;
            lblText6.Name = "lblText6";
            //
            // dtpDate
            //
            dtpDate.Text = " 2026-10-14";
            dtpDate.Location = new System.Drawing.Point(119, 247);
            dtpDate.Size = new System.Drawing.Size(597, 23);
            dtpDate.MinimumSize = new System.Drawing.Size(0, 0);
            dtpDate.MaximumSize = new System.Drawing.Size(0, 0);
            dtpDate.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            dtpDate.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            dtpDate.Dock = System.Windows.Forms.DockStyle.Fill;
            dtpDate.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            dtpDate.AutoSize = false;
            dtpDate.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            dtpDate.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            dtpDate.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            dtpDate.TabIndex = 9;
            dtpDate.TabStop = true;
            dtpDate.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            dtpDate.CustomFormat = " yyyy-MM-dd";
            dtpDate.Value = new System.DateTime(639275707366141137L);
            dtpDate.MinDate = new System.DateTime(552877920000000000L);
            dtpDate.MaxDate = new System.DateTime(3155062752000000000L);
            dtpDate.Name = "dtpDate";
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
            flowActions.Controls.Add(btnSendRequest);
            flowActions.Controls.Add(btnClose);
            //
            // btnSendRequest
            //
            btnSendRequest.Text = "요청 보내기";
            btnSendRequest.Location = new System.Drawing.Point(621, 10);
            btnSendRequest.Size = new System.Drawing.Size(107, 35);
            btnSendRequest.MinimumSize = new System.Drawing.Size(88, 34);
            btnSendRequest.MaximumSize = new System.Drawing.Size(0, 0);
            btnSendRequest.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnSendRequest.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnSendRequest.Dock = System.Windows.Forms.DockStyle.None;
            btnSendRequest.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnSendRequest.AutoSize = true;
            btnSendRequest.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnSendRequest.ForeColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnSendRequest.BackColor = System.Drawing.Color.FromArgb(255, 52, 101, 224);
            btnSendRequest.TabIndex = 0;
            btnSendRequest.TabStop = true;
            btnSendRequest.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnSendRequest.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnSendRequest.UseVisualStyleBackColor = false;
            btnSendRequest.DialogResult = System.Windows.Forms.DialogResult.None;
            btnSendRequest.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnSendRequest.Cursor = System.Windows.Forms.Cursors.Hand;
            btnSendRequest.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 52, 101, 224);
            btnSendRequest.FlatAppearance.BorderSize = 1;
            btnSendRequest.Name = "btnSendRequest";
            btnSendRequest.Click += new System.EventHandler(Unavailable_Click);
            //
            // btnClose
            //
            btnClose.Text = "닫기";
            btnClose.Location = new System.Drawing.Point(525, 10);
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
            // CollaborationRequestForm
            //
            Text = "협업 요청";
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
            Name = "CollaborationRequestForm";
            Controls.Add(layoutContent);
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            CancelButton = btnClose;
            flowActions.ResumeLayout(false);
            flowActions.PerformLayout();
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
        private System.Windows.Forms.Panel panelContent;
        private System.Windows.Forms.TableLayoutPanel layoutContent3;
        private System.Windows.Forms.Panel panelContent2;
        private System.Windows.Forms.TableLayoutPanel layoutContent4;
        private System.Windows.Forms.Label lblText2;
        private System.Windows.Forms.ComboBox cmbOptions;
        private System.Windows.Forms.Label lblText3;
        private System.Windows.Forms.ComboBox cmbOptions2;
        private System.Windows.Forms.Label lblText4;
        private System.Windows.Forms.TextBox txtContent;
        private System.Windows.Forms.Label lblCollaborationReason;
        private System.Windows.Forms.TextBox txtCollaborationReason;
        private System.Windows.Forms.Label lblText6;
        private System.Windows.Forms.DateTimePicker dtpDate;
        private System.Windows.Forms.FlowLayoutPanel flowActions;
        private System.Windows.Forms.Button btnSendRequest;
        private System.Windows.Forms.Button btnClose;
    }
}
