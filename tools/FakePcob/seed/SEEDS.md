# Seed data index (pre-converted by Opus — DO NOT open these JSONs in context)

All five files share the identical shape: `{"playerInfoList":[ ... ]}` with 43 keys per player.
Each is a REAL end-of-match snapshot from a real tournament. Field values below are
computed, so the generator can be written without ever reading the files into context.

## `pmsc_s2_q2_finals_m1.json`
- players **64**, teams **16**, teamIds `[4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19]`
- alive at end (liveState 0): **4**, dead (5): 60
- winner (rank 1): **['MAGICIANS ESPORTS']**
- survivalTime: min 510 max 1660 (seconds — gives exact death ORDER and TIMING)
- killNum max 5, total 57
- killNumByGrenade total **9**, killNumInVehicle total **1**, gotAirDropNum total **3**
- knockouts total 66, assists total 31, headShotNum total 6
- damage max 762, maxKillDistance max 356
- location x 214709..520636, y 81190..282926, z -529..10448
- bHasDied true count: 0  <-- NOTE: unreliable, use liveState

## `pmsc_s2_q2_finals_m2.json`
- players **63**, teams **16**, teamIds `[4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19]`
- alive at end (liveState 0): **3**, dead (5): 59
- winner (rank 1): **['FMA WHITES']**
- survivalTime: min 56 max 1626 (seconds — gives exact death ORDER and TIMING)
- killNum max 5, total 57
- killNumByGrenade total **3**, killNumInVehicle total **0**, gotAirDropNum total **2**
- knockouts total 55, assists total 29, headShotNum total 10
- damage max 769, maxKillDistance max 340
- location x 206924..467258, y 271523..623757, z 5038..17064
- bHasDied true count: 0  <-- NOTE: unreliable, use liveState

## `pmsc_s2_q2_finals_m3.json`
- players **60**, teams **15**, teamIds `[4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 15, 16, 17, 18, 19]`
- alive at end (liveState 0): **4**, dead (5): 56
- winner (rank 1): **['MAGICIANS ESPORTS']**
- survivalTime: min 9 max 1332 (seconds — gives exact death ORDER and TIMING)
- killNum max 5, total 50
- killNumByGrenade total **10**, killNumInVehicle total **0**, gotAirDropNum total **1**
- knockouts total 48, assists total 18, headShotNum total 11
- damage max 866, maxKillDistance max 293
- location x 123043..283045, y 59337..295180, z 176..3837
- bHasDied true count: 0  <-- NOTE: unreliable, use liveState

## `pmsc_s2_q2_finals_m4.json`
- players **60**, teams **15**, teamIds `[4, 5, 6, 7, 8, 9, 10, 11, 12, 14, 15, 16, 17, 18, 19]`
- alive at end (liveState 0): **4**, dead (5): 56
- winner (rank 1): **['4VIKINGS']**
- survivalTime: min 21 max 1646 (seconds — gives exact death ORDER and TIMING)
- killNum max 5, total 54
- killNumByGrenade total **9**, killNumInVehicle total **7**, gotAirDropNum total **3**
- knockouts total 45, assists total 29, headShotNum total 9
- damage max 750, maxKillDistance max 264
- location x 130339..570002, y 284300..596486, z 736..11466
- bHasDied true count: 0  <-- NOTE: unreliable, use liveState

## `tournament_d3m2.json`
- players **64**, teams **16**, teamIds `[2, 3, 4, 5, 6, 7, 8, 9, 18, 19, 20, 21, 22, 23, 24, 25]`
- alive at end (liveState 0): **4**, dead (5): 60
- winner (rank 1): **['Gaming Hub']**
- survivalTime: min 10 max 1295 (seconds — gives exact death ORDER and TIMING)
- killNum max 6, total 59
- killNumByGrenade total **2**, killNumInVehicle total **0**, gotAirDropNum total **1**
- knockouts total 53, assists total 30, headShotNum total 6
- damage max 730, maxKillDistance max 378
- location x 20331..197425, y 122050..268242, z -401..4699
- bHasDied true count: 0  <-- NOTE: unreliable, use liveState

## Canonical key order (43 keys, from the real capture)
```
["uId","playerName","playerOpenId","picUrl","showPicUrl","teamId","teamName","character","isFiring","bHasDied","location","health","healthMax","liveState","killNum","killNumBeforeDie","playerKey","gotAirDropNum","maxKillDistance","damage","killNumInVehicle","killNumByGrenade","AIKillNum","BossKillNum","rank","isOutsideBlueCircle","inDamage","heal","headShotNum","survivalTime","driveDistance","marchDistance","assists","outsideBlueCircleTime","knockouts","rescueTimes","useSmokeGrenadeNum","useFragGrenadeNum","useBurnGrenadeNum","useFlashGrenadeNum","PoisonTotalDamage","UseSelfRescueTime","UseEmergencyCallTime"]
```

## One real player record, verbatim (the ONLY sample you need)
```json
{
 "uId": 5514690730,
 "playerName": "GhPOWER",
 "playerOpenId": "31555669312537896",
 "picUrl": "",
 "showPicUrl": false,
 "teamId": 4,
 "teamName": "Gaming Hub",
 "character": "None",
 "isFiring": true,
 "bHasDied": false,
 "location": {
  "x": 83036,
  "y": 165966,
  "z": 1082
 },
 "health": 99,
 "healthMax": 100,
 "liveState": 0,
 "killNum": 2,
 "killNumBeforeDie": 2,
 "playerKey": 158483779,
 "gotAirDropNum": 0,
 "maxKillDistance": 15,
 "damage": 288,
 "killNumInVehicle": 0,
 "killNumByGrenade": 0,
 "AIKillNum": 2,
 "BossKillNum": 0,
 "rank": 1,
 "isOutsideBlueCircle": false,
 "inDamage": 55,
 "heal": 54,
 "headShotNum": 0,
 "survivalTime": 1295,
 "driveDistance": 1352,
 "marchDistance": 1992,
 "assists": 2,
 "outsideBlueCircleTime": 0,
 "knockouts": 1,
 "rescueTimes": 0,
 "useSmokeGrenadeNum": 3,
 "useFragGrenadeNum": 0,
 "useBurnGrenadeNum": 0,
 "useFlashGrenadeNum": 1,
 "PoisonTotalDamage": 0,
 "UseSelfRescueTime": 0,
 "UseEmergencyCallTime": 0
}
```

Types: `showPicUrl/isFiring/bHasDied/isOutsideBlueCircle` bool; `playerName/playerOpenId/picUrl/teamName/character` string; `outsideBlueCircleTime` float; everything else integer. `location` is an object `{x,y,z}`.

NOTE: the four `pmsc_*` files were converted from an Excel export that lacked
`AIKillNum`, `BossKillNum`, `UseSelfRescueTime`, `UseEmergencyCallTime` — those are present but 0.
---

## Derived demo fixtures (also pre-built — do not open)

### `demo-midmatch.json`
A synthetic **mid-match** state derived from `pmsc_s2_q2_finals_m1.json`. This is the
canonical "interesting moment" for the demo page and for eyeballing the live rankings.
- 16 teams / 64 players. **6 teams fully eliminated**, 10 teams alive.
- 24 players dead (liveState 5), **2 players knocked (liveState 4, health 1–18)**, 38 alive.
- Healths spread across the full gradient: 100 / 97 / 88 / 74 / 61 / 55 / 43 / 32 / 21 / 12.
- Exactly 1 grenade kill, 1 vehicle kill, 1 airdrop pickup — one of each achievement trigger.
- One player has `isOutsideBlueCircle: true`.
- **Every PlayerAfterMatchAPI field is 0**, exactly as the real API behaves mid-match.

### `demo-last4.json`
The same world with only **4 teams alive** (FMA WHITES, RED SAINTS, MAGICIANS ESPORTS,
ALTxNEVER BACK). Use it to verify the live-rankings → Last-4 switch without simulating a match.

---

## The three PCOB update groups — CRITICAL, from the official API rules

The 43 player fields are **not** one payload. The server pushes three groups on two routes
with independent timing, and the PCOB client forwards each as its own POST.

| Group | Fields | Update rule |
|---|---|---|
| **PlayerBaseInfo** (route A) | `location`, `health`, `healthMax`, `liveState`, `killNum`, `killNumBeforeDie` | every 2s when changed — in practice nearly every tick, because positions move constantly |
| **PlayerRealTimeAPI** (route B) | `gotAirDropNum`, `maxKillDistance`, `damage`, `killNumInVehicle`, `killNumByGrenade`, `rank`, `isOutsideBlueCircle` | every 2s **only when changed** — often unchanged for many ticks |
| **PlayerAfterMatchAPI** (route B) | `inDamage`, `heal`, `headShotNum`, `survivalTime`, `driveDistance`, `marchDistance`, `assists`, `outsideBlueCircleTime`, `knockouts`, `rescueTimes`, `useSmokeGrenadeNum`, `useFragGrenadeNum`, `useBurnGrenadeNum`, `useFlashGrenadeNum` | **stays 0 for the whole match** and is only populated once the match ends |

Routes A and B do not affect each other and can arrive in either order, so a tick may carry
a fresh `health` with a stale `damage`, or vice versa.

**Consequences the harness must test:**
1. Anything reading `knockouts`, `assists`, `headShotNum` or `survivalTime` **cannot work live** — it
   reads 0 until the match is over. Live "knocked" state must come from `liveState == 4`, not `knockouts`.
2. Achievements are safe: they key off `killNum`, `damage`, `killNumByGrenade`, `killNumInVehicle`,
   `gotAirDropNum`, all of which are live groups.
3. A graphic must not flicker or reset when a tick arrives with only one group updated.
