using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RemoteWork.WinForms.Infrastructure;

/// <summary>Shared by all feature screens. Tokens stay in memory and are never logged.</summary>
public sealed class ApiClient : IDisposable
{
    private readonly HttpClient http;
    private readonly object identityLock = new();
    private string? token;
    private UserInfo? currentUser;
    private Uri serverUri;
    private long generation;
    private CancellationTokenSource sessionCancellation = new();
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
            previous = ReplaceIdentityLocked(result.AccessToken, result.User);
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

    private RequestContext Capture() { lock (identityLock) return CaptureLocked(); }
    private RequestContext CaptureLocked() => new(generation, token, serverUri, sessionCancellation.Token);

    private CancellationTokenSource ReplaceIdentityLocked(string? nextToken, UserInfo? nextUser)
    {
        var previous = sessionCancellation;
        generation++;
        token = nextToken;
        currentUser = nextUser;
        sessionCancellation = new CancellationTokenSource();
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
        RequestContext? context = null, bool detached = false, string? requestKey = null)
    {
        if (path.StartsWith('/') || path.Contains(":") || path.Split('/').Contains(".."))
            throw new ArgumentException("공통 API에는 상대 경로만 사용할 수 있습니다.");
        context ??= Capture();
        if (!detached) { lock (identityLock) EnsureCurrentLocked(context); }
        using var linked = detached ? CancellationTokenSource.CreateLinkedTokenSource(ct)
            : CancellationTokenSource.CreateLinkedTokenSource(ct, context.SessionCancellation);
        using var request = new HttpRequestMessage(method, new Uri(context.ServerUri, "api/v1/" + path));
        if (context.Token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.Token);
        if (requestKey is not null) request.Headers.Add("Idempotency-Key", requestKey);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        try
        {
            using var response = await http.SendAsync(request, linked.Token);
            if (!detached) { lock (identityLock) EnsureCurrentLocked(context); }
            // The HTTP status is authoritative even for HTML, malformed or empty bodies.
            var unauthorized = response.StatusCode == HttpStatusCode.Unauthorized;
            if (unauthorized) ExpireCurrent(context);
            var content = await response.Content.ReadAsStringAsync(unauthorized ? ct : linked.Token);
            if (!detached && !unauthorized) { lock (identityLock) EnsureCurrentLocked(context); }
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
            (detached || !context.SessionCancellation.IsCancellationRequested))
        { throw new ApiException("서버 응답 시간이 초과되었습니다. 잠시 후 다시 시도해 주세요."); }
    }

    public void Dispose() { ClearIdentity(); http.Dispose(); }
}
