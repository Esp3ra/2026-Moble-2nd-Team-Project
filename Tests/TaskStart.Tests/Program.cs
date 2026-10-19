using System.Net;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Features.Tasks.Starting;
using RemoteWork.WinForms.Infrastructure;

internal static partial class Program
{
    private static int checks;
    private static readonly DateTimeOffset Utc = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
    private static async Task<int> Main(string[] args)
    {
        try
        {
            if (args.SequenceEqual(new[] { "--http" }))
            {
                await LiveHttp();
                Console.WriteLine($"PASS TaskStart real HTTP: {checks} checks; owned fixture server, captured common sessions.");
                return 0;
            }
            if (args.Length != 0) throw new ArgumentException("지원하지 않는 테스트 옵션입니다.");
            await Wire(); await Recovery(); await Errors(); await InvalidWire(); await Sessions(); await ReworkCycles(); await Concurrency();
            Console.WriteLine($"PASS TaskStart controlled HTTP: {checks} checks; captured common sessions, no real server/DB.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(args.SequenceEqual(new[] { "--http" })
                ? $"FAIL TaskStart real HTTP after {checks} checks ({error.GetType().Name})."
                : $"FAIL after {checks}: {error.GetType().Name}: {error.Message}");
            return 1;
        }
    }
    private static void Check(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); checks++; }
    private static TaskStartRequest Request(TaskStartKind kind = TaskStartKind.Accept, string id = "9007199254740993",
        string version = "9007199254740993", int revision = 4, int user = 7)
    {
        var status = kind == TaskStartKind.Accept ? TaskDisplayStatus.NotStarted : TaskDisplayStatus.Returned;
        var detail = new TaskDetail(new TaskSummary(id, "명시적 합성 업무", "합성 사용자", status,
            new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 25)), "합성 테스트 데이터");
        var context = new TaskWriteContext(id, user, new[] { 2 }, version, revision, status, Utc);
        var selected = TaskStartTargetPreparation.Check(kind, detail, context, user);
        Check(selected.Outcome == TaskStartTargetOutcome.Matched && selected.Target is not null, "Read detail/context makes the checked start target");
        return TaskStartRequest.FromCheckedTarget(selected.Target!);
    }
    private static object Key(string value = "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk") =>
        new { request_key = value, expires_at = "2030-01-01T00:00:00Z" };
    private static object Result(string id = "9007199254740993", string row = "9007199254740994", int revision = 4,
        string status = "in_progress", string updated = "2026-10-19T00:00:00Z") =>
        new { task_id = id, row_version = row, assignment_revision = revision, task_status = status, updated_at = updated };
    private static object Completed(object? result = null, int status = 200) =>
        new { state = "completed", http_status = status, result = result ?? Result(), expires_at = "2030-01-01T00:00:00Z" };
    private static async Task<ApiClient> Api(Handler handler, string role = "employee")
    {
        var api = new ApiClient("http://localhost:5000/", handler);
        await Login(api, handler, role);
        return api;
    }
    private static Task Login(ApiClient api, Handler handler, string role = "employee", int user = 7)
    {
        handler.Success(new { access_token = "synthetic-token", token_type = "Bearer", expires_at = "2030-01-01T00:00:00Z",
            user = new { id = user, login_id = "synthetic", display_name = "합성 사용자", role, is_active = true } });
        return api.LoginAsync("synthetic", "not-a-real-password");
    }
    private static async Task Wire()
    {
        foreach (var kind in new[] { TaskStartKind.Accept, TaskStartKind.Rework })
        {
            using var handler = new Handler(); using var api = await Api(handler);
            var service = new HttpTaskStartService(api.CaptureSession());
            var request = Request(kind);
            handler.Success(Key()); var key = await service.GetRequestKeyAsync(request);
            var issued = handler.Calls[^1];
            using var keyBody = JsonDocument.Parse(issued.Body);
            Check(issued.Path == "/api/v1/write-requests/keys" && issued.Key is null
                && keyBody.RootElement.EnumerateObject().Count() == 2
                && keyBody.RootElement.GetProperty("action").GetString() == (kind == TaskStartKind.Accept ? "task.accepted" : "task.rework_started")
                && keyBody.RootElement.GetProperty("target").GetString() == request.TaskId, "Action-scoped server key uses the common key route");
            Check(!key.ToString().Contains(key.Value, StringComparison.Ordinal), "Request key ToString cannot disclose its value");
            handler.Success(Result()); var response = await service.StartAsync(request, key);
            var sent = handler.Calls[^1];
            using var body = JsonDocument.Parse(sent.Body);
            Check(sent.Path == "/api/v1/tasks/9007199254740993/" + (kind == TaskStartKind.Accept ? "accept" : "rework-start")
                && sent.Method == HttpMethod.Post && sent.Key == key.Value && sent.Bearer == "synthetic-token", "Start uses the approved route and captured common bearer/key headers");
            Check(body.RootElement.EnumerateObject().Count() == 2
                && body.RootElement.GetProperty("expected_row_version").GetString() == "9007199254740993"
                && body.RootElement.GetProperty("expected_assignment_revision").GetInt32() == 4,
                "Only expected string BIGINT and integer assignment are sent");
            Check(response.TaskId == request.TaskId && response.RowVersion == "9007199254740994"
                && response.AssignmentRevision == 4 && response.Status == TaskDisplayStatus.InProgress && response.UpdatedAt == Utc,
                "Strict five-field result preserves a BIGINT above JavaScript integer precision");
            handler.Success(Completed()); var lookup = await service.GetResultAsync(request, key);
            Check(lookup.Recorded && lookup.Result?.TaskId == request.TaskId
                && handler.Calls[^1].Path == "/api/v1/write-requests/result" && handler.Calls[^1].Key == key.Value,
                "Receipt lookup uses the common idempotency header and validates HTTP200");
            int before = handler.Calls.Count;
            foreach (var wrongKey in new[] { new TaskStartKey(kind, "2", key.Value, Utc),
                new TaskStartKey(kind == TaskStartKind.Accept ? TaskStartKind.Rework : TaskStartKind.Accept, request.TaskId, key.Value, Utc),
                new TaskStartKey(kind, request.TaskId, "short", Utc),
                new TaskStartKey(kind, request.TaskId, new string('k', 32) + "\n", Utc) })
            {
                try { await service.StartAsync(request, wrongKey); throw new InvalidOperationException("Cross-target key accepted"); }
                catch (ArgumentException) { Check(handler.Calls.Count == before, "Mismatched/invalid scoped keys fail before HTTP"); }
            }
        }
        using var h = new Handler(); using var a = await Api(h);
        var s = new HttpTaskStartService(a.CaptureSession()); int n = h.Calls.Count;
        try { await s.GetRequestKeyAsync(Request(user: 8)); throw new InvalidOperationException("Different identity accepted"); }
        catch (TaskStartException ex) { Check(ex.Failure == TaskStartFailure.Forbidden && h.Calls.Count == n, "A checked target from a different identity is rejected locally"); }
    }
    private static async Task Recovery()
    {
        using var h = new Handler(); using var api = await Api(h);
        using var flow = new TaskStartWorkflow(new HttpTaskStartService(api.CaptureSession()));
        var request = Request(); h.Success(Key()); h.Drop(); await flow.SubmitAsync(request);
        Check(flow.State == TaskStartState.NeedsResultCheck && flow.PendingRequest == request && !flow.CanRetry, "Response loss retains immutable request and requires explicit lookup");
        string body = h.Calls[^1].Body, key = h.Calls[^1].Key!; int count = h.Calls.Count;
        await flow.SubmitAsync(Request(version: "9007199254740995")); await flow.RetrySameRequestAsync();
        Check(h.Calls.Count == count && flow.Result is null, "Uncertain requests cannot acquire a fresh key or retry automatically");
        h.Success(Completed()); await flow.CheckResultAsync();
        Check(flow.State == TaskStartState.Succeeded && flow.Result?.TaskId == request.TaskId && h.Mutations == 1,
            "Committed loss is recovered from receipt without a second mutation");
        Check(flow.AcknowledgeSuccess() && flow.State == TaskStartState.Editing && flow.PendingRequest is null && flow.Result is null,
            "Explicit acknowledged success clears in-memory request/result for later read refresh");
        count = h.Calls.Count; await flow.SubmitAsync(request);
        Check(h.Calls.Count == count && flow.State == TaskStartState.Editing, "An already consumed old checked version cannot restart after acknowledgement");
        var next = Request(version: "9007199254740995");
        h.Success(Key("nnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnnn")); h.Drop(); await flow.SubmitAsync(next);
        body = h.Calls[^1].Body; key = h.Calls[^1].Key!;
        h.Success(new { state = "not_recorded" }); await flow.CheckResultAsync();
        Check(flow.State == TaskStartState.RetryAllowed && flow.CanRetry && h.Mutations == 2, "Only an explicit not-recorded lookup permits the same request retry");
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel(); count = h.Calls.Count; await flow.RetrySameRequestAsync(cancellation.Token);
            Check(h.Calls.Count == count && flow.State == TaskStartState.NeedsResultCheck && flow.PendingRequest == next,
                "A canceled retry retains prior body/key and sends no mutation");
        }
        h.Success(new { state = "not_recorded" }); await flow.CheckResultAsync();
        h.Success(Result(row: "9007199254740996")); await flow.RetrySameRequestAsync();
        Check(flow.State == TaskStartState.Succeeded && h.Calls[^1].Body == body && h.Calls[^1].Key == key && h.Mutations == 3,
            "Explicit retry reuses exactly the original immutable body and server key");

        using var eh = new Handler(); using var ea = await Api(eh);
        using var expired = new TaskStartWorkflow(new HttpTaskStartService(ea.CaptureSession()));
        eh.Success(Key()); eh.Error(410, "IDEMPOTENCY_KEY_EXPIRED"); await expired.SubmitAsync(Request());
        Check(expired.CanCheckResult && !expired.CanRetry, "POST410 still permits checking an already retained successful result");
        eh.Success(Completed()); await expired.CheckResultAsync();
        Check(expired.State == TaskStartState.Succeeded && eh.Mutations == 1, "Expired POST key can recover an existing receipt without a new key");
        using var mh = new Handler(); using var ma = await Api(mh);
        using var manual = new TaskStartWorkflow(new HttpTaskStartService(ma.CaptureSession()));
        mh.Success(Key()); mh.Drop(); await manual.SubmitAsync(Request());
        mh.Error(410, "IDEMPOTENCY_KEY_EXPIRED"); await manual.CheckResultAsync(); count = mh.Calls.Count;
        await manual.SubmitAsync(Request()); await manual.RetrySameRequestAsync(); await manual.CheckResultAsync();
        Check(manual.State == TaskStartState.Blocked && manual.PendingRequest is not null && mh.Calls.Count == count,
            "Receipt410 requires manual verification and never starts a new request");
    }
    private static async Task Errors()
    {
        foreach (var (status, code, state, failure) in new[] {
            (400, "INVALID_REQUEST", TaskStartState.Editing, TaskStartFailure.InvalidInput),
            (404, "TASK_NOT_FOUND", TaskStartState.Editing, TaskStartFailure.NotFound),
            (409, "TASK_ASSIGNMENT_CHANGED", TaskStartState.Editing, TaskStartFailure.AssignmentChanged),
            (409, "TASK_VERSION_CONFLICT", TaskStartState.Editing, TaskStartFailure.VersionConflict),
            (409, "TASK_STATE_CONFLICT", TaskStartState.Editing, TaskStartFailure.StateConflict),
            (409, "TASK_START_DATA_INCONSISTENT", TaskStartState.Editing, TaskStartFailure.DataInconsistent),
            (409, "TASK_VERSION_EXHAUSTED", TaskStartState.Editing, TaskStartFailure.VersionExhausted),
            (409, "TASK_START_BEFORE_START_DATE", TaskStartState.Editing, TaskStartFailure.BeforeStartDate),
            (409, "TASK_START_AFTER_DUE_DATE", TaskStartState.Editing, TaskStartFailure.AfterDueDate),
            (409, "IDEMPOTENCY_KEY_REUSED", TaskStartState.Blocked, TaskStartFailure.KeyReused),
            (409, "UNKNOWN_CONFLICT", TaskStartState.Blocked, TaskStartFailure.Conflict),
            (403, "FORBIDDEN", TaskStartState.Disconnected, TaskStartFailure.Forbidden),
            (401, "AUTH_REQUIRED", TaskStartState.Disconnected, TaskStartFailure.AuthenticationExpired),
            (503, "DB_UNAVAILABLE", TaskStartState.NeedsResultCheck, TaskStartFailure.Unavailable),
            (503, "WRITE_ACTION_NOT_READY", TaskStartState.Editing, TaskStartFailure.NotConfigured) })
        {
            using var h = new Handler(); using var api = await Api(h, "admin");
            using var flow = new TaskStartWorkflow(new HttpTaskStartService(api.CaptureSession()));
            h.Success(Key()); h.Error(status, code); await flow.SubmitAsync(Request());
            Check(flow.State == state && flow.Failure == failure, "Approved error has its safe UI state: " + code);
            Check(flow.Result is null && h.Mutations == 1, "A failed response cannot report success or automatically retry");
            if (state == TaskStartState.Disconnected)
            {
                int count = h.Calls.Count; await flow.SubmitAsync(Request());
                Check(flow.PendingRequest is null && !flow.IsCurrent && h.Calls.Count == count, "401/403 clears pending data and blocks this workflow");
            }
        }
        using var handler = new Handler(); using var client = await Api(handler);
        using var workflow = new TaskStartWorkflow(new HttpTaskStartService(client.CaptureSession()));
        handler.Error(503, "WRITE_ACTION_NOT_READY"); await workflow.SubmitAsync(Request());
        Check(handler.Mutations == 0 && workflow.State == TaskStartState.Editing, "Missing common key/action dependencies never send a start POST");
        int before = handler.Calls.Count;
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); await workflow.SubmitAsync(Request(), cancellation.Token);
        Check(handler.Calls.Count == before && workflow.State == TaskStartState.Editing, "Cancellation before key issuance sends no network request");
    }
    private static async Task InvalidWire()
    {
        foreach (var value in new object[] { Result("01"), Result("9223372036854775808"), Result("2"),
            Result(row: "9007199254740993"), Result(row: "9007199254740995"), Result(revision: 5),
            Result(status: "submitted"), Result(updated: "2026-10-19T09:00:00+09:00"),
            Result(updated: "2026-10-19T00:00:00.000Z"),
            new { task_id = "9007199254740993", row_version = "9007199254740994", assignment_revision = "4", task_status = "in_progress", updated_at = "2026-10-19T00:00:00Z" },
            new { task_id = "9007199254740993", row_version = "9007199254740994", assignment_revision = 4, task_status = "in_progress", updated_at = "2026-10-19T00:00:00Z", extra = "unexpected" },
            new { task_id = "9007199254740993", row_version = "9007199254740994", assignment_revision = 4, task_status = "in_progress" } })
        {
            using var h = new Handler(); using var api = await Api(h);
            using var flow = new TaskStartWorkflow(new HttpTaskStartService(api.CaptureSession()));
            h.Success(Key()); h.Success(value); await flow.SubmitAsync(Request());
            Check(flow.Result is null && flow.State == TaskStartState.NeedsResultCheck && flow.Failure == TaskStartFailure.InvalidResponse,
                "Malformed/mismatched/unknown result fields cannot become a successful write");
        }
        foreach (string raw in new[] {
            "{\"task_id\":\"9007199254740993\",\"task_id\":\"9007199254740993\",\"row_version\":\"9007199254740994\",\"assignment_revision\":4,\"task_status\":\"in_progress\",\"updated_at\":\"2026-10-19T00:00:00Z\"}",
            "{\"task_id\":9007199254740993,\"row_version\":\"9007199254740994\",\"assignment_revision\":4,\"task_status\":\"in_progress\",\"updated_at\":\"2026-10-19T00:00:00Z\"}" })
        {
            using var h = new Handler(); using var api = await Api(h);
            using var flow = new TaskStartWorkflow(new HttpTaskStartService(api.CaptureSession()));
            h.Success(Key()); h.Raw(raw); await flow.SubmitAsync(Request());
            Check(flow.Result is null && flow.State == TaskStartState.NeedsResultCheck, "Duplicate fields and numeric BIGINTs are rejected");
        }
        foreach (object lookup in new[] { Completed(status: 201), Completed(Result(revision: 8)),
            (object)new { state = "not_recorded", result = Result() }, new { state = "not_recorded", unexpected = true },
            new { state = "completed", http_status = 200, result = Result(), expires_at = "2026-10-19T09:00:00+09:00" } })
        {
            using var h = new Handler(); using var api = await Api(h);
            using var flow = new TaskStartWorkflow(new HttpTaskStartService(api.CaptureSession()));
            h.Success(Key()); h.Drop(); await flow.SubmitAsync(Request()); h.Success(lookup); await flow.CheckResultAsync();
            Check(flow.Result is null && flow.State == TaskStartState.NeedsResultCheck && !flow.CanRetry,
                "Invalid receipt never claims success or unlocks a mutation retry");
        }
        foreach (object key in new[] { Key("short"), Key(new string('k', 513)), Key(new string('k', 32) + " "),
            (object)new { request_key = new string('k', 40), expires_at = "2026-10-19T09:00:00+09:00" },
            new { request_key = new string('k', 40), expires_at = "2030-01-01T00:00:00Z", extra = true } })
        {
            using var h = new Handler(); using var api = await Api(h);
            using var flow = new TaskStartWorkflow(new HttpTaskStartService(api.CaptureSession()));
            h.Success(key); await flow.SubmitAsync(Request());
            Check(flow.State == TaskStartState.Editing && h.Mutations == 0, "Invalid server key shape cannot dispatch a mutation");
        }
        using var maxH = new Handler(); using var maxApi = await Api(maxH);
        using var max = new TaskStartWorkflow(new HttpTaskStartService(maxApi.CaptureSession()));
        maxH.Success(Key()); maxH.Error(409, "TASK_VERSION_EXHAUSTED"); await max.SubmitAsync(Request(version: long.MaxValue.ToString()));
        Check(max.Failure == TaskStartFailure.VersionExhausted && max.Result is null, "BIGINT max expected version is safely delegated to the server exhaustion check");
    }
    private static async Task Sessions()
    {
        using var h = new Handler(); using var api = await Api(h);
        Check(TaskStartSessionStore.FindPending(api) is null, "Pending lookup cannot create a key, workflow, or network call");
        var flow = TaskStartSessionStore.Get(api, TaskStartKind.Accept, "9007199254740993")!;
        Check(ReferenceEquals(flow, TaskStartSessionStore.Get(api, TaskStartKind.Accept, "9007199254740993")), "Navigation borrows the same login/action/task workflow");
        Check(!ReferenceEquals(flow, TaskStartSessionStore.Get(api, TaskStartKind.Rework, "9007199254740993")), "Accept and rework have separate request scopes");
        h.Success(Key()); h.Drop(); await flow.SubmitAsync(Request());
        Check(ReferenceEquals(flow, TaskStartSessionStore.FindPending(api)), "Pending loss is reachable without a selected/current-status list row");
        api.ClearIdentity();
        Check(flow.State == TaskStartState.Disconnected && flow.PendingRequest is null && TaskStartSessionStore.FindPending(api) is null
            && TaskStartSessionStore.Get(api, TaskStartKind.Accept, "9007199254740993") is null,
            "Account change clears pending content before opening any new screen");
        await Login(api, h);
        var current = TaskStartSessionStore.Get(api, TaskStartKind.Accept, "9007199254740993")!;
        Check(!ReferenceEquals(current, flow) && current.State == TaskStartState.Editing, "A new login creates a clean isolated workflow");
        h.Success(Key()); h.Error(403, "FORBIDDEN"); await current.SubmitAsync(Request()); int count = h.Calls.Count;
        var reopened = TaskStartSessionStore.Get(api, TaskStartKind.Accept, "9007199254740993");
        await reopened!.SubmitAsync(Request());
        Check(ReferenceEquals(current, reopened) && reopened.State == TaskStartState.Disconnected && h.Calls.Count == count,
            "Reopening a modal cannot bypass a same-login forbidden response");
        using var otherH = new Handler(); using var otherApi = await Api(otherH);
        Check(!ReferenceEquals(current, TaskStartSessionStore.Get(otherApi, TaskStartKind.Accept, "9007199254740993")), "A second ApiClient cannot borrow another client's pending key/body");

        using var lateH = new Handler(); using var lateApi = await Api(lateH);
        var lateFlow = TaskStartSessionStore.Get(lateApi, TaskStartKind.Accept, "9007199254740993")!;
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        lateH.Success(Key()); lateH.Next((_, _) => completion.Task);
        var operation = lateFlow.SubmitAsync(Request());
        await Until(() => lateH.Mutations == 1);
        await Login(lateApi, lateH, user: 8);
        var nextFlow = TaskStartSessionStore.Get(lateApi, TaskStartKind.Accept, "9007199254740993")!;
        completion.SetResult(Handler.Response(Result())); await operation.WaitAsync(TimeSpan.FromSeconds(10));
        Check(lateFlow.State == TaskStartState.Disconnected && lateFlow.Result is null && lateFlow.PendingRequest is null
            && nextFlow.State == TaskStartState.Editing && nextFlow.Result is null,
            "Late previous-login success cannot replace the next user's workflow or show old task data");
    }
    private static async Task Concurrency()
    {
        using var h = new Handler(); using var api = await Api(h);
        using var flow = new TaskStartWorkflow(new HttpTaskStartService(api.CaptureSession()));
        int changed = 0; flow.Changed += (_, _) => { _ = flow.Snapshot; changed++; };
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Success(Key()); h.Next((_, _) => completion.Task);
        var request = Request(); var first = flow.SubmitAsync(request); await Until(() => h.Mutations == 1);
        int count = h.Calls.Count; await flow.SubmitAsync(request); await flow.CheckResultAsync(); await flow.RetrySameRequestAsync();
        Check(flow.IsBusy && flow.State == TaskStartState.Sending && h.Calls.Count == count, "Busy state serializes submits, result checks, and retries");
        Check(ReferenceEquals(TaskStartRequest.FromCheckedTarget(Target()), request) == false, "Checked request creation returns immutable separate values");
        completion.SetResult(Handler.Response(Result())); await first.WaitAsync(TimeSpan.FromSeconds(10));
        Check(flow.State == TaskStartState.Succeeded && changed >= 2 && flow.Snapshot.Result is not null,
            "Snapshot subscribers can read state during notifications without deadlock");
        count = h.Calls.Count; await flow.SubmitAsync(Request());
        Check(h.Calls.Count == count && flow.State == TaskStartState.Succeeded, "Success cannot run again before explicit acknowledgement");
        Check(flow.AcknowledgeSuccess(), "Known success may be acknowledged once"); count = h.Calls.Count;
        await flow.SubmitAsync(Request(id: "2", version: "9007199254740996"));
        Check(h.Calls.Count == count, "A task/action workflow cannot be reused for a different target");
    }
    private static async Task ReworkCycles()
    {
        using var h = new Handler(); using var api = await Api(h);
        var flow = TaskStartSessionStore.Get(api, TaskStartKind.Rework, "9007199254740993")!;
        var first = Request(TaskStartKind.Rework, version: "3");
        h.Success(Key()); h.Success(Result(row: "4")); await flow.SubmitAsync(first);
        Check(flow.State == TaskStartState.Succeeded && ReferenceEquals(flow, TaskStartSessionStore.FindPending(api)),
            "Unacknowledged rework success remains discoverable after closing the initiating page");
        Check(flow.AcknowledgeSuccess() && TaskStartSessionStore.FindPending(api) is null,
            "Acknowledged rework success no longer occupies the pending result entry");
        int count = h.Calls.Count; await flow.SubmitAsync(first);
        Check(h.Calls.Count == count, "The previous rework's checked version cannot be reused after read refresh");
        // Member 3 owns the intervening submit/return; only a new read snapshot is supplied here.
        var second = Request(TaskStartKind.Rework, version: "6");
        h.Success(Key("ssssssssssssssssssssssssssssssssssssssss")); h.Success(Result(row: "7")); await flow.SubmitAsync(second);
        Check(flow.State == TaskStartState.Succeeded && flow.Result?.RowVersion == "7" && h.Mutations == 2,
            "A later returned cycle with a newly checked version can start rework in the same login");
        Check(flow.AcknowledgeSuccess(), "Second known rework success may be acknowledged independently");

        var third = Request(TaskStartKind.Rework, version: "9");
        h.Success(Key()); h.Drop(); await flow.SubmitAsync(third);
        h.Error(403, "FORBIDDEN"); await flow.CheckResultAsync(); count = h.Calls.Count;
        var reopened = TaskStartSessionStore.Get(api, TaskStartKind.Rework, third.TaskId)!;
        await reopened.SubmitAsync(third); await reopened.CheckResultAsync();
        Check(ReferenceEquals(flow, reopened) && reopened.State == TaskStartState.Disconnected
            && reopened.PendingRequest is null && h.Calls.Count == count,
            "Permission revoked during receipt lookup cannot be bypassed by reopening or a fresh request");

        using var ch = new Handler(); using var ca = await Api(ch);
        using var cancelled = new TaskStartWorkflow(new HttpTaskStartService(ca.CaptureSession()));
        using var cancellation = new CancellationTokenSource();
        ch.Success(Key()); ch.Next((_, ct) =>
        { cancellation.Cancel(); return Task.FromCanceled<HttpResponseMessage>(ct); });
        await cancelled.SubmitAsync(Request(), cancellation.Token);
        Check(cancelled.State == TaskStartState.NeedsResultCheck && cancelled.PendingRequest is not null && !cancelled.CanRetry,
            "Cancellation after POST dispatch is uncertain and preserves the immutable request for lookup");
    }
    private static TaskStartTarget Target()
    {
        var status = TaskDisplayStatus.NotStarted;
        return TaskStartTargetPreparation.Check(TaskStartKind.Accept,
            new TaskDetail(new TaskSummary("9007199254740993", "test", "test", status, new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 20)), ""),
            new TaskWriteContext("9007199254740993", 7, new[] { 2 }, "9007199254740993", 4, status, Utc), 7).Target!;
    }
    private static async Task Until(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < until) await Task.Delay(5);
        Check(condition(), "Controlled request reaches the expected boundary");
    }
    private sealed record Sent(HttpMethod Method, string Path, string Body, string? Key, string? Bearer);
    private sealed class Handler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> queued = new();
        public List<Sent> Calls { get; } = new();
        public int Mutations => Calls.Count(c => c.Method == HttpMethod.Post && (c.Path.EndsWith("/accept", StringComparison.Ordinal) || c.Path.EndsWith("/rework-start", StringComparison.Ordinal)));
        public void Next(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action) { lock (queued) queued.Enqueue(action); }
        public void Success(object data, int status = 200) => Next((_, _) => Task.FromResult(Response(data, status)));
        public void Raw(string data) => Next((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"ok\":true,\"data\":" + data + ",\"request_id\":\"synthetic\"}", Encoding.UTF8, "application/json") }));
        public void Error(int status, string code) => Next((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
            { Content = new StringContent(JsonSerializer.Serialize(new { ok = false, error = new { code, message = "합성 오류" }, request_id = "synthetic" }), Encoding.UTF8, "application/json") }));
        public void Drop() => Next((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("synthetic response loss")));
        public static HttpResponseMessage Response(object data, int status = 200) => new((HttpStatusCode)status)
            { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "synthetic" }, ApiClient.Json), Encoding.UTF8, "application/json") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add(new(request.Method, request.RequestUri!.AbsolutePath, body,
                request.Headers.TryGetValues("Idempotency-Key", out var key) ? key.Single() : null, request.Headers.Authorization?.Parameter));
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> next;
            lock (queued) next = queued.Count > 0 ? queued.Dequeue() : throw new InvalidOperationException("Unexpected controlled HTTP call");
            return await next(request, cancellationToken);
        }
    }
}
