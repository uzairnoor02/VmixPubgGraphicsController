import { useEffect, useRef, useState } from "react";
import type { CSSProperties, ReactNode } from "react";
import type { OverlayConfig } from "./api";

// Where each graphic sits on the 1920x1080 broadcast canvas.
//
// Defaults follow the PMGO broadcast layout (live rankings under the in-game minimap on the
// right, Last 4 cards across the top, achievement banner on the left below the in-game team
// panel, ELIMINATED card in the upper centre). Any graphic can be moved from Overlay Settings,
// which stores an override as elementSettings["layout.<id>"] - so a different game HUD or a
// sponsor bug is handled by moving a graphic, not by a code change.

export interface GraphicLayout {
  /** Left edge in canvas px, or "center" to centre horizontally. */
  x: number | "center";
  y: number;
  /** Width in canvas px. */
  w: number;
}

export const CANVAS_W = 1920;
export const CANVAS_H = 1080;

export const DEFAULT_LAYOUT: Record<string, GraphicLayout> = {
  // live
  leaderboard: { x: 1604, y: 250, w: 308 },
  top4: { x: "center", y: 80, w: 1220 },
  circle: { x: "center", y: 16, w: 520 },
  sidebar: { x: 48, y: 48, w: 260 },
  eliminationFeed: { x: 38, y: 568, w: 320 },
  teamEliminatedBanner: { x: "center", y: 232, w: 360 },
  achievement: { x: 0, y: 400, w: 410 },
  spectatorMap: { x: 1552, y: 700, w: 320 },
  // cut-ins / post-match (centre stage)
  teamIntro: { x: 48, y: 690, w: 560 },
  playerHighlight: { x: 1312, y: 690, w: 560 },
  topPlayers: { x: "center", y: 690, w: 560 },
  mvpRankings: { x: "center", y: 130, w: 620 },
  teamsToWatch: { x: "center", y: 130, w: 600 },
  mapPerformers: { x: "center", y: 130, w: 600 },
  champions: { x: "center", y: 108, w: 680 },
  headToHead: { x: "center", y: 130, w: 600 },
  matchRankings: { x: "center", y: 86, w: 900 },
  overallRankings: { x: "center", y: 86, w: 900 },
};

/** The operator's override for a graphic, falling back to the default above. Achievement
 *  types ("achievement.firstKill" ...) all share the "achievement" slot. */
export function layoutFor(config: OverlayConfig | undefined, id: string): GraphicLayout {
  const key = id.startsWith("achievement.") ? "achievement" : id;
  const base = DEFAULT_LAYOUT[key] ?? { x: "center", y: 120, w: 600 };
  const raw = config?.elementSettings?.[`layout.${key}`] as Partial<GraphicLayout> | undefined;
  if (!raw || typeof raw !== "object") return base;
  const num = (v: unknown, fallback: number) => (typeof v === "number" && Number.isFinite(v) ? v : fallback);
  return {
    x: raw.x === "center" ? "center" : num(raw.x, base.x === "center" ? 0 : base.x),
    y: num(raw.y, base.y),
    w: Math.max(80, num(raw.w, base.w)),
  };
}

export function layoutStyle(l: GraphicLayout, extra?: CSSProperties): CSSProperties {
  return {
    position: "absolute",
    top: l.y,
    width: l.w,
    left: l.x === "center" ? "50%" : l.x,
    transform: l.x === "center" ? "translateX(-50%)" : undefined,
    ...extra,
  };
}

/**
 * A fixed 1920x1080 stage scaled to fit its container. vMix renders the Browser Source at exactly
 * 1920x1080, so on air the scale is 1 and nothing changes; in a smaller browser window (the
 * operator checking /overlay, the /demo page) every graphic keeps its real position and size
 * relative to the frame instead of drifting because the window is narrower.
 */
export function OverlayStage({ children }: { children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  const [fit, setFit] = useState({ scale: 1, left: 0, top: 0 });

  useEffect(() => {
    const host = ref.current?.parentElement;
    if (!host) return;
    const measure = () => {
      const w = host.clientWidth, h = host.clientHeight;
      if (!w || !h) return;
      const scale = Math.min(w / CANVAS_W, h / CANVAS_H);
      setFit({ scale, left: (w - CANVAS_W * scale) / 2, top: (h - CANVAS_H * scale) / 2 });
    };
    measure();
    const ro = new ResizeObserver(measure);
    ro.observe(host);
    return () => ro.disconnect();
  }, []);

  return (
    <div ref={ref} style={{
      position: "absolute", left: fit.left, top: fit.top, width: CANVAS_W, height: CANVAS_H,
      transform: `scale(${fit.scale})`, transformOrigin: "top left", overflow: "hidden",
    }}>
      {children}
    </div>
  );
}
