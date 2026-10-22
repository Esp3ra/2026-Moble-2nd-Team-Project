using RemoteWork.WinForms.Features.Tasks.Attachments;

namespace TaskCreationUploads.Tests;

internal static partial class Program
{
    private static async Task DiscardWireAsync()
    {
        using var f = await Fixture.CreateAsync();
        using var attempt = await f.PrepareAsync([1, 2, 3]);
        QueueHappy(f.Handler, attempt, [1, 2, 3]);
        await attempt.StartAsync();
        Check(attempt.CanDiscard, "completed temporary upload cannot be explicitly discarded");
        int before = f.Handler.Count;
        f.Handler.Success(new { attachment_id = UploadId });
        await attempt.DiscardAsync();
        Check(f.Handler.Count == before + 1, "discard did not send exactly one request");
        Request request = f.Handler.Requests[^1];
        Check(request.Method == "DELETE" && request.Path == UploadRoot + UploadId,
            "discard used the wrong method or upload identity");
        Check(request.Body == "" && request.ContentType is null && request.IdempotencyKey is null,
            "discard invented a body, content type or task write key");
        Check(request.Bearer == "synthetic-token", "discard lost captured authentication");
        Check(attempt.State == TaskCreationUploadState.Discarded && attempt.CompletedFile is null
            && attempt.AttachmentId == UploadId && !attempt.CanDiscard, "discard did not invalidate file selection");
        before = f.Handler.Count;
        await attempt.StartAsync(); await attempt.RetryAsync(); await attempt.DiscardAsync();
        Check(f.Handler.Count == before, "discarded file could be recreated or discarded repeatedly");
    }

    private static async Task DiscardEligibilityAsync()
    {
        using var f = await Fixture.CreateAsync();
        using var attempt = await f.PrepareAsync([1]);
        Check(!attempt.CanDiscard, "local prepared file claims a remote discard target");
        await attempt.DiscardAsync();
        Check(f.Handler.Count == 0 && attempt.State == TaskCreationUploadState.Ready, "local preparation sent DELETE");
        f.Handler.Drop();
        await attempt.StartAsync();
        int before = f.Handler.Count;
        Check(attempt.State == TaskCreationUploadState.StartUncertain && !attempt.CanDiscard, "unknown start has a discard target");
        await attempt.DiscardAsync();
        Check(f.Handler.Count == before && attempt.State == TaskCreationUploadState.StartUncertain,
            "unknown start discarded a guessed upload");

        foreach (bool rejected in new[] { false, true })
        {
            using var other = await Fixture.CreateAsync();
            using var known = await other.PrepareAsync([1]);
            other.Handler.Success(Metadata(known), 201);
            if (rejected) other.Handler.Error(409, "UPLOAD_CHUNK_CONFLICT");
            else other.Handler.Drop();
            await known.StartAsync();
            Check(known.State == (rejected ? TaskCreationUploadState.Rejected : TaskCreationUploadState.NeedsRetry)
                && known.CanDiscard, "known incomplete file cannot be explicitly discarded");
            other.Handler.Success(new { attachment_id = UploadId });
            await known.DiscardAsync();
            Check(known.State == TaskCreationUploadState.Discarded, "known incomplete discard failed");
        }
    }

    private static async Task DiscardRecoveryAsync()
    {
        foreach (string response in new[] { "loss", "wrong-id", "missing", "extra", "duplicate", "type", "json", "generic404", "server503" })
        {
            using var f = await Fixture.CreateAsync();
            using var attempt = await f.PrepareAsync([]);
            QueueHappy(f.Handler, attempt, []);
            await attempt.StartAsync();
            switch (response)
            {
                case "loss": f.Handler.Drop(); break;
                case "wrong-id": f.Handler.Success(new { attachment_id = OtherId }); break;
                case "missing": f.Handler.Success(new { }); break;
                case "extra": f.Handler.Success(new { attachment_id = UploadId, deleted = true }); break;
                case "duplicate": f.Handler.Raw("{\"ok\":true,\"data\":{\"attachment_id\":\"" + UploadId
                    + "\",\"attachment_id\":\"" + UploadId + "\"}}"); break;
                case "type": f.Handler.Success(new { attachment_id = 7 }); break;
                case "json": f.Handler.Raw("not-json"); break;
                case "generic404": f.Handler.Error(404, "NOT_FOUND"); break;
                default: f.Handler.Error(503, "DATABASE_UNAVAILABLE"); break;
            }
            int before = f.Handler.Count;
            await attempt.DiscardAsync();
            Check(f.Handler.Count == before + 1, "ambiguous discard automatically retried");
            Check(attempt.State == TaskCreationUploadState.DiscardUncertain && attempt.CompletedFile is null
                && attempt.AttachmentId == UploadId && attempt.CanDiscard, "ambiguous discard lost its original target");
            before = f.Handler.Count;
            await attempt.StartAsync(); await attempt.RetryAsync();
            Check(f.Handler.Count == before, "discard ambiguity resumed an upload");
            f.Handler.Error(404, "ATTACHMENT_NOT_FOUND");
            await attempt.DiscardAsync();
            Check(attempt.State == TaskCreationUploadState.Discarded && attempt.CompletedFile is null,
                "precise provider absence was not recognized");
            Check(f.Handler.Count == before + 1 && f.Handler.Requests[^1].Path == UploadRoot + UploadId,
                "explicit discard recovery used a different target");
        }
    }

    private static async Task DiscardFailuresAsync()
    {
        foreach (int status in new[] { 400, 409, 413, 401, 403 })
        {
            using var f = await Fixture.CreateAsync();
            using var attempt = await f.PrepareAsync([]);
            QueueHappy(f.Handler, attempt, []);
            await attempt.StartAsync();
            f.Handler.Error(status, "SYNTHETIC_DENIAL");
            int before = f.Handler.Count;
            await attempt.DiscardAsync();
            Check(attempt.StatusCode == status && attempt.ErrorCode == "SYNTHETIC_DENIAL", "discard lost error identity");
            Check(attempt.CompletedFile is null && f.Handler.Count == before + 1, "discard failure retained a usable file or retried");
            if (status is 401 or 403)
            {
                Check(attempt.State == TaskCreationUploadState.Disconnected && !attempt.CanDiscard
                    && attempt.AttachmentId is null, "unauthorized discard left a reusable target");
                await f.LoginAsync(8, "new-token");
                before = f.Handler.Count;
                await attempt.DiscardAsync();
                Check(f.Handler.Count == before, "old discard crossed login boundary");
            }
            else
            {
                Check(attempt.State == TaskCreationUploadState.DiscardRejected && attempt.CanDiscard
                    && attempt.AttachmentId == UploadId, "definite discard rejection was treated as success");
                f.Handler.Success(new { attachment_id = UploadId });
                await attempt.DiscardAsync();
                Check(attempt.State == TaskCreationUploadState.Discarded, "explicit retry of rejected discard failed");
            }
        }
    }

    private static async Task DiscardConcurrencyAsync()
    {
        using var f = await Fixture.CreateAsync();
        using var attempt = await f.PrepareAsync([]);
        QueueHappy(f.Handler, attempt, []);
        await attempt.StartAsync();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        int before = f.Handler.Count;
        await attempt.DiscardAsync(cancelled.Token);
        Check(f.Handler.Count == before && attempt.State == TaskCreationUploadState.Completed
            && attempt.CompletedFile is not null && attempt.CanDiscard, "pre-cancelled discard mutated state");
        var gate = new ResponseGate();
        f.Handler.Next(gate.SendAsync);
        Task deleting = attempt.DiscardAsync();
        await gate.Entered.Task.WaitAsync(TestTimeout);
        Check(attempt.State == TaskCreationUploadState.Discarding && !attempt.CanDiscard && attempt.CompletedFile is null,
            "in-flight deletion remained selectable or discardable");
        before = f.Handler.Count;
        await attempt.DiscardAsync(); await attempt.StartAsync(); await attempt.RetryAsync();
        Check(f.Handler.Count == before, "concurrent action escaped the discard lock");
        gate.Release(Handler.Response(new { attachment_id = UploadId }));
        await deleting;
        Check(attempt.State == TaskCreationUploadState.Discarded, "discard completion lost state");

        using var other = await Fixture.CreateAsync();
        using var interrupted = await other.PrepareAsync([]);
        QueueHappy(other.Handler, interrupted, []);
        await interrupted.StartAsync();
        using var stop = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        other.Handler.Next(async (_, ct) =>
        {
            entered.SetResult();
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("unreachable cancelled discard");
        });
        deleting = interrupted.DiscardAsync(stop.Token);
        await entered.Task.WaitAsync(TestTimeout); stop.Cancel(); await deleting;
        Check(interrupted.State == TaskCreationUploadState.DiscardUncertain && interrupted.CanDiscard
            && interrupted.AttachmentId == UploadId && interrupted.CompletedFile is null,
            "cancelled sent discard was treated as unstarted or complete");
    }

    private static async Task DiscardLifetimeAsync()
    {
        foreach (string ending in new[] { "dispose", "logout-success", "logout-401" })
        {
            using var f = await Fixture.CreateAsync();
            using var attempt = await f.PrepareAsync([]);
            QueueHappy(f.Handler, attempt, []);
            await attempt.StartAsync();
            var gate = new ResponseGate();
            f.Handler.Next(gate.SendAsync);
            Task deleting = attempt.DiscardAsync();
            await gate.Entered.Task.WaitAsync(TestTimeout);
            int expired = 0;
            f.Api.AuthenticationExpired += (_, _) => expired++;
            if (ending == "dispose") attempt.Dispose();
            else
            {
                f.Api.ClearIdentity();
                f.Api.SetServer("http://localhost:5001/");
                await f.LoginAsync(9, "new-token");
            }
            Check(gate.Token.IsCancellationRequested, "discard lifetime change did not cancel transport");
            int before = f.Handler.Count;
            gate.Release(ending == "logout-401" ? Handler.ErrorResponse(401, "AUTH_EXPIRED")
                : Handler.Response(new { attachment_id = UploadId }));
            await deleting;
            Check(attempt.State == (ending == "dispose" ? TaskCreationUploadState.Disposed : TaskCreationUploadState.Disconnected)
                && attempt.CompletedFile is null && attempt.AttachmentId is null && !attempt.CanDiscard,
                "late DELETE response revived disposed or old-session file");
            await attempt.DiscardAsync(); await attempt.StartAsync(); await attempt.RetryAsync();
            Check(f.Handler.Count == before, "late discard restarted work in another lifetime");
            if (ending != "dispose") Check(f.Api.CurrentUser?.Id == 9 && expired == 0, "old delete expired new login");
        }
    }
}
