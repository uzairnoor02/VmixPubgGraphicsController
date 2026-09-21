import { Bg, Theme, bgCss } from "../theme";

// The "circle closing in" status bar - the thin always-on strip that tells the viewer which zone
// phase the match is in and how long is left. Data comes from pcob's getcircleinfo (see
// CircleInfo in VmixData/Models/MatchModels/TeamInfo.cs), which reports GameTime, CircleStatus,
// CircleIndex, Counter and MaxTime as strings.
//
// Follows the same contract as every other renderer here: the Studio preview and the live
// /overlay render this exact component, and sizing is the caller's job.

/** pcob reports CircleStatus as a string. These are the two phases that matter on air: the zone
 *  is either counting down before it moves, or actively shrinking. */
export type CirclePhase = "waiting" | "closing";

export interface CircleStatusRendererProps {
  theme: Theme;
  barBg: Bg;
  /** 1-based zone number, straight from CircleIndex. */
  circleIndex: number;
  phase: CirclePhase;
  /** Seconds remaining in the current phase (Counter). */
  secondsRemaining: number;
  /** Total seconds this phase runs for (MaxTime), used for the progress fill. 0 hides the fill
   *  rather than dividing by zero. */
  phaseSeconds: number;
  label?: string;
}

/** mm:ss, clamped at zero - pcob's Counter can briefly report a negative as a phase flips. */
export function formatCountdown(totalSeconds: number): string {
  const safe = Math.max(0, Math.floor(totalSeconds));
  const minutes = Math.floor(safe / 60);
  const seconds = safe % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

/** Parses pcob's string fields into a number without throwing on an unexpected value. */
export function parseCircleNumber(raw: string | null | undefined): number {
  if (raw === null || raw === undefined) return 0;
  const parsed = Number.parseFloat(raw);
  return Number.isFinite(parsed) ? parsed : 0;
}

export function CircleStatusRenderer({
  theme, barBg, circleIndex, phase, secondsRemaining, phaseSeconds, label,
}: CircleStatusRendererProps) {
  const progress = phaseSeconds > 0
    ? Math.min(1, Math.max(0, 1 - secondsRemaining / phaseSeconds))
    : 0;
  const heading = label ?? (phase === "closing" ? "ZONE CLOSING" : "NEXT ZONE IN");
  // Urgency is a visual cue, not data: the last 15 seconds of either phase is when the caster
  // and the viewer care, so the countdown picks up the danger colour then.
  const urgent = secondsRemaining <= 15;

  return (
    <div style={{ width: "100%", borderRadius: theme.radius, overflow: "hidden", fontFamily: theme.fontDisplay, border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow, background: theme.panelBg, backdropFilter: `blur(${theme.panelBlur})`, WebkitBackdropFilter: `blur(${theme.panelBlur})` } as any}>
      <div style={{ display: "flex", alignItems: "center", gap: 12, padding: "8px 14px" }}>
        <div style={{ background: bgCss(barBg), color: theme.headerTextColor, fontWeight: 800, fontSize: 12, padding: "4px 10px", borderRadius: 4, letterSpacing: 0.5, flexShrink: 0 }}>
          ZONE {circleIndex}
        </div>
        <div style={{ fontSize: 11.5, fontWeight: 700, color: theme.textPrimary, letterSpacing: 0.8, flex: 1, whiteSpace: "nowrap", overflow: "hidden", textOverflow: "ellipsis" }}>
          {heading}
        </div>
        <div style={{ fontSize: 19, fontWeight: 800, fontVariantNumeric: "tabular-nums", color: urgent ? "#FF3B5C" : theme.textPrimary, flexShrink: 0 }}>
          {formatCountdown(secondsRemaining)}
        </div>
      </div>
      <div style={{ height: 4, background: "rgba(255,255,255,0.10)" }}>
        <div style={{ height: "100%", width: `${progress * 100}%`, background: bgCss(barBg), transition: "width 0.9s linear" }} />
      </div>
    </div>
  );
}
