"""Independent fixed-tick oracle for layered probe navigation and the run-11 contact."""
from collections import deque
from dataclasses import dataclass, replace
import re

from validate_level_step1 import contains, number


@dataclass(frozen=True)
class Face:
    name: str
    left: float
    right: float
    bottom: float
    top: float
    one_way: bool = True
    wall: bool = False


def room_faces(room):
    result = []
    for s in room['terrain']:
        if s.get('rampRise') or s.get('travelX') or s.get('travelY'):
            continue
        r = s['rect']
        result.append(Face(s['id'], r['x'], r['x'] + r['width'], r['y'], round(r['y'] + r['height'], 5),
                           s.get('oneWay', False), s.get('wallJump', False)))
    wall_top = room['cameraBounds']['y'] + room['cameraBounds']['height'] + 4
    for name, x in (('west_wall', -4), ('east_wall', room['eastBoundary'])):
        result.append(Face(name, x - .2, x + .2, -1, wall_top, False, True))
    return result


def walk_limits(deck, radius):
    west = next((f.right + radius for f in deck if f.name == 'west_wall'), float('-inf'))
    east = next((f.left - radius for f in deck if f.name == 'east_wall'), float('inf'))
    return west, east


def steer(vx, dx, physics):
    _, _, dt, speed, accel, brake, _, _, tolerance = physics
    goal = 0 if abs(dx) <= tolerance else speed if dx > 0 else -speed
    rate = (accel if goal else brake) * dt
    return vx + max(-rate, min(rate, goal - vx))


def can_land(deck, start, end, launch, land, direction, physics):
    jump, gravity, dt, speed, _, _, radius, height, tolerance = physics
    x, y = launch + direction * radius, start.top
    vx, vy = direction * speed, 0 if direction else jump
    west, east = walk_limits(deck, radius)
    if not west <= launch <= east:
        return False
    while True:
        vx = steer(vx, land - x, physics)
        x += vx * dt
        if not west <= x <= east:
            return False
        prev = y
        vy = max(vy, -18) - gravity * dt
        y += vy * dt
        caught = None
        for f in deck:
            if x + radius <= f.left or x - radius >= f.right:
                continue
            if vy < 0 and y <= f.top <= prev:
                if caught is None or f.top > caught.top:
                    caught = f
            elif not f.one_way and y < f.top and y + height > f.bottom:
                return False
        if caught:
            return caught == end and end.left + tolerance < x < end.right - tolerance
        if vy <= 0 and y < end.top:
            return False


def link(deck, a, b, physics):
    inset = physics[6] + physics[8]
    if a == b or a.wall or b.wall or min(a.right - a.left, b.right - b.left) <= 2 * inset:
        return None
    west, east = walk_limits(deck, physics[6])
    land_left, land_right = max(west, b.left + inset), min(east, b.right - inset)
    if land_left >= land_right:
        return None
    lo, hi = max(a.left + inset, land_left), min(a.right - inset, land_right)
    if b.top > a.top and b.one_way and lo <= hi:
        for x in ((lo + hi) / 2, lo, hi):
            if can_land(deck, a, b, x, x, 0, physics):
                return (x, x, 0)
    for direction, lip in ((-1, a.left), (1, a.right)):
        land = max(land_left, min(land_right, lip + direction * (1 + inset)))
        if (land - lip) * direction <= inset:
            continue
        if b.top < a.top and can_land(deck, a, b, lip, land, direction, physics):
            return (lip, land, direction)
        launch = lip - direction * inset
        if can_land(deck, a, b, launch, land, 0, physics):
            return (launch, land, 0)
    return None


def connected_pairs(deck, physics):
    nodes = [f for f in deck if not f.wall]
    edges = {a: [b for b in nodes if link(deck, a, b, physics)] for a in nodes}
    pairs = 0
    for a in nodes:
        seen, todo = {a}, deque([a])
        while todo:
            for b in edges[todo.popleft()]:
                if b not in seen:
                    seen.add(b)
                    todo.append(b)
        pairs += len(seen) - 1
    return pairs, len(nodes) * (len(nodes) - 1)


def replay_sentry(start, goal, target, segments, physics, swing, step_profile, current, live_lip,
                  start_x=22.953977584838867, start_y=11.55999755859375):
    """Replay the saved resting contact. The old waypoint stays authored as live bounds vary."""
    _, gravity, dt, speed, _, _, radius, _, tolerance = physics
    duration, delay, grace, reach, width, height, aim = swing
    # The two upper/lower supports are the only surfaces intersecting this eastward descent.
    upper = replace(start, right=live_lip)
    edge = link([upper, goal], upper, goal, physics)
    assert edge and edge[2] == 1
    x, y, vx, vy = start_x, start_y, 0, 0
    old_waypoint = start.right + 1
    support, facing, attacks, resolved, hits = upper, 1, 0, 0, 0
    first_axis = None
    landing = active = first_hit = last_hit = swing_at = None
    for tick in range(round(20 / dt)):
        now = tick * dt
        if active is None and contains(target['active'], x, y):
            active = now
        if support == upper:
            dx = old_waypoint - x
            vx = speed if current else 0 if abs(dx) <= tolerance else speed if dx > 0 else -speed
            if first_axis is None:
                first_axis = int(vx / speed)
            x += vx * dt
            if x > live_lip + radius:
                support = None
        elif support is None:
            if abs(edge[1] - x) > tolerance:
                facing = 1 if edge[1] > x else -1
            vx = steer(vx, edge[1] - x, physics)
            x += vx * dt
            vy -= gravity * dt
            y += vy * dt
            if y <= goal.top + .05:
                if not goal.left < x < goal.right:
                    break
                y, vy, support, landing = goal.top + .05, 0, goal, now + dt
        else:
            dx = target['x'] - x
            attacking = swing_at is not None and now < swing_at + duration
            if attacking:
                if now < swing_at + step_profile['stepSeconds']:
                    x += facing * step_profile['stepSpeed'] * dt
            elif abs(dx) > aim or (abs(dx) > .06 and dx * facing <= 0):
                facing = 1 if dx > 0 else -1
                x += facing * speed * dt
            elif swing_at is None or now > swing_at + duration + grace:
                swing_at, attacks = now, attacks + 1
            if swing_at is not None and resolved < attacks and now + 1e-8 >= swing_at + delay:
                resolved += 1
                if (contains(target['active'], x, y) and abs(x + facing * reach - target['x']) <= width / 2 + .4
                        and abs(y + 1 - (target['y'] + .9)) <= height / 2 + .9):
                    hits += 1
                    last_hit = now
                    if first_hit is None:
                        first_hit = now
                    if hits == segments:
                        break
    def seconds(t):
        return round(t, 3) if t is not None else None
    return dict(liveLip=live_lip, firstAxis=first_axis, landingSeconds=seconds(landing),
                zoneSeconds=seconds(active), firstCutSeconds=seconds(first_hit), lastCutSeconds=seconds(last_hit),
                attacks=attacks, cuts=hits, remaining=segments-hits, endX=round(x, 4))


def validate_fixed_surface_route(verify, rooms, scripts, motion, sword):
    motor = scripts['CombatMotor2D']
    body = re.search(r'capsule.size = new Vector2\(([^,]+)f, ([^)]+)f\)', motor)
    graph = scripts.get('FixedSurfaceRoute', '')
    tolerance = number(scripts['VelvetTraversal'], 'tolerance')
    physics = (*motion, number(scripts['PlayerController2D'], 'MoveSpeed'), number(motor, 'AirAcceleration'),
               number(motor, 'AirBrake'), float(body[1]) / 2, float(body[2]), tolerance)
    traversal = scripts['ClockTraversal']
    current = ('if (step.WalkOff != 0)' in traversal and
               re.search(r'if \(step.WalkOff != 0\)\s*\{\s*keys.Hold\(step.WalkOff < 0 \? KeyCode.A : KeyCode.D\);',
                         re.sub(r'//[^\n]*', '', traversal)) is not None and
               'route.Next(here, goal, out step)' in traversal and 'here + Math.Sign(goal - here)' not in traversal)
    verify(current and 'MoveTo(step.LandX, FixedSurfaceRoute.AimTolerance)' in traversal and
           'ReadTerrain();' in traversal and 'plannedLayout == Area.ProbeLayout' in traversal,
           'run 11 / a held lip exit and airborne steering use the current room graph')
    verify(all(s in graph for s in ('getBounds(s)', 'TryLink(from, to, out var step)', 'if (LandsOn(from, step))',
                                    'landed == step.To', 'queue.Enqueue((link.To, first))', 'step.WalkOff * radius')) and
           'ProbeCollider(s).bounds' in scripts['PlaytestObservations'],
           'run 11 / fixed collider bounds, first landing and directed search select the next input')
    verify('if (!ReachedCombatSurface(face, at.x)) return;' in traversal and
           'if (jumpingFrom != null || !OnSurface(support))' in traversal and
           'ReachedCombatSurface(face, at.x)' in scripts['FirstDepartureBot'],
           'run 11 / district and common combat share the actual-support handoff')
    deck = room_faces(rooms[1])
    start = next(f for f in deck if f.name == 'pendulum_walk')
    group = next(g for g in rooms[1]['roster'] if g['id'] == 'shell_sentry')
    target = next(p for p in rooms[1]['placements'] if p['id'] == group['placementIds'][0])
    goal, = (f for f in deck if f.left <= target['x'] <= f.right and abs(target['y'] - f.top) < .5)
    verify(goal.name == 'cradle' and group['segments'] == 3 and not contains(target['active'], 22.953978, 11.559998),
           'run 11 / first sentry is three segments below the inactive upper-lip coordinate')
    spec = [float(v.replace('f', '')) for v in re.search(r'new AttackSpec\(([^)]+)\)', motor)[1].split(',')]
    aim = float(re.search(r'if \(range > ([.\d]+)f\) \{ Walk', scripts['FirstDepartureBot'])[1])
    swing = (spec[0], spec[1], number(motor, 'ComboGraceSeconds'), *spec[3:6], aim)
    profile = sword['stages'][-1]
    def replay(new, lip):
        return replay_sentry(start, goal, target, group['segments'], physics, swing, profile, new, lip)
    before = replay(False, 23)
    verify(before['landingSeconds'] is None and before['attacks'] == 0 and before['remaining'] == 3,
           'run 11 / old x=23 stop zone produces no descent or sword input in twenty seconds')
    after = [replay(current, lip) for lip in (23, 24.5, 26.8)]
    for run in after:
        verify(run['zoneSeconds'] is not None and run['landingSeconds'] is not None and run['remaining'] == 0 and
               run['landingSeconds'] < run['firstCutSeconds'] <= run['lastCutSeconds'] < 18,
               f"run 11 / land on cradle and cut all three active segments at test lip {run['liveLip']}")
    authored = replay_sentry(start, goal, target, group['segments'], physics, swing, profile, True, start.right,
                             start_x=(goal.left + start.right) / 2, start_y=start.top + .05)
    verify(authored['remaining'] == 0 and authored['firstAxis'] == 1,
           'run 11 / held ordinary walk from the authored overlap reaches the sentry without a jump')
    pairs = {}
    for room in rooms:
        reached, total = connected_pairs(room_faces(room), physics)
        pairs[room['id']] = [reached, total]
        verify(reached == total, room['id'] + ' / directed paths between every ordered pair of fixed faces')
    a, b, c = Face('base', 0, 15, -.4, 0, False), Face('middle', 7, 12, 1.45, 1.8), Face('top', 10, 16, 3.25, 3.6)
    split = Face('separate', 16, 20, .85, 1.2)
    verify(connected_pairs([c, a, b], physics) == (6, 6) and link([c, a, b], a, c, physics) is None,
           'unordered unrelated face names / two ordinary jumps with an unreachable direct rise')
    verify(link([a, split], a, split, physics) is not None and
           link([a, replace(split, left=24, right=28)], a, replace(split, left=24, right=28), physics) is None,
           'horizontal gap / accept the short air-steered jump and reject the long one')
    wall = Face('wall', 15.1, 15.8, 0, 6, False, True)
    verify(link([a, wall, split], a, split, physics) is None,
           'horizontal gap / a solid wall obstructs the otherwise reachable jump')
    low = Face('lower', 0, 20, -.35, 0, False)
    shelf = Face('shelf', 0, 20, 1.25, 1.6)
    upper = Face('upper', 4, 16, 2.85, 3.2)
    verify(link([low, upper], upper, low, physics) and link([low, shelf, upper], upper, low, physics) is None,
           'descent / an intervening shelf prevents a direct edge to the lower floor')
    # The run-11 image gives a visual width. Exercise that width as a separate sensitivity
    # case: the frame contains no measured Collider2D.bounds for the two return rooms.
    widened = {}
    for room in rooms[6:8]:
        def expand(f):
            dx, dy = .3 * (f.right - f.left), .3 * (f.top - f.bottom)
            return replace(f, left=f.left - dx, right=f.right + dx, bottom=f.bottom - dy, top=f.top + dy)
        deck = [expand(f) for f in room_faces(room)]
        widened[room['id']] = connected_pairs(deck, physics)
        mark = room['stationMarks'][0]
        top = next(f for f in deck if f.name == mark['surface'])
        west, east = walk_limits(deck, physics[6])
        verify(top.left < west < east < top.right and not any(link(deck, top, lower, physics)
               for lower in deck if lower.top < top.top), room['id'] + ' / no walk-off behind solid end walls at 1.6x width')
    return dict(before=before, after=after, authored=authored, orderedPairs=pairs, widerBoundsPairs=widened)
