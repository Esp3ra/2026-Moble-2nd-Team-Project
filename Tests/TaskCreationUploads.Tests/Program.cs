using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks.Attachments;
using RemoteWork.WinForms.Infrastructure;

namespace TaskCreationUploads.Tests;

internal static class Program
{
    private const int ChunkBytes = 16 * 1024;
    private const string UploadId = "00112233-4455-6677-8899-aabbccddeeff";
    private const string OtherId = "11223344-5566-7788-99aa-bbccddeeff00";
    private const string UploadRoot = "/api/v1/results/shared/uploads/";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(15);
    private static int checks;
    private static int scenarios;

    private static async Task<int> Main(string[] args)
    {
        if (args.Contains("--http", StringComparer.Ordinal))
            return await UploadHttpChecks.RunAsync(Console.In.ReadToEnd());

        try
        {
            await Run("immutable file snapshot and actual wire chunks", SnapshotAndWireAsync);
            await Run("empty file uses start and complete only", EmptyFileAsync);
            await Run("preparation is local and rejects oversize/cancellation", PreparationBoundariesAsync);
            await Run("strict start metadata and duplicate fields", InvalidStartResponsesAsync);
            await Run("strict completion metadata", InvalidCompleteResponsesAsync);
            await Run("chunk acknowledgement offset and shape", InvalidChunkResponsesAsync);
            await Run("lost start is never repeated", LostStartAsync);
            await Run("lost chunk retries the same immutable request", LostChunkAsync);
            await Run("lost complete retries completion only", LostCompleteAsync);
            await Run("explicit errors and authentication expiry", HttpFailuresAsync);
            await Run("concurrent send and repeated completion", ConcurrentSendAsync);
            await Run("caller cancellation by upload phase", CancellationAsync);
            await Run("dispose during send suppresses late completion", DisposeAsync);
            await Run("old session late success and error suppression", SessionChangeAsync);
            Console.WriteLine($"PASS: {checks} checks in {scenarios} controlled HTTP scenarios.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"FAIL after {checks} checks: {ex}");
            return 1;
        }
    }

    private static async Task Run(string name, Func<Task> test)
    {
        await test().WaitAsync(TestTimeout);
        scenarios++;
        Console.WriteLine($"PASS {name}");
    }

    private static void Check(bool value, string reason)
    {
        if (!value) throw new InvalidOperationException(reason);
        checks++;
    }

    private static async Task SnapshotAndWireAsync()
    {
        using var f = await Fixture.CreateAsync();
        // FB EF BE encodes to ++++, exposing accidental \u002B expansion in common JSON.
        byte[] original = Enumerable.Range(0, 2 * ChunkBytes + 17)
            .Select(i => new byte[] { 0xfb, 0xef, 0xbe }[i % 3]).ToArray();
        string path = f.File("검증 자료.bin", original);
        using var attempt = await f.Service.PrepareFileAsync(path);
        Check(f.Handler.Count == 0, "prepare performed an HTTP request");
        Check(attempt.Name == "검증 자료.bin", "preparation did not retain basename only");
        Check(attempt.Size == original.Length, "snapshot size changed");
        Check(attempt.Sha256 == Hash(original), "snapshot SHA256 differs from selected bytes");
        Check(attempt.State == TaskCreationUploadState.Ready && attempt.IsCurrent,
            "prepared current file is not Ready");
        Check(attempt.AttachmentId is null && attempt.CompletedFile is null && attempt.ConfirmedBytes == 0,
            "preparation exposed a remote or completed attachment");
        File.WriteAllBytes(path, [1, 2, 3]);
        QueueHappy(f.Handler, attempt, original);
        await attempt.StartAsync();
        AssertCompleted(attempt, original);
        AssertWire(f.Handler.Requests, attempt, original);
        int sent = f.Handler.Count;
        await attempt.StartAsync();
        await attempt.RetryAsync();
        Check(f.Handler.Count == sent, "completed attempt generated another request");
        Check(attempt.State == TaskCreationUploadState.Completed, "completed retry changed success state");
    }

    private static async Task EmptyFileAsync()
    {
        using var f = await Fixture.CreateAsync();
        using var attempt = await f.PrepareAsync([]);
        QueueHappy(f.Handler, attempt, []);
        await attempt.StartAsync();
        AssertCompleted(attempt, []);
        AssertWire(f.Handler.Requests, attempt, []);
        Check(f.Handler.Count == 2, "empty upload sent a chunk or omitted a phase");
    }

    private static async Task PreparationBoundariesAsync()
    {
        using var f = await Fixture.CreateAsync();
        string path = f.File("size-limit.bin", []);
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write))
            stream.SetLength(25L * 1024 * 1024 + 1);
        await ThrowsAsync<ArgumentException>(() => f.Service.PrepareFileAsync(path), "25MiB+1 accepted");
        Check(f.Handler.Count == 0, "oversize preparation called server");
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write))
            stream.SetLength(25L * 1024 * 1024);
        using (var atLimit = await f.Service.PrepareFileAsync(path))
        {
            Check(atLimit.State == TaskCreationUploadState.Ready && atLimit.Size == 25L * 1024 * 1024,
                "exactly 25MiB was not accepted as a prepared file");
            Check(f.Handler.Count == 0 && atLimit.AttachmentId is null && atLimit.CompletedFile is null,
                "exactly 25MiB preparation made HTTP or exposed a remote attachment");
        }
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        string valid = f.File("cancelled.bin", [1, 2, 3]);
        await ThrowsAsync<OperationCanceledException>(
            () => f.Service.PrepareFileAsync(valid, cancellation.Token), "cancelled preparation succeeded");
        Check(f.Handler.Count == 0, "cancelled preparation called server");
        using var attempt = await f.Service.PrepareFileAsync(valid);
        await attempt.StartAsync(cancellation.Token);
        await attempt.RetryAsync(cancellation.Token);
        Check(attempt.State == TaskCreationUploadState.Ready && f.Handler.Count == 0,
            "already cancelled command mutated Ready or sent HTTP");
        await attempt.RetryAsync();
        Check(f.Handler.Count == 0 && attempt.State == TaskCreationUploadState.Ready,
            "Retry on a new file started a new upload");
        f.Api.ClearIdentity();
        Check(!f.Service.IsCurrent, "logout left preparation service current");
        await ThrowsAsync<OperationCanceledException>(() => f.Service.PrepareFileAsync(valid),
            "old session prepared a new file");
        Check(f.Handler.Count == 0, "old service called server while preparing");
    }

    private static async Task InvalidStartResponsesAsync()
    {
        foreach (string kind in new[] { "id", "name", "size", "hash", "size-type", "missing",
                     "duplicate", "duplicate-case", "unknown", "malformed", "null" })
        {
            using var f = await Fixture.CreateAsync();
            using var attempt = await f.PrepareAsync([1, 2, 3]);
            f.Handler.Raw(BadMetadata(attempt, kind, false), 201);
            await attempt.StartAsync();
            Check(attempt.State == TaskCreationUploadState.StartUncertain, $"invalid start {kind} was accepted");
            Check(attempt.AttachmentId is null && attempt.CompletedFile is null && attempt.ConfirmedBytes == 0,
                $"invalid start {kind} adopted unvalidated metadata");
            Check(!string.IsNullOrWhiteSpace(attempt.Message), $"invalid start {kind} lacks explanation");
            await attempt.StartAsync();
            await attempt.RetryAsync();
            Check(f.Handler.Count == 1, $"invalid start {kind} created another upload");
        }
    }

    private static async Task InvalidCompleteResponsesAsync()
    {
        foreach (string kind in new[] { "id", "name", "size", "hash", "size-type", "missing",
                     "duplicate", "duplicate-case", "unknown", "malformed", "null" })
        {
            using var f = await Fixture.CreateAsync();
            byte[] bytes = [7, 8, 9];
            using var attempt = await f.PrepareAsync(bytes);
            f.Handler.Success(Metadata(attempt), 201);
            f.Handler.Success(new { attachment_id = UploadId, next_offset = bytes.Length });
            f.Handler.Raw(BadMetadata(attempt, kind, true));
            await attempt.StartAsync();
            Check(attempt.State == TaskCreationUploadState.NeedsRetry, $"invalid complete {kind} was accepted");
            Check(attempt.CompletedFile is null && attempt.AttachmentId == UploadId,
                $"invalid complete {kind} exposed completed attachment or replaced ID");
            Check(attempt.ConfirmedBytes == bytes.Length && f.Handler.Count == 3,
                $"invalid complete {kind} lost acknowledged progress or retried automatically");
            f.Handler.Success(Metadata(attempt));
            await attempt.RetryAsync();
            Check(attempt.State == TaskCreationUploadState.Completed && f.Handler.Count == 4,
                $"invalid complete {kind} could not retry completion");
            Check(f.Handler.Requests.Last().Path == UploadRoot + UploadId + "/complete",
                $"invalid complete {kind} retried a different phase");
        }
    }

    private static async Task InvalidChunkResponsesAsync()
    {
        string[] badChunks =
        [
            $"{{\"attachment_id\":\"{UploadId}\",\"next_offset\":0}}",
            $"{{\"attachment_id\":\"{UploadId}\",\"next_offset\":4}}",
            $"{{\"attachment_id\":\"{OtherId}\",\"next_offset\":3}}",
            $"{{\"attachment_id\":\"{UploadId}\",\"next_offset\":\"3\"}}",
            $"{{\"attachment_id\":\"{UploadId}\",\"next_offset\":3,\"next_offset\":3}}",
            $"{{\"attachment_id\":\"{UploadId}\",\"next_offset\":3,\"extra\":true}}",
            "{}", "null", "["
        ];
        foreach (string bad in badChunks)
        {
            using var f = await Fixture.CreateAsync();
            using var attempt = await f.PrepareAsync([7, 8, 9]);
            f.Handler.Success(Metadata(attempt), 201);
            f.Handler.Raw(Envelope(bad));
            await attempt.StartAsync();
            Check(attempt.State == TaskCreationUploadState.NeedsRetry, "bad chunk ack did not require explicit retry");
            Check(attempt.ConfirmedBytes == 0 && attempt.CompletedFile is null,
                "bad chunk ack advanced progress or exposed a completed attachment");
            Check(f.Handler.Count == 2, "bad chunk ack retried itself");
            f.Handler.Success(new { attachment_id = UploadId, next_offset = 3 });
            f.Handler.Success(Metadata(attempt));
            await attempt.RetryAsync();
            Check(f.Handler.Count == 4 && attempt.State == TaskCreationUploadState.Completed,
                "bad chunk acknowledgement could not resume");
            Check(f.Handler.Requests[1].Body == f.Handler.Requests[2].Body &&
                  f.Handler.Requests[1].Path == f.Handler.Requests[2].Path,
                "bad acknowledgement retry changed ID, offset, or chunk bytes");
        }
    }

    private static async Task LostStartAsync()
    {
        using var f = await Fixture.CreateAsync();
        using var attempt = await f.PrepareAsync([1]);
        f.Handler.Drop();
        await attempt.StartAsync();
        Check(attempt.State == TaskCreationUploadState.StartUncertain && attempt.AttachmentId is null,
            "lost start was treated as safe to start again");
        Check(f.Handler.Count == 1, "lost start was retried automatically");
        await attempt.StartAsync();
        await attempt.RetryAsync();
        attempt.Dispose();
        Check(f.Handler.Count == 1, "lost start restarted or disposed with remote DELETE");
    }

    private static async Task LostChunkAsync()
    {
        using var f = await Fixture.CreateAsync();
        byte[] bytes = Enumerable.Range(0, ChunkBytes + 3).Select(i => (byte)i).ToArray();
        using var attempt = await f.PrepareAsync(bytes);
        f.Handler.Success(Metadata(attempt), 201);
        f.Handler.Success(new { attachment_id = UploadId, next_offset = ChunkBytes });
        f.Handler.Drop();
        await attempt.StartAsync();
        Check(attempt.State == TaskCreationUploadState.NeedsRetry && attempt.ConfirmedBytes == ChunkBytes,
            "lost second chunk advanced unacknowledged progress");
        Check(f.Handler.Count == 3 && attempt.AttachmentId == UploadId, "lost chunk created another upload");
        await attempt.StartAsync();
        Check(f.Handler.Count == 3, "Start retried a partially uploaded file");
        f.Handler.Success(new { attachment_id = UploadId, next_offset = bytes.Length });
        f.Handler.Success(Metadata(attempt));
        await attempt.RetryAsync();
        Check(f.Handler.Count == 5, "retry repeated acknowledged chunks or upload start");
        Check(f.Handler.Requests[2].Body == f.Handler.Requests[3].Body &&
              f.Handler.Requests[2].Path == f.Handler.Requests[3].Path,
            "retry changed pending chunk, offset, or UUID");
        AssertCompleted(attempt, bytes);
        AssertHeaders(f.Handler.Requests);
    }

    private static async Task LostCompleteAsync()
    {
        using var f = await Fixture.CreateAsync();
        byte[] bytes = [4, 5, 6];
        using var attempt = await f.PrepareAsync(bytes);
        f.Handler.Success(Metadata(attempt), 201);
        f.Handler.Success(new { attachment_id = UploadId, next_offset = bytes.Length });
        f.Handler.Drop();
        await attempt.StartAsync();
        Check(attempt.State == TaskCreationUploadState.NeedsRetry && attempt.CompletedFile is null,
            "lost completion was exposed as completed");
        Check(f.Handler.Count == 3 && attempt.ConfirmedBytes == bytes.Length,
            "completion uncertainty lost progress or retried itself");
        f.Handler.Success(Metadata(attempt));
        await attempt.RetryAsync();
        Check(f.Handler.Count == 4 && f.Handler.Requests[2] == f.Handler.Requests[3],
            "completion retry repeated another phase or changed request");
        AssertCompleted(attempt, bytes);
    }

    private static async Task HttpFailuresAsync()
    {
        foreach (int status in new[] { 400, 404, 409, 413 })
        {
            using var f = await Fixture.CreateAsync();
            using var attempt = await f.PrepareAsync([1]);
            f.Handler.Error(status, "SYNTHETIC_REJECTION");
            await attempt.StartAsync();
            Check(attempt.State == TaskCreationUploadState.Rejected, $"HTTP {status} remained retryable");
            Check(attempt.CompletedFile is null, $"HTTP {status} exposed completed attachment");
            await attempt.RetryAsync();
            await attempt.StartAsync();
            Check(f.Handler.Count == 1, $"HTTP {status} caused a new upload");
        }
        foreach (int status in new[] { 401, 403 })
        foreach (string phase in new[] { "start", "chunk", "complete" })
        {
            using var f = await Fixture.CreateAsync();
            using var attempt = await f.PrepareAsync([1]);
            int expired = 0;
            f.Api.AuthenticationExpired += (_, _) => expired++;
            if (phase != "start") f.Handler.Success(Metadata(attempt), 201);
            if (phase == "complete") f.Handler.Success(new { attachment_id = UploadId, next_offset = 1 });
            f.Handler.Error(status, status == 401 ? "AUTH_REQUIRED" : "FORBIDDEN");
            await attempt.StartAsync();
            Check(attempt.State == TaskCreationUploadState.Disconnected && attempt.CompletedFile is null &&
                  attempt.AttachmentId is null && attempt.ConfirmedBytes == 0,
                $"HTTP {status} during {phase} did not disconnect upload or clear remote state");
            Check(status == 401 ? f.Api.CurrentUser is null && expired == 1 : f.Api.CurrentUser is not null && expired == 0,
                $"HTTP {status} affected authentication incorrectly");
            int count = f.Handler.Count;
            await attempt.RetryAsync();
            await attempt.StartAsync();
            Check(f.Handler.Count == count, $"HTTP {status} during {phase} uploaded again");
        }
        using (var f = await Fixture.CreateAsync())
        using (var attempt = await f.PrepareAsync([1]))
        {
            f.Handler.Error(503, "DATABASE_UNAVAILABLE");
            await attempt.StartAsync();
            Check(attempt.State == TaskCreationUploadState.StartUncertain, "503 start was considered safe to repeat");
            await attempt.RetryAsync();
            Check(f.Handler.Count == 1, "503 start retried without a known ID");
        }
    }

    private static async Task ConcurrentSendAsync()
    {
        using var f = await Fixture.CreateAsync();
        using var attempt = await f.PrepareAsync([7, 8, 9]);
        var gate = new ResponseGate();
        f.Handler.Next(gate.SendAsync);
        f.Handler.Success(new { attachment_id = UploadId, next_offset = 3 });
        f.Handler.Success(Metadata(attempt));
        Task first = attempt.StartAsync();
        await gate.Entered.Task.WaitAsync(TestTimeout);
        Check(attempt.State == TaskCreationUploadState.Sending && attempt.CompletedFile is null,
            "in-flight start was not Sending");
        await attempt.StartAsync();
        await attempt.RetryAsync();
        Check(f.Handler.Count == 1, "concurrent clicks dispatched extra HTTP");
        gate.Release(Handler.Response(Metadata(attempt), 201));
        await first;
        Check(f.Handler.Count == 3, "concurrent send performed unexpected stages");
        await attempt.StartAsync();
        await attempt.RetryAsync();
        Check(f.Handler.Count == 3 && attempt.State == TaskCreationUploadState.Completed,
            "completed attempt was sent again");
    }

    private static async Task CancellationAsync()
    {
        foreach (string phase in new[] { "start", "chunk", "complete" })
        {
            using var f = await Fixture.CreateAsync();
            using var attempt = await f.PrepareAsync([7, 8, 9]);
            using var cancelled = new CancellationTokenSource();
            if (phase != "start") f.Handler.Success(Metadata(attempt), 201);
            if (phase == "complete") f.Handler.Success(new { attachment_id = UploadId, next_offset = 3 });
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            f.Handler.Next(async (_, ct) =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.Infinite, ct);
                throw new InvalidOperationException("unreachable cancelled transport");
            });
            Task sending = attempt.StartAsync(cancelled.Token);
            await entered.Task.WaitAsync(TestTimeout);
            cancelled.Cancel();
            await sending;
            int count = f.Handler.Count;
            Check(attempt.State == (phase == "start" ? TaskCreationUploadState.StartUncertain : TaskCreationUploadState.NeedsRetry),
                $"cancelled {phase} lost its uncertainty boundary");
            Check(attempt.CompletedFile is null, $"cancelled {phase} completed upload");
            await attempt.RetryAsync(cancelled.Token);
            Check(f.Handler.Count == count, $"cancelled retry during {phase} sent HTTP");
            if (phase != "start")
            {
                if (phase == "chunk") f.Handler.Success(new { attachment_id = UploadId, next_offset = 3 });
                f.Handler.Success(Metadata(attempt));
                await attempt.RetryAsync();
                Check(attempt.State == TaskCreationUploadState.Completed, $"cancelled {phase} cannot explicitly resume");
            }
        }
    }

    private static async Task DisposeAsync()
    {
        foreach (string phase in new[] { "start", "chunk", "complete" })
        {
            using var f = await Fixture.CreateAsync();
            using var attempt = await f.PrepareAsync([1]);
            if (phase == "chunk") f.Handler.Success(Metadata(attempt), 201);
            if (phase == "complete")
            {
                f.Handler.Success(Metadata(attempt), 201);
                f.Handler.Success(new { attachment_id = UploadId, next_offset = 1 });
            }
            var gate = new ResponseGate();
            f.Handler.Next(gate.SendAsync);
            Task sending = attempt.StartAsync();
            await gate.Entered.Task.WaitAsync(TestTimeout);
            int count = f.Handler.Count;
            attempt.Dispose();
            Check(gate.Token.IsCancellationRequested, $"dispose did not cancel {phase} transport");
            gate.Release(Handler.Response(phase == "chunk"
                ? new { attachment_id = UploadId, next_offset = 1 } : Metadata(attempt),
                phase == "start" ? 201 : 200));
            await sending;
            Check(attempt.State == TaskCreationUploadState.Disposed && attempt.CompletedFile is null,
                $"late {phase} response revived disposed upload");
            await attempt.StartAsync();
            await attempt.RetryAsync();
            Check(f.Handler.Count == count, "disposed upload sent another request or DELETE");
        }
    }

    private static async Task SessionChangeAsync()
    {
        foreach (bool error in new[] { false, true })
        foreach (string phase in new[] { "start", "complete" })
        {
            using var f = await Fixture.CreateAsync();
            using var attempt = await f.PrepareAsync([1]);
            if (phase == "complete")
            {
                f.Handler.Success(Metadata(attempt), 201);
                f.Handler.Success(new { attachment_id = UploadId, next_offset = 1 });
            }
            var gate = new ResponseGate();
            f.Handler.Next(gate.SendAsync);
            int expired = 0;
            f.Api.AuthenticationExpired += (_, _) => expired++;
            Task sending = attempt.StartAsync();
            await gate.Entered.Task.WaitAsync(TestTimeout);
            f.Api.ClearIdentity();
            Check(gate.Token.IsCancellationRequested && !f.Service.IsCurrent && !attempt.IsCurrent,
                "logout left an upload session valid");
            f.Api.SetServer("http://localhost:5001/");
            await f.LoginAsync(12, "new-token");
            int count = f.Handler.Count;
            gate.Release(error ? Handler.ErrorResponse(401, "AUTH_EXPIRED") :
                Handler.Response(Metadata(attempt), phase == "start" ? 201 : 200));
            await sending;
            Check(f.Api.CurrentUser?.Id == 12 && expired == 0,
                $"old {phase} {(error ? "401" : "success")} affected new login");
            Check(attempt.State == TaskCreationUploadState.Disconnected && attempt.CompletedFile is null && attempt.AttachmentId is null,
                $"old {phase} response revived attachment state");
            await attempt.RetryAsync();
            await attempt.StartAsync();
            Check(f.Handler.Count == count, "old upload sent bytes through new session");
        }
        using (var f = await Fixture.CreateAsync())
        using (var attempt = await f.PrepareAsync([1]))
        {
            QueueHappy(f.Handler, attempt, [1]);
            await attempt.StartAsync();
            f.Api.ClearIdentity();
            Check(attempt.State == TaskCreationUploadState.Disconnected && attempt.CompletedFile is null && attempt.AttachmentId is null,
                "completed attachment survived the login session");
        }
    }

    private static object Metadata(TaskCreationUploadAttempt attempt) =>
        new { attachment_id = UploadId, name = attempt.Name, size = attempt.Size, sha256 = attempt.Sha256 };

    private static string BadMetadata(TaskCreationUploadAttempt attempt, string kind, bool complete)
    {
        var fields = new Dictionary<string, object?>
        {
            ["attachment_id"] = kind == "id" ? (complete ? OtherId : "invalid-upload-id") : UploadId,
            ["name"] = kind == "name" ? "different.bin" : attempt.Name,
            ["size"] = kind == "size" ? attempt.Size + 1 : kind == "size-type" ? "3" : attempt.Size,
            ["sha256"] = kind == "hash" ? new string('0', 64) : attempt.Sha256
        };
        if (kind == "missing") fields.Remove("sha256");
        if (kind == "unknown") fields["task_id"] = 123;
        string data = JsonSerializer.Serialize(fields);
        if (kind == "duplicate") data = data[..^1] + $",\"attachment_id\":\"{UploadId}\"}}";
        if (kind == "duplicate-case") data = data[..^1] + $",\"Attachment_Id\":\"{UploadId}\"}}";
        if (kind == "null") data = "null";
        return kind == "malformed" ? "{ invalid envelope" : Envelope(data);
    }

    private static string Envelope(string data) => "{\"ok\":true,\"data\":" + data + ",\"request_id\":\"synthetic\"}";
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void QueueHappy(Handler handler, TaskCreationUploadAttempt attempt, byte[] bytes)
    {
        handler.Success(Metadata(attempt), 201);
        for (int offset = 0; offset < bytes.Length; offset += ChunkBytes)
            handler.Success(new { attachment_id = UploadId, next_offset = Math.Min(offset + ChunkBytes, bytes.Length) });
        handler.Success(Metadata(attempt));
    }

    private static void AssertCompleted(TaskCreationUploadAttempt attempt, byte[] bytes)
    {
        Check(attempt.State == TaskCreationUploadState.Completed, "upload did not complete");
        Check(attempt.AttachmentId == UploadId && attempt.ConfirmedBytes == bytes.Length,
            "completed upload has wrong UUID or progress");
        Check(attempt.CompletedFile == new TaskAttachment(UploadId, attempt.Name, bytes.Length, Hash(bytes)),
            "completed metadata differs from immutable original file");
    }

    private static void AssertWire(IReadOnlyList<Request> requests, TaskCreationUploadAttempt attempt, byte[] bytes)
    {
        Check(requests.Count == 2 + (bytes.Length + ChunkBytes - 1) / ChunkBytes, "unexpected number of upload stages");
        AssertHeaders(requests);
        Check(requests[0].Path == UploadRoot + "start", "start path differs from member3 contract");
        using (var json = JsonDocument.Parse(requests[0].Body))
        {
            Exact(json.RootElement, "name", "size", "sha256");
            Check(json.RootElement.GetProperty("name").GetString() == attempt.Name, "start sent full path or wrong name");
            Check(json.RootElement.GetProperty("size").GetInt64() == bytes.Length, "start sent changed file length");
            Check(json.RootElement.GetProperty("sha256").GetString() == Hash(bytes), "start sent changed file hash");
        }
        int next = 0;
        foreach (Request request in requests.Skip(1).SkipLast(1))
        {
            Check(request.Path == UploadRoot + UploadId + "/chunks", "chunk used another upload UUID");
            Check(Encoding.UTF8.GetByteCount(request.Body) < 32768,
                "actual common serializer expanded base64 beyond 32KiB request budget");
            using var json = JsonDocument.Parse(request.Body);
            Exact(json.RootElement, "offset", "data");
            Check(json.RootElement.GetProperty("offset").GetInt64() == next, "chunk offset has a gap");
            byte[] chunk = Convert.FromBase64String(json.RootElement.GetProperty("data").GetString()!);
            Check(chunk.Length == Math.Min(ChunkBytes, bytes.Length - next), "chunk size differs from 16KiB protocol");
            Check(chunk.AsSpan().SequenceEqual(bytes.AsSpan(next, chunk.Length)), "chunk read mutated original bytes");
            next += chunk.Length;
        }
        Check(next == bytes.Length, "wire data does not reconstruct entire file");
        Check(requests[^1].Path == UploadRoot + UploadId + "/complete", "complete used another upload UUID");
        using var complete = JsonDocument.Parse(requests[^1].Body);
        Exact(complete.RootElement);
    }

    private static void AssertHeaders(IReadOnlyList<Request> requests)
    {
        foreach (Request request in requests)
        {
            Check(request.Method == "POST", "upload consumer sent an unexpected method including DELETE");
            Check(request.Bearer == "synthetic-token", "upload did not retain captured bearer token");
            Check(request.IdempotencyKey is null, "upload incorrectly used a W1 task write key");
            Check(request.ContentType == "application/json; charset=utf-8", "upload was not JSON UTF8");
        }
    }

    private static void Exact(JsonElement element, params string[] keys)
    {
        Check(element.ValueKind == JsonValueKind.Object &&
              element.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(keys.Order()),
            "wire JSON has missing, duplicate, or unexpected fields");
    }

    private static async Task ThrowsAsync<T>(Func<Task> action, string reason) where T : Exception
    {
        try { await action(); }
        catch (T) { checks++; return; }
        throw new InvalidOperationException(reason);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "RemoteWork-upload-tests-" + Guid.NewGuid().ToString("N"));
        private readonly List<string> files = [];
        public Handler Handler { get; } = new();
        public ApiClient Api { get; }
        public HttpTaskCreationUploadService Service { get; private set; } = null!;
        private Fixture()
        {
            Directory.CreateDirectory(directory);
            Api = new ApiClient("http://localhost:5000/", Handler);
        }
        public static async Task<Fixture> CreateAsync()
        {
            var f = new Fixture();
            await f.LoginAsync(2, "synthetic-token");
            f.Service = new HttpTaskCreationUploadService(f.Api.CaptureSession());
            f.Handler.ResetRequests();
            return f;
        }
        public async Task LoginAsync(int id, string token)
        {
            Handler.Success(new
            {
                access_token = token, token_type = "Bearer", expires_at = "2030-01-01T00:00:00Z",
                user = new { id, login_id = "admin", display_name = "관리자", employee_no = "A2", department = "팀", role = "admin", is_active = true }
            });
            await Api.LoginAsync("synthetic", "not-a-real-password");
        }
        public string File(string name, byte[] bytes)
        {
            string path = Path.Combine(directory, name);
            System.IO.File.WriteAllBytes(path, bytes);
            if (!files.Contains(path)) files.Add(path);
            return path;
        }
        public Task<TaskCreationUploadAttempt> PrepareAsync(byte[] bytes) => Service.PrepareFileAsync(File("sample.bin", bytes));
        public void Dispose()
        {
            Api.Dispose();
            foreach (string path in files) System.IO.File.Delete(path);
            Directory.Delete(directory);
        }
    }

    private sealed record Request(string Method, string Path, string Body, string? Bearer, string? IdempotencyKey, string? ContentType);

    private sealed class Handler : HttpMessageHandler
    {
        private readonly object sync = new();
        private readonly Queue<Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>> queue = new();
        private readonly List<Request> requests = [];
        public IReadOnlyList<Request> Requests { get { lock (sync) return requests.ToArray(); } }
        public int Count { get { lock (sync) return requests.Count; } }
        public void ResetRequests() { lock (sync) requests.Clear(); }
        public void Next(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action)
        { lock (sync) queue.Enqueue(action); }
        public void Success(object data, int status = 200) => Next((_, _) => Task.FromResult(Response(data, status)));
        public void Raw(string json, int status = 200) => Next((_, _) => Task.FromResult(RawResponse(json, status)));
        public void Error(int status, string code) => Next((_, _) => Task.FromResult(ErrorResponse(status, code)));
        public void Drop() => Next((_, _) => Task.FromException<HttpResponseMessage>(new HttpRequestException("synthetic response loss")));
        public static HttpResponseMessage Response(object data, int status = 200) =>
            RawResponse(JsonSerializer.Serialize(new { ok = true, data, request_id = "synthetic" }, ApiClient.Json), status);
        public static HttpResponseMessage ErrorResponse(int status, string code) =>
            RawResponse(JsonSerializer.Serialize(new { ok = false, error = new { code, message = "합성 오류" }, request_id = "synthetic" }), status);
        private static HttpResponseMessage RawResponse(string json, int status) => new((HttpStatusCode)status)
        { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> action;
            lock (sync)
            {
                requests.Add(new Request(request.Method.Method, request.RequestUri!.AbsolutePath, body,
                    request.Headers.Authorization?.Parameter,
                    request.Headers.TryGetValues("Idempotency-Key", out var key) ? key.Single() : null,
                    request.Content?.Headers.ContentType?.ToString()));
                if (queue.Count == 0) throw new InvalidOperationException("Unexpected HTTP request in controlled test");
                action = queue.Dequeue();
            }
            return await action(request, ct);
        }
    }

    private sealed class ResponseGate
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<HttpResponseMessage> response = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token { get; private set; }
        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Token = ct;
            Entered.SetResult();
            // Deliberately ignores cancellation to model an already-arriving old response.
            return response.Task;
        }
        public void Release(HttpResponseMessage value) => response.SetResult(value);
    }
}
