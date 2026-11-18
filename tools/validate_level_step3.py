"""Clock tower geometry, first-visit casts and independent keyboard run."""
from copy import deepcopy
from itertools import groupby
import json
from pathlib import Path
import re

from story_link_baseline import reverse_exact
from validate_level_step1 import contains, digest, number

SKILL = 'pendulum_rewind'
# (archetype, count, finite segments, preparations per segment). P means a pendulum setup.
KINDS = dict(L='sword_latch', S='shell_sentry', R='reflection_statue', G='notary_guard')
FORMATIONS = (
    [('L',1,1,0)], [('L',2,1,0),('S',3,3,0)], [('L',2,1,0),('S',1,3,0),('R',2,4,0)],
    [('L',2,1,0),('R',1,2,0),('S',1,3,0)], [], [],
    [('G',2,3,1),('L',2,1,1)], [('L',1,2,1),('G',3,2,1)],
    [('G',2,3,1),('R',1,3,0),('L',2,1,0)], [('G',3,1,3)])


def fixed(room):
    return [s for s in room['terrain'] if not (s.get('travelX') or s.get('travelY') or s.get('wallJump') or s.get('rampRise'))]


def bounds(s):
    r = s['rect']
    return r['x'], r['x'] + r['width'], round(r['y'] + r['height'], 4)


def faces(room):
    return {s['id']: bounds(s) for s in fixed(room)}


def ordinary_route(room, jump, gravity, dt):
    """Full-height ordinary jumps from rest in the overlap, followed by a walk off each return lip."""
    steps = fixed(room)
    if len(steps) < 4 or steps[0]['id'] != 'drum_floor':
        return
    flight = []
    for low, high in zip(steps, steps[1:]):
        left, right, y0 = bounds(low)
        l, r, y1 = bounds(high)
        if not high.get('oneWay') or y1 <= y0 or min(right,r) - max(left,l) < 1:
            return
        x = (max(left,l) + min(right,r)) / 2
        velocity, y = jump, y0
        for tick in range(1, 120):
            last = y
            velocity -= gravity * dt
            y += velocity * dt
            if any(s.get('wallJump') and s['rect']['x'] - .3 <= x <= bounds(s)[1] + .3
                   and y < bounds(s)[2] and s['rect']['y'] < y + 1.8 for s in room['terrain']):
                return
            crossed = [(z,k['id']) for k in steps for a,b,z in [bounds(k)] if a <= x <= b and y <= z <= last]
            if crossed:
                if max(crossed)[1] != high['id']:
                    return
                flight.append(tick)
                break
        else:
            return
        # A lower tread must project beyond the upper lip by enough space for the capsule.
        drop = l - 1 if left < l else r + 1
        if not left + .3 <= drop <= right - .3:
            return
    return dict(tops=[bounds(s)[2] for s in steps], jumpSeconds=round(sum(flight)*dt,2))


def room_config(room):
    deck = faces(room)
    cam = room['cameraBounds']
    for m in room['floors'] + room['points'] + room.get('stationMarks', []):
        if m.get('surface') not in deck:
            return False
        l,r,y = deck[m['surface']]
        if not l <= m['x'] <= r or not contains(cam, m['x'], y+1.5):
            return False
    return (len(deck) == len(fixed(room)) and room['hasFallLine'] and room['fallLine'] < min(v[2] for v in deck.values()) - 3
            and sum(bool(f.get('contactSave')) for f in room['floors']) == 1 and
            all(s.get('period',0)>0 and not s.get('wallJump') for s in room['terrain'] if s.get('travelX') or s.get('travelY')))


def ramp_connected(room):
    deck = faces(room)
    ramps = [s for s in room['terrain'] if s.get('rampRise')]
    if len(ramps) != 1: return False
    ramp = ramps[0]
    box = ramp['rect']
    start, end = box['x'], box['x'] + box['width']
    bottom = round(box['y'] + box['height'], 3)
    summit = round(bottom + ramp['rampRise'], 3)
    return (ramp.get('oneWay') and 0 < ramp['rampRise']/box['width'] <= .3 and
            deck['drum_floor'][2] == bottom and deck['drum_floor'][0] <= start < end and
            any(l+.35 <= end <= r-.35 and z == summit for l,r,z in deck.values()))


def moving_branch(room):
    parts = [s for s in room['terrain'] if s.get('travelX') or s.get('travelY')]
    if len(parts) != 1: return False
    carrier = parts[0]
    left,right,h = bounds(carrier)
    dx,dy = carrier['travelX'], carrier['travelY']
    period = carrier['period']
    floor = faces(room)['drum_floor']
    # Both extremities stay inside the tower. The low end can be boarded from the permanent floor.
    return (period > 0 and carrier.get('oneWay') and not carrier.get('wallJump') and
            max(abs(dx),abs(dy)) > 0 and floor[0] < left+min(dx,0) < right+max(dx,0) < floor[1] and
            0 < h - floor[2] < 2 and all(f.get('surface') != carrier['id'] for f in room['floors']))


def cast_counts(room):
    groups = room['roster']
    return dict(stations=room.get('stations',0), joints=sum(g['count']*g['segments']*(1 if g.get('skillOnly') else g.get('setupHits',1))
                for g in groups if g.get('setupSkill') == SKILL), silverSeams=room.get('stitchAnchors',0),
                swordSegments=sum(g['count']*g['segments'] for g in groups if not g.get('skillOnly')))


def formation(room, spec):
    points = {p['id']:p for p in room['placements']}
    refs = []
    if len(spec) != len(room['roster']):
        return False
    for g,(letter,n,segs,hits) in zip(room['roster'],spec):
        expected = (KINDS[letter],n,segs,SKILL if hits else '',hits or 1,False)
        if tuple(g.get(k) for k in ('id','count','segments','setupSkill','setupHits','skillOnly')) != expected:
            return False
        ids = g.get('placementIds',[])
        if len(ids) != n: return False
        refs.extend(ids)
    for key in refs:
        p = points.get(key)
        if not p or not contains(p['active'],p['x'],p['y']) or not any(
                l+.3 <= p['x'] <= r-.3 and abs(y+.05-p['y']) < .01 for l,r,y in faces(room).values()):
            return False
    return len(set(refs)) == len(refs) == len(room['placements']) and set(refs) == set(points)


def cast_run(room, skill):
    """Independent stage-1 sweeps, including a supported origin and clear first recall."""
    marks, deck = room.get('stationMarks', []), faces(room)
    if len(marks) != room['stations'] or len(marks) != 42: return False
    entry = room['floors'][0]
    reach = skill['stages'][0]['reach']
    lanes = [list(group) for _,group in groupby(marks,key=lambda m: m.get('surface'))]
    expected = [6]*7 if room['id']=='clock_07_return_upper' else [7]*6
    if list(map(len,lanes)) != expected or len({lane[0].get('surface') for lane in lanes}) != len(lanes):
        return False
    directions = {lane[0].get('surface'): 1 if lane[-1]['x'] > lane[0]['x'] else -1 for lane in lanes}
    for i,m in enumerate(marks):
        direction = directions[m.get('surface')]
        name = m.get('surface')
        if name not in deck: return False
        l,r,y = deck[name]
        x = m['x']
        origin = entry['x'] if i == 0 else x - direction*2.3
        end = origin + direction*reach
        lo,hi = sorted((origin,end))
        if not (l+.35 < lo < x < hi < r-.35 and (i or entry['surface']==name)):
            return False
        # The dash footprint adds 0.5 at the path ends and the target extends 0.4.
        for j,other in enumerate(marks):
            if other.get('surface') not in deck: return False
            oy = deck[other['surface']][2]
            if i!=j and y <= oy+1.85 and oy+.05 <= y+2 and lo-.9 <= other['x'] <= hi+.9:
                return False
        # Watch the feet trigger throughout the cast, not just at the initial stance.
        for p in room['placements']:
            a = p['active']
            if (a['y'] <= y+.05 <= a['y']+a['height'] and lo <= a['x']+a['width'] and a['x'] <= hi and
                    lo-.9 <= p['x'] <= hi+.9):
                return False
        if i:
            prev = marks[i-1]
            if prev['surface'] == name and direction*(x-prev['x']) <= reach:
                return False
            if prev['surface'] != name and (y >= deck[prev['surface']][2] or directions[prev['surface']] == direction):
                return False
    return True


def supported_doors(room, rooms):
    for d in room['exits']:
        if not any(l <= d['x'] <= r and contains(d['approach'],d['x'],y+.05) for l,r,y in faces(room).values()):
            return False
        if d['room'] in rooms and d.get('destinationFloor') not in {m['id'] for m in rooms[d['room']]['floors']}:
            return False
    return True


def lift(rs):
    for a,b,landing in [(0,3,'clock_lift_cage'),(3,0,'clock_lift_foot')]:
        d = next((d for d in rs[a]['exits'] if d['room']==rs[b]['id']),{})
        if (d.get('gate'), d.get('requiresFlag'), d.get('destinationFloor')) != (
                'investigation_complete','clock_04_cleared',landing):
            return False
        mark = next(m for m in rs[a]['floors'] if m['id'] in ('clock_lift_foot','clock_lift_cage'))
        y = faces(rs[a])[mark['surface']][2]
        if not contains(d['approach'],mark['x'],y+.05) or contains(d['approach'],d['x'],faces(rs[a])['drum_floor'][2]):
            return False
    return True


def validate_level_step3(check, project, scripts):
    res = project/'Assets/Resources'
    def read(name):return json.loads((res/name).read_text())
    path = 'Assets/Resources/Story/clock.json'
    raw = (project/path).read_text()
    patch = json.loads(Path(__file__).with_name('level-step3-extensions.json').read_text())[path]
    prior = reverse_exact(patch,raw)
    doc = json.loads(raw)
    rs,old = doc['rooms'],json.loads(prior)['rooms']
    rooms = {r['id']:r for r in rs}
    skill = next(s for s in read('Skills/catalog.json')['skills'] if s['skillId']==SKILL)
    def verify(value,label):check(bool(value),'Level step 3: '+label)
    mutants = []
    def mutant(value,label):
        mutants.append(bool(value))
        verify(value,'mutation detected / '+label)
    verify(raw != prior and doc['rows'] == json.loads(prior)['rows'] and doc['links'] == json.loads(prior)['links'] and
           [r['id'] for r in rs] == [r['id'] for r in old] and all(
               {m['id'] for m in b['floors']} <= {m['id'] for m in a['floors']} for b,a in zip(old,rs)),
           'exact predecessor, 42 original clock rows, entrance links and every v4 arrival ID')
    verify(reverse_exact(patch,raw+' ') == raw+' ', 'unrelated clock edit cannot be reversed as an approved historical change')
    dt = float(re.search(r'Fixed Timestep: ([.\d]+)',(project/'ProjectSettings/TimeManager.asset').read_text())[1])
    motion = number(scripts['PlayerController2D'],'JumpSpeed'), 9.81*number(scripts['CombatMotor2D'],'Body.gravityScale'), dt
    sets = {s['id']:s['layers'] for s in read('GameArt/backgrounds/room-sets.json')['sets']}
    routes,counts = {},{}
    for idx,(r,b,spec) in enumerate(zip(rs,old,FORMATIONS)):
        name = r['id']
        routes[name] = ordinary_route(r,*motion)
        counts[name] = [cast_counts(b),cast_counts(r)]
        verify(routes[name],name+' / ordinary ascent and exposed descent lips on every fixed tier')
        verify(room_config(r) and supported_doors(r,rooms),name+' / supported marks, camera, fall line, door volumes and destinations')
        verify(formation(r,spec),name+' / every instance binds its authored segments, setup, zone and position')
        layers = sets[r['backgroundSet']]
        verify([l['resource'] for l in layers]==['clock-interior','clock-train','clock-train'] and
               layers[1]['crop']['y'] == .5 and layers[2]['crop']['y']==0,name+' / clock painting, suspended works and brass foreground')
        bad=deepcopy(r);bad['terrain'][1]['rect']['y']+=3.5
        mutant(not ordinary_route(bad,*motion),name+' / broken vertical handoff')
        bad=deepcopy(r);bad['floors'][-1]['surface']='unknown'
        mutant(not room_config(bad),name+' / missing support')
        for i,g in enumerate(r['roster']):
            bad=deepcopy(r);bad['roster'][i].pop('setupHits',None)
            mutant(not formation(bad,spec),name+f' / group {i} lost explicit preparation count')
            for j,key in enumerate(g['placementIds']):
                bad=deepcopy(r);bad['roster'][i]['placementIds'][j]='wrong'
                mutant(not formation(bad,spec),name+' / missing instance '+key)
    verify(len({digest(sets[r['backgroundSet']]) for r in rs})==10,'ten distinct clock compositions')
    # Entries meet on the same storey across the five outward doors; the two drums descend in sequence.
    for a,b in [(0,1),(1,2),(2,3),(3,4),(4,5),(6,7)]:
        d=next(d for d in rs[a]['exits'] if d['room']==rs[b]['id'])
        f=next(f for f in rs[b]['floors'] if f['id']==d['destinationFloor'])
        verify(contains(d['approach'],d['x'],faces(rs[b])[f['surface']][2]+.05),rs[a]['id']+' / continuous storey across next door')
    verify(lift(rs),'investigation and escapement clear required in both directions and aligned lift landings')
    for i,j in [(0,3),(3,0)]:
        for k in ('gate','requiresFlag','destinationFloor'):
            bad=deepcopy(rs);next(d for d in bad[i]['exits'] if d['room']==bad[j]['id'])[k]=''
            mutant(not lift(bad),f'lift side {i+1} / lost '+k)
    for i in (0,4):
        verify(ramp_connected(rs[i]),rs[i]['id']+' / continuous sloped face joins two fixed landings')
        bad = deepcopy(rs[i])
        next(t for t in bad['terrain'] if t.get('rampRise'))['rampRise'] += 1
        mutant(not ramp_connected(bad),rs[i]['id']+' / ramp misses upper lip')
    for i in (1,2):
        verify(moving_branch(rs[i]),rs[i]['id']+' / moving branch bounded above fixed floor')
        bad = deepcopy(rs[i])
        next(t for t in bad['terrain'] if t.get('travelX'))['travelX'] = -90
        mutant(not moving_branch(bad),rs[i]['id']+' / carrier leaves shaft')
    timed = next(row for row in doc['rows'] if row['completionFlag'] == 'julius_returned_on_time')
    verify(timed['requiredPlayerAction']==['timed:julius_return_platform'] and
           'Mathf.Repeat(Time.time - signalAt, 6f) < 4f' in scripts['DistrictRoomLayout'],
           'investigated duel keeps four-second open interval in its six-second shutter cycle')
    def granted_before_return(branch, initial):
        flags = set(initial)
        granted = False
        for row in (row for row in doc['rows'] if row['sceneId'] == branch):
            if not all((t[1:] not in flags if t.startswith('!') else t in flags) for t in row['entryCondition']):
                return False
            flags.add(row['completionFlag'])
            if row['effect'] == 'defeat':
                granted = row['requiredPlayerAction'] == ['win:julius']
            if row['resumePoint'] == rs[6]['floors'][0]['id']:
                return granted
        return False
    verify(granted_before_return('julius_bout_i0', set()) and
           granted_before_return('julius_bout_i1', {'investigation_complete'}) and
           abs(rs[6]['stationMarks'][0]['x']-rs[6]['floors'][0]['x']) <= skill['stages'][0]['reach'],
           'both Julius branches land on the immediate first switch tier after the live reward')
    elite=rs[8]['placements']
    verify(any(g['setupSkill']==SKILL and any(contains(p['active'],q['x'],q['y']) for p in elite if p['id'] in g['placementIds']
               for q in elite if q['id'].startswith('reflection_statue')) for g in rs[8]['roster']),
           'chain lift shared guard preparation and defense opening')
    for r in rs[6:8]:
        verify(cast_run(r,skill),r['id']+' / 42 disjoint finite first casts from fixed ledges')
        for key,value in [('x',45),('surface','unknown')]:
            bad=deepcopy(r);bad['stationMarks'][0][key]=value
            mutant(not cast_run(bad,skill),r['id']+' / bad first switch '+key)
        bad=deepcopy(r);bad['stationMarks'][1]=bad['stationMarks'][0]
        mutant(not cast_run(bad,skill),r['id']+' / one activation would touch two stations')
    verify((skill['stage2At'],skill['stage3At'])==(30,80) and sum(r['stations'] for r in rs[6:8])>=skill['stage3At'],
           'station-only first visit clears 30 and 80 without a repeated target')
    wires=[
        ('RepresentativeRoomLayout','!string.IsNullOrEmpty(door.requiresFlag) && !Has(door.requiresFlag)','BuildSurfaces();',
         'Room.Placement(group.placementIds[i])','target.Initialize(kind, spawn, player, square, group.setupHits, group.skillOnly)'),
        ('DistrictRoomLayout','Room.stationMarks[i].x','Room.SurfaceTop(Room.stationMarks[i].surface)','StationTop(i) +',
         'LastReturnUse == switchUse','ReturnWindowOpen && Safe && OnPlank'),
        ('ClockDistrictProbe','bot.Session?.ActiveSlot == 3','area.StandingOn(f.surface)','scene_floor:clock:', 'segmentSeconds = now - lastContact',
         'if (onClock) inDistrict += now - lastFrame','CountStations(Upper) < 42','uses.Add(e.useId)','stations.Add(e.targetId)',
         'rewind.LastReturnUse != firstUse','Time.frameCount > clearedFrame + 1','cage.InReach(area.Rea.Body.position)',
         'Landed(bot, "clock_lift_cage")','Landed(bot, "clock_lift_foot")','result.outboundSeconds <= result.liftSeconds',
         'result.contacts.Count == Route.Length','result.synergyDoorUsed = lastRoom == Hall','Upper + ":station_0:posture"'),
        ('AuthoredSurface','!floor.IsTouching(actor)','Vector2.Dot(center - lip, RampNormal) - radius'),
        ('StoryMovingSurface','StepVelocity = (next - body.position) / Time.fixedDeltaTime','body.MovePosition(next)'),
        ('CombatMotor2D','surface.SupportsRamp(this, capsule, floor)','carrierSpeed = carrier.StepVelocity.x','Vector2.right * carrierSpeed'),
        ('ClockTraversal','keys.Cast(-Player.Facing);','Area.StandingOn(s.id)','keys.Hold(KeyCode.Space);','FaceAt(door.x, box.y','door.InReach(Player.Body.position)'),
        ('AutoTestDriver','if (clock.Drive(bot, keyboard, now))','clock?.ObserveUse(e, bot.Area.Room.id)','if (now - lastProgress > 18)',
         'clock != null && e.kind == "attempt_reset"','new ClockDistrictProbe(report.clockDistrict'),
        ('AutoTestRuntime','completed && Report.clockDistrict.run.status != "pass"','"slot-3.json"','save.storyFlags.Contains("scene_floor:clock:clock_rest_09")')]
    for src,*tokens in wires:
        code=scripts[src]
        verify(all(t in code for t in tokens),src+' / clock runtime contracts')
        for t in tokens:
            mutant(t in code and not all(k in code.replace(t,'') for k in tokens),src+' / lost '+t)
    return dict(routes=routes,countsBeforeAfter=counts,mutations=len(mutants),detected=sum(mutants),unity_executed=False)
