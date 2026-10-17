namespace RemoteWork.WinForms.Features.Tasks.Queries;

/// <summary>A requested view, not evidence that the caller may access it.</summary>
public enum TaskQueryScope
{
    Own,
    Managed
}

/// <summary>
/// The five logical values displayed by this UI; enum ordinals are not wire codes.
/// HttpTaskQueryService explicitly maps the read/type contract accepted by member 1.
/// Write transitions and submission/deadline semantics await joint agreement with member 3.
/// </summary>
public enum TaskDisplayStatus
{
    NotStarted,
    InProgress,
    Submitted,
    Approved,
    Returned
}

public sealed record TaskListQuery(TaskQueryScope Scope, TaskDisplayStatus? Status = null);

/// <param name="TaskId">An opaque identifier; the UI must not parse or create database IDs.</param>
public sealed record TaskSummary(
    string TaskId,
    string Title,
    string AssigneeName,
    TaskDisplayStatus Status,
    DateOnly StartDate,
    DateOnly DueDate);

public sealed record TaskDetail(TaskSummary Summary, string Description);
