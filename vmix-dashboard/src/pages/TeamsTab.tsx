import { useEffect, useRef, useState } from "react";
import { api } from "../lib/api";
import type { TournamentRoster } from "../lib/api";

const SAMPLE_JSON = `{
  "tournament_name": "2025 PMCB",
  "stages": [
    {
      "stage_name": "Semi Finals A",
      "teams": [
        { "team_id": "5", "team_name": "MAG" },
        { "team_id": "6", "team_name": "AE" }
      ]
    }
  ]
}`;

export default function TeamsTab() {
  const [roster, setRoster] = useState<TournamentRoster[]>([]);
  const [loadingRoster, setLoadingRoster] = useState(true);
  const [jsonText, setJsonText] = useState("");
  const [importing, setImporting] = useState(false);
  const [message, setMessage] = useState<{ kind: "ok" | "error"; text: string } | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  async function refresh() {
    setLoadingRoster(true);
    try {
      setRoster(await api.getTeams());
    } catch {
      // Leave the previous roster on screen rather than blanking it on a transient failure.
    } finally {
      setLoadingRoster(false);
    }
  }

  useEffect(() => {
    refresh();
  }, []);

  async function handleImport(raw: string) {
    if (!raw.trim()) {
      setMessage({ kind: "error", text: "Paste or choose a JSON file first." });
      return;
    }
    setImporting(true);
    setMessage(null);
    try {
      const result = await api.loadTeams(raw);
      if (!result.ok) {
        setMessage({ kind: "error", text: result.error ?? "Import failed." });
      } else {
        setMessage({ kind: "ok", text: `${result.tournament}: ${result.teamsAdded ?? 0} added, ${result.teamsUpdated ?? 0} updated.` });
        setJsonText("");
        await refresh();
      }
    } catch (err) {
      setMessage({ kind: "error", text: err instanceof Error ? err.message : "Import failed." });
    } finally {
      setImporting(false);
    }
  }

  async function handleFile(file: File) {
    const text = await file.text();
    setJsonText(text);
    await handleImport(text);
  }

  return (
    <div>
      <div className="header">
        <h1>Teams</h1>
      </div>

      <div className="panel">
        <h2>Load teams</h2>
        <p className="panel-hint">
          Paste the same tournament/stage/team JSON the WinForms "Load Teams" button reads, or choose the file directly.
          Existing teams are matched by tournament + stage + team id and updated in place — nothing is duplicated on a re-import.
        </p>
        <textarea
          className="json-input"
          placeholder={SAMPLE_JSON}
          value={jsonText}
          onChange={(e) => setJsonText(e.target.value)}
          rows={10}
        />
        <div className="actions">
          <button onClick={() => handleImport(jsonText)} disabled={importing}>
            {importing ? "Importing…" : "Load Teams"}
          </button>
          <button className="secondary" onClick={() => fileInputRef.current?.click()} disabled={importing}>
            Choose JSON file…
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept="application/json,.json"
            style={{ display: "none" }}
            onChange={(e) => e.target.files?.[0] && handleFile(e.target.files[0])}
          />
        </div>
        {message && <div className={message.kind === "ok" ? "message-ok" : "message-error"}>{message.text}</div>}
      </div>

      <div className="panel">
        <div className="panel-header-row">
          <h2>Current roster</h2>
          <button className="secondary" onClick={refresh} disabled={loadingRoster}>
            {loadingRoster ? "Refreshing…" : "Refresh"}
          </button>
        </div>

        {roster.length === 0 ? (
          <div className="empty-state">No tournaments yet — load teams above to get started.</div>
        ) : (
          roster.map((tournament) => (
            <div key={tournament.tournamentId} className="roster-tournament">
              <h3>{tournament.name}</h3>
              {tournament.stages.map((stage) => (
                <div key={stage.stageId} className="roster-stage">
                  <div className="roster-stage-name">{stage.name} · {stage.teams.length} teams</div>
                  <div className="roster-team-chips">
                    {stage.teams.map((team) => (
                      <span key={team.id} className="roster-team-chip">
                        {team.teamName} <span className="roster-team-id">#{team.teamId}</span>
                      </span>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          ))
        )}
      </div>
    </div>
  );
}
