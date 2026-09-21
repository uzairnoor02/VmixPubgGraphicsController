import { useState } from "react";
import { Palette, Plus, Rows3, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg, RowRule } from "../theme";
import { BgEditor, ColumnStyle, ColumnStyleEditor, EditorPanel, PageShell, ResetToThemeButton, RowRuleItem, addRowRule, btnGhost } from "../StudioControls";
import { MvpColumns, MvpRankingsRenderer } from "../renderers/MvpRankingsRenderer";
import { SAMPLE_MVP_ROWS } from "../sampleData";

const DEFAULT_COLUMN_STYLES: Record<string, ColumnStyle> = {
  rank: { mode: "default", custom: {} }, playerName: { mode: "default", custom: {} }, stats: { mode: "default", custom: {} },
};

const DEFAULT_COLUMNS: MvpColumns = { team: true, kills: true, damage: true, assists: false, survival: false, rating: true };

export default function MvpRankingsPage() {
  const { theme } = useTheme();
  const [tab, setTab] = useState("canvas");
  const [canvasBgOverride, setCanvasBgOverride] = useStudioElement<Bg | null>("mvp.canvasBg", null);
  const [headerBgOverride, setHeaderBgOverride] = useStudioElement<Bg | null>("mvp.headerBg", null);
  const [columnStyles, setColumnStyles] = useStudioElement("mvp.columnStyles", DEFAULT_COLUMN_STYLES);
  const [rowRules, setRowRules] = useStudioElement<RowRule[]>("mvp.rowRules", []);
  const [columns, setColumns] = useStudioElement<MvpColumns>("mvp.columns", DEFAULT_COLUMNS);
  const [title, setTitle] = useStudioElement("mvp.title", "MVP RANKINGS");
  const [subtitle, setSubtitle] = useStudioElement("mvp.subtitle", "SEMIFINALS — TOP 5");

  const canvasBg = canvasBgOverride || theme.headerBg;
  const headerBg = headerBgOverride || theme.headerBg;
  const setCol = (key: string) => (next: ColumnStyle) => setColumnStyles((prev) => ({ ...prev, [key]: next }));

  return (
    <PageShell title="MVP Rankings" subtitle="Ranked player table with configurable stat columns">
      <div style={{ flex: "1 1 520px" }}>
        <div style={{ width: "100%", maxWidth: 620, aspectRatio: "16/9" }}>
          <MvpRankingsRenderer theme={theme} canvasBg={canvasBg} headerBg={headerBg} title={title} subtitle={subtitle} rows={SAMPLE_MVP_ROWS} columns={columns} columnStyles={columnStyles} rowRules={rowRules} />
        </div>
        <div style={{ marginTop: 14, padding: 12, borderRadius: 8, fontSize: 12, lineHeight: 1.5, background: "rgba(241,196,15,0.1)", border: "1px solid rgba(241,196,15,0.3)", color: "#f5d76e" }}>
          <b>Needs a backend feed:</b> PostMatch.Top5MVP.cs computes these numbers; nothing
          broadcasts them yet. The overlay slot listens for <code>MvpRankingsUpdated</code>. Assists
          and survival read 0 until a match ends, so this belongs in the post-match segment.
        </div>
      </div>

      <EditorPanel tabs={[{ id: "canvas", label: "Canvas", icon: Palette }, { id: "header", label: "Header", icon: Type }, { id: "columns", label: "Columns", icon: Type }, { id: "rows", label: "Row colors", icon: Rows3 }, { id: "text", label: "Text", icon: Type }]} activeTab={tab} setActiveTab={setTab}>
        {tab === "canvas" && (<div><BgEditor bg={canvasBg} setBg={setCanvasBgOverride} />{canvasBgOverride && <ResetToThemeButton onClick={() => setCanvasBgOverride(null)} />}</div>)}
        {tab === "header" && (<div><BgEditor bg={headerBg} setBg={setHeaderBgOverride} maxStops={3} />{headerBgOverride && <ResetToThemeButton onClick={() => setHeaderBgOverride(null)} />}</div>)}
        {tab === "columns" && (
          <div>
            {([
              { key: "team", label: "Team" }, { key: "kills", label: "Eliminations" }, { key: "damage", label: "Damage" },
              { key: "assists", label: "Assists" }, { key: "survival", label: "Survival time" }, { key: "rating", label: "Rating" },
            ] as const).map((c) => (
              <label key={c.key} style={{ display: "flex", alignItems: "center", gap: 8, padding: "6px 0", fontSize: 13, cursor: "pointer" }}>
                <input type="checkbox" checked={columns[c.key]} onChange={(e) => setColumns((prev) => ({ ...prev, [c.key]: e.target.checked }))} />{c.label}
              </label>
            ))}
            <div style={{ height: 1, background: "rgba(255,255,255,0.08)", margin: "12px 0" }} />
            <ColumnStyleEditor label="Rank number" col={columnStyles.rank} setCol={setCol("rank")} />
            <ColumnStyleEditor label="Player name" col={columnStyles.playerName} setCol={setCol("playerName")} />
            <ColumnStyleEditor label="Stat columns" col={columnStyles.stats} setCol={setCol("stats")} />
          </div>
        )}
        {tab === "rows" && (<div>{rowRules.map((rule, i) => (<RowRuleItem key={rule.id} rule={rule} onChange={(next) => setRowRules((prev) => prev.map((r, j) => (j === i ? next : r)))} onRemove={() => setRowRules((prev) => prev.filter((_, j) => j !== i))} />))}<button onClick={() => addRowRule(setRowRules)} style={{ ...btnGhost, width: "100%", display: "flex", alignItems: "center", justifyContent: "center", gap: 6 }}><Plus size={13} /> Add row rule</button></div>)}
        {tab === "text" && (
          <div>
            {[{ label: "Title", value: title, set: setTitle }, { label: "Subtitle", value: subtitle, set: setSubtitle }].map((row) => (
              <label key={row.label} style={{ display: "block", marginBottom: 12, fontSize: 12, color: "#c8c8d0" }}>
                {row.label}
                <input value={row.value} onChange={(e) => row.set(e.target.value)} style={{ width: "100%", marginTop: 5, background: "rgba(255,255,255,0.06)", border: "1px solid rgba(255,255,255,0.12)", borderRadius: 6, color: "#e8e8ec", padding: "7px 9px", fontSize: 12 }} />
              </label>
            ))}
          </div>
        )}
      </EditorPanel>
    </PageShell>
  );
}
