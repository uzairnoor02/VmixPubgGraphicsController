import { useState } from "react";
import { Palette, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg } from "../theme";
import { BgEditor, ColumnStyle, ColumnStyleEditor, EditorPanel, PageShell, ResetToThemeButton, pill } from "../StudioControls";
import { SAMPLE_TOP5_DAMAGE, SAMPLE_TOP5_KILLS } from "../sampleData";
import { TopPlayersRenderer } from "../renderers/TopPlayersRenderer";

const DEFAULT_FIELDS: Record<string, ColumnStyle> = {
  playerName: { mode: "default", custom: {} }, statValue: { mode: "default", custom: {} },
};

export default function TopPlayersPage() {
  const { theme } = useTheme();
  const [statType, setStatType] = useState<"kills" | "damage">("kills");
  const [tab, setTab] = useState("canvas");
  const [canvasBgOverride, setCanvasBgOverride] = useStudioElement<Bg | null>("topPlayers.canvasBg", null);
  const [labelBgOverride, setLabelBgOverride] = useStudioElement<Bg | null>("topPlayers.labelBg", null);
  const [cardBg, setCardBg] = useStudioElement<Bg>("topPlayers.cardBg", { type: "solid", color: "#0a0a0a" });
  const [fields, setFields] = useStudioElement("topPlayers.fields", DEFAULT_FIELDS);

  const canvasBg = canvasBgOverride || theme.headerBg;
  const labelBg = labelBgOverride || theme.headerBg;
  const players = statType === "kills" ? SAMPLE_TOP5_KILLS : SAMPLE_TOP5_DAMAGE;
  const setField = (key: string) => (next: ColumnStyle) => setFields((prev) => ({ ...prev, [key]: next }));

  return (
    <PageShell title="Top Players" subtitle="Podium-style leaderboard for kills or damage">
      <div style={{ flex: "1 1 480px" }}>
        <div style={{ display: "flex", gap: 6, marginBottom: 12 }}>
          <button onClick={() => setStatType("kills")} style={pill(statType === "kills")}>Most eliminations</button>
          <button onClick={() => setStatType("damage")} style={pill(statType === "damage")}>Most damage</button>
        </div>
        <div style={{ width: "100%", maxWidth: 560, aspectRatio: "16/9" }}>
          <TopPlayersRenderer theme={theme} canvasBg={canvasBg} labelBg={labelBg} cardBg={cardBg} fields={fields} players={players} />
        </div>
      </div>

      <EditorPanel tabs={[{ id: "canvas", label: "Canvas", icon: Palette }, { id: "label", label: "Rank label", icon: Type }, { id: "fields", label: "Fields", icon: Type }]} activeTab={tab} setActiveTab={setTab}>
        {tab === "canvas" && (<div><BgEditor bg={canvasBg} setBg={setCanvasBgOverride} defaultGradient={{ type: "gradient", angle: 135, stops: [{ pos: 0, color: "#7B2FF7" }, { pos: 100, color: "#F76B1C" }] }} />{canvasBgOverride && <ResetToThemeButton onClick={() => setCanvasBgOverride(null)} />}</div>)}
        {tab === "label" && (<div><BgEditor bg={labelBg} setBg={setLabelBgOverride} maxStops={3} />{labelBgOverride && <ResetToThemeButton onClick={() => setLabelBgOverride(null)} />}</div>)}
        {tab === "fields" && (<div><ColumnStyleEditor label="Player name" col={fields.playerName} setCol={setField("playerName")} /><ColumnStyleEditor label="Stat value" col={fields.statValue} setCol={setField("statValue")} /></div>)}
      </EditorPanel>
    </PageShell>
  );
}
