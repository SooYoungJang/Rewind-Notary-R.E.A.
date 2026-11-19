"""Run 25: two cleared latches in Update, a trigger until FixedUpdate, then an ordinary approach."""
from dataclasses import replace
import json
import re

from validate_bell_reentry import replay, sword_parameters
from validate_fixed_surface_route import fixed_edges, link, paths_from, room_faces, steer
from validate_level_step1 import contains
from validate_opera_wave import support_at
from validate_story_bout import Actor


def probe_polls(ticks, cuts, required, reachable, synchronized, jammed=False):
    """One entry per bot Update. Repeated tick indices have no intervening FixedUpdate."""
    solid = False
    waiting_since = None
    polls = []
    for tick in ticks:
        if tick > 0 and cuts >= required and not jammed:
            solid = True
        if synchronized and cuts >= required and not solid:
            if waiting_since is None:
                waiting_since = tick
            if tick == waiting_since:
                polls.append('wait')
                continue
        polls.append('route' if reachable[solid] else 'no fixed route')
        break
    return polls


def validate_gallery_bridge(verify, project, room, scripts, physics):
    qa = json.loads((project / 'tools/run25-gallery-bridge.json').read_text())
    state = qa['endState']
    jump, gravity, dt, speed, _, _, radius, _, tolerance = physics
    measured = {w['surface']: w for w in qa['widths']}
    authored = room_faces(room)
    deck = []
    for face in authored:
        if face.wall:
            deck.append(face)
        else:
            w = measured[face.name]
            deck.append(replace(face, left=w['left'], right=w['left'] + w['measured'],
                                bottom=w['bottom'], top=w['bottom'] + w['height']))
    verify(dt == .02 and {f.name for f in authored if not f.wall} == measured.keys() and
           all(max(abs(getattr(a, k) - getattr(b, k)) for k in ('left', 'right', 'bottom', 'top')) <= .005
               for a, b in zip(authored, deck)), 'run 25 / five live room boxes match authored geometry')

    edges = fixed_edges(deck, physics)
    foot = support_at(edges, state['x'], state['y'])
    target_id = state['target'].split(':')[1]
    target = next(p for p in room['placements'] if p['id'] == target_id)
    goal = support_at(edges, target['x'], target['y'])
    start = dict(x=state['x'], y=state['y'], hp=state['hp'], support=foot.name)
    remaining = int(re.search(r'remainingTargets=(\d+)', qa['reason'])[1])
    roster = [key for group in room['roster'] for key in group['placementIds']]
    cleared = roster[:len(roster) - remaining]
    span = next(s for s in room['terrain'] if s.get('latchCuts'))
    latch_ids = next(g['placementIds'] for g in room['roster'] if g['id'] == 'sword_latch')
    cuts = len(set(cleared) & set(latch_ids))
    verify(state['room'] == room['id'] and state['grounded'] and abs(foot.top-state['y']) < .12 and
           state['hp'] == 5 and state['action'] == 'clear:wave' and (foot.name, goal.name) == ('crossbar', 'glass_sill') and
           target_id == roster[len(cleared)] == 'shell_sentry_2' and remaining == 4 and state['targetSegments'] == 3 and
           cuts == span['latchCuts'] == 2 and not contains(target['active'], start['x'], start['y']),
           'run 25 / grounded crossbar, both latches cut, same three-segment sentry on glass_sill')

    closed = [f for f in deck if f.name != span['id']]
    closed_edges = fixed_edges(closed, physics)
    before = paths_from(closed_edges, foot)
    after = paths_from(edges, foot)
    canvas = next(f for f in edges if f.name == span['id'])
    verify(goal not in before and len(before) == 3 and after.get(goal) == canvas and after.get(canvas) == foot and
           not link(deck, foot, goal, physics), 'run 25 / only the open canvas connects crossbar to the east sill')

    # The direct jump sample used by TryLink, without the canvas. Integrate toward the sill until
    # the descending capsule crosses its top, retaining the x of the missed landing.
    launch = foot.right - radius - tolerance
    actor = Actor(launch, foot.top, state['hp'], vy=jump)
    for flight_tick in range(1, round(2 / dt)):
        actor.vx = steer(actor.vx, goal.left + radius + tolerance - actor.x, physics)
        actor.advance(closed, radius, gravity, dt)
        if actor.vy < 0 and actor.y <= goal.top:
            break
    verify(actor.y <= goal.top and actor.x + radius < goal.left and actor.floor is None,
           'run 25 / ordinary direct lip jump misses the first possible sill contact')

    # Tie the static replay to the production clocks, collider filter and bounded bot decision.
    def code(name):
        return ' '.join(re.sub(r'//[^\n]*', '', scripts[name]).split())

    observations = code('PlaytestObservations')
    plates = code('BellWaves')
    traversal = code('ClockTraversal')
    verify('DeliverStrike();' in code('CombatMotor2D').split('private void Update()')[1] and
           'target.Cut(use)' in code('WaveCombatTargets') and 'UpdateBellPlates();' in
           code('DistrictRoomLayout').split('private void FixedUpdate()')[1].split('private void')[0] and
           'cuts >= p.face.RequiredCuts' in plates and 'p.box.isTrigger = !open;' in plates and
           'if (p.face.RequiredCuts > 0) BellPlateRevision++;' in plates and
           'faces.RemoveAll(s => s.RequiredCuts > 0 && ProbeCollider(s).isTrigger);' in observations and
           'plannedPlateRevision == Area.BellPlateRevision) return;' in traversal and
           'terrain = Area.ProbeTerrain(); route = null;' in traversal and
           'roomTargets.Find(t => !t.State.Cleared)' in observations,
           'run 25 / render-frame cut and fixed-tick collider revision feed distinct live snapshots')
    fight = code('GalleryTraversal').split('void GalleryFight(WaveTarget2D target)')[1]
    synchronized = ('if (plate.face.latchCuts <= 0 || !plate.box.isTrigger) continue;' in observations and
                    'foreach (var latch in latches) if (latch.State.Cleared) cuts++;' in observations and
                    'if (cuts >= plate.face.latchCuts || Has("gallery_03_cleared")) return true;' in observations and
                    fight.startswith(' { if (Area.GalleryBridgeOpening()) { pendingBridgeTick ??= Time.fixedTimeAsDouble; '
                                     'if (pendingBridgeTick == Time.fixedTimeAsDouble) return; } else pendingBridgeTick = null;') and
                    'if (!onStroke && !ReachedCombatSurface(face, at.x)) return;' in fight)

    reachable = {False: goal in before, True: goal in after}
    # Three render observations in the same physics interval exercise hit-stop without assuming a frame rate.
    ticks = [0, 0, 0, 1, 2]
    required = span['latchCuts']
    legacy = probe_polls(ticks, cuts, required, reachable, False)
    current = probe_polls(ticks, cuts, required, reachable, synchronized)
    jammed = probe_polls(ticks, cuts, required, reachable, synchronized, jammed=True)
    verify(legacy == ['no fixed route'] and current == ['wait'] * 3 + ['route'],
           'run 25 / same target survives repeated render polls and routes on the next solid physics tick')
    verify(probe_polls([1], cuts, required, reachable, synchronized) == ['route'] and
           probe_polls(ticks, cuts-1, required, reachable, synchronized) == ['no fixed route'] and
           jammed == ['wait'] * 3 + ['no fixed route'],
           'run 25 / solid on first poll, missing latch and stuck trigger keep distinct outcomes')

    # With one latch intact, the next wave target is still on the western side.
    last_latch = next(p for p in room['placements'] if p['id'] == latch_ids[-1])
    latch_floor = support_at(closed_edges, last_latch['x'], last_latch['y'])
    verify(latch_floor in before and probe_polls(ticks, cuts-1, required, {False: latch_floor in before},
                                                synchronized) == ['route'],
           'run 25 / the last uncleared latch can still be approached on the closed layout')
    broken_deck = [replace(f, bottom=f.bottom+12, top=f.top+12) if f == canvas else f for f in deck]
    broken = goal in paths_from(fixed_edges(broken_deck, physics), foot)
    verify(not broken and probe_polls(ticks, cuts, required, {False: False, True: broken}, synchronized)[-1]
           == 'no fixed route', 'run 25 / opened collider twelve units too high still records a disconnected route')

    sword_uses = next(s['validUses'] for s in qa['skills'] if s['id'] == 'basic_sword')
    sword = sword_parameters(project, scripts, sword_uses)
    _, swing, profile, _, _ = sword
    # The capture omits attack age. Take the whole remaining recovery of the first basic swing.
    recovery_ticks = round((swing[0] - swing[1]) / dt)
    landings = []
    approach = None
    if current[-1] == 'route':
        approach = replay(deck, edges, physics, start, target, state['targetSegments'], None, True, sword,
                          recovery_ticks=recovery_ticks, landings=landings)
    verify(approach and profile['stepSpeed'] == 0 and approach['supports'] == [foot.name, canvas.name, goal.name] and
           contains(target['active'], *approach['feet']) and approach['remaining'] == state['targetSegments']-1 and
           approach['cutTick'] is not None and landings[-1]['tick'] <= approach['swordTick'] < approach['cutTick'] and
           approach['cutTick'] * dt < 18, 'run 25 / continuous keyboard approach from saved feet lands and hits the sentry')
    return dict(tickSeconds=dt, start=start, target=target_id, targetFeet=[target['x'], target['y']], cuts=cuts,
                closedReachable=[f.name for f in before], openReachable=[f.name for f in after],
                directJump=dict(launchX=launch, ticks=flight_tick, endX=round(actor.x, 4),
                                shortBy=round(goal.left-actor.x-radius, 4)),
                pollTicks=ticks, legacy=legacy, current=current, stuckTrigger=jammed, recoveryTicks=recovery_ticks,
                landings=landings, approach=approach)
