using System.Net;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;

int checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
foreach (string mode in new[] { "open", "before_boundary", "noon", "end_before", "end_exact", "checked",
    "wrong_day", "wrong_id", "unknown", "bad_due", "outside_due", "early_due", "future", "missing", "offset" })
{
    using var handler = new Handler(mode);
    using var api = new ApiClient("http://127.0.0.1:1", handler);
    await api.LoginAsync("synthetic", "test-only");
    var source = new WorkdayStateConnection(api.CaptureSession());
    if (mode is "open" or "before_boundary" or "noon" or "end_before" or "end_exact" or "checked")
    {
        var state = await source.ReadAsync();
        Check(state.WorkSessionId == Guid.Parse(Handler.Id), mode + " server work ID");
        Check(state.NoonDue == (mode is "noon" or "end_before"), mode + " server noon status");
        Check(state.NoonAt.HasValue == (mode == "checked"), mode + " persisted noon");
        api.ClearIdentity();
        try { await source.ReadAsync(); throw new Exception("stale work read"); }
        catch (OperationCanceledException) { checks++; }
    }
    else
    {
        try { await source.ReadAsync(); throw new Exception("invalid work accepted: " + mode); }
        catch (InvalidDataException) { checks++; }
    }
    Check(handler.WorkReads == 1, mode + " no retries or writes");
}
foreach (int status in new[] { 401, 403, 404, 409, 503 })
{
    using var api = new ApiClient("http://127.0.0.1:1", new Handler("open", status));
    await api.LoginAsync("synthetic", "test-only");
    var source = new WorkdayStateConnection(api.CaptureSession());
    try { await source.ReadAsync(); throw new Exception("error became work"); }
    catch (ApiException error) { Check(error.StatusCode == status, "HTTP error remains distinct"); }
    Check(source.IsCurrent == (status != 401), "existing 401 expiry maintained");
}
Console.WriteLine($"PASS workday state: {checks} assertions, captured ApiClient with controlled HTTP; no live server/DB.");

sealed class Handler(string mode, int status = 200) : HttpMessageHandler
{
    public const string Id = "10000000-0000-4000-8000-000000000001";
    public int WorkReads;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        object data;
        if (request.RequestUri!.AbsolutePath.EndsWith("auth/login"))
            data = new { access_token = "test-only", user = new { id = 2, login_id = "synthetic", display_name = "TEST", department = "TEST", role = "employee", is_active = true } };
        else
        {
            if (request.Method != HttpMethod.Get || request.RequestUri.AbsolutePath != "/api/v1/device-identity/workday") throw new Exception("Unexpected call");
            WorkReads++;
            if (status != 200) return Task.FromResult(Reply(status, new { ok = false, error = new { code = "TEST", message = "synthetic error" } }));
            var value = new Dictionary<string, object?> {
                ["server_time"] = mode switch {
                    "before_boundary" or "early_due" => "2026-10-22T01:59:59Z",
                    "noon" or "bad_due" => "2026-10-22T02:00:00Z",
                    "end_before" => "2026-10-22T02:59:59Z",
                    "checked" or "end_exact" or "outside_due" => "2026-10-22T03:00:00Z",
                    _ => "2026-10-22T01:00:00Z" },
                ["work_session_ref"] = mode == "wrong_id" ? Guid.Empty.ToString() : Id,
                ["work_date"] = mode == "wrong_day" ? "2026-10-21" : "2026-10-22",
                ["state"] = mode == "unknown" ? "unknown" : "open",
                ["started_at"] = mode == "future" ? "2026-10-22T04:00:00Z" : mode == "offset" ? "2026-10-22T09:00:00+09:00" : "2026-10-22T00:00:00Z",
                ["noon_at"] = mode == "checked" ? "2026-10-22T02:30:00Z" : null,
                ["noon_due"] = mode is "noon" or "end_before" or "outside_due" or "early_due" };
            if (mode == "missing") value.Remove("noon_at");
            data = value;
        }
        return Task.FromResult(Reply(200, new { ok = true, data }));
    }
    static HttpResponseMessage Reply(int status, object data) => new((HttpStatusCode)status)
        { Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json") };
}
