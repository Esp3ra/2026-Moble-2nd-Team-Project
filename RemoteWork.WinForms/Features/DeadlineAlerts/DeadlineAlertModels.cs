using System.Globalization;
using RemoteWork.WinForms.Features.Tasks.Queries;

namespace RemoteWork.WinForms.Features.DeadlineAlerts;

// In-process integration draft, NOT an agreed HTTP DTO, event model or DB schema.
// EventToken is the opaque confirmation target provided by the source.
// PR70 uses the server-provided d1 task/event/assignment binding; EventId is the
// separate immutable deadline UUID. Never derive an event from dates/versions.
public sealed record DeadlineAlert(
    string EventToken, string TaskId, string RowVersion, int AssignmentRevision,
    int RecipientId, int CurrentAssigneeId, string Title, DateOnly DueDate,
    TaskDisplayStatus Status, bool IsCancelled = false, bool IsConfirmed = false)
{
    public string? EventId { get; init; }
}

public sealed record DeadlineAlertSnapshot(DateTimeOffset ServerTime, IReadOnlyList<DeadlineAlert> Items);

public enum DeadlineConfirmationState { Confirmed, Changed, NotRecorded, Unknown }
// Confirmed means the source validated the original operation's successful
// receipt for this exact event/account. That proof survives a later list failure.
// Only Confirmed may omit Snapshot; other states require a current full snapshot.
// This is an in-process result, NOT a new HTTP receipt or persistence schema.
public sealed record DeadlineConfirmation(DeadlineConfirmationState State, DeadlineAlertSnapshot? Snapshot = null);

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
    // After validating success, a failed list GET returns Confirmed without a
    // snapshot, not Unknown/an exception. Keep the receipt/key/body in the source.
    // Current auth failures still throw 401/403; they never become list success.
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
        if (snapshot is null || snapshot.Items is null)
            throw new InvalidDataException("마감 알림 목록 형식이 올바르지 않습니다.");
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var tasks = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in snapshot.Items)
        {
            if (item is null || string.IsNullOrWhiteSpace(item.EventToken) || item.EventToken.Length > 256 ||
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
