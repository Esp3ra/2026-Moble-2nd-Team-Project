namespace RemoteWork.WinForms.Features.Tasks.Preparation;

// Local input snapshots only. These are not server request DTOs or agreed write contracts.
public sealed record TaskBasicInputDraft(
    string? TaskId,
    string Title,
    string Description,
    DateOnly StartDate,
    DateOnly DueDate);

public sealed record DeadlineExtensionInputDraft(
    string TaskId,
    DateOnly OriginalDueDate,
    DateOnly RequestedDueDate,
    string Reason);
