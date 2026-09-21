import { useEffect, useRef, useState } from "react";
import type { CSSProperties } from "react";
import { getStudioTheme } from "../studio/configAccess";
import { Bg, DEFAULT_HEALTH_STOPS } from "../studio/theme";
import { GRAPHICS } from "../lib/graphics";
import {
  SAMPLE_STANDINGS, SAMPLE_TOP4, SAMPLE_SIDEBAR_TEAMS, SAMPLE_TOP5_KILLS,
  SAMPLE_HIGHLIGHT_PLAYER, SAMPLE_MVP_ROWS, SAMPLE_TEAMS_TO_WATCH, SAMPLE_CHAMPIONS,
  SAMPLE_HEAD_TO_HEAD, SAMPLE_TEAM_INTRO, SAMPLE_MAP_PLAYERS,
} from "../studio/sampleData";
import { StandingsRenderer, StandingsRow } from "../studio/renderers/StandingsRenderer";
import { Top4Renderer, Top4Team } from "../studio/renderers/Top4Renderer";
import { TopPlayersRenderer, TopPlayerEntry } from "../studio/renderers/TopPlayersRenderer";
import { EliminatedBannerRenderer, SidebarRenderer, SidebarRow } from "../studio/renderers/EliminatedSidebarRenderer";
import { AchievementRenderer, achievementIcon } from "../studio/renderers/AchievementRenderer";
import { CircleStatusRenderer, CirclePhase } from "../studio/renderers/CircleStatusRenderer";
import { PlayerHighlightRenderer } from "../studio/renderers/PlayerHighlightRenderer";
import { MvpRankingsRenderer } from "../studio/renderers/MvpRankingsRenderer";
import { TeamsToWatchRenderer } from "../studio/renderers/TeamsToWatchRenderer";
import { ChampionsRenderer } from "../studio/renderers/ChampionsRenderer";
import { HeadToHeadRenderer } from "../studio/renderers/HeadToHeadRenderer";
import { TeamIntroRenderer } from "../studio/renderers/TeamIntroRenderer";
import { SpectatorMapRenderer } from "../studio/renderers/SpectatorMapRenderer";

// /demo - a click-through preview of every graphic the overlay can show, with no backend and no
// match running. It exists so someone can see (and demo to someone else) what every panel looks
// like without booting FakePcob's serve command or waiting for a real tournament.
//
// Everything here is driven by the Graphics Studio's own SAMPLE_* fixtures (studio/sampleData.ts)
// rather than tools/FakePcob/seed/demo-midmatch.json / demo-last4.json. Per SEEDS.md those two
// fixtures are raw pcob-shaped {"playerInfoList":[...]} records (same 43-field shape as the real
// match seeds), not the TeamLiveStats[]/pre-aggregated shape this page's renderers consume - using
// them here would mean re-implementing the server's own team/rank aggregation (LiveStatsBusiness.cs)
// a second time on the client, only for a demo page, which is exactly the kind of scope creep this
// phase's hard rules (additive, minimal-blast-radius changes) warn against. Task 10 explicitly
// allows the simpler path: "If a renderer's props cannot be produced from this data, render it
// with the Studio's own sample data and note it in REPORT.md" - recorded there under Decisions.
// Every graphic below takes that fallback, not just the ones the task anticipated needing it.
//
// Panel rendering reuses the exact renderer components (and the Task 9 panelOpacity prop) that
// both the Graphics Studio editor and the real /overlay route use - a demo click here shows
// exactly what would be on air, not an approximation of it.

const ACHIEVEMENT_LABELS: Record<string, string> = {
  "achievement.grenadeElim": "GRENADE ELIM",
  "achievement.vehicleKill": "VEHICLE KILL",
  "achievement.airdropLoot": "AIRDROP LOOT",
  "achievement.firstKill": "FIRST KILL",
  "achievement.knockout": "KNOCKOUT",
  "achievement.chickenDinner": "WINNER WINNER CHICKEN DINNER",
};

interface Banner {
  id: string;
  type: string;
  title: string;
  subtitle?: string;
}

interface FeedEntry {
  id: string;
  title: string;
  subtitle?: string;
}

type BgMode = "chromaGreen" | "chromaBlue" | "custom" | "transparent" | "image";

const CHECKERBOARD_BG =
  "repeating-conic-gradient(#2a2a32 0% 25%, #1a1a20 0% 50%) 50% / 24px 24px";

const TOGGLEABLE_GRAPHICS = GRAPHICS.filter(
  (g) => !g.id.startsWith("achievement.") && g.id !== "leaderboard" && g.id !== "top4"
);

export default function DemoPage() {
  const theme = getStudioTheme(undefined);

  // --- Panel opacity (Task 9) -------------------------------------------------------------
  const [opacity, setOpacity] = useState(100);

  // --- Canvas background picker --------------------------------------------------------------
  const [bgMode, setBgMode] = useState<BgMode>("chromaGreen");
  const [customColor, setCustomColor] = useState("#101010");
  const [bgImageUrl, setBgImageUrl] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    return () => {
      if (bgImageUrl) URL.revokeObjectURL(bgImageUrl);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const canvasStyle: CSSProperties =
    bgMode === "chromaGreen" ? { backgroundColor: "#00FF00" } :
    bgMode === "chromaBlue" ? { backgroundColor: "#0047FF" } :
    bgMode === "custom" ? { backgroundColor: customColor } :
    bgMode === "transparent" ? { background: CHECKERBOARD_BG } :
    bgImageUrl ? { backgroundImage: `url(${bgImageUrl})`, backgroundSize: "cover", backgroundPosition: "center" } :
    { background: CHECKERBOARD_BG };

  // --- Which graphics are switched on -----------------------------------------------------
  const [visibility, setVisibility] = useState<Record<string, boolean>>(() => {
    const v: Record<string, boolean> = {};
    for (const g of TOGGLEABLE_GRAPHICS) v[g.id] = true;
    v.spectatorMap = false; // off by default on air too (see Overlay.tsx DEFAULT_CONFIG)
    return v;
  });
  const toggle = (id: string) => setVisibility((prev) => ({ ...prev, [id]: !prev[id] }));

  // --- Simulate elimination: drives the mutually-exclusive Standings <-> Top4/WWCD pair, ---
  // exactly the real ShouldShowTop4Ranking rule from RECON.md #6 (<=4 teams alive shows Top4).
  const [aliveTeams, setAliveTeams] = useState(10);
  const [feed, setFeed] = useState<FeedEntry[]>([
    { id: "seed-1", title: "R3GICIDE scored an elimination", subtitle: "8 total" },
  ]);
  const [teamElim, setTeamElim] = useState<Banner | null>(null);
  const [teamElimVisible, setTeamElimVisible] = useState(false);

  const fireTeamElim = (teamName: string) => {
    const id = `elim-${Date.now()}`;
    setTeamElim({ id, type: "teamEliminated", title: "TEAM ELIMINATED", subtitle: teamName });
    setTeamElimVisible(false);
    window.setTimeout(() => setTeamElimVisible(true), 30);
    window.setTimeout(() => setTeamElim((cur) => (cur?.id === id ? null : cur)), 5000);
  };

  const eliminateNext = () => {
    setAliveTeams((prev) => {
      const next = Math.max(1, prev - 1);
      const eliminated = SAMPLE_STANDINGS.find((t) => t.rank === prev) ?? SAMPLE_STANDINGS[SAMPLE_STANDINGS.length - 1];
      fireTeamElim(eliminated.teamName);
      setFeed((f) => [{ id: `elim-feed-${Date.now()}`, title: `${eliminated.teamName} was eliminated`, subtitle: `${next} teams remain` }, ...f].slice(0, 8));
      return next;
    });
  };
  const resetElimination = () => { setAliveTeams(10); setFeed([{ id: "seed-1", title: "R3GICIDE scored an elimination", subtitle: "8 total" }]); };

  // --- Achievement popup, fireable by click ------------------------------------------------
  const [achievement, setAchievement] = useState<Banner | null>(null);
  const [achievementVisible, setAchievementVisible] = useState(false);
  const fireAchievement = (type: string) => {
    const id = `${type}-${Date.now()}`;
    setAchievement({ id, type, title: ACHIEVEMENT_LABELS[type] ?? type, subtitle: SAMPLE_TOP5_KILLS[0]?.playerName });
    setAchievementVisible(false);
    window.setTimeout(() => setAchievementVisible(true), 30);
    window.setTimeout(() => setAchievement((cur) => (cur?.id === id ? null : cur)), 5500);
  };

  // --- Circle status: a local ticking clock, since nothing broadcasts CircleUpdated yet -----
  // (RECON.md #7) - this demo is the only place today an operator can see this bar move at all.
  const [circleSeconds, setCircleSeconds] = useState(20);
  const [circlePhase, setCirclePhase] = useState<CirclePhase>("waiting");
  const [circleIndex, setCircleIndex] = useState(3);
  const phaseLength = circlePhase === "waiting" ? 20 : 15;
  useEffect(() => {
    if (!visibility.circle) return;
    const t = window.setInterval(() => {
      setCircleSeconds((s) => {
        if (s > 1) return s - 1;
        setCirclePhase((p) => {
          if (p === "waiting") return "closing";
          setCircleIndex((i) => i + 1);
          return "waiting";
        });
        return circlePhase === "waiting" ? 15 : 20;
      });
    }, 1000);
    return () => window.clearInterval(t);
  }, [visibility.circle, circlePhase]);

  // --- Sample-data -> renderer props (same shapes Overlay.tsx builds from live data) --------
  const standingsRows: StandingsRow[] = SAMPLE_STANDINGS.map((t) => ({
    key: t.teamId, rank: t.rank, name: t.teamName, kills: t.kills,
    players: t.players.map((p) => ({ health: p.health, liveState: p.liveState })),
  }));

  const top4Rows: Top4Team[] = SAMPLE_TOP4.map((t, i) => ({
    key: i, overallRank: t.overallRank, tag: t.tag, wwcd: t.wwcd,
    players: t.players.map((p) => ({ health: p.health, liveState: p.liveState })),
  }));

  const sidebarRows: SidebarRow[] = SAMPLE_SIDEBAR_TEAMS.map((t) => ({
    key: t.teamId, rank: t.rank, teamName: t.teamName, points: null, kills: t.kills,
  }));

  const topPlayerEntries: TopPlayerEntry[] = SAMPLE_TOP5_KILLS.map((p) => ({
    rank: p.rank, playerName: p.playerName, value: p.value, statLabel: p.statLabel,
  }));

  const highlightStats = [
    { label: "KILLS", value: SAMPLE_HIGHLIGHT_PLAYER.kills },
    { label: "DAMAGE", value: SAMPLE_HIGHLIGHT_PLAYER.damage },
    { label: "SURVIVAL", value: SAMPLE_HIGHLIGHT_PLAYER.survivalTime },
  ];

  const mvpColumns = { team: true, kills: true, damage: true, assists: false, survival: false, rating: true };
  const wwcdBar: Bg = { type: "gradient", angle: 90, stops: [{ pos: 0, color: "#F5A623" }, { pos: 100, color: "#F76B1C" }] };
  const top4CardBg: Bg = { type: "solid", color: "rgba(10,10,15,0.75)" };
  const topPlayersCardBg: Bg = { type: "solid", color: "#0a0a0a" };
  const mapCanvasBg: Bg = { type: "solid", color: "#101820" };

  const showStandings = aliveTeams > 4;

  return (
    <div style={{ position: "fixed", inset: 0, display: "flex", flexDirection: "column", background: "#0a0a0f", fontFamily: "'Inter', system-ui, sans-serif" }}>
      {/* ---- Canvas: exactly what /overlay would show, at the current demo state ---- */}
      <div style={{ flex: 1, position: "relative", overflow: "hidden", ...canvasStyle }}>
        {showStandings && standingsRows.length > 0 && (
          <div style={{ position: "absolute", top: 48, right: 48, width: 420 }}>
            <StandingsRenderer theme={theme} mode="full" healthStops={DEFAULT_HEALTH_STOPS} columns={{}} rowRules={[]} headerBg={theme.headerBg} rows={standingsRows} maxRows={16} panelOpacity={opacity} />
          </div>
        )}

        {!showStandings && (
          <div style={{ position: "absolute", top: 24, left: "50%", transform: "translateX(-50%)", width: 720 }}>
            <Top4Renderer theme={theme} wwcdBar={wwcdBar} cardBg={top4CardBg} fields={{}} teams={top4Rows} panelOpacity={opacity} />
          </div>
        )}

        {visibility.sidebar && (
          <div style={{ position: "absolute", top: 48, left: 48, width: 260 }}>
            <SidebarRenderer theme={theme} headerBg={theme.headerBg} rows={sidebarRows} maxRows={16} panelOpacity={opacity} />
          </div>
        )}

        {visibility.topPlayers && (
          <div style={{ position: "absolute", bottom: 48, left: "50%", transform: "translateX(-50%)", width: 560, aspectRatio: "16/9" }}>
            <TopPlayersRenderer theme={theme} canvasBg={theme.headerBg} labelBg={theme.headerBg} cardBg={topPlayersCardBg} fields={{}} players={topPlayerEntries} panelOpacity={opacity} />
          </div>
        )}

        {visibility.circle && (
          <div style={{ position: "absolute", top: 24, left: "50%", transform: "translateX(-50%)", width: 520 }}>
            <CircleStatusRenderer theme={theme} barBg={theme.headerBg} circleIndex={circleIndex} phase={circlePhase}
              secondsRemaining={circleSeconds} phaseSeconds={phaseLength}
              label={circlePhase === "closing" ? "ZONE CLOSING" : "NEXT ZONE IN"} panelOpacity={opacity} />
          </div>
        )}

        {visibility.playerHighlight && (
          <div style={{ position: "absolute", bottom: 48, right: 48, width: 560, aspectRatio: "16/9" }}>
            <PlayerHighlightRenderer theme={theme} canvasBg={theme.headerBg} accentBg={theme.headerBg}
              label="MVP OF THE MATCH" playerName={SAMPLE_HIGHLIGHT_PLAYER.playerName} teamName={SAMPLE_HIGHLIGHT_PLAYER.teamName}
              stats={highlightStats} fields={{}} panelOpacity={opacity} />
          </div>
        )}

        {visibility.mvpRankings && (
          <div style={{ position: "absolute", top: "12%", left: "50%", transform: "translateX(-50%)", width: 620, aspectRatio: "16/9" }}>
            <MvpRankingsRenderer theme={theme} canvasBg={theme.headerBg} headerBg={theme.headerBg} title="MVP RANKINGS" subtitle=""
              rows={SAMPLE_MVP_ROWS} columns={mvpColumns} columnStyles={{}} rowRules={[]} panelOpacity={opacity} />
          </div>
        )}

        {visibility.teamsToWatch && (
          <div style={{ position: "absolute", top: "12%", left: "50%", transform: "translateX(-50%)", width: 600, aspectRatio: "16/9" }}>
            <TeamsToWatchRenderer theme={theme} canvasBg={theme.headerBg} accentBg={theme.headerBg}
              title="TEAMS TO WATCH" subtitle="" teams={SAMPLE_TEAMS_TO_WATCH} fields={{}} panelOpacity={opacity} />
          </div>
        )}

        {visibility.champions && (
          <div style={{ position: "absolute", top: "10%", left: "50%", transform: "translateX(-50%)", width: 680, aspectRatio: "16/9" }}>
            <ChampionsRenderer theme={theme} canvasBg={theme.headerBg} accentBg={theme.headerBg} label="CHAMPIONS"
              teamName={SAMPLE_CHAMPIONS.teamName} players={SAMPLE_CHAMPIONS.players} stats={SAMPLE_CHAMPIONS.stats}
              fields={{}} panelOpacity={opacity} />
          </div>
        )}

        {visibility.headToHead && (
          <div style={{ position: "absolute", top: "12%", left: "50%", transform: "translateX(-50%)", width: 600, aspectRatio: "16/9" }}>
            <HeadToHeadRenderer theme={theme} canvasBg={theme.headerBg} accentBg={theme.headerBg}
              title="HEAD TO HEAD" subtitle="" left={SAMPLE_HEAD_TO_HEAD.left} right={SAMPLE_HEAD_TO_HEAD.right}
              stats={SAMPLE_HEAD_TO_HEAD.stats} fields={{}} panelOpacity={opacity} />
          </div>
        )}

        {visibility.teamIntro && (
          <div style={{ position: "absolute", bottom: 48, left: 48, width: 560, aspectRatio: "16/9" }}>
            <TeamIntroRenderer theme={theme} canvasBg={theme.headerBg} accentBg={theme.headerBg} label="TEAM SPOTLIGHT"
              teamName={SAMPLE_TEAM_INTRO.teamName} wwcd={SAMPLE_TEAM_INTRO.wwcd} players={SAMPLE_TEAM_INTRO.players}
              stats={SAMPLE_TEAM_INTRO.stats} fields={{}} panelOpacity={opacity} />
          </div>
        )}

        {visibility.spectatorMap && (
          <div style={{ position: "absolute", bottom: 48, right: 48, width: 320 }}>
            <SpectatorMapRenderer theme={theme} canvasBg={mapCanvasBg} accentBg={theme.headerBg}
              players={SAMPLE_MAP_PLAYERS} />
          </div>
        )}

        {visibility.eliminationFeed && feed.length > 0 && (
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

        {teamElim && visibility.teamEliminatedBanner && (
          <div key={teamElim.id} style={{ position: "absolute", top: "38%", left: "50%", transform: "translateX(-50%)", width: 620 }}>
            <EliminatedBannerRenderer theme={theme} bannerBg={theme.headerBg} teamName={teamElim.subtitle ?? teamElim.title}
              visible={teamElimVisible} panelOpacity={opacity} />
          </div>
        )}

        {achievement && (
          <div key={achievement.id} style={{ position: "absolute", bottom: 48, left: 48, width: 420 }}>
            <AchievementRenderer theme={theme} accentBg={theme.headerBg}
              label={ACHIEVEMENT_LABELS[achievement.type] ?? achievement.title}
              primary={achievement.subtitle ?? achievement.title}
              icon={achievementIcon(achievement.type)} visible={achievementVisible} panelOpacity={opacity} />
          </div>
        )}

        <div style={{ position: "absolute", top: 8, left: "50%", transform: "translateX(-50%)", fontSize: 10, letterSpacing: "0.1em", color: "rgba(255,255,255,0.35)", pointerEvents: "none" }}>
          DEMO PREVIEW - NOT ON AIR
        </div>
      </div>

      {/* ---- Control bar ---- */}
      <div style={{ flex: "0 0 auto", maxHeight: "42vh", overflowY: "auto", background: "#111116", borderTop: "1px solid rgba(255,255,255,0.1)", padding: "14px 20px", color: "#e8e8ec", fontSize: 12.5, display: "flex", flexDirection: "column", gap: 14 }}>
        <div style={{ display: "flex", alignItems: "center", gap: 10, flexWrap: "wrap" }}>
          <strong style={{ fontSize: 13 }}>Demo controls</strong>
          <span style={{ color: "#8a8a94" }}>Every graphic below renders from Studio sample data - no match, no backend needed.</span>
        </div>

        <div>
          <div style={{ color: "#8a8a94", marginBottom: 6 }}>Simulate elimination (drives Standings &lt;-&gt; Top4/WWCD, {aliveTeams} team{aliveTeams === 1 ? "" : "s"} alive)</div>
          <div style={{ display: "flex", gap: 8 }}>
            <button onClick={eliminateNext} disabled={aliveTeams <= 1} style={btn()}>Eliminate next team</button>
            <button onClick={resetElimination} style={btnGhost()}>Reset to 10</button>
            <button onClick={() => fireTeamElim(SAMPLE_STANDINGS[0].teamName)} style={btnGhost()}>Fire elimination banner</button>
          </div>
        </div>

        <div>
          <div style={{ color: "#8a8a94", marginBottom: 6 }}>Graphics</div>
          <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
            {TOGGLEABLE_GRAPHICS.map((g) => (
              <button key={g.id} onClick={() => toggle(g.id)} style={pill(!!visibility[g.id])} title={g.note}>
                {g.label}
              </button>
            ))}
          </div>
        </div>

        <div>
          <div style={{ color: "#8a8a94", marginBottom: 6 }}>Achievements (click to fire)</div>
          <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
            {Object.entries(ACHIEVEMENT_LABELS).map(([type, label]) => (
              <button key={type} onClick={() => fireAchievement(type)} style={btnGhost()}>{label}</button>
            ))}
          </div>
        </div>

        <div style={{ display: "flex", gap: 24, flexWrap: "wrap" }}>
          <div>
            <div style={{ color: "#8a8a94", marginBottom: 6 }}>Panel opacity ({opacity}%)</div>
            <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
              <input type="range" min={0} max={100} value={opacity} onChange={(e) => setOpacity(Number(e.target.value))} style={{ width: 180 }} />
              <button onClick={() => setOpacity(0)} style={btnGhost()}>All -&gt; 0%</button>
              <button onClick={() => setOpacity(100)} style={btnGhost()}>Reset</button>
            </div>
          </div>

          <div>
            <div style={{ color: "#8a8a94", marginBottom: 6 }}>Canvas background</div>
            <div style={{ display: "flex", alignItems: "center", gap: 6, flexWrap: "wrap" }}>
              <button onClick={() => setBgMode("chromaGreen")} style={pill(bgMode === "chromaGreen")}>Chroma green</button>
              <button onClick={() => setBgMode("chromaBlue")} style={pill(bgMode === "chromaBlue")}>Chroma blue</button>
              <button onClick={() => setBgMode("transparent")} style={pill(bgMode === "transparent")}>Transparent</button>
              <button onClick={() => setBgMode("custom")} style={pill(bgMode === "custom")}>Custom</button>
              {bgMode === "custom" && (
                <input type="color" value={customColor} onChange={(e) => setCustomColor(e.target.value)} style={{ width: 32, height: 24, padding: 0, border: "none", background: "none" }} />
              )}
              <button onClick={() => { setBgMode("image"); fileInputRef.current?.click(); }} style={pill(bgMode === "image")}>Image...</button>
              <input ref={fileInputRef} type="file" accept="image/*" style={{ display: "none" }}
                onChange={(e) => {
                  const file = e.target.files?.[0];
                  if (!file) return;
                  if (bgImageUrl) URL.revokeObjectURL(bgImageUrl);
                  setBgImageUrl(URL.createObjectURL(file));
                }} />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}

function btn(): CSSProperties {
  return { padding: "6px 14px", borderRadius: 8, border: "none", background: "#F4C430", color: "#0a0a0f", fontWeight: 700, fontSize: 12, cursor: "pointer" };
}
function btnGhost(): CSSProperties {
  return { padding: "6px 12px", borderRadius: 8, border: "1px solid rgba(255,255,255,0.15)", background: "transparent", color: "#e8e8ec", fontSize: 11.5, cursor: "pointer" };
}
function pill(active: boolean): CSSProperties {
  return {
    padding: "5px 12px", borderRadius: 999, border: "1px solid " + (active ? "#F4C430" : "rgba(255,255,255,0.15)"),
    background: active ? "rgba(244,196,48,0.15)" : "transparent", color: active ? "#F4C430" : "#c8c8d0",
    fontSize: 11.5, cursor: "pointer", fontWeight: active ? 600 : 400,
  };
}
