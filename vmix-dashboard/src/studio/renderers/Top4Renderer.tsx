import { Bg, DEFAULT_HEALTH_STOPS, HealthStop, Theme, bgCss } from "../theme";
import { panelSurface } from "./surface";
import type { ColumnStyle } from "../StudioControls";
import {
  DEFAULT_HEALTH_STYLE, HealthStyle, HelmetIcon, THROWABLE_LABELS, THROWABLE_ORDER, ThrowableIcon, Throwables,
} from "./healthGlyphs";
import { TeamLogo } from "./shared";

// The Last 4 teams cards (PMGO layout): four wide cards across the top of the screen.
//   row 1  rank chip | logo | TEAM TAG ............ one helmet per player, filled to health
//   row 2  frag | smoke | molotov | stun  - what the team is carrying
//   row 3  (optional) WWCD chance bar
// Shared by the Studio preview, /demo and /overlay - one render path.

export interface Top4Team {
  key: string | number;
  overallRank: number;
  tag: string;
  wwcd: number;
  players: { health: number; liveState: number }[];
  logoUrl?: string;
  /** Carried throwables, summed over the team. null/undefined = not seen yet this match (pcob
   *  only reports the inventory of the team the observer is watching) -> shown as "-". */
  throwables?: Throwables | null;
  eliminated?: boolean;
}

export interface Top4RendererProps {
  theme: Theme;
  /** Background of the WWCD chance fill. */
  wwcdBar: Bg;
  cardBg: Bg;
  fields: Record<string, ColumnStyle>;
  teams: Top4Team[];
  /** 0-100, Task 9. Omitted/undefined = 100 = today's appearance, unchanged. */
  panelOpacity?: number;
  healthStyle?: HealthStyle;
  healthStops?: HealthStop[];
  showWwcd?: boolean;
  showThrowables?: boolean;
  showRank?: boolean;
}

export function Top4Renderer({
  theme, wwcdBar, cardBg, fields, teams, panelOpacity,
  healthStyle = DEFAULT_HEALTH_STYLE, healthStops = DEFAULT_HEALTH_STOPS,
  showWwcd = true, showThrowables = true, showRank = false,
}: Top4RendererProps) {
  const rankStyle = fields.overallRank?.mode === "custom" ? fields.overallRank.custom : ({} as any);
  const tagStyle = fields.tag?.mode === "custom" ? fields.tag.custom : ({} as any);
  const wwcdTextStyle = fields.wwcdText?.mode === "custom" ? fields.wwcdText.custom : ({} as any);
  const countStyle = fields.throwables?.mode === "custom" ? fields.throwables.custom : ({} as any);
  const noShadow = panelOpacity !== undefined && panelOpacity <= 0;

  return (
    <div style={{ display: "flex", gap: 14, fontFamily: theme.fontDisplay }}>
      {teams.map((t) => {
        // Tag steps down in size for long names rather than being cut off - it is the one thing
        // a viewer must be able to read.
        const tagLen = t.tag.length;
        const autoTagSize = tagLen > 10 ? 17 : tagLen > 7 ? 20 : tagLen > 5 ? 23 : 26;
        const eliminated = t.eliminated ?? t.players.every((p) => p.liveState === 5 || p.liveState === 6);
        const wwcdPct = Number.isFinite(t.wwcd) ? Math.max(0, Math.min(100, t.wwcd)) : null;
        return (
          <div key={t.key} style={{
            flex: 1, minWidth: 0, borderRadius: Math.min(theme.radius, 8), overflow: "hidden",
            ...panelSurface(panelOpacity, bgCss(cardBg), theme.panelBlur),
            border: noShadow ? "none" : `1px solid ${theme.panelBorder}`, boxShadow: noShadow ? "none" : theme.glow,
            opacity: eliminated ? 0.5 : 1, filter: eliminated ? "grayscale(1)" : "none", transition: "opacity .4s, filter .4s",
          } as any}>
            {/* accent line along the top edge, in the theme's header colours */}
            <div style={{ height: 3, background: bgCss(theme.headerBg) }} />

            <div style={{ display: "flex", alignItems: "center", gap: 8, padding: "7px 10px 6px" }}>
              {showRank && (
                <div style={{
                  minWidth: 22, height: 22, borderRadius: 5, display: "flex", alignItems: "center", justifyContent: "center",
                  background: "rgba(0,0,0,0.45)", flexShrink: 0, fontWeight: 800,
                  fontFamily: rankStyle.fontFamily || theme.fontDisplay,
                  fontSize: rankStyle.fontSize ? `${rankStyle.fontSize}px` : "13px",
                  color: rankStyle.color || theme.textPrimary,
                }}>{t.overallRank}</div>
              )}
              <TeamLogo url={t.logoUrl} size={36} radius={6} />
              <div style={{
                flex: 1, minWidth: 0, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap",
                fontWeight: 800, letterSpacing: 0.4,
                fontFamily: tagStyle.fontFamily || theme.fontDisplay,
                fontSize: tagStyle.fontSize ? `${tagStyle.fontSize}px` : `${autoTagSize}px`,
                color: tagStyle.color || theme.textPrimary,
              }}>{t.tag}</div>
              <div style={{ display: "flex", gap: 3, flexShrink: 0 }}>
                {t.players.slice(0, 4).map((p, i) => <HelmetIcon key={i} player={p} style={healthStyle} stops={healthStops} size={28} />)}
              </div>
            </div>

            {showThrowables && (
              <div style={{ display: "flex", background: "rgba(0,0,0,0.35)", borderTop: "1px solid rgba(255,255,255,0.06)" }}>
                {THROWABLE_ORDER.map((kind, i) => {
                  const count = t.throwables ? t.throwables[kind] : null;
                  const empty = count === 0;
                  return (
                    <div key={kind} title={THROWABLE_LABELS[kind]} style={{
                      flex: 1, display: "flex", alignItems: "center", justifyContent: "center", gap: 5, padding: "5px 0",
                      borderLeft: i === 0 ? "none" : "1px solid rgba(255,255,255,0.08)",
                      opacity: empty ? 0.4 : 1,
                    }}>
                      <ThrowableIcon kind={kind} size={17} color={countStyle.color || theme.textPrimary} />
                      <span style={{
                        fontWeight: 800, fontVariantNumeric: "tabular-nums",
                        fontFamily: countStyle.fontFamily || theme.fontDisplay,
                        fontSize: countStyle.fontSize ? `${countStyle.fontSize}px` : "17px",
                        color: countStyle.color || theme.textPrimary,
                      }}>{count ?? "-"}</span>
                    </div>
                  );
                })}
              </div>
            )}

            {showWwcd && (
              <div style={{ position: "relative", height: 22, background: "rgba(0,0,0,0.5)" }}>
                <div style={{ position: "absolute", inset: 0, width: `${wwcdPct ?? 0}%`, background: bgCss(wwcdBar), transition: "width .6s ease" }} />
                <div style={{
                  position: "relative", height: "100%", display: "flex", alignItems: "center", justifyContent: "space-between", padding: "0 9px",
                  fontFamily: wwcdTextStyle.fontFamily || theme.fontDisplay, color: wwcdTextStyle.color || "#FFFFFF",
                  textShadow: "0 1px 2px rgba(0,0,0,0.6)",
                }}>
                  <span style={{ fontSize: 11, fontWeight: 800, letterSpacing: 1 }}>WWCD</span>
                  <span style={{ fontSize: wwcdTextStyle.fontSize ? `${wwcdTextStyle.fontSize}px` : "15px", fontWeight: 800, fontVariantNumeric: "tabular-nums" }}>
                    {wwcdPct === null ? "—" : `${Math.round(wwcdPct * 10) / 10}%`}
                  </span>
                </div>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
