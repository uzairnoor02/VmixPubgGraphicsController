import { User } from "lucide-react";
import { Bg, Theme, bgCss } from "../theme";
import type { ColumnStyle } from "../StudioControls";

// One player, big - the card behind "MVP of the Match", "Star Player" and "Player Highlight".
// Those are the same graphic with a different eyebrow and a different stat set, so they are one
// renderer with a configurable label and a caller-supplied stat list rather than three
// near-identical components that would drift apart.
//
// Same contract as the other renderers: Studio preview and live /overlay share this exact
// component, and sizing belongs to the caller.

export interface HighlightStat {
  label: string;
  value: string | number;
}

export interface PlayerHighlightRendererProps {
  theme: Theme;
  canvasBg: Bg;
  accentBg: Bg;
  /** Eyebrow above the name, e.g. "MVP OF THE MATCH". */
  label: string;
  playerName: string;
  teamName?: string;
  photoUrl?: string;
  teamLogoUrl?: string;
  stats: HighlightStat[];
  fields: Record<string, ColumnStyle>;
}

export function PlayerHighlightRenderer({
  theme, canvasBg, accentBg, label, playerName, teamName, photoUrl, teamLogoUrl, stats, fields,
}: PlayerHighlightRendererProps) {
  const nameStyle = fields.playerName?.mode === "custom" ? fields.playerName.custom : ({} as any);
  const statStyle = fields.statValue?.mode === "custom" ? fields.statValue.custom : ({} as any);

  return (
    <div style={{ width: "100%", height: "100%", borderRadius: theme.radius, overflow: "hidden", background: bgCss(canvasBg), fontFamily: theme.fontDisplay, display: "flex", border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow } as any}>
      {/* Portrait. Falls back to a generic figure when no photo exists for this UID, the same
          way the achievement banner does, so a missing file is never a broken image. */}
      <div style={{ width: "38%", flexShrink: 0, background: theme.panelBg, display: "flex", alignItems: "center", justifyContent: "center", overflow: "hidden", borderRight: `1px solid ${theme.panelBorder}` }}>
        {photoUrl
          ? <img src={photoUrl} alt="" style={{ width: "100%", height: "100%", objectFit: "cover" }} />
          : <User size={72} color="rgba(255,255,255,0.35)" />}
      </div>

      <div style={{ flex: 1, minWidth: 0, display: "flex", flexDirection: "column", padding: "20px 22px" }}>
        <div style={{ alignSelf: "flex-start", background: bgCss(accentBg), color: theme.headerTextColor, fontSize: 11.5, fontWeight: 800, letterSpacing: 1, padding: "4px 12px", clipPath: "polygon(0 0, 100% 0, 94% 100%, 0% 100%)" }}>
          {label}
        </div>

        <div style={{ marginTop: 12, display: "flex", alignItems: "center", gap: 10, minWidth: 0 }}>
          {teamLogoUrl
            ? <img src={teamLogoUrl} alt="" style={{ width: 26, height: 26, borderRadius: 4, objectFit: "cover", flexShrink: 0 }} />
            : <div style={{ width: 26, height: 26, borderRadius: 4, background: "rgba(255,255,255,0.15)", flexShrink: 0 }} />}
          <div style={{ minWidth: 0 }}>
            <div style={{ fontFamily: nameStyle.fontFamily || theme.fontDisplay, fontSize: nameStyle.fontSize ? `${nameStyle.fontSize}px` : "26px", fontWeight: 800, color: nameStyle.color || theme.textPrimary, lineHeight: 1.1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
              {playerName}
            </div>
            {teamName && (
              <div style={{ fontSize: 12, fontWeight: 600, color: "rgba(255,255,255,0.6)", fontFamily: theme.fontBody, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{teamName}</div>
            )}
          </div>
        </div>

        {/* Stats wrap rather than scroll: the caller decides how many to show, and a broadcast
            graphic must never produce a scrollbar on air. */}
        <div style={{ marginTop: "auto", display: "flex", flexWrap: "wrap", gap: 10 }}>
          {stats.map((stat) => (
            <div key={stat.label} style={{ flex: "1 1 70px", minWidth: 70, background: "rgba(0,0,0,0.35)", borderRadius: 6, padding: "8px 10px" }}>
              <div style={{ fontSize: 9.5, fontWeight: 700, letterSpacing: 0.8, color: "rgba(255,255,255,0.55)", fontFamily: theme.fontBody }}>{stat.label.toUpperCase()}</div>
              <div style={{ fontFamily: statStyle.fontFamily || theme.fontDisplay, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "22px", fontWeight: 800, color: statStyle.color || theme.textPrimary, lineHeight: 1.2 }}>
                {typeof stat.value === "number" ? stat.value.toLocaleString() : stat.value}
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
