// Shared API client for the whole dashboard - every page/tab and the /overlay route go through
// this instead of hand-rolling fetch calls, so the base URL, error handling, and response shapes
// live in exactly one place.

import { clearAuthed, getAuthKey } from "../Login";

export const API_BASE = (import.meta as any).env?.VITE_API_BASE ?? "http://localhost:5050";

export interface OverlayConfig {
  chromaKeyColor: string;
  elementVisibility: Record<string, boolean>;
  elementSettings: Record<string, unknown>;
}

export interface GraphicsFile {
  name: string;
  url: string;
  sizeBytes: number;
  uploadedAtUtc: string;
}

export interface TeamRosterEntry {
  id: number;
  teamId: string;
  teamName: string;
}

export interface StageRoster {
  stageId: number;
  name: string;
  teams: TeamRosterEntry[];
}

export interface TournamentRoster {
  tournamentId: number;
  name: string;
  stages: StageRoster[];
}

export interface LoadTeamsResult {
  ok: boolean;
  tournament?: string;
  teamsAdded?: number;
  teamsUpdated?: number;
  stages?: { stage: string; teamsAdded: number; teamsUpdated: number }[];
  error?: string;
}

// Match Control - mirrors Pubg Ranking System/MatchControlApi.cs (the REST replacement for every
// Form1 button click).
export interface MatchSelector { tournament: string; stage: string; day: string; match: string }

export interface StartMatchResult {
  ok: boolean;
  statusCode: number;
  message: string;
  requiresConfirmation: boolean;
  requiresTypedDelete: boolean;
  matchId: number | null;
}

export const POST_MATCH_STEPS = [
  { id: "teams-to-watch", label: "Teams to Watch" },
  { id: "match-rankings", label: "Match Rankings" },
  { id: "overall-rankings", label: "Overall Rankings" },
  { id: "match-mvp", label: "Match MVP" },
  { id: "wwcd", label: "WWCD Stats" },
  { id: "match-summary", label: "Match Summary" },
  { id: "day-summary", label: "Day Summary" },
  { id: "top5-match-mvp", label: "Top 5 Match MVP" },
  { id: "top5-stage-mvp", label: "Top 5 Stage MVP" },
  { id: "stage-mvp", label: "Stage MVP" },
  { id: "top-grenadiers", label: "Top Grenadiers" },
] as const;

// ---------------------------------------------------------------- Tenancy
// Mirrors Pubg Ranking System/TenancyApi.cs. A tournament is the tenant boundary: its overlay
// token is what vMix points at, its agent key is what the ingest agent authenticates with, and
// neither is the tournament id (which appears in logs and URLs and is therefore not a secret).

export interface IssuedKeyView {
  id: string;
  kind: string;
  mask: string;
  label: string | null;
  current: boolean;
  createdAtUtc: string;
  lastUsedAtUtc: string | null;
  retiredAtUtc: string | null;
  graceSeconds: number;
  usableUntilUtc: string | null;
}

export interface AgentView {
  connected: boolean;
  summary: string;
  version: string | null;
  secondsSinceLastTick: number | null;
  ticksAccepted: number;
  ticksDropped: number;
  versionSupported: boolean;
}

export interface TournamentView {
  id: string;
  name: string;
  orgId: string | null;
  enabled: boolean;
  isDefault: boolean;
  createdAtUtc: string;
  scheduledStartUtc: string | null;
  scheduledEndUtc: string | null;
  overlayUrl: string | null;
  overlayToken: string | null;
  agent: AgentView | null;
  scopeLoaded: boolean;
  keys: IssuedKeyView[];
}

export interface WhoAmI {
  kind: string;
  tournamentId: string | null;
  tournamentName: string | null;
  canAdministerAll: boolean;
  correlationId: string;
}

export interface CreateTournamentResult {
  ok: boolean;
  tournament: TournamentView;
  agentKeyOnce: string | null;
  note: string;
}

export interface IssuedKeyResult {
  ok: boolean;
  kind?: string;
  key: string;
  mask: string;
  graceMinutes?: number;
  note: string;
}

export interface TournamentPatch {
  name?: string;
  enabled?: boolean;
  scheduledStartUtc?: string | null;
  scheduledEndUtc?: string | null;
}

// ---------------------------------------------------------------- Observability
// Mirrors Pubg Ranking System/ObservabilityApi.cs.

export interface LogEntry {
  seq: number;
  ts: string;
  level: string;
  category: string;
  message: string;
  tournamentId: string | null;
  tournament: string | null;
  error: string | null;
  props: Record<string, string> | null;
}

export interface LogPage {
  // Pass this back as afterSeq to tail the log instead of re-reading the whole ring.
  nextSeq: number;
  scope: string;
  records: LogEntry[];
}

export interface LogQuery {
  tournamentId?: string;
  minLevel?: string;
  afterSeq?: number;
  max?: number;
}

export interface AgentStatusView extends AgentView {
  tournamentId: string;
  tournament: string;
  sessionId: string | null;
  lastContactUtc: string | null;
  secondsSinceLastContact: number | null;
  averageTickIntervalSeconds: number | null;
  lastObservedLatencySeconds: number | null;
  minimumVersion: string | null;
  ingest: {
    matchActive: boolean;
    matchId: number | null;
    wasInGame: boolean;
    sessionId: string | null;
    acceptedSeq: number;
    publishedSeq: number;
    staleTicksDropped: number;
    supersededTicksDropped: number;
  };
}

async function req<T>(path: string, init?: RequestInit): Promise<T> {
  const isForm = init?.body instanceof FormData;
  // Every admin-action endpoint requires this as a real Authorization header now (see
  // Pubg Ranking System/DashboardAuth.cs); public ones (GET /api/overlay/config, /api/match/teams,
  // /api/match/status - what /overlay itself reads) ignore it, so it's safe to always attach it
  // when we have one, including from the unauthenticated /overlay route where there won't be one.
  const authKey = getAuthKey();
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      ...(init?.body && !isForm ? { "Content-Type": "application/json" } : {}),
      ...(authKey ? { Authorization: `Bearer ${authKey}` } : {}),
      ...(init?.headers ?? {}),
    },
  });

  if (res.status === 401) {
    // The stored key is missing/wrong/rotated server-side - forget it and send the operator back
    // to the login screen instead of leaving them looking at a dashboard that silently fails
    // every action from here on.
    clearAuthed();
    window.location.reload();
    throw new Error("Session expired - please log in again.");
  }

  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new Error(body?.error || `Request failed (${res.status})`);
  }

  const contentType = res.headers.get("content-type") || "";
  if (!contentType.includes("application/json")) {
    return undefined as unknown as T;
  }
  return res.json();
}

export const api = {
  // Overlay Settings
  // Optional headers so the on-air overlay can identify its tournament with X-Overlay-Token
  // (see Overlay.tsx); the dashboard calls it with none and gets this install's own config.
  getOverlayConfig: (headers?: Record<string, string>) =>
    req<OverlayConfig>("/api/overlay/config", headers ? { headers } : undefined),
  saveOverlayConfig: (config: OverlayConfig) =>
    req<{ ok: boolean }>("/api/overlay/config", { method: "POST", body: JSON.stringify(config) }),

  // Teams
  getTeams: () => req<TournamentRoster[]>("/api/teams"),
  loadTeams: (rawJson: string) =>
    req<LoadTeamsResult>("/api/teams/load", { method: "POST", body: rawJson }),

  // Custom Graphics
  getGraphics: () => req<GraphicsFile[]>("/api/graphics"),
  uploadGraphic: async (file: File): Promise<{ ok: boolean; name: string; url: string }> => {
    const form = new FormData();
    form.append("file", file);
    return req("/api/graphics/upload", { method: "POST", body: form });
  },
  deleteGraphic: (name: string) =>
    req<{ ok: boolean }>(`/api/graphics/${encodeURIComponent(name)}`, { method: "DELETE" }),

  // Match
  resetMatch: () => req<{ ok: boolean }>("/api/match/reset", { method: "POST" }),

  // Match Control (replaces every Form1 button - see MatchControlApi.cs)
  getTournamentNames: () => req<string[]>("/api/tournaments"),
  getStageNames: () => req<string[]>("/api/stages"),
  addTournament: (name: string) =>
    req<{ ok: boolean; message?: string; error?: string }>("/api/tournaments", { method: "POST", body: JSON.stringify({ name }) }),
  addStage: (tournamentName: string, stageName: string) =>
    req<{ ok: boolean; message?: string; error?: string }>("/api/tournaments/stages", { method: "POST", body: JSON.stringify({ tournamentName, stageName }) }),
  startMatch: (selector: MatchSelector, confirm = false, typedConfirmation?: string, mode: "direct" | "agent" = "direct") =>
    req<StartMatchResult>("/api/match/start", { method: "POST", body: JSON.stringify({ ...selector, confirm, typedConfirmation, mode }) }),
  getIngestStatus: () => req<{ active: boolean; matchId: number | null; wasInGame: boolean }>("/api/ingest/status"),
  stopMatch: () => req<{ ok: boolean }>("/api/match/stop", { method: "POST" }),
  runPostMatchStep: (step: string, selector: MatchSelector) =>
    req<{ ok: boolean; error?: string }>(`/api/postmatch/run/${step}`, { method: "POST", body: JSON.stringify(selector) }),
  runAllPostMatch: (selector: MatchSelector) =>
    req<{ ok: boolean; partial?: boolean; errors?: string[] }>("/api/postmatch/run-all", { method: "POST", body: JSON.stringify(selector) }),
  mapTopPerformers: (selector: MatchSelector, mapName: string) =>
    req<{ ok: boolean; error?: string }>(`/api/prematch/map-top-performers?mapName=${encodeURIComponent(mapName)}`, { method: "POST", body: JSON.stringify(selector) }),
  // Tenancy - tournaments and their generated credentials
  whoami: () => req<WhoAmI>("/api/tenancy/whoami"),
  listTournaments: () => req<TournamentView[]>("/api/tenancy/tournaments"),
  createTournament: (name: string, orgId?: string) =>
    req<CreateTournamentResult>("/api/tenancy/tournaments", {
      method: "POST",
      body: JSON.stringify({ name, orgId }),
    }),
  updateTournament: (id: string, patch: TournamentPatch) =>
    req<{ ok: boolean; tournament: TournamentView }>(
      `/api/tenancy/tournaments/${encodeURIComponent(id)}`,
      { method: "POST", body: JSON.stringify(patch) }
    ),
  deleteTournament: (id: string) =>
    req<{ ok: boolean }>(`/api/tenancy/tournaments/${encodeURIComponent(id)}`, { method: "DELETE" }),
  // graceMinutes defaults server-side to a non-zero window so rotating mid-event doesn't black
  // out the graphics; pass 0 explicitly for an immediate cut-off.
  rotateKey: (id: string, kind: string, graceMinutes?: number) =>
    req<IssuedKeyResult>(
      `/api/tenancy/tournaments/${encodeURIComponent(id)}/keys/${encodeURIComponent(kind)}/rotate`,
      { method: "POST", body: JSON.stringify({ graceMinutes }) }
    ),
  issueOperatorKey: (id: string) =>
    req<IssuedKeyResult>(`/api/tenancy/tournaments/${encodeURIComponent(id)}/keys/dashboard`, {
      method: "POST",
      body: JSON.stringify({}),
    }),
  revokeKey: (id: string, keyId: string) =>
    req<{ ok: boolean }>(
      `/api/tenancy/tournaments/${encodeURIComponent(id)}/keys/${encodeURIComponent(keyId)}/revoke`,
      { method: "POST" }
    ),

  // Observability
  getLogs: (query: LogQuery = {}) => {
    const params = new URLSearchParams();
    if (query.tournamentId) params.set("tournamentId", query.tournamentId);
    if (query.minLevel) params.set("minLevel", query.minLevel);
    if (query.afterSeq) params.set("afterSeq", String(query.afterSeq));
    if (query.max) params.set("max", String(query.max));
    const qs = params.toString();
    return req<LogPage>(`/api/observability/logs${qs ? `?${qs}` : ""}`);
  },
  getAgentStatus: () => req<AgentStatusView[]>("/api/observability/agents"),
  reloadTeamsFromConfiguredJson: () => req<{ ok: boolean }>("/api/teams/reload", { method: "POST" }),
};
