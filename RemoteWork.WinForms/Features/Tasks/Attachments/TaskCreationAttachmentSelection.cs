using RemoteWork.WinForms.Features.Tasks.Creation;

namespace RemoteWork.WinForms.Features.Tasks.Attachments;

public sealed record TaskCreationAttachmentItem(Guid Key, string Name, long Size,
    TaskCreationUploadState State, string Message, bool CanUpload, bool CanRetry, bool CanDiscard, bool CanRemove);

// Owned by one creation workflow, not by a modal window. Entries and pending
// registration IDs survive reopening the window for the same captured login.
public sealed class TaskCreationAttachmentSelection : IDisposable
{
    private readonly TaskCreationWorkflow owner;
    private readonly HttpTaskCreationUploadService service;
    private readonly object gate = new();
    private readonly List<(Guid Key, TaskCreationUploadAttempt Attempt)> entries = new();
    private bool disposed, changed;
    private string message = "파일을 선택한 뒤 업로드해 주세요. 파일 선택만으로 전송하지 않습니다.";
    internal TaskCreationAttachmentSelection(TaskCreationWorkflow owner, HttpTaskCreationUploadService service)
    {
        this.owner = owner;
        this.service = service;
        owner.StateChanged += OwnerChanged;
    }

    public event EventHandler? Changed;
    public bool IsCurrent => !disposed && owner.IsCurrent && service.IsCurrent;
    public bool IsBusy => owner.AttachmentsBusy;
    public bool CanChange => IsCurrent && owner.CanEditAttachments;
    public string Message
    {
        get
        {
            if (owner.State != TaskCreationState.Editing) return owner.Message;
            lock (gate) return message;
        }
    }
    public IReadOnlyList<TaskCreationAttachmentItem> Items
    {
        get
        {
            bool can = CanChange;
            (Guid Key, TaskCreationUploadAttempt Attempt)[] snapshot;
            lock (gate) snapshot = entries.ToArray();
            // CanDiscard checks the captured ApiClient identity. Do not hold the
            // selection gate while crossing that lifetime/identity boundary.
            return snapshot.Select(e => new TaskCreationAttachmentItem(e.Key, e.Attempt.Name, e.Attempt.Size,
                e.Attempt.State, e.Attempt.Message, can && e.Attempt.State == TaskCreationUploadState.Ready,
                can && e.Attempt.State == TaskCreationUploadState.NeedsRetry, can && e.Attempt.CanDiscard,
                can && Removable(e.Attempt))).ToArray();
        }
    }

    public bool IsReady
    {
        get { lock (gate) return TrySnapshotLocked(out _); }
    }
    public IReadOnlyList<string>? CompletedIds
    {
        get
        {
            lock (gate)
            {
                return TrySnapshotLocked(out var ids) ? ids : null;
            }
        }
    }
    // Called with the workflow's operation lock held. This only reads local
    // state, never the ApiClient identity lock or another feature's storage.
    internal bool Matches(IReadOnlyList<string>? ids)
    {
        lock (gate)
        {
            if (!TrySnapshotLocked(out var expected)) return false;
            return expected is null ? ids is null : ids is not null && expected.SequenceEqual(ids);
        }
    }

    // The login cancellation callback can clear an attempt's CompletedFile while
    // this gate is held. Capture each value once and reject missing values instead
    // of re-reading it after an earlier IsReady check. No API/session lock here:
    // Matches is also used while the workflow's operation lock is held.
    private bool TrySnapshotLocked(out IReadOnlyList<string>? ids)
    {
        ids = null;
        if (disposed) return false;
        var completed = new List<string>(entries.Count);
        foreach (var entry in entries)
        {
            TaskAttachment? file = entry.Attempt.CompletedFile;
            if (file is null || entry.Attempt.State != TaskCreationUploadState.Completed) return false;
            completed.Add(file.AttachmentId);
        }
        if (changed) ids = completed.AsReadOnly();
        return true;
    }

    public async Task AddFileAsync(string path, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested || !owner.TryBeginAttachmentChange()) return;
        TaskCreationUploadAttempt? attempt = null;
        try
        {
            lock (gate) if (disposed || entries.Count >= 10)
            { message = "첨부파일은 최대 10개까지 선택할 수 있습니다."; return; }
            attempt = await service.PrepareFileAsync(path, ct).ConfigureAwait(false);
            if (!IsCurrent) return;
            lock (gate)
            {
                if (disposed) return;
                entries.Add((Guid.NewGuid(), attempt));
                attempt = null;
                changed = true;
                message = "파일을 선택했습니다. 업로드 버튼을 눌러 전송해 주세요.";
            }
        }
        catch (OperationCanceledException) { SetMessage("파일 준비를 취소했습니다. 새 파일은 추가하지 않았습니다."); }
        catch (ArgumentException error) { SetMessage(error.Message); }
        catch (Exception) { SetMessage("파일을 준비하지 못했습니다. 접속·파일 크기와 읽기 권한을 확인해 주세요."); }
        finally { attempt?.Dispose(); owner.EndAttachmentChange(); Notify(); }
    }

    public Task UploadAsync(Guid key, CancellationToken ct = default) => ActAsync(key, ActionKind.Upload, ct);
    public Task RetryAsync(Guid key, CancellationToken ct = default) => ActAsync(key, ActionKind.Retry, ct);
    public Task DiscardAsync(Guid key, CancellationToken ct = default) => ActAsync(key, ActionKind.Discard, ct);

    private async Task ActAsync(Guid key, ActionKind kind, CancellationToken ct)
    {
        if (ct.IsCancellationRequested || !owner.TryBeginAttachmentChange()) return;
        try
        {
            TaskCreationUploadAttempt? target;
            lock (gate) target = disposed ? null : entries.FirstOrDefault(e => e.Key == key).Attempt;
            if (target is null) return;
            if (kind == ActionKind.Upload) await target.StartAsync(ct).ConfigureAwait(false);
            else if (kind == ActionKind.Retry) await target.RetryAsync(ct).ConfigureAwait(false);
            else await target.DiscardAsync(ct).ConfigureAwait(false);
            if (!IsCurrent) return;
            SetMessage(target.Message);
            if (!target.IsCurrent)
            {
                // A provider 403 must not be bypassed by opening another picker.
                owner.BlockAttachmentSession();
                return;
            }
            if (target.State == TaskCreationUploadState.Discarded)
            {
                lock (gate) entries.RemoveAll(e => e.Key == key);
                target.Dispose();
            }
        }
        finally { owner.EndAttachmentChange(); Notify(); }
    }

    public bool RemoveLocal(Guid key)
    {
        if (!owner.TryBeginAttachmentChange()) return false;
        try
        {
            TaskCreationUploadAttempt? target;
            lock (gate)
            {
                target = disposed ? null : entries.FirstOrDefault(e => e.Key == key).Attempt;
                if (target is null || !Removable(target)) return false;
                entries.RemoveAll(e => e.Key == key);
                changed = true;
                message = target.State == TaskCreationUploadState.StartUncertain
                    ? "선택만 해제했습니다. 시작 결과를 몰라 서버 임시파일은 삭제하지 못했습니다."
                    : target.State == TaskCreationUploadState.DiscardRejected
                    ? "만료된 파일의 선택만 해제했습니다. 서버의 임시파일 정리는 별도로 필요할 수 있습니다."
                    : "로컬 파일 선택을 해제했습니다. 서버 삭제 요청은 보내지 않았습니다.";
            }
            target.Dispose();
            return true;
        }
        finally { owner.EndAttachmentChange(); Notify(); }
    }

    private static bool Removable(TaskCreationUploadAttempt item) => item.State is TaskCreationUploadState.Ready
        or TaskCreationUploadState.StartUncertain or TaskCreationUploadState.Discarded
        || (item.State == TaskCreationUploadState.Rejected && item.AttachmentId is null)
        || (item.State == TaskCreationUploadState.DiscardRejected && item.StatusCode == 409 && item.ErrorCode == "ATTACHMENT_UNAVAILABLE");
    private void SetMessage(string value) { lock (gate) if (!disposed) message = value; }
    private void OwnerChanged(object? sender, EventArgs e)
    {
        if (owner.State is TaskCreationState.Disconnected or TaskCreationState.Succeeded)
            ClearWithoutDelete();
        Notify();
    }
    private void ClearWithoutDelete()
    {
        bool succeeded = owner.State == TaskCreationState.Succeeded;
        TaskCreationUploadAttempt[] old;
        lock (gate)
        {
            old = entries.Select(e => e.Attempt).ToArray();
            entries.Clear(); changed = false;
            message = succeeded
                ? "업무에 첨부 연결이 확인되었습니다. 임시파일 삭제를 요청하지 않습니다."
                : "접속이 종료되어 파일 선택을 지웠습니다. 서버 삭제를 요청하지 않았습니다.";
        }
        foreach (var item in old) item.Dispose();
    }
    private void Notify()
    {
        var handlers = Changed;
        if (handlers is null) return;
        foreach (EventHandler handler in handlers.GetInvocationList())
            try { handler(this, EventArgs.Empty); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
    }
    public void Dispose()
    {
        lock (gate) { if (disposed) return; disposed = true; }
        owner.StateChanged -= OwnerChanged;
        ClearWithoutDelete();
        Notify();
    }
    private enum ActionKind { Upload, Retry, Discard }
}
