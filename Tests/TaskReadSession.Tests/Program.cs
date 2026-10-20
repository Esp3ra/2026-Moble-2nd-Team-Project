using System.Net;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Features.Tasks.Queries.TestDoubles;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private static int checks;
    private static int scenarios;
    private static readonly string[] StatusCodes = ["not_started", "in_progress", "submitted", "approved", "returned"];

    private static async Task<int> Main()
    {
        try
        {
            await Run("capture, frozen origin and borrowed client", CaptureAndOrigin);
            await Run("existing GET contract and typed failures", ContractAndFailures);
            await Run("caller and session cancellation", Cancellation);
            await Run("late previous-account response", LateResponse);
            await Run("completed transport before old DTO mapping", LateMapping);
            await Run("explicit fixture cancellation filters", FixtureFilters);
            Console.WriteLine($"PASS: {scenarios} scenarios / {checks} checks. Explicit HTTP and task doubles; no server, DB, notification service or shared environment tested.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"FAIL after {checks} checks: {error.GetType().Name}: {error.Message}");
            return 1;
        }
    }

    private static async Task CaptureAndOrigin()
    {
        using var handler = new Handler();
        using var api = new ApiClient("http://localhost:5000/", handler);
        var source = new HttpTaskQueryService(api);
        try { source.CaptureReadSession(); throw new Exception("Anonymous capture accepted."); }
        catch (InvalidOperationException) { Check(handler.Requests.Count == 0, "anonymous capture does not send HTTP"); }
        await Login(api, handler);
        var old = source.CaptureReadSession();
        Check(old.IsCurrent && !old.Cancellation.IsCancellationRequested, "captured reader starts current");
        string description = old.SourceDescription;
        Check(description.Contains("http://localhost:5000/") && !description.Contains("fixture-token"), "source includes server and no token");
        api.ClearIdentity();
        api.SetServer("http://localhost:5001/");
        Check(!old.IsCurrent && old.Cancellation.IsCancellationRequested, "logout invalidates borrowed reader");
        Check(old.SourceDescription == description, "old source does not follow later server selection");
        await ExpectCancelled(() => old.GetTasksAsync(new(TaskQueryScope.Own)));
        await ExpectCancelled(() => old.GetTaskAsync("1"));
        try { old.ThrowIfChanged(); throw new Exception("Old reader accepted."); }
        catch (OperationCanceledException) { Check(true, "currentness check rejects old reader"); }
        Check(handler.Requests.Count == 1 && !handler.Disposed, "old reads neither send requests nor dispose shared client");
        await Login(api, handler, 2);
        var current = source.CaptureReadSession();
        handler.Success(new { items = Array.Empty<object>() });
        Check((await current.GetTasksAsync(new(TaskQueryScope.Own))).Count == 0, "new login explicitly captures working reader");
        Check(current.SourceDescription.Contains("http://localhost:5001/") && old.SourceDescription == description, "new and old origins remain separate");
        Check(handler.Requests.Last().Bearer == "fixture-token-2", "new reader uses only new identity");
        await ExpectCancelled(() => old.GetTasksAsync(new(TaskQueryScope.Own)));
        await Login(api, handler, 2); // Same fixture token/user, but a different login generation.
        Check(!current.IsCurrent && current.Cancellation.IsCancellationRequested, "same-user relogin also invalidates previous generation");
        await ExpectCancelled(() => current.GetTaskAsync("2"));
        var latest = source.CaptureReadSession();
        handler.Success(new { items = Array.Empty<object>() });
        Check((await latest.GetTasksAsync(new(TaskQueryScope.Own))).Count == 0 && !handler.Disposed,
            "same-user relogin explicitly captures another usable reader");
    }

    private static async Task ContractAndFailures()
    {
        using var handler = new Handler();
        using var api = new ApiClient("http://localhost:5000/", handler);
        await Login(api, handler);
        var read = new HttpTaskQueryService(api).CaptureReadSession();
        handler.Success(new { items = Enumerable.Range(1, 137).Select(id => Item(id)).ToArray() });
        Check((await read.GetTasksAsync(new(TaskQueryScope.Own))).Count == 137, "whole list is preserved");
        Check(handler.Requests.Last().Path == "/api/v1/tasks?scope=own" && handler.Requests.Last().Bearer == "fixture-token", "read uses existing relative path and captured Bearer");
        for (int index = 0; index < StatusCodes.Length; index++)
        {
            var item = Item(index + 1, StatusCodes[index]);
            item["is_cancelled"] = true;
            handler.Success(new { items = new[] { item } });
            var rows = await read.GetTasksAsync(new(TaskQueryScope.Managed, (TaskDisplayStatus)index, IncludeCancelled: true));
            Check(rows.Single().Status == (TaskDisplayStatus)index && rows.Single().IsCancelled, "five-status and cancellation values survive mapping");
            Check(handler.Requests.Last().Path == "/api/v1/tasks?scope=managed&status=" + StatusCodes[index] + "&include_cancelled=true", "existing status/scope/cancellation query is unchanged");
        }
        handler.Success(new { items = Array.Empty<object>() });
        Check((await read.GetTasksAsync(new(TaskQueryScope.Own))).Count == 0, "empty success remains empty");
        var cancelled = Item(1);
        cancelled["is_cancelled"] = true;
        handler.Success(new { items = new[] { Item(2), cancelled } });
        await ExpectFailure(() => read.GetTasksAsync(new(TaskQueryScope.Own)), TaskQueryFailure.Unavailable);
        handler.Success(new { items = new[] { Item(1, "returned") } });
        await ExpectFailure(() => read.GetTasksAsync(new(TaskQueryScope.Own, TaskDisplayStatus.InProgress)), TaskQueryFailure.Unavailable);
        handler.Success(new { items = new[] { Item(1), Item(1) } });
        await ExpectFailure(() => read.GetTasksAsync(new(TaskQueryScope.Own)), TaskQueryFailure.Unavailable);
        handler.Success(new { task = Item(1, "submitted") });
        var detail = await read.GetTaskAsync("1");
        Check(detail.Description == "Fixture description" && detail.Summary.Status == TaskDisplayStatus.Submitted
            && detail.Summary.DueDate == new DateOnly(2026, 10, 20), "detail shares existing DATE/status/description mapping");
        Check(handler.Requests.Last().Path == "/api/v1/tasks/1", "detail uses existing GET path");
        foreach (var sample in new[]
        {
            (403, "FORBIDDEN", TaskQueryFailure.Forbidden),
            (404, "TASK_NOT_FOUND", TaskQueryFailure.NotFound),
            (404, "HTTP_404", TaskQueryFailure.NotConfigured),
            (500, "SERVER_ERROR", TaskQueryFailure.Unavailable),
            (503, "DATABASE_UNAVAILABLE", TaskQueryFailure.Unavailable)
        })
        {
            handler.Error(sample.Item1, sample.Item2);
            await ExpectFailure(() => read.GetTaskAsync("1"), sample.Item3);
            Check(read.IsCurrent, "ordinary HTTP failure does not expire captured identity");
        }
        handler.Raw(200, "{invalid-json");
        await ExpectFailure(() => read.GetTasksAsync(new(TaskQueryScope.Own)), TaskQueryFailure.Unavailable);
        handler.Enqueue((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("explicit fixture failure")));
        await ExpectFailure(() => read.GetTasksAsync(new(TaskQueryScope.Own)), TaskQueryFailure.Unavailable);
        int expired = 0;
        api.AuthenticationExpired += (_, _) => expired++;
        handler.Error(401, "AUTH_REQUIRED");
        await ExpectFailure(() => read.GetTasksAsync(new(TaskQueryScope.Own)), TaskQueryFailure.AuthenticationExpired);
        Check(expired == 1 && !read.IsCurrent && read.Cancellation.IsCancellationRequested, "401 retains expiry classification and invalidates reader");
        int count = handler.Requests.Count;
        await ExpectCancelled(() => read.GetTaskAsync("1"));
        Check(handler.Requests.Count == count && handler.Remaining == 0, "expired read cannot retry and no failure falls back to data");
    }

    private static async Task Cancellation()
    {
        using var handler = new Handler();
        using var api = new ApiClient("http://localhost:5000/", handler);
        await Login(api, handler);
        var read = new HttpTaskQueryService(api).CaptureReadSession();
        using var caller = new CancellationTokenSource();
        caller.Cancel();
        await ExpectCancelled(() => read.GetTasksAsync(new(TaskQueryScope.Own), caller.Token));
        await ExpectCancelled(() => read.GetTaskAsync("1", caller.Token));
        Check(handler.Requests.Count == 1 && read.IsCurrent, "pre-cancelled calls send no request and keep identity");
        using var during = new CancellationTokenSource();
        handler.Enqueue(Pending);
        var pending = read.GetTasksAsync(new(TaskQueryScope.Own), during.Token);
        during.Cancel();
        await ExpectCancelled(() => pending);
        Check(read.IsCurrent && !read.Cancellation.IsCancellationRequested, "caller cancellation does not expire session");
        handler.Enqueue(Pending);
        var detail = read.GetTaskAsync("1");
        api.ClearIdentity();
        await ExpectCancelled(() => detail);
        Check(!read.IsCurrent && read.Cancellation.IsCancellationRequested, "session cancellation is observable");
    }

    private static async Task LateResponse()
    {
        using var handler = new Handler();
        using var api = new ApiClient("http://localhost:5000/", handler);
        await Login(api, handler);
        var source = new HttpTaskQueryService(api);
        var old = source.CaptureReadSession();
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Enqueue((_, _) => completion.Task); // Deliberately ignores request cancellation.
        var pending = old.GetTasksAsync(new(TaskQueryScope.Own));
        await Login(api, handler, 2);
        completion.SetResult(Response(new { items = new[] { Item(1) } }));
        await ExpectCancelled(() => pending);
        Check(!old.IsCurrent && old.Cancellation.IsCancellationRequested, "old successful transport cannot cross login");
        var current = source.CaptureReadSession();
        handler.Success(new { items = new[] { Item(2) } });
        Check((await current.GetTasksAsync(new(TaskQueryScope.Own))).Single().TaskId == "2", "new session gets only explicit new response");
        Check(handler.Requests.Last().Bearer == "fixture-token-2", "late old completion does not replace new token");
        int count = handler.Requests.Count;
        await ExpectCancelled(() => old.GetTaskAsync("1"));
        Check(handler.Requests.Count == count, "old reader stays invalid after new session succeeds");
    }

    private static async Task LateMapping()
    {
        using var handler = new Handler();
        using var api = new ApiClient("http://localhost:5000/", handler);
        await Login(api, handler);
        var read = new HttpTaskQueryService(api).CaptureReadSession();
        using var context = new QueueContext();
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        handler.Enqueue((_, _) => completion.Task);
        Task<IReadOnlyList<TaskSummary>> pending;
        var previous = SynchronizationContext.Current;
        try
        {
            SynchronizationContext.SetSynchronizationContext(context);
            pending = read.GetTasksAsync(new(TaskQueryScope.Own));
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
        using var response = new ObservedResponse(new { items = new[] { Item(1) } });
        completion.SetResult(response);
        context.RunNext(); // Complete the transport; HttpTaskQueryService mapping is still queued.
        Check(response.Disposed && !pending.IsCompleted, "transport completed before queued query mapping");
        api.ClearIdentity();
        context.RunNext();
        await ExpectCancelled(() => pending);
        Check(!read.IsCurrent, "completed old payload cannot publish after idle logout");
    }

    private static async Task FixtureFilters()
    {
        var fixtures = Enum.GetValues<TaskDisplayStatus>().SelectMany((status, index) => new[]
        {
            new TaskDetail(new TaskSummary("active-" + index, "Active", "Fixture", status,
                new DateOnly(2026, 10, 16), new DateOnly(2026, 10, 20)), "Active"),
            new TaskDetail(new TaskSummary("cancelled-" + index, "Cancelled", "Fixture", status,
                new DateOnly(2026, 10, 16), new DateOnly(2026, 10, 20), true), "Cancelled")
        }).ToArray();
        var fixture = new TaskQueryTestDouble(fixtures);
        Check(fixture.SourceDescription.Contains("테스트 대역"), "fixture remains explicitly identified");
        foreach (var scope in Enum.GetValues<TaskQueryScope>())
        {
            Check((await fixture.GetTasksAsync(new(scope))).Count == 5, "fixture all excludes cancelled by default");
            Check((await fixture.GetTasksAsync(new(scope, IncludeCancelled: true))).Count == 10, "fixture all explicitly includes cancelled");
            foreach (var status in Enum.GetValues<TaskDisplayStatus>())
            {
                var active = await fixture.GetTasksAsync(new(scope, status));
                Check(active.Count == 1 && !active[0].IsCancelled && active[0].Status == status, "fixture status default excludes cancelled");
                var included = await fixture.GetTasksAsync(new(scope, status, true));
                Check(included.Count == 2 && included.All(row => row.Status == status)
                    && included.Count(row => row.IsCancelled) == 1, "fixture combines cancellation and status filters");
            }
        }
        Check((await fixture.GetTaskAsync("cancelled-0")).Summary.IsCancelled, "fixture retains cancelled detail");
    }

    private static async Task Login(ApiClient api, Handler handler, int userId = 1)
    {
        handler.Success(new
        {
            access_token = userId == 1 ? "fixture-token" : "fixture-token-" + userId,
            token_type = "Bearer", expires_at = "2026-10-20T20:00:00Z",
            user = new { id = userId, login_id = "fixture", display_name = "Fixture", employee_no = "TEST-" + userId,
                department = "Test", role = "employee", is_active = true }
        });
        await api.LoginAsync("fixture", "fixture-password");
    }

    private static Dictionary<string, object?> Item(int id, string status = "in_progress") => new()
    {
        ["task_id"] = id.ToString(System.Globalization.CultureInfo.InvariantCulture), ["title"] = "Fixture task " + id,
        ["assignee_id"] = 1, ["assignee_name"] = "Fixture assignee", ["task_status"] = status,
        ["start_date"] = "2026-10-16", ["due_date"] = "2026-10-20", ["description"] = "Fixture description"
    };
    private static Task<HttpResponseMessage> Pending(HttpRequestMessage _, CancellationToken cancellation)
    {
        var completion = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        cancellation.Register(() => completion.TrySetCanceled(cancellation));
        return completion.Task;
    }
    private static async Task ExpectCancelled(Func<Task> action)
    {
        try { await action(); throw new Exception("Expected cancellation."); }
        catch (OperationCanceledException) { Check(true, "cancellation remains cancellation"); }
    }
    private static async Task ExpectFailure(Func<Task> action, TaskQueryFailure expected)
    {
        try { await action(); throw new Exception("Expected typed failure."); }
        catch (TaskQueryException error) { Check(error.Failure == expected, "existing failure classification is preserved"); }
    }
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++;
    }
    private static async Task Run(string name, Func<Task> action)
    { await action().WaitAsync(TimeSpan.FromSeconds(20)); scenarios++; Console.WriteLine("PASS " + name); }
    private static HttpResponseMessage Response(object data) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "fixture-request" }), Encoding.UTF8, "application/json") };

    private sealed class ObservedResponse : HttpResponseMessage
    {
        public bool Disposed { get; private set; }
        public ObservedResponse(object data) : base(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "fixture-request" }), Encoding.UTF8, "application/json"); }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    private sealed class QueueContext : SynchronizationContext, IDisposable
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> pending = new();
        private readonly AutoResetEvent available = new(false);
        public override void Post(SendOrPostCallback callback, object? state)
        { lock (pending) pending.Enqueue((callback, state)); available.Set(); }
        public void RunNext()
        {
            (SendOrPostCallback Callback, object? State) next;
            while (true)
            {
                lock (pending) if (pending.Count > 0) { next = pending.Dequeue(); break; }
                if (!available.WaitOne(TimeSpan.FromSeconds(20))) throw new TimeoutException("Expected queued continuation.");
            }
            var previous = Current;
            try { SetSynchronizationContext(null); next.Callback(next.State); }
            finally { SetSynchronizationContext(previous); }
        }
        public void Dispose() => available.Dispose();
    }
    private sealed record RequestInfo(string Path, string? Bearer);
    private sealed class Handler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> pending = new();
        public List<RequestInfo> Requests { get; } = new();
        public bool Disposed { get; private set; }
        public int Remaining => pending.Count;
        public void Enqueue(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) => pending.Enqueue(response);
        public void Success(object data) => Enqueue((_, _) => Task.FromResult(Response(data)));
        public void Error(int status, string code) => Raw(status, JsonSerializer.Serialize(new
        { ok = false, error = new { code, message = "Fixture failure" }, request_id = "fixture-request" }));
        public void Raw(int status, string body) => Enqueue((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        { Content = new StringContent(body, Encoding.UTF8, "application/json") }));
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new(request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter));
            if (pending.Count == 0) throw new Exception("Unexpected test HTTP request.");
            return pending.Dequeue()(request, cancellationToken);
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
}
