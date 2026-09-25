import { useEffect, useState } from "react";
import { API_BASE, api } from "../lib/api";
import type { OverlayConfig } from "../lib/api";
import { GRAPHICS } from "../lib/graphics";
import { getConfigElement } from "../studio/configAccess";
import { getAuthKey } from "../Login";
import { CANVAS_H, CANVAS_W, DEFAULT_LAYOUT, GraphicLayout, layoutFor } from "../lib/overlayLayout";

// Graphics whose position can be edited here (Overlay Settings > Positions). Achievements share
// one slot.
const POSITIONED: { id: string; label: string }[] = [
  { id: "leaderboard", label: "Live rankings" },
  { id: "top4", label: "Last 4 teams" },
  { id: "circle", label: "Circle status bar" },
  { id: "achievement", label: "Achievement banner (all types)" },
  { id: "teamEliminatedBanner", label: "ELIMINATED banner" },
  { id: "eliminationFeed", label: "Elimination feed" },
  { id: "sidebar", label: "Live sidebar leaderboard" },
  { id: "spectatorMap", label: "Spectator map" },
  { id: "matchRankings", label: "Match rankings" },
  { id: "overallRankings", label: "Overall rankings" },
  { id: "mvpRankings", label: "MVP rankings" },
  { id: "teamsToWatch", label: "Teams to watch" },
  { id: "mapPerformers", label: "Map performers" },
  { id: "champions", label: "Champions / WWCD" },
  { id: "playerHighlight", label: "Player highlight" },
  { id: "topPlayers", label: "Top players" },
  { id: "teamIntro", label: "Team intro" },
  { id: "headToHead", label: "Head to head" },
];

// Element list comes from the shared catalogue in lib/graphics.ts - this tab and the Director
// tab previously kept separate hand-maintained lists, which drifted every time a graphic was
// added. Group labels are mapped to this tab's own Panels/Achievements split.
/** #RGB or #RRGGBB. vMix keys on a flat colour, so anything else is not a usable chroma value. */
function isValidHex(value: string): boolean {
  return /^#([0-9a-fA-F]{3}|[0-9a-fA-F]{6})$/.test(value.trim());
}

/** Expands #RGB to #RRGGBB so what is stored is always the same shape. */
function normalizeHex(value: string): string {
  const v = value.trim();
  if (/^#[0-9a-fA-F]{3}$/.test(v)) return "#" + v.slice(1).split("").map((c) => c + c).join("");
  return v.toUpperCase();
}

const ELEMENTS: { id: string; label: string; group: string }[] = GRAPHICS.map((g) => ({
  id: g.id,
  label: g.label,
  group: g.group === "Achievements" ? "Achievements" : "Panels",
}));

const PRESET_COLORS = ["#00FF00", "#00B140", "#0000FF", "#FF00FF"];

export default function OverlaySettingsTab() {
  const [config, setConfig] = useState<OverlayConfig | null>(null);
  // null = mirror the saved value; a string = the operator is mid-edit.
  const [hexDraft, setHexDraft] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const overlayUrl = `${window.location.origin}/overlay`;

  useEffect(() => {
    api.getOverlayConfig().then(setConfig).catch(() => {});
  }, []);

  async function save(next: OverlayConfig) {
    setConfig(next);
    setSaving(true);
    setSaved(false);
    try {
      await api.saveOverlayConfig(next);
      setSaved(true);
      window.setTimeout(() => setSaved(false), 2000);
    } finally {
      setSaving(false);
    }
  }

  async function previewEvent(type: string, title: string, subtitle: string) {
    // Admin endpoint: needs the dashboard key (it silently 401'd without it before).
    const authKey = getAuthKey();
    const data = type === "teamEliminated"
      ? { rank: "14", eliminations: "3" }
      : type.startsWith("achievement.") ? { playerName: "PREVIEW PLAYER", teamName: "TEAM", victimName: "OPPONENT" } : undefined;
    await fetch(`${API_BASE}/api/overlay/event`, {
      method: "POST",
      headers: { "Content-Type": "application/json", ...(authKey ? { Authorization: `Bearer ${authKey}` } : {}) },
      body: JSON.stringify({ type, title, subtitle, data }),
    }).catch(() => {});
  }

  if (!config) {
    return <div className="empty-state">Loading overlay settings…</div>;
  }

  const grouped = ELEMENTS.reduce<Record<string, typeof ELEMENTS>>((acc, el) => {
    (acc[el.group] ??= []).push(el);
    return acc;
  }, {});

  return (
    <div>
      <div className="header">
        <h1>Overlay Settings</h1>
      </div>

      <div className="panel">
        <h2>vMix Web Browser source URL</h2>
        <p className="panel-hint">
          Add this exact URL as a Web Browser input in vMix. No login is needed on this page — it's the on-air graphic itself.
        </p>
        <div className="copy-row">
          <code>{overlayUrl}</code>
          <button className="secondary" onClick={() => navigator.clipboard?.writeText(overlayUrl)}>Copy</button>
        </div>
      </div>

      <div className="panel">
        <h2>Chroma key color</h2>
        <p className="panel-hint">
          The overlay's background is filled with exactly this color — set vMix's Chroma Key effect on the Web Browser input to match.
        </p>
        <div className="chroma-row">
          <input
            type="color"
            value={config.chromaKeyColor}
            onChange={(e) => { setHexDraft(null); save({ ...config, chromaKeyColor: e.target.value }); }}
          />
          {/* Typing is buffered: a half-finished value like "#00F" must not be committed as the
              on-air background mid-keystroke. Only a complete #RGB/#RRGGBB is saved; anything
              else marks the field invalid and leaves the live colour untouched. */}
          <input
            type="text"
            className="chroma-hex"
            spellCheck={false}
            maxLength={7}
            style={hexDraft !== null && !isValidHex(hexDraft) ? { borderColor: "#ff6b81", color: "#ff6b81" } : undefined}
            value={hexDraft ?? config.chromaKeyColor}
            onChange={(e) => {
              const raw = e.target.value.startsWith("#") ? e.target.value : `#${e.target.value}`;
              setHexDraft(raw);
              if (isValidHex(raw)) save({ ...config, chromaKeyColor: normalizeHex(raw) });
            }}
            onBlur={() => setHexDraft(null)}
          />
          <div className="chroma-presets">
            {PRESET_COLORS.map((c) => (
              <button key={c} className="chroma-preset" style={{ background: c }} onClick={() => { setHexDraft(null); save({ ...config, chromaKeyColor: c }); }} title={c} />
            ))}
          </div>
        </div>
      </div>

      <div className="panel">
        <h2>Canvas background mode</h2>
        <p className="panel-hint">
          "Chroma" is today's behaviour (fills with the color above). "Transparent" paints nothing at
          all, for vMix Browser Sources that composite real alpha instead of a chroma key. "Solid"
          fills with a plain color. "Image" is preview-only - it never renders on air here, only in
          the Demo page (Task 10) - and is stored under a separate preview.* key this page ignores.
        </p>
        <div className="canvas-mode-row">
          {(["chroma", "transparent", "solid", "image"] as const).map((mode) => (
            <button
              key={mode}
              className={mode === getConfigElement<string>(config, "canvas.mode", "chroma") ? "" : "secondary"}
              onClick={() => save({ ...config, elementSettings: { ...config.elementSettings, ["canvas.mode"]: mode } })}
            >
              {mode}
            </button>
          ))}
          {getConfigElement<string>(config, "canvas.mode", "chroma") === "solid" && (
            <input
              type="color"
              value={getConfigElement<string>(config, "canvas.solidColor", "#000000")}
              onChange={(e) => save({ ...config, elementSettings: { ...config.elementSettings, ["canvas.solidColor"]: e.target.value } })}
            />
          )}
        </div>
      </div>

      <div className="panel">
        <h2>Positions</h2>
        <p className="panel-hint">
          Where each graphic sits on the {CANVAS_W}x{CANVAS_H} frame, in pixels. The defaults follow the PMGO
          broadcast layout: live rankings under the in-game minimap, Last 4 cards across the top,
          achievements on the left below the in-game team panel. X "center" centres horizontally.
          Changes go live on the overlay as you type.
        </p>
        <div style={{ display: "grid", gridTemplateColumns: "minmax(160px, 1.4fr) repeat(3, minmax(70px, 1fr)) auto", gap: "6px 10px", alignItems: "center", fontSize: 13 }}>
          <div style={{ color: "var(--muted, #8a8a94)" }}>Graphic</div><div style={{ color: "var(--muted, #8a8a94)" }}>X</div><div style={{ color: "var(--muted, #8a8a94)" }}>Y</div><div style={{ color: "var(--muted, #8a8a94)" }}>Width</div><div />
          {POSITIONED.map((g) => {
            const l = layoutFor(config, g.id);
            const custom = config.elementSettings?.[`layout.${g.id}`] !== undefined;
            const setLayout = (next: GraphicLayout | undefined) => {
              const settings = { ...config.elementSettings };
              if (next === undefined) delete settings[`layout.${g.id}`]; else settings[`layout.${g.id}`] = next;
              save({ ...config, elementSettings: settings });
            };
            const num = (v: string, fallback: number) => { const n = Number(v); return Number.isFinite(n) ? n : fallback; };
            return (
              <div key={g.id} style={{ display: "contents" }}>
                <div>{g.label}{custom && <span style={{ color: "#F4C430", marginLeft: 6, fontSize: 11 }}>custom</span>}</div>
                <input value={l.x === "center" ? "center" : String(l.x)} onChange={(e) => setLayout({ ...l, x: e.target.value.trim().toLowerCase().startsWith("c") ? "center" : num(e.target.value, 0) })} />
                <input type="number" value={l.y} onChange={(e) => setLayout({ ...l, y: num(e.target.value, l.y) })} />
                <input type="number" value={l.w} onChange={(e) => setLayout({ ...l, w: num(e.target.value, l.w) })} />
                <button className="secondary" disabled={!custom} onClick={() => setLayout(undefined)} title={`Default: x ${DEFAULT_LAYOUT[g.id]?.x}, y ${DEFAULT_LAYOUT[g.id]?.y}, w ${DEFAULT_LAYOUT[g.id]?.w}`}>Reset</button>
              </div>
            );
          })}
        </div>
      </div>

      {Object.entries(grouped).map(([group, items]) => (
        <div className="panel" key={group}>
          <h2>{group}</h2>
          <div className="toggle-list">
            {items.map((el) => (
              <label key={el.id} className="toggle-row">
                <input
                  type="checkbox"
                  checked={config.elementVisibility[el.id] ?? true}
                  onChange={(e) =>
                    save({ ...config, elementVisibility: { ...config.elementVisibility, [el.id]: e.target.checked } })
                  }
                />
                <span>{el.label}</span>
                {/* Task 9: per-graphic panel background opacity. 100 (today's appearance) until an
                    operator touches it - stored in elementSettings, same convention as every other
                    per-element override (see configAccess.ts), so no backend change is needed. */}
                <input
                  type="range"
                  min={0}
                  max={100}
                  step={1}
                  className="opacity-slider"
                  title="Panel background opacity"
                  value={getConfigElement<number>(config, `${el.id}.backgroundOpacity`, 100)}
                  onChange={(e) =>
                    save({ ...config, elementSettings: { ...config.elementSettings, [`${el.id}.backgroundOpacity`]: Number(e.target.value) } })
                  }
                />
                <span className="opacity-value">{getConfigElement<number>(config, `${el.id}.backgroundOpacity`, 100)}%</span>
                {el.group === "Achievements" && (
                  <button
                    className="secondary preview-btn"
                    onClick={() => previewEvent(el.id, el.label, "PREVIEW")}
                  >
                    Preview
                  </button>
                )}
                {el.id === "teamEliminatedBanner" && (
                  <button className="secondary preview-btn" onClick={() => previewEvent("teamEliminated", "TEAM ELIMINATED", "PREVIEW")}>
                    Preview
                  </button>
                )}
              </label>
            ))}
          </div>
        </div>
      ))}

      <div className="save-indicator">{saving ? "Saving…" : saved ? "Saved — live on the overlay now." : ""}</div>
    </div>
  );
}
