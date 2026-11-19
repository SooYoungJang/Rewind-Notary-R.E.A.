"""Opera first-visit path, timed span, finite waltz activations and same-endpoint walk comparison."""
import hashlib
import json
import math
import re
from copy import deepcopy
from collections import deque

from validate_level_step1 import contains, number
from validate_fixed_surface_route import fixed_edges, link, paths_from, room_faces, simulate_landing
from validate_opera_wave import validate_opera_wave
from validate_walk_contact import validate_gallery


def top(surface):
    r = surface['rect']
    return round(r['y'] + r['height'], 4)


def simulate_span(room, dt, speed, start_phase, eastbound):
    """Walk along the live carrier. Check support every tick including minimum overlap at either end."""
    surfaces = {s['id']: s for s in room['terrain']}
    span = surfaces['pendant']
    west, east = surfaces['west_gallery']['rect'], surfaces['east_gallery']['rect']
    r = span['rect']
    mid = r['x'] + r['width'] / 2
    start, end = (16.25, 31.25) if eastbound else (31.25, 16.25)
    direction = 1 if eastbound else -1
    x, waited, ridden, crossed = start, 0, False, False
    for tick in range(math.ceil(12/dt)):
        phase = (start_phase + tick*dt) % 6
        dx = span['travelX'] * (.5 - .5*math.cos(2*math.pi*tick*dt/span['period']))
        left, right = r['x']+dx, r['x']+dx+r['width']
        if not (west['x'] <= x <= west['x']+west['width'] or east['x'] <= x <= east['x']+east['width'] or left <= x <= right):
            return
        ahead = x + direction*speed*dt
        if phase < 2.8 and (x == start or abs(ahead-mid) < .55 and (x-mid)*direction < 0):
            waited += 1
            continue
        if left <= x <= right and west['x']+west['width'] < x < east['x']:
            ridden = True
        if (x-mid)*direction < 0 <= (ahead-mid)*direction:
            if phase < 2.8: return
            crossed = True
        x = ahead
        if (x-end)*direction >= 0:
            if ridden and crossed: return round((tick+1)*dt, 3), round(waited*dt, 3)


def waltz_stations(room, profile, lamp_radius, shot_radius, gravity, dt):
    """One grounded upward cast per station. A projectile is tested as it falls, a lamp stays lit."""
    marks = room['stationMarks']
    surfaces = {s['id']: s for s in room['terrain']}
    def center(m):
        return m['x'], top(surfaces[m['surface']]) + (3.45 if m.get('projectile') else 2.4)
    credits = []
    for i, m in enumerate(marks):
        x = m['x']
        f = surfaces[m['surface']]
        if not f['rect']['x'] + .32 < x < f['rect']['x'] + f['rect']['width'] - .32:
            return
        y = top(f)
        v = profile['jumpSpeed']
        touched = set()
        for tick in range(math.ceil(profile['duration']/dt)+1):
            for j, other in enumerate(marks):
                ox, oy = center(other)
                if other.get('projectile'):
                    # Only the housing within its lateral trigger and on this storey fires.
                    if abs(x-ox) > 1.15 or abs(top(f)-top(surfaces[other['surface']])) > .65: continue
                    oy -= tick*dt*2
                radius = shot_radius if other.get('projectile') else lamp_radius
                if abs(x-ox) <= profile['reach']+radius and abs((y+1)-oy) <= profile['reach']+radius:
                    touched.add(j)
            v -= gravity*dt
            y += v*dt
        if touched != {i}: return
        # Guards get first chance at a skill contact. No guard can consume this lamp's activation.
        for g in room['placements']:
            if abs(x-g['x']) <= profile['reach']+.4 and contains(g['active'],x,top(f)+.05):
                return
        credits.append(room['id']+':station_'+str(i))
    return credits


def fixed_walk(edges, physics, a, x, b, end):
    parents = paths_from(edges,a)
    path = []
    while b != a:
        if b not in parents: raise ValueError('Disconnected walk to '+b.name)
        path.append((parents[b],b)); b=parents[b]
    elapsed = 0
    deck = list(edges)
    for source, dest in reversed(path):
        launch, aim, direction = link(deck, source, dest, physics)
        if direction:
            landing, ticks = simulate_landing(deck,source,dest,launch,aim,direction,physics,x)
        else:
            elapsed += abs(x-launch)/physics[3]
            landing, ticks = simulate_landing(deck,source,dest,launch,aim,0,physics)
        x=landing
        elapsed += ticks*physics[2]
    return elapsed + abs(x-end)/physics[3]


def shortest_return(rooms, investigated):
    targets = {r['id']:r for r in rooms}
    start, end = rooms[4]['id'], rooms[1]['id']
    queue = deque([(start,[start])])
    seen = {start}
    while queue:
        here,path = queue.popleft()
        if here == end: return path
        for door in targets[here]['exits']:
            dest=door['room']
            if dest not in targets or dest in seen: continue
            if door['gate'] in ('investigation_complete','synergy') and not investigated: continue
            seen.add(dest)
            queue.append((dest,path+[dest]))
    return []


def walk_comparison(rooms, physics):
    # Follow the same graph and the same two floor markers as the in-engine comparison.
    total = 0
    parts = []
    for i in (4,3,2,1):
        room = rooms[i]
        prev = None if i == 4 else next(d for d in rooms[i+1]['exits'] if d['room']==room['id'])['destinationFloor']
        mark = next(f for f in room['floors'] if f['id']==(prev or 'opera_drop_head'))
        deck = room_faces(room)
        edges = fixed_edges(deck,physics)
        faces = {f.name:f for f in edges}
        if i == 1:
            end = next(f for f in room['floors'] if f['id']=='opera_drop_landing')
            face, x = faces[end['surface']], end['x']
        else:
            door = next(d for d in room['exits'] if d['room']==rooms[i-1]['id'])
            x=door['x']
            face=next(f for f in edges if f.left<=x<=f.right and contains(door['approach'],x,f.top+.05))
        start=faces[mark['surface']]
        if i == 2:
            t=fixed_walk(edges,physics,start,mark['x'],faces['east_gallery'],31.25)
            crossing=simulate_span(room,physics[2],physics[3],t,False)
            if not crossing: raise ValueError('Broken chandelier')
            t+=crossing[0]+fixed_walk(edges,physics,faces['west_gallery'],16.25,face,x)
        else:
            t=fixed_walk(edges,physics,start,mark['x'],face,x)
        parts.append(round(t,2)); total+=t
    return dict(rooms=[rooms[i]['id'] for i in (4,3,2,1)],secondsByRoom=parts,
                normalMovementSeconds=round(total,2),normalDoors=len(shortest_return(rooms,False))-1,dropDoors=len(shortest_return(rooms,True))-1,unitySeconds=None)


def validate_level_step4(check,project,scripts):
    from story_link_baseline import prior_level4_source
    def verify(ok,label): check(bool(ok),'Level step 4: '+label)
    path='Assets/Resources/Story/opera.json'
    raw=(project/path).read_text()
    prior=prior_level4_source(path,raw)
    doc, old=json.loads(raw),json.loads(prior)
    rooms=doc['rooms']
    verify(raw != prior and raw.split('\n  "rows":')[1] == prior.split('\n  "rows":')[1] and
           len(doc['rows'])==42 and [r['id'] for r in rooms]==[r['id'] for r in old['rooms']],
           'all 42 original rows and hub links retain their exact bytes and all ten room IDs')
    dt=float(re.search(r'Fixed Timestep: ([\d.]+)', (project/'ProjectSettings/TimeManager.asset').read_text())[1])
    motor=scripts['CombatMotor2D']
    dims=re.search(r'capsule.size = new Vector2\(([^,]+)f, ([^)]+)f\)', motor)
    physics=(number(scripts['PlayerController2D'],'JumpSpeed'),9.81*number(motor,'Body.gravityScale'),dt,
             number(scripts['PlayerController2D'],'MoveSpeed'), number(motor,'AirAcceleration'),number(motor,'AirBrake'),
             float(dims[1])/2,float(dims[2]),number(scripts['FixedSurfaceRoute'],'AimTolerance'))
    res=project/'Assets/Resources';art=res/'GameArt/backgrounds'
    sets={s['id']:s['layers'] for s in json.loads((art/'room-sets.json').read_text())['sets']}
    hashes,formations=set(),set()
    routes,counts={},{}
    def joint(r):return sum(g['count']*g['segments']*g.get('setupHits',1) for g in r['roster'] if g.get('setupSkill')=='curtain_call_waltz')
    for i,(r,before) in enumerate(zip(rooms,old['rooms'])):
        rid=r['id']
        deck=room_faces(r)
        edges=fixed_edges(deck,physics)
        faces={f.name:f for f in edges}
        verify(len(faces)>=3 and r['hasFallLine'] and r['fallLine']<min(f.top for f in edges)-3 and
               all(contains(r['cameraBounds'],(f.left+f.right)/2,f.top+1.75) for f in edges),rid+' camera and fall limits')
        connected=0
        for a in edges:
            reachable=paths_from(edges,a)
            for b in edges:
                if a==b:continue
                if i==2 and b not in reachable:
                    west={'west_bank','west_gallery'}
                    # The only connection between the two galleries is the suspended deck.
                    verify((a.name in west)!=(b.name in west),rid+' / requires carrier '+a.name+' to '+b.name)
                else:
                    verify(b in reachable,rid+' / ordinary fixed route '+a.name+' to '+b.name)
                    connected+=b in reachable
        routes[rid]=dict(fixedPairs=connected,faces=len(faces))
        verify({f['id'] for f in before['floors']} <= {f['id'] for f in r['floors']} and
               sum(bool(f.get('contactSave')) for f in r['floors'])==1,rid+' saved arrivals and contact floor')
        for m in r['floors']+r['points']+r.get('stationMarks',[]):
            f=faces.get(m['surface'])
            verify(f and f.left+.3<=m['x']<=f.right-.3,rid+' / fixed support '+m.get('id',str(m['x'])+' '+m['surface']))
        for d in r['exits']:
            target=next((other for other in rooms if other['id']==d['room']),None)
            verify(any(f.left+.3<=d['x']<=f.right-.3 and contains(d['approach'],d['x'],f.top+.05) for f in edges) and
                   (not target or any(m['id']==d.get('destinationFloor') for m in target['floors'])),rid+' / height-aware exit '+d['room'])
        all_ids=[p['id'] for p in r['placements']]
        ids=[key for g in r['roster'] for key in g['placementIds']]
        verify(len(ids)==len(set(ids)) and sorted(ids)==sorted(all_ids) and all(len(g['placementIds'])==g['count'] for g in r['roster']),rid+' / finite spawn bindings')
        for p in r['placements']:
            verify(any(f.left<p['x']<f.right and abs(f.top+.05-p['y'])<.005 for f in edges) and contains(p['active'],p['x'],p['y']),rid+' / active fighting floor '+p['id'])
        formation=tuple(sorted((g['id'],g['count'],g['segments'],g.get('setupSkill',''),g['setupHits']) for g in r['roster']))
        if formation:
            verify(formation not in formations,rid+' / distinct encounter');formations.add(formation)
        layers=sets[r['backgroundSet']]
        h=hashlib.sha256((art/(layers[0]['resource']+'.png')).read_bytes()).hexdigest()
        verify(h not in hashes and len(layers)==3 and layers[0]['factor']<layers[1]['factor']<layers[2]['factor'],rid+' / individual painting and depth')
        hashes.add(h)
        counts[rid]=dict(oldStations=before.get('stations',0),stations=r.get('stations',0),oldPreparations=joint(before),preparations=joint(r),
                         projectiles=sum(bool(m.get('projectile')) for m in r.get('stationMarks',[])))
    skills={s['skillId']:s for s in json.loads((res/'Skills/catalog.json').read_text())['skills']}
    skill=skills['curtain_call_waltz']
    guards=json.loads((res/'Waves/test-stage.json').read_text())['archetypes']
    wave=validate_opera_wave(verify,rooms[-1],physics,scripts,skill['stages'][-1],skills['basic_sword']['stages'][1],
                             next(g for g in guards if g['id']==rooms[-1]['roster'][0]['id']))
    representative=json.loads((res/'Story/representative.json').read_text())['rooms']
    gallery=validate_gallery(verify,[r for r in representative if r['stage']=='velvet'],scripts,physics,
                             skills['basic_sword']['stages'][0],guards)
    verify((skill['stage2At'],skill['stage3At'])==(30,80),'unchanged mastery thresholds')
    def casts(r,profile):return waltz_stations(r,profile,number(scripts['BenefactorSkill'],'LampRadius'),
                                               number(scripts['BenefactorSkill'],'ProjectileRadius'),physics[1],dt)
    sequenced=[]
    for r in rooms[6:8]:
        for profile in skill['stages']:
            ids=casts(r,profile)
            verify(ids and len(set(ids))==r['stations']==42,r['id']+' independent contacts for stage '+str(profile['stage']))
        sequenced.extend(casts(r,skill['stages'][0]) or [])
    start=rooms[6]['floors'][0]; first=rooms[6]['stationMarks'][0]
    verify(start['surface']==first['surface'] and abs(first['x']-start['x'])<=skill['stages'][0]['reach'] and
           all(r['resumePoint']==start['id'] for r in doc['rows'] if r['stepId'] in ('06_retire_sanctuary','08_grant_support')),
           'both duel endings place the first lamp within a grounded starting cast')
    verify(len(sequenced)>=80 and sequenced[29].endswith('07_return_roof:station_29') and
           sequenced[79].endswith('08_return_foyer:station_37'),'first visits reach 30/80 without any combat credit or reset')
    verify(sum(joint(r) for r in rooms[6:8])==16 and sum(r['stations'] for r in old['rooms'][6:8])==7,'7 original plus 16 setups become 84 independent stations plus 16 setups')
    early=deepcopy(rooms[6]);early['stationMarks'][1]=early['stationMarks'][0].copy()
    verify(not casts(early,skill['stages'][0]),'duplicate lamp cannot earn two activations')
    p=rooms[8]['placements'];g=next(a for a in p if a['id'].startswith('notary_guard'));r=next(a for a in p if a['id'].startswith('reflection'))
    verify(abs(g['y']-r['y'])<.02 and abs(g['x']-r['x'])<=2 and contains(r['active'],g['x'],g['y']),'mixed guard and reflection engage on the same counterweight')
    chand=rooms[2]
    at=[simulate_span(chand,dt,physics[3],tick*dt,direction) for direction in (False,True) for tick in range(round(6/dt))]
    verify(all(at),'all 300 launch phases in both directions cross the covered chandelier')
    broken=deepcopy(chand);next(s for s in broken['terrain'] if s['id']=='pendant')['rect']['width']=6
    verify(not simulate_span(broken,dt,physics[3],0,True),'a missing east handoff breaks the crossing')
    drop=next(d for d in rooms[4]['exits'] if d['room']==rooms[1]['id'])
    verify(drop['gate']=='investigation_complete' and drop['destinationFloor']=='opera_drop_landing' and
           not any(d['room']==rooms[4]['id'] for d in rooms[1]['exits']),'investigation opens the one-way wings-to-auditorium hatch')
    tour=walk_comparison(rooms,physics)
    verify(shortest_return(rooms,False)==tour['rooms'] and shortest_return(rooms,True)==[rooms[4]['id'],rooms[1]['id']] and
           tour['normalMovementSeconds']>0,'shortest district route goes from three doors to one between identical landings')
    roof_door=next(d for d in rooms[6]['exits'] if d['room']==rooms[7]['id'])
    verify(roof_door.get('waltzCue') and abs(roof_door['x']-rooms[6]['stationMarks'][-1]['x'])<=1 and
           'door.waltzCue && !RoofDoorOpen' in scripts['RepresentativeRoomLayout'] and
           'roofLampLit && CueShade' in scripts['OperaFixtures'] and 'index == Room.stations - 1) roofLampLit = true' in scripts['DistrictRoomLayout'],
           'final roof lamp enables the shaded foyer passage, including previously completed return saves')
    vent_x=number(scripts['OperaFixtures'],'VentX');boost=number(scripts['OperaFixtures'],'VentSpeed')
    faces={f.name:f for f in room_faces(rooms[3])}
    y=faces['gutter'].top; peak=y
    for _ in range(65):
        boost-=physics[1]*dt; y+=boost*dt; peak=max(peak,y)
    verify(faces['ridge'].left<vent_x<faces['ridge'].right and peak>faces['ridge'].top and
           not faces['dormer'].left<=vent_x<=faces['dormer'].right and 'body.linearVelocity.y > 0 && GameInput.JumpHeld' in scripts['OperaFixtures'],
           'roof vent ordinary jump reaches ridge past the intermediate dormer')
    probe=scripts['OperaDistrictProbe']; driver=scripts['AutoTestDriver']; runtime=scripts['AutoTestRuntime']
    verify(sum(bool(m.get('projectile')) for m in rooms[7]['stationMarks'])==6 and
           all(m.get('projectile',False)==(i%7==3) for i,m in enumerate(rooms[7]['stationMarks'])),
           'the fourth station in each foyer arcade emits a real deflectable shot')
    verify(any(d['room']==rooms[8]['id'] and d['gate']=='synergy' for d in rooms[1]['exits']) and
           'Record.Get(Room.skill).acquired && Record.Get(SkillRules.SilverStitch).acquired' in scripts['RepresentativeRoomLayout'],
           'the optional counterweights still require investigation, waltz and stitch')
    verify('liftBase = raisedLift.position.y' in scripts['DistrictRoomLayout'] and
           'raisedLift.position.y >= liftBase + LiftHeight' in scripts['DistrictRoomLayout'] and
           'Area.OnOperaLift(out var lift)' in scripts['OperaTraversal'] and 'lift.max.x + 1f' in scripts['OperaTraversal'],
           'ride from authored floor and step off the raised story lift')
    for label,script,terms in [
        ('real mounted shade and bank',probe,['area.RidingSurface(pendant)','area.ChandelierShaded','area.StandingOn("east_gallery")','!result.shadedCrossing']),
        ('three lighting phases and physical shutter',scripts['OperaFixtures'],['Mathf.Repeat(Time.fixedTime - cueStart, 6f)','CueBeat < 1f','CueBeat >= 2.8f','lightShutter.enabled = !shade','fixedFloors.Remove(lightShutter)']),
        ('single-cast station and downward shot logic',scripts['DistrictRoomLayout'],['Room.stationMarks[i].projectile','3.45f','Vector2.down * 2','emitter.spent = true','station.AddComponent<PracticeLamp2D>()']),
        ('posture and lamp events',probe,['e.kind != "valid_use"','!uses.Add(e.useId)','!stations.Add(e.targetId)','e.after == 30','e.after == 80','station < 42','uses.Count < 80']),
        ('all collider widths measured on entry',probe,['Physics2D.SyncTransforms();','area.Room.terrain','var actual = area.ProbeSurfaceBounds(s);','float measured = actual.size.x;','Mathf.Abs(measured - s.rect.width) > .005f']),
        ('normal and shortened return timed between common marks',probe,['"opera_drop_landing"','"opera_drop_head"','result.shortcutSeconds >= result.longSeconds','Time.frameCount > closedFrame + 1','!result.closedBeforeInvestigation']),
        ('physical ordered checkpoints',probe,['Entry, Hall, Chandelier, Roof, Wings, Arena, Upper, Lower, Stairs, Seam','!area.StandingOn(f.surface)','Mathf.Abs(feet.x - f.x) > .8f','"scene_floor:opera:" + f.id']),
        ('existing and new watchdogs and input',driver,['DrivingDistrict && !CheckDistrictProgress(now)','DistrictIdleLimit = 120','now - lastProgress > 18','opera.Drive(bot, keyboard, now)','opera?.ObserveUse(e, bot.Area.Room.id)','opera != null && e.kind == "attempt_reset"']),
        ('slot readback and other files',runtime,['requested && sandbox != null ? 6 : 3','"slot-4.json"','save.slotId == "slot-4"','"scene_floor:opera:opera_rest_09"','save.Get(BenefactorIds.Waltz).validUseCount >= 80','if (!opera.previousSlotsUnchanged)','completed && Report.operaDistrict.run.status != "pass"'])]:
        verify(all(t in script for t in terms),label)
    return dict(routes=routes,growth=counts,stationOnlyThresholds=[sequenced[i] for i in (29,79)],
                span=dict(samples=len(at),minSeconds=min(v[0] for v in at),maxSeconds=max(v[0] for v in at)),
                comparison=tour,run16Wave=wave,run17Gallery=gallery,
                authoredWidths=sum(len(r['terrain']) for r in rooms),unity_executed=False)
