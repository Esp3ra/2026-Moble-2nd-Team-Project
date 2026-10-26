using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Results;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

// Controlled parent-lifetime checks only. No server, result POST or real child
// success is exercised: the timer deliberately supplies the modal return value.
internal static class SubmissionLifetimeChecks
{
    private static int checks;

    public static int Run()
    {
        checks = 0;
        Check(Application.MessageLoop && SynchronizationContext.Current is WindowsFormsSynchronizationContext,
            "lifetime checks require the STA WinForms message loop");
        foreach (string button in new[] { "btnSubmitResult", "btnReviewRequestPreview" })
        {
            RunScenario(Outcome.Cancel, button);
            RunScenario(Outcome.SessionChanged, button);
            RunScenario(Outcome.PageDisposed, button);
        }
        return checks;
    }

    private enum Outcome { Cancel, SessionChanged, PageDisposed }

    private static void RunScenario(Outcome outcome, string buttonName)
    {
        using var handler = new FixtureHandler();
        using var api = new ApiClient("http://lifetime-fixture.invalid/", handler);
        handler.Enqueue(HttpMethod.Post, "/api/v1/auth/login", LoginData(1));
        Complete(api.LoginAsync("fixture", "fixture-password"));
        handler.Enqueue(HttpMethod.Get, "/api/v1/tasks?scope=own", new { items = new[] { TaskItem() } });
        handler.Enqueue(HttpMethod.Get, "/api/v1/tasks/1", new { task = TaskItem() });
        // The selected task also reads its shared task-attachment summary.
        // Keep this exact request in the queue; unknown reads must still fail.
        handler.Enqueue(HttpMethod.Get, "/api/v1/results/shared/tasks/1", new { items = Array.Empty<object>() });

        using var page = new TasksControl(PreviewRole.Employee, new HttpTaskQueryService(api));
        using var host = new Form
        {
            ClientSize = new Size(1320, 780), ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual, Location = new Point(-20000, -20000)
        };
        page.Dock = DockStyle.Fill;
        host.Controls.Add(page);
        var uiErrors = new List<Exception>();
        ThreadExceptionEventHandler collect = (_, args) => uiErrors.Add(args.Exception);
        Application.ThreadException += collect;
        try
        {
            host.Show();
            var submit = Field<Button>(page, buttonName);
            var grid = Field<DataGridView>(page, "gridTasks");
            var content = Field<TextBox>(page, "txtContent");
            if (Field<Label>(page, "lblTaskQueryStatus").Text == "업무 조회를 기다리고 있습니다.")
                Complete(page.RefreshAsync());
            try { Until(() => submit.Enabled && grid.Rows.Count == 1); }
            catch (TimeoutException)
            {
                Console.WriteLine("FAIL controlled fixture readiness: rows=" + grid.Rows.Count
                    + ", submit=" + submit.Enabled + ", status=" + Field<Label>(page, "lblTaskQueryStatus").Text);
                throw;
            }
            Check(content.Text.Contains("Lifetime fixture description", StringComparison.Ordinal),
                "fixture task detail is loaded before the modal action");
            int initialListGets = handler.ListGets;
            int initialRequests = handler.Count;
            handler.EnqueuePending(HttpMethod.Get, "/api/v1/tasks/1");

            bool visited = false;
            bool timedOut = false;
            Exception? timerFailure = null;
            var watch = Stopwatch.StartNew();
            using var timer = new System.Windows.Forms.Timer { Interval = 10 };
            timer.Tick += (_, _) =>
            {
                SubmissionForm? child = Application.OpenForms.OfType<SubmissionForm>().FirstOrDefault();
                if (watch.Elapsed > TimeSpan.FromSeconds(10))
                {
                    timedOut = true;
                    timer.Stop();
                    child?.Close();
                    return;
                }
                if (child is null || !handler.PendingStarted) return;
                timer.Stop();
                visited = true;
                try
                {
                    Check(child.Tag is IDictionary<string, string> tag && tag["TaskId"] == "1",
                        "the modal receives the selected task identifier");
                    Check(!Field<Button>(page, "btnSubmitResult").Enabled
                        && !Field<Button>(page, "btnReviewRequestPreview").Enabled,
                        "both submission entry points prevent modal reentry");
                    if (outcome == Outcome.SessionChanged)
                    {
                        api.ClearIdentity();
                        handler.Enqueue(HttpMethod.Post, "/api/v1/auth/login", LoginData(2));
                        Task<LoginResult> login = api.LoginAsync("fixture", "fixture-password");
                        Check(login.IsCompletedSuccessfully,
                            "replacement fixture login completes before queued old-view callbacks");
                        login.GetAwaiter().GetResult();
                    }
                    else if (outcome == Outcome.PageDisposed)
                        page.Dispose();

                    // This is an explicit simulated modal outcome, not a result
                    // API response or evidence of a successful server write.
                    child.DialogResult = outcome == Outcome.Cancel ? DialogResult.Cancel : DialogResult.OK;
                }
                catch (Exception error) { timerFailure = error; }
                finally { child.Close(); }
            };
            timer.Start();
            submit.PerformClick();
            timer.Stop();
            if (timerFailure is not null) throw timerFailure;
            Check(visited && !timedOut, "the existing submission form reaches the controlled pending read");
            Until(() => handler.PendingCancelled);
            Drain(host);
            Check(handler.Unexpected == 0 && handler.Remaining == 0,
                "only explicitly planned fixture HTTP requests are made");
            Check(handler.ResultPosts == 0 && handler.ListGets == initialListGets,
                "cancelled or stale modal outcomes never write or refresh the task list");
            Check(handler.Count == initialRequests + (outcome == Outcome.SessionChanged ? 2 : 1),
                "modal outcome does not trigger hidden HTTP work");
            Check(Application.OpenForms.OfType<SubmissionForm>().Count() == 0 && uiErrors.Count == 0,
                "modal closure leaves no child window or unhandled UI continuation");

            if (outcome == Outcome.SessionChanged)
            {
                Check(api.CurrentUser?.Id == 2 && grid.Rows.Count == 0
                    && !content.Text.Contains("Lifetime fixture description", StringComparison.Ordinal)
                    && !submit.Enabled,
                    "late simulated success clears the old view without adopting the replacement login");
            }
            else if (outcome == Outcome.PageDisposed)
            {
                Check(page.IsDisposed && api.CurrentUser?.Id == 1,
                    "late simulated success neither revives a disposed view nor clears its borrowed login");
            }
            else
            {
                Check(!page.IsDisposed && grid.Rows.Count == 1 && submit.Enabled
                    && content.Text.Contains("Lifetime fixture description", StringComparison.Ordinal),
                    "ordinary cancel preserves the current selected task without saving");
            }
            Console.WriteLine("PASS controlled submission lifetime: " + buttonName + "/" + outcome + " (simulated modal outcome only).");
        }
        finally { Application.ThreadException -= collect; }
    }

    private static object LoginData(int id) => new
    {
        access_token = "lifetime-fixture-token-" + id, token_type = "Bearer",
        expires_at = "2099-01-01T00:00:00Z",
        user = new { id, login_id = "fixture", display_name = "Fixture", employee_no = "FIX-" + id,
            department = "Fixture", role = "employee", is_active = true }
    };

    private static object TaskItem() => new
    {
        task_id = "1", title = "Lifetime fixture task", assignee_id = 1,
        assignee_name = "Fixture", task_status = "in_progress", start_date = "2026-01-01",
        due_date = "2099-01-01", description = "Lifetime fixture description"
    };

    private static T Field<T>(object target, string name) where T : class =>
        (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target)
            ?? throw new InvalidOperationException("Required lifetime fixture UI field is unavailable."));

    private static void Complete(Task task)
    {
        Until(() => task.IsCompleted);
        task.GetAwaiter().GetResult();
    }

    private static void Until(Func<bool> ready)
    {
        var watch = Stopwatch.StartNew();
        while (!ready())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(15))
                throw new TimeoutException("Controlled submission lifetime condition timed out.");
            Application.DoEvents();
            Thread.Sleep(1);
        }
    }

    private static void Drain(Control dispatcher)
    {
        // A queue barrier after modal cancellation drains its earlier UI callbacks.
        for (int i = 0; i < 4; i++)
        {
            bool reached = false;
            dispatcher.BeginInvoke((Action)(() => reached = true));
            Until(() => reached);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpMethod Method, string Path,
            Func<CancellationToken, Task<HttpResponseMessage>> Reply)> planned = new();
        public int Count { get; private set; }
        public int ListGets { get; private set; }
        public int ResultPosts { get; private set; }
        public int Unexpected { get; private set; }
        public int Remaining => planned.Count;
        public bool PendingStarted { get; private set; }
        public bool PendingCancelled { get; private set; }

        public void Enqueue(HttpMethod method, string path, object data) => planned.Enqueue((method, path,
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data,
                    request_id = "lifetime-fixture-request" }), Encoding.UTF8, "application/json")
            })));

        public void EnqueuePending(HttpMethod method, string path) => planned.Enqueue((method, path, token =>
        {
            PendingStarted = true;
            var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            token.Register(() => { PendingCancelled = true; pending.TrySetCanceled(token); });
            return pending.Task;
        }));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Count++;
            string path = request.RequestUri!.PathAndQuery;
            if (request.Method == HttpMethod.Get && path.StartsWith("/api/v1/tasks?", StringComparison.Ordinal)) ListGets++;
            if (request.Method == HttpMethod.Post && path.StartsWith("/api/v1/results/", StringComparison.Ordinal)) ResultPosts++;
            if (planned.Count == 0 || planned.Peek().Method != request.Method || planned.Peek().Path != path)
            {
                Unexpected++;
                throw new InvalidOperationException("Unexpected request in controlled submission lifetime fixture.");
            }
            return planned.Dequeue().Reply(token);
        }
    }
}
