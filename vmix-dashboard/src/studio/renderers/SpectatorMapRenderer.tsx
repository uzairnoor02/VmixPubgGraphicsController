import { Bg, Theme, bgCss } from "../theme";

// Spectator map overlay - live team positions plotted on the match map.
//
// Feasible because LivePlayerInfo already carries Location {x, y, z} for every player straight
// from gettotalplayerlist. NOTE: LiveStatsBusiness.FilterPlayerInfo currently drops Location from
// its projection, so one line has to be added there before this can render live data - that is
// the only backend change this graphic needs.
//
// PUBG world coordinates run roughly 0..816000 on an 8x8km map. worldSize is configurable rather
// than hardcoded because map sizes differ (Erangel/Miramar are 8x8km, Sanhok is 4x4km), and
// getting it wrong would bunch every marker into one corner.

export interface MapPlayerMarker {
  key: string | number;
  teamId: number;
  teamName?: string;
  x: number;
  y: number;
  /** 5 = dead, 4 = knocked (see the liveState enum). Dead players are not plotted. */
  liveState: number;
}

export interface SpectatorMapRendererProps {
  theme: Theme;
  canvasBg: Bg;
  accentBg: Bg;
  /** Background map image. Without one the grid alone still reads as a map. */
  mapImageUrl?: string;
  mapName?: string;
  players: MapPlayerMarker[];
  worldSize?: number;
  /** Team to highlight, e.g. the one the director is spectating. */
  focusTeamId?: number;
  showLabels?: boolean;
}

export const DEFAULT_WORLD_SIZE = 816000;

/** Converts a world coordinate to a 0-100 percentage, clamped so a bad reading can never place a
 *  marker outside the map box. */
export function worldToPercent(value: number, worldSize: number = DEFAULT_WORLD_SIZE): number {
  if (!Number.isFinite(value) || worldSize <= 0) return 0;
  return Math.min(100, Math.max(0, (value / worldSize) * 100));
}

export function SpectatorMapRenderer({
  theme, canvasBg, accentBg, mapImageUrl, mapName, players,
  worldSize = DEFAULT_WORLD_SIZE, focusTeamId, showLabels = false,
}: SpectatorMapRendererProps) {
  // Dead players are gone from the map; knocked ones stay, dimmed, because where a team went
  // down is exactly what a spectator wants to see.
  const alive = players.filter((p) => p.liveState !== 5 && p.liveState !== 6);

  return (
    <div style={{ width: "100%", aspectRatio: "1/1", position: "relative", borderRadius: theme.radius, overflow: "hidden", background: bgCss(canvasBg), border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow, fontFamily: theme.fontDisplay } as any}>
      {mapImageUrl && (
        <img src={mapImageUrl} alt="" style={{ position: "absolute", inset: 0, width: "100%", height: "100%", objectFit: "cover", opacity: 0.85 }} />
      )}

      {/* A light grid so the map reads as a map even before an image is configured. */}
      <div style={{ position: "absolute", inset: 0, backgroundImage: "linear-gradient(rgba(255,255,255,0.07) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,0.07) 1px, transparent 1px)", backgroundSize: "12.5% 12.5%" }} />

      {alive.map((player) => {
        const isFocus = focusTeamId !== undefined && player.teamId === focusTeamId;
        const knocked = player.liveState === 4;
        const size = isFocus ? 11 : 8;
        return (
          <div
            key={player.key}
            style={{
              position: "absolute",
              left: `${worldToPercent(player.x, worldSize)}%`,
              top: `${worldToPercent(player.y, worldSize)}%`,
              transform: "translate(-50%, -50%)",
              display: "flex", alignItems: "center", gap: 4,
              // Focused team draws above the rest so it is never hidden under a scrum.
              zIndex: isFocus ? 2 : 1,
            }}
          >
            <div style={{
              width: size, height: size, borderRadius: "50%",
              background: isFocus ? bgCss(accentBg) : "rgba(255,255,255,0.75)",
              border: `1.5px solid ${isFocus ? "#fff" : "rgba(0,0,0,0.55)"}`,
              opacity: knocked ? 0.45 : 1,
              boxShadow: isFocus ? theme.glow : "none",
            }} />
            {showLabels && player.teamName && (
              <span style={{ fontSize: 8.5, fontWeight: 700, color: "#fff", textShadow: "0 1px 2px rgba(0,0,0,0.9)", whiteSpace: "nowrap" }}>
                {player.teamName}
              </span>
            )}
          </div>
        );
      })}

      {mapName && (
        <div style={{ position: "absolute", left: 10, top: 10, background: bgCss(accentBg), color: theme.headerTextColor, fontSize: 10.5, fontWeight: 800, letterSpacing: 0.8, padding: "3px 10px", borderRadius: 3 }}>
          {mapName.toUpperCase()}
        </div>
      )}
      <div style={{ position: "absolute", right: 10, bottom: 10, background: "rgba(0,0,0,0.5)", color: "#fff", fontSize: 10.5, fontWeight: 700, padding: "3px 9px", borderRadius: 3 }}>
        {alive.length} ALIVE
      </div>
    </div>
  );
}
