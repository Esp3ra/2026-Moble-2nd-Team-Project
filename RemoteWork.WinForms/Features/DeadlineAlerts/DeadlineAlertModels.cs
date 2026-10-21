using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Queries;

namespace RemoteWork.WinForms.Features.DeadlineAlerts;

// In-process integration draft, NOT an agreed HTTP DTO, event model or DB schema.
// EventToken must come from authoritative deadline history; never manufacture it
// from a date, row_version or assignment_revision in the client.
public sealed record DeadlineAlert(
    string EventToken, string TaskId, string RowVersion, int AssignmentRevision,
    int RecipientId, int CurrentAssigneeId, string Title, DateOnly DueDate,
    TaskDisplayStatus Status, bool IsCancelled = false, bool IsConfirmed = false);

public sealed record DeadlineAlertSnapshot(DateTimeOffset ServerTime, IReadOnlyList<DeadlineAlert> Items);

public enum DeadlineConfirmationState { Confirmed, Changed, NotRecorded, Unknown }
public sealed record DeadlineConfirmation(DeadlineConfirmationState State, DeadlineAlertSnapshot Snapshot);

/// <summary>
/// Captured login port. Implementation awaits member1/2 API/DB review. Every
/// response must reauthorize current own-assignment/state/cancellation and event.
/// No adapter for an invented endpoint or local persistence is installed.
/// </summary>
public interface IDeadlineAlertSource
{
    int UserId { get; }
    bool IsCurrent { get; }
    CancellationToken Cancellation { get; }
    Task<DeadlineAlertSnapshot> ReadAsync(CancellationToken cancellationToken);
    Task<DeadlineAlertSnapshot> RecheckAsync(string eventToken, CancellationToken cancellationToken);
    // Source owns the common W1 operation/key. Exact event and original key/body
    // survive uncertain results; Resolve is read-only, never an automatic write.
    Task<DeadlineConfirmation> ConfirmAsync(string eventToken, CancellationToken cancellationToken);
    Task<DeadlineConfirmation> ResolveConfirmationAsync(string eventToken, CancellationToken cancellationToken);
}

public static class DeadlineAlertPolicy
{
    public static readonly TimeSpan NormalInterval = TimeSpan.FromSeconds(60);
    public static TimeSpan RetryInterval(int failures) => TimeSpan.FromMinutes(failures switch { <= 1 => 2, 2 => 4, _ => 5 });
    private static readonly TimeSpan Kst = TimeSpan.FromHours(9);
    public static bool IsEligible(DeadlineAlert item, int userId) =>
        item.RecipientId == userId && item.CurrentAssigneeId == userId &&
        !item.IsCancelled && !item.IsConfirmed &&
        item.Status is TaskDisplayStatus.NotStarted or TaskDisplayStatus.InProgress or TaskDisplayStatus.Returned;
    public static bool HasOccurred(DeadlineAlert item, DateTimeOffset serverTime) =>
        item.DueDate != DateOnly.MinValue && serverTime.ToOffset(Kst).DateTime >=
            item.DueDate.AddDays(-1).ToDateTime(new TimeOnly(9, 0));
    public static bool CanPopup(DeadlineAlert item, int userId, DateTimeOffset serverTime) =>
        IsEligible(item, userId) && HasOccurred(item, serverTime) &&
        DateOnly.FromDateTime(serverTime.ToOffset(Kst).DateTime) <= item.DueDate;
    public static void Validate(DeadlineAlertSnapshot snapshot, int userId)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var tasks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot.Items)
        {
            if (string.IsNullOrWhiteSpace(item.EventToken) || item.EventToken.Length > 256 ||
                !tokens.Add(item.EventToken) || !tasks.Add(item.TaskId) ||
                !Bigint(item.TaskId) || !Bigint(item.RowVersion) || item.AssignmentRevision < 1 ||
                item.RecipientId != userId || item.CurrentAssigneeId < 1 ||
                string.IsNullOrWhiteSpace(item.Title) || !Enum.IsDefined(item.Status))
                throw new InvalidDataException("마감 알림 응답이 현재 사용자·식별자 규약과 일치하지 않습니다.");
        }
    }
    private static bool Bigint(string text) => long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long value) &&
        value > 0 && value.ToString(CultureInfo.InvariantCulture) == text;
}
