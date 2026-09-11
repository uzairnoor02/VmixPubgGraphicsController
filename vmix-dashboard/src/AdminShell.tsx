import { useState } from "react";
import { clearAuthed } from "./Login";
import LiveTab from "./pages/LiveTab";
import TeamsTab from "./pages/TeamsTab";
import OverlaySettingsTab from "./pages/OverlaySettingsTab";
import GraphicsTab from "./pages/GraphicsTab";

// Deliberately a plain useState tab switch instead of a routing library - four tabs on one page
// doesn't need real URLs, and it keeps this dashboard dependency-free (no react-router) which
// matters more than deep-linkable tabs for something one operator uses from a laptop next to the
// production PC. The one route that IS a real URL is /overlay (see App.tsx), because that one has
// to be pasted into vMix as its own address.
const TABS = [
  { id: "live", label: "Live", component: LiveTab },
  { id: "teams", label: "Teams", component: TeamsTab },
  { id: "overlay-settings", label: "Overlay Settings", component: OverlaySettingsTab },
  { id: "graphics", label: "Custom Graphics", component: GraphicsTab },
] as const;

export default function AdminShell() {
  const [activeTab, setActiveTab] = useState<(typeof TABS)[number]["id"]>("live");
  const ActiveComponent = TABS.find((t) => t.id === activeTab)?.component ?? LiveTab;

  return (
    <div className="shell">
      <nav className="shell-nav">
        <div className="shell-brand">PUBG Graphics Control</div>
        <div className="shell-tabs">
          {TABS.map((tab) => (
            <button
              key={tab.id}
              className={`shell-tab ${activeTab === tab.id ? "active" : ""}`}
              onClick={() => setActiveTab(tab.id)}
            >
              {tab.label}
            </button>
          ))}
        </div>
        <button className="secondary shell-logout" onClick={() => { clearAuthed(); window.location.reload(); }}>
          Log out
        </button>
      </nav>
      <main className="shell-main">
        <ActiveComponent />
      </main>
    </div>
  );
}
