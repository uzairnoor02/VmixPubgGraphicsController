import { Bg, HealthStop, RowRule, Theme, bgCss, resolveRowBg } from "../theme";
import { panelSurface } from "./surface";
import type { ColumnStyle } from "../StudioControls";
import { DEFAULT_HEALTH_STYLE, HealthBar, HealthStyle } from "./healthGlyphs";
import { TeamLogo } from "./shared";
export { TeamLogo } from "./shared";

// The live team rankings column (PMGO layout: sits under the in-game minimap on the right).
// Shared by the Studio preview, /demo and the real /overlay route - one render path, so what the
// Studio shows is what goes on air.
//
// Row: rank | logo | team | ALIVE (one bar per player, filled to health; red = knocked with the
// bleed-out health left; grey = dead) | PTS | ELIMS. A wiped-out team stays in the table, dimmed.

export interface StandingsRow {
  key: string | number;
  rank: number;
  name: string;
  kills: number;
  players: { health: number; liveState: number }[];
  /** Served by the app at /team-logos/{teamId}.png. Missing or 404 -> neutral placeholder. */
  logoUrl?: string;
  /** Tournament points including this match's kills so far. Omitted -> "-". */
  points?: number | null;
  eliminated?: boolean;
}

export interface StandingsRendererProps {
  theme: Theme;
  mode: "full" | "top4";
  healthStops: HealthStop[];
  columns: Record<string, ColumnStyle>;
  rowRules: RowRule[];
  headerBg: Bg;
  rows: StandingsRow[];
  maxRows?: number;
  /** 0-100, Task 9. Omitted/undefined = 100 = today's appearance, unchanged. */
  panelOpacity?: number;
  healthStyle?: HealthStyle;
  showPoints?: boolean;
  showElims?: boolean;
  /** Dim teams that have been wiped out (PMGO keeps them listed, greyed). Default true. */
  dimEliminated?: boolean;
}

export function StandingsRenderer({
  theme, mode, healthStops, columns, rowRules, headerBg, rows, maxRows = 16, panelOpacity,
  healthStyle = DEFAULT_HEALTH_STYLE, showPoints = true, showElims = true, dimEliminated = true,
}: StandingsRendererProps) {
  const visibleRows = (mode === "top4" ? rows.filter((r) => r.rank <= 4) : rows).slice(0, maxRows);
  const col = (key: string) => columns[key]?.mode === "custom" ? columns[key].custom : ({} as any);
  const logoScale = columns.logo?.mode === "custom" ? (columns.logo.custom.scale ?? 1) : 1;

  // Density follows the roster so 16, 20 and 25 teams all fit the column under the minimap.
  const n = visibleRows.length;
  const rowH = n <= 16 ? 38 : n <= 20 ? 33 : 28;
  const barH = n <= 16 ? 20 : n <= 20 ? 17 : 14;
  const nameSize = n <= 16 ? 20 : n <= 20 ? 18 : 15.5;
  const logoSize = Math.round((rowH - 12) * logoScale);
  const W = { rank: 30, alive: 44, pts: 38, elims: 34 };

  const noShadow = panelOpacity !== undefined && panelOpacity <= 0;
  return (
    <div style={{ width: "100%", borderRadius: Math.min(theme.radius, 8), overflow: "hidden", ...panelSurface(panelOpacity, theme.panelBg, theme.panelBlur), border: noShadow ? "none" : `1px solid ${theme.panelBorder}`, boxShadow: noShadow ? "none" : theme.glow, fontFamily: theme.fontDisplay } as any}>
      <div style={{ display: "flex", alignItems: "center", height: 30, padding: "0 10px", background: bgCss(headerBg), fontSize: 14.5, fontWeight: 700, color: theme.headerTextColor, letterSpacing: 0.8 }}>
        <div style={{ width: W.rank }}>RANK</div>
        <div style={{ flex: 1, paddingLeft: logoSize + 8 }}>TEAM</div>
        <div style={{ width: W.alive, textAlign: "center" }}>ALIVE</div>
        {showPoints && <div style={{ width: W.pts, textAlign: "center" }}>PTS</div>}
        {showElims && <div style={{ width: W.elims, textAlign: "center" }}>ELIMS</div>}
      </div>
      {visibleRows.map((row, i) => {
        const ruleBg = resolveRowBg(rowRules, row.rank);
        const rankStyle = col("rank"), nameStyle = col("teamName"), killStyle = col("kills"), ptsStyle = col("points");
        const dim = dimEliminated && row.eliminated;
        return (
          <div key={row.key} style={{
            display: "flex", alignItems: "center", height: rowH, padding: "0 10px",
            background: ruleBg ? bgCss(ruleBg) : (i % 2 === 0 ? theme.rowBgEven : theme.rowBgOdd),
            borderTop: "1px solid rgba(255,255,255,0.05)",
            opacity: dim ? 0.45 : 1, filter: dim ? "grayscale(1)" : "none", transition: "opacity .4s, filter .4s",
          }}>
            <div style={{ width: W.rank, fontWeight: 800, fontFamily: rankStyle.fontFamily || theme.fontDisplay, fontSize: rankStyle.fontSize ? `${rankStyle.fontSize}px` : `${nameSize}px`, color: rankStyle.color || theme.textPrimary, fontVariantNumeric: "tabular-nums" }}>{row.rank}</div>
            <div style={{ flex: 1, minWidth: 0, display: "flex", alignItems: "center", gap: 8 }}>
              <TeamLogo url={row.logoUrl} size={logoSize} />
              <div style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", fontFamily: nameStyle.fontFamily || theme.fontDisplay, fontSize: nameStyle.fontSize ? `${nameStyle.fontSize}px` : `${nameSize}px`, fontWeight: 700, color: nameStyle.color || theme.textPrimary, letterSpacing: 0.3 }}>{row.name}</div>
            </div>
            <div style={{ width: W.alive, display: "flex", gap: 3, justifyContent: "center" }}>
              {row.players.slice(0, 4).map((p, j) => <HealthBar key={j} player={p} style={healthStyle} stops={healthStops} height={barH} width={6} />)}
            </div>
            {showPoints && <div style={{ width: W.pts, textAlign: "center", fontWeight: 800, fontFamily: ptsStyle.fontFamily || theme.fontDisplay, fontSize: ptsStyle.fontSize ? `${ptsStyle.fontSize}px` : `${nameSize}px`, color: ptsStyle.color || theme.textPrimary, fontVariantNumeric: "tabular-nums" }}>{row.points ?? "-"}</div>}
            {showElims && <div style={{ width: W.elims, textAlign: "center", fontWeight: 700, fontFamily: killStyle.fontFamily || theme.fontDisplay, fontSize: killStyle.fontSize ? `${killStyle.fontSize}px` : `${nameSize - 1}px`, color: killStyle.color || theme.textPrimary, opacity: 0.85, fontVariantNumeric: "tabular-nums" }}>{row.kills}</div>}
          </div>
        );
      })}
    </div>
  );
}
