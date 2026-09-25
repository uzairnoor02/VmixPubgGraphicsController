import { useStudioElement } from "./StudioConfigContext";
import { HealthStop } from "./theme";
import { MiniColorButton, pill } from "./StudioControls";
import { DEFAULT_HEALTH_STYLE, HealthBar, HealthStyle, HelmetIcon, resolveHealthStyle } from "./renderers/healthGlyphs";

/** The one "health.style" setting shared by the Standings bars and the Last 4 helmets. */
export function useHealthStyle(): [HealthStyle, (next: HealthStyle) => void] {
  const [raw, setRaw] = useStudioElement<Partial<HealthStyle> | null>("health.style", null);
  return [resolveHealthStyle(raw), (next) => setRaw(next)];
}

const PREVIEW_PLAYERS = [
  { health: 100, liveState: 0 }, { health: 62, liveState: 0 }, { health: 24, liveState: 0 },
  { health: 55, liveState: 4 }, { health: 0, liveState: 5 },
];

export function HealthStyleEditor({ stops }: { stops: HealthStop[] }) {
  const [style, setStyle] = useHealthStyle();
  const set = (patch: Partial<HealthStyle>) => setStyle({ ...style, ...patch });
  const swatch = (label: string, key: "alive" | "knocked" | "dead" | "track") => (
    <div style={{ display: "flex", alignItems: "center", gap: 8, fontSize: 12, color: "#c8c8d0" }}>
      <MiniColorButton color={style[key].startsWith("#") ? style[key] : undefined} onChange={(c) => set({ [key]: c } as Partial<HealthStyle>)} label={label} />
      {label}
    </div>
  );
  return (
    <div style={{ background: "rgba(255,255,255,0.03)", borderRadius: 10, padding: 12, marginBottom: 12 }}>
      <div style={{ fontSize: 12, color: "#d8d8e0", marginBottom: 8 }}>Player health look (Standings bars and Last 4 helmets)</div>
      <div style={{ fontSize: 11.5, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>
        Bars and helmets fill up to the player's health. Knocked players fill red to their remaining
        bleed-out health and pulse; dead players are solid grey.
      </div>
      <div style={{ display: "flex", gap: 6, marginBottom: 10 }}>
        <button onClick={() => set({ fill: "solid" })} style={{ ...pill(style.fill === "solid"), padding: "4px 10px", fontSize: 11 }}>One colour</button>
        <button onClick={() => set({ fill: "gradient" })} style={{ ...pill(style.fill === "gradient"), padding: "4px 10px", fontSize: 11 }}>Follow health gradient</button>
      </div>
      <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8, marginBottom: 12 }}>
        {style.fill === "solid" && swatch("Alive", "alive")}
        {swatch("Knocked", "knocked")}
        {swatch("Dead", "dead")}
      </div>
      <div style={{ display: "flex", alignItems: "flex-end", gap: 14, padding: 10, borderRadius: 8, background: "rgba(0,0,0,0.35)" }}>
        <div style={{ display: "flex", gap: 3 }}>{PREVIEW_PLAYERS.map((p, i) => <HealthBar key={i} player={p} style={style} stops={stops} width={8} height={24} />)}</div>
        <div style={{ display: "flex", gap: 4 }}>{PREVIEW_PLAYERS.map((p, i) => <HelmetIcon key={i} player={p} style={style} stops={stops} size={28} />)}</div>
      </div>
      <button onClick={() => setStyle(DEFAULT_HEALTH_STYLE)} style={{ marginTop: 10, background: "none", border: "none", color: "#8a8a94", fontSize: 11, cursor: "pointer", padding: 0 }}>Reset to default</button>
    </div>
  );
}
