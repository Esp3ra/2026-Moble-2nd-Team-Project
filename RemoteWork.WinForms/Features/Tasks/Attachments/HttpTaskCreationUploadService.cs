using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.WinForms.Features.Tasks.Attachments;

/// <summary>Prepares initial-task files for member 3's existing shared-upload API.</summary>
public sealed class HttpTaskCreationUploadService(ApiClient.Session session, string? taskId = null)
{
    public const long MaxFileBytes = 25 * 1024 * 1024;
    public bool IsCurrent => session.IsCurrent;
    public string? TaskId => taskId;

    // Preparation takes a private memory snapshot. It sends no HTTP request and never
    // retains the local path. Replacing the local file cannot change a pending upload.
    public async Task<TaskCreationUploadAttempt> PrepareFileAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (taskId is not null && (!long.TryParse(taskId, NumberStyles.None, CultureInfo.InvariantCulture, out long id)
            || id <= 0 || id.ToString(CultureInfo.InvariantCulture) != taskId))
            throw new ArgumentException("첨부를 준비할 업무 번호를 확인해 주세요.");
        session.ThrowIfChanged();
        if (session.User.Role != "admin" || !session.User.IsActive)
            throw new ApiException("업무 등록 첨부는 로그인한 관리자만 준비할 수 있습니다.", 403, "FORBIDDEN");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct, session.Cancellation);
        cancellation.Token.ThrowIfCancellationRequested();
        string name = Path.GetFileName(path);
        ValidateName(name);
        byte[]? content = null;
        try
        {
            await using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                16384, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                long length = file.Length;
                if (length < 0 || length > MaxFileBytes)
                    throw new ArgumentException("첨부 파일은 25 MiB 이하여야 합니다.", nameof(path));
                content = new byte[(int)length];
                await file.ReadExactlyAsync(content.AsMemory(), cancellation.Token).ConfigureAwait(false);
                byte[] extra = new byte[1];
                if (await file.ReadAsync(extra, cancellation.Token).ConfigureAwait(false) != 0 || file.Length != length)
                    throw new IOException("준비 중 파일 크기가 변경되었습니다. 파일을 다시 선택해 주세요.");
            }
            string hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
            session.ThrowIfChanged();
            cancellation.Token.ThrowIfCancellationRequested();
            var attempt = new TaskCreationUploadAttempt(session, name, content, hash, taskId);
            content = null; // The attempt owns and clears the snapshot from this point.
            return attempt;
        }
        finally
        {
            if (content is not null) CryptographicOperations.ZeroMemory(content);
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrEmpty(name) || name is "." or ".." || name.EnumerateRunes().Count() > 255
            || name.IndexOfAny(['/', '\\', '\r', '\n', '\0']) >= 0)
            throw new ArgumentException("파일 이름은 1~255자이며 경로·줄바꿈 문자를 포함할 수 없습니다.");
        try { _ = new UTF8Encoding(false, true).GetByteCount(name); }
        catch (EncoderFallbackException) { throw new ArgumentException("파일 이름의 문자 형식을 확인해 주세요."); }
    }
}
