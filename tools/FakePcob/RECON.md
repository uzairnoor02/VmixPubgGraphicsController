# RECON — FakePcob (Task 1)

## 1. PUSH OR PULL — **PULL**. The app polls PCOB with `HttpClient.GetAsync`.
- `VmixGraphicsBusiness/LiveMatch/GetLiveData.cs:107-108,176-177,219,249` — polls
  `_pcobUrl + "gettotalplayerlist"`, `"getteaminfolist"`, `"getkillinfo"`, `"getcircleinfo"`.
  `_pcobUrl` comes from config key **`pcobUrl`** (`Pubg Ranking System/appsettings.json:28`,
  currently `"http://192.168.1.56:10086/"`). Also polls `"isingame"` (line 58) — the match
  start/end loop is `while (await IsInGame())` (`GetLiveData.cs:54-101`).
- `VmixIngestAgent/Program.cs:74-93` — a **second, independent poller** (the new ingest agent for
  running pcob on a separate PC): polls the same three endpoints (`gettotalplayerlist`,
  `getteaminfolist`, `getkillinfo`) off `pcobBase`, then **POSTs** each tick to the main app's own
  `MapPost("/api/ingest/tick")` (`Pubg Ranking System/IngestApi.cs:52`). FakePcob only has to serve
  GET requests — this agent is a client of FakePcob exactly like GetLiveData.cs, just with a relay
  in between. No listener/MapPost exists on the app side for pcob itself.
- **No `BaseAddress` used** — every call is `_httpClient.GetAsync(_pcobUrl + "<path>")`, a `static
  readonly HttpClient` (`GetLiveData.cs:32`), 5s timeout. Query strings: none observed — all four
  reads are bare `GET /<verb>` with no query params.

## 2. Response envelope
- `{"playerInfoList":[...]}`, `System.Text.Json` (`VmixData/Models/MatchModels/LivePlayerInfo.cs:8`,
  `[JsonPropertyName("playerInfoList")]`). Team list presumably `{"teamInfoList":[...]}` (mirrored
  naming per `IngestApi.cs:19` comment; `TeamInfo.cs` has bare `killNum` etc. — **not confirmed
  field-for-field**, treat team aggregation as computed, see §4).
- `getcircleinfo` → `CircleDataWrapper` wrapping `CircleInfo` object with **string** fields
  `CircleStatus`, `CircleIndex`, `MaxTime`, `Counter` (all `int.Parse()`'d — GetLiveData.cs:255-256).
- `isingame` → `{"IsInGame": bool}` (`GetLiveData.cs:293-296`).
- `getkillinfo` → shape unconfirmed by the app itself (`KillInfo.cs:8,60` — "unconfirmed", best
  effort, swallowed on any failure). Fake server only needs to return a valid empty envelope.

## 3. Fields read live
Confirmed live-path fields (not exhaustive of all 43, but everything wired to output):
`location,health,healthMax,liveState,killNum,killNumBeforeDie,teamId,teamName,gotAirDropNum,
maxKillDistance,damage,killNumInVehicle,killNumByGrenade,rank,isOutsideBlueCircle,UId,PlayerName`.
`liveState` numeric: 0 Normal,1 OnPlane,2 OnParachute,3 OnVehicle,4 Knocked,5 Dead,6 Disconnected
(`VmixGraphicsBusiness/TeamLiveStats.cs:30`, mirrored in dashboard `types.ts:18`).
`isPlayerAlive` = liveState 0..4 (`vmix-dashboard/src/types.ts:34-36`).

## 4. Team aggregation
`LiveStatsBusiness.cs:111` sums per-player via `teamInfoList...killNum` (i.e. the **team** payload
already carries a per-team `killNum`, not summed client-side from players) — but
`ManualDataInputForm.cs:213` does `teamData.teamInfoList.Sum(t => t.killNum)`, i.e. team kill counts
are read directly off the team list, not derived from players in the live path. **The simulator must
emit teamInfoList entries whose `killNum`/alive-count are kept in lock-step with the summed player
counters** (both must agree, since different call sites read from different sources).

## 5. Achievements (`LiveStatsBusiness.SetPlayerAcheivments.cs`)
| Achievement | Field watched | Threshold / dedupe |
|---|---|---|
| First Blood | `KillNum` (top killer) | Redis key `FirstBlood`, set-once (`:191-233`) |
| Grenade Elimination | `KillNumByGrenade` | Redis `GrenadeEliminations:{UId}`, fires when new value > stored (`:90-132`) — **so a second grenade kill by the same player DOES refire** (per-player counter comparison, not a boolean) |
| Vehicle Kill | `KillNumInVehicle` | Redis `VehicleEliminations:{UId}`, same > comparison (`:42-81`) |
| Airdrop Looted | `GotAirDropNum` | Redis `AirDropLooted:{UId}`, same pattern (`:140-183`) |
| Kill Domination | `KillNum` vs other teams' max | Redis `KillDominationKey:{UId}` (`:245-296`) |
| Damage Domination | `Damage` vs other teams' max | Redis `DamageDominationKey:{UId}` (`:301-351`) |
All are live-group fields (safe mid-match per §7).

## 6. Top4 switch
`LiveStatsBusiness.top4.cs:40-45` — `ShouldShowTop4Ranking`: `liveTeamsCount = teams where
liveMemberNum > 0; return liveTeamsCount <= 4 && > 0`. **`liveMemberNum` is per-team, not derived
from `bHasDied`** — confirms the doc's warning that `bHasDied` is useless; the real gate is a
team-level alive-member count, which itself must come from `liveState` (0-4 = alive) per player,
never `bHasDied`. "Team eliminated" = a team's `liveMemberNum` reaches 0, i.e. every member's
`liveState == 5` (or 6). A team with every member knocked (`liveState==4`) still counts as alive
(`isPlayerAlive` includes 4) — must NOT show as eliminated.

## 7. Circle
`GetLiveData.cs:246-270` (`GetCircleInfo`) reads `CircleStatus` (string "0"/"2"), `CircleIndex`,
`MaxTime`, `Counter`. "Closing" = `CircleStatus=="2" && CircleIndex<6 && (MaxTime-Counter)<=17`
(fires once via a `zonemoving` latch, resets when `CircleStatus=="0"`). **No C# `CircleUpdated`
SignalR broadcast exists** — `Overlay.tsx:199` listens for a `"CircleUpdated"` hub event that
nothing currently sends (confirmed: only old vMix XAML `PushCircleAnimationAsync` calls happen
here, no `matchState.Publish...`/hub push). The zone bar will stay dark until that's wired up —
record under REPORT "Production changes needed", do not add it (Task 9-11 only touch 3 files).

## 8. Match start/end
`while (await IsInGame())` loop (`GetLiveData.cs:101`); `isingame` polled once per outer loop tick.
Match end = `isingame` returns false (or errors) → `ResetMatchState()` (`:88`) and
`PublishMatchStatus("")`.

## 9. Team loading
`POST /api/teams/load` (`LiveDashboardHost.cs:321-426`) accepts
`{"tournament_name":str,"stages":[{"stage_name":str,"teams":[{"team_id":str,"team_name":str}]}]}`
(see `teams_data.json` for a real example — `TournamentData`/`TeamData` via `JsonConvert`
(Newtonsoft), snake_case via attributes presumably). Upserts by `team_id`+stage.

## 10. Images
`ConfigGlobal.PlayerImages\{UId}.png` (player photo, keyed by numeric `uId`) and
`ConfigGlobal.LogosImages\{teamid}.png` (team logo, keyed by numeric `teamId`) — both flat
directories, PNG, no enforced size (web renderers box them at 24-38px CSS regardless of source
dims; 256x256 is a safe generous default). Config keys: `PlayerImages`, `TeamLogosImages` /
`Images` (`appsettings.json:16-19`).

## 11. appsettings parsing
**No strict parser** — `appsettings.json` already ships with `//` line comments
(`WebDashboard.AuthKey`, `Agent.IngestKey`, even a commented-out `ConnectionStrings` line) and the
app loads fine, because `Microsoft.Extensions.Configuration.Json` uses
`JsonCommentHandling.Skip` + trailing commas allowed by default. Safe to comment `pcobUrl` with
`//` and add a fake line below per Task 8's instruction.

## 12. Renderers & config (Tasks 10-11)
- `vmix-dashboard/src/studio/renderers/*.tsx` — one file per graphic, each exporting a `XRenderer`
  function component (e.g. `RankingsRenderer`, `Top4Renderer`, `AchievementRenderer`,
  `CircleStatusRenderer`, `EliminatedSidebarRenderer`, `SpectatorMapRenderer`, `StandingsRenderer`,
  `TeamIntroRenderer`, `TeamsToWatchRenderer`, `TopPlayersRenderer`, `MvpRankingsRenderer`,
  `HeadToHeadRenderer`, `PlayerHighlightRenderer`, `ChampionsRenderer`). Each takes a `theme: Theme`
  plus its own props (rows/teams/fields etc., all typed, no shared base props type yet).
- Panel background today is **inlined per-renderer**, not shared: e.g.
  `RankingsRenderer.tsx:98`: `background: theme.panelBg, backdropFilter: 'blur(' + theme.panelBlur
  + ')'`; `Top4Renderer.tsx:59`: same pattern on the card wrapper, no `box-shadow`/inner-glow found
  on panels themselves (the outer `boxShadow: theme.glow` is a colored glow, not a black shadow —
  leave that alone, Task 9 only touches the *panel surface*, i.e. `background`+`backdropFilter`).
  Theme carries `panelBg` (rgba string), `panelBlur` (px string), `panelBorder`. No existing
  `elementSettings`-driven opacity anywhere.
- Overlay config type: `OverlayConfig` (`VmixGraphicsBusiness/Utils/OverlayConfigStore.cs:9-41`) —
  `ChromaKeyColor: string`, `ElementVisibility: Dictionary<string,bool>`,
  `ElementSettings: Dictionary<string, JsonElement>` (free-form, frontend-owned shape, no backend
  change needed to add fields — exactly what Task 9 needs). Mirrored on the frontend as
  `OverlayConfig { chromaKeyColor, elementVisibility, elementSettings: Record<string, unknown> }`
  (`vmix-dashboard/src/lib/api.ts:9-13`), saved via `api.saveOverlayConfig` → `POST
  /api/overlay/config` (`LiveDashboardHost.cs:468`), fetched via `api.getOverlayConfig` → `GET
  /api/overlay/config` (`:466`). Settings UI lives in `OverlaySettingsTab.tsx`. Route/nav: single
  hand-rolled pathname check in `App.tsx:12` (`/overlay` → `<Overlay/>`), everything else is
  `AdminShell` (tabs, not a router) — Task 10's `/demo` route needs the same `App.tsx` pattern plus
  a nav entry inside `AdminShell`/its tab list.

## Predicted trouble spots (carried into REPORT.md, not fixed here)
- Circle bar: no `CircleUpdated` broadcast exists (see §7) — stays dark regardless of scenario.
- `knockouts`/`assists`/`headShotNum`/`survivalTime` are AfterMatch-group fields — 0 until the
  match ends (per the three-route model, Task 4) — any code reading them live will show 0/blank.
- Team elimination and Top4 both key off `liveMemberNum`, which the simulator must compute from
  `liveState<=4` per player, kept consistent with the flat `teamInfoList.killNum`/alive fields.
