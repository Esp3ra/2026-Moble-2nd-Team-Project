using System.Text.Json;

namespace RemoteWork.WinForms.Infrastructure;

/// <summary>PC topology only. Contains no account bearer or device signing keys.</summary>
public sealed class EnrollmentEndpoints
{
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RemoteWork", "enrollment-endpoints.json");
    private readonly Dictionary<string, Uri> devices;
    private EnrollmentEndpoints(Dictionary<string, Uri> devices) => this.devices = devices;
    // Topology only; never implies that a device is assigned to the current user.
    public IReadOnlyList<string> DeviceIds => devices.Keys.Order(StringComparer.Ordinal).ToArray();

    public static EnrollmentEndpoints Load(Uri currentServer, string? path = null)
    {
        try
        {
            var file = new FileInfo(path ?? DefaultPath);
            if (!file.Exists || file.Length > 65536) throw new InvalidDataException();
            return Parse(File.ReadAllBytes(file.FullName), currentServer);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        { throw new InvalidDataException("지문 등록 주소 설정을 확인해 주세요. 서버 주소와 장치별 Pi 주소가 필요합니다.", error); }
    }

    public static EnrollmentEndpoints Parse(ReadOnlyMemory<byte> json, Uri currentServer)
    {
        try
        {
            if (json.Length > 65536) throw new InvalidDataException();
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 6 });
            var root = document.RootElement;
            Fields(root, "version", "server_url", "allow_insecure_http", "devices");
            if (root.GetProperty("version").ValueKind != JsonValueKind.Number
                || !root.GetProperty("version").TryGetInt32(out int version) || version != 1)
                throw new InvalidDataException();
            var insecure = root.GetProperty("allow_insecure_http");
            if (insecure.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new InvalidDataException();
            bool allowHttp = insecure.GetBoolean();
            var server = Address(root.GetProperty("server_url"), allowHttp);
            if (server != ApiClient.ValidateUrl(currentServer.AbsoluteUri))
                throw new InvalidDataException("현재 API 서버와 지문 등록 설정의 서버가 다릅니다.");
            var list = root.GetProperty("devices");
            if (list.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
            var addresses = new Dictionary<string, Uri>(StringComparer.Ordinal);
            foreach (var entry in list.EnumerateObject())
            {
                if (!Guid.TryParseExact(entry.Name, "D", out var id) || id.ToString() != entry.Name)
                    throw new InvalidDataException();
                Fields(entry.Value, "pi_base_url");
                if (!addresses.TryAdd(entry.Name, Address(entry.Value.GetProperty("pi_base_url"), allowHttp)))
                    throw new InvalidDataException();
            }
            return new(addresses);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException)
        { throw new InvalidDataException("지문 등록 주소 설정 형식을 확인해 주세요.", error); }
    }

    public Uri Resolve(string deviceId)
        => devices.TryGetValue(deviceId, out var address) ? address
            : throw new InvalidDataException("선택한 장치의 Pi 주소가 없습니다. 주소 설정 후 다시 시도해 주세요.");

    private static Uri Address(JsonElement value, bool allowHttp)
    {
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
            throw new InvalidDataException();
        var address = ApiClient.ValidateUrl(value.GetString()!);
        if (address.Scheme != "https" && !allowHttp)
            throw new InvalidDataException("지문 등록에는 HTTPS 주소가 필요합니다. 격리 개발 환경의 HTTP는 명시 설정이 필요합니다.");
        return address;
    }

    private static void Fields(JsonElement value, params string[] names)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
        var properties = value.EnumerateObject().Select(property => property.Name).ToArray();
        if (properties.Length != names.Length || properties.Distinct(StringComparer.Ordinal).Count() != properties.Length
            || !properties.Order(StringComparer.Ordinal).SequenceEqual(names.Order(StringComparer.Ordinal)))
            throw new InvalidDataException();
    }
}
