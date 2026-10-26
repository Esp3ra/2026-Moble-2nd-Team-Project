using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Features.Tasks.ReviewRequests;
using RemoteWork.WinForms.Infrastructure;

// Real controls and shared ApiClient, with explicit HTTP doubles only.
// Does not use product accounts, databases, server processes or business writes.
internal static class Program
{
    private const string Target = "9007199254740993";
    private const string Submission = "9007199254740995";
    private const string Original = "  private 원문 <script>표시</script> 😀\r\n끝  ";
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-26T03:00:00Z");
    private static int checks;
    private static string? captures;

    [STAThread]
    private static int Main(string[] args)
    {
        captures = args.FirstOrDefault();
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Control.CheckForIllegalCrossThreadCalls = true;
        int code = 1;
        using var dispatcher = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual,
            Location = new(-20000, -20000), Size = new(1, 1) };
        dispatcher.Shown += (_, _) => dispatcher.BeginInvoke((Action)(() =>
        {
            try
            {
                WireAndRecovery(); MalformedKeysAndResponses(); AcknowledgementIdentity(); DefinitiveFailureAndExpiry();
                StoresAndSessions(); InFlightExclusionAndLateSession();
                ActualDialogReopen(); PermissionLossScreens(); InboxPermissionsAndAcknowledgement(); StatusLayout();
                Console.WriteLine($"PASS {checks} manual review checks (actual WinForms + HTTP doubles; no server/DB).");
                code = 0;
            }
            catch (Exception error) { Console.Error.WriteLine($"FAIL after {checks}: {error}"); }
            finally { dispatcher.Close(); }
        }));
        Application.Run(dispatcher);
        return code;
    }

    private static void Check(bool value, string why)
    { if (!value) throw new InvalidOperationException(why); checks++; }
    private static void Until(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(12);
        while (!ready() && DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(1); }
        if (!ready()) throw new TimeoutException("WinForms/HTTP operation did not complete");
    }
    private static void Wait(Task task) { Until(() => task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static T Wait<T>(Task<T> task) { Until(() => task.IsCompleted); return task.GetAwaiter().GetResult(); }
    private static void Throws<T>(Action action, string why) where T : Exception
    {
        try { action(); }
        catch (T) { Check(true, why); return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name + ": " + why);
    }
    private static T Field<T>(object owner, string name) =>
        (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
    private static ReviewRequestContext Context() => new(Target, "private 업무 제목", "submitted",
        new DateOnly(2026, 10, 30), "9007199254740994", 3, Submission, "2", "private 제출 결과", 2, "지정 검토자", null);
    private static ReviewRequestItem Item(string state = "pending") => new("9", Target, "private 업무 제목", Submission,
        "2", 3, 7, "요청 직원", 2, "지정 검토자", Original, state, Now,
        state == "acknowledged" ? Now : null, state == "acknowledged" ? 2 : null, "9007199254740994", "submitted");
    private static ReviewRequestSaved Saved(bool acknowledged = false) => new("9", Target, Submission,
        "2", acknowledged ? "acknowledged" : "pending", Now, acknowledged ? Now : null);
    private static void Login(ApiClient api, Handler handler, int user = 7)
    {
        handler.Ok(new { access_token = "owned-test-token-" + user, token_type = "Bearer", expires_at = "2030-01-01T00:00:00Z",
            user = new { id = user, login_id = "test" + user, display_name = "테스트", employee_no = "T" + user,
                department = "TEST", role = user == 2 ? "admin" : "employee", is_active = true } });
        Wait(api.LoginAsync("synthetic", "owned-test-only"));
    }
    private sealed class Fixture : IDisposable
    {
        internal readonly Handler Handler = new();
        internal readonly ApiClient Api;
        internal TaskReviewRequestsClient Client => TaskReviewRequestsClient.For(Api);
        internal Fixture(int user = 7) { Api = new("http://localhost:5000/", Handler); Login(Api, Handler, user); }
        public void Dispose() => Api.Dispose();
    }
    private sealed record Recorded(string Method, string Path, string Query, string Body, string? Key, string? Bearer);
    private sealed class Handler : HttpMessageHandler
    {
        private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> steps = new();
        internal readonly List<Recorded> Requests = [];
        internal void Ok(object data, int status = 200) => steps.Enqueue(_ => Task.FromResult(Response(data, status)));
        internal void Key(string value = "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk") =>
            Ok(new { request_key = value, expires_at = "2030-01-01T00:00:00Z" }, 201);
        internal void Error(int status, string code = "FORBIDDEN", string message = "owned test refusal") => steps.Enqueue(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)
        { Content = new StringContent(JsonSerializer.Serialize(new { ok = false, error = new { code, message }, request_id = "test" }), Encoding.UTF8, "application/json") }));
        internal void Lost() => steps.Enqueue(_ => Task.FromException<HttpResponseMessage>(new HttpRequestException("owned response loss")));
        internal TaskCompletionSource<HttpResponseMessage> Delayed()
        {
            var source = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            steps.Enqueue(_ => source.Task); return source;
        }
        internal static HttpResponseMessage Response(object data, int status = 200) => new((HttpStatusCode)status)
        { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data, request_id = "test" }, ApiClient.Json), Encoding.UTF8, "application/json") };
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(new(request.Method.Method, request.RequestUri!.AbsolutePath, request.RequestUri.Query,
                request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct),
                request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null,
                request.Headers.Authorization?.Parameter));
            if (steps.Count == 0) throw new InvalidOperationException("Unexpected HTTP request: " + request.RequestUri.AbsolutePath);
            return await steps.Dequeue()(ct);
        }
    }

    private static void WireAndRecovery()
    {
        using var f = new Fixture(); var client = f.Client;
        f.Handler.Ok(new { context = Context() });
        var context = Wait(client.ContextAsync(Target, CancellationToken.None));
        Check(context.TaskId == Target && context.RowVersion == "9007199254740994", "BIGINT IDs and versions remain exact");
        Check(f.Handler.Requests[^1].Path == "/api/v1/tasks/" + Target + "/review-request-context"
            && f.Handler.Requests[^1].Bearer == "owned-test-token-7", "context uses captured shared session");
        var op = client.Prepare(context, Original);
        Throws<InvalidOperationException>(client.ClearConfirmed, "unconfirmed ready write cannot be cleared");
        f.Handler.Key(); f.Handler.Lost();
        Throws<ApiException>(() => Wait(client.SendAsync(CancellationToken.None)), "lost response preserves pending");
        var write = f.Handler.Requests[^1];
        Check(op.Stage == "check" && ReferenceEquals(client.Pending, op) && op.Reason == Original, "unknown outcome retains same operation and text");
        using (var json = JsonDocument.Parse(write.Body))
            Check(json.RootElement.GetProperty("reason").GetString() == Original
                && json.RootElement.GetProperty("submission_id").GetString() == Submission, "HTTP body preserves exact original reason and selected submission");
        int count = f.Handler.Requests.Count;
        Throws<InvalidOperationException>(() => client.Prepare(context, "replacement"), "new content cannot replace unknown intent");
        Throws<InvalidOperationException>(() => Wait(client.SendAsync(CancellationToken.None)), "unknown cannot automatically resend");
        Throws<InvalidOperationException>(client.ClearConfirmed, "unknown cannot reset");
        Check(f.Handler.Requests.Count == count, "blocked operations issue no HTTP");
        f.Handler.Ok(new { state = "not_recorded" }); Wait(client.CheckAsync(CancellationToken.None));
        Check(op.Stage == "not_recorded" && f.Handler.Requests[^1].Path == "/api/v1/write-requests/result"
            && f.Handler.Requests[^1].Key == write.Key, "receipt check retains same key and does not mutate");
        Throws<InvalidOperationException>(client.ClearConfirmed, "not-recorded intent remains frozen for explicit same-key resend");
        f.Handler.Ok(Saved(), 201); Wait(client.SendAsync(CancellationToken.None));
        Check(f.Handler.Requests[^1].Body == write.Body && f.Handler.Requests[^1].Key == write.Key,
            "explicit retry reuses byte-identical body and key");
        Check(f.Handler.Requests.Count(x => x.Path.EndsWith("/keys")) == 1 && op.Stage == "completed", "retry never issues a replacement key");
        client.ClearConfirmed(); Check(client.Pending is null, "only confirmed completion can reset");
    }

    private static void MalformedKeysAndResponses()
    {
        using var f = new Fixture(); var op = f.Client.Prepare(Context(), Original);
        f.Handler.Key(new string('!', 40));
        Throws<InvalidDataException>(() => Wait(f.Client.SendAsync(CancellationToken.None)), "malformed key rejected before capture");
        Check(op.Stage == "ready" && f.Handler.Requests.All(x => !x.Path.EndsWith("/review-requests")), "invalid key cannot strand an unsent operation in unknown state");
        f.Handler.Key(); f.Handler.Ok(Saved() with { TaskId = "2" }, 201);
        Throws<InvalidDataException>(() => Wait(f.Client.SendAsync(CancellationToken.None)), "wrong task saved result rejected");
        Check(op.Stage == "check" && op.Saved is null, "malformed success remains unknown");
        f.Handler.Ok(new { state = "completed", http_status = 200, result = Saved() });
        Throws<InvalidDataException>(() => Wait(f.Client.CheckAsync(CancellationToken.None)), "wrong receipt HTTP status rejected");
        f.Handler.Ok(new { state = "completed", http_status = 201, result = Saved() });
        Wait(f.Client.CheckAsync(CancellationToken.None));
        Check(op.Stage == "completed" && op.Saved?.RequestId == "9", "valid later receipt resolves original operation");
        using var invalid = new Fixture();
        invalid.Handler.Ok(new { context = Context() with { TaskId = "2" } });
        Throws<InvalidDataException>(() => Wait(invalid.Client.ContextAsync(Target, CancellationToken.None)), "cross-task context rejected");
        invalid.Handler.Ok(new { items = new[] { Item(), Item() } });
        Throws<InvalidDataException>(() => Wait(invalid.Client.ListAsync(false, CancellationToken.None)), "duplicate request identifiers rejected");
    }

    private static void AcknowledgementIdentity()
    {
        using var f = new Fixture(2);
        var op = f.Client.PrepareAcknowledge(Item());
        f.Handler.Key(); f.Handler.Ok(Saved(true) with { RequestId = "10" });
        Throws<InvalidDataException>(() => Wait(f.Client.SendAsync(CancellationToken.None)), "cross-request ACK result rejected");
        var write = f.Handler.Requests[^1];
        Check(write.Path == "/api/v1/tasks/" + Target + "/review-requests/9/acknowledge" && op.Stage == "check",
            "ACK addresses exact selected request and retains unknown response");
        using (var json = JsonDocument.Parse(write.Body))
            Check(json.RootElement.EnumerateObject().Count() == 2 && json.RootElement.GetProperty("expected_assignment_revision").GetInt32() == 3,
                "ACK sends only original concurrency versions, no result decision");
        f.Handler.Ok(new { state = "completed", http_status = 200, result = Saved(true) with { RoundNo = "3" } });
        Throws<InvalidDataException>(() => Wait(f.Client.CheckAsync(CancellationToken.None)), "cross-round receipt rejected");
        f.Handler.Ok(new { state = "completed", http_status = 200, result = Saved(true) });
        Wait(f.Client.CheckAsync(CancellationToken.None));
        Check(op.Stage == "completed" && op.Saved?.RequestState == "acknowledged", "matching ACK result resolves without approval");
        f.Client.ClearConfirmed();
        Throws<InvalidOperationException>(() => f.Client.PrepareAcknowledge(Item("resolved")), "resolved request cannot acknowledge");
        Throws<InvalidOperationException>(() => f.Client.PrepareAcknowledge(Item() with { ReviewerId = 8 }), "other reviewer cannot acknowledge");
    }

    private static void StoresAndSessions()
    {
        using var a = new Fixture(); using var b = new Fixture();
        var old = a.Client; var op = old.Prepare(Context(), Original);
        Check(ReferenceEquals(old, a.Client) && ReferenceEquals(op, a.Client.Pending), "reopening within same ApiClient/session retains pending");
        Check(!ReferenceEquals(old, b.Client) && b.Client.Pending is null, "different ApiClient even same user has separate state");
        a.Api.ClearIdentity(); Login(a.Api, a.Handler);
        int count = a.Handler.Requests.Count;
        Throws<OperationCanceledException>(() => Wait(old.SendAsync(CancellationToken.None)), "old login cannot send in replacement session");
        Throws<OperationCanceledException>(old.ClearConfirmed, "old login cannot reset state using new identity");
        Check(a.Handler.Requests.Count == count && !ReferenceEquals(old, a.Client) && a.Client.Pending is null,
            "new login replaces only its own holder and sends no old write");
        using var expired = new Fixture();
        expired.Handler.Error(401, "AUTH_REQUIRED");
        Throws<ApiException>(() => Wait(expired.Client.ContextAsync(Target, CancellationToken.None)), "401 propagates through shared identity");
        Check(expired.Api.CurrentUser is null, "401 expires shared login");
    }

    private static void DefinitiveFailureAndExpiry()
    {
        using var refused = new Fixture(); var failed = refused.Client.Prepare(Context(), Original);
        refused.Handler.Key(); refused.Handler.Error(409, "REVIEW_REQUEST_PENDING");
        Throws<ApiException>(() => Wait(refused.Client.SendAsync(CancellationToken.None)), "server's deterministic pending refusal is surfaced");
        Check(failed.Stage == "failed" && !refused.Client.IsBlocked, "409 refusal does not revoke current access");
        refused.Client.ClearConfirmed(); Check(refused.Client.Pending is null, "confirmed refusal permits explicit reset");
        using var expired = new Fixture(); var unknown = expired.Client.Prepare(Context(), Original);
        expired.Handler.Key(); expired.Handler.Lost();
        Throws<ApiException>(() => Wait(expired.Client.SendAsync(CancellationToken.None)), "expired-key scenario begins with unknown outcome");
        expired.Handler.Error(410, "IDEMPOTENCY_KEY_EXPIRED");
        Throws<ApiException>(() => Wait(expired.Client.CheckAsync(CancellationToken.None)), "expired receipt is never treated as not-recorded");
        Check(unknown.Stage == "check" && unknown.Reason == Original, "expired outcome remains unconfirmed with original content");
        int before = expired.Handler.Requests.Count;
        Throws<InvalidOperationException>(expired.Client.ClearConfirmed, "expired unconfirmed receipt cannot reset");
        Throws<InvalidOperationException>(() => Wait(expired.Client.SendAsync(CancellationToken.None)), "expired unconfirmed receipt cannot issue a new key");
        Check(expired.Handler.Requests.Count == before, "expiry never generates replacement writes");
    }

    private static void InFlightExclusionAndLateSession()
    {
        using var f = new Fixture(); var client = f.Client; var op = client.Prepare(Context(), Original);
        f.Handler.Key(); var delayed = f.Handler.Delayed(); var send = client.SendAsync(CancellationToken.None);
        Until(() => f.Handler.Requests.Last().Path.EndsWith("/review-requests"));
        int count = f.Handler.Requests.Count;
        Throws<InvalidOperationException>(() => Wait(client.SendAsync(CancellationToken.None)), "parallel send blocked");
        Throws<InvalidOperationException>(() => Wait(client.CheckAsync(CancellationToken.None)), "parallel receipt check blocked");
        Throws<InvalidOperationException>(() => client.Prepare(Context(), "other"), "parallel replacement blocked");
        Check(f.Handler.Requests.Count == count && client.IsBusy, "single in-flight business request");
        delayed.SetResult(Handler.Response(Saved(), 201)); Wait(send);
        Check(!client.IsBusy && op.Stage == "completed", "one completed write releases busy state");
        using var late = new Fixture(); var old = late.Client; var pending = old.Prepare(Context(), Original);
        late.Handler.Key(); var answer = late.Handler.Delayed(); var writing = old.SendAsync(CancellationToken.None);
        Until(() => pending.Stage == "check"); late.Api.ClearIdentity(); Login(late.Api, late.Handler);
        answer.SetResult(Handler.Response(Saved(), 201));
        Throws<OperationCanceledException>(() => Wait(writing), "late write response cannot update replacement login");
        Check(pending.Stage == "check" && late.Client.Pending is null && !old.Session.IsCurrent,
            "stale completion stays only in old session operation");
    }

    private static void Show(Form form)
    { form.StartPosition = FormStartPosition.Manual; form.Location = new(-15000, -15000); form.Show(); Application.DoEvents(); }
    private static void Empty(ReviewRequestForm form, string why)
    {
        foreach (var name in new[] { "txtTaskSummary", "txtCurrentStatus", "txtCurrentDeadline", "txtRequestReason", "txtResultPreview" })
            Check(Field<TextBox>(form, name).Text.Length == 0, why + ": " + name);
        Check(!Field<Button>(form, "btnConfirm").Enabled && Field<TextBox>(form, "txtRequestReason").ReadOnly, why + ": input blocked");
    }
    private static void Capture(Form form, string name)
    {
        if (captures is null) return;
        Directory.CreateDirectory(captures);
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        bitmap.Save(Path.Combine(captures, name + ".png"));
    }
    private static void ActualDialogReopen()
    {
        using var f = new Fixture(); f.Handler.Ok(new { context = Context() });
        using (var form = new ReviewRequestForm(f.Client, Target))
        {
            Show(form); Until(() => Field<Button>(form, "btnConfirm").Enabled);
            Field<TextBox>(form, "txtRequestReason").Text = Original;
            f.Handler.Key(); f.Handler.Lost(); Field<Button>(form, "btnConfirm").PerformClick();
            Until(() => !Field<bool>(form, "busy"));
            Check(f.Client.Pending?.Stage == "check" && Field<Button>(form, "btnCancel").Text == "처리 결과 확인",
                "actual send button enters receipt recovery after response loss");
            Check(Field<TextBox>(form, "txtRequestReason").ReadOnly && !Field<Button>(form, "btnConfirm").Enabled,
                "unknown UI cannot edit or resend");
            Capture(form, "request-unknown"); form.Close();
        }
        int count = f.Handler.Requests.Count;
        using var reopened = new ReviewRequestForm(f.Client, "2"); Show(reopened);
        Check(f.Handler.Requests.Count == count && Field<TextBox>(reopened, "txtRequestReason").Text == Original
            && Field<TextBox>(reopened, "txtTaskSummary").Text.Contains(Target), "reopening another selection recovers original target without new context");
        f.Handler.Ok(new { state = "completed", http_status = 201, result = Saved() });
        Field<Button>(reopened, "btnCancel").PerformClick(); Until(() => Field<Button>(reopened, "btnConfirm").Enabled);
        Check(reopened.Saved && f.Client.Pending?.Stage == "completed", "actual result button recovers successful receipt");
        Capture(reopened, "request-completed");
        Field<Button>(reopened, "btnConfirm").PerformClick();
        Check(reopened.IsDisposed && f.Client.Pending is null, "explicit completion closes and clears confirmed operation");
    }

    private static void PermissionLossScreens()
    {
        foreach (string operation in new[] { "context", "send", "check" })
        {
            using var f = new Fixture(); f.Handler.Ok(new { context = Context() });
            using var form = new ReviewRequestForm(f.Client, Target); Show(form);
            Until(() => Field<Button>(form, "btnConfirm").Enabled);
            Field<TextBox>(form, "txtRequestReason").Text = Original;
            ReviewRequestOperation? original = null;
            if (operation == "context")
            { f.Handler.Error(403); Field<Button>(form, "btnCancel").PerformClick(); }
            else
            {
                f.Handler.Key();
                if (operation == "send") f.Handler.Error(403); else f.Handler.Lost();
                Field<Button>(form, "btnConfirm").PerformClick(); Until(() => !Field<bool>(form, "busy"));
                if (operation == "check")
                {
                    original = f.Client.Pending;
                    f.Handler.Error(403); Field<Button>(form, "btnCancel").PerformClick();
                }
            }
            Until(() => !Field<bool>(form, "busy"));
            Check(f.Client.IsBlocked && f.Client.Session.IsCurrent && f.Client.Pending is null,
                operation + " 403 blocks this feature without clearing shared identity");
            if (original is not null) Check(original.Stage == "check" && original.Reason == Original, "denied unknown receipt retains internal original");
            Empty(form, operation + " denied current screen");
            int count = f.Handler.Requests.Count;
            using var reopened = new ReviewRequestForm(f.Client, Target); Show(reopened);
            Empty(reopened, operation + " denied reopen");
            Throws<InvalidOperationException>(() => f.Client.Prepare(Context(), "overwrite"), "403 cannot replace private pending");
            Check(f.Handler.Requests.Count == count && ReferenceEquals(f.Client, TaskReviewRequestsClient.For(f.Api)),
                "denied reopen cannot refresh identity or restore private text");
            if (operation == "check")
            {
                Check(Field<Button>(reopened, "btnCancel").Enabled && f.Client.CanCheckBlockedResult,
                    "denied unknown operation still exposes a content-free receipt check after reopen");
                var priorWrite = f.Handler.Requests.Last(x => x.Path.EndsWith("/review-requests"));
                f.Handler.Ok(new { state = "not_recorded" });
                Field<Button>(reopened, "btnCancel").PerformClick(); Until(() => !Field<bool>(reopened, "busy"));
                Check(!f.Client.IsBlocked && f.Client.Pending?.Stage == "not_recorded"
                    && Field<TextBox>(reopened, "txtRequestReason").Text == Original,
                    "server-authorized receipt lookup restores original text only after current access succeeds");
                f.Handler.Ok(Saved(), 201); Field<Button>(reopened, "btnConfirm").PerformClick();
                Until(() => !Field<bool>(reopened, "busy"));
                Check(f.Handler.Requests[^1].Key == priorWrite.Key && f.Handler.Requests[^1].Body == priorWrite.Body
                    && f.Client.Pending?.Stage == "completed", "permission-restored explicit resend preserves exact original key and body");
            }
        }
        using var cancelled = new Fixture(); cancelled.Handler.Ok(new { context = Context() });
        using var open = new ReviewRequestForm(cancelled.Client, Target); Show(open);
        Until(() => Field<Button>(open, "btnConfirm").Enabled);
        Field<TextBox>(open, "txtRequestReason").Text = Original;
        cancelled.Api.ClearIdentity(); Until(() => Field<TextBox>(open, "txtRequestReason").Text == "");
        Empty(open, "session cancellation clears visible content");
    }

    private static void InboxPermissionsAndAcknowledgement()
    {
        using var f = new Fixture(2); f.Handler.Ok(new { items = new[] { Item() } });
        using var inbox = new TaskReviewRequestsForm(f.Client, true); Show(inbox);
        Until(() => Field<DataGridView>(inbox, "gridRequests").Rows.Count == 1 && !Field<bool>(inbox, "busy"));
        var grid = Field<DataGridView>(inbox, "gridRequests"); grid.CurrentCell = grid.Rows[0].Cells[0]; grid.Rows[0].Selected = true;
        Check(Field<Button>(inbox, "btnAcknowledge").Enabled && Field<TextBox>(inbox, "txtDetail").Text.Contains(Original),
            "current reviewer sees exact selected pending request and explicit acknowledgement");
        f.Handler.Key(); f.Handler.Ok(Saved(true)); f.Handler.Ok(new { items = new[] { Item("acknowledged") } });
        Field<Button>(inbox, "btnAcknowledge").PerformClick(); Until(() => !Field<bool>(inbox, "busy"));
        Check(f.Client.Pending is null && !Field<Button>(inbox, "btnAcknowledge").Enabled
            && Field<TextBox>(inbox, "txtDetail").Text.Contains("요청 확인"), "actual ACK button saves then requeries without result approval");
        Capture(inbox, "inbox-acknowledged");
        f.Handler.Error(403); Field<Button>(inbox, "btnRefresh").PerformClick(); Until(() => !Field<bool>(inbox, "busy"));
        Check(grid.Rows.Count == 0 && !Field<TextBox>(inbox, "txtDetail").Text.Contains("private")
            && !Field<Button>(inbox, "btnAcknowledge").Enabled, "inbox 403 clears previously displayed request content");
        using var employee = new Fixture(); employee.Handler.Ok(new { items = new[] { Item() } });
        using var own = new TaskReviewRequestsForm(employee.Client, false); Show(own);
        Until(() => !Field<bool>(own, "busy"));
        Check(!Field<ComboBox>(own, "cmbScope").Enabled && !Field<Button>(own, "btnAcknowledge").Enabled,
            "requester own inbox cannot switch to reviewer or acknowledge");
    }

    private static void StatusLayout()
    {
        using var f = new Fixture();
        f.Handler.Error(503, "TASK_REVIEW_REQUEST_SCHEMA_REQUIRED",
            "검토 요청 저장소가 준비되지 않았습니다. 서버 담당자가 019 검토 요청 스키마를 확인해 주세요. "
            + "기존 요청의 저장 여부가 불명확하면 같은 요청의 처리 결과를 확인해야 합니다. "
            + "화면 크기를 줄이거나 배율을 변경해도 이 안내의 마지막 오류 코드까지 읽을 수 있어야 합니다.");
        using var form = new ReviewRequestForm(f.Client, Target); Show(form);
        Until(() => !Field<bool>(form, "busy"));
        form.Size = form.MinimumSize;
        InspectStatus(form, "request-status-minimum");
        form.Scale(new SizeF(1.5f, 1.5f));
        InspectStatus(form, "request-status-scaled150");
    }
    private static void InspectStatus(Form form, string name)
    {
        form.PerformLayout(); Application.DoEvents();
        var status = Field<Label>(form, "lblStatus");
        var parent = Field<TableLayoutPanel>(form, "layoutContent");
        Check(status.Left >= 0 && status.Right <= parent.ClientSize.Width,
            name + " status remains within available content width");
        int availableWidth = Math.Max(1, status.ClientSize.Width - status.Padding.Horizontal);
        var measured = TextRenderer.MeasureText(status.Text, status.Font, new Size(availableWidth, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        Check(status.ClientSize.Height - status.Padding.Vertical >= measured.Height,
            name + " all wrapped status lines fit without clipping");
        foreach (Control action in new[] { Field<Button>(form, "btnConfirm"), Field<Button>(form, "btnCancel"), Field<Button>(form, "btnClose") })
        {
            var bounds = form.RectangleToClient(action.RectangleToScreen(action.ClientRectangle));
            Check(form.ClientRectangle.Contains(bounds), name + " keeps action inside form: " + action.Name);
        }
        Capture(form, name);
    }
}
