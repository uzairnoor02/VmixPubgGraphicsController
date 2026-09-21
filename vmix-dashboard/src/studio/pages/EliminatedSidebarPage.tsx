import { useEffect, useState } from "react";
import { Palette, Play } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useChromaKey, useStudioElement } from "../StudioConfigContext";
import { Bg } from "../theme";
import { BgEditor, EditorPanel, PageShell, ResetToThemeButton, btnGhost, pill } from "../StudioControls";
import { SAMPLE_SIDEBAR_TEAMS } from "../sampleData";
import { EliminatedBannerRenderer, SidebarRenderer } from "../renderers/EliminatedSidebarRenderer";

export default function EliminatedSidebarPage() {
  const { theme } = useTheme();
  const [chromaKey] = useChromaKey();
  const [view, setView] = useState<"eliminated" | "sidebar">("eliminated");
  const [playKey, setPlayKey] = useState(0);
  const [visible, setVisible] = useState(false);
  const [tab, setTab] = useState("banner");
  const [bannerBgOverride, setBannerBgOverride] = useStudioElement<Bg | null>("eliminated.bannerBg", null);
  const bannerBg = bannerBgOverride || theme.headerBg;
  const eliminatedTeam = SAMPLE_SIDEBAR_TEAMS[6];

  useEffect(() => {
    if (view !== "eliminated") return;
    setVisible(false);
    const t1 = setTimeout(() => setVisible(true), 50);
    const t2 = setTimeout(() => setVisible(false), 3200);
    return () => { clearTimeout(t1); clearTimeout(t2); };
  }, [playKey, view]);

  return (
    <PageShell title="Team Eliminated & Sidebar" subtitle="Full-screen elimination banner and always-on live sidebar">
      <div style={{ flex: "1 1 480px" }}>
        <div style={{ display: "flex", gap: 6, marginBottom: 12 }}>
          <button onClick={() => setView("eliminated")} style={pill(view === "eliminated")}>Team Eliminated banner</button>
          <button onClick={() => setView("sidebar")} style={pill(view === "sidebar")}>Live sidebar leaderboard</button>
        </div>
        {view === "eliminated" ? (
          <>
            <div style={{ width: "100%", maxWidth: 560, aspectRatio: "16/9", borderRadius: theme.radius, overflow: "hidden", background: chromaKey, display: "flex", alignItems: "center", justifyContent: "center", border: "1px solid rgba(255,255,255,0.12)", position: "relative" }}>
              <div style={{ position: "absolute", fontSize: 11, color: "rgba(0,0,0,0.4)", top: 8, left: 10 }}>keyed out by vMix in production</div>
              <div style={{ width: "72%" }}>
                <EliminatedBannerRenderer theme={theme} bannerBg={bannerBg} teamName={eliminatedTeam.teamName} visible={visible} />
              </div>
            </div>
            <button onClick={() => setPlayKey((k) => k + 1)} style={{ ...btnGhost, marginTop: 12, display: "flex", alignItems: "center", gap: 6 }}><Play size={13} /> Replay animation</button>
          </>
        ) : (
          <div style={{ width: "100%", maxWidth: 260 }}>
            <SidebarRenderer
              theme={theme}
              headerBg={theme.headerBg}
              rows={SAMPLE_SIDEBAR_TEAMS.map((t) => ({ key: t.teamId, rank: t.rank, teamName: t.teamName, points: t.rank <= 8 ? 100 - t.rank * 4 : null, kills: t.kills }))}
            />
          </div>
        )}
      </div>

      <EditorPanel tabs={[{ id: "banner", label: "Banner", icon: Palette }]} activeTab={tab} setActiveTab={setTab}>
        <div style={{ fontSize: 12, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>The "TEAM ELIMINATED" background, capped at 3 gradient stops.</div>
        <BgEditor bg={bannerBg} setBg={setBannerBgOverride} maxStops={3} />
        {bannerBgOverride && <ResetToThemeButton onClick={() => setBannerBgOverride(null)} />}
      </EditorPanel>
    </PageShell>
  );
}
