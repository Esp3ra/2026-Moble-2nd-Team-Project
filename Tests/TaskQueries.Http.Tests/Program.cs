using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Creation;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

internal static class Program
{
    private static int assertions;
    private static int scenarios;
    private static readonly string[] StatusCodes =
        ["not_started", "in_progress", "submitted", "approved", "returned"];

    [STAThread]
    private static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetDefaultFont(new Font("맑은 고딕", 9));
        Control.CheckForIllegalCrossThreadCalls = true;
        int result = 1;
        using var coordinator = new Form
        {
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = new Point(-20000, -20000),
            Size = new Size(1, 1)
        };
        coordinator.Shown += (_, _) => coordinator.BeginInvoke((Action)(() =>
        {
            try
            {
                if (args.SequenceEqual(new[] { "--live" }))
                    Run("isolated live HTTP and TasksControl", LiveServer);
                else if (args.SequenceEqual(new[] { "--navigation" }))
                    NavigationScenarios();
                else if (args.Length == 0)
                {
                    Run("relative endpoints, Bearer, status mappings and complete list", Endpoints);
                    Run("filtered response integrity and fresh detail status", FilteredResponses);
                    Run("common errors and route absence", Errors);
                    Run("mandatory fields and malformed responses", MalformedResponses);
                    Run("caller and session cancellation", Cancellation);
                    Run("common session cancellation reaches the task screen", SessionCancellationPresentation);
                    Run("idle logout clears task data and explicit new-login refresh recaptures services", IdleSessionInvalidation);
                    Run("completed list transport cannot publish after session invalidation", () => CompletedResponseInvalidation(false));
                    Run("completed detail transport cannot publish after session invalidation", () => CompletedResponseInvalidation(true));
                    Run("queued old invalidation and old rows cannot affect a new session", QueuedInvalidationAfterLogin);
                    Run("logout immediately before row selection safely clears the grid", LogoutBeforeRowSelection);
                    Run("disposed task screen ignores completed response continuations", DisposedResponseContinuation);
                }
                else throw new InvalidOperationException("Use no arguments, --navigation or --live.");
                Console.WriteLine($"PASS: {scenarios} scenarios / {assertions} assertions. "
                    + (!args.Contains("--live") ? "Explicit HTTP test handler; no server or DB tested."
                        : "Explicit isolated live server; production and two-PC operation not tested."));
                result = 0;
            }
            catch (Exception error)
            {
                // Test account credentials and response bodies are never written to output.
                Console.Error.WriteLine($"FAIL after {assertions} assertions: {error.GetType().Name}: {error.Message}");
            }
            finally { coordinator.Close(); }
        }));
        Application.Run(coordinator);
        return result;
    }

    private static void Endpoints()
    {
        using var handler = new TestHandler();
        using var api = new ApiClient("http://localhost:5000/", handler);
        Login(api, handler);
        var service = new HttpTaskQueryService(api);
        Check(service.SourceDescription.Contains("http://localhost:5000/")
            && !service.SourceDescription.Contains("테스트 대역"), "real API origin is visible");

        handler.Success(new { items = StatusCodes.Select((status, index) => Item(index + 1, status)).ToArray() });
        var all = Wait(service.GetTasksAsync(new(TaskQueryScope.Own)));
        Check(all.Count == 5, "all five statuses are returned");
        Check(handler.Last!.Method == HttpMethod.Get && handler.Last.Path == "/api/v1/tasks?scope=own",
            "server prefix occurs once and all omits status");
        Check(handler.Last.Bearer == "fixture-token", "shared token is used");
        for (int index = 0; index < StatusCodes.Length; index++)
        {
            handler.Success(new { items = new[] { Item(index + 1, StatusCodes[index]) } });
            var selected = Wait(service.GetTasksAsync(new(TaskQueryScope.Managed, (TaskDisplayStatus)index)));
            Check(selected.Single().Status == (TaskDisplayStatus)index, "explicit response status mapping");
            Check(handler.Last!.Path == "/api/v1/tasks?scope=managed&status=" + StatusCodes[index],
                "explicit query status mapping");
        }
        handler.Success(new { items = Array.Empty<object>() });
        Check(Wait(service.GetTasksAsync(new(TaskQueryScope.Own))).Count == 0, "empty list remains empty");
        handler.Success(new { items = Enumerable.Range(1, 137).Select(index => Item(index)).ToArray() });
        Check(Wait(service.GetTasksAsync(new(TaskQueryScope.Own))).Count == 137, "complete list is not silently truncated");

        var detailItem = Item(1);
        detailItem["task_id"] = "opaque:one";
        handler.Success(new { task = detailItem });
        var detail = Wait(service.GetTaskAsync("opaque:one"));
        Check(detail.Summary.TaskId == "opaque:one" && detail.Description == "Fixture description",
            "opaque detail identity and description map");
        Check(handler.Last!.Path == "/api/v1/tasks/opaque%3Aone", "detail identifier is escaped");
        int requestCount = handler.Count;
        ExpectArgument(() => Wait(service.GetTasksAsync(new((TaskQueryScope)99))));
        ExpectArgument(() => Wait(service.GetTasksAsync(new(TaskQueryScope.Own, (TaskDisplayStatus)99))));
        ExpectArgument(() => Wait(service.GetTaskAsync(" ")));
        Check(handler.Count == requestCount, "invalid client input is not sent");
    }

    private static void FilteredResponses()
    {
        using var handler = new TestHandler();
        using var api = new ApiClient("http://localhost:5000/", handler);
        var service = new HttpTaskQueryService(api);
        for (int index = 0; index < StatusCodes.Length; index++)
        {
            var query = new TaskListQuery(TaskQueryScope.Own, (TaskDisplayStatus)index);
            string differentStatus = StatusCodes[(index + 1) % StatusCodes.Length];
            handler.Success(new { items = new[] { Item(1, differentStatus) } });
            ExpectFailure(() => Wait(service.GetTasksAsync(query)), TaskQueryFailure.Unavailable);

            // A valid prefix must not become a partial success when a later row violates the filter.
            handler.Success(new { items = new[] { Item(1, StatusCodes[index]), Item(2, differentStatus) } });
            ExpectFailure(() => Wait(service.GetTasksAsync(query)), TaskQueryFailure.Unavailable);
            handler.Success(new { items = Array.Empty<object>() });
            Check(Wait(service.GetTasksAsync(query)).Count == 0, "empty filtered result remains a valid result");
        }

        handler.Success(new { items = StatusCodes.Select((status, index) => Item(index + 1, status)).ToArray() });
        Check(Wait(service.GetTasksAsync(new(TaskQueryScope.Own))).Count == 5,
            "all filter still accepts every defined status");
        handler.Success(new { items = new[] { Item(1, "in_progress") } });
        var listed = Wait(service.GetTasksAsync(new(TaskQueryScope.Own, TaskDisplayStatus.InProgress))).Single();
        handler.Success(new { task = Item(1, "submitted") });
        var fresh = Wait(service.GetTaskAsync(listed.TaskId));
        Check(fresh.Summary.Status == TaskDisplayStatus.Submitted,
            "detail accepts a status changed since the list was read");
        Check(handler.Remaining == 0, "filter validation does not retry or fetch substitute data");
    }

    private static void Errors()
    {
        using var handler = new TestHandler();
        using var api = new ApiClient("http://localhost:5000/", handler);
        Login(api, handler);
        var service = new HttpTaskQueryService(api);
        int expired = 0;
        api.AuthenticationExpired += (_, _) => expired++;
        handler.Error(401, "AUTH_REQUIRED");
        ExpectFailure(() => Wait(service.GetTasksAsync(new(TaskQueryScope.Own))), TaskQueryFailure.AuthenticationExpired);
        Check(expired == 1 && api.CurrentUser is null, "common client owns authentication expiry");

        foreach (var test in new[]
        {
            (403, "FORBIDDEN", TaskQueryFailure.Forbidden),
            (404, "TASK_NOT_FOUND", TaskQueryFailure.NotFound),
            (404, "HTTP_404", TaskQueryFailure.NotConfigured),
            (404, "NOT_FOUND", TaskQueryFailure.Unavailable),
            (500, "SERVER_ERROR", TaskQueryFailure.Unavailable),
            (503, "DATABASE_UNAVAILABLE", TaskQueryFailure.Unavailable)
        })
        {
            handler.Error(test.Item1, test.Item2);
            ExpectFailure(() => Wait(service.GetTaskAsync("1")), test.Item3);
        }
        handler.Raw(502, "<html>gateway unavailable</html>");
        ExpectFailure(() => Wait(service.GetTasksAsync(new(TaskQueryScope.Own))), TaskQueryFailure.Unavailable);
        handler.Raw(401, "");
        ExpectFailure(() => Wait(service.GetTaskAsync("1")), TaskQueryFailure.AuthenticationExpired);
        handler.Enqueue((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("fixture network failure")));
        ExpectFailure(() => Wait(service.GetTasksAsync(new(TaskQueryScope.Own))), TaskQueryFailure.Unavailable);
        handler.Enqueue((_, _) => Task.FromException<HttpResponseMessage>(new TaskCanceledException()));
        ExpectFailure(() => Wait(service.GetTasksAsync(new(TaskQueryScope.Own))), TaskQueryFailure.Unavailable);
        Check(handler.Remaining == 0, "failures consume one request each without automatic retry");
    }

    private static void MalformedResponses()
    {
        using var handler = new TestHandler();
        using var api = new ApiClient("http://localhost:5000/", handler);
        var service = new HttpTaskQueryService(api);
        foreach (string field in new[] { "task_id", "title", "assignee_id", "assignee_name", "task_status", "start_date", "due_date" })
        {
            var item = Item(1);
            item[field] = null;
            RejectList(handler, service, new { items = new[] { item } });
            item.Remove(field);
            RejectList(handler, service, new { items = new[] { item } });
        }
        foreach (var mutation in new (string Field, object Value)[]
        {
            ("task_id", " "), ("title", " "), ("assignee_name", " "), ("assignee_id", 0),
            ("task_status", "pending"), ("task_status", 0), ("assignee_id", "1"),
            ("start_date", "2026-1-01"), ("due_date", "2026-02-30"),
            ("due_date", "2026-10-15"), ("start_date", "2026-10-16T00:00:00Z")
        })
        {
            var item = Item(1);
            item[mutation.Field] = mutation.Value;
            RejectList(handler, service, new { items = new[] { item } });
        }
        RejectList(handler, service, new { items = new[] { Item(1), Item(1) } });
        RejectList(handler, service, new { items = new object?[] { null } });
        RejectList(handler, service, new { items = (object?)null });
        RejectList(handler, service, new { });
        handler.Raw(200, "{invalid-json");
        ExpectFailure(() => Wait(service.GetTasksAsync(new(TaskQueryScope.Own))), TaskQueryFailure.Unavailable);
        handler.Raw(200, "{\"ok\":true,\"data\":null}");
        ExpectFailure(() => Wait(service.GetTasksAsync(new(TaskQueryScope.Own))), TaskQueryFailure.Unavailable);
        handler.Success(new { task = (object?)null });
        ExpectFailure(() => Wait(service.GetTaskAsync("1")), TaskQueryFailure.Unavailable);
        handler.Success(new { task = Item(2) });
        ExpectFailure(() => Wait(service.GetTaskAsync("1")), TaskQueryFailure.Unavailable);
        var detail = Item(1);
        detail["description"] = null;
        handler.Success(new { task = detail });
        ExpectFailure(() => Wait(service.GetTaskAsync("1")), TaskQueryFailure.Unavailable);
        detail.Remove("description");
        handler.Success(new { task = detail });
        ExpectFailure(() => Wait(service.GetTaskAsync("1")), TaskQueryFailure.Unavailable);
        detail["description"] = "";
        handler.Success(new { task = detail });
        Check(Wait(service.GetTaskAsync("1")).Description == "", "empty description is valid when present");
    }

    private static void Cancellation()
    {
        using var handler = new TestHandler();
        using var api = new ApiClient("http://localhost:5000/", handler);
        var service = new HttpTaskQueryService(api);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        ExpectCancelled(() => Wait(service.GetTasksAsync(new(TaskQueryScope.Own), cancelled.Token)));
        ExpectCancelled(() => Wait(service.GetTaskAsync("1", cancelled.Token)));
        Check(handler.Count == 0, "already cancelled queries do not make requests");
        using var during = new CancellationTokenSource();
        handler.Enqueue(Pending);
        var list = service.GetTasksAsync(new(TaskQueryScope.Own), during.Token);
        during.Cancel();
        ExpectCancelled(() => Wait(list));
        handler.Enqueue(Pending);
        var detail = service.GetTaskAsync("1");
        api.ClearIdentity();
        ExpectCancelled(() => Wait(detail));
        Check(handler.Count == 2, "session cancellation remains cancellation, not a server failure");
    }

    private static Task<HttpResponseMessage> Pending(HttpRequestMessage _, CancellationToken token)
    {
        var source = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        token.Register(() => source.TrySetCanceled(token));
        return source.Task;
    }

    private static void SessionCancellationPresentation()
    {
        using var handler = new TestHandler();
        using var api = new ApiClient("http://localhost:5000/", handler);
        Login(api, handler);
        using var page = new TasksControl(PreviewRole.Employee, new HttpTaskQueryService(api));
        using var host = new Form
        {
            ClientSize = new Size(1320, 780), ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000)
        };
        page.Dock = DockStyle.Fill;
        host.Controls.Add(page);
        handler.Success(new { items = Array.Empty<object>() });
        host.Show();
        var status = Field<Label>(page, "lblTaskQueryStatus");
        var retry = Field<Button>(page, "btnRetryQuery");
        var grid = Field<DataGridView>(page, "gridTasks");
        var content = Field<TextBox>(page, "txtContent");
        Until(() => handler.Remaining == 0 && retry.Enabled);

        handler.Enqueue(Pending);
        var list = page.RefreshAsync();
        Check(!list.IsCompleted && !retry.Enabled, "real HTTP list is pending before session cancellation");
        api.ClearIdentity();
        Wait(list);
        Check(api.CurrentUser is null && status.Text.Contains("취소") && !status.Text.Contains("서버 오류"),
            "common session list cancellation is presented as cancellation");
        Check(retry.Enabled && grid.Enabled && grid.Rows.Count == 0,
            "cancelled list restores controls without stale rows");

        Login(api, handler);
        handler.Success(new { items = new[] { Item(1) } });
        handler.Enqueue(Pending);
        var detail = page.RefreshAsync();
        Until(() => handler.Last?.Path == "/api/v1/tasks/1" && !detail.IsCompleted);
        Check(grid.Rows.Count == 1 && !retry.Enabled, "real HTTP detail is pending after the list");
        api.ClearIdentity();
        Wait(detail);
        Check(status.Text.Contains("취소") && !status.Text.Contains("서버 오류"),
            "common session detail cancellation is presented as cancellation");
        Check(retry.Enabled && !content.Text.Contains("Fixture description"),
            "cancelled detail clears old content and permits retry");

        Login(api, handler);
        handler.Success(new { items = new[] { Item(1) } });
        handler.Success(new { task = Item(1) });
        retry.PerformClick();
        Until(() => status.Text == "상세 조회 완료");
        Check(content.Text.Contains("Fixture description") && retry.Enabled,
            "HTTP retry after a new login recovers selected detail");
        Check(handler.Remaining == 0, "session cancellation screen test consumed only planned requests");
    }

    private static void IdleSessionInvalidation()
    {
        WithTaskPage((api, handler, page, host) =>
        {
            var grid = Field<DataGridView>(page, "gridTasks");
            var content = Field<TextBox>(page, "txtContent");
            var retry = Field<Button>(page, "btnRetryQuery");
            var previousPreparation = Field<ITaskPreparationService>(page, "preparationService");
            var previousCreation = Field<TaskCreationWorkflow>(page, "creationWorkflow");
            handler.Success(new { task = Item(2) });
            SelectRow(grid, 1);
            Until(() => content.Text.Contains("Fixture task 2"));
            int beforeLogout = handler.Count;
            api.ClearIdentity();
            DrainUi(host);
            Check(grid.Rows.Count == 0 && !content.Text.Contains("Fixture description"),
                "idle logout clears the previous account list and detail without another request");
            Check(handler.Count == beforeLogout && !previousPreparation.IsCurrent && !previousCreation.IsCurrent,
                "logout invalidates borrowed preparation and creation without performing HTTP");
            Check(!Field<Button>(page, "btnPrepareWrite").Enabled
                && !Field<Button>(page, "btnAcceptTask").Enabled,
                "previous account actions cannot be enabled by retained detail");

            Login(api, handler, userId: 2, role: "admin");
            Check(grid.Rows.Count == 0 && handler.Count == beforeLogout + 1,
                "new login does not implicitly query or restore the previous account data");
            handler.Success(new { items = new[] { Item(3, assigneeId: 2), Item(2, assigneeId: 2) } });
            handler.Success(new { task = Item(3, assigneeId: 2) });
            retry.PerformClick();
            Until(() => Field<Label>(page, "lblTaskQueryStatus").Text == "상세 조회 완료");
            Check(grid.Rows.Count == 2 && ((TaskSummary)grid.SelectedRows[0].Tag!).TaskId == "3",
                "explicit new-login retry selects the fresh first row instead of an old account selection");
            var preparation = Field<ITaskPreparationService>(page, "preparationService");
            var creation = Field<TaskCreationWorkflow>(page, "creationWorkflow");
            Check(preparation.IsCurrent && preparation.UserId == 2 && !ReferenceEquals(preparation, previousPreparation),
                "explicit refresh captures the current account preparation context");
            Check(creation.IsCurrent && creation.UserId == 2 && creation.CanCreate
                && !ReferenceEquals(creation, previousCreation),
                "explicit refresh captures the current account creation workflow");
            Check(Field<ComboBox>(page, "cmbTaskScope").Visible,
                "explicit refresh follows the current administrator role rather than the original employee preview");
            var lastDetail = handler.Requests.Last(x => x.Path.StartsWith("/api/v1/tasks/", StringComparison.Ordinal));
            Check(lastDetail.Path == "/api/v1/tasks/3" && lastDetail.Bearer == "fixture-token-2"
                && handler.Last?.Bearer == "fixture-token-2"
                && handler.Last.Path is "/api/v1/tasks/3" or "/api/v1/results/shared/tasks/3"
                && handler.Remaining == 0,
                "fresh detail and optional attachment selection use only the new session transport");
        });
    }

    private static void CompletedResponseInvalidation(bool detail)
    {
        WithTaskPage((api, handler, page, host) =>
        {
            var grid = Field<DataGridView>(page, "gridTasks");
            var content = Field<TextBox>(page, "txtContent");
            var response = new ObservedResponse(detail
                ? new { task = Item(1) } : new { items = new[] { Item(1), Item(2) } });
            var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (detail) handler.Success(new { items = new[] { Item(1) } });
            handler.Enqueue((_, _) => pending.Task); // Intentionally ignores cancellation: late transport fixture.
            var previousContext = SynchronizationContext.Current;
            using var callbacks = new QueuedSynchronizationContext();
            Task refresh;
            try
            {
                SynchronizationContext.SetSynchronizationContext(callbacks);
                refresh = page.RefreshAsync();
                Check(!refresh.IsCompleted, "screen awaits the deliberately held HTTP response");
                pending.SetResult(response);
                callbacks.RunNext(); // ApiClient receives, validates and disposes the response.
                Check(response.Disposed, "common transport has completed before logout");
                callbacks.RunNext(); // HttpTaskQueryService maps and completes before the view resumes.
                Check(!refresh.IsCompleted && callbacks.Pending > 0,
                    "mapped query completion remains queued before the screen continuation");
                int completedTransportCount = handler.Count;
                api.ClearIdentity();
                DrainUi(host);
                while (!refresh.IsCompleted) callbacks.RunNext();
                refresh.GetAwaiter().GetResult();
                DrainUi(host);
                Check(grid.Rows.Count == 0 && !content.Text.Contains("Fixture description"),
                    "completed previous-session response cannot repopulate visible rows or detail");
                Check(handler.Count == completedTransportCount && handler.Remaining == 0,
                    "previous-session list cannot issue a follow-up detail request after logout");
            }
            finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
        });
    }

    private static void QueuedInvalidationAfterLogin()
    {
        WithTaskPage((api, handler, page, host) =>
        {
            var grid = Field<DataGridView>(page, "gridTasks");
            var content = Field<TextBox>(page, "txtContent");
            Task.Run(api.ClearIdentity).GetAwaiter().GetResult(); // Queue A's invalidation without pumping UI.
            LoginWithoutPumping(api, handler, 2);
            int beforeOldRow = handler.Count;
            Check(grid.Rows.Count == 2, "old account row fixture remains pending before queued invalidation dispatch");
            SelectRow(grid, 1); // Old A row must not become a detail request using B's bearer.
            Check(handler.Count == beforeOldRow, "an old account row cannot request detail through the new account");
            handler.Success(new { items = new[] { Item(3, assigneeId: 2), Item(2, assigneeId: 2) } });
            handler.Success(new { task = Item(3, assigneeId: 2) });
            Task refresh = page.RefreshAsync();
            Check(refresh.IsCompleted, "new account fixture completes before queued old invalidation is dispatched");
            refresh.GetAwaiter().GetResult();
            DrainUi(host);
            Check(grid.Rows.Count == 2 && ((TaskSummary)grid.SelectedRows[0].Tag!).TaskId == "3"
                && content.Text.Contains("Fixture task 3"),
                "queued previous-session invalidation cannot clear a completed new-session refresh");
            Check(Field<ITaskPreparationService>(page, "preparationService").UserId == 2
                && Field<TaskCreationWorkflow>(page, "creationWorkflow").IsCurrent && handler.Remaining == 0,
                "new-session services survive old callback dispatch");
        });
    }

    private static void LogoutBeforeRowSelection()
    {
        WithTaskPage((api, handler, page, host) =>
        {
            var grid = Field<DataGridView>(page, "gridTasks");
            int beforeLogout = handler.Count;
            api.ClearIdentity();
            Check(grid.Rows.Count == 2, "logout invalidation is queued before immediate row selection");
            // SelectionChanged is raised inside DataGridView.CurrentCell's own
            // transition. Clearing rows on that stack would throw/reenter it.
            SelectRow(grid, 1);
            DrainUi(host);
            Check(grid.Rows.Count == 0 && handler.Count == beforeLogout
                && !Field<TextBox>(page, "txtContent").Text.Contains("Fixture description"),
                "immediate old-row selection clears after the current-cell transition without HTTP or stale detail");
        });
    }

    private static void DisposedResponseContinuation()
    {
        WithTaskPage((api, handler, page, host) =>
        {
            var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var response = new ObservedResponse(new { items = new[] { Item(1) } });
            handler.Enqueue((_, _) => pending.Task);
            var previousContext = SynchronizationContext.Current;
            using var callbacks = new QueuedSynchronizationContext();
            try
            {
                SynchronizationContext.SetSynchronizationContext(callbacks);
                Task refresh = page.RefreshAsync();
                pending.SetResult(response);
                callbacks.RunNext();
                callbacks.RunNext();
                Check(response.Disposed && !refresh.IsCompleted && callbacks.Pending > 0,
                    "disposed-screen fixture holds a completed query before the view continuation");
                int count = handler.Count;
                page.Dispose();
                while (!refresh.IsCompleted) callbacks.RunNext();
                refresh.GetAwaiter().GetResult();
                Check(page.IsDisposed && handler.Count == count && handler.Remaining == 0,
                    "disposed screen ignores the completed response without detail requests or continuation errors");
            }
            finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
        });
    }

    private static void NavigationScenarios()
    {
        Run("navigation selects exact BIGINT and explicitly opens own/all/active", NavigationTargetAndScope);
        Run("navigation rejects malformed IDs and missing targets without detail fallback", NavigationInvalidAndMissing);
        Run("navigation reports current list and detail failures without writes", NavigationFailures);
        Run("newer navigation supersedes a delayed list success or failure", NavigationLatestList);
        Run("row selection and filtering supersede delayed navigation detail", NavigationSelectionAndFilter);
        Run("captured navigation does not adopt a later login or server", NavigationSessionChanges);
        Run("late old navigation 401 cannot expire the replacement login", NavigationLateUnauthorized);
        Run("navigation ignores completed transport after logout or disposal", NavigationCompletedTransport);
        Run("navigation rejects worker calls and disposed controls without HTTP", NavigationThreadAndDisposal);
    }

    private static void NavigationTargetAndScope()
    {
        WithNavigationPage((api, handler, page, host) =>
        {
            var navigate = page.CaptureOwnTaskNavigator();
            handler.Success(new { items = new[] { Item(2, "returned") } });
            handler.Success(new { task = Item(2, "returned") });
            Field<ComboBox>(page, "cmbTaskStatusFilter").SelectedIndex = 5;
            Until(() => NavigationSelected(page, "2"));
            handler.Success(new { items = new[] { Item(2, "returned") } });
            handler.Success(new { task = Item(2, "returned") });
            Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 1;
            Until(() => handler.Remaining == 0);
            handler.Success(new { items = new[] { Item(2, "returned") } });
            handler.Success(new { task = Item(2, "returned") });
            Field<CheckBox>(page, "chkIncludeCancelled").Checked = true;
            Until(() => handler.Remaining == 0);
            foreach (string id in new[] { "9007199254740993", "9223372036854775807" })
            {
                int before = handler.Requests.Count;
                handler.Success(new { items = new[] { Item(1), NavigationItem(id) } });
                handler.Success(new { task = NavigationItem(id) });
                Check(Wait(navigate(id)) && NavigationSelected(page, id), "navigation preserves and selects exact large task ID");
                var requests = handler.Requests.Skip(before).ToArray();
                Check(requests.First().Path == "/api/v1/tasks?scope=own"
                    && requests.Count(x => x.Path == "/api/v1/tasks/" + id) == 1
                    && !requests.Any(x => x.Path == "/api/v1/tasks/1"), "navigation loads own list then exact target, never first-row detail");
                Check(Field<ComboBox>(page, "cmbTaskScope").SelectedIndex == 0
                    && Field<ComboBox>(page, "cmbTaskStatusFilter").SelectedIndex == 0
                    && !Field<CheckBox>(page, "chkIncludeCancelled").Checked, "navigation explicitly resets own/all/noncancelled view");
                Check(requests.All(x => x.Method == HttpMethod.Get && x.Bearer == "fixture-token"), "navigation only borrows captured authenticated GETs");
            }
        }, role: "admin");
    }

    private static void NavigationInvalidAndMissing()
    {
        WithNavigationPage((api, handler, page, host) =>
        {
            var navigate = page.CaptureOwnTaskNavigator();
            int before = handler.Count;
            foreach (string id in new[] { "", " ", "0", "01", "-1", "+1", "1/2", "1?scope=managed", "9223372036854775808", "１２", "1 " })
                Check(!Wait(navigate(id)), "malformed navigation task ID is rejected");
            Check(handler.Count == before && NavigationSelected(page, "1"), "invalid input makes no HTTP request or selection change");
            handler.Success(new { items = new[] { Item(1) } });
            before = handler.Count;
            Check(!Wait(navigate("99")), "missing exact target returns false");
            Check(handler.Count == before + 1 && handler.Last?.Path == "/api/v1/tasks?scope=own", "missing target sends no guessed detail request");
            Check(Field<DataGridView>(page, "gridTasks").SelectedRows.Count == 0
                && !Field<TextBox>(page, "txtContent").Text.Contains("Fixture description"), "missing target does not fall back to unrelated first task");
            Check(!Field<Button>(page, "btnAcceptTask").Enabled
                && !Field<Button>(page, "btnPrepareWrite").Enabled, "missing target clears target-dependent actions");
        });
    }

    private static void NavigationFailures()
    {
        foreach (bool detail in new[] { false, true })
        foreach (var failure in new[] { (403, "FORBIDDEN", "권한"), (404, "TASK_NOT_FOUND", "찾을"), (503, "DATABASE_UNAVAILABLE", "서버") })
        {
            WithNavigationPage((api, handler, page, host) =>
            {
                var navigate = page.CaptureOwnTaskNavigator();
                int before = handler.Count;
                if (detail) handler.Success(new { items = new[] { Item(2) } });
                handler.Error(failure.Item1, failure.Item2);
                Check(!Wait(navigate("2")), "current navigation HTTP failure returns false");
                Check(Field<Label>(page, "lblTaskQueryStatus").Text.Contains(failure.Item3)
                    || Field<TextBox>(page, "txtContent").Text.Contains(failure.Item3), "navigation failure preserves useful error classification");
                Check(!Field<TextBox>(page, "txtContent").Text.Contains("Fixture description")
                    && !Field<Button>(page, "btnAcceptTask").Enabled, "failed navigation does not retain successful target actions");
                Check(handler.Count == before + (detail ? 2 : 1) && api.CurrentUser is not null, "ordinary navigation failure neither retries nor expires login");
            });
        }
    }

    private static void NavigationLatestList()
    {
        foreach (bool oldFails in new[] { false, true })
        WithNavigationPage((api, handler, page, host) =>
        {
            var navigate = page.CaptureOwnTaskNavigator();
            var held = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            handler.Enqueue((_, _) => held.Task);
            Task<bool> old = navigate("2");
            handler.Success(new { items = new[] { Item(3) } });
            handler.Success(new { task = Item(3) });
            Check(Wait(navigate("3")) && NavigationSelected(page, "3"), "newer target completes before older list");
            int before = handler.Count;
            held.SetResult(oldFails ? NavigationError(503) : new ObservedResponse(new { items = new[] { Item(2) } }));
            Check(!Wait(old) && NavigationSelected(page, "3"), "late previous list success/error cannot overwrite latest selection");
            Check(handler.Count == before, "superseded list performs no follow-up detail request");
        });
    }

    private static void NavigationSelectionAndFilter()
    {
        foreach (bool filter in new[] { false, true })
        WithNavigationPage((api, handler, page, host) =>
        {
            var navigate = page.CaptureOwnTaskNavigator();
            var held = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            handler.Success(new { items = new[] { Item(2), Item(3) } });
            handler.Enqueue((_, _) => held.Task);
            Task<bool> old = navigate("2");
            Until(() => handler.Last?.Path == "/api/v1/tasks/2");
            if (filter)
            {
                handler.Success(new { items = new[] { Item(3, "returned") } });
                handler.Success(new { task = Item(3, "returned") });
                Field<ComboBox>(page, "cmbTaskStatusFilter").SelectedIndex = 5;
            }
            else
            {
                handler.Success(new { task = Item(3) });
                SelectRow(Field<DataGridView>(page, "gridTasks"), 1);
            }
            Until(() => NavigationSelected(page, "3"));
            int before = handler.Count;
            held.SetResult(new ObservedResponse(new { task = Item(2) }));
            Check(!Wait(old) && NavigationSelected(page, "3"), "user selection/filter wins over navigation detail");
            Check(handler.Count == before, "discarded navigation detail does not load attachments");
            if (filter) Check(Field<ComboBox>(page, "cmbTaskStatusFilter").SelectedIndex == 5, "late navigation does not reset newer filter");
        });
    }

    private static void NavigationSessionChanges()
    {
        foreach (int change in new[] { 0, 1, 2 })
        WithNavigationPage((api, handler, page, host) =>
        {
            var navigate = page.CaptureOwnTaskNavigator();
            api.ClearIdentity();
            if (change == 2) api.SetServer("http://localhost:5001/");
            LoginWithoutPumping(api, handler, change == 1 ? 1 : 2);
            int before = handler.Count;
            Check(!Wait(navigate("2")), "old navigator rejects another account, same-user relogin and server change");
            Check(handler.Count == before, "old navigator does not automatically recapture replacement login");
            DrainUi(host);
            Check(Field<DataGridView>(page, "gridTasks").Rows.Count == 0, "old visible task data is removed after identity change");
            handler.Success(new { items = new[] { Item(3, assigneeId: api.CurrentUser!.Id) } });
            handler.Success(new { task = Item(3, assigneeId: api.CurrentUser!.Id) });
            Wait(page.RefreshAsync());
            before = handler.Count;
            Check(!Wait(navigate("2")) && handler.Count == before && NavigationSelected(page, "3"), "explicit new-session refresh does not revive old navigator");
        });
    }

    private static void NavigationLateUnauthorized()
    {
        WithNavigationPage((api, handler, page, host) =>
        {
            var navigate = page.CaptureOwnTaskNavigator();
            var held = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            handler.Enqueue((_, _) => held.Task);
            Task<bool> old = navigate("2");
            api.ClearIdentity();
            Login(api, handler, 2);
            handler.Success(new { items = new[] { Item(3, assigneeId: 2) } });
            handler.Success(new { task = Item(3, assigneeId: 2) });
            Wait(page.RefreshAsync());
            int before = handler.Count;
            held.SetResult(NavigationError(401));
            Check(!Wait(old) && NavigationSelected(page, "3") && api.CurrentUser?.Id == 2, "late A401 cannot clear B login or B current detail");
            Check(handler.Count == before, "late authentication failure does not issue another request");
        });
    }

    private static void NavigationCompletedTransport()
    {
        foreach (bool detail in new[] { false, true })
        foreach (bool dispose in new[] { false, true })
        WithNavigationPage((api, handler, page, host) =>
        {
            var navigate = page.CaptureOwnTaskNavigator();
            var held = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var response = new ObservedResponse(detail ? new { task = Item(2) } : (object)new { items = new[] { Item(2) } });
            if (detail) handler.Success(new { items = new[] { Item(2) } });
            handler.Enqueue((_, _) => held.Task);
            var previous = SynchronizationContext.Current;
            using var callbacks = new QueuedSynchronizationContext(preserveContext: true);
            try
            {
                SynchronizationContext.SetSynchronizationContext(callbacks);
                Task<bool> pending = navigate("2");
                held.SetResult(response);
                while (!response.Disposed && !pending.IsCompleted) callbacks.RunNext();
                Check(response.Disposed && !pending.IsCompleted, "navigation transport completed while UI continuation remains queued");
                if (dispose) page.Dispose(); else api.ClearIdentity();
                if (!dispose) DrainUi(host);
                int before = handler.Count;
                while (!pending.IsCompleted) callbacks.RunNext();
                Check(!pending.GetAwaiter().GetResult() && handler.Count == before, "completed old response cannot publish or request detail/attachments");
                if (!dispose)
                {
                    while (callbacks.Pending > 0) callbacks.RunNext();
                    SynchronizationContext.SetSynchronizationContext(previous);
                    DrainUi(host);
                    Check(Field<DataGridView>(page, "gridTasks").Rows.Count == 0,
                        "old-session navigation cannot repopulate cleared rows (detail=" + detail + ", status="
                        + Field<Label>(page, "lblTaskQueryStatus").Text + ")");
                }
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        });
    }

    private static void NavigationThreadAndDisposal()
    {
        WithNavigationPage((api, handler, page, host) =>
        {
            var navigate = page.CaptureOwnTaskNavigator();
            int before = handler.Count;
            Check(!Wait(Task.Run(() => navigate("2"))) && handler.Count == before, "worker-thread navigator call is rejected without HTTP");
            var held = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            handler.Enqueue((_, _) => held.Task);
            Task<bool> pending = navigate("2");
            page.Dispose();
            before = handler.Count;
            held.SetResult(new ObservedResponse(new { items = new[] { Item(2) } }));
            Check(!Wait(pending) && !Wait(navigate("2")) && handler.Count == before, "disposed page rejects pending and subsequent navigation without more HTTP");
        });
    }

    private static Dictionary<string, object?> NavigationItem(string id)
    {
        var item = Item(1); item["task_id"] = id; item["title"] = "Fixture task " + id; return item;
    }

    private static HttpResponseMessage NavigationError(int status) => new((HttpStatusCode)status)
    {
        Content = new StringContent(JsonSerializer.Serialize(new
        { ok = false, error = new { code = status == 401 ? "AUTH_REQUIRED" : "DATABASE_UNAVAILABLE", message = "Fixture failure" }, request_id = "fixture-request" }), Encoding.UTF8, "application/json")
    };

    private static bool NavigationSelected(TasksControl page, string id)
    {
        var grid = Field<DataGridView>(page, "gridTasks");
        return grid.SelectedRows.Count == 1 && grid.SelectedRows[0].Tag is TaskSummary item && item.TaskId == id
            && Field<TextBox>(page, "txtContent").Text.Contains("Fixture task " + id)
            && Field<TextBox>(page, "txtContent").Text.Contains("Fixture description");
    }

    private static void WithNavigationPage(Action<ApiClient, TestHandler, TasksControl, Form> check, string role = "employee")
    {
        using var handler = new TestHandler { NavigationAttachments = true };
        using var api = new ApiClient("http://localhost:5000/", handler);
        Login(api, handler, role: role);
        using var page = new TasksControl(role == "admin" ? PreviewRole.Administrator : PreviewRole.Employee, new HttpTaskQueryService(api));
        using var host = new Form { ClientSize = new Size(1320, 780), ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000) };
        page.Dock = DockStyle.Fill; host.Controls.Add(page);
        handler.Success(new { items = new[] { Item(1), Item(2) } });
        handler.Success(new { task = Item(1) });
        host.Show();
        try { Until(() => NavigationSelected(page, "1") && handler.Remaining == 0); }
        catch (TimeoutException)
        {
            throw new InvalidOperationException("Navigation fixture did not load: "
                + Field<Label>(page, "lblTaskQueryStatus").Text + "; last=" + handler.Last?.Path
                + "; remaining=" + handler.Remaining);
        }
        check(api, handler, page, host);
        Check(handler.Remaining == 0, "navigation consumes precisely the planned task responses");
        Check(handler.Requests.All(x => x.Method == HttpMethod.Get || x.Path == "/api/v1/auth/login"), "navigation never sends task writes or confirmation requests");
    }

    private static void WithTaskPage(Action<ApiClient, TestHandler, TasksControl, Form> check)
    {
        using var handler = new TestHandler();
        using var api = new ApiClient("http://localhost:5000/", handler);
        Login(api, handler);
        using var page = new TasksControl(PreviewRole.Employee, new HttpTaskQueryService(api));
        using var host = new Form
        {
            ClientSize = new Size(1320, 780), ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000)
        };
        page.Dock = DockStyle.Fill;
        host.Controls.Add(page);
        handler.Success(new { items = new[] { Item(1), Item(2) } });
        handler.Success(new { task = Item(1) });
        host.Show();
        Until(() => Field<Label>(page, "lblTaskQueryStatus").Text == "상세 조회 완료");
        check(api, handler, page, host);
    }

    private static void SelectRow(DataGridView grid, int index)
    {
        if (grid.Rows.Count <= index) return; // Session invalidation may already have safely removed the rows.
        grid.ClearSelection();
        grid.CurrentCell = grid.Rows[index].Cells[0];
        grid.Rows[index].Selected = true;
    }

    private static void DrainUi(Control host)
    {
        bool reached = false;
        host.BeginInvoke((Action)(() => reached = true));
        var watch = Stopwatch.StartNew();
        while (!reached)
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("UI callback barrier timed out.");
            Application.DoEvents();
            Thread.Yield();
        }
        Application.DoEvents();
    }

    private static void LiveServer()
    {
        Check(Environment.GetEnvironmentVariable("REMOTEWORK_TASK_TEST_ALLOW_LIVE") == "1",
            "explicit isolated live test opt-in is required");
        string Required(string key) => Environment.GetEnvironmentVariable(key)
            is { Length: > 0 } value ? value : throw new InvalidOperationException("Missing live test setting: " + key);
        using var api = new ApiClient(Required("REMOTEWORK_TASK_TEST_URL"));
        var identity = Wait(api.LoginAsync(Required("REMOTEWORK_TASK_TEST_LOGIN_ID"), Required("REMOTEWORK_TASK_TEST_PASSWORD")));
        try
        {
            var service = new HttpTaskQueryService(api);
            var own = Wait(service.GetTasksAsync(new(TaskQueryScope.Own)));
            Check(own.Count > 0, "isolated own fixture is nonempty");
            CheckExpectedIds(own, "REMOTEWORK_TASK_TEST_EXPECTED_OWN_IDS");
            Check(Wait(service.GetTaskAsync(own[0].TaskId)).Summary.TaskId == own[0].TaskId,
                "real HTTP selected detail identity");
            foreach (TaskDisplayStatus status in Enum.GetValues<TaskDisplayStatus>())
            {
                var filtered = Wait(service.GetTasksAsync(new(TaskQueryScope.Own, status)));
                Check(filtered.Count > 0 && filtered.All(task => task.Status == status), "live fixture covers each server status filter");
                Check(filtered.Select(task => task.TaskId).SequenceEqual(
                    own.Where(task => task.Status == status).Select(task => task.TaskId)),
                    "live own filter preserves the complete authorized ordered set");
            }
            IReadOnlyList<TaskSummary>? managed = null;
            if (identity.User.Role == "admin")
            {
                managed = Wait(service.GetTasksAsync(new(TaskQueryScope.Managed)));
                Check(managed.Count > 0, "isolated managed fixture is nonempty");
                CheckExpectedIds(managed, "REMOTEWORK_TASK_TEST_EXPECTED_MANAGED_IDS");
                Check(Wait(service.GetTaskAsync(managed[0].TaskId)).Summary.TaskId == managed[0].TaskId,
                    "real HTTP managed detail");
                foreach (TaskDisplayStatus status in Enum.GetValues<TaskDisplayStatus>())
                {
                    var filtered = Wait(service.GetTasksAsync(new(TaskQueryScope.Managed, status)));
                    Check(filtered.Select(task => task.TaskId).SequenceEqual(
                        managed.Where(task => task.Status == status).Select(task => task.TaskId)),
                        "live managed filter preserves only the designated authorized ordered set");
                }
            }
            else
                ExpectFailure(() => Wait(service.GetTasksAsync(new(TaskQueryScope.Managed))), TaskQueryFailure.Forbidden);
            if (Environment.GetEnvironmentVariable("REMOTEWORK_TASK_TEST_FORBIDDEN_TASK_ID") is { Length: > 0 } forbidden)
                ExpectFailure(() => Wait(service.GetTaskAsync(forbidden)), TaskQueryFailure.Forbidden);

            using var page = new TasksControl(identity.User.Role == "admin" ? PreviewRole.Administrator : PreviewRole.Employee, service);
            using var host = new Form
            {
                ClientSize = new Size(1320, 780), ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000)
            };
            page.Dock = DockStyle.Fill;
            host.Controls.Add(page);
            host.Show();
            Wait(page.RefreshAsync());
            var grid = Field<DataGridView>(page, "gridTasks");
            var content = Field<TextBox>(page, "txtContent");
            var queryStatus = Field<Label>(page, "lblTaskQueryStatus");
            Check(grid.Rows.Count == own.Count && content.Text.Contains(own[0].Title), "real HTTP list and first detail render");
            CheckLiveRows(grid, own, "live own grid preserves authorized IDs and order");
            if (grid.Rows.Count > 1)
            {
                grid.CurrentCell = grid.Rows[1].Cells[0];
                Until(() => queryStatus.Text == "상세 조회 완료" && content.Text.Contains(own[1].Title));
                Check(content.Text.Contains(own[1].TaskId), "live row selection drives detail");
            }
            var filter = Field<ComboBox>(page, "cmbTaskStatusFilter");
            for (int index = 0; index < StatusCodes.Length; index++)
            {
                filter.SelectedIndex = index + 1;
                Until(() => queryStatus.Text == "상세 조회 완료");
                Check(grid.Rows.Count > 0 && grid.Rows.Cast<DataGridViewRow>()
                    .All(row => ((TaskSummary)row.Tag!).Status == (TaskDisplayStatus)index), "live filter renders only selected status");
                CheckLiveRows(grid, own.Where(task => task.Status == (TaskDisplayStatus)index),
                    "live own filtered grid is complete and ordered");
            }
            filter.SelectedIndex = 0;
            Until(() => queryStatus.Text == "상세 조회 완료");
            Check(grid.Rows.Count == own.Count, "live all filter restores full list");
            CheckLiveRows(grid, own, "live all filter restores exact authorized set");
            CheckLivePreparation(page, (TaskSummary)grid.CurrentRow!.Tag!, management: false);
            if (identity.User.Role == "admin")
            {
                Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 1;
                Until(() => queryStatus.Text == "상세 조회 완료");
                Check(grid.Rows.Count > 0, "live managed UI scope renders");
                CheckLiveRows(grid, managed!, "live managed grid includes only designated tasks");
                for (int index = 0; index < StatusCodes.Length; index++)
                {
                    filter.SelectedIndex = index + 1;
                    Until(() => queryStatus.Text == "상세 조회 완료"
                        || (grid.Rows.Count == 0 && queryStatus.Text == "조회된 업무 0개"));
                    CheckLiveRows(grid, managed!.Where(task => task.Status == (TaskDisplayStatus)index),
                        "live managed filtered grid is complete and ordered");
                }
                filter.SelectedIndex = 0;
                Until(() => queryStatus.Text == "상세 조회 완료");
                CheckLiveRows(grid, managed!, "live managed all filter restores exact designated set");
                CheckLivePreparation(page, (TaskSummary)grid.CurrentRow!.Tag!, management: true);
            }
            Check(Field<DataGridView>(page, "gridTaskAttachments").Rows.Count == 0,
                "member 3 attachments remain unconnected");

            // Revoke the server session through a separate caller. The feature's
            // shared ApiClient must discover the 401 on its next real task read.
            using var revoker = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
            using var logout = new HttpRequestMessage(HttpMethod.Post, new Uri(api.ServerUri, "api/v1/auth/logout"));
            logout.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", identity.AccessToken);
            using var revoked = Wait(revoker.SendAsync(logout));
            Check(revoked.IsSuccessStatusCode, "isolated server session was revoked by another caller");
            Wait(page.RefreshAsync());
            Check(grid.Rows.Count == 0 && queryStatus.Text.Contains("인증이 만료"),
                "real revoked-session task read clears the screen and shows authentication expiry");
            Check(api.CurrentUser is null, "common client clears the revoked identity");
        }
        finally { Wait(api.LogoutAsync()); }
    }

    private static void CheckExpectedIds(IReadOnlyList<TaskSummary> tasks, string setting)
    {
        if (Environment.GetEnvironmentVariable(setting) is not { Length: > 0 } raw) return;
        string[]? expected;
        try { expected = JsonSerializer.Deserialize<string[]>(raw); }
        catch (JsonException) { throw new InvalidOperationException("Invalid expected-ID JSON setting: " + setting); }
        Check(expected is not null && expected.All(id => !string.IsNullOrWhiteSpace(id))
            && expected.Distinct(StringComparer.Ordinal).Count() == expected.Length,
            "isolated fixture expected IDs are valid and unique");
        Check(tasks.Select(task => task.TaskId).SequenceEqual(expected!),
            "live response matches the fixture's exact authorized IDs and order");
    }

    private static void CheckLivePreparation(TasksControl page, TaskSummary expected, bool management)
    {
        if (!management)
        {
            if (expected.Status is TaskDisplayStatus.NotStarted or TaskDisplayStatus.Returned)
            {
                WithLiveDialog<TaskAcceptanceForm>(() => Field<Button>(page, "btnAcceptTask").PerformClick(), form =>
                {
                    Check(Field<TextBox>(form, "txtTaskId").Text == expected.TaskId,
                        "real HTTP selected task reaches the matching acceptance/rework target");
                    Check(Field<TextBox>(form, "txtTaskId").ReadOnly
                        && Field<TextBox>(form, "txtTaskStatus").ReadOnly,
                        "acceptance/rework preparation is read-only");
                });
            }
            else Check(!Field<Button>(page, "btnAcceptTask").Enabled,
                "other task states do not expose acceptance/rework target actions");
            WithLiveDialog<DeadlineExtensionForm>(() => Field<Button>(page, "btnDeadlineExtensionPreview").PerformClick(), form =>
            {
                Check(Field<TextBox>(form, "txtTaskSummary").Text.Contains(expected.TaskId)
                    && Field<TextBox>(form, "txtCurrentDeadline").Text == expected.DueDate.ToString("yyyy-MM-dd"),
                    "real HTTP selected task and DATE reach the extension input");
                var desired = Field<DateTimePicker>(form, "dtpRequestedDeadline");
                desired.Value = expected.DueDate.ToDateTime(TimeOnly.MinValue);
                desired.Checked = true;
                var reason = Field<TextBox>(form, "txtExtensionReason");
                reason.Text = "  Local-only live fixture input  ";
                Field<Button>(form, "btnConfirm").PerformClick();
                Check(form.CheckedInput is { } draft && draft.TaskId == expected.TaskId
                    && draft.OriginalDueDate == expected.DueDate && draft.RequestedDueDate == expected.DueDate
                    && draft.Reason == reason.Text && form.DialogResult != DialogResult.OK,
                    "live extension captures exact local values without accepting a request");
                reason.Text += "changed";
                Check(form.CheckedInput is null, "live changed extension invalidates checked input");
            });
            return;
        }
        WithLiveDialog<TaskEditForm>(() => Field<Button>(page, "btnEditTaskPreview").PerformClick(), form =>
        {
            Check(Field<TextBox>(form, "txtTaskTitle").Text == expected.Title,
                "real HTTP managed task reaches its edit input");
            Field<Button>(form, "btnConfirm").PerformClick();
            Check(form.CheckedInput is { } draft && draft.TaskId == expected.TaskId
                && draft.StartDate == expected.StartDate && draft.DueDate == expected.DueDate
                && form.DialogResult != DialogResult.OK,
                "live edit captures the exact local source identity and DATE values");
        });
        WithLiveDialog<TaskEditForm>(() => Field<Button>(page, "btnNewTaskPreview").PerformClick(), form =>
        {
            Field<TextBox>(form, "txtTaskTitle").Text = "Local-only registration fixture";
            var start = Field<DateTimePicker>(form, "dtpStartDate");
            var due = Field<DateTimePicker>(form, "dtpDeadline");
            start.Value = expected.StartDate.ToDateTime(TimeOnly.MinValue);
            due.Value = expected.DueDate.ToDateTime(TimeOnly.MinValue);
            start.Checked = due.Checked = true;
            Field<Button>(form, "btnConfirm").PerformClick();
            Check(form.CheckedInput is { TaskId: null } && form.DialogResult != DialogResult.OK,
                "live registration remains a local draft with no invented persistent identity");
        });
    }

    private static void WithLiveDialog<T>(Action open, Action<T> inspect) where T : Form
    {
        bool found = false;
        Exception? failure = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) =>
        {
            if (Application.OpenForms.OfType<T>().FirstOrDefault() is not { } dialog) return;
            timer.Stop();
            found = true;
            try { inspect(dialog); }
            catch (Exception error) { failure = error; }
            finally { dialog.Close(); }
        };
        timer.Start();
        open();
        timer.Stop();
        if (failure is not null) throw failure;
        Check(found, "the expected preparation dialog opens from the real HTTP task screen");
    }

    private static void CheckLiveRows(DataGridView grid, IEnumerable<TaskSummary> expected, string message) =>
        Check(grid.Rows.Cast<DataGridViewRow>().Select(row => ((TaskSummary)row.Tag!).TaskId)
            .SequenceEqual(expected.Select(task => task.TaskId)), message);

    private static void Login(ApiClient api, TestHandler handler, int userId = 1, string role = "employee")
    {
        QueueLogin(handler, userId, role);
        Wait(api.LoginAsync("fixture", "fixture-password"));
    }

    private static void LoginWithoutPumping(ApiClient api, TestHandler handler, int userId)
    {
        QueueLogin(handler, userId);
        Task<LoginResult> login = api.LoginAsync("fixture", "fixture-password");
        Check(login.IsCompleted, "synthetic new login completes without dispatching old UI callbacks");
        login.GetAwaiter().GetResult();
    }

    private static void QueueLogin(TestHandler handler, int userId, string role = "employee")
    {
        handler.Success(new
        {
            access_token = userId == 1 ? "fixture-token" : "fixture-token-" + userId,
            token_type = "Bearer", expires_at = "2026-10-17T20:00:00Z",
            user = new { id = userId, login_id = "fixture", display_name = "Fixture", employee_no = "TEST-" + userId,
                department = "Test", role, is_active = true }
        });
    }

    private static Dictionary<string, object?> Item(int id, string status = "in_progress", int assigneeId = 1) => new()
    {
        ["task_id"] = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["title"] = "Fixture task " + id, ["assignee_id"] = assigneeId, ["assignee_name"] = "Fixture assignee",
        ["task_status"] = status, ["start_date"] = "2026-10-16", ["due_date"] = "2026-10-20",
        ["description"] = "Fixture description"
    };
    private static void RejectList(TestHandler handler, HttpTaskQueryService service, object data)
    {
        handler.Success(data);
        ExpectFailure(() => Wait(service.GetTasksAsync(new(TaskQueryScope.Own))), TaskQueryFailure.Unavailable);
    }
    private static void ExpectFailure(Action action, TaskQueryFailure failure)
    {
        try { action(); throw new InvalidOperationException("Expected typed task query failure."); }
        catch (TaskQueryException error) { Check(error.Failure == failure, "expected task query failure classification"); }
    }
    private static void ExpectCancelled(Action action)
    {
        try { action(); throw new InvalidOperationException("Expected cancellation."); }
        catch (OperationCanceledException) { Check(true, "cancellation is preserved"); }
    }
    private static void ExpectArgument(Action action)
    {
        try { action(); throw new InvalidOperationException("Expected argument validation."); }
        catch (ArgumentException) { Check(true, "invalid client input rejected"); }
    }
    private static void Run(string name, Action action) { action(); scenarios++; Console.WriteLine("PASS " + name); }
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        assertions++;
    }
    private static T Field<T>(object target, string name) where T : class =>
        (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target)
            ?? throw new InvalidOperationException("Missing UI field: " + name));
    private static T Wait<T>(Task<T> task) { Wait((Task)task); return task.GetAwaiter().GetResult(); }
    private static void Wait(Task task) { Until(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(20)) throw new TimeoutException("Task query test timed out.");
            Application.DoEvents();
            Thread.Sleep(5);
        }
        Application.DoEvents();
    }

    // Each callback is executed without the captured context so the next await
    // continuation must be queued rather than inlined. No wall-clock sleep is
    // used to choose the logout/dispose point between transport, mapping and UI.
    private sealed class QueuedSynchronizationContext : SynchronizationContext, IDisposable
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> callbacks = new();
        private readonly AutoResetEvent posted = new(false);
        private readonly bool preserveContext;
        public QueuedSynchronizationContext(bool preserveContext = false) => this.preserveContext = preserveContext;
        public int Pending { get { lock (callbacks) return callbacks.Count; } }
        public override void Post(SendOrPostCallback callback, object? state)
        {
            lock (callbacks) callbacks.Enqueue((callback, state));
            posted.Set();
        }
        public void RunNext()
        {
            (SendOrPostCallback Callback, object? State) next;
            while (true)
            {
                lock (callbacks)
                    if (callbacks.Count > 0) { next = callbacks.Dequeue(); break; }
                if (!posted.WaitOne(TimeSpan.FromSeconds(20)))
                    throw new TimeoutException("Expected queued task continuation did not arrive.");
            }
            var previous = SynchronizationContext.Current;
            try
            {
                // A fresh proxy prevents await continuations from inlining into
                // the same captured context, while Task.Yield still stays on
                // this explicitly drained UI queue instead of the thread pool.
                SynchronizationContext.SetSynchronizationContext(preserveContext ? new CallbackContext(this) : null);
                next.Callback(next.State);
            }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }
        private sealed class CallbackContext(QueuedSynchronizationContext owner) : SynchronizationContext
        {
            public override void Post(SendOrPostCallback callback, object? state) => owner.Post(callback, state);
        }
        public void Dispose() => posted.Dispose();
    }

    private sealed class ObservedResponse : HttpResponseMessage
    {
        public bool Disposed { get; private set; }
        public ObservedResponse(object data) : base(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "fixture-request" }),
                Encoding.UTF8, "application/json");
        }
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }

    private sealed record RequestInfo(HttpMethod Method, string Path, string? Bearer);
    private sealed class TestHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> responses = new();
        public bool NavigationAttachments { get; init; }
        public List<RequestInfo> Requests { get; } = new();
        public RequestInfo? Last { get; private set; }
        public int Count { get; private set; }
        public int Remaining => responses.Count;
        public void Enqueue(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) => responses.Enqueue(response);
        public void Success(object data) => Raw(200, JsonSerializer.Serialize(new { ok = true, data, request_id = "fixture-request" }));
        public void Error(int status, string code) => Raw(status, JsonSerializer.Serialize(new
        { ok = false, error = new { code, message = "Fixture failure" }, request_id = "fixture-request" }));
        public void Raw(int status, string content) => Enqueue((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        { Content = new StringContent(content, Encoding.UTF8, "application/json") }));
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = new RequestInfo(request.Method, request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.Scheme == "Bearer" ? request.Headers.Authorization.Parameter : null);
            Requests.Add(Last);
            Count++;
            if (NavigationAttachments && request.Method == HttpMethod.Get
                && request.RequestUri.AbsolutePath.StartsWith("/api/v1/results/shared/tasks/", StringComparison.Ordinal))
                return Task.FromResult<HttpResponseMessage>(new ObservedResponse(new { items = Array.Empty<object>() }));
            if (responses.Count == 0) throw new InvalidOperationException("Unexpected HTTP request in test handler.");
            return responses.Dequeue()(request, cancellationToken);
        }
    }
}
