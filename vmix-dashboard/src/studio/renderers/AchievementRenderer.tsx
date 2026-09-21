import type { ReactNode } from "react";
import { Award, Box, Car, Crosshair, Skull, Trophy, Zap } from "lucide-react";
import { Bg, Theme, bgCss } from "../theme";
import { panelSurface } from "./surface";

// The achievement popup visual - extracted out of AchievementPage.tsx so the Studio preview and
// the real /overlay route render from exactly the same component, same as StandingsRenderer.
//
// The Studio and the overlay historically named achievements differently: the Studio's sample
// catalogue uses first_blood / long_range / grenade_master / vehicle_kill, while the live
// OverlayEvent types are achievement.firstKill / achievement.grenadeElim and so on. Rather than
// renaming either side (the live names come from C# detection code, the Studio names from the
// design catalogue), achievementIcon below accepts both spellings so one component serves both.

const ICONS: Record<string, typeof Crosshair> = {
  // Studio catalogue ids
  first_blood: Crosshair, long_range: Zap, grenade_master: Skull, vehicle_kill: Car,
  // Live OverlayEvent types
  "achievement.firstKill": Crosshair, "achievement.grenadeElim": Skull,
  "achievement.vehicleKill": Car, "achievement.airdropLoot": Box,
  "achievement.knockout": Zap, "achievement.chickenDinner": Trophy,
};

export function achievementIcon(type: string, size = 20, color = "#0a0a0f"): ReactNode {
  const Icon = ICONS[type] ?? Award;
  return <Icon size={size} color={color} />;
}

export interface AchievementRendererProps {
  theme: Theme;
  accentBg: Bg;
  /** Eyebrow line, e.g. "GRENADE ELIM". */
  label: string;
  /** The player the achievement belongs to. */
  primary: string;
  /** Supporting line, e.g. "eliminated R3G-ROSHAAN at 187m". */
  detail?: string;
  icon?: ReactNode;
  photoUrl?: string;
  /** Drives the slide-up entrance. The caller owns timing: a real event on air, a Replay button
   *  in the Studio. */
  visible: boolean;
  /** 0-100, Task 9. Omitted/undefined = 100 = today's appearance, unchanged. */
  panelOpacity?: number;
}

export function AchievementRenderer({ theme, accentBg, label, primary, detail, icon, photoUrl, visible, panelOpacity }: AchievementRendererProps) {
  return (
    <div style={{ width: "100%", transform: visible ? "translateY(0)" : "translateY(30px)", opacity: visible ? 1 : 0, transition: "all 0.35s cubic-bezier(0.34, 1.56, 0.64, 1)", display: "flex", alignItems: "center", gap: 10, ...panelSurface(panelOpacity, "rgba(20,20,26,0.8)", theme.panelBlur), borderRadius: 8, padding: "10px 14px" } as any}>
      {photoUrl ? (
        <img src={photoUrl} alt="" style={{ width: 40, height: 40, borderRadius: 8, objectFit: "cover", flexShrink: 0 }} />
      ) : (
        <div style={{ width: 40, height: 40, borderRadius: 8, background: bgCss(accentBg), display: "flex", alignItems: "center", justifyContent: "center", flexShrink: 0 }}>
          {icon ?? achievementIcon("")}
        </div>
      )}
      <div style={{ minWidth: 0 }}>
        <div style={{ fontSize: 11, fontWeight: 700, color: theme.textPrimary, fontFamily: theme.fontDisplay, letterSpacing: 0.5 }}>{label}</div>
        <div style={{ fontSize: 15, fontWeight: 700, color: theme.textPrimary, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{primary}</div>
        {detail && <div style={{ fontSize: 11, color: "rgba(255,255,255,0.6)" }}>{detail}</div>}
      </div>
    </div>
  );
}
