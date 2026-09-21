# Phase 1 — Fake PCOB Server, Match Replay Harness, Demo Page & Transparent Backgrounds

Branch base: `feature/react-webapi-migration` (the NEW branch — test only, production runs the old branch).
Repo: `C:\Users\uzair\source\repos\uzairnoor02\VmixPubgGraphicsController`
Save this file in the repo as `docs/testing/PHASE-1-FAKE-PCOB.md`.

---

## PART A — For Uzair, before you sleep (5 minutes)

1. **Commit or stash** anything uncommitted on `feature/react-webapi-migration`.
2. **Unzip `fakepcob-seed.zip`** into the repo so you end up with `tools/FakePcob/seed/` containing
   `SEEDS.md`, five real match JSONs and two demo fixtures. These are already converted — the agent
   must never open them, only load them at runtime.
3. **Create** `.claude/settings.local.json` in the repo root (lets the agent work without asking, and
   stops it running anything):
   ```json
   {
     "permissions": {
       "allow": [
         "Read", "Edit", "Write", "Glob", "Grep",
         "Bash(dotnet build:*)", "Bash(dotnet new:*)",
         "Bash(git status:*)", "Bash(git branch:*)", "Bash(git switch:*)",
         "Bash(git checkout:*)", "Bash(git add:*)", "Bash(git commit:*)",
         "Bash(mkdir:*)", "Bash(ls:*)", "Bash(npx tsc:*)"
       ],
       "deny": [
         "Bash(dotnet run:*)", "Bash(dotnet test:*)", "Bash(dotnet ef:*)",
         "Bash(git push:*)", "Bash(git reset:*)", "Bash(rm:*)",
         "Bash(npm run dev:*)", "Bash(npm start:*)", "WebFetch", "WebSearch"
       ]
     }
   }
   ```
4. In a terminal at the repo root:
   ```
   claude --model sonnet --permission-mode acceptEdits
   ```
   Paste exactly this:
   > Read docs/testing/PHASE-1-FAKE-PCOB.md and follow PART B from Task 1 to Task 11 without asking me anything. Then stop.

In the morning read `tools/FakePcob/REPORT.md` first, then `tools/FakePcob/README.md`.

---

## PART B — Instructions for the agent

### B0. Hard rules — read twice

1. **Never ask the user a question.** They are asleep. If something is unclear, pick the most
   reasonable option, log it in `tools/FakePcob/REPORT.md` under "Decisions", and keep going.
2. **Never run the application.** No `dotnet run`, no `npm run dev`, no launching the app, the
   server, vMix or a browser. The only commands allowed are `dotnet build`, `npx tsc --noEmit`,
   `dotnet new`, `mkdir`, `ls` and the git commands in B3. The user runs everything themselves.
3. **Additive changes only.** Do not rewrite existing C# or existing renderers. You may edit exactly
   three existing things, and nothing else:
   - `Pubg Ranking System/appsettings.json` (Task 8)
   - the dashboard's route/nav file, to register one new page (Task 10)
   - `src/studio/renderers/` and the overlay config type, **only** for the background-opacity change
     in Task 11, and only additively with a safe default.
   If a test cannot work without some other production change, **do not make it** — describe it in
   REPORT.md under "Production changes needed".
4. **Blocked? Don't stop.** Write the blocker in REPORT.md and move to the next task. Tasks 9–11 are
   independent of Tasks 1–8, so a failure in one half must not stop the other.
5. **No `getkillinfo` logic.** The user does not use it. Kills come from the per-player `killNum`
   counters summed per team (see `LiveRankings.cs`). The fake server returns a valid *empty*
   `getkillinfo` response only so nothing throws.

### B1. Token budget rules — this run must be cheap

- **Search before reading.** Grep with `-C 5` first, then Read with `offset`/`limit`. Never read a
  whole large file.
- **Never open** any file in `tools/FakePcob/seed/*.json`. `seed/SEEDS.md` already documents every
  field, every value range and one verbatim sample record. That is all you need. The code loads the
  JSON at runtime.
- **Never read** `bin/`, `obj/`, `dist/`, `node_modules/`, the orphaned `Vmix Hangfire Graphics/`
  project, or any doc other than this file and `seed/SEEDS.md`.
- **Generate data with code, never by hand.** Do not write match JSON yourself. Ever.
- **Write each source file once, complete.** Avoid many small edits.
- No subagents, no web access, no plans or progress summaries in chat. Notes go in files.
- Each build error gets **at most 3 fix attempts**. Then record it in REPORT.md and move on.

### B2. What is being built

**`tools/FakePcob/FakePcob.csproj`** — one standalone console/web executable, **not** added to the
`.sln`. `Microsoft.NET.Sdk.Web`, `net8.0-windows`, `<UseWindowsForms>true</UseWindowsForms>` (that
gives System.Drawing in-box for image generation). **No NuGet packages at all.**

| Verb | Purpose |
|---|---|
| `serve --match m1 [--speed 1] [--paused] [--port N] [--push http://...]` | Replays a real match as a live feed. One tick per 2s ÷ speed. |
| `serve --scenario scripted [--seed 42]` | The scripted beat list (Task 5) instead of a straight replay. |
| `serve --replay recordings/<folder>` | Replays a recording captured from the real PCOB. |
| `record --upstream http://<real-pcob>:<port>` | Pass-through proxy that saves a real match to disk. |
| `dump --match m1 --out frames` | Writes every tick to `frame_000.json …` plus `EXPECTED.md`. |
| `assets --out out` | Fake team logos, player photos and a `teams.json`. |

Plus, in the dashboard: a **Demo page** (Task 10) and **background opacity** (Task 11).

### B3. Git

Start with `git switch -c test/fake-pcob` from `feature/react-webapi-migration`. Commit after each
task. Never push.

---

## Task 1 — Recon → `tools/FakePcob/RECON.md` (≤160 lines)

Answer each with file path, line numbers and exact names. Grep for: `gettotalplayerlist`,
`playerInfoList`, `getteaminfolist`, `getcircleinfo`, `HttpClient`, `BaseAddress`, `MapPost`,
`MapGet`, `liveState`, `killNum`, `Grenade`, `Vehicle`, `AirDrop`, `FirstBlood`, `Domination`,
`Top4`, `IsEliminated`, `PlayerImages`, `chromaKey`, `elementVisibility`.

1. **PUSH OR PULL — answer this first, it decides the whole server design.**
   The official API remark says the PCOB client *POSTs* `gettotalplayerlist` to a **local HTTP
   server**. But the app may also poll. Determine which this app does:
   - Does the app expose a listener (`MapPost`, a `HttpListener`, an ASP.NET endpoint) that PCOB
     posts *into*? Give the exact route and port.
   - Or does it call out with `HttpClient` to a PCOB address? Give the exact URL and query.
   - It may do both. Record whichever it does, with the config key holding the address/port.
2. **Response envelope.** Confirm it is `{"playerInfoList":[...]}` and note the serializer
   (Newtonsoft vs System.Text.Json) and any naming policy.
3. **Fields actually read.** Which of the 43 player fields does the live path read, and where.
4. **Team aggregation.** Where `LiveRankings.cs` gets a team's kill count and alive count from —
   summed per player, or a separate teams source. The simulator must keep both consistent.
5. **Achievements.** For every method in `SetPlayerAcheivments.cs`: the field watched, the threshold,
   and the dedupe rule (once per player? once per increment?). This decides whether a second
   grenade kill by the same player can fire a second popup.
6. **Top4 switch.** The exact condition that hides live rankings and shows the Last-4 widget, and how
   "team eliminated" is decided. **Note that `bHasDied` is `false` for every player even in real
   end-of-match captures — so it is useless. Confirm the code uses `liveState == 5`.**
7. **Circle.** What `getcircleinfo` fields are read and how "closing" is detected.
8. **Match start/end detection.**
9. **Team loading.** The exact JSON that "Load Teams" accepts.
10. **Images.** Player photo and team logo path conventions and expected sizes.
11. **appsettings parsing.** Is it ever read by a strict parser that would choke on `//` comments?
12. **Renderers & config (for Tasks 10–11).** List the files in `src/studio/renderers/`, the exported
    component name of each, and the props each takes. Find where a renderer's panel background is
    styled (the shared panel/surface style, the theme token for panel background, any
    `backdrop-filter`, `box-shadow`, `border`). Find the overlay config type that carries
    `chromaKeyColor` and `elementSettings`.

Commit.

## Task 2 — Scaffold

Create the csproj and `Program.cs` with verb dispatch. Suggested files: `Program.cs`, `Seeds.cs`,
`MatchBuilder.cs`, `Scenario.cs`, `Frame.cs`, `FeedServer.cs`, `Recorder.cs`, `Assets.cs`,
`Expected.cs`. Build. Commit.

## Task 3 — Reverse-replay engine (`Seeds.cs`, `MatchBuilder.cs`) — the core idea

**Do not invent match data.** Each seed file is a real *end-of-match* snapshot, and a real end state
contains everything needed to reconstruct a plausible match that leads to it. Build the timeline
**backwards from the real final numbers**, so every value the app sees is real-derived and every
counter lands exactly on its real final value.

Reconstruction rules:

- **Death order and timing** come from `survivalTime`, which is the real number of seconds each
  player survived. Sort ascending and you have the true death sequence of the real match. Match
  length = `max(survivalTime)`. Tick count = that ÷ 2.
- A player's `liveState` is `0` while `tick*2 < survivalTime`, then `5`. Insert a **knocked window**
  of 3–6 ticks at `liveState 4` immediately before death, with health bleeding down to 0. Give
  roughly one in six knocked players a **revive** instead: back to `liveState 0` at 10–20% health,
  with their real death pushed later. Revives must never push a death past `max(survivalTime)`.
- **Counters build up to their real totals.** `killNum`, `damage`, `killNumByGrenade`,
  `killNumInVehicle`, `gotAirDropNum`, `maxKillDistance` all start at 0 and rise in steps, reaching
  exactly the seed's final value on the player's last live tick. Distribute each kill at a tick where
  *some other player actually dies*, so a kill increment always coincides with a real death.
  Never let a counter decrease.
- **Health** for a live player outside a fight drifts ±0–4 per tick and is clamped to
  `[1, healthMax]`; during a fight it drops in 8–35 steps. It must visibly change most ticks.
- **Location** interpolates from a spawn point to the player's real final location (where they
  actually died), with small per-tick jitter. A dead player's location freezes at the real value.
- **`rank`** is 0 while alive and set to the seed's real value once dead. Live team ranking is
  computed by the app, not by you.
- Invariants, asserted in code while building the frame list — throw with a clear message on
  violation: dead stays dead; dead health is 0; counters never decrease; `healthMax` never changes;
  every player's final frame equals the seed record exactly for every live-group field.

**Seeds available** (details in `seed/SEEDS.md` — read that file, not the JSONs):
`m1` 16 teams/64 players, 9 grenade kills, 1 vehicle kill, 3 airdrops · `m2` 16 teams/**63 players**
(one team of 3), 0 vehicle kills · `m3` **15 teams**/60 players, 10 grenade kills · `m4` 15 teams,
**7 vehicle kills**, 3 airdrops · `d3m2` 16 teams, non-contiguous teamIds `2–9, 18–25`.

Use `m1` as the default. The variety matters: 15 teams tests the 8/7 ranking page split, 63 players
tests a 3-player team, and non-contiguous teamIds catch any code assuming ids 1..16.

Build. Commit.

## Task 4 — The three-route timing model (`Frame.cs`) — **do not skip this**

The 43 fields are **not** one payload. Per the official API rules, the server sends three groups on
two independent routes, and the PCOB client forwards each as its own POST. The fake feed must
reproduce that exactly, because it is the single most likely source of real bugs.

| Group | Fields | Emission rule |
|---|---|---|
| **PlayerBaseInfo** (route A) | `location`, `health`, `healthMax`, `liveState`, `killNum`, `killNumBeforeDie` | every tick |
| **PlayerRealTimeAPI** (route B) | `gotAirDropNum`, `maxKillDistance`, `damage`, `killNumInVehicle`, `killNumByGrenade`, `rank`, `isOutsideBlueCircle` | only on ticks where a value actually changed; otherwise hold the previous value |
| **PlayerAfterMatchAPI** (route B) | `inDamage`, `heal`, `headShotNum`, `survivalTime`, `driveDistance`, `marchDistance`, `assists`, `outsideBlueCircleTime`, `knockouts`, `rescueTimes`, `useSmokeGrenadeNum`, `useFragGrenadeNum`, `useBurnGrenadeNum`, `useFlashGrenadeNum` | **hard 0 for the entire match**, populated with the seed's real values only in the final frame |

Add `--routes merged|split` (default `split`):
- `split` — route A and route B are emitted as separate payloads, and route B is deliberately offset
  by one tick so the app regularly sees a fresh health with a stale damage. This is realistic.
- `merged` — one combined payload per tick, for isolating whether a bug is caused by split routes.

Two consequences to record in REPORT.md:
1. Any graphic or achievement reading `knockouts`, `assists`, `headShotNum` or `survivalTime` **cannot
   work mid-match** — it sees 0 until the match ends. Live knock state must come from `liveState == 4`.
2. A graphic must not flicker or reset when only one route updates.

Build. Commit.

## Task 5 — Scripted scenario (`Scenario.cs`)

`--scenario scripted` overlays a guaranteed beat list on top of the `m1` replay, so every graphic is
exercised even if the real match did not happen to produce it. Keep the real death order; only
re-label *how* some kills happened and inject the circle timeline.

| ≈tick | Beat | Expected on screen |
|---|---|---|
| 0–5 | liveState 1 (plane) → 2 (parachute) → 0 | live rankings, all teams 4/4 |
| 6–9 | idle drift | health bars move every update |
| 10 | first knock, then the first kill of the match | **First Blood** popup |
| 12–18 | two teams fight; knocks, one revive | health falls and recovers; knocked state visible |
| 20 | player P1 gets a grenade kill | **Grenade Elimination** popup (P1) |
| 24 | a kill from a vehicle (killer `liveState 3`) | **Vehicle Kill** popup |
| 28 | a distant player loots an airdrop | **Airdrop** popup |
| 32 | **P1 again** gets a grenade kill | a **second** Grenade popup for P1 |
| 34–39 | circle countdown ≤10s, then closing | **Zone Closing** bar |
| 40 | last member of a team dies | **Team Eliminated** popup |
| 42–50 | one player crosses Kill Domination, another Damage Domination | both domination popups |
| 51+ | teams eliminated one at a time, fights and a new circle phase in between | a popup each time; rankings reorder |
| — | include a team down to **1 alive**, and a team with **all alive members knocked** | alive counts right; neither shown as eliminated |
| 4 teams left | | **live rankings hide, Last-4 bar appears top-middle** |
| then | one more eliminated | bar shows **3**; the others must **not** change position |
| then | 2, then 1 | bar shrinks; winner state |
| end | match-finished signal, after-match fields populate | post-match graphics get real values |

Every achievement found in Recon 5 must fire at least once; add a beat for any not listed above.
`--scenario random --seed N` shuffles the beats, keeping these constraints: First Blood is always the
first kill, the second grenade kill is always the same player and later, at least two circle phases,
and the Last-4 switch only at 4 teams.

Each beat records an `ExpectedEvent{tick, text}` in plain English, e.g.
`"FIRST BLOOD popup — mgATIFFFF (MAGICIANS ESPORTS)"`. Build. Commit.

## Task 6 — The feed server (`FeedServer.cs`)

- **Match the app's transport from Recon 1.** If the app *listens* for POSTs, the tool POSTs each
  frame to that URL (`--push`). If the app *polls*, serve the same paths and query strings so no app
  code changes. If Recon 1 is ambiguous, **implement both** — push when `--push` is given, serve
  otherwise — and say so in REPORT.md.
- Bind to `127.0.0.1`. `getcircleinfo` is served from the scenario's circle timeline. `getkillinfo`
  returns a valid empty response. Any unknown path returns 404 and logs `UNKNOWN PATH <path>` so the
  user can see what the app actually wanted.
- **Clock:** advance one tick every `2000ms ÷ speed`, holding at the final frame.
- **Control API:** `GET /_sim/status`, `POST /_sim/pause|resume|step|restart`, `/_sim/speed?x=`,
  `/_sim/jump?tick=`.
- **`GET /_sim`** — one self-contained HTML page, inline CSS/JS, no CDN: pause/resume/step, speed
  buttons (0.5x 1x 2x 5x), a jump box, the current tick, and the expected-event checklist with the
  current row highlighted. Polls `/_sim/status` once a second. Must be usable on a phone.
- **Console:** print `[T037 01:14] alive 12 teams / 38 players` each tick and `>>> EXPECT: ...` for
  each beat.
- Build. Commit.

## Task 7 — Record & replay real PCOB (`Recorder.cs`)

`record --upstream <real-pcob>`:
- Accept on the same paths as `serve` (both directions if the app pushes). Forward unchanged with one
  shared `HttpClient` and a 3s timeout. Return upstream's status and body **byte-for-byte**; never
  parse on the response path. On failure return 502 and log it — never crash, never hang the app.
- Persist **off the response path** via a background writer; a disk error must never delay a response.
- `recordings/<yyyyMMdd-HHmmss>/<endpoint>/<seq:00000>_<unixMs>.json` plus `index.ndjson`
  (seq, unixMs, endpoint, query, status, bytes, latencyMs). Print the folder at startup.

`serve --replay <folder>`: rebuild frames from the recording, keeping real inter-tick timing scaled by
`--speed`. No scripted expectations; instead auto-detect and list events from the data — first kill,
each team reaching 0 alive, grenade/vehicle/airdrop counter increments, and the tick where alive teams
hit 4 — so the checklist still works. Build. Commit.

## Task 8 — Dump, assets, appsettings

- `dump` writes `frames/frame_000.json …` (each file holding every route's payload for that tick) plus
  `frames/EXPECTED.md`, a table of tick | time at 1x | expected on screen | ☐ pass.
- `assets` generates 16 teams (name, 3–4 letter tag, distinct colour — invented, no real org names),
  64 players, a 256×256 PNG logo per team (solid colour, rounded square, tag in bold white) and a
  256×256 PNG per player (darker gradient, simple silhouette, initials). Name the files per Recon 10.
  Write to `tools/FakePcob/out/images/` — **never into the real image folders**. Also write
  `out/teams.json` in the exact Load-Teams format, tournament `FAKE TEST CUP`.
  Add `--from-seed m1` to use the seed's real team and player names instead of invented ones, so the
  photos line up with replayed matches.
- **appsettings:** if the PCOB address lives in appsettings and Recon 11 found no strict parser,
  comment the existing line with `//` and add the fake one below it marked
  `// FAKE PCOB — restore the line above for real matches`. If a strict parser exists, keep the
  original in a sibling `"<Key>_REAL_BACKUP"` key instead. If the address is hardcoded in C#, change
  nothing and record the file and line under "Production changes needed".
- Build. Commit.

---

## Task 9 — Background opacity, including true 0% (dashboard) — **required feature, not a test**

Today a graphic's panel background cannot be turned off. It must be.

- Add `backgroundOpacity` (0–100, default = current appearance, so nothing changes until touched) per
  graphic element, stored in the existing `elementSettings` so it saves through
  `POST /api/overlay/config` with no backend change.
- Implement once, in a shared helper — e.g. `src/studio/renderers/surface.ts` exporting
  `panelSurface(opacity, theme)` — and use it in every renderer. Do not fork the logic per renderer.
- **At 0 the panel must paint nothing at all.** Not `rgba(...,0)` — emit **no** `background`, **no**
  `backdrop-filter`, **no** `box-shadow` and no inner glow. This matters because a transparent panel
  that still carries `backdrop-filter: blur()` visibly blurs the gameplay footage behind it in vMix,
  and a `box-shadow` paints semi-transparent black that keys badly. Between 1 and 99 the panel's
  background alpha scales; `backdrop-filter` scales with it and is dropped entirely below 10.
- Text, logos, bars and borders are **not** affected by this control — only the panel surface.
- Add a **canvas background mode** alongside the existing `chromaKeyColor`:
  `chroma` (today's behaviour, default) | `transparent` (paint nothing — for vMix Browser Sources
  that handle real alpha) | `image` (preview only) | `solid`.
  Store the image mode under a `preview.` key prefix and make `Overlay.tsx` ignore any `preview.*`
  key, so a preview background can never reach air. That mirrors the existing `director.*` convention.
- Expose both controls in Overlay Settings: an opacity slider per graphic and the canvas mode picker.
- Verify with `npx tsc --noEmit`. Commit.

## Task 10 — Demo page: see every graphic without a match (`src/pages/DemoPage.tsx`)

A page the user opens to see how everything looks on air, with no backend, no PCOB and no match
running. Register one route (`/demo`) and one nav entry — that is the only existing file you touch.

- **Full-bleed canvas** filling the viewport, showing graphics positioned exactly as they are on air.
- **Bottom control bar**, fixed, with one toggle button per graphic (reuse the catalogue in
  `src/lib/graphics.ts` rather than writing a new list). Clicking toggles that graphic on the canvas.
- **Mutually exclusive pair:** turning on **Last 4** must turn **Live Rankings** off, and vice versa —
  that is the behaviour being demonstrated. A **"Simulate elimination"** button steps the Last-4 bar
  from 4 → 3 → 2 → 1 so the user can watch the positions of the remaining teams stay put.
- **Background picker** in the same bar: chroma green / chroma blue / custom hex / a background image
  (file picker, held in memory only) / transparent checkerboard. This is how the user checks what
  keys out cleanly.
- **Opacity slider** in the bar, wired to Task 9's control, applying to whichever graphic is selected,
  with a quick "all → 0%" button to confirm every panel truly disappears.
- **Data:** load `demo-midmatch.json` and `demo-last4.json` from `seed/`. Copy them into the
  dashboard's `public/demo/` so the page can fetch them at runtime — do not inline the data in the
  source. Mid-match has 16 teams, 6 eliminated, 2 knocked, healths across the whole gradient, one
  grenade kill, one vehicle kill and one airdrop. Map them to renderer props using the **same shape
  the Studio pages already pass** — reuse each Studio page's existing sample-data wiring rather than
  inventing new props. If a renderer's props cannot be produced from this data, render it with the
  Studio's own sample data and note it in REPORT.md.
- Achievement popups fire on click, so the user can trigger First Blood, Grenade, Vehicle and Airdrop
  by hand and watch the animation.
- Everything is client-side; the page must work with the backend stopped.
- Verify with `npx tsc --noEmit`. Commit.

## Task 11 — README.md + REPORT.md (`tools/FakePcob/`)

**README.md** — short, copy-paste commands:
1. `dotnet run -- assets --from-seed m1`, then copy `out/images/*` to the folders from Recon 10.
2. Start the app. Load `out/teams.json` from the dashboard.
3. `dotnet run -- serve --match m1 --paused`, open `http://127.0.0.1:<port>/_sim`, open the
   dashboard and `/overlay`, press Resume, tick through `EXPECTED.md`.
4. Variations: `--speed 5`, `--scenario scripted`, `--match m3` (15 teams), `--match m2` (63 players),
   `--routes merged` to check whether a bug comes from split routes.
5. `/demo` for the click-through page with no match running.
6. Recording a real match: `dotnet run -- record --upstream http://<real-pcob-ip>:<port>` with
   appsettings pointing at FakePcob. Replay later with `serve --replay recordings/<folder> --speed 2`.
   **Warn: test PC only, never in the production broadcast path.**
7. How to restore the real PCOB address.
8. Troubleshooting: `UNKNOWN PATH` in the console means the app wants an endpoint the fake does not serve.

**REPORT.md**:
- **Done**, per task, with build status.
- **Decisions** taken without asking.
- **Push or pull** — what Recon 1 concluded, and which mode was implemented.
- **Predicted failures**, from reading the code: for each expected on-screen event you believe will
  *not* appear on this branch, one line with the reason and file:line. Known suspects: no C#
  `CircleUpdated` broadcast exists yet, so the zone bar will stay dark; second-grenade dedupe; Last-4
  position stability; a fully-knocked team being treated as eliminated; anything reading
  after-match fields mid-match.
- **Production changes needed.**
- **Not done / blockers.**

Final commit, then print exactly: `Phase 1 complete — see tools/FakePcob/REPORT.md`

---

## Later phases (not this run)

Phase 2 fixes what the test run exposes, one small task per failure. Then: the outstanding Build
Solution on the 12 Sep changes; replacing the `"1234"` dashboard key; C# broadcasts for the nine
silent overlay slots; `Location` in `FilterPlayerInfo`; the reliability floor (local-first overlay,
cached config and last tick, SignalR reconnect with replay); per-tournament match control; the Azure
move; Paddle billing; the DB decision; CI running this harness on every commit.
