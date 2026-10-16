namespace RemoteWork.WinForms.Features.Tasks.Queries;

/// <summary>UI boundary failures. Mapping from a future common error envelope is not yet agreed.</summary>
public enum TaskQueryFailure
{
    NotConfigured,
    AuthenticationExpired,
    Forbidden,
    NotFound,
    Unavailable
}

public sealed class TaskQueryException : Exception
{
    public TaskQueryFailure Failure { get; }

    public TaskQueryException(
        TaskQueryFailure failure, string? message = null, Exception? innerException = null)
        : base(message ?? failure.ToString(), innerException)
    {
        Failure = failure;
    }
}
