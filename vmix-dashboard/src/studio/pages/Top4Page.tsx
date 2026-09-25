import { useState } from "react";
import { Palette, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useChromaKey, useStudioElement } from "../StudioConfigContext";
import { Bg } from "../theme";
import { BgEditor, ColumnStyle, ColumnStyleEditor, EditorPanel, PageShell, ResetToThemeButton } from "../StudioControls";
import { SAMPLE_TOP4 } from "../sampleData";
import { DEFAULT_HEALTH_STOPS, HealthStop } from "../theme";
import { HealthStyleEditor, useHealthStyle } from "../HealthStyleEditor";
import { Top4Renderer } from "../renderers/Top4Renderer";

const DEFAULT_FIELDS: Record<string, ColumnStyle> = {
  overallRank: { mode: "default", custom: {} }, tag: { mode: "default", custom: {} }, wwcdText: { mode: "default", custom: {} },
};

export default function Top4Page() {
  const { theme } = useTheme();
  const [chromaKey] = useChromaKey();
  const [tab, setTab] = useState("wwcdBar");
  const [wwcdBarOverride, setWwcdBarOverride] = useStudioElement<Bg | null>("top4.wwcdBar", null);
  const [fields, setFields] = useStudioElement("top4.fields", DEFAULT_FIELDS);
  const [cardBg, setCardBg] = useStudioElement<Bg>("top4.cardBg", { type: "solid", color: "rgba(10,10,15,0.75)" });
  const [showWwcd, setShowWwcd] = useStudioElement<boolean>("top4.showWwcd", true);
  const [showThrowables, setShowThrowables] = useStudioElement<boolean>("top4.showThrowables", true);
  const [showRank, setShowRank] = useStudioElement<boolean>("top4.showRank", false);
  const [healthStops] = useStudioElement<HealthStop[]>("standings.healthStops", DEFAULT_HEALTH_STOPS);
  const [healthStyle] = useHealthStyle();

  const wwcdBar: Bg = wwcdBarOverride || { type: "gradient", angle: 90, stops: [{ pos: 0, color: "#F5A623" }, { pos: 100, color: "#F76B1C" }] };
  const setField = (key: string) => (next: ColumnStyle) => setFields((prev) => ({ ...prev, [key]: next }));

  return (
    <PageShell title="Last 4 Teams" subtitle="Final-four cards: helmets filled to each player's health, carried throwables, WWCD chance">
      <div style={{ flex: "1 1 640px" }}>
        <div style={{ width: "100%", maxWidth: 1000, borderRadius: theme.radius, overflow: "hidden", background: chromaKey, padding: 14 }}>
          <Top4Renderer theme={theme} wwcdBar={wwcdBar} cardBg={cardBg} fields={fields} healthStyle={healthStyle} healthStops={healthStops}
            showWwcd={showWwcd} showThrowables={showThrowables} showRank={showRank}
            teams={SAMPLE_TOP4.map((t) => ({ key: t.overallRank, overallRank: t.overallRank, tag: t.tag, wwcd: t.wwcd, players: t.players, throwables: t.throwables ?? null }))} />
        </div>
        <div style={{ fontSize: 11, color: "#5c5c66", marginTop: 12, lineHeight: 1.6, maxWidth: 560 }}>
          Throwables are what the team is carrying (frag, smoke, molotov, stun). pcob only reports
          the inventory of the team the observer is watching, so each card shows its team's numbers
          as last seen, and "-" until the observer has been on them this match.
        </div>
      </div>

      <EditorPanel tabs={[{ id: "wwcdBar", label: "WWCD bar", icon: Palette }, { id: "card", label: "Card", icon: Palette }, { id: "fields", label: "Fields", icon: Type }, { id: "health", label: "Health", icon: Palette }]} activeTab={tab} setActiveTab={setTab}>
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
        {tab === "health" && <HealthStyleEditor stops={healthStops} />}
        {tab === "fields" && (
          <div>
            <div style={{ display: "flex", flexDirection: "column", gap: 6, marginBottom: 12 }}>
              <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12.5, cursor: "pointer" }}><input type="checkbox" checked={showThrowables} onChange={(e) => setShowThrowables(e.target.checked)} />Throwables row</label>
              <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12.5, cursor: "pointer" }}><input type="checkbox" checked={showWwcd} onChange={(e) => setShowWwcd(e.target.checked)} />WWCD chance bar</label>
              <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12.5, cursor: "pointer" }}><input type="checkbox" checked={showRank} onChange={(e) => setShowRank(e.target.checked)} />Overall rank chip</label>
            </div>
            <ColumnStyleEditor label="Throwable counts" col={fields.throwables ?? { mode: "default", custom: {} }} setCol={setField("throwables")} />
            <ColumnStyleEditor label="Overall rank number" col={fields.overallRank} setCol={setField("overallRank")} />
            <ColumnStyleEditor label="Team tag" col={fields.tag} setCol={setField("tag")} />
            <ColumnStyleEditor label="WWCD % text" col={fields.wwcdText} setCol={setField("wwcdText")} />
          </div>
        )}
      </EditorPanel>
    </PageShell>
  );
}
