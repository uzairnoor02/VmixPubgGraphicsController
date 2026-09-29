using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VmixData.Models.MatchModels;

/// <summary>
/// Real shape of pcob's `getallinfo`, confirmed directly against the 20260923-232832 capture
/// (tools/FakePcob/.../recordings/20260923-232832/getallinfo/*.json — 874 saved polls). It's a
/// superset of gettotalplayerlist + getteaminfolist (identical field casing on every field the
/// app reads -- both reuse LivePlayerInfo/TeamInfo below), plus the match clock and a real
/// pcob-side match id that survives an app restart.
/// </summary>
public class AllInfoWrapper
{
    [JsonPropertyName("allinfo")]
    public AllInfo AllInfo { get; set; }
}

public class AllInfo
{
    [JsonPropertyName("TotalPlayerList")]
    public List<LivePlayerInfo> TotalPlayerList { get; set; } = new();

    [JsonPropertyName("TeamInfoList")]
    public List<TeamInfo> TeamInfoList { get; set; } = new();

    [JsonPropertyName("GameStartTime")]
    public string GameStartTime { get; set; }

    [JsonPropertyName("FightingStartTime")]
    public string FightingStartTime { get; set; }

    [JsonPropertyName("FinishedStartTime")]
    public string FinishedStartTime { get; set; }

    [JsonPropertyName("GameID")]
    public string GameID { get; set; }

    [JsonPropertyName("CurrentTime")]
    public string CurrentTime { get; set; }

    /// pcob sets this to a real Unix timestamp the moment the match ends; confirmed in the
    /// capture: "0" for the entire 30-minute match, a real timestamp from the first poll after
    /// isingame flips to false. This is pcob's own authoritative "has this game ended" signal --
    /// it's true even if this app wasn't running to see the match actually finish.
    public bool HasEnded => !string.IsNullOrEmpty(FinishedStartTime) && FinishedStartTime != "0";
}
