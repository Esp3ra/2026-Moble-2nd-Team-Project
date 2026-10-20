#nullable disable
namespace RemoteWork.WinForms.Features.Tasks
{
    partial class TaskManagementForm
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing) { StopOwnedQueries(); components?.Dispose(); }
            base.Dispose(disposing);
        }
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            layout = new System.Windows.Forms.TableLayoutPanel();
            lblTarget = new System.Windows.Forms.Label();
            fields = new System.Windows.Forms.TableLayoutPanel();
            lblTitle = new System.Windows.Forms.Label(); txtTitle = new System.Windows.Forms.TextBox();
            lblDescription = new System.Windows.Forms.Label(); txtDescription = new System.Windows.Forms.TextBox();
            lblStart = new System.Windows.Forms.Label(); txtStartDate = new System.Windows.Forms.TextBox();
            lblDue = new System.Windows.Forms.Label(); txtDueDate = new System.Windows.Forms.TextBox();
            lblSelection = new System.Windows.Forms.Label(); lblStatus = new System.Windows.Forms.Label();
            actions = new System.Windows.Forms.FlowLayoutPanel();
            btnChooseCandidates = new System.Windows.Forms.Button(); btnReload = new System.Windows.Forms.Button();
            btnConfirm = new System.Windows.Forms.Button(); btnCheckResult = new System.Windows.Forms.Button();
            btnAcknowledge = new System.Windows.Forms.Button(); btnClose = new System.Windows.Forms.Button();
            layout.Name = "layout";
            fields.Name = "fields";
            actions.Name = "actions";
            lblTarget.Name = "lblTarget";
            lblTitle.Name = "lblTitle";
            lblDescription.Name = "lblDescription";
            lblStart.Name = "lblStart";
            lblDue.Name = "lblDue";
            lblSelection.Name = "lblSelection";
            lblStatus.Name = "lblStatus";
            btnChooseCandidates.Name = "btnChooseCandidates";
            btnReload.Name = "btnReload";
            btnConfirm.Name = "btnConfirm";
            btnCheckResult.Name = "btnCheckResult";
            btnAcknowledge.Name = "btnAcknowledge";
            btnClose.Name = "btnClose";
            SuspendLayout(); layout.SuspendLayout(); fields.SuspendLayout(); actions.SuspendLayout();
            layout.Dock = System.Windows.Forms.DockStyle.Fill; layout.Padding = new System.Windows.Forms.Padding(20);
            layout.ColumnCount = 1; layout.RowCount = 5;
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layout.Controls.Add(lblTarget, 0, 0); layout.Controls.Add(fields, 0, 1);
            layout.Controls.Add(lblSelection, 0, 2); layout.Controls.Add(lblStatus, 0, 3); layout.Controls.Add(actions, 0, 4);
            lblTarget.AutoSize = true; lblTarget.Dock = System.Windows.Forms.DockStyle.Fill;
            lblTarget.Margin = new System.Windows.Forms.Padding(0, 0, 0, 14); lblTarget.Text = "현재 업무·변경 구분 확인 필요";
            fields.Dock = System.Windows.Forms.DockStyle.Fill; fields.ColumnCount = 2; fields.RowCount = 4;
            fields.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 110));
            fields.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100));
            fields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            fields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100));
            fields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            fields.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            fields.Controls.Add(lblTitle, 0, 0); fields.Controls.Add(txtTitle, 1, 0);
            fields.Controls.Add(lblDescription, 0, 1); fields.Controls.Add(txtDescription, 1, 1);
            fields.Controls.Add(lblStart, 0, 2); fields.Controls.Add(txtStartDate, 1, 2);
            fields.Controls.Add(lblDue, 0, 3); fields.Controls.Add(txtDueDate, 1, 3);
            lblTitle.AutoSize = true;
            lblDescription.AutoSize = true;
            lblStart.AutoSize = true;
            lblDue.AutoSize = true;
            lblTitle.Text = "업무 제목"; lblDescription.Text = "업무 내용"; lblStart.Text = "시작일"; lblDue.Text = "마감일";
            txtTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            txtDescription.Dock = System.Windows.Forms.DockStyle.Fill;
            txtStartDate.Dock = System.Windows.Forms.DockStyle.Fill;
            txtDueDate.Dock = System.Windows.Forms.DockStyle.Fill;
            txtDescription.Multiline = true; txtDescription.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtDescription.MinimumSize = new System.Drawing.Size(0, 120); txtDescription.MaxLength = 0;
            txtTitle.MaxLength = 0;
            txtStartDate.PlaceholderText = "YYYY-MM-DD";
            txtDueDate.PlaceholderText = "YYYY-MM-DD";
            txtTitle.Name = "txtTitle"; txtDescription.Name = "txtDescription";
            txtStartDate.Name = "txtStartDate"; txtDueDate.Name = "txtDueDate";
            txtTitle.TextChanged += InputEdited; txtDescription.TextChanged += InputEdited;
            txtStartDate.TextChanged += InputEdited; txtDueDate.TextChanged += InputEdited;
            lblSelection.AutoSize = true; lblSelection.Dock = System.Windows.Forms.DockStyle.Fill;
            lblSelection.Margin = new System.Windows.Forms.Padding(0, 12, 0, 8);
            lblSelection.Text = "담당자·관리자 선택 전";
            lblStatus.AutoSize = true; lblStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            lblStatus.Margin = new System.Windows.Forms.Padding(0, 8, 0, 12); lblStatus.Text = "변경 연결 필요";
            actions.Dock = System.Windows.Forms.DockStyle.Fill; actions.AutoSize = true; actions.WrapContents = true;
            actions.Controls.Add(btnChooseCandidates); actions.Controls.Add(btnReload); actions.Controls.Add(btnConfirm);
            actions.Controls.Add(btnCheckResult); actions.Controls.Add(btnAcknowledge); actions.Controls.Add(btnClose);
            btnChooseCandidates.Text = "담당자·관리자 선택"; btnReload.Text = "최신 업무 다시 조회";
            btnConfirm.Text = "입력·대상 확인"; btnCheckResult.Text = "처리 결과 확인";
            btnAcknowledge.Text = "성공 확인"; btnClose.Text = "닫기";
            btnChooseCandidates.AutoSize = true;
            btnReload.AutoSize = true;
            btnConfirm.AutoSize = true;
            btnCheckResult.AutoSize = true;
            btnAcknowledge.AutoSize = true;
            btnClose.AutoSize = true;
            btnChooseCandidates.Padding = new System.Windows.Forms.Padding(8, 5, 8, 5);
            btnReload.Padding = new System.Windows.Forms.Padding(8, 5, 8, 5);
            btnConfirm.Padding = new System.Windows.Forms.Padding(8, 5, 8, 5);
            btnCheckResult.Padding = new System.Windows.Forms.Padding(8, 5, 8, 5);
            btnAcknowledge.Padding = new System.Windows.Forms.Padding(8, 5, 8, 5);
            btnClose.Padding = new System.Windows.Forms.Padding(8, 5, 8, 5);
            btnChooseCandidates.Click += ChooseCandidates_Click; btnReload.Click += Reload_Click;
            btnConfirm.Click += Confirm_Click; btnCheckResult.Click += CheckResult_Click;
            btnAcknowledge.Click += Acknowledge_Click; btnClose.Click += Close_Click;
            Controls.Add(layout); AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            Font = new System.Drawing.Font("맑은 고딕", 9F); MinimumSize = new System.Drawing.Size(820, 540);
            ClientSize = new System.Drawing.Size(920, 620); StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Name = "TaskManagementForm"; Text = "업무 변경"; CancelButton = btnClose;
            actions.ResumeLayout(false); actions.PerformLayout(); fields.ResumeLayout(false); fields.PerformLayout();
            layout.ResumeLayout(false); layout.PerformLayout(); ResumeLayout(false);
        }
        private System.Windows.Forms.TableLayoutPanel layout;
        private System.Windows.Forms.TableLayoutPanel fields;
        private System.Windows.Forms.FlowLayoutPanel actions;
        private System.Windows.Forms.Label lblTarget;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.Label lblStart;
        private System.Windows.Forms.Label lblDue;
        private System.Windows.Forms.Label lblSelection;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.TextBox txtTitle;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.TextBox txtStartDate;
        private System.Windows.Forms.TextBox txtDueDate;
        private System.Windows.Forms.Button btnChooseCandidates;
        private System.Windows.Forms.Button btnReload;
        private System.Windows.Forms.Button btnConfirm;
        private System.Windows.Forms.Button btnCheckResult;
        private System.Windows.Forms.Button btnAcknowledge;
        private System.Windows.Forms.Button btnClose;
    }
}
