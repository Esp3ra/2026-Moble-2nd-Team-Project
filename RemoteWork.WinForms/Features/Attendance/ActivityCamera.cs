using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("ActivityPresence.Tests")]

namespace RemoteWork.WinForms.Features.Attendance;

public sealed record ActivityCameraOptions(int Index = 0, string Backend = "DSHOW",
    int Left = 0, int Top = 35, int Right = 100, int Bottom = 100)
{
    public void Validate()
    {
        if (Index is < 0 or > 4 || Backend is not ("DSHOW" or "MSMF"))
            throw new ArgumentException("카메라 번호와 연결 방식을 확인해 주세요.");
        if (Left < 0 || Top < 0 || Right > 100 || Bottom > 100 || Left >= Right || Top >= Bottom)
            throw new ArgumentException("화면 범위는 0~100이며 왼쪽·위쪽이 오른쪽·아래쪽보다 작아야 합니다.");
    }
}

public sealed record ActivityCameraFrame(string State, double AgeSeconds = 0,
    double? SecondsWithoutHand = null, byte[]? Jpeg = null, string? Error = null)
{
    public long ReceivedAt { get; init; } = Stopwatch.GetTimestamp();
    // CPython 3.13 monotonic and .NET Stopwatch use the same Windows QPC clock.
    public double? ObservedSeconds { get; init; }
    public double CurrentAge => Math.Max(AgeSeconds + Stopwatch.GetElapsedTime(ReceivedAt).TotalSeconds,
        ObservedSeconds.HasValue ? Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency - ObservedSeconds.Value : 0);

    public static ActivityCameraFrame Parse(string line)
    {
        if (line.Length > 600_000) throw new InvalidDataException("카메라 응답이 너무 큽니다.");
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;
        if (root.GetProperty("version").GetInt32() != 1)
            throw new InvalidDataException("카메라 응답 버전을 확인해 주세요.");
        var state = root.GetProperty("state").GetString()!;
        if (state is not ("HAND_DETECTED" or "HAND_NOT_DETECTED" or "CAMERA_ERROR" or "STALE" or "STARTING"))
            throw new InvalidDataException("카메라 상태를 확인할 수 없습니다.");
        var age = root.GetProperty("age_seconds").GetDouble();
        var observed = root.GetProperty("observed_seconds").GetDouble();
        double? missing = root.TryGetProperty("seconds_without_hand", out var value) &&
            value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
        if (!double.IsFinite(age) || age < 0 || !double.IsFinite(observed) || observed < 0 ||
            observed > Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency + 1 ||
            (missing.HasValue && (!double.IsFinite(missing.Value) || missing < 0)))
            throw new InvalidDataException("카메라 응답 시각이 올바르지 않습니다.");
        string? error = root.TryGetProperty("error", out value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;
        byte[]? jpeg = state is "CAMERA_ERROR" or "STALE" || age > 5 ? null :
            root.TryGetProperty("jpeg", out value) && value.ValueKind == JsonValueKind.String
                ? Convert.FromBase64String(value.GetString()!) : null;
        return new(state, age, missing, jpeg, error is { Length: > 500 } ? error[..500] : error) { ObservedSeconds = observed };
    }
}

public interface IActivityCamera : IDisposable
{
    bool IsRunning { get; }
    void Start(ActivityCameraOptions options);
    ActivityCameraFrame? TakeLatest();
    Task StopAsync();
}

/// <summary>Owns one local Python child. Only cropped preview/status travel over private stdio.</summary>
public sealed class ProcessActivityCamera : IActivityCamera
{
    private sealed class Session(Process process)
    {
        public Process Process { get; } = process;
        public volatile bool Stopping;
        public Task Completion = Task.CompletedTask;
    }
    private readonly object gate = new();
    private Session? session;
    private ActivityCameraFrame? latest;
    private bool disposed;
    private readonly Func<ActivityCameraOptions, ProcessStartInfo> startFactory;
    public ProcessActivityCamera() : this(BuildStartInfo) { }
    internal ProcessActivityCamera(Func<ActivityCameraOptions, ProcessStartInfo> startFactory) => this.startFactory = startFactory;
    public bool IsRunning { get { lock (gate) return session is { Completion.IsCompleted: false }; } }

    public void Start(ActivityCameraOptions options)
    {
        options.Validate();
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (session is { Completion.IsCompleted: false }) throw new InvalidOperationException("카메라가 실행 중입니다.");
            session?.Process.Dispose();
            session = null;
            latest = null;
            var start = startFactory(options);
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardInput = start.RedirectStandardOutput = start.RedirectStandardError = true;
            start.StandardOutputEncoding = start.StandardErrorEncoding = Encoding.UTF8;
            start.Environment["PYTHONIOENCODING"] = "utf-8";
            var process = new Process { StartInfo = start };
            try { process.Start(); }
            catch { process.Dispose(); throw; }
            var current = new Session(process);
            session = current;
            current.Completion = Task.Run(() => ReadSessionAsync(current));
        }
    }

    private static ProcessStartInfo BuildStartInfo(ActivityCameraOptions options)
    {
        string tools = FindTools();
        var start = new ProcessStartInfo(Path.Combine(tools, ".venv", "Scripts", "python.exe")) { WorkingDirectory = tools };
        foreach (var argument in new[] { "-u", Path.Combine(tools, "winforms_bridge.py"),
            "--index", options.Index.ToString(CultureInfo.InvariantCulture), "--backend", options.Backend,
            "--left", options.Left.ToString(CultureInfo.InvariantCulture), "--top", options.Top.ToString(CultureInfo.InvariantCulture),
            "--right", options.Right.ToString(CultureInfo.InvariantCulture), "--bottom", options.Bottom.ToString(CultureInfo.InvariantCulture) })
            start.ArgumentList.Add(argument);
        return start;
    }

    private static string FindTools()
    {
        foreach (var initial in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            for (var directory = new DirectoryInfo(initial); directory is not null; directory = directory.Parent)
            {
                var tools = Path.Combine(directory.FullName, "Tools", "HandPresence");
                if (File.Exists(Path.Combine(tools, "winforms_bridge.py")) &&
                    File.Exists(Path.Combine(tools, ".venv", "Scripts", "python.exe"))) return tools;
            }
        throw new FileNotFoundException("카메라 준비가 필요합니다. 저장소의 Tools\\HandPresence\\Setup-Hand-Presence.cmd를 먼저 실행해 주세요.");
    }

    private void Publish(Session owner, ActivityCameraFrame frame)
    {
        lock (gate) if (!disposed && session == owner && !owner.Stopping) latest = frame;
    }

    private async Task ReadSessionAsync(Session owner)
    {
        // Drain both streams concurrently so native diagnostics cannot block the camera pipe.
        Task diagnostics = owner.Process.StandardError.BaseStream.CopyToAsync(Stream.Null);
        bool terminalError = false;
        try
        {
            while (await owner.Process.StandardOutput.ReadLineAsync().ConfigureAwait(false) is { } line)
            {
                var frame = ActivityCameraFrame.Parse(line);
                terminalError |= frame.State == "CAMERA_ERROR";
                Publish(owner, frame);
            }
            await owner.Process.WaitForExitAsync().ConfigureAwait(false);
            if (!terminalError) Publish(owner, new("CAMERA_ERROR", Error: "카메라 처리가 종료되었습니다. 연결 상태를 확인하고 다시 시작해 주세요."));
        }
        catch (Exception error) when (error is not OutOfMemoryException)
        {
            Publish(owner, new("CAMERA_ERROR", Error: "카메라 응답을 처리할 수 없습니다. 중지 후 다시 시작해 주세요."));
            Kill(owner);
        }
        finally
        {
            try { await diagnostics.ConfigureAwait(false); } catch (IOException) { } catch (ObjectDisposedException) { }
        }
    }

    public ActivityCameraFrame? TakeLatest()
    {
        lock (gate) { var frame = latest; latest = null; return frame; }
    }

    public async Task StopAsync()
    {
        Session? current;
        lock (gate) { current = session; if (current is null) return; current.Stopping = true; latest = null; }
        try
        {
            if (!current.Process.HasExited)
            {
                await current.Process.StandardInput.WriteLineAsync("stop").ConfigureAwait(false);
                await current.Process.StandardInput.FlushAsync().ConfigureAwait(false);
            }
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or ObjectDisposedException) { }
        if (await Task.WhenAny(current.Completion, Task.Delay(2000)).ConfigureAwait(false) != current.Completion) Kill(current);
        await current.Completion.ConfigureAwait(false);
    }

    private static void Kill(Session owner)
    {
        try { if (!owner.Process.HasExited) owner.Process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            latest = null;
            if (session is { } owner) { owner.Stopping = true; Kill(owner); }
        }
        // ReadSessionAsync owns stream reads; dispose handles only after they finish.
        if (session is { } current)
            _ = current.Completion.ContinueWith(_ => current.Process.Dispose(), TaskScheduler.Default);
    }
}
