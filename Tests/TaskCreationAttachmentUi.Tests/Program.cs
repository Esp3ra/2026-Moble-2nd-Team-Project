using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Attachments;
using RemoteWork.WinForms.Features.Tasks.Creation;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private static int checks;
    private static string? capture;

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Control.CheckForIllegalCrossThreadCalls = true;
        capture = args.FirstOrDefault();
        int exit = 1;
        using var coordinator = new Form { ShowInTaskbar = false, Location = new(-20000, -20000), StartPosition = FormStartPosition.Manual, Size = new(1, 1) };
        coordinator.Shown += (_, _) => coordinator.BeginInvoke((Action)(() =>
        {
            try
            {
                Run(Preview); Run(SelectionAndReopen); Run(LocalRemoval); Run(DeleteConfirmationTarget);
                Run(DeleteUnknown); Run(ExpiredDeleteRemoval); Run(PendingRegistration); Run(RetryAndManualLocks); Run(BusyAndSessionChange);
                Console.WriteLine($"PASS attachment UI: {checks} checks; actual WinForms, explicit controlled HTTP.");
                exit = 0;
            }
            catch (Exception error) { Console.Error.WriteLine($"FAIL after {checks}: {error.GetType().Name}: {error.Message}"); }
            finally { coordinator.Close(); }
        }));
        Application.Run(coordinator);
        return exit;
    }

    private static void Check(bool value, string label) { if (!value) throw new InvalidOperationException(label); checks++; }
    private static void Run(Action action) { action(); Console.WriteLine("PASS " + action.Method.Name); }
    private static T Field<T>(object owner, string name)
    {
        for (Type? type = owner.GetType(); type is not null; type = type.BaseType)
            if (type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance) is { } field) return (T)field.GetValue(owner)!;
        throw new MissingFieldException(name);
    }
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(owner, value);
    private static void Show(Form form) { form.StartPosition = FormStartPosition.Manual; form.Location = new(-16000, -16000); form.Show(); Application.DoEvents(); }
    private static Button Button(object form, string name) => Field<Button>(form, name);
    private static void Wait(Task task)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while (!task.IsCompleted && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(1); }
        if (!task.IsCompleted) throw new TimeoutException("Controlled UI operation did not finish");
        task.GetAwaiter().GetResult(); Application.DoEvents();
    }
    private static void Until(Func<bool> ready)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        while (!ready() && DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(1); }
        Check(ready(), "UI reaches expected stable state");
        Application.DoEvents();
    }
    private static void Idle(Picker form, Fixture f) => Until(() => !f.Selection.IsBusy && !Field<bool>(form, "operating"));
    private static void Select(Picker form, Guid key)
    {
        var grid = Field<DataGridView>(form, "gridFiles");
        var row = grid.Rows.Cast<DataGridViewRow>().Single(x => Equals(x.Tag, key));
        grid.CurrentCell = row.Cells[0]; Application.DoEvents();
    }
    private static void Add(Picker form, Fixture f, string name)
    {
        int count = f.Selection.Items.Count;
        form.FilePath = f.File(name);
        Button(form, "btnAdd").PerformClick();
        Until(() => f.Selection.Items.Count == count + 1);
        Idle(form, f);
    }
    private static void Upload(Picker form, Fixture f, Guid key)
    {
        Select(form, key);
        Check(Button(form, "btnUpload").Enabled, "selected prepared file permits upload");
        Button(form, "btnUpload").PerformClick();
        Until(() => f.Selection.Items.Single(x => x.Key == key).State == TaskCreationUploadState.Completed);
        Idle(form, f);
    }

    private static void Preview()
    {
        using var form = new TaskCreationAttachmentsForm(); Show(form);
        Check(!Button(form, "btnAdd").Enabled && !Button(form, "btnUpload").Enabled && !Button(form, "btnDelete").Enabled, "preview performs no file or HTTP actions");
        Check(Field<DataGridView>(form, "gridFiles").Rows.Count == 0, "preview contains no sample identity or file");
        Check(Button(form, "btnClose").Enabled, "preview can close");
    }

    private static void SelectionAndReopen()
    {
        using var f = new Fixture();
        using (var form = new Picker(f.Selection))
        {
            Show(form); int before = f.Handler.Requests.Count;
            Add(form, f, "원격근무-업무-긴파일이름-선택검사.bin");
            Check(f.Handler.Requests.Count == before, "choose file only prepares bytes locally");
            Check(Button(form, "btnUpload").Enabled && !Button(form, "btnDelete").Enabled, "ready file permits upload but not delete");
            Check(!f.Selection.IsReady, "unuploaded file blocks registration");
            Upload(form, f, f.Selection.Items.Single().Key);
            Check(Button(form, "btnDelete").Enabled && !Button(form, "btnUpload").Enabled && !Button(form, "btnRemove").Enabled, "completed file requires explicit server deletion");
            Check(f.Selection.IsReady && f.Selection.CompletedIds?.Count == 1, "only completed file enters final ID snapshot");
            Capture(form, "attachment-picker-standard.png");
            form.Size = form.MinimumSize; form.PerformLayout(); Application.DoEvents();
            var actions = Field<FlowLayoutPanel>(form, "actions");
            Check(actions.Height >= 76 && Button(form, "btnClose").Bottom <= actions.ClientSize.Height, "minimum window wraps action buttons inside visible layout");
            Check(Field<DataGridView>(form, "gridFiles").Height >= 70, "minimum window retains readable file rows");
            Capture(form, "attachment-picker-minimum.png");
            form.Close();
            Check(f.Handler.Deletes.Count == 0 && f.Selection.Items.Count == 1, "closing preserves uploaded selection without DELETE");
        }
        using var reopened = new Picker(f.Selection); Show(reopened);
        Check(Field<DataGridView>(reopened, "gridFiles").Rows.Count == 1 && Button(reopened, "btnDelete").Enabled, "reopen restores borrowed selection");
    }

    private static void Capture(Form form, string name)
    {
        if (capture is null) return;
        Directory.CreateDirectory(capture);
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
        bitmap.Save(Path.Combine(capture, name));
    }

    private static void LocalRemoval()
    {
        using var f = new Fixture(); using var form = new Picker(f.Selection); Show(form);
        Add(form, f, "local.bin"); int before = f.Handler.Requests.Count;
        form.AcceptRemoval = false; Button(form, "btnRemove").PerformClick();
        Check(f.Selection.Items.Count == 1 && f.Handler.Requests.Count == before, "cancel local removal preserves selected file");
        form.AcceptRemoval = true; Button(form, "btnRemove").PerformClick();
        Check(f.Selection.Items.Count == 0 && f.Handler.Requests.Count == before, "local removal emits no DELETE");
        Add(form, f, "uncertain-start.bin");
        f.Handler.DropStart = true;
        Button(form, "btnUpload").PerformClick();
        Until(() => f.Selection.Items.Single().State == TaskCreationUploadState.StartUncertain); Idle(form, f);
        Check(Button(form, "btnRemove").Enabled && !Button(form, "btnRetry").Enabled && !Button(form, "btnDelete").Enabled, "unknown start cannot retry or delete unknown UUID");
        Button(form, "btnRemove").PerformClick();
        Check(form.RemovalState == TaskCreationUploadState.StartUncertain && f.Selection.Items.Count == 0 && f.Handler.Deletes.Count == 0, "unknown start warning is shown before local-only removal");
    }

    private static void DeleteConfirmationTarget()
    {
        using var f = new Fixture(); using var form = new Picker(f.Selection); Show(form);
        Add(form, f, "first.bin"); Guid first = f.Selection.Items[0].Key; Upload(form, f, first);
        string firstId = f.Selection.CompletedIds!.Single();
        Add(form, f, "second.bin"); Guid second = f.Selection.Items[1].Key; Upload(form, f, second);
        Select(form, first); form.AcceptDeletion = false;
        Button(form, "btnDelete").PerformClick();
        Check(f.Handler.Deletes.Count == 0 && f.Selection.Items.Count == 2, "cancel deletion makes no request");
        form.AcceptDeletion = true;
        form.DuringConfirmation = () => Select(form, second);
        Button(form, "btnDelete").PerformClick();
        Until(() => f.Selection.Items.Count == 1); Idle(form, f);
        Check(form.ConfirmedKey == first && f.Handler.Deletes.Single().Path.EndsWith("/" + firstId), "DELETE remains bound to the file shown before confirmation");
        Check(f.Selection.Items.Single().Key == second, "selection change cannot delete a different file");
        var deletion = f.Handler.Deletes.Single();
        Check(deletion.Body.Length == 0 && deletion.Key is null && deletion.Bearer == "synthetic-token", "captured DELETE has no body or new request key");
    }

    private static void DeleteUnknown()
    {
        using var f = new Fixture(); using var form = new Picker(f.Selection); Show(form);
        Add(form, f, "delete-unknown.bin"); Guid key = f.Selection.Items.Single().Key; Upload(form, f, key);
        string id = f.Selection.CompletedIds!.Single();
        f.Handler.DropDelete = true; form.AcceptDeletion = true;
        Button(form, "btnDelete").PerformClick();
        Until(() => f.Selection.Items.Single().State == TaskCreationUploadState.DiscardUncertain); Idle(form, f);
        Check(!f.Selection.IsReady && f.Selection.CompletedIds is null, "unknown deletion invalidates registration IDs");
        Check(Button(form, "btnDelete").Enabled && Button(form, "btnDelete").Text == "삭제 다시 확인" && !Button(form, "btnUpload").Enabled, "unknown deletion offers explicit same-ID delete confirmation only");
        int requests = f.Handler.Requests.Count;
        try { Wait(f.Workflow.SubmitAsync(f.Request(new[] { id }))); }
        catch (ArgumentException) { checks++; }
        Check(f.Handler.Requests.Count == requests && f.Workflow.State == TaskCreationState.Editing, "registration cannot race an uncertain file deletion");
        Button(form, "btnDelete").PerformClick(); Until(() => f.Selection.Items.Count == 0); Idle(form, f);
        Check(f.Handler.Deletes.Count == 2 && f.Handler.Deletes.All(r => r.Path.EndsWith("/" + id)), "404 after lost successful DELETE clears the same file without reupload");
    }

    private static void PendingRegistration()
    {
        using var f = new Fixture();
        Wait(f.Selection.AddFileAsync(f.File("pending.bin")));
        Wait(f.Selection.UploadAsync(f.Selection.Items.Single().Key));
        string id = f.Selection.CompletedIds!.Single();
        using (var form = new TaskEditForm(new HttpTaskPreparationService(f.Api.CaptureSession()), f.Workflow))
        {
            Show(form);
            Field<TextBox>(form, "txtTaskTitle").Text = "첨부 포함 업무";
            Field<TextBox>(form, "txtTaskDescription").Text = "원문 유지";
            Field<DateTimePicker>(form, "dtpStartDate").Value = new DateTime(2026, 10, 22);
            Field<DateTimePicker>(form, "dtpStartDate").Checked = true;
            Field<DateTimePicker>(form, "dtpDeadline").Value = new DateTime(2026, 10, 23);
            Field<DateTimePicker>(form, "dtpDeadline").Checked = true;
            Set(form, "preparedSelection", new TaskPreparationSelection(null,
                new TaskUserCandidate(7, "직원", "직원 #7"), Array.AsReadOnly(new[] { 2 })));
            Button(form, "btnConfirm").PerformClick();
            Check(Button(form, "btnCreateTask").Enabled, "completed selection participates in input confirmation");
            f.Handler.DropCreation = true;
            Button(form, "btnCreateTask").PerformClick();
            Until(() => f.Workflow.State == TaskCreationState.NeedsResultCheck);
            Check(f.Workflow.PendingRequest?.AttachmentIds?.Single() == id, "frozen registration request includes uploaded ID");
            using var body = JsonDocument.Parse(f.Handler.Creations.Single().Body);
            Check(body.RootElement.GetProperty("attachment_ids")[0].GetString() == id, "actual common HTTP body includes selected attachment IDs");
            Check(!Button(form, "btnCreateTask").Enabled, "uncertain registration prevents a new POST");
            form.Close();
        }
        using var picker = new Picker(f.Selection); Show(picker);
        Check(Field<DataGridView>(picker, "gridFiles").Rows.Count == 1 && !Button(picker, "btnAdd").Enabled
            && !Button(picker, "btnDelete").Enabled && !Button(picker, "btnUpload").Enabled,
            "reopened uncertain registration preserves display and locks attachment changes");
        int count = f.Handler.Requests.Count;
        Wait(f.Selection.DiscardAsync(f.Selection.Items.Single().Key));
        Wait(f.Selection.AddFileAsync(f.File("blocked.bin")));
        Check(!f.Selection.RemoveLocal(f.Selection.Items.Single().Key) && f.Handler.Requests.Count == count, "direct selection APIs cannot bypass pending registration lock");
        using var reopened = new TaskEditForm(new HttpTaskPreparationService(f.Api.CaptureSession()), f.Workflow); Show(reopened);
        Check(Field<TextBox>(reopened, "txtTaskTitle").Text == "첨부 포함 업무" && f.Workflow.PendingRequest!.AttachmentIds!.Single() == id,
            "registration window restores the same text and final file IDs");
        Button(reopened, "btnCheckCreationResult").PerformClick();
        Until(() => f.Workflow.State == TaskCreationState.Succeeded);
        Check(f.Handler.Deletes.Count == 0 && f.Selection.Items.Count == 0 && f.Handler.Creations.Count == 1,
            "receipt recovery consumes selection locally without DELETE or duplicate creation");
        Check(f.Workflow.ResetAfterSuccess() && f.Selection.IsReady && f.Selection.Items.Count == 0, "next registration starts with an empty selection");
    }

    private static void ExpiredDeleteRemoval()
    {
        using var f = new Fixture(); using var form = new Picker(f.Selection); Show(form);
        Add(form, f, "expired.bin"); Upload(form, f, f.Selection.Items.Single().Key);
        f.Handler.ExpiredDelete = true; form.AcceptDeletion = true;
        Button(form, "btnDelete").PerformClick();
        Until(() => f.Selection.Items.Single().State == TaskCreationUploadState.DiscardRejected); Idle(form, f);
        Check(Button(form, "btnRemove").Enabled && !f.Selection.IsReady, "exact expired-file rejection permits local cleanup but not registration");
        int before = f.Handler.Requests.Count;
        form.AcceptRemoval = true; Button(form, "btnRemove").PerformClick();
        Check(form.RemovalState == TaskCreationUploadState.DiscardRejected && f.Selection.Items.Count == 0
            && f.Handler.Requests.Count == before, "explicit expired-file removal warns about remaining server file and makes no DELETE");
    }

    private static void RetryAndManualLocks()
    {
        using var f = new Fixture();
        Wait(f.Selection.AddFileAsync(f.File("retry-lock.bin")));
        Wait(f.Selection.UploadAsync(f.Selection.Items.Single().Key));
        Guid key = f.Selection.Items.Single().Key;
        var request = f.Request(f.Selection.CompletedIds!);
        f.Handler.DropCreation = true;
        Wait(f.Workflow.SubmitAsync(request));
        Check(f.Workflow.State == TaskCreationState.NeedsResultCheck, "lost registration enters result-check state");
        f.Handler.ResultNotRecorded = true;
        Wait(f.Workflow.CheckResultAsync());
        Check(f.Workflow.State == TaskCreationState.RetryAllowed, "explicit not-recorded permits only original registration retry");
        using var picker = new Picker(f.Selection); Show(picker);
        int count = f.Handler.Requests.Count;
        Wait(f.Selection.DiscardAsync(key)); Wait(f.Selection.AddFileAsync(f.File("retry-blocked.bin")));
        Check(!f.Selection.RemoveLocal(key) && f.Handler.Requests.Count == count && !Button(picker, "btnDelete").Enabled
            && !Button(picker, "btnAdd").Enabled, "not-recorded result does not unlock attachment mutation");
        f.Handler.ConflictCreation = true;
        Wait(f.Workflow.RetryAsync());
        Check(f.Workflow.State == TaskCreationState.ManualReview, "key conflict requires manual review");
        count = f.Handler.Requests.Count;
        Wait(f.Selection.DiscardAsync(key)); Wait(f.Selection.AddFileAsync(f.File("manual-blocked.bin")));
        Check(!f.Selection.RemoveLocal(key) && f.Handler.Requests.Count == count && !Button(picker, "btnDelete").Enabled,
            "manual-review state also keeps original attachment selection locked");
    }

    private static void BusyAndSessionChange()
    {
        using var f = new Fixture(); using var form = new Picker(f.Selection); Show(form);
        Add(form, f, "delayed.bin"); Guid key = f.Selection.Items.Single().Key; Upload(form, f, key);
        var gate = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        f.Handler.DelayedDelete = gate;
        form.AcceptDeletion = true;
        Button(form, "btnDelete").PerformClick();
        Until(() => f.Selection.IsBusy);
        Check(!Button(form, "btnClose").Enabled && !Button(form, "btnAdd").Enabled && !Button(form, "btnDelete").Enabled, "in-flight deletion disables duplicate actions and close");
        int before = f.Handler.Requests.Count;
        Wait(f.Workflow.SubmitAsync(f.Request(Array.Empty<string>())));
        Check(f.Handler.Requests.Count == before && f.Workflow.State == TaskCreationState.Editing, "in-flight file action prevents concurrent registration dispatch");
        form.Close();
        Check(!form.IsDisposed && form.Visible, "user close is refused while a current deletion runs");
        f.Api.ClearIdentity(); Application.DoEvents();
        Until(() => Field<DataGridView>(form, "gridFiles").Rows.Count == 0);
        Check(Button(form, "btnClose").Enabled && !Button(form, "btnAdd").Enabled, "session change clears names and allows closing");
        gate.SetResult(Handler.Success(new { attachment_id = f.Handler.Uploaded.Keys.Single() }));
        Until(() => !Field<bool>(form, "operating"));
        Check(f.Selection.Items.Count == 0 && f.Handler.Deletes.Count == 1, "late delete response cannot restore old selection or retry");
    }

    private sealed class Picker(TaskCreationAttachmentSelection selection) : TaskCreationAttachmentsForm(selection)
    {
        public string? FilePath;
        public bool AcceptDeletion, AcceptRemoval;
        public Action? DuringConfirmation;
        public Guid? ConfirmedKey;
        public TaskCreationUploadState? RemovalState;
        protected override string? ChooseFile() => FilePath;
        protected override bool ConfirmFileDeletion(TaskCreationAttachmentItem item)
        { ConfirmedKey = item.Key; DuringConfirmation?.Invoke(); return AcceptDeletion; }
        protected override bool ConfirmLocalRemoval(TaskCreationAttachmentItem item)
        { RemovalState = item.State; return AcceptRemoval; }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "RemoteWork-attachment-ui-" + Guid.NewGuid().ToString("N"));
        public Handler Handler { get; } = new();
        public ApiClient Api { get; }
        public TaskCreationWorkflow Workflow { get; }
        public TaskCreationAttachmentSelection Selection => Workflow.Attachments!;
        public Fixture()
        {
            Directory.CreateDirectory(directory);
            Api = new ApiClient("http://localhost:5000/", Handler);
            Wait(Api.LoginAsync("synthetic", "synthetic-password"));
            var session = Api.CaptureSession();
            Workflow = new TaskCreationWorkflow(new HttpTaskCreationService(session), new HttpTaskCreationUploadService(session));
        }
        public string File(string name)
        { string path = Path.Combine(directory, name); System.IO.File.WriteAllBytes(path, Enumerable.Range(0, 129).Select(x => (byte)x).ToArray()); return path; }
        public TaskCreationRequest Request(IReadOnlyList<string> ids) => new("업무", "", new(2026, 10, 22), new(2026, 10, 23), 7, new[] { 2 }, ids);
        public void Dispose()
        {
            Workflow.Dispose(); Api.Dispose();
            foreach (string file in Directory.GetFiles(directory)) System.IO.File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private sealed record Request(string Method, string Path, string Body, string? Key, string? Bearer);
    private sealed class Handler : HttpMessageHandler
    {
        public List<Request> Requests { get; } = new();
        public List<Request> Deletes => Requests.Where(x => x.Method == "DELETE").ToList();
        public List<Request> Creations => Requests.Where(x => x.Path == "/api/v1/tasks").ToList();
        public Dictionary<string, object> Uploaded { get; } = new();
        private readonly HashSet<string> removed = new();
        public bool DropStart, DropDelete, DropCreation, ExpiredDelete, ResultNotRecorded, ConflictCreation;
        public TaskCompletionSource<HttpResponseMessage>? DelayedDelete;
        private static object Result() => new { task_id = "777", row_version = "1", assignment_revision = 1, task_status = "not_started", updated_at = "2026-10-22T00:00:00Z" };
        public static HttpResponseMessage Success(object data, int status = 200) => new((HttpStatusCode)status)
        { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "synthetic" }, ApiClient.Json), Encoding.UTF8, "application/json") };
        private static HttpResponseMessage Error(int status, string code) => new((HttpStatusCode)status)
        { Content = new StringContent(JsonSerializer.Serialize(new { ok = false, error = new { code, message = "합성 오류" }, request_id = "synthetic" }), Encoding.UTF8, "application/json") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.AbsolutePath;
            string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add(new(request.Method.Method, path, body, request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null, request.Headers.Authorization?.Parameter));
            if (path.EndsWith("/auth/login")) return Success(new
            {
                access_token = "synthetic-token", token_type = "Bearer", expires_at = "2030-01-01T00:00:00Z",
                user = new { id = 2, login_id = "admin", display_name = "관리자", employee_no = "A2", department = "팀", role = "admin", is_active = true }
            });
            if (path.EndsWith("/uploads/start"))
            {
                using var json = JsonDocument.Parse(body);
                string id = Guid.NewGuid().ToString("D");
                var item = new { attachment_id = id, name = json.RootElement.GetProperty("name").GetString(), size = json.RootElement.GetProperty("size").GetInt64(), sha256 = json.RootElement.GetProperty("sha256").GetString() };
                Uploaded.Add(id, item);
                if (DropStart) { DropStart = false; throw new HttpRequestException("explicit start response loss"); }
                return Success(item, 201);
            }
            if (path.Contains("/results/shared/uploads/"))
            {
                string[] segments = path.Split('/');
                string id = segments[6];
                if (request.Method == HttpMethod.Delete)
                {
                    if (DelayedDelete is not null) return await DelayedDelete.Task;
                    if (ExpiredDelete) return Error(409, "ATTACHMENT_UNAVAILABLE");
                    if (!removed.Add(id)) return Error(404, "ATTACHMENT_NOT_FOUND");
                    if (DropDelete) { DropDelete = false; throw new HttpRequestException("explicit delete response loss"); }
                    return Success(new { attachment_id = id });
                }
                if (path.EndsWith("/complete")) return Success(Uploaded[id]);
                if (path.EndsWith("/chunks"))
                {
                    using var json = JsonDocument.Parse(body);
                    return Success(new { attachment_id = id, next_offset = json.RootElement.GetProperty("offset").GetInt64() + Convert.FromBase64String(json.RootElement.GetProperty("data").GetString()!).Length });
                }
            }
            if (path.EndsWith("/write-requests/keys")) return Success(new { request_key = new string('k', 40), expires_at = "2030-01-01T00:00:00Z" }, 201);
            if (path.EndsWith("/write-requests/result")) return ResultNotRecorded ? Success(new { state = "not_recorded" })
                : Success(new { state = "completed", http_status = 201, result = Result(), expires_at = "2030-01-01T00:00:00Z" });
            if (path == "/api/v1/tasks")
            {
                if (DropCreation) { DropCreation = false; throw new HttpRequestException("explicit creation response loss"); }
                if (ConflictCreation) return Error(409, "IDEMPOTENCY_KEY_REUSED");
                return Success(Result(), 201);
            }
            throw new InvalidOperationException("Unexpected route in explicit controlled UI fixture");
        }
    }
}
