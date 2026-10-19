using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace RemoteWork.WinForms.Features.Attendance;

public sealed record LightMotionSample(bool Available, string? ObservationId = null, string? MotionId = null,
    double? SecondsSinceMotion = null, string? Error = null)
{
    public long ReceivedAt { get; init; } = Stopwatch.GetTimestamp();
    public double CurrentAge => Stopwatch.GetElapsedTime(ReceivedAt).TotalSeconds;
    public double TransportSeconds { get; init; }
    public long Continuity { get; init; }
}

public interface ILightMotionSource : IDisposable
{
    void Start(Uri baseUri);
    LightMotionSample? TakeLatest();
    Task StopAsync();
}

/// <summary>Read-only standalone Pi client. Never attaches common session tokens.</summary>
public sealed class LightMotionClient : IDisposable
{
    private readonly HttpClient http;
    public LightMotionClient() : this(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false }) { }
    internal LightMotionClient(HttpMessageHandler handler) => http = new(handler) { Timeout = TimeSpan.FromSeconds(2) };

    public static Uri ParseBaseUri(string text)
    {
        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || string.IsNullOrEmpty(uri.Host) ||
            uri.UserInfo.Length != 0 || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("라파 주소를 http://라파주소:8080 형식으로 입력해 주세요.");
        return uri;
    }

    public async Task<LightMotionSample> ReadAsync(Uri baseUri, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(ParseBaseUri(baseUri.AbsoluteUri), "motion"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
        long requestedAt = Stopwatch.GetTimestamp();
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(2));
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            return new(false, Error: "라파는 응답하지만 조도센서 데이터가 없습니다.");
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
        using var body = new MemoryStream();
        byte[] buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, deadline.Token).ConfigureAwait(false)) != 0)
        {
            if (body.Length + read > 65536) throw new InvalidDataException("조도센서 응답이 너무 큽니다.");
            body.Write(buffer, 0, read);
        }
        using var document = JsonDocument.Parse(body.ToArray());
        var root = document.RootElement;
        if (root.GetProperty("success").ValueKind != JsonValueKind.True)
            return new(false, Error: "조도센서의 상태를 확인해 주세요.");
        var data = root.GetProperty("data");
        if (data.GetProperty("available").ValueKind != JsonValueKind.True || data.GetProperty("connected").ValueKind != JsonValueKind.True)
            return new(false, Error: "조도센서 연결을 확인해 주세요.");
        if (data.GetProperty("state").GetString() is not ("CLEAR" or "COVERED"))
            return new(false, Error: "조도센서 기준값을 확인하고 있습니다.");
        string observed = Timestamp(data.GetProperty("observed_at"));
        string? motion = data.TryGetProperty("last_motion_at", out var value) && value.ValueKind != JsonValueKind.Null ? Timestamp(value) : null;
        double? age = data.TryGetProperty("seconds_since_motion", out value) && value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
        if ((motion is null) != (age is null) || (age.HasValue && (!double.IsFinite(age.Value) || age.Value < 0)))
            throw new InvalidDataException("조도센서 변화 시각을 확인할 수 없습니다.");
        return new(true, observed, motion, age)
        {
            ReceivedAt = requestedAt,
            TransportSeconds = Stopwatch.GetElapsedTime(requestedAt).TotalSeconds
        };
    }

    private static string Timestamp(JsonElement value)
    {
        string? text = value.GetString();
        if (text is null || !DateTimeOffset.TryParseExact(text,
            new[] { "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz", "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd'T'HH:mm:ss'Z'" },
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
            throw new InvalidDataException("조도센서 관찰 시각이 올바르지 않습니다.");
        return timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    }
    public void Dispose() => http.Dispose();
}

public sealed class PollingLightMotionSource : ILightMotionSource
{
    private readonly LightMotionClient client;
    private readonly object gate = new();
    private CancellationTokenSource? stop;
    private Task? polling;
    private LightMotionSample? latest;
    private bool disposed;
    public PollingLightMotionSource() : this(new LightMotionClient()) { }
    internal PollingLightMotionSource(LightMotionClient client) => this.client = client;

    public void Start(Uri baseUri)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (polling is { IsCompleted: false }) throw new InvalidOperationException("조도센서 조회가 실행 중입니다.");
            stop?.Dispose();
            stop = new();
            latest = null;
            polling = PollAsync(baseUri, stop.Token);
        }
    }

    private async Task PollAsync(Uri uri, CancellationToken token)
    {
        long continuity = 0;
        try
        {
            while (!token.IsCancellationRequested)
            {
                LightMotionSample sample;
                try { sample = await client.ReadAsync(uri, token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception error) when (error is not OutOfMemoryException)
                { sample = new(false, Error: "라파 주소·네트워크·조도센서 연결을 확인해 주세요."); }
                if (!sample.Available) continuity++;
                sample = sample with { Continuity = continuity };
                lock (gate) { if (!disposed && !token.IsCancellationRequested) latest = sample; }
                await Task.Delay(1000, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public LightMotionSample? TakeLatest() { lock (gate) { var sample = latest; latest = null; return sample; } }
    public async Task StopAsync()
    {
        Task? pending;
        lock (gate) { stop?.Cancel(); latest = null; pending = polling; }
        if (pending is not null) await pending.ConfigureAwait(false);
    }
    public void Dispose()
    {
        lock (gate) { if (disposed) return; disposed = true; stop?.Cancel(); latest = null; }
        client.Dispose();
        if (polling is { } pending) _ = pending.ContinueWith(_ => stop?.Dispose(), TaskScheduler.Default);
        else stop?.Dispose();
    }
}
