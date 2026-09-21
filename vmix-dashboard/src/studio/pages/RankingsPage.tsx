import { useState } from "react";
import { Palette, Plus, Rows3, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg, RowRule } from "../theme";
import { BgEditor, ColumnStyle, ColumnStyleEditor, EditorPanel, PageShell, ResetToThemeButton, RowRuleItem, addRowRule, btnGhost, pill } from "../StudioControls";
import { SAMPLE_MATCH_RANKING, SAMPLE_OVERALL_RANKING } from "../sampleData";
import { RankingsRenderer } from "../renderers/RankingsRenderer";

const DEFAULT_COLUMN_STYLES: Record<string, ColumnStyle> = {
  rank: { mode: "default", custom: {} }, teamName: { mode: "default", custom: {} }, stats: { mode: "default", custom: {} }, total: { mode: "default", custom: {} },
};

export default function RankingsPage() {
  const { theme } = useTheme();
  const [view, setView] = useState<"match" | "overall">("overall");
  const [page, setPage] = useState(0);
  const [tab, setTab] = useState("canvas");
  const [canvasBgOverride, setCanvasBgOverride] = useStudioElement<Bg | null>("rankings.canvasBg", null);
  const [headerBgOverride, setHeaderBgOverride] = useStudioElement<Bg | null>("rankings.headerBg", null);
  const [columnStyles, setColumnStyles] = useStudioElement("rankings.columnStyles", DEFAULT_COLUMN_STYLES);
  const [rowRules, setRowRules] = useStudioElement<RowRule[]>("rankings.rowRules", []);
  const [columns, setColumns] = useStudioElement("rankings.columns", { wins: true, placement: true, elim: true });

  const canvasBg = canvasBgOverride || theme.headerBg;
  const headerBg = headerBgOverride || theme.headerBg;
  const setCol = (key: string) => (next: ColumnStyle) => setColumnStyles((prev) => ({ ...prev, [key]: next }));
  const rows = view === "match" ? SAMPLE_MATCH_RANKING : SAMPLE_OVERALL_RANKING;
  const title = view === "match" ? "MATCH RANKING" : "OVERALL RANKING";
  const subtitle = view === "match" ? "SEMIFINALS — MATCH 1" : "SEMIFINALS — 4 MATCHES";

  return (
    <PageShell title="Rankings" subtitle="Match and overall standings, paginated broadcast layout">
      <div style={{ flex: "1 1 500px" }}>
        <div style={{ display: "flex", gap: 6, marginBottom: 12 }}>
          <button onClick={() => { setView("match"); setPage(0); }} style={pill(view === "match")}>Match Ranking</button>
          <button onClick={() => { setView("overall"); setPage(0); }} style={pill(view === "overall")}>Overall Ranking</button>
        </div>
        <div style={{ width: "100%", maxWidth: 640, aspectRatio: "16/9" }}>
          <RankingsRenderer
            theme={theme} canvasBg={canvasBg} headerBg={headerBg}
            title={title} subtitle={subtitle} rows={rows}
            columns={columns} columnStyles={columnStyles} rowRules={rowRules}
            page={page} pager="controls" onPageChange={setPage}
          />
        </div>
      </div>

      <EditorPanel tabs={[{ id: "canvas", label: "Canvas", icon: Palette }, { id: "header", label: "Header", icon: Type }, { id: "columns", label: "Columns", icon: Type }, { id: "rows", label: "Row colors", icon: Rows3 }]} activeTab={tab} setActiveTab={setTab}>
        {tab === "canvas" && (<div><BgEditor bg={canvasBg} setBg={setCanvasBgOverride} defaultGradient={{ type: "gradient", angle: 135, stops: [{ pos: 0, color: "#7B2FF7" }, { pos: 100, color: "#F76B1C" }] }} />{canvasBgOverride && <ResetToThemeButton onClick={() => setCanvasBgOverride(null)} />}</div>)}
        {tab === "header" && (<div><BgEditor bg={headerBg} setBg={setHeaderBgOverride} maxStops={3} />{headerBgOverride && <ResetToThemeButton onClick={() => setHeaderBgOverride(null)} />}</div>)}
        {tab === "columns" && (
          <div>
            {[{ key: "wins", label: "Wins" }, { key: "placement", label: "Placement points" }, { key: "elim", label: "Elimination points" }].map((c) => (
              <label key={c.key} style={{ display: "flex", alignItems: "center", gap: 8, padding: "6px 0", fontSize: 13, cursor: "pointer" }}><input type="checkbox" checked={(columns as any)[c.key]} onChange={(e) => setColumns((prev) => ({ ...prev, [c.key]: e.target.checked }))} />{c.label}</label>
            ))}
            <div style={{ height: 1, background: "rgba(255,255,255,0.08)", margin: "12px 0" }} />
            <ColumnStyleEditor label="Rank number" col={columnStyles.rank} setCol={setCol("rank")} />
            <ColumnStyleEditor label="Team name" col={columnStyles.teamName} setCol={setCol("teamName")} />
            <ColumnStyleEditor label="Stat columns" col={columnStyles.stats} setCol={setCol("stats")} />
            <ColumnStyleEditor label="Total column" col={columnStyles.total} setCol={setCol("total")} />
          </div>
        )}
        {tab === "rows" && (<div>{rowRules.map((rule, i) => (<RowRuleItem key={rule.id} rule={rule} onChange={(next) => setRowRules((prev) => prev.map((r, j) => (j === i ? next : r)))} onRemove={() => setRowRules((prev) => prev.filter((_, j) => j !== i))} />))}<button onClick={() => addRowRule(setRowRules)} style={{ ...btnGhost, width: "100%", display: "flex", alignItems: "center", justifyContent: "center", gap: 6 }}><Plus size={13} /> Add row rule</button></div>)}
      </EditorPanel>
    </PageShell>
  );
}
