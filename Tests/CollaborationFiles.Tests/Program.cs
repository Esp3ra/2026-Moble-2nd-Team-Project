using System.Reflection;
using System.Security.Cryptography;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    [STAThread]
    private static async Task Main()
    {
        string url = Environment.GetEnvironmentVariable("FILES_TEST_URL")!, folder = Environment.GetEnvironmentVariable("FILES_TEST_DIR")!;
        using var api = new ApiClient(url);
        await api.LoginAsync("admin1", "Admin-test-2026!");
        var session = api.CaptureSession(); var files = new ResourceAttachmentClient(session);
        // More than one chunk, many base64 '+' signs, Korean and hostile-looking
        // names, and an empty file exercise the real common JSON size boundary.
        var bytes = Enumerable.Repeat((byte)251, 49153).ToArray();
        string path = Path.Combine(folder, "한글 + 자료.txt"); await File.WriteAllBytesAsync(path, bytes);
        var file = await files.UploadAsync("notice", null, path);
        Check(file.Size == bytes.Length, "desktop upload size");
        Check((await files.UploadAsync("notice", null, path)).AttachmentId == file.AttachmentId, "explicit ready retry preserves temp ID");
        var service = new HttpNoticeService(api);
        var notice = await service.CreateNoticeAsync(new CreateNoticeRequest("C# 실제 첨부", "클라이언트 작성", "전 직원", false, []) { AttachmentIds = [file.AttachmentId] });
        var listed = await files.ListAsync("notice", notice.Id.ToString());
        Check(listed.Single().AttachmentId == file.AttachmentId, "notice same-TX link visible");
        Check((await files.DownloadAsync("notice", notice.Id.ToString(), listed.Single())).SequenceEqual(bytes), "desktop download bytes/hash");
        try { await files.DownloadAsync("notice", notice.Id.ToString(), file with { Sha256 = new string('0', 64) }); throw new Exception("bad hash accepted"); }
        catch (ApiException) { checks++; }
        try { await service.DeleteNoticeAsync(notice.Id); throw new Exception("unagreed deletion accepted"); }
        catch (ApiException ex) { Check(ex.StatusCode == 409, "attached notice delete fails closed"); }
        string empty = Path.Combine(folder, "빈파일.txt"); await File.WriteAllBytesAsync(empty, []);
        var zero = await files.UploadAsync("notice", null, empty);
        var zeroNotice = await service.CreateNoticeAsync(new CreateNoticeRequest("빈 첨부", "0 bytes", "전 직원", false, []) { AttachmentIds = [zero.AttachmentId] });
        Check((await files.DownloadAsync("notice", zeroNotice.Id.ToString(), zero)).Length == 0, "empty attachment round trip");
        // Use the real internal chat service, including its original UUID cache.
        var type = typeof(HttpNoticeService).Assembly.GetType("RemoteWork.WinForms.Features.Collaboration.HttpChatService")!;
        var chat = Activator.CreateInstance(type, api, api.CurrentUser!)!;
        string room = Environment.GetEnvironmentVariable("FILES_TEST_ROOM")!;
        var chatFile = await files.UploadAsync("chat", room, path);
        var method = type.GetMethod("SendMessageWithFilesAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task)method.Invoke(chat, [room, "C# 채팅 첨부", new[] { chatFile.AttachmentId }, CancellationToken.None])!;
        await task; var message = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var messageId = (Guid)message.GetType().GetProperty("Id")!.GetValue(message)!;
        Check((await files.DownloadAsync("chat", messageId.ToString("D"), chatFile)).SequenceEqual(bytes), "real desktop chat service save + file read");
        await api.LogoutAsync(); await api.LoginAsync("staff1", "Staff-test-2026!");
        var staff = new ResourceAttachmentClient(api.CaptureSession());
        Check((await staff.DownloadAsync("chat", messageId.ToString("D"), chatFile)).SequenceEqual(bytes), "current other participant can download");
        Check((await staff.DownloadAsync("notice", notice.Id.ToString(), file)).SequenceEqual(bytes), "notice audience can download after relogin");
        try { await files.ListAsync("notice", notice.Id.ToString()); throw new Exception("old session accepted"); }
        catch (OperationCanceledException) { checks++; }
        await api.LogoutAsync();
        Console.WriteLine($"PASS {checks} actual C# ApiClient/notice/chat transport + native MySQL checks");
    }
}
