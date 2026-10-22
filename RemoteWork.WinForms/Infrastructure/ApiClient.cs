using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace RemoteWork.WinForms.Infrastructure;

/// <summary>Shared by all feature screens. Tokens stay in memory and are never logged.</summary>
public sealed partial class ApiClient : IDisposable
{
    private readonly HttpClient http;
    private readonly object identityLock = new();
    private string? token;
    private UserInfo? currentUser;
    private Uri serverUri;
    private long generation;
    private CancellationTokenSource sessionCancellation = new();
    private RequestTraffic traffic = new();
    private readonly ConditionalWeakTable<LoginResult, RequestContext> acceptedLogins = new();
    internal sealed record RequestContext(long Generation, string? Token, Uri ServerUri,
        CancellationToken SessionCancellation);
    public Uri ServerUri { get { lock (identityLock) return serverUri; } }
    public UserInfo? CurrentUser { get { lock (identityLock) return currentUser; } }
    public event EventHandler? AuthenticationExpired;

    /// <summary>A feature's authenticated lifetime. Never exposes the bearer token.</summary>
    public sealed class Session
    {
        private readonly ApiClient owner;
        private readonly RequestContext context;
        internal Session(ApiClient owner, RequestContext context, UserInfo user)
        { this.owner = owner; this.context = context; User = user; }
        /// <summary>Display snapshot, not server authorization evidence.</summary>
        public UserInfo User { get; }
        public CancellationToken Cancellation => context.SessionCancellation;
        public bool IsCurrent { get { lock (owner.identityLock) return owner.IsCurrentLocked(context); } }
        public void ThrowIfChanged() { lock (owner.identityLock) owner.EnsureCurrentLocked(context); }
        public Task<T> GetAsync<T>(string path, CancellationToken ct = default)
            => owner.SendAsync<T>(HttpMethod.Get, path, null, ct, context);
        public Task<T> PostAsync<T>(string path, object body, CancellationToken ct = default)
            => owner.SendAsync<T>(HttpMethod.Post, path, body, ct, context);
        public Task<T> PatchAsync<T>(string path, object body, CancellationToken ct = default)
            => owner.SendAsync<T>(HttpMethod.Patch, path, body, ct, context);
        /// <summary>One explicit DELETE with this captured login; no body or automatic retry.</summary>
        public Task<T> DeleteAsync<T>(string path, CancellationToken ct = default)
            => owner.SendAsync<T>(HttpMethod.Delete, path, null, ct, context);
        public Task<T> PostIdempotentAsync<T>(string path, object body, string requestKey, CancellationToken ct = default)
            => owner.SendAsync<T>(HttpMethod.Post, path, body, ct, context, requestKey: ValidateRequestKey(requestKey));
        public Task<T> PatchIdempotentAsync<T>(string path, object body, string requestKey, CancellationToken ct = default)
            => owner.SendAsync<T>(HttpMethod.Patch, path, body, ct, context, requestKey: ValidateRequestKey(requestKey));
        /// <summary>After a verified clock-out challenge, drain ordinary traffic and
        /// retain only the original finalize request. Does not issue/scan a challenge.</summary>
        public Task<ClockOutFinalization> BeginClockOutFinalizationAsync(Guid workSessionId, DateOnly workDate,
            Guid challengeId, Guid originalFinalizeId, CancellationToken ct = default)
            => owner.BeginClockOutAsync(context, workSessionId, workDate, challengeId, originalFinalizeId, ct);
    }

    public Session CaptureSession()
    {
        lock (identityLock)
        {
            if (currentUser is null || token is null)
                throw new InvalidOperationException("로그인 후 기능을 연결해 주세요.");
            return new Session(this, CaptureLocked(), currentUser);
        }
    }
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public ApiClient(string serverUrl, HttpMessageHandler? handler = null)
    {
        serverUri = ValidateUrl(serverUrl);
        http = new HttpClient(handler ?? new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromSeconds(12) };
    }

    public static Uri ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https") || !string.IsNullOrEmpty(uri.UserInfo) ||
            uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("서버 주소는 http://서버주소:포트 형식으로 입력해 주세요.");
        return uri;
    }

    public void SetServer(string url)
    {
        var uri = ValidateUrl(url);
        CancellationTokenSource previous;
        lock (identityLock)
        {
            if (currentUser is not null) throw new InvalidOperationException("로그아웃 후 서버 주소를 변경해 주세요.");
            serverUri = uri;
            previous = ReplaceIdentityLocked(null, null);
        }
        previous.Cancel();
    }

    public async Task<LoginResult> LoginAsync(string loginId, string password, CancellationToken ct = default)
    {
        RequestContext context;
        CancellationTokenSource previous;
        lock (identityLock)
        {
            previous = ReplaceIdentityLocked(null, null);
            context = CaptureLocked();
        }
        previous.Cancel();
        var result = await SendAsync<LoginResult>(HttpMethod.Post, "auth/login",
            new { login_id = loginId, password }, ct, context);
        lock (identityLock)
        {
            EnsureCurrentLocked(context);
            ct.ThrowIfCancellationRequested();
            previous = ReplaceIdentityLocked(result.AccessToken, result.User);
            acceptedLogins.Add(result, CaptureLocked());
        }
        previous.Cancel();
        return result;
    }

    public async Task<IdentityResult> RefreshIdentityAsync(CancellationToken ct = default)
    {
        var context = Capture();
        var result = await SendAsync<IdentityResult>(HttpMethod.Get, "auth/me", null, ct, context);
        lock (identityLock)
        {
            EnsureCurrentLocked(context);
            currentUser = result.User;
        }
        return result;
    }

    public async Task LogoutAsync()
    {
        RequestContext context;
        CancellationTokenSource previous;
        lock (identityLock)
        {
            context = CaptureLocked();
            previous = ReplaceIdentityLocked(null, null);
        }
        previous.Cancel();
        // Revoke A using A's captured token. Its late completion must never clear B.
        if (context.Token is not null)
            await SendAsync<MessageResult>(HttpMethod.Post, "auth/logout", new { },
                CancellationToken.None, context, detached: true);
    }

    public void ClearIdentity()
    {
        CancellationTokenSource previous;
        lock (identityLock) previous = ReplaceIdentityLocked(null, null);
        previous.Cancel();
    }

    internal bool IsLoginCurrent(LoginResult login)
    { lock (identityLock) return acceptedLogins.TryGetValue(login, out var accepted) && IsCurrentLocked(accepted); }

    internal void ClearLoginIfCurrent(LoginResult login)
    {
        CancellationTokenSource? previous = null;
        lock (identityLock)
            if (acceptedLogins.TryGetValue(login, out var accepted) && IsCurrentLocked(accepted))
                previous = ReplaceIdentityLocked(null, null);
        previous?.Cancel();
    }

    private RequestContext Capture() { lock (identityLock) return CaptureLocked(); }
    private RequestContext CaptureLocked() => new(generation, token, serverUri, sessionCancellation.Token);

    private CancellationTokenSource ReplaceIdentityLocked(string? nextToken, UserInfo? nextUser)
    {
        var previous = sessionCancellation;
        generation++;
        token = nextToken;
        currentUser = nextUser;
        sessionCancellation = new CancellationTokenSource();
        traffic = new RequestTraffic();
        return previous;
    }

    private bool IsCurrentLocked(RequestContext context) => context.Generation == generation && context.Token == token;

    private void EnsureCurrentLocked(RequestContext context)
    {
        if (!IsCurrentLocked(context))
            throw new OperationCanceledException("이전 접속의 요청을 취소했습니다.", context.SessionCancellation);
    }

    private void ExpireCurrent(RequestContext context)
    {
        CancellationTokenSource? previous = null;
        lock (identityLock)
        {
            if (IsCurrentLocked(context) && context.Token is not null)
            {
                previous = ReplaceIdentityLocked(null, null);
                // Publish the event in the same critical section as the state change.
                // A second 401 or another login cannot interleave this notification.
                AuthenticationExpired?.Invoke(this, EventArgs.Empty);
            }
        }
        previous?.Cancel();
    }
    public Task<T> GetAsync<T>(string path, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Get, path, null, ct);
    public Task<T> PostAsync<T>(string path, object body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Post, path, body, ct);
    public Task<T> PatchAsync<T>(string path, object body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Patch, path, body, ct);
    public Task<T> DeleteAsync<T>(string path, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Delete, path, null, ct);

    public Task<T> PostIdempotentAsync<T>(string path, object body, string requestKey, CancellationToken ct = default)
        => SendAsync<T>(HttpMethod.Post, path, body, ct, requestKey: ValidateRequestKey(requestKey));
    public Task<T> PatchIdempotentAsync<T>(string path, object body, string requestKey, CancellationToken ct = default)
        => SendAsync<T>(HttpMethod.Patch, path, body, ct, requestKey: ValidateRequestKey(requestKey));

    private static string ValidateRequestKey(string key)
    {
        if (key is null || key.Length is < 32 or > 512 ||
            key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new ArgumentException("서버에서 받은 유효한 요청 키가 필요합니다.", nameof(key));
        return key;
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct,
        RequestContext? context = null, bool detached = false, string? requestKey = null,
        ClockOutFinalization? clockOut = null)
    {
        if (path.StartsWith('/') || path.Contains(":") || path.Split('/').Contains(".."))
            throw new ArgumentException("공통 API에는 상대 경로만 사용할 수 있습니다.");
        context ??= Capture();
        if (!detached) { lock (identityLock) EnsureCurrentLocked(context); }
        using var activity = !detached && clockOut is null ? EnterRequest(context) : null;
        if (clockOut is not null) clockOut.Current(ct);
        using var linked = detached ? CancellationTokenSource.CreateLinkedTokenSource(ct)
            : CancellationTokenSource.CreateLinkedTokenSource(ct, context.SessionCancellation,
                activity?.Cancellation ?? clockOut?.Cancellation ?? CancellationToken.None);
        using var request = new HttpRequestMessage(method, new Uri(context.ServerUri, "api/v1/" + path));
        if (context.Token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.Token);
        if (requestKey is not null) request.Headers.Add("Idempotency-Key", requestKey);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        try
        {
            using var response = await http.SendAsync(request, linked.Token);
            // The HTTP status is authoritative even for HTML, malformed or empty bodies.
            var unauthorized = response.StatusCode == HttpStatusCode.Unauthorized;
            lock (identityLock)
            {
                if (!detached) EnsureCurrentLocked(context);
                // A drained ordinary response cannot expire the independent original
                // finalize owner. A genuine current/recovery 401 still expires it.
                activity?.ThrowIfDraining();
                if (clockOut is not null) clockOut.Current(ct);
                if (unauthorized) ExpireCurrent(context);
            }
            var content = await response.Content.ReadAsStringAsync(unauthorized ? ct : linked.Token);
            if (!detached && !unauthorized)
            {
                lock (identityLock) EnsureCurrentLocked(context);
                activity?.ThrowIfDraining();
                if (clockOut is not null) clockOut.Current(ct);
            }
            ApiEnvelope<T>? envelope;
            try { envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(content, Json); }
            catch (JsonException) { throw new ApiException("서버 응답 형식이 올바르지 않습니다. 서버 주소를 확인해 주세요.", (int)response.StatusCode); }
            if (!response.IsSuccessStatusCode || envelope?.Ok != true || envelope.Data is null)
            {
                var message = envelope?.Error?.Message ?? "서버가 요청을 처리하지 못했습니다.";
                if (!string.IsNullOrEmpty(envelope?.RequestId)) message += $"\n요청 번호: {envelope.RequestId}";
                throw new ApiException(message, (int)response.StatusCode, envelope?.Error?.Code);
            }
            return envelope.Data;
        }
        catch (HttpRequestException) { throw new ApiException("서버에 연결할 수 없습니다. 서버 실행 여부와 서버 주소, 네트워크를 확인해 주세요."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested &&
            (detached || !context.SessionCancellation.IsCancellationRequested)
            && !(activity?.Cancellation.IsCancellationRequested ?? false)
            && !(clockOut?.Cancellation.IsCancellationRequested ?? false))
        { throw new ApiException("서버 응답 시간이 초과되었습니다. 잠시 후 다시 시도해 주세요."); }
    }

    public void Dispose() { ClearIdentity(); http.Dispose(); }

    internal sealed class RequestTraffic
    {
        internal readonly CancellationTokenSource Cancellation = new();
        internal int Active;
        internal TaskCompletionSource Idle = CompletedSignal();
        internal ClockOutFinalization? Closing;
        private static TaskCompletionSource CompletedSignal()
        { var value = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); value.SetResult(); return value; }
    }
    private sealed class RequestActivity(ApiClient owner, RequestTraffic lane) : IDisposable
    {
        public CancellationToken Cancellation => lane.Cancellation.Token;
        public void ThrowIfDraining()
        {
            if (lane.Closing is not null || Cancellation.IsCancellationRequested)
                throw new OperationCanceledException("퇴근 전 일반 요청을 정리했습니다.", Cancellation);
        }
        public void Dispose()
        { lock (owner.identityLock) if (--lane.Active == 0) lane.Idle.TrySetResult(); }
    }
    private RequestActivity EnterRequest(RequestContext context)
    {
        lock (identityLock)
        {
            EnsureCurrentLocked(context);
            if (traffic.Closing is not null) throw new OperationCanceledException("퇴근 저장 결과를 먼저 확인해 주세요.");
            if (traffic.Active++ == 0) traffic.Idle = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return new(this, traffic);
        }
    }
    private async Task<ClockOutFinalization> BeginClockOutAsync(RequestContext context, Guid work, DateOnly day,
        Guid challenge, Guid original, CancellationToken ct)
    {
        if (work == Guid.Empty || day == default || challenge == Guid.Empty || original == Guid.Empty)
            throw new ArgumentException("검증된 원 근무·도전·저장 요청 번호가 필요합니다.");
        ct.ThrowIfCancellationRequested();
        var verified = await SendAsync<JsonElement>(HttpMethod.Get,
            "device-identity/challenges/" + challenge.ToString("D"), null, ct, context).ConfigureAwait(false);
        try
        {
            if (verified.GetProperty("challenge_id").GetString() != challenge.ToString("D")
                || verified.GetProperty("state").GetString() != "verified"
                || verified.GetProperty("workflow_state").GetString() != "pending"
                || verified.GetProperty("identity_receipt").GetProperty("purpose").GetString() != "clock_out")
                throw new InvalidDataException();
        }
        catch (Exception e) when (e is InvalidDataException or InvalidOperationException or KeyNotFoundException)
        { throw new InvalidDataException("서버가 확인한 퇴근 지문 결과가 필요합니다."); }
        ClockOutFinalization closing;
        Task idle;
        lock (identityLock)
        {
            EnsureCurrentLocked(context);
            if (context.Token is null || currentUser is null || traffic.Closing is not null)
                throw new InvalidOperationException("현재 로그인에서 하나의 퇴근 작업만 연결할 수 있습니다.");
            closing = new(this, context, traffic, work, day, challenge, original);
            traffic.Closing = closing;
            idle = traffic.Idle.Task;
        }
        try
        {
            closing.lane.Cancellation.Cancel();
            await idle.WaitAsync(ct).ConfigureAwait(false);
            closing.Current(ct);
            return closing;
        }
        catch { closing.Dispose(); throw; }
    }

    /// <summary>RAM owner of ONE original clock-out finalize/replay. No general
    /// GET/POST, new challenge, token export or identity replacement is exposed.
    /// Keep with the shown login owner, never with a borrowed popup.</summary>
    public sealed class ClockOutFinalization : IDisposable
    {
        private readonly ApiClient owner;
        private readonly RequestContext context;
        internal readonly RequestTraffic lane;
        private readonly CancellationTokenSource lifetime;
        private readonly SemaphoreSlim gate = new(1);
        private readonly long started = System.Diagnostics.Stopwatch.GetTimestamp();
        private JsonElement? receipt;
        private int disposed;
        private volatile bool attempted;
        public Guid WorkSessionId { get; }
        public DateOnly WorkDate { get; }
        public Guid ChallengeId { get; }
        public Guid OriginalFinalizeId { get; }
        public bool HasFinalizeAttempt => attempted;
        public bool HasConfirmedReceipt => receipt is not null;
        public CancellationToken Cancellation { get; }
        public bool IsCurrent
        {
            get
            {
                lock (owner.identityLock) return Volatile.Read(ref disposed) == 0 && !Cancellation.IsCancellationRequested
                    && owner.IsCurrentLocked(context) && ReferenceEquals(owner.traffic, lane)
                    && ReferenceEquals(lane.Closing, this)
                    && System.Diagnostics.Stopwatch.GetElapsedTime(started) < TimeSpan.FromHours(24);
            }
        }
        internal ClockOutFinalization(ApiClient owner, RequestContext context, RequestTraffic lane,
            Guid work, DateOnly day, Guid challenge, Guid original)
        {
            this.owner = owner; this.context = context; this.lane = lane;
            WorkSessionId = work; WorkDate = day; ChallengeId = challenge; OriginalFinalizeId = original;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.SessionCancellation);
            Cancellation = lifetime.Token;
        }
        internal void Current(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!IsCurrent) throw new OperationCanceledException("퇴근 결과 확인 접속이 종료되었습니다.", Cancellation);
        }
        public async Task<JsonElement> CompleteAsync(CancellationToken ct = default)
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                Current(ct);
                if (receipt is { } known) return known.Clone();
                // Pin before network I/O. Neither a failed response nor reopening
                // the form changes this challenge/UUID or extends the local bound.
                attempted = true;
                var result = await owner.SendAsync<JsonElement>(HttpMethod.Post,
                    "device-identity/challenges/" + ChallengeId.ToString("D") + "/finalize",
                    new { client_request_id = OriginalFinalizeId.ToString("D") }, ct, context, clockOut: this).ConfigureAwait(false);
                Current(ct);
                try
                {
                    if (result.GetProperty("state").GetString() != "completed"
                        || result.TryGetProperty("access_token", out _) || result.TryGetProperty("pending_token", out _)
                        || result.GetProperty("work_session_ref").GetString() != WorkSessionId.ToString("D")
                        || result.GetProperty("work_date").GetString() != WorkDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                        || !Guid.TryParseExact(result.GetProperty("workflow_receipt_id").GetString(), "D", out var id)
                        || id == Guid.Empty || result.GetProperty("workflow_receipt_id").GetString() != id.ToString("D"))
                        throw new InvalidDataException();
                }
                catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or InvalidDataException)
                { throw new InvalidDataException("퇴근 저장을 확인할 수 없습니다. 원래 작업 결과를 다시 확인해 주세요."); }
                lock (owner.identityLock) { Current(ct); receipt = result.Clone(); }
                return result;
            }
            finally { gate.Release(); }
        }
        /// <summary>Call after the receipt is presented and owned monitor/UI cleanup.
        /// Never sends auth/logout and cannot clear a replacement login.</summary>
        public void EndSession()
        {
            CancellationTokenSource previous;
            lock (owner.identityLock)
            {
                Current(CancellationToken.None);
                if (receipt is null) throw new InvalidOperationException("원 퇴근 저장 성공을 먼저 확인해 주세요.");
                previous = owner.ReplaceIdentityLocked(null, null);
            }
            previous.Cancel();
        }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            CancellationTokenSource? previous = null;
            lock (owner.identityLock)
            {
                if (owner.IsCurrentLocked(context) && ReferenceEquals(owner.traffic, lane))
                {
                    if (attempted) previous = owner.ReplaceIdentityLocked(null, null);
                    else owner.traffic = new RequestTraffic(); // No finalize was ever sent.
                }
            }
            lifetime.Cancel();
            previous?.Cancel();
            // In-flight callbacks may still observe Cancellation; no disposed CTS access.
        }
    }
}
