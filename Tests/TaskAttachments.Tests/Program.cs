using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Attachments;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

internal static class Program
{
    private static int checks;
    private const string Id = "c54d1432-6661-454c-b840-d2cd83ac90b1";
    private static readonly byte[] Bytes = Enumerable.Range(0, 20001).Select(i => (byte)(i % 251)).ToArray();
    private static TaskAttachment FileInfo(byte[]? bytes = null) => new(Id, "검증 파일.bin", (bytes ?? Bytes).Length,
        Convert.ToHexString(SHA256.HashData(bytes ?? Bytes)).ToLowerInvariant());

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        Control.CheckForIllegalCrossThreadCalls = true;
        int exit = 1;
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000), Size = new(1, 1) };
        host.Shown += async (_, _) =>
        {
            try
            {
                await HappyAndSave(); await MetadataRejection(); await ChunkRejection();
                await CurrentLink(); await SessionBoundaries(); await ScreenRace();
                Console.WriteLine($"PASS {checks} task attachment checks. Controlled HTTP and actual WinForms; no product server, DB, upload or shared deployment.");
                exit = 0;
            }
            catch (Exception error) { Console.Error.WriteLine($"FAIL after {checks}: {error}"); }
            finally { host.Close(); }
        };
        Application.Run(host);
        return exit;
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); checks++; }
    private static async Task Fails<T>(Func<Task> run) where T : Exception
    {
        try { await run(); throw new InvalidOperationException("Expected " + typeof(T).Name); }
        catch (T) { checks++; }
    }
    private static async Task<ApiClient> Login(Handler h)
    {
        var api = new ApiClient("http://localhost:5000", h);
        await api.LoginAsync("fixture", "synthetic");
        return api;
    }
    private static object Wire(TaskAttachment f) => new { attachment_id = f.AttachmentId, name = f.Name, size = f.Size, sha256 = f.Sha256 };
    private static HttpResponseMessage Files(params TaskAttachment[] files) => Ok(new { items = files.Select(Wire).ToArray() });
    private static HttpResponseMessage Ok(object value) => Raw(JsonSerializer.Serialize(new { ok = true, data = value, request_id = "fixture" }));
    private static HttpResponseMessage Raw(string value) => new(HttpStatusCode.OK) { Content = new StringContent(value, Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Error(int status, string code) => new((HttpStatusCode)status)
    { Content = new StringContent(JsonSerializer.Serialize(new { ok = false, error = new { code, message = "fixture" }, request_id = "fixture" }), Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Chunk(byte[] bytes, long offset, string? id = null, long? next = null, bool? eof = null, string? data = null)
    {
        byte[] part = bytes.Skip((int)offset).Take(16384).ToArray();
        return Ok(new { attachment_id = id ?? Id, offset, next_offset = next ?? offset + part.Length,
            eof = eof ?? offset + part.Length == bytes.Length, data = data ?? Convert.ToBase64String(part) });
    }
    private static Task<HttpResponseMessage> Ready(HttpResponseMessage response) => Task.FromResult(response);
    private static long Offset(string path) => long.Parse(path[(path.IndexOf("offset=", StringComparison.Ordinal) + 7)..]);

    private static async Task HappyAndSave()
    {
        using var h = new Handler(); using var api = await Login(h);
        var service = new HttpTaskAttachmentService(api.CaptureSession());
        var file = FileInfo();
        h.Respond = path => Ready(path.Contains("/chunks?") ? Chunk(Bytes, Offset(path)) : Files(file));
        var list = await service.GetFilesAsync("9007199254740993");
        Check(list.Single() == file, "Exact opaque BIGINT request and metadata");
        Check(h.Paths.Last().EndsWith("tasks/9007199254740993"), "BIGINT preserved");
        TaskAttachmentDownload download = await service.DownloadAsync("1", file);
        Check(h.Paths.Count(p => p.Contains("chunks?")) == 2, "Chunked GET uses shared paths");
        Check(h.Paths.All(p => !p.Contains("results/attachments/") && !p.Contains("results/uploads/")), "No result transport path substitution");
        string folder = Path.Combine(Path.GetTempPath(), "RemoteWork-TaskAttachments-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string path = Path.Combine(folder, "new.bin");
            await download.SaveNewAsync(path);
            Check(System.IO.File.ReadAllBytes(path).SequenceEqual(Bytes), "Verified bytes saved");
            await Fails<IOException>(() => download.SaveNewAsync(path));
            Check(System.IO.File.ReadAllBytes(path).SequenceEqual(Bytes), "Existing file not overwritten");
            Check(Directory.GetFiles(folder).Length == 1, "Failed save temporary removed");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Fails<OperationCanceledException>(() => download.SaveNewAsync(Path.Combine(folder, "cancelled.bin"), cancelled.Token));
            Check(Directory.GetFiles(folder).Length == 1, "Cancelled save has no output");
            h.Respond = _ => Ready(Error(403, "FORBIDDEN"));
            await Fails<ApiException>(() => download.SaveNewAsync(Path.Combine(folder, "revoked.bin")));
            Check(Directory.GetFiles(folder).Length == 1, "Permission revoked while choosing filename blocks save");
            api.ClearIdentity();
            await Fails<OperationCanceledException>(() => download.SaveNewAsync(Path.Combine(folder, "old-session.bin")));
            Check(Directory.GetFiles(folder).Length == 1, "Old login cannot save checked bytes");
        }
        finally
        {
            foreach (string owned in Directory.GetFiles(folder)) System.IO.File.Delete(owned);
            Directory.Delete(folder);
        }
        using var hz = new Handler(); using var az = await Login(hz);
        var zero = FileInfo([]);
        hz.Respond = path => Ready(path.Contains("/chunks?") ? Chunk([], 0) : Files(zero));
        Check((await new HttpTaskAttachmentService(az.CaptureSession()).DownloadAsync("1", zero)).File.Size == 0, "Empty files verified by hash and eof");
    }

    private static async Task MetadataRejection()
    {
        using var h = new Handler(); using var api = await Login(h);
        var service = new HttpTaskAttachmentService(api.CaptureSession());
        foreach (string bad in new[] { "0", "01", "-1", "1 ", "9223372036854775808", "../1", "" })
        {
            int before = h.Paths.Count;
            await Fails<ArgumentException>(() => service.GetFilesAsync(bad));
            Check(h.Paths.Count == before, "Invalid task ID not sent");
        }
        var valid = FileInfo();
        foreach (var invalid in new[] { valid with { AttachmentId = "../file" }, valid with { AttachmentId = Id.ToUpperInvariant() },
            valid with { Name = "../secret" }, valid with { Name = ".." }, valid with { Name = "line\nname" }, valid with { Name = new string('x', 256) },
            valid with { Size = -1 }, valid with { Size = HttpTaskAttachmentService.MaxFileBytes + 1 }, valid with { Sha256 = "0" }, valid with { Sha256 = new string('Z', 64) } })
        {
            h.Respond = _ => Ready(Files(invalid));
            await Fails<InvalidDataException>(() => service.GetFilesAsync("1"));
        }
        foreach (var invalidList in new[] { new[] { valid, valid }, Enumerable.Repeat(valid, 11).ToArray() })
        {
            h.Respond = _ => Ready(Files(invalidList));
            await Fails<InvalidDataException>(() => service.GetFilesAsync("1"));
        }
        foreach (string data in new[] { "{\"items\":[],\"extra\":1}", "{\"items\":[],\"items\":[]}", "{\"items\":null}",
            "{\"items\":[{\"attachment_id\":\"" + Id + "\",\"name\":\"x\",\"size\":\"1\",\"sha256\":\"" + new string('0',64) + "\"}]}" })
        {
            h.Respond = _ => Ready(Raw("{\"ok\":true,\"data\":" + data + ",\"request_id\":\"fixture\"}"));
            await Fails<InvalidDataException>(() => service.GetFilesAsync("1"));
        }
    }

    private static async Task ChunkRejection()
    {
        foreach (Func<HttpResponseMessage> invalid in new Func<HttpResponseMessage>[] {
            () => Chunk(Bytes, 1), () => Chunk(Bytes, 0, next: 1), () => Chunk(Bytes, 0, id: Guid.NewGuid().ToString()),
            () => Chunk(Bytes, 0, eof: true), () => Chunk(Bytes, 0, data: "***"),
            () => Chunk(Bytes, 0, data: Convert.ToBase64String(new byte[16385])),
            () => Chunk(Bytes, 0, next: 0, data: ""),
            () => Chunk(Bytes, 0, data: " " + Convert.ToBase64String(Bytes[..16384])),
            () => Ok(new { attachment_id = Id, offset = 0, next_offset = 0, eof = true, data = "" }) })
        {
            using var h = new Handler(); using var api = await Login(h);
            h.Respond = path => Ready(path.Contains("/chunks?") ? invalid() : Files(FileInfo()));
            await Fails<InvalidDataException>(() => new HttpTaskAttachmentService(api.CaptureSession()).DownloadAsync("1", FileInfo()));
        }
        using var hashHandler = new Handler(); using var hashApi = await Login(hashHandler);
        byte[] corrupt = (byte[])Bytes.Clone(); corrupt[^1] ^= 1;
        hashHandler.Respond = path => Ready(path.Contains("/chunks?") ? Chunk(corrupt, Offset(path)) : Files(FileInfo()));
        await Fails<InvalidDataException>(() => new HttpTaskAttachmentService(hashApi.CaptureSession()).DownloadAsync("1", FileInfo()));
    }

    private static async Task CurrentLink()
    {
        foreach (string outcome in new[] { "removed", "metadata", "forbidden", "cancelled", "unavailable" })
        {
            using var h = new Handler(); using var api = await Login(h); int lists = 0;
            h.Respond = path =>
            {
                if (path.Contains("/chunks?")) return Ready(Chunk(Bytes, Offset(path)));
                if (++lists == 1) return Ready(Files(FileInfo()));
                return Ready(outcome switch
                {
                    "removed" => Files(), "metadata" => Files(FileInfo() with { Name = "changed.bin" }),
                    "forbidden" => Error(403, "FORBIDDEN"), "cancelled" => Error(409, "TASK_CANCELLED"),
                    _ => Error(503, "SHARED_ATTACHMENT_SCHEMA_REQUIRED")
                });
            };
            await Fails<ApiException>(() => new HttpTaskAttachmentService(api.CaptureSession()).DownloadAsync("1", FileInfo()));
            Check(lists == 2, "Displayed task link checked after transfer: " + outcome);
        }
    }

    private static async Task SessionBoundaries()
    {
        foreach (bool unauthorized in new[] { false, true })
        {
            using var h = new Handler(); using var api = await Login(h);
            var service = new HttpTaskAttachmentService(api.CaptureSession());
            var delayed = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            h.Respond = _ => delayed.Task;
            Task<IReadOnlyList<TaskAttachment>> request = service.GetFilesAsync("1");
            api.ClearIdentity(); await api.LoginAsync("fixture", "synthetic");
            delayed.SetResult(unauthorized ? Error(401, "SESSION_EXPIRED") : Files(FileInfo()));
            await Fails<OperationCanceledException>(async () => await request);
            Check(api.CurrentUser is not null && !service.IsCurrent, "Late old response cannot affect new login");
            int before = h.Paths.Count;
            await Fails<OperationCanceledException>(() => service.GetFilesAsync("1"));
            Check(h.Paths.Count == before, "Old service does not adopt new login");
        }
        using var current = new Handler(); using var currentApi = await Login(current);
        var captured = new HttpTaskAttachmentService(currentApi.CaptureSession());
        current.Respond = _ => Ready(Error(401, "SESSION_EXPIRED"));
        await Fails<ApiException>(() => captured.GetFilesAsync("1"));
        Check(currentApi.CurrentUser is null && !captured.IsCurrent, "Current 401 invalidates common login");
        using var midway = new Handler(); using var midwayApi = await Login(midway);
        var delayedChunk = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        midway.Respond = path => path.Contains("/chunks?") ? delayedChunk.Task : Ready(Files(FileInfo()));
        Task<TaskAttachmentDownload> transfer = new HttpTaskAttachmentService(midwayApi.CaptureSession()).DownloadAsync("1", FileInfo());
        await Until(() => midway.Paths.Any(p => p.Contains("/chunks?")));
        midwayApi.ClearIdentity(); delayedChunk.SetResult(Chunk(Bytes, 0));
        await Fails<OperationCanceledException>(async () => await transfer);
        Check(midway.Paths.Count(p => p.Contains("/chunks?")) == 1, "Logout mid-transfer sends no later chunks and returns no completed download");
    }

    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static async Task Until(Func<bool> ready)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!ready() && DateTime.UtcNow < deadline) await Task.Delay(10);
        Check(ready(), "UI reached expected state");
    }
    private static object TaskWire(string id) => new { task_id = id, title = "업무 " + id, assignee_id = 7,
        assignee_name = "직원", task_status = "not_started", start_date = "2026-10-21", due_date = "2026-10-25", description = "내용 " + id };

    private static async Task ScreenRace()
    {
        using var h = new Handler(); using var api = await Login(h);
        var delayed = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool firstPending = false;
        TaskAttachment secondFile = FileInfo() with { AttachmentId = "3b180fd8-8f30-4c02-9809-738206c5a554", Name = "두 번째.bin" };
        h.Respond = path =>
        {
            if (path.Contains("tasks?")) return Ready(Ok(new { items = new[] { TaskWire("1"), TaskWire("2") } }));
            if (path.EndsWith("shared/tasks/1")) { firstPending = true; return delayed.Task; }
            if (path.EndsWith("shared/tasks/2")) return Ready(Files(secondFile));
            if (path.EndsWith("tasks/1") || path.EndsWith("tasks/2")) return Ready(Ok(new { task = TaskWire(path[^1..]) }));
            return Ready(Error(503, "FIXTURE_NOT_PROVIDED"));
        };
        using var form = new Form { ShowInTaskbar = false, Location = new(-19000, -19000), StartPosition = FormStartPosition.Manual, Size = new(1100, 850) };
        using var control = new TasksControl(PreviewRole.Employee, new HttpTaskQueryService(api)) { Dock = DockStyle.Fill };
        form.Controls.Add(control); form.Show();
        DataGridView tasks = Field<DataGridView>(control, "gridTasks"), attachments = Field<DataGridView>(control, "gridTaskAttachments");
        Task refresh = control.RefreshAsync();
        try { await Until(() => tasks.Rows.Count == 2); }
        catch { throw new InvalidOperationException("Screen list: " + Field<Label>(control, "lblTaskQueryStatus").Text + " | " + string.Join(",", h.Paths)); }
        tasks.CurrentCell = tasks.Rows[0].Cells[0]; tasks.Rows[0].Selected = true;
        await Until(() => firstPending);
        tasks.CurrentCell = tasks.Rows[1].Cells[0]; tasks.Rows[1].Selected = true;
        await Until(() => attachments.Rows.Count == 1 && (attachments.Rows[0].Tag as TaskAttachment)?.Name == secondFile.Name);
        delayed.SetResult(Files(FileInfo())); await refresh; await Task.Delay(100);
        Check((attachments.Rows[0].Tag as TaskAttachment)?.Name == secondFile.Name, "Late task1 attachments cannot replace selected task2");
        Check(attachments.Columns[2] is DataGridViewButtonColumn, "Existing grid exposes explicit save action");
        api.ClearIdentity(); await Until(() => attachments.Rows.Count == 0);
        Check(Field<Label>(control, "lblText4").Text.Contains("업무 선택"), "Logout clears previous attachments");
        form.Close();
    }

    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public Func<string, Task<HttpResponseMessage>> Respond { get; set; } = _ => Ready(Files());
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.PathAndQuery;
            Paths.Add(path);
            if (path.EndsWith("auth/login")) return Ready(Ok(new { access_token = "synthetic-token", token_type = "Bearer", expires_at = "2030-01-01T00:00:00Z",
                user = new { id = 7, login_id = "fixture", display_name = "직원", employee_no = "E7", department = "테스트", role = "employee", is_active = true } }));
            Check(request.Method == HttpMethod.Get, "Attachment consumer only GETs");
            Check(request.Headers.Authorization?.Parameter == "synthetic-token", "Captured common bearer used");
            return Respond(path);
        }
    }
}
