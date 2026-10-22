using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.DeadlineAlerts;

public sealed class DeadlineAlertInboxForm : Form
{
    private readonly DeadlineAlertScope scope;
    private readonly ListBox list = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 48 };
    private readonly Button confirm = new() { Text = "확인", AutoSize = true };
    private bool busy;
    public DeadlineAlertInboxForm(DeadlineAlertScope scope)
    {
        this.scope = scope;
        Text = "미확인 마감 알림"; ClientSize = new Size(570, 330); Padding = new Padding(14);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 38 };
        var refresh = new Button { Text = "새로고침", AutoSize = true };
        refresh.Click += async (_, _) => await scope.CheckAsync();
        confirm.Click += async (_, _) => await ConfirmSelectedAsync();
        actions.Controls.AddRange([refresh, confirm]);
        Controls.Add(list); Controls.Add(status); Controls.Add(actions);
        scope.Changed += RefreshItems;
        Disposed += (_, _) => scope.Changed -= RefreshItems;
        list.SelectedIndexChanged += (_, _) => UpdateConfirm();
        RefreshItems();
    }
    private sealed record Row(DeadlineAlert Item)
    { public override string ToString() => $"{Item.DueDate:yyyy-MM-dd} · {Item.Title}"; }
    private void RefreshItems()
    {
        if (IsDisposed) return;
        string? selected = (list.SelectedItem as Row)?.Item.EventToken;
        list.Items.Clear();
        foreach (var item in scope.Unread) list.Items.Add(new Row(item));
        var match = list.Items.Cast<Row>().FirstOrDefault(x => x.Item.EventToken == selected);
        if (match is not null) list.SelectedItem = match;
        else if (list.Items.Count > 0) list.SelectedIndex = 0;
        status.Text = scope.Status;
        UpdateConfirm();
    }
    private void UpdateConfirm()
    {
        string? token = (list.SelectedItem as Row)?.Item.EventToken;
        confirm.Enabled = !busy && scope.IsCurrent && token is not null;
        confirm.Text = token is not null && scope.NeedsResolution(token) ? "결과 확인" : "확인";
    }
    private async Task ConfirmSelectedAsync()
    {
        if (busy || !scope.IsCurrent || list.SelectedItem is not Row row) return;
        busy = true; UpdateConfirm();
        try
        {
            var result = await scope.ConfirmAsync(row.Item.EventToken);
            if (IsDisposed || !scope.IsCurrent) return;
            RefreshItems();
            status.Text = result.State switch
            {
                DeadlineConfirmationState.Confirmed => result.Snapshot is null
                    ? "확인 완료 · 목록 재조회 필요" : "확인되었습니다.",
                DeadlineConfirmationState.Changed => "업무 또는 기한이 변경됐습니다. 현재 목록을 확인해 주세요.",
                DeadlineConfirmationState.NotRecorded => "미기록입니다. 확인을 다시 누르면 명시 재시도합니다.",
                _ => "결과가 불명확합니다. 결과 확인을 눌러 주세요."
            };
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or InvalidDataException or OperationCanceledException)
        { if (!IsDisposed && scope.IsCurrent) status.Text = "확인 결과를 조회해 주세요. " + ex.Message; }
        finally { busy = false; if (!IsDisposed) UpdateConfirm(); }
    }
}
