"""Summarise a FakePcob `record` session: match clock, team wipes, kill feed, circles, airdrops.
Usage: python analyze_recording.py recordings/<timestamp>
Prints a game-time timeline usable as a replay checklist."""
import json, glob, os, sys, collections
root = sys.argv[1] if len(sys.argv) > 1 else sorted(glob.glob('recordings/*'))[-1]
def load(p): return json.load(open(p, encoding='utf-8'))
def ts(p): return int(os.path.basename(p).split('_')[1].split('.')[0])
def files(e): return sorted(glob.glob(os.path.join(root, e, '*.json')))
allinfo = load(files('getallinfo')[-1])['allinfo']
game_start = int(allinfo['GameStartTime']); fight = int(allinfo['FightingStartTime']); fin = int(allinfo['FinishedStartTime'])
def gt(p): return round(ts(p) / 1000 - fight)
def mmss(s): return f"{int(s)//60:02d}:{int(s)%60:02d}"
ev = []
tp = files('gettotalplayerlist')
first = load(tp[0])['playerInfoList']; size = collections.Counter(x['teamId'] for x in first)
names = {str(x['uId']): (x['playerName'], x['teamId']) for x in first}
print(f"root={root}\nGameID={allinfo['GameID']} lobby->fight {fight-game_start}s, fight->finish {fin-fight}s, recording starts at +{gt(tp[0])}s")
print(f"teams={len(size)} players={len(first)} teamIds={sorted(size)} short teams={[t for t,n in size.items() if n<4]}")
prev_alive = None; dead = set(); flags = {}
for p in tp:
    P = load(p)['playerInfoList']; g = gt(p)
    for t in size:
        if t not in dead and all(x['liveState'] == 5 for x in P if x['teamId'] == t):
            dead.add(t); r = next(x['rank'] for x in P if x['teamId'] == t)
            k = sum(x['killNum'] for x in P if x['teamId'] == t)
            ev.append((g, f"TEAM WIPE team {t} -> rank #{r}, {k} kills"))
    alive = len(size) - len(dead)
    if alive != prev_alive and alive <= 4: ev.append((g, f"{alive} teams alive"))
    prev_alive = alive
    for f, label in [('killNumByGrenade', 'first grenade kill'), ('killNumInVehicle', 'first vehicle kill'), ('gotAirDropNum', 'first airdrop looted')]:
        if f not in flags:
            x = next((x for x in P if x[f]), None)
            if x: flags[f] = 1; ev.append((g, f"{label}: {x['playerName']} (team {x['teamId']})"))
K = load(files('getkillinfo')[-1])['killInfo']  # newest first
offset = fight - game_start
kills = sorted(((int(k["CurGameTime"]) - offset, k) for k in K), key=lambda e: e[0])
fk = next(k for _, k in kills if k['ResultHealthStatus'] == '2')
ev.append((int(fk['CurGameTime']) - offset, f"FIRST BLOOD {fk['CauserName']} -> {fk['VictimName']} ({fk['Distance']}m, item {fk['ItemID']})"))
lr = max((k for _, k in kills if k['ResultHealthStatus'] == '2'), key=lambda k: k['Distance'])
ev.append((int(lr['CurGameTime']) - offset, f"longest kill {lr['Distance']}m {lr['CauserName']} -> {lr['VictimName']}"))
prev = None
for p in files('getcircleinfo'):
    c = load(p)['circleInfo']; key = (c['CircleStatus'], c['CircleIndex'])
    if key != prev and c['CircleStatus'] in ('0', '2'):
        ev.append((gt(p), f"circle {c['CircleIndex']} {'WAITING' if c['CircleStatus']=='0' else 'SHRINKING'} for {c['MaxTime']}s"))
    prev = key
for p in files('getairdropboxinfo')[1:]:
    ev.append((gt(p), f"airdrop #{load(p)['airdropboxinfo']['AirDropBoxInfo'][0]['AirDropID']} spawned"))
ev.append((fin - fight, "MATCH FINISHED (end-of-match fields populate now)"))
print(f"kill feed: {sum(k['ResultHealthStatus']=='2' for k in K)} kills, {sum(k['ResultHealthStatus']=='1' for k in K)} knocks")
for g, s in sorted(ev, key=lambda e: e[0]): print(f"  {mmss(g)}  {s}")
