#nullable disable
namespace RemoteWork.WinForms.Features.Tasks
{
    partial class TaskLifecycleForm
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        { if (disposing) { StopOwnedQueries(); components?.Dispose(); } base.Dispose(disposing); }
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            layout = new System.Windows.Forms.TableLayoutPanel(); lblTarget = new System.Windows.Forms.Label();
            txtContext = new System.Windows.Forms.TextBox(); fields = new System.Windows.Forms.TableLayoutPanel();
            lblDate = new System.Windows.Forms.Label(); txtRequestedDueDate = new System.Windows.Forms.TextBox();
            lblReason = new System.Windows.Forms.Label(); txtReason = new System.Windows.Forms.TextBox();
            lblStatus = new System.Windows.Forms.Label(); actions = new System.Windows.Forms.FlowLayoutPanel();
            btnReload = new System.Windows.Forms.Button(); btnConfirm = new System.Windows.Forms.Button();
            btnCheckResult = new System.Windows.Forms.Button(); btnAcknowledge = new System.Windows.Forms.Button(); btnClose = new System.Windows.Forms.Button();
            SuspendLayout(); layout.SuspendLayout(); fields.SuspendLayout(); actions.SuspendLayout();
            layout.Name = "layout"; layout.Dock = System.Windows.Forms.DockStyle.Fill; layout.Padding = new System.Windows.Forms.Padding(20);
            layout.ColumnCount = 1; layout.RowCount = 5;
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 110));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layout.Controls.Add(lblTarget, 0, 0); layout.Controls.Add(txtContext, 0, 1); layout.Controls.Add(fields, 0, 2);
            layout.Controls.Add(lblStatus, 0, 3); layout.Controls.Add(actions, 0, 4);
            lblTarget.Name = "lblTarget"; lblTarget.AutoSize = true; lblTarget.Dock = System.Windows.Forms.DockStyle.Fill;
            lblTarget.Text = "업무 취소·마감 연장 · 현재 접속 및 업무 확인 필요"; lblTarget.Margin = new System.Windows.Forms.Padding(0, 0, 0, 12);
            txtContext.Name = "txtContext"; txtContext.Dock = System.Windows.Forms.DockStyle.Fill;
            txtContext.Multiline = true; txtContext.ReadOnly = true; txtContext.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtContext.Text = "현재 업무 확인 필요"; txtContext.Margin = new System.Windows.Forms.Padding(0, 0, 0, 14);
            fields.Name = "fields"; fields.Dock = System.Windows.Forms.DockStyle.Fill; fields.ColumnCount = 2; fields.RowCount = 2;
            fields.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 120));
            fields.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100));
            fields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            fields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));
            fields.Controls.Add(lblDate, 0, 0); fields.Controls.Add(txtRequestedDueDate, 1, 0);
            fields.Controls.Add(lblReason, 0, 1); fields.Controls.Add(txtReason, 1, 1);
            lblDate.Name = "lblDate"; lblDate.Text = "요청 마감일"; lblDate.AutoSize = true;
            txtRequestedDueDate.Name = "txtRequestedDueDate"; txtRequestedDueDate.Dock = System.Windows.Forms.DockStyle.Fill;
            txtRequestedDueDate.PlaceholderText = "YYYY-MM-DD"; txtRequestedDueDate.TextChanged += InputEdited;
            lblReason.Name = "lblReason"; lblReason.Text = "사유"; lblReason.AutoSize = true;
            txtReason.Name = "txtReason"; txtReason.Dock = System.Windows.Forms.DockStyle.Fill; txtReason.Multiline = true;
            txtReason.ScrollBars = System.Windows.Forms.ScrollBars.Vertical; txtReason.MaxLength = 0;
            txtReason.MinimumSize = new System.Drawing.Size(0, 100); txtReason.TextChanged += InputEdited;
            lblStatus.Name = "lblStatus"; lblStatus.AutoSize = true; lblStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            lblStatus.MinimumSize = new System.Drawing.Size(0, 65); lblStatus.Text = "연결된 서버에서 현재 업무를 조회해야 합니다. 샘플 처리 결과는 표시하지 않습니다.";
            actions.Name = "actions"; actions.AutoSize = true; actions.Dock = System.Windows.Forms.DockStyle.Fill; actions.WrapContents = true;
            actions.Controls.Add(btnReload); actions.Controls.Add(btnConfirm); actions.Controls.Add(btnCheckResult); actions.Controls.Add(btnAcknowledge); actions.Controls.Add(btnClose);
            btnReload.Name = "btnReload"; btnReload.Text = "최신 업무 조회"; btnReload.AutoSize = true; btnReload.MinimumSize = new System.Drawing.Size(110, 34); btnReload.Click += Reload_Click;
            btnConfirm.Name = "btnConfirm"; btnConfirm.Text = "입력·대상 확인"; btnConfirm.AutoSize = true; btnConfirm.MinimumSize = new System.Drawing.Size(110, 34); btnConfirm.Click += Confirm_Click;
            btnCheckResult.Name = "btnCheckResult"; btnCheckResult.Text = "처리 결과 확인"; btnCheckResult.AutoSize = true; btnCheckResult.MinimumSize = new System.Drawing.Size(110, 34); btnCheckResult.Click += CheckResult_Click;
            btnAcknowledge.Name = "btnAcknowledge"; btnAcknowledge.Text = "확인 후 닫기"; btnAcknowledge.AutoSize = true; btnAcknowledge.MinimumSize = new System.Drawing.Size(110, 34); btnAcknowledge.Click += Acknowledge_Click;
            btnClose.Name = "btnClose"; btnClose.Text = "닫기"; btnClose.AutoSize = true; btnClose.MinimumSize = new System.Drawing.Size(80, 34); btnClose.Click += Close_Click;
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F); AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(800, 530); MinimumSize = new System.Drawing.Size(760, 500);
            Font = new System.Drawing.Font("맑은 고딕", 9F); Name = "TaskLifecycleForm"; Text = "업무 취소·마감 연장";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent; Controls.Add(layout);
            actions.ResumeLayout(false); actions.PerformLayout(); fields.ResumeLayout(false); fields.PerformLayout();
            layout.ResumeLayout(false); layout.PerformLayout(); ResumeLayout(false);
        }
        private System.Windows.Forms.TableLayoutPanel layout, fields;
        private System.Windows.Forms.Label lblTarget, lblDate, lblReason, lblStatus;
        private System.Windows.Forms.TextBox txtContext, txtRequestedDueDate, txtReason;
        private System.Windows.Forms.FlowLayoutPanel actions;
        private System.Windows.Forms.Button btnReload, btnConfirm, btnCheckResult, btnAcknowledge, btnClose;
    }
}
