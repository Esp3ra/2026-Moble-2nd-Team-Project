using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

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
    private static Notice Notice(int id = 1) => new() { Id = id, AuthorId = 1, Author = "���� ������",
        Title = "DB ����", Content = "DB ����", Target = "��������", CreatedAt = DateTime.Now };
    private static object Wire(int id = 1) => new { id, author_id = 1, author = "���� ������",
        title = "DB ����", content = "DB ����", target = "��������", is_pinned = true,
        created_at = "2026-10-17T00:00:00Z", attachment_names = new[] { "�ȳ�.pdf" } };
    private static HttpResponseMessage Json(object data) => new(HttpStatusCode.OK) {
        Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data }, ApiClient.Json), Encoding.UTF8, "application/json") };
    private static HttpResponseMessage Login() => Json(new LoginResult("test-token", "Bearer", DateTimeOffset.UtcNow.AddHours(1),
        new UserInfo(1, "test", "�׽�Ʈ", "T1", "������", "admin", true)));

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
                await HttpContract();
                await SaveLifecycle(host);
                await ScreenLifecycle(host);
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
                Check(json.RootElement.GetProperty("attachment_names")[0].GetString() == "�ȳ�.pdf", "Filename metadata serialized");
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
        var saved = await service.CreateNoticeAsync(new("����", "����", "��������", true, new[] { "�ȳ�.pdf" }));
        Check(saved.Id == 23 && saved.Author == "���� ������" && saved.AttachmentPaths.Length == 0, "Committed server identity replaces local draft");
        Check(saved.AttachmentNames.Single() == "�ȳ�.pdf", "Filename metadata mapped");
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
        title.Text = "�Է� ����"; body.Text = "�ۼ� ���� ����";
        var pending = editor.PublishAsync();
        Check(editor.CreatedNotice is null && editor.DialogResult != DialogResult.OK, "No success before commit response");
        Check(!Field<Button>(editor, "btnPublishNotice").Enabled, "Duplicate click disabled while saving");
        await editor.PublishAsync();
        Check(service.Creates == 1, "Repeated publish while pending does not post twice");
        editor.Close();
        Check(!editor.IsDisposed, "Closing does not pretend pending write was canceled");
        service.CreateCompletion.SetException(new ApiException("synthetic database failure", 503));
        await pending;
        Check(title.Text == "�Է� ����" && body.Text == "�ۼ� ���� ����", "Save failure preserves inputs");
        Check(editor.CreatedNotice is null && editor.DialogResult != DialogResult.OK && !editor.IsDisposed, "Save failure keeps editor open");
        Check(Field<TextBox>(editor, "saveError").Text.Contains("synthetic database failure"), "Error is visible inline");
        Check(Field<Button>(editor, "btnPublishNotice").Enabled, "Can retry after failure");
        service.CreateCompletion = new();
        var retry = editor.PublishAsync();
        service.CreateCompletion.SetResult(Notice(42));
        await retry;
        Check(editor.CreatedNotice?.Id == 42 && editor.DialogResult == DialogResult.OK, "Only successful commit closes editor");
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
        Check(Field<Label>(page, "lblText4").Text == "�������� ��", "Row selection alone leaves detail closed");
        Field<Button>(page, "btnQuery").PerformClick();
        Check(Field<TextBox>(page, "txtContent").Text.Contains("DB ����"), "Query opens current server detail");
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

    private static async Task RealHttp(string url, Form host)
    {
        // Optional fixture server: exclusively uses synthetic accounts in a disposable DB.
        using var api = new ApiClient(url);
        await api.LoginAsync("admin1", "Admin-test-2026!");
        var service = new HttpNoticeService(api);
        using var editor = new NoticeEditForm(service);
        editor.Show(host);
        Field<TextBox>(editor, "txtNoticeTitle").Text = "���� HTTP ����";
        Field<TextBox>(editor, "txtNoticeBody").Text = "���� DB ���� ����";
        Field<ComboBox>(editor, "cmbNoticeAudience").SelectedItem = "��ȹ��";
        Field<CheckBox>(editor, "chkPinToTop").Checked = true;
        await editor.PublishAsync();
        var saved = editor.CreatedNotice ?? throw new Exception("Real editor did not save");
        Check(editor.DialogResult == DialogResult.OK, "Real editor returns OK only after HTTP commit");
        Check(saved.Id > 0 && saved.Author == "������", "Real HTTP POST returns stored author/ID");
        using var otherApi = new ApiClient(url);
        await otherApi.LoginAsync("staff1", "Staff-test-2026!");
        var employee = new HttpNoticeService(otherApi);
        Check((await employee.GetNoticesAsync()).Any(n => n.Id == saved.Id && n.Target == "��ȹ��"), "Employee sees another department via real server");
        using var page = new NoticesControl(PreviewRole.Employee, employee);
        host.Controls.Add(page); page.CreateControl();
        await WaitFor(() => Field<Button>(page, "btnQuery").Enabled);
        await page.ReloadNoticesAsync();
        Check(Field<DataGridView>(page, "gridNotices").Rows.Count > 0, "Real HTTP/database populates WinForms list");
        Field<Button>(page, "btnQuery").PerformClick();
        await WaitFor(() => Field<Button>(page, "btnQuery").Enabled);
        Check(Field<TextBox>(page, "txtContent").Text.Contains("���� DB ���� ����"), "Real query button opens DB detail");
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
        public TaskCompletionSource<Notice> CreateCompletion = new();
        public TaskCompletionSource<IReadOnlyList<Notice>>? ListCompletion;
        public ApiException? ListError;
        public Task<IReadOnlyList<Notice>> GetNoticesAsync(CancellationToken ct = default) => ListError is not null
            ? Task.FromException<IReadOnlyList<Notice>>(ListError) : ListCompletion?.Task ?? Task.FromResult<IReadOnlyList<Notice>>(Items.ToArray());
        public Task<Notice> GetNoticeAsync(int id, CancellationToken ct = default) => Task.FromResult(Items.Single(n => n.Id == id));
        public Task<Notice> CreateNoticeAsync(CreateNoticeRequest request, CancellationToken ct = default) { Creates++; return CreateCompletion.Task; }
        public Task DeleteNoticeAsync(int id, CancellationToken ct = default) { Items.RemoveAll(n => n.Id == id); return Task.CompletedTask; }
    }
}
