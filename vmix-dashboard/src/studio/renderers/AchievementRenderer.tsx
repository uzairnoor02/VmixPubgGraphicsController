import { useState } from "react";
import type { ReactNode } from "react";
import { Award, Box, Car, Crosshair, Skull, Trophy, Zap } from "lucide-react";
import { Bg, Theme, bgCss } from "../theme";
import { panelSurface } from "./surface";
import { TeamLogo, bgPrimaryColor } from "./shared";
export { bgPrimaryColor } from "./shared";

// Achievement banner (First Blood, grenade / vehicle elims, airdrop loot...) in the PMGO layout:
// a wide banner on the left of the screen, below the in-game team panel.
//
//   +---------+---------------------------------+
//   |  player |   FIRST BLOOD                    |   <- big label
//   |  photo  |                                  |
//   +---------+---------------------------------+
//   | [logo] PLAYER  >>  VICTIM                   |   <- accent strip
//   +---------------------------------------------+
//
// Shared by the Studio preview, /demo and /overlay - one render path.
//
// The Studio and the overlay historically named achievements differently (first_blood vs
// achievement.firstKill ...); achievementIcon accepts both spellings.

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
  /** The big label, e.g. "FIRST BLOOD". */
  label: string;
  /** The player the achievement belongs to. */
  primary: string;
  /** Supporting line when there is no victim, e.g. "at 187m". */
  detail?: string;
  /** Who they eliminated, when known - renders "PLAYER >> VICTIM" on the strip. */
  victim?: string;
  teamName?: string;
  teamLogoUrl?: string;
  icon?: ReactNode;
  photoUrl?: string;
  /** Drives the slide-in entrance. The caller owns timing: a real event on air, a Replay button
   *  in the Studio. */
  visible: boolean;
  /** 0-100, Task 9. Omitted/undefined = 100 = today's appearance, unchanged. */
  panelOpacity?: number;
  /** Background behind the big label. Default: near-white, like PMGO. */
  bodyBg?: Bg;
}

export const DEFAULT_ACHIEVEMENT_BODY_BG: Bg = { type: "solid", color: "rgba(245,246,250,0.94)" };

export function AchievementRenderer({
  theme, accentBg, label, primary, detail, victim, teamName, teamLogoUrl, icon, photoUrl, visible, panelOpacity,
  bodyBg = DEFAULT_ACHIEVEMENT_BODY_BG,
}: AchievementRendererProps) {
  const [photoFailed, setPhotoFailed] = useState(false);
  const showPhoto = !!photoUrl && !photoFailed;
  const accent = bgPrimaryColor(accentBg);
  // One line whenever it fits: size the label from its length (condensed display font, ~0.5em per
  // glyph), capped both ways. Very long labels ("WINNER WINNER CHICKEN DINNER") wrap to two lines.
  const labelSize = label.length > 20 ? 28 : Math.max(26, Math.min(50, Math.floor(270 / (label.length * 0.5))));

  return (
    <div style={{
      width: "100%", fontFamily: theme.fontDisplay,
      transform: visible ? "translateX(0)" : "translateX(-60px)", opacity: visible ? 1 : 0,
      transition: "transform .4s cubic-bezier(0.2, 0.9, 0.3, 1.2), opacity .3s ease",
    }}>
      <div style={{ display: "flex", height: 100, ...panelSurface(panelOpacity, bgCss(bodyBg) ?? "#F5F6FA", theme.panelBlur), overflow: "hidden" } as any}>
        <div style={{ width: 112, flexShrink: 0, position: "relative", background: bgCss(accentBg), overflow: "hidden" }}>
          {showPhoto ? (
            <img src={photoUrl} alt="" onError={() => setPhotoFailed(true)}
              style={{ position: "absolute", left: 0, bottom: 0, width: "100%", height: "100%", objectFit: "cover", objectPosition: "top center" }} />
          ) : (
            <div style={{ position: "absolute", inset: 0, display: "flex", alignItems: "center", justifyContent: "center" }}>
              {icon ?? achievementIcon("", 40)}
            </div>
          )}
        </div>
        <div style={{ flex: 1, minWidth: 0, display: "flex", alignItems: "center", padding: "0 16px" }}>
          <div style={{
            fontSize: labelSize, lineHeight: 0.95, fontWeight: 900, fontStyle: "italic", letterSpacing: 0.5,
            whiteSpace: label.length > 20 ? "normal" : "nowrap",
            color: accent, textTransform: "uppercase",
            // Gradient accents render as gradient text; solid ones stay solid.
            ...(accentBg.type === "gradient" ? { background: bgCss(accentBg), WebkitBackgroundClip: "text", backgroundClip: "text", WebkitTextFillColor: "transparent" } : {}),
          } as any}>{label}</div>
        </div>
      </div>
      <div style={{
        display: "flex", alignItems: "center", gap: 8, height: 32, padding: "0 12px",
        background: bgCss(accentBg), color: theme.headerTextColor, fontWeight: 800, fontSize: 19, letterSpacing: 0.4,
        whiteSpace: "nowrap", overflow: "hidden",
      }}>
        {(teamLogoUrl || teamName) && <TeamLogo url={teamLogoUrl} size={20} radius={3} fallback="rgba(0,0,0,0.2)" />}
        <span style={{ overflow: "hidden", textOverflow: "ellipsis" }}>{primary}</span>
        {victim ? (
          <>
            <span style={{ letterSpacing: -3, opacity: 0.9, flexShrink: 0 }}>▶▶</span>
            <span style={{ overflow: "hidden", textOverflow: "ellipsis", opacity: 0.9 }}>{victim}</span>
          </>
        ) : (detail || teamName) ? (
          <span style={{ overflow: "hidden", textOverflow: "ellipsis", opacity: 0.75, fontWeight: 700, fontSize: 16 }}>{detail ?? teamName}</span>
        ) : null}
      </div>
    </div>
  );
}
