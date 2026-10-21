using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration;

public sealed class CollaborationInboxForm : Form
{
    private readonly ICollaborationService service;
    private readonly CancellationTokenSource lifetime;
    private readonly CancellationTokenRegistration identityEnded;
    private readonly ListBox list = new() { Dock = DockStyle.Fill };
    private readonly ComboBox scope = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 50 };
    private readonly Button refresh = new() { Text = "조회", AutoSize = true };
    private readonly Button open = new() { Text = "내용·응답", AutoSize = true };
    private bool loading, ended;
    public CollaborationInboxForm(ICollaborationService service)
    {
        this.service = service; lifetime = CancellationTokenSource.CreateLinkedTokenSource(service.Cancellation);
        identityEnded = CollaborationUiLifetime.CloseOnIdentityEnd(this, service);
        Text = "협업 요청 내역"; ClientSize = new Size(700, 430); Padding = new Padding(14);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
        scope.Items.AddRange(["받은 요청", "보낸 요청"]); scope.SelectedIndex = 0;
        bar.Controls.AddRange([scope, refresh, open]);
        Controls.Add(list); Controls.Add(status); Controls.Add(bar);
        refresh.Click += async (_, _) => await ReloadAsync();
        scope.SelectedIndexChanged += async (_, _) => await ReloadAsync();
        open.Click += (_, _) => OpenSelected(); list.DoubleClick += (_, _) => OpenSelected();
        Shown += async (_, _) => await ReloadAsync();
        FormClosed += (_, _) => lifetime.Cancel();
        Disposed += (_, _) => { if (!ended) { ended = true; identityEnded.Dispose(); lifetime.Cancel(); lifetime.Dispose(); } };
    }
    private bool Current => !ended && !IsDisposed && service.IsCurrent && !lifetime.IsCancellationRequested;
    public async Task ReloadAsync()
    {
        if (loading || !Current) return;
        loading = true; refresh.Enabled = scope.Enabled = open.Enabled = false;
        list.Items.Clear(); status.Text = "조회 중...";
        try
        {
            var entries = await service.ListAsync(scope.SelectedIndex == 1, lifetime.Token);
            if (!Current) return;
            list.Items.AddRange(entries.OrderByDescending(x => x.UpdatedAt).ToArray());
            status.Text = entries.Count == 0 ? "협업 요청이 없습니다." : $"{entries.Count}건 · 요청을 선택하고 내용·응답을 눌러 주세요.";
        }
        catch (OperationCanceledException) { }
        catch (ApiException ex) { if (Current) status.Text = "조회 실패: " + ex.Message; }
        finally { if (Current) { loading = false; refresh.Enabled = scope.Enabled = open.Enabled = true; } }
    }
    private void OpenSelected()
    {
        if (!Current || loading || list.SelectedItem is not CollaborationEntry item || OwnedForms.Any(f => f.Visible)) return;
        var dialog = new CollaborationResponseForm(service, item);
        dialog.FormClosed += async (_, _) => { if (Current) await ReloadAsync(); dialog.Dispose(); };
        dialog.Show(this);
    }
}
