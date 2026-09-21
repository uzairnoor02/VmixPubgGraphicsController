import { useState } from "react";
import { Palette, Play, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg } from "../theme";
import { BgEditor, ColumnStyle, ColumnStyleEditor, EditorPanel, PageShell, ResetToThemeButton, btnGhost } from "../StudioControls";
import { ChampionsRenderer } from "../renderers/ChampionsRenderer";
import { SAMPLE_CHAMPIONS } from "../sampleData";

const DEFAULT_FIELDS: Record<string, ColumnStyle> = {
  teamName: { mode: "default", custom: {} }, statValue: { mode: "default", custom: {} },
};

export default function ChampionsPage() {
  const { theme } = useTheme();
  const [tab, setTab] = useState("canvas");
  const [playKey, setPlayKey] = useState(0);
  const [canvasBgOverride, setCanvasBgOverride] = useStudioElement<Bg | null>("champions.canvasBg", null);
  const [accentBgOverride, setAccentBgOverride] = useStudioElement<Bg | null>("champions.accentBg", null);
  const [fields, setFields] = useStudioElement("champions.fields", DEFAULT_FIELDS);
  const [label, setLabel] = useStudioElement("champions.label", "CHAMPIONS");

  const canvasBg = canvasBgOverride || theme.headerBg;
  const accentBg = accentBgOverride || theme.headerBg;
  const setField = (key: string) => (next: ColumnStyle) => setFields((prev) => ({ ...prev, [key]: next }));

  return (
    <PageShell title="Champions" subtitle="End-of-event celebration card">
      <div style={{ flex: "1 1 520px" }}>
        {/* key forces a remount so the entrance animation replays on demand. */}
        <div key={playKey} style={{ width: "100%", maxWidth: 620, aspectRatio: "16/9" }}>
          <ChampionsRenderer theme={theme} canvasBg={canvasBg} accentBg={accentBg} label={label}
            teamName={SAMPLE_CHAMPIONS.teamName} players={SAMPLE_CHAMPIONS.players}
            stats={SAMPLE_CHAMPIONS.stats} fields={fields} />
        </div>
        <button onClick={() => setPlayKey((k) => k + 1)} style={{ ...btnGhost, marginTop: 12, display: "flex", alignItems: "center", gap: 6 }}>
          <Play size={13} /> Replay animation
        </button>
        <div style={{ marginTop: 14, padding: 12, borderRadius: 8, fontSize: 12, lineHeight: 1.5, background: "rgba(241,196,15,0.1)", border: "1px solid rgba(241,196,15,0.3)", color: "#f5d76e" }}>
          <b>Needs a backend feed:</b> the winner is known from the final standings, but nothing
          broadcasts it yet. The overlay slot listens for <code>ChampionsUpdated</code>.
        </div>
      </div>

      <EditorPanel tabs={[{ id: "canvas", label: "Canvas", icon: Palette }, { id: "accent", label: "Accent", icon: Palette }, { id: "fields", label: "Fields", icon: Type }, { id: "label", label: "Label", icon: Type }]} activeTab={tab} setActiveTab={setTab}>
        {tab === "canvas" && (<div><BgEditor bg={canvasBg} setBg={setCanvasBgOverride} />{canvasBgOverride && <ResetToThemeButton onClick={() => setCanvasBgOverride(null)} />}</div>)}
        {tab === "accent" && (<div><div style={{ fontSize: 12, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>The “CHAMPIONS” badge, capped at 3 gradient stops.</div><BgEditor bg={accentBg} setBg={setAccentBgOverride} maxStops={3} />{accentBgOverride && <ResetToThemeButton onClick={() => setAccentBgOverride(null)} />}</div>)}
        {tab === "fields" && (<div><ColumnStyleEditor label="Team name" col={fields.teamName} setCol={setField("teamName")} /><ColumnStyleEditor label="Stat value" col={fields.statValue} setCol={setField("statValue")} /></div>)}
        {tab === "label" && (
          <label style={{ display: "block", fontSize: 12, color: "#c8c8d0" }}>
            Badge text
            <input value={label} onChange={(e) => setLabel(e.target.value)} style={{ width: "100%", marginTop: 5, background: "rgba(255,255,255,0.06)", border: "1px solid rgba(255,255,255,0.12)", borderRadius: 6, color: "#e8e8ec", padding: "7px 9px", fontSize: 12 }} />
          </label>
        )}
      </EditorPanel>
    </PageShell>
  );
}
