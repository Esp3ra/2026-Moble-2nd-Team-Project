using System.Net;
using System.Net.Http;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Shell;

internal static class Program
{
    private static int checks;
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS " + message); }
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        var api = AppServices.Api;
        var health = api.GetAsync<HealthResult>("health").GetAwaiter().GetResult();
        Check(health.Database == "connected", "C# -> Flask -> MySQL health");
        var admin = api.LoginAsync("smokeadmin", Environment.GetEnvironmentVariable("SMOKE_ADMIN_PASSWORD")!).GetAwaiter().GetResult();
        Check(admin.User.Role == "admin", "administrator login and snake_case JSON mapping");
        Check(api.GetAsync<PageResult<UserInfo>>("users").GetAwaiter().GetResult().Items.Count >= 2, "admin user list");
        Render(new LoginForm(), "login");
        Render(new MainForm(admin.User), "admin");
        Render(new ConnectionSettingsForm(), "settings");
        Render(new UserManagementForm(), "users");
        Render(new PasswordChangeForm(), "password");
        SynchronizationContext.SetSynchronizationContext(null);
        api.LogoutAsync().GetAwaiter().GetResult();
        var staff = api.LoginAsync("smokestaff", Environment.GetEnvironmentVariable("SMOKE_STAFF_PASSWORD")!).GetAwaiter().GetResult();
        Check(staff.User.Role == "employee", "employee login");
        Render(new MainForm(staff.User), "employee");
        SynchronizationContext.SetSynchronizationContext(null);
        try { api.GetAsync<PageResult<UserInfo>>("users").GetAwaiter().GetResult(); throw new Exception("employee admin access succeeded"); }
        catch (ApiException e) { Check(e.StatusCode == 403, "employee gets server-enforced 403"); }
        api.LogoutAsync().GetAwaiter().GetResult();
        try { api.RefreshIdentityAsync().GetAwaiter().GetResult(); throw new Exception("logout token reused"); }
        catch (ApiException e) { Check(e.StatusCode == 401, "logout revokes session"); }
        using var malformed = new ApiClient("http://127.0.0.1:15500", new StubHandler("<html>proxy error</html>", HttpStatusCode.BadGateway));
        try { malformed.GetAsync<HealthResult>("health").GetAwaiter().GetResult(); throw new Exception("malformed body accepted"); }
        catch (ApiException e) { Check(e.Message.Contains("응답 형식"), "malformed response gives readable error"); }
        using var unavailable = new ApiClient("http://127.0.0.1:15500", new OfflineHandler());
        try { unavailable.GetAsync<HealthResult>("health").GetAwaiter().GetResult(); throw new Exception("offline accepted"); }
        catch (ApiException e) { Check(e.Message.Contains("연결할 수 없습니다"), "offline server gives readable error"); }
        Check(ApiClient.ValidateUrl("http://localhost:5000").Port == 5000, "server URL validation");
        try { ApiClient.ValidateUrl("https://user:pass@example.com"); throw new Exception("credentials in URL accepted"); }
        catch (ArgumentException) { Check(true, "URL credentials rejected"); }
        Console.WriteLine($"{checks} client integration checks passed; six forms rendered.");
    }

    private static void Render(Form form, string name)
    {
        using (form)
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-32000, -32000);
            form.ShowInTaskbar = false;
            form.Show();
            var deadline = DateTime.UtcNow.AddMilliseconds(1200);
            while (DateTime.UtcNow < deadline) { Application.DoEvents(); Thread.Sleep(10); }
            form.PerformLayout();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            var output = Path.Combine(Environment.GetEnvironmentVariable("SMOKE_QA_DIR")!, name + ".png");
            bitmap.Save(output);
        }
    }
    private sealed class StubHandler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("simulated offline");
    }
}
