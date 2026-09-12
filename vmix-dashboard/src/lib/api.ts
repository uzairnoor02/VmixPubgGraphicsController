// Shared API client for the whole dashboard - every page/tab and the /overlay route go through
// this instead of hand-rolling fetch calls, so the base URL, error handling, and response shapes
// live in exactly one place.

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

async function req<T>(path: string, init?: RequestInit): Promise<T> {
  const isForm = init?.body instanceof FormData;
  const res = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: {
      ...(init?.body && !isForm ? { "Content-Type": "application/json" } : {}),
      ...(init?.headers ?? {}),
    },
  });

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
  getOverlayConfig: () => req<OverlayConfig>("/api/overlay/config"),
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
  reloadTeamsFromConfiguredJson: () => req<{ ok: boolean }>("/api/teams/reload", { method: "POST" }),
};
