using System.Globalization;
using System.Text.Json;

namespace RemoteWork.WinForms.Infrastructure;

public sealed record WorkdayState(Guid WorkSessionId, DateOnly WorkDate,
    DateTimeOffset ServerTime, DateTimeOffset StartedAt, DateTimeOffset? NoonAt, bool NoonDue);

/// <summary>Current server work only, in the captured normal login. No scheduler,
/// token installation, monitoring, attendance write or fallback work ID.</summary>
public sealed class WorkdayStateConnection(ApiClient.Session session)
{
    public bool IsCurrent => session.IsCurrent;
    public CancellationToken Cancellation => session.Cancellation;

    public async Task<WorkdayState> ReadAsync(CancellationToken ct = default)
    {
        session.ThrowIfChanged();
        var value = await session.GetAsync<JsonElement>("device-identity/workday", ct).ConfigureAwait(false);
        session.ThrowIfChanged();
        ct.ThrowIfCancellationRequested();
        try
        {
            string[] names = ["server_time", "work_session_ref", "work_date", "state", "started_at", "noon_at", "noon_due"];
            if (value.ValueKind != JsonValueKind.Object || value.EnumerateObject().Count() != names.Length
                || names.Any(n => !value.TryGetProperty(n, out _))) throw Invalid();
            string? id = value.GetProperty("work_session_ref").GetString();
            if (!Guid.TryParseExact(id, "D", out var work) || work == Guid.Empty || work.ToString("D") != id
                || value.GetProperty("state").GetString() != "open") throw Invalid();
            if (!DateOnly.TryParseExact(value.GetProperty("work_date").GetString(), "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) || day.Year < 1000) throw Invalid();
            var now = Utc(value.GetProperty("server_time"));
            var started = Utc(value.GetProperty("started_at"));
            var noon = value.GetProperty("noon_at");
            DateTimeOffset? checkedAt = noon.ValueKind == JsonValueKind.Null ? null : Utc(noon);
            var due = value.GetProperty("noon_due").GetBoolean();
            var local = now.ToOffset(TimeSpan.FromHours(9));
            if (DateOnly.FromDateTime(local.DateTime) != day || started > now
                || DateOnly.FromDateTime(started.ToOffset(TimeSpan.FromHours(9)).DateTime) != day
                || checkedAt is not null && (checkedAt < started || checkedAt > now)
                || due != (checkedAt is null && local.Hour >= 11 && local.Hour < 12)) throw Invalid();
            return new(work, day, now, started, checkedAt, due);
        }
        catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException)
        { throw Invalid(); }
    }

    private static DateTimeOffset Utc(JsonElement value)
    {
        var text = value.GetString();
        if (text is null || !text.EndsWith('Z') || text.Any(char.IsWhiteSpace)
            || !DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
            || date.Offset != TimeSpan.Zero) throw Invalid();
        return date;
    }
    private static InvalidDataException Invalid() => new("서버의 현재 근무 정보를 확인할 수 없습니다. 다시 조회해 주세요.");
}
