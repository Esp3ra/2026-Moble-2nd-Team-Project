#nullable disable

namespace RemoteWork.WinForms.Features.Tasks
{
    partial class TaskRequestsForm
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
            System.Windows.Forms.DataGridViewCellStyle headerStyle = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle cellStyle = new System.Windows.Forms.DataGridViewCellStyle();
            layoutRequests = new System.Windows.Forms.TableLayoutPanel();
            lblPageTitle = new System.Windows.Forms.Label();
            groupRequests = new System.Windows.Forms.GroupBox();
            gridRequests = new System.Windows.Forms.DataGridView();
            columnRequestType = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnTask = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnRequester = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnRequestedAt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            columnRequestStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            layoutDetails = new System.Windows.Forms.TableLayoutPanel();
            groupDeadlineRequest = new System.Windows.Forms.GroupBox();
            txtDeadlineRequest = new System.Windows.Forms.TextBox();
            groupReviewRequest = new System.Windows.Forms.GroupBox();
            txtReviewRequest = new System.Windows.Forms.TextBox();
            flowActions = new System.Windows.Forms.FlowLayoutPanel();
            btnClose = new System.Windows.Forms.Button();
            btnConfirm = new System.Windows.Forms.Button();
            layoutRequests.SuspendLayout();
            groupRequests.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)gridRequests).BeginInit();
            layoutDetails.SuspendLayout();
            groupDeadlineRequest.SuspendLayout();
            groupReviewRequest.SuspendLayout();
            flowActions.SuspendLayout();
            SuspendLayout();
            //
            // layoutRequests
            //
            layoutRequests.ColumnCount = 1;
            layoutRequests.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutRequests.RowCount = 4;
            layoutRequests.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 56F));
            layoutRequests.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 48F));
            layoutRequests.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 52F));
            layoutRequests.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            layoutRequests.Dock = System.Windows.Forms.DockStyle.Fill;
            layoutRequests.Margin = new System.Windows.Forms.Padding(0);
            layoutRequests.Name = "layoutRequests";
            layoutRequests.Size = new System.Drawing.Size(956, 656);
            layoutRequests.Controls.Add(lblPageTitle, 0, 0);
            layoutRequests.Controls.Add(groupRequests, 0, 1);
            layoutRequests.Controls.Add(layoutDetails, 0, 2);
            layoutRequests.Controls.Add(flowActions, 0, 3);
            //
            // lblPageTitle
            //
            lblPageTitle.AutoSize = true;
            lblPageTitle.Font = new System.Drawing.Font("맑은 고딕", 20F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblPageTitle.ForeColor = System.Drawing.Color.FromArgb(30, 42, 61);
            lblPageTitle.Margin = new System.Windows.Forms.Padding(0, 3, 0, 12);
            lblPageTitle.Name = "lblPageTitle";
            lblPageTitle.Text = "업무 요청 확인";
            lblPageTitle.TabIndex = 0;
            //
            // groupRequests
            //
            groupRequests.BackColor = System.Drawing.Color.White;
            groupRequests.Dock = System.Windows.Forms.DockStyle.Fill;
            groupRequests.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
            groupRequests.Padding = new System.Windows.Forms.Padding(14, 12, 14, 12);
            groupRequests.Name = "groupRequests";
            groupRequests.Text = "마감 연장 및 검토 요청 목록";
            groupRequests.TabIndex = 1;
            groupRequests.Controls.Add(gridRequests);
            //
            // gridRequests
            //
            gridRequests.AllowUserToAddRows = false;
            gridRequests.AllowUserToDeleteRows = false;
            gridRequests.AllowUserToOrderColumns = false;
            gridRequests.AllowUserToResizeRows = false;
            gridRequests.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            gridRequests.BackgroundColor = System.Drawing.Color.White;
            gridRequests.BorderStyle = System.Windows.Forms.BorderStyle.None;
            gridRequests.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.SingleHorizontal;
            gridRequests.ColumnHeadersHeight = 36;
            gridRequests.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            gridRequests.Dock = System.Windows.Forms.DockStyle.Fill;
            gridRequests.EnableHeadersVisualStyles = false;
            gridRequests.GridColor = System.Drawing.Color.FromArgb(235, 239, 246);
            gridRequests.MultiSelect = false;
            gridRequests.Name = "gridRequests";
            gridRequests.ReadOnly = true;
            gridRequests.RowHeadersVisible = false;
            gridRequests.RowTemplate.Height = 38;
            gridRequests.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            gridRequests.TabIndex = 0;
            headerStyle.BackColor = System.Drawing.Color.FromArgb(243, 246, 251);
            headerStyle.ForeColor = System.Drawing.Color.FromArgb(103, 116, 135);
            headerStyle.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            headerStyle.Padding = new System.Windows.Forms.Padding(6);
            gridRequests.ColumnHeadersDefaultCellStyle = headerStyle;
            cellStyle.BackColor = System.Drawing.Color.White;
            cellStyle.ForeColor = System.Drawing.Color.FromArgb(30, 42, 61);
            cellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(231, 239, 255);
            cellStyle.SelectionForeColor = System.Drawing.Color.FromArgb(30, 42, 61);
            cellStyle.Padding = new System.Windows.Forms.Padding(6);
            gridRequests.DefaultCellStyle = cellStyle;
            columnRequestType.HeaderText = "요청 유형";
            columnRequestType.MinimumWidth = 110;
            columnRequestType.Name = "columnRequestType";
            columnRequestType.ReadOnly = true;
            columnRequestType.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnTask.HeaderText = "대상 업무";
            columnTask.MinimumWidth = 260;
            columnTask.FillWeight = 210F;
            columnTask.Name = "columnTask";
            columnTask.ReadOnly = true;
            columnTask.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnRequester.HeaderText = "요청자";
            columnRequester.MinimumWidth = 90;
            columnRequester.Name = "columnRequester";
            columnRequester.ReadOnly = true;
            columnRequester.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnRequestedAt.HeaderText = "요청일";
            columnRequestedAt.MinimumWidth = 160;
            columnRequestedAt.Name = "columnRequestedAt";
            columnRequestedAt.ReadOnly = true;
            columnRequestedAt.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            columnRequestStatus.HeaderText = "처리 상태";
            columnRequestStatus.MinimumWidth = 100;
            columnRequestStatus.Name = "columnRequestStatus";
            columnRequestStatus.ReadOnly = true;
            columnRequestStatus.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            gridRequests.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] { columnRequestType, columnTask, columnRequester, columnRequestedAt, columnRequestStatus });
            //
            // layoutDetails
            //
            layoutDetails.ColumnCount = 2;
            layoutDetails.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            layoutDetails.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 50F));
            layoutDetails.RowCount = 1;
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutDetails.Dock = System.Windows.Forms.DockStyle.Fill;
            layoutDetails.Margin = new System.Windows.Forms.Padding(0);
            layoutDetails.Name = "layoutDetails";
            layoutDetails.TabIndex = 2;
            layoutDetails.Controls.Add(groupDeadlineRequest, 0, 0);
            layoutDetails.Controls.Add(groupReviewRequest, 1, 0);
            //
            // groupDeadlineRequest
            //
            groupDeadlineRequest.BackColor = System.Drawing.Color.White;
            groupDeadlineRequest.Dock = System.Windows.Forms.DockStyle.Fill;
            groupDeadlineRequest.Margin = new System.Windows.Forms.Padding(0, 0, 6, 0);
            groupDeadlineRequest.Padding = new System.Windows.Forms.Padding(14);
            groupDeadlineRequest.Name = "groupDeadlineRequest";
            groupDeadlineRequest.Text = "마감 연장 요청 내용";
            groupDeadlineRequest.Controls.Add(txtDeadlineRequest);
            //
            // txtDeadlineRequest
            //
            txtDeadlineRequest.BackColor = System.Drawing.Color.White;
            txtDeadlineRequest.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtDeadlineRequest.Dock = System.Windows.Forms.DockStyle.Fill;
            txtDeadlineRequest.ForeColor = System.Drawing.Color.FromArgb(30, 42, 61);
            txtDeadlineRequest.Multiline = true;
            txtDeadlineRequest.Name = "txtDeadlineRequest";
            txtDeadlineRequest.ReadOnly = true;
            txtDeadlineRequest.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtDeadlineRequest.TabStop = false;
            txtDeadlineRequest.Text = "TASK-001 · 주간 업무 자료 정리\r\n요청자: 김직원\r\n현재 마감일: 2026-10-22\r\n요청 마감일: 2026-10-26\r\n\r\n연장 사유\r\n추가 참고 자료를 반영하기 위해 작업 기간 연장을 요청합니다.\r\n\r\n처리 상태: 확인 대기";
            //
            // groupReviewRequest
            //
            groupReviewRequest.BackColor = System.Drawing.Color.White;
            groupReviewRequest.Dock = System.Windows.Forms.DockStyle.Fill;
            groupReviewRequest.Margin = new System.Windows.Forms.Padding(6, 0, 0, 0);
            groupReviewRequest.Padding = new System.Windows.Forms.Padding(14);
            groupReviewRequest.Name = "groupReviewRequest";
            groupReviewRequest.Text = "검토 요청 내용";
            groupReviewRequest.Controls.Add(txtReviewRequest);
            //
            // txtReviewRequest
            //
            txtReviewRequest.BackColor = System.Drawing.Color.White;
            txtReviewRequest.BorderStyle = System.Windows.Forms.BorderStyle.None;
            txtReviewRequest.Dock = System.Windows.Forms.DockStyle.Fill;
            txtReviewRequest.ForeColor = System.Drawing.Color.FromArgb(30, 42, 61);
            txtReviewRequest.Multiline = true;
            txtReviewRequest.Name = "txtReviewRequest";
            txtReviewRequest.ReadOnly = true;
            txtReviewRequest.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtReviewRequest.TabStop = false;
            txtReviewRequest.Text = "TASK-001 · 주간 업무 자료 정리\r\n요청자: 김직원\r\n진행상태: 진행중\r\n현재 마감일: 2026-10-22\r\n\r\n검토 요청 내용\r\n업무 자료의 정리 방향을 마감 전에 검토해 주세요.\r\n\r\n처리 상태: 확인 대기";
            //
            // flowActions
            //
            flowActions.Dock = System.Windows.Forms.DockStyle.Fill;
            flowActions.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            flowActions.Margin = new System.Windows.Forms.Padding(0);
            flowActions.Padding = new System.Windows.Forms.Padding(0, 12, 0, 0);
            flowActions.Name = "flowActions";
            flowActions.WrapContents = false;
            flowActions.TabIndex = 3;
            flowActions.Controls.Add(btnConfirm);
            flowActions.Controls.Add(btnClose);
            //
            // btnConfirm
            //
            btnConfirm.BackColor = System.Drawing.Color.FromArgb(52, 101, 224);
            btnConfirm.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnConfirm.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(52, 101, 224);
            btnConfirm.ForeColor = System.Drawing.Color.White;
            btnConfirm.Margin = new System.Windows.Forms.Padding(8, 0, 0, 0);
            btnConfirm.Name = "btnConfirm";
            btnConfirm.Size = new System.Drawing.Size(100, 34);
            btnConfirm.Text = "요청 확인";
            btnConfirm.UseVisualStyleBackColor = false;
            btnConfirm.Click += new System.EventHandler(Unavailable_Click);
            //
            // btnClose
            //
            btnClose.BackColor = System.Drawing.Color.White;
            btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnClose.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(213, 220, 231);
            btnClose.ForeColor = System.Drawing.Color.FromArgb(30, 42, 61);
            btnClose.Margin = new System.Windows.Forms.Padding(8, 0, 0, 0);
            btnClose.Name = "btnClose";
            btnClose.Size = new System.Drawing.Size(88, 34);
            btnClose.Text = "닫기";
            btnClose.UseVisualStyleBackColor = false;
            btnClose.Click += new System.EventHandler(ClosePreview_Click);
            //
            // TaskRequestsForm
            //
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(242, 245, 250);
            CancelButton = btnClose;
            ClientSize = new System.Drawing.Size(1000, 700);
            Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            ForeColor = System.Drawing.Color.FromArgb(30, 42, 61);
            MinimumSize = new System.Drawing.Size(900, 650);
            MinimizeBox = false;
            Name = "TaskRequestsForm";
            Padding = new System.Windows.Forms.Padding(22);
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "업무 요청 확인";
            Controls.Add(layoutRequests);
            flowActions.ResumeLayout(false);
            groupReviewRequest.ResumeLayout(false);
            groupReviewRequest.PerformLayout();
            groupDeadlineRequest.ResumeLayout(false);
            groupDeadlineRequest.PerformLayout();
            layoutDetails.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)gridRequests).EndInit();
            groupRequests.ResumeLayout(false);
            layoutRequests.ResumeLayout(false);
            layoutRequests.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel layoutRequests;
        private System.Windows.Forms.Label lblPageTitle;
        private System.Windows.Forms.GroupBox groupRequests;
        private System.Windows.Forms.DataGridView gridRequests;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnRequestType;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnTask;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnRequester;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnRequestedAt;
        private System.Windows.Forms.DataGridViewTextBoxColumn columnRequestStatus;
        private System.Windows.Forms.TableLayoutPanel layoutDetails;
        private System.Windows.Forms.GroupBox groupDeadlineRequest;
        private System.Windows.Forms.TextBox txtDeadlineRequest;
        private System.Windows.Forms.GroupBox groupReviewRequest;
        private System.Windows.Forms.TextBox txtReviewRequest;
        private System.Windows.Forms.FlowLayoutPanel flowActions;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnConfirm;
    }
}
