import { useCallback, useEffect, useRef, useState } from "react";
import { api } from "../lib/api";
import type { LogEntry, TournamentView } from "../lib/api";

// The half of observability that actually gets looked at.
//
// The system already wrote log files; nobody read them during an event, which is how an embedded
// Kestrel host that failed to start went unnoticed until much later. During a broadcast the
// operator is in vMix and a browser, so the log has to be in the browser.
//
// It tails rather than re-fetches: each poll passes the last sequence number it saw, so a long
// session doesn't re-download the whole ring every two seconds.

const LEVELS = ["Trace", "Debug", "Information", "Warning", "Error", "Critical"] as const;

const LEVEL_COLOR: Record<string, string> = {
  Trace: "#64748b",
  Debug: "#64748b",
  Information: "#38bdf8",
  Warning: "#f59e0b",
  Error: "#ef4444",
  Critical: "#f472b6",
};

const MAX_ROWS = 600;

export default function LogsTab() {
  const [entries, setEntries] = useState<LogEntry[]>([]);
  const [tournaments, setTournaments] = useState<TournamentView[]>([]);
  const [tournamentId, setTournamentId] = useState<string>("");
  const [minLevel, setMinLevel] = useState<string>("Information");
  const [following, setFollowing] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<number | null>(null);

  const cursorRef = useRef(0);
  const bottomRef = useRef<HTMLDivElement | null>(null);

  // Changing a filter changes what the cursor means, so the tail restarts from scratch.
  const resetTail = useCallback(() => {
    cursorRef.current = 0;
    setEntries([]);
    setExpanded(null);
  }, []);

  useEffect(() => {
    api
      .listTournaments()
      .then(setTournaments)
      .catch(() => {
        // A failure here only costs the filter dropdown; the log itself still works.
      });
  }, []);

  useEffect(() => {
    resetTail();
  }, [tournamentId, minLevel, resetTail]);

  useEffect(() => {
    // Paused means paused: no fetch at all, not "one more batch and then stop". Without this
    // early return, clicking Pause re-runs the effect and pulls one further page, which reads to
    // the operator as the button not working.
    if (!following) return;

    let cancelled = false;

    async function poll() {
      try {
        const page = await api.getLogs({
          tournamentId: tournamentId || undefined,
          minLevel,
          afterSeq: cursorRef.current,
          max: 200,
        });
        if (cancelled) return;

        if (page.records.length > 0) {
          cursorRef.current = page.nextSeq;
          setEntries((prev) => {
            const merged = [...prev, ...page.records];
            // Bounded in the UI as well as on the server, so a long event can't grow this tab
            // until the browser tab itself becomes the performance problem.
            return merged.length > MAX_ROWS ? merged.slice(merged.length - MAX_ROWS) : merged;
          });
        }
        setError(null);
      } catch (err) {
        if (!cancelled) setError(err instanceof Error ? err.message : "Could not read logs.");
      }
    }

    poll();
    const timer = window.setInterval(poll, 2000);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [tournamentId, minLevel, following]);

  useEffect(() => {
    if (following) bottomRef.current?.scrollIntoView({ block: "end" });
  }, [entries, following]);

  return (
    <div style={{ display: "grid", gap: 12 }}>
      <div className="panel" style={{ display: "flex", gap: 10, flexWrap: "wrap", alignItems: "center" }}>
        <label style={{ fontSize: 12, opacity: 0.7 }}>Tournament</label>
        <select value={tournamentId} onChange={(e) => setTournamentId(e.target.value)}>
          <option value="">All (process-wide)</option>
          {tournaments.map((t) => (
            <option key={t.id} value={t.id}>
              {t.name}
            </option>
          ))}
        </select>

        <label style={{ fontSize: 12, opacity: 0.7 }}>Minimum level</label>
        <select value={minLevel} onChange={(e) => setMinLevel(e.target.value)}>
          {LEVELS.map((level) => (
            <option key={level} value={level}>
              {level}
            </option>
          ))}
        </select>

        <button className="secondary" onClick={() => setFollowing((f) => !f)}>
          {following ? "Pause" : "Resume"}
        </button>
        <button className="secondary" onClick={resetTail}>
          Clear view
        </button>

        <span style={{ fontSize: 12, opacity: 0.55, marginLeft: "auto" }}>
          {entries.length} line{entries.length === 1 ? "" : "s"}
          {following ? " · following" : " · paused"}
        </span>
      </div>

      {error && (
        <div className="panel" style={{ borderColor: "#ef4444", color: "#fca5a5" }}>
          {error}
        </div>
      )}

      <div className="panel" style={{ padding: 0, maxHeight: "62vh", overflowY: "auto" }}>
        {entries.length === 0 ? (
          <div style={{ padding: 16, opacity: 0.6, fontSize: 13 }}>
            Nothing at this level yet.
          </div>
        ) : (
          <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 12 }}>
            <tbody>
              {entries.map((entry) => {
                const color = LEVEL_COLOR[entry.level] ?? "#94a3b8";
                const hasDetail = Boolean(entry.error) || Boolean(entry.props);
                const isOpen = expanded === entry.seq;

                return (
                  <tr
                    key={entry.seq}
                    style={{
                      borderTop: "1px solid rgba(255,255,255,0.06)",
                      cursor: hasDetail ? "pointer" : "default",
                    }}
                    onClick={() => hasDetail && setExpanded(isOpen ? null : entry.seq)}
                  >
                    <td style={{ padding: "5px 8px", whiteSpace: "nowrap", opacity: 0.5, fontFamily: "monospace" }}>
                      {new Date(entry.ts).toLocaleTimeString()}
                    </td>
                    <td style={{ padding: "5px 8px", whiteSpace: "nowrap", color, fontWeight: 600 }}>
                      {entry.level}
                    </td>
                    <td style={{ padding: "5px 8px", whiteSpace: "nowrap", opacity: 0.7, fontFamily: "monospace" }}>
                      {entry.category}
                    </td>
                    <td style={{ padding: "5px 8px", whiteSpace: "nowrap", opacity: 0.6 }}>
                      {entry.tournament ?? "—"}
                    </td>
                    <td style={{ padding: "5px 8px", width: "100%" }}>
                      <div>{entry.message}</div>
                      {isOpen && (
                        <div
                          style={{
                            marginTop: 6,
                            padding: 8,
                            background: "rgba(0,0,0,0.3)",
                            borderRadius: 6,
                            whiteSpace: "pre-wrap",
                            fontFamily: "monospace",
                            fontSize: 11,
                            lineHeight: 1.5,
                          }}
                        >
                          {entry.props &&
                            Object.entries(entry.props).map(([key, value]) => (
                              <div key={key}>
                                <span style={{ opacity: 0.55 }}>{key}</span>: {value}
                              </div>
                            ))}
                          {entry.error && <div style={{ color: "#fca5a5" }}>{entry.error}</div>}
                        </div>
                      )}
                      {hasDetail && !isOpen && (
                        <span style={{ fontSize: 11, opacity: 0.4 }}>click for detail</span>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}
        <div ref={bottomRef} />
      </div>
    </div>
  );
}
