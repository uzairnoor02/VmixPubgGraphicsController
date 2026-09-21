using System.Text.Json;

namespace FakePcob;

/// Loads real end-of-match captures from seed/*.json at runtime. Per the hard rules in
/// PHASE-1-FAKE-PCOB.md B1, this code must never be the thing that "opens" a seed JSON in the
/// sense of putting its contents in an LLM's context - it only runs at `dotnet run` time, reading
/// straight off disk, exactly like any other data file a program loads.
public static class Seeds
{
    /// Maps the short keys used on the command line to the actual seed filenames (see
    /// seed/SEEDS.md). d3m2's teamIds are intentionally non-contiguous (2-9, 18-25).
    private static readonly Dictionary<string, string> FileByKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["m1"] = "pmsc_s2_q2_finals_m1.json",
        ["m2"] = "pmsc_s2_q2_finals_m2.json",
        ["m3"] = "pmsc_s2_q2_finals_m3.json",
        ["m4"] = "pmsc_s2_q2_finals_m4.json",
        ["d3m2"] = "tournament_d3m2.json",
    };

    public static IReadOnlyCollection<string> Keys => FileByKey.Keys;

    public static string SeedDir => Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "seed") is var rel && Directory.Exists(rel)
        ? rel
        : Path.Combine(Directory.GetCurrentDirectory(), "seed");

    private static string ResolvePath(string fileName)
    {
        // Prefer a "seed" folder next to the running exe (published/xcopy layout); fall back to
        // one next to the .csproj (dotnet run from the project's own directory), then finally the
        // current working directory - so `dotnet run` works from either tools/FakePcob or the
        // repo root without extra flags.
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "seed", fileName),
            Path.Combine(FindProjectDir(), "seed", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "seed", fileName),
            Path.Combine(Directory.GetCurrentDirectory(), "tools", "FakePcob", "seed", fileName),
        };
        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }
        throw new FileNotFoundException($"Seed file '{fileName}' not found. Looked in: {string.Join(", ", candidates)}");
    }

    private static string FindProjectDir()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 6 && dir is not null; i++)
        {
            if (File.Exists(Path.Combine(dir, "FakePcob.csproj"))) return dir;
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }
        return Directory.GetCurrentDirectory();
    }

    public static List<SeedPlayer> Load(string matchKey)
    {
        if (!FileByKey.TryGetValue(matchKey, out var fileName))
        {
            throw new ArgumentException($"Unknown seed key '{matchKey}'. Known keys: {string.Join(", ", FileByKey.Keys)}");
        }
        var path = ResolvePath(fileName);
        var json = File.ReadAllText(path);
        var envelope = JsonSerializer.Deserialize<SeedEnvelope>(json)
            ?? throw new InvalidDataException($"Seed file '{fileName}' did not deserialize to a playerInfoList envelope.");
        if (envelope.PlayerInfoList.Count == 0)
        {
            throw new InvalidDataException($"Seed file '{fileName}' has an empty playerInfoList.");
        }
        return envelope.PlayerInfoList;
    }
}
