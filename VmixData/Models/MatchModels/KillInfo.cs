using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VmixData.Models.MatchModels;

/// <summary>
/// Real shape of pcob's `getkillinfo` response, confirmed against the 62-player, 16-team
/// recording (tools/FakePcob/recordings/20260923-232832 — see project doc
/// pcob-real-match-analysis.md). The envelope key is `killInfo` (not `killInfoList`).
/// </summary>
public class KillInfoWrapper
{
    [JsonPropertyName("killInfo")]
    public List<KillInfoRow> KillInfo { get; set; } = new();
}

public class KillInfoRow
{
    [JsonPropertyName("CauserName")]
    public string CauserName { get; set; }

    [JsonPropertyName("VictimName")]
    public string VictimName { get; set; }

    [JsonPropertyName("CauserUID")]
    public string CauserUID { get; set; }

    [JsonPropertyName("VictimUID")]
    public string VictimUID { get; set; }

    [JsonPropertyName("ItemID")]
    public string ItemID { get; set; }

    [JsonPropertyName("ResultHealthStatus")]
    public string ResultHealthStatus { get; set; }

    [JsonPropertyName("CurGameTime")]
    public string CurGameTime { get; set; }

    [JsonPropertyName("Distance")]
    public double Distance { get; set; }

    /// ResultHealthStatus: "1" = knock, "2" = kill. Confirmed: 58 x "2" rows == the match's total killNum.
    public bool IsKill => ResultHealthStatus == "2";
    public bool IsKnock => ResultHealthStatus == "1";

    /// 602004 = frag grenade, confirmed directly in the capture (10 grenade kills, all ItemID 602004).
    /// Vehicle/other ids seen in the capture (e.g. 1907066, 1961061) are NOT yet confirmed against a
    /// full item table -- don't switch vehicle-kill detection to ItemID until that's verified.
    public const string FragGrenadeItemId = "602004";
    public bool IsGrenadeKill => IsKill && ItemID == FragGrenadeItemId;

    public long CurGameTimeSeconds => long.TryParse(CurGameTime, out var t) ? t : 0;

    /// CauserName can read "Playzone" even while CauserUID is a real player's UID (seen once in the
    /// capture). Always key on UIDs, never on CauserName/VictimName, for anything that has to be correct.
    public string DedupeKey => $"{CauserUID}|{VictimUID}|{CurGameTime}|{ResultHealthStatus}";
}
