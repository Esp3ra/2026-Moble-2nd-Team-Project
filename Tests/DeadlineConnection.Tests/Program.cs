using System.Net;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.DeadlineAlerts;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

internal static class Program
{
    private const string Event = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
    private const string TaskId = "9007199254740993";
    private static string Target(int revision = 1) => $"d1:{TaskId}:{Event}:{revision}";
    private static int checks;
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Exception? failure = null;
        using var host = new Form { ShowInTaskbar = false, Opacity = 0 };
        host.Shown += async (_, _) =>
        {
            try { if (args.Contains("--shared-read")) await SharedRead(host); else if (args.Contains("--main")) await MainFormLive(); else if (args.Contains("--live")) await Live(host); else await Doubles(); }
            catch (Exception error) { failure = error; }
            finally { host.Close(); }
        };
        Application.Run(host);
        if (failure is not null) { Console.Error.WriteLine(failure); Environment.ExitCode = 1; }
        else Console.WriteLine($"PASS: {checks} binding-aware deadline checks ({(args.Contains("--shared-read") ? "deployed server read-only + captured wrapper/scope/panel; business writes 0" : args.Contains("--live") || args.Contains("--main") ? "actual PR70 HTTP/MySQL + captured wrapper/scope/panel/MainForm; fixture tasks" : "explicit HTTP doubles")}).");
    }
    private static async Task SharedRead(Form host)
    {
        using var api = new ApiClient(Environment.GetEnvironmentVariable("DEADLINE_TEST_URL")!);
        var user = Environment.GetEnvironmentVariable("DEADLINE_SHARED_LOGIN")!;
        var password = Environment.GetEnvironmentVariable("DEADLINE_SHARED_PASSWORD")!;
        await api.LoginAsync(user, password);
        try
        {
            var session = api.CaptureSession();
            using var source = new HttpDeadlineAlertSource(session);
            using var wrapper = new LoginDeadlineNotifications(host, session, source, () => _ => Task.FromResult(false));
            using var panel = new DeadlineAlertPanel(); panel.Bind(wrapper.Scope);
            await wrapper.StartAsync();
            var scope = wrapper.Scope;
            Check(scope.Snapshot is not null, "deployed snapshot validated by real source and captured owner");
            var original = scope.Snapshot!.Items.Select(x => (x.EventToken, x.IsConfirmed)).ToArray();
            foreach (var item in scope.Snapshot.Items)
                Check(item.EventToken == $"d1:{item.TaskId}:{item.EventId}:{item.AssignmentRevision}", "original server binding reaches panel");
            await scope.CheckAsync();
            Check(scope.Snapshot!.Items.Select(x => (x.EventToken, x.IsConfirmed)).SequenceEqual(original), "manual refresh preserves actual state");
            await api.LogoutAsync(); Check(!scope.IsCurrent, "old deployed scope ends on logout");
            await api.LoginAsync(user, password);
            using var restored = new HttpDeadlineAlertSource(api.CaptureSession());
            var latest = await restored.ReadAsync(default);
            Check(latest.Items.Select(x => (x.EventToken, x.IsConfirmed)).SequenceEqual(original), "relogin reads durable current state without confirmation writes");
        }
        finally { if (api.CurrentUser is not null) await api.LogoutAsync(); }
    }
    private static async Task MainFormLive()
    {
        var api=AppServices.Api;var old=api.ServerUri.ToString();
        api.SetServer(Environment.GetEnvironmentVariable("DEADLINE_TEST_URL")!);
        try
        {
            var login=await api.LoginAsync("staff1","Deadline-facts-test-2026!");
            using var main=new MainForm(login.User){ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-20000,-20000)};
            main.Show();
            var end=DateTime.UtcNow.AddSeconds(12);
            while(DeadlineAlertScope.Find(main)?.Snapshot is null && DateTime.UtcNow<end)await Task.Delay(30);
            var scope=DeadlineAlertScope.Find(main) ?? throw new Exception("actual notice view did not connect real HTTP source");
            Check(scope.Snapshot?.Items.Count==1 && scope.Unread.Count==1,"notice view automatically consumes actual ready PR70 HTTP");
            var page=main.Controls.Find("btnTasks",true).OfType<Button>().Single();page.PerformClick();
            Check(DeadlineAlertScope.Find(main)==scope && scope.IsCurrent,"menu disposal keeps the login-owned scope");
            var token=scope.Unread.Single().EventToken;
            Check((await scope.ConfirmAsync(token)).State==DeadlineConfirmationState.Confirmed,"actual MainForm scope confirms DB after menu switch");
            Check(scope.Unread.Count==0,"actual MainForm confirmation updates unread state");
            await api.LogoutAsync();
            await Task.Delay(30);Check(!scope.IsCurrent,"MainForm captured scope ends on logout");
        }
        finally
        {
            if(api.CurrentUser is not null)await api.LogoutAsync();
            api.SetServer(old);
        }
    }
    private static async Task Live(Form host)
    {
        string mode = Environment.GetEnvironmentVariable("DEADLINE_LIVE_MODE") ?? "normal";
        using var handler = new Fault(mode);
        using var api = new ApiClient(Environment.GetEnvironmentVariable("DEADLINE_TEST_URL")!, handler);
        var login = await api.LoginAsync("staff1", "Deadline-facts-test-2026!");
        var session = api.CaptureSession();
        using var source = new HttpDeadlineAlertSource(session);
        using var wrapper = new LoginDeadlineNotifications(host, session, source, () => _ => Task.FromResult(false));
        using var panel = new DeadlineAlertPanel(); panel.Bind(wrapper.Scope);
        await wrapper.StartAsync();
        var scope = wrapper.Scope;
        Check(scope.Snapshot?.Items.Count == 1 && scope.Unread.Count == 1, "actual HTTP snapshot reaches borrowed alert panel");
        var item = scope.Unread.Single();
        Check(item.EventId is not null && item.EventToken == $"d1:{item.TaskId}:{item.EventId}:{item.AssignmentRevision}", "server event and original binding remain distinct through CapturedSource");
        var result = await scope.ConfirmAsync(item.EventToken);
        if (mode == "lost")
        {
            Check(result.State == DeadlineConfirmationState.Unknown && scope.NeedsResolution(item.EventToken), "lost response remains an unresolved original operation");
            result = await scope.ConfirmAsync(item.EventToken);
        }
        Check(result.State == DeadlineConfirmationState.Confirmed, "actual DB receipt is validated by the scope");
        Check(handler.Posts == 1, "confirm/result recovery makes one business POST");
        if (mode == "list-fail")
        {
            Check(scope.Snapshot is null && scope.Status.Contains("확인 완료"), "committed success survives a failed list refresh");
            handler.FailReads = false; await scope.CheckAsync();
        }
        Check(scope.Unread.Count == 0, "confirmed alert removed from unread panel");
        await scope.ConfirmAsync(item.EventToken);
        Check(handler.Posts == 1, "repeated click never writes again");
        await api.LogoutAsync(); Check(!source.IsCurrent, "captured source ends on logout");
        await api.LoginAsync("staff1", "Deadline-facts-test-2026!");
        using var restored = new HttpDeadlineAlertSource(api.CaptureSession());
        Check((await restored.ReadAsync(default)).Items.Single().IsConfirmed, "MySQL confirmation survives relogin/new source");
        await api.LogoutAsync();
    }
    private static async Task Doubles()
    {
        foreach (var mode in new[] { "normal", "lost", "list-fail", "bad-receipt", "wrong-revision", "not-recorded", "retry-version", "expired", "off", "unauthorized" })
        {
            using var handler = new Fake(mode); using var api = new ApiClient("http://127.0.0.1:1",handler);
            await api.LoginAsync("test", "test"); using var source = new HttpDeadlineAlertSource(api.CaptureSession());
            var initial = await source.ReadAsync(default);
            Check(initial.Items.Single().TaskId == TaskId && initial.Items.Single().EventId == Event, mode + " exact BIGINT/event");
            var result = await source.ConfirmAsync(Target(),default);
            if (mode is "normal" or "list-fail")
            {
                Check(result.State == DeadlineConfirmationState.Confirmed, mode + " success");
                if (mode == "list-fail") Check(result.Snapshot is null,"list failure cannot undo a receipt");
                await source.ConfirmAsync(Target(),default); Check(handler.Posts==1,"no repeated success POST");
            }
            else
            {
                Check(result.State==DeadlineConfirmationState.Unknown,mode+" no invented success");
                try { await source.ConfirmAsync(Target(),default); throw new Exception("duplicate POST allowed"); }
                catch(ApiException e) when(e.StatusCode==409){checks++;}
                if(mode is "expired" or "off" or "unauthorized")
                {
                    try { await source.ResolveConfirmationAsync(Target(),default); throw new Exception("failure accepted"); }
                    catch(ApiException e){Check(e.StatusCode==(mode=="expired"?410:mode=="off"?503:401),mode+" failure distinct");}
                }
                else
                {
                    var resolved=await source.ResolveConfirmationAsync(Target(),default);
                    Check(resolved.State==(mode is "not-recorded" or "retry-version"?DeadlineConfirmationState.NotRecorded:DeadlineConfirmationState.Confirmed),mode+" recovery");
                    if(mode is "not-recorded" or "retry-version")
                    {
                        Check(handler.Posts==1 && handler.Keys==1,"lookup performs no automatic POST/key");
                        await source.ConfirmAsync(Target(),default);
                        Check(handler.Keys==1 && handler.Bodies.Count==2 && handler.Bodies[0]==handler.Bodies[1] && handler.PostKeys.Distinct().Count()==1,"explicit retry keeps original key/body after version change");
                    }
                }
            }
            Check(handler.Targets.All(t=>t==Target()),mode+" exact target on key/result");
            Check(handler.Bodies.All(b=>JsonDocument.Parse(b).RootElement.GetProperty("task_id").GetString()==TaskId),mode+" task_id in body");
        }
        foreach(var mode in new[]{"numeric-bigint","string-revision","bad-target","bad-uuid","duplicate","wrong-owner","missing-field","event-task-alias"})
        {
            using var api=new ApiClient("http://127.0.0.1:1",new Fake(mode));await api.LoginAsync("test","test");
            using var source=new HttpDeadlineAlertSource(api.CaptureSession());
            try {await source.ReadAsync(default);throw new Exception(mode+" accepted");}catch(InvalidDataException){checks++;}
        }
        using(var handler=new Fake("normal"))
        using(var api=new ApiClient("http://127.0.0.1:1",handler))
        {
            await api.LoginAsync("test","test");using var source=new HttpDeadlineAlertSource(api.CaptureSession());
            await source.ReadAsync(default);await source.ConfirmAsync(Target(),default);
            handler.Revision=3;handler.Saved=false;
            var newer=await source.ReadAsync(default);
            Check(!newer.Items.Single().IsConfirmed && newer.Items.Single().EventToken==Target(3),"same UUID new assignment is a new confirmation target");
            await source.ConfirmAsync(Target(3),default);
            Check(handler.Posts==2 && handler.Targets.Contains(Target(3)),"new assignment receives its own key/POST");
            await source.ConfirmAsync(Target(),default);Check(handler.Posts==2,"old cached success never confirms another generation");
            try {await source.ConfirmAsync(Event,default);throw new Exception("legacy raw event converted");}catch(InvalidDataException){checks++;}
        }
    }
    private sealed class Fault(string mode):DelegatingHandler(new HttpClientHandler { UseCookies=false,AllowAutoRedirect=false })
    {
        public int Posts; public bool FailReads;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            if(FailReads && request.Method==HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("deadline-alerts"))return Error(503);
            var response=await base.SendAsync(request,ct);
            if(request.Method==HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/confirm"))
            {
                Posts++;
                if(mode=="list-fail")FailReads=true;
                if(mode=="lost" && Posts==1){response.Dispose();throw new HttpRequestException("test lost committed response");}
            }
            return response;
        }
    }
    private static HttpResponseMessage Ok(object data)=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{ok=true,data}),Encoding.UTF8,"application/json")};
    private static HttpResponseMessage Error(int status,string code="TEST")=>new((HttpStatusCode)status){Content=new StringContent(JsonSerializer.Serialize(new{ok=false,error=new{code,message="test"}}),Encoding.UTF8,"application/json")};
    private sealed class Fake(string mode):HttpMessageHandler
    {
        public int Posts,Keys,Revision=1;public bool Saved;public List<string> Bodies=[],PostKeys=[],Targets=[];
        private string version="7";
        private object Receipt(bool bad=false)=>new{task_id=TaskId,event_id=bad?"bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb":Event,assignment_revision=mode=="wrong-revision"?2:Revision,confirmed_at="2026-10-22T01:00:00Z"};
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct)
        {
            var path=req.RequestUri!.AbsolutePath;
            if(path.EndsWith("auth/login"))return Ok(new{access_token="synthetic-only",user=new{id=2,login_id="test",display_name="test",role="employee",department="test",is_active=true}});
            if(path.EndsWith("write-requests/keys"))
            {
                Keys++;Targets.Add(JsonDocument.Parse(await req.Content!.ReadAsStringAsync(ct)).RootElement.GetProperty("target").GetString()!);
                return Ok(new{request_key="test-key-"+new string('a',40),expires_at="2026-10-23T01:00:00Z"});
            }
            if(path.EndsWith("/confirm"))
            {
                Posts++;Bodies.Add(await req.Content!.ReadAsStringAsync(ct));PostKeys.Add(req.Headers.GetValues("Idempotency-Key").Single());
                if(mode=="retry-version" && Posts==2)return Error(409,"DEADLINE_EVENT_CHANGED");
                if(Posts==1 && mode is not ("normal" or "list-fail" or "bad-receipt" or "wrong-revision"))throw new HttpRequestException("test lost response");
                Saved=true;return Ok(Receipt(mode=="bad-receipt"));
            }
            if(path.EndsWith("write-requests/result"))
            {
                Targets.Add(JsonDocument.Parse(await req.Content!.ReadAsStringAsync(ct)).RootElement.GetProperty("target").GetString()!);
                if(mode is "expired" or "off" or "unauthorized")return Error(mode=="expired"?410:mode=="off"?503:401);
                if(mode is "not-recorded" or "retry-version"){version="8";return Ok(new{state="not_recorded"});}
                Saved=true;return Ok(new{state="completed",http_status=200,result=new{task_id=TaskId,event_id=Event,assignment_revision=Revision,confirmed_at="2026-10-22T01:00:00Z"},expires_at="2026-10-23T01:00:00Z"});
            }
            if(Saved && mode=="list-fail")return Error(503);
            Dictionary<string,object?> item=new(){["event_id"]=mode=="bad-uuid"?"legacy-event":Event,["target"]=mode=="bad-target"?Target(2):Target(Revision),
                ["task_id"]=mode=="numeric-bigint"?9007199254740993L:TaskId,["row_version"]=version,["assignment_revision"]=mode=="string-revision"?"1":Revision,
                ["assignee_id"]=mode=="wrong-owner"?3:2,["title"]="test",["status"]="in_progress",["cancelled"]=false,["due_date"]="2026-10-23",["confirmed_at"]=Saved?"2026-10-22T01:00:00Z":null};
            if(mode=="missing-field")item.Remove("cancelled");
            if(mode=="event-task-alias")
            {
                var other=new Dictionary<string,object?>(item){["task_id"]="100",["target"]=$"d1:100:{Event}:1"};
                return Ok(new{server_time="2026-10-22T02:00:00Z",items=new[]{item,other}});
            }
            return Ok(new{server_time="2026-10-22T02:00:00Z",items=mode=="duplicate"?new[]{item,item}:[item]});
        }
    }
}
