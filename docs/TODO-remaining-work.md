# Remaining work: detailed task list

**Companion to:** `docs/HANDOFF-web-only-overlay.md`. Read that first for the architecture rules, what's already done, and how to build and test.
**Branch:** `feature/web-only-output` (uncommitted). **Written:** 25 Sep 2026, 00:30 PKT.
**Goal:** every on-air graphic matches the PMGO S1 broadcast standard, runs from real pcob data, and is proven against the FakePcob replay of the 23 Sep match.

Each task lists: **Why**, **Where** (files), **Data** (source), **Do** (steps), **Done when** (acceptance criteria).

---

## Ground rules for every task

- The app never talks to vMix. The only output is `MatchStateStore.PublishGraphic(event, payload)` → SignalR → `/overlay`.
- Change a graphic's look only in its renderer (`vmix-dashboard/src/studio/renderers/`), so the Studio preview, `/demo` and `/overlay` stay identical.
- Positions come from `lib/overlayLayout.tsx` (`DEFAULT_LAYOUT` + the `layout.<id>` override). Don't hardcode `top/left` in Overlay.tsx.
- Design constraints (don't regress):
  - one flat chroma-key colour
  - panel opacity 0 = no background at all
  - header gradients max 3 stops
  - Match/Overall rankings always exactly 2 pages
  - the Last 4 cards show tag, logo and WWCD
- pcob fields that are end-of-match only (`survivalTime`, `assists`, `heal`, `knockouts`, grenade-use counts, `inDamage`, `headShotNum`) read 0 mid-match. Never show them live.
- Key on pcob UIDs, not names. Optional endpoint polls must swallow failures and never break the 1 s tick.
- Every new setting goes in `OverlayConfig.elementSettings` with a sensible default. Every new graphic is **off by default** in `elementVisibility`.
- After each task: `npx tsc -b`, then `npx vite build`, then the relevant `/demo` screenshot over a PMGO frame (`/demo?bg=<img>&clean=1&show=<id>`).

---

## P0: prove what's already built (do first)

### T1. End-to-end replay test of the live graphics
- **Why:** the four PMGO-style graphics and the new backend data (throwables, victim names, circle) have only been checked with sample data.
- **Where:** app + `tools/FakePcob`.
- **Do:**
  1. Build Solution (Visual Studio), and run `npm run build` in `vmix-dashboard`.
  2. `tools/FakePcob> bin\Debug\net8.0-windows\FakePcob.exe serve --replay recordings\20260923-232832 --paused`. Rebuild FakePcob first; it now serves the recorded `getkillinfo`, `getteambackpackinfo` and `getcircleinfo`.
  3. Load the teams for this match. The team ids in this recording are **5–20**; logos must be `TeamLogosImages\{5..20}.png`.
  4. Dashboard → Match Control → start the match. Open `/_sim`, press Play, speed 5x.
  5. Director: turn on leaderboard, top4, circle, teamEliminatedBanner, the achievements, eliminationFeed.
- **Done when**, against the timeline in `claude/pcob-real-match-analysis.md`:
  - [ ] Rankings show all 16 teams with logos, PTS and ELIMS. Bars fill to health. Knocks show red and drain. Dead players are grey. The 3-man teams (13 and 15) show 3 bars.
  - [ ] ~01:07 FIRST BLOOD banner reads `As1CHOTUŪ ▶▶ StarFURYīLIVE`.
  - [ ] 07:18 ELIMINATED shows team 9 as `#16`, and a GRENADE ELIM banner shows for iSPARTAN. Neither banner cuts the other off; they queue.
  - [ ] Each wipe shows exactly one ELIMINATED banner (the real event and the derived fallback are de-duplicated).
  - [ ] The kill feed shows real `X eliminated Y` lines, and no knocks.
  - [ ] 22:34 the Last 4 cards appear: helmets fill to health, dead helmets are grey, throwables show numbers for teams the observer has watched and "-" for the rest.
  - [ ] The circle bar counts down (MaxTime − Counter) and switches to "ZONE CLOSING" while the zone is shrinking.
  - [ ] 30:00 post-match graphics are published: Match/Overall Rankings, Champions, MVP card, MVP table, Teams to Watch are available in the Director.
  - [ ] Refreshing `/overlay` mid-match restores everything from `/api/overlay/snapshot`.
  - [ ] The Logs tab shows no errors.
- **Record:** fix every issue found and note it in the handoff doc.

### T2. Real player photos on the achievement banner
- **Why:** the banner is built around a photo, but no real photo has been seen in it yet.
- **Where:** `AchievementRenderer.tsx`; photos served from `/player-images/{uid}.png` (`PlayerImages` folder).
- **Do:**
  - Put 3–4 real cut-out PNGs (transparent background) named by UID in `PlayerImages`.
  - Check the crop (`objectPosition: top center`) and scale.
  - Add a setting `achievement.photoFit`: `"cover"` (default) or `"contain"`.
  - If the photo is a transparent cut-out, let it rise above the banner top by ~20 px, like PMGO (overflow visible on the photo block only).
- **Done when:** a real photo looks like the PMGO frame at 1080p, and a missing photo still falls back to the icon cleanly.

### T3. Visual review of the Studio pages for the four graphics
- **Where:** `studio/pages/StandingsPage.tsx`, `Top4Page.tsx`, `AchievementPage.tsx`, `EliminatedSidebarPage.tsx`, `studio/HealthStyleEditor.tsx`.
- **Done when:**
  - [ ] Every control changes the preview.
  - [ ] Every setting reaches `/overlay` live (Overlay Settings saves → `OverlayConfigChanged`).
  - [ ] Nothing overflows at a 1366 px wide browser.

---

## P1: finish the PMGO look on the live graphics

### T4. Leader highlight + "MATCH POINT" strip in the live rankings
- **Why:** PMGO highlights rank 1 (gold/red row) and shows `MATCH POINT: 92` under it, for tournaments using the match-point rule.
- **Where:** `StandingsRenderer.tsx`, `Overlay.tsx`, `StandingsPage.tsx`.
- **Data:** the threshold is a tournament setting, not pcob data. Store it as `standings.matchPoint` (number | null, default null = hidden).
- **Do:**
  - Add a `leaderBg` (Bg, default = the theme's accent) applied to the rank-1 row. Keep row rules able to override it.
  - Add an optional strip under row 1: `MATCH POINT: {n}`, shown when `standings.matchPoint` is set.
  - Optional flag: teams at or above the threshold get a "MP" badge.
- **Done when:** it matches PMGO frame 1/2 visually; with the setting unset, nothing changes.

### T5. Live placement points in PTS (optional; needs the owner's decision)
- **Why:** the owner asked for "total points in live rankings". PTS today = tournament points before this match + this match's kills. Placement points only arrive at match end.
- **Data:** a player's `rank` is set live the moment their team is wiped (confirmed in the recording). The placement scale is **unconfirmed** (placeholder 10/6/5/4/3/2/1/1).
- **Do:**
  1. Add a per-tournament placement table (`Tournament.PlacementPoints` JSON, or appsettings) with an editor on the dashboard.
  2. In `LiveStatsBusiness.CreateLiveStats`, for wiped teams add `placementPoints[rank]` to `totalScore`. Alive teams keep kills only, or optionally add the points for the current worst possible placement.
  3. Show a tooltip-free cue: points that include placement are final.
- **Done when:** PTS for a wiped team equals what `PostMatch.MatchRankings` saves at match end for that team.
- **Ask the owner first:** which placement scale, and whether alive teams should show a provisional value.

### T6. Light-card option for the Last 4 cards (PMGO white cards)
- **Where:** `Top4Renderer.tsx`, `Top4Page.tsx`, `Overlay.tsx`.
- **Do:**
  - Add a `top4.cardStyle` setting: `"dark"` (current, theme glass) or `"light"` (white card, dark text, dark helmets track, coloured team accent).
  - In light mode, helmet track `#D5D7DE`, dead `#9A9DA8`, text `#101218`.
- **Done when:** light mode looks like PMGO frame 3 and dark mode is unchanged.

### T7. Team country flags (PMGO shows a flag before each logo)
- **Why:** PMGO standings and Last 4 show country flags.
- **Where:** DB `Teams` (new nullable `CountryCode` column, ISO-3166 alpha-2), teams JSON import (`/api/teams/load`), Teams tab, `TeamLiveStats`, `Top4TeamStats`, the renderers.
- **Do:**
  1. EF migration or manual ALTER; the model is in `VmixData/Models/Team.cs`. Keep the SQLite fallback working.
  2. Serve flags **offline**: bundle SVG flags (e.g. the `flag-icons` SVGs, copied into `src/assets/flags/`, MIT licence), not a CDN.
  3. Setting `standings.showFlags` / `top4.showFlags`, default false.
- **Done when:** a team with a country shows its flag, a team without one shows nothing, and there's no layout shift.

### T8. Circle / zone status redesign
- **Why:** it's the older design with small text, and it's placed top-centre above the Last 4 cards. PMGO shows the zone timer inside the in-game minimap, so ours should complement it, not duplicate it.
- **Where:** `CircleStatusRenderer.tsx`, `CircleStatusPage.tsx`, `DEFAULT_LAYOUT.circle`.
- **Data:** `CircleUpdated` payload `{circleIndex, circleStatus: "closing"|"waiting", counter: secondsRemaining, maxTime}`.
- **Do:**
  - Make it a compact bar (~320×44) that sits directly under the rankings column header or under the minimap (x≈1604, y≈206).
  - Show zone number, the phase word, mm:ss and a progress fill.
  - Turn red and pulse at ≤ 15 s while closing. Hide it before the first circle (raw status 1).
  - Use the bundled display font at ≥ 18 px.
- **Done when:** it reads from 3 m away on a 1080p screen, doesn't collide with rankings or minimap in the PMGO frames, and counts down in the replay.

### T9. Elimination feed redesign
- **Why:** it's still the old plain grey list (`index.css .overlay-feed`), not theme-aware.
- **Where:** new `renderers/KillFeedRenderer.tsx`, used by Overlay and Demo; remove the CSS classes.
- **Do:**
  - Each row: killer team logo + name, a weapon/method glyph (skull; grenade icon when `ItemID == 602004`), victim team logo + name, distance if ≥ 100 m.
  - Max 4 rows, newest on top, fading out after ~8 s.
  - To support that, the backend must add `killerTeamId`, `victimTeamId` and `itemId` to `LiveKillEvent`. Map UIDs → teamId from the current player list in `KillFeedPublisher`.
- **Done when:** the replay shows real rows with logos, grenade kills show the grenade glyph, and nothing overlaps the achievement banner.

---

## P2: post-match graphics to the same standard

All of these are published automatically at match end (or from Match Control), and each renders centre-stage when the Director turns it on. Bring each up to PMGO style: bundled condensed display font, team logos everywhere, large readable numbers, accent headers, consistent spacing. **Show one at a time; they share the centre.**

### T10. Match / Overall Rankings (PMGO standings table)
- **Where:** `RankingsRenderer.tsx`, `RankingsPage.tsx`.
- **Data:** `MatchRankingsUpdated` / `OverallRankingsUpdated` rows `{rank, teamId, teamName, logoUrl, wins, placementPts, elimPts, total, wwcd?, matchesPlayed?}`.
- **Do:**
  - Add a logo column (`logoUrl` is already in the payload) and a WWCD chicken icon for match winners.
  - Top-3 row highlight. Title/subtitle already come from the payload.
  - Optional flags (T7).
  - Keep exactly 2 pages.
- **Done when:** 16 and 20 teams both fit within 2 pages with no overflow; screenshot compared with the PMGO overall standings.

### T11. Champions / WWCD card
- **Where:** `ChampionsRenderer.tsx`, `ChampionsPage.tsx`.
- **Data:** `ChampionsUpdated` `{label, teamName, teamLogoUrl, players[{playerName, photoUrl}], stats[ELIMS, DAMAGE, POINTS], playerStats[{kills, damage, knocks, assists, survivalTime, contribution, ...}]}`.
- **Do:**
  - A big team logo plus the 4 player cut-outs.
  - A per-player stat row (ELIMS / DMG / KNOCKS / SURVIVAL / CONTRIBUTION %) from `playerStats`, which is already sent and not rendered yet.
  - The "WINNER WINNER CHICKEN DINNER" label.
- **Done when:** the per-player stats render and the missing-photo fallback works.

### T12. MVP of the match / Player Highlight
- **Where:** `PlayerHighlightRenderer.tsx`.
- **Data:** `PlayerHighlightUpdated` `{label, playerName, teamName, photoUrl, teamLogoUrl, stats[6]}`.
- **Do:**
  - A large cut-out photo, team logo, name, a 6-stat grid.
  - Stage MVP uses the same card with label "STAGE MVP".
- **Done when:** it matches the PMGO MVP card and handles 6 stats without wrapping.

### T13. MVP rankings table (top 5)
- **Where:** `MvpRankingsRenderer.tsx`.
- **Data:** `MvpRankingsUpdated` rows include `photoUrl`, `logoUrl`, `contribution` and `knocks`. These are **sent but not rendered**.
- **Do:**
  - Add a photo + logo per row and a contribution % column (column toggles already exist; add `contribution`).
  - Change the rating display to a normalised 0–100 score, or hide it by default. The raw score is ~700 (survival-dominated) and looks odd.
- **Done when:** 5 rows with photos fit in 16:9.

### T14. Teams to Watch / Map Performers cards
- **Where:** `TeamsToWatchRenderer.tsx`.
- **Data:** entries `{teamName, logoUrl, reason, stats[4], rank}`.
- **Do:**
  - Show the logo (sent, check it renders), a big rank number and stat chips with icons.
  - Map Performers reuses the component with the payload title.
- **Done when:** 4 cards and 2 cards both centre nicely.

### T15. Top Players / Top Grenadiers podium
- **Where:** `TopPlayersRenderer.tsx`.
- **Data:** `TopPlayersUpdated` `[{rank, playerName, value, statLabel, photoUrl, teamLogoUrl}]`.
- **Do:** render the photo and team logo (sent), podium heights 1-2-3, and a value with a stat label.
- **Done when:** 3 and 5 entries both look right.

### T16. Match / Day Summary graphic (new, the data already exists)
- **Where:** new `renderers/MatchSummaryRenderer.tsx`, a Studio page, an Overlay slot `matchSummary` (off by default), a `lib/graphics.ts` entry, `DEFAULT_LAYOUT.matchSummary`.
- **Data:** `MatchSummaryUpdated`, either
  - `{kind:"match", matchNumber, totalMatches, eliminations, knocks, longestElim, healing, throwablesUsed, airdropsLooted, headshots, vehicleElims}`, or
  - `{kind:"day", dayNumber, winners[{matchNumber, teamName, logoUrl, eliminations, damage, knocks, healing}]}`.
- **Do:** a match-stats tile grid for `kind:"match"`, and a "chicken dinners of the day" list for `kind:"day"`.
- **Done when:** both kinds render from real replay post-match data.

---

## P3: graphics with a design but no data yet

### T17. Head to Head publisher
- **Where:** new `PostMatch.HeadToHead.cs` (or a Director-triggered endpoint); Overlay already listens to `HeadToHeadUpdated`.
- **Data:** the `TeamPoints` table, one row per team per match. The Director picks the two teams (`elementSettings["director.h2h.leftTeamId"/"rightTeamId"]`).
- **Do:**
  - Add an endpoint `POST /api/graphics/head-to-head`, or recompute when those settings change.
  - Payload `{left:{teamName, logoUrl, matchTotals[]}, right:{...}, stats:[{label, left, right, winner}]}`. Stats: total pts, WWCDs, avg placement (lower is better → set `winner` explicitly), elims.
- **Done when:** picking two teams in the Director updates the graphic within 1 s.

### T18. Team Intro publisher
- **Data:** `director.teamIntro.teamId`, plus the roster from `PlayerStats` / the live player list, `wwcd` = live Top4 probability when available, else null.
- **Done when:** the pre-match intro shows 4 players with photos; mid-match it shows live health.

### T19. Spectator map publisher
- **Where:** `LiveStatsBusiness.FilterPlayerInfo` (add `Location` to the projection) and a publish of `MapPositionsUpdated` `{players:[{uid, teamId, x, y, liveState}], focusTeamId}` each tick (throttle to 1/s).
- **Optional:** the zone circles and plane path from `getgameglobalinfo` (`CircleArray` X/Y/Size; verify Size is a radius in cm), and the auto-focus team from `getobservingplayer`.
- **Done when:** the replay shows moving markers for the focus team on the Erangel map image.

### T20. Missing achievement detectors
- **Knockout:** getkillinfo rows with `ResultHealthStatus == "1"`; decide whether knocks deserve a banner (likely only multi-knocks).
- **Chicken Dinner:** fire at match end for the rank-1 team (`isingame` false + rank 1 players).
- **Long range:** `KillInfo.Distance ≥ 150 m` (constant in `KillFeedTracker`) → new `achievement.longRange` type + label + Studio entry.
- **Grenade:** confirm via `ItemID 602004` in addition to the counter diff, and de-duplicate.
- **Done when:** each fires once, at the right moment in the replay.

---

## P4: platform and ops items that affect graphics on air

- **T21. Agent-mode tenancy.** LiveStatsBusiness/PostMatch publish to the DI singleton store (the default tenant). Route publishes to the tenant's `MatchStateStore` when a match runs in agent mode for a non-default tournament.
- **T22. Security before exposing :5050:** replace `WebDashboard:AuthKey = "1234"` and restrict CORS (currently `AllowAnyOrigin`).
- **T23. Team id mapping check.** pcob team ids (5–20 in the recording) must match the `Teams` table and the logo filenames. Add a Teams-tab warning when a live team id has no DB row or no logo file. Currently it silently falls back to a blank tile.
- **T24. Font subsets.** Player names use CJK/Arabic-script characters (e.g. `丨`, `ـ`) that fall back to system fonts. Consider bundling Noto Sans subsets for those scripts, or accept the fallback.
- **T25. Commit hygiene.** The branch mixes the owner's earlier uncommitted `test/fake-pcob` edits with this work. Commit in logical chunks: (a) vMix removal, (b) post-match publishers, (c) PMGO live graphics, (d) backend data (throwables/kill feed), (e) FakePcob replay feeds, (f) docs.

---

## Suggested order for tonight / next session
T1 → fix findings → T2 → T4 → T6 → T8 → T9 → T10–T15 → T16 → T17–T20 → T21–T25.
