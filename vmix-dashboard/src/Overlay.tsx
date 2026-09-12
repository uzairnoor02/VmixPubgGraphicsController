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
  },
  elementSettings: {},
};

const ACHIEVEMENT_LABELS: Record<string, { label: string; icon: string }> = {
  "achievement.grenadeElim": { label: "GRENADE ELIM", icon: "💣" },
  "achievement.vehicleKill": { label: "VEHICLE KILL", icon: "🚙" },
  "achievement.airdropLoot": { label: "AIRDROP LOOT", icon: "📦" },
  "achievement.firstKill": { label: "FIRST KILL", icon: "🎯" },
  "achievement.knockout": { label: "KNOCKOUT", icon: "⚡" },
  "achievement.chickenDinner": { label: "WINNER WINNER CHICKEN DINNER", icon: "🏆" },
};

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
  const prevTeamsRef = useRef<TeamLiveStats[]>([]);

  useEffect(() => {
    api.getOverlayConfig().then(setConfig).catch(() => {});
    fetch(`${API_BASE}/api/match/teams`).then((r) => r.json()).then(setTeams).catch(() => {});

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE}/hubs/match`)
      .withAutomaticReconnect()
      .build();

    connection.on("TeamsUpdated", (updated: TeamLiveStats[]) => setTeams(updated));
    connection.on("Top4Updated", (updated: RawTop4Team[]) => setRawTop4(updated));
    connection.on("OverlayConfigChanged", (updated: OverlayConfig) => setConfig(updated));
    connection.on("OverlayEvent", (evt: { type: string; title?: string; subtitle?: string; imageUrl?: string; accentColor?: string }) => {
      const id = `${evt.type}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

      if (evt.type === "elimination") {
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
      const meta = ACHIEVEMENT_LABELS[evt.type];
      const banner: Banner = { id, type: evt.type, title: evt.title ?? meta?.label ?? evt.type, subtitle: evt.subtitle, imageUrl: evt.imageUrl, accentColor: evt.accentColor };
      setAchievementBanner(banner);
      window.setTimeout(() => setAchievementBanner((cur) => (cur?.id === id ? null : cur)), 5500);
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
        if (before && team.eliminations > before.eliminations) {
          const id = `derived-elim-${team.tag}-${Date.now()}`;
          setFeed((f) => [{ id, title: `${team.tag} scored an elimination`, subtitle: `${team.eliminations} total`, receivedAt: Date.now() }, ...f].slice(0, 8));
        }
        if (before && !before.teamEliminated && team.teamEliminated && config.elementVisibility.teamEliminatedBanner) {
          const id = `derived-team-elim-${team.tag}-${Date.now()}`;
          setTeamEliminatedBanner({ id, type: "teamEliminated", title: "TEAM ELIMINATED", subtitle: team.tag });
          window.setTimeout(() => setTeamEliminatedBanner((cur) => (cur?.id === id ? null : cur)), 5000);
        }
      }
    }
    prevTeamsRef.current = teams;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [teams]);

  const sortedTeams = useMemo(() => [...teams].sort((a, b) => a.teamRank - b.teamRank), [teams]);
  const visible = (id: string) => config.elementVisibility[id] ?? true;

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
  const standingsRows: StandingsRow[] = sortedTeams.map((team) => ({
    key: team.tag + team.teamRank,
    rank: team.teamRank,
    name: team.tag,
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
        players: team.playersHealth.slice(0, 4).map((p) => ({ health: p.healthPercent, liveState: p.liveState })),
      }))
    : sortedTeams.filter((t) => !t.teamEliminated).slice(0, 4).map((team) => ({
        key: team.tag + team.teamRank,
        overallRank: team.teamRank,
        tag: team.tag,
        wwcd: NaN,
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
        <div className="overlay-banner-fullscreen" key={teamEliminatedBanner.id}>
          <div className="overlay-banner-fullscreen-inner">
            <div className="eyebrow">TEAM</div>
            <div className="headline">{teamEliminatedBanner.subtitle ?? teamEliminatedBanner.title}</div>
            <div className="eyebrow">ELIMINATED</div>
          </div>
        </div>
      )}

      {achievementBanner && visible(achievementBanner.type) && (
        <div className="overlay-achievement" key={achievementBanner.id} style={achievementBanner.accentColor ? { borderColor: achievementBanner.accentColor } : undefined}>
          {achievementBanner.imageUrl ? (
            <img className="overlay-achievement-photo" src={achievementBanner.imageUrl} alt="" />
          ) : (
            <div className="overlay-achievement-icon">{ACHIEVEMENT_LABELS[achievementBanner.type]?.icon ?? "★"}</div>
          )}
          <div>
            <div className="overlay-achievement-label">{ACHIEVEMENT_LABELS[achievementBanner.type]?.label ?? achievementBanner.title}</div>
            {achievementBanner.subtitle && <div className="overlay-achievement-name">{achievementBanner.subtitle}</div>}
          </div>
        </div>
      )}
    </div>
  );
}
