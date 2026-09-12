import { useEffect, useState } from "react";
import { Car, Crosshair, Palette, Play, Skull, Zap } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg, bgCss } from "../theme";
import { BgEditor, EditorPanel, PageShell, ResetToThemeButton, btnGhost, pill } from "../StudioControls";
import { ACHIEVEMENT_TYPES } from "../sampleData";

const ICONS: Record<string, typeof Crosshair> = { first_blood: Crosshair, long_range: Zap, grenade_master: Skull, vehicle_kill: Car };

export default function AchievementPage() {
  const { theme } = useTheme();
  const [activeType, setActiveType] = useState(ACHIEVEMENT_TYPES[0]);
  const [playKey, setPlayKey] = useState(0);
  const [visible, setVisible] = useState(false);
  const [tab, setTab] = useState("accent");
  const [accentBgOverride, setAccentBgOverride] = useStudioElement<Bg | null>("achievement.accentBg", null);
  const accentBg = accentBgOverride || theme.headerBg;
  const Icon = ICONS[activeType.id] ?? Crosshair;
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
        <div style={{ width: "100%", maxWidth: 400, aspectRatio: "16/9", borderRadius: theme.radius, overflow: "hidden", background: theme.chromaKey, display: "flex", alignItems: "flex-end", justifyContent: "center", padding: 24, border: "1px solid rgba(255,255,255,0.12)", position: "relative" }}>
          <div style={{ position: "absolute", fontSize: 10, color: "rgba(0,0,0,0.4)", top: 8, left: 10 }}>keyed out by vMix in production</div>
          <div style={{ width: "88%", transform: visible ? "translateY(0)" : "translateY(30px)", opacity: visible ? 1 : 0, transition: "all 0.35s cubic-bezier(0.34, 1.56, 0.64, 1)", display: "flex", alignItems: "center", gap: 10, background: "rgba(20,20,26,0.8)", backdropFilter: `blur(${theme.panelBlur})`, WebkitBackdropFilter: `blur(${theme.panelBlur})`, borderRadius: 8, padding: "10px 14px" } as any}>
            <div style={{ width: 40, height: 40, borderRadius: 8, background: bgCss(accentBg), display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}><Icon size={20} color="#0a0a0f" /></div>
            <div style={{ minWidth: 0 }}>
              <div style={{ fontSize: 11, fontWeight: 700, color: theme.textPrimary, fontFamily: theme.fontDisplay, letterSpacing: 0.5 }}>{activeType.label.toUpperCase()}</div>
              <div style={{ fontSize: 15, fontWeight: 700, color: theme.textPrimary, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{sample.causer}</div>
              <div style={{ fontSize: 11, color: "rgba(255,255,255,0.6)" }}>eliminated {sample.victim}{activeType.id === "long_range" ? ` at ${sample.distance}m` : ""}</div>
            </div>
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
