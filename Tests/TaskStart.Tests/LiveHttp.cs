using System.Globalization;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Features.Tasks.Starting;
using RemoteWork.WinForms.Infrastructure;

internal static partial class Program
{
    // Only explicit --http reads one private stdin line from the owned fixture runner.
    // Never print input, URLs, users, passwords, bearer tokens, request keys or raw failures.
    private static async Task LiveHttp()
    {
        string? line = await Console.In.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
        if (line is null) throw new InvalidOperationException("Owned fixture input is required.");
        using var input = JsonDocument.Parse(line);
        var expected = new HashSet<string>(new[] { "server_root", "login_id", "password", "accept_task_id", "rework_task_id" }, StringComparer.Ordinal);
        if (input.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Invalid private fixture input.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in input.RootElement.EnumerateObject())
            if (!expected.Contains(field.Name) || !seen.Add(field.Name) || field.Value.ValueKind != JsonValueKind.String
                || string.IsNullOrEmpty(field.Value.GetString())) throw new InvalidOperationException("Invalid private fixture input.");
        if (seen.Count != expected.Count) throw new InvalidOperationException("Invalid private fixture input.");
        string Text(string name) => input.RootElement.GetProperty(name).GetString()!;
        string acceptId = Text("accept_task_id"), reworkId = Text("rework_task_id");
        if (acceptId == reworkId) throw new InvalidOperationException("Independent owned fixture tasks are required.");
        using var handler = new LiveHandler();
        using var api = new ApiClient(Text("server_root"), handler);
        await api.LoginAsync(Text("login_id"), Text("password"));
        var session = api.CaptureSession();
        var query = new HttpTaskQueryService(api);
        var prep = new HttpTaskPreparationService(session);
        var service = new HttpTaskStartService(session);
        Check(service.IsCurrent && service.UserId > 0, "Actual common login supplies a captured authenticated session");
        foreach (var (kind, id) in new[] { (TaskStartKind.Accept, acceptId), (TaskStartKind.Rework, reworkId) })
        {
            var detail = await query.GetTaskAsync(id);
            var before = await prep.GetContextAsync(id);
            var selected = TaskStartTargetPreparation.Check(kind, detail, before, service.UserId);
            Check(selected.Outcome == TaskStartTargetOutcome.Matched && selected.Target is not null,
                "Actual detail/current-context agrees with the current assignee and expected source status");
            var request = TaskStartRequest.FromCheckedTarget(selected.Target!);
            string expectedVersion = checked(long.Parse(request.ExpectedRowVersion, CultureInfo.InvariantCulture) + 1)
                .ToString(CultureInfo.InvariantCulture);
            using var workflow = new TaskStartWorkflow(service);
            await workflow.SubmitAsync(request);
            if (kind == TaskStartKind.Accept)
            {
                Check(workflow.State == TaskStartState.NeedsResultCheck && workflow.Result is null && !workflow.CanRetry
                    && handler.StartAttempts(id) == 1, "A real committed acceptance response loss requires explicit lookup without another POST");
                await workflow.CheckResultAsync();
            }
            Check(workflow.State == TaskStartState.Succeeded && workflow.Result is { Status: TaskDisplayStatus.InProgress } result
                && result.TaskId == id && result.RowVersion == expectedVersion && result.AssignmentRevision == before.AssignmentRevision,
                "Real task mutation returns exactly the expected next version with unchanged assignment");
            Check(handler.LastStartStatus == 200, "Actual direct task mutation HTTP status is 200");
            var key = new TaskStartKey(kind, id, handler.RequestKey(id), DateTimeOffset.UtcNow.AddHours(1));
            var lookup = await service.GetResultAsync(request, key);
            Check(lookup.Recorded && lookup.Result?.RowVersion == expectedVersion && lookup.Result.TaskId == id,
                "The real common receipt endpoint retains HTTP200 and the verified minimal result");
            var replay = await service.StartAsync(request, key);
            var after = await prep.GetContextAsync(id);
            Check(replay.RowVersion == expectedVersion && after.RowVersion == expectedVersion
                && after.AssignmentRevision == before.AssignmentRevision && after.Status == TaskDisplayStatus.InProgress
                && handler.SameRequest(id) && handler.StartAttempts(id) == 2 && handler.LastStartStatus == 200,
                "Same-key real POST replays its receipt without another version increment or a different body");
            var latest = await query.GetTaskAsync(id);
            Check(latest.Summary.Status == TaskDisplayStatus.InProgress, "Actual detail query sees the newly stored in-progress state");
        }
        var own = await query.GetTasksAsync(new TaskListQuery(TaskQueryScope.Own, TaskDisplayStatus.InProgress));
        Check(own.Any(t => t.TaskId == acceptId) && own.Any(t => t.TaskId == reworkId)
            && own.All(t => t.Status == TaskDisplayStatus.InProgress),
            "Actual own-task in-progress filter includes both assigned mutated fixture tasks");
    }

    private sealed class LiveHandler : DelegatingHandler
    {
        private sealed record First(string Key, string Body);
        private readonly Dictionary<string, First> first = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> attempts = new(StringComparer.Ordinal);
        private readonly HashSet<string> changedRequest = new(StringComparer.Ordinal);
        public int LastStartStatus { get; private set; }
        public LiveHandler() : base(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { }
        public int StartAttempts(string id) => attempts.TryGetValue(id, out int count) ? count : 0;
        public string RequestKey(string id) => first[id].Key;
        public bool SameRequest(string id) => !changedRequest.Contains(id);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string[] path = request.RequestUri!.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            bool start = request.Method == HttpMethod.Post && path is ["api", "v1", "tasks", _, "accept" or "rework-start"];
            if (!start) return await base.SendAsync(request, ct);
            string id = path[3], key = request.Headers.GetValues("Idempotency-Key").Single();
            string body = await request.Content!.ReadAsStringAsync(ct);
            int count = StartAttempts(id) + 1;
            attempts[id] = count;
            if (count == 1) first.Add(id, new(key, body));
            else if (first[id].Key != key || first[id].Body != body) changedRequest.Add(id);
            var response = await base.SendAsync(request, ct);
            LastStartStatus = (int)response.StatusCode;
            if (count == 1 && path[4] == "accept" && LastStartStatus == 200)
            {
                await response.Content.ReadAsByteArrayAsync(ct);
                response.Dispose();
                throw new HttpRequestException("Synthetic post-commit response loss in owned fixture.");
            }
            return response;
        }
    }
}
