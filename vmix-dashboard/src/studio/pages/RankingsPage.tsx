import { useState } from "react";
import { ChevronLeft, ChevronRight, Palette, Plus, Rows3, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg, RowRule, bgCss, resolveRowBg } from "../theme";
import { BgEditor, ColumnStyle, ColumnStyleEditor, EditorPanel, PageShell, ResetToThemeButton, RowRuleItem, addRowRule, btnGhost, pill } from "../StudioControls";
import { ROWS_PER_PAGE, SAMPLE_MATCH_RANKING, SAMPLE_OVERALL_RANKING } from "../sampleData";

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
  const totalPages = Math.ceil(rows.length / ROWS_PER_PAGE);
  const pageRows = rows.slice(page * ROWS_PER_PAGE, page * ROWS_PER_PAGE + ROWS_PER_PAGE);
  const col = (key: string) => columnStyles[key]?.mode === "custom" ? columnStyles[key].custom : ({} as any);

  return (
    <PageShell title="Rankings" subtitle="Match and overall standings, paginated broadcast layout">
      <div style={{ flex: "1 1 500px" }}>
        <div style={{ display: "flex", gap: 6, marginBottom: 12 }}>
          <button onClick={() => { setView("match"); setPage(0); }} style={pill(view === "match")}>Match Ranking</button>
          <button onClick={() => { setView("overall"); setPage(0); }} style={pill(view === "overall")}>Overall Ranking</button>
        </div>
        <div style={{ width: "100%", maxWidth: 640, aspectRatio: "16/9", borderRadius: theme.radius, overflow: "hidden", position: "relative", background: bgCss(canvasBg), fontFamily: theme.fontDisplay, display: "flex", flexDirection: "column", border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow } as any}>
          <div style={{ padding: "18px 24px 10px" }}>
            <div style={{ fontSize: 32, fontWeight: 800, color: theme.textPrimary, letterSpacing: 0.5, lineHeight: 1 }}>{title}</div>
            <div style={{ display: "flex", gap: 10, marginTop: 8 }}><div style={{ background: "rgba(255,255,255,0.15)", color: theme.textPrimary, fontSize: 12, fontWeight: 700, padding: "3px 14px", clipPath: "polygon(0 0, 100% 0, 92% 100%, 0% 100%)" }}>{subtitle}</div></div>
          </div>
          <div style={{ flex: 1, padding: "0 24px 18px", display: "flex" }}>
            <div style={{ flex: 1, background: theme.panelBg, backdropFilter: `blur(${theme.panelBlur})`, WebkitBackdropFilter: `blur(${theme.panelBlur})`, borderRadius: 8, overflow: "hidden", fontSize: 12, alignSelf: "flex-start" } as any}>
              <div style={{ display: "flex", background: bgCss(headerBg), fontWeight: 700, padding: "7px 12px", color: theme.headerTextColor, fontFamily: theme.fontDisplay, fontSize: "12px" }}>
                <div style={{ width: 24 }}>#</div><div style={{ flex: 1 }}>TEAM</div>
                {columns.wins && <div style={{ width: 44, textAlign: "center" }}>WINS</div>}
                {columns.placement && <div style={{ width: 64, textAlign: "center" }}>PLACEMENT</div>}
                {columns.elim && <div style={{ width: 48, textAlign: "center" }}>ELIM.</div>}
                <div style={{ width: 48, textAlign: "center" }}>TOTAL</div>
              </div>
              {pageRows.map((row, i) => {
                const ruleBg = resolveRowBg(rowRules, row.rank);
                const defaultAlt = i % 2 === 0 ? theme.rowBgEven : theme.rowBgOdd;
                const rankStyle = col("rank"), nameStyle = col("teamName"), statStyle = col("stats"), totalStyle = col("total");
                return (
                  <div key={row.rank} style={{ display: "flex", alignItems: "center", padding: "6px 12px", background: ruleBg ? bgCss(ruleBg) : defaultAlt, fontWeight: 600 }}>
                    <div style={{ width: 24, fontWeight: 800, fontFamily: rankStyle.fontFamily || theme.fontDisplay, fontSize: rankStyle.fontSize ? `${rankStyle.fontSize}px` : "13px", color: rankStyle.color || theme.textPrimary }}>{row.rank}</div>
                    <div style={{ flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", fontFamily: nameStyle.fontFamily || theme.fontBody, fontSize: nameStyle.fontSize ? `${nameStyle.fontSize}px` : "12.5px", color: nameStyle.color || theme.textPrimary }}>{row.teamName}</div>
                    {columns.wins && <div style={{ width: 44, textAlign: "center", fontFamily: statStyle.fontFamily || theme.fontBody, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "12px", color: statStyle.color || theme.textPrimary }}>{row.wins ?? "-"}</div>}
                    {columns.placement && <div style={{ width: 64, textAlign: "center", fontFamily: statStyle.fontFamily || theme.fontBody, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "12px", color: statStyle.color || theme.textPrimary }}>{row.placementPts}</div>}
                    {columns.elim && <div style={{ width: 48, textAlign: "center", fontFamily: statStyle.fontFamily || theme.fontBody, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : "12px", color: statStyle.color || theme.textPrimary }}>{row.elimPts}</div>}
                    <div style={{ width: 48, textAlign: "center", fontWeight: 800, fontFamily: totalStyle.fontFamily || theme.fontBody, fontSize: totalStyle.fontSize ? `${totalStyle.fontSize}px` : "13px", color: totalStyle.color || theme.textPrimary }}>{row.total}</div>
                  </div>
                );
              })}
            </div>
          </div>
          {totalPages > 1 && (
            <div style={{ position: "absolute", bottom: 10, right: 16, display: "flex", alignItems: "center", gap: 6, background: "rgba(0,0,0,0.4)", borderRadius: 6, padding: "3px 8px" }}>
              <button onClick={() => setPage((p) => Math.max(0, p - 1))} disabled={page === 0} style={{ background: "none", border: "none", color: page === 0 ? "#666" : "#fff", cursor: page === 0 ? "default" : "pointer", display: "flex" }}><ChevronLeft size={14} /></button>
              <span style={{ fontSize: 11, color: "#fff", fontWeight: 700 }}>{page + 1} / {totalPages}</span>
              <button onClick={() => setPage((p) => Math.min(totalPages - 1, p + 1))} disabled={page === totalPages - 1} style={{ background: "none", border: "none", color: page === totalPages - 1 ? "#666" : "#fff", cursor: page === totalPages - 1 ? "default" : "pointer", display: "flex" }}><ChevronRight size={14} /></button>
            </div>
          )}
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
