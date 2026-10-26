using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using RemoteWork.WinForms.Features.Results;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Features.Tasks.ReviewRequests;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

internal static class Program
{
    private static int checks;
    private static string stage = "startup";
    private static Settings settings = null!;
    private const string ReviewReason = "Integration review request · 검토 요청 확인";
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    private sealed record Account(string LoginId, string Password, int Id);
    private sealed record Settings(string Server, string Storage, Account Manager, Account Worker, Account Upper);
    private sealed class CheckFailure(string message) : Exception(message);

    [STAThread]
    private static int Main(string[] args)
    {
        bool live = args.SequenceEqual(new[] { "--http" });
        bool lifetime = args.SequenceEqual(new[] { "--lifetime-only" });
        if (!live && !lifetime)
        {
            Console.Error.WriteLine("Use the explicit disposable MySQL runner; private settings are read from stdin.");
            return 2;
        }
        if (live)
            settings = JsonSerializer.Deserialize<Settings>(Console.In.ReadToEnd(), ApiClient.Json)
                ?? throw new InvalidOperationException("Explicit private settings are required.");
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Control.CheckForIllegalCrossThreadCalls = true;
        int result = 1;
        using var coordinator = new Form
        {
            ShowInTaskbar = false, Location = new Point(-20000, -20000),
            StartPosition = FormStartPosition.Manual, Size = new Size(1, 1)
        };
        coordinator.Shown += (_, _) => coordinator.BeginInvoke((Action)(() =>
        {
            try
            {
                if (lifetime)
                {
                    checks = SubmissionLifetimeChecks.Run();
                    Console.WriteLine($"PASS TaskResults lifetime: {checks} checks.");
                }
                else
                {
                    Run();
                    Console.WriteLine($"PASS TaskResults integration: {checks} checks.");
                }
                result = 0;
            }
            catch (Exception error)
            {
                // Never echo API envelopes, credentials, tokens, body text or arbitrary exception messages.
                string label = error is CheckFailure ? error.Message : error.GetType().Name;
                Console.Error.WriteLine($"FAIL TaskResults integration at {stage}: {label}");
            }
            finally { coordinator.Close(); }
        }));
        Application.Run(coordinator);
        return result;
    }

    private static void Run()
    {
        Directory.CreateDirectory(settings.Storage);
        using var managerTap = new ObservedTransport();
        using var workerTap = new ObservedTransport();
        using var upperTap = new ObservedTransport();
        using var manager = Login(settings.Manager, managerTap);
        using var worker = Login(settings.Worker, workerTap);
        using var upper = Login(settings.Upper, upperTap);

        stage = "explicit task creation";
        string distractor = Create(manager, "integration-distractor", settings.Worker.Id);
        string employeeTask = Create(manager, "integration-employee", settings.Worker.Id);
        string adminTask = Create(manager, "integration-admin-own", settings.Manager.Id);
        Check(new[] { distractor, employeeTask, adminTask }.Distinct().Count() == 3,
            "three independent tasks are created only through the real registration API");

        Flow(worker, workerTap, manager, employeeTask, "employee");
        Flow(manager, managerTap, upper, adminTask, "admin-own");

        stage = "final filters and permission boundaries";
        var workerQueries = new HttpTaskQueryService(worker);
        var own = Wait(workerQueries.GetTasksAsync(new(TaskQueryScope.Own)));
        Check(own.Select(item => item.TaskId).SequenceEqual(new[] { distractor, employeeTask }),
            "employee still receives exactly both permitted tasks in stable order");
        foreach (TaskDisplayStatus status in Enum.GetValues<TaskDisplayStatus>())
        {
            var rows = Wait(workerQueries.GetTasksAsync(new(TaskQueryScope.Own, status)));
            Check(rows.All(item => item.Status == status), "each of the five filters returns only its requested state");
            Check(rows.Count == (status is TaskDisplayStatus.NotStarted or TaskDisplayStatus.Approved ? 1 : 0),
                "all five filter counts match the actual committed states");
        }
        var managed = Wait(new HttpTaskQueryService(manager).GetTasksAsync(new(TaskQueryScope.Managed)));
        Check(managed.Select(item => item.TaskId).ToHashSet().SetEquals(new[] { distractor, employeeTask, adminTask }),
            "creator manager sees only its three explicitly managed tasks");
        var upperManaged = Wait(new HttpTaskQueryService(upper).GetTasksAsync(new(TaskQueryScope.Managed)));
        Check(upperManaged.Count == 0, "upper reviewer status does not grant the member2 managed-task relationship");
        ExpectForbidden(() => Wait(workerQueries.GetTasksAsync(new(TaskQueryScope.Managed))));
        ExpectForbidden(() => Wait(workerQueries.GetTaskAsync(adminTask)));
        Check(Wait(workerQueries.GetTaskAsync(distractor)).Summary.Status == TaskDisplayStatus.NotStarted,
            "the unselected distractor remains unchanged");

        var requests = managerTap.Snapshot().Concat(workerTap.Snapshot()).Concat(upperTap.Snapshot()).ToArray();
        Check(requests.Count(request => request.Method == HttpMethod.Post && request.Path == "/api/v1/tasks") == 3,
            "exactly three registration requests were sent");
        Check(requests.Count(request => request.Method == HttpMethod.Post && request.Path.EndsWith("/accept")) == 2
            && requests.Count(request => request.Method == HttpMethod.Post && request.Path.EndsWith("/rework-start")) == 2,
            "accept and rework execute once per real task");
        Check(requests.Count(request => request.Method == HttpMethod.Post && request.Path.EndsWith("/submissions")) == 4,
            "two submission rounds per task are the only submitted writes");
        Check(requests.Count(request => request.Method == HttpMethod.Post && request.Path.EndsWith("/review-requests")) == 2
            && requests.Count(request => request.Method == HttpMethod.Post && request.Path.EndsWith("/acknowledge")) == 2,
            "each submitted task sends one separate review request and its reviewer acknowledges it once");
        Check(!requests.Any(request => request.Method == HttpMethod.Patch
                || request.Path.Contains("/status", StringComparison.Ordinal)),
            "member2 never sends a separate status mutation after member3 saves");
    }

    private static ApiClient Login(Account account, ObservedTransport transport)
    {
        var api = new ApiClient(settings.Server, transport);
        Wait(api.LoginAsync(account.LoginId, account.Password));
        Check(api.CurrentUser?.Id == account.Id, "real common authentication returns the synthetic account ID");
        return api;
    }

    private static string Create(ApiClient api, string title, int assigneeId)
    {
        var session = api.CaptureSession();
        DateOnly today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).Date);
        var result = Wait(session.PostIdempotentAsync<JsonElement>("tasks", new
        {
            title, description = "Explicit owned integration fixture created through HTTP",
            start_date = today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            due_date = today.AddDays(3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            assignee_id = assigneeId, manager_user_ids = new[] { settings.Manager.Id }
        }, Key(session, "task.created", "new")));
        Check(result.GetProperty("task_status").GetString() == "not_started"
            && result.GetProperty("row_version").GetString() == "1"
            && result.GetProperty("assignment_revision").GetInt32() == 1,
            "real registration starts at not_started and both version counters one");
        return result.GetProperty("task_id").GetString()!;
    }

    private static void Flow(ApiClient api, ObservedTransport tap, ApiClient reviewer, string taskId, string label)
    {
        stage = label + " task screen";
        using var page = new TasksControl(PreviewRole.Employee, new HttpTaskQueryService(api));
        using var host = new Form
        {
            ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
            Location = new Point(-20000, -20000), ClientSize = new Size(1320, 780)
        };
        page.Dock = DockStyle.Fill;
        host.Controls.Add(page);
        host.Show();
        stage = label + " initial list";
        if (Field<Label>(page, "lblTaskQueryStatus").Text == "업무 조회를 기다리고 있습니다.")
            Wait(page.RefreshAsync());
        Until(() => Field<Label>(page, "lblTaskQueryStatus").Text == "상세 조회 완료");
        stage = label + " selected task";
        Select(page, taskId);
        Check(!Field<Button>(page, "btnSubmitResult").Enabled && !Field<Button>(page, "btnReviewRequestPreview").Enabled,
            "not_started task cannot open submission or review request");
        stage = label + " acceptance";
        Start(page, taskId);
        Check(Current(page)?.Status == TaskDisplayStatus.InProgress && Field<Button>(page, "btnSubmitResult").Enabled
            && !Field<Button>(page, "btnReviewRequestPreview").Enabled,
            "actual acceptance enables submission but separate review request waits for a submitted result");
        var accepted = Wait(new HttpTaskPreparationService(api.CaptureSession()).GetContextAsync(taskId));

        if (api.CurrentUser!.Role == "admin")
        {
            stage = label + " managed scope";
            Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 1;
            Until(() => Field<Label>(page, "lblTaskQueryStatus").Text == "상세 조회 완료");
            Check(!Field<Button>(page, "btnSubmitResult").Visible && !Field<Button>(page, "btnReviewRequestPreview").Visible,
                "management view hides both own submission actions");
            Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 0;
            stage = label + " own scope restored";
            Until(() => Field<Label>(page, "lblTaskQueryStatus").Text == "상세 조회 완료");
            Select(page, taskId);
        }

        stage = label + " canceled submission";
        int listGets = tap.Count(HttpMethod.Get, "/api/v1/tasks");
        int submitPosts = tap.Count(HttpMethod.Post, $"/api/v1/results/tasks/{taskId}/submissions");
        Submission(page, taskId, 1, null, cancel: true);
        Check(tap.Count(HttpMethod.Get, "/api/v1/tasks") == listGets
            && tap.Count(HttpMethod.Post, $"/api/v1/results/tasks/{taskId}/submissions") == submitPosts,
            "canceling the real submission dialog does not cause a list refresh or submission write");

        stage = label + " first submission through member2";
        Field<ComboBox>(page, "cmbTaskStatusFilter").SelectedIndex = 2; // in_progress
        Until(() => Field<Label>(page, "lblTaskQueryStatus").Text == "상세 조회 완료");
        Select(page, taskId);
        string file = Path.Combine(settings.Storage, "client-source-" + label + ".any");
        byte[] bytes = Enumerable.Range(0, 4096).Select(index => (byte)(index % 251)).ToArray();
        File.WriteAllBytes(file, bytes);
        Submission(page, taskId, 1, file);
        Until(() => Field<DataGridView>(page, "gridTasks").Rows.Count == 0
            && Field<Button>(page, "btnRetryQuery").Enabled);
        Check(Field<ComboBox>(page, "cmbTaskStatusFilter").SelectedIndex == 2,
            "successful submission preserves in_progress filter while the changed task disappears");
        Check(!Field<Button>(page, "btnSubmitResult").Enabled && !Field<Button>(page, "btnReviewRequestPreview").Enabled,
            "empty filtered list cannot submit or open a sample review request");
        Check(!Field<TextBox>(page, "txtContent").Text.Contains("Explicit owned integration fixture"),
            "successful filtered submission does not retain the previous task detail");
        Field<ComboBox>(page, "cmbTaskStatusFilter").SelectedIndex = 0;
        Until(() => Field<Label>(page, "lblTaskQueryStatus").Text == "상세 조회 완료");
        Select(page, taskId);
        Check(Current(page)?.Status == TaskDisplayStatus.Submitted && !Field<Button>(page, "btnSubmitResult").Enabled
            && Field<Button>(page, "btnReviewRequestPreview").Enabled,
            "submitted status enables only the separate review request without another state POST");
        JsonElement first = Submissions(api, taskId).EnumerateArray().Single();
        Check(first.GetProperty("round_no").GetString() == "1"
            && first.GetProperty("attachments").GetArrayLength() == 1,
            "first actual form submission stores one first-round attachment");
        JsonElement reviewTasks = Wait(reviewer.CaptureSession().GetAsync<JsonElement>("results/review-tasks"));
        Check(reviewTasks.GetProperty("items").EnumerateArray().Any(item =>
            item.GetProperty("task_id").GetString() == taskId && item.GetProperty("task_status").GetString() == "submitted"),
            "actual submission appears in the authenticated reviewer's result pending list");
        Check(first.GetProperty("description").GetString() == Description(1),
            "native Korean and newline edit input is persisted by the real first submission");

        stage = label + " separate review request and acknowledgment";
        SeparateReview(page, api, reviewer, taskId, first.GetProperty("submission_id").GetString()!);

        stage = label + " review return";
        Decide(reviewer, first.GetProperty("submission_id").GetString()!, "returned");
        Wait(page.RefreshAsync());
        Select(page, taskId);
        Check(Current(page)?.Status == TaskDisplayStatus.Returned && !Field<Button>(page, "btnSubmitResult").Enabled
            && !Field<Button>(page, "btnReviewRequestPreview").Enabled,
            "returned task cannot directly reopen submission before rework");
        // MySQL DATETIME(0) may round the accepted instant forward. Wait only to
        // that returned server instant; do not fake a timestamp or retry a write.
        while (DateTimeOffset.UtcNow < accepted.UpdatedAt)
            Wait(Task.Delay(accepted.UpdatedAt - DateTimeOffset.UtcNow > TimeSpan.FromMilliseconds(20)
                ? TimeSpan.FromMilliseconds(20) : TimeSpan.FromMilliseconds(1)));
        stage = label + " member2 rework";
        Start(page, taskId);
        Check(Current(page)?.Status == TaskDisplayStatus.InProgress,
            "explicit member2 rework starts before resubmission");

        stage = label + " second submission through member2";
        Submission(page, taskId, 2, null);
        Until(() => Current(page)?.TaskId == taskId && Current(page)?.Status == TaskDisplayStatus.Submitted);
        JsonElement[] submitted = Submissions(api, taskId).EnumerateArray().ToArray();
        Check(submitted.Length == 2 && submitted[0].GetProperty("round_no").GetString() == "2"
            && submitted[1].GetProperty("description").GetString() == Description(1)
            && submitted[0].GetProperty("description").GetString() == Description(2),
            "second successful submission increments the round and retains the first description");

        stage = label + " review approval and preserved file";
        Decide(reviewer, submitted[0].GetProperty("submission_id").GetString()!, "approved");
        Wait(page.RefreshAsync());
        Select(page, taskId);
        Check(Current(page)?.Status == TaskDisplayStatus.Approved && !Field<Button>(page, "btnSubmitResult").Enabled
            && !Field<Button>(page, "btnReviewRequestPreview").Enabled,
            "approval refresh shows approved and disables further submission");
        string attachmentId = first.GetProperty("attachments")[0].GetProperty("attachment_id").GetString()!;
        JsonElement chunk = Wait(reviewer.CaptureSession().GetAsync<JsonElement>(
            $"results/attachments/{attachmentId}/chunks?offset=0"));
        Check(Convert.FromBase64String(chunk.GetProperty("data").GetString()!).SequenceEqual(bytes)
            && chunk.GetProperty("eof").GetBoolean(), "original submission attachment is still readable after rework and approval");
        JsonElement history = Wait(api.CaptureSession().GetAsync<JsonElement>($"results/tasks/{taskId}/history"));
        Check(history.GetProperty("items").GetArrayLength() == 4, "two submissions and two decisions create exactly four result events");
        File.Delete(file);
    }

    private static JsonElement Submissions(ApiClient api, string id) =>
        Wait(api.CaptureSession().GetAsync<JsonElement>($"results/tasks/{id}/submissions")).GetProperty("items");

    private static void Decide(ApiClient api, string submissionId, string outcome)
    {
        ApiClient.Session session = api.CaptureSession();
        JsonElement context = Wait(session.GetAsync<JsonElement>($"results/submissions/{submissionId}/context"));
        JsonElement result = Wait(session.PostIdempotentAsync<JsonElement>($"results/submissions/{submissionId}/decisions", new
        {
            expected_row_version = context.GetProperty("row_version").GetString(),
            expected_assignment_revision = context.GetProperty("assignment_revision").GetInt32(),
            outcome, reason = outcome == "returned" ? "Integration correction" : ""
        }, Key(session, "submission." + outcome, submissionId)));
        Check(result.GetProperty("task_status").GetString() == outcome, "real current reviewer decision persists the intended outcome");
    }

    private static string Key(ApiClient.Session session, string action, string target) =>
        Wait(session.PostAsync<JsonElement>("write-requests/keys", new { action, target })).GetProperty("request_key").GetString()!;

    private static void Start(TasksControl page, string taskId)
    {
        int step = 0;
        bool checkedId = false;
        DriveModal<TaskAcceptanceForm>(() => Field<Button>(page, "btnAcceptTask").PerformClick(), form =>
        {
            if (!checkedId)
            {
                checkedId = true;
                Check(Field<TextBox>(form, "txtTaskId").Text == taskId, "member2 start dialog receives the selected task ID");
            }
            Button confirm = Field<Button>(form, "btnConfirmTarget");
            if (step < 2 && confirm.Enabled) { step++; confirm.PerformClick(); }
            if (form.ShouldRefreshList && Field<Label>(form, "lblPreparationStatus").Text.Contains("성공")) form.Close();
        });
        Until(() => Current(page)?.TaskId == taskId && Current(page)?.Status == TaskDisplayStatus.InProgress);
        Check(step == 2, "start uses separate target confirmation and actual execution");
    }

    private static string Description(int round) => "Integration result " + round + "\r\n한글 결과 " + round;

    private static void NativeType(TextBox box, string value)
    {
        Check(!box.ReadOnly && box.Enabled, "actual input control is editable before native character input");
        box.Focus();
        foreach (char character in value.Replace("\r\n", "\r", StringComparison.Ordinal))
            SendMessageW(box.Handle, 0x0102, (IntPtr)character, IntPtr.Zero);
        Check(box.Text == value, "WM_CHAR input retains exact Korean text and requested newline");
    }

    private static void Submission(TasksControl page, string taskId, int round, string? attachment, bool cancel = false)
    {
        bool ready = false;
        DriveModal<SubmissionForm>(() => Field<Button>(page, "btnSubmitResult").PerformClick(), form =>
        {
            if (ready || Field<bool>(form, "busy") || !Field<Button>(form, "btnSubmit").Enabled) return;
            ready = true;
            Check(!Application.OpenForms.OfType<ReviewRequestForm>().Any(),
                "result submission remains separate from the manual review-request form");
            Check(form.Tag is IDictionary<string, string> data && data["TaskId"] == taskId,
                "member2 hands the selected task ID to the real member3 submission form");
            Check(Field<TextBox>(form, "txtSubmissionVersion").Text.Contains(round + "차"),
                "member3 reads the actual next submission round");
            Check(Field<DataGridView>(form, "gridPreviousFiles").Rows.Count == (round == 1 ? 0 : 1),
                "member3 previous attachment view reflects only actual earlier submissions");
            if (cancel) { form.Close(); return; }
            NativeType(Field<TextBox>(form, "txtResultDescription"), Description(round));
            if (attachment is not null)
                form.GetType().GetMethod("AddAttachments", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(form, new object[] { new[] { attachment } });
            Field<Button>(form, "btnSubmit").PerformClick();
        });
        Check(ready, "the real member3 submission form becomes ready without preview data");
    }

    private static void SeparateReview(TasksControl page, ApiClient submitter, ApiClient reviewer, string taskId, string submissionId)
    {
        var before = Wait(new HttpTaskPreparationService(submitter.CaptureSession()).GetContextAsync(taskId));
        var client = TaskReviewRequestsClient.For(submitter);
        bool sent = false;
        DriveModal<ReviewRequestForm>(() => Field<Button>(page, "btnReviewRequestPreview").PerformClick(), form =>
        {
            if (Field<bool>(form, "busy") || !Field<Button>(form, "btnConfirm").Enabled) return;
            if (client.Pending?.Stage == "completed")
            {
                Check(form.Saved && client.Pending.Saved?.SubmissionId == submissionId
                    && client.Pending.Saved.RoundNo == "1", "manual request save identifies the original submitted round");
                Field<Button>(form, "btnConfirm").PerformClick();
                return;
            }
            if (sent) return;
            Check(Field<TextBox>(form, "txtResultPreview").Text == Description(1)
                && Field<TextBox>(form, "txtTaskSummary").Text.StartsWith(taskId + " · ", StringComparison.Ordinal),
                "separate request displays the selected saved result instead of a fresh submission draft");
            NativeType(Field<TextBox>(form, "txtRequestReason"), ReviewReason);
            sent = true;
            Field<Button>(form, "btnConfirm").PerformClick();
        });
        Check(sent && client.Pending is null, "confirmed manual request clears only its completed operation");
        var afterRequest = Wait(new HttpTaskPreparationService(submitter.CaptureSession()).GetContextAsync(taskId));
        Check(SameContext(afterRequest, before) && Submissions(submitter, taskId).GetArrayLength() == 1,
            "manual request leaves task status version assignment update time and submission count unchanged");
        var own = Wait(client.ListAsync(false, CancellationToken.None));
        ReviewRequestItem item = own.Single(row => row.TaskId == taskId);
        Check(item.State == "pending" && item.SubmissionId == submissionId && item.RoundNo == "1"
            && item.Reason == ReviewReason, "requester sees one pending request for the exact existing first round");

        bool acknowledged = false;
        var reviewerClient = TaskReviewRequestsClient.For(reviewer);
        using (var inbox = new TaskReviewRequestsForm(reviewerClient, true))
        {
            DriveModal<TaskReviewRequestsForm>(() => inbox.ShowDialog(page.FindForm()), form =>
            {
                var grid = Field<DataGridView>(form, "gridRequests");
                if (Field<bool>(form, "busy")) return;
                var row = grid.Rows.Cast<DataGridViewRow>().SingleOrDefault(row =>
                    row.Tag is ReviewRequestItem current && current.RequestId == item.RequestId);
                if (row is null) return;
                grid.ClearSelection(); grid.CurrentCell = row.Cells[0]; row.Selected = true;
                if (row.Tag is ReviewRequestItem { State: "acknowledged" } updated)
                {
                    Check(updated.AcknowledgedBy == reviewer.CurrentUser!.Id && updated.AcknowledgedAt is not null
                        && Field<TextBox>(form, "txtDetail").Text.Contains(ReviewReason),
                        "actual reviewer inbox shows its persisted acknowledgement and original reason");
                    form.Close(); return;
                }
                if (acknowledged || !Field<Button>(form, "btnAcknowledge").Enabled) return;
                Check(Field<ComboBox>(form, "cmbScope").SelectedIndex == 1, "reviewer opens the actual received-request scope");
                acknowledged = true;
                Field<Button>(form, "btnAcknowledge").PerformClick();
            });
        }
        var afterAcknowledgment = Wait(new HttpTaskPreparationService(submitter.CaptureSession()).GetContextAsync(taskId));
        Check(acknowledged && SameContext(afterAcknowledgment, before) && Submissions(submitter, taskId).GetArrayLength() == 1,
            "acknowledgment changes only the manual request and never approves returns or resubmits the result");
        Check(Wait(client.ListAsync(false, CancellationToken.None)).Single(row => row.RequestId == item.RequestId).State == "acknowledged",
            "the requester sees the actual reviewer acknowledgment through HTTP");
    }

    private static bool SameContext(TaskWriteContext current, TaskWriteContext original) =>
        current.TaskId == original.TaskId && current.AssigneeId == original.AssigneeId
        && current.RowVersion == original.RowVersion && current.AssignmentRevision == original.AssignmentRevision
        && current.Status == original.Status && current.UpdatedAt == original.UpdatedAt
        && current.ManagerUserIds.SequenceEqual(original.ManagerUserIds);

    private static void DriveModal<T>(Action open, Action<T> drive) where T : Form
    {
        Exception? failed = null;
        bool saw = false;
        var watch = Stopwatch.StartNew();
        using var timer = new System.Windows.Forms.Timer { Interval = 1 };
        timer.Tick += (_, _) =>
        {
            T? form = Application.OpenForms.OfType<T>().FirstOrDefault();
            try
            {
                if (watch.Elapsed > TimeSpan.FromSeconds(30)) throw new CheckFailure("modal did not complete its expected real operation");
                if (form is not null) { saw = true; drive(form); }
            }
            catch (Exception error)
            {
                failed = error;
                timer.Stop();
                form?.Dispose();
            }
        };
        timer.Start();
        open();
        timer.Stop();
        if (failed is not null) throw failed;
        Check(saw, "the intended real modal was opened");
    }

    private static void Select(TasksControl page, string taskId)
    {
        var grid = Field<DataGridView>(page, "gridTasks");
        DataGridViewRow row = grid.Rows.Cast<DataGridViewRow>().Single(item => ((TaskSummary)item.Tag!).TaskId == taskId);
        grid.ClearSelection();
        grid.CurrentCell = row.Cells[0];
        row.Selected = true;
        Until(() => Current(page)?.TaskId == taskId && Field<Label>(page, "lblTaskQueryStatus").Text == "상세 조회 완료");
    }

    private static TaskSummary? Current(TasksControl page) => Field<TaskDetail?>(page, "currentTaskDetail")?.Summary;
    private static T Field<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    private static void ExpectForbidden(Action action)
    {
        try { action(); throw new CheckFailure("forbidden query unexpectedly succeeded"); }
        catch (TaskQueryException error) { Check(error.Failure == TaskQueryFailure.Forbidden, "server rejects unauthorized task scope or detail"); }
    }
    private static void Check(bool valid, string label) { if (!valid) throw new CheckFailure(label); checks++; }
    private static T Wait<T>(Task<T> task) { Wait((Task)task); return task.GetAwaiter().GetResult(); }
    private static void Wait(Task task) { Until(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void Until(Func<bool> done)
    {
        var watch = Stopwatch.StartNew();
        while (!done())
        {
            if (watch.Elapsed > TimeSpan.FromSeconds(30)) throw new CheckFailure("UI operation did not complete");
            Application.DoEvents();
            Thread.Yield();
        }
        Application.DoEvents();
    }

    private sealed record RequestRecord(HttpMethod Method, string Path);
    private sealed class ObservedTransport : HttpMessageHandler
    {
        private readonly HttpMessageInvoker inner = new(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false, UseProxy = false });
        private readonly List<RequestRecord> requests = new();
        public RequestRecord[] Snapshot() { lock (requests) return requests.ToArray(); }
        public int Count(HttpMethod method, string path) => Snapshot().Count(item => item.Method == method && item.Path == path);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (requests) requests.Add(new(request.Method, request.RequestUri!.AbsolutePath));
            return inner.SendAsync(request, cancellationToken);
        }
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
}
