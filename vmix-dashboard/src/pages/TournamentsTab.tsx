import { useEffect, useState } from "react";
import { api } from "../lib/api";
import type { IssuedKeyView, TournamentView, WhoAmI } from "../lib/api";

// The tab that replaces hand-edited appsettings.json secrets. Two things it exists to make
// obvious, because both were previously invisible:
//
//  * the exact overlay URL to paste into vMix, per tournament, with a copy button - previously a
//    single hardcoded http://localhost:5050/overlay for the whole install;
//  * whether that tournament's ingest agent is actually alive right now. A silent agent and a
//    quiet match look identical on air, so the dashboard has to say which one it is.
//
// Newly issued agent and operator keys are shown once, in a banner that stays until dismissed,
// because the server keeps only their hash and cannot show them again.

type Reveal = { kind: string; key: string; note: string };

const LEVEL_TINT: Record<string, string> = {
  overlay: "#38bdf8",
  agent: "#f59e0b",
  dashboard: "#a78bfa",
};

export default function TournamentsTab() {
  const [tournaments, setTournaments] = useState<TournamentView[]>([]);
  const [who, setWho] = useState<WhoAmI | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<string | null>(null);
  const [newName, setNewName] = useState("");
  const [reveal, setReveal] = useState<Reveal | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    try {
      const [list, identity] = await Promise.all([api.listTournaments(), api.whoami()]);
      setTournaments(list);
      setWho(identity);
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not load tournaments.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    refresh();
    // Agent liveness goes stale within seconds, so this polls rather than waiting for a click.
    const timer = window.setInterval(refresh, 5000);
    return () => window.clearInterval(timer);
  }, []);

  async function guard(label: string, action: () => Promise<void>) {
    setBusy(label);
    setError(null);
    try {
      await action();
      await refresh();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Action failed.");
    } finally {
      setBusy(null);
    }
  }

  const canCreate = who?.canAdministerAll === true;

  return (
    <div style={{ display: "grid", gap: 16 }}>
      {error && (
        <div className="panel" style={{ borderColor: "#ef4444", color: "#fca5a5" }}>
          {error}
        </div>
      )}

      {reveal && (
        <div className="panel" style={{ borderColor: "#f59e0b" }}>
          <div style={{ fontWeight: 600, marginBottom: 6 }}>
            New {reveal.kind} key — shown once
          </div>
          <div style={{ fontSize: 12, opacity: 0.75, marginBottom: 10 }}>{reveal.note}</div>
          <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
            <code
              style={{
                background: "rgba(0,0,0,0.35)",
                padding: "8px 10px",
                borderRadius: 6,
                fontSize: 13,
                wordBreak: "break-all",
              }}
            >
              {reveal.key}
            </code>
            <button onClick={() => navigator.clipboard?.writeText(reveal.key)}>Copy</button>
            <button className="secondary" onClick={() => setReveal(null)}>
              I've saved it
            </button>
          </div>
        </div>
      )}

      {canCreate && (
        <div className="panel">
          <div style={{ fontWeight: 600, marginBottom: 8 }}>New tournament</div>
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            <input
              value={newName}
              placeholder="e.g. PMGO Pakistan — Grand Finals"
              onChange={(e) => setNewName(e.target.value)}
              style={{ flex: "1 1 260px", minWidth: 0 }}
            />
            <button
              disabled={!newName.trim() || busy === "create"}
              onClick={() =>
                guard("create", async () => {
                  const result = await api.createTournament(newName.trim());
                  setNewName("");
                  if (result.agentKeyOnce) {
                    setReveal({ kind: "agent", key: result.agentKeyOnce, note: result.note });
                  }
                })
              }
            >
              {busy === "create" ? "Creating…" : "Create"}
            </button>
          </div>
          <div style={{ fontSize: 12, opacity: 0.65, marginTop: 8 }}>
            An overlay token and an agent key are generated automatically. The agent key is shown
            once.
          </div>
        </div>
      )}

      {loading && <div className="panel">Loading tournaments…</div>}

      {!loading && tournaments.length === 0 && (
        <div className="panel">No tournaments yet.</div>
      )}

      {tournaments.map((t) => (
        <div key={t.id} className="panel" style={{ display: "grid", gap: 12 }}>
          <div style={{ display: "flex", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }}>
            <div>
              <div style={{ fontWeight: 600, fontSize: 16 }}>
                {t.name}
                {t.isDefault && (
                  <span style={{ fontSize: 11, opacity: 0.6, marginLeft: 8 }}>(this install)</span>
                )}
              </div>
              <div style={{ fontSize: 12, opacity: 0.55, fontFamily: "monospace" }}>{t.id}</div>
            </div>
            <div style={{ display: "flex", gap: 8, alignItems: "center" }}>
              <AgentPill tournament={t} />
              <button
                className="secondary"
                disabled={busy === `toggle:${t.id}`}
                onClick={() =>
                  guard(`toggle:${t.id}`, () =>
                    api.updateTournament(t.id, { enabled: !t.enabled }).then(() => undefined)
                  )
                }
              >
                {t.enabled ? "Disable" : "Enable"}
              </button>
            </div>
          </div>

          <div>
            <div style={{ fontSize: 12, opacity: 0.7, marginBottom: 4 }}>
              vMix Browser Source URL
            </div>
            <div style={{ display: "flex", gap: 8, alignItems: "center", flexWrap: "wrap" }}>
              <code
                style={{
                  background: "rgba(0,0,0,0.3)",
                  padding: "6px 9px",
                  borderRadius: 6,
                  fontSize: 12,
                  wordBreak: "break-all",
                  flex: "1 1 320px",
                  minWidth: 0,
                }}
              >
                {t.overlayUrl ?? "no overlay token"}
              </code>
              <button
                className="secondary"
                disabled={!t.overlayUrl}
                onClick={() => t.overlayUrl && navigator.clipboard?.writeText(t.overlayUrl)}
              >
                Copy
              </button>
            </div>
          </div>

          <KeyTable
            tournament={t}
            busy={busy}
            onRotate={(kind) =>
              guard(`rotate:${t.id}:${kind}`, async () => {
                const result = await api.rotateKey(t.id, kind);
                // An overlay token is re-readable from the list, so only the write-capable
                // credentials need the one-time banner.
                if (kind !== "overlay") {
                  setReveal({ kind, key: result.key, note: result.note });
                }
              })
            }
            onIssueOperator={() =>
              guard(`operator:${t.id}`, async () => {
                const result = await api.issueOperatorKey(t.id);
                setReveal({ kind: "operator", key: result.key, note: result.note });
              })
            }
            onRevoke={(keyId) =>
              guard(`revoke:${t.id}:${keyId}`, () =>
                api.revokeKey(t.id, keyId).then(() => undefined)
              )
            }
            canIssueOperator={canCreate}
          />
        </div>
      ))}
    </div>
  );
}

function AgentPill({ tournament }: { tournament: TournamentView }) {
  const agent = tournament.agent;
  const connected = agent?.connected === true;
  const color = connected ? "#22c55e" : agent ? "#ef4444" : "#64748b";

  return (
    <span
      title={agent?.summary ?? "no agent information yet"}
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 6,
        fontSize: 12,
        padding: "4px 10px",
        borderRadius: 999,
        border: `1px solid ${color}`,
        color,
        whiteSpace: "nowrap",
      }}
    >
      <span style={{ width: 7, height: 7, borderRadius: 999, background: color }} />
      {agent?.summary ?? "agent unknown"}
    </span>
  );
}

function KeyTable({
  tournament,
  busy,
  onRotate,
  onIssueOperator,
  onRevoke,
  canIssueOperator,
}: {
  tournament: TournamentView;
  busy: string | null;
  onRotate: (kind: string) => void;
  onIssueOperator: () => void;
  onRevoke: (keyId: string) => void;
  canIssueOperator: boolean;
}) {
  const [showRetired, setShowRetired] = useState(false);

  const current = tournament.keys.filter((k) => k.current);
  // Keys still inside their grace window stay visible unconditionally: they are the ones a field
  // install may still be using, and hiding them is how you end up surprised when one expires.
  // Fully-retired keys are history and collapse away - rotation is encouraged here, so without
  // this the list grows past the point of being readable.
  const inGrace = tournament.keys.filter(
    (k) => !k.current && k.usableUntilUtc && new Date(k.usableUntilUtc) > new Date()
  );
  const retired = tournament.keys.filter(
    (k) => !k.current && !(k.usableUntilUtc && new Date(k.usableUntilUtc) > new Date())
  );
  const visible = [...current, ...inGrace, ...(showRetired ? retired : [])];

  return (
    <div>
      <div
        style={{
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center",
          marginBottom: 6,
          flexWrap: "wrap",
          gap: 8,
        }}
      >
        <div style={{ fontSize: 12, opacity: 0.7 }}>Keys</div>
        <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
          <button className="secondary" onClick={() => onRotate("overlay")}>
            Rotate overlay token
          </button>
          <button className="secondary" onClick={() => onRotate("agent")}>
            Rotate agent key
          </button>
          {canIssueOperator && (
            <button className="secondary" onClick={onIssueOperator}>
              Issue operator key
            </button>
          )}
        </div>
      </div>

      <div style={{ overflowX: "auto" }}>
        <table style={{ width: "100%", borderCollapse: "collapse", fontSize: 12 }}>
          <thead>
            <tr style={{ textAlign: "left", opacity: 0.6 }}>
              <th style={{ padding: "4px 6px" }}>Kind</th>
              <th style={{ padding: "4px 6px" }}>Key</th>
              <th style={{ padding: "4px 6px" }}>Last used</th>
              <th style={{ padding: "4px 6px" }}>State</th>
              <th style={{ padding: "4px 6px" }} />
            </tr>
          </thead>
          <tbody>
            {visible.map((k) => (
              <KeyRow
                key={k.id}
                entry={k}
                busy={busy === `revoke:${tournament.id}:${k.id}`}
                onRevoke={() => onRevoke(k.id)}
              />
            ))}
          </tbody>
        </table>
      </div>

      {retired.length > 0 && (
        <button
          className="secondary"
          style={{ marginTop: 8, fontSize: 11 }}
          onClick={() => setShowRetired((v) => !v)}
        >
          {showRetired ? "Hide" : "Show"} {retired.length} retired key
          {retired.length === 1 ? "" : "s"}
        </button>
      )}
    </div>
  );
}

function KeyRow({
  entry,
  busy,
  onRevoke,
}: {
  entry: IssuedKeyView;
  busy: boolean;
  onRevoke: () => void;
}) {
  const kind = entry.kind.toLowerCase();
  const tint = LEVEL_TINT[kind] ?? "#94a3b8";

  // A retired key inside its grace window is the state worth calling out: it still works, it is
  // about to stop working, and something in the field is probably still using it.
  const inGrace =
    !entry.current && entry.usableUntilUtc && new Date(entry.usableUntilUtc) > new Date();

  return (
    <tr style={{ borderTop: "1px solid rgba(255,255,255,0.07)", opacity: entry.current ? 1 : 0.6 }}>
      <td style={{ padding: "6px", color: tint, whiteSpace: "nowrap" }}>{entry.kind}</td>
      <td style={{ padding: "6px", fontFamily: "monospace" }}>{entry.mask}</td>
      <td style={{ padding: "6px", whiteSpace: "nowrap" }}>
        {entry.lastUsedAtUtc ? new Date(entry.lastUsedAtUtc).toLocaleString() : "never"}
      </td>
      <td style={{ padding: "6px", whiteSpace: "nowrap" }}>
        {entry.current ? (
          "current"
        ) : inGrace ? (
          <span style={{ color: "#f59e0b" }}>
            grace until {new Date(entry.usableUntilUtc!).toLocaleTimeString()}
          </span>
        ) : (
          "retired"
        )}
      </td>
      <td style={{ padding: "6px", textAlign: "right" }}>
        {(entry.current || inGrace) && (
          <button className="secondary" disabled={busy} onClick={onRevoke}>
            {busy ? "…" : "Revoke now"}
          </button>
        )}
      </td>
    </tr>
  );
}
