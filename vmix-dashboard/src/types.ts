// Mirrors VmixGraphicsBusiness.TeamLiveStats (see ../../VmixGraphicsBusiness/TeamLiveStats.cs).
// Keep this in sync if that C# class's fields change.
export interface TeamLiveStats {
  teamRank: number;
  teamEliminated: boolean;
  logo: string;
  tag: string;
  totalPoints: number;
  eliminations: number;
  player1Health?: string;
  player2Health?: string;
  player3Health?: string;
  player4Health?: string;
  teamBackground: string;
}
