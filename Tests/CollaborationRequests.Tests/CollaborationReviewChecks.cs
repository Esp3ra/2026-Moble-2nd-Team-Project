using System.Reflection;
using System.Text.Json;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Infrastructure;

internal static class CollaborationReviewChecks
{
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;
    public static async Task Run(Action<bool,string> check)
    {
        // R57-03: unknown -> not_recorded -> explicit retry409 -> fresh preparation.
        using (var service = new Service())
        using (var form = await ReadyForm(service))
        {
            service.Execute = _ => service.Writes.Count == 1
                ? Task.FromException<CollaborationResult>(new CollaborationOutcomeUnknownException())
                : service.Writes.Count == 2
                    ? Task.FromException<CollaborationResult>(new ApiException("업무 변경",409,"COLLABORATION_CONTEXT_CHANGED"))
                    : Task.FromResult(service.Result(service.Writes.Last()));
            await form.SendRequestAsync();
            check(Field<TextBox>(form,"txtContent").ReadOnly && service.Prepares == 1,"unknown outcome locks exact original operation");
            await form.ResolveSendAsync();
            check(Field<Button>(form,"resolveSendButton").Text.Contains("재전송"),"not_recorded requires an explicit retry");
            await form.ResolveSendAsync();
            check(!Field<TextBox>(form,"txtContent").ReadOnly && Field<Button>(form,"refreshButton").Enabled,"definite409 unlocks refresh and input");
            check(Field<TextBox>(form,"txtContent").Text=="입력 보존" && Field<TextBox>(form,"txtCollaborationReason").Text=="요청문 보존","definite failure preserves typed content");
            check(service.Writes[0].Key==service.Writes[1].Key && service.Writes[0].Body.GetRawText()==service.Writes[1].Body.GetRawText(),"explicit replay retains key/body");
            await form.SendRequestAsync();
            check(service.Prepares==2 && service.Writes.Last().Body.GetProperty("expected_row_version").GetString()=="2","next send prepares current context");
            check(service.Writes.Last().Key!=service.Writes[0].Key,"fresh preparation after confirmed failure uses a new key");
        }
        using (var service = new Service())
        using (var form = await ReadyForm(service))
        {
            service.Execute = _ => Task.FromException<CollaborationResult>(new CollaborationOutcomeUnknownException());
            service.Lookup = _ => Task.FromException<CollaborationResult?>(new ApiException("조회 실패",409,"LOOKUP_FAILED"));
            await form.SendRequestAsync(); await form.ResolveSendAsync();
            check(Field<TextBox>(form,"txtContent").ReadOnly && service.Prepares==1,"failed lookup alone never discards unresolved key/body");
        }

        using (var service = new Service())
        using (var main = new Form {ShowInTaskbar=false,Opacity=0})
        {
            main.Show();
            using var shared = new CollaborationNotificationScope(main,service,TimeSpan.FromMinutes(10));
            service.Pending = Entry();
            await shared.StartAsync();
            check(Popups()==1,"shared receiver opens one pending popup");
            int queries=service.ListCalls;
            using (var form = new CollaborationRequestForm(new Directory(service),service))
            {
                await ShowAsync(form,main);
                check(ReferenceEquals(Field<CollaborationNotificationScope>(form,"borrowedNotifications"),shared),"normal owner-based entry borrows login scope");
                check(Field<object?>(form,"notifications") is null && Popups()==1 && service.ListCalls==queries,"no duplicate timer/queue/popup when request form opens");
                form.Close();
            }
            check(shared.IsCurrent,"closing borrowed form does not dispose shared receiver");
            ClosePopups();
            service.Pending=Entry() with {RowVersion="2"};
            await shared.CheckAsync();
            check(Popups()==1,"shared receiver continues after borrowed form closes");
            ClosePopups();
            using (var notice = new ImportantNoticeForm())
            {
                notice.Show(main);
                service.Pending=Entry() with {RowVersion="3"};
                int before=service.ListCalls;
                await shared.CheckAsync();
                check(Popups()==0 && service.ListCalls==before,"visible important notice defers collaboration candidate");
                notice.Close();
            }
            await shared.CheckAsync();
            check(Popups()==1,"deferred candidate was not incorrectly marked shown");
            main.Close();
            check(!shared.IsCurrent && Popups()==0,"owner close disposes shared receiver and popup");
        }
        using (var service = new Service())
        using (var form = new CollaborationRequestForm(new Directory(service),service))
        {
            await ShowAsync(form);
            check(Field<object?>(form,"notifications") is not null,"standalone form explicitly owns its receiver");
            form.Close();
            check(Field<object?>(form,"notifications") is null,"standalone owned receiver disposed with form");
        }
        using (var service = new Service())
        using (var main = new Form {ShowInTaskbar=false,Opacity=0})
        {
            main.Show();
            using var shared=new CollaborationNotificationScope(main,service,TimeSpan.FromMinutes(10));
            var delayed=new TaskCompletionSource<IReadOnlyList<CollaborationEntry>>();
            service.List = _ => delayed.Task;
            var waiting=shared.CheckAsync();
            main.Close(); delayed.SetResult([Entry()]); await waiting;
            check(Popups()==0 && service.ListCalls==1,"late response after owner disposal opens no popup or second request");
        }
        await NotificationRaceChecks(check);
        using (var service = new Service())
        {
            var late=Entry() with {Status="accepted",IsLate=true};
            check(late.ToString().Contains("지연"),"accepted late flag visible in inbox item");
            using var response=new CollaborationResponseForm(service,late);
            check(response.Controls.OfType<TextBox>().Single().Text.Contains("지연"),"late flag visible in response detail");
            using var history=new CollaborationHistoryForm(service,late);
            await history.LoadHistoryAsync();
            check(Field<ListBox>(history,"list").Items.Count==2,"status history displayed in dedicated collaboration view");
            check(service.Writes.Count==0,"history and late display do not mutate requests");
        }
    }
    private static async Task NotificationRaceChecks(Action<bool,string> check)
    {
        // Complete a real awaited list only after the UI changes. No sleeps or
        // production guards are replaced; both received and sent waits matter.
        foreach (bool waitForSent in new[] {false,true})
        foreach (string blocker in new[] {"response","notice","hidden","minimized"})
        {
            string scenario=$"R57-04 { (waitForSent ? "sent" : "received") }/{blocker}";
            using var service=new Service {Pending=Entry()};
            using var main=new Form {ShowInTaskbar=false,Opacity=0};
            await ShowAsync(main);
            using var scope=new CollaborationNotificationScope(main,service,TimeSpan.FromMinutes(10));
            var delayed=new TaskCompletionSource<IReadOnlyList<CollaborationEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
            bool held=false;
            service.List=sent=>
            {
                if(sent==waitForSent) { held=true; return delayed.Task; }
                return Task.FromResult<IReadOnlyList<CollaborationEntry>>(sent?[]:[service.Pending!]);
            };
            var waiting=scope.CheckAsync();
            check(held && !waiting.IsCompleted,scenario+" reaches the intended await");
            using Form? other=blocker switch
            {
                "response"=>new CollaborationResponseForm(service,service.Pending!),
                "notice"=>new ImportantNoticeForm(),
                _=>null
            };
            if(other is not null) await ShowAsync(other,main);
            else if(blocker=="hidden") main.Hide();
            else main.WindowState=FormWindowState.Minimized;
            delayed.SetResult(waitForSent?[]:[service.Pending!]);
            await waiting;
            check(Popups()==(blocker=="response"?1:0),scenario+" defers automatic popup after UI change");
            other?.Close();
            if(blocker=="hidden") main.Show();
            else if(blocker=="minimized") main.WindowState=FormWindowState.Normal;
            service.List=null;
            await scope.CheckAsync();
            check(Popups()==1,scenario+" keeps deferred candidate eligible");
            ClosePopups();
            await scope.CheckAsync();
            check(Popups()==0,scenario+" still suppresses an actually displayed version");
            check(service.Writes.Count==0,scenario+" does not write or respond automatically");
            main.Close();
        }
    }
    private static int Popups() => Application.OpenForms.OfType<CollaborationResponseForm>().Count(f=>f.Visible);
    private static async Task ShowAsync(Form form,Form? owner=null)
    {
        var shown=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        form.Shown+=(_,_)=>shown.TrySetResult();
        if(owner is null) form.Show(); else form.Show(owner);
        await shown.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
    private static void ClosePopups() { foreach(var popup in Application.OpenForms.OfType<CollaborationResponseForm>().ToArray()) popup.Close(); }
    private static async Task<CollaborationRequestForm> ReadyForm(Service service)
    {
        var form=new CollaborationRequestForm(new Directory(service),service);
        form.CreateControl(); await form.ReloadDirectoryAsync();
        Field<ComboBox>(form,"cmbOptions").SelectedIndex=0; Field<ComboBox>(form,"cmbOptions2").SelectedIndex=0;
        Field<TextBox>(form,"txtContent").Text="입력 보존"; Field<TextBox>(form,"txtCollaborationReason").Text="요청문 보존";
        return form;
    }
    private static CollaborationEntry Entry()=>new("118b8178-83b5-4b92-a610-8ff777e9a05e","1",1,1,2,"요청자","업무","제목","요청 내용",new(2026,10,25),"pending","1",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow,null,false);
    private sealed class Directory(Service service):ICollaborationDirectory
    {
        public int CurrentUserId=>service.UserId; public bool IsCurrent=>service.IsCurrent; public CancellationToken Cancellation=>service.Cancellation;
        public Task<IReadOnlyList<TaskSummary>> GetOwnTasksAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<TaskSummary>>([new("1","본인 업무","본인",TaskDisplayStatus.InProgress,new(2026,10,20),new(2026,10,30))]);
        public Task<IReadOnlyList<CollaborationRecipient>> GetRecipientsAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<CollaborationRecipient>>([new(1,"상대방","개발팀")]);
    }
    private sealed class Service:ICollaborationService,IDisposable
    {
        private readonly CancellationTokenSource identity=new();
        public int UserId=>2; public bool IsCurrent=>!identity.IsCancellationRequested; public CancellationToken Cancellation=>identity.Token;
        public int Prepares,ListCalls; public List<CollaborationOperation> Writes=[]; public CollaborationEntry? Pending;
        public Func<CollaborationOperation,Task<CollaborationResult>>? Execute;
        public Func<CollaborationOperation,Task<CollaborationResult?>>? Lookup;
        public Func<bool,Task<IReadOnlyList<CollaborationEntry>>>? List;
        public Task<IReadOnlyList<CollaborationEntry>> ListAsync(bool sent,CancellationToken ct)
        { ListCalls++; return List?.Invoke(sent)??Task.FromResult<IReadOnlyList<CollaborationEntry>>(!sent && Pending is not null?[Pending]:[]); }
        public Task<IReadOnlyList<CollaborationHistoryEntry>> GetHistoryAsync(CollaborationEntry item,CancellationToken ct)=>Task.FromResult<IReadOnlyList<CollaborationHistoryEntry>>([
            new("1","pending",null,1,DateTimeOffset.UtcNow),new("2","accepted","recipient_response",2,DateTimeOffset.UtcNow)]);
        public Task<CollaborationOperation> PrepareCreateAsync(string id,int person,string title,string text,DateOnly desired,CancellationToken ct)
        {
            Prepares++;
            return Task.FromResult(new CollaborationOperation("collaboration.created",id,"collaboration-requests",
                JsonSerializer.SerializeToElement(new{request_id=Guid.NewGuid().ToString("D"),title,request_text=text,expected_row_version=Prepares.ToString()}),new string('k',32)+Prepares));
        }
        public Task<CollaborationOperation> PrepareResponseAsync(CollaborationEntry item,string decision,CancellationToken ct)=>throw new NotImplementedException();
        public CollaborationResult Result(CollaborationOperation operation)=>new(operation.Body.GetProperty("request_id").GetString()!,"pending","1",DateTimeOffset.UtcNow);
        public Task<CollaborationResult> ExecuteAsync(CollaborationOperation operation,CancellationToken ct)
        { Writes.Add(operation); return Execute?.Invoke(operation)??Task.FromResult(Result(operation)); }
        public Task<CollaborationResult?> ResolveAsync(CollaborationOperation operation,CancellationToken ct)=>Lookup?.Invoke(operation)??Task.FromResult<CollaborationResult?>(null);
        public void Dispose(){identity.Cancel(); identity.Dispose();}
    }
}
