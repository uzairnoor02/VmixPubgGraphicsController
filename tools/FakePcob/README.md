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

FakePcob can sit between a real vMix PC and a real PCOB server during an actual test match, log
every response, and replay that exact capture later with no PCOB running at all.

**Only do this on a machine you don't mind pointing at a live PCOB feed for real** - `record` is a
pass-through proxy: every request the app makes goes straight to the real server and the exact
response is both returned to the app and saved to disk, so a live test match's data flows through
this tool unmodified.

```
dotnet run --project tools/FakePcob -- record --upstream http://<real-pcob-ip>:10086/ --out tools/FakePcob/recordings
```
Point `appsettings.json`'s `pcobUrl` at `record`'s own address (default `http://127.0.0.1:10086/`)
instead of the real PCOB directly, so every poll goes through the recorder. It writes
`recordings/<timestamp>/<endpoint>/<seq>_<unixMs>.json` plus an `index.ndjson` per session.

To replay a capture later with no PCOB or network involved:
```
dotnet run --project tools/FakePcob -- serve --replay tools/FakePcob/recordings/<timestamp>
```

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
