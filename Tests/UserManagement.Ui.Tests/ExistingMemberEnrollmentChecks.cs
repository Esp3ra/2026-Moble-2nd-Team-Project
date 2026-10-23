using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;
using RemoteWork.WinForms.Features.Attendance;

internal static class ExistingMemberEnrollmentChecks
{
    private const string Device="123e4567-e89b-42d3-a456-426614174000";
    private const string Opaque="cHVibGljLXN5bnRoZXRpYw";
    private static int checks;
    private static bool proofSaved;
    private static T Field<T>(object x,string name)=>(T)x.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(x)!;
    private static Task Call(object x)=>(Task)x.GetType().GetMethod("EnrollExistingUser",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(x,null)!;
    private static void Check(bool value,string message){checks++;if(!value)throw new InvalidOperationException(message);}
    private static void Finish(Task task)
    {
        var timer=Stopwatch.StartNew();while(!task.IsCompleted){Application.DoEvents();Thread.Sleep(2);if(timer.Elapsed.TotalSeconds>15)throw new TimeoutException("Existing registration timed out");}
        task.GetAwaiter().GetResult();Application.DoEvents();
    }
    private static void Pump(Func<bool> done)
    {var timer=Stopwatch.StartNew();while(!done()){Application.DoEvents();Thread.Sleep(2);if(timer.Elapsed.TotalSeconds>15)throw new TimeoutException("Existing UI did not settle");}}
    private sealed class Launcher : ICreatedUserEnrollmentLauncher
    {
        public int Calls;public bool Started;public CreatedUserEnrollmentTarget? Last;
        public Task<EnrollmentLaunchResult> ShowAsync(IWin32Window owner,CreatedUserEnrollmentTarget target,Uri server)
        {target.ThrowIfChanged();Calls++;Last=target;Check(target.UserId==88 && target.IsExistingMember && target.RequiredRecoveryId is null,"Fresh verified existing ID with ordinary registration mode");return Task.FromResult(new EnrollmentLaunchResult(false,Started));}
    }
    private static UserManagementForm Open(ApiClient api,ICreatedUserEnrollmentLauncher launcher,bool consent=true)
    {
        Finish(api.LoginAsync("admin-test","synthetic"));
        var view=new UserManagementForm(api,launcher,confirmExistingEnrollment:user=>{Check(user.Id==88,"Confirmation shows exact server member");return consent;}){ShowInTaskbar=false,Opacity=0};
        view.Show();var grid=Field<DataGridView>(view,"grid");Pump(()=>!Field<bool>(view,"loading") && grid.Rows.Count>0);
        grid.CurrentCell=grid.Rows.Cast<DataGridViewRow>().Single(r=>r.DataBoundItem is UserInfo u && u.Id==88).Cells[0];Application.DoEvents();
        Check(Field<Button>(view,"enrollExisting").Enabled && !Field<Button>(view,"enroll").Enabled,"Existing and created-member actions remain distinct");
        string? proof=Environment.GetEnvironmentVariable("EXISTING_ENROLLMENT_UI_PROOF_DIRECTORY");
        if(!proofSaved && !string.IsNullOrWhiteSpace(proof))
        {
            Directory.CreateDirectory(proof);using var image=new Bitmap(view.Width,view.Height);
            view.DrawToBitmap(image,new Rectangle(Point.Empty,view.Size));image.Save(Path.Combine(proof,"existing-member-new-enrollment.png"));proofSaved=true;
        }
        return view;
    }
    public static void Run()
    {
        foreach(var mode in new[]{"success","cancel","inactive","missing","duplicate","bad_page","selection","logout","close","uncertain"}) Guard(mode);
        Modal();Console.WriteLine($"PASS existing-member NEW enrollment: {checks} assertions; actual forms + controlled HTTP/Pi, no live DB/sensor.");
    }
    private static void Guard(string mode)
    {
        using var handler=new RecoveryReentryChecks.Server();using var api=new ApiClient("http://reentry.test:5000",handler);
        var launcher=new Launcher{Started=mode=="uncertain"};using var view=Open(api,launcher,mode!="cancel");
        int before=handler.UserReads;
        Field<TextBox>(view,"password").Text="unsaved-not-transmitted";
        Field<TextBox>(view,"name").Text="unsaved-name";
        Field<TextBox>(view,"recoveryId").Text="not-used-for-new-enrollment";
        if(mode is "inactive" or "missing" or "duplicate" or "bad_page")handler.UserMode=mode;
        if(mode is "selection" or "logout" or "close")handler.Gate=new(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation=Call(view);
        Check(!Field<Button>(view,"enrollExisting").Enabled || operation.IsCompleted,"Duplicate action disabled while verifying");
        if(handler.Gate is not null)
        {
            Pump(()=>handler.UserReads>before);
            if(mode=="selection")view.GetType().GetMethod("ClearEditor",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(view,null);
            else if(mode=="logout")api.ClearIdentity();else view.Close();
            handler.Gate.SetResult();
        }
        Finish(operation);
        Check(handler.UserWrites==0 && handler.Issues==0 && handler.Reissues==0,"Selection/confirmation never writes account or starts sensor enrollment");
        if(mode is "success" or "uncertain")
        {
            Check(launcher.Calls==1 && handler.UserReads==before+1,"Reverify existing member before launch");
            Check(!Field<Button>(view,"enroll").Enabled,"Existing target never enables created-member button");
            if(mode=="uncertain"){Finish(Call(view));Check(launcher.Calls==1 && !Field<Button>(view,"enrollExisting").Enabled,"Unknown attempt cannot silently restart");}
            else {var old=launcher.Last;Finish(Call(view));Check(launcher.Calls==2 && !old!.IsCurrent,"Explicit reopening invalidates old target");}
        }
        else Check(launcher.Calls==0,"Stale/inactive/invalid/unconfirmed selection never launches");
        view.Close();
    }
    private static void Modal()
    {
        using var handler=new RecoveryReentryChecks.Server{AllowNormalIssue=true};using var api=new ApiClient("http://reentry.test:5000",handler);
        var pi=new RecoveryReentryChecks.Pi();
        var config=JsonSerializer.SerializeToUtf8Bytes(new{version=1,server_url="http://reentry.test:5000/",allow_insecure_http=true,devices=new Dictionary<string,object>{[Device]=new{pi_base_url="https://pi.test/"}}});
        var launcher=new CreatedUserEnrollmentLauncher(uri=>EnrollmentEndpoints.Parse(config,uri),_=>pi);
        using var view=Open(api,launcher);FingerprintEnrollmentForm? registration=null;int stage=0;Exception? failure=null;
        using var driver=new System.Windows.Forms.Timer{Interval=10};
        driver.Tick+=(_,_)=>
        {
            try
            {
                registration??=Application.OpenForms.OfType<FingerprintEnrollmentForm>().SingleOrDefault();if(registration is null)return;
                var start=Field<Button>(registration,"start");
                if(stage==0 && Field<ComboBox>(registration,"devices").Items.Count>0 && start.Enabled)
                {
                    Check(!Field<CheckBox>(registration,"recoveryMode").Checked,"Existing new enrollment opens ordinary mode, not invented recovery");
                    Check(handler.Issues==0 && handler.Reissues==0 && pi.Calls==0,"Opening window never starts registration");
                    stage=1;start.PerformClick();
                }
                else if(stage==1 && pi.Calls==1)
                {Check(handler.Target==88 && handler.Issues==1 && handler.Reissues==0,"Explicit start uses selected existing member with original issue endpoint");stage=2;pi.Ready.SetResult(Opaque);}
                else if(stage==2 && registration.RegistrationCompleted)
                {Check(handler.Completes==1 && handler.PhysicalRaw==Opaque && handler.UserWrites==0,"Exact result stored then completed without recreating account");stage=3;registration.Close();}
            }
            catch(Exception error){failure=error;driver.Stop();registration?.Close();pi.Ready.TrySetResult(Opaque);}
        };
        driver.Start();Finish(Call(view));driver.Stop();if(failure is not null)throw failure;
        Check(stage==3,"Existing-member ordinary registration completes through actual owner/launcher/form");view.Close();
    }
}
