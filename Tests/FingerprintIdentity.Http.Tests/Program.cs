using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string description)
    { checks++; if (!value) throw new InvalidOperationException(description); }
    private static string Encode(byte[] raw) => Convert.ToBase64String(raw).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string text) => Convert.FromBase64String(text.Replace('-', '+').Replace('_', '/') + new string('=', (4 - text.Length % 4) % 4));
    private static async Task Throws<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { Check(true, typeof(T).Name); return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }
    private static async Task<int> Main(string[] args)
    {
        if (args.Length != 2 || args[0] != "--python")
        { Console.Error.WriteLine("Usage: --python <venv Python with Flask/tzdata>; source checkout required."); return 2; }
        try
        {
            await using var host = await Host.Start(args[1]);
            foreach (string fault in new[] { "none", "before_submit", "after_submit", "after_approval" }) await RoundTrip(host, "matched", fault);
            await RoundTrip(host, "no_match", "none");
            await InvalidApproval(host); await SlowApproval(host); await Cancel(host); await ContextChange(host); await CompleteLost(host);
            await MalformedResponse("redirect"); await MalformedResponse("large"); await MalformedResponse("stream");
            await host.Stop();
            Console.WriteLine($"PASS identity HTTP: {checks} assertions; real C# HttpClient ↔ owned Python loopback Flask/Pi core/fake UART/ephemeral issuer. No hardware, production server, DB, real key/profile or login/attendance effects.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static async Task RoundTrip(Host host, string mode, string fault)
    {
        var before = await host.Stats(); var fixture = await host.New(mode);
        using var server = new Server(host.Address, fixture.ChallengeId);
        using var handler = new Faults(fault);
        using var device = new HttpFingerprintIdentityDevice(host.Address, handler);
        using var relay = Create(fixture, server, device);
        var result = await relay.RunAsync();
        Check(result.State == (mode == "matched" ? "verified" : "rejected"), "Cryptographically checked synthetic receipt");
        Check(result.WorkflowState != "completed", "Identity result never reports attendance/login completion");
        await host.Idle(); var after = await host.Stats();
        Check(after.GetProperty("searches").GetInt32() == before.GetProperty("searches").GetInt32() + 1, "One fake search despite transport loss");
        Check(after.GetProperty("completions").GetInt32() == before.GetProperty("completions").GetInt32() + 1, "One synthetic server result");
        Check(server.Approvals == 2, "Exactly capture/result server approvals");
        Check(!after.GetProperty("shared_stopping").GetBoolean(), "Borrowed service stays running");
        Check(handler.Capabilities.Distinct().Count() == 1, "Stable RAM capability through recovery and cleanup");
        Check(handler.HeadersSafe && handler.CapabilitySizesSafe, "Pi receives only 32-byte capability, no account token/cookie");
        Check(handler.SubmitBodies.Distinct().Count() == 1, "Same relay/body if POST is resent");
        if (fault != "none") Check(handler.Dropped, "Requested network fault exercised");
        await relay.ResumeCompletionAsync();
        Check(server.Completes == 1 && server.Gets == 1, "Confirmed result lookup never invokes Pi or completes again");
    }
    private static FingerprintIdentityRelay Create(Fixture fixture, Server server, IIdentityRelayDevice device, Func<bool>? current = null)
        => new(fixture.ChallengeId, fixture.DeviceId, fixture.Purpose, Decode(fixture.RequestB64), current ?? (() => true), CancellationToken.None, server, device);
    private static async Task InvalidApproval(Host host)
    {
        var before = await host.Stats(); var f = await host.New("bad_approval");
        using var server = new Server(host.Address, f.ChallengeId);
        using var device = new HttpFingerprintIdentityDevice(host.Address);
        using var relay = Create(f, server, device);
        await Throws<InvalidOperationException>(() => relay.RunAsync());
        await host.Idle();
        Check((await host.Stats()).GetProperty("images").GetInt32() == before.GetProperty("images").GetInt32(), "Bad server MAC stops before sensor image");
        Check(server.Completes == 0, "No fabricated result after failed approval");
    }
    private static async Task SlowApproval(Host host)
    {
        var before = await host.Stats(); var f = await host.New("slow_approval");
        using var server = new Server(host.Address, f.ChallengeId);
        using var device = new HttpFingerprintIdentityDevice(host.Address);
        using var relay = Create(f, server, device);
        await Throws<OperationCanceledException>(() => relay.RunAsync());
        await host.Idle();
        Check((await host.Stats()).GetProperty("images").GetInt32() == before.GetProperty("images").GetInt32(), "Late approval does not capture");
        Check(server.Completes == 0, "Approval timeout has no success completion");
    }
    private static async Task Cancel(Host host)
    {
        var before = await host.Stats(); var f = await host.New("wait");
        using var server = new Server(host.Address, f.ChallengeId);
        using var device = new HttpFingerprintIdentityDevice(host.Address);
        using var relay = Create(f, server, device);
        using var cancellation = new CancellationTokenSource();
        var pending = relay.RunAsync(cancellation.Token);
        await host.Until(async () => (await host.Stats()).GetProperty("images").GetInt32() > before.GetProperty("images").GetInt32());
        cancellation.Cancel(); await Throws<OperationCanceledException>(() => pending); await host.Idle();
        Check(server.Completes == 0, "Cancellation cannot complete identity");
        Check(!(await host.Stats()).GetProperty("shared_stopping").GetBoolean(), "Pi cancellation does not stop shared fingerprint service");
    }
    private static async Task ContextChange(Host host)
    {
        var before = await host.Stats(); var f = await host.New("matched"); bool current = true;
        using var server = new Server(host.Address, f.ChallengeId) { AfterApproval = () => current = false };
        using var device = new HttpFingerprintIdentityDevice(host.Address);
        using var relay = Create(f, server, device, () => current);
        await Throws<OperationCanceledException>(() => relay.RunAsync()); await host.Idle();
        Check(server.Completes == 0 && (await host.Stats()).GetProperty("images").GetInt32() == before.GetProperty("images").GetInt32(), "Current-context guard prevents late approval delivery");
    }
    private static async Task CompleteLost(Host host)
    {
        var before = await host.Stats(); var f = await host.New("matched");
        using var server = new Server(host.Address, f.ChallengeId) { LoseCompletion = true };
        using var device = new HttpFingerprintIdentityDevice(host.Address);
        using var relay = Create(f, server, device);
        await Throws<HttpRequestException>(() => relay.RunAsync());
        var receipt = await relay.ResumeCompletionAsync(); await host.Idle();
        Check(receipt.State == "verified" && server.Gets == 1 && server.Completes == 1, "Committed lost reply recovered by real HTTP GET");
        Check((await host.Stats()).GetProperty("searches").GetInt32() == before.GetProperty("searches").GetInt32() + 1, "Lost completion has no second scan");
    }
    private static async Task MalformedResponse(string kind)
    {
        using var handler = new BadResponse(kind);
        using var device = new HttpFingerprintIdentityDevice("http://127.0.0.1:1", handler);
        if (kind == "redirect") await Throws<IdentityPiHttpException>(() => device.CaptureAsync(new byte[] { 1 }, (_, _) => throw new InvalidOperationException(), CancellationToken.None));
        else await Throws<InvalidDataException>(() => device.CaptureAsync(new byte[] { 1 }, (_, _) => throw new InvalidOperationException(), CancellationToken.None));
        Check(handler.Calls == 2, "Only original request and owned cleanup, no redirected request/rescan");
    }
    private sealed record Fixture(Guid ChallengeId, string DeviceId, string Purpose, string RequestB64);
    private sealed class Server : IIdentityRelayServer, IDisposable
    {
        private readonly HttpClient http;
        private readonly Guid challenge;
        public int Approvals, Completes, Gets;
        public bool LoseCompletion;
        public Action? AfterApproval;
        public Server(string address, Guid challenge)
        { http = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new(address), Timeout = TimeSpan.FromSeconds(5) }; this.challenge = challenge; }
        public async Task<string> ApproveAsync(Guid id, string probe, CancellationToken cancellation)
        {
            Check(id == challenge, "Approval target context"); Approvals++;
            using var response = await http.PostAsJsonAsync($"_test/{id:D}/approval", new { probe_b64 = probe }, cancellation);
            response.EnsureSuccessStatusCode(); var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellation);
            AfterApproval?.Invoke(); return body.GetProperty("approval_b64").GetString()!;
        }
        public async Task<IdentityStatus> CompleteAsync(Guid id, string result, CancellationToken cancellation)
        {
            Check(id == challenge, "Completion target context"); Completes++;
            using var response = await http.PostAsJsonAsync($"_test/{id:D}/complete", new { result_b64 = result }, cancellation);
            response.EnsureSuccessStatusCode();
            var body = (await response.Content.ReadFromJsonAsync<IdentityStatus>(ApiClient.Json, cancellation))!;
            if (LoseCompletion) { LoseCompletion = false; throw new HttpRequestException("Synthetic committed reply loss"); }
            return body;
        }
        public async Task<IdentityStatus> GetAsync(Guid id, CancellationToken cancellation)
        { Check(id == challenge, "Lookup target context"); Gets++; return (await http.GetFromJsonAsync<IdentityStatus>($"_test/{id:D}/status", ApiClient.Json, cancellation))!; }
        public void Dispose() => http.Dispose();
    }
    private sealed class Faults(string mode) : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseProxy = false })
    {
        public bool Dropped;
        public bool HeadersSafe = true, CapabilitySizesSafe = true;
        public List<string> Capabilities = new(), SubmitBodies = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            HeadersSafe &= request.Headers.Authorization is null && !request.Headers.Contains("Cookie");
            string cap = request.Headers.GetValues("X-Identity-Relay").Single();
            CapabilitySizesSafe &= Decode(cap).Length == 32; Capabilities.Add(cap);
            bool submit = request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/identity/v2/jobs";
            bool approval = request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/approval");
            if (submit) SubmitBodies.Add(await request.Content!.ReadAsStringAsync(cancellation));
            if (!Dropped && submit && mode == "before_submit") { Dropped = true; throw new HttpRequestException("Synthetic pre-send loss"); }
            var response = await base.SendAsync(request, cancellation);
            if (!Dropped && (submit && mode == "after_submit" || approval && mode == "after_approval"))
            { Dropped = true; response.Dispose(); throw new HttpRequestException("Synthetic committed response loss"); }
            return response;
        }
    }
    private sealed class BadResponse(string kind) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            Calls++;
            Check(request.RequestUri!.Host == "127.0.0.1" && request.Headers.Authorization is null, "No alternate target/authentication");
            if (request.RequestUri.AbsolutePath.EndsWith("/cancel"))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"success\":true,\"data\":{}}", Encoding.UTF8, "application/json") });
            var response = new HttpResponseMessage(kind == "redirect" ? HttpStatusCode.Found : HttpStatusCode.OK)
            { Content = kind == "stream" ? new Chunked() : new StringContent(kind == "large" ? new string('x', 65537) : "", Encoding.UTF8, "application/json") };
            response.Headers.Location = new Uri("http://unrelated.invalid/");
            return Task.FromResult(response);
        }
    }
    private sealed class Chunked : HttpContent
    {
        public Chunked() { Headers.ContentType = new("application/json"); }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(new byte[65537]).AsTask();
    }
    private sealed class Host : IAsyncDisposable
    {
        private readonly Process process;
        private readonly Task<string> errors;
        private readonly HttpClient http;
        private bool stopped;
        public string Address { get; }
        private Host(Process process, string address)
        { this.process = process; Address = address; errors = process.StandardError.ReadToEndAsync(); http = new HttpClient(new HttpClientHandler { UseProxy = false }) { BaseAddress = new(address), Timeout = TimeSpan.FromSeconds(5) }; }
        public static async Task<Host> Start(string python)
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "Devices", "RaspberryPi"))) root = root.Parent;
            if (root is null) throw new InvalidOperationException("Source checkout not found");
            var start = new ProcessStartInfo(python) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.Environment["PYTHONIOENCODING"] = "utf-8";
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "loopback_pi.py"));
            start.ArgumentList.Add("--repository"); start.ArgumentList.Add(root.FullName);
            var process = Process.Start(start) ?? throw new InvalidOperationException("Test process did not start");
            try
            {
                string? line = await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
                if (line is null) throw new InvalidOperationException("Test harness exited: " + await process.StandardError.ReadToEndAsync());
                using var ready = JsonDocument.Parse(line);
                Check(ready.RootElement.GetProperty("synthetic").GetBoolean() && ready.RootElement.GetProperty("loopback_only").GetBoolean(), "Synthetic loopback harness only");
                return new Host(process, $"http://127.0.0.1:{ready.RootElement.GetProperty("port").GetInt32()}/");
            }
            catch
            {
                if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
                process.Dispose(); throw;
            }
        }
        public async Task<Fixture> New(string mode)
        {
            using var response = await http.PostAsJsonAsync("_test/new", new { mode }); response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<Fixture>(ApiClient.Json))!;
        }
        public Task<JsonElement> Stats() => http.GetFromJsonAsync<JsonElement>("_test/stats");
        public Task Idle() => Until(async () => !(await Stats()).GetProperty("sensor_locked").GetBoolean());
        public async Task Until(Func<Task<bool>> predicate)
        {
            var timer = Stopwatch.StartNew();
            while (!await predicate())
            { if (timer.Elapsed > TimeSpan.FromSeconds(5)) throw new TimeoutException("Fixture did not settle"); await Task.Delay(20); }
        }
        public async Task Stop()
        {
            if (stopped) return; stopped = true;
            await process.StandardInput.WriteLineAsync("stop"); await process.StandardInput.FlushAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            string output = await process.StandardOutput.ReadToEndAsync();
            Check(process.ExitCode == 0 && output.Contains("\"stopped\": true") && output.Contains("\"shared_stopping\": false"), "Owned workers/process closed, shared stop unchanged");
            Check(string.IsNullOrWhiteSpace(await errors), "Loopback server emitted no hidden exception");
        }
        public async ValueTask DisposeAsync()
        {
            try { await Stop(); }
            finally
            { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); } http.Dispose(); process.Dispose(); }
        }
    }
}
