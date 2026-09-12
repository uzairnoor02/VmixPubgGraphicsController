import { createContext, useContext, type ReactNode } from "react";
import { DEFAULT_THEME_ID, THEMES, Theme } from "./theme";
import { useStudioConfig } from "./StudioConfigContext";

// Org-wide active theme, shared by every Studio page and the live overlay - picking a theme on
// any one page applies everywhere, matching how a real "active theme" setting should behave.

interface ThemeContextValue { theme: Theme; activeThemeId: string; setActiveThemeId: (id: string) => void }
const ThemeContext = createContext<ThemeContextValue | null>(null);

export function useTheme(): ThemeContextValue {
  const ctx = useContext(ThemeContext);
  if (!ctx) throw new Error("useTheme must be used inside <ThemeProvider>");
  return ctx;
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const { activeThemeId, setActiveThemeId } = useStudioConfig();
  const theme = THEMES[activeThemeId] ?? THEMES[DEFAULT_THEME_ID];
  return <ThemeContext.Provider value={{ theme, activeThemeId, setActiveThemeId }}>{children}</ThemeContext.Provider>;
}
