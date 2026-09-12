# React / Web API Migration — Plan & Status

Branch: `feature/react-webapi-migration` (off `feature/2026-start`). Every commit on this branch
builds clean (`dotnet build` on the full solution, `npm run build` in `vmix-dashboard/`) and the
`DummyDataHarness` test suite still passes (4/4) after every change that touched shared logic.

## Architecture decision: endpoints + SignalR, not one or the other

You asked whether to coordinate frontend/backend via REST endpoints or a backend push service
(SignalR). The answer used throughout is **both, split by what each is good at** — this was
already the shape `LiveDashboardHost.cs` was in before this session, so it's a continuation, not a
new decision:

- **REST endpoints** for anything an operator *does* — start a match, run a report, save a Studio
  setting, add a tournament. Request/response, one action, one result. This is 100% of
  `MatchControlApi.cs` and the write side of `OverlayConfigStore`.
- **SignalR (`/hubs/match`)** for anything that *happens* and every connected client needs to know
  about immediately — live team stats every ~1s, match status, overlay config changes, achievement
  events. Nobody polls for these; the hub pushes them the instant they occur.

A Studio edit is the clean example of both working together: saving it is a REST call
(`POST /api/overlay/config`), and it reaching the live `/overlay` route the instant it's saved is
SignalR (`OverlayConfigChanged`) — no polling, no separate "publish" step.

## What's fully implemented and working

**Backend runs headless — no WinForms window at all.**
`Program.cs` starts Kestrel (dashboard + Hangfire), the DB, and the live-dashboard host, then
blocks on a shutdown signal instead of `Application.Run(mainForm)`. `Form1`/`AuthenticationForm`/
`Add_tournament`/`BackupForm`/`ManualDataInputForm` still exist as files and still compile (so
nothing else that touches them breaks) but nothing shows them anymore.

**Every Form1 button is a REST endpoint** (`Pubg Ranking System/MatchControlApi.cs`), calling the
same unchanged business classes (`TournamentBusiness`, `PostMatch`, `PreMatch`, `Reset`):
`GET /api/tournaments`, `GET /api/stages`, `POST /api/tournaments`, `POST /api/tournaments/stages`,
`POST /api/match/start` (with the same in-progress/completed confirmation flow, now
request/response instead of `MessageBox`), `POST /api/match/stop`,
`POST /api/postmatch/run/{step}` ×11, `POST /api/postmatch/run-all`,
`POST /api/prematch/map-top-performers`, `POST /api/teams/reload`.

**React "Match Control" tab** (`vmix-dashboard/src/pages/MatchControlTab.tsx`) is the full
replacement UI for the above — tournament/stage/day/match pickers, start/stop, the same
confirm-then-type-DELETE flow, all 11 report buttons + "run all", add tournament/stage, reload
teams, map top performers, an activity log, and a data-source picker (direct vs. agent, see below).

**Graphics Studio** (`vmix-dashboard/src/studio/`) — all 6 design-editor pages from
`Downloads/files` ported into the real app as typed TSX, sharing one theme system
(`theme.ts`) and one set of editor controls (`StudioControls.tsx`) instead of the 6×-duplicated
code the standalone artifacts had: Standings, Top 4/WWCD, Rankings, Top Players, Eliminated banner
+ sidebar, Achievement popup. Every setting an operator changes persists into
`OverlayConfig.elementSettings` (a `Dictionary<string, JsonElement>` that already existed — **zero
backend schema changes needed**) and reaches the live overlay over the existing
`OverlayConfigChanged` SignalR push.

**Live overlay renders real data for Standings.** `TeamLiveStats` now carries
`Player{1-4}LiveState`/`Player{1-4}HealthPercent` alongside the pre-rendered image-path fields
(computed in `LiveStatsBusiness.cs` from the same values already used for the image). `/overlay`'s
Standings panel renders through the exact same `<StandingsRenderer>` component the Studio editor's
preview uses — a Studio edit and what's actually on air share one render path, not an
approximation of it.

**Remote data collection.** New `VmixIngestAgent/` — a tiny console program meant to run on the
customer's PC next to pcob, polling it and forwarding raw responses to the main app's new
`POST /api/ingest/tick`. This decouples "where pcob is reachable from" (the customer's production
PC/LAN) from "where the main app + React dashboard run" (can now be anywhere). Picking "agent" vs.
"direct" mode is a dropdown on the Match Control tab per match-start.

**Secrets.** `WebDashboard:AuthKey` and the new `Agent:IngestKey` are randomly generated, not
placeholders — see `Claude outputs/CREDENTIALS.md`.

## What's partially done

- **Overlay rendering:** only Standings goes through a Studio-shared renderer against live data.
  Top4/WWCD, Rankings, Top Players, Eliminated banner, and Achievement popup are ported as *design
  editors* (Studio pages, sample data) but `/overlay` itself still renders those with its own
  older, simpler markup — not yet swapped to the Studio-configured look. Standings was the proof
  of concept; the same `<XRenderer>` extraction done for it needs repeating for the other five.
- **Live per-player data** only exists on `TeamLiveStats` (health/liveState). Team *name* still
  isn't on it (only `tag`) — Standings on the live overlay currently shows tags, not full team
  names, until that's added too.
- **Ingest agent mode** is wired end-to-end and builds, but hasn't been run against a real pcob
  instance — only the direct-poll path has actual production mileage. Treat "agent" mode as
  implemented-and-compiled, not field-proven.

## What's not started (from your original 18-item list)

- Per-kill elimination feed still uses the derived team-elimination-count fallback, not real
  `getkillinfo` data (item 5).
- No banners for `KillDominationAsync`/`DamageDominationAsync` yet (item 6).
- Player photos aren't served over HTTP yet — achievement banners still fall back to the icon
  (item 7).
- `Form1.cs`'s missing `await`s (button7_Click) are moot now that Form1 isn't shown, but the
  equivalent code path in the new API (`/api/postmatch/run-all`) already awaits every step
  correctly — nothing left to fix there.
- MVP cards, Player Highlight, Circle Status bar, Head-to-Head, Teams to Watch, Champions graphics:
  not built.
- Manual data entry (`ManualDataInputForm`) and DB backup (`BackupForm`) — WinForms-only still,
  no API/React equivalent. These are the two remaining pieces of real UI logic that haven't been
  ported (511 and 279 lines respectively); everything else Form1-adjacent is done.
- Calibration data (survival-% weights, grenade/vehicle detection approach, placement-point scale)
  — still needs your input, unchanged from before this session.

## How to run it right now

```bash
dotnet run --project "Pubg Ranking System"      # headless backend, port 5050 (+ 5001 Hangfire)
cd vmix-dashboard && npm run dev                # React dev server (or npm run build; the
                                                 # backend serves vmix-dashboard/dist directly too)
```

Open `http://localhost:5050` (or the Vite dev URL), log in with the key in `CREDENTIALS.md`. Paste
`http://<graphics-pc>:5050/overlay` into vMix as before.

## Suggested next slice

1. Repeat the Standings→live-overlay wiring for Top4 (it's the next graphic with real per-player
   data available) and Rankings/Top Players/Eliminated (which only need team-level data already on
   `TeamLiveStats`).
2. Add `TeamName` to `TeamLiveStats` so the overlay isn't tag-only.
3. Port `ManualDataInputForm`/`BackupForm` the same way Add_tournament was (small, mechanical).
