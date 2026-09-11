import { useEffect, useState } from "react";
import { API_BASE, api } from "../lib/api";
import type { OverlayConfig } from "../lib/api";

const ELEMENTS: { id: string; label: string; group: string }[] = [
  { id: "leaderboard", label: "Leaderboard panel", group: "Panels" },
  { id: "eliminationFeed", label: "Elimination feed", group: "Panels" },
  { id: "teamEliminatedBanner", label: '"TEAM ELIMINATED" banner', group: "Panels" },
  { id: "achievement.grenadeElim", label: "Grenade Elimination", group: "Achievements" },
  { id: "achievement.vehicleKill", label: "Vehicle Kill", group: "Achievements" },
  { id: "achievement.airdropLoot", label: "Airdrop Loot", group: "Achievements" },
  { id: "achievement.firstKill", label: "First Kill", group: "Achievements" },
  { id: "achievement.knockout", label: "Knockout", group: "Achievements" },
  { id: "achievement.chickenDinner", label: "Winner Winner Chicken Dinner", group: "Achievements" },
];

const PRESET_COLORS = ["#00FF00", "#00B140", "#0000FF", "#FF00FF"];

export default function OverlaySettingsTab() {
  const [config, setConfig] = useState<OverlayConfig | null>(null);
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
            onChange={(e) => save({ ...config, chromaKeyColor: e.target.value })}
          />
          <input
            type="text"
            className="chroma-hex"
            value={config.chromaKeyColor}
            onChange={(e) => save({ ...config, chromaKeyColor: e.target.value })}
          />
          <div className="chroma-presets">
            {PRESET_COLORS.map((c) => (
              <button key={c} className="chroma-preset" style={{ background: c }} onClick={() => save({ ...config, chromaKeyColor: c })} title={c} />
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
