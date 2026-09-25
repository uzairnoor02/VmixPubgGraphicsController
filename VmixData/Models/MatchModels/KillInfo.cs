using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VmixData.Models.MatchModels;

/// <summary>
/// One entry from pcob's `getkillinfo` - a knock or a kill, with both player names.
///
/// Real shape, confirmed from the 23 Sep 2026 match recording:
/// <code>
/// {"killInfo":[{"CauserName":"VB・XENON","VictimName":"8finNasWarūū",
///   "CauserUID":"5490959605","VictimUID":"5842598447","ItemID":"101003",
///   "ResultHealthStatus":"2","CurGameTime":"1836","Distance":81}]}
/// </code>
/// - the list is cumulative for the match and newest-first;
/// - ResultHealthStatus "1" = knock, "2" = kill (58 x "2" matched the match's kill total exactly);
/// - ItemID is the weapon (602004 = frag grenade, -1 = bleed-out / no weapon);
/// - key on UIDs, never names (one row carried another player's name with a real UID).
///
/// The previous model guessed killerName/killerTeamId/gameTime, so every real row came through as
/// "Unknown eliminated X" and knocks were announced as eliminations. Values that pcob sends as a
/// number in one build and a string in another are read either way.
/// </summary>
public class KillInfo
{
    [JsonPropertyName("CauserName")]
    public string? KillerName { get; set; }

    [JsonPropertyName("VictimName")]
    public string? VictimName { get; set; }

    [JsonPropertyName("CauserUID")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? KillerUid { get; set; }

    [JsonPropertyName("VictimUID")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? VictimUid { get; set; }

    /// <summary>Weapon / item id. 602004 = frag grenade, -1 = no weapon (bleed-out).</summary>
    [JsonPropertyName("ItemID")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? ItemId { get; set; }

    /// <summary>"1" = knock, "2" = kill.</summary>
    [JsonPropertyName("ResultHealthStatus")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? ResultHealthStatus { get; set; }

    /// <summary>Metres between killer and victim. Drives the "long range" achievement.</summary>
    [JsonPropertyName("Distance")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public double? Distance { get; set; }

    /// <summary>pcob's lobby clock (fight clock + the lobby time). Part of the dedupe key.</summary>
    [JsonPropertyName("CurGameTime")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? GameTime { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    public const string FragGrenadeItemId = "602004";

    /// <summary>A knock is not an elimination. Rows with no status at all (an older pcob) are
    /// treated as kills, which is what the field meant before it existed.</summary>
    public bool IsKill => string.IsNullOrEmpty(ResultHealthStatus) || ResultHealthStatus == "2";

    /// <summary>Stable identity for one row, to tell "already shown" from "new".</summary>
    public string DedupeKey() =>
        $"{KillerUid ?? KillerName}|{VictimUid ?? VictimName}|{GameTime}|{ResultHealthStatus}";

    /// <summary>A real elimination with someone to name on either side.</summary>
    public bool IsUsable() =>
        IsKill && (!string.IsNullOrWhiteSpace(KillerName) || !string.IsNullOrWhiteSpace(VictimName));
}

/// <summary>The `getkillinfo` envelope: <c>{ "killInfo": [ ... ] }</c>.</summary>
public class KillInfoList
{
    [JsonPropertyName("killInfo")]
    public List<KillInfo>? KillInfoListItems { get; set; }
}

/// <summary>Reads a JSON string, number or bool into a string (pcob is inconsistent about which
/// it sends for ids and statuses); writes it back as a string.</summary>
public sealed class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String: return reader.GetString();
            case JsonTokenType.Number:
                return reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
            case JsonTokenType.True: return "true";
            case JsonTokenType.False: return "false";
            case JsonTokenType.Null: return null;
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue(); else writer.WriteStringValue(value);
    }
}
