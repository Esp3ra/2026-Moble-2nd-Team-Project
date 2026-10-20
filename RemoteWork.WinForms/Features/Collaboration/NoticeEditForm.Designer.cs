#nullable disable

namespace RemoteWork.WinForms.Features.Collaboration
{
    partial class NoticeEditForm
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
            layoutContent = new TableLayoutPanel();
            layoutContent2 = new TableLayoutPanel();
            lblPageTitle = new Label();
            panelContent = new Panel();
            layoutContent3 = new TableLayoutPanel();
            panelContent2 = new Panel();
            layoutContent4 = new TableLayoutPanel();
            lblNoticeTitle = new Label();
            txtNoticeTitle = new TextBox();
            lblNoticeAudience = new Label();
            cmbNoticeAudience = new ComboBox();
            lblPinToTop = new Label();
            chkPinToTop = new CheckBox();
            lblNoticeBody = new Label();
            txtNoticeBody = new TextBox();
            lblAttachments = new Label();
            txtAttachments = new TextBox();
            flowActions = new FlowLayoutPanel();
            btnSelectAttachment = new Button();
            flowActions2 = new FlowLayoutPanel();
            btnPublishNotice = new Button();
            btnClose = new Button();
            layoutContent.SuspendLayout();
            layoutContent2.SuspendLayout();
            panelContent.SuspendLayout();
            layoutContent3.SuspendLayout();
            panelContent2.SuspendLayout();
            layoutContent4.SuspendLayout();
            flowActions.SuspendLayout();
            flowActions2.SuspendLayout();
            SuspendLayout();
            // 
            // layoutContent
            // 
            layoutContent.BackColor = Color.FromArgb(242, 245, 250);
            layoutContent.ColumnCount = 1;
            layoutContent.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutContent.Controls.Add(layoutContent2, 0, 0);
            layoutContent.Controls.Add(panelContent, 0, 1);
            layoutContent.Controls.Add(flowActions2, 0, 2);
            layoutContent.Dock = DockStyle.Fill;
            layoutContent.Font = new Font("맑은 고딕", 9F);
            layoutContent.ForeColor = Color.FromArgb(0, 0, 0);
            layoutContent.Location = new Point(22, 22);
            layoutContent.Name = "layoutContent";
            layoutContent.RowCount = 3;
            layoutContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            layoutContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            layoutContent.Size = new Size(736, 644);
            layoutContent.TabIndex = 0;
            // 
            // layoutContent2
            // 
            layoutContent2.BackColor = Color.FromArgb(242, 245, 250);
            layoutContent2.ColumnCount = 1;
            layoutContent2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 20F));
            layoutContent2.Controls.Add(lblPageTitle, 0, 0);
            layoutContent2.Dock = DockStyle.Fill;
            layoutContent2.Font = new Font("맑은 고딕", 9F);
            layoutContent2.ForeColor = Color.FromArgb(0, 0, 0);
            layoutContent2.Location = new Point(0, 0);
            layoutContent2.Margin = new Padding(0);
            layoutContent2.Name = "layoutContent2";
            layoutContent2.RowCount = 2;
            layoutContent2.RowStyles.Add(new RowStyle());
            layoutContent2.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutContent2.Size = new Size(736, 70);
            layoutContent2.TabIndex = 0;
            // 
            // lblPageTitle
            // 
            lblPageTitle.AutoSize = true;
            lblPageTitle.BackColor = Color.FromArgb(242, 245, 250);
            lblPageTitle.Font = new Font("맑은 고딕", 20F, FontStyle.Bold);
            lblPageTitle.ForeColor = Color.FromArgb(30, 42, 61);
            lblPageTitle.Location = new Point(0, 0);
            lblPageTitle.Margin = new Padding(0, 0, 8, 0);
            lblPageTitle.Name = "lblPageTitle";
            lblPageTitle.Size = new Size(188, 37);
            lblPageTitle.TabIndex = 0;
            lblPageTitle.Text = "공지사항 작성";
            // 
            // panelContent
            // 
            panelContent.AutoScroll = true;
            panelContent.BackColor = Color.FromArgb(242, 245, 250);
            panelContent.Controls.Add(layoutContent3);
            panelContent.Dock = DockStyle.Fill;
            panelContent.Font = new Font("맑은 고딕", 9F);
            panelContent.ForeColor = Color.FromArgb(0, 0, 0);
            panelContent.Location = new Point(0, 70);
            panelContent.Margin = new Padding(0);
            panelContent.Name = "panelContent";
            panelContent.Size = new Size(736, 524);
            panelContent.TabIndex = 2;
            // 
            // layoutContent3
            // 
            layoutContent3.AutoScroll = true;
            layoutContent3.BackColor = Color.FromArgb(242, 245, 250);
            layoutContent3.ColumnCount = 1;
            layoutContent3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutContent3.Controls.Add(panelContent2, 0, 0);
            layoutContent3.Controls.Add(flowActions, 0, 1);
            layoutContent3.Dock = DockStyle.Fill;
            layoutContent3.Font = new Font("맑은 고딕", 9F);
            layoutContent3.ForeColor = Color.FromArgb(0, 0, 0);
            layoutContent3.Location = new Point(0, 0);
            layoutContent3.Margin = new Padding(0);
            layoutContent3.Name = "layoutContent3";
            layoutContent3.RowCount = 2;
            layoutContent3.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutContent3.RowStyles.Add(new RowStyle());
            layoutContent3.Size = new Size(736, 524);
            layoutContent3.TabIndex = 0;
            // 
            // panelContent2
            // 
            panelContent2.AutoScroll = true;
            panelContent2.BackColor = Color.FromArgb(242, 245, 250);
            panelContent2.Controls.Add(layoutContent4);
            panelContent2.Dock = DockStyle.Fill;
            panelContent2.Font = new Font("맑은 고딕", 9F);
            panelContent2.ForeColor = Color.FromArgb(0, 0, 0);
            panelContent2.Location = new Point(0, 0);
            panelContent2.Margin = new Padding(0);
            panelContent2.Name = "panelContent2";
            panelContent2.Size = new Size(736, 475);
            panelContent2.TabIndex = 0;
            // 
            // layoutContent4
            // 
            layoutContent4.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            layoutContent4.BackColor = Color.FromArgb(242, 245, 250);
            layoutContent4.ColumnCount = 2;
            layoutContent4.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116F));
            layoutContent4.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layoutContent4.Controls.Add(lblNoticeTitle, 0, 0);
            layoutContent4.Controls.Add(txtNoticeTitle, 1, 0);
            layoutContent4.Controls.Add(lblNoticeAudience, 0, 1);
            layoutContent4.Controls.Add(cmbNoticeAudience, 1, 1);
            layoutContent4.Controls.Add(lblPinToTop, 0, 2);
            layoutContent4.Controls.Add(chkPinToTop, 1, 2);
            layoutContent4.Controls.Add(lblNoticeBody, 0, 3);
            layoutContent4.Controls.Add(txtNoticeBody, 1, 3);
            layoutContent4.Controls.Add(lblAttachments, 0, 4);
            layoutContent4.Controls.Add(txtAttachments, 1, 4);
            layoutContent4.Dock = DockStyle.Fill;
            layoutContent4.Font = new Font("맑은 고딕", 9F);
            layoutContent4.ForeColor = Color.FromArgb(0, 0, 0);
            layoutContent4.Location = new Point(0, 0);
            layoutContent4.Margin = new Padding(0);
            layoutContent4.Name = "layoutContent4";
            layoutContent4.RowCount = 5;
            layoutContent4.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layoutContent4.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            layoutContent4.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            layoutContent4.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            layoutContent4.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            layoutContent4.Size = new Size(736, 475);
            layoutContent4.TabIndex = 0;
            // 
            // lblNoticeTitle
            // 
            lblNoticeTitle.BackColor = Color.FromArgb(242, 245, 250);
            lblNoticeTitle.Dock = DockStyle.Fill;
            lblNoticeTitle.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            lblNoticeTitle.ForeColor = Color.FromArgb(30, 42, 61);
            lblNoticeTitle.Location = new Point(0, 4);
            lblNoticeTitle.Margin = new Padding(0, 4, 8, 4);
            lblNoticeTitle.Name = "lblNoticeTitle";
            lblNoticeTitle.Size = new Size(108, 36);
            lblNoticeTitle.TabIndex = 0;
            lblNoticeTitle.Text = "공지사항 제목";
            lblNoticeTitle.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // txtNoticeTitle
            // 
            txtNoticeTitle.BackColor = Color.FromArgb(255, 255, 255);
            txtNoticeTitle.BorderStyle = BorderStyle.FixedSingle;
            txtNoticeTitle.Dock = DockStyle.Fill;
            txtNoticeTitle.Font = new Font("맑은 고딕", 10F);
            txtNoticeTitle.ForeColor = Color.FromArgb(0, 0, 0);
            txtNoticeTitle.Location = new Point(116, 4);
            txtNoticeTitle.Margin = new Padding(0, 4, 0, 6);
            txtNoticeTitle.MinimumSize = new Size(0, 28);
            txtNoticeTitle.Name = "txtNoticeTitle";
            txtNoticeTitle.Size = new Size(620, 28);
            txtNoticeTitle.TabIndex = 1;
            // 
            // lblNoticeAudience
            // 
            lblNoticeAudience.BackColor = Color.FromArgb(242, 245, 250);
            lblNoticeAudience.Dock = DockStyle.Fill;
            lblNoticeAudience.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            lblNoticeAudience.ForeColor = Color.FromArgb(30, 42, 61);
            lblNoticeAudience.Location = new Point(0, 48);
            lblNoticeAudience.Margin = new Padding(0, 4, 8, 4);
            lblNoticeAudience.Name = "lblNoticeAudience";
            lblNoticeAudience.Size = new Size(108, 36);
            lblNoticeAudience.TabIndex = 2;
            lblNoticeAudience.Text = "공지사항 대상";
            lblNoticeAudience.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // cmbNoticeAudience
            // 
            cmbNoticeAudience.BackColor = Color.FromArgb(255, 255, 255);
            cmbNoticeAudience.Dock = DockStyle.Fill;
            cmbNoticeAudience.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbNoticeAudience.Font = new Font("맑은 고딕", 10F);
            cmbNoticeAudience.ForeColor = Color.FromArgb(0, 0, 0);
            cmbNoticeAudience.Items.AddRange(new object[] { "전 직원", "기획팀", "개발팀", "디자인팀" });
            cmbNoticeAudience.Location = new Point(116, 48);
            cmbNoticeAudience.Margin = new Padding(0, 4, 0, 6);
            cmbNoticeAudience.Name = "cmbNoticeAudience";
            cmbNoticeAudience.Size = new Size(620, 25);
            cmbNoticeAudience.TabIndex = 3;
            // 
            // lblPinToTop
            // 
            lblPinToTop.BackColor = Color.FromArgb(242, 245, 250);
            lblPinToTop.Dock = DockStyle.Fill;
            lblPinToTop.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            lblPinToTop.ForeColor = Color.FromArgb(30, 42, 61);
            lblPinToTop.Location = new Point(0, 92);
            lblPinToTop.Margin = new Padding(0, 4, 8, 4);
            lblPinToTop.Name = "lblPinToTop";
            lblPinToTop.Size = new Size(108, 32);
            lblPinToTop.TabIndex = 4;
            lblPinToTop.Text = "게시 설정";
            lblPinToTop.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // chkPinToTop
            // 
            chkPinToTop.AutoSize = true;
            chkPinToTop.Dock = DockStyle.Fill;
            chkPinToTop.Font = new Font("맑은 고딕", 9F);
            chkPinToTop.Location = new Point(116, 92);
            chkPinToTop.Margin = new Padding(0, 4, 0, 6);
            chkPinToTop.Name = "chkPinToTop";
            chkPinToTop.Size = new Size(620, 30);
            chkPinToTop.TabIndex = 5;
            chkPinToTop.Text = "상단 고정";
            chkPinToTop.UseVisualStyleBackColor = true;
            // 
            // lblNoticeBody
            // 
            lblNoticeBody.BackColor = Color.FromArgb(242, 245, 250);
            lblNoticeBody.Dock = DockStyle.Fill;
            lblNoticeBody.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            lblNoticeBody.ForeColor = Color.FromArgb(30, 42, 61);
            lblNoticeBody.Location = new Point(0, 132);
            lblNoticeBody.Margin = new Padding(0, 4, 8, 4);
            lblNoticeBody.Name = "lblNoticeBody";
            lblNoticeBody.Size = new Size(108, 281);
            lblNoticeBody.TabIndex = 6;
            lblNoticeBody.Text = "공지사항 내용";
            lblNoticeBody.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // txtNoticeBody
            // 
            txtNoticeBody.BackColor = Color.FromArgb(255, 255, 255);
            txtNoticeBody.BorderStyle = BorderStyle.FixedSingle;
            txtNoticeBody.Dock = DockStyle.Fill;
            txtNoticeBody.Font = new Font("맑은 고딕", 10F);
            txtNoticeBody.ForeColor = Color.FromArgb(0, 0, 0);
            txtNoticeBody.Location = new Point(116, 132);
            txtNoticeBody.Margin = new Padding(0, 4, 0, 6);
            txtNoticeBody.MinimumSize = new Size(0, 90);
            txtNoticeBody.Multiline = true;
            txtNoticeBody.Name = "txtNoticeBody";
            txtNoticeBody.ScrollBars = ScrollBars.Vertical;
            txtNoticeBody.Size = new Size(620, 279);
            txtNoticeBody.TabIndex = 7;
            // 
            // lblAttachments
            // 
            lblAttachments.Dock = DockStyle.Fill;
            lblAttachments.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
            lblAttachments.ForeColor = Color.FromArgb(30, 42, 61);
            lblAttachments.Location = new Point(0, 421);
            lblAttachments.Margin = new Padding(0, 4, 8, 4);
            lblAttachments.Name = "lblAttachments";
            lblAttachments.Size = new Size(108, 50);
            lblAttachments.TabIndex = 8;
            lblAttachments.Text = "첨부파일";
            lblAttachments.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // txtAttachments
            // 
            txtAttachments.BackColor = Color.FromArgb(255, 255, 255);
            txtAttachments.BorderStyle = BorderStyle.FixedSingle;
            txtAttachments.Dock = DockStyle.Fill;
            txtAttachments.Font = new Font("맑은 고딕", 10F);
            txtAttachments.ForeColor = Color.FromArgb(0, 0, 0);
            txtAttachments.Location = new Point(116, 421);
            txtAttachments.Margin = new Padding(0, 4, 0, 6);
            txtAttachments.Multiline = true;
            txtAttachments.Name = "txtAttachments";
            txtAttachments.ReadOnly = true;
            txtAttachments.ScrollBars = ScrollBars.Vertical;
            txtAttachments.Size = new Size(620, 48);
            txtAttachments.TabIndex = 9;
            // 
            // flowActions
            // 
            flowActions.AutoSize = true;
            flowActions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flowActions.BackColor = Color.FromArgb(242, 245, 250);
            flowActions.Controls.Add(btnSelectAttachment);
            flowActions.Dock = DockStyle.Fill;
            flowActions.Font = new Font("맑은 고딕", 9F);
            flowActions.ForeColor = Color.FromArgb(0, 0, 0);
            flowActions.Location = new Point(0, 475);
            flowActions.Margin = new Padding(0, 0, 0, 8);
            flowActions.MinimumSize = new Size(0, 40);
            flowActions.Name = "flowActions";
            flowActions.Padding = new Padding(116, 0, 0, 0);
            flowActions.Size = new Size(736, 41);
            flowActions.TabIndex = 1;
            // 
            // btnSelectAttachment
            // 
            btnSelectAttachment.AutoSize = true;
            btnSelectAttachment.BackColor = Color.FromArgb(255, 255, 255);
            btnSelectAttachment.Cursor = Cursors.Hand;
            btnSelectAttachment.FlatAppearance.BorderColor = Color.FromArgb(213, 220, 231);
            btnSelectAttachment.FlatStyle = FlatStyle.Flat;
            btnSelectAttachment.Font = new Font("맑은 고딕", 9F);
            btnSelectAttachment.ForeColor = Color.FromArgb(30, 42, 61);
            btnSelectAttachment.Location = new Point(116, 4);
            btnSelectAttachment.Margin = new Padding(0, 4, 8, 2);
            btnSelectAttachment.MinimumSize = new Size(88, 34);
            btnSelectAttachment.Name = "btnSelectAttachment";
            btnSelectAttachment.Padding = new Padding(12, 4, 12, 4);
            btnSelectAttachment.Size = new Size(119, 35);
            btnSelectAttachment.TabIndex = 0;
            btnSelectAttachment.Text = "첨부파일 선택";
            btnSelectAttachment.UseVisualStyleBackColor = false;
            btnSelectAttachment.Click += SelectAttachment_Click;
            // 
            // flowActions2
            // 
            flowActions2.AutoSize = true;
            flowActions2.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flowActions2.BackColor = Color.FromArgb(242, 245, 250);
            flowActions2.Controls.Add(btnPublishNotice);
            flowActions2.Controls.Add(btnClose);
            flowActions2.Dock = DockStyle.Fill;
            flowActions2.FlowDirection = FlowDirection.RightToLeft;
            flowActions2.Font = new Font("맑은 고딕", 9F);
            flowActions2.ForeColor = Color.FromArgb(0, 0, 0);
            flowActions2.Location = new Point(0, 594);
            flowActions2.Margin = new Padding(0, 0, 0, 8);
            flowActions2.MinimumSize = new Size(0, 40);
            flowActions2.Name = "flowActions2";
            flowActions2.Padding = new Padding(0, 10, 0, 0);
            flowActions2.Size = new Size(736, 42);
            flowActions2.TabIndex = 3;
            // 
            // btnPublishNotice
            // 
            btnPublishNotice.AutoSize = true;
            btnPublishNotice.BackColor = Color.FromArgb(52, 101, 224);
            btnPublishNotice.Cursor = Cursors.Hand;
            btnPublishNotice.FlatAppearance.BorderColor = Color.FromArgb(52, 101, 224);
            btnPublishNotice.FlatStyle = FlatStyle.Flat;
            btnPublishNotice.Font = new Font("맑은 고딕", 9F);
            btnPublishNotice.ForeColor = Color.FromArgb(255, 255, 255);
            btnPublishNotice.Location = new Point(609, 10);
            btnPublishNotice.Margin = new Padding(0, 0, 8, 6);
            btnPublishNotice.MinimumSize = new Size(88, 34);
            btnPublishNotice.Name = "btnPublishNotice";
            btnPublishNotice.Padding = new Padding(12, 4, 12, 4);
            btnPublishNotice.Size = new Size(119, 35);
            btnPublishNotice.TabIndex = 0;
            btnPublishNotice.Text = "공지사항 등록";
            btnPublishNotice.UseVisualStyleBackColor = false;
            btnPublishNotice.Click += PublishNotice_Click;
            // 
            // btnClose
            // 
            btnClose.AutoSize = true;
            btnClose.BackColor = Color.FromArgb(255, 255, 255);
            btnClose.Cursor = Cursors.Hand;
            btnClose.DialogResult = DialogResult.Cancel;
            btnClose.FlatAppearance.BorderColor = Color.FromArgb(213, 220, 231);
            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.Font = new Font("맑은 고딕", 9F);
            btnClose.ForeColor = Color.FromArgb(30, 42, 61);
            btnClose.Location = new Point(513, 10);
            btnClose.Margin = new Padding(0, 0, 8, 6);
            btnClose.MinimumSize = new Size(88, 34);
            btnClose.Name = "btnClose";
            btnClose.Padding = new Padding(12, 4, 12, 4);
            btnClose.Size = new Size(88, 35);
            btnClose.TabIndex = 1;
            btnClose.Text = "닫기";
            btnClose.UseVisualStyleBackColor = false;
            btnClose.Click += ClosePreview_Click;
            // 
            // NoticeEditForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(242, 245, 250);
            CancelButton = btnClose;
            ClientSize = new Size(780, 688);
            Controls.Add(layoutContent);
            Font = new Font("맑은 고딕", 9F);
            ForeColor = Color.FromArgb(0, 0, 0);
            MinimizeBox = false;
            MinimumSize = new Size(690, 618);
            Name = "NoticeEditForm";
            Padding = new Padding(22);
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Text = "공지사항 작성";
            layoutContent.ResumeLayout(false);
            layoutContent.PerformLayout();
            layoutContent2.ResumeLayout(false);
            layoutContent2.PerformLayout();
            panelContent.ResumeLayout(false);
            layoutContent3.ResumeLayout(false);
            layoutContent3.PerformLayout();
            panelContent2.ResumeLayout(false);
            layoutContent4.ResumeLayout(false);
            layoutContent4.PerformLayout();
            flowActions.ResumeLayout(false);
            flowActions.PerformLayout();
            flowActions2.ResumeLayout(false);
            flowActions2.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TableLayoutPanel layoutContent;
        private System.Windows.Forms.TableLayoutPanel layoutContent2;
        private System.Windows.Forms.Label lblPageTitle;
        private System.Windows.Forms.Panel panelContent;
        private System.Windows.Forms.TableLayoutPanel layoutContent3;
        private System.Windows.Forms.Panel panelContent2;
        private System.Windows.Forms.TableLayoutPanel layoutContent4;
        private System.Windows.Forms.Label lblNoticeTitle;
        private System.Windows.Forms.TextBox txtNoticeTitle;
        private System.Windows.Forms.Label lblNoticeAudience;
        private System.Windows.Forms.ComboBox cmbNoticeAudience;
        private System.Windows.Forms.Label lblPinToTop;
        private System.Windows.Forms.CheckBox chkPinToTop;
        private System.Windows.Forms.Label lblNoticeBody;
        private System.Windows.Forms.TextBox txtNoticeBody;
        private System.Windows.Forms.Label lblAttachments;
        private System.Windows.Forms.TextBox txtAttachments;
        private System.Windows.Forms.FlowLayoutPanel flowActions;
        private System.Windows.Forms.Button btnSelectAttachment;
        private System.Windows.Forms.FlowLayoutPanel flowActions2;
        private System.Windows.Forms.Button btnPublishNotice;
        private System.Windows.Forms.Button btnClose;
    }
}
