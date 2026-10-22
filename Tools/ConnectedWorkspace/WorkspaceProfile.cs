using System.Text.Json;
using RemoteWork.WinForms.Features.Attendance;
using RemoteWork.WinForms.Infrastructure;

namespace RemoteWork.ConnectedWorkspace;

public sealed record WorkspaceProfile(Uri Server, WorkdayActivityOptions? Workday, bool LeaveEnabled)
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RemoteWork", "workspace-connections.json");
    public static WorkspaceProfile Load(Uri server, string? path = null)
    {
        var file = new FileInfo(path ?? DefaultPath);
        if (!file.Exists || file.Length > 16384) throw new InvalidDataException("통합 연결 설정 파일을 확인해 주세요.");
        return Parse(File.ReadAllBytes(file.FullName), server, EnrollmentEndpoints.Load(server));
    }
    public static WorkspaceProfile Parse(ReadOnlyMemory<byte> bytes, Uri current, EnrollmentEndpoints endpoints)
    {
        try
        {
            if (bytes.Length > 16384) throw new InvalidDataException();
            using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 5 });
            var root = doc.RootElement;
            Fields(root, "version", "server_url", "workday", "leave_enabled");
            if (root.GetProperty("version").GetInt32() != 1) throw new InvalidDataException();
            var server = ApiClient.ValidateUrl(root.GetProperty("server_url").GetString() ?? throw new InvalidDataException());
            if (server != ApiClient.ValidateUrl(current.ToString())) throw new InvalidDataException();
            bool leave = root.GetProperty("leave_enabled").GetBoolean();
            WorkdayActivityOptions? options = null;
            var workday = root.GetProperty("workday");
            if (workday.ValueKind != JsonValueKind.Null)
            {
                Fields(workday, "device_id", "camera");
                string id = workday.GetProperty("device_id").GetString()!;
                if (!Guid.TryParseExact(id, "D", out var device) || device == Guid.Empty || device.ToString("D") != id)
                    throw new InvalidDataException();
                var camera = workday.GetProperty("camera");
                Fields(camera, "index", "backend", "left", "top", "right", "bottom");
                var selected = new ActivityCameraOptions(camera.GetProperty("index").GetInt32(), camera.GetProperty("backend").GetString()!,
                    camera.GetProperty("left").GetInt32(), camera.GetProperty("top").GetInt32(),
                    camera.GetProperty("right").GetInt32(), camera.GetProperty("bottom").GetInt32());
                selected.Validate(); options = new(device, endpoints.Resolve(id), selected);
            }
            if (options is null && !leave) throw new InvalidDataException();
            return new(server, options, leave);
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException)
        { throw new InvalidDataException("통합 연결 설정의 서버·장치·카메라 항목을 확인해 주세요."); }
    }
    private static void Fields(JsonElement value, params string[] expected)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
        var names = value.EnumerateObject().Select(p => p.Name).ToArray();
        if (names.Length != expected.Length || !names.Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal)))
            throw new InvalidDataException();
    }
}
