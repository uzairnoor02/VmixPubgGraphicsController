import { useEffect, useRef, useState } from "react";
import { Trash2 } from "lucide-react";
import { HealthStop } from "./theme";
import { MiniColorButton, btnGhost } from "./StudioControls";

export function HealthGradientEditor({ stops, setStops }: { stops: HealthStop[]; setStops: (updater: any) => void }) {
  const ref = useRef<HTMLDivElement>(null);
  const dragId = useRef<number | null>(null);
  const [activeIdx, setActiveIdx] = useState<number | null>(null);
  const posFromEvent = (clientX: number) => { const rect = ref.current!.getBoundingClientRect(); return Math.max(0, Math.min(100, ((clientX - rect.left) / rect.width) * 100)); };
  useEffect(() => {
    const move = (e: PointerEvent) => { if (dragId.current == null) return; const pos = Math.round(posFromEvent(e.clientX)); setStops((prev: HealthStop[]) => prev.map((s, i) => (i === dragId.current ? { ...s, pos } : s))); };
    const up = () => (dragId.current = null);
    window.addEventListener("pointermove", move); window.addEventListener("pointerup", up);
    return () => { window.removeEventListener("pointermove", move); window.removeEventListener("pointerup", up); };
  }, [setStops]);
  return (
    <div>
      <div style={{ height: 26, borderRadius: 8, background: `linear-gradient(90deg, ${[...stops].sort((a, b) => a.pos - b.pos).map((s) => `${s.color} ${s.pos}%`).join(", ")})`, border: "1px solid rgba(255,255,255,0.15)", marginBottom: 4 }} />
      <div ref={ref} onClick={(e) => { if (e.target !== ref.current) return; const pos = Math.round(posFromEvent(e.clientX)); setStops((prev: HealthStop[]) => [...prev, { pos, color: "#ffffff" }]); }} style={{ position: "relative", height: 20, cursor: "copy" }}>
        {stops.map((s, i) => (<div key={i} onPointerDown={(e) => { e.stopPropagation(); dragId.current = i; setActiveIdx(i); }} onClick={(e) => { e.stopPropagation(); setActiveIdx(i); }} style={{ position: "absolute", left: `${s.pos}%`, top: 0, width: 12, height: 12, borderRadius: "50% 50% 50% 0", background: s.color, border: activeIdx === i ? "2px solid #F4C430" : "2px solid white", transform: "translateX(-50%) rotate(45deg)", cursor: "grab" }} />))}
      </div>
      {activeIdx !== null && stops[activeIdx] && (
        <div style={{ display: "flex", gap: 8, alignItems: "center", marginTop: 8, background: "rgba(255,255,255,0.03)", padding: 8, borderRadius: 8 }}>
          <MiniColorButton color={stops[activeIdx].color} onChange={(c) => setStops((prev: HealthStop[]) => prev.map((s, i) => (i === activeIdx ? { ...s, color: c } : s)))} label="Stop color" />
          <input type="number" min={0} max={100} value={stops[activeIdx].pos} onChange={(e) => { const pos = Math.max(0, Math.min(100, Number(e.target.value))); setStops((prev: HealthStop[]) => prev.map((s, i) => (i === activeIdx ? { ...s, pos } : s))); }} style={{ width: 46, background: "#15151b", border: "1px solid rgba(255,255,255,0.15)", borderRadius: 6, color: "#e8e8ec", fontSize: 11, padding: "3px 5px" }} />
          <span style={{ fontSize: 11, color: "#8a8a94" }}>% health</span>
          {stops.length > 2 && <button onClick={() => { setStops((prev: HealthStop[]) => prev.filter((_, i) => i !== activeIdx)); setActiveIdx(null); }} style={{ ...btnGhost, marginLeft: "auto", padding: 5 }}><Trash2 size={11} /></button>}
        </div>
      )}
    </div>
  );
}
