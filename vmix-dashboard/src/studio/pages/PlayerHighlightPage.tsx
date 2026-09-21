import { useState } from "react";
import { Palette, Type, User } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg } from "../theme";
import { BgEditor, ColumnStyle, ColumnStyleEditor, EditorPanel, PageShell, ResetToThemeButton, pill } from "../StudioControls";
import { HighlightStat, PlayerHighlightRenderer } from "../renderers/PlayerHighlightRenderer";
import { HIGHLIGHT_PRESETS, SAMPLE_HIGHLIGHT_PLAYER } from "../sampleData";

const DEFAULT_FIELDS: Record<string, ColumnStyle> = {
  playerName: { mode: "default", custom: {} }, statValue: { mode: "default", custom: {} },
};

const STAT_LABELS: Record<string, string> = {
  kills: "Eliminations", damage: "Damage", survivalTime: "Survival", assists: "Assists",
};

export default function PlayerHighlightPage() {
  const { theme } = useTheme();
  const [presetId, setPresetId] = useState<(typeof HIGHLIGHT_PRESETS)[number]["id"]>("mvp");
  const [tab, setTab] = useState("canvas");
  const [canvasBgOverride, setCanvasBgOverride] = useStudioElement<Bg | null>("highlight.canvasBg", null);
  const [accentBgOverride, setAccentBgOverride] = useStudioElement<Bg | null>("highlight.accentBg", null);
  const [fields, setFields] = useStudioElement("highlight.fields", DEFAULT_FIELDS);
  // Each preset keeps its own label override, so renaming "MVP OF THE MATCH" doesn't silently
  // rename "STAR PLAYER" too.
  const [labels, setLabels] = useStudioElement<Record<string, string>>("highlight.labels", {});

  const canvasBg = canvasBgOverride || theme.headerBg;
  const accentBg = accentBgOverride || theme.headerBg;
  const preset = HIGHLIGHT_PRESETS.find((p) => p.id === presetId) ?? HIGHLIGHT_PRESETS[0];
  const label = labels[preset.id] || preset.label;
  const setField = (key: string) => (next: ColumnStyle) => setFields((prev) => ({ ...prev, [key]: next }));

  const stats: HighlightStat[] = preset.stats.map((key) => ({
    label: STAT_LABELS[key] ?? key,
    value: (SAMPLE_HIGHLIGHT_PLAYER as unknown as Record<string, string | number>)[key],
  }));

  return (
    <PageShell title="Player Highlight" subtitle="MVP of the Match, Star Player and Player Highlight — one card, three presets">
      <div style={{ flex: "1 1 480px" }}>
        <div style={{ display: "flex", gap: 6, marginBottom: 12, flexWrap: "wrap" }}>
          {HIGHLIGHT_PRESETS.map((p) => (
            <button key={p.id} onClick={() => setPresetId(p.id)} style={pill(presetId === p.id)}>{p.label}</button>
          ))}
        </div>
        <div style={{ width: "100%", maxWidth: 560, aspectRatio: "16/9" }}>
          <PlayerHighlightRenderer
            theme={theme} canvasBg={canvasBg} accentBg={accentBg} label={label}
            playerName={SAMPLE_HIGHLIGHT_PLAYER.playerName}
            teamName={SAMPLE_HIGHLIGHT_PLAYER.teamName}
            stats={stats} fields={fields}
          />
        </div>
        <div style={{ marginTop: 14, padding: 12, borderRadius: 8, fontSize: 12, lineHeight: 1.5, background: "rgba(241,196,15,0.1)", border: "1px solid rgba(241,196,15,0.3)", color: "#f5d76e" }}>
          <b>Needs a backend feed:</b> the numbers already exist in PostMatch.MatchMvp / Top5MVP but
          aren't broadcast live yet. Note that survivalTime, assists and knockouts read 0 until a
          match ends, so this card belongs in the post-match segment, not mid-match.
        </div>
      </div>

      <EditorPanel tabs={[{ id: "canvas", label: "Canvas", icon: Palette }, { id: "accent", label: "Accent", icon: Palette }, { id: "fields", label: "Fields", icon: Type }, { id: "label", label: "Label", icon: User }]} activeTab={tab} setActiveTab={setTab}>
        {tab === "canvas" && (<div><BgEditor bg={canvasBg} setBg={setCanvasBgOverride} />{canvasBgOverride && <ResetToThemeButton onClick={() => setCanvasBgOverride(null)} />}</div>)}
        {tab === "accent" && (<div><div style={{ fontSize: 12, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>The eyebrow banner, capped at 3 gradient stops.</div><BgEditor bg={accentBg} setBg={setAccentBgOverride} maxStops={3} />{accentBgOverride && <ResetToThemeButton onClick={() => setAccentBgOverride(null)} />}</div>)}
        {tab === "fields" && (<div><ColumnStyleEditor label="Player name" col={fields.playerName} setCol={setField("playerName")} /><ColumnStyleEditor label="Stat value" col={fields.statValue} setCol={setField("statValue")} /></div>)}
        {tab === "label" && (
          <label style={{ display: "block", fontSize: 12, color: "#c8c8d0" }}>
            Label for “{preset.label}”
            <input
              value={label}
              onChange={(e) => setLabels((prev) => ({ ...prev, [preset.id]: e.target.value }))}
              style={{ width: "100%", marginTop: 5, background: "rgba(255,255,255,0.06)", border: "1px solid rgba(255,255,255,0.12)", borderRadius: 6, color: "#e8e8ec", padding: "7px 9px", fontSize: 12 }}
            />
          </label>
        )}
      </EditorPanel>
    </PageShell>
  );
}
