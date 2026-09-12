import { useState } from "react";
import { Flame, LayoutGrid, Settings2, ShieldAlert, Sparkles, Trophy, Users, Zap } from "lucide-react";
import { StudioConfigProvider } from "./StudioConfigContext";
import { ThemeProvider, useTheme } from "./ThemeContext";
import { THEMES } from "./theme";
import { ThemePicker, btnGhost } from "./StudioControls";
import StandingsPage from "./pages/StandingsPage";
import Top4Page from "./pages/Top4Page";
import RankingsPage from "./pages/RankingsPage";
import TopPlayersPage from "./pages/TopPlayersPage";
import EliminatedSidebarPage from "./pages/EliminatedSidebarPage";
import AchievementPage from "./pages/AchievementPage";

// The Graphics Studio - design-time editor for every graphic the /overlay route can show. Changes
// here save into OverlayConfig.elementSettings (see StudioConfigContext.tsx) and reach the live
// broadcast output immediately over the same SignalR channel the overlay already listens on -
// there's no separate "publish" button because there's nothing left to publish.

const NAV_ITEMS = [
  { id: "standings", label: "Standings", icon: LayoutGrid, page: StandingsPage },
  { id: "top4", label: "Top 4 / WWCD", icon: Flame, page: Top4Page },
  { id: "rankings", label: "Rankings", icon: Trophy, page: RankingsPage },
  { id: "top-players", label: "Top Players", icon: Users, page: TopPlayersPage },
  { id: "eliminated", label: "Eliminated & Sidebar", icon: ShieldAlert, page: EliminatedSidebarPage },
  { id: "achievement", label: "Achievement Popup", icon: Zap, page: AchievementPage },
] as const;

function GraphicsStudioInner() {
  const { theme, activeThemeId, setActiveThemeId } = useTheme();
  const [activePage, setActivePage] = useState<(typeof NAV_ITEMS)[number]["id"]>("standings");
  const [themePanelOpen, setThemePanelOpen] = useState(false);

  const ActivePageComponent = NAV_ITEMS.find((n) => n.id === activePage)?.page ?? StandingsPage;

  return (
    <div style={{ fontFamily: "'Inter', system-ui, sans-serif", background: "linear-gradient(160deg, #0a0a12, #0d0d18)", color: "#e8e8ec", minHeight: "100%", display: "flex", margin: "-24px", borderRadius: 12 }}>
      <div style={{ width: 220, flexShrink: 0, borderRight: "1px solid rgba(255,255,255,0.08)", background: "rgba(0,0,0,0.2)", borderRadius: "12px 0 0 12px" }}>
        <div style={{ padding: 16 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 20 }}>
            <div style={{ width: 30, height: 30, borderRadius: 8, background: theme.accentGradient, display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}><Sparkles size={15} color="#0a0a0f" /></div>
            <div style={{ fontSize: 13, fontWeight: 700 }}>Graphics Studio</div>
          </div>
          {NAV_ITEMS.map((item) => (
            <button key={item.id} onClick={() => setActivePage(item.id)} style={{
              display: "flex", alignItems: "center", gap: 10, width: "100%", padding: "9px 10px", marginBottom: 3,
              borderRadius: 8, border: "none", cursor: "pointer", textAlign: "left", fontSize: 12.5,
              background: activePage === item.id ? "rgba(244,196,48,0.12)" : "transparent",
              color: activePage === item.id ? "#F4C430" : "#c8c8d0", fontWeight: activePage === item.id ? 600 : 400,
            }}>
              <item.icon size={15} />{item.label}
            </button>
          ))}
        </div>
      </div>

      <div style={{ flex: 1, minWidth: 0 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 12, padding: "14px 20px", borderBottom: "1px solid rgba(255,255,255,0.08)", position: "sticky", top: 0, background: "rgba(10,10,18,0.85)", backdropFilter: "blur(12px)", zIndex: 10, borderRadius: "0 12px 0 0" } as any}>
          <div style={{ fontSize: 11, color: "#8a8a94" }}>Every change here reaches the live /overlay instantly</div>
          <div style={{ marginLeft: "auto", position: "relative" }}>
            <button onClick={() => setThemePanelOpen((o) => !o)} style={{ ...btnGhost, display: "flex", alignItems: "center", gap: 8, padding: "6px 12px" }}>
              <div style={{ width: 16, height: 16, borderRadius: 4, background: theme.accentGradient }} />
              {theme.name}
              <Settings2 size={12} />
            </button>
            {themePanelOpen && (
              <div style={{ position: "absolute", top: 40, right: 0, zIndex: 40, background: "#15151d", border: "1px solid rgba(255,255,255,0.12)", borderRadius: 14, padding: 14, width: 280, boxShadow: "0 16px 40px rgba(0,0,0,0.6)" }}>
                <div style={{ fontSize: 11.5, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>
                  Org-wide theme. Applies to every page here — switching it updates Standings,
                  Rankings, Top Players, and every other element at once, live overlay included.
                </div>
                <ThemePicker activeThemeId={activeThemeId} setActiveThemeId={setActiveThemeId} themes={THEMES} />
              </div>
            )}
          </div>
        </div>

        <div style={{ padding: 24, maxWidth: 1040 }}>
          <ActivePageComponent />
        </div>
      </div>
    </div>
  );
}

export default function GraphicsStudio() {
  return (
    <StudioConfigProvider>
      <ThemeProvider>
        <GraphicsStudioInner />
      </ThemeProvider>
    </StudioConfigProvider>
  );
}
