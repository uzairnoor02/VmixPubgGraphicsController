import { useState } from "react";
import { Palette, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg } from "../theme";
import { BgEditor, ColumnStyle, ColumnStyleEditor, EditorPanel, PageShell, ResetToThemeButton } from "../StudioControls";
import { HeadToHeadRenderer } from "../renderers/HeadToHeadRenderer";
import { SAMPLE_HEAD_TO_HEAD } from "../sampleData";

const DEFAULT_FIELDS: Record<string, ColumnStyle> = {
  teamName: { mode: "default", custom: {} }, statValue: { mode: "default", custom: {} },
};

export default function HeadToHeadPage() {
  const { theme } = useTheme();
  const [tab, setTab] = useState("canvas");
  const [canvasBgOverride, setCanvasBgOverride] = useStudioElement<Bg | null>("h2h.canvasBg", null);
  const [accentBgOverride, setAccentBgOverride] = useStudioElement<Bg | null>("h2h.accentBg", null);
  const [fields, setFields] = useStudioElement("h2h.fields", DEFAULT_FIELDS);
  const [title, setTitle] = useStudioElement("h2h.title", "HEAD TO HEAD");
  const [subtitle, setSubtitle] = useStudioElement("h2h.subtitle", "SEMIFINALS — 4 MATCHES");

  const canvasBg = canvasBgOverride || theme.headerBg;
  const accentBg = accentBgOverride || theme.headerBg;
  const setField = (key: string) => (next: ColumnStyle) => setFields((prev) => ({ ...prev, [key]: next }));

  return (
    <PageShell title="Head to Head" subtitle="Two teams compared across the stage">
      <div style={{ flex: "1 1 520px" }}>
        <div style={{ width: "100%", maxWidth: 600, aspectRatio: "16/9" }}>
          <HeadToHeadRenderer theme={theme} canvasBg={canvasBg} accentBg={accentBg} title={title} subtitle={subtitle}
            left={SAMPLE_HEAD_TO_HEAD.left} right={SAMPLE_HEAD_TO_HEAD.right} stats={SAMPLE_HEAD_TO_HEAD.stats} fields={fields} />
        </div>
        <div style={{ marginTop: 14, padding: 12, borderRadius: 8, fontSize: 12, lineHeight: 1.5, background: "rgba(46,204,113,0.1)", border: "1px solid rgba(46,204,113,0.3)", color: "#7ee8a8" }}>
          <b>Data confirmed available:</b> the TeamPoints table stores one row per team per match
          (WWCD, placement, kill and total points), which is exactly what this needs. A query plus a
          <code>HeadToHeadUpdated</code> broadcast is all that's left.
        </div>
      </div>

      <EditorPanel tabs={[{ id: "canvas", label: "Canvas", icon: Palette }, { id: "accent", label: "Accent", icon: Palette }, { id: "fields", label: "Fields", icon: Type }, { id: "text", label: "Text", icon: Type }]} activeTab={tab} setActiveTab={setTab}>
        {tab === "canvas" && (<div><BgEditor bg={canvasBg} setBg={setCanvasBgOverride} />{canvasBgOverride && <ResetToThemeButton onClick={() => setCanvasBgOverride(null)} />}</div>)}
        {tab === "accent" && (<div><BgEditor bg={accentBg} setBg={setAccentBgOverride} maxStops={3} />{accentBgOverride && <ResetToThemeButton onClick={() => setAccentBgOverride(null)} />}</div>)}
        {tab === "fields" && (<div><ColumnStyleEditor label="Team name" col={fields.teamName} setCol={setField("teamName")} /><ColumnStyleEditor label="Stat value" col={fields.statValue} setCol={setField("statValue")} /></div>)}
        {tab === "text" && (
          <div>
            {[{ label: "Title", value: title, set: setTitle }, { label: "Subtitle", value: subtitle, set: setSubtitle }].map((row) => (
              <label key={row.label} style={{ display: "block", marginBottom: 12, fontSize: 12, color: "#c8c8d0" }}>
                {row.label}
                <input value={row.value} onChange={(e) => row.set(e.target.value)} style={{ width: "100%", marginTop: 5, background: "rgba(255,255,255,0.06)", border: "1px solid rgba(255,255,255,0.12)", borderRadius: 6, color: "#e8e8ec", padding: "7px 9px", fontSize: 12 }} />
              </label>
            ))}
          </div>
        )}
      </EditorPanel>
    </PageShell>
  );
}
