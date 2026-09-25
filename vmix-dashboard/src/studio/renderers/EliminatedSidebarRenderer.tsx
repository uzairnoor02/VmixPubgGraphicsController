import { Bg, Theme, bgCss } from "../theme";
import { panelSurface } from "./surface";
import { TeamLogo, bgPrimaryColor } from "./shared";

// The two graphics from EliminatedSidebarPage.tsx, extracted so the Studio preview and the real
// /overlay route render from exactly the same components - same pattern as StandingsRenderer.
// They ship in one file because they are one Studio page, but they are independent graphics and
// the overlay positions them separately.

// ---------------------------------------------------------------------------------------------
// "ELIMINATED" banner (PMGO layout)
//
//   /  #14   [ LOGO ]   ELIMINATED        /
//  /   TEAM NAME          1 ELIMS        /
//
// A compact angled card in the upper centre of the screen. Finishing position and the team's
// kill count come with the backend's teamEliminated event; either can be missing (a derived
// fallback banner), in which case that part is simply left out.
// ---------------------------------------------------------------------------------------------

export interface EliminatedBannerRendererProps {
  theme: Theme;
  bannerBg: Bg;
  teamName: string;
  logoUrl?: string;
  /** Finishing position, e.g. 14 -> "#14". */
  rank?: number | null;
  /** Team eliminations this match. */
  eliminations?: number | null;
  /** Drives the entrance animation. The caller owns the timing, because on air it is triggered by
   *  a real elimination event and in the Studio it is triggered by a Replay button. */
  visible: boolean;
  label?: string;
  /** 0-100, Task 9. Omitted/undefined = 100 = today's appearance, unchanged. */
  panelOpacity?: number;
}

export function EliminatedBannerRenderer({ theme, bannerBg, teamName, logoUrl, rank, eliminations, visible, label = "ELIMINATED", panelOpacity }: EliminatedBannerRendererProps) {
  const accent = bgPrimaryColor(bannerBg);
  const skew = "polygon(4% 0, 100% 0, 96% 100%, 0% 100%)";
  const hasRank = rank !== undefined && rank !== null && rank > 0;
  const hasElims = eliminations !== undefined && eliminations !== null;
  return (
    <div style={{
      width: "100%", fontFamily: theme.fontDisplay,
      transform: visible ? "scale(1)" : "scale(0.8)", opacity: visible ? 1 : 0,
      transition: "transform .35s cubic-bezier(0.34, 1.56, 0.64, 1), opacity .25s ease",
    }}>
      <div style={{ position: "relative", clipPath: skew, ...panelSurface(panelOpacity, "rgba(12,12,16,0.92)", theme.panelBlur) } as any}>
        {/* accent slash on the left edge */}
        <div style={{ position: "absolute", left: 0, top: 0, bottom: 0, width: 14, background: bgCss(bannerBg), clipPath: "polygon(30% 0, 100% 0, 70% 100%, 0 100%)" }} />
        <div style={{ display: "flex", alignItems: "center", gap: 12, padding: "10px 26px 10px 24px", minHeight: 78 }}>
          <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 2, flexShrink: 0 }}>
            {hasRank && <div style={{ fontSize: 20, fontWeight: 800, color: theme.textPrimary, lineHeight: 1, fontStyle: "italic" }}>#{rank}</div>}
            <TeamLogo url={logoUrl} size={hasRank ? 50 : 60} radius={6} />
          </div>
          <div style={{ minWidth: 0, flex: 1 }}>
            <div style={{
              fontSize: 32, fontWeight: 900, lineHeight: 1, letterSpacing: 0.5, color: accent, textTransform: "uppercase", fontStyle: "italic",
              ...(bannerBg.type === "gradient" ? { background: bgCss(bannerBg), WebkitBackgroundClip: "text", backgroundClip: "text", WebkitTextFillColor: "transparent" } : {}),
            } as any}>{label}</div>
            <div style={{ display: "flex", alignItems: "baseline", gap: 10, marginTop: 5 }}>
              {hasElims && (
                <div style={{ fontSize: 36, fontWeight: 900, lineHeight: 1, color: theme.textPrimary, whiteSpace: "nowrap", fontStyle: "italic" }}>
                  {eliminations} <span style={{ fontSize: 24 }}>ELIMS</span>
                </div>
              )}
              <div style={{ fontSize: hasElims ? 14 : 20, fontWeight: 700, color: theme.textPrimary, opacity: hasElims ? 0.7 : 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{teamName}</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------------------------
// Always-on live sidebar leaderboard
// ---------------------------------------------------------------------------------------------

export interface SidebarRow {
  key: string | number;
  rank: number;
  teamName: string;
  /** null renders "-", which is what a team with no points yet should show rather than a fake 0. */
  points: number | null;
  kills: number;
  logoUrl?: string;
}

export interface SidebarRendererProps {
  theme: Theme;
  headerBg: Bg;
  rows: SidebarRow[];
  maxRows?: number;
  /** 0-100, Task 9. Omitted/undefined = 100 = today's appearance, unchanged. */
  panelOpacity?: number;
}

export function SidebarRenderer({ theme, headerBg, rows, maxRows = 16, panelOpacity }: SidebarRendererProps) {
  return (
    <div style={{ width: "100%", borderRadius: theme.radius, overflow: "hidden", fontFamily: theme.fontDisplay, border: "1px solid rgba(255,255,255,0.12)", boxShadow: theme.glow }}>
      <div style={{ display: "flex", background: bgCss(headerBg), color: theme.headerTextColor, fontWeight: 700, fontSize: 11, padding: "5px 10px" }}>
        <div style={{ width: 20 }}>#</div><div style={{ flex: 1 }}>TEAM</div>
        <div style={{ width: 44, textAlign: "center" }}>PTS</div><div style={{ width: 36, textAlign: "center" }}>ELIM</div>
      </div>
      <div style={{ ...panelSurface(panelOpacity, theme.panelBg, theme.panelBlur) } as any}>
        {rows.slice(0, maxRows).map((t) => (
          <div key={t.key} style={{ display: "flex", alignItems: "center", padding: "5px 10px", borderTop: "1px solid rgba(255,255,255,0.05)", fontSize: 12 }}>
            <div style={{ width: 20, fontWeight: 700, color: theme.textPrimary }}>{t.rank}</div>
            <div style={{ flex: 1, display: "flex", alignItems: "center", gap: 6, overflow: "hidden" }}>
              <TeamLogo url={t.logoUrl} size={16} radius={3} />
              <span style={{ color: theme.textPrimary, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{t.teamName}</span>
            </div>
            <div style={{ width: 44, textAlign: "center", color: theme.textPrimary, fontWeight: 600 }}>{t.points ?? "-"}</div>
            <div style={{ width: 36, textAlign: "center", color: theme.textPrimary, fontWeight: 600 }}>{t.kills}</div>
          </div>
        ))}
      </div>
    </div>
  );
}
