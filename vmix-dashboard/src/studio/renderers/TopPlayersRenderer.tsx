import { User } from "lucide-react";
import { Bg, Theme, bgCss } from "../theme";
import { panelSurface } from "./surface";
import type { ColumnStyle } from "../StudioControls";

// The Top Players podium visual - extracted out of TopPlayersPage.tsx so the Studio preview and
// the real /overlay route render from exactly the same component, matching StandingsRenderer and
// RankingsRenderer. Sizing is the caller's job: this fills 100% of whatever box it's given.

export interface TopPlayerEntry {
  rank: number;
  playerName: string;
  value: number;
  statLabel: string;
  photoUrl?: string;
}

export interface TopPlayersRendererProps {
  theme: Theme;
  canvasBg: Bg;
  labelBg: Bg;
  cardBg: Bg;
  fields: Record<string, ColumnStyle>;
  players: TopPlayerEntry[];
  title?: string;
  /** 0-100, Task 9. Omitted/undefined = 100 = today's appearance, unchanged. */
  panelOpacity?: number;
}

export function TopPlayersRenderer({ theme, canvasBg, labelBg, cardBg, fields, players, title = "TOP PLAYERS", panelOpacity }: TopPlayersRendererProps) {
  const nameStyle = fields.playerName?.mode === "custom" ? fields.playerName.custom : ({} as any);
  const statStyle = fields.statValue?.mode === "custom" ? fields.statValue.custom : ({} as any);
  // The footer label comes from the entries themselves, so an empty list can't crash the graphic
  // the way players[0].statLabel would.
  const statLabel = players[0]?.statLabel ?? "";

  return (
    <div style={{ width: "100%", height: "100%", borderRadius: theme.radius, overflow: "hidden", background: bgCss(canvasBg), fontFamily: theme.fontDisplay, display: "flex", flexDirection: "column", padding: 22, border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow } as any}>
      <div style={{ fontSize: 30, fontWeight: 800, color: theme.textPrimary }}>{title}</div>
      <div style={{ display: "flex", gap: 12, marginTop: 16, flex: 1 }}>
        {players.map((p) => (
          <div key={p.rank} style={{ flex: 1, display: "flex", flexDirection: "column" }}>
            <div style={{ flex: 1, ...panelSurface(panelOpacity, theme.panelBg, theme.panelBlur), border: "1px solid rgba(255,255,255,0.15)", borderRadius: 6, display: "flex", alignItems: "center", justifyContent: "center", overflow: "hidden" } as any}>
              {p.photoUrl
                ? <img src={p.photoUrl} alt="" style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                : <User size={40} color="rgba(255,255,255,0.5)" />}
            </div>
            <div style={{ background: bgCss(labelBg), color: theme.headerTextColor, fontWeight: 800, fontSize: 13, padding: "3px 8px" }}>#{p.rank}</div>
            <div style={{ background: bgCss(cardBg), color: nameStyle.color || theme.textPrimary, fontFamily: nameStyle.fontFamily || theme.fontBody, fontSize: nameStyle.fontSize ? `${nameStyle.fontSize}px` : "14px", fontWeight: 700, padding: "6px 8px", textAlign: "center", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{p.playerName}</div>
            <div style={{ background: "rgba(0,0,0,0.5)", color: statStyle.color || theme.textPrimary, fontFamily: statStyle.fontFamily || theme.fontBody, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "22px", fontWeight: 800, padding: "4px 8px", textAlign: "center" }}>{p.value.toLocaleString()}</div>
          </div>
        ))}
      </div>
      {statLabel && (
        <div style={{ marginTop: 12, display: "flex", justifyContent: "center" }}>
          <div style={{ background: bgCss(labelBg), color: theme.headerTextColor, fontSize: 12, fontWeight: 700, padding: "4px 16px", letterSpacing: 1 }}>{statLabel}</div>
        </div>
      )}
    </div>
  );
}
