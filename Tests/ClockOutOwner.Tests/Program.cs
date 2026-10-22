using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Reflection;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

static class Program
{
    static int checks;
    static readonly Guid Work=Guid.NewGuid(),Challenge=Guid.NewGuid();
    static void Check(bool value,string message){if(!value)throw new Exception(message);checks++;}
    [STAThread]static void Main()
    {
        ApplicationConfiguration.Initialize();Control.CheckForIllegalCrossThreadCalls=true;
        Exception? failure=null;using var loop=new Form{ShowInTaskbar=false,Opacity=0};
        loop.Shown+=async(_,_)=>{try{await Run();}catch(Exception e){failure=e;}finally{loop.Close();}};
        Application.Run(loop);if(failure is not null){Console.Error.WriteLine(failure);Environment.ExitCode=1;}
        else Console.WriteLine($"PASS {checks} actual MainForm owner assertions; synthetic loopback HTTP, no product DB/device.");
    }
    static async Task Run()
    {
        using var server=new Server();var api=AppServices.Api;string previous=api.ServerUri.ToString();api.SetServer(server.Url);
        try
        {
            var login=await api.LoginAsync("synthetic","synthetic");using var main=new MainForm(login.User){ShowInTaskbar=false,Location=new(-20000,-20000),StartPosition=FormStartPosition.Manual};main.Show();
            var stopped=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int stops=0;
            var prepared=main.PrepareClockOutFinalizationAsync(Work,new(2026,10,22),Challenge,Guid.NewGuid(),()=>{stops++;return stopped.Task;});
            Check(!prepared.IsCompleted&&stops==1,"Monitor must stop before finalization owner");
            typeof(MainForm).GetMethod("RefreshIdentity",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,[null,EventArgs.Empty]);
            Check(server.AuthReads==0,"No auth polling while preparing");
            stopped.SetResult();using var owner=await prepared;
            using(var popup=new Form{Owner=main,ShowInTaskbar=false}){popup.Show();popup.Close();}
            Check(owner.IsCurrent,"Borrowed popup close preserves login-owned operation");
            try{main.FinishConfirmedClockOut(owner);throw new Exception("Missing receipt accepted");}catch(InvalidOperationException){checks++;}
            try{await main.PrepareClockOutFinalizationAsync(Work,new(2026,10,22),Challenge,Guid.NewGuid(),()=>Task.CompletedTask);throw new Exception("Duplicate owner");}catch(InvalidOperationException){checks++;}
            await owner.CompleteAsync();Check(owner.HasConfirmedReceipt&&server.Finalizes==1,"Receipt belongs to owner");
            main.FinishConfirmedClockOut(owner);
            Check(!main.Visible&&api.CurrentUser is null,"Confirmed finish closes shown login");
            Check(server.Logouts==0,"No auth/logout after committed clock-out");
            await api.LoginAsync("synthetic","synthetic");var replacement=api.CaptureSession();main.Dispose();owner.Dispose();
            Check(replacement.IsCurrent,"Old UI cleanup cannot clear replacement login");
            using var second=new MainForm(replacement.User){ShowInTaskbar=false,Location=new(-20000,-20000),StartPosition=FormStartPosition.Manual};second.Show();
            var canceled=await second.PrepareClockOutFinalizationAsync(Work,new(2026,10,22),Challenge,Guid.NewGuid(),()=>Task.CompletedTask);canceled.Dispose();
            using var next=await second.PrepareClockOutFinalizationAsync(Work,new(2026,10,22),Challenge,Guid.NewGuid(),()=>Task.CompletedTask);
            Check(next.IsCurrent,"Pre-send disposal permits explicit re-prepare");second.Dispose();Check(replacement.IsCurrent,"No finalize means no clock-out claim/identity clear");
        }
        finally{api.ClearIdentity();api.SetServer(previous);}
    }
    sealed class Server:IDisposable
    {
        readonly HttpListener listener=new();readonly Task worker;public string Url{get;}public int Finalizes,Logouts,AuthReads;
        readonly UserInfo user=new(1,"synthetic","Synthetic","S1","TEST","admin",true);
        public Server(){var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();int port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();Url=$"http://127.0.0.1:{port}/";listener.Prefixes.Add(Url);listener.Start();worker=Serve();}
        async Task Serve()
        {
            while(listener.IsListening)
            {
                HttpListenerContext c;try{c=await listener.GetContextAsync().ConfigureAwait(false);}catch(HttpListenerException){break;}catch(ObjectDisposedException){break;}
                string p=c.Request.Url!.AbsolutePath;object data;
                if(p.EndsWith("/auth/login"))data=new LoginResult("synthetic-token","Bearer",DateTimeOffset.UtcNow.AddHours(1),user);
                else if(p.EndsWith("/auth/me")){AuthReads++;data=new IdentityResult(user,DateTimeOffset.UtcNow.AddHours(1));}
                else if(p.EndsWith("/auth/logout")){Logouts++;data=new MessageResult("ok");}
                else if(p.EndsWith("/finalize")){Finalizes++;data=new{state="completed",workflow_receipt_id=Guid.NewGuid().ToString("D"),work_session_ref=Work.ToString("D"),work_date="2026-10-22"};}
                else if(p.EndsWith(Challenge.ToString("D")))data=new{challenge_id=Challenge.ToString("D"),state="verified",workflow_state="pending",identity_receipt=new{purpose="clock_out"}};
                else data=new{items=Array.Empty<object>(),next_after_id=(int?)null};
                var bytes=JsonSerializer.SerializeToUtf8Bytes(new{ok=true,data},ApiClient.Json);c.Response.ContentType="application/json";c.Response.ContentLength64=bytes.Length;
                await c.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);c.Response.Close();
            }
        }
        public void Dispose(){listener.Close();worker.GetAwaiter().GetResult();}
    }
}
