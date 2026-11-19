"""Replay the first live latch on each run-21 return visit, using ordinary movement and sword inputs."""
from dataclasses import replace
import json
import re
from pathlib import Path

from validate_fixed_surface_route import fixed_edges, link, paths_from, room_faces, steer
from validate_level_step1 import contains
from validate_story_bout import Actor


def sword_parameters(project, scripts, valid_uses):
    motor = scripts['CombatMotor2D']
    swing = [float(s.strip().rstrip('f')) for s in re.search(r'new AttackSpec\(([^)]+)\)', motor)[1].split(',')]
    aim = float(re.search(r'if \(range > ([.\d]+)f\) \{ Walk', scripts['FirstDepartureBot'])[1])
    size = re.search(r'public Bounds Bounds => .*?new Vector3\(([.\d]+)f, ([.\d]+)f, 2\)', scripts['WaveTarget2D'])
    catalog = json.loads((project / 'Assets/Resources/Skills/catalog.json').read_text())
    skill = next(s for s in catalog['skills'] if s['skillId'] == 'basic_sword')
    initial = int(re.search(r'InitialSwordUses = (\d+)', scripts['SkillDefinition'])[1])
    uses = initial + valid_uses
    profile = next(p for p in skill['stages'] if p['stage'] == 1 + (uses >= skill['stage2At']) + (uses >= skill['stage3At']))
    return aim, swing, profile, float(size[1]), float(size[2])


def replay(deck, edges, physics, start, placement, segments, door, fight, sword, manual=False,
           recovery_ticks=0, landings=None):
    """Stop on first sword contact or inspect input. Recompute support on each physics tick."""
    jump, gravity, dt, speed, _, _, radius, _, tolerance = physics
    aim_range, swing, profile, target_width, target_height = sword
    if manual:
        aim_range = swing[3]  # Stand one blade reach away for the independent human-input control.
    source = next(f for f in edges if f.name == start['support'])
    actor = Actor(start['x'], start['y'], start.get('hp', 6), floor=source)
    if fight:
        goal_x = placement['x']
        goal = next((f for f in edges if f.left <= goal_x <= f.right and abs(f.top - placement['y']) < .5), None)
    else:
        goal_x = door['x']
        goal = next((f for f in edges if f.left <= goal_x <= f.right and contains(door['approach'], goal_x, f.top)), None)
    parents = paths_from(edges, source)
    path = []
    dest = goal
    while dest != source:
        if dest not in parents:
            break
        src = parents[dest]
        path.append((src, dest, link(deck, src, dest, physics)))
        dest = src
    path.reverse()
    hops = 0
    landed = [source.name]
    release_jump = False
    swing_tick = cut_tick = door_tick = None
    direction = 1
    # A cut can hand off mid-swing, after the grounded forward step has ended.
    for _ in range(recovery_ticks):
        actor.advance(deck, radius, gravity, dt)
    for tick in range(recovery_ticks, round(120 / dt)):
        axis = 0
        if release_jump:
            release_jump = False  # ClockPosition clears jumpingFrom on the first grounded frame.
        elif hops < len(path):
            src, dest, (launch, land, walk_off) = path[hops]
            if actor.floor == src:
                if walk_off:
                    axis = walk_off
                elif abs(actor.x - launch) > tolerance:
                    axis = 1 if launch > actor.x else -1
                elif abs(actor.vx) < .2:
                    actor.vy, actor.floor = jump, None
                    axis = 0 if abs(land - actor.x) <= tolerance else (1 if land > actor.x else -1)
            elif actor.floor is None:
                axis = 0 if abs(land - actor.x) <= tolerance else (1 if land > actor.x else -1)
            else:
                break  # The wrong tread caught the capsule.
        elif actor.floor == goal:
            dx = goal_x - actor.x
            if not fight:
                if abs(dx) <= .24 and contains(door['approach'], actor.x, actor.y):
                    door_tick = tick
                    break
                axis = 1 if dx > 0 else -1
            elif swing_tick is not None:
                age = (tick - swing_tick) * dt
                if age >= swing[1]:
                    blade_x = actor.x + direction * swing[3]
                    if (contains(placement['active'], actor.x, actor.y)
                            and abs(blade_x - placement['x']) <= (swing[4] + target_width) / 2
                            and abs(actor.y + 1 - placement['y'] - target_height / 2) <= (swing[5] + target_height) / 2):
                        segments -= 1
                        cut_tick = tick
                    break
                if age < profile['stepSeconds']:
                    axis = direction * profile['stepSpeed'] / speed
            elif abs(dx) > aim_range or (abs(dx) > .06 and dx * direction <= 0):
                axis = 1 if dx > 0 else -1
            else:
                swing_tick = tick
                axis = direction * profile['stepSpeed'] / speed if profile['stepSeconds'] > 0 else 0
        else:
            break
        if axis:
            direction = 1 if axis > 0 else -1
        actor.vx = axis * speed if actor.floor else steer(actor.vx, axis, physics)
        actor.vy = max(actor.vy, -18)
        actor.advance(deck, radius, gravity, dt)
        if hops < len(path) and actor.floor == path[hops][1]:
            landed.append(actor.floor.name)
            if landings is not None:
                landings.append(dict(surface=actor.floor.name, tick=tick+1, x=round(actor.x, 4), y=round(actor.y, 4)))
            release_jump = path[hops][2][2] == 0
            hops += 1
    return dict(supports=landed, swordTick=swing_tick, cutTick=cut_tick, inspectTick=door_tick,
                remaining=segments, feet=[round(actor.x, 3), round(actor.y, 3)])


def validate_bell_reentry(verify, project, rooms, scripts, physics):
    qa = json.loads(Path(__file__).with_name('run21-bell-reentry.json').read_text())
    probe = ' '.join(re.sub(r'//[^\n]*', '', scripts['BellDistrictProbe']).split())
    # Both the initial ascent (Route) and every timed/repositioning leg (Trip) issue F through Door.
    door_body = probe.split('bool Door(')[1].split('private bool Compare(')[0]
    before_approach = door_body.split('!bot.BellDoor(door)')[0]
    guard = re.search(r'if \(room.stage == "(\w+)" && surveyed.Contains\((\w+)\) && '
                      r'area.DistrictWaveTarget is WaveTarget2D (\w+)\) \{ bot.BellFight\(\3\); return false; \}',
                      before_approach)
    compare = probe.split('private bool Compare(')[1].split('internal bool Drive(')[0]
    verify('surveyed.Add(room)' in probe and 'Door(bot, keys, Trip[leg + 1]);' in probe and
           'Door(bot, keys, Route[i + 1]);' in probe and 'Door(bot, keys, Seam);' in probe and
           'if (investigated && silver && action == null && Compare(bot, keys, now)) return false;' in probe and
           'keys.Tap(KeyCode.F);' in door_body, 'run 21 / return and both comparison routes use the inspected door')
    depart = compare.find('Door(bot, keys, Trip[leg + 1])')
    verify('if (leg == 3 && result.longSeconds == 0 || leg == 7)' in compare and
           0 <= compare.find('result.longSeconds = now - longStart') < depart and
           0 <= compare.find('result.shortcutSeconds = now - shortcutStart') < depart,
           'run 21 / record the common destination floor before engaging the next room departure')
    verify('roomTargets.Find(t => !t.State.Cleared)' in scripts['PlaytestObservations'] and
           'else if (ReachedCombatSurface(face, at.x)) FightTarget(target);' in scripts['BellTraversal'] and
           'if (target.State.Ready(Time.time)) keys.Tap(KeyCode.J);' in scripts['FirstDepartureBot'],
           'run 21 / live wave state hands off to a real aimed sword input on its support')

    names = dict(re.findall(r'(\w+) = "(bell_\w+)"', probe))
    def itinerary(name):
        tokens = re.search(r'\b' + name + r' = \{ ([^}]+) \}', probe)[1].split(', ')
        return [names[t] for t in tokens]
    route, trip = itinerary('Route'), itinerary('Trip')
    visits = route[:route.index(trip[0])] + trip + [names['Seam']]
    verify(visits[:len(qa['roomLoads'])] == qa['roomLoads'] and trip[:4] == qa['longRooms'] and
           trip[6:] == [names['Landing'], names['Tunnels']] and visits[-1] == names['Seam'],
           'run 21 / observed hub re-entry, ordinary descent, climb back and shortcut to 09')
    idle_limit = float(re.search(r'DistrictIdleLimit = (\d+)', scripts['AutoTestDriver'])[1])
    last_progress = [s['elapsed'] - s['idle'] for s in qa['samples']]
    verify(max(last_progress) - min(last_progress) < .2 and qa['samples'][-1]['idle'] >= idle_limit == 120 and
           'contacts > lastContacts || (room == lastWaveRoom && cleared > lastCleared)' in scripts['AutoTestDriver'],
           'run 21 / rooms changed while the last contact, kill or valid use stayed at 384 seconds')

    archetypes = json.loads((project / 'Assets/Resources/Waves/test-stage.json').read_text())['archetypes']
    latch = next(g for g in archetypes if g['id'] == 'sword_latch')
    sword = sword_parameters(project, scripts, qa['validSwordUses'])
    by_id = {r['id']: r for r in rooms}
    results = {}
    for room_id, measured in qa['bounds'].items():
        room = by_id[room_id]
        authored = room_faces(room)
        if set(measured) != {f.name for f in authored if not f.wall}:
            verify(False, room_id + ' / missing run-21 collider or authored terrain')
            continue
        actual = []
        for f in authored:
            if f.wall:
                actual.append(f)
            else:
                x, y, w, h = measured[f.name]
                actual.append(replace(f, left=x, right=x+w, bottom=y, top=y+h))
        verify(all(
            max(abs(getattr(a, field) - getattr(b, field)) for field in ('left', 'right', 'top', 'bottom')) <= .005
            for a, b in zip(authored, actual)), room_id + ' / all run-21 collider edges match authored rectangles')
        samples = [s for s in qa['samples'] if s['room'] == room_id]
        verify(all(any(f.name == s['support'] and f.left <= s['x'] <= f.right and abs(f.top - s['y']) <= .12
                       for f in actual) for s in samples), room_id + ' / recorded feet agree with measured support')
        live = next(g for g in room['roster'] if g['id'] == 'sword_latch')
        target = next(p for p in room['placements'] if p['id'] == live['placementIds'][0])
        verify(latch['mechanism'] and latch['gate'] == 'device' and live == room['roster'][0] and
               live['segments'] == 1 and not live.get('setupSkill') and not live.get('skillOnly') and
               all(s['target'] == room_id + ':sword_latch_0' and s['segments'] == live['segments'] and
                   s['remainingTargets'] == sum(g['count'] for g in room['roster'])
                   for s in samples), room_id + ' / reloaded first live segment')
        tests = []
        for visit, here in enumerate(visits):
            if here != room_id:
                continue
            previous = by_id[visits[visit-1]]
            entry = next(d['destinationFloor'] for d in previous['exits'] if d['room'] == here)
            mark = next(m for m in room['floors'] if m['id'] == entry)
            support = next(f for f in actual if f.name == mark['surface'])
            starts = [dict(support=support.name, x=mark['x'], y=support.top, elapsed=None)]
            starts.extend(s for s in samples if s['visit'] == visit)
            door = next(d for d in room['exits'] if d['room'] == visits[visit+1])
            for start in starts:
                tests.append((visit, door, start))
        room_results = []
        edges = fixed_edges(actual, physics)
        authored_edges = fixed_edges(authored, physics)
        for visit, door, start in tests:
            label = f"{room_id} / return visit {visit} from {start['support']} at {start['x']:.2f}"
            # On these return visits the story row is empty and the logged target still has one segment.
            surveyed = set(qa['contactRooms'] + visits[:visit+1])
            fight = guard is not None and room['stage'] == guard[1] and names.get(guard[2]) in surveyed
            args = physics, start, target, live['segments'], door
            legacy = replay(actual, edges, *args, False, sword)
            human = replay(authored, authored_edges, *args, True, sword, manual=True)
            current = replay(actual, edges, *args, fight, sword)
            verify(legacy['inspectTick'] is not None and legacy['swordTick'] is None and legacy['remaining'] == 1,
                   label + ' uses F with the latch intact under the run-21 rule')
            verify(human['remaining'] == 0 and human['cutTick'] is not None,
                   label + ' reachable on authored terrain with an ordinary sword strike')
            verify(current['remaining'] == 0 and current['inspectTick'] is None and current['swordTick'] is not None
                   and current['cutTick'] is not None and current['cutTick'] > current['swordTick'],
                   label + ' cuts the live segment before the bot can inspect the exit')
            if start.get('idle', idle_limit) < idle_limit:
                verify(current['cutTick'] is not None and start['idle'] + current['cutTick'] * physics[2] < idle_limit,
                       label + ' cuts within the time remaining on the run-21 watchdog')
            room_results.append(dict(visit=visit, destination=door['room'], qaElapsed=start['elapsed'],
                                     start=[start['x'], start['y']], legacy=legacy, human=human, current=current))
        # A sword input without an active target cannot earn a single segment in this replay.
        _, door, start = tests[0]
        inactive = dict(target, active=dict(target['active'], x=room['eastBoundary'] + 1))
        miss = replay(actual, edges, physics, start, inactive, live['segments'], door, True, sword)
        verify(miss['swordTick'] is not None and miss['cutTick'] is None and miss['remaining'] == 1,
               room_id + ' / missing the activation zone leaves the live segment intact')
        results[room_id] = room_results
    return dict(tickSeconds=physics[2], visits=visits, rooms=results)
