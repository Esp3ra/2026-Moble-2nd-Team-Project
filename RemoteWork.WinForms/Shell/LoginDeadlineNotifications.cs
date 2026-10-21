using System.ComponentModel;
using RemoteWork.WinForms.Features.DeadlineAlerts;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Shell;

/// <summary>Explicit composition with a real source; never manufactures events or HTTP endpoints.</summary>
public sealed class LoginDeadlineNotifications : Component
{
    private readonly Form owner;
    private readonly CapturedSource source;
    private readonly Func<Func<string, Task<bool>>> captureNavigator;
    private readonly CancellationTokenRegistration identityEnded;
    private bool stopped, navigating;
    public DeadlineAlertScope Scope { get; }
    public bool? LastNavigationSucceeded { get; private set; }
    public string NavigationStatus { get; private set; } = "";

    public LoginDeadlineNotifications(Form owner, ApiClient.Session session,
        IDeadlineAlertSource source, Func<Func<string, Task<bool>>> captureNavigator)
    {
        if (!owner.IsHandleCreated || owner.InvokeRequired || owner.IsDisposed)
            throw new InvalidOperationException("살아 있는 로그인 화면의 UI 스레드에서 연결해 주세요.");
        session.ThrowIfChanged();
        if (source.UserId != session.User.Id || !source.IsCurrent || source.Cancellation.IsCancellationRequested)
            throw new ArgumentException("같은 캡처 로그인으로 만든 실제 알림 source가 필요합니다.", nameof(source));
        this.owner = owner;
        this.captureNavigator = captureNavigator ?? throw new ArgumentNullException(nameof(captureNavigator));
        this.source = new CapturedSource(session, source);
        try { Scope = new DeadlineAlertScope(owner, this.source); }
        catch { this.source.Dispose(); throw; }
        Scope.TaskRequested += OpenTask;
        owner.FormClosing += OwnerClosing;
        owner.Disposed += OwnerDisposed;
        identityEnded = this.source.Cancellation.Register(() =>
        {
            if (!owner.IsHandleCreated || owner.IsDisposed) return;
            try { owner.BeginInvoke((Action)Dispose); }
            catch (InvalidOperationException) { }
        });
    }

    public Task StartAsync() => !stopped ? Scope.StartAsync() : Task.CompletedTask;

    private async void OpenTask(string taskId)
    {
        if (stopped || navigating || !source.IsCurrent) return;
        navigating = true;
        LastNavigationSucceeded = null;
        NavigationStatus = "업무 조회 중";
        try
        {
            // Factory activates the current task page and captures its UI/login lifetime.
            var navigate = captureNavigator();
            if (stopped || !source.IsCurrent) return;
            bool displayed = await navigate(taskId);
            if (stopped || !source.IsCurrent) return;
            LastNavigationSucceeded = displayed;
            NavigationStatus = displayed ? "요청한 내 업무 조회 완료" : "업무를 표시하지 못했습니다. 현재 업무 화면을 확인해 주세요.";
        }
        catch (OperationCanceledException) { if (!stopped && source.IsCurrent) NavigationStatus = "업무 조회가 취소되었습니다."; }
        catch (Exception)
        {
            // This is an async event boundary. A failed read never confirms an alert or grants access.
            if (!stopped && source.IsCurrent)
            {
                LastNavigationSucceeded = false;
                NavigationStatus = "업무 조회 중 오류가 발생했습니다. 현재 업무 화면을 확인해 주세요.";
            }
        }
        finally { navigating = false; }
    }

    private void OwnerClosing(object? sender, FormClosingEventArgs e) => Dispose();
    private void OwnerDisposed(object? sender, EventArgs e) => Dispose();
    protected override void Dispose(bool disposing)
    {
        if (disposing && !stopped)
        {
            stopped = true;
            Scope.TaskRequested -= OpenTask;
            owner.FormClosing -= OwnerClosing;
            owner.Disposed -= OwnerDisposed;
            identityEnded.Dispose();
            Scope.Dispose();
            source.Dispose();
            LastNavigationSucceeded = null;
            NavigationStatus = "접속이 종료되었습니다.";
        }
        base.Dispose(disposing);
    }

    private sealed class CapturedSource : IDeadlineAlertSource, IDisposable
    {
        private readonly ApiClient.Session session;
        private readonly IDeadlineAlertSource inner;
        private readonly CancellationTokenSource lifetime;
        public CapturedSource(ApiClient.Session session, IDeadlineAlertSource inner)
        {
            this.session = session; this.inner = inner;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(session.Cancellation, inner.Cancellation);
        }
        public int UserId => session.User.Id;
        public bool IsCurrent => session.IsCurrent && inner.IsCurrent && !lifetime.IsCancellationRequested;
        public CancellationToken Cancellation => lifetime.Token;
        private async Task<T> Current<T>(Func<CancellationToken, Task<T>> call, CancellationToken ct)
        {
            session.ThrowIfChanged();
            if (!IsCurrent) throw new OperationCanceledException();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token);
            var result = await call(linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            session.ThrowIfChanged();
            if (!IsCurrent) throw new OperationCanceledException();
            return result;
        }
        public Task<DeadlineAlertSnapshot> ReadAsync(CancellationToken ct) => Current(inner.ReadAsync, ct);
        public Task<DeadlineAlertSnapshot> RecheckAsync(string token, CancellationToken ct) => Current(c => inner.RecheckAsync(token, c), ct);
        public Task<DeadlineConfirmation> ConfirmAsync(string token, CancellationToken ct) => Current(c => inner.ConfirmAsync(token, c), ct);
        public Task<DeadlineConfirmation> ResolveConfirmationAsync(string token, CancellationToken ct) => Current(c => inner.ResolveConfirmationAsync(token, c), ct);
        // The feature owns its source/transport. Dispose only our linked lifetime.
        public void Dispose() { lifetime.Cancel(); lifetime.Dispose(); }
    }
}
