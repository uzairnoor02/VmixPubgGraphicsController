import { Trophy } from "lucide-react";
import { Bg, Theme, bgCss } from "../theme";
import type { ColumnStyle } from "../StudioControls";

// The Champions graphic - the end-of-event celebration card. Deliberately the loudest thing in
// the catalogue: it goes on air once, holds for a long beat, and is the frame people screenshot.
//
// Same contract as every renderer here: shared by the Studio preview and the live /overlay, and
// sizing belongs to the caller.

export interface ChampionPlayer {
  playerName: string;
  photoUrl?: string;
}

export interface ChampionsRendererProps {
  theme: Theme;
  canvasBg: Bg;
  accentBg: Bg;
  /** Eyebrow above the team name. */
  label?: string;
  teamName: string;
  teamLogoUrl?: string;
  /** Roster shown across the bottom. Four is the normal squad size; more are dropped rather
   *  than squeezed, because a broadcast card must not reflow into unreadable columns. */
  players?: ChampionPlayer[];
  stats?: { label: string; value: string | number }[];
  fields: Record<string, ColumnStyle>;
  /** Drives the entrance; the caller owns timing (a Replay button in the Studio, a real event
   *  on air) exactly as the Eliminated banner does. */
  visible?: boolean;
}

export function ChampionsRenderer({
  theme, canvasBg, accentBg, label = "CHAMPIONS", teamName, teamLogoUrl,
  players = [], stats = [], fields, visible = true,
}: ChampionsRendererProps) {
  const nameStyle = fields.teamName?.mode === "custom" ? fields.teamName.custom : ({} as any);
  const statStyle = fields.statValue?.mode === "custom" ? fields.statValue.custom : ({} as any);
  const roster = players.slice(0, 4);

  return (
    <div style={{
      width: "100%", height: "100%", borderRadius: theme.radius, overflow: "hidden",
      background: bgCss(canvasBg), fontFamily: theme.fontDisplay, display: "flex",
      flexDirection: "column", alignItems: "center", justifyContent: "center", padding: 24,
      border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow,
      transform: visible ? "scale(1)" : "scale(0.92)", opacity: visible ? 1 : 0,
      transition: "all 0.55s cubic-bezier(0.34, 1.56, 0.64, 1)",
    } as any}>
      <div style={{ display: "flex", alignItems: "center", gap: 10, background: bgCss(accentBg), color: theme.headerTextColor, fontSize: 12.5, fontWeight: 800, letterSpacing: 2.5, padding: "5px 20px", borderRadius: 4 }}>
        <Trophy size={15} /> {label}
      </div>

      <div style={{ marginTop: 18, display: "flex", alignItems: "center", gap: 16, maxWidth: "100%", minWidth: 0 }}>
        {teamLogoUrl
          ? <img src={teamLogoUrl} alt="" style={{ width: 72, height: 72, borderRadius: 10, objectFit: "cover", flexShrink: 0 }} />
          : <div style={{ width: 72, height: 72, borderRadius: 10, background: "rgba(255,255,255,0.15)", flexShrink: 0 }} />}
        <div style={{
          fontFamily: nameStyle.fontFamily || theme.fontDisplay,
          fontSize: nameStyle.fontSize ? `${nameStyle.fontSize}px` : "46px",
          fontWeight: 800, color: nameStyle.color || theme.textPrimary, lineHeight: 1.05,
          minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap",
        }}>
          {teamName}
        </div>
      </div>

      {stats.length > 0 && (
        <div style={{ marginTop: 18, display: "flex", gap: 12, flexWrap: "wrap", justifyContent: "center" }}>
          {stats.map((stat) => (
            <div key={stat.label} style={{ background: "rgba(0,0,0,0.38)", borderRadius: 7, padding: "8px 16px", textAlign: "center", minWidth: 84 }}>
              <div style={{ fontSize: 9.5, fontWeight: 700, letterSpacing: 0.9, color: "rgba(255,255,255,0.55)", fontFamily: theme.fontBody }}>{stat.label.toUpperCase()}</div>
              <div style={{ fontFamily: statStyle.fontFamily || theme.fontDisplay, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "24px", fontWeight: 800, color: statStyle.color || theme.textPrimary, lineHeight: 1.2 }}>
                {typeof stat.value === "number" ? stat.value.toLocaleString() : stat.value}
              </div>
            </div>
          ))}
        </div>
      )}

      {roster.length > 0 && (
        <div style={{ marginTop: 20, display: "flex", gap: 12, justifyContent: "center", width: "100%" }}>
          {roster.map((player) => (
            <div key={player.playerName} style={{ flex: "0 1 118px", minWidth: 0, background: theme.panelBg, backdropFilter: `blur(${theme.panelBlur})`, WebkitBackdropFilter: `blur(${theme.panelBlur})`, border: `1px solid ${theme.panelBorder}`, borderRadius: 7, overflow: "hidden" } as any}>
              <div style={{ height: 62, background: "rgba(255,255,255,0.06)", display: "flex", alignItems: "center", justifyContent: "center", overflow: "hidden" }}>
                {player.photoUrl
                  ? <img src={player.photoUrl} alt="" style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                  : <Trophy size={22} color="rgba(255,255,255,0.3)" />}
              </div>
              <div style={{ padding: "5px 8px", fontSize: 11.5, fontWeight: 700, color: theme.textPrimary, textAlign: "center", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                {player.playerName}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
