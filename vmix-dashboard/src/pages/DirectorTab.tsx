import { useEffect, useMemo, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { API_BASE, api } from "../lib/api";
import type { OverlayConfig } from "../lib/api";
import type { TeamLiveStats } from "../types";
import { DIRECTOR_KEYS, GRAPHICS, GRAPHIC_GROUPS, SEGMENT_PRESETS } from "../lib/graphics";

// The director panel: what goes on air, right now.
//
// Overlay Settings is about how a graphic *looks*; this is about whether it is *showing*, which
// during a live broadcast is a completely different job done under time pressure. Hence the
// bigger hit targets, the segment presets, and the blackout button.
//
// Everything here writes to the one OverlayConfig object and POSTs it to the endpoint that
// already exists. That endpoint already broadcasts OverlayConfigChanged, so a click reaches the
// on-air /overlay page immediately - no new API, no polling, no publish step.

export default function DirectorTab() {
  const [config, setConfig] = useState<OverlayConfig | null>(null);
  const [teams, setTeams] = useState<TeamLiveStats[]>([]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Saves are serialised through a ref-held queue. Without it, two quick toggles both POST the
  // config they each captured and the slower response silently reverts the faster click - the
  // exact failure you don't want while a graphic is going to air.
  const latestConfigRef = useRef<OverlayConfig | null>(null);
  const savingRef = useRef(false);
  const pendingRef = useRef(false);

  useEffect(() => {
    api.getOverlayConfig().then((c) => { setConfig(c); latestConfigRef.current = c; }).catch((e) => setError(String(e)));
    fetch(`${API_BASE}/api/match/teams`).then((r) => r.json()).then(setTeams).catch(() => {});

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/match`).withAutomaticReconnect().build();
    connection.on("TeamsUpdated", (updated: TeamLiveStats[]) => setTeams(updated));
    // Another operator (or the Studio) changing config must be reflected here rather than
    // overwritten by this tab's stale copy on the next click.
    connection.on("OverlayConfigChanged", (updated: OverlayConfig) => {
      if (!savingRef.current) { setConfig(updated); latestConfigRef.current = updated; }
    });
    connection.start().catch(() => {});
    return () => { connection.stop(); };
  }, []);

  async function flush() {
    if (savingRef.current) { pendingRef.current = true; return; }
    const next = latestConfigRef.current;
    if (!next) return;
    savingRef.current = true;
    setSaving(true);
    try {
      await api.saveOverlayConfig(next);
      setError(null);
    } catch (e) {
      setError(`Could not save: ${String(e)}`);
    } finally {
      savingRef.current = false;
      setSaving(false);
      if (pendingRef.current) { pendingRef.current = false; void flush(); }
    }
  }

  function update(mutate: (c: OverlayConfig) => OverlayConfig) {
    const base = latestConfigRef.current;
    if (!base) return;
    const next = mutate(base);
    latestConfigRef.current = next;
    setConfig(next);
    void flush();
  }

  const setVisible = (id: string, on: boolean) =>
    update((c) => ({ ...c, elementVisibility: { ...c.elementVisibility, [id]: on } }));

  const setSetting = (key: string, value: unknown) =>
    update((c) => ({ ...c, elementSettings: { ...c.elementSettings, [key]: value } }));

  const applyPreset = (presetId: string) =>
    update((c) => {
      const preset = SEGMENT_PRESETS[presetId];
      const next = { ...c.elementVisibility };
      // A preset sets every non-achievement graphic explicitly, so switching segments can't leave
      // a stray card from the previous one on air. Achievements are deliberately untouched.
      for (const g of GRAPHICS) {
        if (g.group === "Achievements") continue;
        next[g.id] = preset.on.includes(g.id);
      }
      return { ...c, elementVisibility: next };
    });

  const teamOptions = useMemo(
    () => [...teams].sort((a, b) => a.teamRank - b.teamRank).map((t) => ({ id: t.teamRank, label: t.teamName || t.tag })),
    [teams],
  );

  if (error && !config) return <div className="panel"><p style={{ color: "#ff6b81" }}>{error}</p></div>;
  if (!config) return <div className="panel"><p>Loading overlay config…</p></div>;

  const isOn = (id: string) => config.elementVisibility[id] ?? false;
  const liveCount = GRAPHICS.filter((g) => g.group !== "Achievements" && isOn(g.id)).length;

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 4, maxWidth: 1180, margin: "0 auto" }}>
      <div className="panel" style={{ display: "flex", alignItems: "center", gap: 12, flexWrap: "wrap" }}>
        <div style={{ fontWeight: 700 }}>Segment</div>
        {Object.entries(SEGMENT_PRESETS).map(([id, preset]) => (
          <button key={id} onClick={() => applyPreset(id)}
            className={id === "blackout" ? "secondary" : undefined}
            style={{ padding: "9px 16px", fontWeight: 600 }}>
            {preset.label}
          </button>
        ))}
        <div style={{ marginLeft: "auto", fontSize: 12, color: "#8a8a94" }}>
          {liveCount} graphic{liveCount === 1 ? "" : "s"} on air{saving ? " · saving…" : ""}
        </div>
      </div>

      {error && <div className="panel" style={{ color: "#ff6b81" }}>{error}</div>}

      {GRAPHIC_GROUPS.map((group) => {
        const rows = GRAPHICS.filter((g) => g.group === group);
        return (
          <div key={group} className="panel">
            <h3 style={{ marginTop: 0 }}>{group}</h3>
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {rows.map((g) => {
                const on = isOn(g.id);
                return (
                  <div key={g.id} style={{ display: "flex", alignItems: "center", gap: 12, padding: "8px 10px", borderRadius: 8, background: on ? "rgba(46,204,113,0.10)" : "rgba(255,255,255,0.03)", border: `1px solid ${on ? "rgba(46,204,113,0.35)" : "rgba(255,255,255,0.07)"}`, flexWrap: "wrap" }}>
                    <button
                      onClick={() => setVisible(g.id, !on)}
                      className={on ? undefined : "secondary"}
                      style={{ minWidth: 74, padding: "8px 14px", fontWeight: 700, letterSpacing: 0.5 }}
                    >
                      {on ? "ON AIR" : "OFF"}
                    </button>

                    <div style={{ minWidth: 0, flex: "1 1 220px" }}>
                      <div style={{ fontWeight: 600 }}>{g.label}</div>
                      {g.note && <div style={{ fontSize: 11.5, color: "#8a8a94", marginTop: 2 }}>{g.note}</div>}
                    </div>

                    {!g.automatic && (
                      <span style={{ fontSize: 10.5, fontWeight: 700, letterSpacing: 0.6, color: "#f5d76e", background: "rgba(241,196,15,0.12)", border: "1px solid rgba(241,196,15,0.3)", borderRadius: 20, padding: "2px 9px" }}>
                        MANUAL
                      </span>
                    )}

                    {g.selection === "team" && (
                      <TeamPicker
                        label={g.id === "spectatorMap" ? "Focus" : "Team"}
                        value={config.elementSettings[g.id === "spectatorMap" ? DIRECTOR_KEYS.mapFocusTeamId : DIRECTOR_KEYS.teamIntroTeamId] as number | undefined}
                        options={teamOptions}
                        onChange={(v) => setSetting(g.id === "spectatorMap" ? DIRECTOR_KEYS.mapFocusTeamId : DIRECTOR_KEYS.teamIntroTeamId, v)}
                      />
                    )}

                    {g.selection === "twoTeams" && (
                      <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
                        <TeamPicker label="Left" value={config.elementSettings[DIRECTOR_KEYS.h2hLeftTeamId] as number | undefined}
                          options={teamOptions} onChange={(v) => setSetting(DIRECTOR_KEYS.h2hLeftTeamId, v)} />
                        <TeamPicker label="Right" value={config.elementSettings[DIRECTOR_KEYS.h2hRightTeamId] as number | undefined}
                          options={teamOptions} onChange={(v) => setSetting(DIRECTOR_KEYS.h2hRightTeamId, v)} />
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          </div>
        );
      })}
    </div>
  );
}

function TeamPicker({ label, value, options, onChange }: {
  label: string;
  value: number | undefined;
  options: { id: number; label: string }[];
  onChange: (value: number | undefined) => void;
}) {
  return (
    <label style={{ display: "flex", alignItems: "center", gap: 6, fontSize: 12, color: "#c8c8d0" }}>
      {label}
      <select
        value={value ?? ""}
        onChange={(e) => onChange(e.target.value === "" ? undefined : Number(e.target.value))}
        style={{ background: "rgba(255,255,255,0.06)", border: "1px solid rgba(255,255,255,0.12)", borderRadius: 6, color: "#e8e8ec", padding: "6px 8px", fontSize: 12, maxWidth: 160 }}
      >
        <option value="">— none —</option>
        {options.map((o) => <option key={o.id} value={o.id}>{o.label}</option>)}
      </select>
    </label>
  );
}
