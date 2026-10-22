using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Attachments;

namespace RemoteWork.WinForms.Features.Tasks.Management;

public enum TaskManagementState
{
    Editing, Sending, NeedsResultCheck, RetryAllowed, Succeeded, Blocked, Disconnected
}

public sealed record TaskManagementSnapshot(TaskManagementState State, bool IsBusy,
    TaskManagementRequest? PendingRequest, TaskManagementResult? Result, TaskManagementFailure? Failure,
    string Message, bool CanCheckResult, bool CanRetry);

// One login and task/action. A form borrows this across closing/navigation; no disk storage.
public sealed class TaskManagementWorkflow : IDisposable
{
    private readonly ITaskManagementService service;
    private readonly object gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationToken lifetimeToken;
    private readonly CancellationTokenRegistration sessionRegistration;
    private TaskManagementState state = TaskManagementState.Editing;
    private bool disposed, disconnected, busy, attachmentsBusy;
    private long operation, highestSucceededVersion;
    private TaskManagementRequest? pendingRequest;
    private TaskWriteRequestKey? key;
    private TaskManagementResult? result;
    private TaskManagementFailure? failure;
    private string message = "현재 관리 업무와 변경 입력을 확인해 주세요.";
    private TaskManagementKind? boundKind;
    private string? boundTaskId;

    public TaskManagementWorkflow(ITaskManagementService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        lifetimeToken = lifetime.Token;
        sessionRegistration = service.SessionCancellation.Register(OnSessionChanged);
    }

    internal TaskManagementWorkflow(ITaskManagementService service, TaskManagementKind kind, string taskId,
        HttpTaskCreationUploadService? uploads = null, HttpTaskAttachmentService? attachmentReads = null) : this(service)
    {
        boundKind = kind; boundTaskId = taskId;
        if (kind == TaskManagementKind.Update && uploads is not null && attachmentReads is not null)
            Attachments = new TaskManagementAttachmentSelection(this, uploads, attachmentReads, taskId);
    }

    public TaskManagementAttachmentSelection? Attachments { get; }
    public bool AttachmentsBusy { get { lock (gate) return attachmentsBusy; } }
    public bool CanEditAttachments => IsCurrent && !IsBusy && !AttachmentsBusy && State == TaskManagementState.Editing;
    internal bool TryBeginAttachmentChange()
    {
        if (!IsCurrent) return false;
        lock (gate)
        {
            if (disposed || disconnected || busy || attachmentsBusy || state != TaskManagementState.Editing) return false;
            attachmentsBusy = true;
        }
        NotifyChanged();
        return true;
    }
    internal void EndAttachmentChange()
    {
        lock (gate) attachmentsBusy = false;
        NotifyChanged();
    }
    internal void BlockAttachmentSession()
    {
        lock (gate) if (!disposed && !disconnected) DisconnectLocked(TaskManagementFailure.Forbidden);
        NotifyChanged();
    }

    public event EventHandler? StateChanged;
    public event EventHandler? Changed;
    public TaskManagementSnapshot Snapshot
    {
        get
        {
            lock (gate) return new(state, busy || attachmentsBusy, pendingRequest, result, failure, message,
                !busy && state is TaskManagementState.NeedsResultCheck or TaskManagementState.RetryAllowed,
                !busy && state == TaskManagementState.RetryAllowed);
        }
    }
    public TaskManagementState State => Snapshot.State;
    public TaskManagementRequest? PendingRequest => Snapshot.PendingRequest;
    public TaskManagementResult? Result => Snapshot.Result;
    public TaskManagementFailure? Failure => Snapshot.Failure;
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

    public Task SubmitAsync(TaskManagementRequest request, CancellationToken ct = default)
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
            if (disposed || disconnected || busy || attachmentsBusy || state != TaskManagementState.Succeeded) return false;
            operation++;
            pendingRequest = null;
            key = null;
            result = null;
            failure = null;
            state = TaskManagementState.Editing;
            message = "최신 업무 정보를 다시 확인해 주세요.";
        }
        NotifyChanged();
        return true;
    }

    private async Task RunAsync(OperationKind kind, TaskManagementRequest? input, CancellationToken ct)
    {
        bool current = service.IsCurrent && !service.SessionCancellation.IsCancellationRequested;
        long mine = 0;
        TaskManagementRequest? body = null;
        TaskWriteRequestKey? requestKey = null;
        bool started = false;
        lock (gate)
        {
            if (disposed || disconnected || busy || attachmentsBusy) return;
            if (!current) DisconnectLocked(TaskManagementFailure.AuthenticationExpired);
            else if (Allowed(kind))
            {
                if (kind == OperationKind.Submit)
                {
                    if ((Attachments is not null && !Attachments.Matches(input!))
                        || (Attachments is null && input!.AttachmentIds is not null))
                        throw new ArgumentException("현재 업무·버전에서 준비하고 업로드를 마친 첨부 목록으로 다시 확인해 주세요.");
                    if (input!.CheckedUserId != service.UserId)
                    { DisconnectLocked(TaskManagementFailure.Forbidden); }
                    else if ((boundTaskId is not null && (boundTaskId != input.TaskId || boundKind != input.Kind))
                        || long.Parse(input.ExpectedRowVersion, CultureInfo.InvariantCulture) < highestSucceededVersion)
                    {
                        failure = TaskManagementFailure.InvalidInput;
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
                    state = TaskManagementState.Sending;
                    failure = null;
                    message = kind == OperationKind.Check ? "업무 변경 결과를 확인하고 있습니다." : "업무 변경을 요청하고 있습니다.";
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
            if (body is null) throw new TaskManagementException(TaskManagementFailure.InvalidResponse);
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
            if (requestKey is null) throw new TaskManagementException(TaskManagementFailure.InvalidResponse);
            if (kind == OperationKind.Check)
            {
                var lookup = await service.GetResultAsync(body, requestKey, cancellation.Token).ConfigureAwait(false);
                if (lookup is null || lookup.Recorded != (lookup.Result is not null))
                    throw new TaskManagementException(TaskManagementFailure.InvalidResponse);
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
        catch (TaskManagementException error) { CompleteFailure(mine, kind, mutationStarted, error.Failure); }
        catch (OperationCanceledException) { CompleteFailure(mine, kind, mutationStarted, TaskManagementFailure.Unavailable, true); }
        catch (ArgumentException)
        { CompleteFailure(mine, kind, mutationStarted, mutationStarted ? TaskManagementFailure.InvalidResponse : TaskManagementFailure.InvalidInput); }
        catch (Exception) { CompleteFailure(mine, kind, mutationStarted, TaskManagementFailure.InvalidResponse); }
    }

    private bool Allowed(OperationKind kind) => kind switch
    {
        OperationKind.Submit => state == TaskManagementState.Editing,
        OperationKind.Check => state is TaskManagementState.NeedsResultCheck or TaskManagementState.RetryAllowed,
        OperationKind.Retry => state == TaskManagementState.RetryAllowed,
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
            { DisconnectLocked(TaskManagementFailure.AuthenticationExpired); notify = true; active = false; }
        }
        if (notify) NotifyChanged();
        return active;
    }

    private void CompleteSuccess(long mine, TaskManagementRequest request, TaskManagementResult response)
    {
        if (!TaskManagementFormat.MatchesResult(request, response)) throw new TaskManagementException(TaskManagementFailure.InvalidResponse);
        if (!StillCurrent(mine)) return;
        lock (gate)
        {
            if (disposed || disconnected || operation != mine) return;
            result = response;
            highestSucceededVersion = long.Parse(response.RowVersion, CultureInfo.InvariantCulture);
            busy = false;
            state = TaskManagementState.Succeeded;
            failure = null;
            message = "업무 변경 성공을 확인했습니다. 최신 업무 정보를 조회해 주세요.";
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
            state = TaskManagementState.RetryAllowed;
            failure = null;
            message = "저장된 결과가 없습니다. 같은 요청 재전송 버튼으로 다시 시도할 수 있습니다.";
        }
        NotifyChanged();
    }

    private void CompleteFailure(long mine, OperationKind kind, bool mutationStarted, TaskManagementFailure reason, bool cancelled = false)
    {
        if (!StillCurrent(mine)) return;
        lock (gate)
        {
            if (disposed || disconnected || operation != mine) return;
            busy = false;
            failure = reason;
            message = TaskManagementException.MessageFor(reason);
            if (reason == TaskManagementFailure.AuthenticationExpired)
                DisconnectLocked(reason);
            else if (reason == TaskManagementFailure.Forbidden)
            {
                state = TaskManagementState.Blocked;
                message = "현재 권한이 없어 결과를 확인할 수 없습니다. 이전 변경이 반영됐을 수도 있으므로 관리자가 처리 내역을 확인해야 합니다. 자동 재전송하지 않습니다.";
            }
            else if (kind == OperationKind.Submit && !mutationStarted)
            {
                state = TaskManagementState.Editing;
                key = null;
                message = cancelled ? "변경 요청 전에 취소했습니다. 업무를 다시 확인해 주세요." : message;
            }
            else if (reason == TaskManagementFailure.KeyExpired)
                state = kind == OperationKind.Check ? TaskManagementState.Blocked : TaskManagementState.NeedsResultCheck;
            else if (reason is TaskManagementFailure.KeyReused or TaskManagementFailure.Conflict)
                state = TaskManagementState.Blocked;
            else if (kind == OperationKind.Check)
                state = TaskManagementState.NeedsResultCheck;
            else if (reason is TaskManagementFailure.InvalidInput or TaskManagementFailure.AssignmentChanged
                or TaskManagementFailure.VersionConflict or TaskManagementFailure.StateConflict or TaskManagementFailure.DataInconsistent
                or TaskManagementFailure.VersionExhausted or TaskManagementFailure.AssignmentExhausted
                or TaskManagementFailure.DueDateInPast or TaskManagementFailure.NoChange
                or TaskManagementFailure.NotFound or TaskManagementFailure.NotConfigured or TaskManagementFailure.AttachmentUnavailable)
            {
                state = TaskManagementState.Editing;
                key = null;
            }
            else state = TaskManagementState.NeedsResultCheck;
            if (state == TaskManagementState.NeedsResultCheck)
                message = "변경 여부가 불확실합니다. 처리 결과 확인 버튼을 눌러 주세요. " + message;
        }
        NotifyChanged();
    }

    private void OnSessionChanged()
    {
        lock (gate)
        {
            if (disposed || disconnected) return;
            DisconnectLocked(TaskManagementFailure.AuthenticationExpired);
        }
        NotifyChanged();
    }
    private void DisconnectLocked(TaskManagementFailure reason)
    {
        operation++;
        disconnected = true;
        busy = false;
        pendingRequest = null;
        key = null;
        result = null;
        state = TaskManagementState.Disconnected;
        failure = reason;
        message = TaskManagementException.MessageFor(reason);
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
            DisconnectLocked(TaskManagementFailure.AuthenticationExpired);
        }
        sessionRegistration.Unregister();
        Attachments?.Dispose();
        lifetime.Cancel();
        lifetime.Dispose();
        NotifyChanged();
    }
    private enum OperationKind { Submit, Check, Retry }
}
