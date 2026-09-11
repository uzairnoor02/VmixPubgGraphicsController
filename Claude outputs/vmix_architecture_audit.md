# VmixPubgGraphicsController — Architecture Audit & Redesign Plan

Prepared after reading the actual code (via the linked repo on your PC), not guessing. Every finding below cites the file it came from.

## 1. What's actually in this repo

The solution (`VmixPubgGraphicsController.sln`) only lists three projects: `Pubg Ranking System` (WinForms), `VmixGraphicsBusiness` (class library), `VmixData` (models). Two more folders exist but are **not in the solution**:

- **`Vmix Hangfire Graphics`** — an ASP.NET Core host that references a `LiveStatsBusiness` class that doesn't exist anywhere inside that project, and its `.csproj` has no project reference to `VmixGraphicsBusiness` either. **This project cannot compile.** It's dead code from an earlier prototype.
- **`VmixPubgGraphicsController`** — another small web host with its own separate, much simpler `LiveStatsBusiness` (no Redis, no Hangfire, rank assigned by naive loop order). Also orphaned from the .sln.

The **real, running system** is `Pubg Ranking System` — a WinForms desktop app (`Program.cs`) that on `Main()`:
- Connects to Redis and MySQL (`vmix_graphicsContext` via Pomelo/MySQL)
- Spins up **5 separate Hangfire `BackgroundJobServer` instances**, each with `WorkerCount = Environment.ProcessorCount * 5` — on an 8-core machine that's ~200 competing worker threads pulling from the same 3 queues
- Opens a second Kestrel host on a background thread, with its own independent Redis connection, purely to serve the Hangfire dashboard on `:5001`
- Wires up `GetLiveData`, `LiveStatsBusiness`, `PostMatch`, `Reset`, etc. from `VmixGraphicsBusiness` — this is the code that's actually live

**I'd like you to confirm**: is `Pubg Ranking System` the build you run on match day? Everything below assumes yes.

## 2. Root causes of the three problems you described

### 2a. Redis / WSL is a single point of failure for things that never needed a network hop

`Utils/Redis.cs` wraps Redis as a generic cache, and `HelperRedis` keys (`TeamInfoList`, `PlayerInfolist`, `isEliminated:*`, `MatchStatus`, `Top4TeamPositions`, …) are read/written from `GetLiveData.cs`, `LiveStatsBusiness.cs` and `LiveStatsBusiness.top4.cs` constantly. But this is a **single process, single machine** app — nothing here is shared across machines or processes. Redis is being used as a poor-man's in-memory dictionary that happens to require a separate service, a TCP port, and (via WSL) a whole Linux VM to boot correctly before your app can even start. `Program.cs` in `Pubg Ranking System` will silently `return` with zero error message if the Redis connection string is empty — so a Redis hiccup doesn't even fail loudly, it just leaves you looking at a WinForms app that never launched.

Hangfire is *also* backed by Redis (`RedisStorage`), so the job queue itself dies with Redis.

**Bottom line: Redis is doing a job that `ConcurrentDictionary` + a local file for crash-recovery could do better, with zero external processes.**

### 2b. Why the pipeline isn't keeping up with the 2-second PUBG update cadence

Several compounding issues, all in the hot path (`GetLiveData.FetchAndPostData`):

- The loop does two **sequential** HTTP calls (`gettotalplayerlist`, `getteaminfolist`) plus a third for circle info (`GetCircleInfo`), each creating a **new `HttpClient()` instance per call** — a well-known .NET anti-pattern (socket/connection reuse is lost, DNS doesn't get refreshed properly, and it adds TCP handshake overhead every single tick).
- After processing, it does `await Task.Delay(1000)` **unconditionally**, regardless of how long the HTTP calls + processing took. If a tick takes 1.3s of real work, the loop cadence becomes 2.3s — already blowing past your 2-second budget, and it compounds every tick it happens.
- Instead of processing data in-process, every tick does `backgroundJobClient.Enqueue(...)` — round-tripping through Redis-backed Hangfire just to hand data from one part of the same process to another. That's a Redis round trip *and* a queue dequeue *and* a worker pickup, purely as internal plumbing.
- `CreateDynamicLiveStats`/`CreateTop4LiveRanking` use `[DisableConcurrentExecution]`, which is a **distributed lock with a timeout** (2–3s) and `[AutomaticRetry(Attempts = 0)]`. If a tick is still processing when the next one arrives and can't get the lock within the timeout, **that tick's job is silently dropped — no retry, no log the user sees, no display update.** Under any load spike (a flurry of kills, a Redis latency blip, a GC pause) this drops frames, which reads to a viewer as "stats not updating on time."
- Separately, `GetTotalPlayersHangfireJobBusiness.cs` schedules its recurring job with the cron string `"* * * * * *"`. Hangfire's `RecurringJobScheduler` (the component that checks whether a recurring job is due) polls on a much coarser interval by default — this pattern is not built for sub-minute cadences at all, and it's evidence the "run every second" intent was fighting the wrong tool from day one.

None of this is about Redis being *slow* — it's that the whole design routes a 2-second real-time feed through a durable job queue meant for "run this report nightly," and adds network hops where a direct method call would do.

### 2c. The "last 4 teams" position-swap bug

Good news: there's already a real fix attempt in `LiveStatsBusiness.top4.cs` — it assigns each team a **fixed position** (`Top4TeamPositions` dict, TeamId → position 1–4) the first time it sees 4 live teams, and reuses that mapping afterward instead of re-sorting by live stats every tick. In principle this is the right idea. Two concrete bugs undermine it:

1. **`Reset.cs` never clears `Top4TeamPositions`.** `Resetjob()` clears vMix's on-screen fields for every ranking overlay, but never calls `redis.KeyDeleteAsync("Top4TeamPositions")`. The key has a 15-minute TTL that only gets refreshed *while* Top4 mode is active — so if a new match starts within 15 minutes of the last match's Top4 phase, or if the app restarts mid-key-lifetime, the position map can still be sitting there mapping *this* match's team IDs (which PUBG reuses as small per-match integers, e.g. 1–16) to *last* match's arbitrary positions.
2. **Any Redis hiccup forces silent re-initialization.** If `redis.StringGetAsync("Top4TeamPositions")` comes back empty for any reason other than "genuinely first time" (a dropped connection, a WSL blip, the key expiring because the job got dropped per 2b above), the code treats it as "first time entering Top4" and **re-sorts from current live stats**, which can easily produce a different order than the original assignment — this is very likely the exact "Team A becomes Team 1, then next update Team B becomes Team 1" behavior you're seeing. It's not random; it's the position cache being wiped and rebuilt on Redis instability, which is the same instability driving problem #1.

So the swap bug and the Redis-reliability complaint are the same root cause wearing two hats.

### 2d. The survival/win-probability percentage

The live "PERCENTAGE{n}" field shown in the Top4 overlay is computed in `CreateTop4LiveRanking`:

```
rawScore = (memberAdvantage * 0.50 + healthAdvantage * 0.30 + killAdvantage * 0.20) * positionPenalty
```

This already factors in member count (50% weight) and health (30%) — it's not naive. But there's a real accuracy bug: **`healthAdvantage` is averaged only over players whose `LiveState == 0` ("standing alive"), while `memberAdvantage` counts every non-dead player, including knocked-out ones, at full weight.** So a team with 3 players knocked down and 1 standing at full health shows the *same* member-count contribution as a team with all 4 standing, and its health average is computed from that one healthy player alone — the score doesn't reflect that 75% of the team is currently unable to fight and one revive-attempt away from being wiped. That's almost certainly why the percentage feels wrong at the exact moments (last few teams, active fights) where knocks matter most.

**Fix direction:** give knocked players a small non-zero combat weight (not full, not zero) instead of excluding them from the health average, and use it consistently in both the member-count and health terms. Once you share the PUBG international tournament last-4 data, I'll calibrate the actual weights against real outcomes rather than guessing at 50/30/20.

### 2e. Post-match processing: slow and can crash

`PostMatch.createPostMtachStats` (`PostMatchStats/PostMatch.cs`) runs 7 steps **sequentially**, including a hardcoded `await Task.Delay(1000)` between the first two for no stated reason, then `WWCDStatsAsync → MatchMvp → MatchRankings → OverallRankings → TeamsToWatch`, all sharing **one injected `vmix_graphicsContext` DbContext instance**. Two structural problems:

- That DbContext is registered as scoped, but the scope it lives in is created once per match and held for the whole match's duration (30+ minutes), across hundreds of live-tick calls. EF Core's change tracker keeps growing the whole time, which is a classic cause of things getting progressively slower and eventually running out of memory on longer matches — matching "takes time to process."
- There's no try/catch wrapping the *whole* `createPostMtachStats` call at its call site in `GetLiveData.cs` — an unhandled exception in any one of those 7 steps propagates straight up and can take down the match-processing flow entirely, mid-results.

**Fix direction:** use `IDbContextFactory<T>` to get a short-lived, fresh context per step (or per parallel group); run the independent read-heavy steps (WWCD/MVP/Rankings/Overall/TeamsToWatch) with `Task.WhenAll` on separate contexts instead of one after another; wrap the whole pipeline so a failure in one section logs and continues instead of aborting the rest; and make each step idempotent (the pattern `savePlayersinfo` already uses — check `existingPlayerUIDs` before inserting — should be applied to every step) so a crash mid-run can just be re-run safely.

## 3. Proposed architecture — Windows-native, no Redis, no WSL dependency

**One process, one Windows Service**, built as a .NET `Worker Service` (`Microsoft.Extensions.Hosting.WindowsServices`), replacing the WinForms app as the thing that runs on match day:

| Today | Replace with | Why |
|---|---|---|
| Redis (cache + Hangfire storage) | `ConcurrentDictionary`-backed in-process `MatchStateStore` singleton, snapshotted to a local SQLite/LiteDB file every few seconds and on every state change | Nothing here is cross-process. In-memory is faster than any network hop and can't fail to start because WSL didn't boot. The disk snapshot gives you the durability Redis was providing (crash/restart recovery) without a server process. |
| Hangfire enqueue-per-tick for the live pipeline | A single `PeriodicTimer`-driven `BackgroundService`, one poll → one direct in-process call → one push to vMix, no queue hop | Removes the Redis round trip, the distributed-lock timeouts, and the silent job drops. Ordering is guaranteed because there's exactly one consumer. |
| Hangfire for slow background chores (post-match exports, Google Sheets sync) | Keep Hangfire *only* here if useful, backed by `Hangfire.Storage.SQLite` instead of Redis | These aren't time-critical, so a durable queue still makes sense — just not Redis-backed. |
| MySQL (Pomelo) for tournament/stat data | SQLite via `Microsoft.EntityFrameworkCore.Sqlite` (same EF Core model, ~1-line provider swap) — or keep MySQL if you specifically need centralized cross-event reporting, treated as async/non-blocking | One embedded file, zero install, zero network dependency, fully Windows-native. |
| 5× `BackgroundJobServer`, `ProcessorCount*5` workers each | One worker pool sized to actual concurrency needs (a handful of threads, not ~200) | The current setup is wildly over-parallelized for a pipeline that must stay *ordered*, not throughput-maximized. |
| Position-swap-prone Top4 cache | Same fixed-position idea, but state lives in the in-process store (survives Redis blips because there's no Redis to blip), explicitly cleared on match start/reset (fixing the missing `Reset.cs` cleanup), and snapshotted to disk so a crash mid-Top4 doesn't lose the assignment | Keeps the good idea from `top4.cs`, removes the two concrete bugs that undermine it. |

This is a genuinely more "industry standard" shape for this kind of app: broadcast graphics engines (the kind used for esports overlays) are built as a single low-latency process with in-memory state and a lightweight durable snapshot — not a microservices-style queue+cache+DB stack for a single machine feeding a single vMix instance.

## 4. Phase 2 — replace the WinForms UI with a browser dashboard

Since the Worker Service above already hosts Kestrel, add:
- A **SignalR hub** broadcasting match state (teams, players, positions, match status) to connected clients in real time
- A minimal Web API for actions: start match, end match, reset, manual data entry — replacing `AuthenticationForm`, `ManualDataInputForm`, `BackupForm`'s functions
- A **React (Vite) dashboard**, served from the same host or as a static build, that connects over the LAN — bind Kestrel to `0.0.0.0` on a fixed port, and anyone on the network opens `http://<pc-name-or-ip>:<port>` in Chrome to see live stats and drive the match, no RDP or physical access to the graphics PC required

This also directly solves the "should not crash" requirement structurally: a Windows Service with `FailureActions` configured (auto-restart on crash) plus the disk-snapshotted state store means even a hard crash mid-match can recover in seconds instead of losing the match.

## 5. Verification plan before touching production

Build a dummy-data simulator: JSON fixtures for 16 teams / 64 players replaying a realistic match timeline — including the exact edge cases that matter here: the moment the field crosses from 5 to 4 live teams, a team going to 0 alive-standing-but-not-eliminated (all knocked) players, ties in kill count, a simulated Redis/DB restart mid-match (once removed, a simulated *process* restart), and a full post-match run. This lets us prove the position-lock and survival-% fixes before they ever touch a live tournament.

## 6. Open questions before I start building

1. Confirm `Pubg Ranking System` (WinForms) is the build that actually runs on match day, and that `Vmix Hangfire Graphics` / `VmixPubgGraphicsController` can be deleted.
2. SQLite or keep MySQL for the tournament database?
3. Should the React dashboard fully replace the WinForms forms (auth, manual data entry, backup), or run alongside for now?
4. Please share the PUBG international tournament last-4 ranking data when ready — I'll use it to calibrate the survival/win-probability weights instead of guessing.

## 7. Suggested build order

1. In-process `MatchStateStore` + disk snapshot, remove Redis from the hot path, fix the Top4 position-lock bugs (2c) — this alone should eliminate the swapping and most of the "not updating on time" symptoms.
2. `PeriodicTimer` polling loop replacing the Hangfire-enqueue-per-tick pattern; fix `HttpClient` reuse and remove the hardcoded delay.
3. Post-match pipeline hardening (2e): `IDbContextFactory`, parallelized independent steps, whole-pipeline error isolation.
4. Corrected survival % formula (pending your tournament data for calibration).
5. Worker Service packaging (install as a real Windows Service, auto-restart on failure).
6. SignalR hub + Web API for match control.
7. React dashboard, LAN-accessible, replacing WinForms.
8. Dummy-data test harness covering the edge cases in §5, run against the whole rebuilt pipeline before a live event.
