using System.Net;
using System.Text.Json;
using RemoteWork.WinForms.Features.Collaboration;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Ui;

internal static partial class Program
{
    private static async Task OperationsEndToEnd(string url, Form host)
    {
        using var api = new ApiClient(url);
        await api.LoginAsync("boss", "Synthetic-notice-2026!");
        var service = new HttpNoticeService(api);
        var draft = new CreateNoticeRequest("HTTP 원 공지", "실제 저장", "전 직원", false, []);
        var receipt = await service.CreateOrRecoverNoticeAsync(draft);
        Check(receipt.NoticeId > 0 && receipt.OperationId == draft.OperationId, "Actual receipt matches original");
        var repeated = await new HttpNoticeService(api).CreateOrRecoverNoticeAsync(draft);
        Check(repeated == receipt, "New client/new W1 key preserves one original effect");
        await service.DeleteNoticeAsync(receipt.NoticeId);
        var deleted = await new HttpNoticeService(api).CreateOrRecoverNoticeAsync(draft);
        Check(deleted == receipt, "Deleted notice receipt remains completed without recreation");
        Check(!(await service.GetNoticesAsync()).Any(n => n.Id == receipt.NoticeId), "Deleted notice stays absent");
        try { await new HttpNoticeService(api).CreateOrRecoverNoticeAsync(draft with { Content = "different" }); throw new Exception("Body conflict accepted"); }
        catch (ApiException e) { Check(e.StatusCode == 409, "Real original body conflict rejected"); }
        using var form = new NoticeEditForm(service);
        form.Show(host);
        Field<TextBox>(form, "txtNoticeTitle").Text = "실제 화면 공지";
        Field<TextBox>(form, "txtNoticeBody").Text = "원 요청 화면";
        await form.PublishAsync();
        Check(form.CreatedReceipt is not null && form.DialogResult == DialogResult.OK, "Actual UI consumes minimal HTTP receipt");
        var saved = await service.GetNoticeAsync(form.CreatedReceipt!.NoticeId);
        Check(saved.Title == "실제 화면 공지", "Saved form is visible through real read API");
        await service.DeleteNoticeAsync(saved.Id);
    }

    private static async Task RecoverableCreation(Form host)
    {
        foreach (var outcome in new[] { "completed", "not_recorded", "failed_lookup", "wrong_id", "expired", "w1_completed" })
        {
            var id = Guid.NewGuid(); int keys = 0, posts = 0, lookups = 0, common = 0;
            string? originalBody = null;
            object Receipt(Guid operation) => new { notice_id = 501, operation_id = operation.ToString("D"), created_at = "2026-10-23T00:00:00Z" };
            object Completed(Guid operation) => new { state = "completed", http_status = 201, result = Receipt(operation), expires_at = DateTimeOffset.UtcNow.AddHours(1) };
            using var api = new ApiClient("http://test.invalid:5000", new Handler(async (r, ct) =>
            {
                var path = r.RequestUri!.AbsolutePath;
                if (path.EndsWith("auth/login")) return Login();
                if (path.EndsWith("write-requests/keys"))
                {
                    keys++;
                    var keyBody = JsonDocument.Parse(await r.Content!.ReadAsStringAsync(ct));
                    Check(keyBody.RootElement.GetProperty("target").GetString() == id.ToString("D"), "W1 target is original UUID");
                    Check(keyBody.RootElement.GetProperty("action").GetString() == "notice.created", "W1 uses registered action");
                    return Json(new { request_key = "original-key-012345678901234567890123456789", expires_at = DateTimeOffset.UtcNow.AddHours(1) });
                }
                if (path.Contains("notices/operations/"))
                {
                    lookups++; Check(path.EndsWith(id.ToString("D")), "Lookup retains original UUID");
                    if (outcome == "failed_lookup") throw new HttpRequestException("lookup unavailable");
                    if (outcome == "expired") return new HttpResponseMessage(HttpStatusCode.Gone)
                    { Content = new StringContent("{\"ok\":false,\"error\":{\"code\":\"NOTICE_OPERATION_EXPIRED\",\"message\":\"expired\"}}") };
                    return Json(outcome is "not_recorded" or "w1_completed" ? new { state = "not_recorded" } : Completed(outcome == "wrong_id" ? Guid.NewGuid() : id));
                }
                Check(r.Headers.GetValues("Idempotency-Key").Single() == "original-key-012345678901234567890123456789", "Same original W1 key retained");
                if (path.EndsWith("write-requests/result"))
                { common++; return Json(outcome == "w1_completed" ? Completed(id) : new { state = "not_recorded" }); }
                Check(path == "/api/v1/notices", "No legacy or detail fallback request");
                posts++;
                var body = await r.Content!.ReadAsStringAsync(ct);
                if (originalBody is null) originalBody = body;
                else Check(body == originalBody, "Explicit retry preserves exact original body");
                if (posts == 1) throw new HttpRequestException("response lost");
                return Json(Receipt(id));
            }));
            await api.LoginAsync("test", "synthetic");
            var service = new HttpNoticeService(api);
            var draft = new CreateNoticeRequest("원 공지", "본문", "전 직원", false, []) { OperationId = id, AttachmentIds = ["file-original"] };
            try { await service.CreateOrRecoverNoticeAsync(draft); throw new Exception("Lost response accepted"); }
            catch (ApiException) { checks++; }
            Check(keys == 1 && posts == 1 && lookups == 0, "No automatic retry after uncertain POST");
            try
            {
                var receipt = await service.CreateOrRecoverNoticeAsync(draft);
                Check(outcome is "completed" or "not_recorded" or "w1_completed", "Only authoritative completion accepted");
                Check(receipt.NoticeId == 501 && receipt.OperationId == id, "Minimal receipt proves success without detail fetch");
                await service.CreateOrRecoverNoticeAsync(draft);
            }
            catch (ApiException) { Check(outcome is "failed_lookup" or "wrong_id" or "expired", "Lookup failure prevents resend"); }
            Check(keys == 1 && posts == (outcome == "not_recorded" ? 2 : 1), "No replacement key or duplicate create");
            Check(lookups == 1 && common == (outcome is "not_recorded" or "w1_completed" ? 1 : 0), "Original lookup precedes W1 lookup");
            try { await service.CreateOrRecoverNoticeAsync(draft with { Title = "changed" }); throw new Exception("Changed original accepted"); }
            catch (InvalidOperationException) { checks++; }
            await api.LoginAsync("test", "synthetic");
            try { await service.CreateOrRecoverNoticeAsync(draft); throw new Exception("Old session accepted"); }
            catch (OperationCanceledException) { checks++; }
        }

        var fake = new RecoverableFake();
        using var form = new NoticeEditForm(fake);
        form.Show(host);
        Field<TextBox>(form, "txtNoticeTitle").Text = "복구 공지";
        Field<TextBox>(form, "txtNoticeBody").Text = "본문";
        await form.PublishAsync();
        Check(!form.IsDisposed && form.CreatedReceipt is null, "Uncertain form remains open");
        Check(Field<Button>(form, "btnPublishNotice").Enabled && Field<Button>(form, "btnPublishNotice").Text.Contains("결과 확인"), "Explicit recovery button stays usable");
        Check(!Field<Panel>(form, "panelContent").Enabled, "Inputs frozen during recovery");
        await form.PublishAsync();
        Check(fake.Calls == 2 && form.CreatedReceipt?.NoticeId == 501 && form.DialogResult == DialogResult.OK, "Recovered form closes with committed receipt");
        Check(fake.LegacyCalls == 0, "Recoverable form never calls legacy create");
        using var page = new NoticesControl(PreviewRole.Administrator, fake);
        host.Controls.Add(page);
        await page.ReloadAfterCreationAsync(form.CreatedReceipt!);
        Check(Field<TextBox>(page, "txtContent").Text.Contains("등록은 완료"), "List failure cannot turn known commit into unknown save");
        Check(Field<DataGridView>(page, "gridNotices").Rows.Count == 0, "Failed list does not fabricate or resurrect a notice");
        host.Controls.Remove(page);
    }

    private sealed class RecoverableFake : INoticeService, IRecoverableNoticeService
    {
        public int Calls, LegacyCalls;
        private CreateNoticeRequest? original;
        public Task<NoticeCreationReceipt> CreateOrRecoverNoticeAsync(CreateNoticeRequest request, CancellationToken ct = default)
        {
            Calls++;
            if (original is null) { original = request; throw new ApiException("응답 유실", 503); }
            Check(ReferenceEquals(request, original), "Form reuses frozen request and upload list");
            return Task.FromResult(new NoticeCreationReceipt(501, request.OperationId, DateTimeOffset.UtcNow));
        }
        public Task<Notice> CreateNoticeAsync(CreateNoticeRequest request, CancellationToken ct = default)
        { LegacyCalls++; throw new Exception("Legacy fallback forbidden"); }
        public Task<IReadOnlyList<Notice>> GetNoticesAsync(CancellationToken ct = default) => throw new ApiException("목록 장애", 503);
        public Task<Notice> GetNoticeAsync(int id, CancellationToken ct = default) => throw new Exception("No detail dependency");
        public Task DeleteNoticeAsync(int id, CancellationToken ct = default) => Task.CompletedTask;
    }
}
