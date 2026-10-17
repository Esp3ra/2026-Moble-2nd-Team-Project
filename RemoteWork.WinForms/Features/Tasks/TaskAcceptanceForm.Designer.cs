#nullable disable

namespace RemoteWork.WinForms.Features.Tasks
{
    partial class TaskAcceptanceForm
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
            lblPreparationStatus = new System.Windows.Forms.Label();
            flowActions = new System.Windows.Forms.FlowLayoutPanel();
            btnClose = new System.Windows.Forms.Button();
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
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 72F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 48F));
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
            layoutDetails.RowCount = 6;
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 88F));
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 64F));
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutDetails.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
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
            // lblPreparationStatus
            //
            lblPreparationStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            lblPreparationStatus.Margin = new System.Windows.Forms.Padding(0, 8, 0, 0);
            lblPreparationStatus.Name = "lblPreparationStatus";
            lblPreparationStatus.Text = "대상 업무 선택 필요 · 수락 처리 규약 확인 필요 · 상태 미변경";
            //
            // flowActions
            //
            flowActions.Dock = System.Windows.Forms.DockStyle.Fill;
            flowActions.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            flowActions.Margin = new System.Windows.Forms.Padding(0);
            flowActions.Name = "flowActions";
            flowActions.Padding = new System.Windows.Forms.Padding(0, 8, 0, 0);
            flowActions.Controls.Add(btnClose);
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
        private System.Windows.Forms.Label lblPreparationStatus;
        private System.Windows.Forms.FlowLayoutPanel flowActions;
        private System.Windows.Forms.Button btnClose;
    }
}
