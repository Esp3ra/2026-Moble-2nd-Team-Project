using RemoteWork.WinForms.Features.DeadlineAlerts;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

internal static partial class Program
{
    private static int checks;
    private static readonly DateOnly Due = new(2026,10,22);
    private static DateTimeOffset At(int day,int hour=9,int minute=0,int second=0) => new(2026,10,day,hour,minute,second,TimeSpan.FromHours(9));
    private static DeadlineAlert Entry(string token="event-A") => new(token,"9223372036854775807","1",1,2,2,"주간 보고서 작성",Due,TaskDisplayStatus.InProgress);
    private static DeadlineAlertSnapshot Snap(DateTimeOffset time, params DeadlineAlert[] items) => new(time,items);
    private static void Check(bool ok,string text) { if(!ok) throw new Exception(text); checks++; }
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Exception? failure=null;
        using var host=new Form { ShowInTaskbar=false,Opacity=0 };
        host.Shown+=async(_,_)=>{try{PolicyChecks();await UiChecks();await ReviewRegressionChecks();await ConfirmationRefreshChecks();await AssignmentSafetyChecks();}catch(Exception ex){failure=ex;}finally{host.Close();}};
        Application.Run(host);
        if(failure is not null){Console.Error.WriteLine(failure);Environment.ExitCode=1;}
        else Console.WriteLine($"PASS: {checks} deadline policy/UI checks; explicit source doubles, no HTTP server or DB.");
    }
    private static void PolicyChecks()
    {
        foreach(var state in Enum.GetValues<TaskDisplayStatus>())
            Check(DeadlineAlertPolicy.IsEligible(Entry() with {Status=state},2)==
                (state is TaskDisplayStatus.NotStarted or TaskDisplayStatus.InProgress or TaskDisplayStatus.Returned),"approved A1 status "+state);
        Check(!DeadlineAlertPolicy.IsEligible(Entry() with {IsCancelled=true},2),"cancelled excluded");
        Check(!DeadlineAlertPolicy.IsEligible(Entry() with {CurrentAssigneeId=3},2),"managed or reassigned is not own");
        Check(!DeadlineAlertPolicy.IsEligible(Entry() with {IsConfirmed=true},2),"account confirmation excluded");
        Check(!DeadlineAlertPolicy.CanPopup(Entry(),2,At(21,8,59,59)),"before previous-day09 excluded");
        Check(DeadlineAlertPolicy.CanPopup(Entry(),2,At(21)),"previous-day09 included");
        Check(DeadlineAlertPolicy.CanPopup(Entry(),2,At(22,23,59,59)),"late login through due-date end");
        Check(!DeadlineAlertPolicy.CanPopup(Entry(),2,At(23,0)),"after due date no popup");
        Check(DeadlineAlertPolicy.HasOccurred(Entry(),At(23,0)),"overdue still belongs to unread list");
        Check(DeadlineAlertPolicy.CanPopup(Entry(),2,At(21).ToUniversalTime()),"UTC server timestamp evaluated in KST");
        Check(!DeadlineAlertPolicy.HasOccurred(Entry() with {DueDate=DateOnly.MinValue},At(21)),"minimum date never underflows");
        Check(DeadlineAlertPolicy.NormalInterval==TimeSpan.FromSeconds(60),"A3 interval60");
        foreach(int n in new[]{1,2,3,4}) Check(DeadlineAlertPolicy.RetryInterval(n).TotalMinutes==(n==1?2:n==2?4:5),"A3 backoff "+n);
        DeadlineAlertPolicy.Validate(Snap(At(21),Entry()),2); checks++;
        foreach(var bad in new[]{Entry() with {TaskId="01"},Entry() with {TaskId="9223372036854775808"},
            Entry() with {RowVersion="0"},Entry() with {AssignmentRevision=0},Entry() with {RecipientId=3}})
        {
            try{DeadlineAlertPolicy.Validate(Snap(At(21),bad),2);throw new Exception("malformed context accepted");}
            catch(InvalidDataException){checks++;}
        }
        try{DeadlineAlertPolicy.Validate(Snap(At(21),Entry(),Entry("event-B")),2);throw new Exception("two current events for one task");}
        catch(InvalidDataException){checks++;}
    }
    private static async Task UiChecks()
    {
        using(var source=new Source())
        using(var main=await ShowMain())
        using(var scope=new DeadlineAlertScope(main,source))
        {
            await scope.CheckAsync();
            var popup=Application.OpenForms.OfType<DeadlineAlertForm>().Single();
            Check(popup.Width<450 && !popup.Modal,"small nonmodal popup");
            Check(source.Rechecks==1,"current server context checked before display");
            popup.Close();await scope.CheckAsync();
            Check(Popups()==0 && source.Confirms==0,"close suppresses this login without confirmation write");
            using(var panel=new DeadlineAlertPanel()) panel.Bind(scope);
            Check(scope.IsCurrent,"closing menu view preserves borrowed login scope");
            source.Now=At(23,0);await scope.CheckAsync();
            Check(Popups()==0 && scope.Unread.Count==1,"overdue remains list-only");
            using(var inbox=new DeadlineAlertInboxForm(scope))
            {
                await Show(inbox,main);
                Check(inbox.Controls.OfType<ListBox>().Single().Items.Count==1,"actual unread view shows overdue item");
                inbox.Close();
            }
            Check(scope.IsCurrent,"inbox never disposes receiver");
            main.Close();Check(!scope.IsCurrent,"owner close ends receiver");
        }
        // Re-login is a new scope. Account-wide confirmations are source-owned.
        using(var source=new Source())
        {
            using(var first=await ShowMain())
            using(var scope=new DeadlineAlertScope(first,source)){await scope.CheckAsync();ClosePopups();}
            using var second=await ShowMain();using var newLogin=new DeadlineAlertScope(second,source);
            await newLogin.CheckAsync();Check(Popups()==1,"closed reminder can return after new login");ClosePopups();
            source.Items=[Entry() with {IsConfirmed=true}];await newLogin.CheckAsync();
            Check(newLogin.Unread.Count==0 && Popups()==0,"other-PC confirmation reflected at next successful read");
        }
        // Actual changes are source-supplied event identities, not date hashes.
        using(var source=new Source())
        using(var main=await ShowMain())
        using(var scope=new DeadlineAlertScope(main,source))
        {
            await scope.CheckAsync();ClosePopups();
            source.Items=[Entry() with {Title="본문만 수정",RowVersion="2"}];await scope.CheckAsync();
            Check(Popups()==0,"unrelated row_version/title change does not create a new alert");
            source.Items=[Entry("event-B") with {DueDate=Due.AddDays(1),RowVersion="3"}];source.Now=At(22);
            await scope.CheckAsync();Check(Popups()==1,"actual A-to-B date change creates new event");ClosePopups();
            source.Items=[Entry("event-A-again") with {RowVersion="4"}];await scope.CheckAsync();
            Check(Popups()==1,"B-to-A keeps a distinct authoritative event");
            source.Items=[];await scope.CheckAsync();
            Check(Popups()==0 && scope.Unread.Count==0,"invalidation removes open popup and queue");
        }
        foreach(var invalid in new[]{Entry() with {IsCancelled=true},Entry() with {CurrentAssigneeId=3},
            Entry() with {Status=TaskDisplayStatus.Submitted},Entry() with {Status=TaskDisplayStatus.Approved}})
        {
            using var source=new Source();using var main=await ShowMain();using var scope=new DeadlineAlertScope(main,source);
            source.Recheck=_=>Task.FromResult(Snap(source.Now,invalid));await scope.CheckAsync();
            Check(Popups()==0,"current recheck invalidation blocks stale candidate");
        }
        // R57 lesson: change the real UI while the authoritative recheck awaits.
        foreach(var blocker in new[]{"notice","hidden","minimized"})
        {
            using var source=new Source();using var main=await ShowMain();using var scope=new DeadlineAlertScope(main,source);
            var delayed=new TaskCompletionSource<DeadlineAlertSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.Recheck=_=>delayed.Task;
            var waiting=scope.CheckAsync();Check(!waiting.IsCompleted,"await reached before UI change "+blocker);
            using var notice=new ImportantNoticeForm();
            if(blocker=="notice") await Show(notice,main);
            else if(blocker=="hidden") main.Hide();else main.WindowState=FormWindowState.Minimized;
            delayed.SetResult(Snap(source.Now,Entry()));await waiting;
            Check(Popups()==0,"display-time condition rechecked "+blocker);
            notice.Close();main.Show();main.WindowState=FormWindowState.Normal;
            source.Recheck=null;await scope.CheckAsync();Check(Popups()==1,"deferred candidate not consumed "+blocker);ClosePopups();
        }
        using(var source=new Source())
        using(var main=await ShowMain())
        using(var scope=new DeadlineAlertScope(main,source))
        {
            source.Read=()=>Task.FromException<DeadlineAlertSnapshot>(new ApiException("offline",503,"UNAVAILABLE"));
            foreach(int minutes in new[]{2,4,5,5}){await scope.CheckAsync();Check(scope.NextInterval==TimeSpan.FromMinutes(minutes),"failure backoff "+minutes);}
            Check(scope.Snapshot is null,"failure is not empty success or stale authorized data");
            source.Read=null;await scope.CheckAsync();Check(scope.NextInterval==TimeSpan.FromSeconds(60),"success resets interval");ClosePopups();
            source.Read=()=>Task.FromException<DeadlineAlertSnapshot>(new ApiException("revoked",403,"FORBIDDEN"));
            await scope.CheckAsync();Check(!scope.IsCurrent && scope.Snapshot is null,"403 clears and stops captured receiver");
        }
        foreach(bool endIdentity in new[]{false,true})
        {
            using var source=new Source();using var main=await ShowMain();using var scope=new DeadlineAlertScope(main,source);
            var delayed=new TaskCompletionSource<DeadlineAlertSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.Read=()=>delayed.Task;var waiting=scope.CheckAsync();
            if(endIdentity) source.End();else main.Close();
            delayed.SetResult(Snap(source.Now,Entry()));await waiting;
            Check(Popups()==0 && scope.Snapshot is null,"late read after logout/owner close is ignored");
        }
        await ConfirmationChecks();
        using(var source=new Source())
        using(var main=await ShowMain())
        using(var scope=new DeadlineAlertScope(main,source))
        {
            string? opened=null;scope.TaskRequested+=id=>opened=id;
            await scope.CheckAsync();
            Check(await scope.OpenTaskAsync("event-A") && opened==Entry().TaskId,"task navigation passes exact BIGINT after current recheck");
            opened=null;source.Items=[Entry("changed-event")];
            Check(!await scope.OpenTaskAsync("event-A") && opened is null,"obsolete event cannot navigate using stale authorization");
            ClosePopups();
            source.Confirm=_=>Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Confirmed,Snap(source.Now,source.Items[0] with {IsConfirmed=true})));
            await scope.CheckAsync();
            var popup=Application.OpenForms.OfType<DeadlineAlertForm>().Single();
            var closed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            popup.FormClosed+=(_,_)=>closed.TrySetResult();
            Descendants(popup).OfType<Button>().Single(x=>x.Text=="확인").PerformClick();
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Check(source.Confirms==1 && scope.Unread.Count==0,"actual confirm button awaits server receipt before removal");
            using var page=new NoticesControl(PreviewRole.Employee,new Notices());
            page.BindDeadlineAlerts(scope);
            Check(Descendants(page).OfType<DeadlineAlertPanel>().Count()==1,"notice page contains borrowed deadline panel");
            Check(!Descendants(page).OfType<TextBox>().Any(x=>x.Text.Contains("주간 보고서 작성")),"old fictional notice deadline sample removed");
        }
    }
    private static async Task ConfirmationChecks()
    {
        using var source=new Source();using var main=await ShowMain();using var scope=new DeadlineAlertScope(main,source);
        await scope.CheckAsync();ClosePopups();
        source.Confirm=_=>Task.FromException<DeadlineConfirmation>(new HttpRequestException("lost response"));
        try{await scope.ConfirmAsync("event-A");throw new Exception("lost outcome accepted");}catch(HttpRequestException){checks++;}
        Check(scope.NeedsResolution("event-A") && source.Confirms==1,"uncertain result kept, no automatic retry");
        source.Resolve=_=>Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Unknown,Snap(source.Now,Entry())));
        await scope.ConfirmAsync("event-A");Check(source.Confirms==1 && source.Resolves==1,"next click only resolves original result");
        source.Resolve=_=>Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.NotRecorded,Snap(source.Now,Entry())));
        await scope.ConfirmAsync("event-A");Check(!scope.NeedsResolution("event-A") && source.Confirms==1,"not-recorded requires separate explicit retry");
        source.Confirm=_=>Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Confirmed,Snap(source.Now,Entry() with {IsConfirmed=true})));
        await scope.ConfirmAsync("event-A");Check(scope.Unread.Count==0 && source.Confirms==2,"confirmed server receipt removes reminder");
        source.Items=[Entry("event-B")];await scope.CheckAsync();ClosePopups();
        Check((await scope.ConfirmAsync("event-A")).State==DeadlineConfirmationState.Confirmed,"old successful event remains known without a new write");
        Check(source.Confirms==2 && scope.Unread.Single().EventToken=="event-B","old token never confirms new deadline");
        var pending=new TaskCompletionSource<DeadlineConfirmation>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Confirm=_=>pending.Task;
        var a=scope.ConfirmAsync("event-B");var b=scope.ConfirmAsync("event-B");
        Check(ReferenceEquals(a,b) && source.Confirms==3,"two views share one in-flight confirmation");
        pending.SetResult(new(DeadlineConfirmationState.Changed,Snap(source.Now)));
        await Task.WhenAll(a,b);Check(scope.Unread.Count==0,"stale response returns updated context without confirming new event");
        source.Items=[Entry("event-C")];await scope.CheckAsync();ClosePopups();
        var oldRead=new TaskCompletionSource<DeadlineAlertSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        source.Read=()=>oldRead.Task;var old=scope.CheckAsync();
        source.Confirm=_=>Task.FromResult(new DeadlineConfirmation(DeadlineConfirmationState.Confirmed,Snap(source.Now,source.Items[0] with {IsConfirmed=true})));
        await scope.ConfirmAsync("event-C");oldRead.SetResult(Snap(source.Now,source.Items));await old;
        Check(scope.Unread.Count==0 && Popups()==0,"delayed pre-confirm read cannot restore unread/popup");
    }
    private static int Popups()=>Application.OpenForms.OfType<DeadlineAlertForm>().Count(x=>x.Visible);
    private static IEnumerable<Control> Descendants(Control control)=>control.Controls.Cast<Control>()
        .SelectMany(child=>new[]{child}.Concat(Descendants(child)));
    private static void ClosePopups(){foreach(var f in Application.OpenForms.OfType<DeadlineAlertForm>().ToArray())f.Close();}
    private static async Task<Form> ShowMain(){var main=new Form{ShowInTaskbar=false,Opacity=0};await Show(main);return main;}
    private static async Task Show(Form form,Form? owner=null)
    {
        var shown=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        form.Shown+=(_,_)=>shown.TrySetResult();
        if(owner is null)form.Show();else form.Show(owner);
        await shown.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
    private sealed class Source:IDeadlineAlertSource,IDisposable
    {
        private readonly CancellationTokenSource identity=new();
        public int UserId=>2;public bool IsCurrent=>!identity.IsCancellationRequested;public CancellationToken Cancellation=>identity.Token;
        public DateTimeOffset Now=At(21);public DeadlineAlert[] Items=[Entry()];
        public int Rechecks,Confirms,Resolves;
        public Func<Task<DeadlineAlertSnapshot>>? Read;
        public Func<string,Task<DeadlineAlertSnapshot>>? Recheck;
        public Func<string,Task<DeadlineConfirmation>>? Confirm,Resolve;
        public Task<DeadlineAlertSnapshot> ReadAsync(CancellationToken ct)=>Read?.Invoke()??Task.FromResult(Snap(Now,Items));
        public Task<DeadlineAlertSnapshot> RecheckAsync(string token,CancellationToken ct){Rechecks++;return Recheck?.Invoke(token)??Task.FromResult(Snap(Now,Items));}
        public Task<DeadlineConfirmation> ConfirmAsync(string token,CancellationToken ct){Confirms++;return Confirm?.Invoke(token)??throw new NotImplementedException();}
        public Task<DeadlineConfirmation> ResolveConfirmationAsync(string token,CancellationToken ct){Resolves++;return Resolve?.Invoke(token)??throw new NotImplementedException();}
        public void End()=>identity.Cancel();public void Dispose(){identity.Cancel();identity.Dispose();}
    }
    private sealed class Notices:INoticeService
    {
        public Task<IReadOnlyList<Notice>> GetNoticesAsync(CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<Notice>>([]);
        public Task<Notice> GetNoticeAsync(int id,CancellationToken ct=default)=>throw new NotImplementedException();
        public Task<Notice> CreateNoticeAsync(CreateNoticeRequest request,CancellationToken ct=default)=>throw new NotImplementedException();
        public Task DeleteNoticeAsync(int id,CancellationToken ct=default)=>throw new NotImplementedException();
    }
}
