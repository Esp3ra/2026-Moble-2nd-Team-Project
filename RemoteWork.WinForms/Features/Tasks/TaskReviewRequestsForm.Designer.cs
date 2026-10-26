#nullable disable
namespace RemoteWork.WinForms.Features.Tasks;

partial class TaskReviewRequestsForm
{
    private System.ComponentModel.IContainer components;
    protected override void Dispose(bool disposing) { if (disposing) components?.Dispose(); base.Dispose(disposing); }
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        layout = new TableLayoutPanel(); filters = new FlowLayoutPanel(); actions = new FlowLayoutPanel();
        cmbScope = new ComboBox(); btnRefresh = new Button(); btnAcknowledge = new Button(); btnResult = new Button();
        lblStatus = new Label(); txtDetail = new TextBox(); gridRequests = new DataGridView();
        SuspendLayout(); layout.SuspendLayout(); ((System.ComponentModel.ISupportInitialize)gridRequests).BeginInit();
        layout.Dock = DockStyle.Fill; layout.Padding = new Padding(18); layout.ColumnCount = 1; layout.RowCount = 5;
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        filters.Dock = DockStyle.Fill; filters.AutoSize = true;
        cmbScope.Name = "cmbScope"; cmbScope.DropDownStyle = ComboBoxStyle.DropDownList; cmbScope.Width = 180;
        cmbScope.Items.AddRange(new object[] { "내가 보낸 검토 요청", "내가 받은 검토 요청" }); cmbScope.SelectedIndexChanged += ScopeChanged;
        btnRefresh.Name = "btnRefresh"; btnRefresh.Text = "조회"; btnRefresh.AutoSize = true; btnRefresh.MinimumSize = new Size(90, 32); btnRefresh.Click += Refresh_Click;
        filters.Controls.AddRange(new Control[] { cmbScope, btnRefresh });
        lblStatus.Name = "lblStatus"; lblStatus.AutoSize = true; lblStatus.Dock = DockStyle.Fill; lblStatus.MinimumSize = new Size(0, 48);
        lblStatus.Text = "제출 결과의 별도 검토 요청입니다. 결과물 승인·반려는 결과물 메뉴에서 진행합니다.";
        gridRequests.Name = "gridRequests"; gridRequests.Dock = DockStyle.Fill; gridRequests.ReadOnly = true; gridRequests.MultiSelect = false;
        gridRequests.AllowUserToAddRows = gridRequests.AllowUserToDeleteRows = false; gridRequests.RowHeadersVisible = false;
        gridRequests.SelectionMode = DataGridViewSelectionMode.FullRowSelect; gridRequests.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        gridRequests.Columns.AddRange(new DataGridViewTextBoxColumn { Name = "colId", HeaderText = "요청 번호" },
            new DataGridViewTextBoxColumn { Name = "colTask", HeaderText = "업무", FillWeight = 160 },
            new DataGridViewTextBoxColumn { Name = "colRound", HeaderText = "제출 회차" },
            new DataGridViewTextBoxColumn { Name = "colRequester", HeaderText = "요청자" },
            new DataGridViewTextBoxColumn { Name = "colReviewer", HeaderText = "검토자" },
            new DataGridViewTextBoxColumn { Name = "colState", HeaderText = "요청 상태" },
            new DataGridViewTextBoxColumn { Name = "colTime", HeaderText = "요청 시각" });
        gridRequests.SelectionChanged += SelectionChanged;
        txtDetail.Name = "txtDetail"; txtDetail.Dock = DockStyle.Fill; txtDetail.Multiline = true; txtDetail.ReadOnly = true; txtDetail.ScrollBars = ScrollBars.Vertical;
        actions.Dock = DockStyle.Fill; actions.AutoSize = true; actions.WrapContents = true;
        btnAcknowledge.Name = "btnAcknowledge"; btnAcknowledge.Text = "요청 확인"; btnAcknowledge.AutoSize = true; btnAcknowledge.MinimumSize = new Size(110, 35); btnAcknowledge.Click += Acknowledge_Click;
        btnResult.Name = "btnResult"; btnResult.Text = "미확인 처리 결과"; btnResult.AutoSize = true; btnResult.MinimumSize = new Size(130, 35); btnResult.Click += Result_Click;
        actions.Controls.AddRange(new Control[] { btnAcknowledge, btnResult });
        layout.Controls.Add(filters, 0, 0); layout.Controls.Add(lblStatus, 0, 1); layout.Controls.Add(gridRequests, 0, 2); layout.Controls.Add(txtDetail, 0, 3); layout.Controls.Add(actions, 0, 4);
        AutoScaleDimensions = new SizeF(7F, 15F); AutoScaleMode = AutoScaleMode.Font; Font = new Font("맑은 고딕", 9F);
        ClientSize = new Size(1000, 620); MinimumSize = new Size(850, 520); StartPosition = FormStartPosition.CenterParent;
        Text = "검토 요청함"; Name = "TaskReviewRequestsForm"; Controls.Add(layout);
        ((System.ComponentModel.ISupportInitialize)gridRequests).EndInit(); layout.ResumeLayout(false); layout.PerformLayout(); ResumeLayout(false);
    }
    private TableLayoutPanel layout;
    private FlowLayoutPanel filters, actions;
    private ComboBox cmbScope;
    private Button btnRefresh, btnAcknowledge, btnResult;
    private Label lblStatus;
    private TextBox txtDetail;
    private DataGridView gridRequests;
}
