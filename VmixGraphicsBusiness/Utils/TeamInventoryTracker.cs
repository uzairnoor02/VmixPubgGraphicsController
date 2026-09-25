using System.Collections.Concurrent;
using System.Text.Json;

namespace VmixGraphicsBusiness.Utils;

/// <summary>Carried throwables for one team, summed over its players. Serialised camelCase
/// (frag/smoke/molotov/stun) straight into the overlay's Last 4 cards.</summary>
public sealed class TeamThrowables
{
    public int Frag { get; set; }
    public int Smoke { get; set; }
    public int Molotov { get; set; }
    public int Stun { get; set; }
    /// <summary>When pcob last reported this team's inventory. pcob only returns the backpack of
    /// the team the observer is watching, so a team's numbers are "as last seen", refreshed each
    /// time the observer is on them.</summary>
    public DateTime SeenAtUtc { get; set; }
}

/// <summary>
/// Turns pcob's <c>getteambackpackinfo</c> into per-team throwable counts for the Last 4 cards.
///
/// Real shape (23 Sep 2026 recording):
/// <code>
/// {"teambackpackinfo":{"TeamBackPackList":[
///   {"602004":"Quality:0,Num:3,Worth:0", "602002":"Quality:0,Num:1,Worth:0", ...,
///    "MainWeapon1ID":104002, "PlayerKey":1700991685, "TeamID":5}, ... ]}}
/// </code>
/// One object per player of the OBSERVED team only; every item is a property named by its item
/// id, valued "Quality:q,Num:n,Worth:w". Throwable ids: 602004 frag, 602002 smoke, 602003
/// molotov, 602001 stun (602004 is also the ItemID getkillinfo reports for grenade kills).
///
/// Every parse problem is swallowed - this is decoration on the Last 4 cards and runs inside the
/// 1-second live tick.
/// </summary>
public sealed class TeamInventoryTracker
{
    private readonly ConcurrentDictionary<int, TeamThrowables> _byTeam = new();

    public const string FragId = "602004";
    public const string SmokeId = "602002";
    public const string MolotovId = "602003";
    public const string StunId = "602001";

    public void Reset() => _byTeam.Clear();

    public TeamThrowables? Get(int teamId) => _byTeam.TryGetValue(teamId, out var t) ? t : null;

    /// <summary>Parses one getteambackpackinfo response and records the teams in it. Returns how
    /// many teams were updated (0 for an empty or unparseable payload).</summary>
    public int Update(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var list = FindPlayerList(doc.RootElement);
            if (list is null) return 0;

            var totals = new Dictionary<int, TeamThrowables>();
            foreach (var player in list.Value.EnumerateArray())
            {
                if (player.ValueKind != JsonValueKind.Object) continue;
                var teamId = ReadInt(player, "TeamID");
                if (teamId is null) continue;

                if (!totals.TryGetValue(teamId.Value, out var t))
                    totals[teamId.Value] = t = new TeamThrowables { SeenAtUtc = DateTime.UtcNow };

                foreach (var prop in player.EnumerateObject())
                {
                    switch (prop.Name)
                    {
                        case FragId: t.Frag += ItemCount(prop.Value); break;
                        case SmokeId: t.Smoke += ItemCount(prop.Value); break;
                        case MolotovId: t.Molotov += ItemCount(prop.Value); break;
                        case StunId: t.Stun += ItemCount(prop.Value); break;
                    }
                }
            }

            foreach (var (teamId, t) in totals) _byTeam[teamId] = t;
            return totals.Count;
        }
        catch
        {
            return 0;
        }
    }

    private static JsonElement? FindPlayerList(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root;
        if (root.ValueKind != JsonValueKind.Object) return null;
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array) return prop.Value;
            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                var inner = FindPlayerList(prop.Value);
                if (inner is not null) return inner;
            }
        }
        return null;
    }

    private static int? ReadInt(JsonElement obj, string name)
    {
        foreach (var prop in obj.EnumerateObject())
        {
            if (!string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
            if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var n)) return n;
            if (prop.Value.ValueKind == JsonValueKind.String && int.TryParse(prop.Value.GetString(), out var s)) return s;
        }
        return null;
    }

    /// <summary>"Quality:0,Num:3,Worth:0" -> 3. A bare number is taken as the count.</summary>
    private static int ItemCount(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n)) return n;
        if (value.ValueKind != JsonValueKind.String) return 0;
        foreach (var part in (value.GetString() ?? "").Split(','))
        {
            var kv = part.Split(':');
            if (kv.Length == 2 && kv[0].Trim().Equals("Num", StringComparison.OrdinalIgnoreCase) && int.TryParse(kv[1].Trim(), out var num))
                return num;
        }
        return 0;
    }
}
