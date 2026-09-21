import type { CSSProperties } from "react";

/**
 * Task 9 - the one shared implementation of "how a graphic's panel background renders" so every
 * renderer scales/hides it identically instead of forking the same handful of CSS properties per
 * file. Takes the renderer's already-resolved background CSS (`theme.panelBg` for most panels,
 * `bgCss(someBg)` for the ones whose panel color is itself a Studio-configurable field, e.g.
 * Top4Renderer's card) and its blur amount (always `theme.panelBlur`), rather than assuming every
 * panel is colored straight off the theme - so a custom panel color a director picked in the
 * Studio still gets exactly this opacity behavior instead of being silently replaced.
 *
 * `opacityPct` is 0-100. `undefined`/100 reproduces exactly what every renderer already painted
 * before this existed (the given background + a full blur) - the default-is-current-appearance
 * guarantee Task 9 asks for, so nothing changes on screen until an operator touches the slider.
 *
 * At 0 the panel must paint NOTHING - not `rgba(...,0)`, because vMix still applies
 * `backdrop-filter: blur()` to a fully-transparent background (visibly blurring the gameplay
 * footage behind it) and a lingering `box-shadow` paints semi-transparent black that keys badly.
 * So 0 omits `background`, `backdropFilter`/`WebkitBackdropFilter` and `boxShadow` entirely.
 *
 * 1-99 scales the background's own alpha channel; `backdropFilter` scales down with it and is
 * dropped entirely below 10 (a blur that faint is indistinguishable from none, and dropping it
 * early avoids a hairline of residual blur right as the panel disappears).
 *
 * This only ever touches the PANEL SURFACE - text, logos, health bars, borders and a renderer's
 * own colored outer glow (`theme.glow`) are that renderer's business, not this helper's.
 */
export function panelSurface(opacityPct: number | undefined, backgroundCss: string | undefined, panelBlurPx: string | undefined): CSSProperties {
  const opacity = clamp(opacityPct ?? 100, 0, 100);

  if (opacity <= 0) {
    return { background: "none", backdropFilter: "none", WebkitBackdropFilter: "none", boxShadow: "none" } as CSSProperties;
  }

  const scale = opacity / 100;
  const style: CSSProperties = { background: scaleBgAlpha(backgroundCss, scale) };

  if (opacity >= 10 && panelBlurPx) {
    const blur = `blur(${scaleBlurPx(panelBlurPx, scale)})`;
    style.backdropFilter = blur;
    (style as Record<string, unknown>).WebkitBackdropFilter = blur;
  } else {
    style.backdropFilter = "none";
    (style as Record<string, unknown>).WebkitBackdropFilter = "none";
  }

  return style;
}

function clamp(n: number, lo: number, hi: number): number {
  return Math.max(lo, Math.min(hi, n));
}

/** Scales the alpha channel of an rgba(...)/rgb(...) color. A gradient (`linear-gradient(...)`,
 *  from a Bg of type "gradient") or any other non-rgba value is returned unscaled rather than
 *  thrown on - "can't parse this color" is not a reason to crash a live overlay, it just means
 *  that particular background won't visibly fade until it's expressed as a plain rgba color. */
function scaleBgAlpha(bg: string | undefined, scale: number): string | undefined {
  if (!bg) return bg;
  const m = bg.match(/^rgba?\(\s*([\d.]+)\s*,\s*([\d.]+)\s*,\s*([\d.]+)\s*(?:,\s*([\d.]+)\s*)?\)$/i);
  if (!m) return bg;
  const [, r, g, b, a] = m;
  const baseAlpha = a !== undefined ? parseFloat(a) : 1;
  return `rgba(${r}, ${g}, ${b}, ${(baseAlpha * scale).toFixed(4)})`;
}

/** theme.panelBlur is a px string like "18px". Scaling it down with opacity (rather than leaving
 *  it constant until the 10% cutoff) is what avoids a visible "pop" from full blur to none. */
function scaleBlurPx(panelBlurPx: string, scale: number): string {
  const m = panelBlurPx.match(/^([\d.]+)px$/);
  if (!m) return panelBlurPx;
  const px = parseFloat(m[1]) * scale;
  return `${px.toFixed(2)}px`;
}
