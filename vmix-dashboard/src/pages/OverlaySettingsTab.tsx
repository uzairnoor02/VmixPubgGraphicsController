import { useEffect, useState } from "react";
import { API_BASE, api } from "../lib/api";
import type { OverlayConfig } from "../lib/api";
import { GRAPHICS } from "../lib/graphics";

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
    await fetch(`${API_BASE}/api/overlay/event`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ type, title, subtitle }),
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
