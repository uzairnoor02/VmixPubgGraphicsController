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
};
