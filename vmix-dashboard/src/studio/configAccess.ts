import type { OverlayConfig } from "../lib/api";
import { DEFAULT_THEME_ID, THEMES, Theme } from "./theme";

// Pure (non-hook) helpers for reading Studio config out of a plain OverlayConfig object - used by
// the live /overlay route, which maintains its own OverlayConfig state (kept fresh by its existing
// OverlayConfigChanged SignalR subscription) rather than going through StudioConfigProvider's
// React context, so both places read the exact same elementSettings shape without double-fetching
// or double-subscribing to the same config.

export function getConfigElement<T>(config: OverlayConfig | null | undefined, key: string, defaultValue: T): T {
  const raw = config?.elementSettings?.[key];
  return raw === undefined ? defaultValue : (raw as T);
}

export function getStudioTheme(config: OverlayConfig | null | undefined): Theme {
  const themeId = getConfigElement(config, "studio.themeId", DEFAULT_THEME_ID);
  return THEMES[themeId] ?? THEMES[DEFAULT_THEME_ID];
}
