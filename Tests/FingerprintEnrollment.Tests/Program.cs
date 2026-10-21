using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private const string Device = "123e4567-e89b-42d3-a456-426614174000";
    private const string JobId = "31593412-67fd-40d2-878b-868d15f99a11";
    private const string SourceJobId = "b8b34b01-5f2f-4f2a-bd33-34c557c2e932";
    private const string RecoveryId = "bf514df7-f4eb-47b3-b542-a68b4de26a58";
    private static readonly string Opaque = B64("synthetic unchanged signed bytes");
    private static int checks;
    private static void Check(bool value, string label)
    { if (!value) throw new Exception(label); checks++; }
    private static string B64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static async Task Throws<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T) { checks++; return; } throw new Exception($"Expected {typeof(T).Name}"); }

    private sealed class Server : HttpMessageHandler
    {
        public int Issues, Completes, Approvals, Lookups;
        public bool LoseIssue, LoseComplete, FailBeforeCommit;
        public bool LosePhysical, HasIntent;
        public string? PhysicalRaw;
        public int PhysicalReads, PhysicalWrites, StoreIntents;
        public Func<Dictionary<string, object?>, Dictionary<string, object?>> ChangeReceipt = value => value;
        public Func<Dictionary<string, object?>, Dictionary<string, object?>> ChangeRecovery = value => value;
        public Func<EnrollmentJob, EnrollmentJob> ChangeReissuedJob = value => value;
        public int RecoveryReads, Reissues, RejectRecovery, RejectReissue;
        public bool Reissued, LoseReissue, FailReissueBeforeCommit;
        public string ClaimRevision = "1";
        public Func<Task>? BeforeRecoveryReturn;
        public string State = "issued";
        public string ClientId = "";
        public readonly List<string> Results = [];
        public readonly List<int> Targets = [];
        public string RequestEnvelope = Opaque;
        public bool RealProof;
        public Func<string, Task>? VerifyResult;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.AbsolutePath;
            using var document = JsonDocument.Parse(request.Content is null ? "{}" : await request.Content.ReadAsStringAsync(ct));
            var body = document.RootElement;
            object data;
            if (path.EndsWith("auth/login"))
                data = new { access_token = "public-synthetic-bearer", token_type = "Bearer", expires_at = DateTimeOffset.UtcNow.AddHours(1),
                    user = new UserInfo(1, "admin", "가상 관리자", "QA", "QA", "admin", true) };
            else
            {
                Check(request.Headers.Authorization?.Parameter == "public-synthetic-bearer", "server gets pinned bearer");
                if (path.Contains("eligible-devices")) data = new { devices = new[] { new EnrollmentDevice(Device, "가상 장치", "1") } };
                else if (path.Contains("/recoveries/"))
                {
                    bool post = request.Method == HttpMethod.Post;
                    Check(path == "/api/v1/device-enrollments/recoveries/" + RecoveryId + (post ? "/reissue" : ""), "fixed recovery route only");
                    int reject = post ? RejectReissue : RejectRecovery;
                    if (reject != 0) return new HttpResponseMessage((HttpStatusCode)reject)
                    { Content = new StringContent("{\"ok\":false,\"error\":{\"code\":\"SYNTHETIC_REVIEW_REQUIRED\",\"message\":\"review required\"}}", Encoding.UTF8, "application/json") };
                    if (post)
                    {
                        Reissues++;
                        Check(body.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "client_request_id", "target_user_id" }), "reissue sends exactly two fields, no slot/old session/empty assertion");
                        if (FailReissueBeforeCommit) { FailReissueBeforeCommit = false; throw new HttpRequestException("before reissue commit"); }
                        string id = body.GetProperty("client_request_id").GetString()!;
                        Check(!Reissued || ClientId == id, "reissue retains the same request UUID");
                        Targets.Add(body.GetProperty("target_user_id").GetInt32());
                        ClientId = id; Reissued = true; ClaimRevision = "2";
                        if (LoseReissue) { LoseReissue = false; throw new HttpRequestException("after reissue commit"); }
                        data = new { recovery = Recovery(), job = ChangeReissuedJob(Job()) };
                    }
                    else { RecoveryReads++; data = new { recovery = Recovery() }; }
                    if (BeforeRecoveryReturn is not null) await BeforeRecoveryReturn();
                }
                else if (path.EndsWith("/physical-result"))
                {
                    if (request.Method == HttpMethod.Post)
                    {
                        PhysicalWrites++;
                        var raw = body.GetProperty("result_envelope").GetString()!;
                        Check(PhysicalRaw is null || PhysicalRaw == raw, "write-once physical receipt bytes");
                        PhysicalRaw = raw;
                        if (LosePhysical) { LosePhysical = false; throw new HttpRequestException("physical response lost"); }
                    }
                    else PhysicalReads++;
                    data = Physical();
                }
                else if (path.EndsWith("/storage-intent"))
                {
                    StoreIntents++;
                    bool fresh = !HasIntent;
                    HasIntent = true;
                    var value = Physical();
                    value["may_store"] = fresh;
                    value["approval_envelope"] = fresh ? RealProof ? MakeApproval(body) : B64("opaque server approval") : null;
                    data = value;
                }
                else if (path.EndsWith("/device-enrollments"))
                {
                    Issues++;
                    Targets.Add(body.GetProperty("target_user_id").GetInt32());
                    ClientId = body.GetProperty("client_request_id").GetString()!;
                    if (LoseIssue) { LoseIssue = false; throw new HttpRequestException("synthetic issue response lost"); }
                    data = Job();
                }
                else if (path.EndsWith("/approval"))
                {
                    Approvals++;
                    data = new { approval_envelope = RealProof ? MakeApproval(body) : B64("opaque server approval") };
                }
                else if (path.EndsWith("/complete"))
                {
                    Check(PhysicalRaw == body.GetProperty("result_envelope").GetString(), "mapping requires recorded exact result");
                    Completes++;
                    Results.Add(body.GetProperty("result_envelope").GetString()!);
                    if (VerifyResult is not null) await VerifyResult(Results[^1]);
                    if (FailBeforeCommit) { FailBeforeCommit = false; throw new HttpRequestException("before commit"); }
                    State = "completed";
                    if (LoseComplete) { LoseComplete = false; throw new HttpRequestException("after commit"); }
                    data = Job();
                }
                else if (path.EndsWith("/cancel")) { State = "cancelled"; data = Job(); }
                else { Lookups++; data = Job(); }
            }
            return Json(new { ok = true, data });
        }
        public Dictionary<string, object?> Physical()
        {
            Dictionary<string, object?> value = new()
            {
            ["enrollment_id"] = JobId, ["operation_revision"] = "1", ["reserved_slot"] = 7,
            ["claim_revision"] = ClaimRevision, ["key_epoch"] = "1", ["contract"] = "enrollment-mysql-v1",
            ["phase"] = PhysicalRaw is not null ? "result_recorded" : HasIntent ? "store_intent" : "not_recorded",
            ["result_envelope"] = PhysicalRaw, ["result_id"] = PhysicalRaw is null ? null : "af7de985-1df9-4e65-a310-dd86fa4c6862",
            ["outcome"] = PhysicalRaw is null ? null : "sensor_stored", ["failure_code"] = null,
            ["intent_at"] = HasIntent ? "2026-10-20T01:01:00Z" : null,
            ["reported_at"] = PhysicalRaw is null ? null : "2026-10-20T01:01:01Z",
            ["may_store"] = false, ["approval_envelope"] = null
            };
            if (RealProof && PhysicalRaw is not null)
            {
                using var envelope = JsonDocument.Parse(EnrollmentValidation.Base64(PhysicalRaw));
                using var payload = JsonDocument.Parse(EnrollmentValidation.Base64(envelope.RootElement.GetProperty("payload_b64").GetString()!));
                foreach (string name in new[] { "result_id", "outcome", "failure_code" })
                    value[name] = payload.RootElement.GetProperty(name).Clone();
            }
            return ChangeReceipt(value);
        }
        public Dictionary<string, object?> Recovery() => ChangeRecovery(new()
        {
            ["recovery_id"] = RecoveryId, ["source_enrollment_id"] = SourceJobId, ["device_id"] = Device,
            ["reserved_slot"] = 7, ["source_operation_revision"] = "1", ["source_claim_revision"] = "1",
            ["state"] = Reissued ? "reissued" : "closed_awaiting_explicit_reissue",
            ["new_enrollment_id"] = Reissued ? JobId : null, ["evidence_kind"] = "operator_observation_not_signed_result",
            ["closed_at"] = "2026-10-21T00:38:51Z", ["reissued_at"] = Reissued ? "2026-10-21T00:39:00Z" : null
        });
        public EnrollmentJob Job() => new(JobId, "1", 7, ClaimRevision, "1", ClientId, Device, State,
            "2026-10-20T01:03:00Z", State != "issued" && State != "completed", State == "issued" ? RequestEnvelope : null,
            State == "completed" ? new EnrollmentReceipt("af7de985-1df9-4e65-a310-dd86fa4c6862", "completed", null) : null);

        private static string MakeApproval(JsonElement body)
        {
            // PUBLIC synthetic server key only; production relay has NO signer/key.
            var value = new Dictionary<string, object?>
            { ["enrollment_id"] = JobId, ["decision"] = "allow", ["server_time"] = "2026-10-20T01:01:00Z", ["valid_until"] = "2026-10-20T01:01:05Z" };
            foreach (var property in body.EnumerateObject()) value[property.Name] = property.Value.Clone();
            string payload = B64(JsonSerializer.Serialize(value));
            byte[] input = Encoding.ASCII.GetBytes("RW-ENROLL-APPROVAL-v1\0enroll_approval\0" + Device + "\0public-enroll-test-v1\0" + payload);
            string mac = EnrollmentValidation.Encode(HMACSHA256.HashData(Enumerable.Range(0, 32).Select(x => (byte)x).ToArray(), input));
            return B64(JsonSerializer.Serialize(new { v = 1, kind = "enroll_approval", device_id = Device,
                key_id = "public-enroll-test-v1", payload_b64 = payload, mac_b64 = mac }));
        }
    }

    private sealed class Pi : IEnrollmentPiChannel
    {
        public int Writes, Reads;
        public bool Fail, WrongScope;
        public Func<Task>? BeforeReturn;
        public TaskCompletionSource? Wait;
        public async Task<string> TransferAsync(EnrollmentJob job, bool readOnly, Func<JsonElement, CancellationToken, Task<string>> approve,
            IProgress<string>? progress, CancellationToken cancellation)
        {
            if (readOnly) Reads++; else Writes++;
            using var challenge = JsonDocument.Parse(JsonSerializer.Serialize(new
            { operation_revision = job.OperationRevision, reserved_slot = job.ReservedSlot, claim_revision = job.ClaimRevision, key_epoch = job.KeyEpoch,
                ticket_digest = new string('a', 64), probe_nonce = B64(new string('x', 32)),
                scope = WrongScope ? "sensor_write" : readOnly ? "result_read" : "sensor_capture" }));
            string response = await approve(challenge.RootElement, cancellation);
            Check(response == B64("opaque server approval"), "approval bytes forwarded unchanged");
            if (!readOnly)
            {
                var body = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(challenge.RootElement.GetRawText())!;
                body["scope"] = JsonSerializer.SerializeToElement("sensor_store");
                using var storeBody = JsonDocument.Parse(JsonSerializer.Serialize(body));
                string storeReply = await approve(storeBody.RootElement, cancellation);
                using var receipt = JsonDocument.Parse(EnrollmentValidation.Base64(storeReply, 32768));
                Check(receipt.RootElement.GetProperty("may_store").GetBoolean(), "store requires first committed intent reply");
            }
            if (Wait is not null) await Wait.Task;
            if (BeforeReturn is not null) await BeforeReturn();
            if (Fail) { Fail = false; throw new HttpRequestException("synthetic Pi reply lost"); }
            return Opaque;
        }
    }

    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(value, ApiClient.Json), Encoding.UTF8, "application/json") };

    private static async Task Main(string[] args)
    {
        await NormalAndReplay();
        await LostReplies();
        await LifetimesAndScope();
        await PiHttpBoundary();
        await PiFailureReports();
        await MySqlRecovery();
        await ExplicitReissue();
        await ReissueBindingsAndLifetime();
        if (args.Length == 2 && args[0] == "--pi-python") await PythonLoopback(args[1]);
        Console.WriteLine($"Fingerprint enrollment relay: {checks} checks PASS (public synthetic fixtures; no production keys/hardware/DB).");
    }

    private static async Task ExplicitReissue()
    {
        foreach (string mode in new[] { "normal", "lost_after_commit", "lost_before_commit", "conflict", "unavailable" })
        {
            var server = new Server { LoseReissue = mode == "lost_after_commit", FailReissueBeforeCommit = mode == "lost_before_commit" };
            using var api = new ApiClient("http://127.0.0.1:5000", server);
            await api.LoginAsync("admin", "synthetic");
            var pi = new Pi();
            var target = new EnrollmentTarget(88, api.CaptureSession(), () => true, CancellationToken.None);
            var relay = new FingerprintEnrollmentRelay(target, pi);
            var selection = await relay.GetRecoveryAsync(RecoveryId);
            Check(server.Reissues == 0 && server.Issues == 0 && pi.Writes == 0, mode + ": reading recovery grants no issue/store");
            var attempt = relay.CreateRecoveryAttempt(selection, new(Device, "test", "1"));
            string requestId = attempt.ClientRequestId;
            if (mode is "conflict" or "unavailable")
            {
                server.RejectReissue = mode == "conflict" ? 409 : 503;
                if (mode == "conflict") await Throws<ApiException>(() => relay.RunAsync(attempt));
                else await Throws<InvalidOperationException>(() => relay.RunAsync(attempt));
                Check(server.Issues == 0 && pi.Writes == 0 && attempt.Job is null, mode + ": no ordinary issue or sensor fallback");
                continue;
            }
            if (mode == "lost_before_commit") await Throws<InvalidOperationException>(() => relay.RunAsync(attempt));
            Check((await relay.RunAsync(attempt)).State == "completed", mode + ": new recovery job completes through MySQL pipeline");
            await relay.RunAsync(attempt);
            Check(server.Issues == 0 && server.Completes == 1 && pi.Writes == 1 && pi.Reads == 0, mode + ": one sensor write and mapping, no ordinary issue");
            Check(attempt.ClientRequestId == requestId && server.Targets.All(id => id == 88), mode + ": same request UUID and created member ID");
            Check(attempt.Job!.EnrollmentId != SourceJobId && attempt.Job.ClaimRevision == "2", mode + ": old/new ID and incremented claim pinned");
        }
    }

    private static async Task ReissueBindingsAndLifetime()
    {
        {
            var server = new Server { Reissued = true, ClientId = Guid.NewGuid().ToString() };
            using var api = new ApiClient("http://127.0.0.1:5000", server);
            await api.LoginAsync("admin", "synthetic");
            var pi = new Pi(); var relay = new FingerprintEnrollmentRelay(new EnrollmentTarget(88, api.CaptureSession(), () => true, CancellationToken.None), pi);
            await Throws<InvalidOperationException>(() => relay.GetRecoveryAsync(RecoveryId));
            Check(server.Reissues == 0 && pi.Writes == 0, "new view cannot adopt an already-issued recovery without its original attempt");
            int reads = server.RecoveryReads;
            await Throws<InvalidDataException>(() => relay.GetRecoveryAsync("../other-job"));
            Check(server.RecoveryReads == reads, "invalid recovery identifier is rejected before HTTP");
        }
        foreach (int status in new[] { 403, 404, 503 })
        {
            var server = new Server { RejectRecovery = status }; using var api = new ApiClient("http://127.0.0.1:5000", server);
            await api.LoginAsync("admin", "synthetic");
            var pi = new Pi(); var relay = new FingerprintEnrollmentRelay(new EnrollmentTarget(88, api.CaptureSession(), () => true, CancellationToken.None), pi);
            await Throws<ApiException>(() => relay.GetRecoveryAsync(RecoveryId));
            Check(server.Issues == 0 && server.Reissues == 0 && pi.Writes == 0, status + ": lookup errors never fall back to ordinary issue");
        }
        foreach (string field in new[] { "recovery_id", "device_id", "source_enrollment_id", "source_claim_revision", "evidence_kind", "reserved_slot", "closed_at", "extra", "already_reissued" })
        {
            var server = new Server(); using var api = new ApiClient("http://127.0.0.1:5000", server);
            await api.LoginAsync("admin", "synthetic");
            var target = new EnrollmentTarget(88, api.CaptureSession(), () => true, CancellationToken.None);
            var pi = new Pi(); var relay = new FingerprintEnrollmentRelay(target, pi);
            if (field is "evidence_kind" or "reserved_slot" or "closed_at" or "extra" or "already_reissued" or "recovery_id")
            {
                server.ChangeRecovery = value => { value[field == "already_reissued" ? "state" : field] = field switch
                { "reserved_slot" => true, "closed_at" => "2026-10-21T09:38:51+09:00", "recovery_id" => Guid.NewGuid().ToString(),
                    "already_reissued" => "reissued", _ => "unexpected" }; return value; };
                await Throws<InvalidDataException>(() => relay.GetRecoveryAsync(RecoveryId));
            }
            else
            {
                var selected = await relay.GetRecoveryAsync(RecoveryId);
                var attempt = relay.CreateRecoveryAttempt(selected, new(Device, "test", "1"));
                server.ChangeRecovery = value => { value[field] = field == "source_claim_revision" ? "2" : Guid.NewGuid().ToString(); return value; };
                await Throws<InvalidDataException>(() => relay.RunAsync(attempt));
            }
            Check(pi.Writes == 0 && server.Issues == 0 && server.Completes == 0, field + ": mismatched recovery never reaches sensor");
        }
        foreach (string field in new[] { "claim", "slot", "operation", "request", "epoch", "old_job", "no_ticket" })
        {
            var server = new Server(); using var api = new ApiClient("http://127.0.0.1:5000", server);
            await api.LoginAsync("admin", "synthetic");
            var pi = new Pi(); var target = new EnrollmentTarget(88, api.CaptureSession(), () => true, CancellationToken.None);
            var relay = new FingerprintEnrollmentRelay(target, pi);
            var attempt = relay.CreateRecoveryAttempt(await relay.GetRecoveryAsync(RecoveryId), new(Device, "test", "1"));
            server.ChangeReissuedJob = job => field switch
            {
                "claim" => job with { ClaimRevision = "1" }, "slot" => job with { ReservedSlot = 8 },
                "operation" => job with { OperationRevision = "2" }, "request" => job with { ClientRequestId = Guid.NewGuid().ToString() },
                "epoch" => job with { KeyEpoch = "2" }, "old_job" => job with { EnrollmentId = SourceJobId },
                _ => job with { RequestEnvelope = null }
            };
            await Throws<InvalidDataException>(() => relay.RunAsync(attempt));
            Check(pi.Writes == 0 && server.Issues == 0, field + ": invalid reissue job blocked before Pi");
        }
        foreach (string mode in new[] { "target", "logout", "relogin", "foreign_target", "device", "forbidden" })
        {
            var server = new Server(); using var api = new ApiClient("http://127.0.0.1:5000", server);
            await api.LoginAsync("admin", "synthetic");
            bool current = true;
            var target = new EnrollmentTarget(88, api.CaptureSession(), () => current, CancellationToken.None);
            var pi = new Pi(); var relay = new FingerprintEnrollmentRelay(target, pi);
            var selection = await relay.GetRecoveryAsync(RecoveryId);
            if (mode == "foreign_target")
            {
                var other = new FingerprintEnrollmentRelay(new EnrollmentTarget(99, api.CaptureSession(), () => true, CancellationToken.None), pi);
                await Throws<ArgumentException>(() => Task.FromResult(other.CreateRecoveryAttempt(selection, new(Device, "test", "1"))));
            }
            else if (mode == "device")
                await Throws<InvalidDataException>(() => Task.FromResult(relay.CreateRecoveryAttempt(selection, new(Guid.NewGuid().ToString(), "other", "1"))));
            else
            {
                var attempt = relay.CreateRecoveryAttempt(selection, new(Device, "test", "1"));
                if (mode == "forbidden") { server.RejectReissue = 403; await Throws<ApiException>(() => relay.RunAsync(attempt)); }
                else
                {
                    server.BeforeRecoveryReturn = async () => { if (mode == "target") current = false;
                        else if (mode == "logout") api.ClearIdentity(); else await api.LoginAsync("admin", "synthetic"); };
                    await Throws<OperationCanceledException>(() => relay.RunAsync(attempt));
                }
            }
            Check(server.Issues == 0 && pi.Writes == 0 && server.Completes == 0, mode + ": stale/foreign recovery cannot enroll");
        }
    }

    private static async Task MySqlRecovery()
    {
        foreach (string mode in new[] { "intent_only", "physical_response_lost", "old_contract", "wrong_binding" })
        {
            var server = new Server { HasIntent = mode == "intent_only", LosePhysical = mode == "physical_response_lost" };
            if (mode == "old_contract") server.ChangeReceipt = value => { value["contract"] = "legacy"; return value; };
            if (mode == "wrong_binding") server.ChangeReceipt = value => { value["claim_revision"] = "2"; return value; };
            using var api = new ApiClient("http://127.0.0.1:5000", server);
            await api.LoginAsync("admin", "synthetic");
            var pi = new Pi();
            var relay = new FingerprintEnrollmentRelay(new EnrollmentTarget(88, api.CaptureSession(), () => true, CancellationToken.None), pi);
            var attempt = relay.CreateAttempt(Device);
            if (mode is "old_contract" or "wrong_binding")
            {
                await Throws<InvalidDataException>(() => relay.RunAsync(attempt));
                Check(pi.Writes == 0 && pi.Reads == 0 && server.Completes == 0, mode + ": blocked before contacting sensor");
            }
            else
            {
                if (mode == "physical_response_lost")
                    await Throws<ApiException>(() => relay.RunAsync(attempt));
                Check((await relay.RunAsync(attempt)).State == "completed", mode + ": recorded evidence completes");
                Check(pi.Writes == (mode == "intent_only" ? 0 : 1), mode + ": no repeated sensor write");
                Check(pi.Reads == (mode == "intent_only" ? 1 : 0), mode + ": intent is read-only");
                Check(server.PhysicalRaw == Opaque && server.Results.All(raw => raw == Opaque), mode + ": result bytes stay fixed");
            }
        }
    }

    private static async Task NormalAndReplay()
    {
        var server = new Server(); using var api = new ApiClient("http://127.0.0.1:5000", server);
        await api.LoginAsync("admin", "synthetic");
        var target = new EnrollmentTarget(88, api.CaptureSession(), () => true, CancellationToken.None);
        var pi = new Pi(); var relay = new FingerprintEnrollmentRelay(target, pi);
        Check((await relay.GetDevicesAsync()).Count == 1, "eligible device list");
        var attempt = relay.CreateAttempt(Device);
        Check((await relay.RunAsync(attempt)).State == "completed", "only server completed is success");
        Check(server.Targets.SequenceEqual([88]), "created user ID pinned, administrator separate");
        Check(server.Results.SequenceEqual([Opaque]), "same result envelope forwarded");
        await relay.RunAsync(attempt);
        Check(pi.Writes == 1 && pi.Reads == 0 && server.Completes == 1, "server success replay never scans/commits again");
        var held = new Pi { Wait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var other = new FingerprintEnrollmentRelay(target, held);
        server.State = "issued";
        server.PhysicalRaw = null; server.HasIntent = false;
        var next = other.CreateAttempt(Device);
        var first = other.RunAsync(next);
        await Throws<InvalidOperationException>(() => other.RunAsync(next));
        held.Wait.SetResult(); await first;
        Check(held.Writes == 1, "concurrent same-attempt start blocked");
    }

    private static async Task LostReplies()
    {
        foreach (string mode in new[] { "issue", "after_commit", "before_commit", "pi" })
        {
            var server = new Server { LoseIssue = mode == "issue", LoseComplete = mode == "after_commit", FailBeforeCommit = mode == "before_commit" };
            using var api = new ApiClient("http://127.0.0.1:5000", server);
            await api.LoginAsync("admin", "synthetic");
            var target = new EnrollmentTarget(88, api.CaptureSession(), () => true, CancellationToken.None);
            var pi = new Pi { Fail = mode == "pi" }; var relay = new FingerprintEnrollmentRelay(target, pi);
            var attempt = relay.CreateAttempt(Device);
            string id = attempt.ClientRequestId;
            if (mode == "issue") await relay.RunAsync(attempt);
            else
            {
                if (mode == "pi") await Throws<HttpRequestException>(() => relay.RunAsync(attempt));
                else await Throws<ApiException>(() => relay.RunAsync(attempt));
                await relay.RunAsync(attempt);
            }
            Check(attempt.ClientRequestId == id && server.Issues == 1, mode + ": one issue key");
            Check(pi.Writes == 1, mode + ": no automatic second scan");
            Check(pi.Reads == (mode == "pi" ? 1 : 0), mode + ": uncertain Pi send uses read-only recovery");
            Check(server.Results.All(r => r == Opaque), mode + ": exact result bytes");
        }
    }

    private static async Task LifetimesAndScope()
    {
        foreach (string mode in new[] { "target", "logout", "relogin", "scope" })
        {
            var server = new Server(); using var api = new ApiClient("http://127.0.0.1:5000", server);
            await api.LoginAsync("admin", "synthetic");
            bool current = true;
            var target = new EnrollmentTarget(88, api.CaptureSession(), () => current, CancellationToken.None);
            var pi = new Pi { WrongScope = mode == "scope" };
            pi.BeforeReturn = async () =>
            {
                if (mode == "target") current = false;
                if (mode == "logout") api.ClearIdentity();
                if (mode == "relogin") await api.LoginAsync("admin", "synthetic");
            };
            var relay = new FingerprintEnrollmentRelay(target, pi);
            if (mode == "scope") await Throws<InvalidDataException>(() => relay.RunAsync(relay.CreateAttempt(Device)));
            else await Throws<OperationCanceledException>(() => relay.RunAsync(relay.CreateAttempt(Device)));
            Check(server.Completes == 0, mode + ": stale or wrong-scope result never completes");
        }
    }

    private sealed class PiHttp : HttpMessageHandler
    {
        private string? relayId, capability;
        public int Calls, Cancels;
        public int Writes, Reads;
        public bool Failed;
        public object? FailureCode;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Check(request.Headers.Authorization is null, "Pi never receives administrator bearer");
            string incoming = request.Headers.GetValues("X-Enrollment-Relay").Single();
            capability ??= incoming;
            Check(capability == incoming, "one transport capability per Pi relay job");
            object data;
            if (request.RequestUri!.AbsolutePath.EndsWith("/jobs"))
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                relayId = body.RootElement.GetProperty("relay_id").GetString();
                bool readOnly = body.RootElement.GetProperty("mode").GetString() == "read_result";
                if (readOnly) Reads++; else Writes++;
                Check(body.RootElement.GetProperty("request_envelope").GetString() == (readOnly ? null : Opaque), "request envelope stays opaque/read-only has none");
                data = Failed ? new { relay_id = relayId, state = "failed", error_code = FailureCode }
                    : (object)new { relay_id = relayId, state = "awaiting_approval", challenge = new { probe_nonce = B64(new string('x', 32)) } };
            }
            else
            {
                if (request.RequestUri.AbsolutePath.EndsWith("/cancel")) { Cancels++; capability = null; }
                else
                {
                    using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                    Check(body.RootElement.GetProperty("approval_envelope").GetString() == Opaque, "approval envelope stays opaque");
                }
                data = new { relay_id = relayId, state = "succeeded", result_envelope = Opaque };
            }
            return Json(new { success = true, data });
        }
    }

    private static async Task PiHttpBoundary()
    {
        var handler = new PiHttp(); using var channel = new HttpEnrollmentPiChannel("http://127.0.0.1:8080", handler);
        var job = new EnrollmentJob(JobId, "1", 7, "1", "1", Guid.NewGuid().ToString(), Device,
            "issued", "2026-10-20T01:03:00Z", false, Opaque, null);
        string result = await channel.TransferAsync(job, false, (_, _) => Task.FromResult(Opaque), null, CancellationToken.None);
        Check(result == Opaque && handler.Cancels == 1 && handler.Calls == 3, "Pi async job protocol and final cleanup");
    }

    private static async Task PiFailureReports()
    {
        foreach (object? code in new object?[] { "already_registered", "PRIVATE_UNTRUSTED_DETAIL", 42, null })
        {
            var server = new Server(); using var api = new ApiClient("http://127.0.0.1:5000", server);
            await api.LoginAsync("admin", "synthetic");
            var handler = new PiHttp { Failed = true, FailureCode = code };
            using var channel = new HttpEnrollmentPiChannel("http://127.0.0.1:8080", handler);
            var relay = new FingerprintEnrollmentRelay(new EnrollmentTarget(88, api.CaptureSession(), () => true, CancellationToken.None), channel);
            var attempt = relay.CreateAttempt(Device);
            try { await relay.RunAsync(attempt); throw new Exception("Expected Pi failure"); }
            catch (EnrollmentPiFailureException error)
            {
                Check(error.AlreadyRegistered == Equals(code, "already_registered"), "duplicate-finger display code only");
                Check(!error.Message.Contains("PRIVATE_UNTRUSTED_DETAIL"), "untrusted Pi detail is not displayed");
                if (error.AlreadyRegistered) Check(error.Message.Contains("이미 등록된 지문"), "duplicate reason is readable");
            }
            await Throws<EnrollmentPiFailureException>(() => relay.RunAsync(attempt));
            Check(handler.Writes == 1 && handler.Reads == 1, "failure never authorizes a fresh scan on the same attempt");
            Check(server.Issues == 1 && server.Completes == 0 && handler.Cancels == 2, "failure retains claim/job and performs transport cleanup only");
        }
    }

    private static async Task PythonLoopback(string python)
    {
        var start = new ProcessStartInfo(python) { RedirectStandardInput = true, RedirectStandardOutput = true,
            RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add(Path.GetFullPath("Tests/FingerprintEnrollment.Tests/pi_loopback.py"));
        using var process = Process.Start(start)!;
        try
        {
            string ready = (await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(8)))!;
            using var document = JsonDocument.Parse(ready);
            string address = $"http://127.0.0.1:{document.RootElement.GetProperty("port").GetInt32()}";
            using var audit = new HttpClient();
            var server = new Server { RealProof = true, RequestEnvelope = document.RootElement.GetProperty("request_envelope").GetString()! };
            server.VerifyResult = async result =>
            {
                using var reply = await audit.PostAsync(address + "/test/verify", new StringContent(
                    JsonSerializer.Serialize(new { result_envelope = result }), Encoding.UTF8, "application/json"));
                reply.EnsureSuccessStatusCode();
                using var value = JsonDocument.Parse(await reply.Content.ReadAsStringAsync());
                Check(value.RootElement.GetProperty("data").GetProperty("valid").GetBoolean(), "Python verifies real signed result binding");
                Check(value.RootElement.GetProperty("data").GetProperty("stores").GetInt32() == 1, "Python fake sensor stored exactly once");
            };
            using var api = new ApiClient("http://127.0.0.1:5000", server);
            await api.LoginAsync("admin", "synthetic");
            using var channel = new HttpEnrollmentPiChannel(address);
            var relay = new FingerprintEnrollmentRelay(new EnrollmentTarget(88, api.CaptureSession(), () => true, CancellationToken.None), channel);
            var attempt = relay.CreateAttempt(Device);
            Check((await relay.RunAsync(attempt)).State == "completed", "C# controller -> actual loopback HTTP -> Python broker -> signed approvals -> server completion");
            var saved = server.Results.Single();
            var job = attempt.Job!;
            string replayed = await channel.TransferAsync(job, true, async (body, ct) =>
            {
                if (body.TryGetProperty("action", out _))
                    return EnrollmentValidation.Encode(JsonSerializer.SerializeToUtf8Bytes(server.Physical()));
                Check(body.GetProperty("scope").GetString() == "result_read", "real HTTP recovery has read-only scope");
                var approved = await api.CaptureSession().PostAsync<EnrollmentApproval>($"device-enrollments/{JobId}/approval", body, ct);
                return approved.ApprovalEnvelope;
            }, null, CancellationToken.None);
            Check(replayed == saved, "real HTTP recovery returns identical Python-saved bytes");
        }
        finally
        {
            if (!process.HasExited)
            {
                await process.StandardInput.WriteLineAsync();
                try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (TimeoutException) { process.Kill(entireProcessTree: true); }
            }
        }
    }
}
