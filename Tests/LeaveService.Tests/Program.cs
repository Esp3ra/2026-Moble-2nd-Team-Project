using System.Net;
using RemoteWork.WinForms.Infrastructure;
using RemoteWork.WinForms.Features.Attendance;

internal static class Program
{
    private static int count;
    private static void Check(bool value) { if (!value) throw new InvalidDataException("Leave integration assertion failed "+count);count++; }
    private sealed class LoseOnce : DelegatingHandler
    {
        public int SubmitPosts;
        public LoseOnce() : base(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false }) { }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var drop = request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/v1/leave/requests";
            if (drop) SubmitPosts++;
            var response = await base.SendAsync(request, ct);
            if (drop && SubmitPosts == 1 && response.StatusCode == HttpStatusCode.Created)
            { await response.Content.ReadAsByteArrayAsync(ct);response.Dispose();throw new HttpRequestException("synthetic lost response after commit"); }
            return response;
        }
    }
    private static async Task Main()
    {
        var url = Environment.GetEnvironmentVariable("LEAVE_TEST_URL") ?? throw new InvalidOperationException();
        using var handler = new LoseOnce();
        using var worker = new ApiClient(url, handler);using var boss = new ApiClient(url);
        await worker.LoginAsync("worker", "Synthetic-pass-2026!");await boss.LoginAsync("boss", "Synthetic-pass-2026!");
        var session = worker.CaptureSession();var flow = new HttpLeaveWorkflow(session);var review = new HttpLeaveWorkflow(boss.CaptureSession());
        Check((await flow.ReadinessAsync()).WriteReady);Check((await flow.BalanceAsync(2026)).RemainingUnits == 16);
        var draft = new LeaveDraft("반차(오전)", new(2026,10,22), new(2026,10,22), "한글 사유");var id = Guid.NewGuid();
        try { await flow.SubmitAsync(draft,id,default);throw new InvalidOperationException("Expected loss"); }
        catch (ApiException) { Check(handler.SubmitPosts == 1); }
        var receipt = await flow.SubmitAsync(draft,id,default);Check(receipt.RequestId == id && receipt.Request.Status == "pending" && receipt.Request.Draft == draft);
        Check(handler.SubmitPosts == 1);Check((await flow.BalanceAsync(2026)).ReservedUnits == 1);
        var same = await flow.SubmitAsync(draft,id,default);Check(same == receipt && handler.SubmitPosts == 1);
        try { await flow.SubmitAsync(draft with { Reason = "changed" },id,default);throw new InvalidDataException("Changed intent accepted"); }
        catch (InvalidOperationException) { Check(handler.SubmitPosts == 1); }
        var own = await flow.ListAsync(default);Check(own.Count == 1 && own[0].ApplicantId == session.User.Id && !own[0].CanReview);
        var pending = (await review.ListAsync(default)).Single();Check(pending.CanReview && pending.Draft.Type == "반차(오전)");
        var decisionId=Guid.NewGuid();var result=await review.DecideAsync(pending,"rejected","반려 의견",decisionId,default);
        Check(result.Request.Status == "rejected" && result.Request.DecisionReason == "반려 의견" && result.Request.Revision == "2");
        var after=(await flow.ListAsync(default)).Single();Check(after.Status == "rejected" && after.DecisionReason == "반려 의견");
        Check((await flow.BalanceAsync(2026)).RemainingUnits == 16);
        await worker.LoginAsync("other","Synthetic-pass-2026!");
        try { await flow.ListAsync(default);throw new InvalidDataException("Old session accepted"); }
        catch (OperationCanceledException) { Check(!flow.IsConnected); }
        Console.WriteLine($"PASS {count} actual C#/Flask/MySQL leave bridge checks; lost-response POST count1");
    }
}
