import { Bg, Theme, bgCss } from "../theme";
import { panelSurface } from "./surface";
import type { ColumnStyle } from "../StudioControls";

// Head-to-Head - two teams compared side by side. Feasible because the TeamPoints table already
// stores one row per team per match (MatchId, DayId, StageId, WWCD, PlacementPoints, KillPoints,
// TotalPoints), which is exactly the cross-match history this graphic needs.
//
// Same contract as every renderer here: shared by the Studio preview and the live /overlay, and
// sizing belongs to the caller.

export interface HeadToHeadTeam {
  teamName: string;
  logoUrl?: string;
  /** Per-match totals, oldest first, for the form strip along the bottom. */
  matchTotals?: number[];
}

export interface HeadToHeadStat {
  label: string;
  left: number | string;
  right: number | string;
  /** Which side wins this row. Pass "none" for stats where higher isn't better, or where the
   *  two are equal - the renderer never infers a winner from the values, because "lower is
   *  better" stats (average placement) would be highlighted backwards. */
  winner: "left" | "right" | "none";
}

export interface HeadToHeadRendererProps {
  theme: Theme;
  canvasBg: Bg;
  accentBg: Bg;
  title?: string;
  subtitle?: string;
  left: HeadToHeadTeam;
  right: HeadToHeadTeam;
  stats: HeadToHeadStat[];
  fields: Record<string, ColumnStyle>;
  /** 0-100, Task 9. Omitted/undefined = 100 = today's appearance, unchanged. */
  panelOpacity?: number;
}

function TeamHeader({ team, theme, accentBg, align, nameStyle }: { team: HeadToHeadTeam; theme: Theme; accentBg: Bg; align: "left" | "right"; nameStyle: any }) {
  return (
    <div style={{ display: "flex", alignItems: "center", gap: 10, flexDirection: align === "right" ? "row-reverse" : "row", flex: 1, minWidth: 0 }}>
      {team.logoUrl
        ? <img src={team.logoUrl} alt="" style={{ width: 36, height: 36, borderRadius: 7, objectFit: "cover", flexShrink: 0 }} />
        : <div style={{ width: 36, height: 36, borderRadius: 7, background: bgCss(accentBg), flexShrink: 0 }} />}
      <div style={{
        minWidth: 0, textAlign: align,
        fontFamily: nameStyle.fontFamily || theme.fontDisplay,
        fontSize: nameStyle.fontSize ? `${nameStyle.fontSize}px` : "18px",
        fontWeight: 800, color: nameStyle.color || theme.textPrimary,
        overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap",
      }}>
        {team.teamName}
      </div>
    </div>
  );
}

export function HeadToHeadRenderer({
  panelOpacity,
  theme, canvasBg, accentBg, title = "HEAD TO HEAD", subtitle, left, right, stats, fields,
}: HeadToHeadRendererProps) {
  const nameStyle = fields.teamName?.mode === "custom" ? fields.teamName.custom : ({} as any);
  const statStyle = fields.statValue?.mode === "custom" ? fields.statValue.custom : ({} as any);
  const winColor = "#F5A623";

  return (
    <div style={{ width: "100%", height: "100%", borderRadius: theme.radius, overflow: "hidden", background: bgCss(canvasBg), fontFamily: theme.fontDisplay, display: "flex", flexDirection: "column", padding: 18, border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow } as any}>
      <div style={{ fontSize: 22, fontWeight: 800, color: theme.textPrimary, lineHeight: 1 }}>{title}</div>
      {subtitle && (
        <div style={{ alignSelf: "flex-start", marginTop: 6, background: bgCss(accentBg), color: theme.headerTextColor, fontSize: 10.5, fontWeight: 700, letterSpacing: 0.8, padding: "3px 13px", clipPath: "polygon(0 0, 100% 0, 93% 100%, 0% 100%)" }}>{subtitle}</div>
      )}

      <div style={{ display: "flex", alignItems: "center", gap: 12, marginTop: 12 }}>
        <TeamHeader team={left} theme={theme} accentBg={accentBg} align="left" nameStyle={nameStyle} />
        <div style={{ flexShrink: 0, fontSize: 13, fontWeight: 800, color: "rgba(255,255,255,0.45)", letterSpacing: 1 }}>VS</div>
        <TeamHeader team={right} theme={theme} accentBg={accentBg} align="right" nameStyle={nameStyle} />
      </div>

      <div style={{ marginTop: 12, display: "flex", flexDirection: "column", gap: 4, flex: 1 }}>
        {stats.map((stat) => (
          <div key={stat.label} style={{ display: "flex", alignItems: "center", ...panelSurface(panelOpacity, theme.panelBg, theme.panelBlur), borderRadius: 6, padding: "5px 11px" } as any}>
            <div style={{ width: 74, textAlign: "left", fontFamily: statStyle.fontFamily || theme.fontDisplay, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "15px", fontWeight: 800, color: stat.winner === "left" ? winColor : (statStyle.color || theme.textPrimary) }}>
              {typeof stat.left === "number" ? stat.left.toLocaleString() : stat.left}
            </div>
            <div style={{ flex: 1, textAlign: "center", fontSize: 10, fontWeight: 700, letterSpacing: 1, color: "rgba(255,255,255,0.55)", fontFamily: theme.fontBody, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
              {stat.label.toUpperCase()}
            </div>
            <div style={{ width: 74, textAlign: "right", fontFamily: statStyle.fontFamily || theme.fontDisplay, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "15px", fontWeight: 800, color: stat.winner === "right" ? winColor : (statStyle.color || theme.textPrimary) }}>
              {typeof stat.right === "number" ? stat.right.toLocaleString() : stat.right}
            </div>
          </div>
        ))}
      </div>

      {(left.matchTotals?.length || right.matchTotals?.length) ? (
        <div style={{ marginTop: 8, display: "flex", alignItems: "flex-end", gap: 12 }}>
          {[left, right].map((team, side) => (
            <div key={side} style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontSize: 9, fontWeight: 700, letterSpacing: 0.8, color: "rgba(255,255,255,0.45)", marginBottom: 4, textAlign: side === 1 ? "right" : "left" }}>PER-MATCH POINTS</div>
              <div style={{ display: "flex", gap: 4, justifyContent: side === 1 ? "flex-end" : "flex-start" }}>
                {(team.matchTotals ?? []).map((total, i) => (
                  <div key={i} style={{ minWidth: 26, textAlign: "center", background: "rgba(0,0,0,0.35)", borderRadius: 4, padding: "2px 6px", fontSize: 11, fontWeight: 700, color: theme.textPrimary }}>
                    {total}
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      ) : null}
    </div>
  );
}
