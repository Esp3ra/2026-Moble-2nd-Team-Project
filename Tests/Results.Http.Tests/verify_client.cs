
using System.Text.Json;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Features.Tasks.Queries;
using RemoteWork.WinForms.Features.Tasks.Preparation;
using RemoteWork.WinForms.Ui;

namespace RemoteWork.WinForms.Features.Results;

public static class ResultsFunctionalCheck
{
    public static int Count;
    public static void Check(bool ok, string label)
    {
        if (!ok) throw new InvalidOperationException(label);
        Count++;
    }
    public static void Until(Func<bool> done)
    {
        var deadline=DateTime.UtcNow.AddSeconds(25);
        while(!done())
        {
            Application.DoEvents();
            Thread.Sleep(5);
            if(DateTime.UtcNow>deadline) throw new TimeoutException("UI operation did not complete.");
        }
        Application.DoEvents();
    }
    public static T Wait<T>(Task<T> work)
    {
        Until(()=>work.IsCompleted);
        return work.GetAwaiter().GetResult();
    }
    public static void Wait(Task work)
    {
        Until(()=>work.IsCompleted);
        work.GetAwaiter().GetResult();
    }
    private static void Login(string user,string password)
    {
        if(AppServices.Api.CurrentUser!=null) Wait(AppServices.Api.LogoutAsync());
        Wait(AppServices.Api.LoginAsync(user,password));
    }
    public static string Run(string configText)
    {
        Console.WriteLine("C# stage: runtime setup");
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        using var uiHost=new Form {ShowInTaskbar=false};
        _=uiHost.Handle;
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        var config=JsonDocument.Parse(configText).RootElement;
        var shared=AppServices.Api;
        shared.ClearIdentity();
        shared.SetServer(config.GetProperty("server").GetString()!);
        string password=config.GetProperty("password").GetString()!;
        string root=config.GetProperty("storage").GetString()!;
        Directory.CreateDirectory(root);
        string source=Path.Combine(root,"client-source.any");
        byte[] bytes=Enumerable.Range(0,50000).Select(i=>(byte)(i%251)).ToArray();
        File.WriteAllBytes(source,bytes);

        Login("worker",password);
        Console.WriteLine("C# stage: employee submission");
        var view=new HttpTaskQueryService(shared).CaptureViewSession()!;
        var api=new ResultsApi(view.Session,ResultsConnection.Files);
        SubmissionForm.CheckSubmit(view.Session,view.QueryService,source,1);
        var first=Wait(api.SubmissionsAsync("8",CancellationToken.None)).Single();
        Check(first.Description=="C# result 1"&&first.RoundNo=="1"&&first.Attachments.Count==1,"actual submission UI persisted");
        using(var reviews=new ReviewsControl(PreviewRole.Employee))
            reviews.CheckView("8",false,"C# result 1",1);

        Login("manager",password);
        Console.WriteLine("C# stage: reviewer return");
        view=new HttpTaskQueryService(shared).CaptureViewSession()!;
        api=new ResultsApi(view.Session,ResultsConnection.Files);
        var task=Wait(api.ReviewTaskAsync("8",CancellationToken.None)).Summary;
        using(var reviews=new ReviewsControl(PreviewRole.Administrator))
            reviews.CheckView("8",true,"C# result 1",1);
        ReviewDecisionForm.CheckDecision(false,api,task,first);
        Check(Wait(api.SubmissionsAsync("8",CancellationToken.None)).Single().Status=="returned","actual review UI returned");

        Login("worker",password);
        Console.WriteLine("C# stage: employee resubmit");
        view=new HttpTaskQueryService(shared).CaptureViewSession()!;
        api=new ResultsApi(view.Session,ResultsConnection.Files);
        var context=Wait(new HttpTaskPreparationService(view.Session).GetContextAsync("8"));
        var key=Wait(view.Session.PostAsync<ResultKey>("write-requests/keys",
            new {action="task.rework_started",target="8"}));
        Wait(view.Session.PostIdempotentAsync<JsonElement>("tasks/8/rework-start",
            new {expected_row_version=context.RowVersion,expected_assignment_revision=context.AssignmentRevision},key.RequestKey));
        SubmissionForm.CheckSubmit(view.Session,view.QueryService,null,2);
        var second=Wait(api.SubmissionsAsync("8",CancellationToken.None)).First();
        Check(second.RoundNo=="2"&&second.Attachments.Count==0,"second round and no synthetic attachment");

        Login("manager",password);
        Console.WriteLine("C# stage: approve and download");
        view=new HttpTaskQueryService(shared).CaptureViewSession()!;
        api=new ResultsApi(view.Session,ResultsConnection.Files);
        task=Wait(api.ReviewTaskAsync("8",CancellationToken.None)).Summary;
        ReviewDecisionForm.CheckDecision(true,api,task,second);
        var all=Wait(api.SubmissionsAsync("8",CancellationToken.None));
        Check(all.Count==2&&all[0].Status=="approved"&&all[1].Description=="C# result 1","UI approval and past submission retained");
        using var historyView=new HistoryControl();
        historyView.CheckHistory("8");
        string output=Path.Combine(root,"client-download.any");
        Wait(api.DownloadAsync(first.Attachments[0],output,CancellationToken.None));
        Check(File.ReadAllBytes(output).SequenceEqual(bytes),"C# actual upload/download bytes");
        bool corrupt=false;
        try { Wait(api.DownloadAsync(first.Attachments[0] with {Sha256=new string('0',64)},output,CancellationToken.None)); }
        catch(InvalidDataException) {corrupt=true;}
        Check(corrupt&&File.ReadAllBytes(output).SequenceEqual(bytes),"hash failure preserves existing destination");

        Login("worker",password);
        Console.WriteLine("C# stage: response recovery");
        view=new HttpTaskQueryService(shared).CaptureViewSession()!;
        api=new ResultsApi(view.Session,ResultsConnection.Files);
        var ready=Wait(api.ContextAsync("5",CancellationToken.None));
        var pending=api.Begin("submission.submitted","5","results/tasks/5/submissions","5",
            new SubmitResultBody(ready.RowVersion,ready.AssignmentRevision,"C# response loss",[]));
        bool lost=false;
        try {Wait(api.RunAsync(pending,CancellationToken.None));}
        catch(ApiException error) when(error.StatusCode==502){lost=true;}
        Check(lost&&pending.Stage=="check","unknown response retained");
        var reopened=new ResultsApi(view.Session,ResultsConnection.Files);
        Check(ReferenceEquals(reopened.Pending("submission.submitted","5"),pending),"same session reopen restores request");
        var saved=Wait(reopened.RunAsync(pending,CancellationToken.None));
        Check(saved?.TaskStatus=="submitted"&&Wait(api.SubmissionsAsync("5",CancellationToken.None)).Count==1,"receipt lookup recovers exactly one submission");
        var previousSession=view.Session;
        shared.ClearIdentity();
        Application.DoEvents();
        Check(!previousSession.IsCurrent&&reopened.Pending("submission.submitted","5")==null,"session change clears pending request");

        Until(()=>historyView.HistoryIsCleared);
        Check(historyView.HistoryIsCleared,"history clears on changed login session");
        File.Delete(source);
        File.Delete(output);
        return "PASS "+Count+" C# checks: actual loopback HTTP, WinForms controls, file bytes and receipt recovery.";
    }
}

public partial class SubmissionForm
{
    internal static void CheckSubmit(ApiClient.Session session, ITaskQueryService queries,string? file,int round)
    {
        using var form=new SubmissionForm(PreviewRole.Employee,session,queries);
        form.Tag=new Dictionary<string,string>{{"TaskId","8"}};
        _=form.Handle;
        form.OnLoad(EventArgs.Empty);
        ResultsFunctionalCheck.Until(()=>!form.busy);
        ResultsFunctionalCheck.Check(form.btnSubmit.Enabled,"submission UI enabled after actual context");
        ResultsFunctionalCheck.Check(form.txtSubmissionVersion.Text.Contains(round+"차"),"server round displayed");
        ResultsFunctionalCheck.Check(form.gridPreviousFiles.Rows.Count==(round==1?0:1),"actual previous attachment rows");
        form.txtResultDescription.Text="C# result "+round;
        if(file!=null) form.AddAttachments([file]);
        form.Unavailable_Click(form.btnSubmit,EventArgs.Empty);
        ResultsFunctionalCheck.Until(()=>!form.busy);
        ResultsFunctionalCheck.Check(form.DialogResult==DialogResult.OK,"submission closes OK only after real save");
    }
}

public partial class ReviewDecisionForm
{
    internal static void CheckDecision(bool approve,ResultsApi api,TaskSummary task,ResultSubmission submission)
    {
        using var form=new ReviewDecisionForm(approve,api,task,submission);
        _=form.Handle;
        form.OnLoad(EventArgs.Empty);
        ResultsFunctionalCheck.Until(()=>form.context!=null||form.blocked);
        ResultsFunctionalCheck.Check(form.btnDecision.Enabled&&form.txtDecisionReason.Text=="","real decision context and empty reason");
        ResultsFunctionalCheck.Check(form.txtContent5.Text.Contains(submission.Description),"selected submission displayed");
        if(!approve)
        {
            form.Unavailable_Click(form.btnDecision,EventArgs.Empty);
            ResultsFunctionalCheck.Until(()=>!form.busy);
            ResultsFunctionalCheck.Check(form.DialogResult!=DialogResult.OK&&form.txtContent5.Text.Contains("반려 사유"),"empty rejection blocked");
            form.txtDecisionReason.Text="TEST UI correction";
        }
        form.Unavailable_Click(form.btnDecision,EventArgs.Empty);
        ResultsFunctionalCheck.Until(()=>!form.busy);
        ResultsFunctionalCheck.Check(form.DialogResult==DialogResult.OK,"decision closes OK after actual save");
    }
}

public partial class ReviewsControl
{
    internal void CheckView(string taskId,bool reviewing,string description,int rounds)
    {
        _=Handle;
        if(reviewing) cmbResultView.SelectedIndex=1;
        ResultsFunctionalCheck.Wait(RefreshAsync());
        var row=gridReviews.Rows.Cast<DataGridViewRow>().Single(r=>
            ((TaskSummary)r.Tag!).TaskId==taskId);
        gridReviews.CurrentCell=row.Cells[0];
        ResultsFunctionalCheck.Until(()=>selectedTask?.Summary.TaskId==taskId&&
            gridVersions.Rows.Count==rounds&&txtContent2.Text.Contains(description));
        ResultsFunctionalCheck.Check(txtContent2.Text.Contains(description)&&gridVersions.Rows.Count==rounds,"selected real detail and history");
        if(gridAttachments.Rows.Count>0) gridAttachments.CurrentCell=gridAttachments.Rows[0].Cells[0];
        ResultsFunctionalCheck.Check(gridAttachments.Rows.Count==1&&btnDownloadAttachment.Enabled,"actual attachment row and download enabled");
        ResultsFunctionalCheck.Check(!reviewing||(btnApprovePreview.Enabled&&btnRejectPreview.Enabled),"actual current reviewer action buttons");
        txtContent.Text="TEST task "+taskId;
        FilterReviews();
        ResultsFunctionalCheck.Until(()=>selectedTask?.Summary.TaskId==taskId);
        ResultsFunctionalCheck.Check(gridReviews.Rows.Cast<DataGridViewRow>().Count(r=>r.Visible)==1,"task search");
    }
}


public partial class HistoryControl
{
    internal bool HistoryIsCleared => gridHistory.Rows.Count==0&&txtContent.Text.Contains("로그인");
    internal void CheckHistory(string id)
    {
        _=Handle;
        OnLoad(EventArgs.Empty);
        ResultsFunctionalCheck.Wait(RefreshHistoryAsync());
        ResultsFunctionalCheck.Check(gridHistory.Rows.Count>0,"history displays actual audit rows");
        for(int i=1;i<cmbOptions.Items.Count;i++)
            if(cmbOptions.Items[i] is TaskOption option&&option.Id==id) cmbOptions.SelectedIndex=i;
        var rows=gridHistory.Rows.Cast<DataGridViewRow>().ToList();
        ResultsFunctionalCheck.Check(rows.Count==5,"history has two submissions rejection rework approval");
        ResultsFunctionalCheck.Check(rows.All(r=>r.Tag is HistoryRow h&&h.Task.TaskId==id),"history task filter");
        ResultsFunctionalCheck.Check(rows.Any(r=>Convert.ToString(r.Cells[3].Value)=="상태 변경"),"member2 rework record");
        ResultsFunctionalCheck.Check(txtContent.Text.Contains("TEST task "+id)&&txtContent.Text.Contains("수행자"),"selected history detail");
        cmbOptions2.SelectedItem="승인";
        ResultsFunctionalCheck.Check(gridHistory.Rows.Count==1&&txtContent.Text.Contains("2차"),"history approval type filter and round");
        cmbOptions2.SelectedItem="생성";
        ResultsFunctionalCheck.Check(gridHistory.Rows.Count==0&&txtContent.Text.Contains("없습니다"),"empty history filter has no sample records");
        cmbOptions2.SelectedIndex=0;
    }
}
