import { Bg, Theme, bgCss } from "../theme";
import { panelSurface } from "./surface";
import type { ColumnStyle } from "../StudioControls";

// "Teams to Watch" - the pre-match / between-match card highlighting a handful of teams and why
// they matter. PostMatch.TeamToWatch.cs already computes the selection; this is its on-air face.
//
// Same contract as every renderer here: the Studio preview and the live /overlay render this
// exact component, and sizing belongs to the caller.

export interface TeamToWatchEntry {
  key: string | number;
  teamName: string;
  /** The one-line reason this team is worth watching, e.g. "3 WWCDs in 4 matches". */
  reason: string;
  /** Optional supporting figures shown as small chips under the reason. */
  stats?: { label: string; value: string | number }[];
  logoUrl?: string;
  rank?: number;
}

export interface TeamsToWatchRendererProps {
  theme: Theme;
  canvasBg: Bg;
  accentBg: Bg;
  title?: string;
  subtitle?: string;
  teams: TeamToWatchEntry[];
  fields: Record<string, ColumnStyle>;
  /** More than this and the cards get too thin to read at broadcast size. */
  maxTeams?: number;
  /** 0-100, Task 9. Omitted/undefined = 100 = today's appearance, unchanged. */
  panelOpacity?: number;
}

export function TeamsToWatchRenderer({
  panelOpacity,
  theme, canvasBg, accentBg, title = "TEAMS TO WATCH", subtitle, teams, fields, maxTeams = 4,
}: TeamsToWatchRendererProps) {
  const nameStyle = fields.teamName?.mode === "custom" ? fields.teamName.custom : ({} as any);
  const reasonStyle = fields.reason?.mode === "custom" ? fields.reason.custom : ({} as any);
  const visible = teams.slice(0, maxTeams);

  return (
    <div style={{ width: "100%", height: "100%", borderRadius: theme.radius, overflow: "hidden", background: bgCss(canvasBg), fontFamily: theme.fontDisplay, display: "flex", flexDirection: "column", padding: 22, border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow } as any}>
      <div style={{ fontSize: 30, fontWeight: 800, color: theme.textPrimary, lineHeight: 1 }}>{title}</div>
      {subtitle && (
        <div style={{ alignSelf: "flex-start", marginTop: 8, background: bgCss(accentBg), color: theme.headerTextColor, fontSize: 11.5, fontWeight: 700, letterSpacing: 0.8, padding: "3px 14px", clipPath: "polygon(0 0, 100% 0, 93% 100%, 0% 100%)" }}>
          {subtitle}
        </div>
      )}

      {/* Cards are capped and centred rather than stretched: with one or two teams supplied, a
          plain flex:1 would blow a single card out to the full canvas width and leave it mostly
          empty, which looks like a bug on air. */}
      <div style={{ display: "flex", gap: 12, marginTop: 16, flex: 1, justifyContent: "center" }}>
        {visible.map((team) => (
          <div key={team.key} style={{ flex: "1 1 0", maxWidth: 210, minWidth: 0, display: "flex", flexDirection: "column", ...panelSurface(panelOpacity, theme.panelBg, theme.panelBlur), border: `1px solid ${theme.panelBorder}`, borderRadius: 8, overflow: "hidden" } as any}>
            <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "10px 12px", background: bgCss(accentBg) }}>
              {team.logoUrl
                ? <img src={team.logoUrl} alt="" style={{ width: 24, height: 24, borderRadius: 4, objectFit: "cover", flexShrink: 0 }} />
                : <div style={{ width: 24, height: 24, borderRadius: 4, background: "rgba(255,255,255,0.22)", flexShrink: 0 }} />}
              <div style={{ minWidth: 0, fontFamily: nameStyle.fontFamily || theme.fontDisplay, fontSize: nameStyle.fontSize ? `${nameStyle.fontSize}px` : "13.5px", fontWeight: 800, color: nameStyle.color || theme.headerTextColor, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {team.teamName}
              </div>
              {team.rank !== undefined && (
                <div style={{ marginLeft: "auto", fontSize: 11, fontWeight: 800, color: theme.headerTextColor, opacity: 0.85, flexShrink: 0 }}>#{team.rank}</div>
              )}
            </div>

            <div style={{ padding: "10px 12px", display: "flex", flexDirection: "column", gap: 8, flex: 1 }}>
              <div style={{ fontFamily: reasonStyle.fontFamily || theme.fontBody, fontSize: reasonStyle.fontSize ? `${reasonStyle.fontSize}px` : "12px", lineHeight: 1.45, color: reasonStyle.color || theme.textPrimary }}>
                {team.reason}
              </div>
              {team.stats && team.stats.length > 0 && (
                <div style={{ marginTop: "auto", display: "flex", flexWrap: "wrap", gap: 6 }}>
                  {team.stats.map((stat) => (
                    <div key={stat.label} style={{ background: "rgba(0,0,0,0.35)", borderRadius: 5, padding: "4px 8px" }}>
                      <div style={{ fontSize: 8.5, fontWeight: 700, letterSpacing: 0.6, color: "rgba(255,255,255,0.55)", fontFamily: theme.fontBody }}>{stat.label.toUpperCase()}</div>
                      <div style={{ fontSize: 14, fontWeight: 800, color: theme.textPrimary }}>
                        {typeof stat.value === "number" ? stat.value.toLocaleString() : stat.value}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
