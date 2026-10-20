using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

internal static class Program
{
    private static int checks, scenarios;
    private static readonly UserInfo Admin = new(1, "admin-test", "관리자", "A", "개발", "admin", true);
    private static readonly UserInfo Created = new(88, "new-test", "새 직원", "B", "개발", "employee", true);
    [STAThread]
    private static int Main()
    {
        ApplicationConfiguration.Initialize();
        Control.CheckForIllegalCrossThreadCalls = true;
        int exit = 1;
        using var coordinator = new Form { ShowInTaskbar = false, Location = new(-20000, -20000), Size = new(1, 1), StartPosition = FormStartPosition.Manual };
        coordinator.Shown += (_, _) => coordinator.BeginInvoke((Action)(() =>
        {
            try
            {
                CreatedIdentity(false); CreatedIdentity(true); RefreshFailure(); PatchIsNotEnrollment();
                foreach (string change in new[] { "selection", "clear", "relogin", "close", "dispose" }) LateCreate(change);
                LostResponse(); TargetLifetime(); ReentrantHandoff(); Unauthorized(); Disconnected();
                EnrollmentConnectionChecks.Run();
                Console.WriteLine($"PASS UserManagement UI: {scenarios} scenarios / {checks} assertions; actual WinForms + controlled HTTP; no device/server/DB.");
                exit = 0;
            }
            catch (Exception ex) { Console.WriteLine($"FAIL after {checks}: {ex}"); }
            finally { coordinator.Close(); }
        }));
        Application.Run(coordinator); return exit;
    }
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new InvalidOperationException(message); }
    private static void Until(Func<bool> predicate)
    {
        var timer = Stopwatch.StartNew();
        while (!predicate())
        { Application.DoEvents(); Thread.Sleep(1); if (timer.ElapsedMilliseconds > 8000) throw new TimeoutException("UI did not settle"); }
    }
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static object? Call(object owner, string name, params object?[] args) => owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, args);
    private static void Complete(Task task) { Until(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static ApiClient Login(Handler handler)
    { var api = new ApiClient("http://enrollment.test:5000", handler); Complete(api.LoginAsync("admin-test", "synthetic-test-value")); return api; }
    private static void Ready(UserManagementForm form)
    { form.Show(); Application.DoEvents(); Until(() => !Field<bool>(form, "loading")); }
    private static Task Create(UserManagementForm form)
    {
        Call(form, "ClearEditor"); Field<TextBox>(form, "login").Text = Created.LoginId;
        Field<TextBox>(form, "name").Text = Created.DisplayName; Field<TextBox>(form, "password").Text = "synthetic-test-value";
        return (Task)Call(form, "Save")!;
    }
    private static void CreatedIdentity(bool visible)
    {
        scenarios++; using var handler = new Handler { ShowCreated = visible }; using var api = Login(handler);
        using var form = new UserManagementForm(api); Ready(form); Complete(Create(form));
        var target = form.EnrollmentTarget;
        Check(handler.Creates == 1 && target?.UserId == Created.Id, "Creation uses returned User.Id, never row one");
        Check(Field<int>(form, "selectedId") == Created.Id && Field<TextBox>(form, "login").Text == Created.LoginId, "Editor preserves new member even outside current page");
        Check(target!.AdministratorSession.User.Id == Admin.Id && target.UserId != Admin.Id, "Administrator actor and employee target remain distinct");
        Check(target.IsCurrent && !target.Cancellation.IsCancellationRequested, "Post-save refresh preserves current handoff");
        var grid = Field<DataGridView>(form, "grid");
        Check(visible ? (grid.CurrentRow?.DataBoundItem as UserInfo)?.Id == Created.Id : grid.CurrentRow is null, "Grid cannot silently select first row for an off-page member");
        Check(handler.Paths.All(p => !p.Contains("device") && !p.Contains("fingerprint")), "Account save starts no enrollment or device request");
        Check(Field<TextBox>(form, "password").Text == "", "Password cleared after save");
        form.Close(); Check(!target.IsCurrent && target.Cancellation.IsCancellationRequested, "Closing invalidates borrowed target");
    }
    private static void RefreshFailure()
    {
        scenarios++; using var handler = new Handler { FailRefresh = true }; using var api = Login(handler);
        using var form = new UserManagementForm(api); Ready(form); Complete(Create(form));
        Check(form.EnrollmentTarget?.UserId == Created.Id, "List refresh failure does not discard confirmed creation ID");
        Check(Field<Label>(form, "status").Text.Contains("목록 갱신 실패") && handler.Creates == 1, $"Refresh error distinguishes successful creation without retrying it: reads={handler.Reads}, gen={Field<long>(form, "selectionGeneration")}, " + Field<Label>(form, "status").Text);
        form.Close();
    }
    private static void PatchIsNotEnrollment()
    {
        scenarios++; using var handler = new Handler(); using var api = Login(handler);
        using var form = new UserManagementForm(api); Ready(form); Complete((Task)Call(form, "Save")!);
        Check(handler.Patches == 1 && handler.Creates == 0 && form.EnrollmentTarget is null, "Editing existing account cannot trigger fresh enrollment handoff"); form.Close();
    }
    private static void LateCreate(string change)
    {
        scenarios++; using var handler = new Handler { Deferred = new(TaskCreationOptions.RunContinuationsAsynchronously) }; using var api = Login(handler);
        using var form = new UserManagementForm(api); Ready(form); Task save = Create(form);
        Until(() => handler.Creates == 1);
        Complete((Task)Call(form, "Save")!);
        Check(handler.Creates == 1, "Double save while pending is suppressed");
        switch (change)
        {
            case "selection": Call(form, "Fill", Admin, true); break;
            case "clear": Call(form, "ClearEditor"); break;
            case "relogin": Complete(api.LoginAsync("admin-test", "synthetic-test-value")); Application.DoEvents(); break;
            case "close": form.Close(); break;
            case "dispose": form.Dispose(); break;
        }
        handler.Deferred.SetResult(Handler.Json(new UserResult(Created))); Complete(save); Application.DoEvents();
        Check(form.EnrollmentTarget is null, $"Late create after {change} cannot create a handoff");
        if (!form.IsDisposed)
            Check(Field<int>(form, "selectedId") == (change == "selection" ? Admin.Id : 0), "Late response cannot replace changed editor");
        Check(handler.Reads == 1, "Discarded creation response does not start a list refresh");
        if (!form.IsDisposed) form.Close();
    }
    private static void LostResponse()
    {
        scenarios++; using var handler = new Handler { LoseCreate = true }; using var api = Login(handler);
        using var form = new UserManagementForm(api); Ready(form); Complete(Create(form));
        Check(handler.Creates == 1 && form.EnrollmentTarget is null, "Unknown create outcome neither retries nor invents a target");
        Check(Field<Label>(form, "status").Text.Contains("생성 여부"), "Lost response asks for account-result check"); form.Close();
    }
    private static void TargetLifetime()
    {
        scenarios++; using var handler = new Handler(); using var api = Login(handler);
        using var form = new UserManagementForm(api); Ready(form); Complete(Create(form)); var target = form.EnrollmentTarget!;
        Call(form, "Fill", Admin, true);
        Check(!target.IsCurrent && target.Cancellation.IsCancellationRequested && form.EnrollmentTarget is null, "Selection changes cancel old target");
        try { target.ThrowIfChanged(); throw new InvalidOperationException("Stale target accepted"); }
        catch (OperationCanceledException) { checks++; }
        Complete(Create(form)); var second = form.EnrollmentTarget!;
        Complete(api.LoginAsync("admin-test", "synthetic-test-value")); Application.DoEvents();
        Check(!second.IsCurrent && second.Cancellation.IsCancellationRequested, "Same-user relogin is a new administrator session");
        Check(Field<int>(form, "selectedId") == 0 && !Field<Button>(form, "save").Enabled, "Expired form clears old account and cannot save under new login");
        form.Close();
    }
    private static void ReentrantHandoff()
    {
        scenarios++; using var handler = new Handler(); using var api = Login(handler);
        using var form = new UserManagementForm(api); Ready(form);
        form.EnrollmentTargetChanged += (_, _) => { if (form.EnrollmentTarget is not null) Call(form, "ClearEditor"); };
        Complete(Create(form));
        Check(form.EnrollmentTarget is null && Field<int>(form, "selectedId") == 0 && handler.Reads == 1, "Handoff callback target change prevents stale rebind/refresh"); form.Close();
    }
    private static void Unauthorized()
    {
        scenarios++; using var handler = new Handler { UnauthorizedCreate = true }; using var api = Login(handler);
        using var form = new UserManagementForm(api); Ready(form); Complete(Create(form)); Application.DoEvents();
        Check(api.CurrentUser is null && form.EnrollmentTarget is null && Field<int>(form, "selectedId") == 0, "Current 401 clears administrator and target"); form.Close();
    }
    private static void Disconnected()
    {
        scenarios++; using var handler = new Handler(); using var api = new ApiClient("http://enrollment.test:5000", handler);
        using var form = new UserManagementForm(api); Ready(form); Complete((Task)Call(form, "Save")!);
        Check(handler.Paths.Count == 0 && !Field<Button>(form, "save").Enabled, "Disconnected view performs no requests"); form.Close();
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Creates, Patches, Reads; public bool ShowCreated, FailRefresh, LoseCreate, UnauthorizedCreate;
        public TaskCompletionSource<HttpResponseMessage>? Deferred;
        public List<string> Paths = [];
        public static HttpResponseMessage Json(object data) => new(HttpStatusCode.OK)
        { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data }, ApiClient.Json), Encoding.UTF8, "application/json") };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string path = request.RequestUri!.AbsolutePath; Paths.Add(path);
            if (path.EndsWith("auth/login")) return Task.FromResult(Json(new LoginResult("synthetic-token", "Bearer", DateTimeOffset.UtcNow.AddHours(1), Admin)));
            if (request.Method == HttpMethod.Get)
            {
                Reads++;
                if (FailRefresh && Reads > 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                return Task.FromResult(Json(new PageResult<UserInfo>(ShowCreated && Creates > 0 ? [Admin, Created] : [Admin], 1, 50, 100)));
            }
            if (request.Method == HttpMethod.Patch) { Patches++; return Task.FromResult(Json(new UserResult(Admin))); }
            Creates++;
            if (UnauthorizedCreate) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            if (LoseCreate) throw new HttpRequestException("synthetic lost reply");
            // Deliberately ignores cancellation, to exercise response guards.
            return Deferred?.Task ?? Task.FromResult(Json(new UserResult(Created)));
        }
    }
}
