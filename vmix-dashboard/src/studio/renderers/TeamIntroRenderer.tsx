import { Bg, HealthStop, Theme, bgCss, colorForPlayer } from "../theme";
import type { ColumnStyle } from "../StudioControls";

// The team intro / WWCD card - a single team with its roster and win probability, shown before a
// match or when the director cuts to a team. The WWCD figure comes from the same
// CreateTop4LiveRanking calculation that drives the Top 4 panel, so the two can never disagree.

export interface TeamIntroPlayer {
  playerName: string;
  photoUrl?: string;
  /** Optional live state, so the card doubles as a mid-match cut-in. Omit for a pre-match intro. */
  health?: number;
  liveState?: number;
}

export interface TeamIntroRendererProps {
  theme: Theme;
  canvasBg: Bg;
  accentBg: Bg;
  teamName: string;
  teamLogoUrl?: string;
  /** Win-probability percentage. Pass null for a pre-match intro, where no live figure exists —
   *  the bar is hidden entirely rather than showing a misleading 0%. */
  wwcd: number | null;
  players: TeamIntroPlayer[];
  stats?: { label: string; value: string | number }[];
  healthStops?: HealthStop[];
  fields: Record<string, ColumnStyle>;
  label?: string;
}

export function TeamIntroRenderer({
  theme, canvasBg, accentBg, teamName, teamLogoUrl, wwcd, players, stats = [], healthStops, fields, label,
}: TeamIntroRendererProps) {
  const nameStyle = fields.teamName?.mode === "custom" ? fields.teamName.custom : ({} as any);
  const statStyle = fields.statValue?.mode === "custom" ? fields.statValue.custom : ({} as any);
  const roster = players.slice(0, 4);
  const showWwcd = wwcd !== null && Number.isFinite(wwcd);

  return (
    <div style={{ width: "100%", height: "100%", borderRadius: theme.radius, overflow: "hidden", background: bgCss(canvasBg), fontFamily: theme.fontDisplay, display: "flex", flexDirection: "column", padding: 22, border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow } as any}>
      {label && (
        <div style={{ alignSelf: "flex-start", background: bgCss(accentBg), color: theme.headerTextColor, fontSize: 11, fontWeight: 800, letterSpacing: 1.4, padding: "3px 13px", borderRadius: 3, marginBottom: 12 }}>{label}</div>
      )}

      <div style={{ display: "flex", alignItems: "center", gap: 14, minWidth: 0 }}>
        {teamLogoUrl
          ? <img src={teamLogoUrl} alt="" style={{ width: 56, height: 56, borderRadius: 9, objectFit: "cover", flexShrink: 0 }} />
          : <div style={{ width: 56, height: 56, borderRadius: 9, background: "rgba(255,255,255,0.15)", flexShrink: 0 }} />}
        <div style={{ minWidth: 0, flex: 1 }}>
          <div style={{ fontFamily: nameStyle.fontFamily || theme.fontDisplay, fontSize: nameStyle.fontSize ? `${nameStyle.fontSize}px` : "30px", fontWeight: 800, color: nameStyle.color || theme.textPrimary, lineHeight: 1.1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
            {teamName}
          </div>
          {showWwcd && (
            <div style={{ marginTop: 7 }}>
              <div style={{ display: "flex", justifyContent: "space-between", fontSize: 10, fontWeight: 700, letterSpacing: 0.8, color: "rgba(255,255,255,0.6)", fontFamily: theme.fontBody }}>
                <span>WIN PROBABILITY</span><span>{wwcd!.toFixed(1)}%</span>
              </div>
              <div style={{ marginTop: 3, height: 7, borderRadius: 4, background: "rgba(255,255,255,0.10)", overflow: "hidden" }}>
                {/* Clamped so a backend value outside 0-100 can't paint outside the track. */}
                <div style={{ height: "100%", width: `${Math.min(100, Math.max(0, wwcd!))}%`, background: bgCss(accentBg), transition: "width 0.6s ease" }} />
              </div>
            </div>
          )}
        </div>
      </div>

      {stats.length > 0 && (
        <div style={{ marginTop: 14, display: "flex", gap: 10, flexWrap: "wrap" }}>
          {stats.map((stat) => (
            <div key={stat.label} style={{ flex: "1 1 78px", minWidth: 78, background: "rgba(0,0,0,0.35)", borderRadius: 6, padding: "7px 10px" }}>
              <div style={{ fontSize: 9, fontWeight: 700, letterSpacing: 0.7, color: "rgba(255,255,255,0.55)", fontFamily: theme.fontBody }}>{stat.label.toUpperCase()}</div>
              <div style={{ fontFamily: statStyle.fontFamily || theme.fontDisplay, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "20px", fontWeight: 800, color: statStyle.color || theme.textPrimary, lineHeight: 1.2 }}>
                {typeof stat.value === "number" ? stat.value.toLocaleString() : stat.value}
              </div>
            </div>
          ))}
        </div>
      )}

      <div style={{ marginTop: "auto", paddingTop: 14, display: "flex", gap: 10 }}>
        {roster.map((player) => (
          <div key={player.playerName} style={{ flex: "1 1 0", maxWidth: 130, minWidth: 0, background: theme.panelBg, backdropFilter: `blur(${theme.panelBlur})`, WebkitBackdropFilter: `blur(${theme.panelBlur})`, border: `1px solid ${theme.panelBorder}`, borderRadius: 7, overflow: "hidden" } as any}>
            <div style={{ height: 56, background: "rgba(255,255,255,0.06)", display: "flex", alignItems: "center", justifyContent: "center", overflow: "hidden" }}>
              {player.photoUrl
                ? <img src={player.photoUrl} alt="" style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                : <div style={{ width: 26, height: 26, borderRadius: "50%", background: "rgba(255,255,255,0.18)" }} />}
            </div>
            {/* Health strip only when live state was supplied - a pre-match intro has none, and a
                flat bar of "full health" would imply live data that doesn't exist yet. */}
            {healthStops && player.liveState !== undefined && player.health !== undefined && (
              <div style={{ height: 3, background: colorForPlayer(healthStops, { health: player.health, liveState: player.liveState }).color }} />
            )}
            <div style={{ padding: "5px 7px", fontSize: 11, fontWeight: 700, color: theme.textPrimary, textAlign: "center", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
              {player.playerName}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
