#nullable disable

namespace RemoteWork.WinForms.Features.Tasks.Attachments;

partial class TaskManagementAttachmentsForm
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing) Stop();
        if (disposing && components != null) components.Dispose();
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        layout = new TableLayoutPanel();
        lblTitle = new Label();
        lblHint = new Label();
        gridFiles = new DataGridView();
        colName = new DataGridViewTextBoxColumn();
        colSize = new DataGridViewTextBoxColumn();
        colState = new DataGridViewTextBoxColumn();
        lblCount = new Label();
        txtState = new TextBox();
        actions = new FlowLayoutPanel();
        btnAdd = new Button();
        btnUpload = new Button();
        btnRetry = new Button();
        btnDelete = new Button();
        btnRemove = new Button();
        btnClose = new Button();
        layout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)gridFiles).BeginInit();
        actions.SuspendLayout();
        SuspendLayout();
        // layout
        layout.ColumnCount = 1;
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowCount = 6;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Dock = DockStyle.Fill;
        layout.Padding = new Padding(18);
        layout.Name = "layout";
        layout.Controls.Add(lblTitle, 0, 0);
        layout.Controls.Add(lblHint, 0, 1);
        layout.Controls.Add(gridFiles, 0, 2);
        layout.Controls.Add(lblCount, 0, 3);
        layout.Controls.Add(txtState, 0, 4);
        layout.Controls.Add(actions, 0, 5);
        // title and hint
        lblTitle.Text = "업무 수정 첨부 파일";
        lblTitle.Name = "lblTitle";
        lblTitle.Dock = DockStyle.Fill;
        lblTitle.Font = new Font("맑은 고딕", 17F, FontStyle.Bold);
        lblTitle.ForeColor = Color.FromArgb(30, 42, 61);
        lblHint.Text = "최대 10개 · 파일당 25 MiB\r\n기존 첨부는 ‘업무에서 제외’, 새 파일은 업로드 후 ‘변경 실행’으로 반영합니다.";
        lblHint.Name = "lblHint";
        lblHint.Dock = DockStyle.Fill;
        // grid
        gridFiles.Name = "gridFiles";
        gridFiles.Dock = DockStyle.Fill;
        gridFiles.ReadOnly = true;
        gridFiles.AllowUserToAddRows = false;
        gridFiles.AllowUserToDeleteRows = false;
        gridFiles.AllowUserToResizeRows = false;
        gridFiles.MultiSelect = false;
        gridFiles.RowHeadersVisible = false;
        gridFiles.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        gridFiles.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridFiles.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        gridFiles.BackgroundColor = Color.White;
        gridFiles.BorderStyle = BorderStyle.FixedSingle;
        gridFiles.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        gridFiles.DefaultCellStyle.Padding = new Padding(4);
        gridFiles.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        gridFiles.Columns.AddRange(colName, colSize, colState);
        gridFiles.SelectionChanged += GridFiles_SelectionChanged;
        gridFiles.CurrentCellChanged += GridFiles_SelectionChanged;
        colName.Name = "colName";
        colName.HeaderText = "파일 이름";
        colName.FillWeight = 58F;
        colName.MinimumWidth = 180;
        colName.SortMode = DataGridViewColumnSortMode.NotSortable;
        colSize.Name = "colSize";
        colSize.HeaderText = "크기";
        colSize.FillWeight = 17F;
        colSize.MinimumWidth = 90;
        colSize.SortMode = DataGridViewColumnSortMode.NotSortable;
        colState.Name = "colState";
        colState.HeaderText = "상태";
        colState.FillWeight = 25F;
        colState.MinimumWidth = 160;
        colState.SortMode = DataGridViewColumnSortMode.NotSortable;
        // selected-file status
        lblCount.Name = "lblCount";
        lblCount.Text = "선택 파일 없음";
        lblCount.Dock = DockStyle.Fill;
        lblCount.TextAlign = ContentAlignment.MiddleLeft;
        txtState.Name = "txtState";
        txtState.Dock = DockStyle.Fill;
        txtState.ReadOnly = true;
        txtState.Multiline = true;
        txtState.ScrollBars = ScrollBars.Vertical;
        txtState.WordWrap = true;
        txtState.BackColor = Color.White;
        // actions
        actions.Name = "actions";
        actions.Dock = DockStyle.Fill;
        actions.AutoSize = true;
        actions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        actions.WrapContents = true;
        actions.Padding = new Padding(0, 8, 0, 0);
        actions.Controls.Add(btnAdd);
        actions.Controls.Add(btnUpload);
        actions.Controls.Add(btnRetry);
        actions.Controls.Add(btnDelete);
        actions.Controls.Add(btnRemove);
        actions.Controls.Add(btnClose);
        btnAdd.Name = "btnAdd";
        btnAdd.Text = "파일 선택";
        btnAdd.AutoSize = true;
        btnAdd.MinimumSize = new Size(92, 34);
        btnAdd.Click += Add_Click;
        btnUpload.Name = "btnUpload";
        btnUpload.Text = "업로드";
        btnUpload.AutoSize = true;
        btnUpload.MinimumSize = new Size(88, 34);
        btnUpload.Click += Upload_Click;
        btnRetry.Name = "btnRetry";
        btnRetry.Text = "전송 재시도";
        btnRetry.AutoSize = true;
        btnRetry.MinimumSize = new Size(100, 34);
        btnRetry.Click += Retry_Click;
        btnDelete.Name = "btnDelete";
        btnDelete.Text = "임시 파일 삭제";
        btnDelete.AutoSize = true;
        btnDelete.MinimumSize = new Size(116, 34);
        btnDelete.Click += Delete_Click;
        btnRemove.Name = "btnRemove";
        btnRemove.Text = "선택 목록에서 지우기";
        btnRemove.AutoSize = true;
        btnRemove.MinimumSize = new Size(152, 34);
        btnRemove.Click += Remove_Click;
        btnClose.Name = "btnClose";
        btnClose.Text = "닫기";
        btnClose.AutoSize = true;
        btnClose.MinimumSize = new Size(76, 34);
        btnClose.Click += Close_Click;
        // form
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        Font = new Font("맑은 고딕", 9F);
        BackColor = Color.FromArgb(242, 245, 250);
        ClientSize = new Size(820, 540);
        MinimumSize = new Size(680, 460);
        Controls.Add(layout);
        Name = "TaskManagementAttachmentsForm";
        Text = "업무 수정 첨부";
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        layout.ResumeLayout(false);
        layout.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)gridFiles).EndInit();
        actions.ResumeLayout(false);
        actions.PerformLayout();
        ResumeLayout(false);
    }
    #endregion

    private TableLayoutPanel layout;
    private Label lblTitle;
    private Label lblHint;
    private DataGridView gridFiles;
    private DataGridViewTextBoxColumn colName;
    private DataGridViewTextBoxColumn colSize;
    private DataGridViewTextBoxColumn colState;
    private Label lblCount;
    private TextBox txtState;
    private FlowLayoutPanel actions;
    private Button btnAdd;
    private Button btnUpload;
    private Button btnRetry;
    private Button btnDelete;
    private Button btnRemove;
    private Button btnClose;
}
