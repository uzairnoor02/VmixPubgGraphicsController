import { Bg, Theme, bgCss } from "../theme";
import { panelSurface } from "./surface";

// The two graphics from EliminatedSidebarPage.tsx, extracted so the Studio preview and the real
// /overlay route render from exactly the same components - same pattern as StandingsRenderer.
// They ship in one file because they are one Studio page, but they are independent graphics and
// the overlay positions them separately.

// ---------------------------------------------------------------------------------------------
// Full-screen "TEAM ELIMINATED" banner
// ---------------------------------------------------------------------------------------------

export interface EliminatedBannerRendererProps {
  theme: Theme;
  bannerBg: Bg;
  teamName: string;
  logoUrl?: string;
  /** Drives the entrance animation. The caller owns the timing, because on air it is triggered by
   *  a real elimination event and in the Studio it is triggered by a Replay button. */
  visible: boolean;
  label?: string;
  /** 0-100, Task 9. Omitted/undefined = 100 = today's appearance, unchanged. */
  panelOpacity?: number;
}

export function EliminatedBannerRenderer({ theme, bannerBg, teamName, logoUrl, visible, label = "TEAM ELIMINATED", panelOpacity }: EliminatedBannerRendererProps) {
  return (
    <div style={{ width: "100%", transform: visible ? "scale(1) translateY(0)" : "scale(0.85) translateY(-12px)", opacity: visible ? 1 : 0, transition: "all 0.4s cubic-bezier(0.34, 1.56, 0.64, 1)" }}>
      <div style={{ background: bgCss(bannerBg), padding: "10px 20px", clipPath: "polygon(0 0, 100% 0, 96% 100%, 0% 100%)" }}>
        <div style={{ fontSize: 22, fontWeight: 800, color: theme.headerTextColor, fontFamily: theme.fontDisplay, letterSpacing: 0.5 }}>{label}</div>
      </div>
      <div style={{ ...panelSurface(panelOpacity, theme.panelBg, theme.panelBlur), display: "flex", alignItems: "center", gap: 10, padding: "8px 16px" } as any}>
        {logoUrl
          ? <img src={logoUrl} alt="" style={{ width: 32, height: 32, borderRadius: 4, objectFit: "cover", flexShrink: 0 }} />
          : <div style={{ width: 32, height: 32, borderRadius: 4, background: "rgba(255,255,255,0.15)", flexShrink: 0 }} />}
        <div style={{ fontSize: 15, fontWeight: 700, color: theme.textPrimary }}>{teamName}</div>
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
              {t.logoUrl
                ? <img src={t.logoUrl} alt="" style={{ width: 16, height: 16, borderRadius: 3, objectFit: "cover", flexShrink: 0 }} />
                : <div style={{ width: 16, height: 16, borderRadius: 3, background: "rgba(255,255,255,0.15)", flexShrink: 0 }} />}
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
