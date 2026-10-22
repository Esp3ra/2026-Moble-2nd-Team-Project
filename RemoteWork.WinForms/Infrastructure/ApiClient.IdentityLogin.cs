using System.Globalization;
using System.Text.Json;

namespace RemoteWork.WinForms.Infrastructure;

public sealed partial class ApiClient
{
    /// <summary>Reserve one empty login generation, never a normal authenticated session.</summary>
    public IdentityLogin BeginIdentityLogin(HttpMessageHandler? pendingHandler = null, TimeProvider? clock = null,
        CancellationToken lifetime = default)
    {
        RequestContext context;
        CancellationTokenSource previous;
        lock (identityLock)
        {
            if (token is not null || currentUser is not null) throw new InvalidOperationException("로그아웃 후 지문 로그인을 시작해 주세요.");
            previous = ReplaceIdentityLocked(null, null);
            context = CaptureLocked();
        }
        previous.Cancel();
        return new(this, context, pendingHandler, clock ?? TimeProvider.System, lifetime);
    }

    /// <summary>Owner of pending/finalize recovery. No token setter is exposed to UI.
    /// Captures the server and generation; importing a late response cannot replace another login.</summary>
    public sealed class IdentityLogin : IDisposable
    {
        private readonly ApiClient owner;
        private readonly RequestContext context;
        private readonly HttpMessageHandler? handler;
        private readonly TimeProvider clock;
        private readonly CancellationTokenSource lifetime;
        private readonly CancellationToken cancellation;
        private readonly SemaphoreSlim gate = new(1);
        private IdentityPendingConnection? pending;
        private RequestContext? acceptedContext;
        private string? loginId;
        private Guid? challengeId;
        private Receipt? receipt;
        private LoginResult? completed;
        private int disposed;
        private sealed record Receipt(Guid ReceiptId, Guid WorkId, string Day, string Token, DateTimeOffset Expiry);
        public Guid OriginalLoginId { get; } = Guid.NewGuid();
        public Guid OriginalFinalizeId { get; } = Guid.NewGuid();
        public Guid OriginalRevokeId { get; } = Guid.NewGuid();
        public Uri ServerUri => context.ServerUri;
        public CancellationToken Cancellation => cancellation;
        public bool HasFinalizeAttempt => challengeId is not null;
        public bool IsCurrent
        {
            get
            {
                if (Volatile.Read(ref disposed) != 0) return false;
                if (acceptedContext is null && cancellation.IsCancellationRequested) return false;
                lock (owner.identityLock) return owner.IsCurrentLocked(acceptedContext ?? context);
            }
        }
        internal IdentityLogin(ApiClient owner, RequestContext context, HttpMessageHandler? handler, TimeProvider clock, CancellationToken viewLifetime)
        {
            this.owner = owner; this.context = context; this.handler = handler; this.clock = clock;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.SessionCancellation, viewLifetime);
            cancellation = lifetime.Token;
        }
        private void Current(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); cancellation.ThrowIfCancellationRequested();
            ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);
            lock (owner.identityLock) owner.EnsureCurrentLocked(context);
        }
        public async Task<IdentityPendingConnection> OpenAsync(string loginId, string password, CancellationToken ct = default)
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                Current(ct);
                if (pending is not null) throw new InvalidOperationException("원래 지문 대기 접속을 사용해 주세요.");
                this.loginId = loginId.Trim().ToLowerInvariant();
                var opened = await IdentityPendingConnection.LoginAsync(context.ServerUri.ToString(), this.loginId,
                    password, OriginalLoginId, () => IsCurrent && acceptedContext is null, cancellation, ct,
                    handler, clock).ConfigureAwait(false);
                try
                {
                    lock (owner.identityLock) { Current(ct); pending = opened; }
                    return opened;
                }
                catch { opened.Dispose(); throw; }
            }
            finally { gate.Release(); }
        }
        public async Task<LoginResult> CompleteAsync(Guid verifiedChallengeId, CancellationToken ct = default)
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (completed is not null)
                {
                    if (verifiedChallengeId != challengeId) throw Invalid();
                    ct.ThrowIfCancellationRequested();
                    if (!IsCurrent) throw new OperationCanceledException("로그인 접속이 변경되었습니다.");
                    return completed;
                }
                Current(ct);
                var connection = pending;
                if (connection is null || verifiedChallengeId == Guid.Empty) throw Invalid();
                if (challengeId is not null && challengeId != verifiedChallengeId) throw Invalid();
                // Preserve the target and UUID even if finalize commits and its response is lost.
                challengeId = verifiedChallengeId;
                var answer = await connection.FinalizeAsync(verifiedChallengeId, OriginalFinalizeId, ct).ConfigureAwait(false);
                Current(ct);
                var returned = Parse(answer);
                if (receipt is not null && receipt != returned) throw Invalid();
                receipt = returned; // Keep the identical returned token for auth/me recovery; RAM only.
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellation);
                var provisional = new RequestContext(context.Generation, returned.Token, context.ServerUri, context.SessionCancellation);
                // Detached only from token installation: original generation/cancellation checked on both sides.
                var identity = await owner.SendAsync<IdentityResult>(HttpMethod.Get, "auth/me", null,
                    linked.Token, provisional, detached: true).ConfigureAwait(false);
                Current(ct);
                if (identity.User is not { Id: > 0, IsActive: true } user || user.Role is not ("employee" or "admin")
                    || !string.Equals(user.LoginId, loginId, StringComparison.OrdinalIgnoreCase)
                    || identity.ExpiresAt != returned.Expiry || returned.Expiry <= clock.GetUtcNow()) throw Invalid();
                CancellationTokenSource previous;
                lock (owner.identityLock)
                {
                    Current(ct);
                    previous = owner.ReplaceIdentityLocked(returned.Token, user);
                    acceptedContext = owner.CaptureLocked();
                    completed = new(returned.Token, "Bearer", returned.Expiry, user);
                    owner.acceptedLogins.Add(completed, acceptedContext);
                }
                previous.Cancel();
                return completed;
            }
            finally { gate.Release(); }
        }
        public async Task RevokeAsync(CancellationToken ct = default)
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                Current(ct);
                if (pending is not null) await pending.RevokeAsync(OriginalRevokeId, ct).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }
        private Receipt Parse(JsonElement answer)
        {
            try
            {
                if (answer.GetProperty("state").GetString() != "completed" || answer.GetProperty("token_type").GetString() != "Bearer") throw Invalid();
                string token = answer.GetProperty("access_token").GetString() ?? "";
                if (token.Length is < 22 or > 200 || token.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_')) throw Invalid();
                string day = answer.GetProperty("work_date").GetString() ?? "";
                if (!DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw Invalid();
                var expiry = answer.GetProperty("expires_at").GetDateTimeOffset();
                if (expiry.Offset != TimeSpan.Zero || expiry <= clock.GetUtcNow()) throw Invalid();
                return new(Id(answer, "workflow_receipt_id"), Id(answer, "work_session_ref"), day, token, expiry);
            }
            catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { throw Invalid(); }
        }
        private static Guid Id(JsonElement v, string name)
        {
            string? text = v.GetProperty(name).GetString();
            if (!Guid.TryParseExact(text, "D", out var id) || id == Guid.Empty || id.ToString("D") != text) throw Invalid();
            return id;
        }
        private static InvalidDataException Invalid() => new("출근 저장과 로그인 결과를 확인할 수 없습니다. 같은 작업 결과를 다시 확인해 주세요.");
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            lifetime.Cancel();
            IdentityPendingConnection? ending;
            lock (owner.identityLock) { ending = pending; pending = null; }
            ending?.Dispose(); receipt = null;
            // Do not clear an accepted identity or another login. No network in Dispose.
        }
    }
}
