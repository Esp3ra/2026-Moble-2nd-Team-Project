using RemoteWork.WinForms.Features.Tasks.Management;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.Attachments;

public sealed record TaskManagementAttachmentItem(Guid Key, string Name, long Size,
    TaskCreationUploadState State, bool IsExisting, string Message,
    bool CanUpload, bool CanRetry, bool CanDiscard, bool CanRemove);

// One update workflow owns this selection across modal navigation. Existing
// task references are only excluded from the final list; they are never DELETE
// targets. New uploads reuse the captured-session transport and explicit retry.
public sealed class TaskManagementAttachmentSelection : IDisposable
{
    private sealed record Entry(Guid Key, TaskAttachment? Existing, TaskCreationUploadAttempt? Attempt);
    private readonly TaskManagementWorkflow owner;
    private readonly HttpTaskCreationUploadService uploads;
    private readonly HttpTaskAttachmentService reads;
    private readonly string taskId;
    private readonly object gate = new();
    private readonly List<Entry> entries = new();
    private bool disposed, invalidated, loaded, changed;
    private string? rowVersion;
    private int assignmentRevision;
    private string message = "현재 업무·버전과 기존 첨부 목록을 확인해 주세요.";

    internal TaskManagementAttachmentSelection(TaskManagementWorkflow owner,
        HttpTaskCreationUploadService uploads, HttpTaskAttachmentService reads, string taskId)
    {
        if (uploads.TaskId != taskId) throw new ArgumentException("첨부 전송 업무가 변경 대상과 다릅니다.");
        this.owner = owner; this.uploads = uploads; this.reads = reads; this.taskId = taskId;
        owner.StateChanged += OwnerChanged;
    }

    public event EventHandler? Changed;
    public bool IsCurrent => !disposed && owner.IsCurrent && uploads.IsCurrent && reads.IsCurrent;
    public bool IsBusy => owner.AttachmentsBusy;
    public bool Loaded { get { lock (gate) return loaded; } }
    public bool HasChanges { get { lock (gate) return changed; } }
    public bool CanChange => IsCurrent && Loaded && owner.CanEditAttachments;
    public string Message
    {
        get { if (owner.State != TaskManagementState.Editing) return owner.Message; lock (gate) return message; }
    }
    public bool IsReady { get { lock (gate) return TrySnapshotLocked(out _); } }
    public IReadOnlyList<string>? CompletedIds
    {
        get { lock (gate) return TrySnapshotLocked(out var ids) ? ids : null; }
    }
    public IReadOnlyList<TaskManagementAttachmentItem> Items
    {
        get
        {
            bool can = CanChange;
            Entry[] snapshot;
            lock (gate) snapshot = entries.ToArray();
            return snapshot.Select(entry => entry.Existing is { } existing
                ? new TaskManagementAttachmentItem(entry.Key, existing.Name, existing.Size,
                    TaskCreationUploadState.Completed, true, "기존 업무 첨부 · 제외하면 변경 실행 시 업무 참조만 해제됩니다.",
                    false, false, false, can)
                : new TaskManagementAttachmentItem(entry.Key, entry.Attempt!.Name, entry.Attempt.Size,
                    entry.Attempt.State, false, entry.Attempt.Message,
                    can && entry.Attempt.State == TaskCreationUploadState.Ready,
                    can && entry.Attempt.State == TaskCreationUploadState.NeedsRetry,
                    can && entry.Attempt.CanDiscard, can && Removable(entry.Attempt))).ToArray();
        }
    }

    // The form reads context -> files -> context and verifies both versions
    // before calling BindSnapshot. This method uses the very same login reader.
    public async Task<IReadOnlyList<TaskAttachment>> ReadFilesAsync(CancellationToken ct = default)
    {
        if (!IsCurrent || !owner.TryBeginAttachmentChange())
            throw new OperationCanceledException("현재 요청의 첨부 목록을 다시 조회할 수 없습니다.");
        try
        {
            var files = await reads.GetFilesAsync(taskId, ct).ConfigureAwait(false);
            if (!IsCurrent) throw new OperationCanceledException("접속이 변경되었습니다.");
            return files;
        }
        catch (ApiException error) when (error.StatusCode is 401 or 403)
        { ct.ThrowIfCancellationRequested(); owner.BlockAttachmentSession(); throw; }
        finally { owner.EndAttachmentChange(); }
    }
    public bool MatchesContext(TaskWriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        lock (gate) return !disposed && loaded && ContextMatchesLocked(context);
    }
    public bool MayReplaceSnapshot(TaskWriteContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!ValidContext(context)) return false;
        lock (gate) return !disposed && (!loaded || ContextMatchesLocked(context) || !changed);
    }
    public bool BindSnapshot(TaskWriteContext context, IReadOnlyList<TaskAttachment> files)
    {
        ArgumentNullException.ThrowIfNull(context); ArgumentNullException.ThrowIfNull(files);
        if (!ValidContext(context)) return false;
        TaskAttachment[] copy = files.ToArray();
        if (copy.Length > 10 || copy.Any(file => !ValidFile(file))
            || copy.Select(file => file.AttachmentId).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("현재 업무 첨부 목록을 확인해 주세요.");
        if (!owner.TryBeginAttachmentChange()) return false;
        Entry[] old = [];
        try
        {
            lock (gate)
            {
                if (disposed || invalidated) return false;
                if (loaded && changed)
                {
                    if (!ContextMatchesLocked(context))
                    {
                        message = "업무 버전이 바뀌었습니다. 기존 선택을 유지했습니다. 명시적으로 선택을 초기화한 뒤 다시 조회해 주세요.";
                        return false;
                    }
                    // A same-version refresh must not overwrite an explicit final list.
                    return true;
                }
                old = entries.ToArray();
                entries.Clear();
                entries.AddRange(copy.Select(file => new Entry(Guid.NewGuid(), file, null)));
                rowVersion = context.RowVersion; assignmentRevision = context.AssignmentRevision;
                loaded = true; changed = false;
                message = "기존 첨부를 확인했습니다. 선택을 바꾸지 않으면 기존 첨부를 유지합니다.";
                return true;
            }
        }
        finally
        {
            foreach (var entry in old) entry.Attempt?.Dispose();
            owner.EndAttachmentChange(); Notify();
        }
    }
    public bool ResetWithoutDelete()
    {
        if (!owner.TryBeginAttachmentChange()) return false;
        try { ClearWithoutDelete("화면의 첨부 선택만 초기화했습니다. 서버 파일은 삭제하지 않았습니다."); return true; }
        finally { owner.EndAttachmentChange(); Notify(); }
    }

    // Workflow gate -> selection gate -> local attempt state only. Never acquire
    // ApiClient's identity lock here; cancellation uses the inverse boundary.
    internal bool Matches(TaskManagementRequest request)
    {
        lock (gate)
        {
            if (request.Kind != TaskManagementKind.Update || request.TaskId != taskId
                || !TrySnapshotLocked(out var expected)) return false;
            if (loaded && (request.ExpectedRowVersion != rowVersion
                || request.ExpectedAssignmentRevision != assignmentRevision)) return false;
            return expected is null ? request.AttachmentIds is null
                : request.AttachmentIds is not null && expected.SequenceEqual(request.AttachmentIds);
        }
    }
    private bool TrySnapshotLocked(out IReadOnlyList<string>? ids)
    {
        ids = null;
        if (disposed || invalidated) return false;
        if (!loaded) return !changed && entries.Count == 0;
        var final = new List<string>(entries.Count);
        foreach (var entry in entries)
        {
            TaskAttachment? file = entry.Existing ?? entry.Attempt?.CompletedFile;
            if (file is null || (entry.Attempt is not null && entry.Attempt.State != TaskCreationUploadState.Completed))
                return false;
            final.Add(file.AttachmentId);
        }
        if (final.Count > 10 || final.Distinct(StringComparer.Ordinal).Count() != final.Count) return false;
        if (changed) ids = final.AsReadOnly();
        return true;
    }
    private bool ValidContext(TaskWriteContext context) => context.TaskId == taskId
        && TaskManagementFormat.IsId(context.RowVersion) && context.AssignmentRevision > 0
        && context.Status == TaskDisplayStatus.NotStarted && context.ManagerUserIds.Contains(owner.UserId)
        && context.UpdatedAt.Offset == TimeSpan.Zero;
    private bool ContextMatchesLocked(TaskWriteContext context) => ValidContext(context)
        && rowVersion == context.RowVersion && assignmentRevision == context.AssignmentRevision;
    private static bool ValidFile(TaskAttachment? file) => file is not null
        && Guid.TryParseExact(file.AttachmentId, "D", out Guid id) && id.ToString("D") == file.AttachmentId
        && !string.IsNullOrWhiteSpace(file.Name) && file.Size >= 0 && file.Size <= HttpTaskCreationUploadService.MaxFileBytes
        && file.Sha256 is { Length: 64 } && file.Sha256.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public async Task AddFileAsync(string path, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested || !Loaded || !owner.TryBeginAttachmentChange()) return;
        TaskCreationUploadAttempt? attempt = null;
        try
        {
            lock (gate) if (disposed || entries.Count >= 10)
            { message = "첨부파일은 최대 10개까지 선택할 수 있습니다."; return; }
            attempt = await uploads.PrepareFileAsync(path, ct).ConfigureAwait(false);
            if (!IsCurrent) return;
            lock (gate)
            {
                if (disposed || invalidated) return;
                entries.Add(new Entry(Guid.NewGuid(), null, attempt)); attempt = null;
                changed = true; message = "파일을 선택했습니다. 업로드한 뒤 변경 내용을 확인해 주세요.";
            }
        }
        catch (OperationCanceledException) { SetMessage("파일 준비를 취소했습니다. 새 파일은 추가하지 않았습니다."); }
        catch (ArgumentException error) { SetMessage(error.Message); }
        catch (Exception) { SetMessage("파일을 준비하지 못했습니다. 접속·파일 크기·읽기 권한을 확인해 주세요."); }
        finally { attempt?.Dispose(); owner.EndAttachmentChange(); Notify(); }
    }
    public Task UploadAsync(Guid key, CancellationToken ct = default) => ActAsync(key, ActionKind.Upload, ct);
    public Task RetryAsync(Guid key, CancellationToken ct = default) => ActAsync(key, ActionKind.Retry, ct);
    public Task DiscardAsync(Guid key, CancellationToken ct = default) => ActAsync(key, ActionKind.Discard, ct);
    private async Task ActAsync(Guid key, ActionKind kind, CancellationToken ct)
    {
        if (ct.IsCancellationRequested || !Loaded || !owner.TryBeginAttachmentChange()) return;
        try
        {
            TaskCreationUploadAttempt? target;
            lock (gate) target = disposed ? null : entries.FirstOrDefault(entry => entry.Key == key)?.Attempt;
            // Existing references have no upload attempt and cannot issue DELETE.
            if (target is null) return;
            if (kind == ActionKind.Upload) await target.StartAsync(ct).ConfigureAwait(false);
            else if (kind == ActionKind.Retry) await target.RetryAsync(ct).ConfigureAwait(false);
            else await target.DiscardAsync(ct).ConfigureAwait(false);
            if (!IsCurrent) return;
            SetMessage(target.Message);
            if (!target.IsCurrent) { owner.BlockAttachmentSession(); return; }
            if (target.State == TaskCreationUploadState.Discarded)
            {
                lock (gate) { entries.RemoveAll(entry => entry.Key == key); changed = true; }
                target.Dispose();
            }
        }
        finally { owner.EndAttachmentChange(); Notify(); }
    }
    public bool RemoveLocal(Guid key)
    {
        if (!Loaded || !owner.TryBeginAttachmentChange()) return false;
        TaskCreationUploadAttempt? old = null;
        try
        {
            lock (gate)
            {
                Entry? entry = disposed ? null : entries.FirstOrDefault(item => item.Key == key);
                if (entry is null || (entry.Attempt is not null && !Removable(entry.Attempt))) return false;
                entries.Remove(entry); changed = true; old = entry.Attempt;
                message = entry.Existing is not null
                    ? "최종 목록에서 제외했습니다. 변경 실행 때 이 업무의 참조만 해제하며 파일 DELETE는 요청하지 않습니다."
                    : "화면의 선택만 해제했습니다. 서버 임시 파일은 삭제하지 않았으며 별도 정리가 필요할 수 있습니다.";
                return true;
            }
        }
        finally { old?.Dispose(); owner.EndAttachmentChange(); Notify(); }
    }
    private static bool Removable(TaskCreationUploadAttempt item) => item.State is TaskCreationUploadState.Ready
        or TaskCreationUploadState.StartUncertain or TaskCreationUploadState.Discarded
        or TaskCreationUploadState.DiscardUncertain
        || (item.State == TaskCreationUploadState.Rejected && item.AttachmentId is null)
        || (item.State == TaskCreationUploadState.DiscardRejected && item.StatusCode == 409
            && item.ErrorCode == "ATTACHMENT_UNAVAILABLE");
    private void SetMessage(string value) { lock (gate) if (!disposed) message = value; }
    private void OwnerChanged(object? sender, EventArgs e)
    {
        if (owner.State is TaskManagementState.Disconnected or TaskManagementState.Succeeded
            || (owner.State == TaskManagementState.Blocked && owner.Failure == TaskManagementFailure.Forbidden))
            ClearWithoutDelete(owner.State == TaskManagementState.Succeeded
                ? "업무 변경 성공을 확인해 화면 선택을 정리했습니다. 파일 삭제를 요청하지 않았습니다."
                : "접속이 종료되어 화면 선택을 정리했습니다. 파일 삭제를 요청하지 않았습니다.",
                invalidate: owner.State != TaskManagementState.Succeeded);
        Notify();
    }
    private void ClearWithoutDelete(string reason, bool invalidate = false)
    {
        Entry[] old;
        lock (gate)
        {
            invalidated |= invalidate;
            old = entries.ToArray(); entries.Clear();
            rowVersion = null; assignmentRevision = 0; loaded = false; changed = false; message = reason;
        }
        foreach (var entry in old) entry.Attempt?.Dispose();
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
        ClearWithoutDelete("화면의 첨부 선택을 해제했습니다. 서버 파일은 삭제하지 않았습니다.");
        Notify();
    }
    private enum ActionKind { Upload, Retry, Discard }
}
