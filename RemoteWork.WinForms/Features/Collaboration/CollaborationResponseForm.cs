using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration;

/// <summary>Closing postpones a decision. Only a confirmed server result completes it.</summary>
public sealed class CollaborationResponseForm : Form
{
    private readonly ICollaborationService service;
    private readonly CollaborationEntry item;
    private readonly CancellationTokenSource lifetime;
    private readonly CancellationTokenRegistration identityEnded;
    private readonly Label status = new() { AutoSize = false, Height = 52, Dock = DockStyle.Bottom };
    private readonly FlowLayoutPanel actions = new() { Dock = DockStyle.Bottom, Height = 38, AutoScroll = true };
    private readonly Button resolve = new() { Text = "결과 확인", AutoSize = true, Visible = false };
    private readonly List<Button> decisionButtons = [];
    private CollaborationOperation? operation;
    private bool busy, retryAllowed, ended;
    public bool Confirmed { get; private set; }
    public CollaborationResponseForm(ICollaborationService service, CollaborationEntry item)
    {
        this.service = service; this.item = item;
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(service.Cancellation);
        identityEnded = CollaborationUiLifetime.CloseOnIdentityEnd(this, service);
        Text = item.RequesterId == service.UserId ? "협업 요청 응답" : "받은 협업 요청";
        ClientSize = new Size(390, 275); MinimumSize = new Size(360, 270);
        FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false;
        ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; Padding = new Padding(14);
        var details = new TextBox { Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None,
            Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, BackColor = SystemColors.Window,
            Text = $"{item.RequesterName} · {item.TaskTitle}\r\n{item.Title}\r\n\r\n{item.RequestText}\r\n\r\n희망 기한: {item.DesiredDate:yyyy-MM-dd}\r\n상태: {CollaborationEntry.StatusText(item.Status)}{(item.IsLate ? " · 희망 기한이 지난 협업입니다(지연)." : "")}" };
        Controls.Add(details); Controls.Add(status); Controls.Add(actions);
        if (item.RecipientId == service.UserId && item.Status == "pending")
        { AddDecision("승인", "accept"); AddDecision("거절", "decline"); }
        if (item.RequesterId == service.UserId && item.Status is "pending" or "accepted") AddDecision("철회", "withdraw");
        if (item.RequesterId == service.UserId && item.Status == "accepted") AddDecision("협업 완료", "complete");
        if (item.RecipientId == service.UserId && item.Status == "accepted") AddDecision("참여 종료", "leave");
        actions.Controls.Add(resolve);
        resolve.Click += async (_, _) => await ResolveAsync();
        var close = new Button { Text = "닫기", AutoSize = true }; close.Click += (_, _) => Close(); actions.Controls.Add(close);
        Shown += (_, _) =>
        {
            var area = Screen.FromControl(Owner ?? this).WorkingArea;
            Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);
        };
        FormClosed += (_, _) => lifetime.Cancel();
        Disposed += (_, _) => { if (!ended) { ended = true; identityEnded.Dispose(); lifetime.Cancel(); lifetime.Dispose(); } };
    }
    private void AddDecision(string text, string decision)
    {
        var button = new Button { Text = text, AutoSize = true, Tag = decision };
        decisionButtons.Add(button); actions.Controls.Add(button);
        button.Click += async (_, _) => await DecideAsync(decision);
    }
    private bool Current => !ended && !IsDisposed && !Disposing && service.IsCurrent && !lifetime.IsCancellationRequested;
    private void SetBusy(bool value)
    {
        busy = value;
        foreach (var button in decisionButtons) button.Enabled = !value && operation is null && Current;
        resolve.Enabled = !value && Current;
    }
    private void Done()
    {
        if (!Current) return;
        Confirmed = true; operation = null; Close();
    }
    private async Task DecideAsync(string decision)
    {
        if (busy || !Current || operation is not null) return;
        SetBusy(true);
        try
        {
            operation = await service.PrepareResponseAsync(item, decision, lifetime.Token);
            if (!Current) return;
            await service.ExecuteAsync(operation, lifetime.Token);
            Done();
        }
        catch (CollaborationOutcomeUnknownException ex) { if (Current) { status.Text = ex.Message; resolve.Visible = true; } }
        catch (OperationCanceledException) { }
        catch (ApiException ex) { if (Current) { operation = null; status.Text = ex.Message + " 목록을 새로 조회해 주세요."; } }
        finally { if (Current) SetBusy(false); }
    }
    private async Task ResolveAsync()
    {
        if (busy || !Current || operation is null) return;
        SetBusy(true);
        try
        {
            if (retryAllowed) { await service.ExecuteAsync(operation, lifetime.Token); Done(); return; }
            var result = await service.ResolveAsync(operation, lifetime.Token);
            if (!Current) return;
            if (result is not null) { Done(); return; }
            // No automatic replay: retain the exact key/body and require another click.
            retryAllowed = true; resolve.Text = "동일 응답 재전송";
            status.Text = "저장된 결과가 없습니다. 같은 응답을 다시 보내거나 닫고 목록을 확인해 주세요.";
        }
        catch (OperationCanceledException) { }
        catch (CollaborationOutcomeUnknownException ex) { if (Current) { retryAllowed = false; resolve.Text = "결과 확인"; status.Text = ex.Message; } }
        catch (ApiException ex) { if (Current) { retryAllowed = false; resolve.Text = "결과 확인"; status.Text = ex.Message; } }
        finally { if (Current) SetBusy(false); }
    }
}
