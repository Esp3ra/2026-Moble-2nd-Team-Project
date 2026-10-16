#nullable disable

namespace RemoteWork.WinForms.Features.Tasks
{
    partial class TasksControl
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
            panelContent = new System.Windows.Forms.Panel();
            layoutContent3 = new System.Windows.Forms.TableLayoutPanel();
            flowActions = new System.Windows.Forms.FlowLayoutPanel();
            btnNewTaskPreview = new System.Windows.Forms.Button();
            btnEditTaskPreview = new System.Windows.Forms.Button();
            btnCancelTask = new System.Windows.Forms.Button();
            btnAcceptTask = new System.Windows.Forms.Button();
            btnReviewRequestPreview = new System.Windows.Forms.Button();
            btnDeadlineExtensionPreview = new System.Windows.Forms.Button();
            btnCollaborationRequest = new System.Windows.Forms.Button();
            btnTaskRequests = new System.Windows.Forms.Button();
            layoutContent4 = new System.Windows.Forms.TableLayoutPanel();
            panelContent2 = new System.Windows.Forms.Panel();
            layoutContent5 = new System.Windows.Forms.TableLayoutPanel();
            lblTaskListTitle = new System.Windows.Forms.Label();
            gridTasks = new System.Windows.Forms.DataGridView();
            columnGridTasks1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridTasks2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridTasks3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridTasks4 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridTasks5 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridTasks6 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridTasks7 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            layoutContent6 = new System.Windows.Forms.TableLayoutPanel();
            panelContent3 = new System.Windows.Forms.Panel();
            layoutContent7 = new System.Windows.Forms.TableLayoutPanel();
            lblText3 = new System.Windows.Forms.Label();
            txtContent = new System.Windows.Forms.TextBox();
            panelContent4 = new System.Windows.Forms.Panel();
            layoutContent8 = new System.Windows.Forms.TableLayoutPanel();
            lblText4 = new System.Windows.Forms.Label();
            gridTaskAttachments = new System.Windows.Forms.DataGridView();
            columnGridTaskAttachments1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridTaskAttachments2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnGridTaskAttachments3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            layoutContent.SuspendLayout();
            layoutContent2.SuspendLayout();
            panelContent.SuspendLayout();
            layoutContent3.SuspendLayout();
            flowActions.SuspendLayout();
            layoutContent4.SuspendLayout();
            panelContent2.SuspendLayout();
            layoutContent5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridTasks).BeginInit();
            layoutContent6.SuspendLayout();
            panelContent3.SuspendLayout();
            layoutContent7.SuspendLayout();
            panelContent4.SuspendLayout();
            layoutContent8.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridTaskAttachments).BeginInit();
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
            lblPageTitle.Text = "업무 관리";
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
            layoutContent3.RowCount = 2;
            layoutContent3.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent3.Name = "layoutContent3";
            layoutContent3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize, 0F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.Controls.Add(flowActions, 0, 0);
            layoutContent3.Controls.Add(layoutContent4, 0, 1);
            //
            // flowActions
            //
            flowActions.Text = "";
            flowActions.Location = new System.Drawing.Point(0, 0);
            flowActions.Size = new System.Drawing.Size(1045, 41);
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
            flowActions.Controls.Add(btnNewTaskPreview);
            flowActions.Controls.Add(btnEditTaskPreview);
            flowActions.Controls.Add(btnCancelTask);
            flowActions.Controls.Add(btnAcceptTask);
            flowActions.Controls.Add(btnReviewRequestPreview);
            flowActions.Controls.Add(btnDeadlineExtensionPreview);
            flowActions.Controls.Add(btnCollaborationRequest);
            flowActions.Controls.Add(btnTaskRequests);
            //
            // btnNewTaskPreview
            //
            btnNewTaskPreview.Text = "업무 등록";
            btnNewTaskPreview.Location = new System.Drawing.Point(0, 0);
            btnNewTaskPreview.Size = new System.Drawing.Size(139, 35);
            btnNewTaskPreview.MinimumSize = new System.Drawing.Size(88, 34);
            btnNewTaskPreview.MaximumSize = new System.Drawing.Size(0, 0);
            btnNewTaskPreview.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnNewTaskPreview.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnNewTaskPreview.Dock = System.Windows.Forms.DockStyle.None;
            btnNewTaskPreview.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnNewTaskPreview.AutoSize = true;
            btnNewTaskPreview.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnNewTaskPreview.ForeColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnNewTaskPreview.BackColor = System.Drawing.Color.FromArgb(255, 52, 101, 224);
            btnNewTaskPreview.TabIndex = 0;
            btnNewTaskPreview.TabStop = true;
            btnNewTaskPreview.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnNewTaskPreview.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnNewTaskPreview.UseVisualStyleBackColor = false;
            btnNewTaskPreview.DialogResult = System.Windows.Forms.DialogResult.None;
            btnNewTaskPreview.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnNewTaskPreview.Cursor = System.Windows.Forms.Cursors.Hand;
            btnNewTaskPreview.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 52, 101, 224);
            btnNewTaskPreview.FlatAppearance.BorderSize = 1;
            btnNewTaskPreview.Name = "btnNewTaskPreview";
            btnNewTaskPreview.Click += new System.EventHandler(btnNewTaskPreview_Click);
            //
            // btnEditTaskPreview
            //
            btnEditTaskPreview.Text = "업무 수정";
            btnEditTaskPreview.Location = new System.Drawing.Point(147, 0);
            btnEditTaskPreview.Size = new System.Drawing.Size(111, 35);
            btnEditTaskPreview.MinimumSize = new System.Drawing.Size(88, 34);
            btnEditTaskPreview.MaximumSize = new System.Drawing.Size(0, 0);
            btnEditTaskPreview.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnEditTaskPreview.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnEditTaskPreview.Dock = System.Windows.Forms.DockStyle.None;
            btnEditTaskPreview.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnEditTaskPreview.AutoSize = true;
            btnEditTaskPreview.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnEditTaskPreview.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            btnEditTaskPreview.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnEditTaskPreview.TabIndex = 1;
            btnEditTaskPreview.TabStop = true;
            btnEditTaskPreview.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnEditTaskPreview.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnEditTaskPreview.UseVisualStyleBackColor = false;
            btnEditTaskPreview.DialogResult = System.Windows.Forms.DialogResult.None;
            btnEditTaskPreview.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnEditTaskPreview.Cursor = System.Windows.Forms.Cursors.Hand;
            btnEditTaskPreview.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 213, 220, 231);
            btnEditTaskPreview.FlatAppearance.BorderSize = 1;
            btnEditTaskPreview.Name = "btnEditTaskPreview";
            btnEditTaskPreview.Click += new System.EventHandler(btnEditTaskPreview_Click);
            //
            // btnCancelTask
            //
            btnCancelTask.Text = "업무 취소";
            btnCancelTask.Location = new System.Drawing.Point(266, 0);
            btnCancelTask.Size = new System.Drawing.Size(95, 35);
            btnCancelTask.MinimumSize = new System.Drawing.Size(88, 34);
            btnCancelTask.MaximumSize = new System.Drawing.Size(0, 0);
            btnCancelTask.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnCancelTask.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnCancelTask.Dock = System.Windows.Forms.DockStyle.None;
            btnCancelTask.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnCancelTask.AutoSize = true;
            btnCancelTask.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnCancelTask.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            btnCancelTask.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnCancelTask.TabIndex = 2;
            btnCancelTask.TabStop = true;
            btnCancelTask.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnCancelTask.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnCancelTask.UseVisualStyleBackColor = false;
            btnCancelTask.DialogResult = System.Windows.Forms.DialogResult.None;
            btnCancelTask.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnCancelTask.Cursor = System.Windows.Forms.Cursors.Hand;
            btnCancelTask.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 213, 220, 231);
            btnCancelTask.FlatAppearance.BorderSize = 1;
            btnCancelTask.Name = "btnCancelTask";
            btnCancelTask.Click += new System.EventHandler(Unavailable_Click);
            //
            // btnAcceptTask
            //
            btnAcceptTask.Text = "업무 수락";
            btnAcceptTask.Location = new System.Drawing.Point(0, 0);
            btnAcceptTask.Size = new System.Drawing.Size(95, 35);
            btnAcceptTask.MinimumSize = new System.Drawing.Size(88, 34);
            btnAcceptTask.MaximumSize = new System.Drawing.Size(0, 0);
            btnAcceptTask.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnAcceptTask.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnAcceptTask.Dock = System.Windows.Forms.DockStyle.None;
            btnAcceptTask.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnAcceptTask.AutoSize = true;
            btnAcceptTask.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnAcceptTask.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            btnAcceptTask.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnAcceptTask.TabIndex = 0;
            btnAcceptTask.TabStop = true;
            btnAcceptTask.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnAcceptTask.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnAcceptTask.UseVisualStyleBackColor = false;
            btnAcceptTask.DialogResult = System.Windows.Forms.DialogResult.None;
            btnAcceptTask.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnAcceptTask.Cursor = System.Windows.Forms.Cursors.Hand;
            btnAcceptTask.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 213, 220, 231);
            btnAcceptTask.FlatAppearance.BorderSize = 1;
            btnAcceptTask.Name = "btnAcceptTask";
            btnAcceptTask.Click += new System.EventHandler(Unavailable_Click);
            //
            // btnReviewRequestPreview
            //
            btnReviewRequestPreview.Text = "검토 요청";
            btnReviewRequestPreview.Location = new System.Drawing.Point(0, 0);
            btnReviewRequestPreview.Size = new System.Drawing.Size(139, 35);
            btnReviewRequestPreview.MinimumSize = new System.Drawing.Size(88, 34);
            btnReviewRequestPreview.MaximumSize = new System.Drawing.Size(0, 0);
            btnReviewRequestPreview.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnReviewRequestPreview.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnReviewRequestPreview.Dock = System.Windows.Forms.DockStyle.None;
            btnReviewRequestPreview.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnReviewRequestPreview.AutoSize = true;
            btnReviewRequestPreview.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnReviewRequestPreview.ForeColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnReviewRequestPreview.BackColor = System.Drawing.Color.FromArgb(255, 52, 101, 224);
            btnReviewRequestPreview.TabIndex = 1;
            btnReviewRequestPreview.TabStop = true;
            btnReviewRequestPreview.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnReviewRequestPreview.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnReviewRequestPreview.UseVisualStyleBackColor = false;
            btnReviewRequestPreview.DialogResult = System.Windows.Forms.DialogResult.None;
            btnReviewRequestPreview.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnReviewRequestPreview.Cursor = System.Windows.Forms.Cursors.Hand;
            btnReviewRequestPreview.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 52, 101, 224);
            btnReviewRequestPreview.FlatAppearance.BorderSize = 1;
            btnReviewRequestPreview.Name = "btnReviewRequestPreview";
            btnReviewRequestPreview.Click += new System.EventHandler(btnReviewRequestPreview_Click);
            //
            // btnDeadlineExtensionPreview
            //
            btnDeadlineExtensionPreview.Text = "마감 연장";
            btnDeadlineExtensionPreview.Location = new System.Drawing.Point(0, 0);
            btnDeadlineExtensionPreview.Size = new System.Drawing.Size(139, 35);
            btnDeadlineExtensionPreview.MinimumSize = new System.Drawing.Size(88, 34);
            btnDeadlineExtensionPreview.MaximumSize = new System.Drawing.Size(0, 0);
            btnDeadlineExtensionPreview.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnDeadlineExtensionPreview.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnDeadlineExtensionPreview.Dock = System.Windows.Forms.DockStyle.None;
            btnDeadlineExtensionPreview.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnDeadlineExtensionPreview.AutoSize = true;
            btnDeadlineExtensionPreview.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnDeadlineExtensionPreview.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            btnDeadlineExtensionPreview.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnDeadlineExtensionPreview.TabIndex = 2;
            btnDeadlineExtensionPreview.TabStop = true;
            btnDeadlineExtensionPreview.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnDeadlineExtensionPreview.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnDeadlineExtensionPreview.UseVisualStyleBackColor = false;
            btnDeadlineExtensionPreview.DialogResult = System.Windows.Forms.DialogResult.None;
            btnDeadlineExtensionPreview.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnDeadlineExtensionPreview.Cursor = System.Windows.Forms.Cursors.Hand;
            btnDeadlineExtensionPreview.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 213, 220, 231);
            btnDeadlineExtensionPreview.FlatAppearance.BorderSize = 1;
            btnDeadlineExtensionPreview.Name = "btnDeadlineExtensionPreview";
            btnDeadlineExtensionPreview.Click += new System.EventHandler(btnDeadlineExtensionPreview_Click);
            //
            // btnCollaborationRequest
            //
            btnCollaborationRequest.Text = "협업 요청";
            btnCollaborationRequest.Location = new System.Drawing.Point(0, 0);
            btnCollaborationRequest.Size = new System.Drawing.Size(139, 35);
            btnCollaborationRequest.MinimumSize = new System.Drawing.Size(88, 34);
            btnCollaborationRequest.MaximumSize = new System.Drawing.Size(0, 0);
            btnCollaborationRequest.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnCollaborationRequest.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnCollaborationRequest.Dock = System.Windows.Forms.DockStyle.None;
            btnCollaborationRequest.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnCollaborationRequest.AutoSize = true;
            btnCollaborationRequest.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnCollaborationRequest.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            btnCollaborationRequest.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnCollaborationRequest.TabIndex = 2;
            btnCollaborationRequest.TabStop = true;
            btnCollaborationRequest.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnCollaborationRequest.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnCollaborationRequest.UseVisualStyleBackColor = false;
            btnCollaborationRequest.DialogResult = System.Windows.Forms.DialogResult.None;
            btnCollaborationRequest.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnCollaborationRequest.Cursor = System.Windows.Forms.Cursors.Hand;
            btnCollaborationRequest.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 213, 220, 231);
            btnCollaborationRequest.FlatAppearance.BorderSize = 1;
            btnCollaborationRequest.Name = "btnCollaborationRequest";
            btnCollaborationRequest.Click += new System.EventHandler(btnCollaborationRequest_Click);
            //
            // btnTaskRequests
            //
            btnTaskRequests.Text = "요청 확인";
            btnTaskRequests.Location = new System.Drawing.Point(0, 0);
            btnTaskRequests.Size = new System.Drawing.Size(139, 35);
            btnTaskRequests.MinimumSize = new System.Drawing.Size(88, 34);
            btnTaskRequests.MaximumSize = new System.Drawing.Size(0, 0);
            btnTaskRequests.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnTaskRequests.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnTaskRequests.Dock = System.Windows.Forms.DockStyle.None;
            btnTaskRequests.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnTaskRequests.AutoSize = true;
            btnTaskRequests.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnTaskRequests.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            btnTaskRequests.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnTaskRequests.TabIndex = 2;
            btnTaskRequests.TabStop = true;
            btnTaskRequests.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnTaskRequests.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnTaskRequests.UseVisualStyleBackColor = false;
            btnTaskRequests.DialogResult = System.Windows.Forms.DialogResult.None;
            btnTaskRequests.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnTaskRequests.Cursor = System.Windows.Forms.Cursors.Hand;
            btnTaskRequests.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 213, 220, 231);
            btnTaskRequests.FlatAppearance.BorderSize = 1;
            btnTaskRequests.Name = "btnTaskRequests";
            btnTaskRequests.Click += new System.EventHandler(btnTaskRequests_Click);
            //
            // layoutContent4
            //
            layoutContent4.Text = "";
            layoutContent4.Location = new System.Drawing.Point(0, 49);
            layoutContent4.Size = new System.Drawing.Size(1045, 511);
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
            layoutContent4.TabIndex = 1;
            layoutContent4.TabStop = false;
            layoutContent4.AutoScroll = false;
            layoutContent4.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent4.ColumnCount = 2;
            layoutContent4.RowCount = 1;
            layoutContent4.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent4.Name = "layoutContent4";
            layoutContent4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 60F));
            layoutContent4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 40F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent4.Controls.Add(panelContent2, 0, 0);
            layoutContent4.Controls.Add(layoutContent6, 1, 0);
            //
            // panelContent2
            //
            panelContent2.Text = "";
            panelContent2.Location = new System.Drawing.Point(0, 0);
            panelContent2.Size = new System.Drawing.Size(617, 501);
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
            panelContent2.TabIndex = 0;
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
            layoutContent5.Size = new System.Drawing.Size(583, 467);
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
            layoutContent5.Controls.Add(lblTaskListTitle, 0, 0);
            layoutContent5.Controls.Add(gridTasks, 0, 1);
            //
            // lblTaskListTitle
            //
            lblTaskListTitle.Text = "전체 업무";
            lblTaskListTitle.Location = new System.Drawing.Point(0, 4);
            lblTaskListTitle.Size = new System.Drawing.Size(112, 19);
            lblTaskListTitle.MinimumSize = new System.Drawing.Size(0, 0);
            lblTaskListTitle.MaximumSize = new System.Drawing.Size(0, 0);
            lblTaskListTitle.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblTaskListTitle.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblTaskListTitle.Dock = System.Windows.Forms.DockStyle.None;
            lblTaskListTitle.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblTaskListTitle.AutoSize = true;
            lblTaskListTitle.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblTaskListTitle.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblTaskListTitle.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            lblTaskListTitle.TabIndex = 0;
            lblTaskListTitle.TabStop = false;
            lblTaskListTitle.AutoEllipsis = false;
            lblTaskListTitle.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            lblTaskListTitle.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblTaskListTitle.UseCompatibleTextRendering = false;
            lblTaskListTitle.Name = "lblTaskListTitle";
            //
            // gridTasks
            //
            gridTasks.Text = "";
            gridTasks.Location = new System.Drawing.Point(3, 37);
            gridTasks.Size = new System.Drawing.Size(577, 427);
            gridTasks.MinimumSize = new System.Drawing.Size(0, 140);
            gridTasks.MaximumSize = new System.Drawing.Size(0, 0);
            gridTasks.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            gridTasks.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            gridTasks.Dock = System.Windows.Forms.DockStyle.Fill;
            gridTasks.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            gridTasks.AutoSize = false;
            gridTasks.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            gridTasks.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            gridTasks.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            gridTasks.TabIndex = 1;
            gridTasks.TabStop = true;
            gridTasks.ReadOnly = true;
            gridTasks.AllowUserToAddRows = false;
            gridTasks.AllowUserToDeleteRows = false;
            gridTasks.AllowUserToOrderColumns = false;
            gridTasks.AllowUserToResizeRows = false;
            gridTasks.RowHeadersVisible = false;
            gridTasks.MultiSelect = false;
            gridTasks.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            gridTasks.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            gridTasks.BackgroundColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            gridTasks.BorderStyle = System.Windows.Forms.BorderStyle.None;
            gridTasks.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            gridTasks.GridColor = System.Drawing.Color.FromArgb(255, 235, 239, 246);
            gridTasks.EnableHeadersVisualStyles = false;
            gridTasks.ColumnHeadersHeight = 36;
            gridTasks.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            gridTasks.RowTemplate.Height = 38;
            gridTasks.Name = "gridTasks";
            columnGridTasks1.Name = "columnGridTasks1";
            columnGridTasks1.HeaderText = "업무 번호";
            columnGridTasks1.MinimumWidth = 106;
            columnGridTasks1.Width = 106;
            columnGridTasks1.FillWeight = 100F;
            columnGridTasks1.ReadOnly = true;
            columnGridTasks1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridTasks1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridTasks2.Name = "columnGridTasks2";
            columnGridTasks2.HeaderText = "업무 제목";
            columnGridTasks2.MinimumWidth = 168;
            columnGridTasks2.Width = 168;
            columnGridTasks2.FillWeight = 100F;
            columnGridTasks2.ReadOnly = true;
            columnGridTasks2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridTasks2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridTasks3.Name = "columnGridTasks3";
            columnGridTasks3.HeaderText = "담당자";
            columnGridTasks3.MinimumWidth = 82;
            columnGridTasks3.Width = 82;
            columnGridTasks3.FillWeight = 100F;
            columnGridTasks3.ReadOnly = true;
            columnGridTasks3.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridTasks3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridTasks4.Name = "columnGridTasks4";
            columnGridTasks4.HeaderText = "상태";
            columnGridTasks4.MinimumWidth = 82;
            columnGridTasks4.Width = 82;
            columnGridTasks4.FillWeight = 100F;
            columnGridTasks4.ReadOnly = true;
            columnGridTasks4.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridTasks4.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridTasks5.Name = "columnGridTasks5";
            columnGridTasks5.HeaderText = "우선순위";
            columnGridTasks5.MinimumWidth = 82;
            columnGridTasks5.Width = 82;
            columnGridTasks5.FillWeight = 100F;
            columnGridTasks5.ReadOnly = true;
            columnGridTasks5.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridTasks5.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridTasks6.Name = "columnGridTasks6";
            columnGridTasks6.HeaderText = "시작일";
            columnGridTasks6.MinimumWidth = 106;
            columnGridTasks6.Width = 106;
            columnGridTasks6.FillWeight = 100F;
            columnGridTasks6.ReadOnly = true;
            columnGridTasks6.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridTasks6.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridTasks7.Name = "columnGridTasks7";
            columnGridTasks7.HeaderText = "마감일";
            columnGridTasks7.MinimumWidth = 106;
            columnGridTasks7.Width = 106;
            columnGridTasks7.FillWeight = 100F;
            columnGridTasks7.ReadOnly = true;
            columnGridTasks7.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridTasks7.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
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
            gridTasks.ColumnHeadersDefaultCellStyle = headerStyle1;
            gridTasks.DefaultCellStyle = cellStyle1;
            gridTasks.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { columnGridTasks1, columnGridTasks2, columnGridTasks3, columnGridTasks4, columnGridTasks5, columnGridTasks6, columnGridTasks7 });
            //
            // layoutContent6
            //
            layoutContent6.Text = "";
            layoutContent6.Location = new System.Drawing.Point(627, 0);
            layoutContent6.Size = new System.Drawing.Size(418, 511);
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
            layoutContent6.AutoScroll = false;
            layoutContent6.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent6.ColumnCount = 1;
            layoutContent6.RowCount = 2;
            layoutContent6.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent6.Name = "layoutContent6";
            layoutContent6.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent6.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 218F));
            layoutContent6.Controls.Add(panelContent3, 0, 0);
            layoutContent6.Controls.Add(panelContent4, 0, 1);
            //
            // panelContent3
            //
            panelContent3.Text = "";
            panelContent3.Location = new System.Drawing.Point(0, 0);
            panelContent3.Size = new System.Drawing.Size(408, 283);
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
            layoutContent7.Size = new System.Drawing.Size(374, 249);
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
            layoutContent7.Controls.Add(lblText3, 0, 0);
            layoutContent7.Controls.Add(txtContent, 0, 1);
            //
            // lblText3
            //
            lblText3.Text = "업무 상세";
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
            txtContent.Text = "TASK-001\r\n\r\n업무 제목   주간 업무 자료 정리\r\n담당자      김직원\r\n우선순위    높음\r\n현재 상태   진행중\r\n진행률      40%\r\n\r\n시작일      2026-10-15\r\n현재 마감일 2026-10-22\r\n변경 마감일 —\r\n\r\n상세 내용\r\n주간 업무 자료를 모아 항목별로 정리합니다.";
            txtContent.Location = new System.Drawing.Point(3, 37);
            txtContent.Size = new System.Drawing.Size(368, 209);
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
            panelContent4.Location = new System.Drawing.Point(0, 293);
            panelContent4.Size = new System.Drawing.Size(408, 208);
            panelContent4.MinimumSize = new System.Drawing.Size(0, 208);
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
            panelContent4.Controls.Add(layoutContent8);
            //
            // layoutContent8
            //
            layoutContent8.Text = "";
            layoutContent8.Location = new System.Drawing.Point(16, 16);
            layoutContent8.Size = new System.Drawing.Size(374, 174);
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
            layoutContent8.Controls.Add(lblText4, 0, 0);
            layoutContent8.Controls.Add(gridTaskAttachments, 0, 1);
            //
            // lblText4
            //
            lblText4.Text = "업무 첨부파일";
            lblText4.Location = new System.Drawing.Point(0, 4);
            lblText4.Size = new System.Drawing.Size(98, 19);
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
            // gridTaskAttachments
            //
            gridTaskAttachments.Text = "";
            gridTaskAttachments.Location = new System.Drawing.Point(3, 37);
            gridTaskAttachments.Size = new System.Drawing.Size(368, 140);
            gridTaskAttachments.MinimumSize = new System.Drawing.Size(0, 140);
            gridTaskAttachments.MaximumSize = new System.Drawing.Size(0, 0);
            gridTaskAttachments.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
            gridTaskAttachments.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            gridTaskAttachments.Dock = System.Windows.Forms.DockStyle.Fill;
            gridTaskAttachments.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            gridTaskAttachments.AutoSize = false;
            gridTaskAttachments.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            gridTaskAttachments.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            gridTaskAttachments.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            gridTaskAttachments.TabIndex = 1;
            gridTaskAttachments.TabStop = true;
            gridTaskAttachments.ReadOnly = true;
            gridTaskAttachments.AllowUserToAddRows = false;
            gridTaskAttachments.AllowUserToDeleteRows = false;
            gridTaskAttachments.AllowUserToOrderColumns = false;
            gridTaskAttachments.AllowUserToResizeRows = false;
            gridTaskAttachments.RowHeadersVisible = false;
            gridTaskAttachments.MultiSelect = false;
            gridTaskAttachments.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            gridTaskAttachments.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            gridTaskAttachments.BackgroundColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            gridTaskAttachments.BorderStyle = System.Windows.Forms.BorderStyle.None;
            gridTaskAttachments.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            gridTaskAttachments.GridColor = System.Drawing.Color.FromArgb(255, 235, 239, 246);
            gridTaskAttachments.EnableHeadersVisualStyles = false;
            gridTaskAttachments.ColumnHeadersHeight = 36;
            gridTaskAttachments.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            gridTaskAttachments.RowTemplate.Height = 38;
            gridTaskAttachments.Name = "gridTaskAttachments";
            columnGridTaskAttachments1.Name = "columnGridTaskAttachments1";
            columnGridTaskAttachments1.HeaderText = "첨부파일";
            columnGridTaskAttachments1.MinimumWidth = 168;
            columnGridTaskAttachments1.Width = 168;
            columnGridTaskAttachments1.FillWeight = 100F;
            columnGridTaskAttachments1.ReadOnly = true;
            columnGridTaskAttachments1.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridTaskAttachments1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridTaskAttachments2.Name = "columnGridTaskAttachments2";
            columnGridTaskAttachments2.HeaderText = "크기";
            columnGridTaskAttachments2.MinimumWidth = 82;
            columnGridTaskAttachments2.Width = 100;
            columnGridTaskAttachments2.FillWeight = 100F;
            columnGridTaskAttachments2.ReadOnly = true;
            columnGridTaskAttachments2.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridTaskAttachments2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
            columnGridTaskAttachments3.Name = "columnGridTaskAttachments3";
            columnGridTaskAttachments3.HeaderText = "등록자";
            columnGridTaskAttachments3.MinimumWidth = 82;
            columnGridTaskAttachments3.Width = 100;
            columnGridTaskAttachments3.FillWeight = 100F;
            columnGridTaskAttachments3.ReadOnly = true;
            columnGridTaskAttachments3.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnGridTaskAttachments3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.NotSet;
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
            gridTaskAttachments.ColumnHeadersDefaultCellStyle = headerStyle2;
            gridTaskAttachments.DefaultCellStyle = cellStyle2;
            gridTaskAttachments.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { columnGridTaskAttachments1, columnGridTaskAttachments2, columnGridTaskAttachments3 });
            //
            // TasksControl
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
            Name = "TasksControl";
            Controls.Add(layoutContent);
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ((System.ComponentModel.ISupportInitialize)gridTaskAttachments).EndInit();
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
            ((System.ComponentModel.ISupportInitialize)gridTasks).EndInit();
            layoutContent5.ResumeLayout(false);
            layoutContent5.PerformLayout();
            panelContent2.ResumeLayout(false);
            panelContent2.PerformLayout();
            layoutContent4.ResumeLayout(false);
            layoutContent4.PerformLayout();
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
        private System.Windows.Forms.Button btnNewTaskPreview;
        private System.Windows.Forms.Button btnEditTaskPreview;
        private System.Windows.Forms.Button btnCancelTask;
        private System.Windows.Forms.Button btnAcceptTask;
        private System.Windows.Forms.Button btnReviewRequestPreview;
        private System.Windows.Forms.Button btnDeadlineExtensionPreview;
        private System.Windows.Forms.Button btnCollaborationRequest;
        private System.Windows.Forms.Button btnTaskRequests;
        private System.Windows.Forms.TableLayoutPanel layoutContent4;
        private System.Windows.Forms.Panel panelContent2;
        private System.Windows.Forms.TableLayoutPanel layoutContent5;
        private System.Windows.Forms.Label lblTaskListTitle;
        private System.Windows.Forms.DataGridView gridTasks;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridTasks1;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridTasks2;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridTasks3;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridTasks4;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridTasks5;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridTasks6;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridTasks7;
        private System.Windows.Forms.TableLayoutPanel layoutContent6;
        private System.Windows.Forms.Panel panelContent3;
        private System.Windows.Forms.TableLayoutPanel layoutContent7;
        private System.Windows.Forms.Label lblText3;
        private System.Windows.Forms.TextBox txtContent;
        private System.Windows.Forms.Panel panelContent4;
        private System.Windows.Forms.TableLayoutPanel layoutContent8;
        private System.Windows.Forms.Label lblText4;
        private System.Windows.Forms.DataGridView gridTaskAttachments;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridTaskAttachments1;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridTaskAttachments2;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnGridTaskAttachments3;
    }
}
