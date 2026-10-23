using System.ComponentModel;
using System.Runtime.CompilerServices;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.DeadlineAlerts;

/// <summary>One receiver per captured login. Menu views borrow, never dispose it.</summary>
public sealed class DeadlineAlertScope : Component
{
    private static readonly ConditionalWeakTable<Form, DeadlineAlertScope> owners = new();
    private readonly Form owner;
    private readonly IDeadlineAlertSource source;
    private readonly System.Windows.Forms.Timer timer = new();
    private readonly CancellationTokenSource lifetime;
    private readonly CancellationTokenRegistration identitySubscription;
    private readonly HashSet<string> displayed = new(StringComparer.Ordinal);
    private readonly HashSet<string> unresolved = new(StringComparer.Ordinal);
    // Login-local proof from a validated source receipt, not a replacement for
    // account-wide DB confirmation or a source-owned W1 key/body/receipt store.
    private readonly HashSet<string> confirmed = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<DeadlineConfirmation>> confirming = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (string TaskId, int Revision)> eventBindings = new(StringComparer.Ordinal);
    private DeadlineAlertForm? popup;
    private bool stopped, checking, confirmationNeedsRefresh;
    private int failures;
    // Shared request order for polling, navigation rechecks and confirmations.
    // A response may update the view only while its request is still newest.
    private long generation;
    public DeadlineAlertSnapshot? Snapshot { get; private set; }
    public string Status { get; private set; } = "마감 알림 수신 준비 중";
    public TimeSpan NextInterval => TimeSpan.FromMilliseconds(timer.Interval);
    public bool IsCurrent => !stopped && !owner.IsDisposed && !owner.Disposing && source.IsCurrent && !source.Cancellation.IsCancellationRequested;
    public event Action? Changed;
    public event Action<string>? TaskRequested;
    public bool CanOpenTask => IsCurrent && Snapshot is not null && TaskRequested is not null;

    public DeadlineAlertScope(Form owner, IDeadlineAlertSource source)
    {
        if (!source.IsCurrent || source.UserId < 1) throw new ArgumentException("현재 로그인 연결이 필요합니다.", nameof(source));
        if (owners.TryGetValue(owner, out _)) throw new InvalidOperationException("이미 이 로그인에 마감 알림 수신이 있습니다.");
        this.owner = owner; this.source = source;
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(source.Cancellation);
        timer.Interval = (int)DeadlineAlertPolicy.NormalInterval.TotalMilliseconds;
        timer.Tick += async (_, _) => await CheckAsync();
        owners.Add(owner, this);
        owner.Disposed += OwnerDisposed;
        owner.FormClosed += OwnerClosed;
        identitySubscription = source.Cancellation.Register(() =>
        {
            if (owner.IsHandleCreated && !owner.IsDisposed)
                try { owner.BeginInvoke((Action)(() => { if (!stopped) Dispose(); })); }
                catch (InvalidOperationException) { }
        });
    }
    public static DeadlineAlertScope? Find(Form? owner)
    {
        while (owner is not null)
        {
            if (owners.TryGetValue(owner, out var scope)) return scope;
            owner = owner.Owner;
        }
        return null;
    }
    public Task StartAsync()
    {
        if (!IsCurrent) return Task.CompletedTask;
        timer.Start();
        return CheckAsync();
    }
    public IReadOnlyList<DeadlineAlert> Unread => !IsCurrent || Snapshot is null ? [] : Snapshot.Items
        .Where(x => DeadlineAlertPolicy.IsEligible(x, source.UserId) && DeadlineAlertPolicy.HasOccurred(x, Snapshot.ServerTime))
        .OrderBy(x => x.DueDate).ThenBy(x => x.TaskId, StringComparer.Ordinal).ToArray();
    private bool CanPresent => IsCurrent && confirming.Count == 0 && popup is null && owner.Visible && owner.Enabled &&
        owner.WindowState != FormWindowState.Minimized && !Application.OpenForms.Cast<Form>().Any(f => f.Visible &&
        (f is ImportantNoticeForm || f is DeadlineAlertForm || f is DeadlineAlertInboxForm || f.GetType().FullName ==
            "RemoteWork.WinForms.Features.Collaboration.CollaborationResponseForm" || f.Modal));
    public async Task CheckAsync()
    {
        if (!IsCurrent) { Dispose(); return; }
        if (checking || confirming.Count != 0) return;
        checking = true;
        long readGeneration = ++generation;
        try
        {
            var ct = lifetime.Token;
            var snapshot = await source.ReadAsync(ct);
            if (!CurrentRead(readGeneration)) return;
            Apply(snapshot);
            if (!CurrentRead(readGeneration)) return;
            if (!CanPresent) { PollSucceeded(); return; }
            var candidate = Unread.FirstOrDefault(x => !displayed.Contains(x.EventToken) &&
                DeadlineAlertPolicy.CanPopup(x, source.UserId, snapshot.ServerTime));
            if (candidate is null) { PollSucceeded(); return; }
            // The initial list is not current authorization to show a popup.
            snapshot = await source.RecheckAsync(candidate.EventToken, ct);
            if (!CurrentRead(readGeneration)) return;
            Apply(snapshot);
            if (!CurrentRead(readGeneration)) return;
            PollSucceeded();
            candidate = snapshot.Items.FirstOrDefault(x => x.EventToken == candidate.EventToken &&
                DeadlineAlertPolicy.CanPopup(x, source.UserId, snapshot.ServerTime));
            if (candidate is null || displayed.Contains(candidate.EventToken) || !CanPresent) return;
            displayed.Add(candidate.EventToken);
            popup = new DeadlineAlertForm(this, candidate);
            var shown = popup;
            shown.FormClosed += (_, _) => { if (popup == shown) popup = null; shown.Dispose(); };
            shown.Show(owner);
        }
        catch (OperationCanceledException) { if (!IsCurrent) Dispose(); }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or InvalidDataException)
        {
            ReadFailed(ex, readGeneration);
        }
        finally { checking = false; }
    }
    private void Apply(DeadlineAlertSnapshot snapshot)
    {
        ValidateSnapshot(snapshot);
        Snapshot = snapshot;
        confirmationNeedsRefresh = false;
        foreach (var item in snapshot.Items.Where(x => x.IsConfirmed)) unresolved.Remove(item.EventToken);
        if (popup is not null && !snapshot.Items.Any(x => x.EventToken == popup.Item.EventToken &&
            DeadlineAlertPolicy.CanPopup(x, source.UserId, snapshot.ServerTime))) popup.Close();
        Status = $"미확인 마감 알림 {Unread.Count}건";
        Changed?.Invoke();
    }
    private void ValidateSnapshot(DeadlineAlertSnapshot snapshot)
    {
        DeadlineAlertPolicy.Validate(snapshot, source.UserId);
        // Action identity is source-bound. PR70 supplies a distinct server target
        // for each task/event/assignment. Reject any identity reused for another
        // binding; never reuse displayed/confirmed/unresolved state across it.
        if (snapshot.Items.Any(x => eventBindings.TryGetValue(x.EventToken, out var binding) &&
            binding != (x.TaskId, x.AssignmentRevision)))
            throw new InvalidDataException("배정이 변경되었습니다. 사건·배정 세대 연결을 확인해 주세요.");
        if (snapshot.Items.Any(x => confirmed.Contains(x.EventToken) && !x.IsConfirmed))
            throw new InvalidDataException("확인된 사건과 현재 알림 목록이 일치하지 않습니다. 목록을 다시 조회해 주세요.");
        foreach (var item in snapshot.Items) eventBindings[item.EventToken] = (item.TaskId, item.AssignmentRevision);
    }
    private void ConfirmationNeedsRefresh()
    {
        // No fabricated empty success and no use of stale relations for navigation.
        // The source retains the original W1 proof; subsequent recovery is a GET.
        confirmationNeedsRefresh = true;
        Snapshot = null;
        popup?.Close();
        Status = "확인 완료 · 목록 재조회 필요";
        Changed?.Invoke();
    }
    private bool CurrentRead(long request) => IsCurrent && generation == request;
    private void PollSucceeded()
    {
        // Applying the intermediate list is not success of a required recheck.
        failures = 0;
        timer.Interval = (int)DeadlineAlertPolicy.NormalInterval.TotalMilliseconds;
    }
    private void ReadFailed(Exception ex, long request)
    {
        // Revocation of this captured identity is terminal even if its response
        // arrives after another read. Old non-auth failures cannot clear newer UI.
        if (ex is ApiException { StatusCode: 401 or 403 }) { Dispose(); return; }
        if (!CurrentRead(request)) return;
        failures++;
        timer.Interval = (int)DeadlineAlertPolicy.RetryInterval(failures).TotalMilliseconds;
        Snapshot = null; popup?.Close();
        Status = (confirmationNeedsRefresh ? "확인 완료 · 목록 재조회 필요 · " : "마감 알림 조회 실패 · ") + ex.Message;
        Changed?.Invoke();
    }
    public bool NeedsResolution(string token) => unresolved.Contains(token);
    public async Task<bool> OpenTaskAsync(string token)
    {
        if (!CanOpenTask || confirming.Count != 0) return false;
        long readGeneration = ++generation;
        try
        {
            var snapshot = await source.RecheckAsync(token, lifetime.Token);
            if (!CurrentRead(readGeneration)) return false;
            Apply(snapshot);
            if (!CurrentRead(readGeneration)) return false;
            var item = Unread.FirstOrDefault(x => x.EventToken == token);
            if (item is null || !CanOpenTask) return false;
            TaskRequested?.Invoke(item.TaskId);
            return true;
        }
        catch (OperationCanceledException) { if (!IsCurrent) Dispose(); throw; }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or InvalidDataException)
        {
            bool relevant = CurrentRead(readGeneration) || ex is ApiException { StatusCode: 401 or 403 };
            ReadFailed(ex, readGeneration);
            if (!relevant) return false;
            throw;
        }
    }
    public Task<DeadlineConfirmation> ConfirmAsync(string token)
    {
        if (!IsCurrent) throw new OperationCanceledException();
        if (confirming.TryGetValue(token, out var running)) return running;
        // A repeated click may report the old success, never issue another write
        // or apply that old event's confirmation to a newly supplied event.
        if (confirmed.Contains(token))
            return Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Confirmed, Snapshot));
        if (confirming.Count != 0) throw new ApiException("다른 알림을 확인 중입니다. 잠시 뒤 다시 시도해 주세요.",409,"DEADLINE_CONFIRMATION_BUSY");
        if (!unresolved.Contains(token) && !Unread.Any(x => x.EventToken == token))
            throw new ApiException("현재 확인할 마감 알림이 아닙니다. 목록을 다시 조회해 주세요.",409,"DEADLINE_CONTEXT_CHANGED");
        var completion = new TaskCompletionSource<DeadlineConfirmation>(TaskCreationOptions.RunContinuationsAsynchronously);
        generation++;
        confirming.Add(token, completion.Task);
        _ = ExecuteConfirmationAsync(token, completion);
        return completion.Task;
    }
    private async Task ExecuteConfirmationAsync(string token, TaskCompletionSource<DeadlineConfirmation> completion)
    {
        bool saved = false;
        try
        {
            var ct = lifetime.Token;
            var result = unresolved.Contains(token)
                ? await source.ResolveConfirmationAsync(token, ct)
                : await source.ConfirmAsync(token, ct);
            if (!IsCurrent) throw new OperationCanceledException();
            if (result is null || !Enum.IsDefined(result.State))
                throw new InvalidDataException("확인 결과의 상태가 올바르지 않습니다.");
            generation++;
            if (result.State == DeadlineConfirmationState.Confirmed)
            {
                // Receipt proof is independent of the optional refresh result.
                saved = true;
                confirmed.Add(token);
                unresolved.Remove(token);
                bool currentSnapshot = false;
                if (result.Snapshot is not null)
                {
                    try { ValidateSnapshot(result.Snapshot); currentSnapshot = true; }
                    catch (InvalidDataException) { /* Keep success; discard only invalid current facts. */ }
                }
                if (currentSnapshot) Apply(result.Snapshot!);
                else
                {
                    ConfirmationNeedsRefresh();
                    result = result with { Snapshot = null };
                }
            }
            else
            {
                if (result.Snapshot is null)
                    throw new InvalidDataException("확인 응답에 현재 알림 목록이 없습니다.");
                Apply(result.Snapshot);
                if (result.State == DeadlineConfirmationState.Unknown) unresolved.Add(token);
                else unresolved.Remove(token);
            }
            completion.TrySetResult(result);
        }
        catch (Exception ex)
        {
            // An exception is not proof of no write. Source retains its exact
            // W1 operation; another click performs result lookup only.
            if (IsCurrent && !saved) unresolved.Add(token);
            completion.TrySetException(ex);
            if (ex is ApiException { StatusCode: 401 or 403 }) Dispose();
        }
        finally { confirming.Remove(token); }
    }
    private void OwnerDisposed(object? sender, EventArgs e) => Dispose();
    private void OwnerClosed(object? sender, FormClosedEventArgs e) => Dispose();
    protected override void Dispose(bool disposing)
    {
        if (disposing && !stopped)
        {
            stopped = true;
            owners.Remove(owner);
            owner.Disposed -= OwnerDisposed; owner.FormClosed -= OwnerClosed;
            timer.Stop(); timer.Dispose(); lifetime.Cancel(); popup?.Close();
            Snapshot = null; Status = "접속이 종료되었습니다.";
            confirmed.Clear(); unresolved.Clear();
            eventBindings.Clear();
            Changed?.Invoke(); Changed = null;
            TaskRequested = null;
            identitySubscription.Dispose();
            lifetime.Dispose();
        }
        base.Dispose(disposing);
    }
}
