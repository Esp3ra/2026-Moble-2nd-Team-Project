namespace RemoteWork.WinForms.Infrastructure;

public sealed record UserInfo(int Id, string LoginId, string DisplayName, string EmployeeNo,
    string Department, string Role, bool IsActive);
public sealed record LoginResult(string AccessToken, string TokenType, DateTimeOffset ExpiresAt, UserInfo User);
public sealed record IdentityResult(UserInfo User, DateTimeOffset ExpiresAt);
public sealed record UserResult(UserInfo User);
public sealed record PageResult<T>(List<T> Items, int Page, int Limit, int Total);
public sealed record MessageResult(string Message);
public sealed record HealthResult(string Status, string Database, int SchemaVersion, string Version);
internal sealed record ApiError(string Code, string Message);
internal sealed record ApiEnvelope<T>(bool Ok, T? Data, ApiError? Error, string? RequestId);

public sealed class ApiException(string message, int statusCode = 0, string? code = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string? Code { get; } = code;
}
