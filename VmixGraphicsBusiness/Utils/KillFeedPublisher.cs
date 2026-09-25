namespace VmixGraphicsBusiness.Utils;

/// <summary>
/// Applies one raw getkillinfo response to a tournament's state: new eliminations go to the
/// overlay's kill feed, and each kill is remembered (killer -> victim) so the FIRST BLOOD /
/// grenade / vehicle banners can name the victim. Shared by direct polling (GetLiveData) and
/// agent mode (IngestApi).
///
/// Kill COUNTS never come from here - standings and achievements keep using the player list's
/// own counters. This is names only, and every failure is silent.
/// </summary>
public static class KillFeedPublisher
{
    public static void Apply(MatchStateStore store, KillFeedTracker tracker, string? rawJson)
    {
        if (rawJson is null) return;
        store.MarkKillFeedAvailable();

        // pcob lists newest first; publish oldest first so the feed and the "last victim" map end
        // up in real order when several kills land in one poll.
        var fresh = tracker.GetNewKills(rawJson);
        for (int i = fresh.Count - 1; i >= 0; i--)
        {
            var kill = fresh[i];
            store.RecordKill(kill.KillerUid, kill.KillerName, kill.VictimName);
            store.PublishKill(new LiveKillEvent(
                kill.KillerName ?? "Unknown",
                kill.VictimName ?? "an opponent",
                kill.Distance,
                KillFeedTracker.IsLongRange(kill)));
        }
    }
}
