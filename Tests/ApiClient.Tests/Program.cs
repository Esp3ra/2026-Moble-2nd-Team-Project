using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException(name);
        checks++;
    }

    private static async Task Main(string[] args)
    {
        var scenarios = new List<(string Name, Func<Task> Run)>();
        foreach (var body in new[] { "success", "json", "html", "malformed", "empty" })
        {
            var selected = body;
            scenarios.Add(("old-session-" + selected, () => OldSessionResponse(selected)));
        }
        foreach (var body in new[] { "json", "html", "malformed", "empty" })
        {
            var selected = body;
            scenarios.Add(("current-401-" + selected, () => CurrentUnauthorized(selected)));
        }
        scenarios.Add(("current-refresh", CurrentRefresh));
        scenarios.Add(("concurrent-401", ConcurrentUnauthorized));
        scenarios.Add(("late-logout", LateLogout));
        scenarios.Add(("overlapping-logins", OverlappingLogins));
        scenarios.Add(("same-user-relogin", SameUserRelogin));
        scenarios.Add(("non-401-errors", NonAuthenticationErrors));
        scenarios.Add(("caller-cancellation", CallerCancellation));
        scenarios.Add(("server-change-cancels-login", ServerChange));
        scenarios.Add(("repeated-lifecycle", RepeatedLifecycle));
        var selectedScenarios = scenarios.Where(x => args.Length == 0 || x.Name == args[0]).ToList();
        Check(selectedScenarios.Count > 0, "Unknown scenario");
        foreach (var scenario in selectedScenarios)
        {
            await scenario.Run().WaitAsync(TimeSpan.FromSeconds(10));
            Console.WriteLine("PASS " + scenario.Name);
        }
        Console.WriteLine($"PASS: {checks} assertions; {selectedScenarios.Count} scenarios. Actual ApiClient with controlled HTTP responses.");
    }

    private static UserInfo User(string account) => new(account == "A" ? 1 : 2, account,
        account, "test-" + account, "test", "employee", true);
    private static HttpResponseMessage JsonResponse(object data) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "test" }, ApiClient.Json), Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Unauthorized(string kind) => new(HttpStatusCode.Unauthorized)
    {
        Content = new StringContent(kind switch
        {
            "json" => "{\"ok\":false,\"error\":{\"code\":\"AUTH_REQUIRED\",\"message\":\"login required\"}}",
            "html" => "<html>sign in</html>",
            "malformed" => "{broken",
            _ => ""
        })
    };
    private static async Task Canceled(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { checks++; return; }
        throw new InvalidOperationException("Superseded/canceled request was accepted");
    }
    private static async Task ApiError(Task task, int status)
    {
        try { await task; }
        catch (ApiException error) { Check(error.StatusCode == status, "HTTP status preserved"); return; }
        throw new InvalidOperationException("Expected ApiException");
    }
    private static async Task IsB(ApiClient api, Handler handler, int expectedEvents, Func<int> events)
    {
        Check(api.CurrentUser?.LoginId == "B", "B user preserved");
        await api.GetAsync<HealthResult>("health");
        Check(handler.LastBearer == "token-B", "B bearer preserved");
        Check(events() == expectedEvents, "No obsolete expiry event");
    }

    private static async Task OldSessionResponse(string body)
    {
        var handler = new Handler();
        using var api = new ApiClient("http://localhost:5000", handler);
        int events = 0;
        api.AuthenticationExpired += (_, _) => events++;
        await api.LoginAsync("A", "test-password");
        var old = api.RefreshIdentityAsync();
        var pending = await handler.Next();
        await api.LogoutAsync();
        await api.LoginAsync("B", "test-password");
        pending.Complete(body == "success" ? JsonResponse(new IdentityResult(User("A"), DateTimeOffset.UtcNow.AddHours(1))) : Unauthorized(body));
        Exception? failure = null;
        try { await old; }
        catch (Exception error) { failure = error; }
        await IsB(api, handler, 0, () => events);
        Check(pending.Cancellation.IsCancellationRequested, "Previous session cancellation requested");
        Check(failure is OperationCanceledException, "Obsolete result is not returned to the former screen");
    }

    private static async Task CurrentUnauthorized(string body)
    {
        var handler = new Handler();
        using var api = new ApiClient("http://localhost:5000", handler);
        int events = 0;
        api.AuthenticationExpired += (_, _) => events++;
        await api.LoginAsync("B", "test-password");
        var request = api.RefreshIdentityAsync();
        (await handler.Next()).Complete(Unauthorized(body));
        await ApiError(request, 401);
        Check(api.CurrentUser is null, "Current user removed for any 401 body");
        Check(events == 1, "Expiry notified once");
        await api.GetAsync<HealthResult>("health");
        Check(handler.LastBearer is null, "Expired bearer omitted");
    }

    private static async Task CurrentRefresh()
    {
        var handler = new Handler();
        using var api = new ApiClient("http://localhost:5000", handler);
        await api.LoginAsync("B", "test-password");
        var request = api.RefreshIdentityAsync();
        var changed = User("B") with { DisplayName = "updated" };
        (await handler.Next()).Complete(JsonResponse(new IdentityResult(changed, DateTimeOffset.UtcNow.AddHours(1))));
        await request;
        Check(api.CurrentUser == changed, "Current refresh applied");
        await api.GetAsync<HealthResult>("health");
        Check(handler.LastBearer == "token-B", "Current refresh keeps bearer");
    }

    private static async Task ConcurrentUnauthorized()
    {
        var handler = new Handler();
        using var api = new ApiClient("http://localhost:5000", handler);
        int events = 0;
        api.AuthenticationExpired += (_, _) => events++;
        await api.LoginAsync("B", "test-password");
        var first = api.RefreshIdentityAsync();
        var pendingFirst = await handler.Next();
        var second = api.RefreshIdentityAsync();
        var pendingSecond = await handler.Next();
        pendingFirst.Complete(Unauthorized("html"));
        await ApiError(first, 401);
        pendingSecond.Complete(Unauthorized("json"));
        await Canceled(second);
        Check(events == 1 && api.CurrentUser is null, "Parallel 401s cause one expiry");
    }

    private static async Task LateLogout()
    {
        var handler = new Handler { DelayLogout = true };
        using var api = new ApiClient("http://localhost:5000", handler);
        await api.LoginAsync("A", "test-password");
        var logout = api.LogoutAsync();
        var pending = await handler.Next();
        Check(api.CurrentUser is null, "Logout clears local state immediately");
        Check(pending.Bearer == "token-A", "Logout uses the departing token");
        await api.LoginAsync("B", "test-password");
        pending.Complete(JsonResponse(new MessageResult("logged out")));
        await logout;
        await IsB(api, handler, 0, () => 0);
    }

    private static async Task OverlappingLogins()
    {
        var handler = new Handler { DelayLoginA = true };
        using var api = new ApiClient("http://localhost:5000", handler);
        var oldLogin = api.LoginAsync("A", "test-password");
        var pending = await handler.Next();
        await api.LoginAsync("B", "test-password");
        pending.Complete(JsonResponse(new LoginResult("token-A", "Bearer", DateTimeOffset.UtcNow.AddHours(1), User("A"))));
        await Canceled(oldLogin);
        await IsB(api, handler, 0, () => 0);
    }

    private static async Task SameUserRelogin()
    {
        var handler = new Handler();
        using var api = new ApiClient("http://localhost:5000", handler);
        await api.LoginAsync("A", "test-password");
        var old = api.RefreshIdentityAsync();
        var pending = await handler.Next();
        api.ClearIdentity();
        await api.LoginAsync("A", "test-password");
        pending.Complete(Unauthorized("json"));
        await Canceled(old);
        Check(api.CurrentUser?.LoginId == "A", "Generation protects relogin even with equal test tokens");
    }

    private static async Task NonAuthenticationErrors()
    {
        var handler = new Handler();
        using var api = new ApiClient("http://localhost:5000", handler);
        int events = 0;
        api.AuthenticationExpired += (_, _) => events++;
        await api.LoginAsync("B", "test-password");
        foreach (var status in new[] { 500, 502, 200 })
        {
            var request = api.RefreshIdentityAsync();
            (await handler.Next()).Complete(new HttpResponseMessage((HttpStatusCode)status)
                { Content = new StringContent("<html>invalid response</html>") });
            await ApiError(request, status);
            await IsB(api, handler, 0, () => events);
        }
    }

    private static async Task CallerCancellation()
    {
        var handler = new Handler();
        using var api = new ApiClient("http://localhost:5000", handler);
        using var cancellation = new CancellationTokenSource();
        await api.LoginAsync("B", "test-password");
        var request = api.RefreshIdentityAsync(cancellation.Token);
        var pending = await handler.Next();
        cancellation.Cancel();
        pending.Complete(JsonResponse(new IdentityResult(User("B"), DateTimeOffset.UtcNow.AddHours(1))));
        await Canceled(request);
        await IsB(api, handler, 0, () => 0);
    }

    private static async Task ServerChange()
    {
        var handler = new Handler { DelayLoginA = true };
        using var api = new ApiClient("http://localhost:5000", handler);
        var login = api.LoginAsync("A", "test-password");
        var pending = await handler.Next();
        api.SetServer("http://localhost:5001");
        pending.Complete(JsonResponse(new LoginResult("token-A", "Bearer", DateTimeOffset.UtcNow.AddHours(1), User("A"))));
        await Canceled(login);
        Check(api.CurrentUser is null && api.ServerUri.Port == 5001, "Server change invalidates pending login");
    }

    private static async Task RepeatedLifecycle()
    {
        var handler = new Handler();
        using var api = new ApiClient("http://localhost:5000", handler);
        for (int i = 0; i < 6; i++)
        {
            await api.LoginAsync(i % 2 == 0 ? "A" : "B", "test-password");
            var expected = api.CurrentUser!.LoginId;
            await api.GetAsync<HealthResult>("health");
            Check(handler.LastBearer == "token-" + expected, "User/bearer agree through repeated sign-in");
            await api.LogoutAsync();
            Check(api.CurrentUser is null, "Each sign-out clears identity");
        }
    }

    private sealed record Pending(string? Bearer, CancellationToken Cancellation)
    {
        public TaskCompletionSource<HttpResponseMessage> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Complete(HttpResponseMessage response) => Response.SetResult(response);
    }

    private sealed class Handler : HttpMessageHandler
    {
        private readonly Channel<Pending> requests = Channel.CreateUnbounded<Pending>();
        public bool DelayLogout { get; init; }
        public bool DelayLoginA { get; init; }
        public string? LastBearer { get; private set; }
        public Task<Pending> Next() => requests.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastBearer = request.Headers.Authorization?.Parameter;
            var route = request.RequestUri!.AbsolutePath;
            string? account = null;
            if (route.EndsWith("auth/login"))
            {
                using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
                account = json.RootElement.GetProperty("login_id").GetString();
                if (!(account == "A" && DelayLoginA))
                    return JsonResponse(new LoginResult("token-" + account, "Bearer", DateTimeOffset.UtcNow.AddHours(1), User(account!)));
            }
            if (route.EndsWith("auth/me") || account == "A" && DelayLoginA || route.EndsWith("auth/logout") && DelayLogout)
            {
                var pending = new Pending(LastBearer, cancellationToken);
                await requests.Writer.WriteAsync(pending);
                // Intentionally ignore cancellation: late transports must still be harmless.
                return await pending.Response.Task;
            }
            if (route.EndsWith("auth/logout")) return JsonResponse(new MessageResult("logged out"));
            return JsonResponse(new HealthResult("ok", "connected", 1, "test"));
        }
    }
}
