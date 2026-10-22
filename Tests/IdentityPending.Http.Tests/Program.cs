using System.Net;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;

int assertions = 0;
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); assertions++; }
HttpResponseMessage Reply(object data, HttpStatusCode status = HttpStatusCode.OK) => new(status)
{ Content = new StringContent(JsonSerializer.Serialize(new { ok = (int)status < 300, data,
    error = new { code = "IDENTITY_WORKFLOW_NOT_READY", message = "test" } }), Encoding.UTF8, "application/json") };
object Pending() => new { pending_token = new string('p', 43), token_type = "IdentityPending",
    context_ref = new string('c', 43), expires_at = DateTimeOffset.UtcNow.AddMinutes(2), state = "fingerprint_required" };
async Task<IdentityPendingConnection> Login(Handler handler, Func<bool>? current = null, CancellationToken lifetime = default)
    => await IdentityPendingConnection.LoginAsync("http://127.0.0.1:5000", "synthetic", "public-test-password",
        Guid.Parse("30000000-0000-4000-8000-000000000001"), current ?? (() => true), lifetime, handler: handler);

// Exact opaque strings and pending-only Authorization; no normal client mutation.
var seen = new List<(string Path, string? Scheme, string Body)>();
var handler = new Handler(async (request, ct) =>
{
    var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
    seen.Add((request.RequestUri!.AbsolutePath, request.Headers.Authorization?.Scheme, body));
    if (request.RequestUri.AbsolutePath.EndsWith("/login")) return Reply(Pending());
    if (request.RequestUri.AbsolutePath.EndsWith("/approvals")) return Reply(new { approval_b64 = "e30" });
    if (request.RequestUri.AbsolutePath.EndsWith("/finalize")) return Reply(new { }, HttpStatusCode.ServiceUnavailable);
    return Reply(new { state = "verified", workflow_state = "pending" });
});
using (var normal = new ApiClient("http://127.0.0.1:5000"))
using (var pending = await Login(handler))
{
    Check(normal.CurrentUser is null);
    Check(pending.IsCurrent && !pending.Cancellation.IsCancellationRequested);
    var id = Guid.NewGuid();
    Check(await pending.ApproveAsync(id, "eyJ4IjoxfQ") == "e30");
    Check((await pending.CompleteAsync(id, "eyJ5IjoyfQ")).GetProperty("workflow_state").GetString() == "pending");
    Check(seen[0].Scheme is null && seen.Skip(1).All(x => x.Scheme == "IdentityPending"));
    Check(seen[1].Body.Contains("eyJ4IjoxfQ") && seen[2].Body.Contains("eyJ5IjoyfQ"));
    Check(seen[1].Path == "/api/v1/device-identity/challenges/" + id.ToString("D") + "/approvals");
    try { await pending.FinalizeAsync(id, Guid.NewGuid()); throw new Exception("Expected 503"); }
    catch (ApiException ex) { Check(ex.StatusCode == 503 && ex.Code == "IDENTITY_WORKFLOW_NOT_READY"); }
    Check(normal.CurrentUser is null && seen.Count == 4); // No automatic retries or token promotion.
}

// A malformed HTML401 still expires this context.
var unauthorized = new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/login")
    ? Reply(Pending()) : new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("<html>expired</html>") }));
using (var pending = await Login(unauthorized))
{
    try { await pending.GetAsync(Guid.NewGuid()); throw new Exception("Expected 401"); }
    catch (ApiException ex) { Check(ex.StatusCode == 401); }
    Check(!pending.IsCurrent && pending.Cancellation.IsCancellationRequested);
}

// The previous login generation cannot accept a late success even when the
// transport deliberately ignores cancellation.
bool current = true;
var waiting = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
var late = new Handler((request, _) => request.RequestUri!.AbsolutePath.EndsWith("/login")
    ? Task.FromResult(Reply(Pending())) : waiting.Task);
using (var pending = await Login(late, () => current))
{
    var work = pending.GetAsync(Guid.NewGuid());
    current = false;
    waiting.SetResult(Reply(new { state = "verified" }));
    try { await work; throw new Exception("Expected cancellation"); }
    catch (OperationCanceledException) { Check(!pending.IsCurrent); }
}

// Owner shutdown cancels in-flight IO; disposal has no server finalize side effect.
var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var blocked = new Handler(async (request, ct) =>
{
    if (request.RequestUri!.AbsolutePath.EndsWith("/login")) return Reply(Pending());
    entered.SetResult(); await Task.Delay(Timeout.Infinite, ct); return Reply(new { });
});
var owner = await Login(blocked);
var active = owner.GetAsync(Guid.NewGuid()); await entered.Task; owner.Dispose();
try { await active; throw new Exception("Expected cancellation"); }
catch (OperationCanceledException) { Check(owner.Cancellation.IsCancellationRequested); }
owner.Dispose();

// Unbounded or invalid success data cannot become a usable pending session.
var huge = new Handler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("/login")
    ? Reply(Pending()) : Reply(new { huge = new string('x', 65536) })));
using (var pending = await Login(huge))
{
    try { await pending.GetAsync(Guid.NewGuid()); throw new Exception("Expected bounded response failure"); }
    catch (ApiException) { Check(true); }
}
var promoted = new Handler((_, _) => Task.FromResult(Reply(new { pending_token = new string('p', 43),
    token_type = "IdentityPending", context_ref = new string('c', 43), expires_at = DateTimeOffset.UtcNow.AddMinutes(2),
    state = "fingerprint_required", access_token = "forbidden" })));
try { using var unused = await Login(promoted); throw new Exception("Expected token promotion rejection"); }
catch (ApiException) { Check(true); }

Console.WriteLine($"IdentityPending HTTP: {assertions} assertions PASS (controlled HTTP, no product server/DB/Pi)");

sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => send(request, cancellationToken);
}
