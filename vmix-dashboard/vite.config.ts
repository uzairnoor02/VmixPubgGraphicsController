import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The Vite dev server runs on its own port (5173 by default); the live data API/SignalR hub
// is hosted by the WinForms app itself (see LiveDashboardHost.cs) on port 5050. Point this at
// whatever host the graphics PC is reachable at - localhost while developing on that same PC,
// or "http://<graphics-pc-ip>:5050" from any other machine on the LAN.
export default defineConfig({
  plugins: [react()],
  server: {
    host: true,
    port: 5173
  },
  build: {
    // The build environment this was developed in can't delete files in this folder (a sandboxed
    // dev tool restriction, not a vMix/Windows one) - vite normally rimraf's dist/ before every
    // build, which fails under that restriction. Skipping it just means old hashed chunk files
    // from a previous build can linger in dist/assets alongside the new ones; harmless clutter,
    // since index.html always references only the latest ones. Delete dist/ by hand occasionally
    // from Explorer if you want to clean it up, or drop this option once building from a normal
    // Windows shell where deletes work fine.
    emptyOutDir: false
  }
});
