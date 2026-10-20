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
/// Management/deadline policy follows the team-lead decision on 2026-10-20;
/// a read DTO never performs those writes or proves current write eligibility.
/// </summary>
public enum TaskDisplayStatus
{
    NotStarted,
    InProgress,
    Submitted,
    Approved,
    Returned
}

public sealed record TaskListQuery(TaskQueryScope Scope, TaskDisplayStatus? Status = null, bool IncludeCancelled = false);

/// <param name="TaskId">An opaque identifier; the UI must not parse or create database IDs.</param>
public sealed record TaskSummary(
    string TaskId,
    string Title,
    string AssigneeName,
    TaskDisplayStatus Status,
    DateOnly StartDate,
    DateOnly DueDate,
    bool IsCancelled = false);

public sealed record TaskDetail(TaskSummary Summary, string Description);
