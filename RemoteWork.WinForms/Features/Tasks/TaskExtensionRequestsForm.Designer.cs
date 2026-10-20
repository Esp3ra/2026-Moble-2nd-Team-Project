#nullable disable
namespace RemoteWork.WinForms.Features.Tasks
{
    partial class TaskExtensionRequestsForm
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        { if (disposing) { StopOwnedQueries(); components?.Dispose(); } base.Dispose(disposing); }
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container(); layout = new System.Windows.Forms.TableLayoutPanel();
            filters = new System.Windows.Forms.FlowLayoutPanel(); cmbScope = new System.Windows.Forms.ComboBox(); cmbStatus = new System.Windows.Forms.ComboBox();
            btnRefresh = new System.Windows.Forms.Button(); gridRequests = new System.Windows.Forms.DataGridView();
            colId = new System.Windows.Forms.DataGridViewTextBoxColumn(); colTask = new System.Windows.Forms.DataGridViewTextBoxColumn();
            colApplicant = new System.Windows.Forms.DataGridViewTextBoxColumn(); colOriginal = new System.Windows.Forms.DataGridViewTextBoxColumn();
            colRequested = new System.Windows.Forms.DataGridViewTextBoxColumn(); colStatus = new System.Windows.Forms.DataGridViewTextBoxColumn();
            txtDetail = new System.Windows.Forms.TextBox(); lblStatus = new System.Windows.Forms.Label(); actions = new System.Windows.Forms.FlowLayoutPanel();
            btnApprove = new System.Windows.Forms.Button(); btnReject = new System.Windows.Forms.Button(); btnWithdraw = new System.Windows.Forms.Button(); btnResult = new System.Windows.Forms.Button();
            SuspendLayout(); layout.SuspendLayout(); filters.SuspendLayout(); actions.SuspendLayout(); ((System.ComponentModel.ISupportInitialize)gridRequests).BeginInit();
            layout.Name = "layout"; layout.Dock = System.Windows.Forms.DockStyle.Fill; layout.Padding = new System.Windows.Forms.Padding(18); layout.ColumnCount = 1; layout.RowCount = 5;
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 100));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layout.Controls.Add(filters, 0, 0); layout.Controls.Add(gridRequests, 0, 1); layout.Controls.Add(txtDetail, 0, 2); layout.Controls.Add(lblStatus, 0, 3); layout.Controls.Add(actions, 0, 4);
            filters.Name = "filters"; filters.Dock = System.Windows.Forms.DockStyle.Fill; filters.AutoSize = true; filters.WrapContents = true;
            filters.Controls.Add(cmbScope); filters.Controls.Add(cmbStatus); filters.Controls.Add(btnRefresh);
            cmbScope.Name = "cmbScope"; cmbScope.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList; cmbScope.Items.AddRange(new object[] { "내 신청", "관리 업무 신청" }); cmbScope.Width = 160;
            cmbScope.SelectedIndexChanged += FilterChanged;
            cmbStatus.Name = "cmbStatus"; cmbStatus.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList; cmbStatus.Items.AddRange(new object[] { "전체", "대기", "승인", "반려", "철회", "업무 취소로 종료" }); cmbStatus.Width = 160;
            cmbStatus.SelectedIndexChanged += FilterChanged;
            btnRefresh.Name = "btnRefresh"; btnRefresh.Text = "선택 조건 조회"; btnRefresh.AutoSize = true; btnRefresh.MinimumSize = new System.Drawing.Size(120, 32); btnRefresh.Click += Refresh_Click;
            gridRequests.Name = "gridRequests"; gridRequests.Dock = System.Windows.Forms.DockStyle.Fill; gridRequests.ReadOnly = true; gridRequests.MultiSelect = false;
            gridRequests.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect; gridRequests.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            gridRequests.AllowUserToAddRows = false; gridRequests.AllowUserToDeleteRows = false; gridRequests.AllowUserToOrderColumns = false; gridRequests.RowHeadersVisible = false;
            gridRequests.Columns.AddRange(colId, colTask, colApplicant, colOriginal, colRequested, colStatus); gridRequests.SelectionChanged += SelectionChanged;
            colId.Name = "colId"; colId.HeaderText = "신청 번호"; colTask.Name = "colTask"; colTask.HeaderText = "업무 번호";
            colApplicant.Name = "colApplicant"; colApplicant.HeaderText = "신청자"; colOriginal.Name = "colOriginal"; colOriginal.HeaderText = "기존 마감";
            colRequested.Name = "colRequested"; colRequested.HeaderText = "요청 마감"; colStatus.Name = "colStatus"; colStatus.HeaderText = "신청 상태";
            txtDetail.Name = "txtDetail"; txtDetail.Dock = System.Windows.Forms.DockStyle.Fill; txtDetail.Multiline = true; txtDetail.ReadOnly = true; txtDetail.ScrollBars = System.Windows.Forms.ScrollBars.Vertical; txtDetail.Text = "신청을 선택하면 사유·판단 내용이 표시됩니다.";
            lblStatus.Name = "lblStatus"; lblStatus.AutoSize = true; lblStatus.Dock = System.Windows.Forms.DockStyle.Fill; lblStatus.MinimumSize = new System.Drawing.Size(0, 45); lblStatus.Text = "현재 로그인에 연결된 서버에서 조회합니다. 결과물 검토 요청함과 별도입니다.";
            actions.Name = "actions"; actions.Dock = System.Windows.Forms.DockStyle.Fill; actions.AutoSize = true; actions.WrapContents = true;
            actions.Controls.Add(btnApprove); actions.Controls.Add(btnReject); actions.Controls.Add(btnWithdraw); actions.Controls.Add(btnResult);
            btnApprove.Name = "btnApprove"; btnApprove.Text = "연장 승인"; btnApprove.AutoSize = true; btnApprove.MinimumSize = new System.Drawing.Size(100, 34); btnApprove.Click += Approve_Click;
            btnReject.Name = "btnReject"; btnReject.Text = "연장 반려"; btnReject.AutoSize = true; btnReject.MinimumSize = new System.Drawing.Size(100, 34); btnReject.Click += Reject_Click;
            btnWithdraw.Name = "btnWithdraw"; btnWithdraw.Text = "내 신청 철회"; btnWithdraw.AutoSize = true; btnWithdraw.MinimumSize = new System.Drawing.Size(100, 34); btnWithdraw.Click += Withdraw_Click;
            btnResult.Name = "btnResult"; btnResult.Text = "미확인 처리 결과"; btnResult.AutoSize = true; btnResult.MinimumSize = new System.Drawing.Size(130, 34); btnResult.Click += Result_Click;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F); AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Font = new System.Drawing.Font("맑은 고딕", 9F); ClientSize = new System.Drawing.Size(1000, 600); MinimumSize = new System.Drawing.Size(850, 500);
            Name = "TaskExtensionRequestsForm"; Text = "마감 연장 요청함"; StartPosition = System.Windows.Forms.FormStartPosition.CenterParent; Controls.Add(layout);
            ((System.ComponentModel.ISupportInitialize)gridRequests).EndInit(); actions.ResumeLayout(false); actions.PerformLayout(); filters.ResumeLayout(false); filters.PerformLayout(); layout.ResumeLayout(false); layout.PerformLayout(); ResumeLayout(false);
        }
        private System.Windows.Forms.TableLayoutPanel layout;
        private System.Windows.Forms.FlowLayoutPanel filters, actions;
        private System.Windows.Forms.ComboBox cmbScope, cmbStatus;
        private System.Windows.Forms.Button btnRefresh, btnApprove, btnReject, btnWithdraw, btnResult;
        private System.Windows.Forms.DataGridView gridRequests;
        private System.Windows.Forms.DataGridViewTextBoxColumn colId, colTask, colApplicant, colOriginal, colRequested, colStatus;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.TextBox txtDetail;
    }
}
