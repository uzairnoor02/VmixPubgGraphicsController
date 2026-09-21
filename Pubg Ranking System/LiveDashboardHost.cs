using VmixGraphicsBusiness.Tenancy;
using VmixGraphicsBusiness.Observability;
using Pubg_Ranking_System.Tenancy;
using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using VmixGraphicsBusiness;
using VmixGraphicsBusiness.Utils;
using VmixData.Models;

namespace Pubg_Ranking_System
{
    /// <summary>
    /// SignalR hub the React dashboard connects to. Clients only ever receive pushes
    /// (TeamsUpdated / StatusChanged) - there are no client-callable methods yet, this is
    /// intentionally view-only for this first pass. Match control (start/end) stays on the
    /// WinForms app for now per the phased rollout plan.
    /// </summary>
    public class LiveDashboardHub : Hub
    {
        private readonly TournamentRegistry _registry;
        private readonly TenantScopeManager _scopes;

        public LiveDashboardHub(TournamentRegistry registry, TenantScopeManager scopes)
        {
            _registry = registry;
            _scopes = scopes;
        }

        /// <summary>
        /// Puts every connection into exactly one tournament's group, resolved from the overlay
        /// token the client connected with (<c>?t=</c>). Before this, every push went to
        /// Clients.All - which is correct for one event on one machine, and a cross-tenant leak
        /// the moment a second tournament exists on the same host: a browser watching tournament B
        /// would receive tournament A's live stats.
        ///
        /// A connection with no token, or an unrecognised one, lands in this install's own group.
        /// That is deliberately not an error: the dashboard itself connects without a token, and
        /// so does an overlay on a single-tenant install - which is why this change is invisible
        /// to the existing setup.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var token = Context.GetHttpContext()?.Request.Query["t"].ToString();
            var lookup = _registry.ResolveOverlayToken(token);
            var scope = lookup.Success ? _scopes.GetOrCreate(lookup.Tournament!) : _scopes.Default;

            await Groups.AddToGroupAsync(Context.ConnectionId, scope.HubGroup);
            await base.OnConnectedAsync();
        }
    }

    /// <summary>
    /// Request body for POST /api/auth/login.
    /// </summary>
    public record LoginRequest(string Key);

    /// <summary>
    /// A single transient thing that happened worth showing on the overlay - a kill-feed line, a
    /// full-screen "TEAM ELIMINATED" banner, an achievement popup (grenade elim, vehicle kill,
    /// airdrop loot, first kill, ...). One flexible shape instead of a separate endpoint/type per
    /// banner so a new achievement type can be added on the frontend alone. `Type` picks how the
    /// overlay renders it (e.g. "elimination", "teamEliminated", "achievement.grenadeElim") - see
    /// vmix-dashboard/src/Overlay.tsx for the full list this ships with.
    /// </summary>
    public record OverlayEvent(string Type, string? Title, string? Subtitle, string? ImageUrl, string? AccentColor, Dictionary<string, string>? Data);

    /// <summary>
    /// Hosts a small Kestrel server (SignalR hub + a couple of read-only REST endpoints) on the
    /// LAN, alongside the existing WinForms app - so anyone on the network can open Chrome and see
    /// live match stats without needing physical/RDP access to the graphics PC. This mirrors the
    /// same "run an embedded ASP.NET Core host on a background thread" pattern this app already
    /// uses for the Hangfire dashboard, just with its own port and SignalR instead.
    ///
    /// Scope of this first pass is intentionally view-only: it broadcasts live team stats and
    /// match status, and exposes a snapshot endpoint so a browser that connects mid-match sees
    /// current state immediately instead of waiting for the next tick. A POST /api/match/reset
    /// endpoint is included since Reset.ResetAll is already safe to call from anywhere. Starting
    /// and ending a match stay on the WinForms app for now - start_btn_Click has real confirmation
    /// dialogs (resume vs. restart vs. delete-and-restart a completed match) and stop_Click
    /// restarts the whole process, neither of which should be wired up to a web button without
    /// that flow being deliberately redesigned as an API contract first.
    /// </summary>
    public static class LiveDashboardHost
    {
        // Guarded with a lock because PublishLiveTeams/PublishMatchStatus can fire from a
        // background polling thread while a newly-connecting browser's GET request reads it.
        private static readonly object _snapshotLock = new();
        private static List<TeamLiveStats>? _lastTeams;
        private static string _lastStatus = "";

        // Set once the Kestrel host is up; lets any other code in the app push an overlay event
        // (kill feed line, achievement banner, ...) with one call, e.g.
        // LiveDashboardHost.BroadcastOverlayEvent(new OverlayEvent("achievement.grenadeElim",
        // "Grenade Elimination", playerName, photoUrl, null, null)); - this is the hook the actual
        // elimination/achievement detection logic (LiveStatsBusiness.SetPlayerAcheivments.cs etc.)
        // should call into next, once it's safe to touch with a compiler on hand.
        private static IHubContext<LiveDashboardHub>? _hubContext;

        // The SignalR group for this install's own tournament. Set when the host starts; until
        // then there is nowhere to broadcast to anyway.
        private static string? _defaultHubGroup;

        public static void BroadcastOverlayEvent(OverlayEvent overlayEvent, string? hubGroup = null)
        {
            // Defaults to this install's own group rather than Clients.All, so a preview fired
            // from one tournament's dashboard cannot appear on another tournament's live overlay.
            var group = hubGroup ?? _defaultHubGroup;
            if (group is null) return;
            _hubContext?.Clients.Group(group).SendAsync("OverlayEvent", overlayEvent);
        }

        public static void Start(IServiceProvider rootProvider, MatchStateStore matchState, IBackgroundJobClient backgroundJobClient, Reset reset, string urls = "http://0.0.0.0:5050")
        {
            matchState.LiveTeamsUpdated += teams =>
            {
                lock (_snapshotLock) { _lastTeams = teams; }
            };
            matchState.MatchStatusChanged += status =>
            {
                lock (_snapshotLock) { _lastStatus = status; }
            };

            var thread = new Thread(() =>
            {
                try
                {
                    var builder = WebApplication.CreateBuilder();
                    builder.WebHost.UseUrls(urls);

                    var configuration = rootProvider.GetRequiredService<IConfiguration>();
                    var overlayConfigStore = rootProvider.GetRequiredService<OverlayConfigStore>();

                    builder.Services.AddSignalR();
                    builder.Services.AddCors(options =>
                    {
                        // Wide open for the LAN dashboard on a first pass - this is a broadcast-only
                        // read surface today (plus one reset action), not something exposing secrets.
                        // Tighten this to specific origins once the dashboard has real auth.
                        options.AddDefaultPolicy(policy =>
                            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
                    });

                    // This is a second, separate WebApplication with its own DI container - it
                    // does NOT inherit rootProvider's registrations (see the EF Core note further
                    // down for the same trap). TenancyApi.cs and ObservabilityApi.cs's minimal-API
                    // endpoints take TournamentRegistry/TenantScopeManager as parameters and expect
                    // the framework to inject them as services, so they must be registered here too
                    // - otherwise the request-delegate factory can't resolve TenantScopeManager and
                    // startup fails with "Failure to infer one or more parameters" on any POST
                    // endpoint that also has a body parameter (e.g. /api/ingest/heartbeat), since it
                    // tries to fall back to treating it as a second inferred body parameter.
                    builder.Services.AddSingleton(rootProvider.GetRequiredService<TournamentRegistry>());
                    builder.Services.AddSingleton(rootProvider.GetRequiredService<TenantScopeManager>());

                    var app = builder.Build();
                    app.UseCors();

                    // Custom HTML graphics the client can upload from the web instead of asking
                    // for a code change - saved under state/custom-graphics and served statically
                    // at /graphics/<file>.html, so the URL to paste into a vMix Web Browser source
                    // stays stable across re-uploads of the same file name.
                    var graphicsDir = Path.Combine(AppContext.BaseDirectory, "state", "custom-graphics");
                    Directory.CreateDirectory(graphicsDir);
                    app.UseStaticFiles(new StaticFileOptions
                    {
                        FileProvider = new PhysicalFileProvider(graphicsDir),
                        RequestPath = "/graphics"
                    });

                    // Player photos. The WinForms path only ever handed vMix a local file path
                    // (ConfigGlobal.PlayerImages\{UId}.png), which a browser cannot load - so the
                    // web overlay's achievement banners have been falling back to a generic icon.
                    // Serving that same folder read-only at /player-images/{UId}.png lets the
                    // overlay show the real photo, with no change to where the files live or how
                    // they're named. Guarded: a missing or unconfigured folder skips the mount
                    // entirely rather than throwing at startup, and a player with no photo on disk
                    // just 404s, which the overlay already degrades from cleanly.
                    try
                    {
                        var playerImagesDir = ConfigGlobal.PlayerImages;
                        if (!string.IsNullOrWhiteSpace(playerImagesDir) && Directory.Exists(playerImagesDir))
                        {
                            app.UseStaticFiles(new StaticFileOptions
                            {
                                FileProvider = new PhysicalFileProvider(playerImagesDir),
                                RequestPath = "/player-images"
                            });
                        }
                        else
                        {
                            Console.WriteLine($"[dashboard] PlayerImages folder not found ('{playerImagesDir}') - achievement banners will use the fallback icon.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[dashboard] Could not serve player images: {ex.Message}");
                    }

                    // Serves the built React dashboard (vmix-dashboard's `npm run build` output)
                    // from this same host on "/" - one process, one port, no separate "npm run
                    // dev" window to keep open next to the WinForms app. Auto-detected relative to
                    // this dev repo's layout (the bin output sits 4 levels under the repo root);
                    // override with WebDashboard:DashboardDistPath in appsettings.json if that
                    // ever changes. If dist/index.html isn't found, this is skipped entirely and
                    // logged - every endpoint above still works fine without it, so a dashboard
                    // that hasn't been built yet can never take the live data API down with it.
                    var configuredDistPath = configuration["WebDashboard:DashboardDistPath"];
                    var dashboardDistPath = !string.IsNullOrWhiteSpace(configuredDistPath)
                        ? configuredDistPath
                        : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "vmix-dashboard", "dist"));

                    if (File.Exists(Path.Combine(dashboardDistPath, "index.html")))
                    {
                        var dashboardFileProvider = new PhysicalFileProvider(dashboardDistPath);
                        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = dashboardFileProvider });
                        app.UseStaticFiles(new StaticFileOptions { FileProvider = dashboardFileProvider });

                        // SPA fallback so a direct/hard-refresh hit on a client-side route (like
                        // /overlay, which vMix loads directly) gets index.html instead of a 404 -
                        // React Router... er, our hand-rolled pathname check in App.tsx then picks
                        // the right screen once index.html's JS runs.
                        app.MapFallback(async httpContext =>
                        {
                            var requestPath = httpContext.Request.Path.Value ?? "";
                            if (requestPath.StartsWith("/api") || requestPath.StartsWith("/hubs") || requestPath.StartsWith("/graphics"))
                            {
                                httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
                                return;
                            }
                            httpContext.Response.ContentType = "text/html";
                            await httpContext.Response.SendFileAsync(Path.Combine(dashboardDistPath, "index.html"));
                        });

                        Console.WriteLine($"LiveDashboardHost: serving web dashboard from {dashboardDistPath}");
                    }
                    else
                    {
                        Console.WriteLine($"LiveDashboardHost: dashboard build not found at {dashboardDistPath} - run 'npm run build' in vmix-dashboard/ to serve it from here. The API/SignalR endpoints still work without it.");
                    }

                    // Must run before anything that reads a tenant. Resolves the credential on
                    // the request - agent key, overlay token, or dashboard key - and never a body
                    // parameter; see TenantResolution.cs for why that distinction is structural.
                    app.UseTenantResolution();

                    app.MapHub<LiveDashboardHub>("/hubs/match");

                    // Tournaments and their generated credentials (TenancyApi.cs), and the
                    // logs/agent-liveness/health surface (ObservabilityApi.cs).
                    app.MapTenancyEndpoints();
                    app.MapObservabilityEndpoints(async ct =>
                    {
                        // Readiness probe passed in as a delegate so ObservabilityApi has no
                        // dependency on the EF context - and so an install that fell back to
                        // SQLite reports that honestly instead of this code assuming MySQL.
                        using var dbScope = rootProvider.CreateScope();
                        var dbContext = dbScope.ServiceProvider.GetRequiredService<vmix_graphicsContext>();
                        var reachable = await dbContext.Database.CanConnectAsync(ct);
                        var provider = dbContext.Database.ProviderName ?? "unknown provider";
                        return (reachable, (reachable ? "reachable: " : "cannot connect: ") + provider);
                    });

                    // Match control (start/stop/reports/tournament setup) - every action that used
                    // to be a Form1 button click, now REST endpoints. See MatchControlApi.cs.
                    var ingestCoordinator = rootProvider.GetRequiredService<IngestCoordinator>();
                    var tenantScopes = rootProvider.GetRequiredService<TenantScopeManager>();
                    app.MapMatchControlEndpoints(rootProvider, backgroundJobClient, ingestCoordinator);

                    // Ingest endpoint for VmixIngestAgent - the small program that runs on the
                    // customer's PC next to pcob when this application isn't on the same machine.
                    // See IngestApi.cs and VmixIngestAgent/.
                    app.MapIngestEndpoints(rootProvider, configuration, ingestCoordinator);

                    // Simple shared-key login for the web dashboard, mirroring the WinForms app's
                    // key-entry auth screen but without a hard dependency on Google Sheets. The key
                    // lives in appsettings.json (WebDashboard:AuthKey) instead of the React bundle
                    // so it can be changed without rebuilding the dashboard. This endpoint itself
                    // stays unauthenticated (that's the whole point - it's how you get the key
                    // validated in the first place) but every admin action endpoint below now
                    // requires the SAME key as a real `Authorization: Bearer <key>` header
                    // (DashboardAuth.cs / .RequireDashboardKey()) - the SPA sends it on every
                    // request once logged in (see vmix-dashboard/src/lib/api.ts), it's not just a
                    // one-time check the login screen forgets afterward.
                    app.MapPost("/api/auth/login", (LoginRequest request) =>
                    {
                        var expectedKey = configuration["WebDashboard:AuthKey"] ?? "1234";
                        var ok = !string.IsNullOrWhiteSpace(request?.Key) && request.Key.Trim() == expectedKey;
                        return ok
                            ? Results.Ok(new { ok = true })
                            : Results.Json(new { ok = false }, statusCode: StatusCodes.Status401Unauthorized);
                    });

                    // Team roster management for the web dashboard's "Teams" tab. Mirrors the
                    // WinForms JsonTeamDataService (Program.cs) exactly - same case-insensitive
                    // tournament/stage name matching, same "TeamId compared as string, scoped to
                    // StageId" team lookup - but headless (auto-create instead of a MessageBox
                    // confirmation, since there's no dialog to show on a web request) and using a
                    // short-lived DbContext from the pooled factory instead of a long-held scoped
                    // context, since this can be called at any point during a live match.
                    // dbFactory is resolved from rootProvider (the MAIN app's DI container),
                    // not taken as a minimal-API parameter - this second WebApplication's own
                    // builder.Services never registered EF Core at all (see Start()'s
                    // WebApplication.CreateBuilder() above), so asking the minimal API pipeline to
                    // inject it directly silently fails IServiceProviderIsService's check, gets
                    // misread as an inferred request-body parameter, and throws at startup instead
                    // of at first request - which is exactly what happened here before this fix:
                    // the entire second host (this whole file, the React dashboard, /overlay,
                    // everything) failed to start, silently, because Start()'s try/catch swallows
                    // the exception and only logs it to the console.
                    var dbFactory = rootProvider.GetRequiredService<IDbContextFactory<vmix_graphicsContext>>();

                    app.MapPost("/api/teams/load", async (HttpRequest request) =>
                    {
                        string rawJson;
                        using (var reader = new StreamReader(request.Body))
                        {
                            rawJson = await reader.ReadToEndAsync();
                        }

                        TournamentData? data;
                        try
                        {
                            data = JsonConvert.DeserializeObject<TournamentData>(rawJson);
                        }
                        catch (Exception ex)
                        {
                            return Results.Json(new { ok = false, error = $"Couldn't parse that JSON: {ex.Message}" }, statusCode: StatusCodes.Status400BadRequest);
                        }

                        if (data is null || string.IsNullOrWhiteSpace(data.TournamentName) || data.Stages is null || data.Stages.Count == 0)
                        {
                            return Results.Json(new { ok = false, error = "JSON must include a tournament_name and at least one stage with teams." }, statusCode: StatusCodes.Status400BadRequest);
                        }

                        await using var db = await dbFactory.CreateDbContextAsync();

                        var tournament = await db.Tournaments
                            .FirstOrDefaultAsync(t => t.Name.ToLower() == data.TournamentName.ToLower());
                        if (tournament is null)
                        {
                            tournament = new Tournament { Name = data.TournamentName };
                            db.Tournaments.Add(tournament);
                            await db.SaveChangesAsync();
                        }

                        var stagesSummary = new List<object>();
                        var totalAdded = 0;
                        var totalUpdated = 0;

                        foreach (var stageData in data.Stages)
                        {
                            if (string.IsNullOrWhiteSpace(stageData.StageName))
                            {
                                continue;
                            }

                            var stage = await db.Stages.FirstOrDefaultAsync(s =>
                                s.TournamentId == tournament.TournamentId && s.Name.ToLower() == stageData.StageName.ToLower());
                            if (stage is null)
                            {
                                stage = new Stage
                                {
                                    Name = stageData.StageName,
                                    TournamentId = tournament.TournamentId,
                                    NumDays = 1,
                                    NumTeams = stageData.Teams?.Count ?? 0
                                };
                                db.Stages.Add(stage);
                                await db.SaveChangesAsync();
                            }

                            var added = 0;
                            var updated = 0;

                            foreach (var teamData in stageData.Teams ?? new List<TeamData>())
                            {
                                var teamIdStr = teamData.TeamId.ToString();
                                var existingTeam = await db.Teams
                                    .FirstOrDefaultAsync(t => t.TeamId == teamIdStr && t.StageId == stage.StageId);

                                if (existingTeam is null)
                                {
                                    db.Teams.Add(new Team
                                    {
                                        TeamId = teamIdStr,
                                        TeamName = teamData.TeamName,
                                        StageId = stage.StageId,
                                        TournamentId = tournament.TournamentId
                                    });
                                    added++;
                                }
                                else if (existingTeam.TeamName != teamData.TeamName)
                                {
                                    existingTeam.TeamName = teamData.TeamName;
                                    updated++;
                                }
                            }

                            await db.SaveChangesAsync();
                            totalAdded += added;
                            totalUpdated += updated;
                            stagesSummary.Add(new { stage = stage.Name, teamsAdded = added, teamsUpdated = updated });
                        }

                        return Results.Ok(new
                        {
                            ok = true,
                            tournament = tournament.Name,
                            teamsAdded = totalAdded,
                            teamsUpdated = totalUpdated,
                            stages = stagesSummary
                        });
                    }).RequireDashboardKey();

                    // Current roster, grouped by tournament -> stage -> teams, for the Teams tab to
                    // render and for the overlay/graphics pages to pick a team's display name from.
                    app.MapGet("/api/teams", async () =>
                    {
                        await using var db = await dbFactory.CreateDbContextAsync();

                        var tournaments = await db.Tournaments.OrderByDescending(t => t.TournamentId).ToListAsync();
                        var stages = await db.Stages.ToListAsync();
                        var teams = await db.Teams.ToListAsync();

                        var result = tournaments.Select(t => new
                        {
                            tournamentId = t.TournamentId,
                            name = t.Name,
                            stages = stages.Where(s => s.TournamentId == t.TournamentId).Select(s => new
                            {
                                stageId = s.StageId,
                                name = s.Name,
                                teams = teams.Where(tm => tm.StageId == s.StageId)
                                    .OrderBy(tm => tm.TeamName)
                                    .Select(tm => new { id = tm.Id, teamId = tm.TeamId, teamName = tm.TeamName })
                            })
                        });

                        return Results.Json(result);
                    }).RequireDashboardKey();

                    app.MapGet("/api/match/teams", () =>
                    {
                        lock (_snapshotLock) { return Results.Json(_lastTeams ?? new List<TeamLiveStats>()); }
                    });

                    app.MapGet("/api/match/status", () =>
                    {
                        lock (_snapshotLock) { return Results.Json(new { status = _lastStatus }); }
                    });

                    // Overlay look/feel - chroma-key color and per-element show/hide toggles - read by
                    // the /overlay route on load and pushed live to it (and the dashboard's
                    // Overlay Settings page) over SignalR whenever it changes, so a client can
                    // recolor the chroma key or hide a panel mid-broadcast with zero manual
                    // intervention on the graphics PC.
                    app.MapGet("/api/overlay/config", () => Results.Json(overlayConfigStore.Get()));

                    app.MapPost("/api/overlay/config", (OverlayConfig config) =>
                    {
                        if (string.IsNullOrWhiteSpace(config?.ChromaKeyColor))
                        {
                            return Results.Json(new { ok = false, error = "chromaKeyColor is required." }, statusCode: StatusCodes.Status400BadRequest);
                        }

                        overlayConfigStore.Update(config);
                        return Results.Ok(new { ok = true });
                    }).RequireDashboardKey();

                    app.MapGet("/api/graphics", () =>
                    {
                        var files = Directory.Exists(graphicsDir)
                            ? Directory.GetFiles(graphicsDir, "*.html")
                                .Select(p => new FileInfo(p))
                                .OrderByDescending(fi => fi.LastWriteTimeUtc)
                                .Select(fi => new
                                {
                                    name = fi.Name,
                                    url = $"/graphics/{fi.Name}",
                                    sizeBytes = fi.Length,
                                    uploadedAtUtc = fi.LastWriteTimeUtc
                                })
                            : Enumerable.Empty<object>();

                        return Results.Json(files);
                    }).RequireDashboardKey();

                    // Deliberately restricted to .html files only (no arbitrary uploads) since
                    // these are meant to be pasted straight into a vMix Web Browser source, not a
                    // general file store - and Path.GetFileName + an explicit ".." check keep an
                    // uploaded name from escaping the graphics folder.
                    app.MapPost("/api/graphics/upload", async (IFormFile? file) =>
                    {
                        if (file is null || file.Length == 0)
                        {
                            return Results.Json(new { ok = false, error = "No file received." }, statusCode: StatusCodes.Status400BadRequest);
                        }

                        var safeName = Path.GetFileName(file.FileName);
                        if (string.IsNullOrWhiteSpace(safeName) || !safeName.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                        {
                            return Results.Json(new { ok = false, error = "Only .html files are accepted." }, statusCode: StatusCodes.Status400BadRequest);
                        }
                        if (safeName.Contains("..") || Path.GetInvalidFileNameChars().Any(safeName.Contains))
                        {
                            return Results.Json(new { ok = false, error = "Invalid file name." }, statusCode: StatusCodes.Status400BadRequest);
                        }

                        const long maxBytes = 5 * 1024 * 1024; // 5 MB - generous for an overlay HTML/CSS/JS bundle, small enough to not be a DoS vector.
                        if (file.Length > maxBytes)
                        {
                            return Results.Json(new { ok = false, error = "File is too large (5 MB max)." }, statusCode: StatusCodes.Status400BadRequest);
                        }

                        var destPath = Path.Combine(graphicsDir, safeName);
                        await using (var stream = File.Create(destPath))
                        {
                            await file.CopyToAsync(stream);
                        }

                        return Results.Ok(new { ok = true, name = safeName, url = $"/graphics/{safeName}" });
                    }).RequireDashboardKey();

                    app.MapDelete("/api/graphics/{name}", (string name) =>
                    {
                        var safeName = Path.GetFileName(name);
                        if (string.IsNullOrWhiteSpace(safeName))
                        {
                            return Results.Json(new { ok = false, error = "Invalid file name." }, statusCode: StatusCodes.Status400BadRequest);
                        }

                        var targetPath = Path.Combine(graphicsDir, safeName);
                        if (!File.Exists(targetPath))
                        {
                            return Results.Json(new { ok = false, error = "File not found." }, statusCode: StatusCodes.Status404NotFound);
                        }

                        File.Delete(targetPath);
                        return Results.Ok(new { ok = true });
                    }).RequireDashboardKey();

                    app.MapPost("/api/match/reset", async () =>
                    {
                        await reset.ResetAll(backgroundJobClient);
                        return Results.Ok(new { ok = true });
                    }).RequireDashboardKey();

                    app.MapPost("/api/overlay/event", (OverlayEvent overlayEvent) =>
                    {
                        BroadcastOverlayEvent(overlayEvent);
                        return Results.Ok(new { ok = true });
                    }).RequireDashboardKey();

                    // Push every update straight to connected clients as it happens.
                    var hubContext = app.Services.GetRequiredService<IHubContext<LiveDashboardHub>>();
                    _hubContext = hubContext;

                    // These subscriptions are on the singleton stores, which belong to this
                    // install's own tournament - so they publish to its group, not to every
                    // connected client. On a single-tenant install every client is in this group,
                    // making the change behaviour-preserving there.
                    var defaultGroup = tenantScopes.Default.HubGroup;
                    _defaultHubGroup = defaultGroup;

                    // Additional tournaments get the same core feeds wired to their own group as
                    // their scope is created. The achievement/elimination banners below stay on
                    // the default scope for now: those subscriptions carry real transformation
                    // logic, and duplicating it per tenant belongs with per-tenant match control
                    // rather than being copied blind here.
                    tenantScopes.ScopeCreated += newScope =>
                    {
                        if (newScope.IsDefault) return;
                        var group = newScope.HubGroup;
                        newScope.MatchState.LiveTeamsUpdated += teams =>
                        {
                            _ = hubContext.Clients.Group(group).SendAsync("TeamsUpdated", teams);
                        };
                        newScope.MatchState.Top4RankingsUpdated += teams =>
                        {
                            _ = hubContext.Clients.Group(group).SendAsync("Top4Updated", teams);
                        };
                        newScope.MatchState.MatchStatusChanged += status =>
                        {
                            _ = hubContext.Clients.Group(group).SendAsync("StatusChanged", status);
                        };
                        newScope.OverlayConfig.ConfigChanged += config =>
                        {
                            _ = hubContext.Clients.Group(group).SendAsync("OverlayConfigChanged", config);
                        };
                    };

                    // Logs are pushed as they happen so the Logs tab shows a warning the moment it
                    // is raised, rather than on its next poll.
                    tenantScopes.LogAppended += logRecord =>
                    {
                        var target = string.IsNullOrEmpty(logRecord.TournamentId)
                            ? defaultGroup
                            : TenantScope.GroupFor(logRecord.TournamentId!);
                        _ = hubContext.Clients.Group(target).SendAsync("LogAppended", logRecord);
                    };
                    matchState.LiveTeamsUpdated += teams =>
                    {
                        _ = hubContext.Clients.Group(defaultGroup).SendAsync("TeamsUpdated", teams);
                    };
                    matchState.Top4RankingsUpdated += teams =>
                    {
                        _ = hubContext.Clients.Group(defaultGroup).SendAsync("Top4Updated", teams);
                    };
                    matchState.MatchStatusChanged += status =>
                    {
                        _ = hubContext.Clients.Group(defaultGroup).SendAsync("StatusChanged", status);
                    };
                    overlayConfigStore.ConfigChanged += config =>
                    {
                        _ = hubContext.Clients.Group(defaultGroup).SendAsync("OverlayConfigChanged", config);
                    };
                    // Real achievement detection (SetPlayerAcheivments.cs, in VmixGraphicsBusiness)
                    // raises this through MatchStateStore rather than calling BroadcastOverlayEvent
                    // directly, since that project can't reference this one (Pubg Ranking System
                    // depends on VmixGraphicsBusiness, not the other way around) - this subscription
                    // is the hand-off point. Title is left unset so the overlay falls back to its
                    // own label for the achievement type; Subtitle carries who/which team.
                    matchState.AchievementTriggered += achievement =>
                    {
                        var subtitle = string.IsNullOrWhiteSpace(achievement.TeamTag)
                            ? achievement.PlayerName
                            : $"{achievement.PlayerName} ({achievement.TeamTag})";
                        // Relative URL on purpose: the overlay is served from this same host, and
                        // a relative path keeps working whether it's opened as localhost, a LAN
                        // address, or a hostname, without knowing which one vMix used.
                        var photoUrl = string.IsNullOrWhiteSpace(achievement.PlayerUid)
                            ? null
                            : $"/player-images/{achievement.PlayerUid}.png";
                        BroadcastOverlayEvent(new OverlayEvent(achievement.Type, null, subtitle, photoUrl, null, null));
                    };

                    // Real per-elimination feed, replacing the overlay's derived fallback. Type
                    // "elimination" is what Overlay.tsx already routes into the feed list.
                    matchState.KillDetected += kill =>
                    {
                        var detail = kill.Distance is > 0 ? $"{Math.Round(kill.Distance.Value)}m" : null;
                        if (kill.IsLongRange) detail = detail is null ? "long range" : $"{detail} — long range";
                        BroadcastOverlayEvent(new OverlayEvent(
                            "elimination",
                            $"{kill.KillerName} eliminated {kill.VictimName}",
                            detail, null, null, null));
                    };
                    matchState.TeamEliminated += teamEliminatedEvent =>
                    {
                        BroadcastOverlayEvent(new OverlayEvent("teamEliminated", "TEAM ELIMINATED", teamEliminatedEvent.TeamName, null, null, null));
                    };

                    app.Run();
                }
                catch (Exception ex)
                {
                    // This host is a bonus surface, not the critical path - a failure to bind its
                    // port (e.g. already in use) must never take down match control on WinForms.
                    Console.WriteLine($"LiveDashboardHost failed to start: {ex.Message}");
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }
    }
}
