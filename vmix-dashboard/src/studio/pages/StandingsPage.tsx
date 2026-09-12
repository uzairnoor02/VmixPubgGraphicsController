import { useState } from "react";
import { Eye, Palette, Plus, Rows3, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg, DEFAULT_HEALTH_STOPS, HealthStop, RowRule, bgCss, colorForPlayer, resolveRowBg } from "../theme";
import { BgEditor, ColumnStyleEditor, ColumnStyle, EditorPanel, PageShell, ResetToThemeButton, RowRuleItem, addRowRule, btnGhost, pill } from "../StudioControls";
import { SAMPLE_STANDINGS } from "../sampleData";
import { HealthGradientEditor } from "../HealthGradientEditor";

const DEFAULT_COLUMNS: Record<string, ColumnStyle> = {
  rank: { mode: "default", custom: {} },
  logo: { mode: "default", custom: { scale: 1 } },
  teamName: { mode: "default", custom: {} },
  kills: { mode: "default", custom: {} },
};

export default function StandingsPage() {
  const { theme } = useTheme();
  const [mode, setMode] = useState<"full" | "top4">("full");
  const [healthStops, setHealthStops] = useStudioElement<HealthStop[]>("standings.healthStops", DEFAULT_HEALTH_STOPS);
  const [columns, setColumns] = useStudioElement("standings.columns", DEFAULT_COLUMNS);
  const [rowRules, setRowRules] = useStudioElement<RowRule[]>("standings.rowRules", []);
  const [headerBgOverride, setHeaderBgOverride] = useStudioElement<Bg | null>("standings.headerBg", null);
  const [tab, setTab] = useState("header");

  const headerBg = headerBgOverride || theme.headerBg;
  const setCol = (key: string) => (next: ColumnStyle) => setColumns((prev) => ({ ...prev, [key]: next }));
  const visibleTeams = mode === "top4" ? SAMPLE_STANDINGS.filter((t) => t.rank <= 4) : SAMPLE_STANDINGS;
  const col = (key: string) => columns[key]?.mode === "custom" ? columns[key].custom : ({} as any);
  const logoScale = columns.logo?.mode === "custom" ? (columns.logo.custom.scale ?? 1) : 1;

  return (
    <PageShell title="Standings" subtitle="Live team standings with per-player health bars">
      <div style={{ flex: "1 1 420px" }}>
        <div style={{ display: "flex", alignItems: "center", gap: 10, marginBottom: 12 }}>
          <Eye size={13} color="#8a8a94" /><span style={{ fontSize: 12, color: "#8a8a94" }}>Preview (sample data)</span>
          <div style={{ marginLeft: "auto", display: "flex", gap: 6 }}>
            <button onClick={() => setMode("full")} style={{ ...pill(mode === "full"), padding: "4px 10px", fontSize: 11 }}>Full</button>
            <button onClick={() => setMode("top4")} style={{ ...pill(mode === "top4"), padding: "4px 10px", fontSize: 11 }}>Top 4</button>
          </div>
        </div>
        <div style={{ width: "100%", maxWidth: 460, borderRadius: theme.radius + 4, overflow: "hidden", background: theme.chromaKey, padding: 14 }}>
          <div style={{ borderRadius: theme.radius, overflow: "hidden", background: theme.panelBg, backdropFilter: `blur(${theme.panelBlur})`, WebkitBackdropFilter: `blur(${theme.panelBlur})`, border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow } as any}>
            <div style={{ display: "flex", alignItems: "center", padding: "11px 16px", background: bgCss(headerBg), fontSize: 12.5, fontWeight: 700, color: theme.headerTextColor, fontFamily: theme.fontDisplay, letterSpacing: 0.6 }}>
              <div style={{ width: 26 }}>#</div><div style={{ width: 22 * logoScale + 8 }} /><div style={{ flex: 1 }}>TEAM</div><div style={{ width: 64, textAlign: "center" }}>ALIVE</div><div style={{ width: 40, textAlign: "center" }}>ELIMS</div>
            </div>
            {visibleTeams.slice(0, 10).map((team, i) => {
              const ruleBg = resolveRowBg(rowRules, team.rank);
              const rankStyle = col("rank"), nameStyle = col("teamName"), killStyle = col("kills");
              return (
                <div key={team.teamId} style={{ display: "flex", alignItems: "center", padding: "8px 16px", background: (ruleBg ? bgCss(ruleBg) : (i % 2 === 0 ? theme.rowBgEven : theme.rowBgOdd)), borderTop: "1px solid rgba(255,255,255,0.05)" }}>
                  <div style={{ width: 26, fontWeight: 800, fontFamily: rankStyle.fontFamily || theme.fontDisplay, fontSize: rankStyle.fontSize ? `${rankStyle.fontSize}px` : "16px", color: rankStyle.color || theme.textPrimary }}>{team.rank}</div>
                  <div style={{ width: 22 * logoScale + 8, display: "flex", alignItems: "center" }}><div style={{ width: 20 * logoScale, height: 20 * logoScale, borderRadius: 4 * logoScale, background: "rgba(255,255,255,0.15)", flexShrink: 0 }} /></div>
                  <div style={{ flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", paddingRight: 8, fontFamily: nameStyle.fontFamily || theme.fontBody, fontSize: nameStyle.fontSize ? `${nameStyle.fontSize}px` : "13.5px", fontWeight: 600, color: nameStyle.color || theme.textPrimary }}>{team.teamName}</div>
                  <div style={{ width: 64, display: "flex", gap: 3, justifyContent: "center" }}>{team.players.map((p, j) => { const { color, pulse } = colorForPlayer(healthStops, p); return <div key={j} style={{ width: 9, height: 17, borderRadius: 3, background: color, animation: pulse ? "sb-pulse 1s ease-in-out infinite" : "none", opacity: p.liveState === 5 ? 0.5 : 1 }} />; })}</div>
                  <div style={{ width: 40, textAlign: "center", fontWeight: 700, fontFamily: killStyle.fontFamily || theme.fontBody, fontSize: killStyle.fontSize ? `${killStyle.fontSize}px` : "13px", color: killStyle.color || theme.textPrimary }}>{team.kills}</div>
                </div>
              );
            })}
          </div>
          <style>{`@keyframes sb-pulse { 0%,100%{opacity:1} 50%{opacity:.35} }`}</style>
        </div>
      </div>

      <EditorPanel tabs={[{ id: "header", label: "Header", icon: Type }, { id: "columns", label: "Columns", icon: Type }, { id: "rows", label: "Row colors", icon: Rows3 }, { id: "health", label: "Health", icon: Palette }]} activeTab={tab} setActiveTab={setTab}>
        {tab === "header" && (
          <div>
            <BgEditor bg={headerBg} setBg={setHeaderBgOverride} maxStops={3} />
            {headerBgOverride && <ResetToThemeButton onClick={() => setHeaderBgOverride(null)} />}
          </div>
        )}
        {tab === "columns" && (
          <div>
            <ColumnStyleEditor label="Rank number" col={columns.rank} setCol={setCol("rank")} />
            <ColumnStyleEditor label="Team name" col={columns.teamName} setCol={setCol("teamName")} />
            <ColumnStyleEditor label="Elims stat" col={columns.kills} setCol={setCol("kills")} />
            <div style={{ background: "rgba(255,255,255,0.03)", borderRadius: 10, padding: 10 }}>
              <div style={{ fontSize: 12, color: "#d8d8e0", marginBottom: 8 }}>Team logo size</div>
              <div style={{ display: "flex", alignItems: "center", gap: 4, marginBottom: 8 }}>
                <button onClick={() => setCol("logo")({ mode: "default", custom: columns.logo.custom })} style={{ ...pill(columns.logo.mode === "default"), padding: "3px 10px", fontSize: 10.5 }}>Default</button>
                <button onClick={() => setCol("logo")({ mode: "custom", custom: columns.logo.custom })} style={{ ...pill(columns.logo.mode === "custom"), padding: "3px 10px", fontSize: 10.5 }}>Custom</button>
              </div>
              {columns.logo.mode === "custom" && (
                <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                  <input type="range" min={0.5} max={2} step={0.1} value={columns.logo.custom.scale ?? 1} onChange={(e) => setCol("logo")({ mode: "custom", custom: { scale: Number(e.target.value) } })} style={{ flex: 1 }} />
                  <span style={{ fontSize: 11, color: "#c8c8d0" }}>{(columns.logo.custom.scale ?? 1).toFixed(1)}x</span>
                </div>
              )}
            </div>
          </div>
        )}
        {tab === "rows" && (
          <div>
            <div style={{ fontSize: 12, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>Highlight any rank range. Later rules override earlier ones where they overlap.</div>
            {rowRules.map((rule, i) => (<RowRuleItem key={rule.id} rule={rule} onChange={(next) => setRowRules((prev) => prev.map((r, j) => (j === i ? next : r)))} onRemove={() => setRowRules((prev) => prev.filter((_, j) => j !== i))} />))}
            <button onClick={() => addRowRule(setRowRules)} style={{ ...btnGhost, width: "100%", display: "flex", alignItems: "center", justifyContent: "center", gap: 6 }}><Plus size={13} /> Add row rule</button>
          </div>
        )}
        {tab === "health" && <HealthGradientEditor stops={healthStops} setStops={setHealthStops} />}
      </EditorPanel>
    </PageShell>
  );
}
