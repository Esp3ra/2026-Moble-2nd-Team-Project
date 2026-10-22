using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks.Attachments;
using RemoteWork.WinForms.Features.Tasks.Management;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private const string ExistingId = "00112233-4455-6677-8899-aabbccddeeff";
    private const string SecondId = "11223344-5566-7788-99aa-bbccddeeff00";
    private const string NewId = "22334455-6677-8899-aabb-ccddeeff0011";
    private static readonly DateOnly Day = new(2026, 10, 22);
    private static readonly DateTimeOffset Utc = new(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
    private static int checks, scenarios;
    private static TaskDetail Detail(string id = "9") => new(new(id, "원래 업무", "직원", TaskDisplayStatus.NotStarted, Day, Day.AddDays(3)), "원래 설명");
    private static TaskWriteContext Context(string version = "1", int revision = 1, string id = "9") => new(id, 2, new[] { 1, 3 }, version, revision, TaskDisplayStatus.NotStarted, Utc);
    private static TaskManagementRequest Request(IReadOnlyList<string>? ids = null, TaskWriteContext? context = null) =>
        TaskManagementRequest.Update(Detail(context?.TaskId ?? "9"), context ?? Context(), 1, "수정 업무", "수정 설명", Day, Day.AddDays(5), ids);
    private static TaskAttachment FileInfo(string id = ExistingId) => new(id, "기존 자료.txt", 3, Hash([1, 2, 3]));
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static object Result() => new { task_id = "9", row_version = "2", assignment_revision = 1, task_status = "not_started", updated_at = "2026-10-22T00:00:00Z" };
    private static object Meta(string id, string name, byte[] bytes) => new { attachment_id = id, name, size = bytes.LongLength, sha256 = Hash(bytes) };

    private static async Task<int> Main()
    {
        try
        {
            await Run("omission, empty list and existing reference exclusion", Exclusion);
            await Run("task/version fences and explicit local reset", ContextFences);
            await Run("snapshot validation and maximum combined selection", Validation);
            await Run("task-bound upload and ordered immutable request", UploadAndBinding);
            await Run("one session/task workflow survives navigation", SessionStore);
            await Run("unknown write freezes selection and retries exact body", WriteRecovery);
            await Run("definite attachment failure retains local intent", DefiniteFailure);
            await Run("forbidden write preserves receipt but hides metadata", WriteForbidden);
            await Run("read failure invalidates metadata and same-login reopening", ReadFailure);
            await Run("cancelled old read ignores a late forbidden response", CancelledReadForbidden);
            await Run("read and upload operations prevent interleaved writes", ConcurrentOperations);
            await Run("late upload cannot restore a replaced login", LateSession);
            await Run("only an explicit new upload discard sends DELETE", ExplicitDiscard);
            Console.WriteLine($"PASS: {checks} checks in {scenarios} management selection/workflow scenarios; controlled HTTP only.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine($"FAIL after {checks} checks: {error}"); return 1; }
    }
    private static async Task Run(string name, Func<Task> test)
    { await test().WaitAsync(TimeSpan.FromSeconds(20)); scenarios++; Console.WriteLine("PASS " + name); }
    private static void Check(bool condition, string name)
    { if (!condition) throw new InvalidOperationException(name); checks++; }
    private static async Task Throws<T>(Func<Task> action, string name) where T : Exception
    { try { await action(); } catch (T) { checks++; return; } throw new InvalidOperationException(name); }

    private static async Task Exclusion()
    {
        using var f = await Fixture.Create();
        Check(!f.Selection.Loaded && f.Selection.IsReady && f.Selection.CompletedIds is null, "unloaded means omitted, not empty");
        f.Bind(); var item = f.Selection.Items.Single();
        Check(item.IsExisting && item.CanRemove && !item.CanDiscard && !item.CanUpload && !item.CanRetry, "existing reference never offers transport actions");
        Check(f.Selection.CompletedIds is null && !f.Selection.HasChanges, "untouched snapshot omits IDs");
        await f.Selection.DiscardAsync(item.Key);
        Check(f.Handler.Requests.Count == 0 && f.Selection.Items.Count == 1, "discard of existing ID sends nothing");
        Check(f.Selection.RemoveLocal(item.Key) && f.Selection.CompletedIds is { Count: 0 }, "excluding last reference produces explicit []");
        Check(f.Selection.HasChanges && f.Selection.IsReady && f.Handler.Requests.Count == 0, "reference exclusion remains local");
        Check(f.Selection.BindSnapshot(Context(), [FileInfo()]) && f.Selection.Items.Count == 0, "same-version refresh preserves explicit empty list");
        f.Key(); f.Handler.Success(Result()); await f.Workflow.SubmitAsync(Request(f.Selection.CompletedIds));
        Check(f.Workflow.State == TaskManagementState.Succeeded, "explicit empty update succeeds");
        using var json = JsonDocument.Parse(f.Handler.Requests.Last().Body);
        Check(json.RootElement.GetProperty("attachment_ids").GetArrayLength() == 0, "PATCH transmits []");
        Check(!f.Selection.Loaded && f.Selection.Items.Count == 0 && f.Handler.Deletes == 0, "success clears local selection without DELETE");
        using var untouched = await Fixture.Create(); untouched.Bind(); untouched.Key(); untouched.Handler.Success(Result());
        await untouched.Workflow.SubmitAsync(Request());
        using var omitted = JsonDocument.Parse(untouched.Handler.Requests.Last().Body);
        Check(!omitted.RootElement.TryGetProperty("attachment_ids", out _), "untouched list remains absent from PATCH");
    }

    private static async Task ContextFences()
    {
        using var f = await Fixture.Create(); f.Bind(); f.Selection.RemoveLocal(f.Selection.Items.Single().Key);
        Check(!f.Selection.MatchesContext(Context("2")) && !f.Selection.MayReplaceSnapshot(Context("2")), "changed version cannot replace dirty selection");
        Check(!f.Selection.BindSnapshot(Context("2"), [FileInfo(SecondId)]) && f.Selection.CompletedIds is { Count: 0 }, "different row preserves explicit choice");
        Check(!f.Selection.BindSnapshot(Context(revision: 2), [FileInfo(SecondId)]), "assignment change also requires explicit reset");
        await Throws<ArgumentException>(() => f.Workflow.SubmitAsync(Request([], Context("2"))), "stale selection submitted against new row");
        await Throws<ArgumentException>(() => f.Workflow.SubmitAsync(Request([], Context(revision: 2))), "stale selection submitted against new assignment");
        await Throws<ArgumentException>(() => f.Workflow.SubmitAsync(Request([], Context(id: "10"))), "selection submitted for another task");
        Check(f.Handler.Requests.Count == 0, "binding errors happen before key issuance");
        Check(f.Selection.ResetWithoutDelete() && f.Selection.BindSnapshot(Context("2"), [FileInfo(SecondId)]), "explicit reset admits refreshed context");
        Check(f.Selection.MatchesContext(Context("2")) && f.Selection.CompletedIds is null && f.Handler.Deletes == 0, "reset is local and new baseline is untouched");
        Check(!f.Selection.BindSnapshot(Context(id: "10"), []) && !f.Selection.BindSnapshot(Context() with { Status = TaskDisplayStatus.InProgress }, []), "wrong task or mutable state rejected");
        Check(!f.Selection.BindSnapshot(Context() with { ManagerUserIds = new[] { 3 } }, []), "non-manager snapshot rejected");
    }

    private static async Task Validation()
    {
        using var f = await Fixture.Create();
        await Throws<ArgumentException>(() => Task.FromResult(f.Selection.BindSnapshot(Context(), [FileInfo(), FileInfo()])), "duplicate current attachment accepted");
        await Throws<ArgumentException>(() => Task.FromResult(f.Selection.BindSnapshot(Context(), [FileInfo() with { AttachmentId = "BAD" }])), "noncanonical ID accepted");
        var ten = Enumerable.Range(1, 10).Select(x => FileInfo(new Guid(x, 0, 0, new byte[8]).ToString("D"))).ToArray();
        await Throws<ArgumentException>(() => Task.FromResult(f.Selection.BindSnapshot(Context(), ten.Append(FileInfo()).ToArray())), "eleven current attachments accepted");
        Check(f.Selection.BindSnapshot(Context(), ten), "ten current files accepted");
        await f.Selection.AddFileAsync(f.File("eleventh.bin", [1]));
        Check(f.Selection.Items.Count == 10 && f.Selection.CompletedIds is null && f.Handler.Requests.Count == 0, "combined limit blocks eleventh file before HTTP");
        f.Selection.RemoveLocal(f.Selection.Items[0].Key); await f.Selection.AddFileAsync(f.File("tenth.bin", [1]));
        Check(f.Selection.Items.Count == 10 && !f.Selection.IsReady, "unuploaded tenth file blocks readiness");
        await Throws<ArgumentException>(() => f.Workflow.SubmitAsync(Request()), "unuploaded file did not block omitted-list submission");
        Check(f.Handler.Requests.Count == 0, "pending file never issued a W1 key");
    }

    private static async Task UploadAndBinding()
    {
        using var f = await Fixture.Create(); f.Bind([FileInfo(), FileInfo(SecondId)]);
        byte[] bytes = Enumerable.Range(0, 16384 + 3).Select(i => new byte[] { 0xfb, 0xef, 0xbe }[i % 3]).ToArray();
        string path = f.File("new.bin", bytes); await f.Selection.AddFileAsync(path); System.IO.File.WriteAllBytes(path, [0]);
        Guid key = f.Selection.Items.Last().Key;
        Check(!f.Selection.IsReady && f.Handler.Requests.Count == 0, "file selection is local and incomplete");
        f.HappyUpload("new.bin", bytes); await f.Selection.UploadAsync(key);
        Check(f.Selection.IsReady && f.Selection.CompletedIds!.SequenceEqual(new[] { ExistingId, SecondId, NewId }), "ordered final list includes original files then completed upload");
        using (var start = JsonDocument.Parse(f.Handler.Requests[0].Body))
            Check(start.RootElement.GetProperty("task_id").GetString() == "9" && start.RootElement.GetProperty("size").GetInt64() == bytes.Length, "start is task-bound and immutable bytes retained");
        byte[] sent = f.Handler.Requests.Where(r => r.Path.EndsWith("/chunks")).SelectMany(r => Convert.FromBase64String(JsonDocument.Parse(r.Body).RootElement.GetProperty("data").GetString()!)).ToArray();
        Check(sent.SequenceEqual(bytes), "chunks send original bytes despite local file change");
        int count = f.Handler.Requests.Count;
        await Throws<ArgumentException>(() => f.Workflow.SubmitAsync(Request(new[] { NewId, SecondId, ExistingId })), "changed final order passed gate");
        await Throws<ArgumentException>(() => f.Workflow.SubmitAsync(Request()), "explicit final list silently omitted");
        Check(f.Handler.Requests.Count == count, "list mismatch rejected before key");
        string[] ids = f.Selection.CompletedIds!.ToArray(); var request = Request(ids); ids[0] = NewId;
        f.Key(); f.Handler.Success(Result()); await f.Workflow.SubmitAsync(request);
        Check(f.Workflow.State == TaskManagementState.Succeeded, "immutable prepared request accepted");
        using var patch = JsonDocument.Parse(f.Handler.Requests.Last().Body);
        Check(patch.RootElement.GetProperty("attachment_ids").EnumerateArray().Select(x => x.GetString()).SequenceEqual(new[] { ExistingId, SecondId, NewId }), "save retains exact ordered ID snapshot");
        Check(f.Handler.Deletes == 0, "save and success do not delete uploads");
    }

    private static async Task SessionStore()
    {
        using var f = await Fixture.Create(); f.Bind(); f.Selection.RemoveLocal(f.Selection.Items.Single().Key);
        Check(ReferenceEquals(f.Workflow, TaskManagementSessionStore.Get(f.Api, TaskManagementKind.Update, "9")), "same task and login retain workflow");
        Check(ReferenceEquals(f.Selection, TaskManagementSessionStore.Get(f.Api, TaskManagementKind.Update, "9")!.Attachments), "same selection survives navigation");
        Check(TaskManagementSessionStore.Get(f.Api, TaskManagementKind.Update, "10")!.Attachments is { Loaded: false }, "different task selection isolated");
        Check(TaskManagementSessionStore.Get(f.Api, TaskManagementKind.Reassign, "9")!.Attachments is null
            && TaskManagementSessionStore.Get(f.Api, TaskManagementKind.ChangeManagers, "9")!.Attachments is null, "other management actions have no attachment selection");
        using var bare = new TaskManagementWorkflow(new HttpTaskManagementService(f.Api.CaptureSession()));
        await Throws<ArgumentException>(() => bare.SubmitAsync(Request([])), "unbound workflow accepted explicit attachment mutation");
        f.Api.ClearIdentity();
        Check(f.Workflow.State == TaskManagementState.Disconnected && f.Selection.Items.Count == 0, "logout clears original selection");
        await f.Login(); var next = TaskManagementSessionStore.Get(f.Api, TaskManagementKind.Update, "9")!;
        Check(!ReferenceEquals(next, f.Workflow) && next.Attachments is { Loaded: false }, "new login gets a fresh selection");
        Check(f.Handler.Deletes == 0, "session replacement sends no DELETE");
    }

    private static async Task WriteRecovery()
    {
        using var f = await Fixture.Create(); f.Bind(); f.Selection.RemoveLocal(f.Selection.Items.Single().Key);
        var request = Request(f.Selection.CompletedIds); f.Key(); var pending = new ResponseGate(); f.Handler.Next(pending.Send);
        Task write = f.Workflow.SubmitAsync(request); await pending.Entered.Task;
        await AssertFrozen(f, "sending"); pending.Drop(); await write;
        Check(f.Workflow.State == TaskManagementState.NeedsResultCheck && ReferenceEquals(request, f.Workflow.PendingRequest), "lost response retains original request");
        await AssertFrozen(f, "unknown result");
        f.Handler.Success(new { state = "not_recorded" }); await f.Workflow.CheckResultAsync();
        Check(f.Workflow.State == TaskManagementState.RetryAllowed, "explicit not_recorded enables same-request retry");
        await AssertFrozen(f, "retry allowed");
        f.Handler.Success(Result()); await f.Workflow.RetrySameRequestAsync();
        var patches = f.Handler.Requests.Where(x => x.Method == "PATCH").ToArray();
        Check(patches.Length == 2 && patches[0].Body == patches[1].Body && patches[0].Key == patches[1].Key, "retry resends same body and private key");
        Check(f.Handler.Requests.Count(x => x.Path.EndsWith("/keys")) == 1, "unknown retry never obtains a new key");
        Check(f.Workflow.State == TaskManagementState.Succeeded && !f.Selection.Loaded && f.Handler.Deletes == 0, "verified success clears local references only");
    }
    private static async Task AssertFrozen(Fixture f, string phase)
    {
        int count = f.Handler.Requests.Count;
        Check(!f.Selection.CanChange && !f.Selection.BindSnapshot(Context(), [FileInfo()]) && !f.Selection.ResetWithoutDelete(), phase + " prevents reset/rebind");
        await f.Selection.AddFileAsync(f.File("blocked.bin", [1]));
        await f.Selection.UploadAsync(Guid.NewGuid()); await f.Selection.RetryAsync(Guid.NewGuid()); await f.Selection.DiscardAsync(Guid.NewGuid());
        Check(!f.Selection.RemoveLocal(Guid.NewGuid()) && f.Handler.Requests.Count == count && f.Selection.CompletedIds is { Count: 0 }, phase + " keeps exact empty final list");
    }

    private static async Task DefiniteFailure()
    {
        foreach ((int status, string code) in new[] { (409, "ATTACHMENT_UNAVAILABLE"), (404, "ATTACHMENT_NOT_FOUND") })
        {
            using var f = await Fixture.Create(); f.Bind([FileInfo(), FileInfo(SecondId)]); f.Selection.RemoveLocal(f.Selection.Items[0].Key);
            f.Key(); f.Handler.Error(status, code); await f.Workflow.SubmitAsync(Request(f.Selection.CompletedIds));
            Check(f.Workflow.State == TaskManagementState.Editing && f.Workflow.Failure == TaskManagementFailure.AttachmentUnavailable, "exact unavailable response returns to editing");
            Check(f.Selection.Loaded && f.Selection.HasChanges && f.Selection.CompletedIds!.SequenceEqual(new[] { SecondId }), "definite rejection preserves original list intent");
            Check(!f.Selection.BindSnapshot(Context("2"), []) && f.Handler.Deletes == 0, "failure never resets version or discards file automatically");
        }
    }

    private static async Task WriteForbidden()
    {
        using var f = await Fixture.Create(); f.Bind(); f.Selection.RemoveLocal(f.Selection.Items.Single().Key);
        var request = Request(f.Selection.CompletedIds); f.Key(); f.Handler.Error(403, "FORBIDDEN"); await f.Workflow.SubmitAsync(request);
        Check(f.Workflow.State == TaskManagementState.Blocked && ReferenceEquals(request, f.Workflow.PendingRequest), "forbidden write retains potentially committed receipt");
        Check(!f.Selection.Loaded && !f.Selection.CanChange && f.Selection.Items.Count == 0, "forbidden hides old file metadata");
        int count = f.Handler.Requests.Count;
        Check(ReferenceEquals(f.Workflow, TaskManagementSessionStore.Get(f.Api, TaskManagementKind.Update, "9")) && !f.Selection.BindSnapshot(Context(), [FileInfo()]), "same login cannot bypass forbidden by reopening");
        await Throws<OperationCanceledException>(() => f.Selection.ReadFilesAsync(), "blocked receipt allowed a new read");
        Check(f.Handler.Requests.Count == count && f.Handler.Deletes == 0, "blocked action sends nothing");
    }

    private static async Task ReadFailure()
    {
        foreach (int status in new[] { 401, 403 })
        {
            using var f = await Fixture.Create(); f.Bind(); f.Handler.Error(status, "FORBIDDEN");
            await Throws<ApiException>(() => f.Selection.ReadFilesAsync(), "read failure not surfaced");
            Check(!f.Selection.IsCurrent && f.Selection.Items.Count == 0 && !f.Selection.Loaded, "read authentication/permission error clears metadata");
            Check(f.Workflow.State == TaskManagementState.Disconnected && !f.Selection.BindSnapshot(Context(), [FileInfo()]), "read failure invalidates owner write ability");
            if (status == 403) Check(ReferenceEquals(f.Workflow, TaskManagementSessionStore.Get(f.Api, TaskManagementKind.Update, "9")), "same login retains forbidden owner");
            Check(f.Handler.Deletes == 0, "read failure has no cleanup DELETE");
        }
    }

    private static async Task CancelledReadForbidden()
    {
        using var f = await Fixture.Create(); f.Bind([FileInfo(), FileInfo(SecondId)]);
        f.Selection.RemoveLocal(f.Selection.Items.First().Key);
        using var oldForm = new CancellationTokenSource();
        var late = new ResponseGate(); f.Handler.Next(late.Send);
        var session = f.Api.CaptureSession();
        Task<IReadOnlyList<TaskAttachment>> read = f.Selection.ReadFilesAsync(oldForm.Token);
        await late.Entered.Task;
        Check(f.Selection.IsBusy, "old form read reserves the workflow while awaiting HTTP");
        oldForm.Cancel();
        var reopened = TaskManagementSessionStore.Get(f.Api, TaskManagementKind.Update, "9")!;
        Check(ReferenceEquals(f.Workflow, reopened), "new form borrows the same login-owned workflow");
        Check(f.Selection.Loaded && f.Selection.HasChanges && f.Selection.CompletedIds!.SequenceEqual(new[] { SecondId }),
            "closing old form retains current task selection until response arrives");

        // This transport deliberately ignores cancellation and returns a buffered
        // 403. Cached content can still be read after cancellation, so the API
        // error must not invalidate a workflow borrowed by a later form.
        using var forbidden = Handler.ErrorResponse(403, "FORBIDDEN");
        await forbidden.Content.LoadIntoBufferAsync();
        late.Release(forbidden);
        await Throws<OperationCanceledException>(() => read, "cancelled old read surfaced forbidden instead of cancellation");
        Check(session.IsCurrent && f.Selection.IsCurrent && reopened.State == TaskManagementState.Editing,
            "late forbidden from cancelled caller does not invalidate current login or workflow");
        Check(!f.Selection.IsBusy && f.Selection.CanChange && f.Selection.Loaded && f.Selection.HasChanges,
            "late response releases read lock without clearing or disabling selection");
        Check(f.Selection.Items.Single().Name == FileInfo(SecondId).Name
            && f.Selection.CompletedIds!.SequenceEqual(new[] { SecondId }), "late forbidden preserves exact chosen final list");
        Check(f.Handler.Requests.Count == 1 && f.Handler.Deletes == 0 && reopened.PendingRequest is null,
            "old response sends no cleanup or write requests");
        f.Key(); f.Handler.Success(Result()); await reopened.SubmitAsync(Request(f.Selection.CompletedIds));
        Check(reopened.State == TaskManagementState.Succeeded, "reopened form can save the retained selection");
        using var patch = JsonDocument.Parse(f.Handler.Requests.Last().Body);
        Check(patch.RootElement.GetProperty("attachment_ids").EnumerateArray().Select(x => x.GetString()).SequenceEqual(new[] { SecondId }),
            "following save sends original final IDs after cancelled forbidden read");
        Check(f.Handler.Deletes == 0, "late-response recovery never deletes files");
    }

    private static async Task ConcurrentOperations()
    {
        using var f = await Fixture.Create(); f.Bind(); var reading = new ResponseGate(); f.Handler.Next(reading.Send);
        Task<IReadOnlyList<TaskAttachment>> read = f.Selection.ReadFilesAsync(); await reading.Entered.Task;
        Check(f.Selection.IsBusy && !f.Selection.CanChange, "list read holds owner lock");
        await f.Workflow.SubmitAsync(Request());
        Check(f.Handler.Requests.Count == 1 && f.Workflow.PendingRequest is null, "list read blocks key issuance");
        reading.Release(Handler.Response(new { items = new[] { Meta(ExistingId, "기존 자료.txt", [1, 2, 3]) } }));
        Check((await read).Single().AttachmentId == ExistingId && f.Handler.Requests.Single().Path == "/api/v1/results/shared/tasks/9", "reader uses fixed task and common reader");
        await f.Selection.AddFileAsync(f.File("busy.bin", [4, 5])); Guid key = f.Selection.Items.Last().Key;
        var uploading = new ResponseGate(); f.Handler.Next(uploading.Send); f.Handler.Success(new { attachment_id = NewId, next_offset = 2 }); f.Handler.Success(Meta(NewId, "busy.bin", [4, 5]));
        Task upload = f.Selection.UploadAsync(key); await uploading.Entered.Task;
        Check(!f.Selection.RemoveLocal(f.Selection.Items.First().Key) && !f.Selection.ResetWithoutDelete(), "inflight upload prevents list edits");
        await f.Workflow.SubmitAsync(Request());
        Check(f.Handler.Requests.Count == 2 && f.Workflow.PendingRequest is null, "upload blocks write before keys");
        uploading.Release(Handler.Response(Meta(NewId, "busy.bin", [4, 5]))); await upload;
        Check(f.Selection.IsReady && f.Selection.Items.Count == 2, "completion releases gate and preserves target");
    }

    private static async Task LateSession()
    {
        using var f = await Fixture.Create(); f.Bind(); await f.Selection.AddFileAsync(f.File("late.bin", [9]));
        var late = new ResponseGate(); f.Handler.Next(late.Send); Task upload = f.Selection.UploadAsync(f.Selection.Items.Last().Key); await late.Entered.Task;
        f.Api.ClearIdentity(); late.Release(Handler.Response(Meta(NewId, "late.bin", [9]))); await upload;
        Check(f.Workflow.State == TaskManagementState.Disconnected && f.Selection.Items.Count == 0 && !f.Selection.Loaded, "late start cannot restore metadata after logout");
        Check(f.Handler.Requests.Count == 1 && f.Handler.Deletes == 0, "old start never continues chunks or cleanup");
    }

    private static async Task ExplicitDiscard()
    {
        using var f = await Fixture.Create(); f.Bind(); await f.Selection.AddFileAsync(f.File("discard.bin", [1])); Guid key = f.Selection.Items.Last().Key;
        f.HappyUpload("discard.bin", [1]); await f.Selection.UploadAsync(key);
        Check(!f.Selection.RemoveLocal(key), "known completed upload cannot silently disappear");
        f.Handler.Success(new { attachment_id = NewId }); await f.Selection.DiscardAsync(key);
        Check(f.Selection.Items.Count == 1 && f.Selection.CompletedIds!.SequenceEqual(new[] { ExistingId }), "explicit upload discard removes only new entry");
        Check(f.Handler.Deletes == 1 && f.Handler.Requests.Last().Path == "/api/v1/results/shared/uploads/" + NewId, "DELETE uses captured new upload ID");
        f.Selection.ResetWithoutDelete(); f.Workflow.Dispose(); Check(f.Handler.Deletes == 1, "reset and dispose add no DELETE");
        using var uncertain = await Fixture.Create(); uncertain.Bind([]); await uncertain.Selection.AddFileAsync(uncertain.File("lost.bin", [1]));
        Guid lost = uncertain.Selection.Items.Single().Key; uncertain.Handler.Drop(); await uncertain.Selection.UploadAsync(lost);
        await uncertain.Selection.UploadAsync(lost); await uncertain.Selection.RetryAsync(lost);
        Check(uncertain.Selection.Items.Single().State == TaskCreationUploadState.StartUncertain && uncertain.Handler.Requests.Count == 1, "unknown start is never restarted automatically");
        Check(uncertain.Selection.RemoveLocal(lost) && uncertain.Handler.Deletes == 0, "unknown start can only leave local selection explicitly");
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "RemoteWork-management-selection-" + Guid.NewGuid().ToString("N"));
        private readonly HashSet<string> files = [];
        public Handler Handler { get; } = new();
        public ApiClient Api { get; }
        public TaskManagementWorkflow Workflow { get; private set; } = null!;
        public TaskManagementAttachmentSelection Selection => Workflow.Attachments!;
        private Fixture() { Directory.CreateDirectory(directory); Api = new ApiClient("http://management-selection.invalid/", Handler); }
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); await f.Login(); f.Workflow = TaskManagementSessionStore.Get(f.Api, TaskManagementKind.Update, "9")!;
            f.Handler.Clear(); return f;
        }
        public async Task Login()
        {
            Handler.Success(new { access_token = "synthetic-token", token_type = "Bearer", expires_at = "2099-01-01T00:00:00Z",
                user = new { id = 1, login_id = "fixture", display_name = "Fixture", employee_no = "TEST-1", department = "Test", role = "admin", is_active = true } });
            await Api.LoginAsync("fixture", "fixture-only");
        }
        public void Bind(IReadOnlyList<TaskAttachment>? files = null) => Check(Selection.BindSnapshot(Context(), files ?? [FileInfo()]), "fixture baseline bind");
        public string File(string name, byte[] bytes)
        { string path = Path.Combine(directory, name); System.IO.File.WriteAllBytes(path, bytes); files.Add(path); return path; }
        public void Key() => Handler.Success(new { request_key = new string('k', 40), expires_at = "2099-01-01T00:00:00Z" });
        public void HappyUpload(string name, byte[] bytes)
        {
            Handler.Success(Meta(NewId, name, bytes), 201);
            for (int offset = 0; offset < bytes.Length; offset += 16384)
                Handler.Success(new { attachment_id = NewId, next_offset = Math.Min(offset + 16384, bytes.Length) });
            Handler.Success(Meta(NewId, name, bytes));
        }
        public void Dispose() { Api.Dispose(); Workflow.Dispose(); foreach (string path in files) System.IO.File.Delete(path); Directory.Delete(directory); }
    }
    private sealed record RequestLog(string Method, string Path, string Body, string? Key);
    private sealed class Handler : HttpMessageHandler
    {
        private readonly object gate = new();
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> actions = new();
        private readonly List<RequestLog> requests = [];
        public IReadOnlyList<RequestLog> Requests { get { lock (gate) return requests.ToArray(); } }
        public int Deletes => Requests.Count(x => x.Method == "DELETE");
        public void Clear() { lock (gate) requests.Clear(); }
        public void Next(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) { lock (gate) actions.Enqueue(action); }
        public void Success(object data, int status = 200) => Next((_, _) => Task.FromResult(Response(data, status)));
        public void Drop() => Next((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("controlled response loss")));
        public void Error(int status, string code) => Next((_, _) => Task.FromResult(ErrorResponse(status, code)));
        public static HttpResponseMessage ErrorResponse(int status, string code) => new((HttpStatusCode)status)
        { Content = new StringContent(JsonSerializer.Serialize(new { ok = false, error = new { code, message = "합성 오류" }, request_id = "fixture" }), Encoding.UTF8, "application/json") };
        public static HttpResponseMessage Response(object data, int status = 200) => new((HttpStatusCode)status)
        { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "fixture" }, ApiClient.Json), Encoding.UTF8, "application/json") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action;
            lock (gate)
            {
                requests.Add(new(request.Method.Method, request.RequestUri!.AbsolutePath, body, request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null));
                if (actions.Count == 0) throw new InvalidOperationException("Unexpected HTTP request in controlled selection test");
                action = actions.Dequeue();
            }
            return await action(request, ct);
        }
    }
    private sealed class ResponseGate
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<HttpResponseMessage> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken ct) { Entered.TrySetResult(); return response.Task; }
        public void Release(HttpResponseMessage value) => response.SetResult(value);
        public void Drop() => response.SetException(new HttpRequestException("controlled response loss"));
    }
}
