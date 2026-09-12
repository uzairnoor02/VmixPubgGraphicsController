import { useEffect, useState } from "react";
import { Palette, Play } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg, bgCss } from "../theme";
import { BgEditor, EditorPanel, PageShell, ResetToThemeButton, btnGhost, pill } from "../StudioControls";
import { SAMPLE_SIDEBAR_TEAMS } from "../sampleData";

export default function EliminatedSidebarPage() {
  const { theme } = useTheme();
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
            <div style={{ width: "100%", maxWidth: 560, aspectRatio: "16/9", borderRadius: theme.radius, overflow: "hidden", background: theme.chromaKey, display: "flex", alignItems: "center", justifyContent: "center", border: "1px solid rgba(255,255,255,0.12)", position: "relative" }}>
              <div style={{ position: "absolute", fontSize: 11, color: "rgba(0,0,0,0.4)", top: 8, left: 10 }}>keyed out by vMix in production</div>
              <div style={{ width: "72%", transform: visible ? "scale(1) translateY(0)" : "scale(0.85) translateY(-12px)", opacity: visible ? 1 : 0, transition: "all 0.4s cubic-bezier(0.34, 1.56, 0.64, 1)" }}>
                <div style={{ background: bgCss(bannerBg), padding: "10px 20px", clipPath: "polygon(0 0, 100% 0, 96% 100%, 0% 100%)" }}><div style={{ fontSize: 22, fontWeight: 800, color: theme.headerTextColor, fontFamily: theme.fontDisplay, letterSpacing: 0.5 }}>TEAM ELIMINATED</div></div>
                <div style={{ background: theme.panelBg, backdropFilter: `blur(${theme.panelBlur})`, WebkitBackdropFilter: `blur(${theme.panelBlur})`, display: "flex", alignItems: "center", gap: 10, padding: "8px 16px" } as any}><div style={{ width: 32, height: 32, borderRadius: 4, background: "rgba(255,255,255,0.15)", flexShrink: 0 }} /><div style={{ fontSize: 15, fontWeight: 700, color: theme.textPrimary }}>{eliminatedTeam.teamName}</div></div>
              </div>
            </div>
            <button onClick={() => setPlayKey((k) => k + 1)} style={{ ...btnGhost, marginTop: 12, display: "flex", alignItems: "center", gap: 6 }}><Play size={13} /> Replay animation</button>
          </>
        ) : (
          <div style={{ width: "100%", maxWidth: 260, borderRadius: theme.radius, overflow: "hidden", fontFamily: theme.fontDisplay, border: "1px solid rgba(255,255,255,0.12)", boxShadow: theme.glow }}>
            <div style={{ display: "flex", background: bgCss(theme.headerBg), color: theme.headerTextColor, fontWeight: 700, fontSize: 11, padding: "5px 10px" }}><div style={{ width: 20 }}>#</div><div style={{ flex: 1 }}>TEAM</div><div style={{ width: 44, textAlign: "center" }}>PTS</div><div style={{ width: 36, textAlign: "center" }}>ELIM</div></div>
            <div style={{ background: theme.panelBg, backdropFilter: `blur(${theme.panelBlur})`, WebkitBackdropFilter: `blur(${theme.panelBlur})` } as any}>
              {SAMPLE_SIDEBAR_TEAMS.map((t) => (<div key={t.teamId} style={{ display: "flex", alignItems: "center", padding: "5px 10px", borderTop: "1px solid rgba(255,255,255,0.05)", fontSize: 12 }}><div style={{ width: 20, fontWeight: 700, color: theme.textPrimary }}>{t.rank}</div><div style={{ flex: 1, display: "flex", alignItems: "center", gap: 6, overflow: "hidden" }}><div style={{ width: 16, height: 16, borderRadius: 3, background: "rgba(255,255,255,0.15)", flexShrink: 0 }} /><span style={{ color: theme.textPrimary, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{t.teamName}</span></div><div style={{ width: 44, textAlign: "center", color: theme.textPrimary, fontWeight: 600 }}>{t.rank <= 8 ? 100 - t.rank * 4 : "-"}</div><div style={{ width: 36, textAlign: "center", color: theme.textPrimary, fontWeight: 600 }}>{t.kills}</div></div>))}
            </div>
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
