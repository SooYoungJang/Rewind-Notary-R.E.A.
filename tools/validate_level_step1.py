"""Level pass 1. Data integrity, 20 ms feet trajectories and live-probe contracts."""
from copy import deepcopy
from functools import lru_cache
import hashlib
import json
import math
import re

from pathlib import Path
from story_link_baseline import prior_level4_source, prior_pass2_source, reverse_exact
from PIL import Image

PILOTS = ('velvet_02_loom', 'velvet_03_gallery')
HUB = {'hub_attic', 'hub_workroom', 'hub_forge', 'hub_lift', 'hub_sanctuary'}
DOCUMENTS = ('representative', 'clock', 'opera', 'bell', 'gallery', 'finale')


def digest(value):
    data = json.dumps(value, ensure_ascii=False, sort_keys=True, separators=(',', ':'))
    return hashlib.sha256(data.encode()).hexdigest()


def contains(r, x, y):
    return r['x'] <= x <= r['x'] + r['width'] and r['y'] <= y <= r['y'] + r['height']


def number(source, name):
    m = re.search(r'\b' + name + r' = ([.\d]+)f', source)
    return float(m[1]) if m else 0


def traverse(room, movement, wall):
    """Reference flight. Integrate gravity before position, check downward tread crossings.
    Start and finish on foot. Return None for a blocked, unreachable or missing foothold.
    Unity capsule/effector collision and keyboard frame ordering are measured by PilotProbe.
    """
    terrain = {s['id']: s for s in room['terrain']}
    speed, jump, kick_x, kick_y, slide, lock_time, accel, brake, gravity, dt = movement
    if min(movement) <= 0:
        return None
    ground = 0.
    post = terrain['kick_post']
    face = post['rect']['x'] - .31
    takeoff = terrain['tread_1']['rect']['x'] - 2
    left = takeoff if room['id'] == PILOTS[0] else next(p['x']+.8 for p in room['placements'] if p['id']=='sword_latch_1')
    goal = next(f['x'] for f in room['floors'] if f.get('surface') == 'balcony')
    x, y, vx, vy, ticks = left, ground, 0., 0., 0
    landed, kicks, slides = [], 0, 0
    tops = [(s['id'], s['rect']) for s in terrain.values() if s.get('oneWay') and
            not s.get('travelX') and not s.get('travelY')]
    def flight(target, expected, against_wall=False):
        nonlocal x, y, vx, vy, ticks, kicks, slides
        locked_until = 0
        for _ in range(160):
            ticks += 1
            old_y = y
            if against_wall and vy < 0 and not kicks:
                if abs(x - face) > .02 or y + 1. < post['rect']['y'] or y + .9 > post['rect']['y'] + post['rect']['height']:
                    return False
                vy = max(vy, min(0., -slide + gravity * dt))
                slides += 1
                if slides >= 3:
                    vx, vy = -kick_x, kick_y
                    kicks += 1
                    locked_until = ticks + math.ceil(lock_time / dt)
            desired = speed if against_wall and not kicks else (math.copysign(speed, target-x) if abs(target-x) > .19 else 0)
            if ticks < locked_until:
                desired = -kick_x
            vx += max(-(accel if desired else brake)*dt, min((accel if desired else brake)*dt, desired-vx))
            x = min(face, x + vx*dt)
            vy -= gravity*dt
            y += vy*dt
            if vy >= 0:
                continue
            crossed = [(r['y']+r['height'], name) for name, r in tops if
                       y <= r['y'] + r['height'] <= old_y and r['x'] <= x <= r['x']+r['width']]
            if crossed:
                y, name = max(crossed)
                landed.append(name)
                return name == expected
            if y < ground:
                return False
        return False

    if wall:
        if not post.get('wallJump') or post.get('oneWay') or post.get('travelX') or post.get('travelY'):
            return None
        ticks += math.ceil((face-x)/(speed*dt))
        x, vy = face, jump
        if not flight(goal, 'balcony', True):
            return None
    else:
        ticks += math.ceil(abs(takeoff - x)/(speed*dt))
        x = takeoff
        for name in ('tread_1', 'tread_2', 'balcony'):
            r = terrain[name]['rect']
            vx, vy = 0., jump
            if not flight(r['x'] + min(1.4, r['width']/2), name):
                return None
    deck = terrain['balcony']['rect']
    if not deck['x'] <= goal <= deck['x']+deck['width']:
        return None
    ticks += math.ceil(abs(goal-x)/(speed*dt))
    return dict(start_x=round(left, 3), finish_x=goal, finish_y=round(y, 3), landings=landed, kicks=kicks,
                slide_ticks=slides, movement_seconds=round(ticks*dt, 3))


def room_checks(room, rooms):
    surfaces = {s['id']: s for s in room['terrain']}
    floors = {f['id']: f for f in room['floors']}
    placements = {p['id']: p for p in room['placements']}
    refs = [key for g in room['roster'] for key in g['placementIds']]
    exits = room['exits']
    floor_links = all(e['room'] in rooms and (not e.get('destinationFloor') or
        e['destinationFloor'] in {f['id'] for f in rooms[e['room']]['floors']}) for e in exits)
    gates = [e for e in exits if e['x'] == 37]
    balcony = surfaces['balcony']['rect']
    balcony_top = balcony['y'] + balcony['height']
    camera = room['cameraBounds']
    arrivals = {
        PILOTS[0]: {'velvet_01_entry': None, PILOTS[1]: 'gallery_entry_balcony', 'velvet_06_shortcut': 'velvet_upper_safe'},
        PILOTS[1]: {PILOTS[0]: 'loom_balcony_floor', 'velvet_04_arena': 'mikhail_arena_west', 'velvet_05_return': 'velvet_return_start'},
    }
    return {
        'positive and unique surfaces': len(surfaces) == len(room['terrain']) and all(
            min(s['rect']['width'], s['rect']['height']) > 0 for s in surfaces.values()),
        'recoverable fixed floors': all(not f.get('surface') or (f['surface'] in surfaces and
            not surfaces[f['surface']].get('travelX') and not surfaces[f['surface']].get('travelY') and
            surfaces[f['surface']]['rect']['x'] <= f['x'] <= surfaces[f['surface']]['rect']['x'] +
            surfaces[f['surface']]['rect']['width']) for f in floors.values()),
        'destination floors': floor_links and {e['room']: e.get('destinationFloor') for e in exits} == arrivals[room['id']],
        'separate doors at the same x': len(gates) == 2 and
            [[e['room'] for e in gates if contains(e['approach'], 37, y)] for y in (0, sum(surfaces['balcony']['rect'][k] for k in ('y','height')))] ==
            ([['velvet_03_gallery'], ['velvet_06_shortcut']] if room['id'] == PILOTS[0]
             else [['velvet_05_return'], ['velvet_04_arena']]),
        'camera follows both floors and fall below map': room.get('hasFallLine') and room['fallLine'] < -7 and
            camera['y'] <= 1.5 and contains(camera, 37, balcony_top + 2.5),
        'each roster member has a distinct active placement': all(len(g['placementIds']) == g['count'] for g in room['roster'])
            and len(set(refs)) == len(refs) == len(placements) and set(refs) == set(placements) and
            all(contains(p['active'], p['x'], p['y']) and p['active']['height'] > 0 for p in placements.values()),
        'one marked midpoint on the balcony': [f['id'] for f in floors.values() if f.get('contactSave')] ==
            (['loom_balcony_floor'] if room['id'] == PILOTS[0] else ['gallery_balcony_floor']) and
            all(f.get('surface') == 'balcony' for f in floors.values() if f.get('contactSave')),
    }


def art_checks(layers, texture):
    def good_uv(uv):
        return all(0 <= uv[axis] < 1 and 0 < uv[size] and uv[axis]+uv[size] <= 1.000001
                   for axis, size in [('x', 'width'), ('y', 'height')])
    return len(layers) == 3 and len({l['factor'] for l in layers}) == 3 and all(
        not l['resource'].startswith('stage1-') and good_uv(l['crop']) and l['height'] > 0 and
        re.fullmatch(r'#[a-fA-F0-9]{6}', l['tint']) and int(l['tint'][1:], 16) != 0 and
        l['spacing'] >= 1 and l['vertical'] > 0 and texture(l['resource'], tuple(l['crop'][k] for k in ('x','y','width','height')), i > 0)
        for i, l in enumerate(layers))


# Live code contracts. Each mutation below must remove an observable prerequisite from production code.
WIRES = {
    'inclusive feet bounds on both axes': ('RepresentativeScript', 'feet.x >= x && feet.x <= x + width &&',
        'feet.y >= y && feet.y <= y + height'),
    'optional door geometry': ('RepresentativeScript', 'approach != null && approach.Defined', 'approach.Contains(feet)', 'Mathf.Abs(feet.x - x) <= 1.25f'),
    'reachable before nearest': ('RepresentativeRoomLayout', 'Gate(e) && e.InReach(player.Body.position)'),
    'destination arrival': ('RepresentativeArea', 'Travel(Departure(exit.room), exit.destinationFloor)', 'destination.Floor(floorId)?.id ?? destination.floors[0].id'),
    'vertical spawn': ('RepresentativeRoomLayout', 'Room.Surface(Room.Floor(floor)?.surface)', 'authored.rect.y + authored.rect.height + .05f'),
    'camera and old clamp': ('CameraTarget2D', 'Area?.Room.cameraBounds', 'bounds.y + bounds.height', 'Mathf.Clamp(Player.position.y, 1.5f, 5.5f)'),
    'room fall limit': ('GameBootstrap', 'player.transform.position.y < (Area?.Room.FallY ?? -7f)'),
    'default fall limit': ('RepresentativeScript', 'hasFallLine ? fallLine : -7f'),
    'room driven parallax': ('ParallaxBackground', 'area?.Room.backgroundSet != current', 'LoadRoom(area?.Room.backgroundSet)', 'target.transform.position.y * layer.vertical'),
    'optional roster points': ('RepresentativeRoomLayout', 'Room.Placement(group.placementIds[i])', 'point == null ? x : point.x', 'point == null ? .05f : point.y'),
    'activation by feet': ('StoryTerrain', 'entry.zone.Contains(player.Body.position)', 'entry.actor.SetActive(active)'),
    'physical fixed top contact': ('StoryTerrain', 'box.attachedRigidbody != null', 'box.bounds.max.y) <= .12f && box.IsTouching(actor)'),
    'fixed checkpoint persistence': ('StoryTerrain', 'Distance(floor.x) > .8f || !StandingOn(floor.surface)', 'if (Has(FloorPrefix + floor.id))', 'Checkpoint(Room, floor.id);\n                session.Save();'),
    'authored fixed sides': ('StoryTerrain', 'surface.wallJump && !surface.Moving && !surface.oneWay'),
    'forge receipt and face contact': ('PlayerController2D', '!motor.Skills.Progression.Record.storyFlags.Contains("intro_complete")',
        'actorCollider.GetContacts(wallContacts)', 'other.attachedRigidbody != null',
        'Mathf.Abs(hit.normal.x) < .9f', '!surface.Kickable', 'if (axis * side > .25f) return side'),
    'Q and defense own the frame': ('PlayerController2D', 'motor.Defense.BlocksActions || motor.BenefactorSkills.BlocksActions) return 0', 'if (!castDown && !pendingCast.HasValue && GameInput.JumpDown)', 'if (castDown)\n            {\n                jumpPressed = float.NegativeInfinity;'),
    'slide and kick': ('PlayerController2D', 'side != lastKickSide && Time.time - jumpPressed <= JumpBuffer', 'motor.Jump(WallKickY)', 'kickDirection = -side', '-WallSlideSpeed - gravityStep'),
    'ground top excludes wall refunds': ('CombatMotor2D', 'needsLandingContact || floor.TryGetComponent<AuthoredSurface>',
        'Mathf.Abs(Body.position.y - floor.bounds.max.y) > .09f', '!floor.IsTouching(capsule))) continue;'),
    'ground and wall route witnesses': ('PilotProbe', 'area.StandingOn(id)', 'route.fixedKicks = controller.WallJumps - kicksAtStart', 'route.wallKicks = controller.WallJumps - kicksAtStart', 'wallKicks == 1'),
    'spent charge until touchdown': ('PilotProbe', 'observeCharge && !player.Grounded && !defense.AirDashUsed', 'if (!area.StandingOn("balcony"))', 'defense.Phase == DefensePhase.ParryWindow'),
    'physical slot readback and retry': ('PilotProbe', 'var saved = store.Load()', 'store.LoadStatus == SaveLoadStatus.Loaded', 'area.ContactSaveCount <= contactBefore', 'keys.Tap(KeyCode.R)', 'area.StandingOn("arrival_shelf")'),
    'same physical endpoints and actual slide limit': ('PilotProbe', 'Mathf.Abs(fixedStartX - wallStartX) <= .4f',
        'Mathf.Abs(fixedFinishX - finishX) <= .4f', 'Mathf.Abs(fixedTopY - topY) <= .12f',
        'slideSpeed >= -PlayerController2D.WallSlideSpeed - .05f'),
    'lower floor input and upper arrival receipt': ('PilotProbe', 'if (GameInput.InvestigateDown) phase = Phase.VerifyLowerDoor',
        'area.OnReturnFloor && feet < .15f', 'arrivalOnShelf && lowerDoorRejected',
        'bot.Record.storyFlags.Contains("scene_floor:velvet:gallery_entry_balcony")'),
    'input priority witnesses': ('PilotProbe', '!GameInput.CastDown || !GameInput.JumpDown',
        '!GameInput.ParryDown || !GameInput.JumpDown', 'controller.WallJumps == kicksAtStart && !player.Grounded'),
    'no cast or growth while traversing wall path': ('PilotProbe', 'growthDelta == 0 && activations == 0',
        'route.growthDelta = Uses(bot.Record) - growthBefore', 'route.activations++'),
    'record physical contact time and deduplicated write': ('PilotProbe', 'area.LastContactTime - entry - result.comparisonSeconds',
        'result.contactWrites = area.ContactSaveCount - contactBefore', 'contactWrites == 1'),
    'persist pilot measurements at route checkpoints': ('AutoTestDriver', 'new PilotProbe(CapturePilot)',
        'Capture(kind);\n            report.Write(AutoTestRuntime.DirectoryPath)', 'pilot.ObserveUse()'),
    'pilot included in serialized result': ('AutoTestReport', 'public PilotResults pilot = new PilotResults()',
        'Path.Combine(directory, "report.json")', 'JsonUtility.ToJson(this, true)'),
    'route retry invalidates partial traversal': ('AutoTestDriver', 'pilot.ObserveRetry(report.pilot)',
        'if (!string.IsNullOrEmpty(report.pilot.failure)) { Stop(report.pilot.failure); return; }'),
    'failure closes stage and run': ('AutoTestDriver', 'PlaytestStep.Velvet && !report.pilot.Passed', 'now - lastProgress > 18'),
    'failure closes exit': ('AutoTestRuntime', 'completed && !Report.pilot.Passed) Report.Error'),
}


def validate_level_step1(check, project, scripts):
    resources = project/'Assets/Resources'
    docs = [json.loads((resources/'Story'/(name+'.json')).read_text()) for name in DOCUMENTS]
    rooms = {r['id']:r for doc in docs for r in doc['rooms']}
    probes = {}
    mutations = []
    def verify(ok, label):
        check(bool(ok), 'Level step 1: ' + label)
    def mutation(ok, label):
        mutations.append(bool(ok))
        verify(ok, 'mutation detected / ' + label)

    verify(len(rooms) == 56 and sum(len(d['rows']) for d in docs) == 321 and
           digest([d['rows'] for d in docs]) == 'b86303ec3a3ec31e51e41ea1d5399b750fc206086203beb775c74f42c4637203',
           '56 rooms, 321 original authored rows including conditions and rewards')
    clock_patch = json.loads(Path(__file__).with_name('level-step3-extensions.json').read_text())['Assets/Resources/Story/clock.json']
    original_clock = json.loads(reverse_exact(clock_patch, (resources/'Story/clock.json').read_text()))
    opera_path = 'Assets/Resources/Story/opera.json'
    original_opera = json.loads(prior_level4_source(opera_path, (project/opera_path).read_text()))
    bell_path = 'Assets/Resources/Story/bell.json'
    original_bell = json.loads(prior_level4_source(bell_path, (project/bell_path).read_text()))
    gallery_path = 'Assets/Resources/Story/gallery.json'
    original_gallery = json.loads(prior_level4_source(gallery_path, (project/gallery_path).read_text()))
    verify(digest([original_clock, original_opera, original_bell, original_gallery, docs[5]]) == '20aaae8ffd385c67a53e7cd0a55a4b7619ef15f8a46abc5fb238402ce92367ee',
           'all four other districts and finale match the original data')
    patch = json.loads(Path(__file__).with_name('level-step2-extensions.json').read_text())
    old = json.loads(reverse_exact(patch, prior_pass2_source('Assets/Resources/Story/representative.json', (resources/'Story/representative.json').read_text())))
    for r in old['rooms']:
        if r['id'] in HUB | set(PILOTS): r.pop('backgroundSet', None)
        if r['id'] == 'hub_forge': r.pop('terrain', None)
        if r['id'] == 'velvet_06_shortcut': r['exits'][0].pop('destinationFloor', None)
        if r['id'] not in PILOTS: continue
        for k in ('cameraBounds','fallLine','hasFallLine','placements','terrain'): r.pop(k, None)
        for e in r['exits']:
            e.pop('destinationFloor', None)
            e.pop('approach', None)
        r['exits'][2]['x'] = 32
        r['floors'] = [f for f in r['floors'] if not f.get('surface')]
        for g in r['roster']: g.pop('placementIds', None)
    verify(digest(old) == '1c3a1dcc95660141c580d752385bdcfe0d5a90827c4871788c01c3ec261493d7',
           'legacy floors, story flags, roster and doors preserved outside pilot edits')

    player, motor = scripts['PlayerController2D'], scripts['CombatMotor2D']
    dt = float(re.search(r'Fixed Timestep: ([\d.]+)', (project/'ProjectSettings/TimeManager.asset').read_text())[1])
    gravity = number(motor, 'Body.gravityScale') * 9.81
    movement = [number(player, k) for k in ('MoveSpeed','JumpSpeed','WallKickX','WallKickY','WallSlideSpeed','KickControlTime')]
    movement.extend([number(motor,'AirAcceleration'), number(motor,'AirBrake'), gravity, dt])
    for key in PILOTS:
        room = rooms[key]
        for label, passed in room_checks(room, rooms).items(): verify(passed, key+' / '+label)
        for wall in (False, True):
            trace = traverse(room, movement, wall)
            label = key + (' / wall' if wall else ' / fixed')
            probes[label] = trace
            verify(trace and trace['kicks'] == int(wall), label + ' reaches the common fixed balcony')
        for label, change in (
                ('unreachable second tread', lambda r: r['terrain'][1]['rect'].update(y=8)),
                ('moving kick post', lambda r: r['terrain'][3].update(travelX=1)),
                ('unmarked kick post', lambda r: r['terrain'][3].update(wallJump=False))):
            altered = deepcopy(room)
            change(altered)
            mutation(traverse(altered, movement, label != 'unreachable second tread') is None, key+' / '+label)
        for label, change in (
                ('destination floors', lambda r: r['exits'][1].update(destinationFloor='wrong')),
                ('destination floors', lambda r: r['exits'][1].pop('destinationFloor')),
                ('separate doors at the same x', lambda r: r['exits'][2].update(approach=deepcopy(r['exits'][1]['approach']))),
                ('camera follows both floors and fall below map', lambda r: r['cameraBounds'].update(y=4)),
                ('camera follows both floors and fall below map', lambda r: r.update(hasFallLine=False)),
                ('each roster member has a distinct active placement', lambda r: r['placements'][0]['active'].update(y=15)),
                ('recoverable fixed floors', lambda r: r['terrain'][2].update(travelY=1)),
                ('one marked midpoint on the balcony', lambda r: r['floors'][2].update(contactSave=not r['floors'][2].get('contactSave')))):
            altered = deepcopy(room)
            change(altered)
            mutation(not room_checks(altered, rooms)[label], key+' / '+label)
        altered = deepcopy(room)
        door = next(e for e in altered['exits'] if e['room'] in PILOTS)
        door['destinationFloor'] = rooms[door['room']]['floors'][0]['id']
        mutation(not room_checks(altered, rooms)['destination floors'], key+' / existing floor on wrong level')

    verify(any(s.get('wallJump') for s in rooms['hub_forge']['terrain']) and
           [r['completionFlag'] for r in docs[0]['rows'] if r['sceneId']=='intro_forge'][-1]=='intro_complete',
           'forge exit teaching wall and existing final forge receipt')
    verify(rooms[PILOTS[0]]['exits'][2]['gate']=='investigation_complete' and
           any(e['room']=='velvet_04_arena' for e in rooms['velvet_06_shortcut']['exits']) and
           rooms['velvet_06_shortcut']['exits'][0]['destinationFloor']=='loom_balcony_floor',
           'M1 upper two-way loom arrival and onward arena link')
    verify(not re.search(r'BeginUse|ConsumeCounter|\.Land\(|TryDash|TryStartParry|gravityScale\s*=', player),
           'wall controller leaves growth, defense charges and gravity ownership to existing systems')
    for label, (name, *required) in WIRES.items():
        source = scripts[name]
        matches = lambda s: all(token in s for token in required)
        verify(matches(source), label)
        mutation(matches(source) and not matches(source.replace(required[0], 'false')), label)
    art = resources/'GameArt/backgrounds'
    @lru_cache(maxsize=None)
    def texture(name, uv, transparent):
        p = art/(name+'.png')
        if not p.is_file(): return False
        with Image.open(p) as image:
            x,y,w,h = uv
            piece = image.crop((round(x*image.width), round((1-y-h)*image.height),
                                round((x+w)*image.width), round((1-y)*image.height)))
            colors = piece.convert('RGB').resize((64,32)).getcolors(2048)
            return len(colors)>64 and (not transparent or piece.convert('RGBA').getchannel('A').getextrema()[0]<8)
    sets = json.loads((art/'room-sets.json').read_text())['sets']
    lookup = {s['id']:s['layers'] for s in sets}
    ids = [rooms[k].get('backgroundSet') for k in sorted(HUB|set(PILOTS))]
    verify(len(set(ids))==7 and all(i in lookup for i in ids) and len({digest(lookup[i]) for i in ids})==7,
           'seven distinct authored three-layer sets')
    for key,layers in lookup.items():
        verify(art_checks(layers,texture), key+' / textured far plus two transparent depth strips')
        bad=deepcopy(layers)
        bad[1]['resource']='stage1-mid'
        mutation(not art_checks(bad,texture),key+' / old strip substituted')
        bad = deepcopy(layers)
        bad[0]['tint'] = '#000000'
        mutation(not art_checks(bad,texture),key+' / black far layer')
    return dict(routes=probes, mutations=len(mutations), detected=sum(mutations), unity_executed=False)
