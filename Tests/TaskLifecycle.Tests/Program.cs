using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Lifecycle;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

internal static class Program
{
    private static int checks;
    private static readonly DateOnly Day = new(2098, 10, 20);
    private static readonly DateTimeOffset Utc = new(2098, 10, 20, 0, 0, 0, TimeSpan.Zero);
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Inputs(); Http(); Workflows(); Ui(args.Length == 2 && args[0] == "--layout-dir" ? args[1] : null);
            Console.WriteLine($"PASS TaskLifecycle controlled: {checks} checks. Explicit HTTP/workflow doubles; no actual server or DB."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL TaskLifecycle controlled: " + error); return 1; }
    }
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    private static void Invalid(Action action) { try { action(); } catch (ArgumentException) { Check(true, "invalid input rejected"); return; } throw new Exception("invalid input accepted"); }
    private static TaskExtensionRequest Extension(TaskExtensionStatus status = TaskExtensionStatus.Pending) => new("7", "9", 1, 2, Day, Day.AddDays(3), "deadline reason", status, Utc, status == TaskExtensionStatus.Pending ? null : Utc, status == TaskExtensionStatus.Pending ? null : 1, null);
    private static TaskLifecycleContext Context(TaskDisplayStatus status = TaskDisplayStatus.InProgress, bool pending = false, bool enabled = true) => new("9", "1", 1, status, Day.AddDays(-2), Day,
        Utc, 2, Array.AsReadOnly(new[] { 1, 3 }), false, null, pending ? Extension() : null, enabled);
    private static TaskLifecycleRequest Request(TaskLifecycleKind kind = TaskLifecycleKind.Cancel, TaskDisplayStatus status = TaskDisplayStatus.InProgress) =>
        TaskLifecycleRequest.Create(kind, Context(status, kind is not (TaskLifecycleKind.Cancel or TaskLifecycleKind.RequestExtension)),
            kind is TaskLifecycleKind.RequestExtension or TaskLifecycleKind.WithdrawExtension ? 2 : 1, "raw reason ",
            kind == TaskLifecycleKind.RequestExtension ? Day.AddDays(3) : null,
            kind is TaskLifecycleKind.Cancel or TaskLifecycleKind.RequestExtension ? null : Extension(), Day);
    private static TaskLifecycleKey Key(TaskLifecycleRequest request) => new(request.Kind, request.TaskId, new string('k', 40), Utc.AddYears(1));
    private static TaskLifecycleResult Result(TaskLifecycleRequest request) => new("9", "2", 1, request.ExpectedStatus, Utc,
        request.Kind == TaskLifecycleKind.Cancel ? null : "7", request.RequestedDueDate,
        request.Kind == TaskLifecycleKind.ApproveExtension ? request.RequestedDueDate!.Value : request.OriginalDueDate);
    private static void Inputs()
    {
        foreach (var kind in Enum.GetValues<TaskLifecycleKind>())
            foreach (var status in Enum.GetValues<TaskDisplayStatus>())
            {
                bool allowed = status is TaskDisplayStatus.InProgress or TaskDisplayStatus.Returned || kind == TaskLifecycleKind.Cancel && status == TaskDisplayStatus.NotStarted;
                if (allowed) Check(Request(kind, status).ExpectedStatus == status, "approved state matrix preserves five statuses");
                else Invalid(() => Request(kind, status));
            }
        Invalid(() => TaskLifecycleRequest.Create(TaskLifecycleKind.Cancel, Context(), 2, "reason", koreaToday: Day));
        Invalid(() => TaskLifecycleRequest.Create(TaskLifecycleKind.Cancel, Context(), 1, " ", koreaToday: Day));
        Invalid(() => TaskLifecycleRequest.Create(TaskLifecycleKind.Cancel, Context(), 1, "\ud800", koreaToday: Day));
        Invalid(() => TaskLifecycleRequest.Create(TaskLifecycleKind.Cancel, Context(), 1, new string('가', 20_001), koreaToday: Day));
        Check(TaskLifecycleRequest.Create(TaskLifecycleKind.Cancel, Context(), 1, new string('가', 20_000), koreaToday: Day).Reason.Length == 20_000, "reason boundary matches server UTF-8 60000 byte technical contract");
        Invalid(() => TaskLifecycleRequest.Create(TaskLifecycleKind.Cancel, Context() with { IsCancelled = true }, 1, "reason", koreaToday: Day));
        Invalid(() => TaskLifecycleRequest.Create(TaskLifecycleKind.RequestExtension, Context(pending: true), 2, "reason", Day.AddDays(3), koreaToday: Day));
        Invalid(() => TaskLifecycleRequest.Create(TaskLifecycleKind.RequestExtension, Context() with { ManagerUserIds = new[] { 2 } }, 2, "reason", Day.AddDays(3), koreaToday: Day));
        Invalid(() => TaskLifecycleRequest.Create(TaskLifecycleKind.RequestExtension, Context(), 2, "reason", Day, koreaToday: Day));
        Invalid(() => TaskLifecycleRequest.Create(TaskLifecycleKind.RequestExtension, Context(), 2, "reason", Day.AddDays(3), koreaToday: Day.AddDays(4)));
        Invalid(() => TaskLifecycleRequest.Create(TaskLifecycleKind.ApproveExtension, Context(pending: true) with { ManagerUserIds = new[] { 2 } }, 2, extension: Extension(), koreaToday: Day));
        Invalid(() => TaskLifecycleRequest.Create(TaskLifecycleKind.WithdrawExtension, Context(pending: true), 1, extension: Extension(), koreaToday: Day));
        Invalid(() => TaskLifecycleRequest.Create(TaskLifecycleKind.ApproveExtension, Context(pending: true), 1, extension: Extension() with { AssignmentRevision = 2 }, koreaToday: Day));
        try { TaskLifecycleRequest.Create(TaskLifecycleKind.Cancel, Context(enabled: false), 1, "reason", koreaToday: Day); throw new Exception("technical gate bypassed"); }
        catch (TaskLifecycleException error) { Check(error.Failure == TaskLifecycleFailure.TechnicalConnectionRequired, "technical gate is explicit, not policy uncertainty"); }
        Check(TaskLifecycleRequest.Create(TaskLifecycleKind.Cancel, Context(pending: true), 1, "reason", extension: Extension(), koreaToday: Day).ExtensionId is null, "cancel pending request does not become extension action binding");
        var request = Request(); Check(request.Reason == "raw reason " && request.GetType().GetProperties().All(property => property.SetMethod is null), "checked body preserves raw input and cannot be edited");
    }
    private static object ResultWire(TaskLifecycleRequest request) => new { task_id = "9", row_version = "2", assignment_revision = 1,
        task_status = request.ExpectedStatus == TaskDisplayStatus.Returned ? "returned" : request.ExpectedStatus == TaskDisplayStatus.NotStarted ? "not_started" : "in_progress",
        updated_at = "2098-10-20T00:00:00Z", extension_id = request.Kind == TaskLifecycleKind.Cancel ? null : "7",
        requested_due_date = request.RequestedDueDate?.ToString("yyyy-MM-dd"), due_date = (request.Kind == TaskLifecycleKind.ApproveExtension ? request.RequestedDueDate!.Value : Day).ToString("yyyy-MM-dd") };
    private static object ExtensionWire() => new { extension_id = "7", task_id = "9", assignment_revision = 1, requested_by = 2, original_due_date = "2098-10-20",
        requested_due_date = "2098-10-23", reason = "reason", status = "pending", created_at = "2098-10-20T00:00:00Z", decided_at = (string?)null, decided_by = (int?)null, decision_reason = (string?)null };
    private static object ContextWire(bool pending = false, bool enabled = false) => new { task_id = "9", row_version = "1", assignment_revision = 1, task_status = "in_progress",
        start_date = "2098-10-18", due_date = "2098-10-20", updated_at = "2098-10-20T00:00:00Z", assignee_id = 2, manager_user_ids = new[] { 1, 3 },
        is_cancelled = false, cancellation = (object?)null, pending_extension = pending ? ExtensionWire() : null, write_enabled = enabled };
    private static void Http()
    {
        using var handler = new HttpFixture(); using var api = new ApiClient("http://lifecycle-fixture.invalid/", handler);
        handler.Success(new { access_token = "fixture-private-token", token_type = "Bearer", expires_at = "2099-01-01T00:00:00Z",
            user = new { id = 1, login_id = "fixture", display_name = "Fixture", employee_no = "TEST-1", department = "Test", role = "admin", is_active = true } });
        api.LoginAsync("fixture", "fixture-only").GetAwaiter().GetResult();
        var service = new HttpTaskLifecycleService(api.CaptureSession());
        var oversized = TaskLifecycleRequest.Create(TaskLifecycleKind.Cancel, Context(), 1, new string('a', 40_000), koreaToday: Day);
        int beforeOversize = handler.Calls;
        try { service.GetRequestKeyAsync(oversized).GetAwaiter().GetResult(); throw new Exception("oversize body issued request key"); }
        catch (TaskLifecycleException error) { Check(error.Failure == TaskLifecycleFailure.InvalidInput && handler.Calls == beforeOversize, "JSON envelope limit rejected before issuing a server request key"); }
        try { service.ChangeAsync(oversized, Key(oversized)).GetAwaiter().GetResult(); throw new Exception("oversize body sent mutation"); }
        catch (TaskLifecycleException error) { Check(error.Failure == TaskLifecycleFailure.InvalidInput && handler.Calls == beforeOversize, "same JSON transport bound rejects direct mutation without HTTP"); }
        foreach (var item in new[] {
            (HttpStatusCode.NotFound, "TASK_EXTENSION_NOT_FOUND", TaskLifecycleFailure.NotFound),
            (HttpStatusCode.ServiceUnavailable, "DATABASE_UNAVAILABLE", TaskLifecycleFailure.Unavailable),
            (HttpStatusCode.ServiceUnavailable, "TASK_LIFECYCLE_INTEGRATION_REQUIRED", TaskLifecycleFailure.TechnicalConnectionRequired) })
        {
            handler.Error(item.Item1, item.Item2);
            try { service.GetContextAsync("9").GetAwaiter().GetResult(); throw new Exception("error mapped to success"); }
            catch (TaskLifecycleException error) { Check(error.Failure == item.Item3, "not found, DB outage and technical readiness remain distinct"); }
        }
        handler.Success(new { context = ContextWire(pending: true) });
        var context = service.GetContextAsync("9").GetAwaiter().GetResult();
        Check(context.PendingExtension?.ExtensionId == "7" && !context.WriteEnabled && context.ManagerUserIds.SequenceEqual(new[] { 1, 3 }), "readonly lifecycle context keeps technical gate and current relations");
        handler.Success(new { items = new[] { ExtensionWire() } });
        Check(service.GetExtensionsAsync(TaskQueryScope.Managed, TaskExtensionStatus.Pending).GetAwaiter().GetResult().Count == 1
            && handler.LastUri!.Query == "?scope=managed&status=pending", "inbox scope and explicit status contract");
        foreach (var kind in Enum.GetValues<TaskLifecycleKind>())
        {
            // Service identity must match the immutable request's checked actor.
            var request = kind is TaskLifecycleKind.RequestExtension or TaskLifecycleKind.WithdrawExtension
                ? TaskLifecycleRequest.Create(kind, Context(pending: kind == TaskLifecycleKind.WithdrawExtension) with { AssigneeId = 1,
                    PendingExtension = kind == TaskLifecycleKind.WithdrawExtension ? Extension() with { RequestedBy = 1 } : null }, 1, "raw reason ",
                    kind == TaskLifecycleKind.RequestExtension ? Day.AddDays(3) : null,
                    kind == TaskLifecycleKind.WithdrawExtension ? Extension() with { RequestedBy = 1 } : null, Day)
                : Request(kind);
            handler.Success(new { request_key = new string('k', 40), expires_at = "2099-01-01T00:00:00Z" });
            var key = service.GetRequestKeyAsync(request).GetAwaiter().GetResult();
            var keyBody = JsonDocument.Parse(handler.LastBody!).RootElement;
            Check(keyBody.GetProperty("target").GetString() == "9" && keyBody.GetProperty("action").GetString()!.Contains(kind == TaskLifecycleKind.Cancel ? "cancelled" : "deadline_extension"), "common keys use task target and explicit member2 action string");
            handler.Success(ResultWire(request)); var result = service.ChangeAsync(request, key).GetAwaiter().GetResult();
            string suffix = kind switch { TaskLifecycleKind.Cancel => "/cancel", TaskLifecycleKind.RequestExtension => "/deadline-extensions", TaskLifecycleKind.ApproveExtension => "/deadline-extensions/7/approve", TaskLifecycleKind.RejectExtension => "/deadline-extensions/7/reject", _ => "/deadline-extensions/7/withdraw" };
            Check(handler.LastUri!.AbsolutePath == "/api/v1/tasks/9" + suffix && handler.LastMethod == HttpMethod.Post && handler.LastKey == key.Value, "mutation uses exact route, POST and private idempotency key");
            var body = JsonDocument.Parse(handler.LastBody!).RootElement;
            string[] fields = kind switch { TaskLifecycleKind.Cancel or TaskLifecycleKind.RejectExtension => ["expected_row_version", "expected_assignment_revision", "reason"],
                TaskLifecycleKind.RequestExtension => ["expected_row_version", "expected_assignment_revision", "requested_due_date", "reason"], _ => ["expected_row_version", "expected_assignment_revision"] };
            Check(body.EnumerateObject().Select(property => property.Name).ToHashSet().SetEquals(fields), "exact body keeps extension identity in route only");
            Check(result.Status == request.ExpectedStatus && result.AssignmentRevision == 1 && result.RowVersion == "2", "success increments row version only and keeps status");
            handler.Success(new { state = "not_recorded" });
            Check(!service.GetResultAsync(request, key).GetAwaiter().GetResult().Recorded, "sparse not_recorded from existing common server is accepted");
            handler.Success(new { state = "completed", http_status = 200, result = ResultWire(request), expires_at = "2099-01-01T00:00:00Z" });
            Check(service.GetResultAsync(request, key).GetAwaiter().GetResult().Recorded, "retained result matched to frozen request");
        }
        var cancel = Request();
        foreach (string malformed in new[] {
            "{\"task_id\":\"9\",\"row_version\":\"2\",\"assignment_revision\":1,\"task_status\":\"cancelled\",\"updated_at\":\"2098-10-20T00:00:00Z\",\"extension_id\":null,\"requested_due_date\":null,\"due_date\":\"2098-10-20\"}",
            "{\"task_id\":\"9\",\"task_id\":\"9\",\"row_version\":\"2\",\"assignment_revision\":1,\"task_status\":\"in_progress\",\"updated_at\":\"2098-10-20T00:00:00Z\",\"extension_id\":null,\"requested_due_date\":null,\"due_date\":\"2098-10-20\"}",
            "{\"task_id\":\"9\",\"row_version\":\"2\",\"assignment_revision\":1,\"task_status\":\"in_progress\",\"updated_at\":\"2098-10-20T00:00:00Z\",\"extension_id\":\"7\",\"requested_due_date\":null,\"due_date\":\"2098-10-20\"}" })
        {
            handler.Raw("{\"ok\":true,\"data\":" + malformed + ",\"request_id\":\"fixture\"}");
            try { service.ChangeAsync(cancel, Key(cancel)).GetAwaiter().GetResult(); throw new Exception("invalid success accepted"); }
            catch (TaskLifecycleException error) { Check(error.Failure == TaskLifecycleFailure.InvalidResponse, "undeclared sixth status, duplicate and wrong nullable result rejected"); }
        }
        var queries = new HttpTaskQueryService(api);
        object OldTask() => new { task_id = "9", title = "Task", assignee_id = 2, assignee_name = "Fixture", task_status = "in_progress", start_date = "2098-10-18", due_date = "2098-10-20", description = "read" };
        object CancelledTask() => new { task_id = "9", title = "Task", assignee_id = 2, assignee_name = "Fixture", task_status = "in_progress", start_date = "2098-10-18", due_date = "2098-10-20", description = "read", is_cancelled = true };
        handler.Success(new { items = new[] { OldTask() } });
        Check(!queries.GetTasksAsync(new(TaskQueryScope.Own)).GetAwaiter().GetResult()[0].IsCancelled && !handler.LastUri!.Query.Contains("include_cancelled"), "old server missing cancellation flag stays compatible and default query omits opt-in");
        handler.Success(new { items = new[] { CancelledTask() } });
        Check(queries.GetTasksAsync(new(TaskQueryScope.Own, IncludeCancelled: true)).GetAwaiter().GetResult()[0].IsCancelled && handler.LastUri!.Query.Contains("include_cancelled=true"), "only explicit include-cancelled query accepts cancelled task");
        handler.Success(new { items = new[] { CancelledTask() } });
        try { queries.GetTasksAsync(new(TaskQueryScope.Own)).GetAwaiter().GetResult(); throw new Exception("unexpected cancelled row accepted"); } catch (TaskQueryException) { Check(true, "false filter rejects cancelled server row"); }
        handler.Success(new { task = CancelledTask() });
        var detail = queries.GetTaskAsync("9").GetAwaiter().GetResult();
        Check(detail.Summary.IsCancelled && detail.Summary.Status == TaskDisplayStatus.InProgress, "cancelled detail remains readable with original five-state value");
        Check(handler.Remaining == 0, "all HTTP calls are explicit without automatic retries");
        var retained = TaskLifecycleSessionStore.Get(api, TaskLifecycleKind.Cancel, "9")!;
        handler.Success(new { request_key = new string('k', 40), expires_at = "2099-01-01T00:00:00Z" });
        handler.Raw("{\"ok\":false,\"error\":{\"code\":\"TEST_UNKNOWN\",\"message\":\"fixture\"},\"request_id\":\"fixture\"}");
        retained.SubmitAsync(cancel).GetAwaiter().GetResult();
        Check(retained.State == TaskLifecycleState.NeedsResultCheck && ReferenceEquals(TaskLifecycleSessionStore.FindPending(api), retained)
            && ReferenceEquals(TaskLifecycleSessionStore.Get(api, TaskLifecycleKind.Cancel, "9"), retained), "same-login navigation retains pending workflow and recovery needs no selected task row");
        handler.Success(new { access_token = "next-fixture-token", token_type = "Bearer", expires_at = "2099-01-01T00:00:00Z",
            user = new { id = 3, login_id = "next", display_name = "Next", employee_no = "TEST-3", department = "Test", role = "admin", is_active = true } });
        api.LoginAsync("next", "fixture-only").GetAwaiter().GetResult();
        Check(retained.State == TaskLifecycleState.Disconnected && retained.PendingRequest is null && TaskLifecycleSessionStore.FindPending(api) is null, "new login cannot recover former user's request body");
    }
    private static void Workflows()
    {
        using var service = new FakeService(); using var workflow = new TaskLifecycleWorkflow(service); var request = Request();
        service.NextFailure = TaskLifecycleFailure.Unavailable; workflow.SubmitAsync(request).GetAwaiter().GetResult();
        Check(workflow.State == TaskLifecycleState.NeedsResultCheck && ReferenceEquals(workflow.PendingRequest, request) && !workflow.CanRetry, "lost mutation response retains immutable request and blocks retry");
        workflow.RetrySameRequestAsync().GetAwaiter().GetResult(); Check(service.Changes == 1, "no retry before result check");
        workflow.CheckResultAsync().GetAwaiter().GetResult(); Check(workflow.CanRetry, "explicit not_recorded permits retry");
        workflow.RetrySameRequestAsync().GetAwaiter().GetResult();
        Check(workflow.State == TaskLifecycleState.Succeeded && service.Keys == 1 && service.Changes == 2 && ReferenceEquals(service.FirstKey, service.LastKey), "same frozen key/body survives explicit retry");
        Check(workflow.AcknowledgeSuccess() && workflow.PendingRequest is null, "success only cleared by explicit acknowledgement");
        using var forbiddenService = new FakeService(); using var forbidden = new TaskLifecycleWorkflow(forbiddenService);
        forbiddenService.NextFailure = TaskLifecycleFailure.Unavailable; forbidden.SubmitAsync(Request()).GetAwaiter().GetResult();
        forbiddenService.LookupFailure = TaskLifecycleFailure.Forbidden; forbidden.CheckResultAsync().GetAwaiter().GetResult();
        Check(forbidden.State == TaskLifecycleState.Blocked && forbidden.PendingRequest is not null && !forbidden.CanRetry, "current permission loss retains unknown request for manual confirmation");
        forbiddenService.CancelSession(); Check(forbidden.State == TaskLifecycleState.Disconnected && forbidden.PendingRequest is null, "session replacement clears old private request");
        using var delayed = new FakeService(); using var delayedWorkflow = new TaskLifecycleWorkflow(delayed);
        delayed.Delayed = new(TaskCreationOptions.RunContinuationsAsynchronously); var running = delayedWorkflow.SubmitAsync(Request());
        delayed.CancelSession(); delayed.Delayed.SetResult(Result(request)); running.GetAwaiter().GetResult();
        Check(delayedWorkflow.State == TaskLifecycleState.Disconnected && delayedWorkflow.Result is null, "late success after session expiry cannot revive old UI state");
        using var gated = new FakeService(); using var gatedWorkflow = new TaskLifecycleWorkflow(gated);
        gated.NextFailure = TaskLifecycleFailure.TechnicalConnectionRequired; gatedWorkflow.SubmitAsync(Request()).GetAwaiter().GetResult();
        Check(gatedWorkflow.State == TaskLifecycleState.Editing && gatedWorkflow.Result is null, "explicit backend technical gate never reports success");
    }
    private static void Ui(string? directory)
    {
        Application.EnableVisualStyles(); Control.CheckForIllegalCrossThreadCalls = true;
        using var gatedService = new FakeService { CurrentContext = Context(enabled: false) }; using var gatedWorkflow = new TaskLifecycleWorkflow(gatedService);
        using var gatedForm = new TaskLifecycleForm(TaskLifecycleKind.Cancel, "9", gatedService, gatedWorkflow);
        gatedForm.Show(); Application.DoEvents();
        Check(Field<Label>(gatedForm, "lblStatus").Text.Contains("기술 연결 필요") && !Field<Button>(gatedForm, "btnConfirm").Enabled && gatedService.Changes == 0, "connected gate-off form is readable and honest without a fake write");
        gatedForm.Size = gatedForm.MinimumSize; Application.DoEvents();
        Fits(gatedForm, Field<Button>(gatedForm, "btnClose"), "minimum-size lifecycle action remains visible");
        Save(gatedForm, directory, "lifecycle-technical-gate.png");
        string longReason = string.Join("\r\n", Enumerable.Range(1, 30).Select(index => "상세 사유 " + index));
        gatedService.CurrentContext = Context(pending: true, enabled: false) with { PendingExtension = Extension() with { Reason = longReason } };
        gatedForm.ReloadAsync().GetAwaiter().GetResult(); Application.DoEvents();
        Fits(gatedForm, Field<Button>(gatedForm, "btnClose"), "30-line lifecycle context cannot push minimum-size action outside window");
        Check(Field<TextBox>(gatedForm, "txtContext").Text.Contains("상세 사유 30") && Field<TextBox>(gatedForm, "txtContext").ScrollBars == ScrollBars.Vertical,
            "long lifecycle reason remains fully readable in bounded scroll pane");
        Save(gatedForm, directory, "lifecycle-long-reason-minimum.png"); gatedForm.Close();
        using var service = new FakeService(); using var workflow = new TaskLifecycleWorkflow(service);
        using var form = new TaskLifecycleForm(TaskLifecycleKind.Cancel, "9", service, workflow);
        form.Show(); Application.DoEvents(); Field<TextBox>(form, "txtReason").Text = "explicit cancel";
        Field<Button>(form, "btnConfirm").PerformClick(); Application.DoEvents();
        Check(Field<Button>(form, "btnConfirm").Text == "실행" && service.Changes == 0, "input check is separate from mutation execution");
        Field<Button>(form, "btnConfirm").PerformClick(); Application.DoEvents();
        Check(workflow.State == TaskLifecycleState.Succeeded && service.Changes == 1 && form.ShouldRefreshList, "explicit second execution shows only confirmed server success");
        service.AllowCurrent = false;
        form.GetType().GetMethod("ShowWorkflow", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(form, null);
        Check(Field<Label>(form, "lblStatus").Text.Contains("접속이 변경") && !Field<Label>(form, "lblStatus").Text.Contains("서버 처리 성공")
            && form.ConfirmedResult is null && !form.ShouldRefreshList, "identity change before cancellation callback cannot restore stale successful snapshot");
        service.CancelSession(); Application.DoEvents();
        Check(Field<TextBox>(form, "txtReason").Text.Length == 0 && form.ConfirmedResult is null && !form.ShouldRefreshList, "session expiry clears draft and old success flag"); form.Close();
        using var inboxService = new FakeService { CurrentContext = Context(pending: true) };
        using var inbox = new TaskExtensionRequestsForm(inboxService, new FakeSource(inboxService), TaskQueryScope.Managed);
        inbox.Show(); Application.DoEvents();
        Check(Field<DataGridView>(inbox, "gridRequests").Rows.Count == 1 && Field<Button>(inbox, "btnApprove").Enabled, "designated manager inbox can choose another user's pending request");
        Check(Field<TextBox>(inbox, "txtDetail").Text.Contains("deadline reason"), "automatically selected first row immediately shows its actual request reason");
        inbox.Size = inbox.MinimumSize; Application.DoEvents();
        Fits(inbox, Field<Button>(inbox, "btnResult"), "minimum-size request inbox recovery remains visible");
        Save(inbox, directory, "lifecycle-inbox-loaded.png");
        inboxService.ExtensionRows = new[] { Extension() with { Reason = longReason } }; inbox.ReloadAsync().GetAwaiter().GetResult(); Application.DoEvents();
        Fits(inbox, Field<Button>(inbox, "btnResult"), "30-line inbox reason cannot push minimum-size recovery outside window");
        Check(Field<TextBox>(inbox, "txtDetail").Text.Contains("상세 사유 30") && Field<TextBox>(inbox, "txtDetail").ScrollBars == ScrollBars.Vertical,
            "long inbox reason remains fully readable in bounded scroll pane");
        Save(inbox, directory, "lifecycle-inbox-long-reason-minimum.png");
        Field<ComboBox>(inbox, "cmbScope").SelectedIndex = 0; Application.DoEvents();
        Check(Field<DataGridView>(inbox, "gridRequests").Rows.Count == 0 && !Field<Button>(inbox, "btnApprove").Enabled, "scope change clears previous rows before explicit refresh");
        inboxService.CancelSession(); Application.DoEvents(); Check(Field<DataGridView>(inbox, "gridRequests").Rows.Count == 0, "session change clears inbox rows");
        Save(inbox, directory, "lifecycle-inbox.png"); inbox.Close();
        using var alreadyExpired = new FakeService(); using var expiredInbox = new TaskExtensionRequestsForm(alreadyExpired, new FakeSource(alreadyExpired), TaskQueryScope.Managed);
        alreadyExpired.CancelSession(); expiredInbox.Show(); Application.DoEvents();
        Check(Field<Label>(expiredInbox, "lblStatus").Text.Contains("접속이 변경") && Field<DataGridView>(expiredInbox, "gridRequests").Rows.Count == 0, "inbox opened after identity invalidation cannot show stale rows"); expiredInbox.Close();
        using var pendingService = new FakeService(); using var pendingWorkflow = new TaskLifecycleWorkflow(pendingService);
        pendingService.NextFailure = TaskLifecycleFailure.Unavailable; pendingWorkflow.SubmitAsync(Request()).GetAwaiter().GetResult();
        using var pendingInbox = new TaskExtensionRequestsForm(pendingService, new FakeSource(pendingService) { Pending = pendingWorkflow }, TaskQueryScope.Managed);
        pendingInbox.Show(); Application.DoEvents();
        Check(!Field<Button>(pendingInbox, "btnApprove").Enabled && !Field<Button>(pendingInbox, "btnReject").Enabled
            && Field<Button>(pendingInbox, "btnResult").Enabled, "unknown pending lifecycle request blocks new inbox writes while preserving result recovery"); pendingInbox.Close();
        using var page = new TasksControl(PreviewRole.Administrator, new CancelledQuery());
        using var host = new Form { ClientSize = new Size(940, 650), ShowInTaskbar = false };
        page.Dock = DockStyle.Fill; host.Controls.Add(page); host.Show(); page.RefreshAsync().GetAwaiter().GetResult();
        Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 1; Application.DoEvents();
        Check(!Field<Button>(page, "btnCancelTask").Enabled && !Field<Button>(page, "btnEditTaskPreview").Enabled && !Field<Button>(page, "btnReassignTask").Enabled && !Field<Button>(page, "btnChangeTaskManagers").Enabled, "cancelled current detail disables task mutation entries");
        Check(Field<TextBox>(page, "txtContent").Text.Contains("업무 취소"), "cancellation displayed separately without changing task status");
        Check(!Field<Button>(page, "btnTaskRequests").Visible && Field<Button>(page, "btnLifecycleRequests").Visible, "connected lifecycle view hides old sample combined request form and exposes real extension inbox");
        foreach (string name in new[] { "btnLifecycleRequests", "chkIncludeCancelled" })
        { Control control = Field<Control>(page, name); Rectangle rectangle = page.RectangleToClient(control.RectangleToScreen(control.ClientRectangle)); Check(rectangle.Right <= page.Width && rectangle.Bottom <= page.Height && rectangle.Top >= 0, "new entry fits existing Designer layout " + name); }
        Save(page, directory, "tasks-lifecycle.png"); host.Close();
    }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Fits(Control parent, Control control, string label)
    { Rectangle rectangle = parent.RectangleToClient(control.RectangleToScreen(control.ClientRectangle)); Check(rectangle.Left >= 0 && rectangle.Top >= 0 && rectangle.Right <= parent.ClientSize.Width && rectangle.Bottom <= parent.ClientSize.Height, label); }
    private static void Save(Control control, string? directory, string name)
    { if (directory is null) return; Directory.CreateDirectory(directory); using var bitmap = new Bitmap(control.Width, control.Height); control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size)); bitmap.Save(Path.Combine(directory, name)); }
    private sealed class CancelledQuery : ITaskQueryService, ITaskLifecycleSource, RemoteWork.WinForms.Features.Tasks.Management.ITaskManagementSource
    {
        public string SourceDescription => "Explicit cancelled-task fixture";
        public Task<IReadOnlyList<TaskSummary>> GetTasksAsync(TaskListQuery query, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TaskSummary>>(new[] { new TaskSummary("9", "Cancelled", "Fixture", TaskDisplayStatus.InProgress, Day.AddDays(-2), Day, true) });
        public Task<TaskDetail> GetTaskAsync(string id, CancellationToken ct = default) => Task.FromResult(new TaskDetail(new("9", "Cancelled", "Fixture", TaskDisplayStatus.InProgress, Day.AddDays(-2), Day, true), "Historical content"));
        public ITaskLifecycleService? CreateLifecycleService() => null;
        public TaskLifecycleWorkflow? GetLifecycleWorkflow(TaskLifecycleKind kind, string id, string? extensionId = null) => null;
        public TaskLifecycleWorkflow? GetPendingLifecycleWorkflow() => null;
        public RemoteWork.WinForms.Features.Tasks.Management.TaskManagementWorkflow? GetManagementWorkflow(RemoteWork.WinForms.Features.Tasks.Management.TaskManagementKind kind, string taskId) => null;
        public RemoteWork.WinForms.Features.Tasks.Management.TaskManagementWorkflow? GetPendingManagementWorkflow() => null;
    }
    private sealed class FakeSource(FakeService service) : ITaskLifecycleSource
    {
        public TaskLifecycleWorkflow? Pending;
        public ITaskLifecycleService CreateLifecycleService() => service;
        public TaskLifecycleWorkflow GetLifecycleWorkflow(TaskLifecycleKind kind, string taskId, string? extensionId = null) => new(service);
        public TaskLifecycleWorkflow? GetPendingLifecycleWorkflow() => Pending;
    }
    private sealed class FakeService : ITaskLifecycleService, IDisposable
    {
        private readonly CancellationTokenSource session = new();
        public bool AllowCurrent = true;
        public bool IsCurrent => AllowCurrent && !session.IsCancellationRequested;
        public int UserId => 1; public bool IsAdministrator => true;
        public CancellationToken SessionCancellation => session.Token;
        public int Keys, Changes; public TaskLifecycleFailure? NextFailure, LookupFailure;
        public TaskLifecycleKey? FirstKey, LastKey; public TaskCompletionSource<TaskLifecycleResult>? Delayed;
        public TaskLifecycleContext CurrentContext = Context();
        public IReadOnlyList<TaskExtensionRequest> ExtensionRows = new[] { Extension() };
        public Task<TaskLifecycleContext> GetContextAsync(string id, CancellationToken ct = default) => Task.FromResult(CurrentContext);
        public Task<IReadOnlyList<TaskExtensionRequest>> GetExtensionsAsync(TaskQueryScope scope, TaskExtensionStatus? status = null, CancellationToken ct = default) => Task.FromResult(ExtensionRows);
        public Task<TaskLifecycleKey> GetRequestKeyAsync(TaskLifecycleRequest request, CancellationToken ct = default) { Keys++; FirstKey ??= Key(request); return Task.FromResult(FirstKey); }
        public Task<TaskLifecycleResult> ChangeAsync(TaskLifecycleRequest request, TaskLifecycleKey key, CancellationToken ct = default)
        {
            Changes++; LastKey = key;
            if (NextFailure is { } failure) { NextFailure = null; return Task.FromException<TaskLifecycleResult>(new TaskLifecycleException(failure)); }
            return Delayed?.Task ?? Task.FromResult(Result(request));
        }
        public Task<TaskLifecycleLookup> GetResultAsync(TaskLifecycleRequest request, TaskLifecycleKey key, CancellationToken ct = default) => LookupFailure is { } failure
            ? Task.FromException<TaskLifecycleLookup>(new TaskLifecycleException(failure)) : Task.FromResult(new TaskLifecycleLookup(false));
        public void CancelSession() => session.Cancel(); public void Dispose() => session.Dispose();
    }
    private sealed class HttpFixture : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Json)> queue = new(); public Uri? LastUri; public string? LastBody, LastKey; public HttpMethod? LastMethod; public int Calls;
        public int Remaining => queue.Count;
        public void Success(object data) => Raw(JsonSerializer.Serialize(new { ok = true, data, request_id = "fixture" }));
        public void Raw(string json) => queue.Enqueue((HttpStatusCode.OK, json));
        public void Error(HttpStatusCode status, string code) => queue.Enqueue((status, JsonSerializer.Serialize(new { ok = false, error = new { code, message = "fixture" }, request_id = "fixture" })));
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++; LastUri = request.RequestUri; LastMethod = request.Method; LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            LastKey = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
            if (queue.Count == 0) throw new Exception("Unexpected HTTP fixture request");
            var response = queue.Dequeue();
            return new(response.Status) { Content = new StringContent(response.Json, Encoding.UTF8, "application/json") };
        }
    }
}
