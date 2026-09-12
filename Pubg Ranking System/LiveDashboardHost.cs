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

        public static void BroadcastOverlayEvent(OverlayEvent overlayEvent)
        {
            _hubContext?.Clients.All.SendAsync("OverlayEvent", overlayEvent);
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

                    app.MapHub<LiveDashboardHub>("/hubs/match");

                    // Match control (start/stop/reports/tournament setup) - every action that used
                    // to be a Form1 button click, now REST endpoints. See MatchControlApi.cs.
                    app.MapMatchControlEndpoints(rootProvider, backgroundJobClient);

                    // Simple shared-key login for the web dashboard, mirroring the WinForms app's
                    // key-entry auth screen but without a hard dependency on Google Sheets - this
                    // is a LAN-only, view-only surface, so a single configured key is enough for
                    // now. The key lives in appsettings.json (WebDashboard:AuthKey) instead of the
                    // React bundle so it can be changed without rebuilding the dashboard; defaults
                    // to "1234" if unset. This checks the key only - it does not gate the other
                    // endpoints above, which stay open for this first pass same as before.
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
                    app.MapPost("/api/teams/load", async (HttpRequest request, IDbContextFactory<vmix_graphicsContext> dbFactory) =>
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
                    });

                    // Current roster, grouped by tournament -> stage -> teams, for the Teams tab to
                    // render and for the overlay/graphics pages to pick a team's display name from.
                    app.MapGet("/api/teams", async (IDbContextFactory<vmix_graphicsContext> dbFactory) =>
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
                    });

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
                    });

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
                    });

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
                    });

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
                    });

                    app.MapPost("/api/match/reset", async () =>
                    {
                        await reset.ResetAll(backgroundJobClient);
                        return Results.Ok(new { ok = true });
                    });

                    app.MapPost("/api/overlay/event", (OverlayEvent overlayEvent) =>
                    {
                        BroadcastOverlayEvent(overlayEvent);
                        return Results.Ok(new { ok = true });
                    });

                    // Push every update straight to connected clients as it happens.
                    var hubContext = app.Services.GetRequiredService<IHubContext<LiveDashboardHub>>();
                    _hubContext = hubContext;
                    matchState.LiveTeamsUpdated += teams =>
                    {
                        _ = hubContext.Clients.All.SendAsync("TeamsUpdated", teams);
                    };
                    matchState.MatchStatusChanged += status =>
                    {
                        _ = hubContext.Clients.All.SendAsync("StatusChanged", status);
                    };
                    overlayConfigStore.ConfigChanged += config =>
                    {
                        _ = hubContext.Clients.All.SendAsync("OverlayConfigChanged", config);
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
                        BroadcastOverlayEvent(new OverlayEvent(achievement.Type, null, subtitle, null, null, null));
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
