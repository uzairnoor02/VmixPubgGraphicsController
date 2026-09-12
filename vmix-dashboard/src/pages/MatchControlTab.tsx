import { useEffect, useState } from "react";
import { api, POST_MATCH_STEPS, type MatchSelector } from "../lib/api";

// Replaces the WinForms window entirely for match control: pick tournament/stage/day/match, start
// it (with the same "in-progress? completed?" confirmation flow start_btn_Click used to show via
// MessageBox), stop it, and run post-match report generation - one step at a time or all of them.
// See Pubg Ranking System/MatchControlApi.cs for the endpoints this talks to.

const DAYS = Array.from({ length: 8 }, (_, i) => String(i + 1));
const MATCHES = Array.from({ length: 25 }, (_, i) => String(i + 1));
const MAPS = ["Erangel", "Miramar", "Sanhok"];

export default function MatchControlTab() {
  const [tournaments, setTournaments] = useState<string[]>([]);
  const [stages, setStages] = useState<string[]>([]);
  const [selector, setSelector] = useState<MatchSelector>({ tournament: "", stage: "", day: "1", match: "1" });
  const [mapName, setMapName] = useState(MAPS[0]);

  const [newTournamentName, setNewTournamentName] = useState("");
  const [newStageName, setNewStageName] = useState("");

  const [busy, setBusy] = useState<string | null>(null);
  const [log, setLog] = useState<{ text: string; kind: "info" | "error" | "success" }[]>([]);

  const [pendingConfirm, setPendingConfirm] = useState<{ message: string; requiresTypedDelete: boolean } | null>(null);
  const [typedDelete, setTypedDelete] = useState("");

  function pushLog(text: string, kind: "info" | "error" | "success" = "info") {
    setLog((prev) => [{ text, kind }, ...prev].slice(0, 30));
  }

  async function refreshLookups() {
    try {
      const [t, s] = await Promise.all([api.getTournamentNames(), api.getStageNames()]);
      setTournaments(t);
      setStages(s);
      setSelector((prev) => ({ ...prev, tournament: prev.tournament || t[0] || "", stage: prev.stage || s[0] || "" }));
    } catch (err: any) {
      pushLog(`Failed to load tournaments/stages: ${err.message}`, "error");
    }
  }

  useEffect(() => { refreshLookups(); }, []);

  async function handleAddTournament() {
    if (!newTournamentName.trim()) return;
    setBusy("add-tournament");
    try {
      const res = await api.addTournament(newTournamentName.trim());
      pushLog(res.message || (res.ok ? "Tournament created." : res.error || "Failed"), res.ok ? "success" : "error");
      if (res.ok) { setNewTournamentName(""); await refreshLookups(); }
    } catch (err: any) {
      pushLog(err.message, "error");
    } finally {
      setBusy(null);
    }
  }

  async function handleAddStage() {
    if (!newStageName.trim() || !selector.tournament) return;
    setBusy("add-stage");
    try {
      const res = await api.addStage(selector.tournament, newStageName.trim());
      pushLog(res.message || (res.ok ? "Stage created." : res.error || "Failed"), res.ok ? "success" : "error");
      if (res.ok) { setNewStageName(""); await refreshLookups(); }
    } catch (err: any) {
      pushLog(err.message, "error");
    } finally {
      setBusy(null);
    }
  }

  async function handleStart(confirm = false, typed?: string) {
    if (!selector.tournament || !selector.stage) { pushLog("Pick a tournament and stage first.", "error"); return; }
    setBusy("start");
    try {
      const res = await api.startMatch(selector, confirm, typed);
      if (res.requiresConfirmation) {
        setPendingConfirm({ message: res.message, requiresTypedDelete: res.requiresTypedDelete });
      } else {
        setPendingConfirm(null);
        setTypedDelete("");
        pushLog(res.message, res.ok ? "success" : "error");
      }
    } catch (err: any) {
      pushLog(err.message, "error");
    } finally {
      setBusy(null);
    }
  }

  async function handleStop() {
    setBusy("stop");
    try {
      await api.stopMatch();
      pushLog("Match stopped - queued jobs cancelled, match state cleared.", "success");
    } catch (err: any) {
      pushLog(err.message, "error");
    } finally {
      setBusy(null);
    }
  }

  async function handleRunStep(stepId: string, label: string) {
    setBusy(`step:${stepId}`);
    try {
      const res = await api.runPostMatchStep(stepId, selector);
      pushLog(`${label}: ${res.ok ? "done" : res.error || "failed"}`, res.ok ? "success" : "error");
    } catch (err: any) {
      pushLog(`${label}: ${err.message}`, "error");
    } finally {
      setBusy(null);
    }
  }

  async function handleRunAll() {
    setBusy("run-all");
    try {
      const res = await api.runAllPostMatch(selector);
      if (res.ok) pushLog("All post-match reports generated.", "success");
      else pushLog(`Partial: ${(res.errors || []).join("; ")}`, "error");
    } catch (err: any) {
      pushLog(err.message, "error");
    } finally {
      setBusy(null);
    }
  }

  async function handleMapPerformers() {
    setBusy("map-performers");
    try {
      const res = await api.mapTopPerformers(selector, mapName);
      pushLog(res.ok ? `Map Top Performers (${mapName}) generated.` : res.error || "Failed to generate map top performers.", res.ok ? "success" : "error");
    } catch (err: any) {
      pushLog(err.message, "error");
    } finally {
      setBusy(null);
    }
  }

  async function handleReloadTeams() {
    setBusy("reload-teams");
    try {
      await api.reloadTeamsFromConfiguredJson();
      pushLog("Teams reloaded from the configured JSON file.", "success");
      await refreshLookups();
    } catch (err: any) {
      pushLog(err.message, "error");
    } finally {
      setBusy(null);
    }
  }

  return (
    <div>
      <div className="header">
        <h1>Match Control</h1>
      </div>

      <div className="grid" style={{ gridTemplateColumns: "1fr 1fr", gap: 20 }}>
        <div className="panel">
          <h2>Select Match</h2>
          <div className="form-row">
            <label>Tournament</label>
            <select value={selector.tournament} onChange={(e) => setSelector((s) => ({ ...s, tournament: e.target.value }))}>
              {tournaments.length === 0 && <option value="">No tournaments yet</option>}
              {tournaments.map((t) => <option key={t} value={t}>{t}</option>)}
            </select>
          </div>
          <div className="form-row">
            <label>Stage</label>
            <select value={selector.stage} onChange={(e) => setSelector((s) => ({ ...s, stage: e.target.value }))}>
              {stages.length === 0 && <option value="">No stages yet</option>}
              {stages.map((s) => <option key={s} value={s}>{s}</option>)}
            </select>
          </div>
          <div className="form-row">
            <label>Day</label>
            <select value={selector.day} onChange={(e) => setSelector((s) => ({ ...s, day: e.target.value }))}>
              {DAYS.map((d) => <option key={d} value={d}>{d}</option>)}
            </select>
          </div>
          <div className="form-row">
            <label>Match</label>
            <select value={selector.match} onChange={(e) => setSelector((s) => ({ ...s, match: e.target.value }))}>
              {MATCHES.map((m) => <option key={m} value={m}>{m}</option>)}
            </select>
          </div>

          <div className="actions" style={{ marginTop: 12 }}>
            <button onClick={() => handleStart(false)} disabled={busy === "start"}>{busy === "start" ? "Starting…" : "Start Match"}</button>
            <button className="secondary" onClick={handleStop} disabled={busy === "stop"}>{busy === "stop" ? "Stopping…" : "Stop Match"}</button>
          </div>

          {pendingConfirm && (
            <div className="confirm-box">
              <p>{pendingConfirm.message}</p>
              {pendingConfirm.requiresTypedDelete ? (
                <>
                  <input placeholder='Type "DELETE" to confirm' value={typedDelete} onChange={(e) => setTypedDelete(e.target.value)} />
                  <div className="actions">
                    <button onClick={() => handleStart(true, typedDelete)} disabled={busy === "start"}>Confirm &amp; Restart</button>
                    <button className="secondary" onClick={() => { setPendingConfirm(null); setTypedDelete(""); }}>Cancel</button>
                  </div>
                </>
              ) : (
                <div className="actions">
                  <button onClick={() => handleStart(true)} disabled={busy === "start"}>Yes, Continue</button>
                  <button className="secondary" onClick={() => setPendingConfirm(null)}>Cancel</button>
                </div>
              )}
            </div>
          )}

          <h2 style={{ marginTop: 24 }}>Add Tournament / Stage</h2>
          <div className="form-row">
            <label>New tournament</label>
            <div style={{ display: "flex", gap: 6 }}>
              <input value={newTournamentName} onChange={(e) => setNewTournamentName(e.target.value)} placeholder="Tournament name" />
              <button onClick={handleAddTournament} disabled={busy === "add-tournament"}>Add</button>
            </div>
          </div>
          <div className="form-row">
            <label>New stage (in selected tournament)</label>
            <div style={{ display: "flex", gap: 6 }}>
              <input value={newStageName} onChange={(e) => setNewStageName(e.target.value)} placeholder="Stage name" />
              <button onClick={handleAddStage} disabled={busy === "add-stage"}>Add</button>
            </div>
          </div>
          <button className="secondary" onClick={handleReloadTeams} disabled={busy === "reload-teams"} style={{ marginTop: 8 }}>
            {busy === "reload-teams" ? "Reloading…" : "Reload Teams from JSON file"}
          </button>
        </div>

        <div className="panel">
          <h2>Post-Match Reports</h2>
          <div className="actions" style={{ flexWrap: "wrap", marginBottom: 12 }}>
            {POST_MATCH_STEPS.map((step) => (
              <button key={step.id} className="secondary" onClick={() => handleRunStep(step.id, step.label)} disabled={busy === `step:${step.id}`}>
                {busy === `step:${step.id}` ? "…" : step.label}
              </button>
            ))}
          </div>
          <button onClick={handleRunAll} disabled={busy === "run-all"}>{busy === "run-all" ? "Running all…" : "Run All Reports"}</button>

          <h2 style={{ marginTop: 24 }}>Pre-Match: Map Top Performers</h2>
          <div className="form-row">
            <label>Map</label>
            <select value={mapName} onChange={(e) => setMapName(e.target.value)}>
              {MAPS.map((m) => <option key={m} value={m}>{m}</option>)}
            </select>
          </div>
          <button className="secondary" onClick={handleMapPerformers} disabled={busy === "map-performers"}>
            {busy === "map-performers" ? "Generating…" : "Generate"}
          </button>

          <h2 style={{ marginTop: 24 }}>Activity</h2>
          <div className="log-list">
            {log.length === 0 && <div className="empty-state">Nothing yet.</div>}
            {log.map((entry, i) => (
              <div key={i} className={`log-entry log-${entry.kind}`}>{entry.text}</div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
