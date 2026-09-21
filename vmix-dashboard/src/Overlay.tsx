import { useEffect, useMemo, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { API_BASE, api } from "./lib/api";
import type { OverlayConfig } from "./lib/api";
import type { TeamLiveStats } from "./types";
import { getConfigElement, getStudioTheme } from "./studio/configAccess";
import { Bg, DEFAULT_HEALTH_STOPS, HealthStop, RowRule } from "./studio/theme";
import type { ColumnStyle } from "./studio/StudioControls";
import { StandingsRenderer, StandingsRow } from "./studio/renderers/StandingsRenderer";
import { Top4Renderer, Top4Team } from "./studio/renderers/Top4Renderer";
import { TopPlayersRenderer, TopPlayerEntry } from "./studio/renderers/TopPlayersRenderer";
import { EliminatedBannerRenderer, SidebarRenderer, SidebarRow } from "./studio/renderers/EliminatedSidebarRenderer";
import { AchievementRenderer, achievementIcon } from "./studio/renderers/AchievementRenderer";
import { CirclePhase, CircleStatusRenderer, parseCircleNumber } from "./studio/renderers/CircleStatusRenderer";
import { HighlightStat, PlayerHighlightRenderer } from "./studio/renderers/PlayerHighlightRenderer";
import { MvpColumns, MvpRankingsRenderer, MvpRow } from "./studio/renderers/MvpRankingsRenderer";
import { TeamToWatchEntry, TeamsToWatchRenderer } from "./studio/renderers/TeamsToWatchRenderer";
import { ChampionPlayer, ChampionsRenderer } from "./studio/renderers/ChampionsRenderer";
import { HeadToHeadRenderer, HeadToHeadStat, HeadToHeadTeam } from "./studio/renderers/HeadToHeadRenderer";
import { TeamIntroPlayer, TeamIntroRenderer } from "./studio/renderers/TeamIntroRenderer";
import { MapPlayerMarker, SpectatorMapRenderer, DEFAULT_WORLD_SIZE } from "./studio/renderers/SpectatorMapRenderer";

// This is the page you paste into vMix as a Web Browser source - it has no login, no nav, no
// buttons, nothing but the graphics themselves on a solid chroma-key background. Everything about
// how it looks (chroma color, which panels are visible, and now the Standings look itself) comes
// from OverlayConfig, set from the Overlay Settings / Graphics Studio tabs and pushed here live
// over SignalR so a change goes out on-air with zero manual steps on the graphics PC.
//
// The Standings panel below renders through the exact same <StandingsRenderer> the Graphics
// Studio's editor preview uses (see studio/renderers/StandingsRenderer.tsx) - a Studio edit and
// what's on air share one render path, not a "preview approximation" of it. It reads theme/style
// settings directly out of this component's own `config` state (already kept live by the
// OverlayConfigChanged subscription below) via the pure getConfigElement/getStudioTheme helpers,
// rather than through StudioConfigProvider's React context - that context does its own fetch on
// mount with no live-update subscription, which would fall out of sync with Studio edits here;
// reading the one OverlayConfig this component already keeps fresh avoids that entirely.

const DEFAULT_CONFIG: OverlayConfig = {
  chromaKeyColor: "#00FF00",
  elementVisibility: {
    leaderboard: true,
    eliminationFeed: true,
    teamEliminatedBanner: true,
    "achievement.grenadeElim": true,
    "achievement.vehicleKill": true,
    "achievement.airdropLoot": true,
    "achievement.firstKill": true,
    "achievement.knockout": true,
    "achievement.chickenDinner": true,
    // Off by default: these two are new render slots, and a graphic must never appear on air
    // just because a build shipped. The operator turns them on from Overlay Settings.
    sidebar: false,
    topPlayers: false,
    circle: false,
    playerHighlight: false,
    mvpRankings: false,
    teamsToWatch: false,
    champions: false,
    headToHead: false,
    teamIntro: false,
    spectatorMap: false,
  },
  elementSettings: {},
};

const ACHIEVEMENT_LABELS: Record<string, string> = {
  "achievement.grenadeElim": "GRENADE ELIM",
  "achievement.vehicleKill": "VEHICLE KILL",
  "achievement.airdropLoot": "AIRDROP LOOT",
  "achievement.firstKill": "FIRST KILL",
  "achievement.knockout": "KNOCKOUT",
  "achievement.chickenDinner": "WINNER WINNER CHICKEN DINNER",
};

// Pushed over a "TopPlayersUpdated" SignalR event. Nothing broadcasts this yet - the backing
// data exists in PostMatchStats (Top5MatchMVP / TopGrenadiers) but is not published live - so the
// slot simply renders nothing until that C# side lands. Wiring it now means the graphic goes live
// the moment the event starts firing, with no further frontend change.
interface RawTopPlayer {
  rank: number;
  playerName: string;
  value: number;
  statLabel: string;
  photoUrl?: string;
}

// Pushed over a "CircleUpdated" SignalR event. pcob's getcircleinfo is already polled by
// GetLiveData but its value isn't broadcast yet, so this slot stays empty until that lands.
// Every field arrives as a string (see CircleInfo in VmixData) - parseCircleNumber handles that
// without throwing on an unexpected value.
interface RawCircleInfo {
  gameTime?: string;
  circleStatus?: string;
  circleIndex?: string;
  counter?: string;
  maxTime?: string;
}

// Pushed over a "PlayerHighlightUpdated" SignalR event - the MVP / Star Player card. The numbers
// exist in PostMatch.MatchMvp but aren't broadcast live yet. Post-match only: survivalTime and
// assists read 0 until a match ends.
interface RawPlayerHighlight {
  label?: string;
  playerName: string;
  teamName?: string;
  photoUrl?: string;
  teamLogoUrl?: string;
  stats?: { label: string; value: string | number }[];
}

interface FeedEntry {
  id: string;
  title: string;
  subtitle?: string;
  receivedAt: number;
}

interface Banner {
  id: string;
  type: string;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  accentColor?: string;
}

// Mirrors VmixGraphicsBusiness.LiveMatch.LiveStatsBusiness.Top4TeamStats - pushed over the
// "Top4Updated" SignalR event (see MatchStateStore.PublishTop4Rankings), real win-probability
// numbers included, only while 4 or fewer teams remain.
interface RawTop4Team {
  teamId: number;
  teamName: string;
  winProbability: number;
  playersHealth: { healthPercent: number; liveState: number }[];
}

export default function Overlay() {
  const [config, setConfig] = useState<OverlayConfig>(DEFAULT_CONFIG);
  const [teams, setTeams] = useState<TeamLiveStats[]>([]);
  const [rawTop4, setRawTop4] = useState<RawTop4Team[] | null>(null);
  const [feed, setFeed] = useState<FeedEntry[]>([]);
  const [teamEliminatedBanner, setTeamEliminatedBanner] = useState<Banner | null>(null);
  const [achievementBanner, setAchievementBanner] = useState<Banner | null>(null);
  const [topPlayers, setTopPlayers] = useState<RawTopPlayer[]>([]);
  const [circle, setCircle] = useState<RawCircleInfo | null>(null);
  // Three more post-match graphics whose numbers already exist in PostMatchStats but aren't
  // broadcast yet. Wired now so each lights up the moment its C# publish side lands.
  const [mvpRows, setMvpRows] = useState<MvpRow[]>([]);
  const [teamsToWatch, setTeamsToWatch] = useState<TeamToWatchEntry[]>([]);
  const [champions, setChampions] = useState<{ teamName: string; teamLogoUrl?: string; players?: ChampionPlayer[]; stats?: { label: string; value: string | number }[] } | null>(null);
  const [headToHead, setHeadToHead] = useState<{ left: HeadToHeadTeam; right: HeadToHeadTeam; stats: HeadToHeadStat[] } | null>(null);
  const [teamIntro, setTeamIntro] = useState<{ teamName: string; teamLogoUrl?: string; wwcd: number | null; players: TeamIntroPlayer[]; stats?: { label: string; value: string | number }[] } | null>(null);
  const [mapPlayers, setMapPlayers] = useState<MapPlayerMarker[]>([]);
  const [mapFocusTeamId, setMapFocusTeamId] = useState<number | undefined>(undefined);
  const [highlight, setHighlight] = useState<RawPlayerHighlight | null>(null);
  // Entrance animations are driven by a boolean the renderers take, flipped one tick after the
  // banner lands so the browser has a frame to paint the "before" state - otherwise the element
  // mounts already-visible and the slide-in never plays.
  const [teamElimVisible, setTeamElimVisible] = useState(false);
  const [achievementVisible, setAchievementVisible] = useState(false);
  const prevTeamsRef = useRef<TeamLiveStats[]>([]);
  // Once a real per-kill event arrives from getkillinfo, the derived "a team's elimination count
  // went up" fallback must stop, or every elimination would appear in the feed twice - once with
  // real player names and once as the vague derived line.
  const hasRealKillFeedRef = useRef(false);
  // The derived-fallback effect below runs before displayName is defined in render order, so it
  // reaches the current implementation through a ref rather than closing over a stale copy.
  const displayNameRef = useRef((team: TeamLiveStats) => team.teamName || team.tag);

  useEffect(() => {
    // Which tournament is this overlay showing? The token in the URL is the only answer - and
    // deliberately so: a vMix Browser Source cannot log in, so the URL pasted into vMix carries
    // both the identity of the tournament and the authority to view it.
    //
    // Sent on the hub connection as ?t= (LiveDashboardHub.OnConnectedAsync puts the connection
    // into that tournament's group) and on REST reads as X-Overlay-Token. A tokenless
    // /overlay URL keeps working and resolves to this install's own tournament, which is what
    // every existing single-machine setup uses.
    const overlayToken = window.location.pathname.replace(/^\/overlay\/?/, "").replace(/\/$/, "");
    const tokenQuery = overlayToken ? `?t=${encodeURIComponent(overlayToken)}` : "";
    const tokenHeaders: Record<string, string> = overlayToken
      ? { "X-Overlay-Token": overlayToken }
      : {};

    api.getOverlayConfig(tokenHeaders).then(setConfig).catch(() => {});
    fetch(`${API_BASE}/api/match/teams`, { headers: tokenHeaders })
      .then((r) => r.json())
      .then(setTeams)
      .catch(() => {});

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/match${tokenQuery}`)
      .withAutomaticReconnect()
      .build();

    connection.on("TeamsUpdated", (updated: TeamLiveStats[]) => setTeams(updated));
    connection.on("Top4Updated", (updated: RawTop4Team[]) => setRawTop4(updated));
    connection.on("TopPlayersUpdated", (updated: RawTopPlayer[]) => setTopPlayers(updated ?? []));
    connection.on("CircleUpdated", (updated: RawCircleInfo | null) => setCircle(updated));
    connection.on("MvpRankingsUpdated", (updated: MvpRow[]) => setMvpRows(updated ?? []));
    connection.on("TeamsToWatchUpdated", (updated: TeamToWatchEntry[]) => setTeamsToWatch(updated ?? []));
    connection.on("ChampionsUpdated", (updated: typeof champions) => setChampions(updated));
    connection.on("HeadToHeadUpdated", (updated: typeof headToHead) => setHeadToHead(updated));
    connection.on("TeamIntroUpdated", (updated: typeof teamIntro) => setTeamIntro(updated));
    connection.on("MapPositionsUpdated", (updated: { players?: MapPlayerMarker[]; focusTeamId?: number } | null) => {
      setMapPlayers(updated?.players ?? []);
      setMapFocusTeamId(updated?.focusTeamId);
    });
    connection.on("PlayerHighlightUpdated", (updated: RawPlayerHighlight | null) => setHighlight(updated));
    connection.on("OverlayConfigChanged", (updated: OverlayConfig) => setConfig(updated));
    connection.on("OverlayEvent", (evt: { type: string; title?: string; subtitle?: string; imageUrl?: string; accentColor?: string }) => {
      const id = `${evt.type}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

      if (evt.type === "elimination") {
        hasRealKillFeedRef.current = true;
        setFeed((prev) => [{ id, title: evt.title ?? "", subtitle: evt.subtitle, receivedAt: Date.now() }, ...prev].slice(0, 8));
        return;
      }

      if (evt.type === "teamEliminated") {
        const banner: Banner = { id, type: evt.type, title: evt.title ?? "TEAM ELIMINATED", subtitle: evt.subtitle, imageUrl: evt.imageUrl, accentColor: evt.accentColor };
        setTeamEliminatedBanner(banner);
        window.setTimeout(() => setTeamEliminatedBanner((cur) => (cur?.id === id ? null : cur)), 5000);
        return;
      }

      // Everything else ("achievement.*") is an achievement popup.
      const banner: Banner = { id, type: evt.type, title: evt.title ?? ACHIEVEMENT_LABELS[evt.type] ?? evt.type, subtitle: evt.subtitle, imageUrl: evt.imageUrl, accentColor: evt.accentColor };
      setAchievementBanner(banner);
      window.setTimeout(() => setAchievementBanner((cur) => (cur?.id === id ? null : cur)), 5500);
    });

    connection.start().catch(() => {});
    return () => {
      connection.stop();
    };
  }, []);

  useEffect(() => {
    if (!teamEliminatedBanner) { setTeamElimVisible(false); return; }
    const t = window.setTimeout(() => setTeamElimVisible(true), 30);
    return () => window.clearTimeout(t);
  }, [teamEliminatedBanner]);

  useEffect(() => {
    if (!achievementBanner) { setAchievementVisible(false); return; }
    const t = window.setTimeout(() => setAchievementVisible(true), 30);
    return () => window.clearTimeout(t);
  }, [achievementBanner]);

  // Fallback kill-feed derivation from the data we do reliably have: a team's elimination count
  // going up, or a team flipping to eliminated. Used only until real per-elimination events (see
  // OverlayEvent / LiveDashboardHost.BroadcastOverlayEvent) are wired into the actual detection
  // code - this keeps the feed non-empty and roughly right in the meantime instead of sitting
  // blank all match.
  useEffect(() => {
    const prev = prevTeamsRef.current;
    if (prev.length > 0) {
      for (const team of teams) {
        const before = prev.find((t) => t.tag === team.tag);
        if (before && team.eliminations > before.eliminations && !hasRealKillFeedRef.current) {
          const id = `derived-elim-${team.tag}-${Date.now()}`;
          setFeed((f) => [{ id, title: `${team.tag} scored an elimination`, subtitle: `${team.eliminations} total`, receivedAt: Date.now() }, ...f].slice(0, 8));
        }
        if (before && !before.teamEliminated && team.teamEliminated && config.elementVisibility.teamEliminatedBanner) {
          const id = `derived-team-elim-${team.tag}-${Date.now()}`;
          setTeamEliminatedBanner({ id, type: "teamEliminated", title: "TEAM ELIMINATED", subtitle: displayNameRef.current(team) });
          window.setTimeout(() => setTeamEliminatedBanner((cur) => (cur?.id === id ? null : cur)), 5000);
        }
      }
    }
    prevTeamsRef.current = teams;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [teams]);

  const sortedTeams = useMemo(() => [...teams].sort((a, b) => a.teamRank - b.teamRank), [teams]);
  const visible = (id: string, fallback = true) => config.elementVisibility[id] ?? fallback;

  // Same config keys StandingsPage.tsx (Graphics Studio) reads/writes via useStudioElement - see
  // that file and StudioConfigContext.tsx for the write side of this.
  const studioTheme = getStudioTheme(config);
  const healthStops = getConfigElement<HealthStop[]>(config, "standings.healthStops", DEFAULT_HEALTH_STOPS);
  const columns = getConfigElement<Record<string, ColumnStyle>>(config, "standings.columns", {});
  const rowRules = getConfigElement<RowRule[]>(config, "standings.rowRules", []);
  const headerBgOverride = getConfigElement<Bg | null>(config, "standings.headerBg", null);
  const headerBg = headerBgOverride || studioTheme.headerBg;

  // TeamLiveStats doesn't carry a full team name (only `tag`) - real per-player health/liveState
  // now comes from the Player{1-4}LiveState/HealthPercent fields added alongside the pre-rendered
  // image paths (see VmixGraphicsBusiness/TeamLiveStats.cs).
  const displayName = (team: TeamLiveStats) => team.teamName || team.tag;

  const standingsRows: StandingsRow[] = sortedTeams.map((team) => ({
    key: team.tag + team.teamRank,
    rank: team.teamRank,
    name: displayName(team),
    kills: team.eliminations,
    players: [
      { health: team.player1HealthPercent, liveState: team.player1LiveState },
      { health: team.player2HealthPercent, liveState: team.player2LiveState },
      { health: team.player3HealthPercent, liveState: team.player3LiveState },
      { health: team.player4HealthPercent, liveState: team.player4LiveState },
    ],
  }));

  // Top4Page.tsx (Graphics Studio) writes these same keys - see useStudioElement calls there.
  const wwcdBarOverride = getConfigElement<Bg | null>(config, "top4.wwcdBar", null);
  const wwcdBar: Bg = wwcdBarOverride || { type: "gradient", angle: 90, stops: [{ pos: 0, color: "#F5A623" }, { pos: 100, color: "#F76B1C" }] };
  const top4CardBg = getConfigElement<Bg>(config, "top4.cardBg", { type: "solid", color: "rgba(10,10,15,0.75)" });
  const top4Fields = getConfigElement<Record<string, ColumnStyle>>(config, "top4.fields", {});

  // EliminatedSidebarPage.tsx / AchievementPage.tsx / TopPlayersPage.tsx write these same keys.
  const elimBannerOverride = getConfigElement<Bg | null>(config, "eliminated.bannerBg", null);
  const elimBannerBg = elimBannerOverride || studioTheme.headerBg;
  const achievementAccentOverride = getConfigElement<Bg | null>(config, "achievement.accentBg", null);
  const achievementAccentBg = achievementAccentOverride || studioTheme.headerBg;
  const topPlayersCanvasOverride = getConfigElement<Bg | null>(config, "topPlayers.canvasBg", null);
  const topPlayersCanvasBg = topPlayersCanvasOverride || studioTheme.headerBg;
  const topPlayersLabelOverride = getConfigElement<Bg | null>(config, "topPlayers.labelBg", null);
  const topPlayersLabelBg = topPlayersLabelOverride || studioTheme.headerBg;
  const topPlayersCardBg = getConfigElement<Bg>(config, "topPlayers.cardBg", { type: "solid", color: "#0a0a0a" });
  const topPlayersFields = getConfigElement<Record<string, ColumnStyle>>(config, "topPlayers.fields", {});

  // The sidebar is team-level only, so it renders from TeamsUpdated data already in hand.
  // totalPoints of 0 before any points are awarded shows as "-" rather than a misleading zero.
  const sidebarRows: SidebarRow[] = sortedTeams.map((team) => ({
    key: team.tag + team.teamRank,
    rank: team.teamRank,
    teamName: displayName(team),
    points: team.totalPoints > 0 ? team.totalPoints : null,
    kills: team.eliminations,
    logoUrl: team.logo || undefined,
  }));

  const topPlayerEntries: TopPlayerEntry[] = topPlayers.map((p) => ({
    rank: p.rank, playerName: p.playerName, value: p.value, statLabel: p.statLabel, photoUrl: p.photoUrl,
  }));

  // CircleStatusPage.tsx writes these same keys.
  const circleBarOverride = getConfigElement<Bg | null>(config, "circle.barBg", null);
  const circleBarBg = circleBarOverride || studioTheme.headerBg;
  const circleWaitingLabel = getConfigElement<string>(config, "circle.waitingLabel", "NEXT ZONE IN");
  const circleClosingLabel = getConfigElement<string>(config, "circle.closingLabel", "ZONE CLOSING");
  // pcob reports CircleStatus as a string whose exact vocabulary isn't documented, so anything
  // mentioning "clos"/"shrink"/"moving" counts as actively closing and everything else as the
  // waiting phase - a wrong guess changes only the wording, never whether the bar renders.
  const circlePhase: CirclePhase = /clos|shrink|moving/i.test(circle?.circleStatus ?? "") ? "closing" : "waiting";

  const highlightCanvasOverride = getConfigElement<Bg | null>(config, "highlight.canvasBg", null);
  const highlightCanvasBg = highlightCanvasOverride || studioTheme.headerBg;
  const highlightAccentOverride = getConfigElement<Bg | null>(config, "highlight.accentBg", null);
  const highlightAccentBg = highlightAccentOverride || studioTheme.headerBg;
  const highlightFields = getConfigElement<Record<string, ColumnStyle>>(config, "highlight.fields", {});
  const highlightStats: HighlightStat[] = highlight?.stats ?? [];

  // MvpRankingsPage.tsx / TeamsToWatchPage.tsx / ChampionsPage.tsx write these same keys.
  const mvpCanvasBg = getConfigElement<Bg | null>(config, "mvp.canvasBg", null) || studioTheme.headerBg;
  const mvpHeaderBg = getConfigElement<Bg | null>(config, "mvp.headerBg", null) || studioTheme.headerBg;
  const mvpColumns = getConfigElement<MvpColumns>(config, "mvp.columns", { team: true, kills: true, damage: true, assists: false, survival: false, rating: true });
  const mvpColumnStyles = getConfigElement<Record<string, ColumnStyle>>(config, "mvp.columnStyles", {});
  const mvpRowRules = getConfigElement<RowRule[]>(config, "mvp.rowRules", []);
  const mvpTitle = getConfigElement<string>(config, "mvp.title", "MVP RANKINGS");
  const mvpSubtitle = getConfigElement<string>(config, "mvp.subtitle", "");

  const ttwCanvasBg = getConfigElement<Bg | null>(config, "teamsToWatch.canvasBg", null) || studioTheme.headerBg;
  const ttwAccentBg = getConfigElement<Bg | null>(config, "teamsToWatch.accentBg", null) || studioTheme.headerBg;
  const ttwFields = getConfigElement<Record<string, ColumnStyle>>(config, "teamsToWatch.fields", {});
  const ttwTitle = getConfigElement<string>(config, "teamsToWatch.title", "TEAMS TO WATCH");
  const ttwSubtitle = getConfigElement<string>(config, "teamsToWatch.subtitle", "");

  const champCanvasBg = getConfigElement<Bg | null>(config, "champions.canvasBg", null) || studioTheme.headerBg;
  const champAccentBg = getConfigElement<Bg | null>(config, "champions.accentBg", null) || studioTheme.headerBg;
  const champFields = getConfigElement<Record<string, ColumnStyle>>(config, "champions.fields", {});
  const champLabel = getConfigElement<string>(config, "champions.label", "CHAMPIONS");

  // HeadToHeadPage.tsx / TeamIntroPage.tsx / SpectatorMapPage.tsx write these same keys.
  const h2hCanvasBg = getConfigElement<Bg | null>(config, "h2h.canvasBg", null) || studioTheme.headerBg;
  const h2hAccentBg = getConfigElement<Bg | null>(config, "h2h.accentBg", null) || studioTheme.headerBg;
  const h2hFields = getConfigElement<Record<string, ColumnStyle>>(config, "h2h.fields", {});
  const h2hTitle = getConfigElement<string>(config, "h2h.title", "HEAD TO HEAD");
  const h2hSubtitle = getConfigElement<string>(config, "h2h.subtitle", "");

  const introCanvasBg = getConfigElement<Bg | null>(config, "teamIntro.canvasBg", null) || studioTheme.headerBg;
  const introAccentBg = getConfigElement<Bg | null>(config, "teamIntro.accentBg", null) || studioTheme.headerBg;
  const introFields = getConfigElement<Record<string, ColumnStyle>>(config, "teamIntro.fields", {});
  const introLabel = getConfigElement<string>(config, "teamIntro.label", "TEAM SPOTLIGHT");

  const mapCanvasBg = getConfigElement<Bg>(config, "map.canvasBg", { type: "solid", color: "#101820" });
  const mapAccentBg = getConfigElement<Bg | null>(config, "map.accentBg", null) || studioTheme.headerBg;
  const mapShowLabels = getConfigElement<boolean>(config, "map.showLabels", false);
  const mapWorldSize = getConfigElement<number>(config, "map.worldSize", DEFAULT_WORLD_SIZE);
  const mapImageUrl = getConfigElement<string>(config, "map.imageUrl", "");
  // The Director tab's focus pick overrides whatever the feed carried. Applied here rather
  // than server-side so the picker works the moment map data exists, with no backend change.
  const directorMapFocus = getConfigElement<number | undefined>(config, "director.map.focusTeamId", undefined);
  const effectiveMapFocus = directorMapFocus ?? mapFocusTeamId;
  // Prefer the real win-probability board (Top4Updated / CreateTop4LiveRanking) whenever it's
  // available - it only gets pushed once 4 or fewer teams remain. Before that (or if it hasn't
  // arrived yet this session), fall back to the top-ranked surviving teams from TeamsUpdated with
  // no WWCD % shown, rather than fabricating a number - see teamAliveCount for why 0% would be
  // misleading (a team could be down to 1 alive player and still show as "healthy").
  const top4Rows: Top4Team[] = rawTop4 && rawTop4.length > 0
    ? rawTop4.map((team) => ({
        key: team.teamId,
        overallRank: sortedTeams.find((t) => t.tag === team.teamName)?.teamRank ?? 0,
        tag: team.teamName,
        wwcd: Math.round(team.winProbability * 10) / 10,
        logoUrl: sortedTeams.find((t) => t.tag === team.teamName)?.logo || undefined,
        players: team.playersHealth.slice(0, 4).map((p) => ({ health: p.healthPercent, liveState: p.liveState })),
      }))
    : sortedTeams.filter((t) => !t.teamEliminated).slice(0, 4).map((team) => ({
        key: team.tag + team.teamRank,
        overallRank: team.teamRank,
        tag: team.tag,
        wwcd: NaN,
        logoUrl: team.logo || undefined,
        players: [
          { health: team.player1HealthPercent, liveState: team.player1LiveState },
          { health: team.player2HealthPercent, liveState: team.player2LiveState },
          { health: team.player3HealthPercent, liveState: team.player3LiveState },
          { health: team.player4HealthPercent, liveState: team.player4LiveState },
        ],
      }));

  return (
    <div className="overlay-root" style={{ backgroundColor: config.chromaKeyColor }}>
      {visible("leaderboard") && standingsRows.length > 0 && (
        <div style={{ position: "absolute", top: 48, right: 48, width: 420 }}>
          <StandingsRenderer theme={studioTheme} mode="full" healthStops={healthStops} columns={columns} rowRules={rowRules} headerBg={headerBg} rows={standingsRows} maxRows={16} />
        </div>
      )}

      {visible("top4") && top4Rows.length > 0 && (
        <div style={{ position: "absolute", top: 24, left: "50%", transform: "translateX(-50%)", width: 720 }}>
          <Top4Renderer theme={studioTheme} wwcdBar={wwcdBar} cardBg={top4CardBg} fields={top4Fields} teams={top4Rows} />
        </div>
      )}

      {visible("sidebar", false) && sidebarRows.length > 0 && (
        <div style={{ position: "absolute", top: 48, left: 48, width: 260 }}>
          <SidebarRenderer theme={studioTheme} headerBg={studioTheme.headerBg} rows={sidebarRows} maxRows={16} />
        </div>
      )}

      {visible("topPlayers", false) && topPlayerEntries.length > 0 && (
        <div style={{ position: "absolute", bottom: 48, left: "50%", transform: "translateX(-50%)", width: 560, aspectRatio: "16/9" }}>
          <TopPlayersRenderer theme={studioTheme} canvasBg={topPlayersCanvasBg} labelBg={topPlayersLabelBg} cardBg={topPlayersCardBg} fields={topPlayersFields} players={topPlayerEntries} />
        </div>
      )}

      {visible("circle", false) && circle && (
        <div style={{ position: "absolute", top: 24, left: "50%", transform: "translateX(-50%)", width: 520 }}>
          <CircleStatusRenderer
            theme={studioTheme}
            barBg={circleBarBg}
            circleIndex={parseCircleNumber(circle.circleIndex)}
            phase={circlePhase}
            secondsRemaining={parseCircleNumber(circle.counter)}
            phaseSeconds={parseCircleNumber(circle.maxTime)}
            label={circlePhase === "closing" ? circleClosingLabel : circleWaitingLabel}
          />
        </div>
      )}

      {visible("playerHighlight", false) && highlight && (
        <div style={{ position: "absolute", bottom: 48, right: 48, width: 560, aspectRatio: "16/9" }}>
          <PlayerHighlightRenderer
            theme={studioTheme}
            canvasBg={highlightCanvasBg}
            accentBg={highlightAccentBg}
            label={highlight.label ?? "MVP OF THE MATCH"}
            playerName={highlight.playerName}
            teamName={highlight.teamName}
            photoUrl={highlight.photoUrl}
            teamLogoUrl={highlight.teamLogoUrl}
            stats={highlightStats}
            fields={highlightFields}
          />
        </div>
      )}

      {visible("mvpRankings", false) && mvpRows.length > 0 && (
        <div style={{ position: "absolute", top: "12%", left: "50%", transform: "translateX(-50%)", width: 620, aspectRatio: "16/9" }}>
          <MvpRankingsRenderer theme={studioTheme} canvasBg={mvpCanvasBg} headerBg={mvpHeaderBg}
            title={mvpTitle} subtitle={mvpSubtitle} rows={mvpRows} columns={mvpColumns}
            columnStyles={mvpColumnStyles} rowRules={mvpRowRules} />
        </div>
      )}

      {visible("teamsToWatch", false) && teamsToWatch.length > 0 && (
        <div style={{ position: "absolute", top: "12%", left: "50%", transform: "translateX(-50%)", width: 600, aspectRatio: "16/9" }}>
          <TeamsToWatchRenderer theme={studioTheme} canvasBg={ttwCanvasBg} accentBg={ttwAccentBg}
            title={ttwTitle} subtitle={ttwSubtitle} teams={teamsToWatch} fields={ttwFields} />
        </div>
      )}

      {visible("champions", false) && champions && (
        <div style={{ position: "absolute", top: "10%", left: "50%", transform: "translateX(-50%)", width: 680, aspectRatio: "16/9" }}>
          <ChampionsRenderer theme={studioTheme} canvasBg={champCanvasBg} accentBg={champAccentBg}
            label={champLabel} teamName={champions.teamName} teamLogoUrl={champions.teamLogoUrl}
            players={champions.players} stats={champions.stats} fields={champFields} />
        </div>
      )}

      {visible("headToHead", false) && headToHead && (
        <div style={{ position: "absolute", top: "12%", left: "50%", transform: "translateX(-50%)", width: 600, aspectRatio: "16/9" }}>
          <HeadToHeadRenderer theme={studioTheme} canvasBg={h2hCanvasBg} accentBg={h2hAccentBg}
            title={h2hTitle} subtitle={h2hSubtitle} left={headToHead.left} right={headToHead.right}
            stats={headToHead.stats} fields={h2hFields} />
        </div>
      )}

      {visible("teamIntro", false) && teamIntro && (
        <div style={{ position: "absolute", bottom: 48, left: 48, width: 560, aspectRatio: "16/9" }}>
          <TeamIntroRenderer theme={studioTheme} canvasBg={introCanvasBg} accentBg={introAccentBg}
            label={introLabel} teamName={teamIntro.teamName} teamLogoUrl={teamIntro.teamLogoUrl}
            wwcd={teamIntro.wwcd} players={teamIntro.players} stats={teamIntro.stats}
            healthStops={healthStops} fields={introFields} />
        </div>
      )}

      {visible("spectatorMap", false) && mapPlayers.length > 0 && (
        <div style={{ position: "absolute", bottom: 48, right: 48, width: 320 }}>
          <SpectatorMapRenderer theme={studioTheme} canvasBg={mapCanvasBg} accentBg={mapAccentBg}
            mapImageUrl={mapImageUrl || undefined} players={mapPlayers} worldSize={mapWorldSize}
            focusTeamId={effectiveMapFocus} showLabels={mapShowLabels} />
        </div>
      )}

      {visible("eliminationFeed") && feed.length > 0 && (
        <div className="overlay-feed">
          {feed.map((entry) => (
            <div key={entry.id} className="overlay-feed-item">
              <span className="dot" />
              <div>
                <div className="feed-title">{entry.title}</div>
                {entry.subtitle && <div className="feed-subtitle">{entry.subtitle}</div>}
              </div>
            </div>
          ))}
        </div>
      )}

      {teamEliminatedBanner && visible("teamEliminatedBanner") && (
        <div key={teamEliminatedBanner.id} style={{ position: "absolute", top: "38%", left: "50%", transform: "translateX(-50%)", width: 620 }}>
          <EliminatedBannerRenderer
            theme={studioTheme}
            bannerBg={elimBannerBg}
            teamName={teamEliminatedBanner.subtitle ?? teamEliminatedBanner.title}
            logoUrl={teamEliminatedBanner.imageUrl}
            visible={teamElimVisible}
          />
        </div>
      )}

      {achievementBanner && visible(achievementBanner.type) && (
        <div key={achievementBanner.id} style={{ position: "absolute", bottom: 48, left: 48, width: 420 }}>
          <AchievementRenderer
            theme={studioTheme}
            accentBg={achievementAccentBg}
            label={ACHIEVEMENT_LABELS[achievementBanner.type] ?? achievementBanner.title}
            primary={achievementBanner.subtitle ?? achievementBanner.title}
            icon={achievementIcon(achievementBanner.type)}
            photoUrl={achievementBanner.imageUrl}
            visible={achievementVisible}
          />
        </div>
      )}
    </div>
  );
}
