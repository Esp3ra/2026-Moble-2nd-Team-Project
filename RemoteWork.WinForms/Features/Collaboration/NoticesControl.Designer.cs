#nullable disable

namespace RemoteWork.WinForms.Features.Collaboration
{
    partial class NoticesControl
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
            layoutContent = new System.Windows.Forms.TableLayoutPanel();
            layoutContent2 = new System.Windows.Forms.TableLayoutPanel();
            lblPageTitle = new System.Windows.Forms.Label();
            panelContent = new System.Windows.Forms.Panel();
            layoutContent3 = new System.Windows.Forms.TableLayoutPanel();
            layoutContent4 = new System.Windows.Forms.TableLayoutPanel();
            flowActions = new System.Windows.Forms.FlowLayoutPanel();
            cmbOptions2 = new System.Windows.Forms.ComboBox();
            btnQuery = new System.Windows.Forms.Button();
            btnNewNoticePreview = new System.Windows.Forms.Button();
            panelContent2 = new System.Windows.Forms.Panel();
            layoutContent5 = new System.Windows.Forms.TableLayoutPanel();
            lblText2 = new System.Windows.Forms.Label();
            gridNotices = new System.Windows.Forms.DataGridView();
            columnGridNotices1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridNotices2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridNotices3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridNotices4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            layoutContent6 = new System.Windows.Forms.TableLayoutPanel();
            panelContent3 = new System.Windows.Forms.Panel();
            layoutContent7 = new System.Windows.Forms.TableLayoutPanel();
            lblText4 = new System.Windows.Forms.Label();
            txtContent = new System.Windows.Forms.TextBox();
            panelContent4 = new System.Windows.Forms.Panel();
            layoutContent8 = new System.Windows.Forms.TableLayoutPanel();
            lblText5 = new System.Windows.Forms.Label();
            txtContent2 = new System.Windows.Forms.TextBox();
            layoutContent.SuspendLayout();
            layoutContent2.SuspendLayout();
            panelContent.SuspendLayout();
            layoutContent3.SuspendLayout();
            layoutContent4.SuspendLayout();
            flowActions.SuspendLayout();
            panelContent2.SuspendLayout();
            layoutContent5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridNotices).BeginInit();
            layoutContent6.SuspendLayout();
            panelContent3.SuspendLayout();
            layoutContent7.SuspendLayout();
            panelContent4.SuspendLayout();
            layoutContent8.SuspendLayout();
            SuspendLayout();
            //
            // layoutContent
            //
            layoutContent.Text = "";
            layoutContent.Location = new System.Drawing.Point(24, 18);
            layoutContent.Size = new System.Drawing.Size(1062, 668);
            layoutContent.MinimumSize = new System.Drawing.Size(0, 0);
            layoutContent.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
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
            layoutContent.RowCount = 2;
            layoutContent.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent.Name = "layoutContent";
            layoutContent.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 70F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent.Controls.Add(layoutContent2, 0, 0);
            layoutContent.Controls.Add(panelContent, 0, 1);
            //
            // layoutContent2
            //
            layoutContent2.Text = "";
            layoutContent2.Location = new System.Drawing.Point(0, 0);
            layoutContent2.Size = new System.Drawing.Size(1062, 70);
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
            layoutContent2.RowCount = 2;
            layoutContent2.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent2.Name = "layoutContent2";
            layoutContent2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize, 0F));
            layoutContent2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent2.Controls.Add(lblPageTitle, 0, 0);
            //
            // lblPageTitle
            //
            lblPageTitle.Text = "공지사항";
            lblPageTitle.Location = new System.Drawing.Point(0, 4);
            lblPageTitle.Size = new System.Drawing.Size(125, 28);
            lblPageTitle.MinimumSize = new System.Drawing.Size(0, 0);
            lblPageTitle.MaximumSize = new System.Drawing.Size(0, 0);
            lblPageTitle.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
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
            //
            //
            // panelContent
            //
            panelContent.Text = "";
            panelContent.Location = new System.Drawing.Point(0, 110);
            panelContent.Size = new System.Drawing.Size(1062, 558);
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
            layoutContent3.Size = new System.Drawing.Size(1045, 560);
            layoutContent3.MinimumSize = new System.Drawing.Size(0, 560);
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
            layoutContent3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 56F));
            layoutContent3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 44F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.Controls.Add(layoutContent4, 0, 0);
            layoutContent3.Controls.Add(layoutContent6, 1, 0);
            //
            // layoutContent4
            //
            layoutContent4.Text = "";
            layoutContent4.Location = new System.Drawing.Point(0, 0);
            layoutContent4.Size = new System.Drawing.Size(585, 560);
            layoutContent4.MinimumSize = new System.Drawing.Size(0, 0);
            layoutContent4.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent4.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent4.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent4.Dock = System.Windows.Forms.DockStyle.Fill;
            layoutContent4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            layoutContent4.AutoSize = false;
            layoutContent4.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            layoutContent4.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            layoutContent4.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            layoutContent4.TabIndex = 0;
            layoutContent4.TabStop = false;
            layoutContent4.AutoScroll = true;
            layoutContent4.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent4.ColumnCount = 1;
            layoutContent4.RowCount = 2;
            layoutContent4.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent4.Name = "layoutContent4";
            layoutContent4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize, 0F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent4.Controls.Add(flowActions, 0, 0);
            layoutContent4.Controls.Add(panelContent2, 0, 1);
            //
            // flowActions
            //
            flowActions.Text = "";
            flowActions.Location = new System.Drawing.Point(0, 0);
            flowActions.Size = new System.Drawing.Size(585, 41);
            flowActions.MinimumSize = new System.Drawing.Size(0, 40);
            flowActions.MaximumSize = new System.Drawing.Size(0, 0);
            flowActions.Margin = new System.Windows.Forms.Padding(0, 0, 0, 16);
            flowActions.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            flowActions.Dock = System.Windows.Forms.DockStyle.Fill;
            flowActions.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            flowActions.AutoSize = true;
            flowActions.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            flowActions.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            flowActions.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            flowActions.TabIndex = 0;
            flowActions.TabStop = false;
            flowActions.AutoScroll = false;
            flowActions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowActions.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            flowActions.WrapContents = true;
            flowActions.Name = "flowActions";
            flowActions.Controls.Add(cmbOptions2);
            flowActions.Controls.Add(btnQuery);
            flowActions.Controls.Add(btnNewNoticePreview);
            //
            // cmbOptions2
            //
            cmbOptions2.Text = "전체";
            cmbOptions2.Location = new System.Drawing.Point(121, 4);
            cmbOptions2.Size = new System.Drawing.Size(121, 26);
            cmbOptions2.MinimumSize = new System.Drawing.Size(0, 0);
            cmbOptions2.MaximumSize = new System.Drawing.Size(0, 0);
            cmbOptions2.Margin = new System.Windows.Forms.Padding(0, 4, 8, 6);
            cmbOptions2.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            cmbOptions2.Dock = System.Windows.Forms.DockStyle.None;
            cmbOptions2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            cmbOptions2.AutoSize = false;
            cmbOptions2.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            cmbOptions2.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            cmbOptions2.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            cmbOptions2.TabIndex = 1;
            cmbOptions2.TabStop = true;
            cmbOptions2.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbOptions2.IntegralHeight = true;
            cmbOptions2.MaxDropDownItems = 8;
            cmbOptions2.Name = "cmbOptions2";
            cmbOptions2.Items.AddRange(new object[] { "전체", "전 직원", "기획팀", "개발팀" });
            //
            // btnQuery
            //
            btnQuery.Text = "조회";
            btnQuery.Location = new System.Drawing.Point(242, 0);
            btnQuery.Size = new System.Drawing.Size(88, 35);
            btnQuery.MinimumSize = new System.Drawing.Size(88, 34);
            btnQuery.MaximumSize = new System.Drawing.Size(0, 0);
            btnQuery.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnQuery.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnQuery.Dock = System.Windows.Forms.DockStyle.None;
            btnQuery.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnQuery.AutoSize = true;
            btnQuery.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnQuery.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            btnQuery.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnQuery.TabIndex = 2;
            btnQuery.TabStop = true;
            btnQuery.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnQuery.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnQuery.UseVisualStyleBackColor = false;
            btnQuery.DialogResult = System.Windows.Forms.DialogResult.None;
            btnQuery.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnQuery.Cursor = System.Windows.Forms.Cursors.Hand;
            btnQuery.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 213, 220, 231);
            btnQuery.FlatAppearance.BorderSize = 1;
            btnQuery.Name = "btnQuery";
            btnQuery.Click += new System.EventHandler(QueryNotice_Click);
            //
            // btnNewNoticePreview
            //
            btnNewNoticePreview.Text = "공지사항 작성";
            btnNewNoticePreview.Location = new System.Drawing.Point(338, 0);
            btnNewNoticePreview.Size = new System.Drawing.Size(123, 35);
            btnNewNoticePreview.MinimumSize = new System.Drawing.Size(88, 34);
            btnNewNoticePreview.MaximumSize = new System.Drawing.Size(0, 0);
            btnNewNoticePreview.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnNewNoticePreview.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnNewNoticePreview.Dock = System.Windows.Forms.DockStyle.None;
            btnNewNoticePreview.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnNewNoticePreview.AutoSize = true;
            btnNewNoticePreview.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnNewNoticePreview.ForeColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnNewNoticePreview.BackColor = System.Drawing.Color.FromArgb(255, 52, 101, 224);
            btnNewNoticePreview.TabIndex = 3;
            btnNewNoticePreview.TabStop = true;
            btnNewNoticePreview.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnNewNoticePreview.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnNewNoticePreview.UseVisualStyleBackColor = false;
            btnNewNoticePreview.DialogResult = System.Windows.Forms.DialogResult.None;
            btnNewNoticePreview.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnNewNoticePreview.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNewNoticePreview.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 52, 101, 224);
            btnNewNoticePreview.FlatAppearance.BorderSize = 1;
            btnNewNoticePreview.Name = "btnNewNoticePreview";
            btnNewNoticePreview.Click += new System.EventHandler(btnNewNoticePreview_Click);
            //
            // panelContent2
            //
            panelContent2.Text = "";
            panelContent2.Location = new System.Drawing.Point(0, 49);
            panelContent2.Size = new System.Drawing.Size(575, 474);
            panelContent2.MinimumSize = new System.Drawing.Size(0, 208);
            panelContent2.MaximumSize = new System.Drawing.Size(0, 0);
            panelContent2.Margin = new System.Windows.Forms.Padding(0, 0, 10, 10);
            panelContent2.Padding = new System.Windows.Forms.Padding(16, 16, 16, 16);
            panelContent2.Dock = System.Windows.Forms.DockStyle.Fill;
            panelContent2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            panelContent2.AutoSize = false;
            panelContent2.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            panelContent2.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            panelContent2.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            panelContent2.TabIndex = 1;
            panelContent2.TabStop = false;
            panelContent2.AutoScroll = false;
            panelContent2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            panelContent2.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panelContent2.Name = "panelContent2";
            panelContent2.Controls.Add(layoutContent5);
            //
            // layoutContent5
            //
            layoutContent5.Text = "";
            layoutContent5.Location = new System.Drawing.Point(16, 16);
            layoutContent5.Size = new System.Drawing.Size(541, 440);
            layoutContent5.MinimumSize = new System.Drawing.Size(0, 0);
            layoutContent5.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent5.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            layoutContent5.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent5.Dock = System.Windows.Forms.DockStyle.Fill;
            layoutContent5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            layoutContent5.AutoSize = false;
            layoutContent5.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            layoutContent5.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            layoutContent5.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            layoutContent5.TabIndex = 0;
            layoutContent5.TabStop = false;
            layoutContent5.AutoScroll = false;
            layoutContent5.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent5.ColumnCount = 1;
            layoutContent5.RowCount = 2;
            layoutContent5.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent5.Name = "layoutContent5";
            layoutContent5.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent5.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            layoutContent5.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent5.Controls.Add(lblText2, 0, 0);
            layoutContent5.Controls.Add(gridNotices, 0, 1);
            //
            // lblText2
            //
            lblText2.Text = "공지사항 목록";
            lblText2.Location = new System.Drawing.Point(0, 4);
            lblText2.Size = new System.Drawing.Size(112, 19);
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
            // gridNotices
            //
            gridNotices.Text = "";
            gridNotices.Location = new System.Drawing.Point(3, 37);
            gridNotices.Size = new System.Drawing.Size(535, 400);
            gridNotices.MinimumSize = new System.Drawing.Size(0, 140);
            gridNotices.MaximumSize = new System.Drawing.Size(0, 0);
            gridNotices.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            gridNotices.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            gridNotices.Dock = System.Windows.Forms.DockStyle.Fill;
            gridNotices.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            gridNotices.AutoSize = false;
            gridNotices.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            gridNotices.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            gridNotices.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            gridNotices.TabIndex = 1;
            gridNotices.TabStop = true;
            gridNotices.ReadOnly = true;
            gridNotices.AllowUserToAddRows = false;
            gridNotices.AllowUserToDeleteRows = false;
            gridNotices.AllowUserToOrderColumns = false;
            gridNotices.AllowUserToResizeRows = false;
            gridNotices.RowHeadersVisible = false;
            gridNotices.MultiSelect = false;
            gridNotices.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            gridNotices.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            gridNotices.BackgroundColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            gridNotices.BorderStyle = System.Windows.Forms.BorderStyle.None;
            gridNotices.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            gridNotices.GridColor = System.Drawing.Color.FromArgb(255, 235, 239, 246);
            gridNotices.EnableHeadersVisualStyles = false;
            gridNotices.ColumnHeadersHeight = 36;
            gridNotices.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            gridNotices.RowTemplate.Height = 38;
            gridNotices.Name = "gridNotices";
            columnGridNotices1.Name = "columnGridNotices1";
            columnGridNotices1.HeaderText = "대상";
            columnGridNotices1.MinimumWidth = 82;
            columnGridNotices1.Width = 122;
            columnGridNotices1.FillWeight = 100F;
            columnGridNotices1.ReadOnly = true;
            columnGridNotices1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            columnGridNotices1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridNotices2.Name = "columnGridNotices2";
            columnGridNotices2.HeaderText = "제목";
            columnGridNotices2.MinimumWidth = 168;
            columnGridNotices2.Width = 168;
            columnGridNotices2.FillWeight = 100F;
            columnGridNotices2.ReadOnly = true;
            columnGridNotices2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            columnGridNotices2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridNotices3.Name = "columnGridNotices3";
            columnGridNotices3.HeaderText = "작성자";
            columnGridNotices3.MinimumWidth = 82;
            columnGridNotices3.Width = 123;
            columnGridNotices3.FillWeight = 100F;
            columnGridNotices3.ReadOnly = true;
            columnGridNotices3.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            columnGridNotices3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridNotices4.Name = "columnGridNotices4";
            columnGridNotices4.HeaderText = "등록일";
            columnGridNotices4.MinimumWidth = 106;
            columnGridNotices4.Width = 122;
            columnGridNotices4.FillWeight = 100F;
            columnGridNotices4.ReadOnly = true;
            columnGridNotices4.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.Programmatic;
            columnGridNotices4.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
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
            gridNotices.ColumnHeadersDefaultCellStyle = headerStyle1;
            gridNotices.DefaultCellStyle = cellStyle1;
            gridNotices.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { columnGridNotices1, columnGridNotices2, columnGridNotices3, columnGridNotices4 });
            //
            // layoutContent6
            //
            layoutContent6.Text = "";
            layoutContent6.Location = new System.Drawing.Point(585, 0);
            layoutContent6.Size = new System.Drawing.Size(460, 560);
            layoutContent6.MinimumSize = new System.Drawing.Size(0, 0);
            layoutContent6.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent6.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent6.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent6.Dock = System.Windows.Forms.DockStyle.Fill;
            layoutContent6.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            layoutContent6.AutoSize = false;
            layoutContent6.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            layoutContent6.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            layoutContent6.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            layoutContent6.TabIndex = 1;
            layoutContent6.TabStop = false;
            layoutContent6.AutoScroll = true;
            layoutContent6.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent6.ColumnCount = 1;
            layoutContent6.RowCount = 2;
            layoutContent6.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent6.Name = "layoutContent6";
            layoutContent6.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 75F));
            layoutContent6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            layoutContent6.Controls.Add(panelContent3, 0, 0);
            layoutContent6.Controls.Add(panelContent4, 0, 1);
            //
            // panelContent3
            //
            panelContent3.Text = "";
            panelContent3.Location = new System.Drawing.Point(0, 0);
            panelContent3.Size = new System.Drawing.Size(450, 232);
            panelContent3.MinimumSize = new System.Drawing.Size(0, 124);
            panelContent3.MaximumSize = new System.Drawing.Size(0, 0);
            panelContent3.Margin = new System.Windows.Forms.Padding(0, 0, 10, 10);
            panelContent3.Padding = new System.Windows.Forms.Padding(16, 16, 16, 16);
            panelContent3.Dock = System.Windows.Forms.DockStyle.Fill;
            panelContent3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            panelContent3.AutoSize = false;
            panelContent3.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            panelContent3.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            panelContent3.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            panelContent3.TabIndex = 0;
            panelContent3.TabStop = false;
            panelContent3.AutoScroll = false;
            panelContent3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            panelContent3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panelContent3.Name = "panelContent3";
            panelContent3.Controls.Add(layoutContent7);
            //
            // layoutContent7
            //
            layoutContent7.Text = "";
            layoutContent7.Location = new System.Drawing.Point(16, 16);
            layoutContent7.Size = new System.Drawing.Size(416, 198);
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
            layoutContent7.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            layoutContent7.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent7.Controls.Add(lblText4, 0, 0);
            layoutContent7.Controls.Add(txtContent, 0, 1);
            //
            // lblText4
            //
            lblText4.Text = "보안 점검 안내";
            lblText4.Location = new System.Drawing.Point(0, 4);
            lblText4.Size = new System.Drawing.Size(145, 19);
            lblText4.MinimumSize = new System.Drawing.Size(0, 0);
            lblText4.MaximumSize = new System.Drawing.Size(0, 0);
            lblText4.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblText4.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblText4.Dock = System.Windows.Forms.DockStyle.None;
            lblText4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblText4.AutoSize = true;
            lblText4.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblText4.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblText4.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            lblText4.TabIndex = 0;
            lblText4.TabStop = false;
            lblText4.AutoEllipsis = false;
            lblText4.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            lblText4.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblText4.UseCompatibleTextRendering = false;
            lblText4.Name = "lblText4";
            //
            // txtContent
            //
            txtContent.Text = "대상: 전 직원\r\n작성자: 관리자    등록일: 2026.10.13\r\n\r\n원격 근무 환경의 보안 점검이 예정되어 있습니다.\r\n담당 업무와 일정을 확인해 주세요.";
            txtContent.Location = new System.Drawing.Point(3, 37);
            txtContent.Size = new System.Drawing.Size(410, 158);
            txtContent.MinimumSize = new System.Drawing.Size(0, 56);
            txtContent.MaximumSize = new System.Drawing.Size(0, 0);
            txtContent.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            txtContent.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtContent.Dock = System.Windows.Forms.DockStyle.Fill;
            txtContent.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtContent.AutoSize = true;
            txtContent.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtContent.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            txtContent.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtContent.TabIndex = 1;
            txtContent.TabStop = false;
            txtContent.Multiline = true;
            txtContent.ReadOnly = true;
            txtContent.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtContent.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtContent.UseSystemPasswordChar = false;
            txtContent.WordWrap = true;
            txtContent.MaxLength = 32767;
            txtContent.AcceptsReturn = false;
            txtContent.AcceptsTab = false;
            txtContent.Name = "txtContent";
            //
            // panelContent4
            //
            panelContent4.Text = "";
            panelContent4.Location = new System.Drawing.Point(0, 291);
            panelContent4.Size = new System.Drawing.Size(450, 232);
            panelContent4.MinimumSize = new System.Drawing.Size(0, 124);
            panelContent4.MaximumSize = new System.Drawing.Size(0, 0);
            panelContent4.Margin = new System.Windows.Forms.Padding(0, 0, 10, 10);
            panelContent4.Padding = new System.Windows.Forms.Padding(16, 16, 16, 16);
            panelContent4.Dock = System.Windows.Forms.DockStyle.Fill;
            panelContent4.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            panelContent4.AutoSize = false;
            panelContent4.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            panelContent4.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            panelContent4.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            panelContent4.TabIndex = 2;
            panelContent4.TabStop = false;
            panelContent4.AutoScroll = false;
            panelContent4.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            panelContent4.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panelContent4.Name = "panelContent4";
            panelContent4.Controls.Add(layoutContent8);
            //
            // layoutContent8
            //
            layoutContent8.Text = "";
            layoutContent8.Location = new System.Drawing.Point(16, 16);
            layoutContent8.Size = new System.Drawing.Size(416, 198);
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
            layoutContent8.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 34F));
            layoutContent8.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent8.Controls.Add(lblText5, 0, 0);
            layoutContent8.Controls.Add(txtContent2, 0, 1);
            //
            // lblText5
            //
            lblText5.Text = "업무 마감 알림";
            lblText5.Location = new System.Drawing.Point(0, 4);
            lblText5.Size = new System.Drawing.Size(178, 19);
            lblText5.MinimumSize = new System.Drawing.Size(0, 0);
            lblText5.MaximumSize = new System.Drawing.Size(0, 0);
            lblText5.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblText5.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblText5.Dock = System.Windows.Forms.DockStyle.None;
            lblText5.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblText5.AutoSize = true;
            lblText5.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblText5.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblText5.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            lblText5.TabIndex = 0;
            lblText5.TabStop = false;
            lblText5.AutoEllipsis = false;
            lblText5.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            lblText5.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblText5.UseCompatibleTextRendering = false;
            lblText5.Name = "lblText5";
            //
            // txtContent2
            //
            txtContent2.Text = "업무: 주간 보고서 작성\r\n마감: 2026.10.14 18:00\r\n상태: 마감 예정";
            txtContent2.Location = new System.Drawing.Point(3, 37);
            txtContent2.Size = new System.Drawing.Size(410, 158);
            txtContent2.MinimumSize = new System.Drawing.Size(0, 56);
            txtContent2.MaximumSize = new System.Drawing.Size(0, 0);
            txtContent2.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            txtContent2.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtContent2.Dock = System.Windows.Forms.DockStyle.Fill;
            txtContent2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtContent2.AutoSize = true;
            txtContent2.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtContent2.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            txtContent2.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtContent2.TabIndex = 1;
            txtContent2.TabStop = false;
            txtContent2.Multiline = true;
            txtContent2.ReadOnly = true;
            txtContent2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtContent2.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtContent2.UseSystemPasswordChar = false;
            txtContent2.WordWrap = true;
            txtContent2.MaxLength = 32767;
            txtContent2.AcceptsReturn = false;
            txtContent2.AcceptsTab = false;
            txtContent2.Name = "txtContent2";
            //
            // NoticesControl
            //
            Text = "";
            Size = new System.Drawing.Size(1100, 700);
            MinimumSize = new System.Drawing.Size(0, 0);
            MaximumSize = new System.Drawing.Size(0, 0);
            Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            Padding = new System.Windows.Forms.Padding(24, 18, 14, 14);
            AutoSize = false;
            Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            TabIndex = 0;
            TabStop = true;
            Name = "NoticesControl";
            Controls.Add(layoutContent);
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            layoutContent8.ResumeLayout(false);
            layoutContent8.PerformLayout();
            panelContent4.ResumeLayout(false);
            panelContent4.PerformLayout();
            layoutContent7.ResumeLayout(false);
            layoutContent7.PerformLayout();
            panelContent3.ResumeLayout(false);
            panelContent3.PerformLayout();
            layoutContent6.ResumeLayout(false);
            layoutContent6.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)gridNotices).EndInit();
            layoutContent5.ResumeLayout(false);
            layoutContent5.PerformLayout();
            panelContent2.ResumeLayout(false);
            panelContent2.PerformLayout();
            flowActions.ResumeLayout(false);
            flowActions.PerformLayout();
            layoutContent4.ResumeLayout(false);
            layoutContent4.PerformLayout();
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
        private System.Windows.Forms.TableLayoutPanel layoutContent4;
        private System.Windows.Forms.FlowLayoutPanel flowActions;
        private System.Windows.Forms.ComboBox cmbOptions2;
        private System.Windows.Forms.Button btnQuery;
        private System.Windows.Forms.Button btnNewNoticePreview;
        private System.Windows.Forms.Panel panelContent2;
        private System.Windows.Forms.TableLayoutPanel layoutContent5;
        private System.Windows.Forms.Label lblText2;
        private System.Windows.Forms.DataGridView gridNotices;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridNotices1;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridNotices2;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridNotices3;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridNotices4;
        private System.Windows.Forms.TableLayoutPanel layoutContent6;
        private System.Windows.Forms.Panel panelContent3;
        private System.Windows.Forms.TableLayoutPanel layoutContent7;
        private System.Windows.Forms.Label lblText4;
        private System.Windows.Forms.TextBox txtContent;
        private System.Windows.Forms.Panel panelContent4;
        private System.Windows.Forms.TableLayoutPanel layoutContent8;
        private System.Windows.Forms.Label lblText5;
        private System.Windows.Forms.TextBox txtContent2;
    }
}
