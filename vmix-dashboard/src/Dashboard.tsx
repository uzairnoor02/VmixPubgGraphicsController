import { useEffect, useMemo, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import type { TeamLiveStats } from "./types";
import { clearAuthed } from "./Login";

// Where the WinForms app's embedded dashboard host (LiveDashboardHost.cs) is listening.
// Change this if the graphics PC's hostname/IP or port differs - or wire it up to a small
// on-screen settings field later so it doesn't need a rebuild to change.
const API_BASE = (import.meta as any).env?.VITE_API_BASE ?? "http://localhost:5050";

export default function Dashboard() {
  const [teams, setTeams] = useState<TeamLiveStats[]>([]);
  const [status, setStatus] = useState<string>("");
  const [connected, setConnected] = useState(false);
  const [resetting, setResetting] = useState(false);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    // Snapshot first, so a browser that connects mid-match sees current state immediately
    // instead of waiting for the next live tick.
    fetch(`${API_BASE}/api/match/teams`)
      .then((r) => r.json())
      .then(setTeams)
      .catch(() => {});
    fetch(`${API_BASE}/api/match/status`)
      .then((r) => r.json())
      .then((d) => setStatus(d.status ?? ""))
      .catch(() => {});

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/match`)
      .withAutomaticReconnect()
      .build();

    connection.on("TeamsUpdated", (updated: TeamLiveStats[]) => setTeams(updated));
    connection.on("StatusChanged", (updatedStatus: string) => setStatus(updatedStatus));
    connection.onreconnected(() => setConnected(true));
    connection.onreconnecting(() => setConnected(false));
    connection.onclose(() => setConnected(false));

    connection
      .start()
      .then(() => setConnected(true))
      .catch(() => setConnected(false));

    connectionRef.current = connection;
    return () => {
      connection.stop();
    };
  }, []);

  const sortedTeams = useMemo(
    () => [...teams].sort((a, b) => a.teamRank - b.teamRank),
    [teams]
  );

  async function handleReset() {
    setResetting(true);
    try {
      await fetch(`${API_BASE}/api/match/reset`, { method: "POST" });
    } finally {
      setResetting(false);
    }
  }

  return (
    <div>
      <div className="header">
        <h1>Live Match — {teams.length} Teams</h1>
        <div className="actions" style={{ display: "flex", gap: 12, alignItems: "center" }}>
          <span className="status-pill">
            <span className={`status-dot ${connected ? "connected" : "disconnected"}`} />
            {connected ? "Live" : "Disconnected"}
            {status ? ` · ${status}` : ""}
          </span>
          <button onClick={handleReset} disabled={resetting}>
            {resetting ? "Resetting…" : "Reset Overlay"}
          </button>
          <button onClick={() => { clearAuthed(); window.location.reload(); }}>
            Log out
          </button>
        </div>
      </div>

      {sortedTeams.length === 0 ? (
        <div className="empty-state">
          No live data yet — start a match from the WinForms app on the graphics PC.
        </div>
      ) : (
        <div className="grid">
          {sortedTeams.map((team) => (
            <div key={team.tag + team.teamRank} className={`team-card ${team.teamEliminated ? "eliminated" : ""}`}>
              <div className="rank">#{team.teamRank}</div>
              <div className="tag">{team.tag}</div>
              <div className="stats">
                <span>{team.totalPoints} pts</span>
                <span>{team.eliminations} elims</span>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
