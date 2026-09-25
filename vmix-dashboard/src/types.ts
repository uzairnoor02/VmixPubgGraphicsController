// Mirrors VmixGraphicsBusiness.TeamLiveStats (see ../../VmixGraphicsBusiness/TeamLiveStats.cs).
// Keep this in sync if that C# class's fields change.
export interface TeamLiveStats {
  teamRank: number;
  teamEliminated: boolean;
  logo: string;
  tag: string;
  /** Full team name. Optional because an older backend build doesn't send it - callers should
   *  fall back to `tag`, which is what the overlay displayed before this field existed. */
  teamName?: string;
  totalPoints: number;
  eliminations: number;
  player1Health?: string;
  player2Health?: string;
  player3Health?: string;
  player4Health?: string;
  teamBackground: string;
  // Numeric liveState (0 Normal,1 OnPlane,2 OnParachute,3 OnVehicle,4 Knocked,5 Dead,
  // 6 Disconnected) and 0-100 health percent alongside the pre-rendered image-path fields above -
  // lets a web client compute its own ALIVE/TOTAL count or render its own health bar instead of
  // only being able to display vMix's pre-baked image.
  player1LiveState: number;
  player2LiveState: number;
  player3LiveState: number;
  player4LiveState: number;
  player1HealthPercent: number;
  player2HealthPercent: number;
  player3HealthPercent: number;
  player4HealthPercent: number;
  /** Players on the roster (3 for a 3-man team). Optional: older backends don't send it, and the
   *  overlay then shows 4 slots as before. */
  playerCount?: number;
}

/** true for liveState values EvaluateLiveStatus treats as "alive" (0 Normal through 3 OnVehicle);
 *  4 (knocked) counts as alive-but-down for an ALIVE/TOTAL count, 5 (dead) and 6 (disconnected) do not. */
export function isPlayerAlive(liveState: number): boolean {
  return liveState >= 0 && liveState <= 4;
}

export function teamAliveCount(team: TeamLiveStats): number {
  return [team.player1LiveState, team.player2LiveState, team.player3LiveState, team.player4LiveState]
    .filter((s) => isPlayerAlive(s)).length;
}
