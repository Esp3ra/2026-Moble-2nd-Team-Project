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

// R62-02: commit succeeded but its first reply was lost. Advance time, not wall-clock sleep.
var clock = new ManualClock(DateTimeOffset.UtcNow);
var challenge = Guid.NewGuid();
var finalizeId = Guid.NewGuid();
var finalizeBodies = new List<string>();
int recoveryCalls = 0;
bool recoveryCurrent = true;
using var recoveryLifetime = new CancellationTokenSource();
var recoveryHandler = new Handler(async (request, ct) =>
{
    recoveryCalls++;
    if (request.RequestUri!.AbsolutePath.EndsWith("/login"))
        return Reply(new { pending_token = new string('p', 43), token_type = "IdentityPending",
            context_ref = new string('c', 43), expires_at = clock.GetUtcNow().AddMinutes(5), state = "fingerprint_required" });
    Check(request.Headers.Authorization?.Scheme == "IdentityPending");
    if (request.Method == HttpMethod.Get)
        return Reply(new { state = "verified", workflow_state = "completed" });
    finalizeBodies.Add(await request.Content!.ReadAsStringAsync(ct));
    if (finalizeBodies.Count == 1) throw new HttpRequestException("Synthetic reply loss after commit");
    return Reply(new { state = "completed", workflow_receipt_id = "original-receipt", access_token = "synthetic-token" });
});
using (var recovery = await IdentityPendingConnection.LoginAsync("http://127.0.0.1:5000", "synthetic",
    "public-test-password", Guid.NewGuid(), () => recoveryCurrent, recoveryLifetime.Token,
    handler: recoveryHandler, timeProvider: clock))
{
    try { await recovery.FinalizeAsync(challenge, finalizeId); throw new Exception("Expected lost reply"); }
    catch (HttpRequestException) { Check(finalizeBodies.Count == 1); }
    clock.Advance(TimeSpan.FromMinutes(6));
    Check(!recovery.IsCurrent); // New activity remains expired.
    Check((await recovery.GetAsync(challenge)).GetProperty("workflow_state").GetString() == "completed");
    var result = await recovery.FinalizeAsync(challenge, finalizeId);
    Check(result.GetProperty("workflow_receipt_id").GetString() == "original-receipt");
    Check(finalizeBodies.Count == 2 && finalizeBodies[0] == finalizeBodies[1]);
    Check(JsonDocument.Parse(finalizeBodies[1]).RootElement.GetProperty("client_request_id").GetString() == finalizeId.ToString("D"));
    int before = recoveryCalls;
    foreach (var action in new Func<Task>[] {
        async () => { await recovery.IssueAsync(Guid.NewGuid(), Guid.NewGuid()); },
        async () => { await recovery.ApproveAsync(challenge, "e30"); },
        async () => { await recovery.CompleteAsync(challenge, "e30"); },
        async () => { await recovery.CancelAsync(challenge, Guid.NewGuid()); },
        async () => { await recovery.LookupRequestAsync(Guid.NewGuid()); },
        async () => { await recovery.GetAsync(Guid.NewGuid()); },
        async () => { await recovery.FinalizeAsync(challenge, Guid.NewGuid()); },
        async () => { await recovery.FinalizeAsync(Guid.NewGuid(), finalizeId); } })
    {
        try { await action(); throw new Exception("Expired activity must not send"); }
        catch (OperationCanceledException) { Check(recoveryCalls == before); }
    }
    recoveryCurrent = false;
    try { await recovery.GetAsync(challenge); throw new Exception("Changed login must not recover"); }
    catch (OperationCanceledException) { Check(recoveryCalls == before); }
    recoveryCurrent = true;
    using (var cancelled = new CancellationTokenSource())
    {
        cancelled.Cancel();
        try { await recovery.FinalizeAsync(challenge, finalizeId, cancelled.Token); throw new Exception("Expected cancellation"); }
        catch (OperationCanceledException) { Check(recoveryCalls == before); }
    }
    clock.Advance(TimeSpan.FromHours(23));
    await recovery.FinalizeAsync(challenge, finalizeId);
    before = recoveryCalls;
    clock.Advance(TimeSpan.FromHours(1)); // Replays never renew the first attempt's 24-hour ceiling.
    try { await recovery.GetAsync(challenge); throw new Exception("Recovery must expire"); }
    catch (OperationCanceledException) { Check(recoveryCalls == before); }
    try { await recovery.FinalizeAsync(challenge, finalizeId); throw new Exception("Recovery must expire"); }
    catch (OperationCanceledException) { Check(recoveryCalls == before); }
}

// A post-expiry recovery still honors late-login rejection and server-side 401 revocation.
foreach (string end in new[] { "generation", "revoked", "owner" })
{
    using var ownerLifetime = new CancellationTokenSource();
    var recoveryClock = new ManualClock(DateTimeOffset.UtcNow);
    bool generation = true;
    int calls = 0;
    var responseGate = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
    var recoveryTransport = new Handler((request, _) =>
    {
        calls++;
        if (calls == 1) return Task.FromResult(Reply(new { pending_token = new string('p', 43), token_type = "IdentityPending",
            context_ref = new string('c', 43), expires_at = recoveryClock.GetUtcNow().AddMinutes(5), state = "fingerprint_required" }));
        if (calls == 2) return Task.FromException<HttpResponseMessage>(new HttpRequestException("Lost finalize reply"));
        return responseGate.Task;
    });
    using var pending = await IdentityPendingConnection.LoginAsync("http://127.0.0.1:5000", "synthetic",
        "public-test-password", Guid.NewGuid(), () => generation, ownerLifetime.Token,
        handler: recoveryTransport, timeProvider: recoveryClock);
    var cid = Guid.NewGuid(); var fid = Guid.NewGuid();
    try { await pending.FinalizeAsync(cid, fid); } catch (HttpRequestException) { }
    recoveryClock.Advance(TimeSpan.FromMinutes(6));
    var retry = pending.FinalizeAsync(cid, fid);
    if (end == "revoked")
    {
        responseGate.SetResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        try { await retry; throw new Exception("Expected server revocation"); }
        catch (ApiException ex) { Check(ex.StatusCode == 401 && pending.Cancellation.IsCancellationRequested); }
    }
    else
    {
        if (end == "owner") ownerLifetime.Cancel();
        else generation = false;
        responseGate.SetResult(Reply(new { state = "completed" }));
        try { await retry; throw new Exception("Expected late generation rejection"); }
        catch (OperationCanceledException) { Check(!pending.IsCurrent); }
    }
    Check(calls == 3);
}

Console.WriteLine($"IdentityPending HTTP: {assertions} assertions PASS (controlled HTTP, no product server/DB/Pi)");

sealed class ManualClock(DateTimeOffset initial) : TimeProvider
{
    private DateTimeOffset utc = initial;
    public override DateTimeOffset GetUtcNow() => utc;
    public void Advance(TimeSpan elapsed) => utc += elapsed;
}

sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => send(request, cancellationToken);
}
