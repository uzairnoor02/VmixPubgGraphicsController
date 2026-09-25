using System.Text.Json;

namespace FakePcob;

/// Serves the recorded bodies of the endpoints `replay` does not rebuild (getkillinfo,
/// getteambackpackinfo, getcircleinfo, getobservingplayer), in step with the replay clock: for a
/// replay tick, the body recorded most recently at or before that tick's recorded time. This makes
/// the kill-feed names, the Last 4 throwables and the zone timer testable offline from a real
/// match capture. Endpoints missing from a recording fall back to the old synthetic responses.
public sealed class RecordedFeeds
{
    public static readonly string[] Endpoints = { "getkillinfo", "getteambackpackinfo", "getcircleinfo", "getobservingplayer" };

    private readonly string _folder;
    private readonly Dictionary<string, List<(long UnixMs, string File)>> _byEndpoint = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _bodyCache = new();

    private RecordedFeeds(string folder) { _folder = folder; }

    public bool Has(string endpoint) => _byEndpoint.TryGetValue(endpoint, out var l) && l.Count > 0;

    public static RecordedFeeds Load(string folder, IEnumerable<RecordIndexEntry> index)
    {
        var feeds = new RecordedFeeds(folder);
        foreach (var e in index)
        {
            if (e.File is null || e.Status is < 200 or >= 300) continue;
            if (!Endpoints.Contains(e.Endpoint, StringComparer.OrdinalIgnoreCase)) continue;
            if (!feeds._byEndpoint.TryGetValue(e.Endpoint, out var list)) feeds._byEndpoint[e.Endpoint] = list = new();
            list.Add((e.UnixMs, e.File));
        }
        foreach (var list in feeds._byEndpoint.Values) list.Sort((a, b) => a.UnixMs.CompareTo(b.UnixMs));
        return feeds;
    }

    /// The body recorded most recently at or before `unixMs`, or null if there is none yet.
    public string? BodyAt(string endpoint, long unixMs)
    {
        if (!_byEndpoint.TryGetValue(endpoint, out var list) || list.Count == 0) return null;
        int lo = 0, hi = list.Count - 1, found = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (list[mid].UnixMs <= unixMs) { found = mid; lo = mid + 1; } else hi = mid - 1;
        }
        if (found < 0) return null;
        var file = list[found].File;
        lock (_bodyCache)
        {
            if (_bodyCache.TryGetValue(file, out var cached)) return cached;
            var path = Path.Combine(_folder, file.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) return null;
            var body = File.ReadAllText(path);
            if (_bodyCache.Count > 256) _bodyCache.Clear();
            _bodyCache[file] = body;
            return body;
        }
    }
}
