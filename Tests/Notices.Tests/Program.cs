using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;
using RemoteWork.WinForms.Shell;

internal static class Program
{
    private static int checks;
    private static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Hidden)!.GetValue(owner)!;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        checks++;
    }
    private static Notice Notice(int id = 1) => new() { Id = id, AuthorId = 1, Author = "서버 관리자",
        Title = "DB 공지", Content = "DB 본문", Target = "디자인팀", CreatedAt = DateTime.Now };
    private static object Wire(int id = 1) => new { id, author_id = 1, author = "서버 관리자",
        title = "DB 공지", content = "DB 본문", target = "디자인팀", is_pinned = true,
        created_at = "2026-10-17T00:00:00Z", attachment_names = new[] { "안내.pdf" } };
    private static HttpResponseMessage Json(object data) => new(HttpStatusCode.OK) {
        Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data }, ApiClient.Json), Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Login() => Json(new LoginResult("test-token", "Bearer", DateTimeOffset.UtcNow.AddHours(1),
        new UserInfo(1, "test", "테스트", "T1", "개발팀", "admin", true)));

    [STAThread]
    private static int Main(string[] args)
    {
        Application.EnableVisualStyles();
        Control.CheckForIllegalCrossThreadCalls = true;
        using var host = new Form { ShowInTaskbar = false, Location = new Point(-20000, -20000),
            StartPosition = FormStartPosition.Manual, Size = new Size(1000, 750) };
        int result = 1;
        host.Shown += async (_, _) =>
        {
            try
            {
                if (args.Length == 2 && args[0] == "--popup-e2e")
                {
                    await PopupEndToEnd(args[1], host);
                    Console.WriteLine($"PASS: {checks} automatic popup end-to-end checks; actual MainForm/WinForms and HTTP/MySQL.");
                    result = 0;
                    return;
                }
                await HttpContract();
                await PopupUnavailable(host);
                await PopupSessionChanged(host);
                await SaveLifecycle(host);
                await ScreenLifecycle(host);
                await DeleteOutcome(host);
                if (args.Length > 0) await RealHttp(args[0], host);
                Console.WriteLine($"PASS: {checks} notice checks. " + (args.Length > 0 ? "Includes actual local HTTP/database." : "HTTP test double and actual WinForms controls; no database."));
                result = 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); }
            finally { host.Close(); }
        };
        Application.Run(host);
        return result;
    }

    private static async Task PopupEndToEnd(string url, Form host)
    {
        using var adminApi = new ApiClient(url);
        await adminApi.LoginAsync("admin1", "Admin-test-2026!");
        var admin = new HttpNoticeService(adminApi);
        await admin.CreateNoticeAsync(new("old-before-login", "existing", "전 직원", true, Array.Empty<string>()));
        var employeeApi = AppServices.Api;
        await employeeApi.LoginAsync("staff1", "Staff-test-2026!");
        using var main = new MainForm(employeeApi.CurrentUser!);
        main.Show(host);
        var coordinator = Field<ImportantNoticeCoordinator>(main, "noticeCoordinator");
        Check(coordinator is not null, "Signed-in employee MainForm owns the popup coordinator");
        await WaitFor(() => Field<bool>(coordinator!, "baselineInitialized"));
        Check(!main.OwnedForms.OfType<ImportantNoticeForm>().Any(), "Pre-login notice does not open a popup");
        var timer = Field<System.Windows.Forms.Timer>(coordinator!, "timer");
        Check(timer.Interval == 15000, "Production coordinator uses the existing 15-second interval");
        timer.Interval = 100; // Accelerate only this controlled GUI test; keep production default.
        await admin.CreateNoticeAsync(new("ordinary-no-popup", "regular", "전 직원", false, Array.Empty<string>()));
        await admin.CreateNoticeAsync(new("other-department-no-popup", "other", "디자인팀", true, Array.Empty<string>()));
        await Task.Delay(300);
        Check(!main.OwnedForms.OfType<ImportantNoticeForm>().Any(), "Ordinary and other-department notices are excluded");
        var own = await admin.CreateNoticeAsync(new("own-department-new", "own", "개발팀", true, Array.Empty<string>()));
        var all = await admin.CreateNoticeAsync(new("all-employees-new", "all", "전 직원", true, Array.Empty<string>()));
        await WaitFor(() => main.OwnedForms.OfType<ImportantNoticeForm>().Any());
        var popup = main.OwnedForms.OfType<ImportantNoticeForm>().Single();
        Check(Field<Label>(popup, "lblText2").Text == own.Title, "New department notice automatically displays without opening notices page");
        Field<Button>(popup, "btnConfirm").PerformClick();
        await WaitFor(() => popup.IsDisposed);
        await WaitFor(() => main.OwnedForms.OfType<ImportantNoticeForm>().Any());
        var next = main.OwnedForms.OfType<ImportantNoticeForm>().Single();
        Check(Field<Label>(next, "lblText2").Text == all.Title, "Next new all-employee notice automatically displays");
        Field<Button>(next, "btnConfirm").PerformClick();
        await WaitFor(() => next.IsDisposed);
        var pending = await new HttpNoticeService(employeeApi).GetPendingPopupsAsync();
        Check(!pending.Items.Any(n => n.Id == own.Id || n.Id == all.Id), "Confirmed notices disappear from the server pending query");
        main.Close();
        await WaitFor(() => main.IsDisposed);
        Check(!timer.Enabled, "Employee logout stops popup polling");
        await employeeApi.LoginAsync("admin1", "Admin-test-2026!");
        using var manager = new MainForm(employeeApi.CurrentUser!);
        Check(Field<ImportantNoticeCoordinator?>(manager, "noticeCoordinator") is null, "Administrator MainForm does not receive employee popups");
        await employeeApi.LogoutAsync();
        await adminApi.LogoutAsync();
    }

    private static async Task PopupSessionChanged(Form host)
    {
        int pending = 0;
        using var api = new ApiClient("http://test.invalid:5000", new Handler((request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("auth/login"))
                return Task.FromResult(Json(new LoginResult("test-token", "Bearer", DateTimeOffset.UtcNow.AddHours(1),
                    new UserInfo(1, "test", "직원", "T1", "개발팀", "employee", true))));
            pending++;
            return Task.FromResult(Json(new { items = Array.Empty<object>(), next_after_id = (int?)null }));
        }));
        await api.LoginAsync("test", "synthetic");
        var service = new HttpNoticeService(api);
        using var coordinator = new ImportantNoticeCoordinator(host, api, service, 1);
        var check = typeof(ImportantNoticeCoordinator).GetMethod("CheckAsync", Hidden)!;
        await (Task)check.Invoke(coordinator, null)!;
        Check(pending == 1, "Current popup session can establish a baseline");
        await api.LoginAsync("test", "synthetic");
        Field<System.Windows.Forms.Timer>(coordinator, "timer").Start();
        await (Task)check.Invoke(coordinator, null)!;
        Check(pending == 1 && !Field<System.Windows.Forms.Timer>(coordinator, "timer").Enabled,
            "Same-user new login cannot reuse the old popup coordinator");
        try { await service.GetPendingPopupsAsync(); throw new Exception("Old popup identity reused"); }
        catch (OperationCanceledException) { checks++; }
    }

    private static async Task PopupUnavailable(Form host)
    {
        int requests = 0;
        using var api = new ApiClient("http://test.invalid:5000", new Handler((request, ct) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("auth/login"))
                return Task.FromResult(Json(new LoginResult("test-token", "Bearer", DateTimeOffset.UtcNow.AddHours(1),
                    new UserInfo(1, "test", "직원", "T1", "개발팀", "employee", true))));
            requests++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) {
                Content = new StringContent("{\"ok\":false,\"error\":{\"code\":\"HTTP_404\",\"message\":\"not available\"}}", Encoding.UTF8, "application/json") });
        }));
        await api.LoginAsync("test", "synthetic");
        using var coordinator = new ImportantNoticeCoordinator(host, api, new HttpNoticeService(api), 1);
        string status = "";
        coordinator.StatusChanged += text => status = text;
        var timer = Field<System.Windows.Forms.Timer>(coordinator, "timer");
        timer.Start();
        await (Task)typeof(ImportantNoticeCoordinator).GetMethod("CheckAsync", Hidden)!.Invoke(coordinator, null)!;
        Check(requests == 1 && !timer.Enabled, "Unavailable popup API stops polling");
        Check(coordinator.LastError is null && status.Contains("제공하지 않습니다"), "Disabled/unsupported popup is not displayed as a retrying error");
    }

    private static async Task HttpContract()
    {
        int listPages = 0, creates = 0, deletes = 0;
        var handler = new Handler(async (request, ct) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("auth/login")) return Login();
            Check(request.Headers.Authorization?.Parameter == "test-token", "Shared bearer attached");
            if (request.Method == HttpMethod.Post && path == "/api/v1/notices")
            {
                creates++;
                using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                Check(!json.RootElement.TryGetProperty("attachment_ids", out _), "Empty attachments retain the deployed legacy notice contract");
                Check(json.RootElement.GetProperty("attachment_names")[0].GetString() == "안내.pdf", "Filename metadata serialized");
                Check(!json.RootElement.TryGetProperty("author", out _) && !json.RootElement.TryGetProperty("attachment_paths", out _), "No forged author or PC path sent");
                return Json(new { notice = Wire(23) });
            }
            if (path.EndsWith("/delete")) { deletes++; return Json(new MessageResult("deleted")); }
            if (path == "/api/v1/notices")
            {
                listPages++;
                int page = request.RequestUri.Query.Contains("page=1&") ? 1 : 2;
                return Json(new { items = (page == 1 ? Enumerable.Range(1, 100) : new[] { 101 }).Select(i => Wire(i)), page, limit = 100, total = 101 });
            }
            return Json(new { notice = Wire(23) });
        });
        using var api = new ApiClient("http://test.invalid:5000", handler);
        await api.LoginAsync("test", "synthetic");
        var service = new HttpNoticeService(api);
        var saved = await service.CreateNoticeAsync(new("제목", "본문", "디자인팀", true, new[] { "안내.pdf" }));
        Check(saved.Id == 23 && saved.Author == "서버 관리자" && saved.AttachmentPaths.Length == 0, "Committed server identity replaces local draft");
        Check(saved.AttachmentNames.Single() == "안내.pdf", "Filename metadata mapped");
        Check((await service.GetNoticesAsync()).Count == 101 && listPages == 2, "Every page loaded");
        Check((await service.GetNoticeAsync(23)).Id == 23, "Detail ID mapped");
        try { await service.GetNoticeAsync(999); throw new Exception("Mismatched detail accepted"); }
        catch (ApiException) { checks++; }
        await service.DeleteNoticeAsync(23);
        Check(creates == 1 && deletes == 1, "No automatic write retry");
    }

    private static async Task SaveLifecycle(Form host)
    {
        var service = new ControlledService();
        using var editor = new NoticeEditForm(service) { Location = new Point(-20000, -20000) };
        editor.Show(host);
        var title = Field<TextBox>(editor, "txtNoticeTitle");
        var body = Field<TextBox>(editor, "txtNoticeBody");
        title.Text = "입력 유지"; body.Text = "작성 중인 본문";
        var pending = editor.PublishAsync();
        Check(editor.CreatedNotice is null && editor.DialogResult != DialogResult.OK, "No success before commit response");
        Check(!Field<Button>(editor, "btnPublishNotice").Enabled, "Duplicate click disabled while saving");
        await editor.PublishAsync();
        Check(service.Creates == 1, "Repeated publish while pending does not post twice");
        editor.Close();
        Check(!editor.IsDisposed, "Closing does not pretend pending write was canceled");
        service.CreateCompletion.SetException(new ApiException("synthetic database failure", 503));
        await pending;
        Check(title.Text == "입력 유지" && body.Text == "작성 중인 본문", "Save failure preserves inputs");
        Check(editor.CreatedNotice is null && editor.DialogResult != DialogResult.OK && !editor.IsDisposed, "Save failure keeps editor open");
        Check(Field<TextBox>(editor, "saveError").Text.Contains("synthetic database failure"), "Error is visible inline");
        Check(!Field<Button>(editor, "btnPublishNotice").Enabled, "Unknown notice result blocks a duplicate original save");
        await editor.PublishAsync();
        Check(service.Creates == 1, "Unknown save preserves the original inputs and makes no second request");
        editor.Close();
        using var fresh = new NoticeEditForm(service) { Location = new Point(-20000, -20000) };
        fresh.Show(host);
        Field<TextBox>(fresh, "txtNoticeTitle").Text = "새로 확인한 입력";
        Field<TextBox>(fresh, "txtNoticeBody").Text = "본문";
        service.CreateCompletion = new();
        var retry = fresh.PublishAsync();
        service.CreateCompletion.SetResult(Notice(42));
        await retry;
        Check(fresh.CreatedNotice?.Id == 42 && fresh.DialogResult == DialogResult.OK, "Only successful commit closes editor");
    }

    private static async Task ScreenLifecycle(Form host)
    {
        var service = new ControlledService();
        service.Items.Add(Notice(11));
        using var page = new NoticesControl(PreviewRole.Administrator, service);
        host.Controls.Add(page);
        page.CreateControl();
        await page.ReloadNoticesAsync();
        var grid = Field<DataGridView>(page, "gridNotices");
        Check(grid.Rows.Count == 1, "Screen reads injected service");
        Check(Field<Label>(page, "lblText4").Text == "공지사항 상세", "Row selection alone leaves detail closed");
        Field<Button>(page, "btnQuery").PerformClick();
        Check(Field<TextBox>(page, "txtContent").Text.Contains("DB 본문"), "Query opens current server detail");
        service.Items.Add(Notice(12));
        Field<Button>(page, "btnQuery").PerformClick();
        Check(grid.Rows.Count == 2, "Query reloads newly added server notice");
        service.ListError = new ApiException("offline");
        await page.ReloadNoticesAsync();
        Check(grid.Rows.Count == 0 && Field<TextBox>(page, "txtContent").Text.Contains("offline"), "Failure clears stale rows and shows retry message");
        service.ListError = null;
        await page.ReloadNoticesAsync();
        Check(grid.Rows.Count == 2, "List retry recovers");
        service.ListCompletion = new();
        var delayed = page.ReloadNoticesAsync();
        page.Dispose();
        service.ListCompletion.SetResult(new[] { Notice(999) });
        await delayed;
        Check(page.IsDisposed, "Late response after disposal is ignored without UI access");
    }

    private static async Task DeleteOutcome(Form host)
    {
        var service = new ControlledService(); var notice = Notice(88);
        service.Items.Add(notice);
        using var page = new NoticesControl(PreviewRole.Administrator, service);
        host.Controls.Add(page); page.CreateControl(); await page.ReloadNoticesAsync();
        var method = typeof(NoticesControl).GetMethod("DeleteNotice", Hidden)!;
        Task Delete() => (Task)method.Invoke(page, new object[] {notice})!;
        service.DeleteError = new ApiException("response lost", 503);
        await Delete();
        Check(Field<Label>(page,"lblText4").Text=="삭제 결과 확인 필요", "unknown delete is not declared a definite failure or success");
        Check(Field<DataGridView>(page,"gridNotices").Rows.Count==1, "unknown delete retains original notice until GET");
        await Delete(); Check(service.Deletes==1,"unknown original delete does not send another POST");
        service.ListError = new ApiException("offline"); await page.ReloadNoticesAsync(); await Delete();
        Check(service.Deletes==1,"failed list GET cannot unlock original unknown delete");
        service.ListError=null; service.Items.Clear(); await page.ReloadNoticesAsync();
        Check(Field<DataGridView>(page,"gridNotices").Rows.Count==0,"authoritative list confirms a committed deletion");
        service.Items.Add(notice); await page.ReloadNoticesAsync(); service.DeleteError=new ApiException("forbidden",403);
        await Delete(); Check(Field<Label>(page,"lblText4").Text=="공지사항 삭제 실패","definite 403 remains an error with original row");
        service.DeleteError=null; await Delete();
        Check(service.Deletes==3 && Field<DataGridView>(page,"gridNotices").Rows.Count==0,"explicit successful deletion refreshes row only after server response");
        service.Items.Add(notice); await page.ReloadNoticesAsync();
        service.DeleteError=new ApiException("malformed success",200); await Delete(); await Delete();
        Check(service.Deletes==4 && Field<Label>(page,"lblText4").Text=="삭제 결과 확인 필요","malformed HTTP200 also preserves unknown original deletion without retry");
        host.Controls.Remove(page);
    }

    private static async Task RealHttp(string url, Form host)
    {
        // Optional fixture server: exclusively uses synthetic accounts in a disposable DB.
        using var api = new ApiClient(url);
        await api.LoginAsync("admin1", "Admin-test-2026!");
        var service = new HttpNoticeService(api);
        using var editor = new NoticeEditForm(service);
        editor.Show(host);
        Field<TextBox>(editor, "txtNoticeTitle").Text = "실제 HTTP 공지";
        Field<TextBox>(editor, "txtNoticeBody").Text = "실제 DB 저장 본문";
        Field<ComboBox>(editor, "cmbNoticeAudience").SelectedItem = "기획팀";
        Field<CheckBox>(editor, "chkPinToTop").Checked = true;
        await editor.PublishAsync();
        var saved = editor.CreatedNotice ?? throw new Exception("Real editor did not save");
        Check(editor.DialogResult == DialogResult.OK, "Real editor returns OK only after HTTP commit");
        Check(saved.Id > 0 && saved.Author == "관리자", "Real HTTP POST returns stored author/ID");
        using var otherApi = new ApiClient(url);
        await otherApi.LoginAsync("staff1", "Staff-test-2026!");
        var employee = new HttpNoticeService(otherApi);
        Check((await employee.GetNoticesAsync()).Any(n => n.Id == saved.Id && n.Target == "기획팀"), "Employee sees another department via real server");
        using var page = new NoticesControl(PreviewRole.Employee, employee);
        host.Controls.Add(page); page.CreateControl();
        await WaitFor(() => Field<Button>(page, "btnQuery").Enabled);
        await page.ReloadNoticesAsync();
        Check(Field<DataGridView>(page, "gridNotices").Rows.Count > 0, "Real HTTP/database populates WinForms list");
        Field<Button>(page, "btnQuery").PerformClick();
        await WaitFor(() => Field<Button>(page, "btnQuery").Enabled);
        Check(Field<TextBox>(page, "txtContent").Text.Contains("실제 DB 저장 본문"), "Real query button opens DB detail");
        try { await employee.DeleteNoticeAsync(saved.Id); throw new Exception("Employee deletion allowed"); }
        catch (ApiException ex) { Check(ex.StatusCode == 403, "Real server rejects employee delete"); }
        await service.DeleteNoticeAsync(saved.Id);
        Check(!(await employee.GetNoticesAsync()).Any(n => n.Id == saved.Id), "Real DB deletion survives another client query");
    }

    private static async Task WaitFor(Func<bool> ready)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!ready()) await Task.Delay(10, timeout.Token);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
    private sealed class ControlledService : INoticeService
    {
        public List<Notice> Items { get; } = new();
        public int Creates;
        public int Deletes;
        public ApiException? DeleteError;
        public TaskCompletionSource<Notice> CreateCompletion = new();
        public TaskCompletionSource<IReadOnlyList<Notice>>? ListCompletion;
        public ApiException? ListError;
        public Task<IReadOnlyList<Notice>> GetNoticesAsync(CancellationToken ct = default) => ListError is not null
            ? Task.FromException<IReadOnlyList<Notice>>(ListError) : ListCompletion?.Task ?? Task.FromResult<IReadOnlyList<Notice>>(Items.ToArray());
        public Task<Notice> GetNoticeAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.Single(n => n.Id == id));
        public Task<Notice> CreateNoticeAsync(CreateNoticeRequest request, CancellationToken ct = default) { Creates++; return CreateCompletion.Task; }
        public Task DeleteNoticeAsync(int id, CancellationToken ct = default) { Deletes++; if (DeleteError is not null) return Task.FromException(DeleteError); Items.RemoveAll(n => n.Id == id); return Task.CompletedTask; }
    }
}
