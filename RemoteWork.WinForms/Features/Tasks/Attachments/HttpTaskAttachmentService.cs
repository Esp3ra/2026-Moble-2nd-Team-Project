using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.Attachments;

public sealed record TaskAttachment(string AttachmentId, string Name, long Size, string Sha256);

/// <summary>Read consumer of member 3's existing shared-file API, bound to one login.</summary>
public sealed class HttpTaskAttachmentService(ApiClient.Session session)
{
    public const long MaxFileBytes = 25 * 1024 * 1024;
    private const int ChunkBytes = 16384;
    public bool IsCurrent => session.IsCurrent;

    public async Task<IReadOnlyList<TaskAttachment>> GetFilesAsync(string taskId, CancellationToken ct = default)
    {
        RequireTaskId(taskId);
        session.ThrowIfChanged();
        JsonElement data = await session.GetAsync<JsonElement>("results/shared/tasks/" + taskId, ct).ConfigureAwait(false);
        session.ThrowIfChanged(); ct.ThrowIfCancellationRequested();
        Exact(data, "items");
        JsonElement items = data.GetProperty("items");
        if (items.ValueKind != JsonValueKind.Array || items.GetArrayLength() > 10) throw Invalid();
        var files = new List<TaskAttachment>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonElement item in items.EnumerateArray())
        {
            Exact(item, "attachment_id", "name", "size", "sha256");
            string id = Text(item, "attachment_id"), name = Text(item, "name"), hash = Text(item, "sha256");
            if (item.GetProperty("size").ValueKind != JsonValueKind.Number || !item.GetProperty("size").TryGetInt64(out long size)) throw Invalid();
            var file = new TaskAttachment(id, name, size, hash);
            Validate(file);
            if (!ids.Add(id)) throw Invalid();
            files.Add(file);
        }
        session.ThrowIfChanged(); ct.ThrowIfCancellationRequested();
        return files.AsReadOnly();
    }

    public async Task<TaskAttachmentDownload> DownloadAsync(string taskId, TaskAttachment file, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        Validate(file);
        // A file may be shared with other tasks. Check this displayed task before
        // and after transfer; file-ID access through another task is insufficient.
        await RequireCurrentLinkAsync(taskId, file, ct).ConfigureAwait(false);
        using var content = new MemoryStream((int)file.Size);
        long offset = 0;
        do
        {
            JsonElement chunk = await session.GetAsync<JsonElement>(
                "results/shared/files/" + file.AttachmentId + "/chunks?offset=" + offset.ToString(CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
            session.ThrowIfChanged(); ct.ThrowIfCancellationRequested();
            Exact(chunk, "attachment_id", "offset", "next_offset", "eof", "data");
            if (Text(chunk, "attachment_id") != file.AttachmentId
                || chunk.GetProperty("offset").ValueKind != JsonValueKind.Number || !chunk.GetProperty("offset").TryGetInt64(out long actualOffset) || actualOffset != offset
                || chunk.GetProperty("next_offset").ValueKind != JsonValueKind.Number || !chunk.GetProperty("next_offset").TryGetInt64(out long nextOffset)
                || chunk.GetProperty("eof").ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Invalid();
            string encoded = Text(chunk, "data");
            if (encoded.Length > 4 * ((ChunkBytes + 2) / 3)) throw Invalid();
            byte[] bytes;
            try { bytes = Convert.FromBase64String(encoded); }
            catch (FormatException) { throw Invalid(); }
            bool eof = chunk.GetProperty("eof").GetBoolean();
            if (Convert.ToBase64String(bytes) != encoded || bytes.Length > ChunkBytes
                || nextOffset != offset + bytes.Length || nextOffset > file.Size
                || eof != (nextOffset == file.Size) || (!eof && bytes.Length == 0)) throw Invalid();
            content.Write(bytes);
            offset = nextOffset;
            if (eof) break;
        } while (offset < file.Size);
        byte[] complete = content.ToArray();
        string hash = Convert.ToHexString(SHA256.HashData(complete)).ToLowerInvariant();
        if (complete.LongLength != file.Size || hash != file.Sha256) throw Invalid();
        await RequireCurrentLinkAsync(taskId, file, ct).ConfigureAwait(false);
        session.ThrowIfChanged(); ct.ThrowIfCancellationRequested();
        return new TaskAttachmentDownload(file, complete, session, token => RequireCurrentLinkAsync(taskId, file, token));
    }

    private async Task RequireCurrentLinkAsync(string taskId, TaskAttachment file, CancellationToken ct)
    {
        if (!(await GetFilesAsync(taskId, ct).ConfigureAwait(false)).Contains(file))
            throw new ApiException("업무 첨부가 변경됐습니다. 업무 상세를 다시 조회해 주세요.", 409, "TASK_ATTACHMENT_CHANGED");
    }

    private static void RequireTaskId(string id)
    {
        if (!long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out long value)
            || value <= 0 || value.ToString(CultureInfo.InvariantCulture) != id)
            throw new ArgumentException("업무 번호를 확인해 주세요.", nameof(id));
    }

    private static void Validate(TaskAttachment file)
    {
        if (!Guid.TryParseExact(file.AttachmentId, "D", out Guid id) || id.ToString("D") != file.AttachmentId
            || string.IsNullOrEmpty(file.Name) || file.Name.EnumerateRunes().Count() > 255 || file.Name is "." or ".."
            || file.Name.IndexOfAny(['/', '\\', '\r', '\n', '\0']) >= 0
            || file.Size < 0 || file.Size > MaxFileBytes || file.Sha256 is null || file.Sha256.Length != 64
            || file.Sha256.Any(c => !(c is >= '0' and <= '9' or >= 'a' and <= 'f'))) throw Invalid();
    }

    private static string Text(JsonElement value, string field)
    {
        JsonElement text = value.GetProperty(field);
        if (text.ValueKind != JsonValueKind.String || text.GetString() is not { } result) throw Invalid();
        return result;
    }

    private static void Exact(JsonElement value, params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Invalid();
        string[] names = value.EnumerateObject().Select(p => p.Name).ToArray();
        if (names.Length != fields.Length || names.Distinct(StringComparer.Ordinal).Count() != names.Length
            || names.Except(fields, StringComparer.Ordinal).Any()) throw Invalid();
    }

    private static InvalidDataException Invalid() => new("첨부 응답의 이름·크기·파일 내용이 올바르지 않습니다. 다시 조회해 주세요.");
}

/// <summary>Only a fully checked download can be saved; an existing path is never overwritten.</summary>
public sealed class TaskAttachmentDownload
{
    private readonly byte[] content;
    private readonly ApiClient.Session session;
    private readonly Func<CancellationToken, Task> recheck;
    internal TaskAttachmentDownload(TaskAttachment file, byte[] content, ApiClient.Session session, Func<CancellationToken, Task> recheck)
    { File = file; this.content = content; this.session = session; this.recheck = recheck; }
    public TaskAttachment File { get; }

    public async Task SaveNewAsync(string path, CancellationToken ct = default)
    {
        string target = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(target) ?? throw new ArgumentException("저장할 폴더를 확인해 주세요.", nameof(path));
        string temporary = Path.Combine(directory, ".remotework-download-" + Guid.NewGuid().ToString("N") + ".tmp");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, session.Cancellation);
        session.ThrowIfChanged(); linked.Token.ThrowIfCancellationRequested();
        // The user may keep SaveFileDialog open while server relationships change.
        await recheck(linked.Token).ConfigureAwait(false);
        session.ThrowIfChanged(); linked.Token.ThrowIfCancellationRequested();
        bool created = false;
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16384, true))
            {
                created = true;
                await output.WriteAsync(content, linked.Token).ConfigureAwait(false);
                await output.FlushAsync(linked.Token).ConfigureAwait(false);
            }
            session.ThrowIfChanged(); linked.Token.ThrowIfCancellationRequested();
            System.IO.File.Move(temporary, target, overwrite: false);
            created = false;
        }
        finally
        {
            if (created)
            {
                try { System.IO.File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
}
