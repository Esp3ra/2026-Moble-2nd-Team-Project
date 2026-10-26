using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;

namespace RemoteWork.WinForms.Infrastructure;

/// <summary>Member1 common HTTP bridge for member5's existing public seam.
/// Inject ONE instance per captured login into existing forms. No startup/UI
/// replacement, token access, local DB, automatic POST retry or policy seed.
/// A later explicit invocation may resolve the original intent, then resend
/// the SAME key/body only after both intent and W1 say not_recorded.
/// </summary>
public sealed class HttpLeaveWorkflow(ApiClient.Session session) : ILeaveWorkflow
{
    private readonly SemaphoreSlim lane = new(1, 1);
    private readonly Dictionary<Guid, Operation> operations = [];
    public bool IsConnected => session.IsCurrent;
    private sealed class Operation(string action, string target, object body, LeaveRequestView? original, LeaveDraft? draft)
    {
        public readonly string Action = action, Target = target;
        public readonly object Body = body;
        public readonly string Fingerprint = JsonSerializer.Serialize(body, ApiClient.Json);
        public readonly LeaveRequestView? Original = original;
        public readonly LeaveDraft? Draft = draft;
        public readonly long Created = Stopwatch.GetTimestamp();
        public string? Key;
        public bool Sent;
        public Receipt? Confirmed;
    }
    public sealed record Receipt(string RequestId, string Revision, string Status, int ChargedUnits, DateTimeOffset UpdatedAt);
    public sealed record Balance(int Year, int GrantedUnits, int ReservedUnits, int UsedUnits, int RemainingUnits, string UnitDays, string PolicyId);
    public sealed record Preparation(bool SchemaReady, bool WriteReady, string[] Pending, int Year, DateTimeOffset ServerTime, string PolicyId);
    private sealed record KeyReply(string RequestKey, DateTimeOffset ExpiresAt);
    private sealed record Lookup(string State, int? HttpStatus, Receipt? Result, DateTimeOffset? ExpiresAt);
    private sealed record Page(Item[] Items, string? NextCursor);
    private sealed record Item(string RequestId, string Revision, int ApplicantId, string ApplicantName, string Department,
        string Type, DateOnly StartDate, DateOnly EndDate, string Reason, string Status, int ChargedUnits,
        string UnitDays, bool CanReview, string DecisionReason);

    public async Task<Preparation> ReadinessAsync(CancellationToken ct = default)
    {
        var value = await session.GetAsync<Preparation>("leave/readiness", ct).ConfigureAwait(false);
        session.ThrowIfChanged();
        if (!value.SchemaReady || value.Pending is null || value.PolicyId != "calendar-year-reserve-v1") throw Invalid();
        return value;
    }
    public async Task<Balance> BalanceAsync(int year, CancellationToken ct = default)
    {
        if (year is < 2000 or > 9998) throw new ArgumentOutOfRangeException(nameof(year));
        var value = await session.GetAsync<Balance>("leave/balance?year="+year.ToString(CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
        session.ThrowIfChanged();
        if (value.Year != year || value.UnitDays != "0.5" || value.PolicyId != "calendar-year-reserve-v1"
            || value.GrantedUnits is < 0 or > 732 || value.ReservedUnits < 0 || value.UsedUnits < 0 || value.RemainingUnits < 0
            || value.RemainingUnits != value.GrantedUnits-value.ReservedUnits-value.UsedUnits) throw Invalid();
        return value;
    }
    public async Task<LeaveSubmissionReceipt> SubmitAsync(LeaveDraft draft, Guid requestId, CancellationToken cancellation)
    {
        var body = new { intent_id = requestId.ToString("D"), type = WireType(draft.Type),
            start_date = draft.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            end_date = draft.EndDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), reason = draft.Reason };
        var r = await ExecuteAsync(requestId, "leave.submitted", "new", body, null, draft, cancellation).ConfigureAwait(false);
        var view = new LeaveRequestView(r.RequestId, r.Revision, session.User.Id, session.User.DisplayName,
            session.User.Department, draft, r.Status, false, "");
        return new(requestId, view);
    }
    public async Task<LeaveDecisionReceipt> DecideAsync(LeaveRequestView request, string decision, string reason, Guid requestId, CancellationToken cancellation)
    {
        if (decision is not ("approved" or "rejected")) throw new ArgumentException("승인 또는 반려가 필요합니다.");
        Canonical(request.Id);Revision(request.Revision);
        var body = new { intent_id = requestId.ToString("D"), expected_revision = request.Revision, decision, decision_reason = reason };
        var r = await ExecuteAsync(requestId, "leave.decided", request.Id, body, request, null, cancellation).ConfigureAwait(false);
        return new(requestId, request with { Revision = r.Revision, Status = r.Status, CanReview = false, DecisionReason = reason });
    }
    private async Task<Receipt> ExecuteAsync(Guid id, string action, string target, object body,
        LeaveRequestView? original, LeaveDraft? draft, CancellationToken ct)
    {
        if (id == Guid.Empty) throw new ArgumentException("원 요청 UUID가 필요합니다.");
        session.ThrowIfChanged();
        await lane.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            session.ThrowIfChanged();ct.ThrowIfCancellationRequested();
            if (!operations.TryGetValue(id, out var op))
            {
                if (operations.Count >= 64) throw new InvalidOperationException("현재 접속의 연차 작업 한도입니다. 기존 결과부터 확인해 주세요.");
                operations[id] = op = new(action, target, body, original, draft);
            }
            if (op.Action != action || op.Target != target || op.Fingerprint != JsonSerializer.Serialize(body, ApiClient.Json))
                throw new InvalidOperationException("원 요청 UUID·본문을 유지해 주세요.");
            if (Stopwatch.GetElapsedTime(op.Created) >= TimeSpan.FromHours(24)) throw new ApiException("원 요청 결과 보관기간이 지났습니다.", 410, "LEAVE_INTENT_EXPIRED");
            if (op.Confirmed is not null) return op.Confirmed;
            if (op.Sent)
            {
                var query = "leave/operations/"+id.ToString("D")+"?action="+action+"&target="+target;
                var found = await session.GetAsync<Lookup>(query, ct).ConfigureAwait(false);
                session.ThrowIfChanged();ct.ThrowIfCancellationRequested();
                if (found.State == "completed") return op.Confirmed = ValidateLookup(found, op);
                if (found.State != "not_recorded" || op.Key is null) throw Invalid();
                var w1 = await session.PostIdempotentAsync<Lookup>("write-requests/result", new { action, target }, op.Key, ct).ConfigureAwait(false);
                session.ThrowIfChanged();ct.ThrowIfCancellationRequested();
                if (w1.State == "completed") return op.Confirmed = ValidateLookup(w1, op);
                if (w1.State != "not_recorded") throw Invalid();
                // This invocation is an explicit retry by the caller. Never
                // reached automatically after a POST fails in the same call.
            }
            if (op.Key is null)
            {
                var issued = await session.PostAsync<KeyReply>("write-requests/keys", new { action, target }, ct).ConfigureAwait(false);
                session.ThrowIfChanged();ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(issued.RequestKey) || issued.ExpiresAt == default) throw Invalid();
                op.Key = issued.RequestKey;
            }
            session.ThrowIfChanged();ct.ThrowIfCancellationRequested();
            op.Sent = true; // even cancellation/transport loss now means UNCERTAIN
            var path = action == "leave.submitted" ? "leave/requests" : "leave/requests/"+target+"/decision";
            var result = await session.PostIdempotentAsync<Receipt>(path, op.Body, op.Key, ct).ConfigureAwait(false);
            session.ThrowIfChanged();ct.ThrowIfCancellationRequested();
            return op.Confirmed = Validate(result, op);
        }
        finally { lane.Release(); }
    }
    private static Receipt ValidateLookup(Lookup value, Operation op)
    {
        if (value.HttpStatus != (op.Action == "leave.submitted" ? 201 : 200)
            || value.ExpiresAt is null || value.ExpiresAt == default(DateTimeOffset)
            || value.ExpiresAt.Value.Offset != TimeSpan.Zero) throw Invalid();
        return Validate(value.Result, op);
    }
    private static Receipt Validate(Receipt? value, Operation op)
    {
        if (value is null || value.UpdatedAt == default || value.UpdatedAt.Offset != TimeSpan.Zero || value.ChargedUnits is < 0 or > 732) throw Invalid();
        Canonical(value.RequestId);var revision = Revision(value.Revision);
        if (op.Action == "leave.submitted")
        {
            if (value.Status != "pending" || revision != 1) throw Invalid();
        }
        else
        {
            var wanted = JsonSerializer.SerializeToElement(op.Body).GetProperty("decision").GetString();
            if (value.RequestId != op.Target || value.Status != wanted || op.Original is null
                || Revision(op.Original.Revision) == long.MaxValue || revision != Revision(op.Original.Revision)+1) throw Invalid();
        }
        return value;
    }
    public async Task<IReadOnlyList<LeaveRequestView>> ListAsync(CancellationToken ct)
    {
        session.ThrowIfChanged();var result = new Dictionary<string, LeaveRequestView>();
        foreach (var scope in session.User.Role == "admin" ? new[] { "own", "review" } : new[] { "own" })
        {
            string? cursor = null;var seen = new HashSet<string>();var done = false;
            for (var page = 0; page < 20; page++)
            {
                var path = "leave/requests?scope="+scope+"&limit=100"+(cursor is null ? "" : "&after="+cursor);
                var values = await session.GetAsync<Page>(path, ct).ConfigureAwait(false);
                session.ThrowIfChanged();ct.ThrowIfCancellationRequested();
                if (values.Items is null || values.Items.Length > 100) throw Invalid();
                foreach (var item in values.Items)
                {
                    Canonical(item.RequestId);Revision(item.Revision);
                    if (item.ApplicantId <= 0 || item.UnitDays != "0.5" || item.ChargedUnits is < 0 or > 732
                        || item.Status is not ("pending" or "approved" or "rejected") || item.EndDate < item.StartDate
                        || item.Reason is null || item.DecisionReason is null
                        || (scope == "own" && item.ApplicantId != session.User.Id)
                        || (scope == "review" && item.ApplicantId == session.User.Id)) throw Invalid();
                    result[item.RequestId] = new(item.RequestId, item.Revision, item.ApplicantId, item.ApplicantName, item.Department,
                        new(UiType(item.Type), item.StartDate, item.EndDate, item.Reason), item.Status, item.CanReview, item.DecisionReason,
                        CanReadAssigned: scope == "review");
                }
                if (values.NextCursor is null) { done = true;break; }
                Canonical(values.NextCursor);
                if (!seen.Add(values.NextCursor)) throw Invalid();
                cursor = values.NextCursor;
            }
            if (!done) throw new InvalidOperationException("연차 조회 범위가 큽니다. 페이지 조회 연결을 확인해 주세요.");
        }
        session.ThrowIfChanged();return result.Values.ToArray();
    }
    private static string WireType(string value) => value switch
    { "연차" => "annual", "반차(오전)" => "am_half", "반차(오후)" => "pm_half", "병가" => "sick", "공가" => "official", _ => throw Invalid() };
    private static string UiType(string value) => value switch
    { "annual" => "연차", "am_half" => "반차(오전)", "pm_half" => "반차(오후)", "sick" => "병가", "official" => "공가", _ => throw Invalid() };
    private static void Canonical(string value)
    { if (!Guid.TryParseExact(value, "D", out var id) || id.ToString("D") != value) throw Invalid(); }
    private static long Revision(string value)
    { if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n <= 0 || n.ToString(CultureInfo.InvariantCulture) != value) throw Invalid();return n; }
    private static InvalidDataException Invalid() => new("연차 서버의 원 요청·응답 형식을 확인해 주세요.");
}
