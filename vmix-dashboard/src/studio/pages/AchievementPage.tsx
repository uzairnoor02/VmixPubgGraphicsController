import { useEffect, useState } from "react";
import { Palette, Play } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useChromaKey, useStudioElement } from "../StudioConfigContext";
import { Bg } from "../theme";
import { BgEditor, EditorPanel, PageShell, ResetToThemeButton, btnGhost, pill } from "../StudioControls";
import { ACHIEVEMENT_TYPES } from "../sampleData";
import { AchievementRenderer, DEFAULT_ACHIEVEMENT_BODY_BG, achievementIcon } from "../renderers/AchievementRenderer";

export default function AchievementPage() {
  const { theme } = useTheme();
  const [chromaKey] = useChromaKey();
  const [activeType, setActiveType] = useState(ACHIEVEMENT_TYPES[0]);
  const [playKey, setPlayKey] = useState(0);
  const [visible, setVisible] = useState(false);
  const [tab, setTab] = useState("accent");
  const [accentBgOverride, setAccentBgOverride] = useStudioElement<Bg | null>("achievement.accentBg", null);
  const [bodyBgOverride, setBodyBgOverride] = useStudioElement<Bg | null>("achievement.bodyBg", null);
  const accentBg = accentBgOverride || theme.headerBg;
  const bodyBg: Bg = bodyBgOverride || DEFAULT_ACHIEVEMENT_BODY_BG;
  const sample = { causer: "NOVA HHAAMMSIG", victim: "ECHO Frentzy", distance: 187, team: "NOVA" };

  useEffect(() => {
    setVisible(false);
    const t1 = setTimeout(() => setVisible(true), 50);
    const t2 = setTimeout(() => setVisible(false), 3000);
    return () => { clearTimeout(t1); clearTimeout(t2); };
  }, [playKey, activeType]);

  return (
    <PageShell title="Achievement Banner" subtitle="First blood, long range, grenade and vehicle elims - left side, below the in-game team panel">
      <div style={{ flex: "1 1 400px" }}>
        <div style={{ display: "flex", gap: 6, marginBottom: 12, flexWrap: "wrap" }}>
          {ACHIEVEMENT_TYPES.map((a) => (<button key={a.id} onClick={() => setActiveType(a)} style={pill(activeType.id === a.id)}>{a.label}</button>))}
        </div>
        <div style={{ width: "100%", maxWidth: 520, aspectRatio: "16/9", borderRadius: theme.radius, overflow: "hidden", background: chromaKey, display: "flex", alignItems: "center", justifyContent: "flex-start", padding: "24px 0", border: "1px solid rgba(255,255,255,0.12)", position: "relative" }}>
          <div style={{ position: "absolute", fontSize: 10, color: "rgba(0,0,0,0.4)", top: 8, left: 10 }}>keyed out by vMix in production</div>
          <div style={{ width: "82%" }}>
            <AchievementRenderer
              theme={theme} accentBg={accentBg} bodyBg={bodyBg} visible={visible}
              label={activeType.label.toUpperCase()}
              primary={sample.causer}
              victim={activeType.id === "long_range" ? undefined : sample.victim}
              detail={activeType.id === "long_range" ? `${sample.distance}m` : undefined}
              teamName={sample.team}
              icon={achievementIcon(activeType.id, 40)}
            />
          </div>
        </div>
        <button onClick={() => setPlayKey((k) => k + 1)} style={{ ...btnGhost, marginTop: 12, display: "flex", alignItems: "center", gap: 6 }}><Play size={13} /> Replay animation</button>
        <div style={{ marginTop: 14, padding: 12, borderRadius: 8, fontSize: 12, lineHeight: 1.5, background: activeType.dataSource === "real" ? "rgba(46,204,113,0.1)" : "rgba(241,196,15,0.1)", border: `1px solid ${activeType.dataSource === "real" ? "rgba(46,204,113,0.3)" : "rgba(241,196,15,0.3)"}`, color: activeType.dataSource === "real" ? "#7ee8a8" : "#f5d76e" }}>
          <b>{activeType.dataSource === "real" ? "Fully real data" : "Partial — needs cross-referencing"}:</b> {activeType.note}
        </div>
      </div>

      <EditorPanel tabs={[{ id: "accent", label: "Accent", icon: Palette }, { id: "body", label: "Body", icon: Palette }]} activeTab={tab} setActiveTab={setTab}>
        {tab === "accent" && (
          <div>
            <div style={{ fontSize: 12, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>The big label colour, the photo backdrop and the name strip. Capped at 3 gradient stops.</div>
            <BgEditor bg={accentBg} setBg={setAccentBgOverride} maxStops={3} />
            {accentBgOverride && <ResetToThemeButton onClick={() => setAccentBgOverride(null)} />}
          </div>
        )}
        {tab === "body" && (
          <div>
            <div style={{ fontSize: 12, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>Background behind the big label (near-white by default, like the PMGO banner).</div>
            <BgEditor bg={bodyBg} setBg={setBodyBgOverride} />
            {bodyBgOverride && <ResetToThemeButton onClick={() => setBodyBgOverride(null)} />}
          </div>
        )}
      </EditorPanel>
    </PageShell>
  );
}
