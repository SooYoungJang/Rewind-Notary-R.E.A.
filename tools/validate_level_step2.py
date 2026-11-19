"""Velvet first-visit routes, physical supply budget, and independent input-probe contracts."""
from copy import deepcopy
import json
from pathlib import Path
import re

from story_link_baseline import prior_pass2_source, reverse_exact
from validate_level_step1 import contains, digest, number
from validate_fixed_surface_route import Face, fixed_edges, link, simulate_landing
from validate_level_step5 import physics_from_project
from validate_walk_contact import velvet_deck

IDS = ('velvet_01_entry', 'velvet_02_loom', 'velvet_03_gallery', 'velvet_04_arena',
       'velvet_05_return', 'velvet_06_shortcut')


def top(rect):
    return round(rect['y'] + rect['height'], 5)


def fixed_route(room, motion):
    """Steer at the same inset as the keyboard driver. Only descending top crossings can land."""
    surfaces = {s['id']: s for s in room['terrain']}
    route = [surfaces[name] for name in ('tread_1', 'tread_2', 'balcony')]
    first = route[0]['rect']
    direction = 1 if route[1]['rect']['x'] > first['x'] else -1
    x = first['x'] - 1.5 if direction == 1 else first['x'] + first['width'] + 1.5
    y, ticks = 0., 0
    speed, jump, accel, brake, gravity, dt = motion
    for surface in route:
        r = surface['rect']
        if not surface.get('oneWay') or surface.get('travelX') or surface.get('travelY'):
            return
        inset = min(1.35, r['width'] / 2)
        target = r['x'] + (inset if direction == 1 else r['width'] - inset)
        vx, vy = 0., jump
        for _ in range(100):
            desired = 0 if abs(target - x) <= .14 else speed if target > x else -speed
            change = dt * (accel if desired else brake)
            vx += max(-change, min(change, desired - vx))
            x += dt * vx
            previous = y
            vy -= gravity * dt
            y += vy * dt
            ticks += 1
            for other in surfaces.values():
                box = other['rect']
                if not other.get('oneWay') and box['x'] - .28 < x < box['x'] + box['width'] + .28 and                         box['y'] < y + 1.6 and top(box) > y:
                    return
            crossed = [(top(s['rect']), s['id']) for s in surfaces.values() if s.get('oneWay') and
                       y <= top(s['rect']) <= previous and s['rect']['x'] <= x <= s['rect']['x'] + s['rect']['width']]
            if crossed:
                y, landed = max(crossed)
                if landed != surface['id']:
                    return
                break
            if y < -.1:
                return
        else:
            return
    return dict(heights=[top(s['rect']) for s in route], flightSeconds=round(ticks * dt, 2))


def physical_config(room):
    terrain = room['terrain']
    surfaces = {s['id']: s['rect'] for s in terrain if not s.get('travelX') and not s.get('travelY')}
    checkpoints = [f for f in room['floors'] if f.get('contactSave')]
    if len(checkpoints) != 1 or len({s['id'] for s in terrain}) != len(terrain):
        return False
    if any((s.get('travelX') or s.get('travelY')) and s.get('period',0) <= 0 for s in terrain):
        return False
    f = checkpoints[0]
    r = surfaces.get(f.get('surface'))
    if not r or not r['x'] <= f['x'] <= r['x'] + r['width']:
        return False
    return (0 <= room.get('eastBoundary', 46) - room['cameraBounds']['width'] <= 5 and
            room.get('hasFallLine') and room['fallLine'] < -7) and all(
        box['width'] > 0 and box['height'] > 0 and box['x'] >= -4 and
        box['x'] + box['width'] <= room.get('eastBoundary', 46) for box in (s['rect'] for s in terrain)) and all(
        k['surface'] in surfaces and contains(room['cameraBounds'], k['x'], top(surfaces[k['surface']]) + 1.2)
        for k in room['floors'] if k.get('surface'))


def placed(room):
    refs = [id for g in room['roster'] for id in g.get('placementIds', [])]
    points = room.get('placements', [])
    return (len(set(refs)) == len(refs) == len(points) and set(refs) == {p['id'] for p in points} and
            all(g['count'] == len(g.get('placementIds', [])) for g in room['roster']) and
            all(contains(p['active'], p['x'], p['y']) for p in points))


def shortcut(rooms):
    loom, gallery, arena, bridge = (rooms[IDS[i]] for i in (1, 2, 3, 5))
    def gate(src, dst, mark, floor, condition):
        f = next(f for f in src['floors'] if f['id'] == mark)
        s = next(s['rect'] for s in src['terrain'] if s['id'] == f['surface'])
        return any(d['room'] == dst['id'] and d.get('destinationFloor') == floor and
                   d['gate'] == condition and contains(d['approach'], d['x'], top(s)) and
                   s['x'] <= d['x'] <= s['x'] + s['width'] and not contains(d['approach'], d['x'], 0)
                   for d in src['exits'])
    landed = next(f for f in bridge['floors'] if f['id'] == 'velvet_upper_safe')
    direct = next(d['x'] for d in bridge['exits'] if d['room'] == arena['id']) - landed['x']
    long_way = next(d['x'] for d in gallery['exits'] if d['room'] == arena['id']) - next(
        f['x'] for f in gallery['floors'] if f['id'] == 'gallery_entry_balcony')
    return (landed.get('surface') == 'balcony' and 0 < direct < long_way and
            gate(loom, bridge, 'loom_balcony_floor', landed['id'], 'investigation_complete') and
            gate(bridge, arena, landed['id'], 'mikhail_arena_west', 'investigation_complete') and
            gate(arena, bridge, 'mikhail_arena_west', landed['id'], 'investigation_complete') and
            gate(bridge, loom, landed['id'], 'loom_balcony_floor', 'investigation_complete'))


def cast_position(x, facing, seam, direction, half_span, max_lead, stride):
    """Replay grounded A/D inputs until the bot stops to issue neutral Q."""
    for _ in range(80):
        lead = (seam - x) * direction
        if lead >= max_lead:
            facing = direction
        elif lead <= half_span or facing != direction:
            facing = -direction
        else:
            return x
        x += facing * stride
    return None


def silver_supply(room, skill, physics, layout, max_lead, max_ticks):
    deck = velvet_deck(room, layout, True)
    edges = fixed_edges(deck, physics)
    marks = [(run['x'] + i * run['stepX'], run['y'], 1 if run['stepX'] > 0 else -1)
             for run in room['seamRuns'] for i in range(run['count'])]
    short = room['singleSeams']
    if len(set(marks)) != len(marks) or len(marks) != room['stitchAnchors'] or short > len(marks):
        return
    first = room['floors'][0]
    if room['id'] == IDS[4] and not 0 < marks[0][0] - first['x'] <= skill['stages'][0]['reach']:
        return
    def floor(x, y):
        return next((f for f in edges if f.left <= x <= f.right and abs(f.top-y)<.005), None)
    for index, (center, y, direction) in enumerate(marks):
        long = index >= short
        profile = skill['stages'][int(long)]
        span = 2.2 if long else .9
        if span > profile['maxSpan'] or long and span <= skill['stages'][0]['maxSpan']:
            return
        bank = floor(center - 1.7 * direction, y-.24)
        if not bank:
            return
        if long:
            lip = floor(center + 1.85 * direction, y-.24)
            sewn = Face('installed', center-1.1, center+1.1, y-.07, y+.07, False)
            landing = simulate_landing([*deck,sewn], bank, sewn, center-1.7*direction, center, 0, physics)
            if not landing or landing[1]*physics[2] >= profile['lifetime']:
                return
            if (not lip or bank == lip or floor(center, y-.24) or not link(deck, bank, lip, physics) or
                floor(center-direction*1.1, y-.24) != bank or floor(center+direction*1.1, y-.24) != lip):
                return
        for ticks in range(1, max_ticks + 1):
            for facing in (-1, 1):
                stride = physics[2] * physics[3] * ticks
                for lead in (1.70, -.25, 2.70):
                    cast = cast_position(center-direction*lead, facing, center, direction, span/2, max_lead, stride)
                    if cast is None or floor(cast, y-.24) != bank:
                        return
                    if index:
                        px, py, _ = marks[index-1]
                        half = .45 if index-1 < short else 1.1
                        if -.35 <= y-.24-py <= .4 and abs(cast-px) <= half+.4:
                            return
                    distance = (cast-center)**2 + .24**2
                    if distance > profile['reach']**2:
                        return
                    for j,(other, height, _) in enumerate(marks):
                        if j == index or height-.15 > y-.24+2 or height+1.45 < y-.24: continue
                        half = .45 if j < short else 1.1
                        if (other-cast)*direction >= -half and (other-cast)*direction <= profile['reach']+half and (
                            (other-cast)**2+(height-y+.24)**2 <= distance):
                            return
    groups = [g for g in room['roster'] if g['id']=='notary_guard' and not g.get('skillOnly')]
    actors = {p['id']:p for p in room.get('placements',[])}
    guards = [actors[id] for g in groups for id in g['placementIds']]
    reach = skill['stages'][1]['reach']
    for p in guards:
        def isolated(direction):
            x, y = p['x'] - direction*1.1, p['y']
            return floor(x, y-.05) and contains(p['active'],x,y) and not any(
                other != p and contains(other['active'],x,y) and -.4 <= direction*(other['x']-x) <= reach+.4
                for other in guards)
        if not any(isolated(d) for d in (-1,1)): return
    setups = sum(g['count']*g['segments']*g.get('setupHits',1) for g in groups)
    return dict(short=short, supports=len(marks)-short, preparations=setups, total=len(marks)+setups)


def elite_pocket(room):
    # Skill joints and a parry statue share a lower encounter. The overhead shortcut is clear.
    groups = {p: g for g in room['roster'] for p in g['placementIds']}
    deck = top(next(s['rect'] for s in room['terrain'] if s['id'] == 'balcony'))
    zones = room['placements']
    if any(contains(p['active'], p['x'], deck) for p in zones):
        return False
    return any(groups[a['id']].get('skillOnly') and groups[a['id']].get('setupSkill') == 'silver_stitch'
               and groups[b['id']]['id'] == 'reflection_statue' and contains(a['active'], b['x'], b['y'])
               for a in zones for b in zones)


def validate_level_step2(check, project, scripts):
    res = project/'Assets/Resources'
    read = lambda path: json.loads((res/path).read_text())
    doc = read('Story/representative.json')
    rooms = {r['id']: r for r in doc['rooms']}
    audit = json.loads(Path(__file__).with_name('level-step2-extensions.json').read_text())
    src = (res/'Story/representative.json').read_text()
    previous = json.loads(reverse_exact(audit, prior_pass2_source('Assets/Resources/Story/representative.json', src)))
    before = {r['id']: r for r in previous['rooms']}
    skill = next(s for s in read('Skills/catalog.json')['skills'] if s['skillId'] == 'silver_stitch')
    grades, counts, traces = [], {}, {}
    def verify(value, name): check(bool(value), 'Level step 2: ' + name)
    def mutation(value, name):
        grades.append(bool(value))
        verify(value, 'mutation detected / ' + name)
    verify(previous != doc and previous['rows'] == doc['rows'] and before.keys() == rooms.keys() and
           all(before[k] == rooms[k] for k in rooms if k not in IDS) and all(
               {f['id'] for f in before[k]['floors']} <= {f['id'] for f in rooms[k]['floors']} for k in IDS),
           'exact step 1 predecessor, original hub, rows and all v4 floor IDs')
    motion = [number(scripts['PlayerController2D'], k) for k in ('MoveSpeed','JumpSpeed')]
    motion += [number(scripts['CombatMotor2D'], k) for k in ('AirAcceleration','AirBrake')]
    motion += [9.81 * number(scripts['CombatMotor2D'], 'Body.gravityScale'),
               float(re.search(r'Fixed Timestep: ([.\d]+)', (project/'ProjectSettings/TimeManager.asset').read_text())[1])]
    sets = {s['id']:s['layers'] for s in read('GameArt/backgrounds/room-sets.json')['sets']}
    verify(len({digest(sets[rooms[k]['backgroundSet']]) for k in IDS}) == 6, 'six distinct parallax compositions')
    for key in IDS:
        room = rooms[key]
        traces[key] = fixed_route(room, motion)
        verify(traces[key], key + ' / three plain jumps on actual fixed tops')
        verify(physical_config(room), key + ' / camera, fall line and one supported contact floor')
        verify(placed(room), key + ' / every roster actor owns a reachable activation zone')
        verify(all(not d.get('destinationFloor') or any(f['id'] == d['destinationFloor'] for f in rooms[d['room']]['floors'])
                   for d in room['exits']), key + ' / arrival floor ownership')
        counts[key] = [{'short': r['singleSeams'], 'long': r['stitchAnchors']-r['singleSeams'],
                        'releaseKnots': r['releaseKnots']} for r in [before[key], room]]
        bad = deepcopy(room)
        bad['terrain'][1]['rect']['y'] += 4
        mutation(not fixed_route(bad, motion), key + ' / broken fixed ascent')
        bad = deepcopy(room)
        next(f for f in bad['floors'] if f.get('contactSave'))['surface'] = 'missing'
        mutation(not physical_config(bad), key + ' / unsupported contact mark')
        if room['roster']:
            bad = deepcopy(room)
            bad['roster'][0]['placementIds'][0] = 'missing'
            mutation(not placed(bad), key + ' / missing placement')
    verify(shortcut(rooms), 'investigated upper doors in both directions with shorter intermediate walk')
    for i, idx, changes in [(1, 2, {'gate':''}), (3, 2, {'gate':''}), (5, 1, {'destinationFloor':'mikhail_arena_center'}),
                             (5, 1, {'approach':{'x':9,'y':-.3,'width':2,'height':1}})]:
        bad = deepcopy(rooms)
        bad[IDS[i]]['exits'][idx].update(changes)
        mutation(not shortcut(bad), IDS[i] + ' / shortcut ' + next(iter(changes)))
    formations = [set(g['id'] for g in rooms[k]['roster']) for k in IDS[1:4]]
    verify(len(set(map(frozenset, formations))) == 3 and rooms[IDS[3]]['boutHealth'] == 14 and not formations[2],
           'loom, gallery and solo bout retain three different first-visit encounters')
    verify(elite_pocket(rooms[IDS[5]]), 'optional guard and parry elite pocket below the upper return')
    bad = deepcopy(rooms[IDS[5]])
    bad['placements'][0]['active']['height'] += 5
    mutation(not elite_pocket(bad), 'lower ambush spills onto the upper bridge')
    room = rooms[IDS[4]]
    short = room['seamRuns'][0]
    second = short['x'] + short['stepX']
    traversal = scripts['VelvetTraversal']
    max_lead = number(traversal, 'maxCastLead')
    stride = motion[0] * motion[-1]
    # Run 7 ended inside the old Walk tolerance facing west. One D tick left that tolerance.
    stop_x = 6.9064002
    old_stance = second - 1.70
    verify(abs(stop_x - old_stance) <= .12 < stop_x + stride - old_stance,
           'run 7 / turn for seam_1 left the old Walk window in one physics tick')
    cast = cast_position(stop_x, -1, second, 1, .45, max_lead, stride)
    verify(cast is not None and 0 < second - cast < skill['stages'][0]['reach'],
           'run 7 / reverse facing at failure position converges to neutral Q on seam_1')
    timing = (project/'ProjectSettings/TimeManager.asset').read_text()
    max_ticks = round(float(re.search(r'Maximum Allowed Timestep: ([.\d]+)', timing)[1]) / motion[-1])
    physics = physics_from_project(project, scripts)
    layout = scripts['RepresentativeRoomLayout']
    def audit(r, limit=max_lead):
        return silver_supply(r, skill, physics, layout, limit, max_ticks)

    supply = {}
    for key in (IDS[4], *IDS[:4]):
        result = audit(rooms[key])
        verify(result is not None, key + ' / separate casts, supported stance, fixed crossing and finite setup opportunities')
        if result: supply[key] = result
    total = sum(row['total'] for row in supply.values())
    verify(total >= skill['stage3At'] * 1.15 and all(2 * row['total'] <= total for row in supply.values()),
           'pre-investigation supply exceeds 15 percent reserve and each room supplies at most half')
    training = [supply.get(key,{}) for key in (IDS[4], IDS[0])]
    verify(sum(r.get('short',0) for r in training) == 16 and sum(r.get('preparations',0) for r in training)==16 and
           sum(r.get('supports',0) for r in supply.values()) >= 48,
           'first 16 short seams and 16 setups unlock 48 separate support contacts')
    verify(all(any(d['room']==b and d['gate'] in ('','velvet_entry_complete','loom_bridge','gallery_bridge') for d in rooms[a]['exits'])
               for a,b in zip((IDS[4],*IDS[:3]),IDS[:4])) and
           all(d['gate']=='investigation_complete' for r in rooms.values() for d in r['exits'] if d['room']==IDS[5]),
           'return circuit follows earned doors and excludes the locked upper bridge')
    for label,key,mutate in (
        ('unreachable first cast',IDS[4],lambda r:r['seamRuns'][0].update(x=9)),
        ('two guards share an activation',IDS[4],lambda r:r['placements'].__setitem__(1,dict(r['placements'][0],id=r['placements'][1]['id']))),
        ('long anchor above the launch bank',IDS[1],lambda r:r['seamRuns'][0].update(y=24)),
        ('missing receiving bank',IDS[1],lambda r:r['terrain'].__setitem__(-1,dict(r['terrain'][-1],rect=dict(r['terrain'][-1]['rect'],y=32))))):
        bad = deepcopy(rooms[key])
        mutate(bad)
        mutation(not audit(bad),label)
    for limit in (1.11, 3.5):
        mutation(not audit(rooms[IDS[1]],limit), 'cast window misses fixed bank at lead '+str(limit))

    def walk_goals(source):
        approach = source.partition('if (seam.Installed)')[2].partition('keys.Cast();')[0]
        return re.findall(r'\bWalk\(([^)]+)\)', approach)
    goals = ['x, .15f', 'x, 0f']  # mount the installed seam, or approach the uninstalled one
    verify(walk_goals(traversal) == goals, 'seam approach has no extra waypoint before turning or casting')
    legacy_gate = 'if (!Walk(x - direction * 1.70f, .12f)) return true;'
    bad = traversal.replace('if (lead >= maxCastLead)', legacy_gate + '\n            if (lead >= maxCastLead)')
    mutation(walk_goals(bad) != goals, 'run 7 exact waypoint restored before the cast window')

    contracts = [
        ('RepresentativeRoomLayout','x = run.x + inRun * run.stepX;', 'y = run.y;', 'Place("seam_" + i, x, y)',
         'i < Room.singleSeams ? .9f : 2.2f'),
        ('StitchSurface2D','span > profile.maxSpan','PlayerOnFloor() && player.Grounded',
         'floor.IsTouching(player.GetComponent<Collider2D>())','TargetId + ":support"'),
        ('AutoTestDriver','if (++index == report.steps.Count) { BeginDistrict();','bot = new FirstDepartureBot(keyboard, Stop);',
         'if (now - lastProgress > 18)', 'if (district.Drive(bot, keyboard, now))'),
        ('VelvetDistrictProbe','bot.Session?.ActiveSlot == 2','area.StandingOn(mark.surface)',
         'Mathf.Abs(area.Rea.Body.position.x - mark.x) > .8f','scene_floor:velvet:', 'now - last',
         'inDistrict < result.targetMinSeconds','inDistrict > result.targetMaxSeconds','publish();'),
        ('VelvetDistrictProbe','upper.InReach(area.Rea.Body.position)','if (GameInput.InvestigateDown) closedInputFrame',
         'previousRoom != Loom || !investigated','previousRoom == Bridge','result.contacts.Count != 6',
         'result.firstSilverTarget != Waterway + ":seam_0"','uses.Add(e.useId)'),
        ('VelvetTraversal','keys.Cast()', 'if (seam.Installed)','Walk(x, .15f)','lastSewn == id + ":support"'),
        ('VelvetTraversal','int direction = run.stepX > 0f ? 1 : -1;',
         'float lead = (x - Player.Body.position.x) * direction;', 'if (lead >= maxCastLead)\n                Walk(x, 0f);',
         'else if (lead <= seam.Bounds.extents.x || Player.Facing != direction)',
         'keys.Hold(direction > 0 ? KeyCode.A : KeyCode.D);', 'else if (Player.Grounded)\n                keys.Cast();'),
        ('AutoTestRuntime','Report.velvetDistrict.run.status != "pass"')]
    for script, *tokens in contracts:
        source = scripts[script]
        verify(all(t in source for t in tokens), script + ' / ' + tokens[0])
        for t in tokens:
            mutation(all(x in source for x in tokens) and
                     not all(x in source.replace(t, '') for x in tokens), script + ' / ' + t)
    return dict(fixedRoutes=traces, seamCountsBeforeAfter=counts, firstVisit=supply,
                mutations=len(grades), detected=sum(grades), unity_executed=False)
