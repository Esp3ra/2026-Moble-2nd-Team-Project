using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private static int checks;
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Exception? failure = null;
        using var host = new Form { ShowInTaskbar = false, Opacity = 0 };
        host.Shown += async (_, _) => { try { await Run(); } catch (Exception ex) { failure = ex; } finally { host.Close(); } };
        Application.Run(host);
        if (failure is not null) { Console.Error.WriteLine(failure); Environment.ExitCode = 1; }
        else Console.WriteLine($"PASS: {checks} collaboration checks (explicit HTTP/UI doubles; no shared server)");
    }
    private static async Task Run()
    {
        var handler = new Handler();
        using var api = new ApiClient("http://localhost:5000/", handler);
        await api.LoginAsync("fixture", "fixture-password");
        var service = new HttpCollaborationService(api.CaptureSession());
        var operation = await service.PrepareCreateAsync("9223372036854775807", 1, "검토 요청", "함께 검토", new(2026, 10, 25), default);
        Check(operation.Body.GetProperty("task_id").GetString() == "9223372036854775807", "BIGINT stays exact string");
        Check(operation.Body.GetProperty("expected_assignment_revision").GetInt32() == 1, "revision is integer");
        Check(operation.Body.GetProperty("expected_row_version").ValueKind == JsonValueKind.String, "version is string");
        var result = await service.ExecuteAsync(operation, default);
        Check(result.RequestId == operation.Body.GetProperty("request_id").GetString(), "created ID preserved");
        var received = await service.ListAsync(false, default);
        Check(received.Count == 1 && received[0].Status == "pending", "DB response mapped");
        var response = await service.PrepareResponseAsync(received[0], "decline", default);
        Check(response.Action == "collaboration.declined" && response.Body.GetProperty("expected_row_version").GetString() == "1", "response action and concurrency token");
        handler.DropResponse = true;
        int previous = handler.Writes;
        try { await service.ExecuteAsync(response, default); throw new Exception("unknown result not detected"); }
        catch (CollaborationOutcomeUnknownException) { checks++; }
        Check(handler.Writes == previous + 1, "no automatic replay after failure");
        handler.Recorded = true;
        var retained = await service.ResolveAsync(response, default);
        Check(retained?.Status == "declined", "lost response recovered from receipt");
        Check(handler.Writes == previous + 1, "result lookup does not mutate");
        handler.Recorded = false;
        Check(await service.ResolveAsync(response, default) is null, "not-recorded is not success");
        handler.DropResponse = false;
        await service.ExecuteAsync(response, default);
        Check(handler.Keys.TakeLast(2).Distinct().Count() == 1, "explicit retry retains exact key");
        handler.ForgedRecipient = true;
        try { await service.ListAsync(false, default); throw new Exception("foreign recipient accepted"); }
        catch (ApiException) { checks++; }
        handler.ForgedRecipient = false;
        handler.StringRevision = true;
        try { await service.PrepareCreateAsync("1", 1, "t", "b", new(2026, 10, 25), default); throw new Exception("string revision accepted"); }
        catch (ApiException) { checks++; }
        handler.StringRevision = false;
        var history = await service.GetHistoryAsync(received[0], default);
        Check(history.Count == 2 && history[1].EventId == "9223372036854775807", "history preserves BIGINT and event order");
        handler.ForgedHistory = true;
        try { await service.GetHistoryAsync(received[0], default); throw new Exception("foreign history actor accepted"); }
        catch (ApiException) { checks++; }
        handler.ForgedHistory = false;

        var fake = new FakeService();
        using (var form = new CollaborationRequestForm(new Directory(), fake))
        {
            form.CreateControl(); await form.ReloadDirectoryAsync();
            Field<ComboBox>(form, "cmbOptions").SelectedIndex = 0;
            Field<ComboBox>(form, "cmbOptions2").SelectedIndex = 0;
            Field<TextBox>(form, "txtContent").Text = "요청 제목";
            Field<TextBox>(form, "txtCollaborationReason").Text = "요청 내용";
            Check(Field<Button>(form, "btnSendRequest").Enabled, "valid current selection enables sending");
            await form.SendRequestAsync();
            Check(fake.Writes == 1, "send button reaches persistence service");
            Check(Field<Label>(form, "directoryStatus").Text.Contains("서버 DB에 저장"), "confirmed save status");
            Check(Field<TextBox>(form, "txtContent").Text == "", "clear only after confirmed save");
        }
        var entry = Handler.Entry();
        using (var popup = new CollaborationResponseForm(fake, entry))
        {
            var buttons = popup.Controls.OfType<FlowLayoutPanel>().Single().Controls.OfType<Button>().ToArray();
            Check(buttons.Any(b => b.Text == "승인") && buttons.Any(b => b.Text == "거절"), "recipient approval and rejection controls");
            popup.Show(); Application.DoEvents();
            Check(popup.Width < 500 && !popup.Modal, "small nonmodal notification");
            popup.Close(); Check(fake.Writes == 1, "close postpones without server response");
        }
        await CollaborationReviewChecks.Run(Check);
        api.ClearIdentity(); api.SetServer("http://localhost:5001/");
        Check(!service.IsCurrent, "server/login lifetime bound");
        try { await service.ListAsync(false, default); throw new Exception("stale session allowed"); }
        catch (OperationCanceledException) { checks++; }
    }
    private static T Field<T>(object form, string name) => (T)form.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
    private sealed class Directory : ICollaborationDirectory
    {
        public int CurrentUserId => 2; public bool IsCurrent => true; public CancellationToken Cancellation => default;
        public Task<IReadOnlyList<TaskSummary>> GetOwnTasksAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<TaskSummary>>([
            new("1", "진행 업무", "직원1", TaskDisplayStatus.InProgress, new(2026,10,20), new(2026,10,30))]);
        public Task<IReadOnlyList<CollaborationRecipient>> GetRecipientsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CollaborationRecipient>>([new(1,"직원1","개발팀")]);
    }
    private sealed class FakeService : ICollaborationService
    {
        public int UserId => 2; public bool IsCurrent => true; public CancellationToken Cancellation => default; public int Writes;
        public Task<IReadOnlyList<CollaborationEntry>> ListAsync(bool sent, CancellationToken ct) => Task.FromResult<IReadOnlyList<CollaborationEntry>>([]);
        public Task<IReadOnlyList<CollaborationHistoryEntry>> GetHistoryAsync(CollaborationEntry item, CancellationToken ct) => Task.FromResult<IReadOnlyList<CollaborationHistoryEntry>>([]);
        public Task<CollaborationOperation> PrepareCreateAsync(string id, int person, string title, string text, DateOnly desired, CancellationToken ct) => Task.FromResult(new CollaborationOperation("collaboration.created",id,"collaboration-requests",JsonSerializer.SerializeToElement(new {}),new string('k',32)));
        public Task<CollaborationOperation> PrepareResponseAsync(CollaborationEntry item, string decision, CancellationToken ct) => throw new NotImplementedException();
        public Task<CollaborationResult> ExecuteAsync(CollaborationOperation operation, CancellationToken ct) { Writes++; return Task.FromResult(new CollaborationResult(Handler.Identifier,"pending","1",DateTimeOffset.UtcNow)); }
        public Task<CollaborationResult?> ResolveAsync(CollaborationOperation operation, CancellationToken ct) => throw new NotImplementedException();
    }
    private sealed class Handler : HttpMessageHandler
    {
        public const string Identifier = "118b8178-83b5-4b92-a610-8ff777e9a05e";
        public bool DropResponse, Recorded, ForgedRecipient, StringRevision, ForgedHistory;
        public int Writes; public List<string> Keys = [];
        public static CollaborationEntry Entry() => new(Identifier,"1",1,1,2,"직원1","업무","검토","검토 부탁",new(2026,10,25),"pending","1",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,null,false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string path = request.RequestUri!.PathAndQuery;
            JsonElement body = request.Content is null ? default : JsonDocument.Parse(await request.Content.ReadAsStringAsync(ct)).RootElement.Clone();
            object data;
            if (path.EndsWith("auth/login")) data = new { access_token="fixture-token",token_type="Bearer",expires_at="2099-01-01T00:00:00Z",user=new {id=2,login_id="fixture",display_name="직원2",employee_no="E2",department="개발팀",role="employee",is_active=true} };
            else if (path.Contains("task-context/")) data = new {task_id=path.Split('/').Last(),row_version="1",assignment_revision=StringRevision ? (object)"1" : 1,due_date="2026-10-30",server_time="2026-10-21T03:00:00Z"};
            else if (path.EndsWith("write-requests/keys")) data = new {request_key=new string('k',40),expires_at="2099-01-01T00:00:00Z"};
            else if (path.EndsWith("write-requests/result")) data = Recorded ? new {state="completed",http_status=200,result=new CollaborationResult(Identifier,"declined","2",DateTimeOffset.UtcNow)} : (object)new {state="not_recorded"};
            else if (path.EndsWith("/history")) data = new {request_id=Identifier,items=new[]{
                new CollaborationHistoryEntry("9223372036854775806","pending",null,1,DateTimeOffset.UtcNow),
                new CollaborationHistoryEntry("9223372036854775807","declined","recipient_response",ForgedHistory ? 3 : 2,DateTimeOffset.UtcNow)},server_time=DateTimeOffset.UtcNow};
            else if (request.Method == HttpMethod.Get) data = new {items=new[]{ Entry() with {RecipientId=ForgedRecipient ? 3 : 2} },server_time=DateTimeOffset.UtcNow};
            else
            {
                Writes++; Keys.Add(request.Headers.GetValues("Idempotency-Key").Single());
                if (DropResponse) return new(HttpStatusCode.ServiceUnavailable) {Content=new StringContent("{\"ok\":false,\"error\":{\"message\":\"lost response\",\"code\":\"UNAVAILABLE\"}}",Encoding.UTF8,"application/json")};
                data = new CollaborationResult(path.EndsWith("collaboration-requests") ? body.GetProperty("request_id").GetString()! : Identifier,
                    path.Contains("/decline") ? "declined" : "pending","1",DateTimeOffset.UtcNow);
            }
            return new(HttpStatusCode.OK) {Content=new StringContent(JsonSerializer.Serialize(new {ok=true,data,request_id="fixture"},ApiClient.Json),Encoding.UTF8,"application/json")};
        }
    }
}
