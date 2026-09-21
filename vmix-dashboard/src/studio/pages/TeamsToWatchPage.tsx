import { useState } from "react";
import { Palette, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg } from "../theme";
import { BgEditor, ColumnStyle, ColumnStyleEditor, EditorPanel, PageShell, ResetToThemeButton } from "../StudioControls";
import { TeamsToWatchRenderer } from "../renderers/TeamsToWatchRenderer";
import { SAMPLE_TEAMS_TO_WATCH } from "../sampleData";

const DEFAULT_FIELDS: Record<string, ColumnStyle> = {
  teamName: { mode: "default", custom: {} }, reason: { mode: "default", custom: {} },
};

export default function TeamsToWatchPage() {
  const { theme } = useTheme();
  const [tab, setTab] = useState("canvas");
  const [canvasBgOverride, setCanvasBgOverride] = useStudioElement<Bg | null>("teamsToWatch.canvasBg", null);
  const [accentBgOverride, setAccentBgOverride] = useStudioElement<Bg | null>("teamsToWatch.accentBg", null);
  const [fields, setFields] = useStudioElement("teamsToWatch.fields", DEFAULT_FIELDS);
  const [title, setTitle] = useStudioElement("teamsToWatch.title", "TEAMS TO WATCH");
  const [subtitle, setSubtitle] = useStudioElement("teamsToWatch.subtitle", "SEMIFINALS — DAY 2");

  const canvasBg = canvasBgOverride || theme.headerBg;
  const accentBg = accentBgOverride || theme.headerBg;
  const setField = (key: string) => (next: ColumnStyle) => setFields((prev) => ({ ...prev, [key]: next }));

  return (
    <PageShell title="Teams to Watch" subtitle="Pre-match card highlighting the teams that matter">
      <div style={{ flex: "1 1 520px" }}>
        <div style={{ width: "100%", maxWidth: 600, aspectRatio: "16/9" }}>
          <TeamsToWatchRenderer theme={theme} canvasBg={canvasBg} accentBg={accentBg} title={title} subtitle={subtitle} teams={SAMPLE_TEAMS_TO_WATCH} fields={fields} />
        </div>
        <div style={{ marginTop: 14, padding: 12, borderRadius: 8, fontSize: 12, lineHeight: 1.5, background: "rgba(241,196,15,0.1)", border: "1px solid rgba(241,196,15,0.3)", color: "#f5d76e" }}>
          <b>Needs a backend feed:</b> PostMatch.TeamToWatch.cs already picks the teams, but nothing
          broadcasts them yet. The overlay slot listens for <code>TeamsToWatchUpdated</code>.
        </div>
      </div>

      <EditorPanel tabs={[{ id: "canvas", label: "Canvas", icon: Palette }, { id: "accent", label: "Accent", icon: Palette }, { id: "fields", label: "Fields", icon: Type }, { id: "text", label: "Text", icon: Type }]} activeTab={tab} setActiveTab={setTab}>
        {tab === "canvas" && (<div><BgEditor bg={canvasBg} setBg={setCanvasBgOverride} />{canvasBgOverride && <ResetToThemeButton onClick={() => setCanvasBgOverride(null)} />}</div>)}
        {tab === "accent" && (<div><div style={{ fontSize: 12, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>Card headers and the subtitle chip, capped at 3 gradient stops.</div><BgEditor bg={accentBg} setBg={setAccentBgOverride} maxStops={3} />{accentBgOverride && <ResetToThemeButton onClick={() => setAccentBgOverride(null)} />}</div>)}
        {tab === "fields" && (<div><ColumnStyleEditor label="Team name" col={fields.teamName} setCol={setField("teamName")} /><ColumnStyleEditor label="Reason text" col={fields.reason} setCol={setField("reason")} /></div>)}
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
