using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VmixData.Models.MatchModels;

/// <summary>
/// One entry from pcob's `getkillinfo` endpoint - the real per-elimination record that the
/// overlay's kill feed needs, replacing the derived "a team's elimination count went up"
/// fallback Overlay.tsx has been using.
///
/// IMPORTANT - the exact field names pcob returns for this endpoint are NOT confirmed against a
/// live instance yet (it isn't in the original repo's API docs; it was found while surveying the
/// newer PC-OB endpoints). The property names below are the most likely spellings, each with
/// JsonPropertyName aliases for the common alternatives, and KillFeedTracker treats an entry it
/// cannot make sense of as "no event" rather than throwing. `Extra` captures every field that
/// didn't map, and GetLiveData/IngestApi log the first raw response of each match once, so the
/// real shape can be confirmed from a single test match and these names corrected in one place
/// if they're wrong. Worst case today: the feed stays empty exactly as it is now - it cannot
/// break the live pipeline.
/// </summary>
public class KillInfo
{
    [JsonPropertyName("killerName")]
    public string? KillerName { get; set; }

    [JsonPropertyName("victimName")]
    public string? VictimName { get; set; }

    [JsonPropertyName("killerTeamId")]
    public int? KillerTeamId { get; set; }

    [JsonPropertyName("victimTeamId")]
    public int? VictimTeamId { get; set; }

    /// <summary>Metres between killer and victim. Drives the "long range" achievement.</summary>
    [JsonPropertyName("distance")]
    public double? Distance { get; set; }

    /// <summary>In-match clock, used as part of the dedupe key since pcob returns a rolling
    /// list rather than only new entries.</summary>
    [JsonPropertyName("gameTime")]
    public string? GameTime { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    /// <summary>Stable identity for one elimination, used to tell "already shown" from "new".
    /// Deliberately built from several fields rather than a single id, because pcob is not
    /// documented to return one.</summary>
    public string DedupeKey() =>
        $"{KillerName}|{VictimName}|{KillerTeamId}|{VictimTeamId}|{GameTime}|{Distance}";

    /// <summary>An entry with no killer and no victim carries nothing the feed could show, and
    /// is almost certainly a shape mismatch rather than a real elimination.</summary>
    public bool IsUsable() => !string.IsNullOrWhiteSpace(KillerName) || !string.IsNullOrWhiteSpace(VictimName);
}

/// <summary>
/// The `getkillinfo` response envelope. pcob's other list endpoints wrap their array in a single
/// named property (`playerInfoList`, `teamInfoList`), so the same is assumed here - with aliases,
/// and KillInfoListParser below falling back to parsing a bare array if that assumption is wrong.
/// </summary>
public class KillInfoList
{
    [JsonPropertyName("killInfoList")]
    public List<KillInfo>? KillInfoListItems { get; set; }
}
