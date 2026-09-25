# Handoff: web-only overlay + PMGO-style live graphics

**For:** Claude Code (or any engineer) picking this up next.
**Branch:** `feature/web-only-output`, cut from `test/fake-pcob` (which sits on `feature/react-webapi-migration`).
**State:** everything below is **uncommitted** in the working tree, alongside a few uncommitted edits the owner had already made on `test/fake-pcob`. The 28 Sep live event runs from an older branch. Do not touch that branch.
**Last updated:** 25 Sep 2026, ~00:25 PKT.

---

## 1. The architecture rule (read this first)

```
PUBG OB API (pcob :10086) -> poller (GetLiveData / IngestApi) -> MatchStateStore
   -> web host :5050 (SignalR hub /hubs/match + REST) -> /overlay page -> vMix Browser Source
```

- **The app never talks to vMix.** vMix only loads `http://<server>:5050/overlay[/<token>]` as a Browser Source. The `vmixutils/` folder and `ApiCallProcessor` were **deleted**. Never re-add vMix HTTP calls, GT Title field pushes or `SetVMIXDataoperations`.
- **One output:** `MatchStateStore.PublishGraphic(eventName, payload)`. LiveDashboardHost forwards each publish to the tournament's SignalR group under that event name. The last payload per event is kept, and `GET /api/overlay/snapshot` returns them all so a reloading overlay is hydrated immediately.
- Event names live in `GraphicEvents` (MatchStateStore.cs). Image URLs come from `MediaUrls`: `/team-logos/{teamId}.png` (served from `TeamLogosImages`) and `/player-images/{uid}.png` (served from `PlayerImages`).
- **Frontend rule:** Studio preview, `/demo` and `/overlay` all render through the same components in `vmix-dashboard/src/studio/renderers/`. Change a graphic's look **only** in its renderer, never by forking it inside Overlay.tsx.

## 2. Repo map (only what matters here)

| Area | Path |
|---|---|
| Live poll loop | `VmixGraphicsBusiness/LiveMatch/GetLiveData.cs` |
| Standings / team stats | `VmixGraphicsBusiness/LiveMatch/LiveStatsBusiness.cs` |
| Last 4 / win probability | `VmixGraphicsBusiness/LiveMatch/LiveStatsBusiness.top4.cs` |
| Achievements | `VmixGraphicsBusiness/LiveMatch/LiveStatsBusiness.SetPlayerAcheivments.cs` |
| Post-match graphics | `VmixGraphicsBusiness/PostMatchStats/PostMatch.*.cs`, `PreMatch/PreMatch.MapPerformers.cs` |
| State + events | `VmixGraphicsBusiness/Utils/MatchStateStore.cs` (GraphicEvents, MediaUrls, LiveAchievementEvent) |
| New helpers | `Utils/TeamInventoryTracker.cs`, `Utils/CirclePayload.cs`, `Utils/KillFeedPublisher.cs` |
| getkillinfo model | `VmixData/Models/MatchModels/KillInfo.cs` |
| Web host | `Pubg Ranking System/LiveDashboardHost.cs`, `IngestApi.cs`, `MatchControlApi.cs` |
| Remote agent | `VmixIngestAgent/Program.cs` |
| Overlay page | `vmix-dashboard/src/Overlay.tsx` |
| Positions | `vmix-dashboard/src/lib/overlayLayout.tsx` (DEFAULT_LAYOUT, OverlayStage) |
| Banner queue | `vmix-dashboard/src/lib/useBannerQueue.ts` |
| Renderers | `vmix-dashboard/src/studio/renderers/*.tsx` (+ `healthGlyphs.tsx`, `shared.tsx`) |
| Demo | `vmix-dashboard/src/pages/DemoPage.tsx` (`/demo`) |
| Fake pcob | `tools/FakePcob/` (replay of a real match: `recordings/20260923-232832`) |
| Real-match data notes | project doc `claude/pcob-real-match-analysis.md` (endpoint shapes, timeline) |

## 3. What was done

### 3.1 vMix removed from the pipeline (24 Sep)
- Live loop, standings, Top 4, achievements, eliminated banner, circle and Reset no longer call vMix. Before, the whole live job died on its first line when vMix wasn't running, and the web overlay got nothing.
- Top 4 and achievements now run **inline** each tick instead of as Hangfire jobs. Hangfire is still used only to launch `FetchAndPostData`.
- `CreateLiveStats` now fills `TeamRank`, `TotalPoints`, `Logo`, `TeamEliminated`, `PlayerCount` and `TeamBackground`. Before, only the vMix fields got real values.
- **Start match waits up to 10 minutes for `isingame`.** Before, starting in the lobby saved empty lobby data as the match result.
- `isingame` is parsed case-insensitively. Real pcob sends `isInGame`; FakePcob used to send `IsInGame` and now matches real pcob.
- **Post-match graphics publish to the web**, automatically when a match ends (`PostMatch.createPostMtachStats`): MatchRankings, OverallRankings, WWCD→Champions, MatchMvp→PlayerHighlight, Top5MatchMVP→MvpRankings, TeamsToWatch, MatchSummary. Stage MVP, Top5 Stage MVP, Top Grenadiers and Map Performers are manual, from the Match Control buttons.
- New overlay panels: Match Rankings and Overall Rankings (auto page flip, `rankings.pageSeconds`), and Map Performers.
- Dashboard `API_BASE` is now same-origin in the built app. Before, it was hardcoded `http://localhost:5050`, so a vMix PC on the LAN fetched from its own localhost.
- Fixed along the way:
  - vehicle-kill achievement crashed on first use
  - airdrop detector used `return` instead of `continue`
  - achievement markers weren't reset per match
  - WWCD throwables counted smokes twice and missed frags; WWCD points were hardcoded to 10 + kills
  - overall WWCD summed across all stages

### 3.2 PMGO-style live graphics (overnight 24→25 Sep)
Reference frames: PMGO S1 Grand Finals. The owner has the screenshots.

| Graphic | Renderer | Now |
|---|---|---|
| Live rankings (under the minimap, right) | `StandingsRenderer` | RANK · logo · TEAM · ALIVE bars · **PTS** · ELIMS. Bars **fill to health**. Knocked = red fill of the remaining bleed-out health, pulsing. Dead = solid grey. A 3-man team shows 3 bars. Wiped teams are dimmed. Density adapts to 16/20/25 teams. |
| Last 4 teams (top) | `Top4Renderer` | Logo, tag, **helmet icons filled to health** (grey when dead), a **throwables row** (frag/smoke/molotov/stun counts), optional WWCD bar and optional rank chip. |
| First Blood / achievements (left, below the in-game team panel) | `AchievementRenderer` | Photo block, big italic label, strip reading "PLAYER ▶▶ VICTIM" (victim from getkillinfo). The label auto-sizes to one line. |
| ELIMINATED (upper centre) | `EliminatedBannerRenderer` | Angled card: `#rank`, logo, ELIMINATED, `N ELIMS`, team name. |

- **Health look is one shared setting**, `health.style`: solid colour (PMGO default) or follow the health gradient. It's edited in Studio → Standings → Health or Last 4 → Health.
- **Banners queue.** ELIMINATED and achievement banners play one at a time, in order; duplicates within 15 s are dropped. See `useBannerQueue`.
- **Positions are configurable:** `elementSettings["layout.<id>"] = {x|"center", y, w}` on a 1920×1080 canvas. There's an editor in Overlay Settings → Positions; defaults are in `DEFAULT_LAYOUT`.
- **OverlayStage:** `/overlay` and `/demo` render on a fixed 1920×1080 stage scaled to the window. vMix sees scale 1.
- **Fonts are bundled offline:** Barlow Condensed, Inter, Oswald and Bebas Neue, latin + latin-ext, in `src/assets/fonts/` and `fonts.css`. Before, the theme fonts never loaded and everything fell back to sans-serif.
- `/demo` query params: `?bg=<img url>` (draw over a reference frame), `?clean=1` (hide controls), `?show=id1,id2`, `?hide=id`, `?alive=N`.
- Overlay Settings "Preview" buttons now send the auth header; they used to fail with 401.

### 3.3 Backend data for the new graphics
- **Throwables:** GetLiveData polls `getteambackpackinfo` each tick → `MatchStateStore.Inventory` (TeamInventoryTracker) → `Top4TeamStats.Throwables`.
  - pcob only returns the backpack of the **observed team**, so counts are "as last seen" and show "-" until the observer has been on that team.
  - Item ids: 602004 frag, 602002 smoke, 602003 molotov, 602001 stun.
- **Kill feed fixed to the real getkillinfo shape:** `CauserName`, `VictimName`, `CauserUID`, `VictimUID`, `ItemID`, `ResultHealthStatus` (1 = knock, 2 = kill), `CurGameTime`, `Distance`. Before, every row read "Unknown eliminated X" and knocks were announced as kills.
  - Kill **counts** still come from the player list, as the owner requires. getkillinfo is only used for names.
- **Victim names:** `KillFeedPublisher` records killer → victim. First Blood waits up to 3 ticks for the victim name when getkillinfo is answering.
- The ELIMINATED banner's elim count uses `KillNum` (not `KillNumBeforeDie`) and carries rank/eliminations in `OverlayEvent.Data`.
- Top 4 cards include dead players, as grey helmets. The win-probability maths is unchanged.
- **Agent mode parity:** VmixIngestAgent also sends `circleJson` and `backpackJson`; IngestApi applies the kill feed, inventory and circle before the stats run.
- **Circle bar:** `CirclePayload` normalises pcob's strings. `circleStatus` is "closing" (status 2) or "waiting"; `counter` is **seconds remaining** (MaxTime − Counter).
- **FakePcob replay** now serves the recorded `getkillinfo`, `getteambackpackinfo`, `getcircleinfo` and `getobservingplayer` in step with the replay clock (`tools/FakePcob/RecordedFeeds.cs`). Before, kill info was always empty.

### 3.4 Verification done
- `tsc -b` is clean, and `vite build` is clean.
- Headless Chromium against a mock backend using C#-shaped payloads: 10/10 overlay checks pass, including the rankings page flip.
- `/demo` screenshots were composited over the PMGO reference frames: all four graphics land where PMGO puts them.
- **C# compile check (new):** VmixData + VmixGraphicsBusiness + the web-host part of `Pubg Ranking System` compiled with `dotnet build` against the real dependency DLLs taken from `Pubg Ranking System/bin/Debug/net8.0-windows`. Result: **Build succeeded**. WinForms files were stubbed. `Form1.cs` and FakePcob's `Assets.cs` were not compiled.
- FakePcob (with RecordedFeeds) builds and serves the replay. It returned real `getteambackpackinfo` and `killInfo` bodies.
- **Not yet done: an end-to-end run** (app + FakePcob replay → `/overlay`) was set up but not run. See 4.1.

## 4. What still needs to be done (in priority order)

### 4.1 End-to-end test with the replay (do this first)
```
cd tools/FakePcob
bin\Debug\net8.0-windows\FakePcob.exe serve --replay recordings\20260923-232832 --paused
```
Then start the app, open `http://localhost:5050` → Match Control → start a match, and press Play on `http://127.0.0.1:10086/_sim`. Speed 5x makes the 30-min match take 6 min. Check `/overlay` against this timeline, from `claude/pcob-real-match-analysis.md`:

| Replay tick / fight clock | Expect on /overlay |
|---|---|
| start | live rankings with logos, PTS, 4 green bars per team; circle bar (turn `circle` on in Director) |
| ~01:07 | **FIRST BLOOD** As1CHOTUŪ ▶▶ StarFURYīLIVE |
| 07:18 | **ELIMINATED #16** team 9; GRENADE ELIM iSPARTAN (team 12) |
| knocks | red partial bars that drain |
| 22:34 | Last 4 cards appear. The window is only ~11 s (4 alive → 3 at 22:45), and the cards stay while ≤ 4 are alive. |
| 30:00 | isingame false → post-match graphics published (Match/Overall rankings, Champions, MVP...) |

Also check:
- Throwables fill in on the Last 4 cards once the observer watches those teams.
- `/api/overlay/snapshot` returns all events.
- The Logs tab shows no errors.

**Known risk to check:** the `Teams` table / logos must use pcob's real team ids (5–20 in this recording), or names and logos fall back.

### 4.2 Owner's open requests
- **"Total points of team in the live rankings":** implemented as the **PTS** column = tournament points before this match + this match's kills so far (`LiveTeamPointStats.totalScore`). **Placement points are not added live**; a team's placement is only final when it's wiped (`rank` is set live then) or at match end. Possible improvement: add the placement points for already-wiped teams using their live `rank` and the tournament's placement table. That needs the placement scale, which is still unconfirmed; 10/6/5/4/3/2/1/1 is the placeholder.
- Studio pages for the four graphics were updated; review them visually (Graphics Studio → Standings, Last 4, Achievement, Team Eliminated).

### 4.3 Still missing publishers (the overlay slots exist and are off by default)
- `HeadToHeadUpdated`, `TeamIntroUpdated`: data exists in `TeamPoints`; needs a publisher driven by the Director picks (`director.h2h.*`, `director.teamIntro.teamId`).
- `MapPositionsUpdated` (spectator map): add `Location` to `LiveStatsBusiness.FilterPlayerInfo` and publish the player positions each tick.
- Match/Day Summary has data (`MatchSummaryUpdated`) but **no overlay widget**.
- Knockout / chicken-dinner achievements have labels but no detector.

### 4.4 Known limitations / tech debt
- Agent mode: LiveStatsBusiness/PostMatch publish to the DI singleton store, i.e. the **default tenant**. Per-tournament match control isn't done.
- Grenade/vehicle achievements still use counter diffs. getkillinfo `ItemID 602004` could confirm grenade kills directly.
- `WebDashboard:AuthKey` is still `"1234"`. Change it before exposing :5050 beyond the LAN.
- A Google Sheets auth `GoogleApiException` at startup is caught and harmless, but noisy.
- `dist/` is git-ignored. The owner runs `npm run build` in `vmix-dashboard` on Windows (no new npm packages were added; the fonts are vendored files).

## 5. How to work on this reliably

- **Build C# on Windows:** Visual Studio → Build Solution. From a Linux sandbox, NuGet is blocked. What worked: `apt install dotnet-sdk-8.0`, then compile against the DLLs in `Pubg Ranking System/bin/Debug/net8.0-windows` via `<Reference HintPath>`, with `EnableDefaultCompileItems=false`, stubs for the WinForms classes, and an empty `nuget.config` (`<clear/>`).
- **Frontend check:** `cd vmix-dashboard && npx tsc -b && npx vite build`. The repo's `node_modules` are Windows-installed, so vite's native binaries only work on Windows; `tsc` works anywhere. On Linux, copy `src` + package files elsewhere and run `npm ci`.
- **Visual check:** open `/demo?bg=<screenshot>&clean=1` to line graphics up against a real broadcast frame.
- **Design constraints (don't regress):** see project memory `design-constraints.md`.
  - One flat chroma-key colour.
  - Panel opacity 0 = no background at all.
  - Header gradients max 3 stops.
  - Rankings always exactly 2 pages.
  - The Last 4 cards must show tag, logo and WWCD.
- **Never** re-add vMix API calls. **Never** treat end-of-match-only pcob fields (`survivalTime`, `assists`, `heal`, grenade-use counts...) as live data; they read 0 until the match ends.
- Key on pcob **UIDs**, not names.
- Keep every optional-endpoint poll (`getkillinfo`, `getteambackpackinfo`, `getcircleinfo`) best-effort: swallow the failure, and never break the stats tick.

## 6. Settings keys added (`OverlayConfig.elementSettings`)
| Key | Meaning | Default |
|---|---|---|
| `layout.<graphicId>` | `{x:number|"center", y, w}` position | see `DEFAULT_LAYOUT` |
| `health.style` | `{fill:"solid"|"gradient", alive, knocked, dead, track}` | solid, #3DDC5C / #E8323C / #6E6E78 |
| `standings.showPoints` / `standings.showElims` | columns | true / true |
| `top4.showWwcd` / `top4.showThrowables` / `top4.showRank` | card rows | true / true / false |
| `achievement.bodyBg` | label background | near-white |
| `rankings.pageSeconds` | page flip interval | 8 |

Visibility ids added: `matchRankings`, `overallRankings`, `mapPerformers`. All are off by default.
