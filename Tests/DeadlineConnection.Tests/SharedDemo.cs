using System.Reflection;
using RemoteWork.WinForms.Features.DeadlineAlerts;
using RemoteWork.WinForms.Features.Tasks;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

internal static partial class Program
{
    private static async Task SharedDemo()
    {
        // Explicit --shared-demo only. Credentials are ephemeral environment
        // values from a private launcher, never checked-in fixtures or stdout.
        string task=Environment.GetEnvironmentVariable("DEADLINE_DEMO_TASK_ID")??throw new Exception("User-approved NEW demo task ID required");
        if(!long.TryParse(task,out long id)||id<=0||id.ToString()!=task)throw new Exception("Invalid demo ID");
        var api=AppServices.Api;string old=api.ServerUri.ToString();
        string url=Environment.GetEnvironmentVariable("DEADLINE_TEST_URL")!,user=Environment.GetEnvironmentVariable("DEADLINE_SHARED_LOGIN")!,password=Environment.GetEnvironmentVariable("DEADLINE_SHARED_PASSWORD")!;
        api.SetServer(url);
        try
        {
            var login=await api.LoginAsync(user,password);
            using var main=new MainForm(login.User){ShowInTaskbar=false,Opacity=0,WindowState=FormWindowState.Minimized};
            main.Show();
            var end=DateTime.UtcNow.AddSeconds(15);
            while(DeadlineAlertScope.Find(main)?.Snapshot is null&&DateTime.UtcNow<end)await Task.Delay(30);
            var scope=DeadlineAlertScope.Find(main)??throw new Exception("Actual MainForm deadline source was not connected");
            Check(scope.Snapshot is not null,"real MainForm snapshot ready");
            var item=scope.Snapshot!.Items.Single(x=>x.TaskId==task);
            Check(!item.IsConfirmed&&scope.Unread.Any(x=>x.TaskId==task),"new approved demo is unread/eligible");
            var untouched=scope.Snapshot.Items.Where(x=>x.TaskId!=task).ToDictionary(x=>x.EventToken,x=>x.IsConfirmed);
            Check(await scope.OpenTaskAsync(item.EventToken),"real alert recheck requests exact task navigation");
            var notifications=(LoginDeadlineNotifications)typeof(MainForm).GetField("deadlineNotifications",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!;
            end=DateTime.UtcNow.AddSeconds(15);
            while(notifications.LastNavigationSucceeded is null&&DateTime.UtcNow<end)await Task.Delay(30);
            Check(notifications.LastNavigationSucceeded==true,"common MainForm callback completes real task HTTP navigation");
            var page=typeof(MainForm).GetField("currentPage",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main);
            Check(page is TasksControl&&((TasksControl)page).GetType().GetField("selectedTaskId",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(page) as string==task,"actual selected own task matches original demo ID");
            var result=await scope.ConfirmAsync(item.EventToken);
            Check(result.State==DeadlineConfirmationState.Confirmed,"actual product source commits approved demo confirmation");
            Check(scope.Snapshot!.Items.Single(x=>x.TaskId==task).IsConfirmed,"current panel observes saved confirmation");
            Check(scope.Snapshot.Items.Where(x=>untouched.ContainsKey(x.EventToken)).All(x=>untouched[x.EventToken]==x.IsConfirmed),"other existing confirmation states untouched");
            // A second independent login/session verifies shared DB visibility.
            // It runs on this PC; do not call it a two-physical-PC test.
            using var other=new ApiClient(url);await other.LoginAsync(user,password);
            using(var second=new HttpDeadlineAlertSource(other.CaptureSession()))
                Check((await second.ReadAsync(default)).Items.Single(x=>x.TaskId==task).IsConfirmed,"independent account session sees shared confirmation");
            await other.LogoutAsync();
            await api.LogoutAsync();Check(!scope.IsCurrent,"original login scope ends");
            await api.LoginAsync(user,password);
            using var restored=new HttpDeadlineAlertSource(api.CaptureSession());
            Check((await restored.ReadAsync(default)).Items.Single(x=>x.TaskId==task).IsConfirmed,"relogin/new product source sees durable confirmation");
        }
        finally{if(api.CurrentUser is not null)await api.LogoutAsync();api.SetServer(old);}
    }
}
