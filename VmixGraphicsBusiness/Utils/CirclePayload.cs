using System.Text.Json;
using VmixData.Models.MatchModels;

namespace VmixGraphicsBusiness.Utils;

/// <summary>
/// Turns a raw pcob <c>getcircleinfo</c> body into the overlay's "CircleUpdated" payload. Shared
/// by direct polling (GetLiveData) and agent mode (IngestApi), so both feed the Circle bar the
/// same way.
///
/// pcob's fields are all strings. Counter counts UP from 0 to MaxTime within a phase, so the
/// seconds remaining is MaxTime - Counter; CircleStatus 2 = zone moving, 0 = next zone announced
/// (waiting), 1 = before the first circle. The payload is normalised to "closing"/"waiting" and a
/// real countdown, with the raw values kept alongside for debugging.
/// </summary>
public static class CirclePayload
{
    /// <summary>Publishes the circle state; returns the circle index (0 when unknown).</summary>
    public static int Publish(MatchStateStore store, string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return 0;
        CircleInfo? circleInfo;
        try { circleInfo = JsonSerializer.Deserialize<CircleDataWrapper>(rawJson)?.CircleInfo; }
        catch { return 0; }
        if (circleInfo is null) return 0;

        int.TryParse(circleInfo.CircleIndex, out var circleIndex);
        int.TryParse(circleInfo.Counter, out var elapsed);
        int.TryParse(circleInfo.MaxTime, out var maxTime);
        var remaining = Math.Max(0, maxTime - elapsed);
        var closing = circleInfo.CircleStatus == "2";

        store.PublishGraphic(GraphicEvents.CircleUpdated, new
        {
            gameTime = circleInfo.GameTime,
            circleIndex = circleInfo.CircleIndex,
            circleStatus = closing ? "closing" : "waiting",
            counter = remaining.ToString(),
            maxTime = circleInfo.MaxTime,
            rawCircleStatus = circleInfo.CircleStatus,
            rawCounter = circleInfo.Counter,
        });
        return circleIndex;
    }
}
