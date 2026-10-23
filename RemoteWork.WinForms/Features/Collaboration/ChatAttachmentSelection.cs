using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Collaboration;

/// <summary>Original selection order and all unfinished paths, including untried
/// files after a failure. Local selection removal never releases server refs.</summary>
internal sealed class ChatAttachmentSelection
{
    internal sealed record Entry(string Path, ResourceAttachment? File);
    private readonly List<Entry> entries = new();
    internal IReadOnlyList<Entry> Entries => entries.AsReadOnly();
    internal bool HasPending => entries.Any(e => e.File is null);
    internal ResourceAttachment[] Completed => entries.Where(e => e.File is not null).Select(e => e.File!).ToArray();
    internal string[] Add(IEnumerable<string> selected)
    {
        var paths = selected.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var added = paths.Where(p => !entries.Any(e => StringComparer.OrdinalIgnoreCase.Equals(e.Path, p))).ToArray();
        if (entries.Count + added.Length > 10) throw new ApiException("전송 대기 파일을 포함해 최대 10개까지 선택해 주세요.");
        entries.AddRange(added.Select(p => new Entry(p, null)));
        return paths;
    }
    internal async Task UploadAsync(IEnumerable<string> selected,
        Func<string, CancellationToken, Task<ResourceAttachment>> upload, CancellationToken ct)
    {
        // Register the COMPLETE selection before any upload can throw.
        var paths = Add(selected);
        foreach (var path in paths)
        {
            ct.ThrowIfCancellationRequested();
            int index = entries.FindIndex(e => StringComparer.OrdinalIgnoreCase.Equals(e.Path, path));
            if (entries[index].File is not null) continue;
            var file = await upload(entries[index].Path, ct);
            ct.ThrowIfCancellationRequested();
            if (entries.Any(e => e.File?.AttachmentId == file.AttachmentId))
                throw new ApiException("서로 다른 선택 파일에 같은 첨부 ID가 전달됐습니다.");
            entries[index] = entries[index] with { File = file };
        }
    }
    internal void Remove(string path) => entries.RemoveAll(e => StringComparer.OrdinalIgnoreCase.Equals(e.Path, path));
}
