#nullable disable

namespace RemoteWork.WinForms.Features.Tasks
{
    partial class TaskEditForm
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
            lblTaskTitle = new System.Windows.Forms.Label();
            txtTaskTitle = new System.Windows.Forms.TextBox();
            lblTaskDescription = new System.Windows.Forms.Label();
            txtTaskDescription = new System.Windows.Forms.TextBox();
            lblAssignee = new System.Windows.Forms.Label();
            cmbAssignee = new System.Windows.Forms.ComboBox();
            lblStartDate = new System.Windows.Forms.Label();
            dtpStartDate = new System.Windows.Forms.DateTimePicker();
            lblDeadline = new System.Windows.Forms.Label();
            dtpDeadline = new System.Windows.Forms.DateTimePicker();
            lblAttachments = new System.Windows.Forms.Label();
            txtAttachments = new System.Windows.Forms.TextBox();
            flowActions = new System.Windows.Forms.FlowLayoutPanel();
            btnSelectAttachment = new System.Windows.Forms.Button();
            flowActions2 = new System.Windows.Forms.FlowLayoutPanel();
            btnConfirm = new System.Windows.Forms.Button();
            btnCancel = new System.Windows.Forms.Button();
            btnClose = new System.Windows.Forms.Button();
            layoutContent.SuspendLayout();
            layoutContent2.SuspendLayout();
            panelContent.SuspendLayout();
            layoutContent3.SuspendLayout();
            panelContent2.SuspendLayout();
            layoutContent4.SuspendLayout();
            flowActions.SuspendLayout();
            flowActions2.SuspendLayout();
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
            layoutContent.Controls.Add(flowActions2, 0, 2);
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
            lblPageTitle.Text = "업무 등록";
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
            layoutContent3.AutoScroll = false;
            layoutContent3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent3.ColumnCount = 1;
            layoutContent3.RowCount = 2;
            layoutContent3.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent3.Name = "layoutContent3";
            layoutContent3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize, 0F));
            layoutContent3.Controls.Add(panelContent2, 0, 0);
            layoutContent3.Controls.Add(flowActions, 0, 1);
            //
            // panelContent2
            //
            panelContent2.Text = "";
            panelContent2.Location = new System.Drawing.Point(0, 0);
            panelContent2.Size = new System.Drawing.Size(719, 391);
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
            layoutContent4.Size = new System.Drawing.Size(719, 388);
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
            layoutContent4.RowCount = 6;
            layoutContent4.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent4.Name = "layoutContent4";
            layoutContent4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 116F));
            layoutContent4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 112F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            layoutContent4.Controls.Add(lblTaskTitle, 0, 0);
            layoutContent4.Controls.Add(txtTaskTitle, 1, 0);
            layoutContent4.Controls.Add(lblTaskDescription, 0, 1);
            layoutContent4.Controls.Add(txtTaskDescription, 1, 1);
            layoutContent4.Controls.Add(lblAssignee, 0, 2);
            layoutContent4.Controls.Add(cmbAssignee, 1, 2);
            layoutContent4.Controls.Add(lblStartDate, 0, 3);
            layoutContent4.Controls.Add(dtpStartDate, 1, 3);
            layoutContent4.Controls.Add(lblDeadline, 0, 4);
            layoutContent4.Controls.Add(dtpDeadline, 1, 4);
            layoutContent4.Controls.Add(lblAttachments, 0, 5);
            layoutContent4.Controls.Add(txtAttachments, 1, 5);
            //
            // lblTaskTitle
            //
            lblTaskTitle.Text = "업무 제목";
            lblTaskTitle.Location = new System.Drawing.Point(0, 4);
            lblTaskTitle.Size = new System.Drawing.Size(108, 36);
            lblTaskTitle.MinimumSize = new System.Drawing.Size(0, 0);
            lblTaskTitle.MaximumSize = new System.Drawing.Size(0, 0);
            lblTaskTitle.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblTaskTitle.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblTaskTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            lblTaskTitle.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblTaskTitle.AutoSize = false;
            lblTaskTitle.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblTaskTitle.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblTaskTitle.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblTaskTitle.TabIndex = 0;
            lblTaskTitle.TabStop = false;
            lblTaskTitle.AutoEllipsis = false;
            lblTaskTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblTaskTitle.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblTaskTitle.UseCompatibleTextRendering = false;
            lblTaskTitle.Name = "lblTaskTitle";
            //
            // txtTaskTitle
            //
            txtTaskTitle.Text = "";
            txtTaskTitle.Location = new System.Drawing.Point(116, 4);
            txtTaskTitle.Size = new System.Drawing.Size(603, 28);
            txtTaskTitle.MinimumSize = new System.Drawing.Size(0, 28);
            txtTaskTitle.MaximumSize = new System.Drawing.Size(0, 0);
            txtTaskTitle.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            txtTaskTitle.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtTaskTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            txtTaskTitle.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtTaskTitle.AutoSize = true;
            txtTaskTitle.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtTaskTitle.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            txtTaskTitle.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtTaskTitle.TabIndex = 1;
            txtTaskTitle.TabStop = true;
            txtTaskTitle.Multiline = false;
            txtTaskTitle.ReadOnly = false;
            txtTaskTitle.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtTaskTitle.ScrollBars = System.Windows.Forms.ScrollBars.None;
            txtTaskTitle.UseSystemPasswordChar = false;
            txtTaskTitle.WordWrap = true;
            txtTaskTitle.MaxLength = 32767;
            txtTaskTitle.AcceptsReturn = false;
            txtTaskTitle.AcceptsTab = false;
            txtTaskTitle.Name = "txtTaskTitle";
            //
            // lblTaskDescription
            //
            lblTaskDescription.Text = "상세 내용";
            lblTaskDescription.Location = new System.Drawing.Point(0, 48);
            lblTaskDescription.Size = new System.Drawing.Size(108, 104);
            lblTaskDescription.MinimumSize = new System.Drawing.Size(0, 0);
            lblTaskDescription.MaximumSize = new System.Drawing.Size(0, 0);
            lblTaskDescription.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblTaskDescription.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblTaskDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            lblTaskDescription.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblTaskDescription.AutoSize = false;
            lblTaskDescription.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblTaskDescription.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblTaskDescription.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblTaskDescription.TabIndex = 2;
            lblTaskDescription.TabStop = false;
            lblTaskDescription.AutoEllipsis = false;
            lblTaskDescription.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblTaskDescription.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblTaskDescription.UseCompatibleTextRendering = false;
            lblTaskDescription.Name = "lblTaskDescription";
            //
            // txtTaskDescription
            //
            txtTaskDescription.Text = "";
            txtTaskDescription.Location = new System.Drawing.Point(116, 48);
            txtTaskDescription.Size = new System.Drawing.Size(603, 102);
            txtTaskDescription.MinimumSize = new System.Drawing.Size(0, 90);
            txtTaskDescription.MaximumSize = new System.Drawing.Size(0, 0);
            txtTaskDescription.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            txtTaskDescription.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtTaskDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            txtTaskDescription.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtTaskDescription.AutoSize = true;
            txtTaskDescription.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtTaskDescription.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            txtTaskDescription.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtTaskDescription.TabIndex = 3;
            txtTaskDescription.TabStop = true;
            txtTaskDescription.Multiline = true;
            txtTaskDescription.ReadOnly = false;
            txtTaskDescription.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtTaskDescription.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtTaskDescription.UseSystemPasswordChar = false;
            txtTaskDescription.WordWrap = true;
            txtTaskDescription.MaxLength = 32767;
            txtTaskDescription.AcceptsReturn = false;
            txtTaskDescription.AcceptsTab = false;
            txtTaskDescription.Name = "txtTaskDescription";
            //
            // lblAssignee
            //
            lblAssignee.Text = "담당자";
            lblAssignee.Location = new System.Drawing.Point(0, 160);
            lblAssignee.Size = new System.Drawing.Size(108, 36);
            lblAssignee.MinimumSize = new System.Drawing.Size(0, 0);
            lblAssignee.MaximumSize = new System.Drawing.Size(0, 0);
            lblAssignee.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblAssignee.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblAssignee.Dock = System.Windows.Forms.DockStyle.Fill;
            lblAssignee.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblAssignee.AutoSize = false;
            lblAssignee.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblAssignee.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblAssignee.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblAssignee.TabIndex = 4;
            lblAssignee.TabStop = false;
            lblAssignee.AutoEllipsis = false;
            lblAssignee.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblAssignee.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblAssignee.UseCompatibleTextRendering = false;
            lblAssignee.Name = "lblAssignee";
            //
            // cmbAssignee
            //
            cmbAssignee.Text = "담당자 선택";
            cmbAssignee.Location = new System.Drawing.Point(116, 160);
            cmbAssignee.Size = new System.Drawing.Size(603, 26);
            cmbAssignee.MinimumSize = new System.Drawing.Size(0, 0);
            cmbAssignee.MaximumSize = new System.Drawing.Size(0, 0);
            cmbAssignee.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            cmbAssignee.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            cmbAssignee.Dock = System.Windows.Forms.DockStyle.Fill;
            cmbAssignee.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            cmbAssignee.AutoSize = false;
            cmbAssignee.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            cmbAssignee.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            cmbAssignee.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            cmbAssignee.TabIndex = 5;
            cmbAssignee.TabStop = true;
            cmbAssignee.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbAssignee.IntegralHeight = true;
            cmbAssignee.MaxDropDownItems = 8;
            cmbAssignee.Name = "cmbAssignee";
            cmbAssignee.Items.AddRange(new object[] { "담당자 선택", "김직원", "이직원", "박직원", "이관리자" });
            //
            // lblStartDate
            //
            lblStartDate.Text = "시작일";
            lblStartDate.Location = new System.Drawing.Point(0, 248);
            lblStartDate.Size = new System.Drawing.Size(108, 36);
            lblStartDate.MinimumSize = new System.Drawing.Size(0, 0);
            lblStartDate.MaximumSize = new System.Drawing.Size(0, 0);
            lblStartDate.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblStartDate.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblStartDate.Dock = System.Windows.Forms.DockStyle.Fill;
            lblStartDate.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblStartDate.AutoSize = false;
            lblStartDate.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblStartDate.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblStartDate.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblStartDate.TabIndex = 8;
            lblStartDate.TabStop = false;
            lblStartDate.AutoEllipsis = false;
            lblStartDate.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblStartDate.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblStartDate.UseCompatibleTextRendering = false;
            lblStartDate.Name = "lblStartDate";
            //
            // dtpStartDate
            //
            dtpStartDate.Text = " 2026-10-15";
            dtpStartDate.Location = new System.Drawing.Point(116, 248);
            dtpStartDate.Size = new System.Drawing.Size(603, 23);
            dtpStartDate.MinimumSize = new System.Drawing.Size(0, 0);
            dtpStartDate.MaximumSize = new System.Drawing.Size(0, 0);
            dtpStartDate.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            dtpStartDate.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            dtpStartDate.Dock = System.Windows.Forms.DockStyle.Fill;
            dtpStartDate.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            dtpStartDate.AutoSize = false;
            dtpStartDate.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            dtpStartDate.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            dtpStartDate.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            dtpStartDate.TabIndex = 9;
            dtpStartDate.TabStop = true;
            dtpStartDate.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            dtpStartDate.CustomFormat = " yyyy-MM-dd";
            dtpStartDate.Value = new System.DateTime(639276192000000000L);
            dtpStartDate.MinDate = new System.DateTime(552877920000000000L);
            dtpStartDate.MaxDate = new System.DateTime(3155062752000000000L);
            dtpStartDate.Name = "dtpStartDate";
            //
            // lblDeadline
            //
            lblDeadline.Text = "마감일";
            lblDeadline.Location = new System.Drawing.Point(0, 292);
            lblDeadline.Size = new System.Drawing.Size(108, 36);
            lblDeadline.MinimumSize = new System.Drawing.Size(0, 0);
            lblDeadline.MaximumSize = new System.Drawing.Size(0, 0);
            lblDeadline.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblDeadline.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblDeadline.Dock = System.Windows.Forms.DockStyle.Fill;
            lblDeadline.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblDeadline.AutoSize = false;
            lblDeadline.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblDeadline.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblDeadline.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblDeadline.TabIndex = 10;
            lblDeadline.TabStop = false;
            lblDeadline.AutoEllipsis = false;
            lblDeadline.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblDeadline.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblDeadline.UseCompatibleTextRendering = false;
            lblDeadline.Name = "lblDeadline";
            //
            // dtpDeadline
            //
            dtpDeadline.Text = " 2026-10-22";
            dtpDeadline.Location = new System.Drawing.Point(116, 292);
            dtpDeadline.Size = new System.Drawing.Size(603, 23);
            dtpDeadline.MinimumSize = new System.Drawing.Size(0, 0);
            dtpDeadline.MaximumSize = new System.Drawing.Size(0, 0);
            dtpDeadline.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            dtpDeadline.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            dtpDeadline.Dock = System.Windows.Forms.DockStyle.Fill;
            dtpDeadline.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            dtpDeadline.AutoSize = false;
            dtpDeadline.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            dtpDeadline.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            dtpDeadline.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            dtpDeadline.TabIndex = 11;
            dtpDeadline.TabStop = true;
            dtpDeadline.Format = System.Windows.Forms.DateTimePickerFormat.Custom;
            dtpDeadline.CustomFormat = " yyyy-MM-dd";
            dtpDeadline.Value = new System.DateTime(639282240000000000L);
            dtpDeadline.MinDate = new System.DateTime(552877920000000000L);
            dtpDeadline.MaxDate = new System.DateTime(3155062752000000000L);
            dtpDeadline.Name = "dtpDeadline";
            //
            // lblAttachments
            //
            lblAttachments.Text = "첨부파일";
            lblAttachments.Location = new System.Drawing.Point(0, 336);
            lblAttachments.Size = new System.Drawing.Size(108, 48);
            lblAttachments.MinimumSize = new System.Drawing.Size(0, 0);
            lblAttachments.MaximumSize = new System.Drawing.Size(0, 0);
            lblAttachments.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblAttachments.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblAttachments.Dock = System.Windows.Forms.DockStyle.Fill;
            lblAttachments.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblAttachments.AutoSize = false;
            lblAttachments.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblAttachments.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblAttachments.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblAttachments.TabIndex = 12;
            lblAttachments.TabStop = false;
            lblAttachments.AutoEllipsis = false;
            lblAttachments.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblAttachments.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblAttachments.UseCompatibleTextRendering = false;
            lblAttachments.Name = "lblAttachments";
            //
            // txtAttachments
            //
            txtAttachments.Text = "등록된 첨부파일이 없습니다.";
            txtAttachments.Location = new System.Drawing.Point(119, 335);
            txtAttachments.Size = new System.Drawing.Size(597, 50);
            txtAttachments.MinimumSize = new System.Drawing.Size(0, 46);
            txtAttachments.MaximumSize = new System.Drawing.Size(0, 0);
            txtAttachments.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            txtAttachments.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtAttachments.Dock = System.Windows.Forms.DockStyle.Fill;
            txtAttachments.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtAttachments.AutoSize = true;
            txtAttachments.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtAttachments.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            txtAttachments.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtAttachments.TabIndex = 13;
            txtAttachments.TabStop = false;
            txtAttachments.Multiline = true;
            txtAttachments.ReadOnly = true;
            txtAttachments.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtAttachments.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtAttachments.UseSystemPasswordChar = false;
            txtAttachments.WordWrap = true;
            txtAttachments.MaxLength = 32767;
            txtAttachments.AcceptsReturn = false;
            txtAttachments.AcceptsTab = false;
            txtAttachments.Name = "txtAttachments";
            //
            // flowActions
            //
            flowActions.Text = "";
            flowActions.Location = new System.Drawing.Point(0, 391);
            flowActions.Size = new System.Drawing.Size(719, 41);
            flowActions.MinimumSize = new System.Drawing.Size(0, 40);
            flowActions.MaximumSize = new System.Drawing.Size(0, 0);
            flowActions.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            flowActions.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            flowActions.Dock = System.Windows.Forms.DockStyle.Fill;
            flowActions.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            flowActions.AutoSize = true;
            flowActions.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            flowActions.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            flowActions.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            flowActions.TabIndex = 1;
            flowActions.TabStop = false;
            flowActions.AutoScroll = false;
            flowActions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowActions.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            flowActions.WrapContents = true;
            flowActions.Name = "flowActions";
            flowActions.Controls.Add(btnSelectAttachment);
            //
            // btnSelectAttachment
            //
            btnSelectAttachment.Text = "첨부파일 선택";
            btnSelectAttachment.Location = new System.Drawing.Point(0, 0);
            btnSelectAttachment.Size = new System.Drawing.Size(119, 35);
            btnSelectAttachment.MinimumSize = new System.Drawing.Size(88, 34);
            btnSelectAttachment.MaximumSize = new System.Drawing.Size(0, 0);
            btnSelectAttachment.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnSelectAttachment.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnSelectAttachment.Dock = System.Windows.Forms.DockStyle.None;
            btnSelectAttachment.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnSelectAttachment.AutoSize = true;
            btnSelectAttachment.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnSelectAttachment.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            btnSelectAttachment.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnSelectAttachment.TabIndex = 0;
            btnSelectAttachment.TabStop = true;
            btnSelectAttachment.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnSelectAttachment.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnSelectAttachment.UseVisualStyleBackColor = false;
            btnSelectAttachment.DialogResult = System.Windows.Forms.DialogResult.None;
            btnSelectAttachment.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnSelectAttachment.Cursor = System.Windows.Forms.Cursors.Hand;
            btnSelectAttachment.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 213, 220, 231);
            btnSelectAttachment.FlatAppearance.BorderSize = 1;
            btnSelectAttachment.Name = "btnSelectAttachment";
            btnSelectAttachment.Click += new System.EventHandler(Unavailable_Click);
            //
            // flowActions2
            //
            flowActions2.Text = "";
            flowActions2.Location = new System.Drawing.Point(0, 536);
            flowActions2.Size = new System.Drawing.Size(736, 42);
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
            flowActions2.Controls.Add(btnConfirm);
            flowActions2.Controls.Add(btnCancel);
            flowActions2.Controls.Add(btnClose);
            //
            // btnConfirm
            //
            btnConfirm.Text = "등록 확인";
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
            // TaskEditForm
            //
            Text = "업무 등록";
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
            Name = "TaskEditForm";
            Controls.Add(layoutContent);
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            CancelButton = btnClose;
            flowActions2.ResumeLayout(false);
            flowActions2.PerformLayout();
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
        private System.Windows.Forms.Label lblTaskTitle;
        private System.Windows.Forms.TextBox txtTaskTitle;
        private System.Windows.Forms.Label lblTaskDescription;
        private System.Windows.Forms.TextBox txtTaskDescription;
        private System.Windows.Forms.Label lblAssignee;
        private System.Windows.Forms.ComboBox cmbAssignee;
        private System.Windows.Forms.Label lblStartDate;
        private System.Windows.Forms.DateTimePicker dtpStartDate;
        private System.Windows.Forms.Label lblDeadline;
        private System.Windows.Forms.DateTimePicker dtpDeadline;
        private System.Windows.Forms.Label lblAttachments;
        private System.Windows.Forms.TextBox txtAttachments;
        private System.Windows.Forms.FlowLayoutPanel flowActions;
        private System.Windows.Forms.Button btnSelectAttachment;
        private System.Windows.Forms.FlowLayoutPanel flowActions2;
        private System.Windows.Forms.Button btnConfirm;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnClose;
    }
}
