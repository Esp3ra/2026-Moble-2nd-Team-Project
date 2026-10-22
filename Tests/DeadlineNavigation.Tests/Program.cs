using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.DeadlineAlerts;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

internal static class Program
{
    private static int checks;
    private const string Big = "9223372036854775807";
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize(); Control.CheckForIllegalCrossThreadCalls = true;
        Exception? failure = null;
        using var coordinator = new Form { ShowInTaskbar = false, Opacity = 0 };
        coordinator.Shown += async (_, _) =>
        {
            try { await Run(); } catch (Exception ex) { failure = ex; } finally { coordinator.Close(); }
        };
        Application.Run(coordinator);
        if (failure is not null) { Console.Error.WriteLine(failure); Environment.ExitCode = 1; }
        else Console.WriteLine($"PASS: {checks} MainForm/deadline navigation checks; synthetic loopback HTTP/source, no product server, DB or hardware.");
    }

    private static async Task Run()
    {
        using var server = new Server();
        var api = AppServices.Api;
        var previous = api.ServerUri.ToString();
        api.SetServer(server.Url);
        try
        {
            var login = await api.LoginAsync("synthetic", "synthetic");
            using var main = new MainForm(login.User) { ShowInTaskbar = false, Location = new Point(-20000, -20000), StartPosition = FormStartPosition.Manual };
            main.Show();
            var source = new Source();
            ApiClient.Session? supplied = null;
            await main.ConnectDeadlineAlertsAsync(session => { supplied = session; return source; });
            var host = Get<LoginDeadlineNotifications>(main, "deadlineNotifications");
            Check(supplied is { IsCurrent: true } && supplied.User.Id == 1, "factory gets original authenticated session");
            Check(host.Scope.Snapshot?.Items.Count == 2, "initial full snapshot preserves both task reminders");
            Check(DeadlineAlertScope.Find(main) == host.Scope, "one login scope belongs to actual MainForm");
            try { await main.ConnectDeadlineAlertsAsync(_ => new Source()); throw new Exception("duplicate allowed"); }
            catch (InvalidOperationException) { checks++; }
            Check(await host.Scope.OpenTaskAsync("event-big"), "scope dispatches a rechecked task request");
            await Until(() => host.LastNavigationSucceeded is not null);
            Check(host.LastNavigationSucceeded == true, "navigator, not synchronous event, confirms displayed detail");
            var page = Get<TasksControl>(main, "currentPage");
            Check(Selected(page) == Big, "actual MainForm selects canonical BIGINT task without rounding");
            Check(host.Scope.Snapshot?.Items.Count == 2 && source.Confirms == 0, "navigation neither truncates reminders nor confirms");
            Check(server.SeenPaths.Any(p => p == "/api/v1/tasks?scope=own"), "navigation reads fresh own/all/noncancelled list");
            Check(server.SeenPaths.Any(p => p == "/api/v1/tasks/" + Big), "actual detail GET uses exact task ID");

            source.Items = [Entry("missing", "2")];
            await host.Scope.CheckAsync();
            await host.Scope.OpenTaskAsync("missing");
            await Until(() => host.LastNavigationSucceeded is not null);
            Check(host.LastNavigationSucceeded == false, "absent own task is not falsely reported as opened");
            Check(Selected(page) is null, "missing requested task never falls back to first or managed row");

            source.Items = [Entry("event-big", Big)];
            await host.Scope.CheckAsync();
            server.FailLists = true;
            await host.Scope.OpenTaskAsync("event-big");
            await Until(() => host.LastNavigationSucceeded is not null);
            Check(host.LastNavigationSucceeded == false && source.Confirms == 0, "503 navigation does not grant access or confirm");
            server.FailLists = false;
            Check(server.SeenPaths.All(p => !p.Contains("deadline-extensions") && !p.Contains("/confirm")), "navigation sends no task mutation or confirmation HTTP");

            var held = new TaskCompletionSource<DeadlineAlertSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.Recheck = () => held.Task;
            var late = host.Scope.OpenTaskAsync("event-big");
            Check(!late.IsCompleted, "source recheck is genuinely awaiting a delayed result");
            var old = supplied!;
            await api.LoginAsync("synthetic", "synthetic");
            held.SetResult(new(DateTimeOffset.UtcNow, source.Items));
            try { await late; throw new Exception("old session response accepted"); }
            catch (OperationCanceledException) { checks++; }
            await Until(() => !host.Scope.IsCurrent);
            Check(!old.IsCurrent && host.LastNavigationSucceeded is null, "same-account relogin stops old scope and result status");
            Check(api.CurrentUser?.Id == 1, "old scope cleanup never clears new login");
            main.Dispose();

            using var owner = new Form { ShowInTaskbar = false, Opacity = 0 }; owner.Show();
            var current = api.CaptureSession();
            try { using var rejected = new LoginDeadlineNotifications(owner, current, new Source { UserId = 2 }, () => _ => Task.FromResult(true)); throw new Exception("foreign source allowed"); }
            catch (ArgumentException) { checks++; }
            var navigation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var closing = new LoginDeadlineNotifications(owner, current, new Source(), () => _ => navigation.Task);
            await closing.StartAsync();
            await closing.Scope.OpenTaskAsync("event-big");
            Check(closing.LastNavigationSucceeded is null, "synchronous dispatch is not asynchronous display completion");
            owner.Close(); navigation.SetResult(true); await Task.Yield();
            Check(!closing.Scope.IsCurrent && closing.LastNavigationSucceeded is null, "owner closing detaches event and discards late navigator success");
            Check(api.CaptureSession().IsCurrent, "owner/component disposal borrows rather than disposes ApiClient");

            using var preview = new MainForm(); preview.Show();
            bool created = false;
            try { await preview.ConnectDeadlineAlertsAsync(_ => { created = true; return new Source(); }); throw new Exception("preview allowed"); }
            catch (InvalidOperationException) { checks++; }
            Check(!created, "preview never instantiates a source or makes deadline requests");
            preview.Dispose();
        }
        finally { api.ClearIdentity(); api.SetServer(previous); }
    }

    private static T Get<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static string? Selected(TasksControl page) => Get<DataGridView>(page, "gridTasks").CurrentRow?.Tag is TaskSummary item ? item.TaskId : null;
    private static async Task Until(Func<bool> condition)
    {
        var limit = DateTime.UtcNow.AddSeconds(10);
        while (!condition()) { if (DateTime.UtcNow >= limit) throw new TimeoutException(); await Task.Delay(5); }
    }
    private static DeadlineAlert Entry(string token, string task) => new(token, task, "1", 1, 1, 1, "Synthetic task", new DateOnly(2026, 1, 1), TaskDisplayStatus.InProgress);
    private sealed class Source : IDeadlineAlertSource
    {
        public int UserId { get; init; } = 1;
        public bool IsCurrent => true; // Intentionally ignores login: the common wrapper must still reject late results.
        public CancellationToken Cancellation => CancellationToken.None;
        public IReadOnlyList<DeadlineAlert> Items = [Entry("event-one", "1"), Entry("event-big", Big)];
        public Func<Task<DeadlineAlertSnapshot>>? Recheck;
        public int Confirms;
        public Task<DeadlineAlertSnapshot> ReadAsync(CancellationToken ct) => Task.FromResult(new DeadlineAlertSnapshot(new(2026, 10, 21, 9, 0, 0, TimeSpan.Zero), Items));
        public Task<DeadlineAlertSnapshot> RecheckAsync(string token, CancellationToken ct) => Recheck?.Invoke() ?? ReadAsync(ct);
        public Task<DeadlineConfirmation> ConfirmAsync(string token, CancellationToken ct) { Confirms++; throw new Exception("Navigation must not confirm"); }
        public Task<DeadlineConfirmation> ResolveConfirmationAsync(string token, CancellationToken ct) => throw new Exception("Navigation must not resolve writes");
    }
    private sealed class Server : IDisposable
    {
        private readonly HttpListener listener = new();
        private readonly Task serving;
        private int login;
        private readonly List<string> Paths = [];
        public string[] SeenPaths { get { lock (Paths) return Paths.ToArray(); } }
        public volatile bool FailLists;
        public string Url { get; }
        public Server()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); int port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
            Url = $"http://127.0.0.1:{port}/"; listener.Prefixes.Add(Url); listener.Start(); serving = Serve();
        }
        private async Task Serve()
        {
            while (listener.IsListening)
            {
                HttpListenerContext c;
                try { c = await listener.GetContextAsync().ConfigureAwait(false); } catch (HttpListenerException) { break; } catch (ObjectDisposedException) { break; }
                string path = c.Request.Url!.PathAndQuery; lock (Paths) Paths.Add(path);
                object data;
                if (path == "/api/v1/auth/login") data = new LoginResult("synthetic-" + ++login, "Bearer", DateTimeOffset.UtcNow.AddHours(1), new UserInfo(1,"synthetic","합성 직원","S1","시험","employee",true));
                else if (path.StartsWith("/api/v1/tasks?")) data = new { items = new[] { Item("1"), Item(Big) } };
                else if (path.StartsWith("/api/v1/tasks/")) data = new { task = Item(c.Request.Url.AbsolutePath.Split('/').Last()) };
                else data = new { items = Array.Empty<object>(), next_after_id = (int?)null };
                bool fail = FailLists && path.StartsWith("/api/v1/tasks?"); c.Response.StatusCode = fail ? 503 : 200;
                var bytes = JsonSerializer.SerializeToUtf8Bytes(fail
                    ? (object)new { ok = false, error = new { code = "DATABASE_UNAVAILABLE", message = "Synthetic unavailable" }, request_id = "synthetic" }
                    : new { ok = true, data, request_id = "synthetic" }, ApiClient.Json);
                c.Response.ContentType = "application/json"; c.Response.ContentLength64 = bytes.Length;
                await c.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false); c.Response.Close();
            }
        }
        private static object Item(string id) => new { task_id = id, title = "Synthetic task " + id, description = "Synthetic", assignee_id = 1, assignee_name = "Synthetic", task_status = "in_progress", start_date = "2026-01-01", due_date = "2026-01-01" };
        public void Dispose() { listener.Close(); serving.GetAwaiter().GetResult(); }
    }
}
