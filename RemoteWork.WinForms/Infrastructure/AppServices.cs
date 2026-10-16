using System.Text.Json;

namespace RemoteWork.WinForms.Infrastructure;

public static class AppServices
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RemoteWork", "client.json");
    public static ApiClient Api { get; } = new(LoadUrl());

    private static string LoadUrl()
    {
        var environmentUrl = Environment.GetEnvironmentVariable("REMOTEWORK_API_URL");
        if (!string.IsNullOrWhiteSpace(environmentUrl))
        {
            try { return ApiClient.ValidateUrl(environmentUrl).ToString(); }
            catch (ArgumentException) { }
        }
        try
        {
            if (File.Exists(ConfigPath))
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(ConfigPath));
                if (config?.TryGetValue("server_url", out var url) == true) return ApiClient.ValidateUrl(url).ToString();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException) { }
        return "http://127.0.0.1:5000/";
    }

    public static void SaveServerUrl(string url)
    {
        var uri = ApiClient.ValidateUrl(url);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(new { server_url = uri.ToString() }));
        Api.SetServer(uri.ToString());
    }
}
