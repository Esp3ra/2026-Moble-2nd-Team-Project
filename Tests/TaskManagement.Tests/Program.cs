using System.Net;
using System.Text;
using System.Text.Json;
using System.Reflection;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Features.Tasks.Management;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private static int checks;
    private static readonly DateOnly Day = new(2026, 10, 20);
    private static readonly DateTimeOffset Utc = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Inputs(); HttpContract(); AttachmentContract(); Workflow();
            UiReadFenceAndLayout(args.Length == 2 && args[0] == "--layout-dir" ? Path.GetFullPath(args[1]) : null);
            Console.WriteLine($"PASS TaskManagement controlled: {checks} checks. Explicit HTTP/workflow doubles; no server or DB."); return 0;
        }
        catch (Exception error)
        { Console.Error.WriteLine("FAIL TaskManagement controlled: " + error.GetType().Name + (error is CheckFailure ? ": " + error.Message : "")); return 1; }
    }
    private sealed class CheckFailure(string label) : Exception(label);
    private static void Check(bool value, string label) { if (!value) throw new CheckFailure(label); checks++; }
    private static TaskDetail Detail(TaskDisplayStatus status) => new(new("9", "Original", "Owner", status, Day, Day.AddDays(3)), "Original description");
    private static TaskWriteContext Context(TaskDisplayStatus status, string version = "1") => new("9", 2, new[] { 1, 3 }, version, 1, status, Utc);
    private static TaskManagementRequest Request(TaskManagementKind kind = TaskManagementKind.Update,
        TaskDisplayStatus status = TaskDisplayStatus.NotStarted, string version = "1") => kind switch
    {
        TaskManagementKind.Update => TaskManagementRequest.Update(Detail(status), Context(status, version), 1, " New title ", " body ", Day, Day.AddDays(5)),
        TaskManagementKind.Reassign => TaskManagementRequest.Reassign(Detail(status), Context(status, version), 1, 4),
        _ => TaskManagementRequest.ChangeManagers(Detail(status), Context(status, version), 1, new[] { 3 })
    };
    private static void Invalid(Action action)
    { try { action(); throw new CheckFailure("invalid checked input was accepted"); } catch (ArgumentException) { Check(true, "invalid input rejected"); } }
    private static void Inputs()
    {
        foreach (var status in Enum.GetValues<TaskDisplayStatus>())
            foreach (var kind in Enum.GetValues<TaskManagementKind>())
            {
                bool allowed = kind == TaskManagementKind.ChangeManagers
                    ? status is TaskDisplayStatus.NotStarted or TaskDisplayStatus.InProgress or TaskDisplayStatus.Returned
                    : status == TaskDisplayStatus.NotStarted;
                if (allowed) Check(Request(kind, status).ExpectedStatus == status, "allowed action preserves checked status");
                else Invalid(() => Request(kind, status));
            }
        int[] ids = [3, 1];
        var request = TaskManagementRequest.ChangeManagers(Detail(TaskDisplayStatus.InProgress), Context(TaskDisplayStatus.InProgress), 1, ids);
        ids[0] = 99;
        Check(request.ManagerUserIds.SequenceEqual(new[] { 1, 3 }), "manager selection is copied and canonically sorted");
        Check(Request(TaskManagementKind.ChangeManagers).ManagerUserIds.SequenceEqual(new[] { 3 }), "creator and requester can be removed when another manager remains");
        Invalid(() => TaskManagementRequest.ChangeManagers(Detail(TaskDisplayStatus.NotStarted), Context(TaskDisplayStatus.NotStarted), 1, []));
        Invalid(() => TaskManagementRequest.ChangeManagers(Detail(TaskDisplayStatus.NotStarted), Context(TaskDisplayStatus.NotStarted), 1, [3, 3]));
        Invalid(() => TaskManagementRequest.Reassign(Detail(TaskDisplayStatus.NotStarted), Context(TaskDisplayStatus.NotStarted), 7, 4));
        Invalid(() => TaskManagementRequest.Update(Detail(TaskDisplayStatus.NotStarted), Context(TaskDisplayStatus.NotStarted), 1, "x", "", Day, Day.AddDays(-1)));
        string emoji200 = string.Concat(Enumerable.Repeat("😀", 200));
        Check(TaskManagementRequest.Update(Detail(TaskDisplayStatus.NotStarted), Context(TaskDisplayStatus.NotStarted), 1, emoji200, "", Day, Day).Title == emoji200,
            "title limit counts Unicode scalars and empty description remains valid");
        Invalid(() => TaskManagementRequest.Update(Detail(TaskDisplayStatus.NotStarted), Context(TaskDisplayStatus.NotStarted), 1, emoji200 + "x", "", Day, Day));
        Invalid(() => TaskManagementRequest.Update(Detail(TaskDisplayStatus.NotStarted), Context(TaskDisplayStatus.NotStarted), 1, "x", "\ud800", Day, Day));
    }
    private static void HttpContract()
    {
        using var handler = new HttpFixture();
        using var api = new ApiClient("http://management-fixture.invalid/", handler);
        handler.Success(new { access_token = "fixture-private-token", token_type = "Bearer", expires_at = "2099-01-01T00:00:00Z",
            user = new { id = 1, login_id = "fixture", display_name = "Fixture", employee_no = "TEST-1", department = "Test", role = "admin", is_active = true } });
        api.LoginAsync("fixture", "fixture-only").GetAwaiter().GetResult();
        var service = new HttpTaskManagementService(api.CaptureSession());
        foreach (var kind in Enum.GetValues<TaskManagementKind>())
        {
            var request = Request(kind);
            handler.Success(new { request_key = new string('k', 40), expires_at = "2099-01-01T00:00:00Z" });
            var key = service.GetRequestKeyAsync(request).GetAwaiter().GetResult();
            Check(handler.LastPath == "/api/v1/write-requests/keys", "existing common key endpoint is reused");
            handler.Success(ResultBody(request));
            var result = service.ChangeAsync(request, key).GetAwaiter().GetResult();
            Check(result.RowVersion == "2" && result.AssignmentRevision == (kind == TaskManagementKind.Reassign ? 2 : 1), "success changes only the intended version counters");
            string suffix = kind == TaskManagementKind.Reassign ? "/reassign" : kind == TaskManagementKind.ChangeManagers ? "/managers" : "";
            Check(handler.LastPath == "/api/v1/tasks/9" + suffix && handler.LastMethod == (kind == TaskManagementKind.Reassign ? HttpMethod.Post : HttpMethod.Patch), "correct feature method and relative path");
            var body = JsonDocument.Parse(handler.LastBody!).RootElement;
            var expected = kind == TaskManagementKind.Update
                ? new[] { "expected_row_version", "expected_assignment_revision", "title", "description", "start_date", "due_date" }
                : new[] { "expected_row_version", "expected_assignment_revision", kind == TaskManagementKind.Reassign ? "assignee_id" : "manager_user_ids" };
            Check(body.EnumerateObject().Select(field => field.Name).ToHashSet().SetEquals(expected), "request body contains only the contracted fields");
            Check(body.GetProperty("expected_row_version").GetString() == "1" && body.GetProperty("expected_assignment_revision").GetInt32() == 1
                && handler.LastKey == key.Value, "BIGINT string, INTEGER revision and the same private key are used");
            if (kind == TaskManagementKind.Update) Check(body.GetProperty("title").GetString() == " New title " && body.GetProperty("description").GetString() == " body ", "raw input whitespace is preserved");
            handler.Success(new { state = "not_recorded" });
            Check(!service.GetResultAsync(request, key).GetAwaiter().GetResult().Recorded && handler.LastPath == "/api/v1/write-requests/result", "explicit not_recorded reuses the common result endpoint");
            handler.Success(new { state = "completed", http_status = 200, result = ResultBody(request), expires_at = "2099-01-01T00:00:00Z" });
            Check(service.GetResultAsync(request, key).GetAwaiter().GetResult().Result?.TaskId == "9", "retained result is parsed against this exact request");
        }
        var update = Request(); var savedKey = Key(update);
        foreach (string malformed in new[]
        {
            "{\"task_id\":\"9\",\"row_version\":\"2\",\"assignment_revision\":\"1\",\"task_status\":\"not_started\",\"updated_at\":\"2026-10-20T00:00:00Z\"}",
            "{\"task_id\":\"9\",\"row_version\":\"2\",\"assignment_revision\":1,\"task_status\":\"not_started\",\"updated_at\":\"2026-10-20T00:00:00Z\",\"extra\":1}",
            "{\"task_id\":\"9\",\"task_id\":\"9\",\"row_version\":\"2\",\"assignment_revision\":1,\"task_status\":\"not_started\",\"updated_at\":\"2026-10-20T00:00:00Z\"}"
        })
        {
            handler.Raw("{\"ok\":true,\"data\":" + malformed + ",\"request_id\":\"fixture\"}");
            try { service.ChangeAsync(update, savedKey).GetAwaiter().GetResult(); throw new CheckFailure("malformed success accepted"); }
            catch (TaskManagementException error) { Check(error.Failure == TaskManagementFailure.InvalidResponse, "wrong types, duplicates and undeclared result fields are rejected"); }
        }
        Check(handler.Remaining == 0, "HTTP contract test sends no automatic retry");
    }
    private static object ResultBody(TaskManagementRequest request) => new { task_id = "9", row_version = "2",
        assignment_revision = request.Kind == TaskManagementKind.Reassign ? 2 : 1,
        task_status = request.ExpectedStatus == TaskDisplayStatus.InProgress ? "in_progress" : request.ExpectedStatus == TaskDisplayStatus.Returned ? "returned" : "not_started",
        updated_at = "2026-10-20T00:00:00Z" };
    private static readonly string[] AttachmentIds =
    ["ffffffff-ffff-4fff-8fff-ffffffffffff", "11111111-1111-4111-8111-111111111111"];
    private static TaskManagementRequest AttachmentRequest(IReadOnlyList<string>? ids) =>
        TaskManagementRequest.Update(Detail(TaskDisplayStatus.NotStarted), Context(TaskDisplayStatus.NotStarted), 1,
            "Original", "Original description", Day, Day.AddDays(3), ids);
    private static void AttachmentContract()
    {
        using var handler = new HttpFixture();
        using var api = new ApiClient("http://management-attachments.invalid/", handler);
        handler.Success(new { access_token = "fixture-private-token", token_type = "Bearer", expires_at = "2099-01-01T00:00:00Z",
            user = new { id = 1, login_id = "fixture", display_name = "Fixture", employee_no = "TEST-1", department = "Test", role = "admin", is_active = true } });
        api.LoginAsync("fixture", "fixture-only").GetAwaiter().GetResult();
        var service = new HttpTaskManagementService(api.CaptureSession());
        var mutable = AttachmentIds.ToArray();
        var request = AttachmentRequest(mutable);
        mutable[0] = "22222222-2222-4222-8222-222222222222";
        Check(request.AttachmentIds!.SequenceEqual(AttachmentIds), "attachment-only edit copies IDs without sorting or retaining caller mutation");
        try { ((IList<string>)request.AttachmentIds!)[0] = mutable[0]; throw new CheckFailure("attachment copy was mutable"); }
        catch (NotSupportedException) { Check(true, "frozen attachment list cannot be modified"); }
        foreach (var selection in new IReadOnlyList<string>?[] { null, Array.Empty<string>(), AttachmentIds })
        {
            var selected = AttachmentRequest(selection);
            handler.Success(ResultBody(selected));
            service.ChangeAsync(selected, Key(selected)).GetAwaiter().GetResult();
            using var parsed = JsonDocument.Parse(handler.LastBody!);
            bool present = parsed.RootElement.TryGetProperty("attachment_ids", out var wire);
            Check(present == (selection is not null), "omitted and explicitly empty attachment selections remain distinct on PATCH");
            Check(parsed.RootElement.EnumerateObject().Count() == (selection is null ? 6 : 7), "update contains only agreed optional attachment field");
            if (selection is not null)
                Check(wire.EnumerateArray().Select(x => x.GetString()).SequenceEqual(selection), "wire preserves final ID order and explicit empty list");
        }
        foreach (var kind in new[] { TaskManagementKind.Reassign, TaskManagementKind.ChangeManagers })
            Check(Request(kind).AttachmentIds is null, "other management actions do not carry an attachment selection");
        foreach (string[] invalid in new[]
        {
            new[] { "" }, new[] { (string)null! }, new[] { AttachmentIds[0], AttachmentIds[0] },
            new[] { AttachmentIds[0].ToUpperInvariant() }, new[] { Guid.NewGuid().ToString("N") },
            Enumerable.Range(0, 11).Select(_ => Guid.NewGuid().ToString("D")).ToArray()
        })
        {
            int before = handler.Count; Invalid(() => AttachmentRequest(invalid));
            Check(handler.Count == before, "invalid UUID selection never sends HTTP");
        }
        var ten = AttachmentRequest(Enumerable.Range(0, 10).Select(_ => Guid.NewGuid().ToString("D")).ToArray());
        Check(ten.AttachmentIds?.Count == 10, "maximum ten final files are accepted without introducing a lower limit");
        foreach (var (status, code, failure) in new[]
        {
            (404, "ATTACHMENT_NOT_FOUND", TaskManagementFailure.AttachmentUnavailable),
            (409, "ATTACHMENT_UNAVAILABLE", TaskManagementFailure.AttachmentUnavailable),
            (503, "TASK_ATTACHMENT_REPLACEMENT_NOT_READY", TaskManagementFailure.NotConfigured),
            (503, "SHARED_ATTACHMENT_SCHEMA_REQUIRED", TaskManagementFailure.NotConfigured),
            (503, "SERVER_ERROR", TaskManagementFailure.Unavailable),
            (403, "FORBIDDEN", TaskManagementFailure.Forbidden)
        })
        {
            handler.Error(status, code);
            try { service.ChangeAsync(request, Key(request)).GetAwaiter().GetResult(); throw new CheckFailure("attachment error was accepted"); }
            catch (TaskManagementException error) { Check(error.Failure == failure, "attachment/common error keeps distinct classification: " + code); }
        }
        handler.Success(new { request_key = new string('q', 40), expires_at = "2099-01-01T00:00:00Z" });
        var key = service.GetRequestKeyAsync(request).GetAwaiter().GetResult();
        handler.Error(503, "SERVER_ERROR");
        try { service.ChangeAsync(request, key).GetAwaiter().GetResult(); throw new CheckFailure("server error was accepted"); }
        catch (TaskManagementException error) { Check(error.Failure == TaskManagementFailure.Unavailable, "unknown attachment save is an unavailable result"); }
        string originalBody = handler.LastBody!, originalKey = handler.LastKey!;
        Check(handler.Remaining == 0 && request.AttachmentIds!.SequenceEqual(AttachmentIds),
            "failed service call preserves original attachment body and performs no automatic request");
        handler.Success(new { state = "not_recorded" });
        Check(!service.GetResultAsync(request, key).GetAwaiter().GetResult().Recorded,
            "explicit same-key result query returns not_recorded without changing final IDs");
        handler.Success(ResultBody(request));
        service.ChangeAsync(request, key).GetAwaiter().GetResult();
        Check(handler.LastBody == originalBody && handler.LastKey == originalKey,
            "retry sends exact original final IDs body and key, without reupload or replacement key");
        Check(handler.Remaining == 0, "attachment flow has no unplanned HTTP calls");
    }
    private static TaskWriteRequestKey Key(TaskManagementRequest request) => new(request.Kind switch
        { TaskManagementKind.Update => TaskWriteAction.Update, TaskManagementKind.Reassign => TaskWriteAction.Reassign, _ => TaskWriteAction.ChangeManagers }, "9", new string('k', 40), Utc.AddDays(1));
    private static TaskManagementResult Result(TaskManagementRequest request) => new("9", (long.Parse(request.ExpectedRowVersion) + 1).ToString(),
        request.ExpectedAssignmentRevision + (request.Kind == TaskManagementKind.Reassign ? 1 : 0), request.ExpectedStatus, Utc);
    private static void Workflow()
    {
        using var service = new FakeService(); using var workflow = new TaskManagementWorkflow(service);
        var request = Request();
        service.NextFailure = TaskManagementFailure.Unavailable;
        workflow.SubmitAsync(request).GetAwaiter().GetResult();
        Check(workflow.State == TaskManagementState.NeedsResultCheck && ReferenceEquals(workflow.PendingRequest, request), "unknown result retains the frozen body");
        Check(service.Keys == 1 && service.Changes == 1 && !workflow.CanRetry, "unknown result never automatically issues a new key or retries");
        workflow.RetrySameRequestAsync().GetAwaiter().GetResult();
        Check(service.Changes == 1, "retry is blocked until an explicit not_recorded result");
        workflow.CheckResultAsync().GetAwaiter().GetResult();
        Check(workflow.State == TaskManagementState.RetryAllowed, "explicit not_recorded permits same-request retry");
        workflow.RetrySameRequestAsync().GetAwaiter().GetResult();
        Check(workflow.State == TaskManagementState.Succeeded && service.Keys == 1 && service.Changes == 2
            && ReferenceEquals(service.LastRequest, request) && ReferenceEquals(service.FirstKey, service.LastKey), "same-key retry preserves request and key identity");
        Check(workflow.PendingRequest is not null && workflow.Result?.RowVersion == "2", "known success stays retained before explicit acknowledgement");
        Check(workflow.AcknowledgeSuccess(), "confirmed success can be explicitly acknowledged");
        workflow.SubmitAsync(Request(version: "2")).GetAwaiter().GetResult();
        Check(workflow.State == TaskManagementState.Succeeded && workflow.Result?.RowVersion == "3", "fresh result version can immediately be the next edit target");

        using var forbiddenService = new FakeService(); using var forbidden = new TaskManagementWorkflow(forbiddenService);
        forbiddenService.NextFailure = TaskManagementFailure.Unavailable;
        forbidden.SubmitAsync(Request(TaskManagementKind.ChangeManagers)).GetAwaiter().GetResult();
        forbiddenService.LookupFailure = TaskManagementFailure.Forbidden;
        forbidden.CheckResultAsync().GetAwaiter().GetResult();
        Check(forbidden.State == TaskManagementState.Blocked && forbidden.PendingRequest is not null && forbidden.IsCurrent,
            "lost self-removal response followed by 403 preserves the unknown request for manual confirmation");
        forbidden.RetrySameRequestAsync().GetAwaiter().GetResult();
        Check(forbiddenService.Keys == 1 && forbiddenService.Changes == 1, "blocked authorization result cannot create a replacement key or resend");
        forbiddenService.CancelSession();
        Check(forbidden.State == TaskManagementState.Disconnected && forbidden.PendingRequest is null, "account change clears old pending private data");

        using var expiredService = new FakeService(); using var expired = new TaskManagementWorkflow(expiredService);
        expiredService.NextFailure = TaskManagementFailure.KeyExpired;
        expired.SubmitAsync(Request()).GetAwaiter().GetResult();
        Check(expired.CanCheckResult, "POST key expiry still allows checking a retained result");
        expiredService.LookupFailure = TaskManagementFailure.KeyExpired;
        expired.CheckResultAsync().GetAwaiter().GetResult();
        Check(expired.State == TaskManagementState.Blocked && expired.PendingRequest is not null, "expired result retention blocks a new write and retains manual-review context");

        using var noChangeService = new FakeService(); using var noChange = new TaskManagementWorkflow(noChangeService);
        noChangeService.NextFailure = TaskManagementFailure.NoChange;
        noChange.SubmitAsync(Request()).GetAwaiter().GetResult();
        Check(noChange.State == TaskManagementState.Editing && noChange.Result is null, "definitive no-op does not become an uncertain success");
    }
    private static void UiReadFenceAndLayout(string? layoutDirectory)
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.EnableVisualStyles();
        Control.CheckForIllegalCrossThreadCalls = true;
        using var service = new FakeService();
        using var workflow = new TaskManagementWorkflow(service);
        var preparation = new PreparationFixture("1", "2", "2", "2", "2", "2");
        var queries = new QueryFixture();
        using var form = new TaskManagementForm(TaskManagementKind.Update, Detail(TaskDisplayStatus.NotStarted), queries, preparation, workflow);
        form.Show(); Application.DoEvents();
        Check(Field<TextBox>(form, "txtTitle").Text.Length == 0 && !Field<Button>(form, "btnConfirm").Enabled,
            "context v1 detail old context v2 never becomes editable or checked");
        Check(service.Keys == 0 && service.Changes == 0 && preparation.ContextReads == 2,
            "a mixed-version read is rejected before key or write");
        form.ReloadAsync().GetAwaiter().GetResult();
        Check(Field<TextBox>(form, "txtTitle").Text == "New server title" && !Field<TextBox>(form, "txtTitle").ReadOnly,
            "explicit stable reload binds the new text to its new version");
        Field<Button>(form, "btnConfirm").PerformClick(); Application.DoEvents();
        Check(Field<Button>(form, "btnConfirm").Text == "변경 실행" && service.Changes == 0,
            "first confirmation checks the target without mutation");
        Field<TextBox>(form, "txtTitle").Text = "User edited title";
        Check(Field<Button>(form, "btnConfirm").Text == "입력·대상 확인", "editing input invalidates the checked snapshot");
        Field<Button>(form, "btnConfirm").PerformClick(); Application.DoEvents();
        Check(service.Keys == 0 && preparation.ContextReads == 6, "changed input requires another explicit current-context confirmation");
        form.Size = form.MinimumSize; form.PerformLayout();
        foreach (string button in new[] { "btnReload", "btnConfirm", "btnCheckResult", "btnAcknowledge", "btnClose" })
            VisibleBounds(form, Field<Button>(form, button), "management form minimum layout " + button);
        SaveLayout(form, layoutDirectory, "management-form-min.png");
        Field<Button>(form, "btnConfirm").PerformClick(); Application.DoEvents();
        Check(workflow.State == TaskManagementState.Succeeded && service.Changes == 1 && service.LastRequest?.ExpectedRowVersion == "2",
            "only the distinct execution step submits the checked stable version");
        // Identity replacement can precede notification of its cancellation.
        service.AllowCurrent = false;
        form.GetType().GetMethod("ShowWorkflow", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
        Check(Field<Label>(form, "lblStatus").Text.Contains("접속이 변경")
            && !Field<Label>(form, "lblStatus").Text.Contains("서버 변경 성공"),
            "an old successful snapshot cannot overwrite the session-change message");
        Check(form.ConfirmedResult is null && !form.ShouldRefreshList,
            "invalid session clears the old confirmed result and parent refresh flag");
        form.Close();

        using var page = new TasksControl(PreviewRole.Administrator, new QueryFixture(layout: true), new PreparationFixture());
        using var host = new Form { ClientSize = new Size(940, 650), ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        page.Dock = DockStyle.Fill; host.Controls.Add(page); host.Show();
        page.RefreshAsync().GetAwaiter().GetResult();
        Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 1; Application.DoEvents();
        foreach (string button in new[] { "btnEditTaskPreview", "btnReassignTask", "btnChangeTaskManagers" })
        {
            Button action = Field<Button>(page, button);
            Check(action.Visible && action.Enabled, "management action remains available at the smaller task page " + button);
            VisibleBounds(page, action, "task page action layout " + button);
        }
        SaveLayout(page, layoutDirectory, "tasks-management-small.png");
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static void VisibleBounds(Control parent, Control control, string label)
    {
        Rectangle bounds = parent.RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
        Check(bounds.Left >= 0 && bounds.Top >= 0 && bounds.Right <= parent.ClientSize.Width && bounds.Bottom <= parent.ClientSize.Height, label);
    }
    private static void SaveLayout(Control control, string? directory, string file)
    {
        if (directory is null) return;
        Directory.CreateDirectory(directory);
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        bitmap.Save(Path.Combine(directory, file), System.Drawing.Imaging.ImageFormat.Png);
    }
    private sealed class PreparationFixture(params string[] versions) : ITaskPreparationService
    {
        private readonly Queue<string> versions = new(versions);
        public int ContextReads;
        public bool IsCurrent => true;
        public bool CanSelectCandidates => true;
        public int UserId => 1;
        public CancellationToken SessionCancellation => CancellationToken.None;
        public Task<TaskWriteContext> GetContextAsync(string id, CancellationToken ct = default)
        { ContextReads++; return Task.FromResult(Context(TaskDisplayStatus.NotStarted, versions.Count > 0 ? versions.Dequeue() : "2")); }
        public Task<TaskCandidatePage> GetCandidatesAsync(TaskCandidateQuery query, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<TaskWriteRequestKey> GetRequestKeyAsync(TaskWriteAction action, string? id, CancellationToken ct = default) => throw new InvalidOperationException();
        public Task<TaskWriteResultLookup> GetResultAsync(TaskWriteRequestKey key, CancellationToken ct = default) => throw new InvalidOperationException();
    }
    private sealed class QueryFixture(bool layout = false) : ITaskQueryService, ITaskManagementSource
    {
        private int detailReads;
        public string SourceDescription => "Explicit controlled task fixture";
        public Task<IReadOnlyList<TaskSummary>> GetTasksAsync(TaskListQuery query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<TaskSummary>>(new[] { Detail(TaskDisplayStatus.NotStarted).Summary });
        public Task<TaskDetail> GetTaskAsync(string id, CancellationToken ct = default)
        {
            detailReads++;
            TaskDetail original = Detail(TaskDisplayStatus.NotStarted);
            return Task.FromResult(original with { Summary = original.Summary with { Title = layout || detailReads == 1 ? "Old server title" : "New server title" } });
        }
        public TaskManagementWorkflow? GetManagementWorkflow(TaskManagementKind kind, string id) => null;
        public TaskManagementWorkflow? GetPendingManagementWorkflow() => null;
    }

    private sealed class FakeService : ITaskManagementService, IDisposable
    {
        private readonly CancellationTokenSource session = new();
        public bool AllowCurrent = true;
        public bool IsCurrent => AllowCurrent && !session.IsCancellationRequested;
        public int UserId => 1;
        public CancellationToken SessionCancellation => session.Token;
        public int Keys, Changes;
        public TaskManagementFailure? NextFailure, LookupFailure;
        public TaskManagementRequest? LastRequest;
        public TaskWriteRequestKey? FirstKey, LastKey;
        public Task<TaskWriteRequestKey> GetRequestKeyAsync(TaskManagementRequest request, CancellationToken ct = default)
        { Keys++; FirstKey ??= Key(request); return Task.FromResult(FirstKey); }
        public Task<TaskManagementResult> ChangeAsync(TaskManagementRequest request, TaskWriteRequestKey key, CancellationToken ct = default)
        {
            Changes++; LastRequest = request; LastKey = key;
            if (NextFailure is { } reason) { NextFailure = null; return Task.FromException<TaskManagementResult>(new TaskManagementException(reason)); }
            return Task.FromResult(Result(request));
        }
        public Task<TaskManagementLookup> GetResultAsync(TaskManagementRequest request, TaskWriteRequestKey key, CancellationToken ct = default)
            => LookupFailure is { } reason ? Task.FromException<TaskManagementLookup>(new TaskManagementException(reason)) : Task.FromResult(new TaskManagementLookup(false));
        public void CancelSession() => session.Cancel();
        public void Dispose() => session.Dispose();
    }
    private sealed class HttpFixture : HttpMessageHandler
    {
        private readonly Queue<(int Status, string Json)> queue = new();
        public string? LastPath, LastBody, LastKey;
        public HttpMethod? LastMethod;
        public int Remaining => queue.Count;
        public int Count { get; private set; }
        public void Success(object data) => Raw(JsonSerializer.Serialize(new { ok = true, data, request_id = "fixture" }));
        public void Raw(string json) => queue.Enqueue((200, json));
        public void Error(int status, string code) => queue.Enqueue((status,
            JsonSerializer.Serialize(new { ok = false, error = new { code, message = "합성 오류" }, request_id = "fixture" })));
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Count++;
            LastPath = request.RequestUri!.AbsolutePath; LastMethod = request.Method;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            LastKey = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
            if (queue.Count == 0) throw new CheckFailure("unexpected HTTP request");
            var response = queue.Dequeue();
            return new((HttpStatusCode)response.Status) { Content = new StringContent(response.Json, Encoding.UTF8, "application/json") };
        }
    }
}
