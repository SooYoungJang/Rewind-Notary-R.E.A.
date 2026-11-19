"""Run 16, after the reunion. Replay ordinary keys, the raised lift and the finite wave on a fixed tick."""
import math
import re

from validate_fixed_surface_route import Face, fixed_edges, link, paths_from, room_faces, steer
from validate_level_step1 import contains, number
from validate_story_bout import Actor


def support_at(edges, x, y):
    return min((f for f in edges if f.left <= x <= f.right and abs(f.top - y) < .5),
               key=lambda f: abs(f.top - y))


def replay(room, physics, lift, waltz, sword, swing, guard, aim, wave_first, hop_at_contact,
           route_on_lift=False, cleared=0):
    jump, gravity, dt, speed, _, _, radius, height, tolerance = physics
    deck = room_faces(room)
    edges = fixed_edges(deck, physics)
    # The kinematic lift is absent from the fixed graph but present in physical contacts.
    solids = [*deck, lift]
    actor = Actor(16.99, .01, 6, floor=support_at(edges, 16.99, .01))
    door = next(d for d in room['exits'] if d['room'] == 'hub_lift')
    door_face = support_at(edges, door['x'], door['approach']['y'] + door['approach']['height'] / 2)
    placements = {p['id']: p for p in room['placements']}
    wave = [(placements[key], group) for group in room['roster'] for key in group['placementIds']][cleared:]
    landings, cuts, casts = [], [], []
    hits = segments = swings = 0
    step = first_launch = None
    sword_pending = False
    hold_until = spin_end = swing_start = strike_at = None
    next_cast = next_enemy = 0
    for tick in range(round(20 / dt)):
        now = tick * dt
        point, group = wave[len(cuts)]
        goal = support_at(edges, point['x'], point['y']) if wave_first else door_face
        dest_x = point['x'] if wave_first else door['x']
        active = contains(point['active'], actor.x, actor.y)
        dx = dest_x - actor.x
        axis = 0
        takeoff = False
        if spin_end is not None:
            if now >= spin_end:
                if actor.floor:
                    spin_end = None
                else:
                    actor.vy = -waltz['plungeSpeed']
        elif swing_start is not None:
            # One contact query at the first swing's windup, with its actual forward step and bounds.
            age = now - swing_start
            if age < sword['stepSeconds']:
                axis = actor.face * sword['stepSpeed'] / speed
            if age >= swing[1] and sword_pending:
                sword_pending = False
                blade_x = actor.x + actor.face * swing[3]
                if (active and abs(blade_x - point['x']) <= swing[4] / 2 + .4 and
                        abs(actor.y + 1 - point['y'] - .9) <= swing[5] / 2 + .9 and hits == group['setupHits']):
                    segments += 1
                    hits = 0
                    if segments == group['segments']:
                        cuts.append(dict(target=point['id'], surface=goal.name, setups=casts.count(point['id']),
                                         seconds=round(now, 2)))
                        segments = 0
                        strike_at = None
                        if len(cuts) == len(wave):
                            break
            if age >= swing[0]:
                swing_start = None
        elif actor.floor == goal and wave_first:
            step = None
            if abs(dx) > aim or (abs(dx) > .06 and dx * actor.face <= 0):
                axis = math.copysign(1, dx)
            elif hits < group['setupHits']:
                if now >= next_cast:
                    next_cast = now + waltz['cooldown']
                    spin_end = now + waltz['duration']
                    actor.vy = waltz['jumpSpeed']
                    if (active and group['setupSkill'] == 'curtain_call_waltz' and
                            abs(dx) <= waltz['reach'] + .4 and abs(actor.y + 1 - point['y'] - .9) <= waltz['reach'] + .9):
                        hits += 1
                        casts.append(point['id'])
                        strike_at, next_enemy = None, now + guard['attackInterval']
            else:
                swings += 1
                swing_start = now
                sword_pending = True
        elif actor.floor == lift:
            toward = step[0] if route_on_lift and step else dest_x
            axis = 1 if toward >= (lift.left + lift.right) / 2 else -1
        elif actor.floor:
            if actor.floor == goal:
                if abs(dx) < tolerance:
                    break  # the old no-row branch would use the door
                waypoint = dest_x
            else:
                parents = paths_from(edges, actor.floor)
                if goal not in parents:
                    break
                child = goal
                while parents[child] != actor.floor:
                    child = parents[child]
                step = link(deck, actor.floor, child, physics)
                waypoint, _, walk_off = step
                if first_launch is None:
                    first_launch = waypoint
                if walk_off:
                    raise ValueError('Expected an ascending stair in the run-16 replay')
            if abs(waypoint - actor.x) > tolerance:
                axis = math.copysign(1, waypoint - actor.x)
                gap = lift.left - actor.x if axis > 0 else actor.x - lift.right
                low_ray_hits = 0 < gap <= .95 and lift.bottom <= actor.y + .18 <= lift.top
                side_contact = (abs(gap - radius) < 1e-5 and actor.y < lift.top <= actor.y + height)
                if low_ray_hits or (hop_at_contact and side_contact):
                    takeoff, hold_until = True, now + .24
            elif step and abs(actor.vx) < .2:
                takeoff, hold_until = True, None  # full Space until the fixed surface catches Rea
        elif step:
            axis = 0 if abs(step[1] - actor.x) <= tolerance else math.copysign(1, step[1] - actor.x)

        if takeoff:
            actor.vy = jump
        if hold_until is not None and now >= hold_until:
            actor.vy *= .5 if actor.vy > 0 else 1
            hold_until = None
        if axis:
            actor.face = 1 if axis > 0 else -1
        if spin_end is not None:
            actor.vx = 0
        elif actor.floor and not takeoff:
            actor.vx = axis * speed
        else:
            actor.vx = steer(actor.vx, axis, physics)
        previous_floor, old_x = actor.floor, actor.x
        actor.advance(solids, radius, gravity, dt)
        if axis and actor.x == old_x:
            actor.vx = 0
        if actor.floor and actor.floor != previous_floor and (not landings or landings[-1]['surface'] != actor.floor.name):
            landings.append(dict(surface=actor.floor.name, x=round(actor.x, 2), top=actor.floor.top,
                                 seconds=round(now + dt, 2)))

        # A changed posture cancels the telegraph. Stop if an unmodelled defensive input is required.
        threatened = (active and abs(actor.x - point['x']) <= guard['attackReach'] + radius and
                      abs(actor.y + height / 2 - point['y'] - 1) <= 1.1 + height / 2)
        if not active:
            strike_at = None
        elif hits < group['setupHits'] and threatened and now >= next_enemy and strike_at is None:
            strike_at = now + guard['telegraphSeconds']
        if strike_at is not None and threatened and strike_at - now <= .085:
            break
    return dict(objective=wave[0][0]['id'] if wave_first else door['room'], firstLaunchX=first_launch,
                seconds=round(now if len(cuts) == len(wave) else now + dt, 2),
                endFeet=[round(actor.x, 2), round(actor.y, 2)], landings=landings,
                setups=len(casts), swordInputs=swings, cuts=cuts, remaining=len(wave) - len(cuts))


def validate_opera_wave(verify, room, physics, scripts, waltz, sword, guard):
    probe = scripts['OperaDistrictProbe']
    drive = probe.split('internal bool Drive(')[1]
    # Require the live dispatch at method scope, before any no-row door. Moving it back under
    # the action check must also switch the numeric replay back to the old door objective.
    dispatch = 'var target = timingTrip ? null : area.DistrictWaveTarget;'
    before = drive.split(dispatch)[0]
    wave_first = (dispatch in drive and before.count('{') - before.count('}') == 1 and
                  'if (room == Stairs)' not in before and
                  'if (target != null) { bot.OperaFight(target); return false; }' in drive and
                  'bool timingTrip = action == null && (longStart > 0 && result.longSeconds == 0 || dropStart > 0 && !result.dropLanding);' in drive)
    bot = scripts['FirstDepartureBot']
    contact = all(s in bot for s in ('|| TouchingStepAhead(facing)', 'actor.GetContacts(walkContacts)',
                                      'Mathf.Abs(hit.normal.x) >= .9f', '(hit.point.x - body.center.x) * facing > 0',
                                      'other.bounds.max.y <= body.max.y'))
    navigation = ' '.join(scripts['OperaTraversal'].split())
    verify(wave_first and 'roomTargets.Find(t => !t.State.Cleared)' in scripts['PlaytestObservations'] and
           all(s in navigation for s in ('if (!TryFaceAt(at.x, at.y, out var face)) return;',
                                         'if (!OnSurface(face)) { OperaPosition(face.id, at.x); return; } ClearStep();',
                                         'return ClockPosition(face, x);',
                                         'if (Aim(at.x) && Player.Grounded) keys.Cast(0, 1);')),
           'run 16 / row-independent wave dispatch routes to the target support before setup or sword')
    verify(contact and '|| TouchingStepAhead(facing))) { jumpUntil = Time.time + .24f; keys.Hold(KeyCode.Space);' in
           ' '.join(bot.split()), 'run 16 / a grounded side contact ahead can trigger the ordinary obstacle jump')
    layout = scripts['RepresentativeRoomLayout']
    plank = layout.split('fixtures[p.id] = Block(p.id, p.x,')[1].split('plank);')[0]
    center_y, width, thickness = map(float, re.findall(r'plank \? ([.\d]+)f', plank))
    mark, = (p for p in room['points'] if p.get('platform'))
    base = next(f.top for f in room_faces(room) if f.name == mark['surface'])
    bottom = base + center_y + number(scripts['DistrictRoomLayout'], 'LiftHeight') - thickness / 2
    lift = Face(mark['id'], mark['x'] - width / 2, mark['x'] + width / 2, bottom, bottom + thickness, False)
    aim = float(re.search(r'if \(range > ([.\d]+)f\) \{ Walk', bot)[1])
    swing = [float(v.strip().rstrip('f')) for v in re.search(r'new AttackSpec\(([^)]+)\)', scripts['CombatMotor2D'])[1].split(',')]
    follow_step = ('toward = jumpingFrom == null && step.WalkOff == 0 ? step.LaunchX : step.LandX;' in scripts['OperaTraversal']
                   and 'MoveTo(toward < lift.center.x' in scripts['OperaTraversal'])
    def run(dispatch_wave, hop, follow=False, cleared=0):
        return replay(room, physics, lift, waltz, sword, swing, guard, aim, dispatch_wave, hop, follow, cleared)
    old, targeting_only, current = run(False, False), run(True, False), run(wave_first, contact, follow_step)
    for label, result in [('old door objective', old), ('target dispatch alone', targeting_only)]:
        verify(result['seconds'] == 20 and result['endFeet'][0] == 16.99 and not result['landings'] and
               not result['setups'] and not result['swordInputs'] and result['remaining'] == 3,
               'run 16 / ' + label + ' stays against the raised deck for twenty seconds')
    expected = [p for g in room['roster'] for p in g['placementIds']]
    verify([c['target'] for c in current['cuts']] == expected and current['remaining'] == 0 and
           all(c['setups'] == 3 for c in current['cuts']) and current['setups'] == 9 and
           current['swordInputs'] == 3 and current['seconds'] < 20,
           'run 16 / nine separate waltz setups and three sword cuts clear the active roster in order')
    verify(all(any(l['surface'] == c['surface'] and l['seconds'] < c['seconds'] for l in current['landings'])
               for c in current['cuts']) and len({c['surface'] for c in current['cuts']}) == 3 and
           any(l['surface'] == lift.name for l in current['landings']),
           'run 16 / ordinary jumps cross the raised lift and land on all three guards’ fixed supports')
    last_only = run(wave_first, contact, follow_step, cleared=2)
    last_guard = next(p for p in room['placements'] if p['id'] == expected[-1])
    verify(current['firstLaunchX'] == old['firstLaunchX'] == targeting_only['firstLaunchX'] and
           last_only['firstLaunchX'] > lift.right and last_guard['x'] < lift.left and
           [c['target'] for c in last_only['cuts']] == expected[-1:],
           'run 16 / dismount toward the graph launch even with only the guard across the lift remaining')
    return dict(startFeet=[16.99, .01], liftBounds=[lift.left, lift.right, lift.bottom, lift.top],
                before=old, dispatchOnly=targeting_only, after=current, lastGuardOnly=last_only)
