import { useState } from "react";
import { Palette, Type } from "lucide-react";
import { useTheme } from "../ThemeContext";
import { useStudioElement } from "../StudioConfigContext";
import { Bg } from "../theme";
import { BgEditor, EditorPanel, PageShell, ResetToThemeButton, pill } from "../StudioControls";
import { DEFAULT_WORLD_SIZE, SpectatorMapRenderer } from "../renderers/SpectatorMapRenderer";
import { SAMPLE_MAP_PLAYERS } from "../sampleData";

export default function SpectatorMapPage() {
  const { theme } = useTheme();
  const [tab, setTab] = useState("canvas");
  const [focus, setFocus] = useState<number | undefined>(1);
  const [canvasBgOverride, setCanvasBgOverride] = useStudioElement<Bg | null>("map.canvasBg", null);
  const [accentBgOverride, setAccentBgOverride] = useStudioElement<Bg | null>("map.accentBg", null);
  const [showLabels, setShowLabels] = useStudioElement("map.showLabels", false);
  const [worldSize, setWorldSize] = useStudioElement("map.worldSize", DEFAULT_WORLD_SIZE);
  const [mapImageUrl, setMapImageUrl] = useStudioElement("map.imageUrl", "");

  const canvasBg = canvasBgOverride || { type: "solid", color: "#101820" } as Bg;
  const accentBg = accentBgOverride || theme.headerBg;

  return (
    <PageShell title="Spectator Map" subtitle="Live team positions plotted on the match map">
      <div style={{ flex: "1 1 420px" }}>
        <div style={{ display: "flex", gap: 6, marginBottom: 12, flexWrap: "wrap" }}>
          <button onClick={() => setFocus(undefined)} style={pill(focus === undefined)}>No focus</button>
          {[1, 2, 3].map((id) => (
            <button key={id} onClick={() => setFocus(id)} style={pill(focus === id)}>Focus team {id}</button>
          ))}
        </div>
        <div style={{ width: "100%", maxWidth: 420 }}>
          <SpectatorMapRenderer theme={theme} canvasBg={canvasBg} accentBg={accentBg}
            mapImageUrl={mapImageUrl || undefined} mapName="ERANGEL"
            players={SAMPLE_MAP_PLAYERS} worldSize={worldSize} focusTeamId={focus} showLabels={showLabels} />
        </div>
        <div style={{ marginTop: 14, padding: 12, borderRadius: 8, fontSize: 12, lineHeight: 1.5, background: "rgba(241,196,15,0.1)", border: "1px solid rgba(241,196,15,0.3)", color: "#f5d76e" }}>
          <b>One backend line needed:</b> LivePlayerInfo already carries Location&nbsp;{"{x,y,z}"} from
          gettotalplayerlist, but <code>LiveStatsBusiness.FilterPlayerInfo</code> drops it from its
          projection. Add Location there and broadcast <code>MapPositionsUpdated</code> and this
          renders live.
        </div>
      </div>

      <EditorPanel tabs={[{ id: "canvas", label: "Canvas", icon: Palette }, { id: "accent", label: "Focus", icon: Palette }, { id: "map", label: "Map", icon: Type }]} activeTab={tab} setActiveTab={setTab}>
        {tab === "canvas" && (<div><BgEditor bg={canvasBg} setBg={setCanvasBgOverride} />{canvasBgOverride && <ResetToThemeButton onClick={() => setCanvasBgOverride(null)} />}</div>)}
        {tab === "accent" && (<div><div style={{ fontSize: 12, color: "#8a8a94", marginBottom: 10, lineHeight: 1.5 }}>Colour of the focused team's markers and the map badge.</div><BgEditor bg={accentBg} setBg={setAccentBgOverride} maxStops={3} />{accentBgOverride && <ResetToThemeButton onClick={() => setAccentBgOverride(null)} />}</div>)}
        {tab === "map" && (
          <div>
            <label style={{ display: "flex", alignItems: "center", gap: 8, padding: "6px 0", fontSize: 13, cursor: "pointer" }}>
              <input type="checkbox" checked={showLabels} onChange={(e) => setShowLabels(e.target.checked)} />Show team labels
            </label>
            <label style={{ display: "block", marginTop: 10, fontSize: 12, color: "#c8c8d0" }}>
              Map image URL (optional)
              <input value={mapImageUrl} onChange={(e) => setMapImageUrl(e.target.value)} placeholder="/graphics/erangel.png"
                style={{ width: "100%", marginTop: 5, background: "rgba(255,255,255,0.06)", border: "1px solid rgba(255,255,255,0.12)", borderRadius: 6, color: "#e8e8ec", padding: "7px 9px", fontSize: 12 }} />
            </label>
            <label style={{ display: "block", marginTop: 10, fontSize: 12, color: "#c8c8d0" }}>
              World size (8×8km maps ≈ 816000; Sanhok ≈ 408000)
              <input type="number" value={worldSize} onChange={(e) => setWorldSize(Number(e.target.value) || DEFAULT_WORLD_SIZE)}
                style={{ width: "100%", marginTop: 5, background: "rgba(255,255,255,0.06)", border: "1px solid rgba(255,255,255,0.12)", borderRadius: 6, color: "#e8e8ec", padding: "7px 9px", fontSize: 12 }} />
            </label>
          </div>
        )}
      </EditorPanel>
    </PageShell>
  );
}
