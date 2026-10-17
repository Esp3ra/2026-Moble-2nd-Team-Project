#nullable disable

namespace RemoteWork.WinForms.Features.Collaboration
{
    partial class ImportantNoticeForm
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
            lblText2 = new System.Windows.Forms.Label();
            txtContent = new System.Windows.Forms.TextBox();
            flowActions = new System.Windows.Forms.FlowLayoutPanel();
            btnClose = new System.Windows.Forms.Button();
            layoutContent.SuspendLayout();
            layoutContent2.SuspendLayout();
            panelContent.SuspendLayout();
            layoutContent3.SuspendLayout();
            panelContent2.SuspendLayout();
            layoutContent4.SuspendLayout();
            flowActions.SuspendLayout();
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
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 70F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 50F));
            layoutContent.Controls.Add(layoutContent2, 0, 0);
            layoutContent.Controls.Add(panelContent, 0, 1);
            layoutContent.Controls.Add(flowActions, 0, 2);
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
            layoutContent2.RowCount = 2;
            layoutContent2.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent2.Name = "layoutContent2";
            layoutContent2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 36F));
            layoutContent2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent2.Controls.Add(lblPageTitle, 0, 0);
            //
            // lblPageTitle
            //
            lblPageTitle.Text = "상단 고정 공지사항";
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
            //
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
            layoutContent3.AutoScroll = true;
            layoutContent3.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            layoutContent3.ColumnCount = 1;
            layoutContent3.RowCount = 1;
            layoutContent3.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent3.Name = "layoutContent3";
            layoutContent3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.Controls.Add(panelContent2, 0, 0);
            //
            // panelContent2
            //
            panelContent2.Text = "";
            panelContent2.Location = new System.Drawing.Point(0, 0);
            panelContent2.Size = new System.Drawing.Size(709, 403);
            panelContent2.MinimumSize = new System.Drawing.Size(0, 124);
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
            panelContent2.Controls.Add(layoutContent4);
            //
            // layoutContent4
            //
            layoutContent4.Text = "";
            layoutContent4.Location = new System.Drawing.Point(16, 16);
            layoutContent4.Size = new System.Drawing.Size(675, 369);
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
            layoutContent4.Controls.Add(txtContent, 0, 1);
            //
            // lblText2
            //
            lblText2.Text = "보안 점검 안내";
            lblText2.Location = new System.Drawing.Point(0, 4);
            lblText2.Size = new System.Drawing.Size(187, 19);
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
            // txtContent
            //
            txtContent.Text = "대상: 전 직원\r\n등록일: 2026.10.13\r\n\r\n원격 근무 환경의 보안 점검이 예정되어 있습니다.\r\n업무 일정과 공지사항 내용을 확인해 주세요.";
            txtContent.Location = new System.Drawing.Point(3, 37);
            txtContent.Size = new System.Drawing.Size(669, 329);
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
            // flowActions
            //
            flowActions.Text = "";
            flowActions.Location = new System.Drawing.Point(0, 536);
            flowActions.Size = new System.Drawing.Size(736, 42);
            flowActions.MinimumSize = new System.Drawing.Size(0, 40);
            flowActions.MaximumSize = new System.Drawing.Size(0, 0);
            flowActions.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            flowActions.Padding = new System.Windows.Forms.Padding(0, 10, 0, 0);
            flowActions.Dock = System.Windows.Forms.DockStyle.Fill;
            flowActions.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            flowActions.AutoSize = true;
            flowActions.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            flowActions.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            flowActions.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            flowActions.TabIndex = 3;
            flowActions.TabStop = false;
            flowActions.AutoScroll = false;
            flowActions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowActions.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            flowActions.WrapContents = true;
            flowActions.Name = "flowActions";
            flowActions.Controls.Add(btnClose);
            //
            // btnClose
            //
            btnClose.Text = "닫기";
            btnClose.Location = new System.Drawing.Point(537, 10);
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
            btnClose.TabIndex = 1;
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
            // ImportantNoticeForm
            //
            Text = "상단 고정 공지사항";
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
            Name = "ImportantNoticeForm";
            Controls.Add(layoutContent);
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            CancelButton = btnClose;
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
        private System.Windows.Forms.Label lblText2;
        private System.Windows.Forms.TextBox txtContent;
        private System.Windows.Forms.FlowLayoutPanel flowActions;
        private System.Windows.Forms.Button btnClose;
    }
}
