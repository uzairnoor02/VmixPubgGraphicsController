import { useState } from "react";
import { Bg } from "../theme";

// Small pieces several renderers share.

/** A team logo that falls back to a neutral tile when the file is missing (a logo not uploaded
 *  yet, a team id with no PNG) instead of the browser's broken-image icon on air. */
export function TeamLogo({ url, size, radius = 4, fallback = "rgba(255,255,255,0.14)" }: { url?: string; size: number; radius?: number; fallback?: string }) {
  const [failed, setFailed] = useState(false);
  if (!url || failed) return <div style={{ width: size, height: size, borderRadius: radius, background: fallback, flexShrink: 0 }} />;
  return <img src={url} alt="" onError={() => setFailed(true)} style={{ width: size, height: size, borderRadius: radius, objectFit: "contain", flexShrink: 0 }} />;
}

/** First colour of a Bg - for places that need one flat colour (text, chevrons). */
export function bgPrimaryColor(bg: Bg): string {
  if (bg.type === "solid") return bg.color;
  return [...bg.stops].sort((a, b) => a.pos - b.pos)[0]?.color ?? "#FFFFFF";
}
