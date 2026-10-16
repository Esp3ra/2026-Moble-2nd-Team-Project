#nullable disable

namespace RemoteWork.WinForms.Features.Results
{
    partial class HistoryControl
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
            flowActions = new System.Windows.Forms.FlowLayoutPanel();
            cmbOptions = new System.Windows.Forms.ComboBox();
            cmbOptions2 = new System.Windows.Forms.ComboBox();
            btnQuery = new System.Windows.Forms.Button();
            panelContent2 = new System.Windows.Forms.Panel();
            layoutContent4 = new System.Windows.Forms.TableLayoutPanel();
            lblText2 = new System.Windows.Forms.Label();
            gridHistory = new System.Windows.Forms.DataGridView();
            columnGridHistory1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridHistory2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridHistory3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridHistory4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridHistory5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            panelContent3 = new System.Windows.Forms.Panel();
            layoutContent5 = new System.Windows.Forms.TableLayoutPanel();
            lblText3 = new System.Windows.Forms.Label();
            txtContent = new System.Windows.Forms.TextBox();
            layoutContent.SuspendLayout();
            layoutContent2.SuspendLayout();
            panelContent.SuspendLayout();
            layoutContent3.SuspendLayout();
            flowActions.SuspendLayout();
            panelContent2.SuspendLayout();
            layoutContent4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridHistory).BeginInit();
            panelContent3.SuspendLayout();
            layoutContent5.SuspendLayout();
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
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
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
            layoutContent2.RowCount = 1;
            layoutContent2.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent2.Name = "layoutContent2";
            layoutContent2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            layoutContent2.Controls.Add(lblPageTitle, 0, 0);
            //
            // lblPageTitle
            //
            lblPageTitle.Text = "업무 이력";
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
            layoutContent3.ColumnCount = 1;
            layoutContent3.RowCount = 3;
            layoutContent3.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent3.Name = "layoutContent3";
            layoutContent3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 62F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 170F));
            layoutContent3.Controls.Add(flowActions, 0, 0);
            layoutContent3.Controls.Add(panelContent2, 0, 1);
            layoutContent3.Controls.Add(panelContent3, 0, 2);
            //
            // flowActions
            //
            flowActions.Text = "";
            flowActions.Location = new System.Drawing.Point(0, 0);
            flowActions.Size = new System.Drawing.Size(1045, 54);
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
            flowActions.TabIndex = 0;
            flowActions.TabStop = false;
            flowActions.AutoScroll = false;
            flowActions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowActions.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            flowActions.WrapContents = true;
            flowActions.Name = "flowActions";
            flowActions.Controls.Add(cmbOptions);
            flowActions.Controls.Add(cmbOptions2);
            flowActions.Controls.Add(btnQuery);
            //
            // cmbOptions
            //
            cmbOptions.Text = "전체 업무";
            cmbOptions.Location = new System.Drawing.Point(0, 4);
            cmbOptions.Size = new System.Drawing.Size(121, 26);
            cmbOptions.MinimumSize = new System.Drawing.Size(0, 0);
            cmbOptions.MaximumSize = new System.Drawing.Size(0, 0);
            cmbOptions.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            cmbOptions.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            cmbOptions.Dock = System.Windows.Forms.DockStyle.None;
            cmbOptions.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            cmbOptions.AutoSize = false;
            cmbOptions.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            cmbOptions.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            cmbOptions.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            cmbOptions.TabIndex = 0;
            cmbOptions.TabStop = true;
            cmbOptions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbOptions.IntegralHeight = true;
            cmbOptions.MaxDropDownItems = 8;
            cmbOptions.Name = "cmbOptions";
            cmbOptions.Items.AddRange(new object[] { "전체 업무", "WRK-2026-002 · 월간 운영 현황 정리" });
            //
            // cmbOptions2
            //
            cmbOptions2.Text = "전체 변경 유형";
            cmbOptions2.Location = new System.Drawing.Point(121, 4);
            cmbOptions2.Size = new System.Drawing.Size(121, 26);
            cmbOptions2.MinimumSize = new System.Drawing.Size(0, 0);
            cmbOptions2.MaximumSize = new System.Drawing.Size(0, 0);
            cmbOptions2.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
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
            cmbOptions2.Items.AddRange(new object[] { "전체 변경 유형", "생성", "담당자 변경", "상태 변경", "진행률 변경", "결과 제출", "반려", "승인" });
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
            btnQuery.Click += new System.EventHandler(Unavailable_Click);
            //
            // panelContent2
            //
            panelContent2.Text = "";
            panelContent2.Location = new System.Drawing.Point(0, 62);
            panelContent2.Size = new System.Drawing.Size(1035, 318);
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
            panelContent2.Controls.Add(layoutContent4);
            //
            // layoutContent4
            //
            layoutContent4.Text = "";
            layoutContent4.Location = new System.Drawing.Point(16, 16);
            layoutContent4.Size = new System.Drawing.Size(1001, 284);
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
            layoutContent4.Controls.Add(gridHistory, 0, 1);
            //
            // lblText2
            //
            lblText2.Text = "업무 변경 이력";
            lblText2.Location = new System.Drawing.Point(0, 4);
            lblText2.Size = new System.Drawing.Size(205, 19);
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
            // gridHistory
            //
            gridHistory.Text = "";
            gridHistory.Location = new System.Drawing.Point(3, 37);
            gridHistory.Size = new System.Drawing.Size(995, 244);
            gridHistory.MinimumSize = new System.Drawing.Size(0, 140);
            gridHistory.MaximumSize = new System.Drawing.Size(0, 0);
            gridHistory.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            gridHistory.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            gridHistory.Dock = System.Windows.Forms.DockStyle.Fill;
            gridHistory.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            gridHistory.AutoSize = false;
            gridHistory.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            gridHistory.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            gridHistory.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            gridHistory.TabIndex = 1;
            gridHistory.TabStop = true;
            gridHistory.ReadOnly = true;
            gridHistory.AllowUserToAddRows = false;
            gridHistory.AllowUserToDeleteRows = false;
            gridHistory.AllowUserToOrderColumns = false;
            gridHistory.AllowUserToResizeRows = false;
            gridHistory.RowHeadersVisible = false;
            gridHistory.MultiSelect = false;
            gridHistory.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            gridHistory.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            gridHistory.BackgroundColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            gridHistory.BorderStyle = System.Windows.Forms.BorderStyle.None;
            gridHistory.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            gridHistory.GridColor = System.Drawing.Color.FromArgb(255, 235, 239, 246);
            gridHistory.EnableHeadersVisualStyles = false;
            gridHistory.ColumnHeadersHeight = 36;
            gridHistory.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            gridHistory.RowTemplate.Height = 38;
            gridHistory.Name = "gridHistory";
            columnGridHistory1.Name = "columnGridHistory1";
            columnGridHistory1.HeaderText = "업무 번호";
            columnGridHistory1.MinimumWidth = 106;
            columnGridHistory1.Width = 196;
            columnGridHistory1.FillWeight = 100F;
            columnGridHistory1.ReadOnly = true;
            columnGridHistory1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridHistory1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridHistory2.Name = "columnGridHistory2";
            columnGridHistory2.HeaderText = "수행자";
            columnGridHistory2.MinimumWidth = 82;
            columnGridHistory2.Width = 195;
            columnGridHistory2.FillWeight = 100F;
            columnGridHistory2.ReadOnly = true;
            columnGridHistory2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridHistory2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridHistory3.Name = "columnGridHistory3";
            columnGridHistory3.HeaderText = "변경 시각";
            columnGridHistory3.MinimumWidth = 144;
            columnGridHistory3.Width = 196;
            columnGridHistory3.FillWeight = 100F;
            columnGridHistory3.ReadOnly = true;
            columnGridHistory3.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridHistory3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridHistory4.Name = "columnGridHistory4";
            columnGridHistory4.HeaderText = "변경 유형";
            columnGridHistory4.MinimumWidth = 82;
            columnGridHistory4.Width = 195;
            columnGridHistory4.FillWeight = 100F;
            columnGridHistory4.ReadOnly = true;
            columnGridHistory4.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridHistory4.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridHistory5.Name = "columnGridHistory5";
            columnGridHistory5.HeaderText = "변경 내용";
            columnGridHistory5.MinimumWidth = 168;
            columnGridHistory5.Width = 196;
            columnGridHistory5.FillWeight = 100F;
            columnGridHistory5.ReadOnly = true;
            columnGridHistory5.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridHistory5.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
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
            gridHistory.ColumnHeadersDefaultCellStyle = headerStyle1;
            gridHistory.DefaultCellStyle = cellStyle1;
            gridHistory.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { columnGridHistory1, columnGridHistory2, columnGridHistory3, columnGridHistory4, columnGridHistory5 });
            //
            // panelContent3
            //
            panelContent3.Text = "";
            panelContent3.Location = new System.Drawing.Point(0, 390);
            panelContent3.Size = new System.Drawing.Size(1035, 160);
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
            panelContent3.TabIndex = 2;
            panelContent3.TabStop = false;
            panelContent3.AutoScroll = false;
            panelContent3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            panelContent3.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panelContent3.Name = "panelContent3";
            panelContent3.Controls.Add(layoutContent5);
            //
            // layoutContent5
            //
            layoutContent5.Text = "";
            layoutContent5.Location = new System.Drawing.Point(16, 16);
            layoutContent5.Size = new System.Drawing.Size(1001, 126);
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
            layoutContent5.Controls.Add(lblText3, 0, 0);
            layoutContent5.Controls.Add(txtContent, 0, 1);
            //
            // lblText3
            //
            lblText3.Text = "이력 상세";
            lblText3.Location = new System.Drawing.Point(0, 4);
            lblText3.Size = new System.Drawing.Size(145, 19);
            lblText3.MinimumSize = new System.Drawing.Size(0, 0);
            lblText3.MaximumSize = new System.Drawing.Size(0, 0);
            lblText3.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblText3.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblText3.Dock = System.Windows.Forms.DockStyle.None;
            lblText3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblText3.AutoSize = true;
            lblText3.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblText3.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblText3.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            lblText3.TabIndex = 0;
            lblText3.TabStop = false;
            lblText3.AutoEllipsis = false;
            lblText3.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            lblText3.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblText3.UseCompatibleTextRendering = false;
            lblText3.Name = "lblText3";
            //
            // txtContent
            //
            txtContent.Text = "WRK-2026-002 · 월간 운영 현황 정리\r\n수행자: 이서연 (관리자)   |   변경 시각: 2026-10-22 10:30   |   유형: 승인\r\n변경 내용: v2 제출 결과 검토 승인";
            txtContent.Location = new System.Drawing.Point(3, 37);
            txtContent.Size = new System.Drawing.Size(995, 86);
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
            // HistoryControl
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
            Name = "HistoryControl";
            Controls.Add(layoutContent);
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            layoutContent5.ResumeLayout(false);
            layoutContent5.PerformLayout();
            panelContent3.ResumeLayout(false);
            panelContent3.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)gridHistory).EndInit();
            layoutContent4.ResumeLayout(false);
            layoutContent4.PerformLayout();
            panelContent2.ResumeLayout(false);
            panelContent2.PerformLayout();
            flowActions.ResumeLayout(false);
            flowActions.PerformLayout();
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
        private System.Windows.Forms.FlowLayoutPanel flowActions;
        private System.Windows.Forms.ComboBox cmbOptions;
        private System.Windows.Forms.ComboBox cmbOptions2;
        private System.Windows.Forms.Button btnQuery;
        private System.Windows.Forms.Panel panelContent2;
        private System.Windows.Forms.TableLayoutPanel layoutContent4;
        private System.Windows.Forms.Label lblText2;
        private System.Windows.Forms.DataGridView gridHistory;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridHistory1;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridHistory2;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridHistory3;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridHistory4;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridHistory5;
        private System.Windows.Forms.Panel panelContent3;
        private System.Windows.Forms.TableLayoutPanel layoutContent5;
        private System.Windows.Forms.Label lblText3;
        private System.Windows.Forms.TextBox txtContent;
    }
}
