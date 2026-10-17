using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
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
        int exit = 1;
        using var coordinator = new Form { ShowInTaskbar = false, Location = new(-20000, -20000), StartPosition = FormStartPosition.Manual, Size = new(1, 1) };
        coordinator.Shown += (_, _) => coordinator.BeginInvoke((Action)(() =>
        {
            try
            {
                if (args.SequenceEqual(new[] { "--live" })) Live();
                else
                {
                    capture = args.FirstOrDefault();
                    Wire(); Malformed(); Receipts(); SessionChanges(); CandidateScreen(); LateResponses(); ParentDialog();
                }
                Console.WriteLine($"PASS: {checks} preparation checks. " + (args.SequenceEqual(new[] { "--live" }) ? "Actual HTTP/MySQL fixture; no product writes." : "Controlled HTTP and actual WinForms; no server/DB."));
                exit = 0;
            }
            catch (Exception error) { Console.Error.WriteLine($"FAIL after {checks}: {error.GetType().Name}: {error.Message}"); }
            finally { coordinator.Close(); }
        }));
        Application.Run(coordinator);
        return exit;
    }
    private static void Check(bool condition, string why) { if (!condition) throw new InvalidOperationException(why); checks++; }
    private static T Wait<T>(Task<T> task)
    {
        var stop = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < stop) { Application.DoEvents(); Thread.Sleep(1); }
        if (!task.IsCompleted) throw new TimeoutException("Preparation check timed out");
        return task.GetAwaiter().GetResult();
    }
    private static void PumpUntil(Func<bool> ready)
    {
        var stop = DateTime.UtcNow.AddSeconds(15);
        while (!ready() && DateTime.UtcNow < stop) { Application.DoEvents(); Thread.Sleep(1); }
        Check(ready(), "UI reached the expected state");
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static ApiClient Api(Handler handler)
    {
        var api = new ApiClient("http://localhost:5000/", handler);
        Login(api, handler);
        return api;
    }
    private static void Login(ApiClient api, Handler handler)
    {
        handler.Success(new { access_token = "synthetic-token", token_type = "Bearer", expires_at = "2030-01-01T00:00:00Z", user = new { id = 2, login_id = "admin", display_name = "관리자", employee_no = "A2", department = "팀", role = "admin", is_active = true } });
        Wait(api.LoginAsync("synthetic", "not-a-real-password"));
    }
    private static object Context(string id = "9007199254740993", string version = "9007199254740994") => new
    { context = new { task_id = id, assignee_id = 7, manager_user_ids = new[] { 2 }, row_version = version, assignment_revision = 3, task_status = "not_started", updated_at = "2026-10-17T00:00:00Z" } };
    private static void Failure(Action action, TaskPreparationFailure expected)
    {
        try { action(); throw new InvalidOperationException("Expected preparation failure"); }
        catch (TaskPreparationException error) { Check(error.Failure == expected, "Expected failure category"); }
    }
    private static void Wire()
    {
        using var h = new Handler(); using var api = Api(h);
        var service = new HttpTaskPreparationService(api.CaptureSession());
        h.Success(new { items = new[] { new { id = 7, display_name = "동명이인", selection_label = "동명이인 · 개발 · #7" } }, page = 2, limit = 1, total = 2 });
        var page = Wait(service.GetCandidatesAsync(new(TaskCandidateKind.Assignee, "9007199254740993", "가&%", 2, 1)));
        Check(page.Items.Single().Id == 7 && page.Total == 2, "Candidate IDs and page metadata survive mapping");
        Check(h.Last!.RequestUri!.AbsoluteUri.Contains("q=%EA%B0%80%26%25") && h.Last.RequestUri.Query.Contains("task_id=9007199254740993"), "Search is escaped and BIGINT is preserved");
        Check(h.Bearer == "synthetic-token", "Uses the captured shared bearer");
        h.Success(Context());
        var context = Wait(service.GetContextAsync("9007199254740993"));
        Check(context.RowVersion == "9007199254740994" && context.AssignmentRevision == 3 && context.UpdatedAt.Offset == TimeSpan.Zero, "Context IDs/versions remain exact strings and UTC");
        Check(context.ManagerUserIds.SequenceEqual(new[] { 2 }) && context.AssigneeId == 7, "Current relationships are separate fields");
        foreach (string invalid in new[] { "0", "01", "-1", "1 ", "9223372036854775808", "opaque:1" })
        {
            int count = h.Count;
            try { Wait(service.GetContextAsync(invalid)); throw new InvalidOperationException("Invalid ID accepted"); }
            catch (ArgumentException) { Check(h.Count == count, "Malformed context IDs are not sent"); }
        }
        h.Success(new { items = Array.Empty<object>(), page = 1, limit = 20, total = 0 });
        Check(Wait(service.GetCandidatesAsync(new(TaskCandidateKind.Manager))).Items.Count == 0, "Empty manager candidates are valid");
        Check(h.Last!.RequestUri!.AbsolutePath == "/api/v1/tasks/manager-candidates", "Manager candidates use their own endpoint");
    }
    private static void Malformed()
    {
        using var h = new Handler(); using var api = Api(h);
        var service = new HttpTaskPreparationService(api.CaptureSession());
        h.Success(Context("1", "01")); Failure(() => Wait(service.GetContextAsync("1")), TaskPreparationFailure.Unavailable);
        h.Success(Context("2")); Failure(() => Wait(service.GetContextAsync("1")), TaskPreparationFailure.Unavailable);
        h.Success(new { items = new[] { new { id = 1, display_name = "가", selection_label = "가#1" }, new { id = 1, display_name = "나", selection_label = "나#1" } }, page = 1, limit = 20, total = 2 });
        Failure(() => Wait(service.GetCandidatesAsync(new(TaskCandidateKind.Assignee))), TaskPreparationFailure.Unavailable);
        h.Success(new { items = Array.Empty<object>(), page = 2, limit = 20, total = 0 });
        Failure(() => Wait(service.GetCandidatesAsync(new(TaskCandidateKind.Assignee))), TaskPreparationFailure.Unavailable);
        foreach (var (status, code, expected) in new[] {
            (403,"FORBIDDEN",TaskPreparationFailure.Forbidden), (404,"TASK_NOT_FOUND",TaskPreparationFailure.NotFound),
            (404,"HTTP_404",TaskPreparationFailure.NotConfigured), (503,"WRITE_SCHEMA_REQUIRED",TaskPreparationFailure.NotConfigured),
            (409,"TASK_ASSIGNMENT_CHANGED",TaskPreparationFailure.AssignmentChanged), (409,"TASK_VERSION_CONFLICT",TaskPreparationFailure.VersionConflict) })
        { h.Error(status, code); Failure(() => Wait(service.GetContextAsync("1")), expected); }
    }
    private static void Receipts()
    {
        using var h = new Handler(); using var api = Api(h);
        var service = new HttpTaskPreparationService(api.CaptureSession());
        string value = new('k', 40);
        h.Success(new { request_key = value, expires_at = "2030-01-01T00:00:00Z" });
        var key = Wait(service.GetRequestKeyAsync(TaskWriteAction.Accept, "1"));
        Check(key.Value == value && !key.ToString().Contains(value), "Key stays usable in memory and is not exposed by ToString");
        Check(h.Body.Contains("task.accepted") && h.Last!.RequestUri!.AbsolutePath == "/api/v1/write-requests/keys", "Key issuance is separate from a mutation route");
        h.Success(new { state = "not_recorded" });
        Check(!Wait(service.GetResultAsync(key)).Recorded && h.Key == value, "Not-recorded lookup preserves the same key without retrying a write");
        h.Success(new { state = "completed", http_status = 200, result = new { task_id = "1", row_version = "2" }, expires_at = "2030-01-01T00:00:00Z" });
        var result = Wait(service.GetResultAsync(key));
        Check(result.Recorded && result.Result!.Value.GetProperty("row_version").GetString() == "2", "A stored result is returned without executing an action");
        h.Error(410, "IDEMPOTENCY_KEY_EXPIRED"); Failure(() => Wait(service.GetResultAsync(key)), TaskPreparationFailure.KeyExpired);
        h.Success(new { state = "processing" }); Failure(() => Wait(service.GetResultAsync(key)), TaskPreparationFailure.Unavailable);
    }
    private static void SessionChanges()
    {
        using var h = new Handler(); using var api = Api(h);
        var old = new HttpTaskPreparationService(api.CaptureSession());
        api.ClearIdentity(); Login(api, h);
        int count = h.Count;
        try { Wait(old.GetCandidatesAsync(new(TaskCandidateKind.Assignee))); throw new InvalidOperationException("Old screen sent as new login"); }
        catch (OperationCanceledException) { Check(h.Count == count && !old.IsCurrent, "Old preparation cannot send with a new session, even the same user"); }
        var current = new HttpTaskPreparationService(api.CaptureSession());
        h.Error(401, "AUTH_REQUIRED"); Failure(() => Wait(current.GetContextAsync("1")), TaskPreparationFailure.AuthenticationExpired);
        Check(api.CurrentUser is null && !current.IsCurrent, "401 clears shared identity and preparation scope");
    }
    private static void Show(Form form) { form.StartPosition = FormStartPosition.Manual; form.Location = new(-15000, -15000); form.Show(); Application.DoEvents(); }
    private static void CandidateScreen()
    {
        using var stub = new Stub(); using var form = new TaskPreparationForm(stub, "1"); Show(form);
        var grid = Field<DataGridView>(form, "gridCandidates");
        PumpUntil(() => grid.Rows.Count == 20 && Field<Button>(form, "btnSearch").Enabled);
        grid.CurrentCell = grid.Rows[0].Cells[0]; grid.Rows[0].Selected = true;
        Field<Button>(form, "btnChoose").PerformClick();
        Field<ComboBox>(form, "cmbKind").SelectedIndex = 1;
        PumpUntil(() => grid.Rows.Count == 20);
        grid.CurrentCell = grid.Rows[2].Cells[0]; grid.Rows[2].Selected = true;
        Field<Button>(form, "btnChoose").PerformClick();
        Field<Button>(form, "btnNext").PerformClick();
        PumpUntil(() => grid.Rows.Count == 1);
        Check(Field<ListBox>(form, "listSelected").Items.Count == 3, "Assignee and multiple manager selections survive paging");
        Field<TextBox>(form, "txtSearch").Text = "empty";
        Field<Button>(form, "btnSearch").PerformClick();
        PumpUntil(() => grid.Rows.Count == 0 && Field<Button>(form, "btnConfirm").Enabled);
        Capture(form, "Selection-Empty-Filter");
        form.Size = form.MinimumSize; Application.DoEvents();
        Capture(form, "Selection-Minimum");
        Check(Field<Button>(form, "btnClose").Bottom <= Field<FlowLayoutPanel>(form, "actionPanel").Height,
            "The close action remains inside its row at minimum size");
        Field<ListBox>(form, "listSelected").SelectedIndex = 2;
        Check(Field<Button>(form, "btnRemove").Enabled, "Selected manager can be removed explicitly");
        Field<Button>(form, "btnRemove").PerformClick();
        Field<Button>(form, "btnConfirm").PerformClick();
        Check(form.Selection?.Assignee?.Id == 1 && form.Selection.ManagerUserIds.SequenceEqual(new[] { 2 }), "Local selection preserves exact IDs without saving");
        Check(stub.KeyCalls == 0, "Candidate screen never issues keys or product mutations");
    }
    private static void LateResponses()
    {
        using var stub = new Stub(); using var form = new TaskPreparationForm(stub, "1"); Show(form);
        var first = new TaskCompletionSource<TaskCandidatePage>();
        stub.Next = _ => first.Task;
        var delayed = form.LoadCandidatesAsync(1);
        stub.Next = _ => Task.FromResult(new TaskCandidatePage(Array.AsReadOnly(new[] { new TaskUserCandidate(99, "최신", "최신#99") }), 1, 20, 1));
        var latest = form.LoadCandidatesAsync(1); Wait(latest.ContinueWith(_ => true));
        first.SetResult(new(Array.AsReadOnly(new[] { new TaskUserCandidate(88, "이전", "이전#88") }), 1, 20, 1));
        Wait(delayed.ContinueWith(_ => true));
        Check((int)Field<DataGridView>(form, "gridCandidates").Rows[0].Cells[0].Value! == 99, "An ignored cancellation cannot overwrite the latest candidates");
        var revoked = new TaskCompletionSource<TaskCandidatePage>(); stub.Next = _ => revoked.Task;
        var pending = form.LoadCandidatesAsync(1); stub.Revoke(); Application.DoEvents();
        revoked.SetResult(new(Array.Empty<TaskUserCandidate>(), 1, 20, 0)); Wait(pending.ContinueWith(_ => true));
        PumpUntil(() => Field<DataGridView>(form, "gridCandidates").Rows.Count == 0);
        Check(!Field<Button>(form, "btnConfirm").Enabled && Field<ListBox>(form, "listSelected").Items.Count == 0, "Session revocation removes context/selection and rejects a late response");
        form.Close();
    }
    private static void ParentDialog()
    {
        using var stub = new Stub(); using var form = new TaskEditForm(false, stub); Show(form);
        using var timer = new System.Windows.Forms.Timer { Interval = 10 };
        var deadline = DateTime.UtcNow.AddSeconds(10);
        timer.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.OfType<TaskPreparationForm>().FirstOrDefault();
            if (DateTime.UtcNow > deadline) { dialog?.Close(); timer.Stop(); return; }
            if (dialog is null || !Field<Button>(dialog, "btnConfirm").Enabled) return;
            var grid = Field<DataGridView>(dialog, "gridCandidates"); grid.CurrentCell = grid.Rows[0].Cells[0]; grid.Rows[0].Selected = true;
            Field<Button>(dialog, "btnChoose").PerformClick();
            Capture(dialog, "New-Task-Candidates");
            Field<Button>(dialog, "btnConfirm").PerformClick(); timer.Stop();
        };
        timer.Start(); Field<Button>(form, "btnPrepareCandidates").PerformClick(); timer.Stop();
        Check(form.PreparedSelection?.Assignee?.Id == 1 && form.CheckedInput is null, "The parent receives a separate unsaved selection without changing CheckedInput");
        Check(Field<ComboBox>(form, "cmbAssignee").Text.Contains("#1"), "The parent displays the server candidate label");
        Capture(form, "Task-Edit-Candidate-Selection");
        stub.Revoke(); Application.DoEvents();
        PumpUntil(() => form.PreparedSelection is null && !Field<Button>(form, "btnPrepareCandidates").Enabled);
        form.Close();
    }
    private static void Capture(Form form, string name)
    {
        if (capture is null) return;
        Directory.CreateDirectory(capture);
        using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(Path.Combine(capture, name + ".png"));
    }
    private static void Live()
    {
        var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(Console.In.ReadToEnd())!;
        using var api = new ApiClient(settings["url"]);
        Wait(api.LoginAsync("admin", settings["admin_password"]));
        var admin = new HttpTaskPreparationService(api.CaptureSession());
        Check(Wait(admin.GetCandidatesAsync(new(TaskCandidateKind.Assignee))).Items.Count >= 2, "Actual assignee candidates");
        Check(Wait(admin.GetCandidatesAsync(new(TaskCandidateKind.Manager))).Items.All(x => x.Id == api.CurrentUser!.Id), "Actual active manager candidates");
        Check(Wait(admin.GetContextAsync("101")).RowVersion == "1", "Actual MySQL context");
        using (var form = new TaskPreparationForm(admin, "101"))
        { Show(form); PumpUntil(() => Field<Button>(form, "btnConfirm").Enabled); Check(Field<DataGridView>(form, "gridCandidates").Rows.Count >= 2, "Actual WinForms candidates via HTTP/MySQL"); form.Close(); }
        using (var host = new Form { Size = new(1100, 800) })
        using (var page = new TasksControl(RemoteWork.WinForms.Ui.PreviewRole.Administrator, new HttpTaskQueryService(api)))
        {
            host.Controls.Add(page); page.Dock = DockStyle.Fill; Show(host);
            Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 1;
            PumpUntil(() => Field<Button>(page, "btnPrepareWrite").Enabled);
            bool opened = false;
            using var timer = new System.Windows.Forms.Timer { Interval = 10 };
            var deadline = DateTime.UtcNow.AddSeconds(10);
            timer.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.OfType<TaskPreparationForm>().FirstOrDefault();
                if (DateTime.UtcNow > deadline) { dialog?.Close(); timer.Stop(); return; }
                if (dialog is null || !Field<Button>(dialog, "btnConfirm").Enabled) return;
                opened = Field<DataGridView>(dialog, "gridCandidates").Rows.Count >= 2;
                dialog.Close(); timer.Stop();
            };
            timer.Start(); Field<Button>(page, "btnPrepareWrite").PerformClick(); timer.Stop();
            Check(opened, "Actual TasksControl discovers the captured preparation service and opens the selected task dialog");
            host.Close();
        }
        Wait(api.LoginAsync("staff", settings["staff_password"]));
        var staff = new HttpTaskPreparationService(api.CaptureSession());
        Check(Wait(staff.GetContextAsync("101")).AssigneeId == api.CurrentUser!.Id, "Actual staff own context");
        Failure(() => Wait(staff.GetCandidatesAsync(new(TaskCandidateKind.Assignee))), TaskPreparationFailure.Forbidden);
        var key = Wait(staff.GetRequestKeyAsync(TaskWriteAction.Accept, "101"));
        Check(!Wait(staff.GetResultAsync(key)).Recorded, "Actual receipt lookup remains not-recorded; no action was executed");
        api.ClearIdentity();
        try { Wait(staff.GetContextAsync("101")); throw new InvalidOperationException("Revoked scope sent"); }
        catch (OperationCanceledException) { Check(true, "Actual captured scope cancels after logout/identity clear"); }
    }
    private sealed class Handler : HttpMessageHandler
    {
        private HttpResponseMessage? next;
        public HttpRequestMessage? Last; public string Body = ""; public string? Bearer; public string? Key; public int Count;
        public void Success(object data) => next = new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "fixture" }), Encoding.UTF8, "application/json") };
        public void Error(int status, string code) => next = new((HttpStatusCode)status) { Content = new StringContent(JsonSerializer.Serialize(new { ok = false, error = new { code, message = "합성 오류" }, request_id = "fixture" }), Encoding.UTF8, "application/json") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Count++; Last = request; Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Bearer = request.Headers.Authorization?.Parameter; Key = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
            var response = next ?? throw new InvalidOperationException("Unexpected HTTP call"); next = null; return response;
        }
    }
    private sealed class Stub : ITaskPreparationService, IDisposable
    {
        private readonly CancellationTokenSource cancellation = new();
        public bool IsCurrent { get; private set; } = true;
        public bool CanSelectCandidates => true;
        public int UserId => 2;
        public CancellationToken SessionCancellation => cancellation.Token;
        public int KeyCalls;
        public Func<TaskCandidateQuery, Task<TaskCandidatePage>>? Next;
        public Task<TaskWriteContext> GetContextAsync(string id, CancellationToken ct = default) => Task.FromResult(new TaskWriteContext(id, 7, Array.AsReadOnly(new[] { 2 }), "1", 1, TaskDisplayStatus.NotStarted, DateTimeOffset.UtcNow));
        public Task<TaskCandidatePage> GetCandidatesAsync(TaskCandidateQuery q, CancellationToken ct = default)
        {
            if (Next is not null) return Next(q);
            var items = q.Search == "empty" ? Array.Empty<TaskUserCandidate>()
                : Enumerable.Range(q.Page == 1 ? 1 : 21, q.Page == 1 ? 20 : 1).Select(id => new TaskUserCandidate(id, "동명이인", "동명이인 · #" + id)).ToArray();
            return Task.FromResult(new TaskCandidatePage(Array.AsReadOnly(items), q.Page, q.Limit, q.Search == "empty" ? 0 : 21));
        }
        public Task<TaskWriteRequestKey> GetRequestKeyAsync(TaskWriteAction action, string? taskId, CancellationToken ct = default) { KeyCalls++; throw new InvalidOperationException("A preparation screen must not issue keys"); }
        public Task<TaskWriteResultLookup> GetResultAsync(TaskWriteRequestKey key, CancellationToken ct = default) => throw new InvalidOperationException("No write from this screen");
        public void Revoke() { IsCurrent = false; cancellation.Cancel(); }
        public void Dispose() => cancellation.Dispose();
    }
}
