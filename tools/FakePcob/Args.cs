namespace FakePcob;

/// Minimal `--key value` / `--flag` parser. No third-party CLI package - this whole tool ships
/// with zero NuGet packages (Task 2).
public sealed class Args
{
    private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

    public static Args Parse(string[] args)
    {
        var a = new Args();
        for (int i = 0; i < args.Length; i++)
        {
            var tok = args[i];
            if (!tok.StartsWith("--")) continue;
            var key = tok[2..];
            string? val = null;
            if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
            {
                val = args[++i];
            }
            a._values[key] = val;
        }
        return a;
    }

    public bool Has(string key) => _values.ContainsKey(key);
    public string Get(string key, string fallback) => _values.TryGetValue(key, out var v) && v is not null ? v : fallback;
    public string? GetOrNull(string key) => _values.TryGetValue(key, out var v) ? v : null;
    public int GetInt(string key, int fallback) => _values.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : fallback;
    public double GetDouble(string key, double fallback) => _values.TryGetValue(key, out var v) && double.TryParse(v, out var d) ? d : fallback;
}
