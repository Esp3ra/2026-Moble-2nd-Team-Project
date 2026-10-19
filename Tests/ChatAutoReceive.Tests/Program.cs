using System.Collections;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Infrastructure;

internal static class Program
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly Assembly Product=typeof(ChatControl).Assembly;
    private static int checks;
    private const string R1="00000000-0000-0000-0000-000000000001", R2="00000000-0000-0000-0000-000000000002";
    private static object New(string name,params object[] args)=>Activator.CreateInstance(Product.GetType("RemoteWork.WinForms.Features.Collaboration."+name)!,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,args,null)!;
    private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Hidden)!.GetValue(target)!;
    private static object Prop(object target,string name)=>target.GetType().GetProperty(name)!.GetValue(target)!;
    private static async Task<object?> Call(object target,string method,params object[] args)
    {
        var task=(Task)target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(target,args)!;
        await task;return task.GetType().GetProperty("Result")?.GetValue(task);
    }
    private static void Check(bool ok,string reason){if(!ok)throw new Exception(reason);checks++;}
    private static async Task Wait(Func<bool> ready,string reason,int ms=6000)
    {
        var until=DateTime.UtcNow.AddMilliseconds(ms);
        while(!ready()&&DateTime.UtcNow<until)await Task.Delay(25);
        Check(ready(),reason);
    }
    private static UserControl View(Form host,object service,int index)
    {
        var view=(UserControl)New("ChatControl",service);
        view.Location=new Point(0,index*460);view.Size=new Size(1080,450);host.Controls.Add(view);view.Show();return view;
    }
    private static string History(UserControl view)=>Field<TextBox>(view,"txtContent2").Text;
    private static DataGridView Grid(UserControl view)=>Field<DataGridView>(view,"gridRooms");
    private static TextBox Input(UserControl view)=>Field<TextBox>(view,"txtContent3");
    private static string? Selected(UserControl view)=>Grid(view).CurrentRow?.Tag is {} room?(string)Prop(room,"Id"):null;
    private static void Select(UserControl view,string id)
    {var row=Grid(view).Rows.Cast<DataGridViewRow>().Single(r=>(string)Prop(r.Tag!,"Id")==id);Grid(view).CurrentCell=row.Cells[1];}
    private static nint TextMessage(TextBox box,uint message,nint value=default)=>(nint)typeof(ChatControl).GetMethod("TextMessage",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,new object[]{box.Handle,message,(nint)0,value})!;
    private static Task Round(UserControl view)=>Call(view,"ReceiveOnceAsync");

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();Control.CheckForIllegalCrossThreadCalls=true;
        int exit=1;
        using var host=new Form{ShowInTaskbar=false,Location=new Point(-20000,-20000),Size=new Size(1120,990),StartPosition=FormStartPosition.Manual};
        host.Shown+=async(_,_)=>
        {
            try
            {
                if(args.Length==2&&args[0]=="--http")await Live(host,args[1]);else await Double(host);
                Console.WriteLine($"PASS {checks} automatic receive checks; actual WinForms + "+(args.Length>0?"TCP HTTP/database, two accounts on one PC":"explicit HTTP double"));exit=0;
            }
            catch(Exception ex){Console.Error.WriteLine(ex);}
            finally{host.Close();}
        };
        Application.Run(host);return exit;
    }

    private static async Task Double(Form host)
    {
        using var fake=new Handler();
        using var a=new ApiClient("http://127.0.0.1:1",fake);
        using var b=new ApiClient("http://127.0.0.1:1",fake);
        var sa=New("HttpChatService",a,(await a.LoginAsync("staff1","synthetic")).User);
        var sb=New("HttpChatService",b,(await b.LoginAsync("admin1","synthetic")).User);
        using var va=View(host,sa,0);using var vb=View(host,sb,1);
        await Wait(()=>Field<bool>(va,"autoReceiveLoaded")&&Field<bool>(vb,"autoReceiveLoaded"),"Both views loaded");
        Check(Grid(va).Rows.Count==0,"Initial list empty");
        fake.AddRoom(R1);
        await Wait(()=>Grid(va).Rows.Count==1&&Grid(vb).Rows.Count==1,"Invitation appears on both views without navigation");
        fake.AddMessage(R1,"new automatic message");
        await Wait(()=>History(va).Contains("new automatic message")&&History(vb).Contains("new automatic message"),"Message received automatically");
        Input(va).Text="unsent draft";Input(va).Select(3,4);
        fake.AddMessage(R1,"next automatic message");
        await Wait(()=>History(va).Contains("next automatic message"),"Second message received");
        Check(Input(va).Text=="unsent draft"&&Input(va).SelectionStart==3&&Input(va).SelectionLength==4,"Composer and selection preserved");
        var text=Field<TextBox>(va,"txtContent2");text.Select(0,4);
        int before=fake.CountA;await Wait(()=>fake.CountA>=before+3,"Polling continues");
        Check(text.SelectionStart==0&&text.SelectionLength==4,"Unchanged history does not reset selection");
        for(int i=0;i<80;i++)fake.AddMessage(R1,"scroll line "+i);
        await Wait(()=>History(va).Contains("scroll line 79"),"Long history arrived");
        text.Select(0,5);text.ScrollToCaret();int first=(int)TextMessage(text,0xCE);
        fake.AddMessage(R1,"do not jump");await Wait(()=>History(va).Contains("do not jump"),"Message arrives while reading old history");
        Check((int)TextMessage(text,0xCE)==first&&text.SelectionStart==0&&text.SelectionLength==5,"Scroll and selected history preserved");
        fake.AddRoom(R2);fake.AddMessage(R2,"room two");
        await Wait(()=>Grid(va).Rows.Count==2,"New room auto appears");
        Check(Selected(va)==R1&&Input(va).Text=="unsent draft","New invitation does not steal room or draft");

        fake.Gate(R1);await Wait(()=>fake.GateEntered,"Old-room poll delayed");
        Select(va,R2);await Wait(()=>History(va).Contains("room two"),"Manual switch still works during polling");
        Input(va).Text="room two draft";fake.Release();await Task.Delay(100);
        Check(Selected(va)==R2&&!History(va).Contains("scroll line"),"Late old-room result discarded");
        Check(Input(va).Text=="room two draft","Late result does not replace draft");

        fake.Gate(R2);await Wait(()=>fake.GateEntered,"One poll remains in flight");
        before=fake.CountA;await Round(va);await Task.Delay(1150);
        Check(fake.CountA==before,"No overlapping poll on timer ticks");
        Input(va).Text="send during poll";Field<Button>(va,"btnSendMessage").PerformClick();
        await Wait(()=>History(va).Contains("send during poll"),"Sending succeeds while old poll is delayed");
        fake.Release();await Task.Delay(100);
        Check(History(va).Contains("send during poll"),"Old poll cannot overwrite sent message");

        va.Visible=false;before=fake.CountA;await Task.Delay(1300);
        Check(fake.CountA==before,"Hidden chat stops polling");
        fake.AddMessage(R2,"received after reopening");va.Visible=true;
        await Wait(()=>History(va).Contains("received after reopening"),"Reopened chat resumes polling");
        Input(va).Text="keep on error";fake.OfflineA=true;
        await Round(va);Check(Field<System.Windows.Forms.Timer>(va,"receiveTimer").Interval>=2000,"Transient error backs off");
        Check(History(va).Contains("send during poll")&&Input(va).Text=="keep on error","Transient error retains history and draft");
        fake.OfflineA=false;await Round(va);
        Check(Field<System.Windows.Forms.Timer>(va,"receiveTimer").Interval==1000,"Recovery restores one-second cadence");

        fake.AddMessage(R2,"must wait until visible");fake.Gate(R2);await Wait(()=>fake.GateEntered,"Delayed poll before hide");
        va.Visible=false;string previous=History(va);fake.Release();await Task.Delay(150);
        Check(History(va)==previous,"Hidden view ignores late response");
        va.Visible=true;await Wait(()=>History(va).Contains("must wait until visible"),"Visible view catches up again");
        a.ClearIdentity();await Wait(()=>!Field<System.Windows.Forms.Timer>(va,"receiveTimer").Enabled,"Logout stops timer");
        before=fake.CountA;await Task.Delay(1250);Check(fake.CountA==before,"No requests after logout");
        va.Dispose();va.Dispose();Check(true,"Repeated dispose safe");
    }

    private static async Task Live(Form host,string root)
    {
        using var a=new ApiClient(root);using var b=new ApiClient(root);
        var userA=(await a.LoginAsync("staff1","Staff-test-2026!")).User;
        var userB=(await b.LoginAsync("admin1","Admin-test-2026!")).User;
        var sa=New("HttpChatService",a,userA);var sb=New("HttpChatService",b,userB);
        using var va=View(host,sa,0);using var vb=View(host,sb,1);
        await Wait(()=>Field<bool>(va,"autoReceiveLoaded")&&Field<bool>(vb,"autoReceiveLoaded"),"Live views loaded");
        var request=New("CreateChatRoomRequest","Automatic receive",false,new[]{userB.Id.ToString()});
        var room=await Call(sa,"CreateRoomAsync",request,CancellationToken.None);string id=(string)Prop(room!,"Id");
        await Wait(()=>Selected(va)==id&&Selected(vb)==id,"New room appears on both actual clients");
        await Wait(()=>!Field<bool>(va,"loadingMessages")&&!Field<bool>(vb,"loadingMessages"),"Both can compose");
        Input(vb).Text="B draft";
        Input(va).Text="actual A message 😀";Field<Button>(va,"btnSendMessage").PerformClick();
        await Wait(()=>History(vb).Contains("actual A message 😀"),"A -> B automatic TCP/DB receipt");
        Check(Input(vb).Text=="B draft","Live receipt preserves B draft");
        Input(vb).Text="actual B reply";Field<Button>(vb,"btnSendMessage").PerformClick();
        await Wait(()=>History(va).Contains("actual B reply"),"B -> A automatic TCP/DB receipt");
        await a.RefreshIdentityAsync();
        await Call(sb,"SendMessageAsync",id,"after auth me",CancellationToken.None);
        await Wait(()=>History(va).Contains("after auth me"),"Live same-session refresh tolerated");
        Check(Selected(va)==id&&Selected(vb)==id,"No manual room reselection");
        await Call(sa,"DeleteRoomAsync",id,CancellationToken.None);
        await Wait(()=>Grid(va).Rows.Count==0,"External personal hide is observed automatically");
        Check(Grid(vb).Rows.Count==1&&History(vb).Contains("actual A message"),"Other account history remains");
        a.ClearIdentity();await Wait(()=>!Field<System.Windows.Forms.Timer>(va,"receiveTimer").Enabled,"Live logout stops reception");
    }

    private sealed class Handler:HttpMessageHandler
    {
        private readonly Dictionary<string,List<object>> messages=new();
        public int CountA;public bool OfflineA;
        private string? gateRoom;private TaskCompletionSource? gateRelease;
        public bool GateEntered;
        public void Gate(string room){gateRoom=room;gateRelease=new(TaskCreationOptions.RunContinuationsAsynchronously);GateEntered=false;}
        public void Release(){gateRelease?.TrySetResult();gateRoom=null;}
        public void AddRoom(string id)=>messages.Add(id,new());
        public void AddMessage(string room,string text)=>messages[room].Add(new{id=Guid.NewGuid(),room_id=room,sender_id=2,sender_name="B",content=text,sent_at=DateTimeOffset.UtcNow});
        private static object User(int id)=>new{id,login_id="test",display_name=id==1?"A":"B",employee_no="T",department="dev",role="employee",is_active=true};
        private static object Room(string id)=>new{id,name=id==R1?"one":"two",is_group=false,participants=new[]{new{id=1,name="A",department="dev"},new{id=2,name="B",department="dev"}}};
        private static HttpResponseMessage Response(object data)=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{ok=true,data,request_id="test"}),Encoding.UTF8,"application/json")};
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct)
        {
            string path=req.RequestUri!.AbsolutePath;
            if(path.EndsWith("auth/login"))
            {
                string body=await req.Content!.ReadAsStringAsync(ct);int id=body.Contains("staff1")?1:2;
                return Response(new{access_token="synthetic-"+id,token_type="Bearer",expires_at=DateTimeOffset.UtcNow.AddHours(1),user=User(id)});
            }
            int actor=req.Headers.Authorization?.Parameter=="synthetic-1"?1:2;
            if(actor==1)CountA++;
            if(OfflineA&&actor==1)return new(HttpStatusCode.ServiceUnavailable){Content=new StringContent("temporary offline")};
            if(path.EndsWith("/rooms"))return Response(new{items=messages.Keys.Order().Select(Room).ToArray(),next_cursor=(string?)null});
            string room=path.Split('/')[5];
            if(req.Method==HttpMethod.Post)
            {
                using var body=JsonDocument.Parse(await req.Content!.ReadAsStringAsync(ct));
                var message=new{id=body.RootElement.GetProperty("message_id").GetString(),room_id=room,sender_id=actor,sender_name=actor==1?"A":"B",content=body.RootElement.GetProperty("content").GetString(),sent_at=DateTimeOffset.UtcNow};
                messages[room].Add(message);return Response(new{message});
            }
            var snapshot=messages[room].ToArray();
            if(actor==1&&gateRoom==room&&!GateEntered){GateEntered=true;await gateRelease!.Task;}
            return Response(new{items=snapshot,next_cursor=(string?)null});
        }
    }
}
