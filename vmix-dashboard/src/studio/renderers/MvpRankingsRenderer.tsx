import { Bg, RowRule, Theme, bgCss, resolveRowBg } from "../theme";
import { panelSurface } from "./surface";
import type { ColumnStyle } from "../StudioControls";

// "MVP Rankings" - the ranked player table behind PostMatch.Top5MVP / StageMVP. Distinct from
// TopPlayersRenderer on purpose: that one is the three-up podium with portraits for a single
// stat, this one is a multi-column table comparing players across several stats at once, which
// is what real PMGC MVP boards show.
//
// Columns are configurable rather than fixed, because different events show different stat sets -
// the same reason RankingsRenderer takes a column-visibility object.

export interface MvpRow {
  rank: number;
  playerName: string;
  teamName?: string;
  kills: number;
  damage: number;
  assists?: number;
  survivalTime?: string;
  /** Composite MVP score when the backend computes one; null renders "-" rather than a fake 0. */
  rating?: number | null;
  logoUrl?: string;
}

export interface MvpColumns {
  team: boolean;
  kills: boolean;
  damage: boolean;
  assists: boolean;
  survival: boolean;
  rating: boolean;
}

export interface MvpRankingsRendererProps {
  theme: Theme;
  canvasBg: Bg;
  headerBg: Bg;
  title?: string;
  subtitle?: string;
  rows: MvpRow[];
  columns: MvpColumns;
  columnStyles: Record<string, ColumnStyle>;
  rowRules: RowRule[];
  maxRows?: number;
  /** 0-100, Task 9. Omitted/undefined = 100 = today's appearance, unchanged. */
  panelOpacity?: number;
}

export function MvpRankingsRenderer({
  panelOpacity,
  theme, canvasBg, headerBg, title = "MVP RANKINGS", subtitle, rows, columns, columnStyles, rowRules, maxRows = 5,
}: MvpRankingsRendererProps) {
  const col = (key: string) => (columnStyles[key]?.mode === "custom" ? columnStyles[key].custom : ({} as any));
  const visible = rows.slice(0, maxRows);

  return (
    <div style={{ width: "100%", height: "100%", borderRadius: theme.radius, overflow: "hidden", background: bgCss(canvasBg), fontFamily: theme.fontDisplay, display: "flex", flexDirection: "column", border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow } as any}>
      <div style={{ padding: "18px 24px 10px" }}>
        <div style={{ fontSize: 32, fontWeight: 800, color: theme.textPrimary, letterSpacing: 0.5, lineHeight: 1 }}>{title}</div>
        {subtitle && (
          <div style={{ display: "flex", gap: 10, marginTop: 8 }}>
            <div style={{ background: "rgba(255,255,255,0.15)", color: theme.textPrimary, fontSize: 12, fontWeight: 700, padding: "3px 14px", clipPath: "polygon(0 0, 100% 0, 92% 100%, 0% 100%)" }}>{subtitle}</div>
          </div>
        )}
      </div>

      <div style={{ flex: 1, padding: "0 24px 18px", display: "flex" }}>
        <div style={{ flex: 1, ...panelSurface(panelOpacity, theme.panelBg, theme.panelBlur), borderRadius: 8, overflow: "hidden", fontSize: 12, alignSelf: "flex-start" } as any}>
          <div style={{ display: "flex", background: bgCss(headerBg), fontWeight: 700, padding: "7px 12px", color: theme.headerTextColor, fontFamily: theme.fontDisplay, fontSize: "12px" }}>
            <div style={{ width: 24 }}>#</div>
            <div style={{ flex: 1 }}>PLAYER</div>
            {columns.team && <div style={{ width: 110 }}>TEAM</div>}
            {columns.kills && <div style={{ width: 48, textAlign: "center" }}>ELIMS</div>}
            {columns.damage && <div style={{ width: 62, textAlign: "center" }}>DAMAGE</div>}
            {columns.assists && <div style={{ width: 54, textAlign: "center" }}>ASSISTS</div>}
            {columns.survival && <div style={{ width: 64, textAlign: "center" }}>SURVIVAL</div>}
            {columns.rating && <div style={{ width: 54, textAlign: "center" }}>RATING</div>}
          </div>

          {visible.map((row, i) => {
            const ruleBg = resolveRowBg(rowRules, row.rank);
            const defaultAlt = i % 2 === 0 ? theme.rowBgEven : theme.rowBgOdd;
            const rankStyle = col("rank"), nameStyle = col("playerName"), statStyle = col("stats");
            return (
              <div key={row.rank} style={{ display: "flex", alignItems: "center", padding: "7px 12px", background: ruleBg ? bgCss(ruleBg) : defaultAlt, fontWeight: 600 }}>
                <div style={{ width: 24, fontWeight: 800, fontFamily: rankStyle.fontFamily || theme.fontDisplay, fontSize: rankStyle.fontSize ? `${rankStyle.fontSize}px` : "13px", color: rankStyle.color || theme.textPrimary }}>{row.rank}</div>
                <div style={{ flex: 1, minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", fontFamily: nameStyle.fontFamily || theme.fontBody, fontSize: nameStyle.fontSize ? `${nameStyle.fontSize}px` : "12.5px", color: nameStyle.color || theme.textPrimary }}>{row.playerName}</div>
                {columns.team && (
                  <div style={{ width: 110, display: "flex", alignItems: "center", gap: 6, overflow: "hidden" }}>
                    {row.logoUrl
                      ? <img src={row.logoUrl} alt="" style={{ width: 16, height: 16, borderRadius: 3, objectFit: "cover", flexShrink: 0 }} />
                      : <div style={{ width: 16, height: 16, borderRadius: 3, background: "rgba(255,255,255,0.15)", flexShrink: 0 }} />}
                    <span style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", fontSize: 11.5, color: theme.textPrimary, opacity: 0.85 }}>{row.teamName ?? "-"}</span>
                  </div>
                )}
                {columns.kills && <div style={{ width: 48, textAlign: "center", fontFamily: statStyle.fontFamily || theme.fontBody, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "12px", color: statStyle.color || theme.textPrimary }}>{row.kills}</div>}
                {columns.damage && <div style={{ width: 62, textAlign: "center", fontFamily: statStyle.fontFamily || theme.fontBody, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "12px", color: statStyle.color || theme.textPrimary }}>{row.damage.toLocaleString()}</div>}
                {columns.assists && <div style={{ width: 54, textAlign: "center", fontFamily: statStyle.fontFamily || theme.fontBody, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "12px", color: statStyle.color || theme.textPrimary }}>{row.assists ?? "-"}</div>}
                {columns.survival && <div style={{ width: 64, textAlign: "center", fontFamily: statStyle.fontFamily || theme.fontBody, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "12px", color: statStyle.color || theme.textPrimary }}>{row.survivalTime ?? "-"}</div>}
                {columns.rating && <div style={{ width: 54, textAlign: "center", fontWeight: 800, fontFamily: statStyle.fontFamily || theme.fontBody, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "13px", color: statStyle.color || theme.textPrimary }}>{row.rating ?? "-"}</div>}
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
