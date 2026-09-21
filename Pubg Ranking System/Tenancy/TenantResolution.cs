using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VmixGraphicsBusiness.Tenancy;

namespace Pubg_Ranking_System.Tenancy
{
    public enum TenantPrincipalKind
    {
        /// <summary>No credential presented, and no fallback applied.</summary>
        None,

        /// <summary>An <c>/overlay/{token}</c> viewer. Read-only: it can render, it cannot change anything.</summary>
        Overlay,

        /// <summary>An ingest agent. Can write live match state for its own tournament and nothing else.</summary>
        Agent,

        /// <summary>A dashboard operator scoped to one tournament by a <c>dsh_</c> key.</summary>
        Operator,

        /// <summary>
        /// The legacy shared <c>WebDashboard:AuthKey</c>. Administers every tournament on this
        /// install — correct for a single-machine install, and the reason this key must not be
        /// the credential handed to a customer once more than one tournament exists here.
        /// </summary>
        LegacyAdmin
    }

    /// <summary>
    /// Who is making this request and which tournament they are allowed to touch. One instance
    /// per request, stashed in <see cref="HttpContext.Items"/>.
    /// </summary>
    public sealed class TenantContext
    {
        public const string ItemKey = "__vmix_tenant";

        public TenantPrincipalKind Kind { get; init; } = TenantPrincipalKind.None;
        public TenantScope? Scope { get; init; }
        public IssuedKey? Key { get; init; }
        public TenantResolution Resolution { get; init; } = TenantResolution.NotFound;

        /// <summary>
        /// True when the presented credential had already been rotated and only worked because of
        /// its grace window. Logged as a warning: it means a field install is still holding an old
        /// key and will break when the window closes.
        /// </summary>
        public bool UsedGracePeriod { get; init; }

        /// <summary>Correlates every log line produced while handling this request.</summary>
        public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N").Substring(0, 12);

        public string? TournamentId => Scope?.TournamentId;
        public bool IsResolved => Scope is not null;
        public bool CanWriteLiveState => Kind is TenantPrincipalKind.Agent or TenantPrincipalKind.LegacyAdmin;
        public bool CanAdminister => Kind is TenantPrincipalKind.Operator or TenantPrincipalKind.LegacyAdmin;

        /// <summary>True only for the shared key, which is the one principal not bound to a single tournament.</summary>
        public bool CanAdministerAllTournaments => Kind == TenantPrincipalKind.LegacyAdmin;
    }

    /// <summary>
    /// Resolves the tenant for a request, and provides endpoint filters that enforce it.
    ///
    /// Two rules this layer exists to enforce structurally rather than by convention, both of them
    /// findings from the SaaS gap analysis:
    ///
    /// 1. <b>The tenant is never read from the request body.</b> It comes from a credential — a
    ///    header, or the overlay token in the path. A body parameter would mean any authenticated
    ///    caller could name someone else's tournament and be believed, and it only takes one
    ///    endpoint forgetting to check for that to become a cross-tenant read.
    ///
    /// 2. <b>The tournament id is not a credential.</b> Ids appear in logs, URLs of admin pages,
    ///    screenshots and support tickets. Access is granted only by an <c>ovl_</c>, <c>agt_</c>
    ///    or <c>dsh_</c> key, which are separate values and independently rotatable.
    /// </summary>
    public static class TenantResolutionExtensions
    {
        public const string CorrelationHeader = "X-Correlation-Id";
        public const string OverlayTokenHeader = "X-Overlay-Token";
        public const string AgentKeyHeader = "X-Agent-Key";

        /// <summary>The resolved tenant for this request, or an unresolved context if none was attached.</summary>
        public static TenantContext GetTenant(this HttpContext http)
            => http.Items.TryGetValue(TenantContext.ItemKey, out var value) && value is TenantContext ctx
                ? ctx
                : new TenantContext();

        private static void SetTenant(HttpContext http, TenantContext ctx)
            => http.Items[TenantContext.ItemKey] = ctx;

        /// <summary>
        /// Attaches a <see cref="TenantContext"/> to every request, and stamps a correlation id on
        /// the response so a log line can be tied to a specific request after the fact.
        ///
        /// Resolution order is most-specific-credential-first. An unresolvable credential is not an
        /// error here — the per-endpoint filters below decide what the absence of a tenant means
        /// for each route, because the answer differs: <c>/overlay</c> must work with only a token,
        /// admin routes must not work without an admin key, and ingest must not work without an
        /// agent key.
        /// </summary>
        public static void UseTenantResolution(this WebApplication app)
        {
            app.Use(async (http, next) =>
            {
                var registry = http.RequestServices.GetRequiredService<TournamentRegistry>();
                var scopes = http.RequestServices.GetRequiredService<TenantScopeManager>();
                var config = http.RequestServices.GetRequiredService<IConfiguration>();

                var correlationId = http.Request.Headers.TryGetValue(CorrelationHeader, out var incoming)
                                    && !string.IsNullOrWhiteSpace(incoming.ToString())
                    ? incoming.ToString().Trim()
                    : Guid.NewGuid().ToString("N").Substring(0, 12);

                var ctx = Resolve(http, registry, scopes, config, correlationId);
                SetTenant(http, ctx);

                http.Response.Headers[CorrelationHeader] = correlationId;

                if (ctx.UsedGracePeriod && ctx.Scope is not null)
                {
                    scopes.Publish("Warning", "tenancy",
                        "request authenticated with a rotated key still inside its grace window",
                        ctx.TournamentId,
                        properties: new Dictionary<string, string>
                        {
                            ["key"] = ctx.Key?.Mask ?? "(unknown)",
                            ["kind"] = ctx.Kind.ToString(),
                            ["path"] = http.Request.Path.ToString(),
                            ["correlationId"] = correlationId
                        });
                }

                await next();
            });
        }

        private static TenantContext Resolve(
            HttpContext http,
            TournamentRegistry registry,
            TenantScopeManager scopes,
            IConfiguration config,
            string correlationId)
        {
            // 1. Ingest agent. Checked first because it is the highest-frequency caller by orders
            //    of magnitude — roughly one request every two seconds per live tournament.
            var agentKey = http.Request.Headers[AgentKeyHeader].ToString();
            if (!string.IsNullOrWhiteSpace(agentKey))
            {
                var lookup = registry.ResolveAgentKey(agentKey);
                if (lookup.Success)
                {
                    return new TenantContext
                    {
                        Kind = TenantPrincipalKind.Agent,
                        Scope = scopes.GetOrCreate(lookup.Tournament!),
                        Key = lookup.Key,
                        Resolution = TenantResolution.Ok,
                        UsedGracePeriod = lookup.UsedGracePeriod,
                        CorrelationId = correlationId
                    };
                }

                // Legacy single-tenant ingest secret. Kept working deliberately: the agent builds
                // already in the field use it, and breaking them to ship tenancy would be a
                // self-inflicted outage.
                var legacyIngestKey = config["Agent:IngestKey"];
                if (!string.IsNullOrWhiteSpace(legacyIngestKey) &&
                    FixedTimeEquals(agentKey.Trim(), legacyIngestKey!.Trim()))
                {
                    return new TenantContext
                    {
                        Kind = TenantPrincipalKind.Agent,
                        Scope = scopes.Default,
                        Resolution = TenantResolution.Ok,
                        CorrelationId = correlationId
                    };
                }

                return new TenantContext
                {
                    Kind = TenantPrincipalKind.None,
                    Resolution = lookup.Resolution,
                    CorrelationId = correlationId
                };
            }

            // 2. Dashboard operator. The legacy shared key is checked before per-tournament keys
            //    because it is what the existing SPA sends today.
            var bearer = ReadBearer(http);
            if (!string.IsNullOrWhiteSpace(bearer))
            {
                var dashboardKey = config["WebDashboard:AuthKey"];
                if (!string.IsNullOrWhiteSpace(dashboardKey) &&
                    FixedTimeEquals(bearer!, dashboardKey!.Trim()))
                {
                    return new TenantContext
                    {
                        Kind = TenantPrincipalKind.LegacyAdmin,
                        // Requests that do not name a tournament act on this install's own one,
                        // which is what every existing dashboard call means today.
                        Scope = ResolveRequestedScope(http, scopes) ?? scopes.Default,
                        Resolution = TenantResolution.Ok,
                        CorrelationId = correlationId
                    };
                }

                var operatorLookup = registry.ResolveDashboardKey(bearer);
                if (operatorLookup.Success)
                {
                    return new TenantContext
                    {
                        Kind = TenantPrincipalKind.Operator,
                        Scope = scopes.GetOrCreate(operatorLookup.Tournament!),
                        Key = operatorLookup.Key,
                        Resolution = TenantResolution.Ok,
                        UsedGracePeriod = operatorLookup.UsedGracePeriod,
                        CorrelationId = correlationId
                    };
                }
            }

            // 3. Overlay viewer: /overlay/{token}, ?t={token}, or the header (used by the SPA's
            //    own fetches once it knows its token).
            var overlayToken = ReadOverlayToken(http);
            if (!string.IsNullOrWhiteSpace(overlayToken))
            {
                var lookup = registry.ResolveOverlayToken(overlayToken);
                if (lookup.Success)
                {
                    return new TenantContext
                    {
                        Kind = TenantPrincipalKind.Overlay,
                        Scope = scopes.GetOrCreate(lookup.Tournament!),
                        Key = lookup.Key,
                        Resolution = TenantResolution.Ok,
                        UsedGracePeriod = lookup.UsedGracePeriod,
                        CorrelationId = correlationId
                    };
                }

                return new TenantContext
                {
                    Kind = TenantPrincipalKind.None,
                    Resolution = lookup.Resolution,
                    CorrelationId = correlationId
                };
            }

            // 4. No credential. The tokenless /overlay route and the unauthenticated GETs that
            //    vMix depends on resolve to this install's own tournament, which is exactly the
            //    behaviour that exists today.
            return new TenantContext
            {
                Kind = TenantPrincipalKind.None,
                Scope = scopes.Default,
                Resolution = TenantResolution.Ok,
                CorrelationId = correlationId
            };
        }

        /// <summary>
        /// Only the install-wide admin may name a tournament explicitly, and only via a header —
        /// never a body field. This is the single, audited exception to rule 1 above.
        /// </summary>
        private static TenantScope? ResolveRequestedScope(HttpContext http, TenantScopeManager scopes)
        {
            var requested = http.Request.Headers["X-Tournament-Id"].ToString();
            if (string.IsNullOrWhiteSpace(requested)) return null;
            return scopes.GetOrCreate(requested.Trim());
        }

        private static string? ReadBearer(HttpContext http)
        {
            var header = http.Request.Headers.Authorization.ToString();
            if (string.IsNullOrWhiteSpace(header)) return null;
            return header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? header["Bearer ".Length..].Trim()
                : header.Trim();
        }

        private static string? ReadOverlayToken(HttpContext http)
        {
            var fromHeader = http.Request.Headers[OverlayTokenHeader].ToString();
            if (!string.IsNullOrWhiteSpace(fromHeader)) return fromHeader.Trim();

            if (http.Request.Query.TryGetValue("t", out var q) && !string.IsNullOrWhiteSpace(q.ToString()))
                return q.ToString().Trim();

            // /overlay/{token} — matched on the raw path so this works for the SPA fallback route
            // as well as for API calls, without needing route values to have been bound yet.
            var path = http.Request.Path.Value ?? string.Empty;
            const string prefix = "/overlay/";
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var rest = path[prefix.Length..].Trim('/');
                if (!string.IsNullOrWhiteSpace(rest) && !rest.Contains('/')) return rest;
            }

            return null;
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            var x = System.Text.Encoding.UTF8.GetBytes(a);
            var y = System.Text.Encoding.UTF8.GetBytes(b);
            if (x.Length != y.Length) return false;
            return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(x, y);
        }

        // ------------------------------------------------------------------ filters

        /// <summary>
        /// Requires a valid ingest credential. Replaces the inline <c>X-Agent-Key</c> check in
        /// <c>IngestApi</c> so that the per-tournament key and the legacy shared secret are
        /// accepted by the same code path, and so the tournament is already resolved by the time
        /// the handler runs.
        /// </summary>
        public static TBuilder RequireAgentKey<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
        {
            builder.AddEndpointFilter(async (context, next) =>
            {
                var tenant = context.HttpContext.GetTenant();
                if (tenant.Kind != TenantPrincipalKind.Agent || tenant.Scope is null)
                {
                    var scopes = context.HttpContext.RequestServices.GetRequiredService<TenantScopeManager>();
                    scopes.Publish("Warning", "ingest", "rejected tick: invalid or missing agent key", null,
                        properties: new Dictionary<string, string>
                        {
                            ["reason"] = tenant.Resolution.ToString(),
                            ["remoteIp"] = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "-",
                            ["correlationId"] = tenant.CorrelationId
                        });

                    return Results.Json(
                        new { ok = false, error = "Invalid or missing " + AgentKeyHeader + " header." },
                        statusCode: StatusCodes.Status401Unauthorized);
                }
                return await next(context);
            });
            return builder;
        }

        /// <summary>
        /// Requires an operator credential — either a per-tournament <c>dsh_</c> key or the
        /// install-wide dashboard key. Use alongside (not instead of) the existing
        /// <c>RequireDashboardKey</c> while both credential styles are in circulation.
        /// </summary>
        public static TBuilder RequireTenantAdmin<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
        {
            builder.AddEndpointFilter(async (context, next) =>
            {
                var tenant = context.HttpContext.GetTenant();
                if (!tenant.CanAdminister || tenant.Scope is null)
                {
                    return Results.Json(
                        new { ok = false, error = "Unauthorized - missing or invalid access key." },
                        statusCode: StatusCodes.Status401Unauthorized);
                }
                return await next(context);
            });
            return builder;
        }

        /// <summary>
        /// Requires a resolvable tournament, by any credential including none (which resolves to
        /// this install's own). For the read-only routes the on-air overlay depends on.
        ///
        /// A bad token returns 404 rather than 403 on purpose: 403 confirms that a token was
        /// well-formed and merely unauthorised, which is a free oracle for anyone probing.
        /// </summary>
        public static TBuilder RequireTenant<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
        {
            builder.AddEndpointFilter(async (context, next) =>
            {
                var tenant = context.HttpContext.GetTenant();
                if (tenant.Scope is null)
                    return Results.NotFound(new { ok = false, error = "Unknown overlay token." });
                return await next(context);
            });
            return builder;
        }
    }
}
