using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.DeadlineAlerts;

public sealed class DeadlineAlertForm : Form
{
    public DeadlineAlert Item { get; }
    private readonly Label status = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Button confirm = new() { Text = "확인", AutoSize = true };
    private bool busy;
    public DeadlineAlertForm(DeadlineAlertScope scope, DeadlineAlert item)
    {
        Item = item;
        Text = "업무 마감 안내";
        ClientSize = new Size(370, 205); MinimumSize = new Size(350, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; Padding = new Padding(14);
        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        content.RowStyles.Add(new(SizeType.Percent, 65)); content.RowStyles.Add(new(SizeType.Percent, 35));
        content.RowStyles.Add(new(SizeType.Absolute, 35));
        content.Controls.Add(new TextBox { ReadOnly = true, Multiline = true, BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill, Text = $"{item.Title}\r\n마감: {item.DueDate:yyyy-MM-dd}\r\n업무 마감이 다가왔습니다." },0,0);
        content.Controls.Add(status,0,1);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var close = new Button { Text = "닫기", AutoSize = true };
        var task = new Button { Text = "업무 보기", AutoSize = true, Visible = scope.CanOpenTask };
        task.Click += async (_, _) =>
        {
            task.Enabled = false;
            try
            {
                if (!await scope.OpenTaskAsync(item.EventToken) && !IsDisposed)
                    status.Text = "업무가 변경되었습니다. 목록을 다시 조회해 주세요.";
            }
            catch (Exception ex) when (ex is ApiException or HttpRequestException or InvalidDataException or OperationCanceledException)
            { if (!IsDisposed) status.Text = ex.Message; }
            finally { if (!IsDisposed) task.Enabled = scope.CanOpenTask; }
        };
        close.Click += (_, _) => Close();
        actions.Controls.Add(close); actions.Controls.Add(confirm); actions.Controls.Add(task); content.Controls.Add(actions,0,2);
        Controls.Add(content);
        confirm.Click += async (_, _) =>
        {
            if (busy || !scope.IsCurrent) return;
            busy = true; confirm.Enabled = false;
            try
            {
                var result = await scope.ConfirmAsync(item.EventToken);
                if (IsDisposed || !scope.IsCurrent) return;
                if (result.State is DeadlineConfirmationState.Confirmed or DeadlineConfirmationState.Changed) Close();
                else status.Text = result.State == DeadlineConfirmationState.NotRecorded
                    ? "확인 저장은 미기록입니다. 다시 확인을 누르면 명시 재시도합니다."
                    : "확인 결과가 불명확합니다. 결과 확인을 눌러 주세요.";
            }
            catch (Exception ex) when (ex is ApiException or HttpRequestException or InvalidDataException or OperationCanceledException)
            { if (!IsDisposed) status.Text = "확인 결과를 조회해 주세요. " + ex.Message; }
            finally
            {
                busy = false;
                if (!IsDisposed) { confirm.Enabled = scope.IsCurrent; confirm.Text = scope.NeedsResolution(item.EventToken) ? "결과 확인" : "확인"; }
            }
        };
        Shown += (_, _) =>
        {
            var area = Screen.FromControl(Owner ?? this).WorkingArea;
            Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);
        };
    }
}
