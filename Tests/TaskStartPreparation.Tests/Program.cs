using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

internal static class Program
{
    private static int checks;
    private static string? capture;
    private const string Id = "9007199254740993", Version = "9007199254740994";
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Control.CheckForIllegalCrossThreadCalls = true;
        int exit = 1;
        using var coordinator = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000), Size = new(1, 1) };
        coordinator.Shown += (_, _) => coordinator.BeginInvoke((Action)(() =>
        {
            try
            {
                if (args.SequenceEqual(new[] { "--http" })) Live();
                else { capture = args.FirstOrDefault(); Decisions(); InvalidContexts(); StaticForms(); ConnectedForms(); Rejections(); Failures(); Lifetime(); ParentButtons(); ParentRefreshLifetime(); }
                Console.WriteLine(args.SequenceEqual(new[] { "--http" })
                    ? $"PASS TaskStartPreparation HTTP: {checks} checks."
                    : $"PASS TaskStartPreparation controlled: {checks} checks; actual WinForms and controlled HTTP, no server/DB.");
                exit = 0;
            }
            catch (Exception error) { Console.Error.WriteLine($"FAIL after {checks}: {error.GetType().Name}: {error.Message}"); }
            finally { coordinator.Close(); }
        }));
        Application.Run(coordinator);
        return exit;
    }
    private static void Check(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); checks++; }
    private static void Wait(Task task)
    {
        var stop = DateTime.UtcNow.AddSeconds(30);
        while (!task.IsCompleted && DateTime.UtcNow < stop) { Application.DoEvents(); Thread.Sleep(1); }
        if (!task.IsCompleted) throw new TimeoutException("Preparation operation timed out");
        task.GetAwaiter().GetResult();
    }
    private static T Wait<T>(Task<T> task) { Wait((Task)task); return task.GetAwaiter().GetResult(); }
    private static void Until(Func<bool> ready)
    {
        var stop = DateTime.UtcNow.AddSeconds(30);
        while (!ready() && DateTime.UtcNow < stop) { Application.DoEvents(); Thread.Sleep(1); }
        Check(ready(), "UI reaches expected state");
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Show(Form form) { form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new(-15000, -15000); form.Show(); Application.DoEvents(); }
    private static string Text(Control owner) => owner.Text + " " + string.Join(" ", owner.Controls.Cast<Control>().Select(Text));
    private static TaskDetail Detail(TaskDisplayStatus status = TaskDisplayStatus.NotStarted, string id = Id) => new(new TaskSummary(id, "조회 당시 제목", "표시명으로 ID 추측 금지", status, new DateOnly(2000, 1, 1), new DateOnly(2000, 1, 2)), "조회 당시 상세");
    private static TaskWriteContext Context(TaskDisplayStatus status = TaskDisplayStatus.NotStarted, int assignee = 7, string version = Version, string id = Id) => new(id, assignee, Array.AsReadOnly(new[] { 2 }), version, 3, status, new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero));
    private static object Wire(TaskDisplayStatus status = TaskDisplayStatus.NotStarted, int assignee = 7, string version = Version, string id = Id) => new { context = new { task_id = id, assignee_id = assignee, manager_user_ids = new[] { 2 }, row_version = version, assignment_revision = 3, task_status = Code(status), updated_at = "2026-10-19T00:00:00Z" } };
    private static string Code(TaskDisplayStatus status) => status switch { TaskDisplayStatus.NotStarted => "not_started", TaskDisplayStatus.InProgress => "in_progress", TaskDisplayStatus.Submitted => "submitted", TaskDisplayStatus.Approved => "approved", TaskDisplayStatus.Returned => "returned", _ => throw new ArgumentException() };
    private static ApiClient Api(Handler h, int userId = 7, string role = "employee") { var api = new ApiClient("http://localhost:5000/", h); Login(api, h, userId, role); return api; }
    private static void Login(ApiClient api, Handler h, int userId = 7, string role = "employee")
    {
        h.Success(new { access_token = "synthetic-token", token_type = "Bearer", expires_at = "2030-01-01T00:00:00Z", user = new { id = userId, login_id = "fixture", display_name = "합성 사용자", employee_no = "FIX", department = "검증팀", role, is_active = true } });
        Wait(api.LoginAsync("synthetic", "not-a-real-password"));
    }
    private static void Decisions()
    {
        foreach (var kind in Enum.GetValues<TaskStartKind>())
            foreach (var status in Enum.GetValues<TaskDisplayStatus>())
                foreach (bool own in new[] { true, false })
                {
                    var result = TaskStartTargetPreparation.Check(kind, Detail(status), Context(status, own ? 7 : 8), 7);
                    var expected = !own ? TaskStartTargetOutcome.NotCurrentAssignee
                        : (kind == TaskStartKind.Accept ? status == TaskDisplayStatus.NotStarted : status == TaskDisplayStatus.Returned)
                            ? TaskStartTargetOutcome.Matched : TaskStartTargetOutcome.WrongState;
                    Check(result.Outcome == expected && (result.Target is not null) == (expected == TaskStartTargetOutcome.Matched), "Both actions use current assignee and their own state, across all five states");
                }
        var changed = TaskStartTargetPreparation.Check(TaskStartKind.Accept, Detail(), Context(TaskDisplayStatus.InProgress), 7);
        Check(changed.Outcome == TaskStartTargetOutcome.SourceStatusChanged && changed.Target is null, "Different source/current states never become a checked target");
        var accepted = TaskStartTargetPreparation.Check(TaskStartKind.Accept, Detail(), Context(), 7).Target!;
        Check(accepted.TaskId == Id && accepted.RowVersion == Version && accepted.AssignmentRevision == 3 && accepted.UserId == 7 && accepted.AssigneeId == 7, "Snapshot keeps exact BIGINT strings, user and revision");
        Check(Detail().Summary.DueDate.Year == 2000 && accepted.Status == TaskDisplayStatus.NotStarted, "Target preparation does not invent a deadline permission rule");
    }
    private static void InvalidContexts()
    {
        var context = Context();
        foreach (Action action in new Action[] {
            () => TaskStartTargetPreparation.Check((TaskStartKind)99, Detail(), context, 7),
            () => TaskStartTargetPreparation.Check(TaskStartKind.Accept, Detail(), context, 0),
            () => TaskStartTargetPreparation.Check(TaskStartKind.Accept, Detail(), context with { TaskId = "2" }, 7),
            () => TaskStartTargetPreparation.Check(TaskStartKind.Accept, Detail(), context with { RowVersion = "01" }, 7),
            () => TaskStartTargetPreparation.Check(TaskStartKind.Accept, Detail(), context with { AssigneeId = 0 }, 7),
            () => TaskStartTargetPreparation.Check(TaskStartKind.Accept, Detail(), context with { AssignmentRevision = 0 }, 7),
            () => TaskStartTargetPreparation.Check(TaskStartKind.Accept, Detail(), context with { Status = (TaskDisplayStatus)99 }, 7),
            () => TaskStartTargetPreparation.Check(TaskStartKind.Accept, Detail(), context with { UpdatedAt = DateTimeOffset.Now.ToOffset(TimeSpan.FromHours(9)) }, 7) })
        { try { action(); throw new InvalidOperationException("Invalid context accepted"); } catch (ArgumentException) { checks++; } }
    }
    private static void StaticForms()
    {
        using var empty = new TaskAcceptanceForm(); Show(empty);
        Check(empty.CheckedTarget is null && !Field<Button>(empty, "btnConfirmTarget").Visible, "Default scaffold has no actionable or checked target"); empty.Close();
        using var legacy = new TaskAcceptanceForm(Detail(TaskDisplayStatus.Returned)); Show(legacy);
        Check(legacy.CheckedTarget is null && Text(legacy).Contains(Id) && !Field<Button>(legacy, "btnReloadContext").Visible, "Existing detail constructor stays read-only and disconnected"); legacy.Close();
    }
    private static void ConnectedForms()
    {
        foreach (var (kind, status, role, userId) in new[] {
            (TaskStartKind.Accept, TaskDisplayStatus.NotStarted, "employee", 7),
            (TaskStartKind.Rework, TaskDisplayStatus.Returned, "employee", 7),
            (TaskStartKind.Accept, TaskDisplayStatus.NotStarted, "admin", 2),
            (TaskStartKind.Rework, TaskDisplayStatus.Returned, "admin", 2) })
        {
            using var h = new Handler(); using var api = Api(h, userId, role); h.Success(Wire(status, userId));
            using var form = new TaskAcceptanceForm(Detail(status), kind, new HttpTaskPreparationService(api.CaptureSession())); Show(form);
            Until(() => Field<Button>(form, "btnConfirmTarget").Enabled);
            Check(form.CheckedTarget is null, "Loading matching data does not implicitly confirm a target");
            Field<Button>(form, "btnConfirmTarget").PerformClick(); var target = form.CheckedTarget;
            Check(target?.Kind == kind && target.TaskId == Id && target.RowVersion == Version && target.AssigneeId == userId && !form.IsDisposed && form.DialogResult != DialogResult.OK, "Explicit target confirmation is immutable and does not report write success");
            Check(h.LastPath == "/api/v1/tasks/" + Id + "/write-context" && h.Bearer == "synthetic-token" && h.ProductWrites == 0, "Connected form uses only the captured common GET endpoint");
            Check(Text(form).Contains("합의") && (Text(form).Contains("변경") || Text(form).Contains("저장")), "Form explains unresolved execution policy and unchanged state");
            if (kind == TaskStartKind.Rework && role == "employee")
            {
                Capture(form, "rework-confirmed"); form.Size = form.MinimumSize; Application.DoEvents(); Capture(form, "rework-minimum");
                var actions = Field<FlowLayoutPanel>(form, "flowActions");
                Check(actions.Controls.OfType<Button>().Where(b => b.Visible).All(b => b.Left >= 0 && b.Right <= actions.ClientSize.Width && b.Bottom <= actions.ClientSize.Height), "Target actions fit minimum size");
                Field<Panel>(form, "panelContent").ScrollControlIntoView(Field<Label>(form, "lblSourceNotice")); Application.DoEvents(); Capture(form, "rework-minimum-current-context");
            }
            h.Success(Wire(status, userId, "9007199254740995")); Wait(form.RefreshContextAsync());
            Check(form.CheckedTarget is null && target!.RowVersion == Version, "Reload invalidates current confirmation without mutating earlier snapshot");
            Field<Button>(form, "btnConfirmTarget").PerformClick(); Check(form.CheckedTarget?.RowVersion == "9007199254740995", "New explicit confirmation uses the newly read version");
            form.Close(); Check(form.CheckedTarget is null && h.ProductWrites == 0, "Closing clears preparation and never writes an action/key/receipt");
        }
    }
    private static void Rejections()
    {
        foreach (var (source, current, assignee, refresh) in new[] {
            (TaskDisplayStatus.NotStarted, TaskDisplayStatus.NotStarted, 8, true),
            (TaskDisplayStatus.NotStarted, TaskDisplayStatus.InProgress, 7, true),
            (TaskDisplayStatus.InProgress, TaskDisplayStatus.InProgress, 7, false),
            (TaskDisplayStatus.Submitted, TaskDisplayStatus.Submitted, 7, false),
            (TaskDisplayStatus.Approved, TaskDisplayStatus.Approved, 7, false),
            (TaskDisplayStatus.Returned, TaskDisplayStatus.Returned, 7, false) })
        {
            using var h = new Handler(); using var api = Api(h); h.Success(Wire(current, assignee));
            using var form = new TaskAcceptanceForm(Detail(source), TaskStartKind.Accept, new HttpTaskPreparationService(api.CaptureSession())); Show(form);
            Until(() => Field<Button>(form, "btnReloadContext").Enabled);
            Check(!Field<Button>(form, "btnConfirmTarget").Enabled && form.CheckedTarget is null && form.ShouldRefreshList == refresh && h.ProductWrites == 0, "Nonself, changed and wrong-state targets cannot be confirmed"); form.Close();
        }
        using var handler = new Handler(); using var admin = Api(handler, 2, "admin"); handler.Success(Wire(TaskDisplayStatus.Returned, 7));
        using var managed = new TaskAcceptanceForm(Detail(TaskDisplayStatus.Returned), TaskStartKind.Rework, new HttpTaskPreparationService(admin.CaptureSession())); Show(managed);
        Until(() => Field<Button>(managed, "btnReloadContext").Enabled);
        Check(!Field<Button>(managed, "btnConfirmTarget").Enabled && managed.CheckedTarget is null, "Designated-manager GET permission does not mean assignee action permission"); managed.Close();
    }
    private static void Failures()
    {
        foreach (var (status, code, label) in new[] {
            (401, "AUTH_REQUIRED", "로그인"), (403, "FORBIDDEN", "권한"), (404, "TASK_NOT_FOUND", "찾"),
            (404, "HTTP_404", "연결"), (503, "WRITE_SCHEMA_REQUIRED", "연결"), (503, "DB_UNAVAILABLE", "불러"),
            (409, "TASK_ASSIGNMENT_CHANGED", "배정"), (409, "TASK_VERSION_CONFLICT", "변경") })
        {
            using var h = new Handler(); using var api = Api(h); h.Success(Wire());
            using var form = new TaskAcceptanceForm(Detail(), TaskStartKind.Accept, new HttpTaskPreparationService(api.CaptureSession())); Show(form);
            Until(() => Field<Button>(form, "btnConfirmTarget").Enabled); Field<Button>(form, "btnConfirmTarget").PerformClick(); Check(form.CheckedTarget is not null, "A valid confirmation exists before failure");
            h.Error(status, code); Wait(form.RefreshContextAsync());
            Check(form.CheckedTarget is null && !Field<Button>(form, "btnConfirmTarget").Enabled && Field<Label>(form, "lblPreparationStatus").Text.Contains(label), "Failure clears context/confirmation and explains its category: " + code);
            if (status is 401 or 403) { int count = h.Count; Wait(form.RefreshContextAsync()); Check(h.Count == count, "Authentication/authorization failure prevents reuse of the old form"); }
            Check(h.ProductWrites == 0, "Error handling never issues mutation/key/result requests"); form.Close();
        }
        using var malformedHandler = new Handler(); using var malformedApi = Api(malformedHandler); malformedHandler.Success(Wire(id: "2"));
        using var malformed = new TaskAcceptanceForm(Detail(), TaskStartKind.Accept, new HttpTaskPreparationService(malformedApi.CaptureSession())); Show(malformed);
        Until(() => Field<Button>(malformed, "btnReloadContext").Enabled); Check(malformed.CheckedTarget is null && !Field<Button>(malformed, "btnConfirmTarget").Enabled, "Mismatched server task ID never becomes a current target"); malformed.Close();
    }
    private static void Lifetime()
    {
        using var h = new Handler(); using var api = Api(h); h.Success(Wire());
        using var form = new TaskAcceptanceForm(Detail(), TaskStartKind.Accept, new HttpTaskPreparationService(api.CaptureSession())); Show(form); Until(() => Field<Button>(form, "btnConfirmTarget").Enabled);
        var slow = new TaskCompletionSource<HttpResponseMessage>(); h.Next((_, _) => slow.Task); var older = form.RefreshContextAsync();
        Check(!Field<Button>(form, "btnConfirmTarget").Enabled && form.CheckedTarget is null, "Loading removes the old confirmation immediately");
        int busyCount = h.Count; Field<Button>(form, "btnReloadContext").PerformClick(); Check(h.Count == busyCount, "Disabled reload prevents duplicate clicks");
        h.Success(Wire(version: "5")); Wait(form.RefreshContextAsync()); slow.SetResult(Handler.Response(Wire(version: "4"))); Wait(older);
        Field<Button>(form, "btnConfirmTarget").PerformClick(); Check(form.CheckedTarget?.RowVersion == "5", "Older late response cannot replace a newer context");
        var late = new TaskCompletionSource<HttpResponseMessage>(); h.Next((_, _) => late.Task); var pending = form.RefreshContextAsync();
        api.ClearIdentity(); Login(api, h); late.SetResult(Handler.Response(Wire(version: "6"))); Wait(pending);
        Check(form.CheckedTarget is null && !Field<Button>(form, "btnConfirmTarget").Enabled && Field<TextBox>(form, "txtWriteContext").Text.Length == 0, "Same-account relogin clears IDs/versions and ignores old reply");
        int count = h.Count; Wait(form.RefreshContextAsync()); Check(h.Count == count, "Captured old session cannot send after relogin"); form.Close();
        h.Next((_, _) => { var completion = new TaskCompletionSource<HttpResponseMessage>(); Task.Run(async () => { await Task.Delay(35); completion.SetResult(Handler.Response(Wire())); }); return completion.Task; });
        using var closed = new TaskAcceptanceForm(Detail(), TaskStartKind.Accept, new HttpTaskPreparationService(api.CaptureSession())); Show(closed); closed.Close();
        var until = DateTime.UtcNow.AddMilliseconds(120); while (DateTime.UtcNow < until) { Application.DoEvents(); Thread.Sleep(1); }
        Check(closed.CheckedTarget is null && h.ProductWrites == 0, "Closing during read cancels safely without late target publication");
    }
    private static void ParentButtons()
    {
        using var host = new Form(); var query = new QueryStub(); using var control = new TasksControl(PreviewRole.Administrator, query) { Dock = DockStyle.Fill }; host.Controls.Add(control); Show(host); Wait(control.RefreshAsync());
        var grid = Field<DataGridView>(control, "gridTasks");
        foreach (int index in Enumerable.Range(0, 5))
        {
            grid.ClearSelection(); grid.CurrentCell = grid.Rows[index].Cells[0]; grid.Rows[index].Selected = true;
            Until(() => Field<TaskDetail?>(control, "currentTaskDetail")?.Summary.TaskId == (index + 1).ToString());
            var button = Field<Button>(control, "btnAcceptTask");
            Check(button.Enabled == (index is 0 or 4) && (index != 4 || button.Text.Contains("재작업")), "Parent selects accept/rework only for their matching displayed state");
            if (index == 4) WithModal(() => button.PerformClick(), dialog => Check(Text(dialog).Contains("재작업") && Text(dialog).Contains("5"), "Returned task opens a distinct rework target dialog"));
        }
        Field<ComboBox>(control, "cmbTaskScope").SelectedIndex = 1; Wait(control.RefreshAsync());
        Check(!Field<Button>(control, "btnAcceptTask").Visible && !Field<Button>(control, "btnAcceptTask").Enabled, "Managed scope does not expose acting on another assignee"); host.Close();
    }
    private static void WithModal(Action open, Action<TaskAcceptanceForm> inspect)
    {
        Exception? failure = null; bool found = false; using var timer = new System.Windows.Forms.Timer { Interval = 20 };
        timer.Tick += (_, _) => { if (Application.OpenForms.OfType<TaskAcceptanceForm>().FirstOrDefault() is not { } form) return; timer.Stop(); found = true; try { inspect(form); } catch (Exception e) { failure = e; } finally { form.Close(); } };
        timer.Start(); open(); timer.Stop(); if (failure is not null) throw failure; Check(found, "Real parent button opens the target dialog");
    }
    private static void ParentRefreshLifetime()
    {
        foreach (bool relogin in new[] { false, true })
        {
            using var h = new Handler(); using var api = Api(h); var prep = new HttpTaskPreparationService(api.CaptureSession());
            using var host = new Form(); var query = new QueryStub(); using var control = new TasksControl(PreviewRole.Employee, query, prep) { Dock = DockStyle.Fill }; host.Controls.Add(control); Show(host);
            Field<ComboBox>(control, "cmbTaskStatusFilter").SelectedIndex = 5; Wait(control.RefreshAsync());
            Until(() => Field<TaskDetail?>(control, "currentTaskDetail")?.Summary.TaskId == "5"); int before = query.ListCalls;
            h.Success(Wire(TaskDisplayStatus.InProgress, id: "5"));
            WithModal(() => Field<Button>(control, "btnAcceptTask").PerformClick(), form =>
            {
                Until(() => form.ShouldRefreshList); Check(form.CheckedTarget is null && !Field<Button>(form, "btnConfirmTarget").Enabled, "Changed status cannot reuse the old returned target");
                query.ReturnedBecameInProgress = true;
                if (relogin) { api.ClearIdentity(); Login(api, h); }
            });
            if (relogin)
            {
                Application.DoEvents(); Check(query.ListCalls == before, "Closing an old-session dialog cannot trigger a query under the new login");
            }
            else
            {
                Until(() => query.ListCalls > before && Field<DataGridView>(control, "gridTasks").Rows.Count == 0);
                Check(query.Last?.Status == TaskDisplayStatus.Returned && query.Last.Scope == TaskQueryScope.Own && Field<ComboBox>(control, "cmbTaskStatusFilter").SelectedIndex == 5, "Changed-status close refresh preserves own scope and returned filter");
            }
            Check(h.ProductWrites == 0, "Parent refresh never mutates the task or common request store"); host.Close();
        }
    }
    private static void Capture(Form form, string name)
    {
        if (capture is null) return; Directory.CreateDirectory(capture); using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size)); bitmap.Save(Path.Combine(capture, name + ".png"));
    }
    private static void Live()
    {
        using var doc = JsonDocument.Parse(Console.In.ReadToEnd()); var data = doc.RootElement;
        string Value(string name) => data.GetProperty(name).GetString()!;
        foreach (var (role, userId, first, second) in new[] { ("staff", data.GetProperty("staff_user_id").GetInt32(), "101", "102"), ("admin", data.GetProperty("admin_user_id").GetInt32(), "103", "104") })
        {
            using var h = new ReadOnlyHandler(); using var api = new ApiClient(Value("url"), h); Wait(api.LoginAsync(Value(role + "_login_id"), Value(role + "_password")));
            foreach (var (id, kind, status) in new[] { (first, TaskStartKind.Accept, TaskDisplayStatus.NotStarted), (second, TaskStartKind.Rework, TaskDisplayStatus.Returned) })
            {
                var detail = Wait(new HttpTaskQueryService(api).GetTaskAsync(id));
                using var form = new TaskAcceptanceForm(detail, kind, new HttpTaskPreparationService(api.CaptureSession())); Show(form);
                Until(() => Field<Button>(form, "btnConfirmTarget").Enabled); Field<Button>(form, "btnConfirmTarget").PerformClick();
                Check(form.CheckedTarget is { } target && target.TaskId == id && target.AssigneeId == userId && target.UserId == userId && target.Kind == kind && target.Status == status, "Actual employee/admin current context supports its own target kind");
                Check(form.DialogResult != DialogResult.OK && !form.IsDisposed && h.ProductWrites == 0, "Actual target confirmation does not mutate a task or issue common keys");
                Wait(form.RefreshContextAsync()); Check(form.CheckedTarget is null, "Real context reload invalidates the confirmed target"); form.Close();
            }
            if (role == "admin")
            {
                var detail = Wait(new HttpTaskQueryService(api).GetTaskAsync("105")); using var form = new TaskAcceptanceForm(detail, TaskStartKind.Rework, new HttpTaskPreparationService(api.CaptureSession())); Show(form);
                Until(() => Field<Button>(form, "btnReloadContext").Enabled); Check(!Field<Button>(form, "btnConfirmTarget").Enabled && form.CheckedTarget is null && form.ShouldRefreshList, "Real designated-manager access never grants acting as another assignee"); form.Close();
            }
            Check(h.ProductWrites == 0 && h.ContextGets >= 4, "All real task preparation traffic stays GET-only");
        }
    }
    private sealed class QueryStub : ITaskQueryService
    {
        public string SourceDescription => "명시적 읽기 테스트 대역";
        public int ListCalls; public TaskListQuery? Last; public bool ReturnedBecameInProgress;
        private TaskDetail[] Items => Enum.GetValues<TaskDisplayStatus>().Select((s, i) => Detail(ReturnedBecameInProgress && s == TaskDisplayStatus.Returned ? TaskDisplayStatus.InProgress : s, (i + 1).ToString())).ToArray();
        public Task<IReadOnlyList<TaskSummary>> GetTasksAsync(TaskListQuery query, CancellationToken ct = default)
        { ListCalls++; Last = query; return Task.FromResult<IReadOnlyList<TaskSummary>>(Items.Where(t => query.Status is null || t.Summary.Status == query.Status).Select(t => t.Summary).ToArray()); }
        public Task<TaskDetail> GetTaskAsync(string taskId, CancellationToken ct = default) => Task.FromResult(Items.Single(t => t.Summary.TaskId == taskId));
    }
    private sealed class Handler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> queue = new();
        public int Count, ProductWrites; public string? LastPath, Bearer;
        public void Next(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) => queue.Enqueue(response);
        public void Success(object data) => Next((_, _) => Task.FromResult(Response(data)));
        public void Error(int status, string code) => Next((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(JsonSerializer.Serialize(new { ok = false, error = new { code, message = "합성 오류" }, request_id = "fixture" }), Encoding.UTF8, "application/json") }));
        public static HttpResponseMessage Response(object data) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "fixture" }, ApiClient.Json), Encoding.UTF8, "application/json") };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Count++; LastPath = request.RequestUri!.AbsolutePath; Bearer = request.Headers.Authorization?.Parameter;
            if (request.Method != HttpMethod.Get && LastPath != "/api/v1/auth/login") ProductWrites++;
            if (queue.Count == 0) throw new InvalidOperationException("Unexpected HTTP request in preparation test");
            return queue.Dequeue()(request, ct);
        }
    }
    private sealed class ReadOnlyHandler() : DelegatingHandler(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
    {
        public int ProductWrites, ContextGets;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.AbsolutePath;
            if (request.Method != HttpMethod.Get && path != "/api/v1/auth/login") { ProductWrites++; throw new InvalidOperationException("Read-only scenario attempted a mutation/key/result request"); }
            if (request.Method == HttpMethod.Get && path.EndsWith("/write-context")) ContextGets++;
            return base.SendAsync(request, ct);
        }
    }
}
