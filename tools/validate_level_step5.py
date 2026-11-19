"""Bell district geometry, silent warning replay and first-visit forge supply, using authored rooms and motor tuning."""
from collections import Counter
from copy import deepcopy
import hashlib
import json
import math
import re
from PIL import Image

from story_link_baseline import prior_level5_source
from validate_bell_reentry import validate_bell_reentry
from validate_fixed_surface_route import fixed_edges, paths_from, room_faces, simulate_landing
from validate_level_step1 import contains, number
from validate_level_step4 import fixed_walk, shortest_return


def physics_from_project(project, scripts):
    motor = scripts['CombatMotor2D']
    dims = re.search(r'capsule.size = new Vector2\(([^,]+)f, ([^)]+)f\)', motor)
    dt = float(re.search(r'Fixed Timestep: ([\d.]+)', (project/'ProjectSettings/TimeManager.asset').read_text())[1])
    controls = scripts['PlayerController2D']
    return (number(controls, 'JumpSpeed'), 9.81 * number(motor, 'Body.gravityScale'), dt, number(controls, 'MoveSpeed'),
            number(motor, 'AirAcceleration'), number(motor, 'AirBrake'), float(dims[1])/2, float(dims[2]),
            number(scripts['FixedSurfaceRoute'], 'AimTolerance'))


def first_landing(faces, x, start, fall_line, physics):
    """Drop from a restore mark, stopping at the first supporting top or the room's retry line."""
    y, vy = start, 0
    while y >= fall_line:
        prev = y
        vy = max(-18, vy-physics[1]*physics[2])
        y += vy*physics[2]
        hits = [f for f in faces if x+physics[6]>f.left and x-physics[6]<f.right and y<=f.top<=prev]
        if hits:
            return max(hits, key=lambda f:f.top).name
    return None


def silent_wave(room, wave, physics):
    deck = room_faces(room)
    faces = {f.name: f for f in deck}
    low, high = faces[wave['surface']], faces[wave['refuge']]
    x, dt = wave['atX'], physics[2]
    if not low.left < x < low.right or wave['speed'] <= 0 or not high.top > low.top + wave['height'] > low.top:
        return None
    landing = simulate_landing(deck, low, high, x, x, 0, physics)
    if not landing:
        return None
    # Check both edges and every 0.1 unit of the cue's trigger. Charge a 0.20 s reaction
    # and two input/physics ticks. Bound first possible impact with the near edge of the capsule.
    worst = None
    for i in range(-30, 31):
        feet = x + i/10
        if not low.left + physics[6] <= feet <= low.right - physics[6]:
            continue
        walk = math.ceil(abs(feet-x) / (physics[3]*dt)) * dt
        safe = .20 + 2*dt + walk + landing[1]*dt
        earliest = wave['windup'] + max(0, min(feet,x)-physics[6]-.16-low.left)/wave['speed']
        if worst is None or earliest - safe < worst[0]:
            worst = earliest - safe, earliest, safe, feet
    if worst is None or worst[0] <= 0: return None
    return dict(surface=low.name, refuge=high.name, launchX=x, rise=round(high.top-low.top, 2),
                forecastSeconds=wave['windup'], jumpSeconds=round(landing[1]*dt, 2),
                earliestHit=round(worst[1], 3), refugeBy=round(worst[2], 3), spareSeconds=round(worst[0], 3),
                worstStartX=worst[3])


def forge_contacts(room, i, profile, physics, guard, geometry):
    marks = room['stationMarks']
    faces = {f.name:f for f in room_faces(room)}
    m = marks[i]
    f = faces[m['surface']]
    direction = 1 if i//7 % 2 == 0 else -1
    # Same stand-off and tolerance as the keyboard path. A down cast needs an airborne frame.
    origin = m['x'] - direction*1.1
    normal_width, normal_height, seal_width, seal_height = guard
    for offset in (-.16, 0, .16):
        launch = origin + offset
        if not f.left+physics[6] <= launch <= f.right-physics[6]: return False
        for tick in (1, 2, 3):
            t = tick*physics[2]
            feet = f.top + physics[0]*t - physics[1]*physics[2]**2*tick*(tick+1)/2
            if profile['stage'] == 3:
                # Downward strike over the same support. Both bounding boxes are axis aligned.
                lo, hi = launch-profile['burstRadius'], launch+profile['burstRadius']
                bottom, ceiling = f.top+.05-profile['burstRadius'], f.top+.05+profile['burstRadius']
            else:
                lo, hi = sorted((launch,launch + direction*profile['reach']))
                center, height = geometry
                bottom, ceiling = feet+center-height/2, feet+center+height/2
            touched = []
            for j, other in enumerate(marks):
                seal = j == 0 and room['id']=='bell_07_return_chime'
                target_height, footprint = (seal_height,seal_width) if seal else (normal_height,normal_width)
                x, y = other['x'], faces[other['surface']].top+.55+(target_height-normal_height)/2
                if x+footprint/2 >= lo and x-footprint/2 <= hi and y+target_height/2 >= bottom and y-target_height/2 <= ceiling:
                    touched.append(j)
            if touched != [i]: return False
            # WaveCombatTargets gets the first chance to credit an activation.
            for p in room['placements']:
                if (contains(p['active'],launch,feet) and p['x']+.4 >= lo and p['x']-.4 <= hi and
                        p['y']+1.8 >= bottom and p['y'] <= ceiling):
                    return False
    return True


def trip(rooms, physics):
    seconds = []
    for i in (4,3,2,1):
        r = rooms[i]
        start_id = 'bell_drop_head' if i == 4 else next(d for d in rooms[i+1]['exits'] if d['room']==r['id'])['destinationFloor']
        start = next(m for m in r['floors'] if m['id']==start_id)
        edges = fixed_edges(room_faces(r), physics)
        faces = {f.name:f for f in edges}
        if i == 1:
            end = next(m for m in r['floors'] if m['id']=='bell_drop_landing')
            face, x = faces[end['surface']], end['x']
        else:
            door = next(d for d in r['exits'] if d['room']==rooms[i-1]['id'])
            x = door['x']
            face, = (f for f in edges if f.left<=x<=f.right and contains(door['approach'],x,f.top+.05))
        seconds.append(fixed_walk(edges,physics,faces[start['surface']],start['x'],face,x))
    return dict(before=shortest_return(rooms,False), after=shortest_return(rooms,True),
                secondsByRoom=[round(x,2) for x in seconds], movementSeconds=round(sum(seconds),2))


def validate_level_step5(check, project, scripts):
    def verify(ok, label): check(bool(ok), 'Level step 5: '+label)
    path = 'Assets/Resources/Story/bell.json'
    raw = (project/path).read_text()
    original = prior_level5_source(path, raw)
    doc, old = json.loads(raw), json.loads(original)
    rooms = doc['rooms']
    verify(raw != original and raw.split('\n  "rows":')[1] == original.split('\n  "rows":')[1] and
           len(doc['rows']) == 42, '42 rows and hub links byte-identical')
    physics = physics_from_project(project, scripts)
    art = project/'Assets/Resources/GameArt/backgrounds'
    sets = {s['id']:s['layers'] for s in json.loads((art/'room-sets.json').read_text())['sets']}
    skill = next(s for s in json.loads((project/'Assets/Resources/Skills/catalog.json').read_text())['skills'] if s['skillId']=='resonance_forge')
    guard = tuple(number(scripts['BenefactorSkill'], v) for v in ('GuardWidth','GuardHeight')) + tuple(number(scripts['ResonanceTarget2D'], v) for v in ('SealWidth','SealHeight'))
    geometry = tuple(number(scripts['BenefactorSkill'], v) for v in ('Height','HitHeight'))
    routes, waves, paintings, formations, seq = {}, {}, set(), set(), []
    total_widths = 0
    for r, previous in zip(rooms,old['rooms'],strict=True):
        rid = r['id']
        deck = room_faces(r)
        edges = fixed_edges(deck,physics)
        faces = {f.name:f for f in edges}
        cam = r['cameraBounds']
        low, high = min(f.top for f in edges), max(f.top for f in edges)
        verify(rid==previous['id'] and r['hasFallLine'] and r['fallLine']<=low-4 and
               all(contains(cam,(f.left+f.right)/2,f.top+1.9) for f in edges) and high-low>=4, rid+' camera and fall margin')
        pairs = 0
        for f in edges:
            linked = paths_from(edges,f)
            verify(len(linked)==len(edges), rid+' / ordinary jump and walk to every face from '+f.name)
            pairs += len(linked)-1
        total_widths += len(r['terrain'])
        verify(len(faces)==sum(not (s.get('travelX') or s.get('travelY')) for s in r['terrain']),rid+' all authored terrain participates in fixed contact and width checks')
        verify({m['id'] for m in previous['floors']}<={m['id'] for m in r['floors']} and
               sum(bool(m.get('contactSave')) for m in r['floors'])==1,rid+' original restore IDs and one contact save')
        for m in r['floors']+r['points']+r.get('stationMarks',[]):
            f = faces[m['surface']]
            verify(f.left+physics[6]<=m['x']<=f.right-physics[6] and r['fallLine']<f.top and
                   not any(g.top>f.top and not g.one_way and g.bottom<f.top+physics[7] and g.left<m['x']<g.right for g in edges),
                   rid+' / supported feet and capsule clearance '+m.get('id',str((m['surface'],m['x']))))
        for mark in r['floors']:
            f = faces[mark['surface']]
            verify(first_landing(edges,mark['x'],f.top+.05,r['fallLine'],physics)==f.name,
                   rid+' / restore lands on named collider '+mark['id'])
        # Sweep accessible columns inside the bounding walls, including the foot radius.
        west, east = (next(f for f in deck if f.name==v) for v in ('west_wall','east_wall'))
        gap = next((i/10 for i in range(math.ceil((west.right+physics[6])*10),math.floor((east.left-physics[6])*10)+1)
                    if first_landing(edges,i/10,high+1,r['fallLine'],physics) is None), None)
        verify(gap is not None,rid+' exposed fall column crosses its own retry line')
        for d in r['exits']:
            target = next((room for room in rooms if room['id']==d['room']),None)
            verify(sum(f.left+.31<=d['x']<=f.right-.31 and contains(d['approach'],d['x'],f.top+.05) for f in edges)==1 and
                   (target is None or any(m['id']==d['destinationFloor'] for m in target['floors'])),rid+' / door and destination '+d['room'])
        bound = [key for g in r['roster'] for key in g['placementIds']]
        verify(len(set(bound))==len(bound) and Counter(bound)==Counter(p['id'] for p in r['placements']) and
               all(len(g['placementIds'])==g['count'] for g in r['roster']),rid+' finite roster binding')
        for p in r['placements']:
            verify(any(f.left+.31<p['x']<f.right-.31 and abs(f.top+.05-p['y'])<.001 for f in edges) and
                   contains(p['active'],p['x'],p['y']),rid+' / grounded enemy '+p['id'])
        form = tuple(sorted((g['id'],g['count'],g['segments'],g.get('setupSkill','')) for g in r['roster']))
        if form:
            verify(form not in formations,rid+' distinct encounter')
            formations.add(form)
        layers = sets[r['backgroundSet']]
        signature = hashlib.sha256((art/(layers[0]['resource']+'.png')).read_bytes()).digest()
        verify(r['backgroundSet']==rid and signature not in paintings and len(layers)==3 and
               layers[0]['factor']<layers[1]['factor']<layers[2]['factor'] and layers[1]['resource'] in ('bell-bronze','bell-weight') and
               layers[2]['resource']=='bell-dust',rid+' individual painting, bellwork and foreground dust')
        paintings.add(signature)
        for j,layer in enumerate(layers):
            with Image.open(art/(layer['resource']+'.png')) as img:
                verify(min(img.size)>64 and (j==0 or (img.getextrema()[-1][0]==0 and img.mode=='RGBA' and img.getextrema()[-1][1]>=128)),
                       rid+' visible picture and required transparency '+layer['resource'])
        far = layers[0]
        verify(all(far['bottom']+cy*far['vertical']<=cy-5.5 and far['bottom']+cy*far['vertical']+far['height']>=cy+5.5
                   for cy in (cam['y'],cam['y']+cam['height'])),rid+' far painting covers both vertical camera limits')
        if r.get('bellWaves'):
            waves[rid] = []
            for w in r['bellWaves']:
                result = silent_wave(r,w,physics)
                verify(result is not None,rid+' / silent cue to upper ledge '+w['surface'])
                waves[rid].append(result)
        routes[rid] = dict(faces=len(edges),ordinaryPairs=pairs,bottom=low,top=high,fallLine=r['fallLine'],fallColumn=gap)
    tunnel, acoustic = rooms[1:3]
    gate_decks = {}
    for cuts in (0,1,2):
        closed = {s['id'] for s in tunnel['terrain'] if s.get('bellLatch',0)>cuts}
        graph = fixed_edges([f for f in room_faces(tunnel) if f.name not in closed],physics)
        latches = [p for p in tunnel['placements'] if p['id'].startswith('sword_latch')]
        next_latch = latches[min(cuts,1)]
        reachable = all(len(paths_from(graph,f))==len(graph) for f in graph)
        verify(reachable and any(f.left<next_latch['x']<f.right and abs(f.top+.05-next_latch['y'])<.001 for f in graph)
               and len(closed)==2-cuts, 'tunnel permanent retreat and next sword latch with '+str(cuts)+' bridged forks')
        gate_decks[str(cuts)] = len(graph)
    timed, = (s for s in acoustic['terrain'] if s.get('bellBeat'))
    verify(all(m['surface']!=timed['id'] for m in acoustic['floors']) and
           'beat >= .8f && beat < 3.2f' in scripts['BellWaves'] and 'cuts >= 2 || Has("bell_03_cleared")' in scripts['BellWaves'] and
           'fixedFloors.Remove(box)' in scripts['BellWaves'] and 'box.isTrigger = !open' in scripts['BellWaves'],
           'ring bridge forecasts each open beat, holds a rider and bolts after both latches')
    deck = room_faces(acoustic)
    by_id = {f.name:f for f in deck}
    launch = 18
    landing = simulate_landing(deck,by_id['dais'],by_id[timed['id']],launch,launch,0,physics)
    crossing_times = []
    dt = physics[2]
    for frame in range(round(4/dt)):
        for wait in range(round(4/dt)+1):
            phase = ((frame+wait)*dt)%4
            if landing and .8 <= phase and phase + landing[1]*dt <= 3.2:
                crossing_times.append(round((wait+landing[1])*dt,2))
                break
    verify(len(crossing_times)==round(4/dt), 'acoustic hall ordinary jump reaches the lit step from every starting beat')
    verify([r['id'] for r in rooms[3:6]]==list(waves) and all(waves.values()),'each room on the ascent has a live pulse')
    verify(not silent_wave(rooms[3],dict(rooms[3]['bellWaves'][0],windup=.01,speed=100),physics),
           'early unannounced wave fails the refuge timeline')
    source = scripts['BellWaves']
    verify(all(x in source for x in ('outline.SetActive(true)', 'weight.transform.position', 'elapsed >= data.windup',
           'data.speed * Time.fixedDeltaTime','body.min.y < floor + data.height','actor.Health.Hit(1','area.StandingOn(data.refuge)')) and
           'Audio' not in source and 'wave.Tick(this, player)' in scripts['DistrictRoomLayout'] and 'BuildBellWaves();' in scripts['RepresentativeRoomLayout'],
           'clapper, outline, sweep and damage share a fixed clock independent of audio')
    verify([routes[r['id']]['top'] for r in rooms[3:6]]==sorted(routes[r['id']]['top'] for r in rooms[3:6]) and
           routes[rooms[1]['id']]['bottom']==0 and routes[rooms[5]['id']]['top']==58 and
           all(r['stationMarks'][0]['surface']!=r['stationMarks'][-1]['surface'] for r in rooms[6:8]),'58-unit relief and descending return')
    for r in rooms[6:8]:
        for profile in skill['stages']:
            for i in range(len(r['stationMarks'])):
                verify(forge_contacts(r,i,profile,physics,guard,geometry),f"{r['id']} / independent forge {i+1}, stage {profile['stage']}")
        verify(r['stations']==len(r['stationMarks'])==42,r['id']+' six descending tiers of seven mechanisms')
        seq.extend(r['id']+':station_'+str(i) for i in range(r['stations']))
    first = rooms[6]['floors'][0]
    target = rooms[6]['stationMarks'][0]
    verify(target['surface']==first['surface'] and abs(target['x']-1.1-first['x'])<.001 and
           all(row['resumePoint']==first['id'] for row in doc['rows'] if row['stepId'] in ('06_retire_sanctuary','08_grant_support')),
           'both endings put the first air strike within reach at the saved horizontal position')
    prepare = lambda r: sum(g['count']*g['segments']*g.get('setupHits',1) for g in r['roster'] if g.get('setupSkill')==skill['skillId'])
    before = sum(r['stations']+prepare(r) for r in old['rooms'][6:8])
    total = len(set(seq))+sum(prepare(r) for r in rooms[6:8])
    verify((before,total,skill['stage2At'],skill['stage3At'])==(24,100,30,80) and
           seq[29].startswith(rooms[6]['id']) and seq[79].startswith(rooms[7]['id']), 'finite 30 and 80 in first visits with no resets')
    merged = deepcopy(rooms[6]);merged['stationMarks'][1]=merged['stationMarks'][0].copy()
    verify(not forge_contacts(merged,0,skill['stages'][0],physics,guard,geometry),'two plates within one swing cannot supply two credits')
    mixed = rooms[8]['placements']
    verify(any(g['id'].startswith('notary_guard') and s['id'].startswith('reflection_statue') and g['y']==s['y'] and
               contains(s['active'],g['x'],g['y']) for g in mixed for s in mixed),'shortcut elite guard and statue share a combat floor')
    comparison = trip(rooms, physics)
    h = next(d for d in rooms[4]['exits'] if d['room']==rooms[1]['id'])
    head = next(m for m in rooms[4]['floors'] if m['id']=='bell_drop_head')
    verify(h['gate']=='investigation_complete' and h['destinationFloor']=='bell_drop_landing' and h['x']==head['x'] and
           comparison['before']==[r['id'] for r in rooms[4:0:-1]] and comparison['after']==[rooms[4]['id'],rooms[1]['id']],
           'closed before investigation, three doors become one between the same marks')
    trials = json.loads((project/'Assets/Resources/Trials/catalog.json').read_text())['trials']
    patterns = next(t for t in trials if t['id']=='gabriel')['patterns']
    verify('trialTop = floorY;' in scripts['BenefactorTrialOpponent'] and 'Definition.spawnX, trialTop + 0.15f' in scripts['BenefactorTrialOpponent'] and
           'Room.SurfaceTop("refuge") - 1, 15, 2' in scripts['RepresentativeRoomLayout'] and 'player.ResetAt(new Vector2(d.playerX, trialTop + .1f))' in scripts['DistrictRoomLayout'],
           'renewal arena, player and opponent share the high refuge floor')
    verify('target.InitializeBell(OperaSprite(art, 1.1f), seal)' in scripts['DistrictRoomLayout'] and 'box.isTrigger = !seal' in scripts['ResonanceTarget2D'] and
           'GetComponent<Collider2D>().enabled = false' in scripts['ResonanceTarget2D'] and guard[3] > physics[0]**2/(2*physics[1]) and
           target['x']-guard[2]/2 >= first['x']+physics[6] and 'grounded ? BenefactorIds.Canvas : BenefactorIds.Forge' in scripts['GameInput'] and
           'keys.Cast(0, -1)' in scripts['BellTraversal'], 'airborne down-cast shatters the immediate physical seal from clear standing space')
    verify(len(patterns)==2 and all(p['action']=='bell' for p in patterns) and
           'belfry || gallery ? DistrictTrial : mikhailTrial' in scripts['RepresentativeArea'] and
           all(v in scripts['BenefactorTrialOpponent'] for v in ('pattern == 0 ? .75f : 2.8f','BellOutline(bellLanding, Pattern.windup)',
               'if (StoryBell) return bellLanding;', 'Threat().Intersects(player.GetComponent<Collider2D>().bounds)')),
           'two catalog strikes lock their low radial or tall landing outline and use that volume on impact')
    low_strike, landing_strike = patterns
    heights = re.search(r'float height = pattern == 0 \? ([.\d]+)f : ([.\d]+)f', scripts['BenefactorTrialOpponent'])
    low_height, tall_height = (float(heights[i]) for i in (1,2))
    dt = physics[2]
    # Start the ordinary jump ten physics ticks before the first impact. Check the whole active interval.
    ys = [physics[0]*tick*dt-physics[1]*dt**2*tick*(tick+1)/2
          for tick in range(10, 11+math.ceil(low_strike['duration']/dt))]
    sidestep = .2 + dt + math.ceil((landing_strike['reach']/2+physics[6]+.1)/(physics[3]*dt))*dt
    verify(min(ys)>low_height and low_strike['windup']>10*dt and tall_height>max(ys) and sidestep<landing_strike['windup'],
           'normal jump clears the full low strike and ordinary walk exits the marked tall strike before impact')
    boss = dict(lowHeight=low_height,lowWindup=low_strike['windup'],minimumJumpFeet=round(min(ys),2),
                tallHeight=tall_height,landingWindup=landing_strike['windup'],sidestepSeconds=round(sidestep,2))
    probe, runtime, driver = (scripts[k] for k in ('BellDistrictProbe','AutoTestRuntime','AutoTestDriver'))
    verify('Entry, Tunnels, Hall, Shaft, Landing, Arena, Chime, Under, Table, Seam' in probe and 'area.StandingOn(floor.surface)' in probe and
           'Mathf.Abs(feet.x - floor.x) > .8f' in probe and 'scene_floor:bell:' in probe and 'result.waveRooms.Count != 3' in probe,
           'ordered physical contact receipts and three visual refuge witnesses')
    verify(all(k in probe for k in ('!uses.Add(e.useId)','!stations.Add(e.targetId)','e.after == 30','e.after == 80','station != 42',
                                  'Time.frameCount > closedFrame + 1','result.shortcutSeconds >= result.longSeconds')),'probe rejects duplicate growth and measures closed and opened journeys')
    verify(all(k in runtime for k in ('requested && sandbox != null ? 6 : 3','ReadPreviousSlotHashes(4)','save.slotId == "slot-5"',
           'previousSlotsBefore?.Length == 4','if (!bell.previousSlotsUnchanged)','"scene_floor:bell:bell_rest_09"','if (!bell.slotReadback)')) and
           'bot.Select(4)' in probe and 'AutoTestRuntime.ReadPreviousSlotHashes(4)' in probe and
           'bell != null && e.kind == "attempt_reset"' in driver and 'now - entered > 1500) { Stop("Bell district deadline")' in driver,
           'sandbox fifth slot, four SHA-256 comparisons, disk readback and unchanged watchdogs')
    opera = json.loads((project/'Assets/Resources/Story/opera.json').read_text())['rooms']
    verify(sum(len(r['terrain']) for r in opera)==55 and all('TerrainWidth.Measure(area, result.widths)' in scripts[name]
           for name in ('OperaDistrictProbe','BellDistrictProbe')) and 'ProbeCollider(s).bounds' in scripts['PlaytestObservations'] and
           'Mathf.Abs(measured - s.rect.width) > .005f' in scripts['OperaDistrictProbe'],
           'both probes measure every authored root collider at the 0.005 world-unit tolerance')
    reentry = validate_bell_reentry(verify, project, rooms, scripts, physics)
    return dict(run21Reentry=reentry, routes=routes, waves=waves, tunnelFacesByLatchCount=gate_decks, timedHall=dict(samples=len(crossing_times),seconds=max(crossing_times,default=None)), boss=boss, before=before, stationOnlyThresholds=[seq[29],seq[79]],
                stationSupply=len(seq), totalSupply=total, shortcut=comparison, operaWidths=55,bellWidths=total_widths,
                unity_executed=False)
