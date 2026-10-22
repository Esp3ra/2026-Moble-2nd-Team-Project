using System.ComponentModel;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration;

/// <summary>One coordinator per signed-in employee window, independent of the selected menu.</summary>
public sealed class ImportantNoticeCoordinator : Component
{
    private readonly Form owner;
    private readonly ApiClient api;
    private readonly ApiClient.Session session;
    private readonly string department;
    private readonly INoticePopupService service;
    private readonly int userId;
    private readonly Uri server;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 15000 };
    private readonly CancellationTokenSource lifetime = new();
    private readonly HashSet<string> shownThisLogin = new();
    private ImportantNoticeForm? popup;
    private bool checking;
    private bool stopped;
    private bool baselineInitialized;
    public event Action<string>? StatusChanged;
    public string? LastError { get; private set; }

    public ImportantNoticeCoordinator(Form owner, ApiClient api, INoticePopupService service, int userId)
    {
        this.owner = owner;
        this.api = api;
        session = api.CaptureSession();
        if (session.User.Id != userId) throw new OperationCanceledException("로그인이 변경되었습니다.");
        department = api.CurrentUser!.Department;
        this.service = service;
        this.userId = userId;
        server = api.ServerUri;
        owner.Shown += Start;
        owner.FormClosing += StopOnClosing;
        timer.Tick += Check;
    }

    private bool IsCurrent => session.IsCurrent && api.CurrentUser?.Department == department && !stopped && !owner.IsDisposed && !owner.Disposing &&
        api.CurrentUser is { Role: "employee" } user && user.Id == userId && api.ServerUri == server;

    private async void Start(object? sender, EventArgs e)
    {
        if (!IsCurrent) return;
        timer.Start();
        await CheckAsync();
    }

    private async void Check(object? sender, EventArgs e) => await CheckAsync();
    private void StopOnClosing(object? sender, FormClosingEventArgs e) => Dispose();

    private async Task CheckAsync()
    {
        if (!IsCurrent) { timer.Stop(); return; }
        if (checking || popup is not null) return;
        if (baselineInitialized && (!owner.Visible || !owner.Enabled ||
            owner.WindowState == FormWindowState.Minimized || owner.OwnedForms.Any(f => f.Visible && f.Modal))) return;
        checking = true;
        try
        {
            int afterId = 0;
            // The first complete snapshot is only a baseline, never a popup or acknowledgement.
            // Do not commit a partial baseline: a failed page must be retried before showing anything.
            var baselineKeys = baselineInitialized ? null : new HashSet<string>();
            while (IsCurrent)
            {
                var pending = await service.GetPendingPopupsAsync(afterId, lifetime.Token);
                if (!IsCurrent) return;
                LastError = null;
                if (baselineKeys is not null)
                {
                    foreach (var existing in pending.Items) baselineKeys.Add(existing.PopupKey);
                    if (pending.NextAfterId is int baselineNext)
                    {
                        afterId = baselineNext;
                        continue;
                    }
                    shownThisLogin.UnionWith(baselineKeys);
                    baselineInitialized = true;
                    StatusChanged?.Invoke("새 중요 공지 대기 · 15초마다 확인");
                    return;
                }
                var notice = pending.Items.FirstOrDefault(n => !shownThisLogin.Contains(n.PopupKey) &&
                    n.IsPinned && (n.Target == "전 직원" || n.Target == api.CurrentUser?.Department));
                if (notice is not null)
                {
                    // Recheck after every awaited page; yielding must not mark
                    // this notice shown, so the next poll can present it.
                    if (!NotificationPresentation.CanPresent(owner)) return;
                    var window = new ImportantNoticeForm(notice,
                        ct => service.ConfirmNoticeAsync(notice, ct));
                    popup = window;
                    shownThisLogin.Add(notice.PopupKey);
                    window.FormClosed += (_, _) =>
                    {
                        if (ReferenceEquals(popup, window)) popup = null;
                        // Do not repeat a dismissed popup. A new login baselines existing notices again.
                        if (IsCurrent && owner.IsHandleCreated)
                            owner.BeginInvoke((Action)(async () => await CheckAsync()));
                    };
                    window.Show(owner);
                    StatusChanged?.Invoke("중요 공지 팝업 표시 중");
                    return;
                }
                if (pending.NextAfterId is not int next)
                {
                    StatusChanged?.Invoke("새 중요 공지 대기 · 15초마다 확인");
                    return;
                }
                afterId = next;
            }
        }
        catch (OperationCanceledException) { /* Window/session ended; ignore obsolete responses. */ }
        catch (ApiException ex)
        {
            if (!IsCurrent) return;
            if (ex.StatusCode == 404)
            {
                // This server does not provide the separately enabled popup endpoint.
                // Retry only in a new window/session after server configuration changes.
                timer.Stop();
                LastError = null;
                StatusChanged?.Invoke("이 서버에서는 공지 팝업 기능을 제공하지 않습니다.");
                return;
            }
            LastError = ex.Message;
            StatusChanged?.Invoke(ex.Code == "NOTICE_POPUP_SCHEMA_REQUIRED"
                ? "공지 팝업 DB 준비 필요" : "공지 팝업 조회 실패 · 재연결 중");
        }
        finally { checking = false; }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !stopped)
        {
            stopped = true;
            owner.Shown -= Start;
            owner.FormClosing -= StopOnClosing;
            timer.Stop();
            timer.Tick -= Check;
            timer.Dispose();
            lifetime.Cancel();
            lifetime.Dispose();
            // Dispose also closes a popup currently waiting for its confirmation response.
            popup?.Dispose();
            popup = null;
            shownThisLogin.Clear();
        }
        base.Dispose(disposing);
    }
}
