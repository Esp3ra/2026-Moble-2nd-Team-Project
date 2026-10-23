using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Infrastructure;

internal static class PartialSelectionChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static void Check(bool ok,string label) { if(!ok)throw new Exception(label);checks++; }
    private static T Field<T>(object owner,string name) => (T)owner.GetType().GetField(name,Hidden)!.GetValue(owner)!;
    private static async Task Invoke(object owner,string name,params object[] args) => await (Task)owner.GetType().GetMethod(name,Hidden)!.Invoke(owner,args)!;
    [STAThread]
    internal static int Run()
    {
        ApplicationConfiguration.Initialize();Exception? failure=null;
        using var host=new Form { ShowInTaskbar=false,Opacity=0 };
        host.Shown+=async(_,_)=>{try{await Actual(host);}catch(Exception ex){failure=ex;}finally{host.Close();}};
        Application.Run(host);
        if(failure is not null){Console.Error.WriteLine(failure);return 1;}
        Console.WriteLine($"PASS {checks} R78-01 actual WinForms/ApiClient/HTTP/MySQL checks (injected response faults; no file-dialog automation)");return 0;
    }
    private sealed record Message(string Id,string Content);
    private sealed record Messages(Message[] Items);
    private static async Task Actual(Form host)
    {
        using var fault=new Fault();using var api=new ApiClient(Environment.GetEnvironmentVariable("FILES_TEST_URL")!,fault);
        var user=(await api.LoginAsync("admin1","Admin-test-2026!")).User;
        var assembly=typeof(HttpNoticeService).Assembly;
        var serviceType=assembly.GetType("RemoteWork.WinForms.Features.Collaboration.HttpChatService")!;
        var service=Activator.CreateInstance(serviceType,api,user)!;
        string room=Environment.GetEnvironmentVariable("FILES_TEST_ROOM")!,folder=Environment.GetEnvironmentVariable("FILES_TEST_DIR")!;
        var paths=new[]{"A.txt","B.txt","C.txt"}.Select(n=>Path.Combine(folder,n)).ToArray();
        foreach(var path in paths)await File.WriteAllTextAsync(path,"R78 fixture "+Path.GetFileName(path));
        var roomsTask=(Task)serviceType.GetMethod("GetRoomsAsync")!.Invoke(service,[CancellationToken.None])!;await roomsTask;
        var rooms=(System.Collections.IEnumerable)roomsTask.GetType().GetProperty("Result")!.GetValue(roomsTask)!;
        var current=rooms.Cast<object>().Single(r=>(string)r.GetType().GetProperty("Id")!.GetValue(r)! == room);
        var controlType=assembly.GetType("RemoteWork.WinForms.Features.Collaboration.ChatControl")!;
        using var page=(UserControl)Activator.CreateInstance(controlType,BindingFlags.Instance|BindingFlags.NonPublic,null,[service,false],null)!;
        controlType.GetField("selectedRoom",Hidden)!.SetValue(page,current);
        Field<TextBox>(page,"txtContent3").Text="R78 ABC "+Guid.NewGuid().ToString("N");
        var files=new ResourceAttachmentClient(api.CaptureSession());
        Func<string,CancellationToken,Task<ResourceAttachment>> upload=(path,ct)=>files.UploadAsync("chat",room,path,ct);
        await Invoke(page,"UploadSelectedFilesAsync",room,paths,upload);
        Check(fault.Starts.GetValueOrDefault("A.txt")==1 && fault.Starts.GetValueOrDefault("B.txt")==1 && !fault.Starts.ContainsKey("C.txt"),"A succeeds/B fails/C untried");
        Check(!Field<Button>(page,"btnSendMessage").Enabled,"incomplete selection blocks actual send button");
        await Invoke(page,"UploadSelectedFilesAsync",room,new[]{paths[1]},upload);
        Check(!Field<Button>(page,"btnSendMessage").Enabled,"B-only retry keeps C blocking");
        await Invoke(page,"SendMessageAsync");Check(fault.Messages.Count==0,"Enter/programmatic send also blocked while C pending");
        Check(fault.Starts.GetValueOrDefault("B.txt")==1,"B retry keeps original upload ID");
        await Invoke(page,"UploadSelectedFilesAsync",room,new[]{paths[2]},upload);
        Check(Field<Button>(page,"btnSendMessage").Enabled,"C completion enables send");
        await Invoke(page,"SendMessageAsync");
        var original=fault.Messages.Single();var sent=JsonDocument.Parse(original).RootElement;
        Check(sent.GetProperty("attachment_ids").GetArrayLength()==3 && sent.GetProperty("attachment_ids").EnumerateArray().Select(x=>x.GetString()).Distinct().Count()==3,"original ABC each once in final body");
        var linked=await files.ListAsync("chat",sent.GetProperty("message_id").GetString()!);
        Check(linked.Length==3 && linked.Select(x=>x.Name).Order().SequenceEqual(new[]{"A.txt","B.txt","C.txt"}),"actual DB links contain ABC exactly once");
        // Explicit removal is a separate user action, not silent loss. It never
        // deletes bytes or releases resource references.
        files.ForgetLinkedUploads(linked.Select(f=>f.AttachmentId));fault.FailNextB=true;
        Field<TextBox>(page,"txtContent3").Text="R78 remove C "+Guid.NewGuid().ToString("N");
        await Invoke(page,"UploadSelectedFilesAsync",room,paths,upload);
        await Invoke(page,"UploadSelectedFilesAsync",room,new[]{paths[1]},upload);
        Check(!Field<Button>(page,"btnSendMessage").Enabled,"second B-only retry still blocks C");
        Check((bool)controlType.GetMethod("RemoveSelectedFile",Hidden)!.Invoke(page,[room,paths[2]])!,"explicitly remove C from local selection");
        Check(Field<Button>(page,"btnSendMessage").Enabled,"explicit C removal enables AB send");
        fault.LoseNextMessage=true;await Invoke(page,"SendMessageAsync");
        Check(!Field<Button>(page,"btnAttachFile").Enabled,"unknown committed message freezes attachment editing");
        Check(!(bool)controlType.GetMethod("RemoveSelectedFile",Hidden)!.Invoke(page,[room,paths[0]])!,"unknown original send refuses removal");
        await Invoke(page,"SendMessageAsync");
        Check(fault.Messages.Count==3 && fault.Messages[1]==fault.Messages[2],"manual unknown retry retains exact UUID/body/file array");
        var second=JsonDocument.Parse(fault.Messages[1]).RootElement;
        Check(second.GetProperty("attachment_ids").GetArrayLength()==2,"explicit removal sends AB only");
        Check((await files.ListAsync("chat",second.GetProperty("message_id").GetString()!)).Length==2,"replay preserves two DB links");
        await api.LogoutAsync();
    }
    private sealed class Fault : DelegatingHandler
    {
        internal readonly Dictionary<string,int> Starts=new();internal readonly List<string> Messages=new();
        internal bool FailNextB=true,LoseNextMessage;private string? bId;
        internal Fault():base(new HttpClientHandler {UseCookies=false,AllowAutoRedirect=false}){}
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            string path=request.RequestUri!.AbsolutePath;
            string? body=request.Content is null?null:await request.Content.ReadAsStringAsync(ct);
            if(request.Method==HttpMethod.Post && path.EndsWith("/messages"))Messages.Add(body!);
            var response=await base.SendAsync(request,ct);
            if(request.Method==HttpMethod.Post && path.EndsWith("/uploads/start") && response.IsSuccessStatusCode)
            {
                string name=JsonDocument.Parse(body!).RootElement.GetProperty("name").GetString()!;
                Starts[name]=Starts.GetValueOrDefault(name)+1;
                if(name=="B.txt")bId=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.GetProperty("data").GetProperty("attachment_id").GetString();
            }
            if(response.IsSuccessStatusCode && ((FailNextB && bId is not null && path.EndsWith($"/uploads/{bId}/chunks")) || (LoseNextMessage && request.Method==HttpMethod.Post && path.EndsWith("/messages"))))
            {
                if(path.EndsWith("/chunks"))FailNextB=false;else LoseNextMessage=false;
                response.Dispose();return new(HttpStatusCode.ServiceUnavailable){Content=new StringContent("{\"ok\":false,\"error\":{\"code\":\"TEST_RESPONSE_LOSS\",\"message\":\"synthetic committed response loss\"}}",Encoding.UTF8,"application/json")};
            }
            return response;
        }
    }
}
