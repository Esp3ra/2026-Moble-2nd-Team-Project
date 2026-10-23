using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;
using RemoteWork.WinForms.Features.Attendance;

internal static class RecoveryReentryChecks
{
    private const string Recovery = "bf514df7-f4eb-47b3-b542-a68b4de26a58";
    private const string Device = "123e4567-e89b-42d3-a456-426614174000";
    private const string Job = "31593412-67fd-40d2-878b-868d15f99a11";
    private const string Opaque = "cHVibGljLXN5bnRoZXRpYw";
    private static readonly UserInfo Admin = new(1,"admin-test","QA admin","QA1","QA","admin",true);
    private static readonly UserInfo Existing = new(88,"existing-test","QA member","QA88","QA","employee",true);
    private static int checks;
    private static bool proofSaved;
    private static void Check(bool value,string text) { checks++;if(!value)throw new InvalidOperationException(text); }
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(owner)!;
    private static Task Call(object owner)=>(Task)owner.GetType().GetMethod("RecoverSelectedUser",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(owner,null)!;
    private static void Pump(Func<bool> done)
    {
        var watch=Stopwatch.StartNew();
        while(!done()){Application.DoEvents();Thread.Sleep(2);if(watch.Elapsed.TotalSeconds>12)throw new TimeoutException("Recovery UI did not settle");}
        Application.DoEvents();
    }
    private static void Complete(Task task){Pump(()=>task.IsCompleted);task.GetAwaiter().GetResult();}
    private static UserManagementForm Owner(ApiClient api,ICreatedUserEnrollmentLauncher launcher,bool consent=true)
    {
        Complete(api.LoginAsync("admin-test","synthetic"));
        var owner=new UserManagementForm(api,launcher,(user,id)=>{Check(user.Id==Existing.Id && id==Recovery,"Explicit confirmation uses selected server member/recovery");return consent;}){ShowInTaskbar=false,Opacity=0};
        owner.Show();Pump(()=>!Field<bool>(owner,"loading"));
        var grid=Field<DataGridView>(owner,"grid");
        grid.CurrentCell=grid.Rows.Cast<DataGridViewRow>().Single(row=>row.DataBoundItem is UserInfo user && user.Id==Existing.Id).Cells[0];
        Application.DoEvents();
        Check(Field<TextBox>(owner,"login").Text==Existing.LoginId,"Displayed member follows the newly selected grid row");
        Field<TextBox>(owner,"recoveryId").Text=Recovery;
        Check(!Field<Button>(owner,"enroll").Enabled && Field<Button>(owner,"recover").Enabled,"Existing member has recovery-only action, never created-member ordinary action");
        string? proof=Environment.GetEnvironmentVariable("RECOVERY_UI_PROOF_DIRECTORY");
        if(!proofSaved && !string.IsNullOrWhiteSpace(proof))
        {
            Directory.CreateDirectory(proof);using var bitmap=new Bitmap(owner.Width,owner.Height);
            owner.DrawToBitmap(bitmap,new Rectangle(Point.Empty,owner.Size));bitmap.Save(Path.Combine(proof,"existing-member-recovery.png"));proofSaved=true;
        }
        return owner;
    }
    public static void Run()
    {
        foreach(string mode in new[]{"success","bad_id","missing","inactive","duplicate","bad_page","cancel","uncertain","selection","logout","close"}) Guard(mode);
        Modal(false);Modal(true);
        Console.WriteLine($"PASS existing-member recovery: {checks} checks; real WinForms + synthetic HTTP/channel; no live account/device/DB changes.");
    }
    private sealed class Launcher : ICreatedUserEnrollmentLauncher
    {
        public int Calls;public bool Uncertain;public CreatedUserEnrollmentTarget? Last;
        public Task<EnrollmentLaunchResult> ShowAsync(IWin32Window owner,CreatedUserEnrollmentTarget target,Uri server)
        {target.ThrowIfChanged();Calls++;Last=target;Check(target.UserId==Existing.Id && target.RequiredRecoveryId==Recovery,"Recovery handoff retains exact verified member and required recovery");return Task.FromResult(new EnrollmentLaunchResult(false,Uncertain));}
    }
    private static void Guard(string mode)
    {
        using var handler=new Server();using var api=new ApiClient("http://reentry.test:5000",handler);
        var launcher=new Launcher {Uncertain=mode=="uncertain"};using var owner=Owner(api,launcher,mode!="cancel");
        int before=handler.UserReads;
        if(mode=="bad_id")Field<TextBox>(owner,"recoveryId").Text="not-a-uuid";
        if(mode is "missing" or "inactive" or "duplicate" or "bad_page")handler.UserMode=mode;
        if(mode is "selection" or "logout" or "close")handler.Gate=new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task operation=Call(owner);
        if(handler.Gate is not null)
        {
            Pump(()=>handler.UserReads>before);
            if(mode=="selection")owner.GetType().GetMethod("Fill",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(owner,[Admin,true]);
            else if(mode=="logout")api.ClearIdentity();else owner.Close();
            handler.Gate.SetResult();
        }
        Complete(operation);
        if(mode is "success" or "uncertain")
        {
            Check(launcher.Calls==1 && handler.UserReads==before+1,"Existing member is freshly read before opening");
            Check(handler.UserWrites==0 && handler.Issues==0 && handler.Reissues==0,"Owner action never recreates member or issues a job");
            if(mode=="success")
            {
                var old=launcher.Last;Complete(Call(owner));
                Check(launcher.Calls==2 && !old!.IsCurrent && ReferenceEquals(old.AdministratorSession,launcher.Last!.AdministratorSession),"Explicit reopening keeps same administrator session and member, invalidates previous view target");
            }
            else {Complete(Call(owner));Check(launcher.Calls==1 && !Field<Button>(owner,"recover").Enabled,"Uncertain attempt cannot be reopened blindly");}
        }
        else Check(launcher.Calls==0 && handler.UserWrites==0 && handler.Issues==0 && handler.Reissues==0,"Invalid/stale/unconfirmed target never opens or writes");
        if(mode=="bad_id")Check(handler.UserReads==before,"Invalid recovery ID rejected before HTTP");
        owner.Close();
    }
    internal sealed class Pi : IEnrollmentPiChannel
    {
        public int Calls;public TaskCompletionSource<string> Ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<string> TransferAsync(EnrollmentJob job,bool readOnly,Func<JsonElement,CancellationToken,Task<string>> approve,IProgress<string>? progress,CancellationToken cancellation)
        {Calls++;return await Ready.Task;}
    }
    private static void Modal(bool denied)
    {
        using var handler=new Server {DenyRecovery=denied};using var api=new ApiClient("http://reentry.test:5000",handler);
        var pi=new Pi();
        var config=JsonSerializer.SerializeToUtf8Bytes(new{version=1,server_url="http://reentry.test:5000/",allow_insecure_http=true,devices=new Dictionary<string,object>{[Device]=new{pi_base_url="https://pi.test/"}}});
        var launcher=new CreatedUserEnrollmentLauncher(uri=>EnrollmentEndpoints.Parse(config,uri),_=>pi);
        using var owner=Owner(api,launcher);FingerprintEnrollmentForm? view=null;int stage=0;Exception? failure=null;
        using var driver=new System.Windows.Forms.Timer {Interval=10};
        driver.Tick+=(_,_)=>
        {
            try
            {
                view??=Application.OpenForms.OfType<FingerprintEnrollmentForm>().SingleOrDefault();if(view is null)return;
                var mode=Field<CheckBox>(view,"recoveryMode");var start=Field<Button>(view,"start");var check=Field<Button>(view,"checkRecovery");
                if(stage==0 && Field<ComboBox>(view,"devices").Items.Count>0)
                {
                    Check(mode.Checked && !mode.Enabled && Field<TextBox>(view,"recoveryId").ReadOnly,"Recovery-only form pins mode and identifier");
                    Check(!start.Enabled && handler.Issues==0 && handler.Reissues==0 && pi.Calls==0,"Opening/reading never auto-starts ordinary issue/reissue/scan");
                    mode.Checked=false;Check(mode.Checked,"Forced programmatic uncheck cannot select ordinary issue");
                    stage=1;check.PerformClick();
                }
                else if(stage==1 && handler.RecoveryReads>0 && check.Enabled)
                {
                    Check(!mode.Enabled && Field<TextBox>(view,"recoveryId").ReadOnly,"Recovery error/success cannot unlock ordinary mode or identifier");
                    if(denied){Check(!start.Enabled && handler.Reissues==0 && pi.Calls==0,"Operator rejection blocks start and Pi");stage=4;view.Close();}
                    else {Check(start.Enabled && handler.Reissues==0,"Verified recovery waits for explicit start");stage=2;start.PerformClick();}
                }
                else if(stage==2 && pi.Calls==1)
                {Check(handler.Target==Existing.Id && handler.Issues==0 && handler.Reissues==1,"Explicit recovery uses same existing member, never normal issue");stage=3;pi.Ready.SetResult(Opaque);}
                else if(stage==3 && view.RegistrationCompleted){Check(handler.Completes==1 && handler.PhysicalRaw==Opaque,"Exact result stored before mapping completion");stage=4;view.Close();}
            }
            catch(Exception error){failure=error;driver.Stop();view?.Close();pi.Ready.TrySetResult(Opaque);}
        };
        driver.Start();Complete(Call(owner));driver.Stop();if(failure is not null)throw failure;
        Check(stage==4 && handler.UserWrites==0,"Same account remains; expected modal flow completed without account creation");owner.Close();
    }
    internal sealed class Server : HttpMessageHandler
    {
        public int UserReads,UserWrites,Issues,Reissues,RecoveryReads,Completes,Target;public string UserMode="";public bool DenyRecovery;public string? PhysicalRaw;private string client="";
        public TaskCompletionSource? Gate;
        public bool AllowNormalIssue;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellation)
        {
            string path=request.RequestUri!.AbsolutePath;object data;
            if(path.EndsWith("/auth/login"))data=new LoginResult("synthetic-token","Bearer",DateTimeOffset.UtcNow.AddHours(1),Admin);
            else if(path.EndsWith("/users"))
            {
                Check(request.Headers.Authorization?.Parameter=="synthetic-token","Fresh lookup uses current administrator bearer");
                if(request.Method!=HttpMethod.Get){UserWrites++;throw new Exception("Account mutation forbidden in recovery");}
                UserReads++;if(Gate is not null)await Gate.Task;
                var member=UserMode=="inactive"?Existing with{IsActive=false}:Existing;
                var items=UserMode=="missing"?new List<UserInfo>{Admin}:UserMode=="duplicate"?new List<UserInfo>{Admin,member,member}:new List<UserInfo>{Admin,member};
                data=new PageResult<UserInfo>(items,UserMode=="bad_page"?2:1,50,items.Count);
            }
            else if(path.EndsWith("/eligible-devices"))
            {Check(request.RequestUri.Query=="?target_user_id=88","Eligible target is reverified existing ID");data=new EnrollmentDevices([new(Device,"QA device","1")]);}
            else if(path.Contains("/recoveries/"))
            {
                RecoveryReads++;
                if(DenyRecovery)return Error(403,"ENROLLMENT_RECOVERY_OPERATOR_MISMATCH");
                if(path.EndsWith("/reissue"))
                {
                    using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
                    Check(body.RootElement.EnumerateObject().Count()==2,"Reissue body keeps exactly two fields");
                    client=body.RootElement.GetProperty("client_request_id").GetString()!;Target=body.RootElement.GetProperty("target_user_id").GetInt32();Reissues++;
                    data=new{recovery=RecoveryValue(),job=JobValue()};
                }
                else data=new{recovery=RecoveryValue()};
            }
            else if(path.EndsWith("/device-enrollments"))
            {
                Issues++;if(!AllowNormalIssue)throw new Exception("Recovery-only owner must never issue normally");
                using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));
                Check(body.RootElement.EnumerateObject().Count()==3,"Normal registration uses original three-field contract");
                client=body.RootElement.GetProperty("client_request_id").GetString()!;
                Target=body.RootElement.GetProperty("target_user_id").GetInt32();data=JobValue();
            }
            else if(path.EndsWith("/physical-result"))
            {
                if(request.Method==HttpMethod.Post){using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellation));PhysicalRaw=body.RootElement.GetProperty("result_envelope").GetString();}
                data=new{enrollment_id=Job,operation_revision="1",reserved_slot=7,claim_revision="2",key_epoch="1",contract="enrollment-mysql-v1",phase=PhysicalRaw is null?"not_recorded":"result_recorded",result_envelope=PhysicalRaw,result_id=PhysicalRaw is null?null:"af7de985-1df9-4e65-a310-dd86fa4c6862",outcome=PhysicalRaw is null?null:"sensor_stored",failure_code=(string?)null,intent_at=PhysicalRaw is null?null:"2026-10-21T00:39:01Z",reported_at=PhysicalRaw is null?null:"2026-10-21T00:39:02Z",may_store=false,approval_envelope=(string?)null};
            }
            else if(path.EndsWith("/complete")){Check(PhysicalRaw is not null,"Result storage precedes complete");Completes++;data=JobValue();}
            else throw new Exception("Unexpected recovery-owner API "+path);
            return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{ok=true,data},ApiClient.Json),Encoding.UTF8,"application/json")};
        }
        private object RecoveryValue()=>new{recovery_id=Recovery,source_enrollment_id="b8b34b01-5f2f-4f2a-bd33-34c557c2e932",device_id=Device,reserved_slot=7,source_operation_revision="1",source_claim_revision="1",state=Reissues>0?"reissued":"closed_awaiting_explicit_reissue",new_enrollment_id=Reissues>0?Job:null,evidence_kind="operator_observation_not_signed_result",closed_at="2026-10-21T00:38:51Z",reissued_at=Reissues>0?"2026-10-21T00:39:00Z":null};
        private EnrollmentJob JobValue()=>new(Job,"1",7,"2","1",client,Device,Completes>0?"completed":"issued","2026-10-21T01:03:00Z",false,Opaque,Completes>0?new EnrollmentReceipt("af7de985-1df9-4e65-a310-dd86fa4c6862","completed",null):null);
        private static HttpResponseMessage Error(int status,string code)=>new((HttpStatusCode)status){Content=new StringContent(JsonSerializer.Serialize(new{ok=false,error=new{code,message="Synthetic rejection"},request_id="synthetic-error"}),Encoding.UTF8,"application/json")};
    }
}
