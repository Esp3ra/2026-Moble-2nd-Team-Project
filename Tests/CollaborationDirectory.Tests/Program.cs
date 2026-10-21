using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

static class Program
{
    private static int checks;
    private static TaskSummary Row(string id, TaskDisplayStatus status = TaskDisplayStatus.InProgress, bool cancelled = false) =>
        new(id, "Fixture task", "Fixture owner", status, new(2026, 10, 21), new(2026, 10, 30), cancelled);
    private static T Field<T>(object form, string name) => (T)form.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(form)!;
    private static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); checks++; }
    private static CollaborationRequestForm Open(Source source)
    {
        var form = new CollaborationRequestForm(source) { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
        form.Show(); return form;
    }
    [STAThread]
    private static int Main()
    {
        Application.EnableVisualStyles();
        Control.CheckForIllegalCrossThreadCalls = true;
        int result = 1;
        using var host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-20000, -20000) };
        host.Shown += async (_, _) =>
        {
            try { await Run(); Console.WriteLine($"PASS: 6 scenarios / {checks} checks; actual WinForms, explicit HTTP/data doubles. No real server or DB."); result = 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            finally { host.Close(); }
        };
        Application.Run(host); return result;
    }
    private static async Task Run()
    {
        // 1. Exercise the actual adapter and existing captured-login GET contracts.
        using (var handler = new Handler())
        using (var api = new ApiClient("http://localhost:5000/", handler))
        {
            await api.LoginAsync("fixture", "fixture-password");
            var type = typeof(CollaborationRequestForm).Assembly.GetType("RemoteWork.WinForms.Features.Collaboration.HttpCollaborationDirectory")!;
            var read = (ICollaborationDirectory)Activator.CreateInstance(type, api)!;
            Check((await read.GetOwnTasksAsync(default)).Count == 0, "empty server task list remains empty");
            Check(handler.Paths.Last() == "/api/v1/tasks?scope=own&status=in_progress", "only own/in_progress reads");
            Check((await read.GetRecipientsAsync(default)).Single().UserId == 2, "existing minimal user DTO mapping");
            Check(handler.Paths.Last() == "/api/v1/chat/participants?page=1&limit=100", "existing active user directory, not administrator users API");
            api.ClearIdentity();
            try { await read.GetRecipientsAsync(default); throw new Exception("old identity accepted"); }
            catch (OperationCanceledException) { Check(!read.IsCurrent, "old login never follows a new identity"); }
            Check(handler.Paths.Count == 3, "only login and two reads; no writes");
        }
        // 2. Choices are identity-bearing objects, no static/department-only people.
        using (var source = new Source())
        using (var form = Open(source))
        {
            source.Tasks = () => Task.FromResult<IReadOnlyList<TaskSummary>>([
                Row("9223372036854775807"), Row("2", TaskDisplayStatus.Submitted), Row("3", cancelled: true)]);
            await form.ReloadDirectoryAsync();
            var tasks = Field<ComboBox>(form, "cmbOptions"); var people = Field<ComboBox>(form, "cmbOptions2");
            Check(tasks.Items.Count == 1 && tasks.Items[0]!.ToString()!.Contains("9223372036854775807"), "non-cancelled/in-progress; task ID stays opaque string");
            Check(people.Items.Count == 2, "exclude self and duplicate IDs; retain same-name distinct users");
            Check(people.Items.Cast<CollaborationRecipient>().All(p => p.UserId != 1), "no self recipient");
            Check(people.Items[0]!.ToString() != people.Items[1]!.ToString(), "same-name people distinguishable by stable ID");
            Check(tasks.SelectedIndex == -1 && people.SelectedIndex == -1, "no automatic selection");
            Check(!Field<Button>(form, "btnSendRequest").Enabled, "request saving is outside this list-only scope");
        }
        // 3. The current deployment's directory404 must not hide a successful task list.
        using (var source = new Source())
        using (var form = Open(source))
        {
            source.Users = () => Task.FromException<IReadOnlyList<CollaborationRecipient>>(new ApiException("request fixture", 404, "HTTP_404"));
            await form.ReloadDirectoryAsync();
            Check(Field<ComboBox>(form, "cmbOptions").Items.Count == 1 && Field<ComboBox>(form, "cmbOptions").Enabled, "task list independent of failed directory");
            Check(Field<ComboBox>(form, "cmbOptions2").Items.Count == 0 && !Field<ComboBox>(form, "cmbOptions2").Enabled, "no stale/sample people on404");
            Check(Field<Label>(form, "directoryStatus").Text.Contains("API가 준비되지"), "404 distinguished from empty user list");
            Check(Field<Button>(form, "refreshButton").Enabled, "retry available after404");
            source.Users = Source.People;
            await form.ReloadDirectoryAsync();
            Check(Field<ComboBox>(form, "cmbOptions2").Items.Count == 2, "directory can recover after server preparation");
            source.Tasks = () => Task.FromException<IReadOnlyList<TaskSummary>>(new TaskQueryException(TaskQueryFailure.Unavailable));
            await form.ReloadDirectoryAsync();
            Check(Field<ComboBox>(form, "cmbOptions").Items.Count == 0 && Field<ComboBox>(form, "cmbOptions2").Items.Count == 2, "task failure also independent of valid people");
            Check(Field<Label>(form, "directoryStatus").Text.Contains("업무 조회 실패"), "task failure not converted to empty success");
            source.Tasks = () => Task.FromResult<IReadOnlyList<TaskSummary>>([]);
            source.Users = () => Task.FromResult<IReadOnlyList<CollaborationRecipient>>([]);
            await form.ReloadDirectoryAsync();
            Check(Field<Label>(form, "directoryStatus").Text.Contains("진행중 업무가 없습니다") && Field<Label>(form, "directoryStatus").Text.Contains("활성 사용자가 없습니다"), "explicit empty states");
        }
        // 4. A late response/error from a cancelled refresh must not replace newer data.
        using (var source = new Source())
        using (var form = Open(source))
        {
            var late = new TaskCompletionSource<IReadOnlyList<TaskSummary>>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.Tasks = () => late.Task; var oldRead = form.ReloadDirectoryAsync();
            source.Tasks = () => Task.FromResult<IReadOnlyList<TaskSummary>>([Row("current")]);
            await form.ReloadDirectoryAsync();
            late.SetException(new TaskQueryException(TaskQueryFailure.Unavailable)); await oldRead;
            Check(Field<ComboBox>(form, "cmbOptions").Items[0]!.ToString()!.Contains("current"), "stale failure cannot overwrite latest task data");
            Check(!Field<Label>(form, "directoryStatus").Text.Contains("업무 조회 실패"), "stale error not displayed");
        }
        // 5. Idle choices disappear when the captured login ends.
        using (var source = new Source())
        using (var form = Open(source))
        {
            await form.ReloadDirectoryAsync(); source.EndIdentity(); await Task.Delay(20);
            Check(Field<ComboBox>(form, "cmbOptions").Items.Count == 0 && Field<ComboBox>(form, "cmbOptions2").Items.Count == 0, "logout clears both lists");
            Check(!Field<Button>(form, "refreshButton").Enabled, "old source cannot refresh under another login");
        }
        // 6. The view cancels only itself; it never disposes the borrowed identity.
        using (var source = new Source())
        {
            var form = Open(source);
            var late = new TaskCompletionSource<IReadOnlyList<TaskSummary>>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.Tasks = () => late.Task; var pending = form.ReloadDirectoryAsync();
            form.Dispose(); late.SetResult([Row("closed")]); await pending;
            Check(form.IsDisposed && source.IsCurrent, "closed view rejects late result without owning shared session");
        }
    }
    private sealed class Source : ICollaborationDirectory, IDisposable
    {
        private readonly CancellationTokenSource identity = new();
        public int CurrentUserId => 1;
        public bool IsCurrent { get; private set; } = true;
        public CancellationToken Cancellation => identity.Token;
        public Func<Task<IReadOnlyList<TaskSummary>>> Tasks = () => Task.FromResult<IReadOnlyList<TaskSummary>>([Row("1")]);
        public Func<Task<IReadOnlyList<CollaborationRecipient>>> Users = People;
        public static Task<IReadOnlyList<CollaborationRecipient>> People() => Task.FromResult<IReadOnlyList<CollaborationRecipient>>([
            new(1, "Self", "Test"), new(2, "Same", "Test"), new(3, "Same", "Test"), new(2, "Duplicate", "Test")]);
        public Task<IReadOnlyList<TaskSummary>> GetOwnTasksAsync(CancellationToken ct) => Tasks();
        public Task<IReadOnlyList<CollaborationRecipient>> GetRecipientsAsync(CancellationToken ct) => Users();
        public void EndIdentity() { IsCurrent = false; identity.Cancel(); }
        public void Dispose() => identity.Dispose();
    }
    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery; Paths.Add(path);
            object data = path.EndsWith("auth/login") ? new {
                access_token = "fixture-token", token_type = "Bearer", expires_at = "2099-01-01T00:00:00Z",
                user = new { id = 1, login_id = "fixture", display_name = "Fixture", employee_no = "TEST", department = "Test", role = "employee", is_active = true }
            } : path.StartsWith("/api/v1/tasks?") ? new { items = Array.Empty<object>() }
                : new { items = new[] { new { id = 2, display_name = "Fixture peer", department = "Test" } }, page = 1, limit = 100, total = 1 };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "fixture" }), Encoding.UTF8, "application/json") });
        }
    }
}
