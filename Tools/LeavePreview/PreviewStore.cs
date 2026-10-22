using System.Net;
using System.Text;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.LeavePreview;

/// <summary>Only this separate preview executable references these synthetic
/// accounts/rows. Memory only; no network transport, file, SQL or balance calculation.</summary>
internal sealed class PreviewStore
{
    internal static readonly UserInfo Employee = new(5, "preview-employee", "체험 직원", "DEMO-5", "체험 부서", "employee", true);
    internal static readonly UserInfo Reviewer = new(1, "preview-reviewer", "체험 담당자", "DEMO-1", "체험 부서", "admin", true);
    private readonly Dictionary<string, LeaveRequestView> rows = new();
    private readonly Dictionary<Guid, LeaveSubmissionReceipt> submissions = new();
    private readonly Dictionary<Guid, (string Id, string Revision, string Decision, string Reason, LeaveDecisionReceipt Receipt)> decisions = new();
    public PreviewStore()
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        rows["example-pending"] = new("example-pending", "1", 5, Employee.DisplayName, Employee.Department,
            new("연차", today.AddDays(3), today.AddDays(3), "화면 체험용 신청입니다."), "pending");
        rows["example-sick"] = new("example-sick", "1", 5, Employee.DisplayName, Employee.Department,
            new("병가", today.AddDays(-3), today.AddDays(-3), "병가 차감 제외 안내 확인용 예시입니다."), "approved");
        rows["example-rejected"] = new("example-rejected", "1", 5, Employee.DisplayName, Employee.Department,
            new("연차", today.AddDays(1), today.AddDays(1), "반려 사유 확인용 예시 신청입니다."), "rejected",
            DecisionReason: "해당 날짜에 부서 일정이 있습니다.\r\n다른 날짜로 다시 신청해 주세요.");
        rows["not-assigned"] = new("not-assigned", "1", 99, "다른 예시 직원", "다른 체험 부서",
            new("공가", today, today, "지정되지 않은 직원은 목록과 검토에서 제외됩니다."), "pending");
    }
    public ILeaveWorkflow For(ApiClient.Session session) => new Workflow(this, session);

    internal static ApiClient CreateIdentity(bool reviewer)
        => new("http://leave-preview.invalid", new SyntheticIdentity(reviewer ? Reviewer : Employee));

    private sealed class SyntheticIdentity(UserInfo user) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (request.Method != HttpMethod.Post || request.RequestUri!.Host != "leave-preview.invalid"
                || request.RequestUri.AbsolutePath != "/api/v1/auth/login")
                throw new InvalidOperationException("체험 도구에서는 실제 서버 요청을 사용할 수 없습니다.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(JsonSerializer.Serialize(new { ok = true, data =
                new LoginResult("preview-only-" + user.Id, "Bearer", DateTimeOffset.UtcNow.AddHours(4), user) }, ApiClient.Json), Encoding.UTF8, "application/json") });
        }
    }
    private sealed class Workflow(PreviewStore store, ApiClient.Session session) : ILeaveWorkflow
    {
        public bool IsConnected => true; // Connected to this RAM fixture, never a server.
        private bool CanReview(LeaveRequestView r) => session.User.Id == Reviewer.Id && session.User.Role == "admin" && r.ApplicantId == Employee.Id;
        private void Current(CancellationToken ct) { ct.ThrowIfCancellationRequested(); session.ThrowIfChanged(); }
        public Task<IReadOnlyList<LeaveRequestView>> ListAsync(CancellationToken ct)
        {
            Current(ct);
            return Task.FromResult<IReadOnlyList<LeaveRequestView>>(store.rows.Values
                .Where(r => session.User.Role == "admin" ? CanReview(r) : r.ApplicantId == session.User.Id)
                .Select(r => r with { CanReview = CanReview(r) }).ToArray());
        }
        public Task<LeaveSubmissionReceipt> SubmitAsync(LeaveDraft draft, Guid requestId, CancellationToken ct)
        {
            Current(ct);
            if (session.User.Id != Employee.Id || session.User.Role != "employee" || requestId == Guid.Empty)
                throw new InvalidOperationException("체험 직원의 신청만 입력할 수 있습니다.");
            if (store.submissions.TryGetValue(requestId, out var old))
            {
                if (old.Request.Draft != draft) throw new InvalidOperationException("같은 요청의 입력이 달라졌습니다.");
                return Task.FromResult(old);
            }
            var row = new LeaveRequestView(Guid.NewGuid().ToString("D"), "1", Employee.Id, Employee.DisplayName,
                Employee.Department, draft, "pending");
            store.rows.Add(row.Id, row);
            var receipt = new LeaveSubmissionReceipt(requestId, row);
            store.submissions.Add(requestId, receipt);
            return Task.FromResult(receipt);
        }
        public Task<LeaveDecisionReceipt> DecideAsync(LeaveRequestView request, string decision, string reason, Guid requestId, CancellationToken ct)
        {
            Current(ct);
            if (requestId == Guid.Empty || decision is not ("approved" or "rejected")
                || !store.rows.TryGetValue(request.Id, out var current) || !CanReview(current))
                throw new InvalidOperationException("지정된 직원의 체험 신청만 검토할 수 있습니다.");
            if (store.decisions.TryGetValue(requestId, out var old))
            {
                if ((old.Id, old.Revision, old.Decision, old.Reason) != (request.Id, request.Revision, decision, reason))
                    throw new InvalidOperationException("같은 검토의 내용이 달라졌습니다.");
                return Task.FromResult(old.Receipt);
            }
            if (current.Status != "pending" || current.Revision != request.Revision || current.Draft != request.Draft)
                throw new InvalidOperationException("목록을 다시 확인해 주세요.");
            var changed = current with { Status = decision, Revision = "2", CanReview = true, DecisionReason = reason };
            store.rows[current.Id] = changed with { CanReview = false };
            var receipt = new LeaveDecisionReceipt(requestId, changed);
            store.decisions.Add(requestId, (request.Id, request.Revision, decision, reason, receipt));
            return Task.FromResult(receipt);
        }
    }
}
