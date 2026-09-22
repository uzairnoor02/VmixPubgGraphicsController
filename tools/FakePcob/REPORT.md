# Phase 1 report - FakePcob + Task 9/10

Branch: `test/fake-pcob` (off `feature/react-webapi-migration`). One commit per task, never pushed.
This report covers all 11 tasks from `docs/testing/PHASE-1-FAKE-PCOB.md`, run unattended per that
doc's own Part A instruction and Hard rules (B0).

## Done

| Task | What | Commit | Build status |
|---|---|---|---|
| 1 | Recon of PCOB transport/fields/achievements/Top4/circle/overlay config | `42c95ba` | N/A (markdown) |
| 2 | `tools/FakePcob` scaffold, CLI args, seed models/loader | `050ad18` | **Not verified** - see Blockers |
| 3 | Reverse-replay engine (`MatchBuilder.cs`) - tick timelines built backwards from real end-of-match seeds | `74d3f69` | Not verified |
| 4 | Three-route timing model (`Frame.cs`) - merged/split `gettotalplayerlist`/`getteaminfolist` | `9b29518` | Not verified |
| 5 | Scripted/random scenario engine + circle timeline + expected-event checklist (`Scenario.cs`) | `5f56741` | Not verified |
| 6-7 | Feed server (`serve`, `_sim` control page, `/_sim/*` API) + record/replay (`Recorder.cs`) | `69785ba` | Not verified |
| 8 | `assets` command (fake teams/logos/photos), `appsettings.json` `pcobUrl` swap | `7f83bdc` | Not verified |
| 9 | Shared `panelSurface()` opacity helper wired into all 13 panel renderers; canvas background mode (chroma/transparent/solid); Overlay Settings UI | `5813ca1` | **Verified** - `npx tsc --noEmit` clean |
| 10 | `/demo` page - click-through preview of every graphic, no backend/match needed | `e84e283`, `f7724c5` | **Verified** - `npx tsc --noEmit` clean |
| 11 | This report + README | (this commit) | N/A |

Tasks 1-8 (C#/FakePcob) and Tasks 9-10 (TypeScript/dashboard) are independent per the doc's own
instruction, and neither half's issues blocked the other - all 11 tasks completed to the extent
described here.

## Decisions

Judgment calls made without asking, per Hard rule B0.1 ("never ask a question - pick the most
reasonable option and log it here"):

1. **Push vs. pull** - confirmed via code (not assumed): the app polls PCOB with `HttpClient.GetAsync`
   (`GetLiveData.cs`), so FakePcob only needs to serve plain GETs. See RECON.md #1.
2. **"Three separate routes" (Task 4) re-interpreted as tick-offset semantics on one endpoint** -
   the app calls a single `gettotalplayerlist`/`getteaminfolist` per poll, not three distinct URLs.
   Implemented as `Frame.cs`'s `RouteMode.Merged` (base+realtime fields from the same tick) vs.
   `RouteMode.Split` (realtime fields lag one tick), matching SEEDS.md's documented route-A/route-B
   update-group split rather than fabricating endpoints the app never calls.
3. **`getkillinfo` returns `{"killInfoList":[]}`**, not a bare `[]` - matched to the primary shape
   `VmixData/Models/MatchModels/KillInfo.cs` assumes (with a bare-array fallback the app also
   handles), since the app's own comment says the real shape is unconfirmed.
4. **`bHasDied` always emitted as `false`** - deliberate, not a bug: SEEDS.md and RECON.md #6 both
   confirm the real PCOB field is unreliable and the app must (and does) key off `liveState`
   instead. Emitting it as always-false is the most faithful simulation of the real API's known
   defect, not an oversight.
5. **Second grenade/vehicle kill by the same player only fires when the seed data actually contains
   one** - `MatchBuilder`'s event-detection pass only emits an event where a real counter increase
   occurred; it does not fabricate a phantom second kill to exercise the achievement's "refires on
   each new kill" behavior (RECON.md #5) where a given seed's data doesn't naturally contain one.
   This is a data-availability limitation of the five real seeds, not a design gap - documented here
   rather than faked, per Hard rule B0 ("generate data with code, never by hand").
6. **Kill/Damage Domination treated as soft/best-effort expectations in `Scenario.cs`**, not hard
   assertions - both are relative-to-other-teams comparisons recomputed every tick from whichever
   frame the simulator happens to be on, so the "expected" checklist names them as likely rather
   than guaranteed at an exact tick.
7. **File-scope interpretation (Hard rule B0.3 vs. Tasks 9/10's own instructions)** - B0.3 lists
   three specific existing-file edits, one worded as applying "only for the background-opacity
   change in Task 11" (almost certainly a typo for Task 9, since Task 11 is this report and has no
   opacity work). Task 9 itself explicitly asks to "expose both controls in Overlay Settings" and
   Task 10 to "register one new page" (implying a route file). Making Task 9/10 possible as
   specified required also touching `Overlay.tsx` (to actually apply opacity/canvas-mode on air -
   without this, Task 9's UI would save settings that do nothing), `OverlaySettingsTab.tsx` (the
   named settings UI), and `App.tsx` (the named route registration). Resolved by treating B0.3's
   enumeration as the general "stay additive, minimal blast radius" intent rather than a literal
   file whitelist that would make the named tasks impossible - every touch to these files is a new
   optional prop/key with a safe default, never a removed or renamed behavior.
8. **`SpectatorMapRenderer` excluded from the Task 9 opacity change** - its "panel" is a full-bleed
   map fill, not the glassmorphic `background`+`backdropFilter` pattern every other renderer shares;
   there is nothing for `panelSurface()` to scale.
9. **Task 10's `/demo` page uses the Graphics Studio's `SAMPLE_*` fixtures for every graphic**,
   not `tools/FakePcob/seed/demo-midmatch.json`/`demo-last4.json`. SEEDS.md documents those two
   fixtures as raw `{"playerInfoList":[...]}` records (the same 43-field pcob shape as the real
   match seeds) rather than the pre-aggregated `TeamLiveStats[]`/renderer-prop shapes this page
   needs - using them would mean re-implementing the server's own team/rank aggregation
   (`LiveStatsBusiness.cs`) a second time, client-side, solely for a demo page. Task 10 explicitly
   allows the simpler path ("if a renderer's props cannot be produced from this data, render it with
   the Studio's own sample data and note it here"), so every graphic on `/demo` takes that fallback,
   not only the ones the task anticipated needing it.
10. **`/demo`'s Standings <-> Top4/WWCD panel is mutually exclusive, driven by a "Simulate
    elimination" stepper (10 -> 1 teams alive)**, mirroring the real `ShouldShowTop4Ranking` rule
    (RECON.md #6: Top4 shows once `<=4` teams have any live member). Every other graphic gets an
    independent on/off toggle instead, since the real app shows them independently too.
11. **`/demo`'s Circle Status panel runs its own local countdown timer** rather than staying static
    - since nothing in the app broadcasts a `CircleUpdated` event yet (see Predicted failures
    below), `/demo` is the only place in the whole system today where this graphic can be seen
    moving at all, which seemed more useful than a frozen preview.
12. **No nav entry added to `AdminShell.tsx`** for `/demo` - Task 10 calls the route registration
    "the only existing file you touch" for this task, and `/demo` is unauthenticated and chrome-free
    like `/overlay` (both are meant to be opened as bare URLs, not navigated to from inside the
    logged-in shell), so only `App.tsx`'s pathname check was added.

## Push-or-pull conclusion

**Pull.** See Decision #1 and RECON.md #1. FakePcob is a plain HTTP GET server; it never needs to
push anything to the app.

## Predicted failures (real behavior, not a FakePcob bug)

- **Circle status bar stays dark on `/overlay`** regardless of what FakePcob's `getcircleinfo`
  returns or what `/_sim`'s expected-events list says. `Overlay.tsx` listens for a `CircleUpdated`
  SignalR event, and nothing in the current backend sends one (`GetLiveData.cs` polls
  `getcircleinfo` but never publishes it) - confirmed in RECON.md #7. `/demo`'s circle panel works
  because it runs its own local timer instead of waiting for that broadcast (Decision #11).
- **`knockouts`, `assists`, `headShotNum`, `survivalTime` read 0 for the entire match** on any
  graphic that reads them live - these are `PlayerAfterMatchAPI` group fields per SEEDS.md, and
  FakePcob (correctly) only populates them once the match reaches its final tick, exactly matching
  real PCOB behavior.
- Several post-match graphics (`mvpRankings`, `topPlayers`, `teamsToWatch`, `champions`,
  `headToHead`, `teamIntro`, `playerHighlight`) will stay empty on the real `/overlay` route no
  matter what FakePcob serves, because nothing in the backend broadcasts their corresponding
  SignalR events yet (`MvpRankingsUpdated`, `TopPlayersUpdated`, etc. - see the `RawXxx` interface
  comments already in `Overlay.tsx`, all pre-existing before this phase). `/demo` shows what they
  will look like once that backend work lands.

## Production changes needed (not made - additive-only constraint)

- A `CircleUpdated` SignalR broadcast from the existing `getcircleinfo` poll in `GetLiveData.cs`,
  so the live circle bar can ever show anything on `/overlay`. FakePcob already serves a correct
  `getcircleinfo` response with real phase/countdown data (`Frame.cs`'s `CircleTimeline`); only the
  publish side is missing, and Tasks 9-11 are limited to three existing-file edits (see Decision #7)
  that don't include this.
- The `MvpRankingsUpdated`/`TopPlayersUpdated`/`TeamsToWatchUpdated`/`ChampionsUpdated`/
  `HeadToHeadUpdated`/`TeamIntroUpdated`/`PlayerHighlightUpdated` broadcasts named above - all
  pre-existing gaps (not introduced by this phase), listed here because FakePcob's correctness
  can't be observed on the real `/overlay` route until they exist. `/demo` (Task 10) exists
  specifically so these graphics can be evaluated today without waiting on that work.

## Post-report fixes (build/runtime errors found after this report was first written)

While actually running the build in Visual Studio, three issues surfaced that this session's lack
of a local .NET SDK couldn't catch (see "Not done / blockers" below) - all fixed and committed:

- `Frame.cs` referenced an `AfterMatchFields` class (`.Zero`/`.FromSeed(...)`) that was never
  actually written - added it, mapping 1:1 to the `PlayerAfterMatchAPI` seed fields.
- `MatchBuilder.cs`'s tick-loop declared `int health;` with no default, and the compiler's
  definite-assignment analysis couldn't prove every code path assigned it before use (even though,
  at runtime, every reachable path did) - gave it a `p.HealthMax` fallback default.
- Runtime crash on `--match m1`: `AssertInvariants` threw `"uId ... came back from liveState 5 ...
  (dead must stay dead)"`. Root cause - a player's "survivor" (alive-through-the-whole-match) status
  was inferred from `SurvivalTime` rounding to the match length (`dt >= tickCount`), which breaks
  for a real survivor whose `SurvivalTime` is a couple of seconds under the match max (integer
  division put their computed death tick one short of the final tick) - the final-frame
  force-overwrite then correctly reset them to the seed's real `liveState: 0` (alive), which the
  invariant check read as an illegal revival. Fixed by deriving survivor status directly from the
  seed's own `LiveState == 0` instead of reverse-engineering it from timing.

**Note on map mechanics (raised by Uzair mid-session):** the newer "Rondo" map has a player-recall
mechanic where a dead player can come back mid-match, which would make "dead must stay dead" a
false invariant for a Rondo match. None of the five bundled seeds (`m1`-`m4`, `d3m2`) carry a map
name in `SEEDS.md`, and none of their final snapshots show a revival (every seed's dead-player count
is a clean monotonic total, no liveState transitioning back from 5) - so this fix is correct for all
five as they stand, and the invariant itself was not the bug. Simulating an actual mid-match recall
is out of scope here (it would need new tick-generation logic, not a bug fix) - flagged as a
limitation: **if FakePcob is ever pointed at seed data from a real Rondo match where a player's
final `liveState` reflects a comeback, `AssertInvariants` will legitimately reject it**, and
`MatchBuilder`'s reverse-replay model would need an explicit recall window (similar to the existing
knock/revive window, but crossing back from liveState 5) added as new work, not a fix.

## Not done / blockers

- **No C# code in `tools/FakePcob` has been verified with `dotnet build`.** This session's shell
  (`device_bash`) runs in an isolated Ubuntu 22.04 VM with the Windows repo folder bridge-mounted,
  not on a machine with the .NET SDK installed, and outbound network access to fetch one
  (`dot.net/v1/dotnet-install.sh`) was blocked by the environment's proxy (`403` on every attempt).
  Every `.cs` file under `tools/FakePcob/` (Args, SeedModels, Seeds, Program, MatchBuilder, Frame,
  Scenario, FeedServer, SimPage, Recorder, Expected, Assets) was written carefully against the
  actual model types in `VmixData`/`VmixGraphicsBusiness` and cross-checked by reading those types
  directly, but is **unverified by a compiler**. Per Hard rule B0.4 ("blocked? don't stop - write
  the blocker here and move on"), all eight FakePcob tasks were completed as far as possible without
  that verification rather than left undone.
  **Recommended next step: run `dotnet build tools/FakePcob/FakePcob.csproj` on a Windows machine
  with the .NET 8 SDK before relying on this tool**, and fix whatever the compiler finds - most
  likely spots for a mistake, in rough order of risk: `SeedModels.cs`'s `JsonPropertyName` casing
  against the one verbatim sample record in `seed/SEEDS.md` (43 fields, several irregular-cased
  ones like `AIKillNum`/`PoisonTotalDamage`); `MatchBuilder.cs`'s tick-loop indexing and the
  post-pass event-detection code (both large and never executed even once); and `FeedServer.cs`'s
  minimal-API route registrations.
- The TypeScript side (Tasks 9-10) **was** verified - `npx tsc --noEmit` ran cleanly from
  `vmix-dashboard/` after each change (node/npm/tsc were already present in this environment with
  no network needed), so that half of the phase has real compiler confidence behind it.
- No graphic was ever visually rendered in a browser (Hard rule B0.2 forbids running
  `dotnet run`/launching anything) - `npx tsc --noEmit` catches type errors but not layout mistakes,
  so a first real look at `/overlay` with FakePcob running and at `/demo` is still worth doing before
  trusting either.

---
Phase 1 complete - see tools/FakePcob/REPORT.md
