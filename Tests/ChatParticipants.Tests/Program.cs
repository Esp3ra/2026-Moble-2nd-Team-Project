using System.Collections;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private static int checks;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Assembly Product = typeof(ChatControl).Assembly;
    private static Type Model(string name) => Product.GetType("RemoteWork.WinForms.Features.Collaboration." + name)!;
    private static object New(string name, params object[] args) => Activator.CreateInstance(Model(name),
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null)!;
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, Hidden)!.GetValue(obj)!;
    private static object Prop(object obj, string name) => obj.GetType().GetProperty(name)!.GetValue(obj)!;
    private static async Task<object> Call(object obj, string method, params object[] args)
    {
        var task = (Task)obj.GetType().GetMethod(method)!.Invoke(obj, args)!;
        await task;
        return task.GetType().GetProperty("Result")?.GetValue(task)!;
    }
    private static object[] Items(object value) => ((IEnumerable)value).Cast<object>().ToArray();
    private static void Check(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); checks++; }
    private static async Task Reject(Func<Task> action, string reason)
    { try { await action(); } catch (Exception ex) when (ex is ApiException or ArgumentException or OperationCanceledException) { checks++; return; } throw new InvalidOperationException(reason); }

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Control.CheckForIllegalCrossThreadCalls = true;
        int exit = 1;
        using var host = new Form { ShowInTaskbar = false, Location = new Point(-20000,-20000),
            StartPosition = FormStartPosition.Manual };
        host.Shown += async (_, _) =>
        {
            try { await Run(host, args.FirstOrDefault()); Console.WriteLine($"PASS: {checks} chat participant checks; actual WinForms and " + (args.Length > 0 ? "loopback HTTP/database." : "explicit HTTP double.")); exit = 0; }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            finally { host.Close(); }
        };
        Application.Run(host);
        return exit;
    }

    private static async Task Run(Form host, string? baseUrl)
    {
        var fake = new DirectoryHandler();
        using var api = new ApiClient(baseUrl ?? "http://127.0.0.1:1", baseUrl is null ? fake : null);
        var auth = await api.LoginAsync("staff1", "Staff-test-2026!");
        var directory = New("HttpChatParticipantDirectory", api, auth.User.Id);
        var user = New("ChatParticipant", auth.User.Id.ToString(), auth.User.DisplayName, auth.User.Department);
        var store = New("InMemoryChatService", user, directory);
        Check(Items(await Call(store, "GetRoomsAsync", CancellationToken.None)).Length == 0, "No sample rooms for authenticated chat");
        var candidates = Items(await Call(store, "GetAvailableParticipantsAsync", CancellationToken.None));
        Check(candidates.Length == (baseUrl is null ? 102 : 2), "Read complete active directory");
        Check(candidates.All(p => (string)Prop(p,"Id") != auth.User.Id.ToString()), "Exclude current server ID");
        Check(candidates.All(p => !((string)Prop(p,"Id")).StartsWith("sample-")), "Never use sample participants");
        if (baseUrl is null) Check(fake.Pages.SequenceEqual(new[] {1,2}), "Fetch all pages");
        using (var form = (Form)New("CreateChatRoomForm", store))
        {
            form.Show(host);
            var list = Field<CheckedListBox>(form,"lstParticipants");
            for (int n=0; n<300 && list.Items.Count==0 && Field<Label>(form,"lblError").Text.Length==0; n++) await Task.Delay(10);
            Check(list.Items.Count == candidates.Length,"Actual invitation UI uses DB candidates");
            Check(list.Items.Cast<object>().All(x => x.ToString()!.Contains("(#")),"IDs distinguish duplicate display names");
            form.Close();
        }
        string chosen=(string)Prop(candidates[0],"Id");
        var request=New("CreateChatRoomRequest","테스트 대화",false,new[] {chosen});
        var room=await Call(store,"CreateRoomAsync",request,CancellationToken.None);
        Check(Items(Prop(room,"Participants")).Length==2,"Current user plus selected server user");
        Check(Items(Prop(room,"Participants")).Any(p=>(string)Prop(p,"Id")==chosen),"Selected ID retained");
        await Call(store,"SendMessageAsync",Prop(room,"Id"),"기존 로컬 메시지",CancellationToken.None);
        Check(Items(await Call(store,"GetMessagesAsync",Prop(room,"Id"),CancellationToken.None)).Length==1,"Existing local messages preserved");
        using (var chat=(UserControl)New("ChatControl",store))
        {
            var text=Field<TextBox>(chat,"txtContent3");
            var send=Field<Button>(chat,"btnSendMessage");
            chat.GetType().GetField("selectedRoom",Hidden)!.SetValue(chat,room);
            Check(text.MaxLength==4000,"UI allows 2,000 supplementary code points");
            foreach(var unit in new[]{"a","가","😀"})
            {
                text.Text=string.Concat(Enumerable.Repeat(unit,2000));
                Check(send.Enabled,"UI accepts 2,000 code points: "+unit);
                text.Text=string.Concat(Enumerable.Repeat(unit,2001));
                Check(!send.Enabled,"UI rejects 2,001 code points: "+unit);
            }
        }
        if (baseUrl is null)
        {
            fake.RemoveId=int.Parse(chosen);
            await Reject(async()=>await Call(store,"CreateRoomAsync",request,CancellationToken.None),"Reject newly inactive participant");
            Check(Items(await Call(store,"GetRoomsAsync",CancellationToken.None)).Length==1,"No room created for rejected user");
            fake.RemoveId=0;fake.FailSecondPage=true;
            await Reject(async()=>await Call(directory,"GetParticipantsAsync",CancellationToken.None),"Page2 error must reject complete lookup");
            using var badForm=(Form)New("CreateChatRoomForm",store);badForm.Show(host);
            for(int n=0;n<300 && Field<Label>(badForm,"lblError").Text.Length==0;n++)await Task.Delay(10);
            Check(Field<CheckedListBox>(badForm,"lstParticipants").Items.Count==0,"No partial or sample UI on lookup error");
            Check(!Field<Button>(badForm,"btnCreate").Enabled,"Cannot create from failed lookup");badForm.Close();
            fake.FailSecondPage=false;
        }
        api.ClearIdentity();
        await Reject(async()=>await Call(directory,"GetParticipantsAsync",CancellationToken.None),"Reject directory after logout");
    }

    private sealed class DirectoryHandler : HttpMessageHandler
    {
        public List<int> Pages {get;}=new();
        public int RemoveId {get;set;}
        public bool FailSecondPage {get;set;}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct)
        {
            object data;
            if(req.RequestUri!.AbsolutePath.EndsWith("auth/login"))
                data=new {access_token="synthetic-chat-token",token_type="Bearer",expires_at=DateTimeOffset.UtcNow.AddHours(1),user=new {id=1,login_id="staff1",display_name="테스트 직원",employee_no="T1",department="개발팀",role="employee",is_active=true}};
            else
            {
                Check(req.RequestUri.AbsolutePath=="/api/v1/chat/participants","Dedicated participant route");
                Check(req.Headers.Authorization?.Parameter=="synthetic-chat-token","Shared authenticated client");
                int page=req.RequestUri.Query.Contains("page=2&")?2:1;Pages.Add(page);
                if(FailSecondPage&&page==2)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable){Content=new StringContent("{}")});
                var all=Enumerable.Range(2,102).Where(id=>id!=RemoveId).Select(id=>new {id,display_name="동명이인",department=id%2==0?"개발팀":"디자인팀"}).ToArray();
                data=new {items=all.Skip((page-1)*100).Take(100).ToArray(),page,limit=100,total=all.Length};
            }
            string json=JsonSerializer.Serialize(new {ok=true,data,request_id="chat-test"});
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")});
        }
    }
}
