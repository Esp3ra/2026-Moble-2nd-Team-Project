using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.Attachments;

public enum TaskCreationUploadState
{
    Ready, Sending, NeedsRetry, StartUncertain, Completed, Rejected, Disconnected, Disposed,
    Discarding, DiscardUncertain, DiscardRejected, Discarded
}

/// <summary>
/// One immutable file, one captured login and at most one upload start. Only an
/// explicit retry repeats a chunk or completion for the same validated upload ID.
/// This is not an attachment selection or a saved task. DELETE requires a separate
/// explicit call; the creation owner must exclude pending or committed task files.
/// </summary>
public sealed class TaskCreationUploadAttempt : IDisposable
{
    private const int ChunkBytes = 16384;
    private const string UploadPath = "results/shared/uploads";
    private readonly ApiClient.Session session;
    private readonly string? taskId;
    private readonly object gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly CancellationTokenRegistration sessionRegistration;
    private byte[]? content;
    private bool busy, disposed, disconnected, lifetimeDisposed, disposalCancellationFinished;
    private TaskCreationUploadState state = TaskCreationUploadState.Ready;
    private UploadPhase phase;
    private string? attachmentId;
    private long confirmedBytes;
    private TaskAttachment? completedFile;
    private string message = "파일 준비가 완료되었습니다. 업로드 시작 전입니다.";
    private int? statusCode;
    private string? errorCode;

    internal TaskCreationUploadAttempt(ApiClient.Session session, string name, byte[] content, string sha256, string? taskId = null)
    {
        this.session = session;
        this.taskId = taskId;
        this.content = content;
        Name = name;
        Size = content.LongLength;
        Sha256 = sha256;
        sessionRegistration = session.Cancellation.Register(OnSessionChanged);
    }

    public string Name { get; }
    public long Size { get; }
    public string Sha256 { get; }
    public string? AttachmentId { get { lock (gate) return attachmentId; } }
    public long ConfirmedBytes { get { lock (gate) return confirmedBytes; } }
    public TaskAttachment? CompletedFile { get { lock (gate) return completedFile; } }
    public TaskCreationUploadState State { get { lock (gate) return state; } }
    public string Message { get { lock (gate) return message; } }
    public int? StatusCode { get { lock (gate) return statusCode; } }
    public string? ErrorCode { get { lock (gate) return errorCode; } }
    public bool IsCurrent
    {
        get
        {
            lock (gate) if (disposed || disconnected) return false;
            // ApiClient can invoke cancellation while holding its own identity lock.
            // Never acquire that lock while holding our state lock.
            return session.IsCurrent && !session.Cancellation.IsCancellationRequested;
        }
    }

    public bool CanDiscard
    {
        get
        {
            bool allowed;
            lock (gate) allowed = !disposed && !disconnected && !busy && attachmentId is not null
                && state is (TaskCreationUploadState.Completed or TaskCreationUploadState.NeedsRetry
                    or TaskCreationUploadState.Rejected or TaskCreationUploadState.DiscardUncertain
                    or TaskCreationUploadState.DiscardRejected);
            return allowed && IsCurrent;
        }
    }

    public Task StartAsync(CancellationToken ct = default) => RunAsync(retry: false, ct);
    public Task RetryAsync(CancellationToken ct = default) => RunAsync(retry: true, ct);

    /// <summary>
    /// Explicitly discard this captured login's known temporary upload. The owner
    /// must lock out this action while any task write includes the ID or its result
    /// is uncertain. This method never obtains a new ID/key or retries on its own.
    /// </summary>
    public async Task DiscardAsync(CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested) return;
        bool current = session.IsCurrent && !session.Cancellation.IsCancellationRequested;
        string id;
        TaskCreationUploadState previous;
        TaskAttachment? previousCompleted;
        lock (gate)
        {
            if (disposed || disconnected || busy) return;
            if (!current) { DisconnectLocked("접속이 변경되었습니다. 이전 파일을 삭제할 수 없습니다."); return; }
            if (attachmentId is null || state is not (TaskCreationUploadState.Completed
                or TaskCreationUploadState.NeedsRetry or TaskCreationUploadState.Rejected
                or TaskCreationUploadState.DiscardUncertain or TaskCreationUploadState.DiscardRejected)) return;
            if (ct.IsCancellationRequested) return;
            id = attachmentId;
            previous = state;
            previousCompleted = completedFile;
            busy = true;
            state = TaskCreationUploadState.Discarding;
            completedFile = null;
            statusCode = null;
            errorCode = null;
            message = "선택한 임시 첨부의 삭제를 요청하고 있습니다.";
        }

        bool dispatched = false;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token, session.Cancellation);
        try
        {
            cancellation.Token.ThrowIfCancellationRequested();
            session.ThrowIfChanged();
            dispatched = true;
            JsonElement result = await session.DeleteAsync<JsonElement>(UploadPath + "/" + id,
                cancellation.Token).ConfigureAwait(false);
            Exact(result, "attachment_id");
            if (Text(result, "attachment_id") != id) throw InvalidResponse();
            if (!KeepCurrent()) return;
            lock (gate)
            {
                if (disposed || disconnected) return;
                state = TaskCreationUploadState.Discarded;
                message = "서버가 임시 첨부 삭제 요청을 처리했습니다.";
            }
        }
        catch (Exception error)
        {
            if (error is ApiException responseError)
            {
                lock (gate)
                {
                    if (!disposed)
                    {
                        statusCode = responseError.StatusCode;
                        errorCode = responseError.Code;
                    }
                }
            }
            if (!KeepCurrent()) return;
            lock (gate)
            {
                if (disposed || disconnected) return;
                if (error is ApiException api)
                {
                    if (api.StatusCode is 401 or 403)
                    {
                        DisconnectLocked(api.Message);
                        return;
                    }
                    // Only the provider's precise "missing/deleting" response proves
                    // absence. A generic route/proxy 404 is not a deletion receipt.
                    if (api.StatusCode == 404 && api.Code == "ATTACHMENT_NOT_FOUND")
                    {
                        state = TaskCreationUploadState.Discarded;
                        message = "서버에 해당 임시 첨부가 없거나 이미 삭제 처리 중입니다.";
                        return;
                    }
                    if (api.StatusCode is 400 or 409 or 413)
                    {
                        state = TaskCreationUploadState.DiscardRejected;
                        message = api.Message;
                        return;
                    }
                }
                if (!dispatched)
                {
                    state = previous;
                    completedFile = previousCompleted;
                    message = "임시 첨부 삭제를 요청하지 않았습니다.";
                }
                else
                {
                    state = TaskCreationUploadState.DiscardUncertain;
                    message = "삭제 결과를 확인하지 못했습니다. 같은 임시 첨부의 삭제를 명시적으로 다시 확인해 주세요.";
                }
            }
        }
        finally { FinishRun(); }
    }

    private async Task RunAsync(bool retry, CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;
        bool current = session.IsCurrent && !session.Cancellation.IsCancellationRequested;
        TaskCreationUploadState previous;
        lock (gate)
        {
            if (disposed || disconnected || busy) return;
            if (!current) { DisconnectLocked("접속이 변경되었습니다. 새 로그인에서 파일을 다시 준비해 주세요."); return; }
            if ((!retry && state != TaskCreationUploadState.Ready)
                || (retry && state != TaskCreationUploadState.NeedsRetry)) return;
            if (ct.IsCancellationRequested) return;
            previous = state;
            busy = true;
            state = TaskCreationUploadState.Sending;
            statusCode = null;
            errorCode = null;
            message = "첨부 파일을 업로드하고 있습니다.";
        }

        bool dispatched = false;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token, session.Cancellation);
        try
        {
            if (phase == UploadPhase.Start)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                session.ThrowIfChanged();
                dispatched = true;
                object body = taskId is null ? new { name = Name, size = Size, sha256 = Sha256 }
                    : new { name = Name, size = Size, sha256 = Sha256, task_id = taskId };
                JsonElement data = await session.PostAsync<JsonElement>(UploadPath + "/start",
                    body, cancellation.Token).ConfigureAwait(false);
                TaskAttachment started = ReadFile(data, expectedId: null);
                if (!KeepCurrent()) return;
                lock (gate)
                {
                    if (disposed || disconnected) return;
                    attachmentId = started.AttachmentId;
                    phase = Size == 0 ? UploadPhase.Complete : UploadPhase.Chunks;
                }
            }

            while (phase == UploadPhase.Chunks)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                session.ThrowIfChanged();
                string id;
                long offset;
                string data;
                int count;
                lock (gate)
                {
                    if (disposed || disconnected) return;
                    id = attachmentId!;
                    offset = confirmedBytes;
                    count = (int)Math.Min(ChunkBytes, Size - offset);
                    data = Convert.ToBase64String(content!, (int)offset, count);
                }
                dispatched = true;
                JsonElement progress = await session.PostAsync<JsonElement>(UploadPath + "/" + id + "/chunks",
                    new ChunkRequest(offset, data), cancellation.Token).ConfigureAwait(false);
                Exact(progress, "attachment_id", "next_offset");
                if (Text(progress, "attachment_id") != id || Number(progress, "next_offset") != offset + count)
                    throw InvalidResponse();
                if (!KeepCurrent()) return;
                lock (gate)
                {
                    if (disposed || disconnected) return;
                    confirmedBytes = offset + count;
                    if (confirmedBytes == Size) phase = UploadPhase.Complete;
                }
            }

            cancellation.Token.ThrowIfCancellationRequested();
            session.ThrowIfChanged();
            string completingId;
            lock (gate)
            {
                if (disposed || disconnected) return;
                completingId = attachmentId!;
            }
            dispatched = true;
            JsonElement complete = await session.PostAsync<JsonElement>(UploadPath + "/" + completingId + "/complete",
                new { }, cancellation.Token).ConfigureAwait(false);
            TaskAttachment file = ReadFile(complete, completingId);
            if (!KeepCurrent()) return;
            lock (gate)
            {
                if (disposed || disconnected) return;
                completedFile = file;
                state = TaskCreationUploadState.Completed;
                message = taskId is null
                    ? "첨부 업로드가 확인되었습니다. 업무 등록과 첨부 연결은 아직 수행하지 않았습니다."
                    : "첨부 업로드가 확인되었습니다. 업무 변경과 첨부 연결은 아직 수행하지 않았습니다.";
            }
        }
        catch (Exception error)
        {
            if (error is ApiException responseError)
            {
                lock (gate)
                {
                    if (!disposed)
                    {
                        statusCode = responseError.StatusCode;
                        errorCode = responseError.Code;
                    }
                }
            }
            if (!KeepCurrent()) return;
            lock (gate)
            {
                if (disposed || disconnected) return;
                if (error is ApiException api)
                {
                    statusCode = api.StatusCode;
                    errorCode = api.Code;
                    if (api.StatusCode is 401 or 403)
                    {
                        DisconnectLocked(api.Message);
                        return;
                    }
                    if (api.StatusCode is 400 or 404 or 409 or 413)
                    {
                        state = TaskCreationUploadState.Rejected;
                        message = api.Message;
                        return;
                    }
                }
                if (!dispatched)
                {
                    state = previous;
                    message = previous == TaskCreationUploadState.Ready
                        ? "업로드를 시작하지 않았습니다." : "같은 업로드 재시도를 시작하지 않았습니다.";
                }
                else if (attachmentId is null)
                {
                    state = TaskCreationUploadState.StartUncertain;
                    message = "업로드 시작 결과를 확인할 수 없습니다. 중복 생성을 막기 위해 이 요청을 다시 시작하지 않습니다.";
                }
                else
                {
                    state = TaskCreationUploadState.NeedsRetry;
                    message = phase == UploadPhase.Complete
                        ? "완료 결과를 확인하지 못했습니다. 재시도하면 같은 첨부의 완료만 다시 확인합니다."
                        : "전송 결과를 확인하지 못했습니다. 재시도하면 같은 첨부·위치·파일 내용으로 이어갑니다.";
                }
            }
        }
        finally { FinishRun(); }
    }

    private void FinishRun()
    {
        bool disposeLifetime;
        lock (gate)
        {
            busy = false;
            if (state is TaskCreationUploadState.Completed or TaskCreationUploadState.Rejected
                or TaskCreationUploadState.StartUncertain or TaskCreationUploadState.Disconnected or TaskCreationUploadState.Disposed
                or TaskCreationUploadState.DiscardUncertain or TaskCreationUploadState.DiscardRejected or TaskCreationUploadState.Discarded)
                ClearContentLocked();
            disposeLifetime = disposed && disposalCancellationFinished && !lifetimeDisposed;
            if (disposeLifetime) lifetimeDisposed = true;
        }
        if (disposeLifetime) lifetime.Dispose();
    }

    private bool KeepCurrent()
    {
        bool current = session.IsCurrent && !session.Cancellation.IsCancellationRequested;
        lock (gate)
        {
            if (disposed || disconnected) return false;
            if (!current) DisconnectLocked("로그인이 만료되거나 변경되었습니다. 파일을 다시 준비해 주세요.");
            return current;
        }
    }

    private TaskAttachment ReadFile(JsonElement data, string? expectedId)
    {
        Exact(data, "attachment_id", "name", "size", "sha256");
        string id = Text(data, "attachment_id");
        if (!Guid.TryParseExact(id, "D", out Guid guid) || guid.ToString("D") != id
            || (expectedId is not null && id != expectedId)
            || Text(data, "name") != Name || Number(data, "size") != Size || Text(data, "sha256") != Sha256)
            throw InvalidResponse();
        return new TaskAttachment(id, Name, Size, Sha256);
    }

    private static void Exact(JsonElement data, params string[] fields)
    {
        if (data.ValueKind != JsonValueKind.Object) throw InvalidResponse();
        string[] names = data.EnumerateObject().Select(p => p.Name).ToArray();
        if (names.Length != fields.Length || names.Distinct(StringComparer.Ordinal).Count() != names.Length
            || names.Except(fields, StringComparer.Ordinal).Any()) throw InvalidResponse();
    }

    private static string Text(JsonElement data, string field)
    {
        JsonElement value = data.GetProperty(field);
        return value.ValueKind == JsonValueKind.String && value.GetString() is { } result ? result : throw InvalidResponse();
    }

    private static long Number(JsonElement data, string field)
    {
        JsonElement value = data.GetProperty(field);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long result) ? result : throw InvalidResponse();
    }

    private static InvalidDataException InvalidResponse() => new("첨부 업로드 응답 형식을 확인할 수 없습니다.");

    private void OnSessionChanged()
    {
        lock (gate)
        {
            if (disposed || disconnected) return;
            DisconnectLocked("접속이 변경되었습니다. 새 로그인에서 파일을 다시 준비해 주세요.");
        }
    }

    private void DisconnectLocked(string reason)
    {
        disconnected = true;
        state = TaskCreationUploadState.Disconnected;
        message = reason;
        attachmentId = null;
        completedFile = null;
        confirmedBytes = 0;
        if (!busy) ClearContentLocked();
    }

    private void ClearContentLocked()
    {
        if (content is null) return;
        CryptographicOperations.ZeroMemory(content);
        content = null;
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            state = TaskCreationUploadState.Disposed;
            message = "파일 준비를 해제했습니다. 추가 서버 삭제를 요청하지 않았습니다.";
            attachmentId = null;
            completedFile = null;
            confirmedBytes = 0;
            if (!busy) ClearContentLocked();
        }
        lifetime.Cancel();
        sessionRegistration.Dispose();
        bool disposeLifetime;
        lock (gate)
        {
            disposalCancellationFinished = true;
            disposeLifetime = !busy && !lifetimeDisposed;
            if (disposeLifetime) lifetimeDisposed = true;
        }
        if (disposeLifetime) lifetime.Dispose();
    }

    private enum UploadPhase { Start, Chunks, Complete }

    // The server requires exact 16 KiB chunks and limits each JSON body to 32 KiB.
    // Default JSON encoding turns '+' into six bytes, exceeding that limit for
    // valid binary files. Only this internally generated base64 value is pre-encoded;
    // common JSON options, file names and other strings keep their existing encoder.
    [JsonConverter(typeof(ChunkRequestConverter))]
    private sealed record ChunkRequest(long Offset, string Data);

    private sealed class ChunkRequestConverter : JsonConverter<ChunkRequest>
    {
        public ChunkRequestConverter() { }

        public override ChunkRequest Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, ChunkRequest value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("offset", value.Offset);
            writer.WriteString(JsonEncodedText.Encode("data"),
                JsonEncodedText.Encode(value.Data, JavaScriptEncoder.UnsafeRelaxedJsonEscaping));
            writer.WriteEndObject();
        }
    }
}
