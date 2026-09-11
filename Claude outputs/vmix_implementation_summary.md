# VmixPubgGraphicsController — What Was Built This Pass

All changes were made directly in your repo (`Pubg Ranking System`, `VmixGraphicsBusiness`, plus two new projects). **I could not run a compiler in this session** — the sandbox this task runs in has file access to your repo but no .NET SDK and no network to install one, so none of this has been build-verified. Please open the solution in Visual Studio, build, and smoke-test before a live event. Everything below was reviewed carefully by hand and the changes are deliberately conservative/mechanical where possible, but treat this as a thorough first draft, not a merge-and-forget change.

## 1. Redis is gone from the hot path

New file: `VmixGraphicsBusiness/Utils/MatchStateStore.cs` — an in-process, thread-safe store that mirrors Redis's `IDatabase` shape (`StringGet(Async)`, `StringSet(Async)`, `KeyDeleteAsync`, `KeyExpireAsync`) so every call site could be swapped over mechanically instead of redesigned. It snapshots to a local JSON file every 5 seconds for crash recovery, and exposes two in-process events (`MatchStatusChanged`, `LiveTeamsUpdated`) that replace the old Redis pub/sub channel.

Migrated off Redis entirely: `GetLiveData.cs`, `LiveStatsBusiness.cs`, `LiveStatsBusiness.top4.cs`, `LiveStatsBusiness.SetPlayerAcheivments.cs`, `Reset.cs`, `Form1.cs`, `Program.cs`. Deleted `Utils/Redis.cs` (moved to `_to_delete/` — nothing could delete it directly, see note below). Removed the `StackExchange.Redis` package from both `VmixGraphicsBusiness.csproj` and `Pubg Ranking System.csproj`, and `Hangfire.Redis.StackExchange` from the latter.

Hangfire still exists, but only for the small non-time-critical jobs left (achievement popups, reset/animation pushes) — it now runs on `Hangfire.MemoryStorage` (zero external services) instead of Redis, with **one** `BackgroundJobServer` instead of the previous five (each of those five ran `ProcessorCount * 5` workers — roughly 200 threads competing for the same queues on an 8-core machine, which was actively working against the ordering guarantees this fix relies on).

**Not migrated:** `Vmix Hangfire Graphics/Program.cs` still references Redis — this is the orphaned project that isn't in the `.sln` and doesn't compile as-is (see the audit doc). Left untouched since it isn't shipped.

## 2. Last-4-teams position swap — fixed at the root cause

Two concrete bugs, both fixed:
- `Reset.cs` never cleared the Top4 position lock. It now calls `matchState.ResetMatchState()` at the start of every reset, and `GetLiveData.FetchAndPostData` also calls it at the start of every match — so a stale mapping from a previous match can no longer leak into the next one.
- The position lock previously lived in Redis, so any Redis hiccup made the code treat "can't read the key" as "first time entering Top 4" and re-sort from current stats — which is what produced the swapping. With Redis removed from this path, that failure mode is gone structurally, not just patched over.

## 3. Survival / win-probability % — the knocked-player bug

`LiveStatsBusiness.top4.cs`: a team's win-probability score used to average health only across players in `LiveState == 0` ("standing"), while counting every non-dead player (including knocked-out ones) at full weight in the member-count term. A team with 3 knocked players and 1 healthy one showed the same score as a team with all 4 standing. Knocked players (`LiveState == 4`) now contribute at 15% weight instead of being excluded, and the "standing" check was widened from exactly `LiveState == 0` to the full `0-3` range `EvaluateLiveStatus` itself already treats as alive (narrower before, for no apparent reason). The 15% weight is a reasonable starting point, not a calibrated number — **send over the PUBG international tournament last-4 data when you're ready and I'll tune it against real outcomes.**

## 4. Live polling loop — latency and reliability

`GetLiveData.cs`: replaced `new HttpClient()` per call (three places) with one shared, reused client with a 5s timeout; replaced the Hangfire-enqueue-per-tick hop with a direct `await` call (removes a Redis round trip *and* the risk of a tick being silently dropped by a distributed-lock timeout); replaced the unconditional `Task.Delay(1000)` with an adaptive delay that accounts for how long the tick's HTTP calls + processing actually took, so the loop can't silently drift past PUBG's own ~2s update cadence under load.

## 5. Hybrid database: MySQL with automatic SQLite fallback

`Program.cs`: on startup, probes MySQL with a 4-second timeout (via `ServerVersion.AutoDetect`). If it's unreachable, falls back to a local SQLite file at `state/vmix_fallback.db` automatically — the app can no longer fail to start just because the DB server is down. This is a startup-time choice, not live failover: if MySQL comes back mid-session, restart the app to pick it back up. Also registered a pooled `IDbContextFactory<vmix_graphicsContext>` alongside the existing scoped context, for short-lived-context use (see next section).

## 6. Post-match pipeline — crash isolation

`PostMatch.cs`'s `createPostMtachStats` used to run 7 steps back-to-back with no error isolation — one unhandled exception partway through silently dropped every step after it, and (since `GetLiveData.cs` didn't wrap the call either) could propagate out and take the whole match-processing flow down right as a match finished. Each step is now wrapped individually (`RunPostMatchStepAsync`) so a failure is logged and the rest of the report still gets produced; `GetLiveData.cs` also now wraps the call itself as a second layer. Removed an unexplained hardcoded `Task.Delay(1000)` between the first two steps (unnecessary given they're already properly awaited in sequence).

**Deferred, on purpose:** the deeper fix — giving each step its own short-lived `DbContext` via the new `IDbContextFactory` instead of one context held for the whole match's duration — touches ~7 files and several hundred lines I've only partially read, each calling a shared `_vmix_GraphicsContext` field throughout. Doing that blind, without the ability to compile-check it, was too likely to introduce a subtle bug in exactly the code you said must not crash. The factory is registered and ready; this is the natural next slice of work.

## 7. Live web dashboard (Phase 2, view-only first pass)

New file `Pubg Ranking System/LiveDashboardHost.cs`: runs a second embedded Kestrel host (same pattern already used for the Hangfire dashboard) on port 5050, bound to `0.0.0.0` so it's reachable from anywhere on the LAN. Exposes a SignalR hub (`/hubs/match`) that pushes live team stats and match status the instant `GetLiveData` computes them, plus two GET endpoints so a browser that connects mid-match gets current state immediately, and a `POST /api/match/reset`.

New folder `vmix-dashboard/`: a real, buildable React + Vite + TypeScript app (`npm install && npm run dev`) that connects to that hub and renders a live grid of all 16 teams. See its `README.md` for how to point it at the graphics PC's address from another machine.

**Scoped out on purpose, for now:** starting and ending matches from the browser. `start_btn_Click` has real branching confirmation dialogs (resume an in-progress match vs. restart a completed one, with a type-DELETE confirmation) and `stop_Click` restarts the entire WinForms process — neither should be wired to a web button without deliberately redesigning that flow as an API contract first, which is real design work, not a mechanical port. WinForms keeps doing match control for now, exactly as you asked.

Added a `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to `Pubg Ranking System.csproj` — needed for SignalR/minimal-API types to be available at compile time in a WinForms (non-Web SDK) project.

## 8. Dummy-data verification harness

New project `DummyDataHarness/` (added to the `.sln`, runnable with `dotnet run` from that folder). Doesn't try to drive the full `LiveStatsBusiness` (that needs a live vMix instance, Hangfire and EF Core wired up — stubbing all of that blind wasn't worth the risk). Instead it exercises the two things that actually needed proving:
- Runs `MatchStateStore` through the exact same call sequence `CreateTop4LiveRanking` makes, across 50 simulated ticks, and asserts no team's locked position ever changes — and that `ResetMatchState()` actually clears it.
- Exercises the survival-score weighting formula (mirrored from `top4.cs`) against a "3 knocked + 1 healthy" team vs. a "4 standing at 60%" team, and asserts the knocked-heavy team now scores lower, not the same or higher.
- Generates a dummy 16-team/64-player match as a reusable fixture for further manual testing.

## What to do next, in order

1. Open the solution in Visual Studio, build, fix whatever the compiler finds (I'd bet on small things — a missed `using`, a type mismatch I didn't catch by eye).
2. Run `DummyDataHarness` — all 4 tests should print `[PASS]`.
3. Smoke-test a full match end-to-end against dummy/replay data before touching a live tournament, with particular attention to the 5-to-4-team transition and a team going fully knocked.
4. Send over the PUBG international tournament last-4 data so the survival-% weights can be calibrated against real outcomes.
5. When ready: extend the dashboard with match control (start/end), and finish the post-match `IDbContextFactory` migration for the remaining 6 steps.
