using System.Net;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Features.Collaboration;

internal static class Program
{
    private static int passed, failed;
    private const string Room = "00000000-0000-0000-0000-000000000001";
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static async Task Case(string name, Func<Task> run)
    {
        try { await run(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex.GetType().Name + " " + ex.Message); }
    }
    private static async Task Canceled(Func<Task> run)
    { try { await run(); } catch (OperationCanceledException) { return; } throw new Exception("Old session accepted"); }
    private static async Task Rejected(Func<Task> run)
    { try { await run(); } catch (Exception ex) when (ex is ApiException or ArgumentException) { return; } throw new Exception("Invalid text accepted"); }
    private static async Task Operate(HttpChatService service, string operation)
    {
        switch (operation)
        {
            case "participants": await service.GetAvailableParticipantsAsync(); break;
            case "rooms": await service.GetRoomsAsync(); break;
            case "messages": await service.GetMessagesAsync(Room); break;
            case "create": await service.CreateRoomAsync(new("room", false, new[] {"2"})); break;
            case "send": await service.SendMessageAsync(Room,"hello"); break;
            case "hide": await service.DeleteRoomAsync(Room); break;
        }
    }
    private static async Task SessionTest(string operation, string change, bool pages)
    {
        using var handler = new Handler { ManyPages = pages };
        using var api = new ApiClient("http://127.0.0.1:1", handler);
        var user = (await api.LoginAsync("staff1", "synthetic")).User;
        var service = new HttpChatService(api,user);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool blocked = false;
        handler.Before = async req =>
        {
            if (blocked || (pages && !req.RequestUri!.Query.Contains(operation == "participants" ? "page=2" : "after="))) return;
            // Message first cursor is after=0, so stop at page 2 only.
            if (pages && operation == "messages" && req.RequestUri!.Query.Contains("after=0")) return;
            blocked = true; entered.SetResult(); await release.Task;
        };
        var pending = Operate(service,operation);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var original = api.CaptureSession();
        if (change == "refresh") { await api.RefreshIdentityAsync(); Check(original.IsCurrent,"Refresh replaced session"); }
        else if (change == "logout") api.ClearIdentity();
        else await api.LoginAsync(change == "same-login" ? "staff1" : "staff2", "synthetic");
        release.SetResult();
        if (change == "refresh") await pending; else await Canceled(()=>pending);
        if (change != "refresh")
        {
            int before = handler.ChatRequests;
            await Canceled(()=>Operate(service,operation));
            Check(handler.ChatRequests==before,"Old service sent using a new session");
        }
    }
    private static async Task TextTest(string text, bool valid)
    {
        using var handler = new Handler { MessageText = text };
        using var api = new ApiClient("http://127.0.0.1:1",handler);
        var user=(await api.LoginAsync("staff1","synthetic")).User;
        var service=new HttpChatService(api,user);
        if (valid)
        {
            Check((await service.GetMessagesAsync(Room)).Single().Content==text,"Read text changed");
            Check((await service.SendMessageAsync(Room,text)).Content==text,"Send text changed");
        }
        else
        {
            await Rejected(async()=>await service.GetMessagesAsync(Room));
            int before=handler.ChatRequests;
            await Rejected(async()=>await service.SendMessageAsync(Room,text));
            Check(handler.ChatRequests==before,"Invalid outgoing body reached server");
        }
    }
    private static async Task Live(string root)
    {
        int checks=0;
        void Confirm(bool ok,string reason){Check(ok,reason);checks++;}
        using var staffApi=new ApiClient(root);
        using var adminApi=new ApiClient(root);
        var staff=new HttpChatService(staffApi,(await staffApi.LoginAsync("staff1","Staff-test-2026!")).User);
        var admin=new HttpChatService(adminApi,(await adminApi.LoginAsync("admin1","Admin-test-2026!")).User);
        var people=await staff.GetAvailableParticipantsAsync();
        Confirm(people.Any(p=>p.Id==admin.CurrentUser.Id),"Active DB admin missing");
        Confirm(people.All(p=>p.Id!=staff.CurrentUser.Id),"Self included");
        var room=await staff.CreateRoomAsync(new("HTTP test",false,new[]{admin.CurrentUser.Id}));
        Confirm(room.Participants.Count==2,"Wrong participants");
        string text=string.Concat(Enumerable.Repeat("😀",2000));
        var message=await staff.SendMessageAsync(room.Id,text);
        Confirm(message.Content==text,"Actual saved content differs");
        Confirm((await staff.GetMessagesAsync(room.Id)).Single().Content==text,"Actual MySQL read failed");
        await staffApi.RefreshIdentityAsync();
        Confirm((await staff.GetRoomsAsync()).Any(r=>r.Id==room.Id),"Same-session refresh rejected");
        Confirm((await admin.GetRoomsAsync()).Any(r=>r.Id==room.Id),"Other participant cannot see room");
        Confirm((await admin.GetMessagesAsync(room.Id)).Single().Id==message.Id,"Other participant cannot read saved message");
        using var reloginApi=new ApiClient(root);
        var relogin=new HttpChatService(reloginApi,(await reloginApi.LoginAsync("staff1","Staff-test-2026!")).User);
        Confirm((await relogin.GetRoomsAsync()).Any(r=>r.Id==room.Id),"New client did not restore rooms");
        Confirm((await relogin.GetMessagesAsync(room.Id)).Single().Content==text,"New client did not restore messages");
        await relogin.DeleteRoomAsync(room.Id);
        Confirm((await relogin.GetRoomsAsync()).All(r=>r.Id!=room.Id),"Personal hide failed");
        Confirm((await admin.GetRoomsAsync()).Any(r=>r.Id==room.Id),"Hide removed other's room");
        Confirm((await admin.GetMessagesAsync(room.Id)).Single().Content==text,"Hide deleted other's history");
        await admin.SendMessageAsync(room.Id,"after hide");
        Confirm((await relogin.GetRoomsAsync()).All(r=>r.Id!=room.Id),"Hidden room reappeared");
        await Rejected(async()=>await admin.SendMessageAsync(room.Id,text+"a"));
        Confirm((await admin.GetMessagesAsync(room.Id)).Count==2,"Rejected message was stored");
        Console.WriteLine($"LIVE {checks} checks: actual ApiClient -> TCP HTTP -> owned MySQL; two accounts on one PC");
    }
    public static async Task<int> Main(string[] args)
    {
        if(args.Length==2 && args[0]=="--http")
        {
            await Case("actual-http-mysql",()=>Live(args[1]));
            return failed==0?0:1;
        }
        foreach(string op in new[]{"participants","rooms","messages","create","send","hide"})
            foreach(string change in new[]{"refresh","logout","same-login","other-login"})
                await Case($"session/{op}/{change}",()=>SessionTest(op,change,false));
        foreach(string op in new[]{"participants","rooms","messages"})
            foreach(string change in new[]{"refresh","same-login"})
                await Case($"pagination/{op}/{change}",()=>SessionTest(op,change,true));
        foreach(var item in new[]{("ASCII", "a"), ("Korean","가"), ("emoji","😀")})
            foreach(int n in new[]{2000,2001})
                await Case($"length/{item.Item1}/{n}",()=>TextTest(string.Concat(Enumerable.Repeat(item.Item2,n)),n==2000));
        await Case("length/reported-emoji1001",()=>TextTest(string.Concat(Enumerable.Repeat("😀",1001)),true));
        await Case("length/mixed2000",()=>TextTest(new string('가',1000)+string.Concat(Enumerable.Repeat("😀",1000)),true));
        await Case("length/mixed2001",()=>TextTest(new string('a',1001)+string.Concat(Enumerable.Repeat("😀",1000)),false));
        await Case("write/unknown-result-reuses-id",async()=>
        {
            using var handler=new Handler { LoseFirstSend=true };
            using var api=new ApiClient("http://127.0.0.1:1",handler);
            var service=new HttpChatService(api,(await api.LoginAsync("staff1","synthetic")).User);
            await Rejected(async()=>await service.SendMessageAsync(Room,"hello"));
            await api.RefreshIdentityAsync();
            await service.SendMessageAsync(Room,"hello");
            Check(handler.SentIds.Count==2 && handler.SentIds.Distinct().Count()==1,"Retry got a new UUID");
        });
        Console.WriteLine($"RESULT {passed} passed, {failed} failed (explicit HTTP double; no real DB)");
        return failed==0?0:1;
    }
    private sealed class Handler : HttpMessageHandler
    {
        public Func<HttpRequestMessage,Task>? Before;
        public bool ManyPages, LoseFirstSend;
        public int ChatRequests;
        public string MessageText="hello";
        public List<string> SentIds=new();
        private int loggedIn=1;
        private static object User(int id)=>new {id,login_id="synthetic",display_name="user",employee_no="T",department="dev",role="employee",is_active=true};
        private static HttpResponseMessage Ok(object data)=>new(HttpStatusCode.OK) {Content=new StringContent(JsonSerializer.Serialize(new {ok=true,data,request_id="test"}),Encoding.UTF8,"application/json")};
        private static object RoomData(string id)=>new {id,name="room",is_group=false,participants=new[]{new{id=1,name="A",department="dev"},new{id=2,name="B",department="dev"}}};
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct)
        {
            string path=req.RequestUri!.AbsolutePath;
            if(path.EndsWith("auth/login"))
            {
                string body=await req.Content!.ReadAsStringAsync(ct); loggedIn=body.Contains("staff2")?2:1;
                return Ok(new {access_token="synthetic-"+Guid.NewGuid(),token_type="Bearer",expires_at=DateTimeOffset.UtcNow.AddHours(1),user=User(loggedIn)});
            }
            if(path.EndsWith("auth/me")) return Ok(new {user=User(loggedIn),expires_at=DateTimeOffset.UtcNow.AddHours(1)});
            ChatRequests++;
            if(Before is not null)await Before(req); // Deliberately ignore ct to deliver a stale late response.
            if(path.EndsWith("participants"))
            {
                bool second=req.RequestUri.Query.Contains("page=2");
                return Ok(new {items=Enumerable.Range(second?102:2,ManyPages?(second?1:100):1).Select(id=>new{id,display_name="candidate",department="dev"}),page=second?2:1,limit=100,total=ManyPages?101:1});
            }
            if(path.EndsWith("/hide")) return Ok(new {message="hidden"});
            if(path.EndsWith("/messages"))
            {
                if(req.Method==HttpMethod.Post)
                {
                    using var body=JsonDocument.Parse(await req.Content!.ReadAsStringAsync(ct));
                    Check(!body.RootElement.TryGetProperty("attachment_ids",out _), "Plain messages retain the deployed legacy chat contract");
                    string id=body.RootElement.GetProperty("message_id").GetString()!;
                    string content=body.RootElement.GetProperty("content").GetString()!;
                    SentIds.Add(id);
                    if(LoseFirstSend && SentIds.Count==1) throw new HttpRequestException("Synthetic response loss");
                    return Ok(new {message=new{id,room_id=Room,sender_id=1,sender_name="A",content,sent_at=DateTimeOffset.UtcNow}});
                }
                bool second=req.RequestUri.Query.Contains("after=100");
                return Ok(new {items=Enumerable.Range(second?101:1,ManyPages?(second?1:100):1).Select(n=>new{id=$"00000000-0000-0000-0000-{n:000000000000}",room_id=Room,sender_id=1,sender_name="A",content=MessageText,sent_at=DateTimeOffset.UtcNow}),next_cursor=ManyPages&&!second?"100":null});
            }
            if(req.Method==HttpMethod.Post)
            {
                using var body=JsonDocument.Parse(await req.Content!.ReadAsStringAsync(ct));
                return Ok(new {room=RoomData(body.RootElement.GetProperty("room_id").GetString()!)});
            }
            bool after=req.RequestUri.Query.Contains("after=");
            return Ok(new {items=Enumerable.Range(after?101:1,ManyPages?(after?1:100):1).Select(n=>RoomData($"00000000-0000-0000-0000-{n:000000000000}")),next_cursor=ManyPages&&!after?"00000000-0000-0000-0000-000000000100":null});
        }
    }
}
