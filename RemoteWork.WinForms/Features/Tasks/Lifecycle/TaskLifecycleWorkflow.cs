using System.Globalization;


namespace RemoteWork.WinForms.Features.Tasks.Lifecycle;

public enum TaskLifecycleState
{
    Editing, Sending, NeedsResultCheck, RetryAllowed, Succeeded, Blocked, Disconnected
}

public sealed record TaskLifecycleSnapshot(TaskLifecycleState State, bool IsBusy,
    TaskLifecycleRequest? PendingRequest, TaskLifecycleResult? Result, TaskLifecycleFailure? Failure,
    string Message, bool CanCheckResult, bool CanRetry);

// One login and task/action. A form borrows this across closing/navigation; no disk storage.
public sealed class TaskLifecycleWorkflow : IDisposable
{
    private readonly ITaskLifecycleService service;
    private readonly object gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationToken lifetimeToken;
    private readonly CancellationTokenRegistration sessionRegistration;
    private TaskLifecycleState state = TaskLifecycleState.Editing;
    private bool disposed, disconnected, busy;
    private long operation, highestSucceededVersion;
    private TaskLifecycleRequest? pendingRequest;
    private TaskLifecycleKey? key;
    private TaskLifecycleResult? result;
    private TaskLifecycleFailure? failure;
    private string message = "현재 업무와 취소·마감 연장 입력을 확인해 주세요.";
    private TaskLifecycleKind? boundKind;
    private string? boundTaskId;
    private string? boundExtensionId;

    public TaskLifecycleWorkflow(ITaskLifecycleService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        lifetimeToken = lifetime.Token;
        sessionRegistration = service.SessionCancellation.Register(OnSessionChanged);
    }

    internal TaskLifecycleWorkflow(ITaskLifecycleService service, TaskLifecycleKind kind, string taskId, string? extensionId = null) : this(service)
    { boundKind = kind; boundTaskId = taskId; boundExtensionId = extensionId; }

    public event EventHandler? StateChanged;
    public event EventHandler? Changed;
    public TaskLifecycleSnapshot Snapshot
    {
        get
        {
            lock (gate) return new(state, busy, pendingRequest, result, failure, message,
                !busy && state is TaskLifecycleState.NeedsResultCheck or TaskLifecycleState.RetryAllowed,
                !busy && state == TaskLifecycleState.RetryAllowed);
        }
    }
    public TaskLifecycleState State => Snapshot.State;
    public TaskLifecycleRequest? PendingRequest => Snapshot.PendingRequest;
    public TaskLifecycleResult? Result => Snapshot.Result;
    public TaskLifecycleFailure? Failure => Snapshot.Failure;
    public string Message => Snapshot.Message;
    public bool IsBusy => Snapshot.IsBusy;
    public bool CanCheckResult => Snapshot.CanCheckResult;
    public bool CanRetry => Snapshot.CanRetry;
    public int UserId => service.UserId;
    public CancellationToken SessionCancellation => service.SessionCancellation;
    public bool IsCurrent
    {
        get
        {
            lock (gate) if (disposed || disconnected) return false;
            // ApiClient identity lock must never be acquired while holding gate.
            return service.IsCurrent && !service.SessionCancellation.IsCancellationRequested;
        }
    }

    public Task SubmitAsync(TaskLifecycleRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RunAsync(OperationKind.Submit, request, ct);
    }
    public Task CheckResultAsync(CancellationToken ct = default) => RunAsync(OperationKind.Check, null, ct);
    public Task RetrySameRequestAsync(CancellationToken ct = default) => RunAsync(OperationKind.Retry, null, ct);

    // Call only after showing success and refreshing reads; a later rework needs a new checked version.
    public bool AcknowledgeSuccess()
    {
        if (!IsCurrent) return false;
        lock (gate)
        {
            if (disposed || disconnected || busy || state != TaskLifecycleState.Succeeded) return false;
            operation++;
            pendingRequest = null;
            key = null;
            result = null;
            failure = null;
            state = TaskLifecycleState.Editing;
            message = "최신 업무 정보를 다시 확인해 주세요.";
        }
        NotifyChanged();
        return true;
    }

    private async Task RunAsync(OperationKind kind, TaskLifecycleRequest? input, CancellationToken ct)
    {
        bool current = service.IsCurrent && !service.SessionCancellation.IsCancellationRequested;
        long mine = 0;
        TaskLifecycleRequest? body = null;
        TaskLifecycleKey? requestKey = null;
        bool started = false;
        lock (gate)
        {
            if (disposed || disconnected || busy) return;
            if (!current) DisconnectLocked(TaskLifecycleFailure.AuthenticationExpired);
            else if (Allowed(kind))
            {
                if (kind == OperationKind.Submit)
                {
                    if (input!.CheckedUserId != service.UserId)
                    { DisconnectLocked(TaskLifecycleFailure.Forbidden); }
                    else if ((boundTaskId is not null && (boundTaskId != input.TaskId || boundKind != input.Kind || boundExtensionId != input.ExtensionId))
                        || long.Parse(input.ExpectedRowVersion, CultureInfo.InvariantCulture) < highestSucceededVersion)
                    {
                        failure = TaskLifecycleFailure.InvalidInput;
                        message = "이미 확인한 요청입니다. 최신 업무 정보를 다시 조회해 주세요.";
                    }
                    else
                    {
                        boundKind = input.Kind;
                        boundTaskId = input.TaskId;
                        boundExtensionId = input.ExtensionId;
                        pendingRequest = input;
                        key = null;
                        result = null;
                        started = true;
                    }
                }
                else started = true;
                if (started)
                {
                    mine = ++operation;
                    body = pendingRequest;
                    requestKey = key;
                    busy = true;
                    state = TaskLifecycleState.Sending;
                    failure = null;
                    message = kind == OperationKind.Check ? "업무 취소·마감 연장 결과를 확인하고 있습니다." : "업무 취소·마감 연장을 요청하고 있습니다.";
                }
            }
            else return;
        }
        NotifyChanged();
        if (!started) return;
        bool mutationStarted = false;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetimeToken, service.SessionCancellation);
        try
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (body is null) throw new TaskLifecycleException(TaskLifecycleFailure.InvalidResponse);
            if (kind == OperationKind.Submit)
            {
                requestKey = await service.GetRequestKeyAsync(body, cancellation.Token).ConfigureAwait(false);
                if (!StillCurrent(mine)) return;
                lock (gate)
                {
                    if (disposed || disconnected || operation != mine) return;
                    key = requestKey;
                }
            }
            if (requestKey is null) throw new TaskLifecycleException(TaskLifecycleFailure.InvalidResponse);
            if (kind == OperationKind.Check)
            {
                var lookup = await service.GetResultAsync(body, requestKey, cancellation.Token).ConfigureAwait(false);
                if (lookup is null || lookup.Recorded != (lookup.Result is not null))
                    throw new TaskLifecycleException(TaskLifecycleFailure.InvalidResponse);
                if (lookup.Recorded) CompleteSuccess(mine, body, lookup.Result!);
                else CompleteNotRecorded(mine);
            }
            else
            {
                cancellation.Token.ThrowIfCancellationRequested();
                mutationStarted = true;
                var response = await service.ChangeAsync(body, requestKey, cancellation.Token).ConfigureAwait(false);
                CompleteSuccess(mine, body, response);
            }
        }
        catch (TaskLifecycleException error) { CompleteFailure(mine, kind, mutationStarted, error.Failure); }
        catch (OperationCanceledException) { CompleteFailure(mine, kind, mutationStarted, TaskLifecycleFailure.Unavailable, true); }
        catch (ArgumentException)
        { CompleteFailure(mine, kind, mutationStarted, mutationStarted ? TaskLifecycleFailure.InvalidResponse : TaskLifecycleFailure.InvalidInput); }
        catch (Exception) { CompleteFailure(mine, kind, mutationStarted, TaskLifecycleFailure.InvalidResponse); }
    }

    private bool Allowed(OperationKind kind) => kind switch
    {
        OperationKind.Submit => state == TaskLifecycleState.Editing,
        OperationKind.Check => state is TaskLifecycleState.NeedsResultCheck or TaskLifecycleState.RetryAllowed,
        OperationKind.Retry => state == TaskLifecycleState.RetryAllowed,
        _ => false
    };

    private bool StillCurrent(long mine)
    {
        bool current = service.IsCurrent && !service.SessionCancellation.IsCancellationRequested;
        bool active, notify = false;
        lock (gate)
        {
            active = !disposed && !disconnected && operation == mine;
            if (active && !current)
            { DisconnectLocked(TaskLifecycleFailure.AuthenticationExpired); notify = true; active = false; }
        }
        if (notify) NotifyChanged();
        return active;
    }

    private void CompleteSuccess(long mine, TaskLifecycleRequest request, TaskLifecycleResult response)
    {
        if (!TaskLifecycleFormat.MatchesResult(request, response)) throw new TaskLifecycleException(TaskLifecycleFailure.InvalidResponse);
        if (!StillCurrent(mine)) return;
        lock (gate)
        {
            if (disposed || disconnected || operation != mine) return;
            result = response;
            highestSucceededVersion = long.Parse(response.RowVersion, CultureInfo.InvariantCulture);
            busy = false;
            state = TaskLifecycleState.Succeeded;
            failure = null;
            message = "업무 취소·마감 연장 성공을 확인했습니다. 최신 업무 정보를 조회해 주세요.";
        }
        NotifyChanged();
    }

    private void CompleteNotRecorded(long mine)
    {
        if (!StillCurrent(mine)) return;
        lock (gate)
        {
            if (disposed || disconnected || operation != mine) return;
            busy = false;
            state = TaskLifecycleState.RetryAllowed;
            failure = null;
            message = "저장된 결과가 없습니다. 같은 요청 재전송 버튼으로 다시 시도할 수 있습니다.";
        }
        NotifyChanged();
    }

    private void CompleteFailure(long mine, OperationKind kind, bool mutationStarted, TaskLifecycleFailure reason, bool cancelled = false)
    {
        if (!StillCurrent(mine)) return;
        lock (gate)
        {
            if (disposed || disconnected || operation != mine) return;
            busy = false;
            failure = reason;
            message = TaskLifecycleException.MessageFor(reason);
            if (reason == TaskLifecycleFailure.AuthenticationExpired)
                DisconnectLocked(reason);
            else if (reason == TaskLifecycleFailure.Forbidden)
            {
                state = TaskLifecycleState.Blocked;
                message = "현재 권한이 없어 결과를 확인할 수 없습니다. 이전 변경이 반영됐을 수도 있으므로 관리자가 처리 내역을 확인해야 합니다. 자동 재전송하지 않습니다.";
            }
            else if (kind == OperationKind.Submit && !mutationStarted)
            {
                state = TaskLifecycleState.Editing;
                key = null;
                message = cancelled ? "처리 요청 전에 취소했습니다. 업무를 다시 확인해 주세요." : message;
            }
            else if (reason == TaskLifecycleFailure.KeyExpired)
                state = kind == OperationKind.Check ? TaskLifecycleState.Blocked : TaskLifecycleState.NeedsResultCheck;
            else if (reason is TaskLifecycleFailure.KeyReused or TaskLifecycleFailure.Conflict)
                state = TaskLifecycleState.Blocked;
            else if (kind == OperationKind.Check)
                state = TaskLifecycleState.NeedsResultCheck;
            else if (reason is TaskLifecycleFailure.InvalidInput or TaskLifecycleFailure.AssignmentChanged
                or TaskLifecycleFailure.VersionConflict or TaskLifecycleFailure.StateConflict or TaskLifecycleFailure.Cancelled
                or TaskLifecycleFailure.PendingExtension or TaskLifecycleFailure.TechnicalConnectionRequired
                or TaskLifecycleFailure.NotFound or TaskLifecycleFailure.NotConfigured)
            {
                state = TaskLifecycleState.Editing;
                key = null;
            }
            else state = TaskLifecycleState.NeedsResultCheck;
            if (state == TaskLifecycleState.NeedsResultCheck)
                message = "처리 여부가 불확실합니다. 처리 결과 확인 버튼을 눌러 주세요. " + message;
        }
        NotifyChanged();
    }

    private void OnSessionChanged()
    {
        lock (gate)
        {
            if (disposed || disconnected) return;
            DisconnectLocked(TaskLifecycleFailure.AuthenticationExpired);
        }
        NotifyChanged();
    }
    private void DisconnectLocked(TaskLifecycleFailure reason)
    {
        operation++;
        disconnected = true;
        busy = false;
        pendingRequest = null;
        key = null;
        result = null;
        state = TaskLifecycleState.Disconnected;
        failure = reason;
        message = TaskLifecycleException.MessageFor(reason);
    }
    private void NotifyChanged()
    {
        var handlers = StateChanged?.GetInvocationList().Concat(Changed?.GetInvocationList() ?? [])
            ?? Changed?.GetInvocationList() ?? [];
        foreach (EventHandler handler in handlers)
        {
            try { handler(this, EventArgs.Empty); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            DisconnectLocked(TaskLifecycleFailure.AuthenticationExpired);
        }
        sessionRegistration.Unregister();
        lifetime.Cancel();
        lifetime.Dispose();
        NotifyChanged();
    }
    private enum OperationKind { Submit, Check, Retry }
}
