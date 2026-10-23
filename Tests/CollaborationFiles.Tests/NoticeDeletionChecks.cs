using System.Net;
using System.Reflection;
using System.Text;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

internal static class NoticeDeletionChecks
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    [STAThread] internal static int Run()
    {
        ApplicationConfiguration.Initialize(); Exception? failure=null;
        using var host=new Form {ShowInTaskbar=false,Opacity=0};
        host.Shown+=async(_,_)=>{try{await Actual(host);}catch(Exception e){failure=e;}finally{host.Close();}};
        Application.Run(host);
        if(failure is not null){Console.Error.WriteLine(failure);return 1;}
        Console.WriteLine("PASS 7 D3 actual WinForms/HttpNoticeService/HTTP/MySQL delete checks; committed response loss injected");return 0;
    }
    private static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Hidden)!.GetValue(o)!;
    private static async Task Actual(Form host)
    {
        using var loss=new Loss();using var api=new ApiClient(Environment.GetEnvironmentVariable("FILES_TEST_URL")!,loss);
        await api.LoginAsync("admin1","Admin-test-2026!");
        var service=new HttpNoticeService(api);var files=new ResourceAttachmentClient(api.CaptureSession());
        var path=Path.Combine(Environment.GetEnvironmentVariable("FILES_TEST_DIR")!,"D3 desktop.txt");await File.WriteAllTextAsync(path,"D3 actual desktop bytes");
        var file=await files.UploadAsync("notice",null,path);
        var item=await service.CreateNoticeAsync(new("D3 actual UI","delete only test notice","전 직원",false,[]){AttachmentIds=[file.AttachmentId]});
        using var page=new NoticesControl(PreviewRole.Administrator,service);host.Controls.Add(page);page.CreateControl();
        var deadline=DateTime.UtcNow.AddSeconds(15);
        while(Field<bool>(page,"busy") && DateTime.UtcNow<deadline)await Task.Delay(20);
        Check(!Field<bool>(page,"busy"),"initial actual HTTP load completes before delete action");
        await page.ReloadNoticesAsync();
        bool Listed()=>Field<DataGridView>(page,"gridNotices").Rows.Cast<DataGridViewRow>().Any(r=>r.Tag is Notice n && n.Id==item.Id);
        Task Delete()=> (Task)typeof(NoticesControl).GetMethod("DeleteNotice",Hidden)!.Invoke(page,[item])!;
        loss.Drop=true;await Delete();
        Check(loss.Deletes==1 && Listed(),"unknown result keeps original row and sends once");
        Check(Field<Label>(page,"lblText4").Text=="삭제 결과 확인 필요","actual committed loss is shown as unknown");
        await Delete();Check(loss.Deletes==1,"unknown delete cannot POST again before authoritative GET");
        await page.ReloadNoticesAsync();Check(!Listed(),"real list GET confirms deletion without retrying POST");
        try{await service.GetNoticeAsync(item.Id);throw new Exception("deleted notice exists");}catch(ApiException e){Check(e.StatusCode==404,"notice removed from actual DB");}
        try{await files.ListAsync("notice",item.Id.ToString());throw new Exception("deleted attachment accessible");}catch(ApiException e){Check(e.StatusCode==404,"deleted resource download access removed");}
        await api.LogoutAsync();host.Controls.Remove(page);
    }
    private sealed class Loss:DelegatingHandler
    {
        internal bool Drop;internal int Deletes;
        internal Loss():base(new HttpClientHandler {UseCookies=false}){}
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            bool delete=request.Method==HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/delete");
            if(delete)Deletes++;
            var response=await base.SendAsync(request,ct);
            if(delete && Drop && response.IsSuccessStatusCode)
            {
                Drop=false;response.Dispose();return new(HttpStatusCode.ServiceUnavailable){Content=new StringContent("{\"ok\":false,\"error\":{\"code\":\"TEST_RESPONSE_LOSS\",\"message\":\"synthetic response loss\"}}",Encoding.UTF8,"application/json")};
            }
            return response;
        }
    }
}
