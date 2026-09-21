import { useEffect, useState } from "react";
import { Palette, Play } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useChromaKey, useStudioElement } from "../StudioConfigContext";
import { Bg } from "../theme";
import { BgEditor, EditorPanel, PageShell, ResetToThemeButton, btnGhost, pill } from "../StudioControls";
import { ACHIEVEMENT_TYPES } from "../sampleData";
import { AchievementRenderer, achievementIcon } from "../renderers/AchievementRenderer";

export default function AchievementPage() {
  const { theme } = useTheme();
  const [chromaKey] = useChromaKey();
  const [activeType, setActiveType] = useState(ACHIEVEMENT_TYPES[0]);
  const [playKey, setPlayKey] = useState(0);
  const [visible, setVisible] = useState(false);
  const [tab, setTab] = useState("accent");
  const [accentBgOverride, setAccentBgOverride] = useStudioElement<Bg | null>("achievement.accentBg", null);
  const accentBg = accentBgOverride || theme.headerBg;
  const sample = { causer: "Asi8CASANOVA", victim: "R3G・ROSHAAN", distance: 187 };

  useEffect(() => {
    setVisible(false);
    const t1 = setTimeout(() => setVisible(true), 50);
    const t2 = setTimeout(() => setVisible(false), 3000);
    return () => { clearTimeout(t1); clearTimeout(t2); };
  }, [playKey, activeType]);

  return (
    <PageShell title="Achievement Popup" subtitle="First blood, long range, grenade and vehicle elims">
      <div style={{ flex: "1 1 400px" }}>
        <div style={{ display: "flex", gap: 6, marginBottom: 12, flexWrap: "wrap" }}>
          {ACHIEVEMENT_TYPES.map((a) => (<button key={a.id} onClick={() => setActiveType(a)} style={pill(activeType.id === a.id)}>{a.label}</button>))}
        </div>
        <div style={{ width: "100%", maxWidth: 400, aspectRatio: "16/9", borderRadius: theme.radius, overflow: "hidden", background: chromaKey, display: "flex", alignItems: "flex-end", justifyContent: "center", padding: 24, border: "1px solid rgba(255,255,255,0.12)", position: "relative" }}>
          <div style={{ position: "absolute", fontSize: 10, color: "rgba(0,0,0,0.4)", top: 8, left: 10 }}>keyed out by vMix in production</div>
          <div style={{ width: "88%" }}>
            <AchievementRenderer
              theme={theme} accentBg={accentBg} visible={visible}
              label={activeType.label.toUpperCase()}
              primary={sample.causer}
              detail={`eliminated ${sample.victim}${activeType.id === "long_range" ? ` at ${sample.distance}m` : ""}`}
              icon={achievementIcon(activeType.id)}
            />
          </div>
        </div>
        <button onClick={() => setPlayKey((k) => k + 1)} style={{ ...btnGhost, marginTop: 12, display: "flex", alignItems: "center", gap: 6 }}><Play size={13} /> Replay animation</button>
        <div style={{ marginTop: 14, padding: 12, borderRadius: 8, fontSize: 12, lineHeight: 1.5, background: activeType.dataSource === "real" ? "rgba(46,204,113,0.1)" : "rgba(241,196,15,0.1)", border: `1px solid ${activeType.dataSource === "real" ? "rgba(46,204,113,0.3)" : "rgba(241,196,15,0.3)"}`, color: activeType.dataSource === "real" ? "#7ee8a8" : "#f5d76e" }}>
          <b>{activeType.dataSource === "real" ? "Fully real data" : "Partial — needs cross-referencing"}:</b> {activeType.note}
        </div>
      </div>

      <EditorPanel tabs={[{ id: "accent", label: "Accent", icon: Palette }]} activeTab={tab} setActiveTab={setTab}>
        <div style={{ fontSize: 12, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>The icon badge background, capped at 3 gradient stops.</div>
        <BgEditor bg={accentBg} setBg={setAccentBgOverride} maxStops={3} />
        {accentBgOverride && <ResetToThemeButton onClick={() => setAccentBgOverride(null)} />}
      </EditorPanel>
    </PageShell>
  );
}
