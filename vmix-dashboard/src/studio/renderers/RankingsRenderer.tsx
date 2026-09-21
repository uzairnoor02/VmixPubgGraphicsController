import { ChevronLeft, ChevronRight } from "lucide-react";
import { Bg, RowRule, Theme, bgCss, resolveRowBg } from "../theme";
import type { ColumnStyle } from "../StudioControls";

// The Match/Overall Ranking visual - extracted out of RankingsPage.tsx so the Studio preview and
// the real /overlay route render from exactly the same component, the same way StandingsRenderer
// already does. A Studio edit and what's on air can never drift apart because there is only one
// render path, not a "preview approximation" of it.
//
// Sizing is deliberately the caller's job: this fills 100% of whatever box it's given. The Studio
// puts it in a 16/9 preview card; the overlay gives it the real broadcast dimensions.

export interface RankingRow {
  rank: number;
  teamName: string;
  wins?: number;
  placementPts: number;
  elimPts: number;
  total: number;
}

export interface RankingColumns {
  wins: boolean;
  placement: boolean;
  elim: boolean;
}

export interface RankingsRendererProps {
  theme: Theme;
  canvasBg: Bg;
  headerBg: Bg;
  title: string;
  subtitle: string;
  rows: RankingRow[];
  columns: RankingColumns;
  columnStyles: Record<string, ColumnStyle>;
  rowRules: RowRule[];
  page: number;
  /** "controls" gives clickable arrows (Studio); "indicator" shows the page number only, which is
   *  what belongs on air where nobody can click; "none" hides it entirely. */
  pager?: "controls" | "indicator" | "none";
  onPageChange?: (page: number) => void;
}

// Rankings always run as exactly TWO pages, split as evenly as possible, with any odd row on
// page 1: 16 teams -> 8/8, 17 -> 9/8, 15 -> 8/7. A fixed rows-per-page would instead give 16
// teams two pages but 17 teams three, the last holding a single row - which looks broken on air
// and makes the segment length unpredictable for the director.
export const RANKING_PAGE_COUNT = 2;

/** Rows on page 1; page 2 takes the rest. */
export function rankingRowsOnFirstPage(rowCount: number): number {
  return Math.ceil(rowCount / RANKING_PAGE_COUNT);
}

/** Always 2 once there is anything at all to show, so the pager reads "1 / 2" consistently.
 *  A roster small enough to fit one page is still shown as two, the second simply empty -
 *  except when there are no rows at all, where one empty page is the honest answer. */
export function totalRankingPages(rowCount: number): number {
  return rowCount <= 1 ? 1 : RANKING_PAGE_COUNT;
}

/** The slice for a given zero-based page. */
export function rankingPageRows<T>(rows: T[], page: number): T[] {
  const firstPageSize = rankingRowsOnFirstPage(rows.length);
  return page <= 0 ? rows.slice(0, firstPageSize) : rows.slice(firstPageSize);
}

export function RankingsRenderer({
  theme, canvasBg, headerBg, title, subtitle, rows, columns, columnStyles, rowRules,
  page, pager = "indicator", onPageChange,
}: RankingsRendererProps) {
  const totalPages = totalRankingPages(rows.length);
  // Clamp rather than trust the caller: the overlay's auto-advance timer and a live roster that
  // shrinks mid-match can otherwise land on a page that no longer exists, blanking the graphic.
  const safePage = Math.min(Math.max(0, page), totalPages - 1);
  const pageRows = rankingPageRows(rows, safePage);
  const col = (key: string) => (columnStyles[key]?.mode === "custom" ? columnStyles[key].custom : ({} as any));

  // A page now holds half the field rather than a fixed 8, so row count tracks the roster
  // (16 teams -> 8 a page, 25 -> 13). Density adapts so the table still fits a 16/9 frame rather
  // than growing past it - a broadcast graphic must never overflow the box it was given.
  const density = pageRows.length > 11 ? "tight" : pageRows.length > 8 ? "compact" : "normal";
  const rowPadY = density === "tight" ? 1.5 : density === "compact" ? 3.5 : 6;
  const rowFont = density === "tight" ? 10.5 : density === "compact" ? 11.75 : 12.5;
  const titleFont = density === "tight" ? 23 : density === "compact" ? 28 : 32;

  return (
    <div style={{ width: "100%", height: "100%", borderRadius: theme.radius, overflow: "hidden", position: "relative", background: bgCss(canvasBg), fontFamily: theme.fontDisplay, display: "flex", flexDirection: "column", border: `1px solid ${theme.panelBorder}`, boxShadow: theme.glow } as any}>
      <div style={{ padding: density === "normal" ? "18px 24px 10px" : "10px 24px 6px" }}>
        <div style={{ fontSize: titleFont, fontWeight: 800, color: theme.textPrimary, letterSpacing: 0.5, lineHeight: 1 }}>{title}</div>
        <div style={{ display: "flex", gap: 10, marginTop: 8 }}>
          <div style={{ background: "rgba(255,255,255,0.15)", color: theme.textPrimary, fontSize: 12, fontWeight: 700, padding: "3px 14px", clipPath: "polygon(0 0, 100% 0, 92% 100%, 0% 100%)" }}>{subtitle}</div>
        </div>
      </div>

      <div style={{ flex: 1, padding: density === "normal" ? "0 24px 18px" : "0 24px 12px", display: "flex" }}>
        <div style={{ flex: 1, background: theme.panelBg, backdropFilter: `blur(${theme.panelBlur})`, WebkitBackdropFilter: `blur(${theme.panelBlur})`, borderRadius: 8, overflow: "hidden", fontSize: 12, alignSelf: "flex-start" } as any}>
          <div style={{ display: "flex", background: bgCss(headerBg), fontWeight: 700, padding: density === "normal" ? "7px 12px" : "5px 12px", color: theme.headerTextColor, fontFamily: theme.fontDisplay, fontSize: "12px" }}>
            <div style={{ width: 24 }}>#</div>
            <div style={{ flex: 1 }}>TEAM</div>
            {columns.wins && <div style={{ width: 44, textAlign: "center" }}>WINS</div>}
            {columns.placement && <div style={{ width: 82, textAlign: "center" }}>PLACEMENT</div>}
            {columns.elim && <div style={{ width: 48, textAlign: "center" }}>ELIM.</div>}
            <div style={{ width: 48, textAlign: "center" }}>TOTAL</div>
          </div>
          {pageRows.map((row, i) => {
            const ruleBg = resolveRowBg(rowRules, row.rank);
            const defaultAlt = i % 2 === 0 ? theme.rowBgEven : theme.rowBgOdd;
            const rankStyle = col("rank"), nameStyle = col("teamName"), statStyle = col("stats"), totalStyle = col("total");
            return (
              <div key={row.rank} style={{ display: "flex", alignItems: "center", padding: `${rowPadY}px 12px`, background: ruleBg ? bgCss(ruleBg) : defaultAlt, fontWeight: 600 }}>
                <div style={{ width: 24, fontWeight: 800, fontFamily: rankStyle.fontFamily || theme.fontDisplay, fontSize: rankStyle.fontSize ? `${rankStyle.fontSize}px` : "13px", color: rankStyle.color || theme.textPrimary }}>{row.rank}</div>
                <div style={{ flex: 1, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap", fontFamily: nameStyle.fontFamily || theme.fontBody, fontSize: nameStyle.fontSize ? `${nameStyle.fontSize}px` : `${rowFont}px`, color: nameStyle.color || theme.textPrimary }}>{row.teamName}</div>
                {columns.wins && <div style={{ width: 44, textAlign: "center", fontFamily: statStyle.fontFamily || theme.fontBody, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : `${rowFont - 0.5}px`, color: statStyle.color || theme.textPrimary }}>{row.wins ?? "-"}</div>}
                {columns.placement && <div style={{ width: 82, textAlign: "center", fontFamily: statStyle.fontFamily || theme.fontBody, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : `${rowFont - 0.5}px`, color: statStyle.color || theme.textPrimary }}>{row.placementPts}</div>}
                {columns.elim && <div style={{ width: 48, textAlign: "center", fontFamily: statStyle.fontFamily || theme.fontBody, fontSize: statStyle.fontSize ? `${statStyle.fontSize}px` : `${rowFont - 0.5}px`, color: statStyle.color || theme.textPrimary }}>{row.elimPts}</div>}
                <div style={{ width: 48, textAlign: "center", fontWeight: 800, fontFamily: totalStyle.fontFamily || theme.fontBody, fontSize: totalStyle.fontSize ? `${totalStyle.fontSize}px` : "13px", color: totalStyle.color || theme.textPrimary }}>{row.total}</div>
              </div>
            );
          })}
        </div>
      </div>

      {pager !== "none" && totalPages > 1 && (
        <div style={{ position: "absolute", bottom: 10, right: 16, display: "flex", alignItems: "center", gap: 6, background: "rgba(0,0,0,0.4)", borderRadius: 6, padding: "3px 8px" }}>
          {pager === "controls" && (
            <button onClick={() => onPageChange?.(Math.max(0, safePage - 1))} disabled={safePage === 0} style={{ background: "none", border: "none", color: safePage === 0 ? "#666" : "#fff", cursor: safePage === 0 ? "default" : "pointer", display: "flex" }}><ChevronLeft size={14} /></button>
          )}
          <span style={{ fontSize: 11, color: "#fff", fontWeight: 700 }}>{safePage + 1} / {totalPages}</span>
          {pager === "controls" && (
            <button onClick={() => onPageChange?.(Math.min(totalPages - 1, safePage + 1))} disabled={safePage === totalPages - 1} style={{ background: "none", border: "none", color: safePage === totalPages - 1 ? "#666" : "#fff", cursor: safePage === totalPages - 1 ? "default" : "pointer", display: "flex" }}><ChevronRight size={14} /></button>
          )}
        </div>
      )}
    </div>
  );
}
