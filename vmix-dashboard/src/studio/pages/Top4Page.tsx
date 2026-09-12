import { useState } from "react";
import { Palette, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg, DEAD_COLOR, KNOCKED_COLOR, hexToRgb, rgbToHex } from "../theme";
import { bgCss } from "../theme";
import { BgEditor, ColumnStyle, ColumnStyleEditor, EditorPanel, PageShell, ResetToThemeButton } from "../StudioControls";
import { SAMPLE_TOP4 } from "../sampleData";

const T4_HEALTH_STOPS = [{ pos: 100, color: "#2ECC71" }, { pos: 60, color: "#8BC34A" }, { pos: 30, color: "#F1C40F" }, { pos: 1, color: "#E74C3C" }];

function t4TickColor(p: { health: number; liveState: number }) {
  if (p.liveState === 5) return { color: DEAD_COLOR, pulse: false };
  if (p.liveState === 4) return { color: KNOCKED_COLOR, pulse: true };
  const sorted = [...T4_HEALTH_STOPS].sort((a, b) => b.pos - a.pos);
  const h = p.health;
  if (h >= sorted[0].pos) return { color: sorted[0].color, pulse: false };
  for (let i = 0; i < sorted.length - 1; i++) {
    const hi = sorted[i], lo = sorted[i + 1];
    if (h <= hi.pos && h >= lo.pos) {
      const t = (h - lo.pos) / (hi.pos - lo.pos || 1);
      const [r1, g1, b1] = hexToRgb(lo.color), [r2, g2, b2] = hexToRgb(hi.color);
      return { color: rgbToHex(r1 + (r2 - r1) * t, g1 + (g2 - g1) * t, b1 + (b2 - b1) * t), pulse: false };
    }
  }
  return { color: sorted[sorted.length - 1].color, pulse: false };
}

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
  const rankStyle = fields.overallRank?.mode === "custom" ? fields.overallRank.custom : ({} as any);
  const tagStyle = fields.tag?.mode === "custom" ? fields.tag.custom : ({} as any);
  const wwcdTextStyle = fields.wwcdText?.mode === "custom" ? fields.wwcdText.custom : ({} as any);

  return (
    <PageShell title="Top 4 / WWCD Chance" subtitle="Final-four panel with per-player health ticks and win-probability bar">
      <div style={{ flex: "1 1 560px" }}>
        <div style={{ width: "100%", maxWidth: 680, borderRadius: theme.radius, overflow: "hidden", background: theme.chromaKey, padding: 14 }}>
          <div style={{ display: "flex", gap: 6 }}>
            {SAMPLE_TOP4.map((t) => (
              <div key={t.overallRank} style={{ flex: 1, borderRadius: 8, overflow: "hidden", background: bgCss(cardBg), backdropFilter: `blur(${theme.panelBlur})`, WebkitBackdropFilter: `blur(${theme.panelBlur})`, border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow, display: "flex", flexDirection: "column" } as any}>
                <div style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 8px", background: "rgba(255,255,255,0.06)" }}>
                  <div style={{ fontWeight: 800, minWidth: 18, fontFamily: rankStyle.fontFamily || theme.fontDisplay, fontSize: rankStyle.fontSize ? `${rankStyle.fontSize}px` : "16px", color: rankStyle.color || theme.textPrimary }}>{t.overallRank}</div>
                  <div style={{ width: 20, height: 20, borderRadius: 5, background: theme.accentGradient, flexShrink: 0 }} />
                  <div style={{ flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", fontWeight: 700, fontFamily: tagStyle.fontFamily || theme.fontBody, fontSize: tagStyle.fontSize ? `${tagStyle.fontSize}px` : "13px", color: tagStyle.color || theme.textPrimary }}>{t.tag}</div>
                  <div style={{ display: "flex", gap: 2 }}>
                    {t.players.map((p, i) => { const { color, pulse } = t4TickColor(p); return <div key={i} style={{ width: 5, height: 14, borderRadius: 1, background: color, animation: pulse ? "t4p-pulse 1s ease-in-out infinite" : "none", opacity: p.liveState === 5 ? 0.5 : 1 }} />; })}
                  </div>
                </div>
                <div style={{ background: bgCss(wwcdBar), textAlign: "center", padding: "5px 0", fontWeight: 800, letterSpacing: 0.3, fontFamily: wwcdTextStyle.fontFamily || theme.fontBody, fontSize: wwcdTextStyle.fontSize ? `${wwcdTextStyle.fontSize}px` : "11px", color: wwcdTextStyle.color || "#1a0a05" }}>
                  WWCD CHANCE: {t.wwcd}%
                </div>
              </div>
            ))}
          </div>
          <style>{`@keyframes t4p-pulse { 0%,100%{opacity:1} 50%{opacity:.35} }`}</style>
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
