# VMix PUBG Live Dashboard

A read-only web dashboard for watching live match stats from any PC on the network, without
needing RDP or physical access to the graphics PC. Connects to the WinForms app's embedded
SignalR hub / REST API (`LiveDashboardHost.cs`), which listens on port 5050 by default.

This first pass is intentionally view-only (plus a "Reset Overlay" button). Starting and ending
matches stay on the WinForms app for now — see the comment at the top of `LiveDashboardHost.cs`
for why, and what a control-enabled second pass would need.

## Run it

```bash
npm install
npm run dev
```

Then open the URL Vite prints (defaults to http://localhost:5173). By default it talks to
`http://localhost:5050` for the API — if you're opening the dashboard from a *different* PC on
the network than the one running the WinForms app, set the graphics PC's address before starting:

```bash
# Windows PowerShell
$env:VITE_API_BASE = "http://<graphics-pc-ip-or-hostname>:5050"
npm run dev
```

Or create a `.env.local` file with:

```
VITE_API_BASE=http://<graphics-pc-ip-or-hostname>:5050
```

## Build for production

```bash
npm run build
```

Outputs static files to `dist/` — serve them with any static file server, or from the WinForms
app's own Kestrel host in a later pass.
