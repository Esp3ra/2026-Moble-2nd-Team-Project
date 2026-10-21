using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;

namespace RemoteWork.WinForms.Features.Tasks.Creation;

public enum TaskCreationState
{
    Editing, Sending, NeedsResultCheck, RetryAllowed, Succeeded, ManualReview, Disconnected
}

// The feature's login-session store owns this across page changes; modal forms borrow it.
// No request key or pending body is persisted to disk.
public sealed class TaskCreationWorkflow : IDisposable
{
    private readonly ITaskCreationService service;
    private readonly object stateLock = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationToken lifetimeToken;
    private readonly CancellationTokenRegistration sessionRegistration;
    private long operation;
    private bool disposed;
    private bool disconnected;
    private bool busy;
    private TaskCreationState state = TaskCreationState.Editing;
    private TaskCreationRequest? pendingRequest;
    private TaskWriteRequestKey? requestKey;
    private TaskCreationResult? createdResult;
    private TaskCreationFailure? failure;
    private string message = "입력과 담당자·관리자를 확인한 뒤 등록해 주세요.";

    public TaskCreationWorkflow(ITaskCreationService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        lifetimeToken = lifetime.Token;
        sessionRegistration = service.SessionCancellation.Register(OnSessionChanged);
    }

    public event EventHandler? StateChanged;
    public TaskCreationRequest? PendingRequest { get { lock (stateLock) return pendingRequest; } }
    public TaskCreationResult? CreatedResult { get { lock (stateLock) return createdResult; } }
    public TaskCreationState State { get { lock (stateLock) return state; } }
    public bool IsBusy { get { lock (stateLock) return busy; } }
    public bool CanRetry { get { lock (stateLock) return !busy && state == TaskCreationState.RetryAllowed; } }
    public bool CanCheckResult
    {
        get
        {
            lock (stateLock)
                return !busy && state is TaskCreationState.NeedsResultCheck or TaskCreationState.RetryAllowed;
        }
    }
    public bool IsCurrent
    {
        get
        {
            lock (stateLock) if (disposed || disconnected) return false;
            // Never acquire ApiClient's identity lock while holding stateLock.
            return service.IsCurrent && !service.SessionCancellation.IsCancellationRequested;
        }
    }
    public bool CanCreate => IsCurrent && service.CanCreate;
    public int UserId => service.UserId;
    public CancellationToken SessionCancellation => service.SessionCancellation;
    public string Message { get { lock (stateLock) return message; } }
    public TaskCreationFailure? Failure { get { lock (stateLock) return failure; } }

    public Task SubmitAsync(TaskCreationRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return RunAsync(OperationKind.Submit, request, ct);
    }

    public Task CheckResultAsync(CancellationToken ct = default) => RunAsync(OperationKind.Check, null, ct);
    public Task RetryAsync(CancellationToken ct = default) => RunAsync(OperationKind.Retry, null, ct);

    // A new registration is allowed only after a verified success was displayed.
    public bool ResetAfterSuccess()
    {
        if (!IsCurrent) return false;
        lock (stateLock)
        {
            if (disposed || disconnected || busy || state != TaskCreationState.Succeeded) return false;
            operation++;
            pendingRequest = null;
            requestKey = null;
            createdResult = null;
            failure = null;
            state = TaskCreationState.Editing;
            message = "입력과 담당자·관리자를 확인한 뒤 등록해 주세요.";
        }
        NotifyChanged();
        return true;
    }

    private async Task RunAsync(OperationKind kind, TaskCreationRequest? input, CancellationToken ct)
    {
        bool current = service.IsCurrent && !service.SessionCancellation.IsCancellationRequested;
        bool permitted = service.CanCreate;
        long thisOperation = 0;
        TaskCreationRequest? body = null;
        TaskWriteRequestKey? key = null;
        bool started = false;
        lock (stateLock)
        {
            if (disposed || disconnected || busy) return;
            if (!current || !permitted)
                DisconnectLocked(current ? TaskCreationFailure.Forbidden : TaskCreationFailure.AuthenticationExpired);
            else if (Allowed(kind))
            {
                thisOperation = ++operation;
                if (kind == OperationKind.Submit)
                {
                    pendingRequest = input;
                    requestKey = null;
                    createdResult = null;
                }
                body = pendingRequest;
                key = requestKey;
                busy = true;
                state = TaskCreationState.Sending;
                failure = null;
                message = kind == OperationKind.Check ? "등록 결과를 확인하고 있습니다." : "업무 등록을 요청하고 있습니다.";
                started = true;
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
            if (kind == OperationKind.Submit)
            {
                key = await service.GetRequestKeyAsync(cancellation.Token).ConfigureAwait(false);
                if (!StillCurrent(thisOperation)) return;
                lock (stateLock)
                {
                    if (operation != thisOperation || disposed || disconnected) return;
                    requestKey = key;
                }
            }
            if (key is null || body is null) throw new TaskCreationException(TaskCreationFailure.InvalidResponse);

            if (kind == OperationKind.Check)
            {
                var lookup = await service.GetResultAsync(key, cancellation.Token).ConfigureAwait(false);
                if (lookup is null || lookup.Recorded != (lookup.Result is not null))
                    throw new TaskCreationException(TaskCreationFailure.InvalidResponse);
                if (lookup.Recorded) CompleteSuccess(thisOperation, lookup.Result!);
                else CompleteNotRecorded(thisOperation);
            }
            else
            {
                cancellation.Token.ThrowIfCancellationRequested();
                mutationStarted = true;
                var result = await service.CreateAsync(body, key, cancellation.Token).ConfigureAwait(false);
                CompleteSuccess(thisOperation, result);
            }
        }
        catch (TaskCreationException error)
        {
            CompleteFailure(thisOperation, kind, mutationStarted, error.Failure);
        }
        catch (OperationCanceledException)
        {
            CompleteFailure(thisOperation, kind, mutationStarted, TaskCreationFailure.Unavailable, cancelled: true);
        }
        catch (ArgumentException)
        {
            // After dispatch starts, even an unexpected decoder argument error
            // must be treated as an uncertain write, not as rejected input.
            CompleteFailure(thisOperation, kind, mutationStarted,
                mutationStarted ? TaskCreationFailure.InvalidResponse : TaskCreationFailure.InvalidInput);
        }
        catch (Exception)
        {
            // Even an unexpected transport/decoder failure must retain a possibly
            // committed request. The UI must never infer that it is safe to recreate it.
            CompleteFailure(thisOperation, kind, mutationStarted, TaskCreationFailure.InvalidResponse);
        }
    }

    private bool Allowed(OperationKind kind)
    {
        return kind switch
        {
            OperationKind.Submit => state == TaskCreationState.Editing,
            OperationKind.Check => state is TaskCreationState.NeedsResultCheck or TaskCreationState.RetryAllowed,
            OperationKind.Retry => state == TaskCreationState.RetryAllowed,
            _ => false
        };
    }

    private bool StillCurrent(long thisOperation)
    {
        bool current = service.IsCurrent && !service.SessionCancellation.IsCancellationRequested;
        bool notify = false;
        bool active;
        lock (stateLock)
        {
            active = !disposed && !disconnected && operation == thisOperation;
            if (active && !current)
            {
                DisconnectLocked(TaskCreationFailure.AuthenticationExpired);
                notify = true;
                active = false;
            }
        }
        if (notify) NotifyChanged();
        return active;
    }

    private void CompleteSuccess(long thisOperation, TaskCreationResult result)
    {
        if (!ValidResult(result)) throw new TaskCreationException(TaskCreationFailure.InvalidResponse);
        if (!StillCurrent(thisOperation)) return;
        lock (stateLock)
        {
            if (disposed || disconnected || operation != thisOperation) return;
            createdResult = result;
            busy = false;
            state = TaskCreationState.Succeeded;
            failure = null;
            message = "업무 등록이 확인되었습니다.";
        }
        NotifyChanged();
    }

    private void CompleteNotRecorded(long thisOperation)
    {
        if (!StillCurrent(thisOperation)) return;
        lock (stateLock)
        {
            if (disposed || disconnected || operation != thisOperation) return;
            busy = false;
            state = TaskCreationState.RetryAllowed;
            failure = null;
            message = "저장된 등록 결과가 없습니다. 같은 요청 재전송 버튼으로 다시 시도할 수 있습니다.";
        }
        NotifyChanged();
    }

    private void CompleteFailure(long thisOperation, OperationKind kind, bool mutationStarted,
        TaskCreationFailure reason, bool cancelled = false)
    {
        if (!StillCurrent(thisOperation)) return;
        lock (stateLock)
        {
            if (disposed || disconnected || operation != thisOperation) return;
            busy = false;
            failure = reason;
            message = TaskCreationException.MessageFor(reason);
            if (reason is TaskCreationFailure.AuthenticationExpired or TaskCreationFailure.Forbidden)
                DisconnectLocked(reason);
            else if (kind == OperationKind.Submit && !mutationStarted)
            {
                state = TaskCreationState.Editing;
                requestKey = null;
                message = cancelled ? "등록 요청 전에 취소했습니다. 입력을 확인한 뒤 다시 등록해 주세요."
                    : "등록 요청을 전송하지 못했습니다. 입력은 유지했습니다. " + message;
            }
            else if (reason == TaskCreationFailure.KeyExpired)
            {
                // An expired write key can still have a retained completed receipt.
                // Only expiry reported by receipt lookup requires manual review.
                state = kind == OperationKind.Check ? TaskCreationState.ManualReview : TaskCreationState.NeedsResultCheck;
            }
            else if (reason is TaskCreationFailure.KeyReused or TaskCreationFailure.Conflict)
                state = TaskCreationState.ManualReview;
            else if (kind == OperationKind.Check)
                state = TaskCreationState.NeedsResultCheck;
            else if (reason is TaskCreationFailure.InvalidInput or TaskCreationFailure.AssigneeUnavailable
                or TaskCreationFailure.ManagerUnavailable or TaskCreationFailure.NotConfigured
                or TaskCreationFailure.AttachmentUnavailable)
            {
                // A definite rejection permits explicit editing; keep the
                // original selection/body until the user submits a new one.
                // This is not permission to delete or replace temporary files.
                state = TaskCreationState.Editing;
                requestKey = null;
                message = cancelled ? "등록 요청 전에 취소했습니다. 입력을 확인한 뒤 다시 등록해 주세요."
                    : TaskCreationException.MessageFor(reason);
            }
            else state = TaskCreationState.NeedsResultCheck;
            if (state == TaskCreationState.NeedsResultCheck)
                message = "등록 여부를 아직 확인할 수 없습니다. 등록 결과 확인 버튼을 눌러 주세요. " + message;
        }
        NotifyChanged();
    }

    private static bool ValidResult(TaskCreationResult? result)
    {
        if (result is null || result.RowVersion != "1" || result.AssignmentRevision != 1
            || result.Status != TaskDisplayStatus.NotStarted || result.UpdatedAt.Offset != TimeSpan.Zero
            || result.TaskId is null || result.TaskId.Length is < 1 or > 19 || result.TaskId[0] == '0') return false;
        foreach (char digit in result.TaskId) if (!char.IsAsciiDigit(digit)) return false;
        return long.TryParse(result.TaskId, NumberStyles.None, CultureInfo.InvariantCulture, out long id) && id > 0;
    }

    private void OnSessionChanged()
    {
        lock (stateLock)
        {
            if (disposed || disconnected) return;
            DisconnectLocked(TaskCreationFailure.AuthenticationExpired);
        }
        NotifyChanged();
    }

    private void DisconnectLocked(TaskCreationFailure reason)
    {
        operation++;
        disconnected = true;
        busy = false;
        pendingRequest = null;
        requestKey = null;
        createdResult = null;
        state = TaskCreationState.Disconnected;
        failure = reason;
        message = TaskCreationException.MessageFor(reason);
    }

    private void NotifyChanged()
    {
        // Consumers marshal to the UI thread and unsubscribe when their form closes.
        var handlers = StateChanged;
        if (handlers is null) return;
        foreach (EventHandler handler in handlers.GetInvocationList())
        {
            try { handler(this, EventArgs.Empty); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }
    }

    public void Dispose()
    {
        lock (stateLock)
        {
            if (disposed) return;
            disposed = true;
            DisconnectLocked(TaskCreationFailure.AuthenticationExpired);
        }
        // Unregister does not wait on a callback that might be waiting for the UI.
        sessionRegistration.Unregister();
        lifetime.Cancel();
        lifetime.Dispose();
        NotifyChanged();
    }

    private enum OperationKind { Submit, Check, Retry }
}
