import { useEffect, useMemo, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { API_BASE, api } from "../lib/api";
import type { TeamLiveStats } from "../types";

export default function LiveTab() {
  const [teams, setTeams] = useState<TeamLiveStats[]>([]);
  const [status, setStatus] = useState<string>("");
  const [connected, setConnected] = useState(false);
  const [resetting, setResetting] = useState(false);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  useEffect(() => {
    fetch(`${API_BASE}/api/match/teams`).then((r) => r.json()).then(setTeams).catch(() => {});
    fetch(`${API_BASE}/api/match/status`).then((r) => r.json()).then((d) => setStatus(d.status ?? "")).catch(() => {});

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/match`)
      .withAutomaticReconnect()
      .build();

    connection.on("TeamsUpdated", (updated: TeamLiveStats[]) => setTeams(updated));
    connection.on("StatusChanged", (updatedStatus: string) => setStatus(updatedStatus));
    connection.onreconnected(() => setConnected(true));
    connection.onreconnecting(() => setConnected(false));
    connection.onclose(() => setConnected(false));

    connection.start().then(() => setConnected(true)).catch(() => setConnected(false));
    connectionRef.current = connection;
    return () => {
      connection.stop();
    };
  }, []);

  const sortedTeams = useMemo(() => [...teams].sort((a, b) => a.teamRank - b.teamRank), [teams]);

  async function handleReset() {
    setResetting(true);
    try {
      await api.resetMatch();
    } finally {
      setResetting(false);
    }
  }

  return (
    <div>
      <div className="header">
        <h1>Live Match — {teams.length} Teams</h1>
        <div className="actions">
          <span className="status-pill">
            <span className={`status-dot ${connected ? "connected" : "disconnected"}`} />
            {connected ? "Live" : "Disconnected"}
            {status ? ` · ${status}` : ""}
          </span>
          <button onClick={handleReset} disabled={resetting}>
            {resetting ? "Resetting…" : "Reset Overlay"}
          </button>
        </div>
      </div>

      {sortedTeams.length === 0 ? (
        <div className="empty-state">No live data yet — start a match from the Match Control tab.</div>
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
