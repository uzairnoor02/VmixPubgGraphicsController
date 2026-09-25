// One catalogue of every graphic the overlay can show, shared by the Director tab and the
// Overlay Settings tab. Previously each kept its own hand-maintained list, which is exactly the
// kind of duplication that silently drifts the moment a graphic is added.

export type GraphicGroup = "Always-on" | "Live cut-ins" | "Post-match" | "Achievements";

/** What the director has to pick before this graphic makes sense on air. */
export type GraphicSelection = "none" | "team" | "twoTeams";

export interface GraphicDef {
  /** Key in OverlayConfig.elementVisibility. */
  id: string;
  label: string;
  group: GraphicGroup;
  /** Renders itself from the live feed with no director input. */
  automatic: boolean;
  selection: GraphicSelection;
  /** Why it may be dark even when switched on. */
  note?: string;
}

export const GRAPHICS: GraphicDef[] = [
  { id: "leaderboard", label: "Leaderboard / Standings", group: "Always-on", automatic: true, selection: "none" },
  { id: "sidebar", label: "Live sidebar leaderboard", group: "Always-on", automatic: true, selection: "none" },
  { id: "top4", label: "Top 4 / WWCD panel", group: "Always-on", automatic: true, selection: "none", note: "Only populates once 4 or fewer teams remain." },
  { id: "circle", label: "Circle status bar", group: "Always-on", automatic: true, selection: "none" },
  { id: "eliminationFeed", label: "Elimination feed", group: "Always-on", automatic: true, selection: "none" },

  { id: "teamEliminatedBanner", label: '"TEAM ELIMINATED" banner', group: "Live cut-ins", automatic: true, selection: "none" },
  { id: "spectatorMap", label: "Spectator map", group: "Live cut-ins", automatic: true, selection: "team", note: "Awaiting MapPositionsUpdated. Focus team is applied live." },
  { id: "teamIntro", label: "Team Intro / WWCD", group: "Live cut-ins", automatic: false, selection: "team", note: "Awaiting TeamIntroUpdated." },

  { id: "matchRankings", label: "Match Rankings", group: "Post-match", automatic: true, selection: "none", note: "Filled automatically when a match ends." },
  { id: "overallRankings", label: "Overall Rankings", group: "Post-match", automatic: true, selection: "none", note: "Filled automatically when a match ends." },
  { id: "champions", label: "Champions / WWCD card", group: "Post-match", automatic: true, selection: "none", note: "Match winner, filled automatically when a match ends." },
  { id: "playerHighlight", label: "Player Highlight / MVP card", group: "Post-match", automatic: true, selection: "none", note: "Match MVP when a match ends; Stage MVP on request." },
  { id: "mvpRankings", label: "MVP Rankings table", group: "Post-match", automatic: true, selection: "none", note: "Match top 5 when a match ends; stage top 5 on request." },
  { id: "teamsToWatch", label: "Teams to Watch", group: "Post-match", automatic: true, selection: "none", note: "Filled automatically when a match ends." },
  { id: "topPlayers", label: "Top Players podium", group: "Post-match", automatic: false, selection: "none", note: "Top grenadiers - generate from Match Control." },
  { id: "mapPerformers", label: "Top Map Performers", group: "Post-match", automatic: false, selection: "none", note: "Generate from Match Control for the next map." },
  { id: "headToHead", label: "Head to Head", group: "Post-match", automatic: false, selection: "twoTeams", note: "Awaiting HeadToHeadUpdated." },

  { id: "achievement.grenadeElim", label: "Grenade Elimination", group: "Achievements", automatic: true, selection: "none" },
  { id: "achievement.vehicleKill", label: "Vehicle Kill", group: "Achievements", automatic: true, selection: "none" },
  { id: "achievement.airdropLoot", label: "Airdrop Loot", group: "Achievements", automatic: true, selection: "none" },
  { id: "achievement.firstKill", label: "First Kill", group: "Achievements", automatic: true, selection: "none" },
  { id: "achievement.knockout", label: "Knockout", group: "Achievements", automatic: true, selection: "none" },
  { id: "achievement.chickenDinner", label: "Winner Winner Chicken Dinner", group: "Achievements", automatic: true, selection: "none" },
];

export const GRAPHIC_GROUPS: GraphicGroup[] = ["Always-on", "Live cut-ins", "Post-match", "Achievements"];

/** Director selections live in elementSettings under these keys rather than in a new API: the
 *  existing POST /api/overlay/config already persists them and already broadcasts
 *  OverlayConfigChanged, so the overlay and the backend both see a pick immediately with no new
 *  endpoint and no extra round trip. */
export const DIRECTOR_KEYS = {
  mapFocusTeamId: "director.map.focusTeamId",
  teamIntroTeamId: "director.teamIntro.teamId",
  h2hLeftTeamId: "director.h2h.leftTeamId",
  h2hRightTeamId: "director.h2h.rightTeamId",
} as const;

/** Graphics a "go live" preset turns on, and the ones a post-match segment turns on. Achievements
 *  are left alone by both: whether they fire is a per-event editorial choice, not a segment. */
export const SEGMENT_PRESETS: Record<string, { label: string; on: string[] }> = {
  live: { label: "Live match", on: ["leaderboard", "top4", "circle", "eliminationFeed", "teamEliminatedBanner"] },
  postMatch: { label: "Post-match", on: ["matchRankings"] },
  blackout: { label: "Blackout", on: [] },
};
