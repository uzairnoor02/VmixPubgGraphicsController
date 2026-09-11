import { useEffect, useMemo, useRef, useState } from "react";
import * as signalR from "@microsoft/signalr";
import { API_BASE, api } from "./lib/api";
import type { OverlayConfig } from "./lib/api";
import type { TeamLiveStats } from "./types";

// This is the page you paste into vMix as a Web Browser source - it has no login, no nav, no
// buttons, nothing but the graphics themselves on a solid chroma-key background. Everything about
// how it looks (chroma color, which panels are visible) comes from OverlayConfig, set from the
// Overlay Settings tab and pushed here live over SignalR so a change goes out on-air with zero
// manual steps on the graphics PC - no re-uploading a title, no touching vMix at all.
//
// One honest limitation, on purpose: the WinForms app's TeamLiveStats.Player1-4Health fields are
// image *file paths* it swaps into vMix's native Title graphics (see EvaluateLiveStatus in
// LiveStatsBusiness.cs), not a plain alive/knocked/dead number - so a browser can't render a
// correct "players alive" count from them without one small backend addition (exposing the
// existing numeric LiveState alongside the image path). Left out here rather than guessed at.

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

export default function Overlay() {
  const [config, setConfig] = useState<OverlayConfig>(DEFAULT_CONFIG);
  const [teams, setTeams] = useState<TeamLiveStats[]>([]);
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

  return (
    <div className="overlay-root" style={{ backgroundColor: config.chromaKeyColor }}>
      {visible("leaderboard") && sortedTeams.length > 0 && (
        <div className="overlay-leaderboard">
          <div className="overlay-leaderboard-header">
            <span>#</span>
            <span>TEAM</span>
            <span>ELIMS</span>
            <span>PTS</span>
          </div>
          {sortedTeams.map((team) => (
            <div key={team.tag + team.teamRank} className={`overlay-leaderboard-row ${team.teamEliminated ? "is-eliminated" : ""}`}>
              <span className="rank">{team.teamRank}</span>
              <span className="tag">{team.tag}</span>
              <span className="elims">{team.eliminations}</span>
              <span className="pts">{team.totalPoints}</span>
            </div>
          ))}
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
