using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration;

public sealed class CollaborationHistoryForm : Form
{
    private readonly ICollaborationService service;
    private readonly CollaborationEntry item;
    private readonly CancellationTokenSource lifetime;
    private readonly CancellationTokenRegistration identityEnded;
    private readonly ListBox list = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    private readonly Label status = new() { Dock = DockStyle.Bottom, Height = 45 };
    private bool ended;
    public CollaborationHistoryForm(ICollaborationService service, CollaborationEntry item)
    {
        this.service = service; this.item = item;
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(service.Cancellation);
        identityEnded = CollaborationUiLifetime.CloseOnIdentityEnd(this, service);
        Text = "협업 요청 상태 이력"; ClientSize = new Size(700, 340); Padding = new Padding(14);
        var title = new Label { Text = item.TaskTitle + " · " + item.Title, Dock = DockStyle.Top, Height = 40, AutoEllipsis = true };
        Controls.Add(list); Controls.Add(status); Controls.Add(title);
        Shown += async (_, _) => await LoadHistoryAsync();
        FormClosed += (_, _) => lifetime.Cancel();
        Disposed += (_, _) => { if (!ended) { ended = true; identityEnded.Dispose(); lifetime.Cancel(); lifetime.Dispose(); } };
    }
    public async Task LoadHistoryAsync()
    {
        if (ended || !service.IsCurrent) return;
        status.Text = "상태 이력을 조회하는 중입니다.";
        try
        {
            var entries = await service.GetHistoryAsync(item, lifetime.Token);
            if (ended || !service.IsCurrent || lifetime.IsCancellationRequested) return;
            list.Items.Clear(); list.Items.AddRange(entries.ToArray());
            status.Text = $"상태 이력 {entries.Count}건 · 시각은 한국 표준시(KST)입니다.";
        }
        catch (OperationCanceledException) { }
        catch (ApiException ex) { if (!ended && service.IsCurrent) status.Text = "이력 조회 실패: " + ex.Message; }
    }
}
