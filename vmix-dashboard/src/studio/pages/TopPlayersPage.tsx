import { useState } from "react";
import { Palette, Type, User } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg, bgCss } from "../theme";
import { BgEditor, ColumnStyle, ColumnStyleEditor, EditorPanel, PageShell, ResetToThemeButton, pill } from "../StudioControls";
import { SAMPLE_TOP5_DAMAGE, SAMPLE_TOP5_KILLS } from "../sampleData";

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
  const nameStyle = fields.playerName?.mode === "custom" ? fields.playerName.custom : ({} as any);
  const statStyle = fields.statValue?.mode === "custom" ? fields.statValue.custom : ({} as any);

  return (
    <PageShell title="Top Players" subtitle="Podium-style leaderboard for kills or damage">
      <div style={{ flex: "1 1 480px" }}>
        <div style={{ display: "flex", gap: 6, marginBottom: 12 }}>
          <button onClick={() => setStatType("kills")} style={pill(statType === "kills")}>Most eliminations</button>
          <button onClick={() => setStatType("damage")} style={pill(statType === "damage")}>Most damage</button>
        </div>
        <div style={{ width: "100%", maxWidth: 560, aspectRatio: "16/9", borderRadius: theme.radius, overflow: "hidden", background: bgCss(canvasBg), fontFamily: theme.fontDisplay, display: "flex", flexDirection: "column", padding: 22, border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow } as any}>
          <div style={{ fontSize: 30, fontWeight: 800, color: theme.textPrimary }}>TOP PLAYERS</div>
          <div style={{ display: "flex", gap: 12, marginTop: 16, flex: 1 }}>
            {players.map((p) => (
              <div key={p.rank} style={{ flex: 1, display: "flex", flexDirection: "column" }}>
                <div style={{ flex: 1, background: theme.panelBg, backdropFilter: `blur(${theme.panelBlur})`, WebkitBackdropFilter: `blur(${theme.panelBlur})`, border: "1px solid rgba(255,255,255,0.15)", borderRadius: 6, display: "flex", alignItems: "center", justifyContent: "center" } as any}><User size={40} color="rgba(255,255,255,0.5)" /></div>
                <div style={{ background: bgCss(labelBg), color: theme.headerTextColor, fontWeight: 800, fontSize: 13, padding: "3px 8px" }}>#{p.rank}</div>
                <div style={{ background: bgCss(cardBg), color: nameStyle.color || theme.textPrimary, fontFamily: nameStyle.fontFamily || theme.fontBody, fontSize: nameStyle.fontSize ? `${nameStyle.fontSize}px` : "14px", fontWeight: 700, padding: "6px 8px", textAlign: "center", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{p.playerName}</div>
                <div style={{ background: "rgba(0,0,0,0.5)", color: statStyle.color || theme.textPrimary, fontFamily: statStyle.fontFamily || theme.fontBody, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "22px", fontWeight: 800, padding: "4px 8px", textAlign: "center" }}>{p.value.toLocaleString()}</div>
              </div>
            ))}
          </div>
          <div style={{ marginTop: 12, display: "flex", justifyContent: "center" }}><div style={{ background: bgCss(labelBg), color: theme.headerTextColor, fontSize: 12, fontWeight: 700, padding: "4px 16px", letterSpacing: 1 }}>{players[0].statLabel}</div></div>
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
