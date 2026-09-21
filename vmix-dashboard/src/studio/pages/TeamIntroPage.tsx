import { useState } from "react";
import { Palette, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg, DEFAULT_HEALTH_STOPS, HealthStop } from "../theme";
import { BgEditor, ColumnStyle, ColumnStyleEditor, EditorPanel, PageShell, ResetToThemeButton, pill } from "../StudioControls";
import { TeamIntroRenderer } from "../renderers/TeamIntroRenderer";
import { SAMPLE_TEAM_INTRO } from "../sampleData";

const DEFAULT_FIELDS: Record<string, ColumnStyle> = {
  teamName: { mode: "default", custom: {} }, statValue: { mode: "default", custom: {} },
};

export default function TeamIntroPage() {
  const { theme } = useTheme();
  const [mode, setMode] = useState<"live" | "intro">("live");
  const [tab, setTab] = useState("canvas");
  const [canvasBgOverride, setCanvasBgOverride] = useStudioElement<Bg | null>("teamIntro.canvasBg", null);
  const [accentBgOverride, setAccentBgOverride] = useStudioElement<Bg | null>("teamIntro.accentBg", null);
  const [fields, setFields] = useStudioElement("teamIntro.fields", DEFAULT_FIELDS);
  const [label, setLabel] = useStudioElement("teamIntro.label", "TEAM SPOTLIGHT");

  const canvasBg = canvasBgOverride || theme.headerBg;
  const accentBg = accentBgOverride || theme.headerBg;
  const setField = (key: string) => (next: ColumnStyle) => setFields((prev) => ({ ...prev, [key]: next }));
  const healthStops: HealthStop[] = DEFAULT_HEALTH_STOPS;

  return (
    <PageShell title="Team Intro / WWCD" subtitle="One team with roster and win probability">
      <div style={{ flex: "1 1 500px" }}>
        <div style={{ display: "flex", gap: 6, marginBottom: 12 }}>
          <button onClick={() => setMode("live")} style={pill(mode === "live")}>Mid-match (live WWCD)</button>
          <button onClick={() => setMode("intro")} style={pill(mode === "intro")}>Pre-match intro</button>
        </div>
        <div style={{ width: "100%", maxWidth: 560, aspectRatio: "16/9" }}>
          <TeamIntroRenderer
            theme={theme} canvasBg={canvasBg} accentBg={accentBg} label={label}
            teamName={SAMPLE_TEAM_INTRO.teamName}
            // A pre-match intro has no live figure, so the bar is hidden rather than showing 0%.
            wwcd={mode === "live" ? SAMPLE_TEAM_INTRO.wwcd : null}
            players={mode === "live" ? SAMPLE_TEAM_INTRO.players : SAMPLE_TEAM_INTRO.players.map((p) => ({ playerName: p.playerName }))}
            stats={SAMPLE_TEAM_INTRO.stats}
            healthStops={mode === "live" ? healthStops : undefined}
            fields={fields}
          />
        </div>
        <div style={{ marginTop: 14, padding: 12, borderRadius: 8, fontSize: 12, lineHeight: 1.5, background: "rgba(46,204,113,0.1)", border: "1px solid rgba(46,204,113,0.3)", color: "#7ee8a8" }}>
          <b>Data confirmed available:</b> the WWCD figure is the same one CreateTop4LiveRanking
          already computes for the Top 4 panel, so the two can't disagree. Needs a
          <code>TeamIntroUpdated</code> broadcast carrying the selected team.
        </div>
      </div>

      <EditorPanel tabs={[{ id: "canvas", label: "Canvas", icon: Palette }, { id: "accent", label: "Accent", icon: Palette }, { id: "fields", label: "Fields", icon: Type }, { id: "label", label: "Label", icon: Type }]} activeTab={tab} setActiveTab={setTab}>
        {tab === "canvas" && (<div><BgEditor bg={canvasBg} setBg={setCanvasBgOverride} />{canvasBgOverride && <ResetToThemeButton onClick={() => setCanvasBgOverride(null)} />}</div>)}
        {tab === "accent" && (<div><div style={{ fontSize: 12, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>Badge and the win-probability bar, capped at 3 gradient stops.</div><BgEditor bg={accentBg} setBg={setAccentBgOverride} maxStops={3} />{accentBgOverride && <ResetToThemeButton onClick={() => setAccentBgOverride(null)} />}</div>)}
        {tab === "fields" && (<div><ColumnStyleEditor label="Team name" col={fields.teamName} setCol={setField("teamName")} /><ColumnStyleEditor label="Stat value" col={fields.statValue} setCol={setField("statValue")} /></div>)}
        {tab === "label" && (
          <label style={{ display: "block", fontSize: 12, color: "#c8c8d0" }}>Badge text
            <input value={label} onChange={(e) => setLabel(e.target.value)} style={{ width: "100%", marginTop: 5, background: "rgba(255,255,255,0.06)", border: "1px solid rgba(255,255,255,0.12)", borderRadius: 6, color: "#e8e8ec", padding: "7px 9px", fontSize: 12 }} />
          </label>
        )}
      </EditorPanel>
    </PageShell>
  );
}
