namespace RemoteWork.WinForms.Shell;

public partial class MainForm
{
    private Label? workspaceConnectionNotice;
    private RowStyle? workspaceConnectionNoticeRow;
    public Task PendingWorkspaceConnection { get; internal set; } = Task.CompletedTask;

    internal void SetWorkspaceConnectionNotice(string message)
    {
        if (IsDisposed || Disposing) return;
        if (workspaceConnectionNotice is null)
        {
            workspaceConnectionNotice = new Label
            {
                Name = "workspaceConnectionNotice", Dock = DockStyle.Fill, AutoSize = true,
                Padding = new Padding(10, 6, 10, 6), Margin = new Padding(0),
                BackColor = Color.FromArgb(255, 249, 226), ForeColor = Color.FromArgb(105, 76, 12)
            };
            int row = layoutWorkspace.RowCount++;
            workspaceConnectionNoticeRow = new RowStyle(SizeType.AutoSize);
            layoutWorkspace.RowStyles.Add(workspaceConnectionNoticeRow);
            layoutWorkspace.SetRowSpan(layoutSidebar, layoutWorkspace.RowCount);
            layoutWorkspace.Controls.Add(workspaceConnectionNotice, 1, row);
        }
        workspaceConnectionNotice.Text = message;
        workspaceConnectionNotice.Visible = message.Length != 0;
        workspaceConnectionNoticeRow!.SizeType = message.Length == 0 ? SizeType.Absolute : SizeType.AutoSize;
        workspaceConnectionNoticeRow.Height = 0;
    }
}
