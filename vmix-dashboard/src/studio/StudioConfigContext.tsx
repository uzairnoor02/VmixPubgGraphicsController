import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { api, type OverlayConfig } from "../lib/api";
import { DEFAULT_THEME_ID, THEMES } from "./theme";

// Persistence backbone for the whole Graphics Studio. Every page's style config (header
// background, column styles, row-color rules, health-gradient stops, ...) lives under one key in
// OverlayConfig.elementSettings - the backend already has this as a free-form
// Dictionary<string, JsonElement> (see VmixGraphicsBusiness/Utils/OverlayConfigStore.cs), so no
// backend schema change was needed to add persistence for six new editor pages. Saving here also
// pushes the change to the real /overlay route over the SignalR OverlayConfigChanged event that
// already exists - so a Studio edit reaches the live broadcast output the instant it's made,
// with no separate "publish" step.
//
// The active theme (which of THEMES every page's "Default" fields resolve against) is stored the
// same way, under the reserved key "studio.themeId".

const THEME_KEY = "studio.themeId";
const SAVE_DEBOUNCE_MS = 400;

interface StudioConfigContextValue {
  loading: boolean;
  /** The org-wide chroma key the live overlay actually paints. Studio previews must use this,
   *  not the per-theme placeholder, or the operator keys against a green that isn't on air. */
  chromaKeyColor: string;
  setChromaKeyColor: (hex: string) => void;
  activeThemeId: string;
  setActiveThemeId: (id: string) => void;
  getElement: <T,>(key: string, defaultValue: T) => T;
  setElement: <T,>(key: string, value: T) => void;
}

const StudioConfigContext = createContext<StudioConfigContextValue | null>(null);

export function useStudioConfig(): StudioConfigContextValue {
  const ctx = useContext(StudioConfigContext);
  if (!ctx) throw new Error("useStudioConfig must be used inside <StudioConfigProvider>");
  return ctx;
}

/** Convenience hook for one page's config slice: behaves like useState, but reads its initial
 *  value from the shared OverlayConfig and persists every change through it. */
/** The live chroma key, for any Studio preview that shows a keyed background. */
export function useChromaKey(): [string, (hex: string) => void] {
  const { chromaKeyColor, setChromaKeyColor } = useStudioConfig();
  return [chromaKeyColor, setChromaKeyColor];
}

export function useStudioElement<T>(key: string, defaultValue: T): [T, (value: T | ((prev: T) => T)) => void] {
  const { getElement, setElement } = useStudioConfig();
  const value = getElement(key, defaultValue);
  const setValue = useCallback((next: T | ((prev: T) => T)) => {
    const resolved = typeof next === "function" ? (next as (prev: T) => T)(getElement(key, defaultValue)) : next;
    setElement(key, resolved);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key]);
  return [value, setValue];
}

export function StudioConfigProvider({ children }: { children: ReactNode }) {
  const [config, setConfig] = useState<OverlayConfig | null>(null);
  const [loading, setLoading] = useState(true);
  const saveTimer = useRef<number | null>(null);
  const latestConfig = useRef<OverlayConfig | null>(null);

  useEffect(() => {
    api.getOverlayConfig()
      .then((c) => { setConfig(c); latestConfig.current = c; })
      .catch(() => { const fallback = { chromaKeyColor: "#00FF00", elementVisibility: {}, elementSettings: {} }; setConfig(fallback); latestConfig.current = fallback; })
      .finally(() => setLoading(false));
  }, []);

  const scheduleSave = useCallback((next: OverlayConfig) => {
    latestConfig.current = next;
    if (saveTimer.current) window.clearTimeout(saveTimer.current);
    saveTimer.current = window.setTimeout(() => {
      api.saveOverlayConfig(latestConfig.current!).catch((err) => console.error("Failed to save Studio config:", err));
    }, SAVE_DEBOUNCE_MS);
  }, []);

  const getElement = useCallback(<T,>(key: string, defaultValue: T): T => {
    const raw = config?.elementSettings?.[key];
    return raw === undefined ? defaultValue : (raw as T);
  }, [config]);

  const setElement = useCallback(<T,>(key: string, value: T) => {
    setConfig((prev) => {
      const base: OverlayConfig = prev ?? { chromaKeyColor: "#00FF00", elementVisibility: {}, elementSettings: {} };
      const next: OverlayConfig = { ...base, elementSettings: { ...base.elementSettings, [key]: value } };
      scheduleSave(next);
      return next;
    });
  }, [scheduleSave]);

  const chromaKeyColor = config?.chromaKeyColor || "#00FF00";
  const setChromaKeyColor = useCallback((hex: string) => {
    setConfig((prev) => {
      const base: OverlayConfig = prev ?? { chromaKeyColor: "#00FF00", elementVisibility: {}, elementSettings: {} };
      const next: OverlayConfig = { ...base, chromaKeyColor: hex };
      scheduleSave(next);
      return next;
    });
  }, [scheduleSave]);

  const activeThemeId = getElement(THEME_KEY, DEFAULT_THEME_ID);
  const setActiveThemeId = useCallback((id: string) => {
    if (THEMES[id]) setElement(THEME_KEY, id);
  }, [setElement]);

  const value = useMemo<StudioConfigContextValue>(() => ({
    loading, chromaKeyColor, setChromaKeyColor, activeThemeId, setActiveThemeId, getElement, setElement,
  }), [loading, chromaKeyColor, setChromaKeyColor, activeThemeId, setActiveThemeId, getElement, setElement]);

  return <StudioConfigContext.Provider value={value}>{children}</StudioConfigContext.Provider>;
}
