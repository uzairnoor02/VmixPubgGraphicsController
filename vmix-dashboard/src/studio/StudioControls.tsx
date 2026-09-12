import { useEffect, useRef, useState, type CSSProperties, type ReactNode } from "react";
import { Plus, RotateCcw, Trash2, type LucideIcon } from "lucide-react";
import { Bg, FONT_OPTIONS, RowRule, Theme, bgCss } from "./theme";

// Shared editor-chrome components used by every Studio page: color swatches, gradient-stop bars,
// background/column/row-rule editors, the theme picker, and the page/panel shells. Ported from
// graphics-studio-app.jsx - each of the 6 standalone artifacts under Downloads/files duplicated
// all of this; this is the one copy the whole Studio now shares.

export const btnGhost: CSSProperties = { background: "rgba(255,255,255,0.06)", border: "1px solid rgba(255,255,255,0.12)", borderRadius: 8, color: "#c8c8d0", padding: "6px 10px", fontSize: 12, cursor: "pointer" };
export const pill = (active: boolean): CSSProperties => ({ border: "1px solid", borderRadius: 20, padding: "7px 14px", fontSize: 12, fontWeight: 600, cursor: "pointer", background: active ? "#F4C430" : "transparent", color: active ? "#0a0a0f" : "#c8c8d0", borderColor: active ? "#F4C430" : "rgba(255,255,255,0.2)" });

export function MiniColorButton({ color, onChange, label }: { color?: string; onChange: (c: string) => void; label: string }) {
  const [open, setOpen] = useState(false);
  return (
    <div style={{ position: "relative" }}>
      <button onClick={() => setOpen((o) => !o)} title={label} style={{ width: 22, height: 22, borderRadius: 6, background: color || "#333", border: "1px solid rgba(255,255,255,0.25)", cursor: "pointer", padding: 0 }} />
      {open && (
        <div style={{ position: "absolute", top: 26, left: 0, zIndex: 40, background: "#1c1c24", border: "1px solid rgba(255,255,255,0.15)", borderRadius: 10, padding: 10, boxShadow: "0 8px 24px rgba(0,0,0,0.6)", width: 150 }}>
          <input type="color" value={color || "#333333"} onChange={(e) => onChange(e.target.value)} style={{ width: "100%", height: 30, border: "none", background: "none", cursor: "pointer" }} />
          <button onClick={() => setOpen(false)} style={{ ...btnGhost, width: "100%", marginTop: 6, fontSize: 11 }}>Done</button>
        </div>
      )}
    </div>
  );
}

export function GradientStopBar({ stops, setStops, maxStops }: { stops: { pos: number; color: string }[]; setStops: (updater: any) => void; maxStops?: number }) {
  const ref = useRef<HTMLDivElement>(null);
  const dragId = useRef<number | null>(null);
  const [activeIdx, setActiveIdx] = useState(0);
  const atMax = maxStops != null && stops.length >= maxStops;
  const posFromEvent = (clientX: number) => { const rect = ref.current!.getBoundingClientRect(); return Math.max(0, Math.min(100, ((clientX - rect.left) / rect.width) * 100)); };
  useEffect(() => {
    const move = (e: PointerEvent) => { if (dragId.current == null) return; const pos = Math.round(posFromEvent(e.clientX)); setStops((prev: any) => prev.map((s: any, i: number) => (i === dragId.current ? { ...s, pos } : s))); };
    const up = () => (dragId.current = null);
    window.addEventListener("pointermove", move); window.addEventListener("pointerup", up);
    return () => { window.removeEventListener("pointermove", move); window.removeEventListener("pointerup", up); };
  }, [setStops]);
  return (
    <div>
      <div style={{ height: 24, borderRadius: 7, background: `linear-gradient(90deg, ${[...stops].sort((a, b) => a.pos - b.pos).map((s) => `${s.color} ${s.pos}%`).join(", ")})`, border: "1px solid rgba(255,255,255,0.15)", marginBottom: 4 }} />
      <div ref={ref} onClick={(e) => { if (e.target !== ref.current || atMax) return; const pos = Math.round(posFromEvent(e.clientX)); setStops((prev: any) => { const next = [...prev, { pos, color: "#ffffff" }]; setActiveIdx(next.length - 1); return next; }); }} style={{ position: "relative", height: 20, cursor: atMax ? "default" : "copy" }}>
        {stops.map((s, i) => (<div key={i} onPointerDown={(e) => { e.stopPropagation(); dragId.current = i; setActiveIdx(i); }} onClick={(e) => { e.stopPropagation(); setActiveIdx(i); }} style={{ position: "absolute", left: `${s.pos}%`, top: 0, width: 12, height: 12, borderRadius: "50% 50% 50% 0", background: s.color, border: activeIdx === i ? "2px solid #F4C430" : "2px solid white", transform: "translateX(-50%) rotate(45deg)", cursor: "grab" }} />))}
      </div>
      <div style={{ fontSize: 10, color: atMax ? "#F4A261" : "#6b6b78", marginTop: 4 }}>{maxStops != null ? `${stops.length} / ${maxStops} stops${atMax ? " — max reached" : " — click to add"}` : `${stops.length} stops — click to add`}</div>
      {stops[activeIdx] && (
        <div style={{ display: "flex", gap: 8, alignItems: "center", marginTop: 8, background: "rgba(255,255,255,0.03)", padding: 8, borderRadius: 8 }}>
          <MiniColorButton color={stops[activeIdx].color} onChange={(c) => setStops((prev: any) => prev.map((s: any, i: number) => (i === activeIdx ? { ...s, color: c } : s)))} label="Stop color" />
          <input type="number" min={0} max={100} value={stops[activeIdx].pos} onChange={(e) => { const pos = Math.max(0, Math.min(100, Number(e.target.value))); setStops((prev: any) => prev.map((s: any, i: number) => (i === activeIdx ? { ...s, pos } : s))); }} style={{ width: 46, background: "#15151b", border: "1px solid rgba(255,255,255,0.15)", borderRadius: 6, color: "#e8e8ec", fontSize: 11, padding: "3px 5px" }} />
          <span style={{ fontSize: 11, color: "#8a8a94" }}>%</span>
          {stops.length > 2 && <button onClick={() => { setStops((prev: any) => prev.filter((_: any, i: number) => i !== activeIdx)); setActiveIdx(0); }} style={{ ...btnGhost, marginLeft: "auto", padding: 5 }}><Trash2 size={11} /></button>}
        </div>
      )}
    </div>
  );
}

export function BgEditor({ bg, setBg, maxStops, defaultSolid = "#333333", defaultGradient }: { bg: Bg; setBg: (b: Bg) => void; maxStops?: number; defaultSolid?: string; defaultGradient?: Bg }) {
  const setStops = (updater: any) => {
    const current = bg.type === "gradient" ? bg.stops : [];
    const next = typeof updater === "function" ? updater(current) : updater;
    setBg({ type: "gradient", angle: bg.type === "gradient" ? bg.angle : 90, stops: maxStops != null ? next.slice(0, maxStops) : next });
  };
  return (
    <div>
      <div style={{ display: "flex", gap: 4, marginBottom: 8 }}>
        <button onClick={() => setBg({ type: "solid", color: bg.type === "solid" ? bg.color : (bg.stops?.[0]?.color ?? defaultSolid) })} style={{ ...pill(bg.type === "solid"), padding: "4px 10px", fontSize: 11 }}>Solid</button>
        <button onClick={() => setBg(bg.type === "gradient" ? bg : (defaultGradient || { type: "gradient", angle: 90, stops: [{ pos: 0, color: "#333" }, { pos: 100, color: "#111" }] }))} style={{ ...pill(bg.type === "gradient"), padding: "4px 10px", fontSize: 11 }}>Gradient</button>
      </div>
      {bg.type === "solid" ? (
        <MiniColorButton color={bg.color} onChange={(c) => setBg({ type: "solid", color: c })} label="Background" />
      ) : (
        <div>
          {maxStops != null && <div style={{ fontSize: 10.5, color: "#8a8a94", marginBottom: 4 }}>Capped at {maxStops} stops.</div>}
          <GradientStopBar stops={bg.stops} setStops={setStops} maxStops={maxStops} />
        </div>
      )}
    </div>
  );
}

export function ResetToThemeButton({ onClick }: { onClick: () => void }) {
  return <button onClick={onClick} style={{ ...btnGhost, marginTop: 10, fontSize: 11, display: "flex", alignItems: "center", gap: 4 }}><RotateCcw size={11} /> Reset to theme</button>;
}

export interface ColumnStyle { mode: "default" | "custom"; custom: { fontFamily?: string; fontSize?: number; color?: string; scale?: number } }

export function ColumnStyleEditor({ label, col, setCol }: { label: string; col: ColumnStyle; setCol: (c: ColumnStyle) => void }) {
  const mode = col?.mode ?? "default";
  const custom = col?.custom ?? {};
  return (
    <div style={{ background: "rgba(255,255,255,0.03)", borderRadius: 10, padding: 10, marginBottom: 8 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: mode === "custom" ? 8 : 0 }}>
        <span style={{ fontSize: 12, color: "#d8d8e0", fontWeight: 500 }}>{label}</span>
        <div style={{ display: "flex", gap: 4 }}>
          <button onClick={() => setCol({ ...col, mode: "default" })} style={{ ...pill(mode === "default"), padding: "3px 10px", fontSize: 10.5 }}>Default</button>
          <button onClick={() => setCol({ ...col, mode: "custom", custom: col?.custom ?? {} })} style={{ ...pill(mode === "custom"), padding: "3px 10px", fontSize: 10.5 }}>Custom</button>
        </div>
      </div>
      {mode === "custom" && (
        <div style={{ display: "flex", flexWrap: "wrap", gap: 8, alignItems: "center" }}>
          <select value={custom.fontFamily ?? "theme"} onChange={(e) => setCol({ ...col, custom: { ...custom, fontFamily: e.target.value === "theme" ? undefined : e.target.value } })} style={{ background: "#15151b", border: "1px solid rgba(255,255,255,0.15)", borderRadius: 6, color: "#e8e8ec", fontSize: 11, padding: "4px 6px" }}>
            {FONT_OPTIONS.map((f) => <option key={f.id} value={f.id}>{f.label}</option>)}
          </select>
          <div style={{ display: "flex", alignItems: "center", gap: 4 }}>
            <input type="number" min={8} max={72} value={custom.fontSize ?? 13} onChange={(e) => setCol({ ...col, custom: { ...custom, fontSize: Number(e.target.value) } })} style={{ width: 44, background: "#15151b", border: "1px solid rgba(255,255,255,0.15)", borderRadius: 6, color: "#e8e8ec", fontSize: 11, padding: "3px 5px", textAlign: "center" }} />
            <span style={{ fontSize: 10, color: "#6b6b78" }}>px</span>
          </div>
          <MiniColorButton color={custom.color} label="Text color" onChange={(c) => setCol({ ...col, custom: { ...custom, color: c } })} />
          <button onClick={() => setCol({ ...col, custom: {} })} style={{ ...btnGhost, padding: "3px 8px", fontSize: 10, display: "flex", alignItems: "center", gap: 3, marginLeft: "auto" }}><RotateCcw size={10} /> Reset</button>
        </div>
      )}
    </div>
  );
}

export function RowRuleItem({ rule, onChange, onRemove }: { rule: RowRule; onChange: (r: RowRule) => void; onRemove: () => void }) {
  return (
    <div style={{ background: "rgba(255,255,255,0.03)", borderRadius: 10, padding: 10, marginBottom: 8 }}>
      <div style={{ display: "flex", gap: 8, alignItems: "center", marginBottom: 8 }}>
        <input value={rule.label} onChange={(e) => onChange({ ...rule, label: e.target.value })} style={{ flex: 1, background: "#15151b", border: "1px solid rgba(255,255,255,0.15)", borderRadius: 6, color: "#e8e8ec", fontSize: 12, padding: "4px 6px" }} />
        <button onClick={onRemove} style={{ ...btnGhost, padding: 5 }}><Trash2 size={12} /></button>
      </div>
      <div style={{ display: "flex", gap: 8, alignItems: "center", marginBottom: 8 }}>
        <span style={{ fontSize: 11, color: "#8a8a94" }}>Rank</span>
        <input type="number" min={1} max={18} value={rule.from} onChange={(e) => onChange({ ...rule, from: Number(e.target.value) })} style={{ width: 42, background: "#15151b", border: "1px solid rgba(255,255,255,0.15)", borderRadius: 6, color: "#e8e8ec", fontSize: 12, padding: "3px 5px", textAlign: "center" }} />
        <span style={{ fontSize: 11, color: "#8a8a94" }}>to</span>
        <input type="number" min={1} max={18} value={rule.to} onChange={(e) => onChange({ ...rule, to: Number(e.target.value) })} style={{ width: 42, background: "#15151b", border: "1px solid rgba(255,255,255,0.15)", borderRadius: 6, color: "#e8e8ec", fontSize: 12, padding: "3px 5px", textAlign: "center" }} />
        <div style={{ display: "flex", gap: 4, marginLeft: "auto" }}>
          <button onClick={() => onChange({ ...rule, bg: { type: "solid", color: rule.bg.type === "solid" ? rule.bg.color : (rule.bg.stops?.[0]?.color ?? "#2ECC71") } })} style={{ ...pill(rule.bg.type === "solid"), fontSize: 10, padding: "3px 8px" }}>Solid</button>
          <button onClick={() => onChange({ ...rule, bg: rule.bg.type === "gradient" ? rule.bg : { type: "gradient", angle: 90, stops: [{ pos: 0, color: "#2ECC71" }, { pos: 100, color: "#1B8A4C" }] } })} style={{ ...pill(rule.bg.type === "gradient"), fontSize: 10, padding: "3px 8px" }}>Gradient</button>
        </div>
      </div>
      {rule.bg.type === "solid" ? (
        <MiniColorButton color={rule.bg.color} onChange={(c) => onChange({ ...rule, bg: { type: "solid", color: c } })} label="Row color" />
      ) : (
        <GradientStopBar stops={rule.bg.stops} setStops={(updater: any) => { const next = typeof updater === "function" ? updater((rule.bg as any).stops) : updater; onChange({ ...rule, bg: { ...(rule.bg as any), stops: next } }); }} />
      )}
      <div style={{ height: 16, borderRadius: 5, background: bgCss(rule.bg), marginTop: 8 }} />
    </div>
  );
}

export function ThemePicker({ activeThemeId, setActiveThemeId, themes }: { activeThemeId: string; setActiveThemeId: (id: string) => void; themes: Record<string, Theme> }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
      {Object.values(themes).map((t) => (
        <button key={t.id} onClick={() => setActiveThemeId(t.id)} style={{ display: "flex", alignItems: "center", gap: 12, padding: 10, borderRadius: 12, background: activeThemeId === t.id ? "rgba(244,196,48,0.1)" : "rgba(255,255,255,0.03)", border: activeThemeId === t.id ? "1.5px solid #F4C430" : "1.5px solid transparent", cursor: "pointer", textAlign: "left" }}>
          <div style={{ width: 40, height: 40, borderRadius: 10, background: t.accentGradient, flexShrink: 0, boxShadow: t.glow }} />
          <div><div style={{ fontSize: 13, fontWeight: 700, color: "#f0f0f2" }}>{t.name}</div><div style={{ fontSize: 10.5, color: "#8a8a94", lineHeight: 1.3 }}>{t.description}</div></div>
        </button>
      ))}
    </div>
  );
}

export function EditorPanel({ tabs, activeTab, setActiveTab, children }: { tabs: { id: string; label: string; icon: LucideIcon }[]; activeTab: string; setActiveTab: (id: string) => void; children: ReactNode }) {
  return (
    <div style={{ flex: "1 1 340px", minWidth: 320, background: "rgba(255,255,255,0.02)", border: "1px solid rgba(255,255,255,0.08)", borderRadius: 16, padding: 16, backdropFilter: "blur(20px)", alignSelf: "flex-start" }}>
      <div style={{ display: "flex", gap: 4, marginBottom: 16, flexWrap: "wrap" }}>
        {tabs.map((t) => (
          <button key={t.id} onClick={() => setActiveTab(t.id)} style={{ ...pill(activeTab === t.id), padding: "7px 10px", display: "flex", alignItems: "center", gap: 4, fontSize: 11 }}>
            <t.icon size={11} />{t.label}
          </button>
        ))}
      </div>
      {children}
    </div>
  );
}

export function PageShell({ title, subtitle, children }: { title: string; subtitle: string; children: ReactNode }) {
  return (
    <div>
      <div style={{ marginBottom: 18 }}>
        <div style={{ fontSize: 20, fontWeight: 700 }}>{title}</div>
        <div style={{ fontSize: 12.5, color: "#8a8a94", marginTop: 2 }}>{subtitle}</div>
      </div>
      <div style={{ display: "flex", gap: 24, flexWrap: "wrap" }}>{children}</div>
    </div>
  );
}

export function addRowRule(setRowRules: (updater: any) => void) {
  setRowRules((prev: RowRule[]) => [...prev, { id: Math.random().toString(36).slice(2, 8), label: "New rule", from: 1, to: 1, bg: { type: "solid", color: "#2ECC71" } }]);
}
