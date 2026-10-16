using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RemoteWork.WinForms.Infrastructure;

/// <summary>Shared by all feature screens. Tokens stay in memory and are never logged.</summary>
public sealed class ApiClient : IDisposable
{
    private readonly HttpClient http;
    private string? token;
    public Uri ServerUri { get; private set; }
    public UserInfo? CurrentUser { get; private set; }
    public event EventHandler? AuthenticationExpired;
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public ApiClient(string serverUrl, HttpMessageHandler? handler = null)
    {
        ServerUri = ValidateUrl(serverUrl);
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
        if (CurrentUser is not null) throw new InvalidOperationException("로그아웃 후 서버 주소를 변경해 주세요.");
        ServerUri = ValidateUrl(url);
    }

    public async Task<LoginResult> LoginAsync(string loginId, string password, CancellationToken ct = default)
    {
        ClearIdentity();
        var result = await PostAsync<LoginResult>("auth/login", new { login_id = loginId, password }, ct);
        token = result.AccessToken;
        CurrentUser = result.User;
        return result;
    }

    public async Task<IdentityResult> RefreshIdentityAsync(CancellationToken ct = default)
    {
        var result = await GetAsync<IdentityResult>("auth/me", ct);
        CurrentUser = result.User;
        return result;
    }

    public async Task LogoutAsync()
    {
        try
        {
            if (token is not null) await PostAsync<MessageResult>("auth/logout", new { });
        }
        finally { ClearIdentity(); }
    }

    public void ClearIdentity() { token = null; CurrentUser = null; }
    public Task<T> GetAsync<T>(string path, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Get, path, null, ct);
    public Task<T> PostAsync<T>(string path, object body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Post, path, body, ct);
    public Task<T> PatchAsync<T>(string path, object body, CancellationToken ct = default) => SendAsync<T>(HttpMethod.Patch, path, body, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        if (path.StartsWith('/') || path.Contains(":") || path.Split('/').Contains(".."))
            throw new ArgumentException("공통 API에는 상대 경로만 사용할 수 있습니다.");
        using var request = new HttpRequestMessage(method, new Uri(ServerUri, "api/v1/" + path));
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        try
        {
            using var response = await http.SendAsync(request, ct);
            var content = await response.Content.ReadAsStringAsync(ct);
            ApiEnvelope<T>? envelope;
            try { envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(content, Json); }
            catch (JsonException) { throw new ApiException("서버 응답 형식이 올바르지 않습니다. 서버 주소를 확인해 주세요.", (int)response.StatusCode); }
            if (response.StatusCode == HttpStatusCode.Unauthorized && token is not null)
            {
                ClearIdentity();
                AuthenticationExpired?.Invoke(this, EventArgs.Empty);
            }
            if (!response.IsSuccessStatusCode || envelope?.Ok != true || envelope.Data is null)
            {
                var message = envelope?.Error?.Message ?? "서버가 요청을 처리하지 못했습니다.";
                if (!string.IsNullOrEmpty(envelope?.RequestId)) message += $"\n요청 번호: {envelope.RequestId}";
                throw new ApiException(message, (int)response.StatusCode, envelope?.Error?.Code);
            }
            return envelope.Data;
        }
        catch (HttpRequestException) { throw new ApiException("서버에 연결할 수 없습니다. 서버 실행 여부와 서버 주소, 네트워크를 확인해 주세요."); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException("서버 응답 시간이 초과되었습니다. 잠시 후 다시 시도해 주세요."); }
    }

    public void Dispose() => http.Dispose();
}
