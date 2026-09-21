using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using VmixData.Models.MatchModels;

namespace VmixGraphicsBusiness.Utils;

/// <summary>
/// Turns pcob's rolling `getkillinfo` list into "which eliminations are NEW since last tick",
/// which is what the overlay's kill feed actually needs. pcob returns the match's kills so far
/// on every poll, not a delta, so without this every tick would re-announce every kill.
///
/// Deliberately total: any parse failure, shape mismatch or unexpected payload yields zero new
/// events rather than an exception. This sits in the 1-second live path, and a kill feed that
/// stays empty is a cosmetic problem, while an exception here would take a tick's whole stats
/// update down with it.
///
/// One instance per match - Reset() is called when a match starts so kills from a previous match
/// can never leak into the next one's feed.
/// </summary>
public class KillFeedTracker
{
    private readonly object _lock = new();
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private bool _loggedRawSample;

    /// <summary>Distance in metres at or above which a kill also counts as "long range". Matches
    /// the Studio's long_range achievement; a starting point, not a calibrated number.</summary>
    public const double LongRangeMetres = 150;

    /// <summary>True the first time a match sees a raw payload, so the caller can log exactly one
    /// sample per match to confirm the field names above against a real pcob instance without
    /// spamming a line every second for thirty minutes.</summary>
    public bool ShouldLogRawSample()
    {
        lock (_lock)
        {
            if (_loggedRawSample) return false;
            _loggedRawSample = true;
            return true;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _seen.Clear();
            _loggedRawSample = false;
        }
    }

    /// <summary>
    /// Parses a raw getkillinfo body and returns only the entries not seen before in this match.
    /// Handles both the assumed `{ "killInfoList": [...] }` envelope and a bare `[...]` array,
    /// because the real shape is unconfirmed.
    /// </summary>
    public List<KillInfo> GetNewKills(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return new List<KillInfo>();

        List<KillInfo> parsed;
        try
        {
            parsed = ParseFlexible(rawJson);
        }
        catch
        {
            // Unparseable payload: no feed events, no exception into the live loop.
            return new List<KillInfo>();
        }

        var fresh = new List<KillInfo>();
        lock (_lock)
        {
            foreach (var kill in parsed)
            {
                if (!kill.IsUsable()) continue;
                var key = kill.DedupeKey();
                if (_seen.Add(key)) fresh.Add(kill);
            }
        }
        return fresh;
    }

    private static List<KillInfo> ParseFlexible(string rawJson)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        using var doc = JsonDocument.Parse(rawJson);

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            return JsonSerializer.Deserialize<List<KillInfo>>(rawJson, options) ?? new List<KillInfo>();
        }

        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            var envelope = JsonSerializer.Deserialize<KillInfoList>(rawJson, options);
            if (envelope?.KillInfoListItems is { Count: > 0 }) return envelope.KillInfoListItems;

            // Envelope property wasn't the assumed name - take the first array-of-objects
            // property instead, which covers any single-list spelling pcob might use.
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Array) continue;
                var list = JsonSerializer.Deserialize<List<KillInfo>>(prop.Value.GetRawText(), options);
                if (list is { Count: > 0 }) return list;
            }
        }

        return new List<KillInfo>();
    }

    /// <summary>Feed line for one elimination, e.g. "AsiCASANOVA eliminated R3G-ROSHAAN".</summary>
    public static string DescribeKill(KillInfo kill)
    {
        var killer = string.IsNullOrWhiteSpace(kill.KillerName) ? "Unknown" : kill.KillerName;
        var victim = string.IsNullOrWhiteSpace(kill.VictimName) ? "an opponent" : kill.VictimName;
        return $"{killer} eliminated {victim}";
    }

    /// <summary>Supporting line - the distance, when pcob reported one.</summary>
    public static string? DescribeKillDetail(KillInfo kill)
        => kill.Distance is > 0 ? $"{Math.Round(kill.Distance.Value)}m" : null;

    public static bool IsLongRange(KillInfo kill) => kill.Distance is >= LongRangeMetres;
}
