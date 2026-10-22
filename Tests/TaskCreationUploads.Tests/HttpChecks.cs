using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks.Attachments;
using RemoteWork.WinForms.Features.Tasks.Creation;
using RemoteWork.WinForms.Infrastructure;

namespace TaskCreationUploads.Tests;

// Private stdin settings are supplied only by the owned MySQL runner. No tokens,
// passwords, raw envelopes, local file names or server addresses are printed.
internal static class UploadHttpChecks
{
    public static async Task<int> RunAsync(string settings)
    {
        int checks = 0;
        void Check(bool value, string label)
        { if (!value) throw new InvalidOperationException(label); checks++; }
        try
        {
            using var json = JsonDocument.Parse(settings);
            var s = json.RootElement;
            string Get(string key) => s.GetProperty(key).GetString()!;
            string root = Get("scratch_dir");
            using var handler = new LosingReplyHandler();
            using var admin = new ApiClient(Get("url"), handler);
            await admin.LoginAsync(Get("admin_login_id"), Get("admin_password"));
            var session = admin.CaptureSession();
            var uploads = new HttpTaskCreationUploadService(session);
            byte[] bytes = Enumerable.Range(0, 32773).Select(i => new byte[] { 0xfb, 0xef, 0xbe }[i % 3]).ToArray();
            string path = Path.Combine(root, "owned-upload.bin");
            await File.WriteAllBytesAsync(path, bytes);
            int before = handler.UploadRequests;
            using var file = await uploads.PrepareFileAsync(path);
            Check(handler.UploadRequests == before, "prepare sends no HTTP");
            Check(file.Sha256 == Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), "snapshot hash");
            await File.WriteAllTextAsync(path, "source changed after preparation");
            handler.LoseChunk = true;
            handler.LoseComplete = true;
            await file.StartAsync();
            Check(file.State == TaskCreationUploadState.NeedsRetry && file.AttachmentId is not null, "committed chunk reply loss");
            Check(file.ConfirmedBytes == 0, "lost chunk does not advance local progress");
            string id = file.AttachmentId!;
            await file.RetryAsync();
            Check(file.State == TaskCreationUploadState.NeedsRetry && file.ConfirmedBytes == bytes.Length, "committed complete reply loss");
            Check(file.AttachmentId == id, "same upload identity survives both losses");
            Check(handler.StartCount == 1 && handler.ChunkCount == 4 && handler.CompleteCount == 1, "one start and repeated first chunk only");
            await file.RetryAsync();
            Check(file.State == TaskCreationUploadState.Completed && file.CompletedFile?.AttachmentId == id, "ready completion replay");
            Check(handler.StartCount == 1 && handler.ChunkCount == 4 && handler.CompleteCount == 2, "completion retry skips chunk writes");
            Check(handler.Losses == 2 && handler.MaxChunkBodyBytes <= 32768, "actual escaped-base64 body limit");
            before = handler.UploadRequests;
            await file.StartAsync(); await file.RetryAsync();
            Check(handler.UploadRequests == before, "completed attempt cannot duplicate writes");

            string emptyPath = Path.Combine(root, "owned-empty.bin");
            await File.WriteAllBytesAsync(emptyPath, Array.Empty<byte>());
            using var empty = await uploads.PrepareFileAsync(emptyPath);
            await empty.StartAsync();
            Check(empty.State == TaskCreationUploadState.Completed && empty.CompletedFile?.Size == 0, "zero byte complete");
            Check(handler.ChunkCount == 4, "zero byte file has no chunk request");

            using var toDelete = await uploads.PrepareFileAsync(path);
            await toDelete.StartAsync();
            Check(toDelete.State == TaskCreationUploadState.Completed, "temporary file ready for explicit deletion");
            string deletedId = toDelete.AttachmentId!;
            handler.LoseDelete = true;
            await toDelete.DiscardAsync();
            Check(toDelete.State == TaskCreationUploadState.DiscardUncertain && toDelete.CompletedFile is null,
                "lost committed delete invalidates selection without inventing success");
            Check(toDelete.AttachmentId == deletedId && handler.DeleteCount == 1, "lost delete retains original target and does not retry");
            await toDelete.DiscardAsync();
            Check(toDelete.State == TaskCreationUploadState.Discarded && handler.DeleteCount == 2,
                "explicit missing-file replay confirms deletion");
            before = handler.UploadRequests;
            await toDelete.DiscardAsync(); await toDelete.RetryAsync();
            Check(handler.UploadRequests == before, "discarded file cannot upload or delete again");

            var creation = new HttpTaskCreationService(session);
            var key = await creation.GetRequestKeyAsync();
            DateOnly today = DateOnly.Parse(Get("today"));
            var request = new TaskCreationRequest("Owned upload TCP fixture", "Synthetic integration fixture", today, today,
                s.GetProperty("assignee_id").GetInt32(), new[] { s.GetProperty("admin_user_id").GetInt32() },
                attachmentIds: new[] { id, empty.AttachmentId! });
            var created = await creation.CreateAsync(request, key);
            Check(created.RowVersion == "1" && created.AssignmentRevision == 1, "initial versions");
            var replay = await creation.CreateAsync(request, key);
            Check(SameResult(replay, created), "same key original creation receipt");
            var lookup = await creation.GetResultAsync(key);
            Check(lookup.Recorded && SameResult(lookup.Result, created), "retained creation result");

            using var staff = new ApiClient(Get("url"));
            await staff.LoginAsync(Get("staff_login_id"), Get("staff_password"));
            var downloads = new HttpTaskAttachmentService(staff.CaptureSession());
            var listed = await downloads.GetFilesAsync(created.TaskId);
            Check(listed.Count == 2 && listed.Contains(file.CompletedFile!) && listed.Contains(empty.CompletedFile!), "staff current task files");
            foreach (var item in listed)
            {
                var downloaded = await downloads.DownloadAsync(created.TaskId, item);
                string save = Path.Combine(root, item.Size == 0 ? "empty-downloaded.bin" : "downloaded.bin");
                await downloaded.SaveNewAsync(save);
                Check((await File.ReadAllBytesAsync(save)).SequenceEqual(item.Size == 0 ? Array.Empty<byte>() : bytes), "physical downloaded original bytes");
            }
            try
            {
                using var forbidden = await new HttpTaskCreationUploadService(staff.CaptureSession()).PrepareFileAsync(path);
                throw new InvalidOperationException("staff preparation allowed");
            }
            catch (ApiException ex) when (ex.StatusCode == 403) { checks++; }
            // Server authorization is independently exercised; the UI guard alone is insufficient.
            try
            {
                await staff.CaptureSession().PostAsync<JsonElement>("results/shared/uploads/start",
                    new { name = "forbidden.bin", size = 0, sha256 = empty.Sha256 });
                throw new InvalidOperationException("staff unbound upload allowed");
            }
            catch (ApiException ex) when (ex.StatusCode == 403) { checks++; }
            using var outsider = new ApiClient(Get("url"));
            await outsider.LoginAsync(Get("outsider_login_id"), Get("outsider_password"));
            try
            {
                await new HttpTaskAttachmentService(outsider.CaptureSession()).GetFilesAsync(created.TaskId);
                throw new InvalidOperationException("unassigned admin read allowed");
            }
            catch (ApiException ex) when (ex.StatusCode == 503 && ex.Code == "RESULTS_CONTEXT_MODE_REQUIRED") { checks++; }
            // This fixture deliberately has no reviewer binding. An unrelated admin
            // is denied with the provider's dependency error; do not invent reviewer facts.
            using var otherStaff = new ApiClient(Get("url"));
            await otherStaff.LoginAsync(Get("other_staff_login_id"), Get("other_staff_password"));
            try
            {
                await new HttpTaskAttachmentService(otherStaff.CaptureSession()).GetFilesAsync(created.TaskId);
                throw new InvalidOperationException("unassigned staff read allowed");
            }
            catch (ApiException ex) when (ex.StatusCode == 403) { checks++; }

            await file.DiscardAsync();
            Check(file.State == TaskCreationUploadState.Disconnected && file.StatusCode == 403,
                "server denies deleting a task-linked file even for the uploader");
            Check((await downloads.GetFilesAsync(created.TaskId)).Count == 2, "denied deletion preserves registered task references");

            using var uncertain = await uploads.PrepareFileAsync(path);
            handler.LoseStart = true;
            await uncertain.StartAsync();
            Check(uncertain.State == TaskCreationUploadState.StartUncertain && uncertain.AttachmentId is null, "lost committed start remains uncertain");
            before = handler.UploadRequests;
            await uncertain.StartAsync(); await uncertain.RetryAsync(); uncertain.Dispose();
            Check(handler.UploadRequests == before && handler.DeleteCount == 3, "unknown upload is not restarted or discarded");
            Check(handler.StartCount == 4 && handler.ChunkCount == 5 && handler.CompleteCount == 4, "exact real upload route counts");
            Console.WriteLine($"PASS TaskCreationUploads HTTP: {checks} checks.");
            return 0;
        }
        catch (Exception)
        {
            Console.Error.WriteLine($"FAIL TaskCreationUploads HTTP after {checks} checks; details omitted.");
            return 1;
        }
    }

    private static bool SameResult(TaskCreationResult? a, TaskCreationResult b) =>
        a is not null && a.TaskId == b.TaskId && a.RowVersion == b.RowVersion
        && a.AssignmentRevision == b.AssignmentRevision && a.Status == b.Status && a.UpdatedAt == b.UpdatedAt;

    // Faults occur only AFTER the real server has returned a successful response.
    // Requests, database mutations and files are real; only selected replies are lost.
    private sealed class LosingReplyHandler : DelegatingHandler
    {
        public bool LoseStart, LoseChunk, LoseComplete, LoseDelete;
        public int StartCount, ChunkCount, CompleteCount, DeleteCount, Losses, MaxChunkBodyBytes;
        public int UploadRequests => StartCount + ChunkCount + CompleteCount + DeleteCount;
        public LosingReplyHandler() : base(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false, UseProxy = false }) { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.AbsolutePath;
            bool upload = path.Contains("/results/shared/uploads/", StringComparison.Ordinal);
            bool start = upload && path.EndsWith("/start", StringComparison.Ordinal);
            bool chunk = upload && path.EndsWith("/chunks", StringComparison.Ordinal);
            bool complete = upload && path.EndsWith("/complete", StringComparison.Ordinal);
            if (start) StartCount++;
            if (complete) CompleteCount++;
            bool delete = upload && request.Method == HttpMethod.Delete;
            if (delete) DeleteCount++;
            if (chunk)
            {
                ChunkCount++;
                MaxChunkBodyBytes = Math.Max(MaxChunkBodyBytes, (await request.Content!.ReadAsByteArrayAsync(ct)).Length);
            }
            var response = await base.SendAsync(request, ct);
            if (response.IsSuccessStatusCode && ((start && LoseStart) || (chunk && LoseChunk) || (complete && LoseComplete) || (delete && LoseDelete)))
            {
                await response.Content.LoadIntoBufferAsync(); response.Dispose(); Losses++;
                if (start) LoseStart = false;
                if (chunk) LoseChunk = false;
                if (complete) LoseComplete = false;
                if (delete) LoseDelete = false;
                throw new HttpRequestException("Explicit test-only loss after actual successful HTTP response");
            }
            return response;
        }
    }
}
