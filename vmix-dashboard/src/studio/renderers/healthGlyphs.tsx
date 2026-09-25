import { useId } from "react";
import { HealthStop, interpolateHealthColor } from "../theme";

// Shared per-player health glyphs for the live graphics (Standings bars, Top 4 helmets), so both
// read a player's state the same way:
//
//   alive    - filled to the player's health, from the bottom up
//   knocked  - filled RED to the bleed-out health pcob reports while downed (liveState 4 health
//              runs 0-100 as the knock bar drains - confirmed in the 23 Sep real-match recording),
//              pulsing so it reads as urgent
//   dead     - solid grey, no fill level
//
// The fill colour is either one flat colour (the PMGO look) or the continuous health gradient
// from the Standings "Health" tab - operator's choice, stored as `health.style`.

export interface PlayerHealth { health: number; liveState: number }

export interface HealthStyle {
  /** "solid" = one alive colour at every health level (PMGO). "gradient" = colour follows the
   *  health gradient stops (green -> amber -> red as health drops). */
  fill: "solid" | "gradient";
  alive: string;
  knocked: string;
  dead: string;
  /** The empty part of a bar / helmet. */
  track: string;
}

export const DEFAULT_HEALTH_STYLE: HealthStyle = {
  fill: "solid",
  alive: "#3DDC5C",
  knocked: "#E8323C",
  dead: "#6E6E78",
  track: "rgba(255,255,255,0.16)",
};

export function resolveHealthStyle(partial: Partial<HealthStyle> | null | undefined): HealthStyle {
  return { ...DEFAULT_HEALTH_STYLE, ...(partial ?? {}) };
}

export interface PlayerGlyphState {
  /** 0-100, how much of the glyph is filled. */
  pct: number;
  color: string;
  dead: boolean;
  knocked: boolean;
}

const clampPct = (n: number) => (Number.isFinite(n) ? Math.max(0, Math.min(100, n)) : 0);

export function playerGlyphState(p: PlayerHealth, style: HealthStyle, stops: HealthStop[]): PlayerGlyphState {
  // 5 = dead, 6 = disconnected: neither can fight, both read as out of the game.
  if (p.liveState === 5 || p.liveState === 6) return { pct: 100, color: style.dead, dead: true, knocked: false };
  if (p.liveState === 4) return { pct: clampPct(p.health), color: style.knocked, dead: false, knocked: true };
  // On the plane / parachuting health can read 0 before landing; show them as full.
  const health = p.liveState === 1 || p.liveState === 2 ? 100 : clampPct(p.health);
  const color = style.fill === "gradient" && stops.length > 0 ? interpolateHealthColor(stops, health) : style.alive;
  return { pct: health, color, dead: false, knocked: false };
}

const PULSE_CSS = `@keyframes hg-pulse { 0%,100% { opacity: 1 } 50% { opacity: .45 } }`;

/** Vertical bar, filled from the bottom. The live Standings "ALIVE" column. */
export function HealthBar({ player, style, stops, width = 7, height = 20, radius = 2 }: {
  player: PlayerHealth; style: HealthStyle; stops: HealthStop[]; width?: number; height?: number; radius?: number;
}) {
  const s = playerGlyphState(player, style, stops);
  return (
    <div style={{ position: "relative", width, height, borderRadius: radius, background: s.dead ? s.color : style.track, overflow: "hidden", flexShrink: 0 }}>
      {!s.dead && (
        <div style={{
          position: "absolute", left: 0, right: 0, bottom: 0, height: `${s.pct}%`, background: s.color,
          transition: "height .45s ease, background-color .3s ease",
          animation: s.knocked ? "hg-pulse 1s ease-in-out infinite" : "none",
        }} />
      )}
      <style>{PULSE_CSS}</style>
    </div>
  );
}

// A combat helmet in side profile, 36x28. Kept as one closed path so it can be used as a clip for
// the fill level.
const HELMET_PATH = "M2 21 C2 10 9 3 18.5 3 C27.5 3 34 9.5 34 18 L34 21.5 C34 22.3 33.3 23 32.5 23 L25 23 L23.2 26 L5 26 C3.3 26 2 24.7 2 23 Z";

/** Helmet icon filled to the player's health - the Last 4 cards (PMGO style). */
export function HelmetIcon({ player, style, stops, size = 24 }: {
  player: PlayerHealth; style: HealthStyle; stops: HealthStop[]; size?: number;
}) {
  const s = playerGlyphState(player, style, stops);
  const clipId = `helm-${useId().replace(/:/g, "")}`;
  const fillTop = 28 - (28 * s.pct) / 100;
  return (
    <svg width={size} height={(size * 28) / 36} viewBox="0 0 36 28" style={{ flexShrink: 0, display: "block" }}>
      <defs><clipPath id={clipId}><path d={HELMET_PATH} /></clipPath></defs>
      <path d={HELMET_PATH} fill={s.dead ? s.color : style.track} />
      {!s.dead && (
        <rect x={0} y={fillTop} width={36} height={28 - fillTop} fill={s.color} clipPath={`url(#${clipId})`}
          style={{ transition: "y .45s ease, height .45s ease", animation: s.knocked ? "hg-pulse 1s ease-in-out infinite" : "none" } as any} />
      )}
      {/* visor line + rim, drawn over the fill so the silhouette stays readable at any level */}
      <path d="M22 13.5 L33.5 13.5" stroke="rgba(0,0,0,0.35)" strokeWidth={2} strokeLinecap="round" />
      <path d={HELMET_PATH} fill="none" stroke="rgba(0,0,0,0.35)" strokeWidth={1} />
      <style>{PULSE_CSS}</style>
    </svg>
  );
}

// ---------------------------------------------------------------------------------------------
// Throwables (Last 4 cards)
// ---------------------------------------------------------------------------------------------

export type ThrowableKind = "frag" | "smoke" | "molotov" | "stun";
export const THROWABLE_ORDER: ThrowableKind[] = ["frag", "smoke", "molotov", "stun"];
export const THROWABLE_LABELS: Record<ThrowableKind, string> = { frag: "Frag", smoke: "Smoke", molotov: "Molotov", stun: "Stun" };

export interface Throwables { frag: number; smoke: number; molotov: number; stun: number }

/** Small monochrome glyph per throwable, drawn in `color`. 20x20 viewBox. */
export function ThrowableIcon({ kind, size = 16, color = "currentColor" }: { kind: ThrowableKind; size?: number; color?: string }) {
  const common = { width: size, height: size, viewBox: "0 0 20 20", style: { flexShrink: 0, display: "block" } as const };
  switch (kind) {
    case "frag": // pineapple grenade: body, spoon, pin ring
      return (
        <svg {...common}>
          <ellipse cx="9.5" cy="12.5" rx="5.5" ry="6" fill={color} />
          <path d="M6.5 10 H12.5 M6.5 13 H12.5 M6.5 16 H12.5" stroke="rgba(0,0,0,0.35)" strokeWidth="1" />
          <rect x="7.5" y="4" width="4" height="3" rx="0.8" fill={color} />
          <path d="M11.5 5 L16 9.5" stroke={color} strokeWidth="1.6" strokeLinecap="round" />
          <circle cx="14.5" cy="3.5" r="2" fill="none" stroke={color} strokeWidth="1.3" />
        </svg>
      );
    case "smoke": // canister with a puff
      return (
        <svg {...common}>
          <rect x="4" y="7" width="8" height="12" rx="1.5" fill={color} />
          <rect x="5" y="5" width="6" height="2.5" rx="0.8" fill={color} />
          <circle cx="14.5" cy="5" r="2.4" fill={color} opacity="0.75" />
          <circle cx="17" cy="8" r="1.8" fill={color} opacity="0.55" />
          <circle cx="12.5" cy="2.5" r="1.6" fill={color} opacity="0.55" />
        </svg>
      );
    case "molotov": // bottle with a flame
      return (
        <svg {...common}>
          <path d="M8 7 V5 H11 V7 L13 10 V18.5 C13 19 12.6 19.3 12.2 19.3 H6.8 C6.4 19.3 6 19 6 18.5 V10 Z" fill={color} />
          <path d="M9.5 4.5 C8 3 9 1.5 9.8 0.6 C10 2 12 2.4 11.2 4 C10.9 4.6 10.2 4.9 9.5 4.5 Z" fill={color} opacity="0.85" />
        </svg>
      );
    case "stun": // flashbang cylinder with a bolt
      return (
        <svg {...common}>
          <rect x="3.5" y="6" width="8" height="13" rx="1.5" fill={color} />
          <rect x="4.5" y="4" width="6" height="2.5" rx="0.8" fill={color} />
          <path d="M16 3 L13.2 9 H15.6 L13.8 15 L18.4 7.8 H15.9 L17.6 3 Z" fill={color} />
        </svg>
      );
  }
}
