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
import { RankingColumns, RankingRow, RankingsRenderer, totalRankingPages } from "./studio/renderers/RankingsRenderer";
import { HealthStyle, Throwables, resolveHealthStyle } from "./studio/renderers/healthGlyphs";
import { Bg as StudioBg } from "./studio/theme";
import { OverlayStage, layoutFor, layoutStyle } from "./lib/overlayLayout";
import { useBannerQueue } from "./lib/useBannerQueue";

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
    matchRankings: false,
    overallRankings: false,
    mapPerformers: false,
  },
  elementSettings: {},
};

// The feed sits between the achievement banner and the bottom HUD on the left; 4 lines fit.
const FEED_MAX = 4;

const ACHIEVEMENT_LABELS: Record<string, string> = {
  "achievement.grenadeElim": "GRENADE ELIM",
  "achievement.vehicleKill": "VEHICLE KILL",
  "achievement.airdropLoot": "AIRDROP LOOT",
  "achievement.firstKill": "FIRST BLOOD",
  "achievement.knockout": "KNOCKOUT",
  "achievement.chickenDinner": "WINNER WINNER CHICKEN DINNER",
};

// Every graphic below is fed by a SignalR event of the same name, published by the backend
// (MatchStateStore.PublishGraphic) - the backend never talks to vMix; vMix only loads this page.
// On load, GET /api/overlay/snapshot supplies the latest payload of each, so a Browser Source
// that refreshes mid-show comes back with what it had instead of blank panels.

// "TopPlayersUpdated" - PostMatch.TopGrenadiers.
interface RawTopPlayer {
  rank: number;
  playerName: string;
  value: number;
  statLabel: string;
  photoUrl?: string;
}

// "CircleUpdated" - GetLiveData.GetCircleInfo, every tick. Normalised server-side: circleStatus is
// "closing" | "waiting" and counter is SECONDS REMAINING in the phase (pcob's own Counter counts
// up). Values stay strings; parseCircleNumber handles that without throwing.
interface RawCircleInfo {
  gameTime?: string;
  circleStatus?: string;
  circleIndex?: string;
  counter?: string;
  maxTime?: string;
}

// "PlayerHighlightUpdated" - PostMatch.MatchMvp / StageMVP, the MVP / Star Player card.
interface RawPlayerHighlight {
  label?: string;
  playerName: string;
  teamName?: string;
  photoUrl?: string;
  teamLogoUrl?: string;
  stats?: { label: string; value: string | number }[];
}

// "MatchRankingsUpdated" / "OverallRankingsUpdated" - PostMatch.MatchRankings / OverallRankings.
interface RawRankings {
  title?: string;
  subtitle?: string;
  rows: (RankingRow & { logoUrl?: string })[];
}

// "MapPerformersUpdated" - PreMatch.MapTopPerformers, rendered with the Teams to Watch card.
interface RawMapPerformers {
  title?: string;
  teams: TeamToWatchEntry[];
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
  /** Structured fields the backend sends with the event (rank, eliminations, playerName,
   *  teamName, teamId, victimName ...). All optional - a banner must still render from title /
   *  subtitle alone, e.g. one fired by hand from Overlay Settings. */
  data?: Record<string, string>;
  dedupeKey?: string;
}

/** Parses an optional numeric field from an event's data, e.g. "14" -> 14. */
const dataNumber = (b: Banner, key: string): number | undefined => {
  const raw = b.data?.[key];
  if (raw === undefined || raw === null || raw === "") return undefined;
  const n = Number(raw);
  return Number.isFinite(n) ? n : undefined;
};

// Mirrors VmixGraphicsBusiness.LiveMatch.LiveStatsBusiness.Top4TeamStats - pushed over the
// "Top4Updated" SignalR event (see MatchStateStore.PublishTop4Rankings), real win-probability
// numbers included, only while 4 or fewer teams remain.
interface RawTop4Team {
  teamId: number;
  teamName: string;
  teamLogo?: string;
  winProbability: number;
  liveMemberCount?: number;
  playersHealth: { healthPercent: number; liveState: number }[];
  /** Carried frag/smoke/molotov/stun, from getteambackpackinfo. null = not seen yet this match. */
  throwables?: Throwables | null;
}

export default function Overlay() {
  const [config, setConfig] = useState<OverlayConfig>(DEFAULT_CONFIG);
  const [teams, setTeams] = useState<TeamLiveStats[]>([]);
  const [rawTop4, setRawTop4] = useState<RawTop4Team[] | null>(null);
  const [feed, setFeed] = useState<FeedEntry[]>([]);
  // ELIMINATED and achievement banners play one at a time, in order (see useBannerQueue).
  const elimQueue = useBannerQueue<Banner>(4200);
  const achievementQueue = useBannerQueue<Banner>(5000);
  const teamEliminatedBanner = elimQueue.current;
  const achievementBanner = achievementQueue.current;
  const [topPlayers, setTopPlayers] = useState<RawTopPlayer[]>([]);
  const [circle, setCircle] = useState<RawCircleInfo | null>(null);
  // Three more post-match graphics whose numbers already exist in PostMatchStats but aren't
  // broadcast yet. Wired now so each lights up the moment its C# publish side lands.
  const [mvpRows, setMvpRows] = useState<MvpRow[]>([]);
  const [teamsToWatch, setTeamsToWatch] = useState<TeamToWatchEntry[]>([]);
  const [champions, setChampions] = useState<{ label?: string; teamName: string; teamLogoUrl?: string; players?: ChampionPlayer[]; stats?: { label: string; value: string | number }[] } | null>(null);
  const [headToHead, setHeadToHead] = useState<{ left: HeadToHeadTeam; right: HeadToHeadTeam; stats: HeadToHeadStat[] } | null>(null);
  const [teamIntro, setTeamIntro] = useState<{ teamName: string; teamLogoUrl?: string; wwcd: number | null; players: TeamIntroPlayer[]; stats?: { label: string; value: string | number }[] } | null>(null);
  const [mapPlayers, setMapPlayers] = useState<MapPlayerMarker[]>([]);
  const [mapFocusTeamId, setMapFocusTeamId] = useState<number | undefined>(undefined);
  const [highlight, setHighlight] = useState<RawPlayerHighlight | null>(null);
  const [matchRankings, setMatchRankings] = useState<RawRankings | null>(null);
  const [overallRankings, setOverallRankings] = useState<RawRankings | null>(null);
  const [mapPerformers, setMapPerformers] = useState<RawMapPerformers | null>(null);
  // Rankings always run as 2 pages (see RankingsRenderer); on air nobody can click a pager, so
  // the page flips on a timer instead.
  const [rankingsPage, setRankingsPage] = useState(0);
  const teamElimVisible = elimQueue.visible;
  const achievementVisible = achievementQueue.visible;
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

    // One handler per graphic event, shared by the live SignalR subscription and the snapshot
    // hydration below, so both paths apply a payload identically.
    const graphicHandlers: Record<string, (payload: any) => void> = {
      Top4Updated: (updated: RawTop4Team[] | null) => setRawTop4(updated),
      TopPlayersUpdated: (updated: RawTopPlayer[] | null) => setTopPlayers(updated ?? []),
      CircleUpdated: (updated: RawCircleInfo | null) => setCircle(updated),
      MvpRankingsUpdated: (updated: MvpRow[] | null) => setMvpRows(updated ?? []),
      TeamsToWatchUpdated: (updated: TeamToWatchEntry[] | null) => setTeamsToWatch(updated ?? []),
      ChampionsUpdated: (updated: typeof champions) => setChampions(updated),
      HeadToHeadUpdated: (updated: typeof headToHead) => setHeadToHead(updated),
      TeamIntroUpdated: (updated: typeof teamIntro) => setTeamIntro(updated),
      MapPositionsUpdated: (updated: { players?: MapPlayerMarker[]; focusTeamId?: number } | null) => {
        setMapPlayers(updated?.players ?? []);
        setMapFocusTeamId(updated?.focusTeamId);
      },
      PlayerHighlightUpdated: (updated: RawPlayerHighlight | null) => setHighlight(updated),
      MatchRankingsUpdated: (updated: RawRankings | null) => { setMatchRankings(updated); setRankingsPage(0); },
      OverallRankingsUpdated: (updated: RawRankings | null) => { setOverallRankings(updated); setRankingsPage(0); },
      MapPerformersUpdated: (updated: RawMapPerformers | null) => setMapPerformers(updated),
    };
    // Events that arrived live before the snapshot response - the snapshot must not overwrite
    // them with an older value.
    const receivedLive = new Set<string>();
    for (const [eventName, handler] of Object.entries(graphicHandlers)) {
      connection.on(eventName, (payload: unknown) => {
        receivedLive.add(eventName);
        handler(payload);
      });
    }
    fetch(`${API_BASE}/api/overlay/snapshot`, { headers: tokenHeaders })
      .then((r) => (r.ok ? r.json() : {}))
      .then((snapshot: Record<string, unknown>) => {
        for (const [eventName, payload] of Object.entries(snapshot ?? {})) {
          if (!receivedLive.has(eventName)) graphicHandlers[eventName]?.(payload);
        }
      })
      .catch(() => {});
    connection.on("OverlayConfigChanged", (updated: OverlayConfig) => setConfig(updated));
    connection.on("OverlayEvent", (evt: { type: string; title?: string; subtitle?: string; imageUrl?: string; accentColor?: string; data?: Record<string, string> | null }) => {
      const id = `${evt.type}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
      const data = evt.data ?? undefined;

      if (evt.type === "elimination") {
        hasRealKillFeedRef.current = true;
        setFeed((prev) => [{ id, title: evt.title ?? "", subtitle: evt.subtitle, receivedAt: Date.now() }, ...prev].slice(0, FEED_MAX));
        return;
      }

      if (evt.type === "teamEliminated") {
        elimQueue.push({
          id, type: evt.type, title: evt.title ?? "ELIMINATED", subtitle: evt.subtitle, imageUrl: evt.imageUrl, accentColor: evt.accentColor, data,
          dedupeKey: evt.subtitle ? `elim:${evt.subtitle}` : undefined,
        });
        return;
      }

      // Everything else ("achievement.*") is an achievement banner.
      achievementQueue.push({
        id, type: evt.type, title: evt.title ?? ACHIEVEMENT_LABELS[evt.type] ?? evt.type, subtitle: evt.subtitle, imageUrl: evt.imageUrl, accentColor: evt.accentColor, data,
      });
    });

    connection.start().catch(() => {});
    return () => {
      connection.stop();
    };
  }, []);

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
          setFeed((f) => [{ id, title: `${team.tag} scored an elimination`, subtitle: `${team.eliminations} total`, receivedAt: Date.now() }, ...f].slice(0, FEED_MAX));
        }
        if (before && !before.teamEliminated && team.teamEliminated && config.elementVisibility.teamEliminatedBanner) {
          // Fallback only: the backend's real teamEliminated event (with rank and elims) normally
          // arrives first for the same wipe, and the shared dedupe key drops this one.
          const id = `derived-team-elim-${team.tag}-${Date.now()}`;
          const name = displayNameRef.current(team);
          elimQueue.push({ id, type: "teamEliminated", title: "ELIMINATED", subtitle: name, imageUrl: team.logo || undefined,
            data: { eliminations: String(team.eliminations) }, dedupeKey: `elim:${name}` });
        }
      }
    }
    prevTeamsRef.current = teams;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [teams]);

  // Flip Match/Overall Rankings between their two pages while either is on air.
  const rankingsOnAir =
    (config.elementVisibility["matchRankings"] === true && (matchRankings?.rows.length ?? 0) > 1) ||
    (config.elementVisibility["overallRankings"] === true && (overallRankings?.rows.length ?? 0) > 1);
  const rankingsPageSecondsCfg = getConfigElement<number>(config, "rankings.pageSeconds", 8);
  useEffect(() => {
    if (!rankingsOnAir) { setRankingsPage(0); return; }
    const rowCount = Math.max(matchRankings?.rows.length ?? 0, overallRankings?.rows.length ?? 0);
    const pages = totalRankingPages(rowCount);
    const t = window.setInterval(() => setRankingsPage((p) => (p + 1) % pages), Math.max(3, rankingsPageSecondsCfg) * 1000);
    return () => window.clearInterval(t);
  }, [rankingsOnAir, rankingsPageSecondsCfg, matchRankings, overallRankings]);

  const sortedTeams = useMemo(() => [...teams].sort((a, b) => a.teamRank - b.teamRank), [teams]);
  const visible = (id: string, fallback = true) => config.elementVisibility[id] ?? fallback;
  // Task 9: per-graphic panel background opacity, stored in elementSettings via the same
  // getConfigElement convention as every other per-element override on this page (see
  // standings.healthStops etc. just below) - no new API, no backend change.
  const panelOpacity = (id: string) => getConfigElement<number>(config, `${id}.backgroundOpacity`, 100);
  // Canvas background mode (Task 9): chroma (default, unchanged behavior) | transparent (paint
  // nothing, for vMix Browser Sources that handle real alpha) | solid | image. "image" is
  // preview-only (Task 10's Demo page) and is never rendered here - only the Demo page reads
  // the matching `preview.*`-prefixed key, mirroring the existing director.* convention of
  // keys this page must ignore.
  const canvasMode = getConfigElement<string>(config, "canvas.mode", "chroma");
  const canvasSolidColor = getConfigElement<string>(config, "canvas.solidColor", "#000000");
  const canvasBackgroundStyle: { backgroundColor: string } =
    canvasMode === "transparent" ? { backgroundColor: "transparent" } :
    canvasMode === "solid" ? { backgroundColor: canvasSolidColor } :
    { backgroundColor: config.chromaKeyColor }; // "chroma" and "image" (preview-only) both key as normal on air

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

  // PlayerCount (newer backends) trims a 3-man roster to 3 bars instead of showing a phantom
  // dead 4th player.
  const teamPlayers = (team: TeamLiveStats) => [
    { health: team.player1HealthPercent, liveState: team.player1LiveState },
    { health: team.player2HealthPercent, liveState: team.player2LiveState },
    { health: team.player3HealthPercent, liveState: team.player3LiveState },
    { health: team.player4HealthPercent, liveState: team.player4LiveState },
  ].slice(0, team.playerCount && team.playerCount > 0 ? Math.min(4, team.playerCount) : 4);

  const standingsRows: StandingsRow[] = sortedTeams.map((team) => ({
    key: team.tag + team.teamRank,
    rank: team.teamRank,
    name: team.tag || displayName(team),
    kills: team.eliminations,
    players: teamPlayers(team),
    logoUrl: team.logo || undefined,
    points: team.totalPoints,
    eliminated: team.teamEliminated,
  }));

  // One health look for every live graphic (Standings bars, Last 4 helmets) - set in the Studio's
  // Standings > Health tab.
  const healthStyle: HealthStyle = resolveHealthStyle(getConfigElement<Partial<HealthStyle> | null>(config, "health.style", null));
  const standingsShowPoints = getConfigElement<boolean>(config, "standings.showPoints", true);
  const standingsShowElims = getConfigElement<boolean>(config, "standings.showElims", true);
  const top4ShowWwcd = getConfigElement<boolean>(config, "top4.showWwcd", true);
  const top4ShowThrowables = getConfigElement<boolean>(config, "top4.showThrowables", true);
  const top4ShowRank = getConfigElement<boolean>(config, "top4.showRank", false);
  const achievementBodyBg = getConfigElement<StudioBg | null>(config, "achievement.bodyBg", null) ?? undefined;
  const L = (id: string, extra?: React.CSSProperties) => layoutStyle(layoutFor(config, id), extra);

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
  // An operator-set label wins; otherwise the payload's own ("WINNER WINNER CHICKEN DINNER" for a
  // per-match WWCD), otherwise the Studio default.
  const champLabel = getConfigElement<string>(config, "champions.label", champions?.label ?? "CHAMPIONS");

  // RankingsPage.tsx (Graphics Studio) writes these same keys; Match and Overall share one look.
  const rankingsCanvasBg = getConfigElement<Bg | null>(config, "rankings.canvasBg", null) || studioTheme.headerBg;
  const rankingsHeaderBg = getConfigElement<Bg | null>(config, "rankings.headerBg", null) || studioTheme.headerBg;
  const rankingsColumns = getConfigElement<RankingColumns>(config, "rankings.columns", { wins: true, placement: true, elim: true });
  const rankingsColumnStyles = getConfigElement<Record<string, ColumnStyle>>(config, "rankings.columnStyles", {});
  const rankingsRowRules = getConfigElement<RowRule[]>(config, "rankings.rowRules", []);

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
        logoUrl: team.teamLogo || sortedTeams.find((t) => t.tag === team.teamName)?.logo || undefined,
        players: team.playersHealth.slice(0, 4).map((p) => ({ health: p.healthPercent, liveState: p.liveState })),
        throwables: team.throwables ?? null,
        eliminated: team.liveMemberCount === 0,
      }))
    // Fallback only in the final four (same <= 4 rule as the backend's ShouldShowTop4Ranking) -
    // otherwise the Last 4 cards sat on screen all match with the current top 4 of 16 teams.
    : sortedTeams.filter((t) => !t.teamEliminated).length > 4 ? []
    : sortedTeams.filter((t) => !t.teamEliminated).slice(0, 4).map((team) => ({
        key: team.tag + team.teamRank,
        overallRank: team.teamRank,
        tag: team.tag,
        wwcd: NaN,
        logoUrl: team.logo || undefined,
        players: teamPlayers(team),
        throwables: null,
      }));

  return (
    <div className="overlay-root" style={canvasBackgroundStyle}>
     <OverlayStage>
      {visible("leaderboard") && standingsRows.length > 0 && (
        <div style={L("leaderboard")}>
          <StandingsRenderer theme={studioTheme} mode="full" healthStops={healthStops} healthStyle={healthStyle} columns={columns} rowRules={rowRules} headerBg={headerBg} rows={standingsRows} maxRows={25}
            showPoints={standingsShowPoints} showElims={standingsShowElims} panelOpacity={panelOpacity("leaderboard")} />
        </div>
      )}

      {visible("top4") && top4Rows.length > 0 && (
        <div style={L("top4")}>
          <Top4Renderer theme={studioTheme} wwcdBar={wwcdBar} cardBg={top4CardBg} fields={top4Fields} teams={top4Rows} panelOpacity={panelOpacity("top4")}
            healthStyle={healthStyle} healthStops={healthStops} showWwcd={top4ShowWwcd} showThrowables={top4ShowThrowables} showRank={top4ShowRank} />
        </div>
      )}

      {visible("sidebar", false) && sidebarRows.length > 0 && (
        <div style={L("sidebar")}>
          <SidebarRenderer theme={studioTheme} headerBg={studioTheme.headerBg} rows={sidebarRows} maxRows={16} panelOpacity={panelOpacity("sidebar")} />
        </div>
      )}

      {visible("topPlayers", false) && topPlayerEntries.length > 0 && (
        <div style={L("topPlayers", { aspectRatio: "16/9" })}>
          <TopPlayersRenderer theme={studioTheme} canvasBg={topPlayersCanvasBg} labelBg={topPlayersLabelBg} cardBg={topPlayersCardBg} fields={topPlayersFields} players={topPlayerEntries} panelOpacity={panelOpacity("topPlayers")} />
        </div>
      )}

      {visible("circle", false) && circle && (
        // Top centre, above the Last 4 cards - the two are both on air during the final circles.
        <div style={L("circle")}>
          <CircleStatusRenderer
            theme={studioTheme}
            barBg={circleBarBg}
            circleIndex={parseCircleNumber(circle.circleIndex)}
            phase={circlePhase}
            secondsRemaining={parseCircleNumber(circle.counter)}
            phaseSeconds={parseCircleNumber(circle.maxTime)}
            label={circlePhase === "closing" ? circleClosingLabel : circleWaitingLabel}
            panelOpacity={panelOpacity("circle")}
          />
        </div>
      )}

      {visible("playerHighlight", false) && highlight && (
        <div style={L("playerHighlight", { aspectRatio: "16/9" })}>
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
            panelOpacity={panelOpacity("playerHighlight")}
          />
        </div>
      )}

      {visible("mvpRankings", false) && mvpRows.length > 0 && (
        <div style={L("mvpRankings", { aspectRatio: "16/9" })}>
          <MvpRankingsRenderer theme={studioTheme} canvasBg={mvpCanvasBg} headerBg={mvpHeaderBg}
            title={mvpTitle} subtitle={mvpSubtitle} rows={mvpRows} columns={mvpColumns}
            columnStyles={mvpColumnStyles} rowRules={mvpRowRules} panelOpacity={panelOpacity("mvpRankings")} />
        </div>
      )}

      {visible("teamsToWatch", false) && teamsToWatch.length > 0 && (
        <div style={L("teamsToWatch", { aspectRatio: "16/9" })}>
          <TeamsToWatchRenderer theme={studioTheme} canvasBg={ttwCanvasBg} accentBg={ttwAccentBg}
            title={ttwTitle} subtitle={ttwSubtitle} teams={teamsToWatch} fields={ttwFields} panelOpacity={panelOpacity("teamsToWatch")} />
        </div>
      )}

      {visible("champions", false) && champions && (
        <div style={L("champions", { aspectRatio: "16/9" })}>
          <ChampionsRenderer theme={studioTheme} canvasBg={champCanvasBg} accentBg={champAccentBg}
            label={champLabel} teamName={champions.teamName} teamLogoUrl={champions.teamLogoUrl}
            players={champions.players} stats={champions.stats} fields={champFields} panelOpacity={panelOpacity("champions")} />
        </div>
      )}

      {visible("matchRankings", false) && matchRankings && matchRankings.rows.length > 0 && (
        <div style={L("matchRankings", { aspectRatio: "16/9" })}>
          <RankingsRenderer theme={studioTheme} canvasBg={rankingsCanvasBg} headerBg={rankingsHeaderBg}
            title={matchRankings.title ?? "MATCH RANKINGS"} subtitle={matchRankings.subtitle ?? ""}
            rows={matchRankings.rows} columns={rankingsColumns} columnStyles={rankingsColumnStyles} rowRules={rankingsRowRules}
            page={rankingsPage} pager="indicator" panelOpacity={panelOpacity("matchRankings")} />
        </div>
      )}

      {visible("overallRankings", false) && overallRankings && overallRankings.rows.length > 0 && (
        <div style={L("overallRankings", { aspectRatio: "16/9" })}>
          <RankingsRenderer theme={studioTheme} canvasBg={rankingsCanvasBg} headerBg={rankingsHeaderBg}
            title={overallRankings.title ?? "OVERALL RANKINGS"} subtitle={overallRankings.subtitle ?? ""}
            rows={overallRankings.rows} columns={rankingsColumns} columnStyles={rankingsColumnStyles} rowRules={rankingsRowRules}
            page={rankingsPage} pager="indicator" panelOpacity={panelOpacity("overallRankings")} />
        </div>
      )}

      {visible("mapPerformers", false) && mapPerformers && mapPerformers.teams.length > 0 && (
        <div style={L("mapPerformers", { aspectRatio: "16/9" })}>
          <TeamsToWatchRenderer theme={studioTheme} canvasBg={ttwCanvasBg} accentBg={ttwAccentBg}
            title={mapPerformers.title ?? "TOP MAP PERFORMERS"} subtitle="" teams={mapPerformers.teams} fields={ttwFields} panelOpacity={panelOpacity("mapPerformers")} />
        </div>
      )}

      {visible("headToHead", false) && headToHead && (
        <div style={L("headToHead", { aspectRatio: "16/9" })}>
          <HeadToHeadRenderer theme={studioTheme} canvasBg={h2hCanvasBg} accentBg={h2hAccentBg}
            title={h2hTitle} subtitle={h2hSubtitle} left={headToHead.left} right={headToHead.right}
            stats={headToHead.stats} fields={h2hFields} panelOpacity={panelOpacity("headToHead")} />
        </div>
      )}

      {visible("teamIntro", false) && teamIntro && (
        <div style={L("teamIntro", { aspectRatio: "16/9" })}>
          <TeamIntroRenderer theme={studioTheme} canvasBg={introCanvasBg} accentBg={introAccentBg}
            label={introLabel} teamName={teamIntro.teamName} teamLogoUrl={teamIntro.teamLogoUrl}
            wwcd={teamIntro.wwcd} players={teamIntro.players} stats={teamIntro.stats}
            healthStops={healthStops} fields={introFields} panelOpacity={panelOpacity("teamIntro")} />
        </div>
      )}

      {visible("spectatorMap", false) && mapPlayers.length > 0 && (
        <div style={L("spectatorMap")}>
          <SpectatorMapRenderer theme={studioTheme} canvasBg={mapCanvasBg} accentBg={mapAccentBg}
            mapImageUrl={mapImageUrl || undefined} players={mapPlayers} worldSize={mapWorldSize}
            focusTeamId={effectiveMapFocus} showLabels={mapShowLabels} />
        </div>
      )}

      {visible("eliminationFeed") && feed.length > 0 && (
        <div className="overlay-feed" style={L("eliminationFeed")}>
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
        <div key={teamEliminatedBanner.id} style={L("teamEliminatedBanner")}>
          <EliminatedBannerRenderer
            theme={studioTheme}
            bannerBg={elimBannerBg}
            teamName={teamEliminatedBanner.subtitle ?? teamEliminatedBanner.title}
            logoUrl={teamEliminatedBanner.imageUrl}
            rank={dataNumber(teamEliminatedBanner, "rank")}
            eliminations={dataNumber(teamEliminatedBanner, "eliminations")}
            visible={teamElimVisible}
            panelOpacity={panelOpacity("teamEliminatedBanner")}
          />
        </div>
      )}

      {achievementBanner && visible(achievementBanner.type) && (
        <div key={achievementBanner.id} style={L(achievementBanner.type)}>
          <AchievementRenderer
            theme={studioTheme}
            accentBg={achievementAccentBg}
            bodyBg={achievementBodyBg}
            label={ACHIEVEMENT_LABELS[achievementBanner.type] ?? achievementBanner.title}
            primary={achievementBanner.data?.playerName ?? achievementBanner.subtitle ?? achievementBanner.title}
            victim={achievementBanner.data?.victimName || undefined}
            teamName={achievementBanner.data?.teamName || undefined}
            teamLogoUrl={achievementBanner.data?.teamId ? `/team-logos/${achievementBanner.data.teamId}.png` : undefined}
            icon={achievementIcon(achievementBanner.type, 40)}
            photoUrl={achievementBanner.imageUrl}
            visible={achievementVisible}
            panelOpacity={panelOpacity(achievementBanner.type)}
          />
        </div>
      )}
     </OverlayStage>
    </div>
  );
}
