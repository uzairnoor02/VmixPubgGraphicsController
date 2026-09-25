// FakePcob - a standalone replay/record harness that stands in for a real PCOB server so the
// vMix overlay stack can be exercised end to end with no real tournament running. See
// docs/testing/PHASE-1-FAKE-PCOB.md for the full spec. Verbs: serve, record, dump, assets.

using FakePcob;

var verb = args.Length > 0 ? args[0] : "";
var opts = Args.Parse(args.Skip(1).ToArray());

switch (verb)
{
    case "serve":
        return await ServeCommand.RunAsync(opts);
    case "record":
        return await RecordCommand.RunAsync(opts);
    case "dump":
        return await DumpCommand.RunAsync(opts);
    case "assets":
        return await AssetsCommand.RunAsync(opts);
    default:
        PrintUsage();
        return verb.Length == 0 ? 1 : 1;
}

static void PrintUsage()
{
    Console.WriteLine("FakePcob - fake PCOB server / match replay harness");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run -- serve --match m1 [--speed 1] [--paused] [--port 10086] [--push http://host/path]");
    Console.WriteLine("  dotnet run -- serve --scenario scripted [--seed 42] --match m1 [--speed 1] [--port 10086]");
    Console.WriteLine("  dotnet run -- serve --replay recordings/<folder> [--speed 1] [--port 10086]");
    Console.WriteLine("  dotnet run -- serve --match m1 --routes merged|split   (default: split)");
    Console.WriteLine("  dotnet run -- record --upstream http://<real-pcob-ip>:<port> [--poll-only] [--poll 2000] [--port 10087] [--out recordings]");
    Console.WriteLine("  dotnet run -- dump --match m1 --out frames [--scenario scripted]");
    Console.WriteLine("  dotnet run -- assets --out out [--from-seed m1]");
    Console.WriteLine();
    Console.WriteLine($"Known seed keys: {string.Join(", ", Seeds.Keys)}");
}
