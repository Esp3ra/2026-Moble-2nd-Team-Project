using RemoteWork.WinForms.Features.DeadlineAlerts;
using RemoteWork.WinForms.Infrastructure;

internal static partial class Program
{
    private static async Task ReviewRegressionChecks()
    {
        var failures=new List<string>();
        void ReviewCheck(bool ok,string text)
        {
            Console.WriteLine((ok?"PASS: ":"FAIL: ")+text);
            if(ok)checks++;else failures.Add(text);
        }
        foreach(int code in new[]{401,403})
        {
            using var source=new Source();using var main=await ShowMain();using var scope=new DeadlineAlertScope(main,source);
            scope.TaskRequested+=_=>throw new Exception("revoked source navigated");
            await scope.StartAsync();
            ReviewCheck(Popups()==1,"R61-01 popup exists before "+code);
            source.Recheck=_=>Task.FromException<DeadlineAlertSnapshot>(new ApiException("권한 만료",code,"DENIED"));
            try{await scope.OpenTaskAsync("event-A");throw new Exception("authentication error hidden");}
            catch(ApiException ex){ReviewCheck(ex.StatusCode==code,"R61-01 preserves actual error "+code);}
            ReviewCheck(!scope.IsCurrent && scope.Snapshot is null && scope.Unread.Count==0 && Popups()==0,
                "R61-01 "+code+" disposes current snapshot and popup");
            var timer=(System.Windows.Forms.Timer)typeof(DeadlineAlertScope).GetField("timer",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(scope)!;
            ReviewCheck(!timer.Enabled && !scope.CanOpenTask && DeadlineAlertScope.Find(main) is null,
                "R61-01 "+code+" stops timer callback and owner registration");
            ClosePopups();
        }
        // Both Read/OpenTask arrival orders must preserve the newer request.
        foreach(bool oldOpen in new[]{true,false})
        {
            using var source=new Source();using var main=await ShowMain();using var scope=new DeadlineAlertScope(main,source);
            int navigations=0;scope.TaskRequested+=_=>navigations++;
            await scope.CheckAsync();
            var delayed=new TaskCompletionSource<DeadlineAlertSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task? oldRead=null;Task<bool>? oldNavigation=null;
            if(oldOpen)
            {
                source.Recheck=_=>delayed.Task;
                oldNavigation=scope.OpenTaskAsync("event-A");
                source.Items=[];await scope.CheckAsync();
            }
            else
            {
                source.Read=()=>delayed.Task;oldRead=scope.CheckAsync();
                source.Recheck=_=>Task.FromResult(Snap(source.Now));
                ReviewCheck(!await scope.OpenTaskAsync("event-A"),"R61-02 newer navigation recheck rejects removed event");
            }
            ReviewCheck(scope.Unread.Count==0 && Popups()==0,"R61-02 newer empty context removes old popup "+oldOpen);
            delayed.SetResult(Snap(source.Now,Entry()));
            if(oldNavigation is not null)ReviewCheck(!await oldNavigation,"R61-02 stale navigation returns false");
            if(oldRead is not null)await oldRead;
            ReviewCheck(scope.Unread.Count==0 && Popups()==0 && navigations==0,
                "R61-02 late "+(oldOpen?"OpenTask":"Read")+" cannot resurrect or navigate");
            ClosePopups();
        }
        // Two independent navigation requests, both response completion orders.
        foreach(bool newerFirst in new[]{false,true})
        {
            using var source=new Source();using var main=await ShowMain();using var scope=new DeadlineAlertScope(main,source);
            int navigations=0;scope.TaskRequested+=_=>navigations++;
            await scope.CheckAsync();ClosePopups();
            var older=new TaskCompletionSource<DeadlineAlertSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            var newer=new TaskCompletionSource<DeadlineAlertSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            int calls=0;source.Recheck=_=>++calls==1?older.Task:newer.Task;
            var a=scope.OpenTaskAsync("event-A");var b=scope.OpenTaskAsync("event-A");
            var current=Entry() with {Title="최신 제목",RowVersion="2"};
            if(newerFirst){newer.SetResult(Snap(source.Now,current));await b;older.SetResult(Snap(source.Now,Entry()));}
            else{older.SetResult(Snap(source.Now,Entry()));await a;newer.SetResult(Snap(source.Now,current));}
            await Task.WhenAll(a,b);
            ReviewCheck(!a.Result && b.Result && navigations==1 && scope.Unread.Single().RowVersion=="2",
                "R61-02 navigation request ordering newer-first="+newerFirst);
        }
        // An obsolete non-auth error must not erase a newer successful context.
        using(var source=new Source())
        using(var main=await ShowMain())
        using(var scope=new DeadlineAlertScope(main,source))
        {
            scope.TaskRequested+=_=>{};await scope.CheckAsync();ClosePopups();
            var delayed=new TaskCompletionSource<DeadlineAlertSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.Recheck=token=>token=="event-A"?delayed.Task:Task.FromResult(Snap(source.Now,source.Items));
            var older=scope.OpenTaskAsync("event-A");
            source.Items=[Entry("event-B")];await scope.CheckAsync();
            delayed.SetException(new ApiException("old offline response",503,"UNAVAILABLE"));
            bool returned=false;try{returned=!await older;}catch(ApiException){}
            ReviewCheck(returned && scope.Unread.Single().EventToken=="event-B" && Popups()==1 && scope.NextInterval==TimeSpan.FromSeconds(60),
                "R61-02 obsolete503 cannot erase or change retry after newer success");
            ClosePopups();
        }
        foreach(bool failRead in new[]{false,true})
        {
            using var source=new Source();using var main=await ShowMain();using var scope=new DeadlineAlertScope(main,source);
            if(failRead)source.Read=()=>Task.FromException<DeadlineAlertSnapshot>(new ApiException("Read503",503,"UNAVAILABLE"));
            else source.Recheck=_=>Task.FromException<DeadlineAlertSnapshot>(new ApiException("Recheck503",503,"UNAVAILABLE"));
            foreach(int minutes in new[]{2,4,5,5})
            {
                await scope.CheckAsync();
                ReviewCheck(scope.NextInterval==TimeSpan.FromMinutes(minutes) && scope.Snapshot is null && Popups()==0,
                    "R61-03 "+(failRead?"Read":"Recheck")+"503 backoff="+minutes);
            }
            source.Read=null;source.Recheck=null;await scope.CheckAsync();
            ReviewCheck(scope.NextInterval==TimeSpan.FromSeconds(60) && Popups()==1,"R61-03 full successful pass resets60 "+failRead);
            ClosePopups();
        }
        if(failures.Count!=0)throw new Exception("PR61 regressions: "+string.Join("; ",failures));
    }
}
