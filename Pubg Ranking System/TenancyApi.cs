using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Pubg_Ranking_System.Tenancy;
using VmixGraphicsBusiness.Tenancy;

namespace Pubg_Ranking_System
{
    public record CreateTournamentRequest(string Name, string? OrgId = null);
    public record UpdateTournamentRequest(string? Name = null, bool? Enabled = null, DateTime? ScheduledStartUtc = null, DateTime? ScheduledEndUtc = null);
    public record RotateKeyRequest(int? GraceMinutes = null, string? Label = null);

    /// <summary>
    /// Tournament and credential management for the dashboard's Tournaments tab.
    ///
    /// The shape to notice: <b>a newly issued agent or operator key is returned exactly once</b>,
    /// in the response to the call that created it, and is never retrievable afterwards. Overlay
    /// tokens are the deliberate exception — they live in a URL the operator re-copies into vMix
    /// for the life of the event, so <c>GET</c> returns them in full. Everything else comes back
    /// masked.
    ///
    /// Issuing keys automatically is the point of this API. Today both credentials this system
    /// uses are hand-typed strings in <c>appsettings.json</c> (<c>WebDashboard:AuthKey</c>,
    /// defaulting to "1234", and <c>Agent:IngestKey</c>) — shared across every user, never
    /// rotated, and impossible to revoke for one customer without locking out all of them.
    /// </summary>
    public static class TenancyApi
    {
        public static void MapTenancyEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/api/tenancy").RequireTenantAdmin();

            // What credential am I holding, and what does it open? The SPA calls this after login
            // to decide whether to show install-wide controls or scope itself to one tournament.
            group.MapGet("/whoami", (HttpContext http) =>
            {
                var tenant = http.GetTenant();
                return Results.Json(new
                {
                    kind = tenant.Kind.ToString(),
                    tournamentId = tenant.TournamentId,
                    tournamentName = tenant.Scope?.TournamentName,
                    canAdministerAll = tenant.CanAdministerAllTournaments,
                    correlationId = tenant.CorrelationId
                });
            });

            group.MapGet("/tournaments", (HttpContext http, TournamentRegistry registry, TenantScopeManager scopes) =>
            {
                var tenant = http.GetTenant();

                // An operator key scoped to one tournament must not be able to enumerate the
                // others. Same list endpoint, different visibility — enforced here rather than
                // relying on the UI to ask for the right thing.
                var visible = tenant.CanAdministerAllTournaments
                    ? registry.All()
                    : registry.All().Where(t => t.Id == tenant.TournamentId).ToList();

                return Results.Json(visible.Select(t => Describe(t, scopes, http)).ToList());
            });

            group.MapPost("/tournaments", (HttpContext http, CreateTournamentRequest? request,
                                           TournamentRegistry registry, TenantScopeManager scopes) =>
            {
                if (!http.GetTenant().CanAdministerAllTournaments)
                    return Results.Json(new { ok = false, error = "Creating tournaments requires the install-wide dashboard key." },
                        statusCode: StatusCodes.Status403Forbidden);

                if (string.IsNullOrWhiteSpace(request?.Name))
                    return Results.BadRequest(new { ok = false, error = "Name is required." });

                var created = registry.CreateWithKeys(request!.Name, request.OrgId);
                scopes.GetOrCreate(created.Record);

                scopes.Publish("Information", "tenancy", "tournament created", created.Record.Id,
                    properties: new Dictionary<string, string> { ["name"] = created.Record.Name });

                // The agent key is shown here and nowhere else, so the response says so explicitly
                // rather than leaving the caller to discover it by coming back for it later.
                return Results.Json(new
                {
                    ok = true,
                    tournament = Describe(created.Record, scopes, http),
                    agentKeyOnce = created.AgentKey,
                    note = "The agent key is shown once. Copy it into the ingest agent's configuration now; it cannot be retrieved later."
                });
            });

            group.MapPost("/tournaments/{id}", (HttpContext http, string id, UpdateTournamentRequest? request,
                                                TournamentRegistry registry, TenantScopeManager scopes) =>
            {
                if (!CanTouch(http, id)) return Forbidden();
                if (registry.Get(id) is null) return Results.NotFound(new { ok = false, error = "Unknown tournament." });

                if (!string.IsNullOrWhiteSpace(request?.Name)) registry.Rename(id, request!.Name!);
                if (request?.Enabled is bool enabled) registry.SetEnabled(id, enabled);
                if (request?.ScheduledStartUtc is not null || request?.ScheduledEndUtc is not null)
                    registry.SetSchedule(id, request.ScheduledStartUtc, request.ScheduledEndUtc);

                var updated = registry.Get(id)!;
                scopes.Publish("Information", "tenancy", "tournament updated", id);
                return Results.Json(new { ok = true, tournament = Describe(updated, scopes, http) });
            });

            group.MapDelete("/tournaments/{id}", (HttpContext http, string id,
                                                  TournamentRegistry registry, TenantScopeManager scopes) =>
            {
                if (!http.GetTenant().CanAdministerAllTournaments) return Forbidden();
                if (string.Equals(id, TenantScopeManager.DefaultTournamentId, StringComparison.Ordinal))
                    return Results.BadRequest(new { ok = false, error = "This install's own tournament cannot be deleted." });

                if (!registry.Delete(id)) return Results.NotFound(new { ok = false, error = "Unknown tournament." });
                scopes.Evict(id);
                scopes.Publish("Warning", "tenancy", "tournament deleted", null,
                    properties: new Dictionary<string, string> { ["tournamentId"] = id });
                return Results.Json(new { ok = true });
            });

            // ------------------------------------------------------------------ keys

            group.MapPost("/tournaments/{id}/keys/{kind}/rotate",
                (HttpContext http, string id, string kind, RotateKeyRequest? request,
                 TournamentRegistry registry, TenantScopeManager scopes) =>
            {
                if (!CanTouch(http, id)) return Forbidden();
                if (!TryParseKind(kind, out var keyKind))
                    return Results.BadRequest(new { ok = false, error = "kind must be overlay, agent or dashboard." });

                // Default grace is non-zero so rotating a key during a live event does not black
                // out the graphics the moment the button is clicked. Zero is available, explicitly,
                // for the case where a key is believed leaked and the broadcast matters less.
                var grace = request?.GraceMinutes is int minutes
                    ? TimeSpan.FromMinutes(Math.Max(0, minutes))
                    : TournamentRegistry.DefaultRotationGrace;

                var issued = registry.RotateKey(id, keyKind, grace, request?.Label);
                if (issued is null) return Results.NotFound(new { ok = false, error = "Unknown tournament." });

                scopes.Publish("Warning", "tenancy", "key rotated", id,
                    properties: new Dictionary<string, string>
                    {
                        ["kind"] = keyKind.ToString(),
                        ["graceMinutes"] = ((int)grace.TotalMinutes).ToString(),
                        ["newKey"] = issued.Value.key.Mask
                    });

                return Results.Json(new
                {
                    ok = true,
                    kind = keyKind.ToString(),
                    // Overlay tokens are always visible; agent/dashboard plaintext appears only here.
                    key = issued.Value.plaintext,
                    mask = issued.Value.key.Mask,
                    graceMinutes = (int)grace.TotalMinutes,
                    note = keyKind == TournamentKeyKind.Overlay
                        ? "The previous overlay URL keeps working for the grace period, so vMix can be repointed between matches."
                        : "Copy this now - it is not retrievable later. The previous key keeps working for the grace period."
                });
            });

            group.MapPost("/tournaments/{id}/keys/dashboard",
                (HttpContext http, string id, RotateKeyRequest? request,
                 TournamentRegistry registry, TenantScopeManager scopes) =>
            {
                if (!http.GetTenant().CanAdministerAllTournaments) return Forbidden();

                var issued = registry.IssueKey(id, TournamentKeyKind.Dashboard, request?.Label ?? "operator");
                if (issued is null) return Results.NotFound(new { ok = false, error = "Unknown tournament." });

                scopes.Publish("Information", "tenancy", "operator key issued", id,
                    properties: new Dictionary<string, string> { ["key"] = issued.Value.key.Mask });

                return Results.Json(new
                {
                    ok = true,
                    key = issued.Value.plaintext,
                    mask = issued.Value.key.Mask,
                    note = "Give this to the operator for this tournament. It administers only this tournament, unlike the install-wide dashboard key."
                });
            });

            group.MapPost("/tournaments/{id}/keys/{keyId}/revoke",
                (HttpContext http, string id, string keyId,
                 TournamentRegistry registry, TenantScopeManager scopes) =>
            {
                if (!CanTouch(http, id)) return Forbidden();
                if (!registry.RevokeKey(id, keyId))
                    return Results.NotFound(new { ok = false, error = "Unknown tournament or key." });

                scopes.Publish("Warning", "tenancy", "key revoked immediately (no grace period)", id,
                    properties: new Dictionary<string, string> { ["keyId"] = keyId });
                return Results.Json(new { ok = true });
            });

            // ------------------------------------------------------------------ helpers

            static bool CanTouch(HttpContext http, string tournamentId)
            {
                var tenant = http.GetTenant();
                return tenant.CanAdministerAllTournaments ||
                       string.Equals(tenant.TournamentId, tournamentId, StringComparison.Ordinal);
            }

            static IResult Forbidden() => Results.Json(
                new { ok = false, error = "This key does not administer that tournament." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        private static bool TryParseKind(string raw, out TournamentKeyKind kind)
        {
            switch ((raw ?? "").Trim().ToLowerInvariant())
            {
                case "overlay": kind = TournamentKeyKind.Overlay; return true;
                case "agent": kind = TournamentKeyKind.Agent; return true;
                case "dashboard": case "operator": kind = TournamentKeyKind.Dashboard; return true;
                default: kind = default; return false;
            }
        }

        private static object Describe(TournamentRecord record, TenantScopeManager scopes, HttpContext http)
        {
            var overlayToken = record.CurrentKey(TournamentKeyKind.Overlay)?.Plaintext;
            var scope = scopes.TryGet(record.Id);
            var agent = scope?.Agent.Snapshot();

            return new
            {
                id = record.Id,
                name = record.Name,
                orgId = record.OrgId,
                enabled = record.Enabled,
                isDefault = record.Id == TenantScopeManager.DefaultTournamentId,
                createdAtUtc = record.CreatedAtUtc,
                scheduledStartUtc = record.ScheduledStartUtc,
                scheduledEndUtc = record.ScheduledEndUtc,

                // The exact string to paste into vMix as a Browser Source.
                overlayUrl = overlayToken is null
                    ? null
                    : http.Request.Scheme + "://" + http.Request.Host + "/overlay/" + overlayToken,
                overlayToken,

                agent = agent is null ? null : new
                {
                    connected = agent.Connected,
                    summary = agent.Summary,
                    version = agent.AgentVersion,
                    secondsSinceLastTick = agent.SecondsSinceLastTick,
                    ticksAccepted = agent.TicksAccepted,
                    ticksDropped = agent.TicksDropped,
                    versionSupported = agent.VersionSupported
                },

                scopeLoaded = scope is not null,
                keys = record.Keys.Select(k => new
                {
                    id = k.Id,
                    kind = k.Kind.ToString(),
                    mask = k.Mask,
                    label = k.Label,
                    current = k.IsCurrent,
                    createdAtUtc = k.CreatedAtUtc,
                    lastUsedAtUtc = k.LastUsedAtUtc,
                    retiredAtUtc = k.RetiredAtUtc,
                    graceSeconds = k.GraceSeconds,
                    usableUntilUtc = k.RetiredAtUtc?.AddSeconds(k.GraceSeconds)
                }).ToList()
            };
        }
    }
}
