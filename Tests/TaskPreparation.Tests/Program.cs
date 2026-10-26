using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Management;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Features.Tasks.Queries.TestDoubles;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private static int checks;
    private static string? capture;
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Control.CheckForIllegalCrossThreadCalls = true;
        int exit = 1;
        using var coordinator = new Form { ShowInTaskbar = false, Location = new(-20000, -20000), StartPosition = FormStartPosition.Manual, Size = new(1, 1) };
        coordinator.Shown += (_, _) => coordinator.BeginInvoke((Action)(() =>
        {
            try
            {
                if (args.SequenceEqual(new[] { "--live" })) Live();
                else
                {
                    capture = args.FirstOrDefault();
                    Wire(); Malformed(); Receipts(); SessionChanges(); CandidateScreen(); ReadOnlyConnectionInfo();
                    NewTaskManagers(); NewTaskInvalidation(); NonAdminNewTask(); ExistingTaskManagerRemoval();
                    LateResponses(); ParentDialog(); PurposeSpecificManagementDialogs();
                }
                Console.WriteLine($"PASS: {checks} preparation checks. " + (args.SequenceEqual(new[] { "--live" }) ? "Actual HTTP/MySQL fixture; no product writes." : "Controlled HTTP and actual WinForms; no server/DB."));
                exit = 0;
            }
            catch (Exception error) { Console.Error.WriteLine($"FAIL after {checks}: {error.GetType().Name}: {error.Message}"); }
            finally { coordinator.Close(); }
        }));
        Application.Run(coordinator);
        return exit;
    }
    private static void Check(bool condition, string why) { if (!condition) throw new InvalidOperationException(why); checks++; }
    private static T Wait<T>(Task<T> task)
    {
        var stop = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < stop) { Application.DoEvents(); Thread.Sleep(1); }
        if (!task.IsCompleted) throw new TimeoutException("Preparation check timed out");
        return task.GetAwaiter().GetResult();
    }
    private static void PumpUntil(Func<bool> ready)
    {
        var stop = DateTime.UtcNow.AddSeconds(15);
        while (!ready() && DateTime.UtcNow < stop) { Application.DoEvents(); Thread.Sleep(1); }
        Check(ready(), "UI reached the expected state");
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static ApiClient Api(Handler handler)
    {
        var api = new ApiClient("http://localhost:5000/", handler);
        Login(api, handler);
        return api;
    }
    private static void Login(ApiClient api, Handler handler)
    {
        handler.Success(new { access_token = "synthetic-token", token_type = "Bearer", expires_at = "2030-01-01T00:00:00Z", user = new { id = 2, login_id = "admin", display_name = "관리자", employee_no = "A2", department = "팀", role = "admin", is_active = true } });
        Wait(api.LoginAsync("synthetic", "not-a-real-password"));
    }
    private static object Context(string id = "9007199254740993", string version = "9007199254740994") => new
    { context = new { task_id = id, assignee_id = 7, manager_user_ids = new[] { 2 }, row_version = version, assignment_revision = 3, task_status = "not_started", updated_at = "2026-10-17T00:00:00Z" } };
    private static void Failure(Action action, TaskPreparationFailure expected)
    {
        try { action(); throw new InvalidOperationException("Expected preparation failure"); }
        catch (TaskPreparationException error) { Check(error.Failure == expected, "Expected failure category"); }
    }
    private static void Wire()
    {
        using var h = new Handler(); using var api = Api(h);
        var service = new HttpTaskPreparationService(api.CaptureSession());
        h.Success(new { items = new[] { new { id = 7, display_name = "동명이인", selection_label = "동명이인 · 개발 · #7" } }, page = 2, limit = 1, total = 2 });
        var page = Wait(service.GetCandidatesAsync(new(TaskCandidateKind.Assignee, "9007199254740993", "가&%", 2, 1)));
        Check(page.Items.Single().Id == 7 && page.Total == 2, "Candidate IDs and page metadata survive mapping");
        Check(h.Last!.RequestUri!.AbsoluteUri.Contains("q=%EA%B0%80%26%25") && h.Last.RequestUri.Query.Contains("task_id=9007199254740993"), "Search is escaped and BIGINT is preserved");
        Check(h.Bearer == "synthetic-token", "Uses the captured shared bearer");
        h.Success(Context());
        var context = Wait(service.GetContextAsync("9007199254740993"));
        Check(context.RowVersion == "9007199254740994" && context.AssignmentRevision == 3 && context.UpdatedAt.Offset == TimeSpan.Zero, "Context IDs/versions remain exact strings and UTC");
        Check(context.ManagerUserIds.SequenceEqual(new[] { 2 }) && context.AssigneeId == 7, "Current relationships are separate fields");
        foreach (string invalid in new[] { "0", "01", "-1", "1 ", "9223372036854775808", "opaque:1" })
        {
            int count = h.Count;
            try { Wait(service.GetContextAsync(invalid)); throw new InvalidOperationException("Invalid ID accepted"); }
            catch (ArgumentException) { Check(h.Count == count, "Malformed context IDs are not sent"); }
        }
        h.Success(new { items = Array.Empty<object>(), page = 1, limit = 20, total = 0 });
        Check(Wait(service.GetCandidatesAsync(new(TaskCandidateKind.Manager))).Items.Count == 0, "Empty manager candidates are valid");
        Check(h.Last!.RequestUri!.AbsolutePath == "/api/v1/tasks/manager-candidates", "Manager candidates use their own endpoint");
    }
    private static void Malformed()
    {
        using var h = new Handler(); using var api = Api(h);
        var service = new HttpTaskPreparationService(api.CaptureSession());
        h.Success(Context("1", "01")); Failure(() => Wait(service.GetContextAsync("1")), TaskPreparationFailure.Unavailable);
        h.Success(Context("2")); Failure(() => Wait(service.GetContextAsync("1")), TaskPreparationFailure.Unavailable);
        h.Success(new { items = new[] { new { id = 1, display_name = "가", selection_label = "가#1" }, new { id = 1, display_name = "나", selection_label = "나#1" } }, page = 1, limit = 20, total = 2 });
        Failure(() => Wait(service.GetCandidatesAsync(new(TaskCandidateKind.Assignee))), TaskPreparationFailure.Unavailable);
        h.Success(new { items = Array.Empty<object>(), page = 2, limit = 20, total = 0 });
        Failure(() => Wait(service.GetCandidatesAsync(new(TaskCandidateKind.Assignee))), TaskPreparationFailure.Unavailable);
        foreach (var (status, code, expected) in new[] {
            (403,"FORBIDDEN",TaskPreparationFailure.Forbidden), (404,"TASK_NOT_FOUND",TaskPreparationFailure.NotFound),
            (404,"HTTP_404",TaskPreparationFailure.NotConfigured), (503,"WRITE_SCHEMA_REQUIRED",TaskPreparationFailure.NotConfigured),
            (409,"TASK_ASSIGNMENT_CHANGED",TaskPreparationFailure.AssignmentChanged), (409,"TASK_VERSION_CONFLICT",TaskPreparationFailure.VersionConflict) })
        { h.Error(status, code); Failure(() => Wait(service.GetContextAsync("1")), expected); }
    }
    private static void Receipts()
    {
        using var h = new Handler(); using var api = Api(h);
        var service = new HttpTaskPreparationService(api.CaptureSession());
        string value = new('k', 40);
        h.Success(new { request_key = value, expires_at = "2030-01-01T00:00:00Z" });
        var key = Wait(service.GetRequestKeyAsync(TaskWriteAction.Accept, "1"));
        Check(key.Value == value && !key.ToString().Contains(value), "Key stays usable in memory and is not exposed by ToString");
        Check(h.Body.Contains("task.accepted") && h.Last!.RequestUri!.AbsolutePath == "/api/v1/write-requests/keys", "Key issuance is separate from a mutation route");
        h.Success(new { state = "not_recorded" });
        Check(!Wait(service.GetResultAsync(key)).Recorded && h.Key == value, "Not-recorded lookup preserves the same key without retrying a write");
        h.Success(new { state = "completed", http_status = 200, result = new { task_id = "1", row_version = "2" }, expires_at = "2030-01-01T00:00:00Z" });
        var result = Wait(service.GetResultAsync(key));
        Check(result.Recorded && result.Result!.Value.GetProperty("row_version").GetString() == "2", "A stored result is returned without executing an action");
        h.Error(410, "IDEMPOTENCY_KEY_EXPIRED"); Failure(() => Wait(service.GetResultAsync(key)), TaskPreparationFailure.KeyExpired);
        h.Success(new { state = "processing" }); Failure(() => Wait(service.GetResultAsync(key)), TaskPreparationFailure.Unavailable);
    }
    private static void SessionChanges()
    {
        using var h = new Handler(); using var api = Api(h);
        var old = new HttpTaskPreparationService(api.CaptureSession());
        api.ClearIdentity(); Login(api, h);
        int count = h.Count;
        try { Wait(old.GetCandidatesAsync(new(TaskCandidateKind.Assignee))); throw new InvalidOperationException("Old screen sent as new login"); }
        catch (OperationCanceledException) { Check(h.Count == count && !old.IsCurrent, "Old preparation cannot send with a new session, even the same user"); }
        var current = new HttpTaskPreparationService(api.CaptureSession());
        h.Error(401, "AUTH_REQUIRED"); Failure(() => Wait(current.GetContextAsync("1")), TaskPreparationFailure.AuthenticationExpired);
        Check(api.CurrentUser is null && !current.IsCurrent, "401 clears shared identity and preparation scope");
    }
    private static void Show(Form form) { form.StartPosition = FormStartPosition.Manual; form.Location = new(-15000, -15000); form.Show(); Application.DoEvents(); }
    private static void CandidateScreen()
    {
        using var stub = new Stub(); using var form = new TaskPreparationForm(stub, "1"); Show(form);
        var grid = Field<DataGridView>(form, "gridCandidates");
        PumpUntil(() => grid.Rows.Count == 20 && Field<Button>(form, "btnSearch").Enabled);
        grid.CurrentCell = grid.Rows[0].Cells[0]; grid.Rows[0].Selected = true;
        Field<Button>(form, "btnChoose").PerformClick();
        Field<ComboBox>(form, "cmbKind").SelectedIndex = 1;
        PumpUntil(() => grid.Rows.Count == 20);
        grid.CurrentCell = grid.Rows[2].Cells[0]; grid.Rows[2].Selected = true;
        Field<Button>(form, "btnChoose").PerformClick();
        Field<Button>(form, "btnNext").PerformClick();
        PumpUntil(() => grid.Rows.Count == 1);
        Check(Field<ListBox>(form, "listSelected").Items.Count == 3, "Assignee and multiple manager selections survive paging");
        Field<TextBox>(form, "txtSearch").Text = "empty";
        Field<Button>(form, "btnSearch").PerformClick();
        PumpUntil(() => grid.Rows.Count == 0 && Field<Button>(form, "btnConfirm").Enabled);
        Capture(form, "Selection-Empty-Filter");
        form.Size = form.MinimumSize; Application.DoEvents();
        Capture(form, "Selection-Minimum");
        Check(Field<Button>(form, "btnClose").Bottom <= Field<FlowLayoutPanel>(form, "actionPanel").Height,
            "The close action remains inside its row at minimum size");
        Field<ListBox>(form, "listSelected").SelectedIndex = 2;
        Check(Field<Button>(form, "btnRemove").Enabled, "Selected manager can be removed explicitly");
        Field<Button>(form, "btnRemove").PerformClick();
        Field<Button>(form, "btnConfirm").PerformClick();
        Check(form.Selection?.Assignee?.Id == 1 && form.Selection.ManagerUserIds.SequenceEqual(new[] { 2 }), "Local selection preserves exact IDs without saving");
        Check(stub.KeyCalls == 0, "Candidate screen never issues keys or product mutations");
    }
    private static int SelectedItemIndex(ListBox list, bool manager, int id)
    {
        for (int index = 0; index < list.Items.Count; index++)
        {
            object item = list.Items[index];
            Type type = item.GetType();
            bool itemManager = (bool)type.GetProperty("Manager")!.GetValue(item)!;
            int itemId = (int)type.GetProperty("Id")!.GetValue(item)!;
            if (itemManager == manager && itemId == id) return index;
        }
        return -1;
    }
    private static void ReadOnlyConnectionInfo()
    {
        using var stub = new Stub();
        using var form = new TaskPreparationForm(stub, "1", readOnly: true);
        Show(form);
        var list = Field<ListBox>(form, "listSelected");
        PumpUntil(() => list.Items.Count == 2 && Field<Button>(form, "btnRefreshContext").Enabled);
        Check(stub.CanSelectCandidates && stub.ContextCalls == 1 && stub.CandidateCalls == 0,
            "Read-only connection info loads context without candidate requests even for an authorized manager");
        Check(SelectedItemIndex(list, false, 7) >= 0 && SelectedItemIndex(list, true, stub.UserId) >= 0,
            "Read-only connection info displays the current assignee and managers");
        list.SelectedIndex = SelectedItemIndex(list, true, stub.UserId);
        foreach (string name in new[] { "btnSearch", "btnPrevious", "btnNext", "btnChoose", "btnRemove", "btnConfirm" })
        {
            var button = Field<Button>(form, name);
            Check(!button.Enabled, "Read-only connection info disables " + name);
            button.PerformClick();
        }
        Field<ComboBox>(form, "cmbKind").SelectedIndex = 1;
        Wait(form.LoadCandidatesAsync(1).ContinueWith(_ => true));
        Check(stub.CandidateCalls == 0 && list.Items.Count == 2 && form.Selection is null,
            "Disabled selection actions and direct candidate loading cannot change read-only connection info");
        Field<Button>(form, "btnRefreshContext").PerformClick();
        PumpUntil(() => stub.ContextCalls == 2 && Field<Button>(form, "btnRefreshContext").Enabled);
        Check(stub.CandidateCalls == 0 && form.Selection is null && list.Items.Count == 2,
            "Read-only refresh reloads only context and never returns a candidate selection");
        Check(Field<Label>(form, "lblStatus").Text.Contains("읽기 전용")
            && Field<Label>(form, "lblStatus").Text.Contains("담당자 변경")
            && Field<Label>(form, "lblStatus").Text.Contains("관리자 변경"),
            "Read-only connection info directs edits to the actual task-management actions");
        NoScreenWrites(stub);
        Capture(form, "Read-Only-Connection-Info");
        form.Close();
    }
    private static void ChooseCandidate(TaskPreparationForm form, int id)
    {
        var grid = Field<DataGridView>(form, "gridCandidates");
        for (int index = 0; index < grid.Rows.Count; index++)
        {
            if ((int)grid.Rows[index].Cells[0].Value! != id) continue;
            grid.ClearSelection();
            grid.CurrentCell = grid.Rows[index].Cells[0];
            grid.Rows[index].Selected = true;
            Field<Button>(form, "btnChoose").PerformClick();
            return;
        }
        throw new InvalidOperationException("Expected candidate is missing: " + id);
    }
    private static void NoScreenWrites(Stub stub)
    {
        Check(stub.KeyCalls == 0 && stub.ResultCalls == 0,
            "Selection preparation never issues a request key or looks up a write result");
    }
    private static void NewTaskManagers()
    {
        using var stub = new Stub();
        using var form = new TaskPreparationForm(stub, null);
        Show(form);
        var grid = Field<DataGridView>(form, "gridCandidates");
        var list = Field<ListBox>(form, "listSelected");
        var kind = Field<ComboBox>(form, "cmbKind");
        var remove = Field<Button>(form, "btnRemove");
        PumpUntil(() => grid.Rows.Count == 20 && Field<Button>(form, "btnConfirm").Enabled);
        int creatorIndex = SelectedItemIndex(list, true, stub.UserId);
        Check(list.Items.Count == 1 && creatorIndex >= 0,
            "A new admin task includes its creator as the first required manager");
        string creatorLabel = list.Items[creatorIndex].ToString()!;
        Check(creatorLabel.Contains("등록자") && creatorLabel.Contains("필수"),
            "The required creator manager is explained in the selection list");
        list.SelectedIndex = creatorIndex;
        Check(!remove.Enabled, "The creator manager cannot be removed during initial registration");
        remove.PerformClick();
        Check(SelectedItemIndex(list, true, stub.UserId) >= 0,
            "Clicking the disabled remove action preserves the creator manager");

        ChooseCandidate(form, 1);
        kind.SelectedIndex = 1;
        PumpUntil(() => grid.Rows.Count == 20 && Field<Button>(form, "btnSearch").Enabled);
        ChooseCandidate(form, 3);
        list.SelectedIndex = SelectedItemIndex(list, true, 3);
        Check(remove.Enabled, "An additional new-task manager can be removed");
        remove.PerformClick();
        Check(SelectedItemIndex(list, true, 3) == -1 && SelectedItemIndex(list, true, stub.UserId) >= 0,
            "Removing an optional manager preserves the required creator");
        ChooseCandidate(form, 3);
        Field<Button>(form, "btnNext").PerformClick();
        PumpUntil(() => grid.Rows.Count == 1);
        Check(SelectedItemIndex(list, true, stub.UserId) >= 0 && SelectedItemIndex(list, true, 3) >= 0,
            "Both manager selections survive candidate paging");
        Field<TextBox>(form, "txtSearch").Text = "empty";
        Field<Button>(form, "btnSearch").PerformClick();
        PumpUntil(() => grid.Rows.Count == 0 && Field<Button>(form, "btnConfirm").Enabled);
        kind.SelectedIndex = 0;
        PumpUntil(() => grid.Rows.Count == 0 && Field<Button>(form, "btnConfirm").Enabled);
        Check(SelectedItemIndex(list, false, 1) >= 0 && SelectedItemIndex(list, true, stub.UserId) >= 0
            && SelectedItemIndex(list, true, 3) >= 0,
            "An empty search and candidate-kind change preserve the local selections");
        Field<Button>(form, "btnRefreshContext").PerformClick();
        PumpUntil(() => Field<Button>(form, "btnConfirm").Enabled);
        Check(list.Items.Count == 1 && SelectedItemIndex(list, true, stub.UserId) >= 0,
            "Refreshing a new-task context restores its required creator after clearing old choices");
        Check(stub.ContextCalls == 0, "New-task preparation never fetches an existing-task write context");
        Field<TextBox>(form, "txtSearch").Clear();
        Field<Button>(form, "btnSearch").PerformClick();
        PumpUntil(() => grid.Rows.Count == 20);
        ChooseCandidate(form, 1);
        Capture(form, "New-Task-Required-Creator");
        Field<Button>(form, "btnConfirm").PerformClick();
        Check(form.Selection is not null && form.Selection.Context is null && form.Selection.Assignee?.Id == 1
            && form.Selection.ManagerUserIds.Count == 1 && form.Selection.ManagerUserIds[0] == stub.UserId,
            "Confirming new-task preparation returns at least its creator manager without creating a task");
        NoScreenWrites(stub);
    }
    private static void NewTaskInvalidation()
    {
        foreach (TaskPreparationFailure failure in new[] {
            TaskPreparationFailure.Forbidden, TaskPreparationFailure.AuthenticationExpired })
        {
            using var stub = new Stub();
            using var form = new TaskPreparationForm(stub, null);
            Show(form);
            var list = Field<ListBox>(form, "listSelected");
            PumpUntil(() => Field<Button>(form, "btnConfirm").Enabled);
            Check(SelectedItemIndex(list, true, stub.UserId) >= 0, "Creator is present before access is lost");
            stub.Next = _ => Task.FromException<TaskCandidatePage>(new TaskPreparationException(failure));
            Wait(form.LoadCandidatesAsync(1).ContinueWith(_ => true));
            Check(list.Items.Count == 0 && form.Selection is null && !Field<Button>(form, "btnConfirm").Enabled,
                "An authentication or permission error clears the required creator along with all preparation");
            NoScreenWrites(stub);
            form.Close();
        }
        using var revokedStub = new Stub();
        using var revokedForm = new TaskPreparationForm(revokedStub, null);
        Show(revokedForm);
        PumpUntil(() => Field<Button>(revokedForm, "btnConfirm").Enabled);
        revokedStub.Revoke();
        PumpUntil(() => Field<ListBox>(revokedForm, "listSelected").Items.Count == 0);
        Check(revokedForm.Selection is null && !Field<Button>(revokedForm, "btnConfirm").Enabled,
            "Logout or session replacement clears the new-task creator and disables confirmation");
        NoScreenWrites(revokedStub);
        revokedForm.Close();
    }
    private static void NonAdminNewTask()
    {
        using var stub = new Stub(false);
        using var form = new TaskPreparationForm(stub, null);
        Show(form);
        PumpUntil(() => Field<Button>(form, "btnRefreshContext").Enabled);
        Check(Field<ListBox>(form, "listSelected").Items.Count == 0 && form.Selection is null
            && !Field<Button>(form, "btnConfirm").Enabled && stub.CandidateCalls == 0,
            "A non-admin new-task screen does not assign itself as manager or load candidates");
        NoScreenWrites(stub);
        form.Close();
    }
    private static void ExistingTaskManagerRemoval()
    {
        using var stub = new Stub();
        using var form = new TaskPreparationForm(stub, "1");
        Show(form);
        PumpUntil(() => Field<Button>(form, "btnConfirm").Enabled);
        var list = Field<ListBox>(form, "listSelected");
        list.SelectedIndex = SelectedItemIndex(list, true, stub.UserId);
        Check(Field<Button>(form, "btnRemove").Enabled,
            "The new-task creator requirement does not lock current managers of an existing task");
        Field<Button>(form, "btnRemove").PerformClick();
        Check(SelectedItemIndex(list, true, stub.UserId) == -1,
            "Existing-task manager selection keeps its prior remove behavior");
        Field<Button>(form, "btnConfirm").PerformClick();
        Check(form.Selection?.Context?.TaskId == "1" && form.Selection.ManagerUserIds.Count == 0,
            "Existing-task preparation does not invent an agreed minimum-manager write rule");
        NoScreenWrites(stub);
    }
    private static void LateResponses()
    {
        using var stub = new Stub(); using var form = new TaskPreparationForm(stub, "1"); Show(form);
        var first = new TaskCompletionSource<TaskCandidatePage>();
        stub.Next = _ => first.Task;
        var delayed = form.LoadCandidatesAsync(1);
        stub.Next = _ => Task.FromResult(new TaskCandidatePage(Array.AsReadOnly(new[] { new TaskUserCandidate(99, "최신", "최신#99") }), 1, 20, 1));
        var latest = form.LoadCandidatesAsync(1); Wait(latest.ContinueWith(_ => true));
        first.SetResult(new(Array.AsReadOnly(new[] { new TaskUserCandidate(88, "이전", "이전#88") }), 1, 20, 1));
        Wait(delayed.ContinueWith(_ => true));
        Check((int)Field<DataGridView>(form, "gridCandidates").Rows[0].Cells[0].Value! == 99, "An ignored cancellation cannot overwrite the latest candidates");
        var revoked = new TaskCompletionSource<TaskCandidatePage>(); stub.Next = _ => revoked.Task;
        var pending = form.LoadCandidatesAsync(1); stub.Revoke(); Application.DoEvents();
        revoked.SetResult(new(Array.Empty<TaskUserCandidate>(), 1, 20, 0)); Wait(pending.ContinueWith(_ => true));
        PumpUntil(() => Field<DataGridView>(form, "gridCandidates").Rows.Count == 0);
        Check(!Field<Button>(form, "btnConfirm").Enabled && Field<ListBox>(form, "listSelected").Items.Count == 0, "Session revocation removes context/selection and rejects a late response");
        form.Close();
    }
    private static void ParentDialog()
    {
        using var stub = new Stub(); using var form = new TaskEditForm(false, stub); Show(form);
        using var timer = new System.Windows.Forms.Timer { Interval = 10 };
        var deadline = DateTime.UtcNow.AddSeconds(10);
        timer.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.OfType<TaskPreparationForm>().FirstOrDefault();
            if (DateTime.UtcNow > deadline) { dialog?.Close(); timer.Stop(); return; }
            if (dialog is null || !Field<Button>(dialog, "btnConfirm").Enabled) return;
            var grid = Field<DataGridView>(dialog, "gridCandidates"); grid.CurrentCell = grid.Rows[0].Cells[0]; grid.Rows[0].Selected = true;
            Field<Button>(dialog, "btnChoose").PerformClick();
            Capture(dialog, "New-Task-Candidates");
            Field<Button>(dialog, "btnConfirm").PerformClick(); timer.Stop();
        };
        timer.Start(); Field<Button>(form, "btnPrepareCandidates").PerformClick(); timer.Stop();
        Check(form.PreparedSelection?.Assignee?.Id == 1 && form.CheckedInput is null, "The parent receives a separate unsaved selection without changing CheckedInput");
        Check(form.PreparedSelection is not null && form.PreparedSelection.ManagerUserIds.Count == 1
            && form.PreparedSelection.ManagerUserIds[0] == stub.UserId,
            "The registration form receives its required creator manager in the unsaved selection");
        Check(Field<ComboBox>(form, "cmbAssignee").Text.Contains("#1"), "The parent displays the server candidate label");
        Capture(form, "Task-Edit-Candidate-Selection");
        stub.Revoke(); Application.DoEvents();
        PumpUntil(() => form.PreparedSelection is null && !Field<Button>(form, "btnPrepareCandidates").Enabled);
        form.Close();
    }
    private static void PurposeSpecificManagementDialogs()
    {
        foreach (TaskManagementKind action in new[] { TaskManagementKind.Reassign, TaskManagementKind.ChangeManagers })
        {
            using var handler = new Handler();
            using var api = Api(handler);
            using var preparation = new Stub();
            using var workflow = new TaskManagementWorkflow(new HttpTaskManagementService(api.CaptureSession()));
            var detail = new TaskDetail(new TaskSummary("1", "목적별 선택 검사", "현재 담당자", TaskDisplayStatus.NotStarted,
                new DateOnly(2026, 10, 1), new DateOnly(2026, 12, 31)), "검사 업무");
            var queries = new TaskQueryTestDouble(new[] { detail });
            using var form = new TaskManagementForm(action, detail, queries, preparation, workflow);
            Show(form);
            PumpUntil(() => Field<Button>(form, "btnChooseCandidates").Enabled);
            bool managerAction = action == TaskManagementKind.ChangeManagers;
            TaskCandidateKind expectedKind = managerAction ? TaskCandidateKind.Manager : TaskCandidateKind.Assignee;
            Exception? modalError = null;
            bool exercised = false;
            using var timer = new System.Windows.Forms.Timer { Interval = 10 };
            var deadline = DateTime.UtcNow.AddSeconds(10);
            timer.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.OfType<TaskPreparationForm>().FirstOrDefault();
                if (DateTime.UtcNow > deadline)
                {
                    modalError = new TimeoutException("Purpose-specific management picker did not become ready");
                    dialog?.Close(); timer.Stop(); return;
                }
                if (dialog is null || !Field<Button>(dialog, "btnConfirm").Enabled) return;
                timer.Stop();
                try
                {
                    var kind = Field<ComboBox>(dialog, "cmbKind");
                    var list = Field<ListBox>(dialog, "listSelected");
                    Check(kind.SelectedIndex == (managerAction ? 1 : 0) && !kind.Enabled,
                        "The actual management modal starts with its own candidate kind and cannot switch roles");
                    Check(list.Items.Count == 1 && SelectedItemIndex(list, managerAction, managerAction ? 2 : 7) == 0,
                        "The management picker displays only the relationship this action can change");
                    kind.SelectedIndex = managerAction ? 0 : 1;
                    Check(kind.SelectedIndex == (managerAction ? 1 : 0)
                        && preparation.CandidateKinds.All(value => value == expectedKind),
                        "Even a programmatic kind change cannot query the unrelated candidate endpoint");
                    list.SelectedIndex = 0;
                    Field<Button>(dialog, "btnRemove").PerformClick();
                    Check(!Field<Button>(dialog, "btnConfirm").Enabled,
                        "A purpose-specific picker cannot confirm an empty required relationship");
                    ChooseCandidate(dialog, 3);
                    Check(Field<Button>(dialog, "btnConfirm").Enabled,
                        "Choosing a valid replacement enables confirmation");
                    Capture(dialog, managerAction ? "Managers-Only-Picker" : "Assignee-Only-Picker");
                    Field<Button>(dialog, "btnConfirm").PerformClick();
                    exercised = true;
                }
                catch (Exception error) { modalError = error; dialog.Close(); }
            };
            timer.Start();
            Field<Button>(form, "btnChooseCandidates").PerformClick();
            timer.Stop();
            if (modalError is not null) throw modalError;
            Check(exercised && preparation.CandidateCalls > 0 && preparation.CandidateKinds.All(value => value == expectedKind),
                "The actual management button opens and completes only its intended picker");
            Check(Field<int>(form, "selectedAssignee") == (managerAction ? 7 : 3)
                && Field<IReadOnlyList<int>>(form, "selectedManagers").SequenceEqual(managerAction ? new[] { 3 } : new[] { 2 }),
                "The parent receives the intended replacement while preserving the other relationship");
            Field<Button>(form, "btnConfirm").PerformClick();
            var request = Field<TaskManagementRequest>(form, "checkedRequest");
            Check(request.Kind == action && (managerAction
                    ? request.ManagerUserIds.SequenceEqual(new[] { 3 }) && request.AssigneeId is null
                    : request.AssigneeId == 3 && request.ManagerUserIds.Count == 0),
                "The checked management request contains exactly the relationship selected in the modal");
            Check(handler.Count == 1, "Candidate selection and input checking do not send a management write");
            NoScreenWrites(preparation);
            form.Close();
        }
    }
    private static void Capture(Form form, string name)
    {
        if (capture is null) return;
        Directory.CreateDirectory(capture);
        using var bitmap = new Bitmap(form.Width, form.Height); form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(Path.Combine(capture, name + ".png"));
    }
    private static void Live()
    {
        var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(Console.In.ReadToEnd())!;
        using var api = new ApiClient(settings["url"]);
        Wait(api.LoginAsync("admin", settings["admin_password"]));
        var admin = new HttpTaskPreparationService(api.CaptureSession());
        Check(Wait(admin.GetCandidatesAsync(new(TaskCandidateKind.Assignee))).Items.Count >= 2, "Actual assignee candidates");
        Check(Wait(admin.GetCandidatesAsync(new(TaskCandidateKind.Manager))).Items.All(x => x.Id == api.CurrentUser!.Id), "Actual active manager candidates");
        Check(Wait(admin.GetContextAsync("101")).RowVersion == "1", "Actual MySQL context");
        using (var form = new TaskPreparationForm(admin, "101"))
        { Show(form); PumpUntil(() => Field<Button>(form, "btnConfirm").Enabled); Check(Field<DataGridView>(form, "gridCandidates").Rows.Count >= 2, "Actual WinForms candidates via HTTP/MySQL"); form.Close(); }
        using (var host = new Form { Size = new(1100, 800) })
        using (var page = new TasksControl(RemoteWork.WinForms.Ui.PreviewRole.Administrator, new HttpTaskQueryService(api)))
        {
            host.Controls.Add(page); page.Dock = DockStyle.Fill; Show(host);
            Field<ComboBox>(page, "cmbTaskScope").SelectedIndex = 1;
            PumpUntil(() => Field<Button>(page, "btnPrepareWrite").Enabled);
            bool opened = false;
            using var timer = new System.Windows.Forms.Timer { Interval = 10 };
            var deadline = DateTime.UtcNow.AddSeconds(10);
            timer.Tick += (_, _) =>
            {
                var dialog = Application.OpenForms.OfType<TaskPreparationForm>().FirstOrDefault();
                if (DateTime.UtcNow > deadline) { dialog?.Close(); timer.Stop(); return; }
                if (dialog is null || !Field<Button>(dialog, "btnConfirm").Enabled) return;
                opened = Field<DataGridView>(dialog, "gridCandidates").Rows.Count >= 2;
                dialog.Close(); timer.Stop();
            };
            timer.Start(); Field<Button>(page, "btnPrepareWrite").PerformClick(); timer.Stop();
            Check(opened, "Actual TasksControl discovers the captured preparation service and opens the selected task dialog");
            host.Close();
        }
        Wait(api.LoginAsync("staff", settings["staff_password"]));
        var staff = new HttpTaskPreparationService(api.CaptureSession());
        Check(Wait(staff.GetContextAsync("101")).AssigneeId == api.CurrentUser!.Id, "Actual staff own context");
        Failure(() => Wait(staff.GetCandidatesAsync(new(TaskCandidateKind.Assignee))), TaskPreparationFailure.Forbidden);
        var key = Wait(staff.GetRequestKeyAsync(TaskWriteAction.Accept, "101"));
        Check(!Wait(staff.GetResultAsync(key)).Recorded, "Actual receipt lookup remains not-recorded; no action was executed");
        api.ClearIdentity();
        try { Wait(staff.GetContextAsync("101")); throw new InvalidOperationException("Revoked scope sent"); }
        catch (OperationCanceledException) { Check(true, "Actual captured scope cancels after logout/identity clear"); }
    }
    private sealed class Handler : HttpMessageHandler
    {
        private HttpResponseMessage? next;
        public HttpRequestMessage? Last; public string Body = ""; public string? Bearer; public string? Key; public int Count;
        public void Success(object data) => next = new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "fixture" }), Encoding.UTF8, "application/json") };
        public void Error(int status, string code) => next = new((HttpStatusCode)status) { Content = new StringContent(JsonSerializer.Serialize(new { ok = false, error = new { code, message = "합성 오류" }, request_id = "fixture" }), Encoding.UTF8, "application/json") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Count++; Last = request; Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Bearer = request.Headers.Authorization?.Parameter; Key = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
            var response = next ?? throw new InvalidOperationException("Unexpected HTTP call"); next = null; return response;
        }
    }
    private sealed class Stub : ITaskPreparationService, IDisposable
    {
        private readonly CancellationTokenSource cancellation = new();
        public bool IsCurrent { get; private set; } = true;
        public bool CanSelectCandidates { get; }
        public int UserId => 2;
        public CancellationToken SessionCancellation => cancellation.Token;
        public int KeyCalls;
        public int ResultCalls;
        public int CandidateCalls;
        public readonly List<TaskCandidateKind> CandidateKinds = new();
        public int ContextCalls;
        public Func<TaskCandidateQuery, Task<TaskCandidatePage>>? Next;
        public Stub(bool canSelectCandidates = true) { CanSelectCandidates = canSelectCandidates; }
        public Task<TaskWriteContext> GetContextAsync(string id, CancellationToken ct = default)
        {
            ContextCalls++;
            return Task.FromResult(new TaskWriteContext(id, 7, Array.AsReadOnly(new[] { 2 }), "1", 1, TaskDisplayStatus.NotStarted, DateTimeOffset.UtcNow));
        }
        public Task<TaskCandidatePage> GetCandidatesAsync(TaskCandidateQuery q, CancellationToken ct = default)
        {
            CandidateCalls++;
            CandidateKinds.Add(q.Kind);
            if (Next is not null) return Next(q);
            var items = q.Search == "empty" ? Array.Empty<TaskUserCandidate>()
                : Enumerable.Range(q.Page == 1 ? 1 : 21, q.Page == 1 ? 20 : 1).Select(id => new TaskUserCandidate(id, "동명이인", "동명이인 · #" + id)).ToArray();
            return Task.FromResult(new TaskCandidatePage(Array.AsReadOnly(items), q.Page, q.Limit, q.Search == "empty" ? 0 : 21));
        }
        public Task<TaskWriteRequestKey> GetRequestKeyAsync(TaskWriteAction action, string? taskId, CancellationToken ct = default) { KeyCalls++; throw new InvalidOperationException("A preparation screen must not issue keys"); }
        public Task<TaskWriteResultLookup> GetResultAsync(TaskWriteRequestKey key, CancellationToken ct = default)
        {
            ResultCalls++;
            throw new InvalidOperationException("No write from this screen");
        }
        public void Revoke() { IsCurrent = false; cancellation.Cancel(); }
        public void Dispose() => cancellation.Dispose();
    }
}
