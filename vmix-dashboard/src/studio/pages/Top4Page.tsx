import { useState } from "react";
import { Palette, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg } from "../theme";
import { BgEditor, ColumnStyle, ColumnStyleEditor, EditorPanel, PageShell, ResetToThemeButton } from "../StudioControls";
import { SAMPLE_TOP4 } from "../sampleData";
import { Top4Renderer } from "../renderers/Top4Renderer";

const DEFAULT_FIELDS: Record<string, ColumnStyle> = {
  overallRank: { mode: "default", custom: {} }, tag: { mode: "default", custom: {} }, wwcdText: { mode: "default", custom: {} },
};

export default function Top4Page() {
  const { theme } = useTheme();
  const [tab, setTab] = useState("wwcdBar");
  const [wwcdBarOverride, setWwcdBarOverride] = useStudioElement<Bg | null>("top4.wwcdBar", null);
  const [fields, setFields] = useStudioElement("top4.fields", DEFAULT_FIELDS);
  const [cardBg, setCardBg] = useStudioElement<Bg>("top4.cardBg", { type: "solid", color: "rgba(10,10,15,0.75)" });

  const wwcdBar: Bg = wwcdBarOverride || { type: "gradient", angle: 90, stops: [{ pos: 0, color: "#F5A623" }, { pos: 100, color: "#F76B1C" }] };
  const setField = (key: string) => (next: ColumnStyle) => setFields((prev) => ({ ...prev, [key]: next }));

  return (
    <PageShell title="Top 4 / WWCD Chance" subtitle="Final-four panel with per-player health ticks and win-probability bar">
      <div style={{ flex: "1 1 560px" }}>
        <div style={{ width: "100%", maxWidth: 680, borderRadius: theme.radius, overflow: "hidden", background: theme.chromaKey, padding: 14 }}>
          <Top4Renderer theme={theme} wwcdBar={wwcdBar} cardBg={cardBg} fields={fields} teams={SAMPLE_TOP4.map((t) => ({ key: t.overallRank, overallRank: t.overallRank, tag: t.tag, wwcd: t.wwcd, players: t.players }))} />
        </div>
        <div style={{ fontSize: 11, color: "#5c5c66", marginTop: 12, lineHeight: 1.6, maxWidth: 500 }}>
          Health ticks reuse the same continuous gradient model as Standings — green alive, fading
          through amber as health drops, red pulsing when knocked, grey and still visible when dead.
          Overall rank is the team's current tournament placement, not their 1–4 position within this panel.
        </div>
      </div>

      <EditorPanel tabs={[{ id: "wwcdBar", label: "WWCD bar", icon: Palette }, { id: "card", label: "Card", icon: Palette }, { id: "fields", label: "Fields", icon: Type }]} activeTab={tab} setActiveTab={setTab}>
        {tab === "wwcdBar" && (
          <div>
            <div style={{ fontSize: 12, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>The win-chance bar background — capped at 3 gradient stops, like every header-style element.</div>
            <BgEditor bg={wwcdBar} setBg={setWwcdBarOverride} maxStops={3} defaultGradient={{ type: "gradient", angle: 90, stops: [{ pos: 0, color: "#F5A623" }, { pos: 100, color: "#F76B1C" }] }} />
            {wwcdBarOverride && <ResetToThemeButton onClick={() => setWwcdBarOverride(null)} />}
          </div>
        )}
        {tab === "card" && (
          <div>
            <div style={{ fontSize: 12, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>Card background behind the rank/logo/tag row.</div>
            <BgEditor bg={cardBg} setBg={setCardBg} />
          </div>
        )}
        {tab === "fields" && (
          <div>
            <ColumnStyleEditor label="Overall rank number" col={fields.overallRank} setCol={setField("overallRank")} />
            <ColumnStyleEditor label="Team tag" col={fields.tag} setCol={setField("tag")} />
            <ColumnStyleEditor label="WWCD % text" col={fields.wwcdText} setCol={setField("wwcdText")} />
          </div>
        )}
      </EditorPanel>
    </PageShell>
  );
}
