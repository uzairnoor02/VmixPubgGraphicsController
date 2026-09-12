using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Pubg_Ranking_System
{
    /// <summary>
    /// Gates every dashboard *action* endpoint (start/stop a match, run a report, write overlay
    /// config, create a tournament, ...) behind the same key the React login screen already
    /// checks (WebDashboard:AuthKey) - as a real `Authorization: Bearer &lt;key&gt;` header the
    /// SPA now sends on every request, not just something the login screen checks once and
    /// forgets.
    ///
    /// What this deliberately does NOT gate, and why: GET /api/overlay/config,
    /// GET /api/match/teams, GET /api/match/status, and the SignalR hub connection itself all
    /// have to stay reachable with no key, because /overlay is the page pasted into vMix as a
    /// Browser Source - there is no way for vMix to "log in". Everything an operator actually
    /// *does* (as opposed to what the on-air overlay passively displays) goes through this filter.
    /// The ingest endpoint (POST /api/ingest/tick) uses its own separate secret (Agent:IngestKey)
    /// instead of this one, since VmixIngestAgent is a different caller with a different trust
    /// boundary, not a dashboard user.
    /// </summary>
    public static class DashboardAuthExtensions
    {
        /// <summary>Generic over RouteHandlerBuilder/RouteGroupBuilder (the delegate-based
        /// AddEndpointFilter overload works for any IEndpointConventionBuilder, unlike the
        /// generic-type-parameter one which is RouteHandlerBuilder-only) so this can be applied
        /// once to a whole group of endpoints - see MatchControlApi.MapMatchControlEndpoints,
        /// which maps every admin-action endpoint onto one MapGroup("") with this attached -
        /// instead of having to remember it on every individual MapGet/MapPost call.</summary>
        public static TBuilder RequireDashboardKey<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
        {
            builder.AddEndpointFilter(async (context, next) =>
            {
                var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
                var expectedKey = configuration["WebDashboard:AuthKey"];
                if (string.IsNullOrWhiteSpace(expectedKey))
                {
                    // No key configured server-side at all - fail closed, not open. An admin
                    // action must never become "no auth required" just because a setting was left
                    // blank.
                    return Results.Json(new { ok = false, error = "WebDashboard:AuthKey is not configured on the server." }, statusCode: StatusCodes.Status500InternalServerError);
                }

                var header = context.HttpContext.Request.Headers.Authorization.ToString();
                var provided = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                    ? header["Bearer ".Length..].Trim()
                    : header.Trim();

                if (string.IsNullOrEmpty(provided) || provided != expectedKey)
                {
                    return Results.Json(new { ok = false, error = "Unauthorized - missing or invalid access key." }, statusCode: StatusCodes.Status401Unauthorized);
                }

                return await next(context);
            });
            return builder;
        }
    }
}
