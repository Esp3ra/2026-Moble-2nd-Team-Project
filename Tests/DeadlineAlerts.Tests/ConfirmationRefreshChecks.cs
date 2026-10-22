using System.Net;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.DeadlineAlerts;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

internal static partial class Program
{
    private static async Task ConfirmationRefreshChecks()
    {
        // A valid source receipt and a failed current-list GET are different facts.
        using (var source = new Source())
        using (var main = await ShowMain())
        using (var scope = new DeadlineAlertScope(main, source))
        {
            int navigation = 0; scope.TaskRequested += _ => navigation++;
            await scope.CheckAsync();
            source.Confirm = _ => Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Confirmed, null!));
            var saved = await scope.ConfirmAsync("event-A");
            Check(saved.State == DeadlineConfirmationState.Confirmed && saved.Snapshot is null, "receipt success does not require a current snapshot");
            Check(scope.Status.Contains("확인 완료") && scope.Status.Contains("목록 재조회 필요"), "saved receipt has an explicit refresh-needed status");
            Check(scope.Snapshot is null && scope.Unread.Count == 0 && Popups() == 0, "missing snapshot clears usable current facts and closes old popup");
            Check(!scope.NeedsResolution("event-A") && !scope.CanOpenTask, "saved receipt is not unresolved and stale task navigation is disabled");
            Check(!await scope.OpenTaskAsync("event-A") && navigation == 0, "navigation waits for a successful current-list refresh");
            var again = await scope.ConfirmAsync("event-A");
            Check(again.State == DeadlineConfirmationState.Confirmed && source.Confirms == 1 && source.Resolves == 0, "another view cannot POST or resolve a proven confirmation again");

            int gets = 0;
            source.Read = () => { gets++; return Task.FromException<DeadlineAlertSnapshot>(new ApiException("GET unavailable", 503, "UNAVAILABLE")); };
            await scope.CheckAsync();
            Check(gets == 1 && source.Confirms == 1 && !scope.NeedsResolution("event-A"), "refresh failure retries GET without changing known write success");
            Check(scope.Status.Contains("확인 완료") && scope.NextInterval == TimeSpan.FromMinutes(2), "GET failure preserves confirmation status and backoff");
            source.Read = () => Task.FromException<DeadlineAlertSnapshot>(new InvalidDataException("malformed list"));
            await scope.CheckAsync();
            Check(scope.Status.Contains("확인 완료") && scope.NextInterval == TimeSpan.FromMinutes(4), "malformed GET is not not-recorded or a normal empty result");

            // The authoritative full snapshot contains another task too.
            source.Read = null;
            source.Items = [Entry() with { IsConfirmed = true }, Entry("event-other") with { TaskId = "1" }];
            await scope.CheckAsync();
            Check(scope.Snapshot?.Items.Count == 2 && scope.Unread.Single().EventToken == "event-other", "GET recovery preserves the entire list and only the exact event is confirmed");
            Check(scope.CanOpenTask && scope.NextInterval == TimeSpan.FromSeconds(60) && !scope.Status.Contains("재조회 필요"), "successful full refresh restores current navigation and normal polling");
            Check(source.Confirms == 1 && source.Resolves == 0, "GET recovery never creates a replacement confirmation operation");
            ClosePopups();

            // A -> B -> A uses a new source token, even for the same date/account.
            source.Items = [Entry("event-A-again") with { RowVersion = "3" }];
            await scope.CheckAsync();
            Check(scope.Unread.Single().EventToken == "event-A-again" && Popups() == 1, "saved old event does not suppress a later event with the same due date");
            await scope.ConfirmAsync("event-A");
            Check(source.Confirms == 1 && scope.Unread.Single().EventToken == "event-A-again", "old success lookup cannot confirm the newer event");
        }

        // Even an invalid follow-up snapshot must not erase a valid source receipt.
        foreach (var invalid in new[] {
            new DeadlineAlertSnapshot(At(21), null!),
            Snap(At(21), new DeadlineAlert[] { null! }),
            Snap(At(21), Entry()), // contradicts the just-proven account confirmation
            Snap(At(21), Entry() with { IsConfirmed = true, RecipientId = 3 }),
            Snap(At(21), Entry() with { IsConfirmed = true }, Entry("duplicate-task")) })
        {
            using var source = new Source(); using var main = await ShowMain(); using var scope = new DeadlineAlertScope(main, source);
            await scope.CheckAsync();
            source.Confirm = _ => Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Confirmed, invalid));
            var saved = await scope.ConfirmAsync("event-A");
            Check(saved.State == DeadlineConfirmationState.Confirmed && saved.Snapshot is null && scope.Snapshot is null, "invalid refresh data is discarded without downgrading a successful receipt");
            Check(!scope.NeedsResolution("event-A") && scope.Status.Contains("재조회 필요"), "invalid snapshot never triggers result lookup or another POST");
            await scope.ConfirmAsync("event-A");
            Check(source.Confirms == 1 && source.Resolves == 0, "invalid refresh data keeps original successful operation proof");
        }

        // First response is lost; read-only result recovery later proves success.
        using (var source = new Source())
        using (var main = await ShowMain())
        using (var scope = new DeadlineAlertScope(main, source))
        {
            await scope.CheckAsync(); ClosePopups();
            source.Confirm = _ => Task.FromException<DeadlineConfirmation>(new HttpRequestException("response lost"));
            try { await scope.ConfirmAsync("event-A"); throw new Exception("lost response accepted"); }
            catch (HttpRequestException) { Check(scope.NeedsResolution("event-A"), "lost POST remains unresolved"); }
            foreach (int code in new[] { 503, 410 })
            {
                source.Resolve = _ => Task.FromException<DeadlineConfirmation>(new ApiException("reader unavailable/expired", code, "UNAVAILABLE"));
                try { await scope.ConfirmAsync("event-A"); throw new Exception("error treated as no record"); }
                catch (ApiException) { Check(scope.NeedsResolution("event-A") && source.Confirms == 1, "result reader error cannot become not-recorded or automatic resubmission " + code); }
            }
            source.Resolve = _ => Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Confirmed, null!));
            var saved = await scope.ConfirmAsync("event-A");
            Check(saved.State == DeadlineConfirmationState.Confirmed && !scope.NeedsResolution("event-A"), "successful retained receipt resolves uncertainty despite failed refresh");
            Check(source.Confirms == 1 && source.Resolves == 3 && scope.Status.Contains("재조회 필요"), "result lookup success leaves only GET recovery outstanding");
            source.Items = [Entry() with { IsConfirmed = true }]; await scope.CheckAsync();
            Check(scope.Snapshot is not null && scope.Unread.Count == 0 && source.Confirms == 1, "resolved receipt recovers through GET only");
        }

        // Optional data is not permission to treat an unknown/malformed outcome as success.
        foreach (var state in new[] { DeadlineConfirmationState.Unknown, DeadlineConfirmationState.NotRecorded,
            DeadlineConfirmationState.Changed, (DeadlineConfirmationState)999 })
        {
            using var source = new Source(); using var main = await ShowMain(); using var scope = new DeadlineAlertScope(main, source);
            await scope.CheckAsync(); ClosePopups();
            source.Confirm = _ => Task.FromResult(new DeadlineConfirmation(state, null!));
            try { await scope.ConfirmAsync("event-A"); throw new Exception("invalid result accepted"); }
            catch (InvalidDataException) { Check(scope.NeedsResolution("event-A"), "missing current data/invalid state remains uncertain " + state); }
            source.Resolve = _ => Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Unknown, Snap(source.Now, Entry())));
            await scope.ConfirmAsync("event-A");
            Check(source.Confirms == 1 && source.Resolves == 1 && scope.NeedsResolution("event-A"), "malformed result never creates a success cache or a replacement write " + state);
        }

        // A pre-confirmation list cannot restore stale facts after receipt success.
        using (var source = new Source())
        using (var main = await ShowMain())
        using (var scope = new DeadlineAlertScope(main, source))
        {
            await scope.CheckAsync(); ClosePopups();
            var held = new TaskCompletionSource<DeadlineAlertSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.Read = () => held.Task; var old = scope.CheckAsync();
            source.Confirm = _ => Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Confirmed, null!));
            await scope.ConfirmAsync("event-A"); held.SetResult(Snap(source.Now, Entry())); await old;
            Check(scope.Snapshot is null && scope.Unread.Count == 0 && !scope.NeedsResolution("event-A"), "late pre-confirm GET cannot restore a confirmed popup or usable facts");
        }

        // The real inbox keeps success visible while offering a separate refresh.
        using (var source = new Source())
        using (var main = await ShowMain())
        using (var scope = new DeadlineAlertScope(main, source))
        {
            await scope.CheckAsync(); ClosePopups();
            using var inbox = new DeadlineAlertInboxForm(scope); await Show(inbox, main);
            source.Confirm = _ => Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Confirmed, null!));
            Descendants(inbox).OfType<Button>().Single(x => x.Text == "확인").PerformClick();
            Check(Descendants(inbox).OfType<Label>().Single().Text.Contains("확인 완료") && Descendants(inbox).OfType<Label>().Single().Text.Contains("재조회 필요"), "inbox shows stored success and separate refresh failure");
            Check(!Descendants(inbox).OfType<Button>().Single(x => x.Text == "확인").Enabled && Descendants(inbox).OfType<Button>().Single(x => x.Text == "새로고침").Enabled, "inbox disables repeat confirmation and retains GET refresh action");
        }

        using (var source = new Source())
        using (var main = await ShowMain())
        using (var scope = new DeadlineAlertScope(main, source))
        {
            await scope.CheckAsync();
            var popup = Application.OpenForms.OfType<DeadlineAlertForm>().Single();
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            popup.FormClosed += (_, _) => closed.TrySetResult();
            source.Confirm = _ => Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Confirmed, null!));
            Descendants(popup).OfType<Button>().Single(x => x.Text == "확인").PerformClick();
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(scope.Status.Contains("확인 완료") && scope.Snapshot is null && !scope.NeedsResolution("event-A"), "real popup closes on receipt success without requesting another confirmation");
        }

        // Auth failures and changed captured identities remain terminal boundaries.
        foreach (int code in new[] { 401, 403 })
        {
            using var source = new Source(); using var main = await ShowMain(); using var scope = new DeadlineAlertScope(main, source);
            await scope.CheckAsync(); ClosePopups();
            source.Confirm = _ => Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Confirmed, null!));
            await scope.ConfirmAsync("event-A");
            source.Read = () => Task.FromException<DeadlineAlertSnapshot>(new ApiException("revoked", code, "DENIED"));
            await scope.CheckAsync();
            Check(!scope.IsCurrent && scope.Snapshot is null && DeadlineAlertScope.Find(main) is null, "saved receipt cannot bypass current authentication " + code);
        }
        using (var source = new Source())
        using (var oldOwner = await ShowMain())
        using (var oldScope = new DeadlineAlertScope(oldOwner, source))
        {
            await oldScope.CheckAsync(); ClosePopups();
            var held = new TaskCompletionSource<DeadlineConfirmation>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.Confirm = _ => held.Task; var old = oldScope.ConfirmAsync("event-A"); source.End();
            using var nextSource = new Source(); using var nextOwner = await ShowMain(); using var next = new DeadlineAlertScope(nextOwner, nextSource);
            await next.CheckAsync();
            held.SetResult(new(DeadlineConfirmationState.Confirmed, null!));
            try { await old; throw new Exception("old identity success was applied"); } catch (OperationCanceledException) { checks++; }
            Check(next.Unread.Count == 1 && !next.NeedsResolution("event-A") && nextSource.Confirms == 0, "same-account new login does not inherit old in-memory confirmation proof");
        }
        await CapturedConfirmationChecks();
    }

    private static async Task CapturedConfirmationChecks()
    {
        // Use member1's actual captured-session wrapper, without modifying it or
        // inventing a deadline endpoint. Only login uses an explicit HTTP handler.
        using var api = new ApiClient("http://127.0.0.1:1", new ConfirmationLoginHandler());
        await api.LoginAsync("synthetic", "synthetic");
        using var source = new Source(); using var owner = await ShowMain();
        using var host = new LoginDeadlineNotifications(owner, api.CaptureSession(), source, () => _ => Task.FromResult(false));
        await host.StartAsync(); ClosePopups();
        source.Confirm = _ => Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Confirmed));
        var saved = await host.Scope.ConfirmAsync("event-A");
        Check(saved.State == DeadlineConfirmationState.Confirmed && saved.Snapshot is null && host.Scope.IsCurrent,
            "PR64 captured source forwards a successful receipt without a current snapshot");
        Check(!host.Scope.CanOpenTask && host.Scope.Status.Contains("재조회 필요"), "captured login cannot navigate stale facts after receipt success");
        await host.Scope.ConfirmAsync("event-A");
        Check(source.Confirms == 1 && source.Resolves == 0, "shared captured scope preserves proven success across menu consumers");
        var held = new TaskCompletionSource<DeadlineAlertSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Read = () => held.Task; var old = host.Scope.CheckAsync();
        await api.LoginAsync("synthetic", "synthetic");
        held.SetResult(Snap(source.Now, Entry() with { IsConfirmed = true })); await old;
        Check(!host.Scope.IsCurrent && host.Scope.Snapshot is null, "PR64 captured session rejects a late refresh after same-account relogin");
        host.Dispose();
        Check(source.IsCurrent, "login wrapper never disposes the borrowed source");
        using var nextSource = new Source();
        using var nextHost = new LoginDeadlineNotifications(owner, api.CaptureSession(), nextSource, () => _ => Task.FromResult(false));
        await nextHost.StartAsync();
        Check(nextHost.Scope.Unread.Count == 1 && nextHost.Scope.IsCurrent, "new captured scope obtains its own current source without old confirmation state");
    }

    private sealed class ConfirmationLoginHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri?.AbsolutePath != "/api/v1/auth/login")
                throw new InvalidOperationException("No deadline API is installed in this test.");
            var data = new LoginResult("synthetic", "Bearer", DateTimeOffset.UtcNow.AddHours(1),
                new UserInfo(2, "synthetic", "합성 직원", "S2", "시험", "employee", true));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "synthetic" }, ApiClient.Json), Encoding.UTF8, "application/json") });
        }
    }
}
