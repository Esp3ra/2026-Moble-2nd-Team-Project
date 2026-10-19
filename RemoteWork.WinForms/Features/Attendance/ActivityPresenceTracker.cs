namespace RemoteWork.WinForms.Features.Attendance;

public sealed record ActivityPresenceView(string Status, double QuietSeconds, string Detail);

/// <summary>Local display only. Unknown input breaks continuous quiet time.</summary>
public sealed class ActivityPresenceTracker
{
    public const double AwayAfterSeconds = 30;
    private double? cameraAt, lightAt, lightChangedAt, quietSince;
    private double lastActivityAt = double.NegativeInfinity;
    private string? lightObservationId, lightMotionId;
    private bool hand, cameraMotion;
    private string? lightError;
    private double? evaluatedAt;
    private double? lastLightPulseAt;
    private long? lightContinuity;

    public void Reset()
    {
        cameraAt = lightAt = lightChangedAt = quietSince = evaluatedAt = null;
        lastLightPulseAt = null;
        lightContinuity = null;
        lastActivityAt = double.NegativeInfinity;
        lightObservationId = lightMotionId = lightError = null;
        hand = cameraMotion = false;
    }

    public void InvalidateCamera() { cameraAt = quietSince = null; }

    public void ObserveCamera(bool handDetected, bool movement, double capturedAt,
        double? secondsSinceHand = null, double? secondsSinceMovement = null)
    {
        if (!double.IsFinite(capturedAt)) throw new ArgumentOutOfRangeException(nameof(capturedAt));
        if (cameraAt.HasValue && capturedAt - cameraAt > 5) quietSince = null;
        cameraAt = capturedAt;
        hand = handDetected;
        cameraMotion = movement;
        if (hand || cameraMotion) lastActivityAt = Math.Max(lastActivityAt, capturedAt);
        foreach (var age in new[] { secondsSinceHand, secondsSinceMovement })
            if (age is { } seconds && double.IsFinite(seconds) && seconds >= 0)
                lastActivityAt = Math.Max(lastActivityAt, capturedAt - seconds);
    }

    public void ObserveLight(LightMotionSample sample, double receivedAt)
    {
        if (lightContinuity.HasValue && lightContinuity != sample.Continuity) quietSince = null;
        lightContinuity = sample.Continuity;
        if (!sample.Available)
        {
            lightAt = quietSince = null;
            lightError = sample.Error ?? "조도센서 연결을 확인해 주세요.";
            return;
        }
        if (lightAt.HasValue && receivedAt - lightAt > 3) quietSince = null;
        if (lightChangedAt.HasValue && receivedAt - lightChangedAt > 3) quietSince = null;
        lightError = null;
        lightAt = receivedAt;
        if (lightObservationId != sample.ObservationId)
        {
            lightObservationId = sample.ObservationId;
            lightChangedAt = receivedAt;
        }
        if (sample.MotionId is { } id && id != lightMotionId)
        {
            lightMotionId = id;
            if (sample.SecondsSinceMotion is { } age)
            {
                // Use the response end as the latest possible event time, avoiding early away
                // due to request travel time. Freshness still uses the request start.
                lastLightPulseAt = receivedAt + sample.TransportSeconds - age;
                lastActivityAt = Math.Max(lastActivityAt, lastLightPulseAt.Value);
            }
        }
    }

    public ActivityPresenceView Evaluate(double now)
    {
        if (evaluatedAt.HasValue && (now - evaluatedAt > 3 || now < evaluatedAt)) quietSince = null;
        evaluatedAt = now;
        if (cameraAt is null || now - cameraAt > 5 || now < cameraAt)
        {
            quietSince = null;
            return new("확인 필요", 0, "카메라의 최신 영상을 확인해 주세요.");
        }
        if (lightAt is null || lightChangedAt is null || now - lightAt > 3 || now - lightChangedAt > 3 || now < lightAt)
        {
            quietSince = null;
            return new("확인 필요", 0, lightError ?? "조도센서의 최신 응답을 기다리고 있습니다.");
        }
        quietSince ??= now;
        quietSince = Math.Max(quietSince.Value, lastActivityAt);
        double quiet = Math.Max(0, now - quietSince.Value);
        string status = quiet >= AwayAfterSeconds ? "자리 비움" : "활동 중";
        string lightText = lastLightPulseAt.HasValue ? $"조도 변화 {Math.Max(0, now - lastLightPulseAt.Value):F1}초 전" : "조도 변화 없음";
        string signals = $"손 {(hand ? "있음" : "없음")} · 카메라 움직임 {(cameraMotion ? "있음" : "없음")} · {lightText}";
        string detail = quiet >= AwayAfterSeconds ? $"{signals}\n세 신호 모두 30초 이상 감지되지 않았습니다." :
            $"{signals}\n자리 비움까지 {Math.Ceiling(AwayAfterSeconds - quiet):0}초 · 감지 시 다시 30초부터 확인합니다.";
        return new(status, quiet, detail);
    }
}
