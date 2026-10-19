using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string name)
    { if (!condition) throw new InvalidOperationException(name); checks++; }

    private static async Task Main()
    {
        PublicVectors();
        var queue = new Queue<Action>();
        bool alive = true;
        int applied = 0;
        using var api = new ApiClient("http://127.0.0.1:5000", new FakeHttp());
        await api.LoginAsync("staff1", "synthetic-fixture-password");
        using var scope = new FeatureUiScope(api.CaptureSession(), queue.Enqueue, () => alive);
        var first = scope.BeginOperation();
        Check(scope.TryPost(first, () => applied++), "current update queued");
        queue.Dequeue()(); Check(applied == 1, "current update applied");
        Check(scope.TryPost(first, () => applied++), "old selection queued");
        var second = scope.BeginOperation();
        Check(first.Cancellation.IsCancellationRequested && !first.IsCurrent, "prior selection cancelled");
        queue.Dequeue()(); Check(applied == 1, "queued old selection suppressed");
        Check(!scope.TryPost(first, () => applied++), "old selection not queued again");
        Check(scope.TryPost(second, () => applied++), "next update queued");
        alive = false; queue.Dequeue()(); Check(applied == 1, "disposed view callback suppressed");
        alive = true;
        await api.RefreshIdentityAsync();
        Check(second.IsCurrent, "same-session identity refresh preserved");
        Check(scope.TryPost(second, () => applied++), "update queued before same-user relogin");
        await api.LoginAsync("staff1", "synthetic-fixture-password");
        queue.Dequeue()(); Check(applied == 1 && !second.IsCurrent, "same-user relogin suppresses old callback");
        Check(second.Cancellation.IsCancellationRequested, "session cancellation linked");

        using var next = new FeatureUiScope(api.CaptureSession(), queue.Enqueue, () => true);
        var current = next.BeginOperation();
        Check(!next.TryPost(second, () => applied++), "operation from another scope rejected");
        Check(next.TryPost(current, () => applied++), "update queued before logout");
        await api.LogoutAsync();
        queue.Dequeue()(); Check(applied == 1 && !current.IsCurrent, "logout suppresses callback");

        await api.LoginAsync("admin", "synthetic-fixture-password");
        var disposable = new FeatureUiScope(api.CaptureSession(), queue.Enqueue, () => true);
        var closing = disposable.BeginOperation();
        Check(disposable.TryPost(closing, () => applied++), "update queued before scope disposal");
        disposable.Dispose(); disposable.Dispose();
        queue.Dequeue()(); Check(applied == 1 && closing.Cancellation.IsCancellationRequested, "scope dispose suppresses callback");
        Check(!disposable.TryPost(closing, () => applied++), "disposed scope refuses updates");
        using var brokenPost = new FeatureUiScope(api.CaptureSession(), _ => throw new ObjectDisposedException("view"), () => true);
        Check(!brokenPost.TryPost(brokenPost.BeginOperation(), () => applied++), "destroyed dispatcher handled");
        using var inline = new FeatureUiScope(api.CaptureSession(), action => action(), () => true);
        bool propagated = false;
        try { inline.TryPost(inline.BeginOperation(), () => throw new InvalidOperationException("update bug")); }
        catch (InvalidOperationException) { propagated = true; }
        Check(propagated, "user-update exception not hidden as destroyed dispatcher");
        for (int i = 0; i < 1000; i++)
        {
            var old = inline.BeginOperation();
            inline.BeginOperation();
            if (old.IsCurrent || !old.Cancellation.IsCancellationRequested) throw new InvalidOperationException("generation leak");
        }
        Check(true, "1000 generations release previous cancellation sources");
        using var expiry = new FeatureUiScope(api.CaptureSession(), queue.Enqueue, () => true);
        var expiring = expiry.BeginOperation();
        expiry.TryPost(expiring, () => applied++);
        try { await api.GetAsync<object>("fixture-expired"); } catch (ApiException ex) { Check(ex.StatusCode == 401, "synthetic expiry"); }
        queue.Dequeue()(); Check(applied == 1 && expiring.Cancellation.IsCancellationRequested, "401 suppresses queued callback");
        Console.WriteLine($"Integration support: {checks} checks passed (synthetic HTTP/UI queue; no devices/DB).");
    }

    private static void PublicVectors()
    {
        // Independent test-only MAC verifier. NEVER ship this public key in the PC relay.
        using var fixture = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "device_protocol_draft.json")));
        var key = Convert.FromHexString(fixture.RootElement.GetProperty("public_test_key_hex").GetString()!);
        foreach (var v in fixture.RootElement.GetProperty("vectors").EnumerateArray())
        {
            string Field(string name) => v.GetProperty(name).GetString()!;
            static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var input = Encoding.ASCII.GetBytes("RW-ID-v1\0" + Field("kind") + "\0" + Field("device_id") + "\0" + Field("key_id") + "\0" + Field("payload_b64"));
            Check(B64(Encoding.UTF8.GetBytes(Field("payload_json"))) == Field("payload_b64"), "public UTF8 bytes");
            Check(Convert.ToHexString(input).ToLowerInvariant() == Field("mac_input_hex"), "public MAC input");
            Check(B64(HMACSHA256.HashData(key, input)) == Field("mac_b64"), "public MAC value");
            var changed = input.ToArray(); changed[^1] ^= 1;
            Check(!CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(key, input), HMACSHA256.HashData(key, changed)), "tampering changes MAC");
        }
    }

    private sealed class FakeHttp : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            bool expired = request.RequestUri!.AbsolutePath.EndsWith("fixture-expired", StringComparison.Ordinal);
            var user = new UserInfo(2, "staff1", "가상 직원", "TEST-ONLY", "개발팀", "employee", true);
            object response = expired ? new { ok = false, error = new { code = "AUTH_REQUIRED", message = "fixture" } }
                : request.RequestUri.AbsolutePath.EndsWith("auth/login", StringComparison.Ordinal)
                    ? new { ok = true, data = (object)new LoginResult("synthetic-test-token", "Bearer", DateTimeOffset.UtcNow.AddHours(1), user) }
                    : request.RequestUri.AbsolutePath.EndsWith("auth/me", StringComparison.Ordinal)
                        ? new { ok = true, data = (object)new IdentityResult(user, DateTimeOffset.UtcNow.AddHours(1)) }
                        : new { ok = true, data = (object)new MessageResult("fixture") };
            return Task.FromResult(new HttpResponseMessage(expired ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(response, ApiClient.Json), Encoding.UTF8, "application/json") });
        }
    }
}
