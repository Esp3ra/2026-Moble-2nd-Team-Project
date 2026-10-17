#nullable disable

namespace RemoteWork.WinForms.Features.Collaboration;

partial class CreateChatRoomForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private void InitializeComponent()
    {
        layoutMain = new TableLayoutPanel();
        lblHeading = new Label();
        lblDescription = new Label();
        layoutFields = new TableLayoutPanel();
        lblTitle = new Label();
        txtTitle = new TextBox();
        lblType = new Label();
        flowType = new FlowLayoutPanel();
        rdoPrivate = new RadioButton();
        rdoGroup = new RadioButton();
        lblSelf = new Label();
        lblCurrentUser = new Label();
        lblParticipantHint = new Label();
        lstParticipants = new CheckedListBox();
        lblSelection = new Label();
        lblError = new Label();
        flowActions = new FlowLayoutPanel();
        btnCancel = new Button();
        btnCreate = new Button();
        layoutMain.SuspendLayout();
        layoutFields.SuspendLayout();
        flowType.SuspendLayout();
        flowActions.SuspendLayout();
        SuspendLayout();
        // 
        // layoutMain
        // 
        layoutMain.ColumnCount = 1;
        layoutMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutMain.Controls.Add(lblHeading, 0, 0);
        layoutMain.Controls.Add(lblDescription, 0, 1);
        layoutMain.Controls.Add(layoutFields, 0, 2);
        layoutMain.Controls.Add(lblParticipantHint, 0, 3);
        layoutMain.Controls.Add(lstParticipants, 0, 4);
        layoutMain.Controls.Add(lblSelection, 0, 5);
        layoutMain.Controls.Add(lblError, 0, 6);
        layoutMain.Controls.Add(flowActions, 0, 7);
        layoutMain.Dock = DockStyle.Fill;
        layoutMain.Location = new Point(22, 22);
        layoutMain.Margin = new Padding(0);
        layoutMain.Name = "layoutMain";
        layoutMain.RowCount = 8;
        layoutMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        layoutMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        layoutMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));
        layoutMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        layoutMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layoutMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        layoutMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        layoutMain.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
        layoutMain.Size = new Size(636, 636);
        layoutMain.TabIndex = 0;
        // 
        // lblHeading
        // 
        lblHeading.Dock = DockStyle.Fill;
        lblHeading.Font = new Font("맑은 고딕", 18F, FontStyle.Bold);
        lblHeading.Location = new Point(0, 0);
        lblHeading.Margin = new Padding(0);
        lblHeading.Name = "lblHeading";
        lblHeading.Size = new Size(636, 34);
        lblHeading.TabIndex = 0;
        lblHeading.Text = "대화방 만들기";
        // 
        // lblDescription
        // 
        lblDescription.Dock = DockStyle.Fill;
        lblDescription.ForeColor = Color.FromArgb(103, 116, 135);
        lblDescription.Location = new Point(0, 34);
        lblDescription.Margin = new Padding(0);
        lblDescription.Name = "lblDescription";
        lblDescription.Size = new Size(636, 32);
        lblDescription.TabIndex = 1;
        lblDescription.Text = "제목과 참여자를 선택해 주세요. 현재는 이 작업 화면에 임시로 저장됩니다.";
        lblDescription.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // layoutFields
        // 
        layoutFields.ColumnCount = 2;
        layoutFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100F));
        layoutFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layoutFields.Controls.Add(lblTitle, 0, 0);
        layoutFields.Controls.Add(txtTitle, 1, 0);
        layoutFields.Controls.Add(lblType, 0, 1);
        layoutFields.Controls.Add(flowType, 1, 1);
        layoutFields.Controls.Add(lblSelf, 0, 2);
        layoutFields.Controls.Add(lblCurrentUser, 1, 2);
        layoutFields.Dock = DockStyle.Fill;
        layoutFields.Location = new Point(0, 66);
        layoutFields.Margin = new Padding(0);
        layoutFields.Name = "layoutFields";
        layoutFields.RowCount = 3;
        layoutFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        layoutFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        layoutFields.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
        layoutFields.Size = new Size(636, 132);
        layoutFields.TabIndex = 0;
        // 
        // lblTitle
        // 
        lblTitle.Dock = DockStyle.Fill;
        lblTitle.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
        lblTitle.Location = new Point(3, 0);
        lblTitle.Name = "lblTitle";
        lblTitle.Size = new Size(94, 44);
        lblTitle.TabIndex = 0;
        lblTitle.Text = "대화방 제목";
        lblTitle.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // txtTitle
        // 
        txtTitle.Dock = DockStyle.Fill;
        txtTitle.Location = new Point(100, 8);
        txtTitle.Margin = new Padding(0, 8, 0, 6);
        txtTitle.MaxLength = 60;
        txtTitle.Name = "txtTitle";
        txtTitle.PlaceholderText = "제목을 입력해 주세요 (최대 60자)";
        txtTitle.Size = new Size(536, 23);
        txtTitle.TabIndex = 0;
        txtTitle.TextChanged += Title_TextChanged;
        // 
        // lblType
        // 
        lblType.Dock = DockStyle.Fill;
        lblType.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
        lblType.Location = new Point(3, 44);
        lblType.Name = "lblType";
        lblType.Size = new Size(94, 44);
        lblType.TabIndex = 1;
        lblType.Text = "대화 구분";
        lblType.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // flowType
        // 
        flowType.Controls.Add(rdoPrivate);
        flowType.Controls.Add(rdoGroup);
        flowType.Dock = DockStyle.Fill;
        flowType.Location = new Point(100, 44);
        flowType.Margin = new Padding(0);
        flowType.Name = "flowType";
        flowType.Size = new Size(536, 44);
        flowType.TabIndex = 1;
        // 
        // rdoPrivate
        // 
        rdoPrivate.AutoSize = true;
        rdoPrivate.Checked = true;
        rdoPrivate.Location = new Point(0, 10);
        rdoPrivate.Margin = new Padding(0, 10, 24, 0);
        rdoPrivate.Name = "rdoPrivate";
        rdoPrivate.Size = new Size(49, 19);
        rdoPrivate.TabIndex = 0;
        rdoPrivate.TabStop = true;
        rdoPrivate.Text = "개인";
        rdoPrivate.CheckedChanged += RoomType_Changed;
        // 
        // rdoGroup
        // 
        rdoGroup.AutoSize = true;
        rdoGroup.Location = new Point(73, 10);
        rdoGroup.Margin = new Padding(0, 10, 0, 0);
        rdoGroup.Name = "rdoGroup";
        rdoGroup.Size = new Size(49, 19);
        rdoGroup.TabIndex = 1;
        rdoGroup.Text = "그룹";
        rdoGroup.CheckedChanged += RoomType_Changed;
        // 
        // lblSelf
        // 
        lblSelf.Dock = DockStyle.Fill;
        lblSelf.Font = new Font("맑은 고딕", 9F, FontStyle.Bold);
        lblSelf.Location = new Point(3, 88);
        lblSelf.Name = "lblSelf";
        lblSelf.Size = new Size(94, 44);
        lblSelf.TabIndex = 2;
        lblSelf.Text = "참여자 · 본인";
        lblSelf.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblCurrentUser
        // 
        lblCurrentUser.Dock = DockStyle.Fill;
        lblCurrentUser.Location = new Point(100, 88);
        lblCurrentUser.Margin = new Padding(0);
        lblCurrentUser.Name = "lblCurrentUser";
        lblCurrentUser.Size = new Size(536, 44);
        lblCurrentUser.TabIndex = 3;
        lblCurrentUser.Text = "본인은 자동으로 포함됩니다.";
        lblCurrentUser.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblParticipantHint
        // 
        lblParticipantHint.Dock = DockStyle.Fill;
        lblParticipantHint.Location = new Point(0, 198);
        lblParticipantHint.Margin = new Padding(0);
        lblParticipantHint.Name = "lblParticipantHint";
        lblParticipantHint.Size = new Size(636, 36);
        lblParticipantHint.TabIndex = 2;
        lblParticipantHint.Text = "개인: 상대방 1명을 선택합니다. 본인을 포함해 총 2명입니다.";
        lblParticipantHint.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lstParticipants
        // 
        lstParticipants.BackColor = Color.White;
        lstParticipants.BorderStyle = BorderStyle.FixedSingle;
        lstParticipants.CheckOnClick = true;
        lstParticipants.Dock = DockStyle.Fill;
        lstParticipants.Font = new Font("맑은 고딕", 11F);
        lstParticipants.IntegralHeight = false;
        lstParticipants.Location = new Point(0, 234);
        lstParticipants.Margin = new Padding(0, 0, 0, 6);
        lstParticipants.Name = "lstParticipants";
        lstParticipants.Size = new Size(636, 274);
        lstParticipants.TabIndex = 1;
        lstParticipants.ItemCheck += Participants_ItemCheck;
        // 
        // lblSelection
        // 
        lblSelection.Dock = DockStyle.Fill;
        lblSelection.ForeColor = Color.FromArgb(103, 116, 135);
        lblSelection.Location = new Point(0, 514);
        lblSelection.Margin = new Padding(0);
        lblSelection.Name = "lblSelection";
        lblSelection.Size = new Size(636, 28);
        lblSelection.TabIndex = 3;
        lblSelection.Text = "상대방 0명 선택 · 본인 포함 1명";
        lblSelection.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // lblError
        // 
        lblError.Dock = DockStyle.Fill;
        lblError.ForeColor = Color.Firebrick;
        lblError.Location = new Point(0, 542);
        lblError.Margin = new Padding(0);
        lblError.Name = "lblError";
        lblError.Size = new Size(636, 44);
        lblError.TabIndex = 4;
        lblError.TextAlign = ContentAlignment.MiddleLeft;
        // 
        // flowActions
        // 
        flowActions.Controls.Add(btnCancel);
        flowActions.Controls.Add(btnCreate);
        flowActions.Dock = DockStyle.Fill;
        flowActions.FlowDirection = FlowDirection.RightToLeft;
        flowActions.Location = new Point(0, 586);
        flowActions.Margin = new Padding(0);
        flowActions.Name = "flowActions";
        flowActions.Padding = new Padding(0, 8, 0, 0);
        flowActions.Size = new Size(636, 50);
        flowActions.TabIndex = 2;
        flowActions.WrapContents = false;
        // 
        // btnCancel
        // 
        btnCancel.BackColor = Color.White;
        btnCancel.DialogResult = DialogResult.Cancel;
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(213, 220, 231);
        btnCancel.FlatStyle = FlatStyle.Flat;
        btnCancel.Location = new Point(540, 8);
        btnCancel.Margin = new Padding(8, 0, 0, 0);
        btnCancel.Name = "btnCancel";
        btnCancel.Size = new Size(96, 36);
        btnCancel.TabIndex = 1;
        btnCancel.Text = "취소";
        btnCancel.UseVisualStyleBackColor = false;
        // 
        // btnCreate
        // 
        btnCreate.BackColor = Color.FromArgb(52, 101, 224);
        btnCreate.Enabled = false;
        btnCreate.FlatAppearance.BorderColor = Color.FromArgb(52, 101, 224);
        btnCreate.FlatStyle = FlatStyle.Flat;
        btnCreate.ForeColor = Color.White;
        btnCreate.Location = new Point(428, 8);
        btnCreate.Margin = new Padding(8, 0, 0, 0);
        btnCreate.Name = "btnCreate";
        btnCreate.Size = new Size(104, 36);
        btnCreate.TabIndex = 0;
        btnCreate.Text = "만들기";
        btnCreate.UseVisualStyleBackColor = false;
        btnCreate.Click += Create_Click;
        // 
        // CreateChatRoomForm
        // 
        AcceptButton = btnCreate;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(242, 245, 250);
        CancelButton = btnCancel;
        ClientSize = new Size(680, 680);
        Controls.Add(layoutMain);
        Font = new Font("맑은 고딕", 9F);
        ForeColor = Color.FromArgb(30, 42, 61);
        MaximizeBox = false;
        MinimizeBox = false;
        MinimumSize = new Size(620, 640);
        Name = "CreateChatRoomForm";
        Padding = new Padding(22);
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "대화방 만들기";
        layoutMain.ResumeLayout(false);
        layoutFields.ResumeLayout(false);
        layoutFields.PerformLayout();
        flowType.ResumeLayout(false);
        flowType.PerformLayout();
        flowActions.ResumeLayout(false);
        ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel layoutMain;
    private Label lblHeading;
    private Label lblDescription;
    private TableLayoutPanel layoutFields;
    private Label lblTitle;
    private TextBox txtTitle;
    private Label lblType;
    private FlowLayoutPanel flowType;
    private RadioButton rdoPrivate;
    private RadioButton rdoGroup;
    private Label lblSelf;
    private Label lblCurrentUser;
    private Label lblParticipantHint;
    private CheckedListBox lstParticipants;
    private Label lblSelection;
    private Label lblError;
    private FlowLayoutPanel flowActions;
    private Button btnCreate;
    private Button btnCancel;
}
