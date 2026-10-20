using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

internal static class Program
{
    private sealed record Account(string LoginId, string Password, int Id);
    private sealed record Settings(string Server, string Storage, Account Manager, Account Worker,
        Account Upper, Account OtherWorker, Account Outsider);
    private sealed class CheckFailure(string message) : Exception(message);
    private static int checks;
    private static string stage = "startup";
    private static Settings settings = null!;

    [STAThread]
    private static int Main(string[] args)
    {
        if (!args.SequenceEqual(new[] { "--http" }))
        { Console.Error.WriteLine("Use the explicit disposable MySQL runner; private settings are read from stdin."); return 2; }
        settings = JsonSerializer.Deserialize<Settings>(Console.In.ReadToEnd(), ApiClient.Json)!;
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Application.SetHighDpiMode(HighDpiMode.DpiUnawareGdiScaled);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Control.CheckForIllegalCrossThreadCalls = true;
        int result = 1;
        using var coordinator = new Form { ShowInTaskbar = false, Location = new(-20000, -20000),
            StartPosition = FormStartPosition.Manual, Size = new(1, 1) };
        coordinator.Shown += (_, _) => coordinator.BeginInvoke((Action)(() =>
        {
            try { Run(); Console.WriteLine($"PASS TaskManagement integration: {checks} checks."); result = 0; }
            catch (Exception error)
            {
                // Static authored labels only. Never echo API data, credentials or arbitrary exceptions.
                string label = error is CheckFailure ? error.Message : error.GetType().Name;
                Console.Error.WriteLine($"FAIL TaskManagement integration at {stage}: {label}");
            }
            finally { coordinator.Close(); }
        }));
        Application.Run(coordinator);
        return result;
    }

    private static void Run()
    {
        using var manager = Login(settings.Manager);
        using var worker = Login(settings.Worker);
        using var upper = Login(settings.Upper);
        using var other = Login(settings.OtherWorker);
        using var outsider = Login(settings.Outsider);
        stage = "task creation";
        string editable = Create(manager, "management-editable");
        string inProgress = Create(manager, "management-progress");
        string returned = Create(manager, "management-returned");
        string judged = Create(manager, "management-judged");
        string conflict = Create(manager, "management-conflict");

        stage = "current manager permissions";
        foreach (ApiClient denied in new[] { worker, outsider })
        {
            ExpectStatus(() => Update(denied, editable, "1", 1, "must-not-save"), HttpStatusCode.Forbidden);
            ExpectStatus(() => Reassign(denied, editable, "1", 1, settings.OtherWorker.Id), HttpStatusCode.Forbidden);
            ExpectStatus(() => Managers(denied, editable, "1", 1, settings.Upper.Id), HttpStatusCode.Forbidden);
        }

        stage = "three actual management dialogs";
        using (var page = new TasksControl(PreviewRole.Administrator, new HttpTaskQueryService(manager)))
        using (var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
            Location = new(-20000, -20000), ClientSize = new(1320, 780) })
        {
            page.Dock = DockStyle.Fill;
            host.Controls.Add(page);
            host.Show();
            if (Field<Label>(page, "lblTaskQueryStatus").Text == "업무 조회를 기다리고 있습니다.") Wait(page.RefreshAsync());
            stage = "managed list ready";
            Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 1;
            Until(() => Field<Label>(page, "lblTaskQueryStatus").Text.Contains("상세 조회 완료"));
            stage = "editable selected";
            Select(page, editable);
            stage = "edit dialog";
            ManagementDialog(page, "btnEditTaskPreview", "update");
            stage = "edited list refresh";
            Until(() => Current(page)?.Title == "management-edited");
            Check(Wait(Context(manager, editable)).RowVersion == "2", "actual edit increments row version once");
            stage = "reassign dialog";
            ManagementDialog(page, "btnReassignTask", "reassign");
            stage = "reassigned list refresh";
            Until(() => Current(page)?.TaskId == editable && Field<Label>(page, "lblTaskQueryStatus").Text.Contains("상세 조회 완료"));
            var reassigned = Wait(Context(manager, editable));
            Check(reassigned.RowVersion == "3" && reassigned.AssignmentRevision == 2,
                "actual reassignment increments both counters and refreshes detail");
            stage = "managers dialog";
            ManagementDialog(page, "btnChangeTaskManagers", "managers");
            stage = "manager handover list refresh";
            Until(() => Field<Button>(page, "btnRetryQuery").Enabled);
            Check(Field<DataGridView>(page, "gridTasks").Rows.Cast<DataGridViewRow>()
                .All(row => ((TaskSummary)row.Tag!).TaskId != editable),
                "self-removing manager loses the task from the actual managed list");
        }
        Check(Wait(Context(upper, editable)).ManagerUserIds.SequenceEqual(new[] { settings.Upper.Id }),
            "creator can hand over to one active different manager");
        ExpectQueryForbidden(manager, editable);
        ExpectQueryForbidden(worker, editable);
        Check(Wait(Context(other, editable)).AssigneeId == settings.OtherWorker.Id,
            "new assignee receives the task while the former assignee loses access");

        stage = "post-handover authorizer and empty manager guard";
        var after = Wait(Context(upper, editable));
        ExpectStatus(() => Update(manager, editable, after.RowVersion, 2, "denied-creator"), HttpStatusCode.Forbidden);
        ExpectStatus(() => Managers(upper, editable, after.RowVersion, 2), HttpStatusCode.BadRequest);
        ExpectStatus(() => Managers(upper, editable, after.RowVersion, 2, settings.Worker.Id), HttpStatusCode.Conflict);

        stage = "current versions no op and stale versions";
        string updateKey = Key(manager.CaptureSession(), "task.updated", conflict);
        Check(Update(manager, conflict, "1", 1, "management-conflict-edited", updateKey).GetProperty("row_version").GetString() == "2",
            "fresh update succeeds");
        Check(Update(manager, conflict, "1", 1, "management-conflict-edited", updateKey).GetProperty("row_version").GetString() == "2",
            "identical replay returns the existing success without another write");
        JsonElement lookup = Wait(manager.CaptureSession().PostIdempotentAsync<JsonElement>("write-requests/result",
            new { action = "task.updated", target = conflict }, updateKey));
        Check(lookup.GetProperty("state").GetString() == "completed"
            && lookup.GetProperty("result").GetProperty("row_version").GetString() == "2",
            "actual common result lookup confirms the same committed result");
        ExpectStatus(() => Update(manager, conflict, "1", 1, "changed-body", updateKey), HttpStatusCode.Conflict);
        ExpectStatus(() => Update(manager, conflict, "1", 1, "stale-title"), HttpStatusCode.Conflict);
        ExpectStatus(() => Reassign(manager, conflict, "2", 2, settings.OtherWorker.Id), HttpStatusCode.Conflict);
        ExpectStatus(() => Update(manager, conflict, "2", 1, "management-conflict-edited"), HttpStatusCode.Conflict);
        ExpectStatus(() => Reassign(manager, conflict, "2", 1, settings.Worker.Id), HttpStatusCode.Conflict);
        ExpectStatus(() => Managers(manager, conflict, "2", 1, settings.Manager.Id), HttpStatusCode.Conflict);

        stage = "in progress constraints";
        Start(worker, inProgress);
        ExpectStatus(() => Update(manager, inProgress, "2", 1, "forbidden-progress-edit"), HttpStatusCode.Conflict);
        ExpectStatus(() => Reassign(manager, inProgress, "2", 1, settings.OtherWorker.Id), HttpStatusCode.Conflict);
        Check(Managers(manager, inProgress, "2", 1, settings.Upper.Id).GetProperty("row_version").GetString() == "3",
            "in progress manager handover is allowed and retains assignment revision");

        stage = "actual returned result preservation";
        Start(worker, returned);
        byte[] bytes = Enumerable.Range(0, 80).Select(index => (byte)index).ToArray();
        string attachment = Upload(worker, returned, bytes);
        JsonElement submitted = Submit(worker, returned, attachment);
        Decide(manager, submitted.GetProperty("submission_id").GetString()!, "returned");
        ExpectStatus(() => Update(manager, returned, "4", 1, "forbidden-returned-edit"), HttpStatusCode.Conflict);
        ExpectStatus(() => Reassign(manager, returned, "4", 1, settings.OtherWorker.Id), HttpStatusCode.Conflict);
        Check(Managers(manager, returned, "4", 1, settings.Upper.Id).GetProperty("task_status").GetString() == "returned",
            "returned manager handover retains returned status");
        JsonElement previous = Wait(worker.CaptureSession().GetAsync<JsonElement>($"results/tasks/{returned}/submissions"))
            .GetProperty("items").EnumerateArray().Single();
        Check(previous.GetProperty("reviewer_id").GetInt32() == settings.Manager.Id
            && previous.GetProperty("description").GetString() == "Actual management preservation result"
            && previous.GetProperty("attachments").GetArrayLength() == 1,
            "manager handover preserves original result reviewer content and attachment");
        JsonElement chunk = Wait(worker.CaptureSession().GetAsync<JsonElement>($"results/attachments/{attachment}/chunks?offset=0"));
        Check(Convert.FromBase64String(chunk.GetProperty("data").GetString()!).SequenceEqual(bytes),
            "first actual attachment remains downloadable after manager handover");
        Check(Wait(worker.CaptureSession().GetAsync<JsonElement>($"results/tasks/{returned}/history"))
            .GetProperty("items").GetArrayLength() == 2, "existing result history is retained without a synthetic extra event");

        stage = "submitted and approved restrictions";
        Start(worker, judged);
        JsonElement pending = Submit(worker, judged);
        foreach (string status in new[] { "submitted", "approved" })
        {
            string version = status == "submitted" ? "3" : "4";
            ExpectStatus(() => Update(manager, judged, version, 1, "forbidden-judged-edit"), HttpStatusCode.Conflict);
            ExpectStatus(() => Reassign(manager, judged, version, 1, settings.OtherWorker.Id), HttpStatusCode.Conflict);
            ExpectStatus(() => Managers(manager, judged, version, 1, settings.Upper.Id), HttpStatusCode.Conflict);
            if (status == "submitted") Decide(manager, pending.GetProperty("submission_id").GetString()!, "approved");
        }
    }

    private static ApiClient Login(Account account)
    {
        var api = new ApiClient(settings.Server, new HttpClientHandler { UseProxy = false, UseCookies = false });
        Wait(api.LoginAsync(account.LoginId, account.Password));
        Check(api.CurrentUser?.Id == account.Id, "common authentication returns the intended fixture user");
        return api;
    }
    private static DateOnly Today => DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).Date);
    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    private static string Create(ApiClient api, string title)
    {
        var session = api.CaptureSession();
        var result = Wait(session.PostIdempotentAsync<JsonElement>("tasks", new { title,
            description = "Explicit owned management fixture", start_date = Iso(Today.AddDays(-1)), due_date = Iso(Today.AddDays(3)),
            assignee_id = settings.Worker.Id, manager_user_ids = new[] { settings.Manager.Id } }, Key(session, "task.created", "new")));
        Check(result.GetProperty("row_version").GetString() == "1", "creation starts at version one");
        return result.GetProperty("task_id").GetString()!;
    }
    private static Task<TaskWriteContext> Context(ApiClient api, string id) => new HttpTaskPreparationService(api.CaptureSession()).GetContextAsync(id);
    private static string Key(ApiClient.Session session, string action, string target) =>
        Wait(session.PostAsync<JsonElement>("write-requests/keys", new { action, target })).GetProperty("request_key").GetString()!;
    private static JsonElement Update(ApiClient api, string id, string version, int revision, string title, string? key = null)
    {
        var session = api.CaptureSession();
        return Wait(session.PatchIdempotentAsync<JsonElement>($"tasks/{id}", new { expected_row_version = version,
            expected_assignment_revision = revision, title, description = "Explicit owned management fixture",
            start_date = Iso(Today.AddDays(-1)), due_date = Iso(Today.AddDays(3)) }, key ?? Key(session, "task.updated", id)));
    }
    private static JsonElement Reassign(ApiClient api, string id, string version, int revision, int user)
    {
        var session = api.CaptureSession();
        return Wait(session.PostIdempotentAsync<JsonElement>($"tasks/{id}/reassign", new { expected_row_version = version,
            expected_assignment_revision = revision, assignee_id = user }, Key(session, "task.reassigned", id)));
    }
    private static JsonElement Managers(ApiClient api, string id, string version, int revision, params int[] users)
    {
        var session = api.CaptureSession();
        return Wait(session.PatchIdempotentAsync<JsonElement>($"tasks/{id}/managers", new { expected_row_version = version,
            expected_assignment_revision = revision, manager_user_ids = users }, Key(session, "task.managers_changed", id)));
    }
    private static void Start(ApiClient api, string id)
    {
        var session = api.CaptureSession();
        var context = Wait(Context(api, id));
        Check(Wait(session.PostIdempotentAsync<JsonElement>($"tasks/{id}/accept", new { expected_row_version = context.RowVersion,
            expected_assignment_revision = context.AssignmentRevision }, Key(session, "task.accepted", id)))
            .GetProperty("task_status").GetString() == "in_progress", "acceptance uses the real current assignee API");
    }
    private static string Upload(ApiClient api, string id, byte[] bytes)
    {
        var session = api.CaptureSession();
        string hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var uploaded = Wait(session.PostAsync<JsonElement>($"results/tasks/{id}/uploads/start", new { name = "preserved.bin", size = bytes.Length, sha256 = hash }));
        string fileId = uploaded.GetProperty("attachment_id").GetString()!;
        Wait(session.PostAsync<JsonElement>($"results/uploads/{fileId}/chunks", new { offset = 0, data = Convert.ToBase64String(bytes) }));
        Wait(session.PostAsync<JsonElement>($"results/uploads/{fileId}/complete", new { }));
        return fileId;
    }
    private static JsonElement Submit(ApiClient api, string id, params string[] files)
    {
        var session = api.CaptureSession();
        var context = Wait(Context(api, id));
        return Wait(session.PostIdempotentAsync<JsonElement>($"results/tasks/{id}/submissions", new { expected_row_version = context.RowVersion,
            expected_assignment_revision = context.AssignmentRevision, description = "Actual management preservation result", attachment_ids = files },
            Key(session, "submission.submitted", id)));
    }
    private static void Decide(ApiClient api, string id, string outcome)
    {
        var session = api.CaptureSession();
        var context = Wait(session.GetAsync<JsonElement>($"results/submissions/{id}/context"));
        Wait(session.PostIdempotentAsync<JsonElement>($"results/submissions/{id}/decisions", new {
            expected_row_version = context.GetProperty("row_version").GetString(),
            expected_assignment_revision = context.GetProperty("assignment_revision").GetInt32(), outcome,
            reason = outcome == "returned" ? "Actual correction required" : "" }, Key(session, "submission." + outcome, id)));
    }

    private static void ManagementDialog(TasksControl page, string buttonName, string operation)
    {
        bool changed = false, picked = false;
        int confirms = 0;
        DriveModal(() => Field<Button>(page, buttonName).PerformClick(), form =>
        {
            if (form is TaskPreparationForm picker)
            {
                if (!picked && Field<DataGridView>(picker, "gridCandidates").Enabled)
                {
                    if (operation == "managers" && Field<ComboBox>(picker, "cmbKind").SelectedIndex != 1)
                    { Field<ComboBox>(picker, "cmbKind").SelectedIndex = 1; return; }
                    int target = operation == "reassign" ? settings.OtherWorker.Id : settings.Upper.Id;
                    var grid = Field<DataGridView>(picker, "gridCandidates");
                    var row = grid.Rows.Cast<DataGridViewRow>().SingleOrDefault(row => ((TaskUserCandidate)row.Tag!).Id == target);
                    if (row is null) return;
                    grid.ClearSelection(); grid.CurrentCell = row.Cells[0]; row.Selected = true;
                    Field<Button>(picker, "btnChoose").PerformClick();
                    if (operation == "managers")
                    {
                        var selected = Field<ListBox>(picker, "listSelected");
                        selected.SelectedIndex = selected.Items.Cast<object>().Select((item, index) => (item, index))
                            .Single(item => item.item.ToString()!.Contains("#" + settings.Manager.Id)).index;
                        Field<Button>(picker, "btnRemove").PerformClick();
                    }
                    picked = true;
                    Field<Button>(picker, "btnConfirm").PerformClick();
                }
                return;
            }
            if (form.GetType().Name != "TaskManagementForm") return;
            var confirm = Field<Button>(form, "btnConfirm");
            if (!changed && confirm.Enabled)
            {
                changed = true;
                if (operation == "update") Field<TextBox>(form, "txtTitle").Text = "management-edited";
                else Field<Button>(form, "btnChooseCandidates").PerformClick();
                return;
            }
            if (changed && (operation == "update" || picked) && confirms < 2 && confirm.Enabled)
            { confirms++; confirm.PerformClick(); }
            if (Field<Button>(form, "btnAcknowledge").Enabled)
                Field<Button>(form, "btnAcknowledge").PerformClick();
        });
        Check(changed && confirms == 2 && (operation == "update" || picked),
            "actual management dialog uses selected candidates and separate input check before write");
    }
    private static void DriveModal(Action open, Action<Form> drive)
    {
        Exception? failure = null;
        bool saw = false;
        var watch = Stopwatch.StartNew();
        using var timer = new System.Windows.Forms.Timer { Interval = 1 };
        timer.Tick += (_, _) =>
        {
            Form? form = Application.OpenForms.Cast<Form>().LastOrDefault(item => item.Modal);
            try {
                if (watch.Elapsed > TimeSpan.FromSeconds(40)) throw new CheckFailure("management modal did not complete");
                if (form is not null) { saw = true; drive(form); }
            }
            catch (Exception error) { failure = error; timer.Stop(); foreach (var item in Application.OpenForms.Cast<Form>().Where(item => item.Modal).Reverse().ToArray()) item.Dispose(); }
        };
        timer.Start(); open(); timer.Stop();
        if (failure is not null) throw failure;
        Check(saw, "the intended real management modal opens");
    }
    private static void Select(TasksControl page, string id)
    {
        var grid = Field<DataGridView>(page, "gridTasks");
        var row = grid.Rows.Cast<DataGridViewRow>().Single(row => ((TaskSummary)row.Tag!).TaskId == id);
        grid.ClearSelection(); grid.CurrentCell = row.Cells[0]; row.Selected = true;
        Until(() => Current(page)?.TaskId == id && Field<Label>(page, "lblTaskQueryStatus").Text.Contains("상세 조회 완료"));
    }
    private static TaskSummary? Current(TasksControl page) => Field<TaskDetail?>(page, "currentTaskDetail")?.Summary;
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance)!;
    private static void ExpectQueryForbidden(ApiClient api, string id)
    {
        try { Wait(new HttpTaskQueryService(api).GetTaskAsync(id)); throw new CheckFailure("former relation unexpectedly retains access"); }
        catch (TaskQueryException error) { Check(error.Failure == TaskQueryFailure.Forbidden, "server revokes former relation access"); }
    }
    private static void ExpectStatus(Action action, HttpStatusCode status)
    {
        try { action(); throw new CheckFailure("disallowed management request unexpectedly succeeds"); }
        catch (ApiException error) { Check(error.StatusCode == (int)status, "server rejects the management request with its expected status"); }
    }
    private static void Check(bool valid, string label) { if (!valid) throw new CheckFailure(label); checks++; }
    private static T Wait<T>(Task<T> task) { Wait((Task)task); return task.GetAwaiter().GetResult(); }
    private static void Wait(Task task) { Until(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static void Until(Func<bool> ready)
    {
        var watch = Stopwatch.StartNew();
        while (!ready()) { if (watch.Elapsed > TimeSpan.FromSeconds(40)) throw new CheckFailure("UI or HTTP operation did not complete"); Application.DoEvents(); Thread.Yield(); }
        Application.DoEvents();
    }
}
