using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Queries;
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
                else if (args.Length == 0)
                {
                    Run("relative endpoints, Bearer, status mappings and complete list", Endpoints);
                    Run("filtered response integrity and fresh detail status", FilteredResponses);
                    Run("common errors and route absence", Errors);
                    Run("mandatory fields and malformed responses", MalformedResponses);
                    Run("caller and session cancellation", Cancellation);
                    Run("common session cancellation reaches the task screen", SessionCancellationPresentation);
                }
                else throw new InvalidOperationException("Use no arguments or --live.");
                Console.WriteLine($"PASS: {scenarios} scenarios / {assertions} assertions. "
                    + (args.Length == 0 ? "Explicit HTTP test handler; no server or DB tested."
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

    private static void Login(ApiClient api, TestHandler handler)
    {
        handler.Success(new
        {
            access_token = "fixture-token", token_type = "Bearer", expires_at = "2026-10-17T20:00:00Z",
            user = new { id = 1, login_id = "fixture", display_name = "Fixture", employee_no = "TEST-1", department = "Test", role = "employee", is_active = true }
        });
        Wait(api.LoginAsync("fixture", "fixture-password"));
    }

    private static Dictionary<string, object?> Item(int id, string status = "in_progress") => new()
    {
        ["task_id"] = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["title"] = "Fixture task " + id, ["assignee_id"] = 1, ["assignee_name"] = "Fixture assignee",
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

    private sealed record RequestInfo(HttpMethod Method, string Path, string? Bearer);
    private sealed class TestHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> responses = new();
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
            Count++;
            if (responses.Count == 0) throw new InvalidOperationException("Unexpected HTTP request in test handler.");
            return responses.Dequeue()(request, cancellationToken);
        }
    }
}
