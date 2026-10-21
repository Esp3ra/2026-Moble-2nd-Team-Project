using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Creation;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

internal static class Program
{
    private static int checks;
    private static string? capture;
    private static readonly DateOnly Start = new(2026, 10, 19), Due = new(2026, 10, 25);
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
                else
                {
                    capture = args.FirstOrDefault();
                    Wire(); InvalidBodies(); AttachmentContract(); AttachmentBodyLimit(); AttachmentRecovery(); AttachmentFailures();
                    Recovery(); DefiniteFailures(); MalformedResults(); SessionsAndConcurrency(); Screens(); NavigationRecovery(); RefreshViews();
                }
                Console.WriteLine(args.SequenceEqual(new[] { "--http" })
                    ? $"PASS TaskCreation HTTP: {checks} checks."
                    : $"PASS TaskCreation controlled: {checks} checks; actual WinForms with controlled HTTP, no server/DB.");
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
        var stop = DateTime.UtcNow.AddSeconds(35);
        while (!task.IsCompleted && DateTime.UtcNow < stop) { Application.DoEvents(); Thread.Sleep(1); }
        if (!task.IsCompleted) throw new TimeoutException("Creation operation timed out");
        task.GetAwaiter().GetResult();
    }
    private static T Wait<T>(Task<T> task) { Wait((Task)task); return task.GetAwaiter().GetResult(); }
    private static void Until(Func<bool> ready)
    {
        var stop = DateTime.UtcNow.AddSeconds(35);
        while (!ready() && DateTime.UtcNow < stop) { Application.DoEvents(); Thread.Sleep(1); }
        Check(ready(), "UI reaches the expected state");
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(owner, value);
    private static void Show(Form form) { form.StartPosition = FormStartPosition.Manual; form.Location = new(-15000, -15000); form.Show(); Application.DoEvents(); }
    private static object Result(string id = "9007199254740993", string version = "1", int revision = 1, string status = "not_started", string updated = "2026-10-19T00:00:00Z") =>
        new { task_id = id, row_version = version, assignment_revision = revision, task_status = status, updated_at = updated };
    private static object Key() => new { request_key = new string('k', 40), expires_at = "2030-01-01T00:00:00Z" };
    private static TaskCreationRequest Request(string title = "  업무😀  ", int assignee = 7, int creator = 2, int additional = 3) => new(title, "\n 원문 설명 \n", Start, Due, assignee, Array.AsReadOnly(new[] { creator, additional }));
    private static ApiClient Api(Handler handler)
    {
        var api = new ApiClient("http://localhost:5000/", handler); Login(api, handler); return api;
    }
    private static void Login(ApiClient api, Handler handler)
    {
        handler.Success(new { access_token = "synthetic-token", token_type = "Bearer", expires_at = "2030-01-01T00:00:00Z", user = new { id = 2, login_id = "admin", display_name = "관리자", employee_no = "A2", department = "팀", role = "admin", is_active = true } });
        Wait(api.LoginAsync("synthetic", "not-a-real-password"));
    }
    private static void Wire()
    {
        using var h = new Handler(); using var api = Api(h);
        var service = new HttpTaskCreationService(api.CaptureSession());
        h.Success(Key()); var key = Wait(service.GetRequestKeyAsync());
        Check(h.Last!.RequestUri!.AbsolutePath == "/api/v1/write-requests/keys" && h.Body.Contains("task.created"), "Creation requests a scoped common key");
        h.Success(Result(), 201); var result = Wait(service.CreateAsync(Request(), key));
        Check(result.TaskId == "9007199254740993" && result.RowVersion == "1" && result.AssignmentRevision == 1 && result.Status == TaskDisplayStatus.NotStarted, "BIGINT string and initial result remain exact");
        using var json = JsonDocument.Parse(h.Body); var body = json.RootElement;
        Check(body.EnumerateObject().Count() == 6 && body.GetProperty("title").GetString() == "  업무😀  " && body.GetProperty("description").GetString() == "\n 원문 설명 \n", "Exactly six fields preserve Unicode and raw text");
        Check(body.GetProperty("start_date").GetString() == "2026-10-19" && body.GetProperty("manager_user_ids").EnumerateArray().Select(v => v.GetInt32()).SequenceEqual(new[] { 2, 3 }), "Dates and selected IDs are sent explicitly");
        Check(h.Last.RequestUri.AbsolutePath == "/api/v1/tasks" && h.Bearer == "synthetic-token" && h.Key == key.Value, "Common bearer and idempotency header are reused");
        var ids = new List<int> { 2, 3 }; var copied = new TaskCreationRequest("제목", "", Start, Due, 7, ids); ids.Clear();
        Check(copied.ManagerUserIds.SequenceEqual(new[] { 2, 3 }), "Checked manager IDs are immutable copies");
    }
    private static void InvalidBodies()
    {
        foreach (Action action in new Action[] {
            () => Request(" "), () => Request(string.Concat(Enumerable.Repeat("😀", 201))), () => Request("\ud800"),
            () => new TaskCreationRequest("제목", "\udfff", Start, Due, 7, new[] { 2 }),
            () => new TaskCreationRequest("제목", "", Due, Start, 7, new[] { 2 }),
            () => new TaskCreationRequest("제목", "", new DateOnly(999, 1, 1), Due, 7, new[] { 2 }),
            () => new TaskCreationRequest("제목", "", Start, Due, 0, new[] { 2 }),
            () => new TaskCreationRequest("제목", "", Start, Due, 7, new[] { 2, 2 }),
            () => new TaskCreationRequest("제목", "", Start, Due, 7, new[] { -1 }) })
        {
            try { action(); throw new InvalidOperationException("Invalid checked input accepted"); }
            catch (ArgumentException) { checks++; }
        }
        Check(Request(string.Concat(Enumerable.Repeat("😀", 200))).Title.Length == 400, "200 Unicode codepoints are supported");
        using var h = new Handler(); using var api = Api(h); var service = new HttpTaskCreationService(api.CaptureSession());
        h.Success(Key()); var key = Wait(service.GetRequestKeyAsync()); int count = h.Count;
        foreach (var request in new[] { new TaskCreationRequest("제목", "", Start, Due, 7, new[] { 3 }), new TaskCreationRequest("제목", new string('a', 33000), Start, Due, 7, new[] { 2 }) })
        {
            try { Wait(service.CreateAsync(request, key)); throw new InvalidOperationException("Invalid body sent"); }
            catch (TaskCreationException ex) { Check(ex.Failure == TaskCreationFailure.InvalidInput && h.Count == count, "Missing creator and oversized body are rejected before POST"); }
        }
    }
    private static readonly string[] AttachmentFixtureIds =
        ["ffffffff-ffff-ffff-ffff-ffffffffffff", "11111111-1111-1111-1111-111111111111"];
    private static TaskCreationRequest AttachmentRequest(IReadOnlyList<string>? ids, string description = "") =>
        new("Attachment fixture", description, Start, Due, 7, new[] { 2, 3 }, attachmentIds: ids);

    private static void AttachmentContract()
    {
        using var h = new Handler(); using var api = Api(h);
        var service = new HttpTaskCreationService(api.CaptureSession());
        h.Success(Key()); var key = Wait(service.GetRequestKeyAsync());
        h.Success(Result(), 201); Wait(service.CreateAsync(AttachmentRequest(null), key));
        using (var json = JsonDocument.Parse(h.Body))
            Check(!json.RootElement.TryGetProperty("attachment_ids", out _) && json.RootElement.EnumerateObject().Count() == 6,
                "Null attachment selection preserves the original six-field wire body");
        h.Success(Result(), 201); Wait(service.CreateAsync(AttachmentRequest(Array.Empty<string>()), key));
        using (var json = JsonDocument.Parse(h.Body))
            Check(json.RootElement.GetProperty("attachment_ids").GetArrayLength() == 0
                && json.RootElement.EnumerateObject().Count() == 7, "Explicit empty attachment selection is sent as an empty array");

        var input = new List<string>(AttachmentFixtureIds);
        var request = AttachmentRequest(input);
        input.Reverse(); input.Add("22222222-2222-2222-2222-222222222222");
        Check(request.AttachmentIds is not null && request.AttachmentIds.SequenceEqual(AttachmentFixtureIds),
            "Checked attachment IDs are copied without reordering or following mutable caller input");
        try
        {
            ((IList<string>)request.AttachmentIds!)[0] = input[0];
            throw new InvalidOperationException("Checked attachment IDs were mutable");
        }
        catch (NotSupportedException) { checks++; }
        h.Success(Result(), 201); Wait(service.CreateAsync(request, key));
        using (var json = JsonDocument.Parse(h.Body))
            Check(json.RootElement.GetProperty("attachment_ids").EnumerateArray().Select(x => x.GetString()).SequenceEqual(AttachmentFixtureIds),
                "Attachment wire preserves the selected canonical UUID strings and original array order");

        foreach (var invalid in new IReadOnlyList<string>[]
        {
            new[] { "" }, new[] { "not-a-uuid" }, new[] { "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF" },
            new[] { "ffffffffffffffffffffffffffffffff" }, new[] { "{ffffffff-ffff-ffff-ffff-ffffffffffff}" },
            new[] { " ffffffff-ffff-ffff-ffff-ffffffffffff" }, new string[] { null! },
            new[] { AttachmentFixtureIds[0], AttachmentFixtureIds[0] },
            Enumerable.Range(1, 11).Select(i => $"00000000-0000-0000-0000-{i:D12}").ToArray()
        })
        {
            int count = h.Count;
            try { _ = AttachmentRequest(invalid); throw new InvalidOperationException("Invalid attachment selection accepted"); }
            catch (ArgumentException) { Check(h.Count == count, "Invalid/duplicate/null or excessive attachment IDs fail before HTTP"); }
        }
        var ten = Enumerable.Range(1, 10).Select(i => $"00000000-0000-0000-0000-{i:D12}").ToArray();
        h.Success(Result(), 201); Wait(service.CreateAsync(AttachmentRequest(ten), key));
        using (var json = JsonDocument.Parse(h.Body))
            Check(json.RootElement.GetProperty("attachment_ids").GetArrayLength() == 10, "Ten final attachment IDs remain allowed");
    }

    private static void AttachmentBodyLimit()
    {
        using var h = new Handler(); using var api = Api(h);
        var service = new HttpTaskCreationService(api.CaptureSession());
        h.Success(Key()); var key = Wait(service.GetRequestKeyAsync());
        h.Success(Result(), 201); Wait(service.CreateAsync(AttachmentRequest(null), key));
        string boundaryDescription = new('a', 32 * 1024 - Encoding.UTF8.GetByteCount(h.Body));
        h.Success(Result(), 201); Wait(service.CreateAsync(AttachmentRequest(null, boundaryDescription), key));
        Check(Encoding.UTF8.GetByteCount(h.Body) == 32 * 1024, "Existing common 32KiB creation body boundary is unchanged");
        int count = h.Count;
        try
        {
            Wait(service.CreateAsync(AttachmentRequest(AttachmentFixtureIds, boundaryDescription), key));
            throw new InvalidOperationException("Attachment payload bypassed the common body limit");
        }
        catch (TaskCreationException error)
        {
            Check(error.Failure == TaskCreationFailure.InvalidInput && h.Count == count,
                "Attachment array bytes count toward the same 32KiB limit before POST");
        }
    }

    private static void AttachmentRecovery()
    {
        foreach (bool recorded in new[] { true, false })
        {
            using var h = new Handler(); using var api = Api(h);
            using var flow = new TaskCreationWorkflow(new HttpTaskCreationService(api.CaptureSession()));
            var input = new List<string>(AttachmentFixtureIds);
            var request = AttachmentRequest(input);
            h.Success(Key()); h.Drop(); Wait(flow.SubmitAsync(request));
            string originalBody = h.Body, originalKey = h.Key!;
            input.Clear(); input.Add("22222222-2222-2222-2222-222222222222");
            Check(flow.State == TaskCreationState.NeedsResultCheck && ReferenceEquals(flow.PendingRequest, request)
                && flow.PendingRequest.AttachmentIds!.SequenceEqual(AttachmentFixtureIds),
                "Uncertain creation keeps the frozen final IDs after caller list changes");
            int count = h.Count;
            Wait(flow.SubmitAsync(AttachmentRequest(input))); Wait(flow.RetryAsync());
            Check(h.Count == count && h.Posts == 1, "Uncertain attachments do not permit a new body/key or unverified retry");
            if (recorded)
            {
                h.Success(new { state = "completed", http_status = 201, result = Result(), expires_at = "2030-01-01T00:00:00Z" });
                Wait(flow.CheckResultAsync());
                Check(flow.State == TaskCreationState.Succeeded && h.Posts == 1 && h.Key == originalKey
                    && flow.PendingRequest!.AttachmentIds!.SequenceEqual(AttachmentFixtureIds),
                    "Recorded success is recovered without reupload, replacement IDs or another create POST");
                count = h.Count;
                Wait(flow.CheckResultAsync()); Wait(flow.RetryAsync()); Wait(flow.SubmitAsync(AttachmentRequest(input)));
                Check(h.Count == count && flow.State == TaskCreationState.Succeeded
                    && flow.PendingRequest!.AttachmentIds!.SequenceEqual(AttachmentFixtureIds),
                    "Verified receipt success cannot trigger another lookup, retry or replacement request before explicit reset");
            }
            else
            {
                h.Success(new { state = "not_recorded" }); Wait(flow.CheckResultAsync());
                Check(flow.State == TaskCreationState.RetryAllowed && h.Posts == 1,
                    "Not-recorded attachment lookup requires an explicit retry");
                h.Success(Result(), 201); Wait(flow.RetryAsync());
                Check(flow.State == TaskCreationState.Succeeded && h.Posts == 2
                    && h.Key == originalKey && h.Body == originalBody,
                    "Explicit retry preserves the original UUID order, whole body and request key byte for byte");
            }
        }
    }

    private static void AttachmentFailures()
    {
        foreach (var (status, code, expectedState, expectedFailure) in new[]
        {
            (404, "ATTACHMENT_NOT_FOUND", TaskCreationState.Editing, TaskCreationFailure.AttachmentUnavailable),
            (409, "ATTACHMENT_UNAVAILABLE", TaskCreationState.Editing, TaskCreationFailure.AttachmentUnavailable),
            (409, "SHARED_ATTACHMENT_DATA_INCONSISTENT", TaskCreationState.ManualReview, TaskCreationFailure.Conflict),
            (503, "SHARED_ATTACHMENT_SCHEMA_REQUIRED", TaskCreationState.NeedsResultCheck, TaskCreationFailure.Unavailable),
            (503, "RESULTS_STORAGE_REQUIRED", TaskCreationState.NeedsResultCheck, TaskCreationFailure.Unavailable),
            (503, "TASK_ATTACHMENT_PROVIDER_REQUIRED", TaskCreationState.NeedsResultCheck, TaskCreationFailure.Unavailable),
            (403, "FORBIDDEN", TaskCreationState.Disconnected, TaskCreationFailure.Forbidden),
            (401, "AUTH_REQUIRED", TaskCreationState.Disconnected, TaskCreationFailure.AuthenticationExpired)
        })
        {
            using var h = new Handler(); using var api = Api(h);
            using var flow = new TaskCreationWorkflow(new HttpTaskCreationService(api.CaptureSession()));
            var request = AttachmentRequest(AttachmentFixtureIds);
            h.Success(Key()); h.Error(status, code); Wait(flow.SubmitAsync(request));
            Check(flow.State == expectedState && flow.Failure == expectedFailure,
                "Actual attachment/common error code keeps its distinct recovery policy: " + code);
            if (expectedState == TaskCreationState.Disconnected)
                Check(flow.PendingRequest is null, "Common authentication/permission rejection retains existing captured-session cleanup");
            else
                Check(ReferenceEquals(flow.PendingRequest, request)
                    && flow.PendingRequest.AttachmentIds!.SequenceEqual(AttachmentFixtureIds),
                    "Rejected or uncertain file selection remains frozen and is never automatically replaced");
            int count = h.Count;
            Wait(flow.RetryAsync());
            Check(h.Count == count && h.Posts == 1,
                "Attachment failure does not issue an automatic retry, discard or replacement request");
            if (expectedFailure == TaskCreationFailure.AttachmentUnavailable)
                Check(flow.Message.Contains("첨부") && !flow.Message.Contains("연결이 활성화되지"),
                    "Missing/unready attachment is not mislabeled as an unconfigured task route");
            if (expectedState == TaskCreationState.NeedsResultCheck)
            {
                h.Success(new { state = "completed", http_status = 201, result = Result(), expires_at = "2030-01-01T00:00:00Z" });
                Wait(flow.CheckResultAsync());
                Check(flow.State == TaskCreationState.Succeeded && h.Posts == 1,
                    "Uncertain attachment server failure can recover a retained success without reexecuting creation");
            }
        }
    }

    private static void Recovery()
    {
        using var h = new Handler(); using var api = Api(h); using var flow = new TaskCreationWorkflow(new HttpTaskCreationService(api.CaptureSession()));
        h.Success(Key()); h.Drop(); Wait(flow.SubmitAsync(Request()));
        string body = h.Body, key = h.Key!; int count = h.Count;
        Check(flow.State == TaskCreationState.NeedsResultCheck && flow.PendingRequest is not null && !flow.CanRetry, "Lost response retains checked request and requires lookup");
        Wait(flow.SubmitAsync(Request("다른 제목"))); Check(h.Count == count, "Uncertain request cannot issue a fresh key or POST");
        h.Success(new { state = "completed", http_status = 201, result = Result(), expires_at = "2030-01-01T00:00:00Z" }); Wait(flow.CheckResultAsync());
        Check(flow.State == TaskCreationState.Succeeded && flow.CreatedResult?.TaskId == "9007199254740993" && h.Key == key, "Stored result completes without resend");
        Check(h.Posts == 1 && flow.ResetAfterSuccess(), "Only one create POST; explicit handoff resets successful state");
        h.Success(Key()); h.Drop(); Wait(flow.SubmitAsync(Request()));
        body = h.Body; key = h.Key!; count = h.Count;
        h.Success(new { state = "not_recorded" }); Wait(flow.CheckResultAsync());
        Check(flow.State == TaskCreationState.RetryAllowed && flow.CanRetry && h.Posts == 2, "Not-recorded lookup enables explicit retry without automatic POST");
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel(); Wait(flow.RetryAsync(cancellation.Token));
            Check(h.Posts == 2 && flow.State == TaskCreationState.NeedsResultCheck && flow.PendingRequest is not null, "Cancelled retry preserves the uncertain body/key without a POST");
        }
        h.Success(new { state = "not_recorded" }); Wait(flow.CheckResultAsync());
        h.Success(Result("2"), 201); Wait(flow.RetryAsync());
        Check(flow.State == TaskCreationState.Succeeded && h.Key == key && h.Body == body && h.Count == count + 3, "Explicit retry uses identical key and checked body");
    }
    private static void DefiniteFailures()
    {
        foreach (var (status, code, state, failure) in new[] {
            (409, "ASSIGNEE_UNAVAILABLE", TaskCreationState.Editing, TaskCreationFailure.AssigneeUnavailable),
            (409, "TASK_MANAGER_UNAVAILABLE", TaskCreationState.Editing, TaskCreationFailure.ManagerUnavailable),
            (403, "FORBIDDEN", TaskCreationState.Disconnected, TaskCreationFailure.Forbidden),
            (401, "AUTH_REQUIRED", TaskCreationState.Disconnected, TaskCreationFailure.AuthenticationExpired),
            (503, "DB_UNAVAILABLE", TaskCreationState.NeedsResultCheck, TaskCreationFailure.Unavailable),
            (410, "IDEMPOTENCY_KEY_EXPIRED", TaskCreationState.NeedsResultCheck, TaskCreationFailure.KeyExpired),
            (409, "IDEMPOTENCY_KEY_REUSED", TaskCreationState.ManualReview, TaskCreationFailure.KeyReused) })
        {
            using var h = new Handler(); using var api = Api(h); using var flow = new TaskCreationWorkflow(new HttpTaskCreationService(api.CaptureSession()));
            h.Success(Key()); h.Error(status, code); Wait(flow.SubmitAsync(Request()));
            Check(flow.State == state && flow.Failure == failure, "HTTP failure has the correct safe state: " + code);
            if (state == TaskCreationState.Disconnected) Check(flow.PendingRequest is null, "Disconnected session clears pending body");
            else if (state == TaskCreationState.Editing) Check(flow.PendingRequest is not null && !flow.CanRetry, "Definite candidate rejection preserves editable input without retry");
            else Check(flow.PendingRequest is not null && !flow.CanRetry, "Uncertain/expired request is retained without a fresh write");
        }
        using var handler = new Handler(); using var client = Api(handler); using var workflow = new TaskCreationWorkflow(new HttpTaskCreationService(client.CaptureSession()));
        handler.Error(404, "HTTP_404"); Wait(workflow.SubmitAsync(Request()));
        Check(workflow.State == TaskCreationState.Editing && handler.Posts == 0, "Key endpoint unavailable does not send a task or pretend uncertain storage");
        handler.Success(Key()); handler.Drop(); Wait(workflow.SubmitAsync(Request()));
        handler.Error(410, "IDEMPOTENCY_KEY_EXPIRED"); Wait(workflow.CheckResultAsync());
        int count = handler.Count; Wait(workflow.SubmitAsync(Request())); Wait(workflow.RetryAsync());
        Check(workflow.State == TaskCreationState.ManualReview && handler.Count == count, "Expired uncertain result prohibits retry and fresh registration");
        using var retainedHandler = new Handler(); using var retainedApi = Api(retainedHandler); using var retained = new TaskCreationWorkflow(new HttpTaskCreationService(retainedApi.CaptureSession()));
        retainedHandler.Success(Key()); retainedHandler.Error(410, "IDEMPOTENCY_KEY_EXPIRED"); Wait(retained.SubmitAsync(Request()));
        Check(retained.CanCheckResult && !retained.CanRetry, "Expired POST key still permits retained result lookup");
        retainedHandler.Success(new { state = "completed", http_status = 201, result = Result(), expires_at = "2030-01-01T00:00:00Z" }); Wait(retained.CheckResultAsync());
        Check(retained.State == TaskCreationState.Succeeded && retainedHandler.Posts == 1, "Expired POST can recover a retained success without a new key or POST");
    }
    private static void MalformedResults()
    {
        foreach (object result in new[] { Result("01"), Result("9223372036854775808"), Result(version: "2"), Result(revision: 2), Result(status: "in_progress"), Result(updated: "2026-10-19T09:00:00+09:00") })
        {
            using var h = new Handler(); using var api = Api(h); using var flow = new TaskCreationWorkflow(new HttpTaskCreationService(api.CaptureSession()));
            h.Success(Key()); h.Success(result, 201); Wait(flow.SubmitAsync(Request()));
            Check(flow.State == TaskCreationState.NeedsResultCheck && flow.CreatedResult is null, "Malformed success cannot be claimed as created");
        }
        using var handler = new Handler(); using var client = Api(handler); using var workflow = new TaskCreationWorkflow(new HttpTaskCreationService(client.CaptureSession()));
        handler.Success(Key()); handler.Drop(); Wait(workflow.SubmitAsync(Request()));
        handler.Success(new { state = "completed", http_status = 200, result = Result(), expires_at = "2030-01-01T00:00:00Z" }); Wait(workflow.CheckResultAsync());
        Check(workflow.CreatedResult is null && workflow.State == TaskCreationState.NeedsResultCheck, "Creation receipt must retain HTTP201");
    }
    private static void SessionsAndConcurrency()
    {
        using var h = new Handler(); using var api = Api(h); using var flow = new TaskCreationWorkflow(new HttpTaskCreationService(api.CaptureSession()));
        var delayed = new TaskCompletionSource<HttpResponseMessage>(); h.Success(Key()); h.Next((_, _) => delayed.Task);
        Task first = flow.SubmitAsync(Request()); Until(() => h.Posts == 1); Wait(flow.SubmitAsync(Request("중복")));
        Check(h.Posts == 1 && flow.IsBusy, "Concurrent clicks issue only one POST");
        api.ClearIdentity(); Login(api, h); delayed.SetResult(Handler.Response(Result(), 201)); Wait(first);
        Check(flow.State == TaskCreationState.Disconnected && flow.CreatedResult is null && flow.PendingRequest is null, "Late success from replaced login cannot update or retain prior body");
        int count = h.Count; Wait(flow.SubmitAsync(Request())); Check(h.Count == count, "Old workflow cannot send under the new login");
    }
    private static void Fill(TaskEditForm form, string title, int assignee = 7, int creator = 2, int additional = 3)
    {
        Field<TextBox>(form, "txtTaskTitle").Text = title;
        Field<TextBox>(form, "txtTaskDescription").Text = "\n 원문 설명 \n";
        foreach (string name in new[] { "dtpStartDate", "dtpDeadline" }) { var picker = Field<DateTimePicker>(form, name); picker.Value = (name == "dtpStartDate" ? Start : Due).ToDateTime(TimeOnly.MinValue); picker.Checked = true; }
        Set(form, "preparedSelection", new TaskPreparationSelection(null, new TaskUserCandidate(assignee, "테스트 담당자", "테스트 담당자 · #" + assignee), Array.AsReadOnly(new[] { creator, additional })));
        Field<Button>(form, "btnConfirm").PerformClick();
    }
    private static void Screens()
    {
        using var h = new Handler(); using var api = Api(h); var prep = new HttpTaskPreparationService(api.CaptureSession());
        using var flow = new TaskCreationWorkflow(new HttpTaskCreationService(api.CaptureSession()));
        using (var form = new TaskEditForm(prep, flow))
        {
            Show(form); Fill(form, "화면 등록"); Check(Field<Button>(form, "btnCreateTask").Enabled && h.Posts == 0, "Input confirmation prepares but does not create");
            Field<TextBox>(form, "txtTaskTitle").Text += " 변경";
            Check(!Field<Button>(form, "btnCreateTask").Enabled && form.CheckedInput is null, "Changed checked text invalidates registration");
            Field<Button>(form, "btnConfirm").PerformClick(); h.Success(Key()); h.Drop(); Field<Button>(form, "btnCreateTask").PerformClick();
            Until(() => flow.State == TaskCreationState.NeedsResultCheck && Field<Button>(form, "btnCheckCreationResult").Enabled);
            Check(!Field<TextBox>(form, "txtTaskTitle").Enabled && !Field<Button>(form, "btnRetryCreation").Enabled, "Unknown result freezes input and disables unchecked retry");
            Capture(form, "creation-result-unknown"); form.Size = form.MinimumSize; Application.DoEvents(); Capture(form, "creation-minimum");
            var actions = Field<FlowLayoutPanel>(form, "flowActions2");
            Check(actions.Controls.OfType<Button>().Where(b => b.Visible).All(b => b.Bottom <= actions.ClientSize.Height && b.Right <= actions.ClientSize.Width && b.Left >= 0), "Visible actions fit the minimum-size form");
            form.Close(); Check(!form.Visible && flow.PendingRequest is not null, "Closing an uncertain form retains its workflow");
        }
        using (var reopened = new TaskEditForm(prep, flow))
        {
            Show(reopened); Check(Field<TextBox>(reopened, "txtTaskTitle").Text == "화면 등록 변경" && !Field<TextBox>(reopened, "txtTaskTitle").Enabled, "Reopening restores the immutable pending body");
            h.Success(new { state = "not_recorded" }); Field<Button>(reopened, "btnCheckCreationResult").PerformClick(); Until(() => Field<Button>(reopened, "btnRetryCreation").Enabled);
            h.Success(Result("3"), 201); Field<Button>(reopened, "btnRetryCreation").PerformClick(); Until(() => reopened.CreatedResult is not null);
            Check(reopened.DialogResult == DialogResult.OK && !reopened.Visible && h.Posts == 2, "Explicit retry closes with confirmed success only");
        }
        flow.ResetAfterSuccess();
        using (var busy = new TaskEditForm(prep, flow))
        {
            Show(busy); Fill(busy, "처리중"); var delayed = new TaskCompletionSource<HttpResponseMessage>(); h.Success(Key()); h.Next((_, _) => delayed.Task);
            Field<Button>(busy, "btnCreateTask").PerformClick(); Until(() => flow.IsBusy); busy.Close();
            Check(busy.Visible && flow.IsBusy, "User close is blocked while the POST is in flight");
            delayed.SetException(new HttpRequestException()); Until(() => flow.State == TaskCreationState.NeedsResultCheck); busy.Close();
        }
        using var preview = new TaskEditForm(); Show(preview); Check(!Field<Button>(preview, "btnCreateTask").Visible, "Unconfigured preview remains preparation only"); preview.Close();
        using var edit = new TaskEditForm(new TaskDetail(new TaskSummary("1", "기존", "담당", TaskDisplayStatus.InProgress, Start, Due), "설명"), prep); Show(edit);
        Check(!Field<Button>(edit, "btnCreateTask").Visible, "Existing-task modification has no creation path"); edit.Close();
    }
    private static void Capture(Form form, string name)
    {
        if (capture is null) return; Directory.CreateDirectory(capture);
        using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, form.ClientRectangle); bitmap.Save(Path.Combine(capture, name + ".png"));
    }
    private static void NavigationRecovery()
    {
        using var h = new Handler(); using var api = Api(h);
        var first = new TasksControl(PreviewRole.Administrator, new HttpTaskQueryService(api));
        var pending = Field<TaskCreationWorkflow>(first, "creationWorkflow");
        h.Success(Key()); h.Drop(); Wait(pending.SubmitAsync(Request("메뉴 복원")));
        string key = h.Key!; first.Dispose();
        using var second = new TasksControl(PreviewRole.Administrator, new HttpTaskQueryService(api));
        var restored = Field<TaskCreationWorkflow>(second, "creationWorkflow");
        Check(ReferenceEquals(pending, restored) && restored.State == TaskCreationState.NeedsResultCheck && restored.PendingRequest?.Title == "메뉴 복원", "Menu navigation retains the same pending workflow in the current login");
        using (var form = new TaskEditForm(new HttpTaskPreparationService(api.CaptureSession()), restored))
        {
            Show(form); Check(Field<TextBox>(form, "txtTaskTitle").Text == "메뉴 복원" && Field<Button>(form, "btnCheckCreationResult").Enabled, "Returning to tasks restores the pending form and result action"); form.Close();
        }
        h.Success(new { state = "completed", http_status = 201, result = Result("4"), expires_at = "2030-01-01T00:00:00Z" }); Wait(restored.CheckResultAsync());
        Check(restored.State == TaskCreationState.Succeeded && h.Key == key && h.Posts == 1, "Restored menu workflow checks the original key without duplicate registration");
        restored.ResetAfterSuccess(); h.Success(Key()); h.Error(403, "FORBIDDEN"); Wait(restored.SubmitAsync(Request())); second.Dispose();
        using var third = new TasksControl(PreviewRole.Administrator, new HttpTaskQueryService(api));
        var blocked = Field<TaskCreationWorkflow>(third, "creationWorkflow");
        Check(ReferenceEquals(restored, blocked) && blocked.State == TaskCreationState.Disconnected && api.CurrentUser is not null, "Menu recreation cannot bypass a current-session 403 block");
        api.ClearIdentity(); Login(api, h);
        using var freshControl = new TasksControl(PreviewRole.Administrator, new HttpTaskQueryService(api));
        var fresh = Field<TaskCreationWorkflow>(freshControl, "creationWorkflow");
        Check(!ReferenceEquals(fresh, blocked) && fresh.State == TaskCreationState.Editing && blocked.PendingRequest is null, "A new login generation gets a fresh workflow and clears the previous session");
        using var otherHandler = new Handler(); using var otherApi = Api(otherHandler); using var isolatedControl = new TasksControl(PreviewRole.Administrator, new HttpTaskQueryService(otherApi));
        Check(!ReferenceEquals(fresh, Field<TaskCreationWorkflow>(isolatedControl, "creationWorkflow")), "Different API clients do not share pending requests");
    }
    private static void RefreshViews()
    {
        using var host = new Form(); var query = new QueryStub(); using var control = new TasksControl(PreviewRole.Administrator, query) { Dock = DockStyle.Fill }; host.Controls.Add(control); Show(host);
        Field<ComboBox>(control, "cmbTaskScope").SelectedIndex = 1; Wait(control.RefreshAsync());
        var created = new TaskCreationResult("2", "1", 1, TaskDisplayStatus.NotStarted, DateTimeOffset.UtcNow);
        query.IncludeCreated = true;
        Wait((Task)typeof(TasksControl).GetMethod("RefreshAfterCreationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, new object[] { created })!);
        Until(() => Field<TaskDetail?>(control, "currentTaskDetail")?.Summary.TaskId == "2");
        Check(query.Last?.Scope == TaskQueryScope.Managed && Field<DataGridView>(control, "gridTasks").SelectedRows[0].Tag is TaskSummary { TaskId: "2" }, "Successful creation preserves managed scope and selects/refetches new detail");
        Field<ComboBox>(control, "cmbTaskStatusFilter").SelectedIndex = 2; Wait(control.RefreshAsync());
        Wait((Task)typeof(TasksControl).GetMethod("RefreshAfterCreationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, new object[] { created })!);
        Check(Field<ComboBox>(control, "cmbTaskStatusFilter").SelectedIndex == 2 && query.Last?.Status == TaskDisplayStatus.InProgress, "Success refresh never changes the user's status filter");
        Check(Field<Label>(control, "lblTaskQueryStatus").Text.Contains("등록") && Field<DataGridView>(control, "gridTasks").Rows.Count == 1, "Excluded new task gets a clear saved-but-filtered notice");
        host.Close();
    }
    private static void Live()
    {
        using var input = JsonDocument.Parse(Console.In.ReadToEnd()); var settings = input.RootElement;
        string Text(string name) => settings.GetProperty(name).GetString()!;
        int assignee = settings.GetProperty("assignee_id").GetInt32(), creator = settings.GetProperty("admin_user_id").GetInt32(), additional = settings.GetProperty("additional_manager_id").GetInt32();
        using (var api = new ApiClient(Text("url")))
        {
            Wait(api.LoginAsync(Text("admin_login_id"), Text("admin_password")));
            using var host = new Form { Size = new(1150, 850) }; using var control = new TasksControl(PreviewRole.Administrator, new HttpTaskQueryService(api)) { Dock = DockStyle.Fill }; host.Controls.Add(control); Show(host);
            Field<ComboBox>(control, "cmbTaskScope").SelectedIndex = 1; Wait(control.RefreshAsync());
            Exception? timerFailure = null; bool clicked = false;
            using var timer = new System.Windows.Forms.Timer { Interval = 20 };
            timer.Tick += (_, _) =>
            {
                var form = Application.OpenForms.OfType<TaskEditForm>().FirstOrDefault(); if (form is null || clicked) return; clicked = true;
                try { Fill(form, "creation-live-ui", assignee, creator, additional); Check(Field<Button>(form, "btnCreateTask").Enabled, "Real UI validates selected fixture IDs"); Field<Button>(form, "btnCreateTask").PerformClick(); }
                catch (Exception ex) { timerFailure = ex; form.Close(); }
            };
            timer.Start(); Field<Button>(control, "btnNewTaskPreview").PerformClick(); timer.Stop(); if (timerFailure is not null) throw timerFailure;
            Until(() => Field<TaskDetail?>(control, "currentTaskDetail")?.Summary.Title == "creation-live-ui");
            Check(clicked && Field<ComboBox>(control, "cmbTaskScope").SelectedIndex == 1 && Field<DataGridView>(control, "gridTasks").Rows.Count == 1, "Actual modal UI POST refreshes managed list and new detail"); host.Close();
        }
        foreach (bool afterCommit in new[] { true, false })
        {
            using var handler = new LossHandler(afterCommit); using var api = new ApiClient(Text("url"), handler); Wait(api.LoginAsync(Text("admin_login_id"), Text("admin_password")));
            using var flow = new TaskCreationWorkflow(new HttpTaskCreationService(api.CaptureSession()));
            Wait(flow.SubmitAsync(Request(afterCommit ? "creation-live-lost" : "creation-live-retry", assignee, creator, additional)));
            Check(flow.State == TaskCreationState.NeedsResultCheck && handler.Attempts == 1, "Real HTTP loss requires explicit result check");
            Wait(flow.CheckResultAsync());
            if (afterCommit) Check(flow.State == TaskCreationState.Succeeded && handler.Sent == 1 && handler.Attempts == 1, "Committed response loss is recovered through stored result without resend");
            else
            {
                Check(flow.State == TaskCreationState.RetryAllowed && handler.Sent == 0, "Actual not-recorded receipt does not silently resend");
                Wait(flow.RetryAsync()); Check(flow.State == TaskCreationState.Succeeded && handler.Sent == 1 && handler.Attempts == 2 && handler.SameRequest, "Real retry reuses identical key/body and creates once");
            }
            Check(flow.CreatedResult is { Status: TaskDisplayStatus.NotStarted, RowVersion: "1", AssignmentRevision: 1 }, "Real HTTP creation returns initial state and revisions");
        }
    }
    private sealed class QueryStub : ITaskQueryService
    {
        public string SourceDescription => "명시적 테스트 대역";
        public bool IncludeCreated; public TaskListQuery? Last;
        private static TaskSummary Old => new("1", "기존", "직원", TaskDisplayStatus.InProgress, Start, Due);
        private static TaskSummary New => new("2", "새 업무", "직원", TaskDisplayStatus.NotStarted, Start, Due);
        public Task<IReadOnlyList<TaskSummary>> GetTasksAsync(TaskListQuery query, CancellationToken ct = default)
        { Last = query; return Task.FromResult<IReadOnlyList<TaskSummary>>((IncludeCreated ? new[] { New, Old } : new[] { Old }).Where(t => query.Status is null || t.Status == query.Status).ToArray()); }
        public Task<TaskDetail> GetTaskAsync(string taskId, CancellationToken ct = default) => Task.FromResult(new TaskDetail(taskId == "2" ? New : Old, "조회된 상세"));
    }
    private sealed class Handler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> queue = new();
        public int Count, Posts; public HttpRequestMessage? Last; public string Body = ""; public string? Key, Bearer;
        public void Next(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> next) => queue.Enqueue(next);
        public void Success(object data, int status = 200) => Next((_, _) => Task.FromResult(Response(data, status)));
        public void Error(int status, string code) => Next((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(JsonSerializer.Serialize(new { ok = false, error = new { code, message = "합성 오류" }, request_id = "synthetic" }), Encoding.UTF8, "application/json") }));
        public void Drop() => Next((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("synthetic loss")));
        public static HttpResponseMessage Response(object data, int status = 200) => new((HttpStatusCode)status) { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "synthetic" }, ApiClient.Json), Encoding.UTF8, "application/json") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request; Count++; if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/v1/tasks") Posts++;
            Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken); Bearer = request.Headers.Authorization?.Parameter;
            Key = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
            if (queue.Count == 0) throw new InvalidOperationException("Unexpected HTTP call in controlled test");
            return await queue.Dequeue()(request, cancellationToken);
        }
    }
    private sealed class LossHandler(bool afterCommit) : DelegatingHandler(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
    {
        public int Attempts, Sent; public bool SameRequest = true; private string? firstKey, firstBody;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Post || request.RequestUri!.AbsolutePath != "/api/v1/tasks") return await base.SendAsync(request, cancellationToken);
            Attempts++; string key = request.Headers.GetValues("Idempotency-Key").Single(), body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (Attempts == 1) { firstKey = key; firstBody = body; } else SameRequest &= firstKey == key && firstBody == body;
            if (Attempts == 1 && !afterCommit) throw new HttpRequestException("synthetic pre-send loss");
            Sent++; var response = await base.SendAsync(request, cancellationToken);
            if (Attempts == 1 && afterCommit) { await response.Content.ReadAsByteArrayAsync(cancellationToken); response.Dispose(); throw new HttpRequestException("synthetic post-commit response loss"); }
            return response;
        }
    }
}
