import { useEffect, useState } from "react";
import { Palette, Play, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useChromaKey, useStudioElement } from "../StudioConfigContext";
import { Bg } from "../theme";
import { BgEditor, EditorPanel, PageShell, ResetToThemeButton, btnGhost, pill } from "../StudioControls";
import { CirclePhase, CircleStatusRenderer } from "../renderers/CircleStatusRenderer";

export default function CircleStatusPage() {
  const { theme } = useTheme();
  const [chromaKey] = useChromaKey();
  const [phase, setPhase] = useState<CirclePhase>("waiting");
  const [tab, setTab] = useState("bar");
  const [barBgOverride, setBarBgOverride] = useStudioElement<Bg | null>("circle.barBg", null);
  const [waitingLabel, setWaitingLabel] = useStudioElement("circle.waitingLabel", "NEXT ZONE IN");
  const [closingLabel, setClosingLabel] = useStudioElement("circle.closingLabel", "ZONE CLOSING");
  const barBg = barBgOverride || theme.headerBg;

  // Live countdown in the preview, so the urgency colour change and the progress fill can be
  // judged in motion rather than as a still frame - they are the whole point of this graphic.
  const phaseSeconds = phase === "closing" ? 90 : 180;
  const [secondsRemaining, setSecondsRemaining] = useState(phaseSeconds);
  useEffect(() => {
    setSecondsRemaining(phaseSeconds);
    const id = window.setInterval(() => setSecondsRemaining((s) => (s <= 0 ? phaseSeconds : s - 1)), 1000);
    return () => window.clearInterval(id);
  }, [phase, phaseSeconds]);

  return (
    <PageShell title="Circle Status" subtitle="Zone countdown bar, driven by pcob's getcircleinfo">
      <div style={{ flex: "1 1 460px" }}>
        <div style={{ display: "flex", gap: 6, marginBottom: 12 }}>
          <button onClick={() => setPhase("waiting")} style={pill(phase === "waiting")}>Waiting for zone</button>
          <button onClick={() => setPhase("closing")} style={pill(phase === "closing")}>Zone closing</button>
        </div>
        <div style={{ width: "100%", maxWidth: 520, borderRadius: theme.radius, overflow: "hidden", background: chromaKey, padding: 24, border: "1px solid rgba(255,255,255,0.12)", position: "relative" }}>
          <div style={{ position: "absolute", fontSize: 10, color: "rgba(0,0,0,0.4)", top: 8, left: 10 }}>keyed out by vMix in production</div>
          <CircleStatusRenderer
            theme={theme} barBg={barBg} circleIndex={4} phase={phase}
            secondsRemaining={secondsRemaining} phaseSeconds={phaseSeconds}
            label={phase === "closing" ? closingLabel : waitingLabel}
          />
        </div>
        <button onClick={() => setSecondsRemaining(12)} style={{ ...btnGhost, marginTop: 12, display: "flex", alignItems: "center", gap: 6 }}>
          <Play size={13} /> Jump to final 12s
        </button>
        <div style={{ marginTop: 14, padding: 12, borderRadius: 8, fontSize: 12, lineHeight: 1.5, background: "rgba(241,196,15,0.1)", border: "1px solid rgba(241,196,15,0.3)", color: "#f5d76e" }}>
          <b>Needs a backend feed:</b> getcircleinfo is already polled by GetLiveData, but its value
          isn't broadcast to the overlay yet. The overlay slot is wired and will light up as soon as
          a <code>CircleUpdated</code> SignalR event starts firing.
        </div>
      </div>

      <EditorPanel tabs={[{ id: "bar", label: "Bar", icon: Palette }, { id: "labels", label: "Labels", icon: Type }]} activeTab={tab} setActiveTab={setTab}>
        {tab === "bar" && (
          <div>
            <div style={{ fontSize: 12, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>The zone badge and progress fill, capped at 3 gradient stops.</div>
            <BgEditor bg={barBg} setBg={setBarBgOverride} maxStops={3} />
            {barBgOverride && <ResetToThemeButton onClick={() => setBarBgOverride(null)} />}
          </div>
        )}
        {tab === "labels" && (
          <div>
            {[
              { label: "Waiting phase", value: waitingLabel, set: setWaitingLabel },
              { label: "Closing phase", value: closingLabel, set: setClosingLabel },
            ].map((row) => (
              <label key={row.label} style={{ display: "block", marginBottom: 12, fontSize: 12, color: "#c8c8d0" }}>
                {row.label}
                <input
                  value={row.value}
                  onChange={(e) => row.set(e.target.value)}
                  style={{ width: "100%", marginTop: 5, background: "rgba(255,255,255,0.06)", border: "1px solid rgba(255,255,255,0.12)", borderRadius: 6, color: "#e8e8ec", padding: "7px 9px", fontSize: 12 }}
                />
              </label>
            ))}
          </div>
        )}
      </EditorPanel>
    </PageShell>
  );
}
