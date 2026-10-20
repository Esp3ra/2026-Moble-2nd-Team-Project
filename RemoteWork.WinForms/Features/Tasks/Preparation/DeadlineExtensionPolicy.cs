using RemoteWork.WinForms.Features.Tasks.Queries;

namespace RemoteWork.WinForms.Features.Tasks.Preparation;

/// <summary>Approved local input rules, not evidence of current assignment or server acceptance.</summary>
public static class DeadlineExtensionPolicy
{
    public static DateOnly KoreanDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(instant.ToOffset(TimeSpan.FromHours(9)).DateTime);

    public static bool AllowsRequest(TaskDisplayStatus status) =>
        status is TaskDisplayStatus.InProgress or TaskDisplayStatus.Returned;

    public static string? DateError(DateOnly currentDueDate, DateOnly requestedDueDate, DateOnly todayKst)
    {
        if (requestedDueDate <= currentDueDate)
            return "희망 마감일은 현재 마감일보다 뒤여야 합니다.";
        if (requestedDueDate < todayKst)
            return "희망 마감일은 한국 시간 오늘 이상이어야 합니다. 승인 시점에도 다시 확인합니다.";
        return null;
    }
}
