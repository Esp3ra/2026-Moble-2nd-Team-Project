using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Preparation;

namespace RemoteWork.WinForms.Features.Tasks.Starting;

public enum TaskStartState
{
    Editing, Sending, NeedsResultCheck, RetryAllowed, Succeeded, Blocked, Disconnected
}

public sealed record TaskStartSnapshot(TaskStartState State, bool IsBusy,
    TaskStartRequest? PendingRequest, TaskStartResult? Result, TaskStartFailure? Failure,
    string Message, bool CanCheckResult, bool CanRetry);

// One login and task/action. A form borrows this across closing/navigation; no disk storage.
public sealed class TaskStartWorkflow : IDisposable
{
    private readonly ITaskStartService service;
    private readonly object gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationToken lifetimeToken;
    private readonly CancellationTokenRegistration sessionRegistration;
    private TaskStartState state = TaskStartState.Editing;
    private bool disposed, disconnected, busy;
    private long operation, highestSucceededVersion;
    private TaskStartRequest? pendingRequest;
    private TaskStartKey? key;
    private TaskStartResult? result;
    private TaskStartFailure? failure;
    private string message = "현재 본인 업무를 확인한 뒤 시작해 주세요.";
    private TaskStartKind? boundKind;
    private string? boundTaskId;

    public TaskStartWorkflow(ITaskStartService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        lifetimeToken = lifetime.Token;
        sessionRegistration = service.SessionCancellation.Register(OnSessionChanged);
    }

    internal TaskStartWorkflow(ITaskStartService service, TaskStartKind kind, string taskId) : this(service)
    { boundKind = kind; boundTaskId = taskId; }

    public event EventHandler? StateChanged;
    public event EventHandler? Changed;
    public TaskStartSnapshot Snapshot
    {
        get
        {
            lock (gate) return new(state, busy, pendingRequest, result, failure, message,
                !busy && state is TaskStartState.NeedsResultCheck or TaskStartState.RetryAllowed,
                !busy && state == TaskStartState.RetryAllowed);
        }
    }
    public TaskStartState State => Snapshot.State;
    public TaskStartRequest? PendingRequest => Snapshot.PendingRequest;
    public TaskStartResult? Result => Snapshot.Result;
    public TaskStartFailure? Failure => Snapshot.Failure;
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

    public Task SubmitAsync(TaskStartRequest request, CancellationToken ct = default)
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
            if (disposed || disconnected || busy || state != TaskStartState.Succeeded) return false;
            operation++;
            pendingRequest = null;
            key = null;
            result = null;
            failure = null;
            state = TaskStartState.Editing;
            message = "최신 업무 정보를 다시 확인해 주세요.";
        }
        NotifyChanged();
        return true;
    }

    private async Task RunAsync(OperationKind kind, TaskStartRequest? input, CancellationToken ct)
    {
        bool current = service.IsCurrent && !service.SessionCancellation.IsCancellationRequested;
        long mine = 0;
        TaskStartRequest? body = null;
        TaskStartKey? requestKey = null;
        bool started = false;
        lock (gate)
        {
            if (disposed || disconnected || busy) return;
            if (!current) DisconnectLocked(TaskStartFailure.AuthenticationExpired);
            else if (Allowed(kind))
            {
                if (kind == OperationKind.Submit)
                {
                    if (input!.CheckedUserId != service.UserId)
                    { DisconnectLocked(TaskStartFailure.Forbidden); }
                    else if ((boundTaskId is not null && (boundTaskId != input.TaskId || boundKind != input.Kind))
                        || long.Parse(input.ExpectedRowVersion, CultureInfo.InvariantCulture) <= highestSucceededVersion)
                    {
                        failure = TaskStartFailure.InvalidInput;
                        message = "이미 확인한 요청입니다. 최신 업무 정보를 다시 조회해 주세요.";
                    }
                    else
                    {
                        boundKind = input.Kind;
                        boundTaskId = input.TaskId;
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
                    state = TaskStartState.Sending;
                    failure = null;
                    message = kind == OperationKind.Check ? "업무 시작 결과를 확인하고 있습니다." : "업무 시작을 요청하고 있습니다.";
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
            if (body is null) throw new TaskStartException(TaskStartFailure.InvalidResponse);
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
            if (requestKey is null) throw new TaskStartException(TaskStartFailure.InvalidResponse);
            if (kind == OperationKind.Check)
            {
                var lookup = await service.GetResultAsync(body, requestKey, cancellation.Token).ConfigureAwait(false);
                if (lookup is null || lookup.Recorded != (lookup.Result is not null))
                    throw new TaskStartException(TaskStartFailure.InvalidResponse);
                if (lookup.Recorded) CompleteSuccess(mine, body, lookup.Result!);
                else CompleteNotRecorded(mine);
            }
            else
            {
                cancellation.Token.ThrowIfCancellationRequested();
                mutationStarted = true;
                var response = await service.StartAsync(body, requestKey, cancellation.Token).ConfigureAwait(false);
                CompleteSuccess(mine, body, response);
            }
        }
        catch (TaskStartException error) { CompleteFailure(mine, kind, mutationStarted, error.Failure); }
        catch (OperationCanceledException) { CompleteFailure(mine, kind, mutationStarted, TaskStartFailure.Unavailable, true); }
        catch (ArgumentException)
        { CompleteFailure(mine, kind, mutationStarted, mutationStarted ? TaskStartFailure.InvalidResponse : TaskStartFailure.InvalidInput); }
        catch (Exception) { CompleteFailure(mine, kind, mutationStarted, TaskStartFailure.InvalidResponse); }
    }

    private bool Allowed(OperationKind kind) => kind switch
    {
        OperationKind.Submit => state == TaskStartState.Editing,
        OperationKind.Check => state is TaskStartState.NeedsResultCheck or TaskStartState.RetryAllowed,
        OperationKind.Retry => state == TaskStartState.RetryAllowed,
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
            { DisconnectLocked(TaskStartFailure.AuthenticationExpired); notify = true; active = false; }
        }
        if (notify) NotifyChanged();
        return active;
    }

    private void CompleteSuccess(long mine, TaskStartRequest request, TaskStartResult response)
    {
        if (!TaskStartFormat.MatchesResult(request, response)) throw new TaskStartException(TaskStartFailure.InvalidResponse);
        if (!StillCurrent(mine)) return;
        lock (gate)
        {
            if (disposed || disconnected || operation != mine) return;
            result = response;
            highestSucceededVersion = long.Parse(response.RowVersion, CultureInfo.InvariantCulture);
            busy = false;
            state = TaskStartState.Succeeded;
            failure = null;
            message = "업무 시작이 확인되었습니다. 최신 업무 정보를 조회해 주세요.";
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
            state = TaskStartState.RetryAllowed;
            failure = null;
            message = "저장된 결과가 없습니다. 같은 요청 재전송 버튼으로 다시 시도할 수 있습니다.";
        }
        NotifyChanged();
    }

    private void CompleteFailure(long mine, OperationKind kind, bool mutationStarted, TaskStartFailure reason, bool cancelled = false)
    {
        if (!StillCurrent(mine)) return;
        lock (gate)
        {
            if (disposed || disconnected || operation != mine) return;
            busy = false;
            failure = reason;
            message = TaskStartException.MessageFor(reason);
            if (reason is TaskStartFailure.AuthenticationExpired or TaskStartFailure.Forbidden)
                DisconnectLocked(reason);
            else if (kind == OperationKind.Submit && !mutationStarted)
            {
                state = TaskStartState.Editing;
                key = null;
                message = cancelled ? "시작 요청 전에 취소했습니다. 업무를 다시 확인해 주세요." : message;
            }
            else if (reason == TaskStartFailure.KeyExpired)
                state = kind == OperationKind.Check ? TaskStartState.Blocked : TaskStartState.NeedsResultCheck;
            else if (reason is TaskStartFailure.KeyReused or TaskStartFailure.Conflict)
                state = TaskStartState.Blocked;
            else if (kind == OperationKind.Check)
                state = TaskStartState.NeedsResultCheck;
            else if (reason is TaskStartFailure.InvalidInput or TaskStartFailure.AssignmentChanged
                or TaskStartFailure.VersionConflict or TaskStartFailure.StateConflict or TaskStartFailure.DataInconsistent
                or TaskStartFailure.VersionExhausted or TaskStartFailure.BeforeStartDate or TaskStartFailure.AfterDueDate
                or TaskStartFailure.NotFound or TaskStartFailure.NotConfigured)
            {
                state = TaskStartState.Editing;
                key = null;
            }
            else state = TaskStartState.NeedsResultCheck;
            if (state == TaskStartState.NeedsResultCheck)
                message = "시작 여부가 불확실합니다. 처리 결과 확인 버튼을 눌러 주세요. " + message;
        }
        NotifyChanged();
    }

    private void OnSessionChanged()
    {
        lock (gate)
        {
            if (disposed || disconnected) return;
            DisconnectLocked(TaskStartFailure.AuthenticationExpired);
        }
        NotifyChanged();
    }
    private void DisconnectLocked(TaskStartFailure reason)
    {
        operation++;
        disconnected = true;
        busy = false;
        pendingRequest = null;
        key = null;
        result = null;
        state = TaskStartState.Disconnected;
        failure = reason;
        message = TaskStartException.MessageFor(reason);
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
            DisconnectLocked(TaskStartFailure.AuthenticationExpired);
        }
        sessionRegistration.Unregister();
        lifetime.Cancel();
        lifetime.Dispose();
        NotifyChanged();
    }
    private enum OperationKind { Submit, Check, Retry }
}
