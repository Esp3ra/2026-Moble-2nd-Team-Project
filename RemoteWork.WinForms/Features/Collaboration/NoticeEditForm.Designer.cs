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
            components = new System.ComponentModel.Container();
            layoutContent = new System.Windows.Forms.TableLayoutPanel();
            layoutContent2 = new System.Windows.Forms.TableLayoutPanel();
            lblPageTitle = new System.Windows.Forms.Label();
            lblText = new System.Windows.Forms.Label();
            panelContent = new System.Windows.Forms.Panel();
            layoutContent3 = new System.Windows.Forms.TableLayoutPanel();
            panelContent2 = new System.Windows.Forms.Panel();
            layoutContent4 = new System.Windows.Forms.TableLayoutPanel();
            lblNoticeTitle = new System.Windows.Forms.Label();
            txtNoticeTitle = new System.Windows.Forms.TextBox();
            lblNoticeAudience = new System.Windows.Forms.Label();
            cmbNoticeAudience = new System.Windows.Forms.ComboBox();
            lblPinToTop = new System.Windows.Forms.Label();
            chkPinToTop = new System.Windows.Forms.CheckBox();
            lblNoticeBody = new System.Windows.Forms.Label();
            txtNoticeBody = new System.Windows.Forms.TextBox();
            flowActions = new System.Windows.Forms.FlowLayoutPanel();
            btnSelectAttachment = new System.Windows.Forms.Button();
            flowActions2 = new System.Windows.Forms.FlowLayoutPanel();
            btnPublishNotice = new System.Windows.Forms.Button();
            btnClose = new System.Windows.Forms.Button();
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
            layoutContent.Controls.Add(flowActions2, 0, 2);
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
            layoutContent2.Controls.Add(lblText, 0, 1);
            //
            // lblPageTitle
            //
            lblPageTitle.Text = "공지 작성";
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
            // lblText
            //
            lblText.Text = "공지 대상과 내용을 작성합니다.";
            lblText.Location = new System.Drawing.Point(0, 40);
            lblText.Size = new System.Drawing.Size(246, 15);
            lblText.MinimumSize = new System.Drawing.Size(0, 0);
            lblText.MaximumSize = new System.Drawing.Size(0, 0);
            lblText.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblText.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblText.Dock = System.Windows.Forms.DockStyle.None;
            lblText.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblText.AutoSize = true;
            lblText.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            lblText.ForeColor = System.Drawing.Color.FromArgb(255, 103, 116, 135);
            lblText.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblText.TabIndex = 1;
            lblText.TabStop = false;
            lblText.AutoEllipsis = false;
            lblText.TextAlign = System.Drawing.ContentAlignment.TopLeft;
            lblText.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblText.UseCompatibleTextRendering = false;
            lblText.Name = "lblText";
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
            layoutContent3.MinimumSize = new System.Drawing.Size(0, 0);
            layoutContent3.MaximumSize = new System.Drawing.Size(0, 0);
            layoutContent3.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent3.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            layoutContent3.Dock = System.Windows.Forms.DockStyle.Fill;
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
            layoutContent3.RowCount = 2;
            layoutContent3.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent3.Name = "layoutContent3";
            layoutContent3.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent3.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize, 0F));
            layoutContent3.Controls.Add(panelContent2, 0, 0);
            layoutContent3.Controls.Add(flowActions, 0, 1);
            //
            // panelContent2
            //
            panelContent2.Text = "";
            panelContent2.Location = new System.Drawing.Point(0, 0);
            panelContent2.Size = new System.Drawing.Size(719, 364);
            panelContent2.MinimumSize = new System.Drawing.Size(0, 0);
            panelContent2.MaximumSize = new System.Drawing.Size(0, 0);
            panelContent2.Margin = new System.Windows.Forms.Padding(0, 0, 0, 0);
            panelContent2.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            panelContent2.Dock = System.Windows.Forms.DockStyle.Fill;
            panelContent2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            panelContent2.AutoSize = false;
            panelContent2.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            panelContent2.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            panelContent2.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            panelContent2.TabIndex = 0;
            panelContent2.TabStop = false;
            panelContent2.AutoScroll = true;
            panelContent2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            panelContent2.BorderStyle = System.Windows.Forms.BorderStyle.None;
            panelContent2.Name = "panelContent2";
            panelContent2.Controls.Add(layoutContent4);
            //
            // layoutContent4
            //
            layoutContent4.Text = "";
            layoutContent4.Location = new System.Drawing.Point(0, 0);
            layoutContent4.Size = new System.Drawing.Size(719, 244);
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
            layoutContent4.TabIndex = 0;
            layoutContent4.TabStop = false;
            layoutContent4.AutoScroll = false;
            layoutContent4.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            layoutContent4.ColumnCount = 2;
            layoutContent4.RowCount = 4;
            layoutContent4.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.AddRows;
            layoutContent4.Name = "layoutContent4";
            layoutContent4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 116F));
            layoutContent4.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 44F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 40F));
            layoutContent4.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            layoutContent4.Controls.Add(lblNoticeTitle, 0, 0);
            layoutContent4.Controls.Add(txtNoticeTitle, 1, 0);
            layoutContent4.Controls.Add(lblNoticeAudience, 0, 1);
            layoutContent4.Controls.Add(cmbNoticeAudience, 1, 1);
            layoutContent4.Controls.Add(lblPinToTop, 0, 2);
            layoutContent4.Controls.Add(chkPinToTop, 1, 2);
            layoutContent4.Controls.Add(lblNoticeBody, 0, 3);
            layoutContent4.Controls.Add(txtNoticeBody, 1, 3);
            //
            // lblNoticeTitle
            //
            lblNoticeTitle.Text = "공지 제목";
            lblNoticeTitle.Location = new System.Drawing.Point(0, 4);
            lblNoticeTitle.Size = new System.Drawing.Size(108, 36);
            lblNoticeTitle.MinimumSize = new System.Drawing.Size(0, 0);
            lblNoticeTitle.MaximumSize = new System.Drawing.Size(0, 0);
            lblNoticeTitle.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblNoticeTitle.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblNoticeTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            lblNoticeTitle.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblNoticeTitle.AutoSize = false;
            lblNoticeTitle.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblNoticeTitle.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblNoticeTitle.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblNoticeTitle.TabIndex = 0;
            lblNoticeTitle.TabStop = false;
            lblNoticeTitle.AutoEllipsis = false;
            lblNoticeTitle.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblNoticeTitle.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblNoticeTitle.UseCompatibleTextRendering = false;
            lblNoticeTitle.Name = "lblNoticeTitle";
            //
            // txtNoticeTitle
            //
            txtNoticeTitle.Text = "";
            txtNoticeTitle.Location = new System.Drawing.Point(116, 4);
            txtNoticeTitle.Size = new System.Drawing.Size(603, 28);
            txtNoticeTitle.MinimumSize = new System.Drawing.Size(0, 28);
            txtNoticeTitle.MaximumSize = new System.Drawing.Size(0, 0);
            txtNoticeTitle.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            txtNoticeTitle.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtNoticeTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            txtNoticeTitle.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtNoticeTitle.AutoSize = true;
            txtNoticeTitle.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtNoticeTitle.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            txtNoticeTitle.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtNoticeTitle.TabIndex = 1;
            txtNoticeTitle.TabStop = true;
            txtNoticeTitle.Multiline = false;
            txtNoticeTitle.ReadOnly = false;
            txtNoticeTitle.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtNoticeTitle.ScrollBars = System.Windows.Forms.ScrollBars.None;
            txtNoticeTitle.UseSystemPasswordChar = false;
            txtNoticeTitle.WordWrap = true;
            txtNoticeTitle.MaxLength = 32767;
            txtNoticeTitle.AcceptsReturn = false;
            txtNoticeTitle.AcceptsTab = false;
            txtNoticeTitle.Name = "txtNoticeTitle";
            //
            // lblNoticeAudience
            //
            lblNoticeAudience.Text = "공지 대상";
            lblNoticeAudience.Location = new System.Drawing.Point(0, 48);
            lblNoticeAudience.Size = new System.Drawing.Size(108, 36);
            lblNoticeAudience.MinimumSize = new System.Drawing.Size(0, 0);
            lblNoticeAudience.MaximumSize = new System.Drawing.Size(0, 0);
            lblNoticeAudience.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblNoticeAudience.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblNoticeAudience.Dock = System.Windows.Forms.DockStyle.Fill;
            lblNoticeAudience.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblNoticeAudience.AutoSize = false;
            lblNoticeAudience.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblNoticeAudience.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblNoticeAudience.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblNoticeAudience.TabIndex = 2;
            lblNoticeAudience.TabStop = false;
            lblNoticeAudience.AutoEllipsis = false;
            lblNoticeAudience.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblNoticeAudience.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblNoticeAudience.UseCompatibleTextRendering = false;
            lblNoticeAudience.Name = "lblNoticeAudience";
            //
            // cmbNoticeAudience
            //
            cmbNoticeAudience.Text = "전 직원";
            cmbNoticeAudience.Location = new System.Drawing.Point(116, 48);
            cmbNoticeAudience.Size = new System.Drawing.Size(603, 26);
            cmbNoticeAudience.MinimumSize = new System.Drawing.Size(0, 0);
            cmbNoticeAudience.MaximumSize = new System.Drawing.Size(0, 0);
            cmbNoticeAudience.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            cmbNoticeAudience.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            cmbNoticeAudience.Dock = System.Windows.Forms.DockStyle.Fill;
            cmbNoticeAudience.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            cmbNoticeAudience.AutoSize = false;
            cmbNoticeAudience.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            cmbNoticeAudience.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            cmbNoticeAudience.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            cmbNoticeAudience.TabIndex = 3;
            cmbNoticeAudience.TabStop = true;
            cmbNoticeAudience.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbNoticeAudience.IntegralHeight = true;
            cmbNoticeAudience.MaxDropDownItems = 8;
            cmbNoticeAudience.Name = "cmbNoticeAudience";
            cmbNoticeAudience.Items.AddRange(new object[] { "전 직원", "기획팀", "개발팀", "디자인팀" });
            //
            // lblPinToTop
            //
            lblPinToTop.Text = "게시 설정";
            lblPinToTop.Location = new System.Drawing.Point(0, 92);
            lblPinToTop.Size = new System.Drawing.Size(108, 36);
            lblPinToTop.MinimumSize = new System.Drawing.Size(0, 0);
            lblPinToTop.MaximumSize = new System.Drawing.Size(0, 0);
            lblPinToTop.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblPinToTop.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblPinToTop.Dock = System.Windows.Forms.DockStyle.Fill;
            lblPinToTop.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblPinToTop.AutoSize = false;
            lblPinToTop.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblPinToTop.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblPinToTop.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblPinToTop.TabIndex = 4;
            lblPinToTop.TabStop = false;
            lblPinToTop.AutoEllipsis = false;
            lblPinToTop.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblPinToTop.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblPinToTop.UseCompatibleTextRendering = false;
            lblPinToTop.Name = "lblPinToTop";
            //
            // chkPinToTop
            //
            chkPinToTop.Text = "상단 고정";
            chkPinToTop.AutoSize = true;
            chkPinToTop.Dock = System.Windows.Forms.DockStyle.Fill;
            chkPinToTop.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            chkPinToTop.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            chkPinToTop.Name = "chkPinToTop";
            chkPinToTop.TabIndex = 5;
            chkPinToTop.UseVisualStyleBackColor = true;
            //
            // lblNoticeBody
            //
            lblNoticeBody.Text = "공지 내용";
            lblNoticeBody.Location = new System.Drawing.Point(0, 136);
            lblNoticeBody.Size = new System.Drawing.Size(108, 104);
            lblNoticeBody.MinimumSize = new System.Drawing.Size(0, 0);
            lblNoticeBody.MaximumSize = new System.Drawing.Size(0, 0);
            lblNoticeBody.Margin = new System.Windows.Forms.Padding(0, 4, 8, 4);
            lblNoticeBody.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            lblNoticeBody.Dock = System.Windows.Forms.DockStyle.Fill;
            lblNoticeBody.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            lblNoticeBody.AutoSize = false;
            lblNoticeBody.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            lblNoticeBody.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            lblNoticeBody.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            lblNoticeBody.TabIndex = 6;
            lblNoticeBody.TabStop = false;
            lblNoticeBody.AutoEllipsis = false;
            lblNoticeBody.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblNoticeBody.BorderStyle = System.Windows.Forms.BorderStyle.None;
            lblNoticeBody.UseCompatibleTextRendering = false;
            lblNoticeBody.Name = "lblNoticeBody";
            //
            // txtNoticeBody
            //
            txtNoticeBody.Text = "";
            txtNoticeBody.Location = new System.Drawing.Point(116, 136);
            txtNoticeBody.Size = new System.Drawing.Size(603, 102);
            txtNoticeBody.MinimumSize = new System.Drawing.Size(0, 90);
            txtNoticeBody.MaximumSize = new System.Drawing.Size(0, 0);
            txtNoticeBody.Margin = new System.Windows.Forms.Padding(0, 4, 0, 6);
            txtNoticeBody.Padding = new System.Windows.Forms.Padding(0, 0, 0, 0);
            txtNoticeBody.Dock = System.Windows.Forms.DockStyle.Fill;
            txtNoticeBody.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            txtNoticeBody.AutoSize = true;
            txtNoticeBody.Font = new System.Drawing.Font("맑은 고딕", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            txtNoticeBody.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            txtNoticeBody.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            txtNoticeBody.TabIndex = 7;
            txtNoticeBody.TabStop = true;
            txtNoticeBody.Multiline = true;
            txtNoticeBody.ReadOnly = false;
            txtNoticeBody.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            txtNoticeBody.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            txtNoticeBody.UseSystemPasswordChar = false;
            txtNoticeBody.WordWrap = true;
            txtNoticeBody.MaxLength = 32767;
            txtNoticeBody.AcceptsReturn = false;
            txtNoticeBody.AcceptsTab = false;
            txtNoticeBody.Name = "txtNoticeBody";
            //
            // flowActions
            //
            flowActions.Text = "";
            flowActions.Location = new System.Drawing.Point(0, 364);
            flowActions.Size = new System.Drawing.Size(719, 41);
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
            flowActions.TabIndex = 1;
            flowActions.TabStop = false;
            flowActions.AutoScroll = false;
            flowActions.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowActions.FlowDirection = System.Windows.Forms.FlowDirection.LeftToRight;
            flowActions.WrapContents = true;
            flowActions.Name = "flowActions";
            flowActions.Controls.Add(btnSelectAttachment);
            //
            // btnSelectAttachment
            //
            btnSelectAttachment.Text = "첨부파일 선택";
            btnSelectAttachment.Location = new System.Drawing.Point(0, 0);
            btnSelectAttachment.Size = new System.Drawing.Size(119, 35);
            btnSelectAttachment.MinimumSize = new System.Drawing.Size(88, 34);
            btnSelectAttachment.MaximumSize = new System.Drawing.Size(0, 0);
            btnSelectAttachment.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnSelectAttachment.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnSelectAttachment.Dock = System.Windows.Forms.DockStyle.None;
            btnSelectAttachment.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnSelectAttachment.AutoSize = true;
            btnSelectAttachment.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnSelectAttachment.ForeColor = System.Drawing.Color.FromArgb(255, 30, 42, 61);
            btnSelectAttachment.BackColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnSelectAttachment.TabIndex = 0;
            btnSelectAttachment.TabStop = true;
            btnSelectAttachment.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnSelectAttachment.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnSelectAttachment.UseVisualStyleBackColor = false;
            btnSelectAttachment.DialogResult = System.Windows.Forms.DialogResult.None;
            btnSelectAttachment.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnSelectAttachment.Cursor = System.Windows.Forms.Cursors.Hand;
            btnSelectAttachment.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 213, 220, 231);
            btnSelectAttachment.FlatAppearance.BorderSize = 1;
            btnSelectAttachment.Name = "btnSelectAttachment";
            btnSelectAttachment.Click += new System.EventHandler(Unavailable_Click);
            //
            // flowActions2
            //
            flowActions2.Text = "";
            flowActions2.Location = new System.Drawing.Point(0, 536);
            flowActions2.Size = new System.Drawing.Size(736, 42);
            flowActions2.MinimumSize = new System.Drawing.Size(0, 40);
            flowActions2.MaximumSize = new System.Drawing.Size(0, 0);
            flowActions2.Margin = new System.Windows.Forms.Padding(0, 0, 0, 8);
            flowActions2.Padding = new System.Windows.Forms.Padding(0, 10, 0, 0);
            flowActions2.Dock = System.Windows.Forms.DockStyle.Fill;
            flowActions2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            flowActions2.AutoSize = true;
            flowActions2.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            flowActions2.ForeColor = System.Drawing.Color.FromArgb(255, 0, 0, 0);
            flowActions2.BackColor = System.Drawing.Color.FromArgb(255, 242, 245, 250);
            flowActions2.TabIndex = 3;
            flowActions2.TabStop = false;
            flowActions2.AutoScroll = false;
            flowActions2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            flowActions2.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            flowActions2.WrapContents = true;
            flowActions2.Name = "flowActions2";
            flowActions2.Controls.Add(btnPublishNotice);
            flowActions2.Controls.Add(btnClose);
            //
            // btnPublishNotice
            //
            btnPublishNotice.Text = "공지 등록";
            btnPublishNotice.Location = new System.Drawing.Point(633, 10);
            btnPublishNotice.Size = new System.Drawing.Size(95, 35);
            btnPublishNotice.MinimumSize = new System.Drawing.Size(88, 34);
            btnPublishNotice.MaximumSize = new System.Drawing.Size(0, 0);
            btnPublishNotice.Margin = new System.Windows.Forms.Padding(0, 0, 8, 6);
            btnPublishNotice.Padding = new System.Windows.Forms.Padding(12, 4, 12, 4);
            btnPublishNotice.Dock = System.Windows.Forms.DockStyle.None;
            btnPublishNotice.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left;
            btnPublishNotice.AutoSize = true;
            btnPublishNotice.Font = new System.Drawing.Font("맑은 고딕", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            btnPublishNotice.ForeColor = System.Drawing.Color.FromArgb(255, 255, 255, 255);
            btnPublishNotice.BackColor = System.Drawing.Color.FromArgb(255, 52, 101, 224);
            btnPublishNotice.TabIndex = 0;
            btnPublishNotice.TabStop = true;
            btnPublishNotice.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            btnPublishNotice.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            btnPublishNotice.UseVisualStyleBackColor = false;
            btnPublishNotice.DialogResult = System.Windows.Forms.DialogResult.None;
            btnPublishNotice.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowOnly;
            btnPublishNotice.Cursor = System.Windows.Forms.Cursors.Hand;
            btnPublishNotice.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(255, 52, 101, 224);
            btnPublishNotice.FlatAppearance.BorderSize = 1;
            btnPublishNotice.Name = "btnPublishNotice";
            btnPublishNotice.Click += new System.EventHandler(Unavailable_Click);
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
            // NoticeEditForm
            //
            Text = "공지 작성";
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
            Name = "NoticeEditForm";
            Controls.Add(layoutContent);
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            CancelButton = btnClose;
            flowActions2.ResumeLayout(false);
            flowActions2.PerformLayout();
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
        private System.Windows.Forms.Label lblText;
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
        private System.Windows.Forms.FlowLayoutPanel flowActions;
        private System.Windows.Forms.Button btnSelectAttachment;
        private System.Windows.Forms.FlowLayoutPanel flowActions2;
        private System.Windows.Forms.Button btnPublishNotice;
        private System.Windows.Forms.Button btnClose;
    }
}
