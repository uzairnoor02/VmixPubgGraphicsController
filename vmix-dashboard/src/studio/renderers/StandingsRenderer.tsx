import { Bg, HealthStop, RowRule, Theme, bgCss, colorForPlayer, resolveRowBg } from "../theme";
import type { ColumnStyle } from "../StudioControls";

// The actual Standings visual - extracted out of StandingsPage.tsx so the Studio preview and the
// real /overlay route render from exactly the same component. A Studio edit and what's on air can
// never drift apart because there is only one render path, not a "preview approximation" of it.

export interface StandingsRow {
  key: string | number;
  rank: number;
  name: string;
  kills: number;
  players: { health: number; liveState: number }[];
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
}

export function StandingsRenderer({ theme, mode, healthStops, columns, rowRules, headerBg, rows, maxRows = 10 }: StandingsRendererProps) {
  const visibleRows = mode === "top4" ? rows.filter((r) => r.rank <= 4) : rows;
  const col = (key: string) => columns[key]?.mode === "custom" ? columns[key].custom : ({} as any);
  const logoScale = columns.logo?.mode === "custom" ? (columns.logo.custom.scale ?? 1) : 1;

  return (
    <div style={{ width: "100%", borderRadius: theme.radius, overflow: "hidden", background: theme.panelBg, backdropFilter: `blur(${theme.panelBlur})`, WebkitBackdropFilter: `blur(${theme.panelBlur})`, border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow } as any}>
      <div style={{ display: "flex", alignItems: "center", padding: "11px 16px", background: bgCss(headerBg), fontSize: 12.5, fontWeight: 700, color: theme.headerTextColor, fontFamily: theme.fontDisplay, letterSpacing: 0.6 }}>
        <div style={{ width: 26 }}>#</div><div style={{ width: 22 * logoScale + 8 }} /><div style={{ flex: 1 }}>TEAM</div><div style={{ width: 64, textAlign: "center" }}>ALIVE</div><div style={{ width: 40, textAlign: "center" }}>ELIMS</div>
      </div>
      {visibleRows.slice(0, maxRows).map((row, i) => {
        const ruleBg = resolveRowBg(rowRules, row.rank);
        const rankStyle = col("rank"), nameStyle = col("teamName"), killStyle = col("kills");
        return (
          <div key={row.key} style={{ display: "flex", alignItems: "center", padding: "8px 16px", background: (ruleBg ? bgCss(ruleBg) : (i % 2 === 0 ? theme.rowBgEven : theme.rowBgOdd)), borderTop: "1px solid rgba(255,255,255,0.05)" }}>
            <div style={{ width: 26, fontWeight: 800, fontFamily: rankStyle.fontFamily || theme.fontDisplay, fontSize: rankStyle.fontSize ? `${rankStyle.fontSize}px` : "16px", color: rankStyle.color || theme.textPrimary }}>{row.rank}</div>
            <div style={{ width: 22 * logoScale + 8, display: "flex", alignItems: "center" }}><div style={{ width: 20 * logoScale, height: 20 * logoScale, borderRadius: 4 * logoScale, background: "rgba(255,255,255,0.15)", flexShrink: 0 }} /></div>
            <div style={{ flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", paddingRight: 8, fontFamily: nameStyle.fontFamily || theme.fontBody, fontSize: nameStyle.fontSize ? `${nameStyle.fontSize}px` : "13.5px", fontWeight: 600, color: nameStyle.color || theme.textPrimary }}>{row.name}</div>
            <div style={{ width: 64, display: "flex", gap: 3, justifyContent: "center" }}>{row.players.map((p, j) => { const { color, pulse } = colorForPlayer(healthStops, p); return <div key={j} style={{ width: 9, height: 17, borderRadius: 3, background: color, animation: pulse ? "sb-pulse 1s ease-in-out infinite" : "none", opacity: p.liveState === 5 ? 0.5 : 1 }} />; })}</div>
            <div style={{ width: 40, textAlign: "center", fontWeight: 700, fontFamily: killStyle.fontFamily || theme.fontBody, fontSize: killStyle.fontSize ? `${killStyle.fontSize}px` : "13px", color: killStyle.color || theme.textPrimary }}>{row.kills}</div>
          </div>
        );
      })}
      <style>{`@keyframes sb-pulse { 0%,100%{opacity:1} 50%{opacity:.35} }`}</style>
    </div>
  );
}
