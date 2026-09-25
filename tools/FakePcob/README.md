# FakePcob

A standalone tool that fakes PUBG's PCOB match server well enough for the vMix overlay stack
(`Pubg Ranking System`, `vmix-dashboard`) to be exercised end-to-end with no live tournament
running. It never touches the real app's runtime — only three lines of `appsettings.json` point the
app at it instead of a real PCOB address.

It is not part of the solution (`FakePcob.csproj` is not referenced from
`VmixPubgGraphicsController.sln`) and is not meant to ship — it is a test/dev tool that lives on the
`test/fake-pcob` branch.

**Build**: `dotnet build tools/FakePcob/FakePcob.csproj` (Windows only - it targets
`net8.0-windows` for `System.Drawing.Common`, used by the `assets` command to draw fake team
logos/photos). **This has not been verified to build in this session** - see REPORT.md,
"Not done / blockers".

## Quick start - watch a full match, no real PCOB needed

1. Generate fake team logos/photos and a `teams.json` you can load into the dashboard:
   ```
   dotnet run --project tools/FakePcob -- assets --from-seed m1 --out tools/FakePcob/out
   ```
   Copy `tools/FakePcob/out/images/*` into the folders your `appsettings.json` points
   `PlayerImages`/`TeamLogosImages` at, then load `tools/FakePcob/out/teams.json` from the
   dashboard's Teams tab (`POST /api/teams/load`).

2. Point the app at FakePcob instead of a real PCOB address. In
   `Pubg Ranking System/appsettings.json`, the real `pcobUrl` line is already commented out and a
   fake one added underneath (Task 8):
   ```
   //"pcobUrl": "http://192.168.1.56:10086/",
   "pcobUrl": "http://127.0.0.1:10086/", // FAKE PCOB - restore the line above for real matches
   ```

3. Start FakePcob, paused, on match `m1`:
   ```
   dotnet run --project tools/FakePcob -- serve --match m1 --paused
   ```
   Open `http://127.0.0.1:10086/_sim` in a browser - a dark control page with the current tick,
   pause/resume/step/restart, speed buttons, a jump-to-tick box, and the current + upcoming expected
   events for this match (see `frames/EXPECTED.md` below for the full list up front).

4. Start `Pubg Ranking System` and the dashboard as normal, open `/overlay` (or the Graphics Studio),
   then click **Resume** on `/_sim`. Tick through `EXPECTED.md` (below) to see everything the app
   is supposed to do at each moment, and compare it to what's actually on screen.

5. Variations:
   - `--speed 5` - 5x normal tick rate (default tick = 2s of match time per 2000ms wall clock, i.e.
     `--speed 1`; `--speed 0` or omitted also works at 1x).
   - `--scenario scripted` (default) walks the match's *real* recorded events in order (first
     blood, a grenade/vehicle/airdrop kill, a team elimination, last-4 switch, all down to 1 team).
     `--scenario random` shuffles the order of same-tick filler events only - it never moves a real
     event to a different tick, since that would violate the seed's actual final stats.
   - `--match m2` / `m3` / `m4` / `d3m2` - the other four real seeds (`dotnet run --project
     tools/FakePcob -- serve` with no `--match` prints the full list of valid keys).
   - `--routes merged` (default) vs `--routes split` - whether `gettotalplayerlist`'s "live" fields
     (health/location/etc.) and "realtime" fields (damage/kills/etc.) report the same tick or the
     realtime fields lag one tick behind, mirroring the real two-route timing SEEDS.md documents.
   - `--port 10086` to change the listen port (must match `pcobUrl` in `appsettings.json`).

6. `/demo` on the dashboard (`http://localhost:<dashboard-port>/demo`, no login) is a separate,
   FakePcob-independent preview: every graphic the overlay can show, rendered from the Graphics
   Studio's own sample data, with buttons to toggle each panel, fire achievements, step through team
   eliminations, adjust panel opacity, and change the canvas background (chroma green/blue,
   transparent, custom color, or a picked image). Useful for a demo or a look-and-feel check with
   nothing else running at all.

## Recording and replaying a real match (test PC only)

`record` captures **all 31 endpoints** the PCOB client's local API server (`ObToolsNew/ob.js`,
build 4.6.0.21520) exposes - not just the 5 the app reads. Two capture paths write into one folder:

- **Poller** (every `--poll` ms, default 2000): fetches every ob.js endpoint itself and saves a
  body only when it changed since the last save. This is what picks up revives/recalls, mortars,
  pickups, weapon detail, team/player report data, `getgameglobalinfo`, etc.
- **Proxy**: the app polls the recorder, which forwards to real PCOB and returns the body
  byte-for-byte (any endpoint name is forwarded now). Saves every call the app makes.

On the PC running PCOB, **the safest option is `--poll-only`**. The app keeps talking straight to
PCOB, `appsettings.json` stays unchanged, and the recorder just reads alongside it:
```
REM 1. PCOB client running, ObToolsNew\launch.bat open, "API Enable" clicked
dotnet run --project tools/FakePcob -- record --upstream http://127.0.0.1:10086/ --poll-only
```

To also capture exactly what the app saw, run the proxy on a **different port** (PCOB already
owns 10086 on that PC) and point `appsettings.json`'s `pcobUrl` at it:
```
dotnet run --project tools/FakePcob -- record --upstream http://127.0.0.1:10086/ --port 10087
```
Options: `--poll 1000` (faster), `--poll 0` (proxy only, old behaviour), `--out <folder>`.

Output: one folder per recording session, one sub-folder per endpoint, and in each sub-folder
one file per new response, numbered in save order:
```
recordings/20260925-181500/
  index.ndjson                      every saved file, in save order (Source = poll | proxy)
  gettotalplayerlist/000001_<unixMs>.json, 000002_..., 000003_...
  getkillinfo/000001_<unixMs>.json, ...
  getreviveplayer/...               (a folder appears once that endpoint returns data)
```
000001 is the first file saved for that endpoint and the highest number is the last. Keep it running ~60s after the match ends: the
end-of-match fields (heal, assists, knockouts, survivalTime) only arrive then. Stop with Ctrl+C.

Notes:
- ob.js logs every response it serves to `ObToolsNew/log/`. Polling 31 endpoints adds to that
  log, so check free disk on the PCOB PC over a long event day.
- ob.js keeps event lists (kills, revives, pickups...) for its whole lifetime and never clears
  them between matches. Restart `launch.bat` between matches if you want each recording to hold
  one match only.

To replay a capture later with no PCOB or network involved:
```
dotnet run --project tools/FakePcob -- serve --replay tools/FakePcob/recordings/<timestamp>
```
Replay builds frames from `gettotalplayerlist`, using the proxy copies if there are any and the
poller copies otherwise, so no tick plays twice. The other 30 endpoints are saved for analysis
but are not replayed yet.

## Restoring the real PCOB address

Undo Task 8's `appsettings.json` edit: delete the fake `pcobUrl` line and remove the `//` from the
real one above it. `Microsoft.Extensions.Configuration.Json` already tolerates `//` line comments in
this file (confirmed in RECON.md #11), so no other change is needed.

## Other commands

- `dump --match <key> --out <dir>` - writes every tick's four route responses as
  `frames/frame_NNN.json` plus a human-readable `frames/EXPECTED.md`, without starting a server.
  Useful for reading through a whole match's expected behavior at once, or for building your own
  test harness against the JSON files directly.
- `assets --from-seed <key>|--random --out <dir>` - generates `teams.json` (dashboard's
  `POST /api/teams/load` shape) plus 256x256 PNG team logos and player photos. `--from-seed` uses
  the real names/team IDs from that seed; omit it (or pass `--random`) for 16 invented teams of 4.

## Troubleshooting

- **`UNKNOWN PATH <method> <path>` in the FakePcob console** - the app requested an endpoint
  FakePcob doesn't implement. It always answers 404 rather than crashing, so the app should recover
  on its own 5s-timeout retry loop, but if a graphic depends on that endpoint it will simply stay
  empty. Check the printed path/method against RECON.md's endpoint list (`gettotalplayerlist`,
  `getteaminfolist`, `getcircleinfo`, `getkillinfo`, `isingame`) - if it's a genuinely new endpoint
  the app added, it needs a case added to `FeedServer.cs`'s route map.
- **Circle bar (`CircleStatusRenderer`) never lights up on `/overlay`** - expected. See REPORT.md,
  "Predicted failures" - nothing in the app currently broadcasts a `CircleUpdated` SignalR event,
  regardless of what FakePcob returns from `getcircleinfo`.
- **`knockouts`/`assists`/`headShotNum`/`survivalTime` read 0 all match** - also expected, and
  matches real PCOB behavior exactly (SEEDS.md's "PlayerAfterMatchAPI" group). They populate only
  once FakePcob reaches the match's final tick.
- **App can't reach FakePcob at all** - check `pcobUrl` in `appsettings.json` matches the `--port`
  FakePcob is listening on (default `10086` both sides), and that nothing else is bound to that
  port.
