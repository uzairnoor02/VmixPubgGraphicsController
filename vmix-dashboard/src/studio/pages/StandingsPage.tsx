import { useState } from "react";
import { Eye, Palette, Plus, Rows3, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg, DEFAULT_HEALTH_STOPS, HealthStop, RowRule } from "../theme";
import { BgEditor, ColumnStyleEditor, ColumnStyle, EditorPanel, PageShell, ResetToThemeButton, RowRuleItem, addRowRule, btnGhost, pill } from "../StudioControls";
import { SAMPLE_STANDINGS } from "../sampleData";
import { HealthGradientEditor } from "../HealthGradientEditor";
import { StandingsRenderer } from "../renderers/StandingsRenderer";

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
  const rows = SAMPLE_STANDINGS.map((t) => ({ key: t.teamId, rank: t.rank, name: t.teamName, kills: t.kills, players: t.players }));

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
          <StandingsRenderer theme={theme} mode={mode} healthStops={healthStops} columns={columns} rowRules={rowRules} headerBg={headerBg} rows={rows} />
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
