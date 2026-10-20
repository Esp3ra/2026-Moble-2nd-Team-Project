using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private sealed record Settings(string ServerUrl, string AdminPassword, string StaffPassword, string TaskId);
    private sealed record Candidate(int Id, string DisplayName, string SelectionLabel);
    private sealed record Context(string TaskId, int AssigneeId, int[] ManagerUserIds, string RowVersion,
        int AssignmentRevision, string TaskStatus, DateTimeOffset UpdatedAt);
    private sealed record ContextResult(Context Context);
    private sealed record KeyResult(string RequestKey, DateTimeOffset ExpiresAt);
    private sealed record Mutation(string TaskId, string RowVersion);
    private sealed record Stored(string State, int HttpStatus, Mutation Result, DateTimeOffset ExpiresAt);
    private static int checks;
    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        checks++;
    }

    private static async Task Main()
    {
        // Synthetic fixture credentials arrive via stdin, never argv or logs.
        var settings = JsonSerializer.Deserialize<Settings>(await Console.In.ReadToEndAsync(), ApiClient.Json)
            ?? throw new InvalidOperationException("Missing isolated fixture settings");
        using var api = new ApiClient(settings.ServerUrl);
        await api.LoginAsync("admin1", settings.AdminPassword);
        var adminSession = api.CaptureSession();
        var candidates = await api.GetAsync<PageResult<Candidate>>("tasks/assignment-candidates?limit=1");
        Check(candidates.Items.Count == 1 && candidates.Total >= 2, "Candidate paging over HTTP");
        Check(candidates.Items[0].SelectionLabel.Length > 0, "Server selection label");
        var managed = await api.GetAsync<ContextResult>($"tasks/{settings.TaskId}/write-context");
        Check(managed.Context.TaskId == settings.TaskId, "BIGINT context ID survives JSON/C#");
        await api.LoginAsync("staff1", settings.StaffPassword);
        Check(!adminSession.IsCurrent && adminSession.Cancellation.IsCancellationRequested,
            "Real login transition invalidates old feature session");
        var session = api.CaptureSession();
        Check(session.User.Id == api.CurrentUser!.Id && session.User.Role == "employee",
            "Feature session receives authenticated server identity");
        try
        {
            await api.GetAsync<PageResult<Candidate>>("tasks/assignment-candidates");
            throw new InvalidOperationException("Employee candidates were opened");
        }
        catch (ApiException error) { Check(error.StatusCode == 403, "Employee candidate access denied"); }
        var own = await session.GetAsync<ContextResult>($"tasks/{settings.TaskId}/write-context");
        Check(own.Context.RowVersion == "1" && own.Context.AssignmentRevision == 1, "Separate preparation versions");
        var scope = new { action = "task.accepted", target = settings.TaskId };
        var key = await session.PostAsync<KeyResult>("write-requests/keys", scope);
        Check(key.ExpiresAt > DateTimeOffset.UtcNow, "Server-issued key has an expiry");
        var body = new { expected_row_version = own.Context.RowVersion,
            expected_assignment_revision = own.Context.AssignmentRevision, title = "HTTP fixture changed" };
        var result = await session.PostIdempotentAsync<Mutation>("_write_support_probe", body, key.RequestKey);
        Check(result.TaskId == settings.TaskId && result.RowVersion == "2", "Actual HTTP fixture mutation committed");
        var repeated = await session.PostIdempotentAsync<Mutation>("_write_support_probe", body, key.RequestKey);
        Check(repeated == result, "Same key returns original result despite old expected version");
        try
        {
            await session.PostIdempotentAsync<Mutation>("_write_support_probe", new { title = "different" }, key.RequestKey);
            throw new InvalidOperationException("Different body reused key");
        }
        catch (ApiException error)
        {
            Check(error.StatusCode == 409 && error.Code == "IDEMPOTENCY_KEY_REUSED", "Changed body conflicts");
        }
        var stored = await session.PostIdempotentAsync<Stored>("write-requests/result", scope, key.RequestKey);
        Check(stored.State == "completed" && stored.HttpStatus == 200 && stored.Result == result,
            "Lost-response recovery reads original result");
        Check(stored.ExpiresAt > DateTimeOffset.UtcNow.AddHours(23), "Success result retained for 24 hours");
        var latest = await api.GetAsync<ContextResult>($"tasks/{settings.TaskId}/write-context");
        Check(latest.Context.RowVersion == "2" && latest.Context.TaskStatus == own.Context.TaskStatus,
            "Exactly one fixture version increment; no business state transition invented");
        await api.LogoutAsync();
        Check(!session.IsCurrent && session.Cancellation.IsCancellationRequested,
            "Real logout invalidates feature lifetime");
        await api.LoginAsync("admin1", settings.AdminPassword);
        try
        {
            await session.PostIdempotentAsync<Mutation>("_write_support_probe", body, key.RequestKey);
            throw new InvalidOperationException("Old feature request survived new login");
        }
        catch (OperationCanceledException) { checks++; }
        await api.LogoutAsync();
        Console.WriteLine($"PASS: {checks} assertions; real C# ApiClient -> HTTP -> isolated MySQL. Test-only mutation.");
    }
}
