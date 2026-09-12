import { Bg, DEAD_COLOR, KNOCKED_COLOR, Theme, bgCss, hexToRgb, rgbToHex } from "../theme";
import type { ColumnStyle } from "../StudioControls";

// The actual Top 4 / WWCD Chance visual - extracted out of Top4Page.tsx so the Studio preview and
// the real /overlay route render from exactly the same component, same reasoning as
// StandingsRenderer.tsx.

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

export interface Top4Team { key: string | number; overallRank: number; tag: string; wwcd: number; players: { health: number; liveState: number }[] }

export interface Top4RendererProps {
  theme: Theme;
  wwcdBar: Bg;
  cardBg: Bg;
  fields: Record<string, ColumnStyle>;
  teams: Top4Team[];
}

export function Top4Renderer({ theme, wwcdBar, cardBg, fields, teams }: Top4RendererProps) {
  const rankStyle = fields.overallRank?.mode === "custom" ? fields.overallRank.custom : ({} as any);
  const tagStyle = fields.tag?.mode === "custom" ? fields.tag.custom : ({} as any);
  const wwcdTextStyle = fields.wwcdText?.mode === "custom" ? fields.wwcdText.custom : ({} as any);

  return (
    <div style={{ display: "flex", gap: 6 }}>
      {teams.map((t) => (
        <div key={t.key} style={{ flex: 1, borderRadius: 8, overflow: "hidden", background: bgCss(cardBg), backdropFilter: `blur(${theme.panelBlur})`, WebkitBackdropFilter: `blur(${theme.panelBlur})`, border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow, display: "flex", flexDirection: "column" } as any}>
          <div style={{ display: "flex", alignItems: "center", gap: 6, padding: "6px 8px", background: "rgba(255,255,255,0.06)" }}>
            <div style={{ fontWeight: 800, minWidth: 18, fontFamily: rankStyle.fontFamily || theme.fontDisplay, fontSize: rankStyle.fontSize ? `${rankStyle.fontSize}px` : "16px", color: rankStyle.color || theme.textPrimary }}>{t.overallRank}</div>
            <div style={{ width: 20, height: 20, borderRadius: 5, background: theme.accentGradient, flexShrink: 0 }} />
            <div style={{ flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", fontWeight: 700, fontFamily: tagStyle.fontFamily || theme.fontBody, fontSize: tagStyle.fontSize ? `${tagStyle.fontSize}px` : "13px", color: tagStyle.color || theme.textPrimary }}>{t.tag}</div>
            <div style={{ display: "flex", gap: 2 }}>
              {t.players.map((p, i) => { const { color, pulse } = t4TickColor(p); return <div key={i} style={{ width: 5, height: 14, borderRadius: 1, background: color, animation: pulse ? "t4p-pulse 1s ease-in-out infinite" : "none", opacity: p.liveState === 5 ? 0.5 : 1 }} />; })}
            </div>
          </div>
          <div style={{ background: bgCss(wwcdBar), textAlign: "center", padding: "5px 0", fontWeight: 800, letterSpacing: 0.3, fontFamily: wwcdTextStyle.fontFamily || theme.fontBody, fontSize: wwcdTextStyle.fontSize ? `${wwcdTextStyle.fontSize}px` : "11px", color: wwcdTextStyle.color || "#1a0a05" }}>
            WWCD CHANCE: {Number.isFinite(t.wwcd) ? `${t.wwcd}%` : "—"}
          </div>
        </div>
      ))}
      <style>{`@keyframes t4p-pulse { 0%,100%{opacity:1} 50%{opacity:.35} }`}</style>
    </div>
  );
}
