#nullable disable

namespace RemoteWork.WinForms.Features.Tasks
{
    partial class TaskAcceptanceForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                StopRequests();
                if (components != null) components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            layoutContent = new System.Windows.Forms.TableLayoutPanel();
            lblPageTitle = new System.Windows.Forms.Label();
            panelContent = new System.Windows.Forms.Panel();
            layoutDetails = new System.Windows.Forms.TableLayoutPanel();
            lblTaskId = new System.Windows.Forms.Label();
            txtTaskId = new System.Windows.Forms.TextBox();
            lblTaskTitle = new System.Windows.Forms.Label();
            txtTaskTitle = new System.Windows.Forms.TextBox();
            lblAssignee = new System.Windows.Forms.Label();
            txtAssignee = new System.Windows.Forms.TextBox();
            lblTaskStatus = new System.Windows.Forms.Label();
            txtTaskStatus = new System.Windows.Forms.TextBox();
            lblStartDate = new System.Windows.Forms.Label();
            txtStartDate = new System.Windows.Forms.TextBox();
            lblDeadline = new System.Windows.Forms.Label();
            txtDeadline = new System.Windows.Forms.TextBox();
            lblWriteContext = new System.Windows.Forms.Label();
            txtWriteContext = new System.Windows.Forms.TextBox();
            lblSourceNotice = new System.Windows.Forms.Label();
            lblPreparationStatus = new System.Windows.Forms.Label();
            flowActions = new System.Windows.Forms.FlowLayoutPanel();
            btnClose = new System.Windows.Forms.Button();
            btnReloadContext = new System.Windows.Forms.Button();
            btnConfirmTarget = new System.Windows.Forms.Button();
            layoutContent.SuspendLayout();
            panelContent.SuspendLayout();
            layoutDetails.SuspendLayout();
            flowActions.SuspendLayout();
            SuspendLayout();
            //
            // layoutContent
            //
            layoutContent.ColumnCount = 1;
            layoutContent.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent.RowCount = 4;
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 54F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layoutContent.Dock = System.Windows.Forms.DockStyle.Fill;
            layoutContent.Margin = new System.Windows.Forms.Padding(0);
            layoutContent.Name = "layoutContent";
            layoutContent.Controls.Add(lblPageTitle, 0, 0);
            layoutContent.Controls.Add(panelContent, 0, 1);
            layoutContent.Controls.Add(lblPreparationStatus, 0, 2);
            layoutContent.Controls.Add(flowActions, 0, 3);
            //
            // lblPageTitle
            //
            lblPageTitle.AutoSize = true;
            lblPageTitle.Font = new System.Drawing.Font("맑은 고딕", 20F, System.Drawing.FontStyle.Bold);
            lblPageTitle.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            lblPageTitle.Name = "lblPageTitle";
            lblPageTitle.Text = "업무 수락 대상 확인";
            //
            // panelContent
            //
            panelContent.AutoScroll = true;
            panelContent.Dock = System.Windows.Forms.DockStyle.Fill;
            panelContent.Margin = new System.Windows.Forms.Padding(0);
            panelContent.Name = "panelContent";
            panelContent.Controls.Add(layoutDetails);
            //
            // layoutDetails
            //
            layoutDetails.AutoSize = true;
            layoutDetails.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            layoutDetails.ColumnCount = 2;
            layoutDetails.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 116F));
            layoutDetails.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutDetails.Dock = System.Windows.Forms.DockStyle.Top;
            layoutDetails.Margin = new System.Windows.Forms.Padding(0);
            layoutDetails.Name = "layoutDetails";
            layoutDetails.RowCount = 8;
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 88F));
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 132F));
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            layoutDetails.Controls.Add(lblTaskId, 0, 0);
            layoutDetails.Controls.Add(txtTaskId, 1, 0);
            layoutDetails.Controls.Add(lblTaskTitle, 0, 1);
            layoutDetails.Controls.Add(txtTaskTitle, 1, 1);
            layoutDetails.Controls.Add(lblAssignee, 0, 2);
            layoutDetails.Controls.Add(txtAssignee, 1, 2);
            layoutDetails.Controls.Add(lblTaskStatus, 0, 3);
            layoutDetails.Controls.Add(txtTaskStatus, 1, 3);
            layoutDetails.Controls.Add(lblStartDate, 0, 4);
            layoutDetails.Controls.Add(txtStartDate, 1, 4);
            layoutDetails.Controls.Add(lblDeadline, 0, 5);
            layoutDetails.Controls.Add(txtDeadline, 1, 5);
            layoutDetails.Controls.Add(lblWriteContext, 0, 6);
            layoutDetails.Controls.Add(txtWriteContext, 1, 6);
            layoutDetails.Controls.Add(lblSourceNotice, 0, 7);
            layoutDetails.SetColumnSpan(lblSourceNotice, 2);
            //
            // lblTaskId
            //
            lblTaskId.Dock = System.Windows.Forms.DockStyle.Fill;
            lblTaskId.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblTaskId.Name = "lblTaskId";
            lblTaskId.Text = "업무 ID";
            lblTaskId.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // txtTaskId
            //
            txtTaskId.Dock = System.Windows.Forms.DockStyle.Fill;
            txtTaskId.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            txtTaskId.Multiline = true;
            txtTaskId.Name = "txtTaskId";
            txtTaskId.ReadOnly = true;
            txtTaskId.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtTaskId.Text = "대상 업무 선택 필요";
            //
            // lblTaskTitle
            //
            lblTaskTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            lblTaskTitle.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblTaskTitle.Name = "lblTaskTitle";
            lblTaskTitle.Text = "업무 제목";
            lblTaskTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // txtTaskTitle
            //
            txtTaskTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            txtTaskTitle.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            txtTaskTitle.Multiline = true;
            txtTaskTitle.Name = "txtTaskTitle";
            txtTaskTitle.ReadOnly = true;
            txtTaskTitle.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            //
            // lblAssignee
            //
            lblAssignee.Dock = System.Windows.Forms.DockStyle.Fill;
            lblAssignee.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblAssignee.Name = "lblAssignee";
            lblAssignee.Text = "담당자";
            lblAssignee.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // txtAssignee
            //
            txtAssignee.Dock = System.Windows.Forms.DockStyle.Fill;
            txtAssignee.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            txtAssignee.Multiline = true;
            txtAssignee.Name = "txtAssignee";
            txtAssignee.ReadOnly = true;
            txtAssignee.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            //
            // lblTaskStatus
            //
            lblTaskStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            lblTaskStatus.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblTaskStatus.Name = "lblTaskStatus";
            lblTaskStatus.Text = "현재 상태";
            lblTaskStatus.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // txtTaskStatus
            //
            txtTaskStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            txtTaskStatus.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            txtTaskStatus.Name = "txtTaskStatus";
            txtTaskStatus.ReadOnly = true;
            //
            // lblStartDate
            //
            lblStartDate.Dock = System.Windows.Forms.DockStyle.Fill;
            lblStartDate.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblStartDate.Name = "lblStartDate";
            lblStartDate.Text = "시작일";
            lblStartDate.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // txtStartDate
            //
            txtStartDate.Dock = System.Windows.Forms.DockStyle.Fill;
            txtStartDate.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            txtStartDate.Name = "txtStartDate";
            txtStartDate.ReadOnly = true;
            //
            // lblDeadline
            //
            lblDeadline.Dock = System.Windows.Forms.DockStyle.Fill;
            lblDeadline.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblDeadline.Name = "lblDeadline";
            lblDeadline.Text = "현재 마감일";
            lblDeadline.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            //
            // txtDeadline
            //
            txtDeadline.Dock = System.Windows.Forms.DockStyle.Fill;
            txtDeadline.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            txtDeadline.Name = "txtDeadline";
            txtDeadline.ReadOnly = true;
            //
            // lblWriteContext
            //
            lblWriteContext.Dock = System.Windows.Forms.DockStyle.Fill;
            lblWriteContext.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblWriteContext.Name = "lblWriteContext";
            lblWriteContext.Text = "최신 확인 정보";
            lblWriteContext.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblWriteContext.Visible = false;
            //
            // txtWriteContext
            //
            txtWriteContext.Dock = System.Windows.Forms.DockStyle.Fill;
            txtWriteContext.Margin = new System.Windows.Forms.Padding(0, 4, 0, 4);
            txtWriteContext.Multiline = true;
            txtWriteContext.Name = "txtWriteContext";
            txtWriteContext.ReadOnly = true;
            txtWriteContext.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtWriteContext.Visible = false;
            //
            // lblSourceNotice
            //
            lblSourceNotice.AutoSize = true;
            lblSourceNotice.Dock = System.Windows.Forms.DockStyle.Fill;
            lblSourceNotice.Margin = new System.Windows.Forms.Padding(0, 8, 0, 12);
            lblSourceNotice.Name = "lblSourceNotice";
            lblSourceNotice.Text = "위 제목·담당자명·상태·날짜는 상세 조회 당시의 정보입니다. 최신 확인 정보는 별도 요청으로 조회하므로 같은 시점의 정보라고 보장하지 않습니다.\r\n수락·재작업 시작의 날짜 기준은 한국시간 시작일~마감일 당일입니다. 위 날짜만으로 현재 실행 자격을 판단하지 않습니다.\r\n수락·재작업 실행 규약은 합의 필요이며, 실행 시점의 서버 재검사와 실제 상태 저장은 아직 연결하지 않았습니다.";
            lblSourceNotice.Visible = false;
            //
            // lblPreparationStatus
            //
            lblPreparationStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            lblPreparationStatus.AutoSize = true;
            lblPreparationStatus.MinimumSize = new System.Drawing.Size(0, 64);
            lblPreparationStatus.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            lblPreparationStatus.Name = "lblPreparationStatus";
            lblPreparationStatus.Text = "대상 업무 선택 필요 · 수락 처리 규약 확인 필요 · 상태 미변경";
            //
            // flowActions
            //
            flowActions.Dock = System.Windows.Forms.DockStyle.Fill;
            flowActions.AutoSize = true;
            flowActions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowActions.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            flowActions.Margin = new System.Windows.Forms.Padding(0);
            flowActions.Name = "flowActions";
            flowActions.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
            flowActions.Controls.Add(btnClose);
            flowActions.Controls.Add(btnConfirmTarget);
            flowActions.Controls.Add(btnReloadContext);
            //
            // btnClose
            //
            btnClose.AutoSize = true;
            btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnClose.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            btnClose.MinimumSize = new System.Drawing.Size(88, 34);
            btnClose.Name = "btnClose";
            btnClose.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnClose.Text = "닫기";
            btnClose.Click += new System.EventHandler(Close_Click);
            //
            // btnConfirmTarget
            //
            btnConfirmTarget.AutoSize = true;
            btnConfirmTarget.Enabled = false;
            btnConfirmTarget.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            btnConfirmTarget.MinimumSize = new System.Drawing.Size(104, 34);
            btnConfirmTarget.Name = "btnConfirmTarget";
            btnConfirmTarget.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnConfirmTarget.Text = "대상 확인";
            btnConfirmTarget.Visible = false;
            btnConfirmTarget.Click += new System.EventHandler(ConfirmTarget_Click);
            //
            // btnReloadContext
            //
            btnReloadContext.AutoSize = true;
            btnReloadContext.Enabled = false;
            btnReloadContext.Margin = new System.Windows.Forms.Padding(0, 0, 8, 0);
            btnReloadContext.MinimumSize = new System.Drawing.Size(144, 34);
            btnReloadContext.Name = "btnReloadContext";
            btnReloadContext.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnReloadContext.Text = "최신 정보 다시 조회";
            btnReloadContext.Visible = false;
            btnReloadContext.Click += new System.EventHandler(ReloadContext_Click);
            //
            // TaskAcceptanceForm
            //
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = System.Drawing.Color.FromArgb(242, 245, 250);
            CancelButton = btnClose;
            ClientSize = new System.Drawing.Size(780, 630);
            Controls.Add(layoutContent);
            Font = new System.Drawing.Font("맑은 고딕", 9F);
            ForeColor = System.Drawing.Color.FromArgb(30, 42, 61);
            MinimumSize = new System.Drawing.Size(690, 560);
            MinimizeBox = false;
            Name = "TaskAcceptanceForm";
            Padding = new System.Windows.Forms.Padding(22);
            ShowInTaskbar = false;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            Text = "업무 수락 대상 확인";
            flowActions.ResumeLayout(false);
            flowActions.PerformLayout();
            layoutDetails.ResumeLayout(false);
            layoutDetails.PerformLayout();
            panelContent.ResumeLayout(false);
            panelContent.PerformLayout();
            layoutContent.ResumeLayout(false);
            layoutContent.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel layoutContent;
        private System.Windows.Forms.Label lblPageTitle;
        private System.Windows.Forms.Panel panelContent;
        private System.Windows.Forms.TableLayoutPanel layoutDetails;
        private System.Windows.Forms.Label lblTaskId;
        private System.Windows.Forms.TextBox txtTaskId;
        private System.Windows.Forms.Label lblTaskTitle;
        private System.Windows.Forms.TextBox txtTaskTitle;
        private System.Windows.Forms.Label lblAssignee;
        private System.Windows.Forms.TextBox txtAssignee;
        private System.Windows.Forms.Label lblTaskStatus;
        private System.Windows.Forms.TextBox txtTaskStatus;
        private System.Windows.Forms.Label lblStartDate;
        private System.Windows.Forms.TextBox txtStartDate;
        private System.Windows.Forms.Label lblDeadline;
        private System.Windows.Forms.TextBox txtDeadline;
        private System.Windows.Forms.Label lblWriteContext;
        private System.Windows.Forms.TextBox txtWriteContext;
        private System.Windows.Forms.Label lblSourceNotice;
        private System.Windows.Forms.Label lblPreparationStatus;
        private System.Windows.Forms.FlowLayoutPanel flowActions;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnReloadContext;
        private System.Windows.Forms.Button btnConfirmTarget;
    }
}
