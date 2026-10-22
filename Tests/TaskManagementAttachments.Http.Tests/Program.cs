using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks.Attachments;
using RemoteWork.WinForms.Features.Tasks.Creation;
using RemoteWork.WinForms.Features.Tasks.Management;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private static int checks;
    private static string stage = "startup";
    private static void Check(bool value, string label)
    { if (!value) throw new InvalidOperationException(label); checks++; }
    private static async Task<int> Main(string[] args)
    {
        if (!args.SequenceEqual(new[] { "--http" })) return 2;
        try
        {
            using var config = JsonDocument.Parse(await Console.In.ReadToEndAsync());
            var s = config.RootElement;
            string Get(string key) => s.GetProperty(key).GetString()!;
            using var tap = new ReplyLossHandler();
            using var api = new ApiClient(Get("url"), tap);
            await api.LoginAsync(Get("admin_login_id"), Get("admin_password"));
            var session = api.CaptureSession();
            var preparation = new HttpTaskPreparationService(session);
            var reads = new HttpTaskQueryService(api).CaptureReadSession();
            var files = new HttpTaskAttachmentService(session);
            var creator = new HttpTaskCreationService(session);
            string root = Get("scratch_dir");
            DateOnly today = DateOnly.ParseExact(Get("today"), "yyyy-MM-dd", CultureInfo.InvariantCulture);

            stage = "create shared original file and two tasks";
            byte[] original = Enumerable.Range(0, 4096).Select(i => (byte)(i % 251)).ToArray();
            string initialPath = Path.Combine(root, "management-original.bin");
            await File.WriteAllBytesAsync(initialPath, original);
            using var originalUpload = await new HttpTaskCreationUploadService(session).PrepareFileAsync(initialPath);
            await originalUpload.StartAsync();
            Check(originalUpload.State == TaskCreationUploadState.Completed, "original upload completes through real provider");
            string originalId = originalUpload.AttachmentId!;
            async Task<TaskCreationResult> Create(string title)
            {
                var request = new TaskCreationRequest(title, "Synthetic owner2 attachment integration", today, today.AddDays(3),
                    s.GetProperty("assignee_id").GetInt32(), new[] { s.GetProperty("admin_user_id").GetInt32() },
                    attachmentIds: new[] { originalId });
                return await creator.CreateAsync(request, await creator.GetRequestKeyAsync());
            }
            var created = await Create("management-attachment-primary");
            var other = await Create("management-attachment-retained");
            Check((await files.GetFilesAsync(created.TaskId)).Single().AttachmentId == originalId
                && (await files.GetFilesAsync(other.TaskId)).Single().AttachmentId == originalId,
                "both real tasks initially reference the same completed file");

            var workflow = TaskManagementSessionStore.Get(api, TaskManagementKind.Update, created.TaskId)!;
            var selection = workflow.Attachments ?? throw new InvalidOperationException("missing update selection");
            Check(!selection.Loaded && selection.CompletedIds is null,
                "session store supplies update selection with omitted uninitialized field");
            Check(TaskManagementSessionStore.Get(api, TaskManagementKind.Reassign, created.TaskId)!.Attachments is null
                && TaskManagementSessionStore.Get(api, TaskManagementKind.ChangeManagers, created.TaskId)!.Attachments is null,
                "other management actions have no attachment selection");

            async Task<(TaskDetail Detail, TaskWriteContext Context)> Bind()
            {
                var before = await preparation.GetContextAsync(created.TaskId);
                var detail = await reads.GetTaskAsync(created.TaskId);
                var list = await selection.ReadFilesAsync();
                var after = await preparation.GetContextAsync(created.TaskId);
                Check(before.RowVersion == after.RowVersion && before.AssignmentRevision == after.AssignmentRevision,
                    "actual file read stays inside original version fence");
                Check(selection.BindSnapshot(after, list) && selection.MatchesContext(after), "selection binds actual current files and versions");
                return (detail, after);
            }
            TaskManagementRequest Update((TaskDetail Detail, TaskWriteContext Context) input, string? title = null)
                => TaskManagementRequest.Update(input.Detail, input.Context, session.User.Id,
                    title ?? input.Detail.Summary.Title, input.Detail.Description, input.Detail.Summary.StartDate,
                    input.Detail.Summary.DueDate, selection.CompletedIds);
            var snapshot = await Bind();
            Check(selection.IsReady && selection.CompletedIds is null && selection.Items.Single().IsExisting,
                "unchanged existing selection preserves omission");
            Guid existingKey = selection.Items.Single().Key;
            int deleteBefore = tap.DeleteCount;
            await selection.DiscardAsync(existingKey);
            Check(tap.DeleteCount == deleteBefore && selection.Items.Count == 1, "existing task file cannot issue DELETE");
            Check(selection.RemoveLocal(existingKey) && selection.CompletedIds!.Count == 0 && tap.DeleteCount == deleteBefore,
                "existing exclusion builds explicit empty final list without DELETE");
            Check((await files.GetFilesAsync(created.TaskId)).Single().AttachmentId == originalId,
                "local exclusion does not alter server references before execution");

            stage = "prepare and task-bound upload";
            byte[] replacement = Enumerable.Range(0, 32773).Select(i => new byte[] { 0xfb, 0xef, 0xbe }[i % 3]).ToArray();
            string replacementPath = Path.Combine(root, "management-replacement.bin");
            await File.WriteAllBytesAsync(replacementPath, replacement);
            int calls = tap.Requests.Count;
            await selection.AddFileAsync(replacementPath);
            Check(tap.Requests.Count == calls && !selection.IsReady && selection.CompletedIds is null,
                "new selection prepares local bytes only and cannot save unfinished upload");
            Guid newKey = selection.Items.Single().Key;
            await File.WriteAllTextAsync(replacementPath, "local source changed after capture");
            await selection.UploadAsync(newKey);
            Check(selection.IsReady && selection.Items.Single().State == TaskCreationUploadState.Completed,
                "explicit selected file upload completes");
            string newId = selection.CompletedIds!.Single();
            var start = tap.Requests.Last(r => r.Path.EndsWith("/results/shared/uploads/start", StringComparison.Ordinal));
            using (var body = JsonDocument.Parse(start.Body))
                Check(body.RootElement.GetProperty("task_id").GetString() == created.TaskId,
                    "general-edit upload start carries exactly the current task identity");
            Check(tap.MaxChunkBytes <= 32768, "actual plus-heavy base64 chunk JSON stays within server body limit");
            Check(ReferenceEquals(workflow, TaskManagementSessionStore.Get(api, TaskManagementKind.Update, created.TaskId))
                && selection.CompletedIds!.SequenceEqual(new[] { newId }), "same login and target reopens original selection");

            stage = "unknown successful patch and receipt recovery";
            var request = Update(snapshot);
            tap.LoseNextPatch = true;
            await workflow.SubmitAsync(request);
            Check(workflow.State == TaskManagementState.NeedsResultCheck && tap.Losses == 1,
                "actual committed PATCH reply loss enters result-check state");
            Check(ReferenceEquals(request, workflow.PendingRequest) && workflow.PendingRequest!.AttachmentIds!.SequenceEqual(new[] { newId }),
                "uncertain request preserves original object and final ID array");
            var patch = tap.Requests.Last(r => r.Method == "PATCH");
            using (var body = JsonDocument.Parse(patch.Body))
                Check(body.RootElement.GetProperty("attachment_ids")[0].GetString() == newId
                    && body.RootElement.GetProperty("expected_row_version").GetString() == "1",
                    "actual PATCH contains frozen final IDs and original version");
            calls = tap.Requests.Count;
            Check(!selection.CanChange && !selection.RemoveLocal(newKey), "uncertain save blocks local selection changes");
            await selection.DiscardAsync(newKey); await selection.UploadAsync(newKey); await selection.AddFileAsync(initialPath);
            Check(tap.Requests.Count == calls && tap.DeleteCount == 0,
                "uncertain save blocks direct delete upload and file preparation paths");
            Check(ReferenceEquals(workflow, TaskManagementSessionStore.FindPending(api)), "pending manager result recovers same owner workflow");
            Check((await files.GetFilesAsync(created.TaskId)).Single().AttachmentId == newId,
                "server reference changed even while client outcome is unknown");
            int patchCount = tap.Requests.Count(r => r.Method == "PATCH");
            await workflow.CheckResultAsync();
            Check(workflow.State == TaskManagementState.Succeeded && workflow.Result!.RowVersion == "2"
                && workflow.Result.AssignmentRevision == 1, "receipt confirms one version increment without assignment change");
            Check(tap.Requests.Count(r => r.Method == "PATCH") == patchCount && selection.Items.Count == 0 && tap.DeleteCount == 0,
                "result lookup clears local selection without replaying PATCH or DELETE");
            var current = (await files.GetFilesAsync(created.TaskId)).Single();
            var download = await files.DownloadAsync(created.TaskId, current);
            string downloaded = Path.Combine(root, "management-downloaded.bin");
            await download.SaveNewAsync(downloaded);
            Check((await File.ReadAllBytesAsync(downloaded)).SequenceEqual(replacement)
                && current.Sha256 == Convert.ToHexString(SHA256.HashData(replacement)).ToLowerInvariant(),
                "actual current download retains captured bytes after source modification");
            Check((await files.GetFilesAsync(other.TaskId)).Single().AttachmentId == originalId,
                "replaced original file remains attached to the other task");
            Check(workflow.AcknowledgeSuccess(), "explicit success acknowledgement permits a new checked edit");

            stage = "omission keeps and explicit empty unlinks";
            snapshot = await Bind();
            Check(selection.CompletedIds is null, "fresh unchanged snapshot omits IDs after success");
            await workflow.SubmitAsync(Update(snapshot, "management-attachment-title-only"));
            Check(workflow.State == TaskManagementState.Succeeded && workflow.Result!.RowVersion == "3", "title-only edit succeeds once");
            using (var body = JsonDocument.Parse(tap.Requests.Last(r => r.Method == "PATCH").Body))
                Check(!body.RootElement.TryGetProperty("attachment_ids", out _), "actual title-only PATCH omits attachment_ids");
            Check((await files.GetFilesAsync(created.TaskId)).Single().AttachmentId == newId, "omitted final list preserves existing attachment");
            Check(workflow.AcknowledgeSuccess(), "second explicit acknowledgement succeeds");
            snapshot = await Bind();
            Check(selection.RemoveLocal(selection.Items.Single().Key), "explicit existing reference exclusion succeeds");
            Check(selection.CompletedIds!.Count == 0, "last exclusion preserves explicit empty array");
            await workflow.SubmitAsync(Update(snapshot));
            Check(workflow.State == TaskManagementState.Succeeded && workflow.Result!.RowVersion == "4"
                && workflow.Result.AssignmentRevision == 1, "empty final list increments only task version once");
            using (var body = JsonDocument.Parse(tap.Requests.Last(r => r.Method == "PATCH").Body))
                Check(body.RootElement.GetProperty("attachment_ids").GetArrayLength() == 0, "actual explicit empty PATCH is not omission");
            Check((await files.GetFilesAsync(created.TaskId)).Count == 0 && tap.DeleteCount == 0, "target reference cleared without client DELETE");
            Check((await files.GetFilesAsync(other.TaskId)).Single().AttachmentId == originalId,
                "other task retains its original file after target all-unlink");
            Check(workflow.AcknowledgeSuccess(), "third success acknowledged");
            snapshot = await Bind();
            await workflow.SubmitAsync(Update(snapshot));
            Check(workflow.State == TaskManagementState.Editing && workflow.Failure == TaskManagementFailure.NoChange,
                "identical final fields are a definitive no-change rejection");
            Check((await preparation.GetContextAsync(created.TaskId)).RowVersion == "4", "no-change request creates no new task version");
            Check(tap.DeleteCount == 0 && tap.Requests.Count(r => r.Method == "PATCH") == 4,
                "three successful edits plus one no-change request and no hidden DELETE");

            Console.WriteLine($"PASS TaskManagementAttachments HTTP: {checks} checks.");
            return 0;
        }
        catch
        {
            Console.Error.WriteLine($"FAIL TaskManagementAttachments HTTP after {checks} checks at {stage}; details omitted.");
            return 1;
        }
    }

    private sealed record CapturedRequest(string Method, string Path, string Body);
    private sealed class ReplyLossHandler : DelegatingHandler
    {
        public readonly List<CapturedRequest> Requests = new();
        public bool LoseNextPatch;
        public int Losses, DeleteCount, MaxChunkBytes;
        public ReplyLossHandler() : base(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false, UseProxy = false }) { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.AbsolutePath;
            string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add(new(request.Method.Method, path, body));
            if (request.Method == HttpMethod.Delete) DeleteCount++;
            if (path.Contains("/results/shared/uploads/", StringComparison.Ordinal) && path.EndsWith("/chunks", StringComparison.Ordinal))
                MaxChunkBytes = Math.Max(MaxChunkBytes, System.Text.Encoding.UTF8.GetByteCount(body));
            var response = await base.SendAsync(request, ct);
            if (LoseNextPatch && request.Method == HttpMethod.Patch && response.IsSuccessStatusCode)
            {
                LoseNextPatch = false; Losses++;
                await response.Content.LoadIntoBufferAsync(); response.Dispose();
                throw new HttpRequestException("Explicit test-only lost reply after real successful PATCH");
            }
            return response;
        }
    }
}
