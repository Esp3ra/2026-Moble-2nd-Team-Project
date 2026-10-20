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
    private static readonly List<string> failures = new();
    private static readonly TaskDetail Detail = new(new TaskSummary("9007199254740993", "개인 업무 제목", "본인", TaskDisplayStatus.InProgress,
        new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 20)), "개인 업무 설명");

    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        Control.CheckForIllegalCrossThreadCalls = true;
        using var coordinator = new Form { ShowInTaskbar = false, Location = new(-20000, -20000), StartPosition = FormStartPosition.Manual, Size = new(1, 1) };
        coordinator.Shown += (_, _) => coordinator.BeginInvoke((Action)(() =>
        {
            try
            {
                Run("edit captured-session invalidation", EditSession);
                Run("workflow-only captured-session invalidation", WorkflowOnlySession);
                Run("extension captured-session invalidation", ExtensionSession);
                Run("cancelled preparation lifetime", TokenCancellation);
                Run("expired before show", ExpiredBeforeShow);
                Run("close and dispose", ClosedInputs);
                Run("legacy preview", LegacyPreview);
                Run("parent injects captured service", ParentInjection);
            }
            finally { coordinator.Close(); }
        }));
        Application.Run(coordinator);
        foreach (string failure in failures) Console.Error.WriteLine("FAIL " + failure);
        Console.WriteLine($"TaskInputSession: {checks} checks; {failures.Count} failed scenarios; actual WinForms, controlled login only, no server/DB.");
        return failures.Count == 0 ? 0 : 1;
    }

    private static void Run(string name, Action scenario)
    {
        try { scenario(); Console.WriteLine("PASS " + name); }
        catch (Exception error) { failures.Add(name + ": " + error.GetType().Name + ": " + error.Message); }
    }
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new InvalidOperationException(message); }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(owner)!;
    private static void Show(Form form)
    { form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new(-15000, -15000); form.Show(); Application.DoEvents(); }
    private static void Wait(Task task)
    {
        var end = DateTime.UtcNow.AddSeconds(15);
        while (!task.IsCompleted && DateTime.UtcNow < end) { Application.DoEvents(); Thread.Sleep(1); }
        if (!task.IsCompleted) throw new TimeoutException();
        task.GetAwaiter().GetResult();
    }
    private static ApiClient Login(Handler handler, int userId = 7)
    {
        handler.UserId = userId;
        var api = new ApiClient("http://localhost:5000/", handler);
        Wait(api.LoginAsync("fixture", "synthetic"));
        return api;
    }
    private static DeadlineExtensionForm Extension(ITaskPreparationService service)
    {
        // The fallback permits the exact old product to compile and demonstrate its missing session boundary.
        var constructor = typeof(DeadlineExtensionForm).GetConstructor(new[] { typeof(PreviewRole), typeof(TaskDetail), typeof(ITaskPreparationService) });
        return constructor is null ? new DeadlineExtensionForm(PreviewRole.Employee, Detail)
            : (DeadlineExtensionForm)constructor.Invoke(new object[] { PreviewRole.Employee, Detail, service });
    }
    private static void FillEdit(TaskEditForm form, bool confirm = true)
    {
        Field<TextBox>(form, "txtTaskTitle").Text = "확인한 개인 업무";
        Field<TextBox>(form, "txtTaskDescription").Text = "개인 입력";
        foreach (string name in new[] { "dtpStartDate", "dtpDeadline" })
        { var picker = Field<DateTimePicker>(form, name); picker.Value = new DateTime(2026, 10, name == "dtpStartDate" ? 19 : 20); picker.Checked = true; }
        if (confirm)
        {
            Field<Button>(form, "btnConfirm").PerformClick();
            Check(form.CheckedInput is not null, "edit input is prepared before invalidation");
        }
    }
    private static void FillExtension(DeadlineExtensionForm form)
    {
        Field<TextBox>(form, "txtExtensionReason").Text = "개인 연장 사유";
        var requested = DeadlineExtensionPolicy.KoreanDate(DateTimeOffset.UtcNow).AddDays(1);
        if (requested <= Detail.Summary.DueDate) requested = Detail.Summary.DueDate.AddDays(1);
        var picker = Field<DateTimePicker>(form, "dtpRequestedDeadline"); picker.Value = requested.ToDateTime(TimeOnly.MinValue); picker.Checked = true;
        Field<Button>(form, "btnConfirm").PerformClick();
        Check(form.CheckedInput is not null, "extension input is prepared before invalidation");
    }
    private static void EditSession()
    {
        using var handler = new Handler(); using var api = Login(handler);
        using var form = new TaskEditForm(Detail, new HttpTaskPreparationService(api.CaptureSession())); Show(form); FillEdit(form);
        api.ClearIdentity();
        Check(form.CheckedInput is null, "edit getter must discard old input before the queued UI callback runs");
        Application.DoEvents();
        Check(!Field<Button>(form, "btnConfirm").Enabled && Field<TextBox>(form, "txtTaskDescription").Text.Length == 0,
            "edit callback blocks confirmation and clears prior private input");
        Check(Field<TextBox>(form, "txtTaskTitle").Text.Length == 0
            && !Field<DateTimePicker>(form, "dtpStartDate").Text.Contains("2026")
            && !Field<DateTimePicker>(form, "dtpDeadline").Text.Contains("2026"), "edit callback hides old task title and dates");
        Check(handler.ProductRequests == 0, "edit invalidation makes no task/key/result/write request");
    }
    private static void ExtensionSession()
    {
        using var handler = new Handler(); using var api = Login(handler);
        using var form = Extension(new HttpTaskPreparationService(api.CaptureSession())); Show(form); FillExtension(form);
        var original = form.CheckedInput!;
        api.ClearIdentity();
        Check(form.CheckedInput is null, "extension getter must discard old input before the queued UI callback runs");
        Application.DoEvents();
        Check(!Field<Button>(form, "btnConfirm").Enabled && !Field<TextBox>(form, "txtExtensionReason").Enabled,
            "extension callback blocks previous session input");
        Check(Field<TextBox>(form, "txtTaskSummary").Text.Length == 0 && Field<TextBox>(form, "txtCurrentDeadline").Text.Length == 0
            && Field<TextBox>(form, "txtExtensionReason").Text.Length == 0 && Field<TextBox>(form, "txtExtensionNotice").Text.Length == 0,
            "extension callback clears old task summary, dates, reason and notice");
        Check(!Field<DateTimePicker>(form, "dtpRequestedDeadline").Text.Contains("2026"), "extension callback hides the previous requested date");
        handler.UserId = 8; Wait(api.LoginAsync("other", "synthetic"));
        Field<Button>(form, "btnConfirm").PerformClick();
        Check(form.CheckedInput is null && !Field<Button>(form, "btnConfirm").Enabled, "old extension form cannot be reused by a replacement login");
        Check(original.TaskId == Detail.Summary.TaskId && original.Reason == "개인 연장 사유", "previous immutable snapshot is not rewritten");
        Check(handler.ProductRequests == 0, "extension invalidation and replacement login make no product request");
    }
    private static void WorkflowOnlySession()
    {
        using var handler = new Handler { Role = "admin" }; using var api = Login(handler);
        using var workflow = new TaskCreationWorkflow(new HttpTaskCreationService(api.CaptureSession()));
        using var form = new TaskEditForm(false, null, workflow); Show(form); FillEdit(form, confirm: false);
        // This public constructor deliberately has no candidate service and cannot submit.
        // Seed only a matching local snapshot to exercise the getter's workflow-only trust boundary.
        var input = new TaskBasicInputDraft(null, "확인한 개인 업무", "개인 입력", new(2026, 10, 19), new(2026, 10, 20));
        typeof(TaskEditForm).GetField("checkedInput", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(form, input);
        Check(ReferenceEquals(form.CheckedInput, input), "workflow-only getter starts with a matching local snapshot");
        Task.Run(api.ClearIdentity).GetAwaiter().GetResult(); // Do not pump the queued UI callback yet.
        Check(form.CheckedInput is null, "workflow-only getter discards its snapshot before the queued UI callback");
        Application.DoEvents();
        Check(!Field<Button>(form, "btnConfirm").Enabled && Field<TextBox>(form, "txtTaskDescription").Text.Length == 0,
            "workflow-only callback blocks and clears its old input");
        Check(handler.ProductRequests == 0, "workflow-only lifetime check sends no product request");
    }
    private static void TokenCancellation()
    {
        using var prep = new Preparation();
        using var edit = new TaskEditForm(Detail, prep); Show(edit); FillEdit(edit);
        using var extension = Extension(prep); Show(extension); FillExtension(extension);
        prep.Lifetime.Cancel(); // Exercise cancellation even if a test adapter still reports IsCurrent.
        Check(edit.CheckedInput is null && extension.CheckedInput is null, "cancelled captured lifetime invalidates both getters immediately");
        Application.DoEvents();
        Check(!Field<Button>(edit, "btnConfirm").Enabled && !Field<Button>(extension, "btnConfirm").Enabled, "cancelled lifetime cannot confirm either form");
        Check(prep.Requests == 0, "local preparation never invokes the service");
    }
    private static void ExpiredBeforeShow()
    {
        using var handler = new Handler(); using var api = Login(handler);
        var prep = new HttpTaskPreparationService(api.CaptureSession()); api.ClearIdentity();
        using var edit = new TaskEditForm(Detail, prep); Show(edit);
        using var extension = Extension(prep); Show(extension);
        Check(edit.CheckedInput is null && extension.CheckedInput is null, "expired session cannot publish an input");
        Check(!Field<Button>(edit, "btnConfirm").Enabled && !Field<Button>(extension, "btnConfirm").Enabled, "expired-before-show forms begin blocked");
        Check(handler.ProductRequests == 0, "opening expired forms sends no product request");
    }
    private static void ClosedInputs()
    {
        foreach (bool dispose in new[] { false, true })
        {
            using var edit = new TaskEditForm(Detail); Show(edit); FillEdit(edit);
            if (dispose) edit.Dispose(); else edit.Close();
            Check(edit.CheckedInput is null, "closed/disposed edit cannot expose input even in preview");
            using var extension = new DeadlineExtensionForm(PreviewRole.Employee, Detail); Show(extension); FillExtension(extension);
            if (dispose) extension.Dispose(); else extension.Close();
            Check(extension.CheckedInput is null, "closed/disposed extension cannot expose input even in preview");
        }
        using var prep = new Preparation();
        var beforeShown = Extension(prep); beforeShown.Dispose(); prep.Lifetime.Cancel();
        Check(beforeShown.CheckedInput is null, "dispose-before-show and later cancellation are harmless");
    }
    private static void LegacyPreview()
    {
        using var empty = new DeadlineExtensionForm(); Show(empty);
        Check(empty.CheckedInput is null && !Field<Button>(empty, "btnConfirm").Enabled, "empty Designer constructor has no task or connected service");
        using var old = new DeadlineExtensionForm(PreviewRole.Employee, Detail); Show(old); FillExtension(old);
        Check(old.CheckedInput!.OriginalDueDate == Detail.Summary.DueDate, "legacy detail preview still confirms a local input only");
        Field<TextBox>(old, "txtExtensionReason").Text += " 수정";
        Check(old.CheckedInput is null, "editing the reason invalidates local input");
    }
    private static void ParentInjection()
    {
        using var handler = new Handler(); using var api = Login(handler);
        using var host = new Form { Size = new(1200, 850), ShowInTaskbar = false };
        using var tasks = new TasksControl(PreviewRole.Employee, new Queries(new HttpTaskPreparationService(api.CaptureSession())));
        host.Controls.Add(tasks); tasks.Dock = DockStyle.Fill; Show(host); Wait(tasks.RefreshAsync());
        bool inspected = false; Exception? error = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 20 };
        timer.Tick += (_, _) =>
        {
            if (Application.OpenForms.OfType<DeadlineExtensionForm>().FirstOrDefault() is not { } child) return;
            timer.Stop();
            try
            {
                FillExtension(child); api.ClearIdentity();
                Check(child.CheckedInput is null, "TasksControl injects the captured session into its extension modal");
                Application.DoEvents();
                Check(!Field<Button>(child, "btnConfirm").Enabled, "parent-opened extension blocks after session change");
                inspected = true;
            }
            catch (Exception ex) { error = ex; }
            finally { child.Close(); }
        };
        timer.Start(); Field<Button>(tasks, "btnDeadlineExtensionPreview").PerformClick(); timer.Stop();
        if (error is not null) throw error;
        Check(inspected, "the actual parent button opened and inspected its modal");
        Check(handler.ProductRequests == 0, "parent/modal path sends no task/key/result/write HTTP request");
    }

    private sealed class Handler : HttpMessageHandler
    {
        public int UserId { get; set; } = 7;
        public string Role { get; init; } = "employee";
        public int ProductRequests { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath != "/api/v1/auth/login")
            { ProductRequests++; throw new InvalidOperationException("Unexpected product request"); }
            var payload = new { ok = true, data = new { access_token = "synthetic-token", token_type = "Bearer", expires_at = "2030-01-01T00:00:00Z",
                user = new { id = UserId, login_id = "fixture", display_name = "합성 사용자", employee_no = "FIX", department = "검증팀", role = Role, is_active = true } }, request_id = "fixture" };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json") });
        }
    }
    private sealed class Preparation : ITaskPreparationService, IDisposable
    {
        public CancellationTokenSource Lifetime { get; } = new();
        public bool IsCurrent => true;
        public bool CanSelectCandidates => false;
        public int UserId => 7;
        public CancellationToken SessionCancellation => Lifetime.Token;
        public int Requests { get; private set; }
        private Task<T> Unexpected<T>() { Requests++; throw new InvalidOperationException("Unexpected preparation request"); }
        public Task<TaskCandidatePage> GetCandidatesAsync(TaskCandidateQuery query, CancellationToken ct = default) => Unexpected<TaskCandidatePage>();
        public Task<TaskWriteContext> GetContextAsync(string taskId, CancellationToken ct = default) => Unexpected<TaskWriteContext>();
        public Task<TaskWriteRequestKey> GetRequestKeyAsync(TaskWriteAction action, string? taskId, CancellationToken ct = default) => Unexpected<TaskWriteRequestKey>();
        public Task<TaskWriteResultLookup> GetResultAsync(TaskWriteRequestKey key, CancellationToken ct = default) => Unexpected<TaskWriteResultLookup>();
        public void Dispose() => Lifetime.Dispose();
    }
    private sealed class Queries(ITaskPreparationService prep) : ITaskQueryService, ITaskPreparationSource
    {
        public string SourceDescription => "명시적 조회 테스트 대역";
        public ITaskPreparationService CreatePreparationService() => prep;
        public Task<IReadOnlyList<TaskSummary>> GetTasksAsync(TaskListQuery query, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TaskSummary>>(new[] { Detail.Summary });
        public Task<TaskDetail> GetTaskAsync(string taskId, CancellationToken cancellationToken = default) => Task.FromResult(Detail);
    }
}
