using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration;

public sealed record ResourceAttachment(string AttachmentId, string Name, long Size, string Sha256);

/// <summary>Member 3 JSON chunk protocol; captured login, no automatic POST retry.</summary>
public sealed class ResourceAttachmentClient(ApiClient.Session session)
{
    private const string Prefix = "results/shared/resources";
    private const int ChunkBytes = 16384;
    private readonly Dictionary<string, ResourceAttachment> pending = new();
    public void ForgetLinkedUploads(IEnumerable<string> ids)
    {
        var linked = ids.ToHashSet(StringComparer.Ordinal);
        foreach (var key in pending.Where(p => linked.Contains(p.Value.AttachmentId)).Select(p => p.Key).ToArray()) pending.Remove(key);
    }
    private sealed record ListData(ResourceAttachment[] Items);
    private sealed record Chunk(string AttachmentId, int Offset, int NextOffset, bool Eof, string Data);
    private sealed record UploadState(string State, int[] ReceivedOffsets);
    private sealed record Empty;
    // Default JSON escaping expands '+' to six bytes, exceeding the common 32KiB
    // request cap for a valid 16KiB chunk. Only validated base64 is written raw.
    private sealed class Base64Converter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetString()!;
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            if (value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '+' and not '/' and not '='))
                throw new JsonException("Invalid base64");
            writer.WriteRawValue("\"" + value + "\"");
        }
    }
    private sealed record UploadChunk(int Offset, [property: JsonConverter(typeof(Base64Converter))] string Data);
    private static string Kind(string kind) => kind is "notice" or "chat" ? kind : throw new ArgumentException("첨부 종류를 확인해 주세요.");
    private static void Validate(ResourceAttachment file)
    {
        if (!Guid.TryParseExact(file.AttachmentId, "D", out var id) || id.ToString("D") != file.AttachmentId
            || file.Size is < 0 or > 26214400 || file.Sha256.Length != 64
            || file.Sha256.Any(c => !char.IsAsciiHexDigitLower(c)) || string.IsNullOrWhiteSpace(file.Name)
            || Path.GetFileName(file.Name) != file.Name || file.Name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ApiException("첨부파일 응답 형식을 확인해 주세요.");
    }
    public async Task<ResourceAttachment[]> ListAsync(string kind, string resource, CancellationToken ct = default)
    {
        var data = await session.GetAsync<ListData>($"{Prefix}/{Kind(kind)}/{Uri.EscapeDataString(resource)}/files", ct);
        if (data.Items is null || data.Items.Length > 10 || data.Items.Select(f => f.AttachmentId).Distinct().Count() != data.Items.Length)
            throw new ApiException("첨부파일 목록을 확인해 주세요.");
        foreach (var file in data.Items) Validate(file);
        return data.Items;
    }
    public async Task<ResourceAttachment> UploadAsync(string kind, string? context, string path, CancellationToken ct = default)
    {
        session.ThrowIfChanged();
        var info = new FileInfo(path);
        if (!info.Exists || info.Length > 26214400) throw new ApiException("파일 하나는 25MiB 이하여야 합니다.");
        var bytes = await File.ReadAllBytesAsync(path, ct);
        if (bytes.Length > 26214400) throw new ApiException("파일 하나는 25MiB 이하여야 합니다.");
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string cache = $"{Kind(kind)}:{context}:{Path.GetFullPath(path)}:{hash}";
        string root = $"{Prefix}/{kind}/uploads";
        if (!pending.TryGetValue(cache, out var file))
        {
            file = await session.PostAsync<ResourceAttachment>(root + "/start",
                new { name = Path.GetFileName(path), size = bytes.Length, sha256 = hash, context_id = context }, ct);
            Validate(file);
            if (file.Size != bytes.Length || file.Sha256 != hash || file.Name != Path.GetFileName(path)) throw new ApiException("업로드 정보가 일치하지 않습니다.");
            pending[cache] = file;
        }
        // GET determines which chunks a previous explicit attempt stored. It is
        // never evidence that the original notice/message was saved.
        var state = await session.GetAsync<UploadState>($"{root}/{file.AttachmentId}", ct);
        if (state.State == "ready") return file;
        if (state.State != "uploading" || state.ReceivedOffsets is null) throw new ApiException("첨부 전송 상태를 확인해 주세요.");
        var received = state.ReceivedOffsets.ToHashSet();
        for (int offset = 0; offset < bytes.Length; offset += ChunkBytes)
        {
            if (received.Contains(offset)) continue;
            await session.PostAsync<Empty>($"{root}/{file.AttachmentId}/chunks",
                new UploadChunk(offset, Convert.ToBase64String(bytes, offset, Math.Min(ChunkBytes, bytes.Length - offset))), ct);
        }
        await session.PostAsync<ResourceAttachment>($"{root}/{file.AttachmentId}/complete", new { }, ct);
        session.ThrowIfChanged();
        return file;
    }
    public async Task<byte[]> DownloadAsync(string kind, string resource, ResourceAttachment file, CancellationToken ct = default)
    {
        Validate(file);
        using var output = new MemoryStream();
        int offset = 0;
        while (true)
        {
            var part = await session.GetAsync<Chunk>($"{Prefix}/{Kind(kind)}/{Uri.EscapeDataString(resource)}/files/{file.AttachmentId}/chunks?offset={offset}", ct);
            byte[] bytes;
            try { bytes = Convert.FromBase64String(part.Data); }
            catch (FormatException) { throw new ApiException("첨부 조각의 형식이 올바르지 않습니다."); }
            if (part.AttachmentId != file.AttachmentId || part.Offset != offset || bytes.Length != Math.Min(ChunkBytes, file.Size - offset)
                || part.NextOffset != offset + bytes.Length || part.Eof != (part.NextOffset == file.Size))
                throw new ApiException("첨부 조각이 일치하지 않습니다.");
            output.Write(bytes); offset = part.NextOffset;
            if (part.Eof) break;
        }
        session.ThrowIfChanged();
        var result = output.ToArray();
        if (result.LongLength != file.Size || Convert.ToHexString(SHA256.HashData(result)).ToLowerInvariant() != file.Sha256)
            throw new ApiException("다운로드 크기·SHA-256 검증에 실패했습니다. 파일을 저장하지 않았습니다.");
        return result;
    }
}
