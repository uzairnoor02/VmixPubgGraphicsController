// Sample match data for Studio previews - ported verbatim from graphics-studio-app.jsx. Real
// tournament data (Downloads/files' actual semifinal match), used only so an operator designing a
// graphic's look sees something realistic without a live match running. The live /overlay route
// uses real SignalR data instead of this - see OverlayRenderer.tsx.

export interface SamplePlayer { playerName: string; health: number; liveState: number }
export interface SampleTeam { teamId: number; teamName: string; rank: number; kills: number; players: SamplePlayer[] }

export const SAMPLE_STANDINGS: SampleTeam[] = [
  { teamId: 5, teamName: "Team Star", rank: 1, kills: 12, players: [{ playerName: "starHOUYIrd", health: 19, liveState: 0 }, { playerName: "starHARALDrd", health: 10, liveState: 0 }, { playerName: "starTABBYrd", health: 75, liveState: 0 }, { playerName: "starSHAHEERrd", health: 100, liveState: 2 }] },
  { teamId: 6, teamName: "R3GICIDE", rank: 2, kills: 8, players: [{ playerName: "R3G・SHAKA¹", health: 0, liveState: 5 }, { playerName: "R3G・IRON", health: 0, liveState: 5 }, { playerName: "R3G・VAAS", health: 0, liveState: 5 }, { playerName: "R3G・ROSHAAN", health: 0, liveState: 5 }] },
  { teamId: 18, teamName: "ASxi8 Esports", rank: 3, kills: 9, players: [{ playerName: "Asi8CRYPT0", health: 0, liveState: 5 }, { playerName: "Asdddddddddď", health: 0, liveState: 5 }, { playerName: "Asi8GHOOST", health: 0, liveState: 5 }, { playerName: "Asi8CASANOVA", health: 0, liveState: 5 }] },
  { teamId: 20, teamName: "RPG", rank: 4, kills: 4, players: [{ playerName: "rpgREHMAN", health: 0, liveState: 5 }, { playerName: "SNDZxzoGHOOST", health: 0, liveState: 5 }, { playerName: "rpgMISTAKER", health: 0, liveState: 5 }, { playerName: "rpgDEXTER", health: 0, liveState: 5 }] },
  { teamId: 19, teamName: "Seventh Elements", rank: 5, kills: 4, players: [{ playerName: "7EūSHAHMEERR", health: 0, liveState: 5 }, { playerName: "7EūHUZAIFAx", health: 0, liveState: 5 }, { playerName: "7EūALIYAN", health: 0, liveState: 5 }, { playerName: "7EūGOKUBOT", health: 0, liveState: 5 }] },
  { teamId: 16, teamName: "TOGxUZ", rank: 6, kills: 3, players: [{ playerName: "togxuzALIEN", health: 0, liveState: 5 }, { playerName: "togKHANx", health: 0, liveState: 5 }, { playerName: "togxuzGOGO", health: 0, liveState: 5 }, { playerName: "togxuzNOCTO", health: 0, liveState: 5 }] },
  { teamId: 17, teamName: "Team Chronicles", rank: 7, kills: 2, players: [{ playerName: "tqxTCīHUSSAIN", health: 0, liveState: 5 }, { playerName: "BATMANūīūī", health: 0, liveState: 5 }, { playerName: "Q1・TAREEN", health: 0, liveState: 5 }, { playerName: "Q1ūHASEEEEB", health: 0, liveState: 5 }] },
  { teamId: 8, teamName: "Tear1 Esports", rank: 8, kills: 3, players: [{ playerName: "・Waŀèéð", health: 0, liveState: 5 }, { playerName: "T1丨ALPHA", health: 0, liveState: 5 }, { playerName: "T1丨CAPTAIN", health: 0, liveState: 5 }, { playerName: "T1丨HAZE", health: 0, liveState: 5 }] },
  { teamId: 9, teamName: "The Rulers Esports", rank: 9, kills: 0, players: [{ playerName: "hyperHASIـTR", health: 0, liveState: 5 }, { playerName: "hyperGHOSTـTR", health: 0, liveState: 5 }, { playerName: "hyperJ1NNـ7TR", health: 0, liveState: 5 }, { playerName: "TRESـSHAMMAAS", health: 0, liveState: 5 }] },
  { teamId: 12, teamName: "Swat Rivals", rank: 10, kills: 4, players: [{ playerName: "srCADō", health: 0, liveState: 5 }, { playerName: "WHOONAIN", health: 0, liveState: 5 }, { playerName: "thSHERYŷ", health: 0, liveState: 5 }, { playerName: "srKHANop7", health: 0, liveState: 5 }] },
];

export interface SampleRankingRow { rank: number; teamName: string; wins?: number; placementPts: number; elimPts: number; total: number }

export const SAMPLE_MATCH_RANKING: SampleRankingRow[] = [
  { rank: 1, teamName: "Team Star", placementPts: 10, elimPts: 12, total: 22 }, { rank: 2, teamName: "R3GICIDE", placementPts: 6, elimPts: 8, total: 14 },
  { rank: 3, teamName: "ASxi8 Esports", placementPts: 5, elimPts: 9, total: 14 }, { rank: 4, teamName: "RPG", placementPts: 4, elimPts: 4, total: 8 },
  { rank: 5, teamName: "Seventh Elements", placementPts: 3, elimPts: 4, total: 7 }, { rank: 6, teamName: "TOGxUZ", placementPts: 2, elimPts: 3, total: 5 },
  { rank: 7, teamName: "Team Chronicles", placementPts: 1, elimPts: 2, total: 3 }, { rank: 8, teamName: "Tear1 Esports", placementPts: 1, elimPts: 3, total: 4 },
  { rank: 9, teamName: "The Rulers Esports", placementPts: 0, elimPts: 0, total: 0 }, { rank: 10, teamName: "Swat Rivals", placementPts: 0, elimPts: 4, total: 4 },
  { rank: 11, teamName: "Vortex Rangers", placementPts: 0, elimPts: 1, total: 1 }, { rank: 12, teamName: "XQR Esports", placementPts: 0, elimPts: 5, total: 5 },
  { rank: 13, teamName: "Fire X", placementPts: 0, elimPts: 0, total: 0 }, { rank: 14, teamName: "Too Easy For", placementPts: 0, elimPts: 1, total: 1 },
  { rank: 15, teamName: "General Esports", placementPts: 0, elimPts: 4, total: 4 }, { rank: 16, teamName: "Red Zone eSPORTS", placementPts: 0, elimPts: 4, total: 4 },
  { rank: 17, teamName: "Incredible", placementPts: 0, elimPts: 1, total: 1 }, { rank: 18, teamName: "Team Vortex9 esports", placementPts: 0, elimPts: 1, total: 1 },
];

export const SAMPLE_OVERALL_RANKING: SampleRankingRow[] = [
  { rank: 1, teamName: "ASxi8 Esports", wins: 1, placementPts: 21, elimPts: 30, total: 51 }, { rank: 2, teamName: "General Esports", wins: 1, placementPts: 14, elimPts: 25, total: 39 },
  { rank: 3, teamName: "Team Star", wins: 1, placementPts: 12, elimPts: 27, total: 39 }, { rank: 4, teamName: "Seventh Elements", wins: 1, placementPts: 13, elimPts: 24, total: 37 },
  { rank: 5, teamName: "R3GICIDE", wins: 0, placementPts: 13, elimPts: 24, total: 37 }, { rank: 6, teamName: "Team Chronicles", wins: 0, placementPts: 10, elimPts: 22, total: 32 },
  { rank: 7, teamName: "Red Zone eSPORTS", wins: 0, placementPts: 4, elimPts: 21, total: 25 }, { rank: 8, teamName: "RPG", wins: 0, placementPts: 8, elimPts: 17, total: 25 },
  { rank: 9, teamName: "The Rulers Esports", wins: 0, placementPts: 10, elimPts: 13, total: 23 }, { rank: 10, teamName: "XQR Esports", wins: 0, placementPts: 6, elimPts: 13, total: 19 },
  { rank: 11, teamName: "Swat Rivals", wins: 0, placementPts: 6, elimPts: 6, total: 12 }, { rank: 12, teamName: "Fire X", wins: 0, placementPts: 7, elimPts: 4, total: 11 },
  { rank: 13, teamName: "TOGxUZ", wins: 0, placementPts: 2, elimPts: 9, total: 11 }, { rank: 14, teamName: "Tear1 Esports", wins: 0, placementPts: 2, elimPts: 8, total: 10 },
  { rank: 15, teamName: "Too Easy For", wins: 0, placementPts: 0, elimPts: 9, total: 9 }, { rank: 16, teamName: "Incredible", wins: 0, placementPts: 0, elimPts: 5, total: 5 },
  { rank: 17, teamName: "Team Vortex9 esports", wins: 0, placementPts: 0, elimPts: 3, total: 3 }, { rank: 18, teamName: "Vortex Rangers", wins: 0, placementPts: 0, elimPts: 3, total: 3 },
];

export interface SampleTopPlayer { rank: number; playerName: string; value: number; statLabel: string }
export const SAMPLE_TOP5_KILLS: SampleTopPlayer[] = [
  { rank: 1, playerName: "Asi8CASANOVA", value: 12, statLabel: "ELIMINATIONS" }, { rank: 2, playerName: "rpgREHMAN", value: 11, statLabel: "ELIMINATIONS" }, { rank: 3, playerName: "R3G・IRON", value: 9, statLabel: "ELIMINATIONS" },
];
export const SAMPLE_TOP5_DAMAGE: SampleTopPlayer[] = [
  { rank: 1, playerName: "Asi8CASANOVA", value: 1894, statLabel: "DAMAGE" }, { rank: 2, playerName: "R3G・VAAS", value: 1830, statLabel: "DAMAGE" }, { rank: 3, playerName: "GEN・MAFIA", value: 1764, statLabel: "DAMAGE" },
];

export interface SampleSidebarTeam { teamId: number; teamName: string; rank: number; kills: number }
export const SAMPLE_SIDEBAR_TEAMS: SampleSidebarTeam[] = [
  { teamId: 5, teamName: "Team Star", rank: 1, kills: 12 }, { teamId: 6, teamName: "R3GICIDE", rank: 2, kills: 8 }, { teamId: 18, teamName: "ASxi8 Esports", rank: 3, kills: 9 },
  { teamId: 20, teamName: "RPG", rank: 4, kills: 4 }, { teamId: 19, teamName: "Seventh Elements", rank: 5, kills: 4 }, { teamId: 16, teamName: "TOGxUZ", rank: 6, kills: 3 }, { teamId: 17, teamName: "Team Chronicles", rank: 7, kills: 2 },
];

export interface SampleTop4Team { overallRank: number; tag: string; wwcd: number; players: SamplePlayer[] }
export const SAMPLE_TOP4: SampleTop4Team[] = [
  { overallRank: 9, tag: "FL", wwcd: 25.7, players: [{ playerName: "", health: 80, liveState: 0 }, { playerName: "", health: 45, liveState: 0 }, { playerName: "", health: 100, liveState: 0 }, { playerName: "", health: 0, liveState: 5 }] },
  { overallRank: 12, tag: "HORAA", wwcd: 27.6, players: [{ playerName: "", health: 60, liveState: 0 }, { playerName: "", health: 20, liveState: 4 }, { playerName: "", health: 90, liveState: 0 }, { playerName: "", health: 0, liveState: 5 }] },
  { overallRank: 14, tag: "VIT", wwcd: 22.8, players: [{ playerName: "", health: 100, liveState: 0 }, { playerName: "", health: 0, liveState: 5 }, { playerName: "", health: 0, liveState: 5 }, { playerName: "", health: 55, liveState: 0 }] },
  { overallRank: 16, tag: "ULA", wwcd: 23.9, players: [{ playerName: "", health: 100, liveState: 0 }, { playerName: "", health: 100, liveState: 0 }, { playerName: "", health: 70, liveState: 0 }, { playerName: "", health: 0, liveState: 5 }] },
];

export interface AchievementTypeDef { id: string; label: string; dataSource: "real" | "partial"; note: string }
export const ACHIEVEMENT_TYPES: AchievementTypeDef[] = [
  { id: "first_blood", label: "First Blood", dataSource: "real", note: "First kill_event by game_time_sec — fully real via getkillinfo." },
  { id: "long_range", label: "Long Range Elim", dataSource: "real", note: "Distance field on kill_event is exact — fully real via getkillinfo." },
  { id: "grenade_master", label: "Grenade Elim", dataSource: "partial", note: "getkillinfo has no weapon-type field. Needs killNumByGrenade tick-diff (derived) or getplayerweapondetailinfo cross-reference." },
  { id: "vehicle_kill", label: "Vehicle Elim", dataSource: "partial", note: "Same gap as grenade — getkillinfo doesn't carry weapon/method type directly." },
];

export const ROWS_PER_PAGE = 8;
