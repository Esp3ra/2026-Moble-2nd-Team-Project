namespace RemoteWork.WinForms.Features.Tasks.Queries;

/// <summary>UI query failures mapped by the HTTP adapter. Cancellation remains OperationCanceledException.</summary>
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
