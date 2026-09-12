// Shared theme system + small color/style utilities used by every Graphics Studio page and by
// the live /overlay renderer. Ported from graphics-studio-app.jsx (Downloads/files) - kept as one
// module instead of copy-pasted per page (the original had this duplicated across 6 standalone
// artifacts) so a theme tweak or a bug fix in interpolateHealthColor only has to happen once, and
// so the live overlay can import the exact same render math the Studio preview uses - what you
// design is what's actually on air, not a close approximation of it.

export type Gradient = { type: "gradient"; angle: number; stops: { pos: number; color: string }[] };
export type Solid = { type: "solid"; color: string };
export type Bg = Gradient | Solid;

export interface Theme {
  id: string;
  name: string;
  description: string;
  chromaKey: string;
  panelBg: string;
  panelBlur: string;
  panelBorder: string;
  accentGradient: string;
  headerBg: Bg;
  headerTextColor: string;
  rowBgEven: string;
  rowBgOdd: string;
  textPrimary: string;
  fontDisplay: string;
  fontBody: string;
  glow: string;
  radius: number;
}

export const THEMES: Record<string, Theme> = {
  glass_neon: {
    id: "glass_neon", name: "Glass Neon", description: "Cool, luminous, glassmorphic — the aquatic direction",
    chromaKey: "#00FF00", panelBg: "rgba(20, 24, 42, 0.6)", panelBlur: "18px", panelBorder: "rgba(120, 200, 255, 0.25)",
    accentGradient: "linear-gradient(120deg, #00E5FF 0%, #7B2FF7 100%)",
    headerBg: { type: "gradient", angle: 120, stops: [{ pos: 0, color: "#00E5FF" }, { pos: 100, color: "#7B2FF7" }] },
    headerTextColor: "#0A0E1A", rowBgEven: "rgba(255,255,255,0.05)", rowBgOdd: "rgba(255,255,255,0.01)",
    textPrimary: "#F2F6FF", fontDisplay: "'Barlow Condensed', sans-serif", fontBody: "'Inter', sans-serif",
    glow: "0 0 24px rgba(0,229,255,0.25)", radius: 14,
  },
  sunset_arena: {
    id: "sunset_arena", name: "Sunset Arena", description: "Warm, high-energy broadcast gradient — matches real PMGC key art",
    chromaKey: "#00FF00", panelBg: "rgba(30, 10, 20, 0.55)", panelBlur: "14px", panelBorder: "rgba(255, 180, 120, 0.3)",
    accentGradient: "linear-gradient(120deg, #FF6B35 0%, #F72585 55%, #7209B7 100%)",
    headerBg: { type: "gradient", angle: 120, stops: [{ pos: 0, color: "#FF6B35" }, { pos: 100, color: "#F72585" }] },
    headerTextColor: "#FFF7F0", rowBgEven: "rgba(255,255,255,0.06)", rowBgOdd: "rgba(0,0,0,0.15)",
    textPrimary: "#FFF7F0", fontDisplay: "'Barlow Condensed', sans-serif", fontBody: "'Inter', sans-serif",
    glow: "0 0 24px rgba(255,107,53,0.3)", radius: 10,
  },
  carbon_gold: {
    id: "carbon_gold", name: "Carbon Gold", description: "Restrained near-black with a single gold accent",
    chromaKey: "#00FF00", panelBg: "rgba(15, 15, 20, 0.75)", panelBlur: "10px", panelBorder: "rgba(244, 196, 48, 0.2)",
    accentGradient: "linear-gradient(120deg, #F4C430 0%, #B8860B 100%)",
    headerBg: { type: "solid", color: "#0A0A0F" },
    headerTextColor: "#F4C430", rowBgEven: "rgba(255,255,255,0.03)", rowBgOdd: "rgba(0,0,0,0.15)",
    textPrimary: "#F0F0F2", fontDisplay: "'Oswald', sans-serif", fontBody: "'Inter', sans-serif",
    glow: "0 0 16px rgba(244,196,48,0.15)", radius: 8,
  },
};

export const DEFAULT_THEME_ID = "glass_neon";

export function bgCss(bg: Bg | null | undefined): string | undefined {
  if (!bg) return undefined;
  if (bg.type === "solid") return bg.color;
  const stops = [...bg.stops].sort((a, b) => a.pos - b.pos).map((s) => `${s.color} ${s.pos}%`).join(", ");
  return `linear-gradient(${bg.angle}deg, ${stops})`;
}

export interface RowRule { id: string; label: string; from: number; to: number; bg: Bg }

export function resolveRowBg(rules: RowRule[], rank: number): Bg | null {
  let result: Bg | null = null;
  for (const rule of rules) if (rank >= rule.from && rank <= rule.to) result = rule.bg;
  return result;
}

export function hexToRgb(hex: string): [number, number, number] {
  const m = hex.replace("#", "").match(/^([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})$/i);
  return m ? [parseInt(m[1], 16), parseInt(m[2], 16), parseInt(m[3], 16)] : [255, 255, 255];
}

export function rgbToHex(r: number, g: number, b: number): string {
  return "#" + [r, g, b].map((v) => Math.max(0, Math.min(255, Math.round(v))).toString(16).padStart(2, "0")).join("");
}

export interface HealthStop { pos: number; color: string }

export function interpolateHealthColor(stops: HealthStop[], health: number): string {
  const sorted = [...stops].sort((a, b) => b.pos - a.pos);
  if (health >= sorted[0].pos) return sorted[0].color;
  if (health <= sorted[sorted.length - 1].pos) return sorted[sorted.length - 1].color;
  for (let i = 0; i < sorted.length - 1; i++) {
    const hi = sorted[i], lo = sorted[i + 1];
    if (health <= hi.pos && health >= lo.pos) {
      const t = (health - lo.pos) / (hi.pos - lo.pos || 1);
      const [r1, g1, b1] = hexToRgb(lo.color), [r2, g2, b2] = hexToRgb(hi.color);
      return rgbToHex(r1 + (r2 - r1) * t, g1 + (g2 - g1) * t, b1 + (b2 - b1) * t);
    }
  }
  return sorted[sorted.length - 1].color;
}

export const DEFAULT_HEALTH_STOPS: HealthStop[] = [
  { pos: 100, color: "#2ECC71" }, { pos: 80, color: "#8BC34A" }, { pos: 60, color: "#F1C40F" },
  { pos: 20, color: "#E74C3C" }, { pos: 0, color: "#C0392B" },
];
export const DEAD_COLOR = "#4A4A56";
export const KNOCKED_COLOR = "#FF3B5C";

/** liveState per the official PC-OB API: 0 Normal,1 OnPlane,2 OnParachute,3 OnVehicle,4 Knocked,5 Dead,6 Disconnected. */
export function colorForPlayer(stops: HealthStop[], player: { health: number; liveState: number }): { color: string; pulse: boolean } {
  if (player.liveState === 5) return { color: DEAD_COLOR, pulse: false };
  if (player.liveState === 4) return { color: KNOCKED_COLOR, pulse: true };
  return { color: interpolateHealthColor(stops, player.health), pulse: false };
}

export const FONT_OPTIONS = [
  { id: "theme", label: "Theme default" },
  { id: "'Barlow Condensed', sans-serif", label: "Barlow Condensed" },
  { id: "'Oswald', sans-serif", label: "Oswald" },
  { id: "'Inter', sans-serif", label: "Inter" },
  { id: "'Bebas Neue', sans-serif", label: "Bebas Neue" },
  { id: "'JetBrains Mono', monospace", label: "JetBrains Mono" },
];

export interface FieldStyle { mode: "default" | "custom"; custom: { fontFamily?: string; fontSize?: number; color?: string } }

export function resolveFieldStyle(field: FieldStyle | undefined): { fontFamily?: string; fontSize?: string; color?: string } {
  if (!field || field.mode !== "custom") return {};
  const c = field.custom;
  return { fontFamily: c.fontFamily, fontSize: c.fontSize ? `${c.fontSize}px` : undefined, color: c.color };
}
