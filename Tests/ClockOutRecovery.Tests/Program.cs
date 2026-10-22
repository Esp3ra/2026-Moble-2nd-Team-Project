using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using RemoteWork.WinForms.Infrastructure;

static class Program
{
    static int checks;
    static readonly Guid Work=Guid.Parse("11111111-1111-4111-8111-111111111111");
    static readonly Guid Challenge=Guid.Parse("22222222-2222-4222-8222-222222222222");
    static readonly Guid Original=Guid.Parse("33333333-3333-4333-8333-333333333333");
    static readonly DateOnly Day=new(2026,10,22);
    static object Receipt=>new {state="completed",workflow_receipt_id="44444444-4444-4444-8444-444444444444",work_session_ref=Work.ToString("D"),work_date="2026-10-22"};
    static HttpResponseMessage Json(object data)=>new(HttpStatusCode.OK) {Content=new StringContent(JsonSerializer.Serialize(new{ok=true,data},ApiClient.Json),Encoding.UTF8,"application/json")};
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    sealed record Pending(string Path,string Body,string? Bearer,CancellationToken Cancellation,TaskCompletionSource<HttpResponseMessage> Reply);
    sealed class Handler:HttpMessageHandler
    {
        internal readonly Channel<Pending> Sent=Channel.CreateUnbounded<Pending>();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            string path=request.RequestUri!.AbsolutePath;
            if(path=="/api/v1/auth/login") return Json(new {access_token="same-token",user=new UserInfo(1,"user","User","E1","TEST","employee",true)});
            if(path.EndsWith("/"+Challenge.ToString("D")))return Json(new {challenge_id=Challenge.ToString("D"),state="verified",workflow_state="pending",identity_receipt=new {purpose=Purpose}});
            var pending=new Pending(path,request.Content is null?"":await request.Content.ReadAsStringAsync(),request.Headers.Authorization?.Parameter,ct,new(TaskCreationOptions.RunContinuationsAsynchronously));
            await Sent.Writer.WriteAsync(pending);
            // Intentionally ignores cancellation to exercise late responses and drain.
            return await pending.Reply.Task;
        }
        internal string Purpose="clock_out";
        internal async Task<Pending> Next()=>await Sent.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }
    static async Task Canceled(Task task){try{await task;throw new Exception("Expected cancellation");}catch(OperationCanceledException){checks++;}}
    static async Task Error<T>(Task<T> task,int status){try{await task;throw new Exception("Expected API error");}catch(ApiException e){Check(e.StatusCode==status,"HTTP status");}}
    static async Task<(ApiClient api,Handler h,ApiClient.Session session)> Login()
    {var h=new Handler();var a=new ApiClient("http://127.0.0.1:5000",h);await a.LoginAsync("user","unused-test-only");return(a,h,a.CaptureSession());}
    static Task<ApiClient.ClockOutFinalization> Begin(ApiClient.Session s,CancellationToken ct=default)=>s.BeginClockOutFinalizationAsync(Work,Day,Challenge,Original,ct);
    static async Task Main(string[] args)
    {
        if(args.Length==3&&args[0]=="--owned-loopback"){await Live(args[1],args[2]);return;}
        foreach(var run in new Func<Task>[] {DrainLate401,LostResultRecovery,RealRecovery401,AbandonUnknown,CancelBeforeSend,
            SameTokenNewGeneration,RejectWrongPurpose,ConcurrentFinalize,WrongReceipt,NoEndWithoutReceipt,IndependentOwner,CallerCancellation})
        {await run().WaitAsync(TimeSpan.FromSeconds(12));Console.WriteLine("PASS "+run.Method.Name);}
        Console.WriteLine($"PASS {checks} assertions / 12 scenarios; actual ApiClient, controlled HTTP; no shared server/device.");
    }
    sealed class LossHandler(bool loss):DelegatingHandler(new HttpClientHandler{UseCookies=false,AllowAutoRedirect=false})
    {
        internal int Finalizes;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var response=await base.SendAsync(request,ct);
            if(request.RequestUri!.AbsolutePath.EndsWith("/finalize"))
            {
                Finalizes++;
                if(loss&&Finalizes==1&&response.IsSuccessStatusCode)
                {response.Dispose();throw new HttpRequestException("Synthetic response loss after actual commit");}
            }
            return response;
        }
    }
    static async Task Live(string url,string mode)
    {
        if(Environment.GetEnvironmentVariable("CLOCK_OUT_OWNED_TEST")!="1"||!url.StartsWith("http://127.0.0.1:"))throw new Exception("Owned fixture only");
        var handler=new LossHandler(mode=="loss");using var api=new ApiClient(url,handler);
        await api.LoginAsync("staff1",Environment.GetEnvironmentVariable("CLOCK_OUT_TEST_PASSWORD")??throw new Exception("Test password required"));
        var session=api.CaptureSession();var bootstrap=await session.GetAsync<JsonElement>("__test/clock-out");
        var work=bootstrap.GetProperty("work_id").GetGuid();var challenge=bootstrap.GetProperty("challenge_id").GetGuid();
        using var close=await session.BeginClockOutFinalizationAsync(work,DateOnly.Parse(bootstrap.GetProperty("work_date").GetString()!),challenge,Guid.NewGuid());
        if(mode=="loss")
        {
            await Error(close.CompleteAsync(),0);
            await Canceled(api.RefreshIdentityAsync());
            Check(close.IsCurrent,"Real committed clock-out keeps original result owner");
        }
        var answer=await close.CompleteAsync();Check(answer.GetProperty("work_session_ref").GetGuid()==work,"Real original receipt");
        Check(handler.Finalizes==(mode=="loss"?2:1),"Exact finalize count");
        close.EndSession();Check(api.CurrentUser is null&&!session.IsCurrent,"Real confirmed logout");
        Console.WriteLine(JsonSerializer.Serialize(new{passed=true,mode,finalizes=handler.Finalizes,checks}));
    }
    static async Task DrainLate401()
    {
        var(a,h,s)=await Login();using(a)
        {
            int expired=0;a.AuthenticationExpired+=(_,_)=>expired++;
            var normal=s.GetAsync<JsonElement>("auth/me");var pending=await h.Next();
            var begin=Begin(s);
            Check(!begin.IsCompleted,"Must wait for in-flight response");
            Check(pending.Cancellation.IsCancellationRequested,"Ordinary traffic canceled");
            await Canceled(s.GetAsync<JsonElement>("tasks"));
            pending.Reply.SetResult(new(HttpStatusCode.Unauthorized){Content=new StringContent("<html>expired</html>")});
            await Canceled(normal);using var close=await begin;
            Check(expired==0&&s.IsCurrent,"Drained late401 must not expire recovery");
            var final=close.CompleteAsync();var sent=await h.Next();sent.Reply.SetResult(Json(Receipt));await final;
            Check(close.HasConfirmedReceipt,"Valid receipt");close.EndSession();Check(!s.IsCurrent&&a.CurrentUser is null,"Confirmed clock-out ends normal identity");
        }
    }
    static async Task LostResultRecovery()
    {
        var(a,h,s)=await Login();using(a)using(var close=await Begin(s))
        {
            var first=close.CompleteAsync();var p1=await h.Next();p1.Reply.SetException(new HttpRequestException("lost after server commit"));await Error(first,0);
            Check(close.IsCurrent&&close.HasFinalizeAttempt&&!close.HasConfirmedReceipt,"Unknown retains original owner");
            await Canceled(a.RefreshIdentityAsync());Check(!h.Sent.Reader.TryRead(out _),"No ordinary auth/me network request");
            var retry=close.CompleteAsync();var p2=await h.Next();
            Check(p1.Path==p2.Path&&p1.Body==p2.Body&&p1.Bearer==p2.Bearer,"Same context/challenge/UUID/body");
            p2.Reply.SetResult(Json(Receipt));await retry;
            await close.CompleteAsync();Check(!h.Sent.Reader.TryRead(out _),"Known success has no extra POST");
        }
    }
    static async Task RealRecovery401()
    {
        var(a,h,s)=await Login();using(a)using(var close=await Begin(s))
        {int expired=0;a.AuthenticationExpired+=(_,_)=>expired++;var t=close.CompleteAsync();var p=await h.Next();p.Reply.SetResult(new(HttpStatusCode.Unauthorized){Content=new StringContent("{\"ok\":false,\"error\":{\"code\":\"AUTH_REQUIRED\"}}")});await Error(t,401);Check(expired==1&&!s.IsCurrent&&!close.IsCurrent,"Real401 expires, never ignored");}
    }
    static async Task AbandonUnknown()
    {
        var(a,h,s)=await Login();using(a)
        {var close=await Begin(s);var t=close.CompleteAsync();var p=await h.Next();p.Reply.SetException(new HttpRequestException());await Error(t,0);close.Dispose();Check(!s.IsCurrent&&a.CurrentUser is null,"Disposing unknown never resumes normal session");await Canceled(s.GetAsync<JsonElement>("tasks"));}
    }
    static async Task CancelBeforeSend()
    {
        var(a,h,s)=await Login();using(a)
        {var close=await Begin(s);close.Dispose();Check(s.IsCurrent,"No finalize permits ordinary resume");var t=s.GetAsync<JsonElement>("tasks");var p=await h.Next();p.Reply.SetResult(Json(new{}));await t;checks++;}
    }
    static async Task SameTokenNewGeneration()
    {
        var(a,h,s)=await Login();using(a)
        {var close=await Begin(s);var t=close.CompleteAsync();var p=await h.Next();await a.LoginAsync("user","unused-test-only");var current=a.CaptureSession();p.Reply.SetResult(Json(Receipt));await Canceled(t);close.Dispose();Check(current.IsCurrent&&a.CurrentUser is not null,"Old same-token owner cannot clear replacement");}
    }
    static async Task RejectWrongPurpose()
    {
        var(a,h,s)=await Login();using(a)
        {h.Purpose="noon_check";try{await Begin(s);throw new Exception("Expected purpose denial");}catch(InvalidDataException){checks++;}Check(s.IsCurrent,"Wrong purpose never closes ordinary lane");}
    }
    static async Task ConcurrentFinalize()
    {
        var(a,h,s)=await Login();using(a)using(var close=await Begin(s))
        {var one=close.CompleteAsync();var two=close.CompleteAsync();var p=await h.Next();Check(!h.Sent.Reader.TryRead(out _),"Finalizes serialized");p.Reply.SetResult(Json(Receipt));await Task.WhenAll(one,two);Check(!h.Sent.Reader.TryRead(out _),"One successful finalize only");}
    }
    static async Task WrongReceipt()
    {
        var(a,h,s)=await Login();using(a)using(var close=await Begin(s))
        {var t=close.CompleteAsync();var p=await h.Next();p.Reply.SetResult(Json(new {state="completed",workflow_receipt_id=Original.ToString("D"),work_session_ref=Guid.NewGuid().ToString("D"),work_date="2026-10-22"}));try{await t;throw new Exception("Expected wrong-work denial");}catch(InvalidDataException){checks++;}Check(!close.HasConfirmedReceipt&&s.IsCurrent,"Wrong receipt stays unknown/gated");}
    }
    static async Task NoEndWithoutReceipt()
    {
        var(a,h,s)=await Login();using(a)using(var close=await Begin(s))
        {try{close.EndSession();throw new Exception("Expected no-receipt denial");}catch(InvalidOperationException){checks++;}Check(s.IsCurrent,"No success falsely installed");}
    }
    static async Task IndependentOwner()
    {
        var(a,h,s)=await Login();using(a)using(var close=await Begin(s))
        {await Canceled(Begin(s));Check(!h.Sent.Reader.TryRead(out _),"No second owner network request");}
    }
    static async Task CallerCancellation()
    {
        var(a,h,s)=await Login();using(a)using(var close=await Begin(s))
        {using var ct=new CancellationTokenSource();var t=close.CompleteAsync(ct.Token);var p=await h.Next();ct.Cancel();p.Reply.SetResult(Json(Receipt));await Canceled(t);Check(close.IsCurrent&&close.HasFinalizeAttempt,"Caller cancellation preserves RAM recovery");var retry=close.CompleteAsync();var r=await h.Next();r.Reply.SetResult(Json(Receipt));await retry;Check(r.Body==p.Body,"Canceled finalize retains exact UUID");}
    }
}
