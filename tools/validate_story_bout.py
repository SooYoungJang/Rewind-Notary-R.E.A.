"""Run-14 story bout. Discrete keyboard/feet model, no Unity contact solver or save writes."""
from dataclasses import dataclass
import json
import math
import re

from validate_fixed_surface_route import Face, fixed_edges, link, paths_from, room_faces
from validate_level_step1 import number


def toward(value, goal, step):
    return value + max(-step, min(step, goal - value))


def return_door(room, inbound, door, physics, hop):
    jump, gravity, dt, speed, _, _, radius, _, _ = physics
    floor = Face('floor', -4, room['eastBoundary'], -2, 0, False)
    x = next(p['x'] for p in room['points'] if p['id'] == 'mikhail_reunion_plank')
    plank = Face('reunion plank', x - 1.7, x + 1.7, 0, .32, False)
    player = Actor(inbound['x'], 0, 6, floor=floor)
    jumped = False
    for tick in range(round(10 / dt)):
        dx = door['x'] - player.x
        if abs(dx) <= .35 and player.floor:
            return dict(reached=True, x=round(player.x, 3), seconds=round(tick * dt, 2))
        player.vx = speed if dx > .35 else 0
        if hop and player.floor == floor and 0 < plank.left - player.x <= .95:
            player.vy = jump
            jumped = True
        elif jumped:
            player.vy *= .5  # Single Space edge on the fallback Walk approach.
            jumped = False
        player.advance([floor, plank], radius, gravity, dt)
    return dict(reached=False, x=round(player.x, 3))


@dataclass
class Actor:
    x: float
    y: float
    hp: int
    vx: float = 0
    vy: float = 0
    face: int = 1
    floor: Face = None
    stun: float = 0
    immune: float = 0

    def advance(self, deck, radius, gravity, dt):
        # Horizontal side of a solid step, then first downward crossing (including one-way cloth).
        x = self.x + self.vx * dt
        for f in deck:
            if f.one_way or self.y >= f.top - 1e-6 or self.y + 1.9 <= f.bottom:
                continue
            if self.x + radius <= f.left and x + radius >= f.left:
                x = f.left - radius
            elif self.x - radius >= f.right and x - radius <= f.right:
                x = f.right + radius
        self.x = x
        self.vy -= gravity * dt
        y = self.y + self.vy * dt
        self.floor = max((f for f in deck if self.x + radius > f.left and self.x - radius < f.right
                          and y <= f.top <= self.y + 1e-6 and self.vy <= 0),
                         key=lambda f: f.top, default=None)
        if self.floor:
            y, self.vy = self.floor.top, 0
        self.y = y

    def damage(self, amount, source_x, now, immunity, minimum=0):
        if now < self.immune:
            return False
        self.hp = max(minimum, self.hp - amount)
        self.vx = 5 if self.x >= source_x else -5
        self.vy, self.stun, self.immune = 4, now + .18, now + immunity
        return True


def replay(room, rows, trial, profile, swings, physics, policy, player_hp, start):
    jump, gravity, dt, speed, accel, brake, radius, height, tolerance = physics
    floor = Face('floor', -4, room['eastBoundary'], -2, 0, False)
    terrain = [floor, *room_faces(room)]
    balcony = next(f for f in terrain if f.name == room['floors'][0]['surface'])
    latch_id = next(t.split(':')[1] for r in rows for t in r['requiredPlayerAction'] if t.startswith('strike:'))
    latch = next(p['x'] for p in room['points'] if p['id'] == latch_id)
    plank_x = next(p['x'] for p in room['points'] if p['id'] == next(
        r['resumePoint'] for r in rows if 'strike:' + latch_id in r['requiredPlayerAction']))
    plank = Face('retreat plank', plank_x - 1.7, plank_x + 1.7, 0, .32, False)
    # The short case starts after a legal first swing at the latch, including the stage-2 lunge.
    final_only = start == 'plank'
    player = Actor(latch - 1 + profile['stepSpeed'] * profile['stepSeconds'] if final_only else room['floors'][0]['x'],
                   floor.top if final_only else balcony.top, player_hp, floor=floor if final_only else balcony)
    phase = 'half' if start == 'investigated' else 'win'
    deck = terrain + [plank] if final_only else terrain
    edges = fixed_edges(terrain, physics)
    half_hp = room['interludeHealth']

    def spawn(hp, now):
        x = max(3, min(38, player.x + 5))
        support = plank if plank in deck and plank.left <= x <= plank.right else floor
        return Actor(x, max(.1, support.top), hp, immune=now + .8)

    foe = spawn(half_hp if final_only else room['boutHealth'], 0)
    pattern = mode = 0  # windup, active, recovery
    remaining = trial['patterns'][0]['windup']
    direction = -1
    contacted = False
    airborne_goal = None
    combo = swing = 0
    attack_end = strike_at = combo_until = guard_until = parry_until = block_until = counter_until = -1
    jump_until = -1
    held_space = False
    last_key = None
    hits = parries = 0
    west_ticks = 0
    events = []
    for tick in range(round(40 / dt)):
        now = round(tick * dt, 6)
        attacking = now < attack_end
        pat = trial['patterns'][pattern]
        dx = foe.x - player.x
        sign = 1 if dx >= 0 else -1
        strike_in = remaining if mode == 0 and remaining < pat['windup'] else math.inf
        axis = 0
        key = None
        walk = False
        if phase == 'latch':
            lead = (latch - player.x) * player.face
            if not attacking:
                if .65 <= lead <= 1.4:
                    if player.floor:
                        key = 'J'
                else:
                    axis = 1 if latch >= player.x else -1
                    if abs(latch - player.x) < 1.4:
                        axis = -axis
        elif policy['west'] and player.y > 1:
            axis = -1 if player.x > tolerance else 1 if player.x < -tolerance else 0
            if phase == 'win':
                west_ticks += 1
        elif strike_in <= policy['parry']:
            key = 'L'
        elif (not policy['legacy'] and ((player.floor and abs(player.y - foe.y) > policy['tier'])
                                      or airborne_goal is not None)
              and (player.floor != floor or airborne_goal is not None) and foe.y <= .5):
            if player.floor == floor:
                airborne_goal = None
            elif player.floor:
                parents = paths_from(edges, player.floor)
                if floor not in parents:
                    raise ValueError('unreachable arena floor')
                next_face = floor
                while parents[next_face] != player.floor:
                    next_face = parents[next_face]
                _, airborne_goal, axis = link(terrain, player.floor, next_face, physics)
            else:
                axis = 0 if abs(airborne_goal - player.x) <= tolerance else (1 if airborne_goal > player.x else -1)
        elif abs(dx) > policy['reach']:
            axis, walk = sign, True
        elif abs(dx) > .06 and dx * player.face < 0:
            axis = sign
        elif (strike_in > policy['reserve'] and not attacking
              and (policy['legacy'] or (player.floor and abs(player.y - foe.y) <= policy['tier']))):
            key = 'J'

        if walk and player.floor:
            ray = player.x + axis * .95
            if any(min(player.x, ray) <= f.right and f.left <= max(player.x, ray)
                   and f.bottom <= player.y + .18 <= f.top for f in deck):
                jump_until = now + .24
        space = now < jump_until
        if now >= player.stun and now >= block_until:
            if space and not held_space and player.floor:
                player.vy, player.floor = jump, None
            if key != last_key and not attacking:
                if key == 'L' and now >= guard_until:
                    parry_until = now + policy['window']
                    block_until = parry_until + policy['recovery']
                    combo_until = -1
                elif key == 'J':
                    combo = combo + 1 if now <= combo_until and combo < profile['comboLimit'] else 1
                    swing = swings[combo - 1]
                    duration, delay = swing[:2]
                    attack_end, strike_at = now + duration, now + delay
                    combo_until = attack_end + .18
                    dmg = swing[6] * (policy['counter'] if now < counter_until else 1)
                    counter_until = -1
                    attacking = True
        last_key = key if key != last_key else None
        if held_space and not space and player.vy > 0:
            player.vy *= .5
        held_space = space
        if now >= player.stun:
            if now < block_until:
                player.vx = 0
            elif player.floor:
                if axis and not attacking:
                    player.face = axis
                stepping = attacking and now < attack_end - swing[0] + profile['stepSeconds']
                player.vx = (player.face * profile['stepSpeed'] if stepping else 0) if attacking else axis * speed
            else:
                player.vx = toward(player.vx, axis * speed, dt * (accel if axis else 6 if attacking else brake))
                if axis:
                    player.face = axis
        was_airborne = player.floor is None
        player.advance(deck, radius, gravity, dt)
        if player.floor and was_airborne:
            combo_until = -1

        if strike_at >= 0 and now >= strike_at:
            strike_at = -1
            reach, width, blade_h = swing[3:6]
            wave = profile['waveReach'] if combo == profile['comboLimit'] else 0
            blade_x = player.x + player.face * (reach + wave / 2)
            if phase == 'latch':
                if abs(blade_x - latch) <= (width + wave) / 2 and abs(player.y) <= blade_h / 2:
                    events.append(('latch', now, player.hp))
                    phase, deck = 'win', terrain + [plank]
                    foe = spawn(half_hp, now)
                    pattern = mode = 0
                    remaining = trial['patterns'][0]['windup']
                continue
            if (abs(blade_x - foe.x) <= (width + wave) / 2 + radius
                    and abs(player.y + 1 - foe.y - height / 2) <= (blade_h + height) / 2
                    and foe.damage(dmg, player.x, now, .35, half_hp if phase == 'half' else 0)):
                events.append((phase, now, foe.hp))
                if phase == 'half' and foe.hp <= half_hp:
                    phase = 'latch'
                if foe.hp == 0:
                    break
        if phase == 'latch':
            continue
        pressing = foe.hp <= half_hp
        if now >= foe.stun:
            dx = player.x - foe.x
            if mode == 0 and remaining == pat['windup'] and abs(dx) > pat['reach'] - .35:
                foe.vx = (1 if dx > 0 else -1) * trial['approachSpeed'] * (1.5 if pressing else 1)
            else:
                foe.vx = 0
                if mode == 0 and remaining == pat['windup']:
                    direction = 1 if dx >= 0 else -1
                threat_height = 3 if pat['action'] == 'thread_cross' else 2
                if (mode == 1 and not contacted and -radius <= dx * direction <= pat['reach'] + radius
                        and player.y < foe.y + threat_height and player.y + height > foe.y):
                    contacted = True
                    if now >= player.immune:
                        if now < parry_until:
                            parries += 1
                            parry_until = block_until = -1
                            guard_until = now + policy['guard']
                            counter_until = now + policy['counterTime']
                            player.face = -1 if dx > 0 else 1
                        elif now >= guard_until and player.damage(pat['damage'], foe.x, now, .75):
                            hits += 1
                            strike_at = attack_end = combo_until = -1
                            if player.hp <= 0:
                                break
                remaining = round(remaining - dt, 6)
                if remaining <= 0:
                    if mode == 0:
                        mode, remaining, contacted = 1, pat['duration'], False
                    elif mode == 1:
                        mode, remaining = 2, pat['recovery'] * (.65 if pressing else 1)
                    else:
                        pattern = (pattern + 1) % len(trial['patterns'])
                        mode, remaining = 0, trial['patterns'][pattern]['windup']
        foe.advance(deck, radius, gravity, dt)
    return dict(playerHP=player.hp, bossHP=foe.hp, hits=hits, parries=parries, lastPhaseWestTicks=west_ticks,
                seconds=now, events=events)


def validate_story_bout(verify, project, scripts, motion, sword):
    def read(name):
        return json.loads((project / 'Assets/Resources' / name).read_text())

    story = read('Story/representative.json')
    rows = [r for r in story['rows'] if r['sceneId'] == 'mikhail_bout_i1']
    room = next(r for r in story['rooms'] if r['id'] == rows[0]['room'])
    trials = read('Trials/catalog.json')['trials']
    trial = next(d for d in trials if d['id'] == 'mikhail')
    body = scripts['FirstDepartureBot'].split('void FightBoss()')[1].split('internal WorldState')[0]
    motor = scripts['CombatMotor2D']
    swings = [tuple(float(n.strip().rstrip('f')) for n in m.split(','))
              for m in re.findall(r'new AttackSpec\(([^)]+)\)', motor)[:4]]
    capsule = re.search(r'capsule.size = new Vector2\(([^,]+)f, ([^)]+)f\)', motor)
    physics = (*motion, number(scripts['PlayerController2D'], 'MoveSpeed'), number(motor, 'AirAcceleration'),
               number(motor, 'AirBrake'), float(capsule[1]) / 2, float(capsule[2]), number(scripts['VelvetTraversal'], 'tolerance'))
    defense = scripts['DefenseTuning']
    guards = dict(window=number(defense, 'ParryWindowSeconds'), recovery=number(defense, 'ParryRecoverySeconds'),
                  counter=number(defense, 'CounterDamageMultiplier'), counterTime=number(defense, 'CounterSeconds'),
                  guard=number(defense, 'ParrySuccessGuardSeconds'))
    before = dict(guards, legacy=True, west=True, reach=1.15, reserve=.38, parry=.09)
    current = before if 'MoveTo(0)' in body else dict(
        guards, legacy=False, west=False, reach=number(body, 'swordRange'),
        reserve=number(scripts['FirstDepartureBot'], 'SafeSwingLead'),
        parry=float(re.search(r'strikeIn <= ([.\d]+)f', body)[1]),
        tier=float(re.search(r'Mathf.Abs\(feet.y - at.y\) > ([.\d]+)f', body)[1]))
    profile = sword['stages'][1]
    def run(policy, hp, start, sword_profile=profile):
        return replay(room, rows, trial, sword_profile, swings, physics, policy, hp, start)

    old, fixed = run(before, 6, 'investigated'), run(current, 6, 'investigated')
    finals = {str(hp): run(current, hp, 'plank') for hp in (1, 3, 6)}
    old_plank = run(before, 6, 'plank')
    no_retreat = run(dict(before, west=False), 6, 'plank')
    main = run(current, 6, 'standard', sword['stages'][0])
    area, bot = scripts['RepresentativeArea'], scripts['FirstDepartureBot']
    def compact(source):
        return ' '.join(re.sub(r'//[^\n]*', '', source).split())

    def ordered(source, *parts):
        offset = 0
        for part in parts:
            pos = source.find(part, offset)
            if pos < 0:
                return False
            offset = pos + len(part)
        return True

    verify([r['requiredPlayerAction'] for r in rows] == [
        ['confirm'], ['confirm'], ['strike:mikhail_retreat_latch'], ['win:mikhail'], ['confirm']] and
        'half' in rows[1]['entryCondition'] and rows[2]['completionFlag'] in rows[3]['entryCondition'] and
        rows[3]['effect'] == 'defeat' and rows[3]['completionFlag'] in rows[4]['entryCondition'] and
        rows[4]['effect'] == 'duelHelp' and room['boutHealth'] == 14 and room['interludeHealth'] == 7,
        'protective bout stops at half, cuts the latch, requires a live win and grants silver')
    verify('opponent.gameObject.activeSelf && FightNeeded() ? opponent : null' in scripts['PlaytestObservations'] and
           ordered(compact(bot), 'case ShellPage.StoryDialogue:', 'if (Area.BoutOpponent != null) { FightBoss(); return; }',
                   'string action = Area.PendingAction;') and 'if (area.BoutOpponent != null) { bot.FightBoss();' in scripts['ClockDistrictProbe'] and
           'ActionToken.StartsWith("win:",' in area and 'ActionToken.StartsWith("interlude:",' in area and
           'if (opponent.Health.Dead) { if (Complete()) StopBout();' in area and
           'if (ActiveRow == null || committed) return false;' in area,
           'both probes follow the active story opponent across implicit half and explicit win actions')
    verify(ordered(compact(body), 'Area.Bout?.StrikeIn', 'if (strikeIn <=', 'keys.Tap(KeyCode.L); return;',
                   'Player.Grounded && Mathf.Abs(feet.y - at.y) >', 'FixedFaceAt(at.x, at.y)',
                   '!ReachedCombatSurface(support, at.x)', 'if (Mathf.Abs(dx) > swordRange) { Walk(at.x, swordRange); return; }',
                   'dx * Player.Facing < 0', 'keys.Hold(dx < 0 ? KeyCode.A : KeyCode.D); return;',
                   'Player.Grounded && Mathf.Abs(feet.y - at.y) <=', 'strikeIn > SafeSwingLead',
                   '!Player.Attacking && !Player.Stunned && !Player.Defense.BlocksActions', 'keys.Tap(KeyCode.J);') and
           all(t not in body for t in ('MoveTo(0)', 'Area.Room.id', 'textKey', 'Player.Health.Current')),
           'live parry precedes tier travel, facing and a grounded sword with no room or HP avoidance')
    verify(not current['legacy'] and current['parry'] < guards['window'] and
           current['reserve'] + 1e-8 >= max(s[0] for s in swings) + current['parry'] and
           current['reach'] <= swings[0][3] + swings[0][4] / 2 + physics[6],
           'swing and parry decisions fit the first blade, longest recovery and actual defense window')
    verify(old['playerHP'] == 0 and old['bossHP'] == 7 and any(e[0] == 'latch' for e in old['events']) and
           not any(e[0] == 'win' for e in old['events']) and old['lastPhaseWestTicks'] > 0,
           'run 14 controller reproduces death with 7 HP on the resummoned opponent')
    verify(old_plank['bossHP'] == 7 and old_plank['playerHP'] == 0 and
           no_retreat['bossHP'] == 0 and no_retreat['playerHP'] > 0,
           'same plank spawn, health and old attack timings isolate the absolute-height retreat')
    verify(fixed['playerHP'] > 0 and fixed['bossHP'] == 0 and
           list(dict.fromkeys(e[0] for e in fixed['events'])) == ['half', 'latch', 'win'] and
           [e[2] for e in fixed['events'] if e[0] == 'half'][-1] == room['interludeHealth'],
           'protected variant reaches half health, cuts the latch and wins the final bout')
    for hp, result in finals.items():
        verify(result['bossHP'] == 0 and result['playerHP'] == int(hp) and result['hits'] == 0 and
               any(e[0] == 'win' for e in result['events']),
               'last phase wins without a refill starting at ' + hp + ' HP')
    verify(main['bossHP'] == 0 and main['playerHP'] > 0 and all(e[0] == 'win' for e in main['events']),
           'uninvestigated 14 HP win from the same balcony with the stage-one sword')

    restore = area.split('public void Restore()')[1].split('private void Travel(')[0]
    reset = motor.split('public void ResetAt(')[1].split('private void OnDisable')[0]
    contact = scripts['StoryTerrain'].split('bool SaveContactFloor()')[1].split('void UpdateActivationZones()')[0]
    verify('player.ResetAt(' in restore and 'Health.Refill();' in reset and
           'Current = Maximum;' in scripts['Health'].split('void Refill()')[1] and
           'relocate = destination != Room;' in area and 'if (relocate) { Restore(); return; }' in area and
           'Checkpoint(Room, floor.id);' in contact and 'session.Save();' in contact and
           all(t not in contact for t in ('ResetAt(', 'Refill(', 'Heal(', 'Restore(')),
           'a room arrival refills HP while same-room checkpoint contact only saves')
    healing = [d for d in trials if d['support']['effect'] == 'heal']
    heal_method = bot.partition('bool TryDistrictHeal()')[2].partition('void FightBoss()')[0]
    assist = scripts['BenefactorSupportController']
    verify([(d['id'], d['support']['heal'], d['support']['maxCharges']) for d in healing] == [('irina', 2, 1)] and
           ordered(compact(heal_method), '!Player.Grounded || Player.Health.Dead', '!Session.Progression.Trials.OrdinaryCombat',
                   'GameInput.ResolveSkill(Vector2.down, Player.Facing, Player.Grounded)', 'ForSkill(skill)',
                   'assist?.support.effect != "heal"', '!BenefactorTrialProgress.Has(Record, assist, "charged")',
                   'Player.Health.Maximum - Player.Health.Current < assist.support.heal', 'keys.Hold(KeyCode.DownArrow)', 'keys.Tap(KeyCode.H)') and
           'bot.TryDistrictHeal()' in scripts['ClockDistrictProbe'] and 'bot.TryDistrictHeal()' in scripts['VelvetDistrictProbe'] and
           all(t in assist for t in ('GameInput.SupportDown', 'ReserveSupport(d)', 'session.LastSaveSucceeded', 'player.Health.Heal(s.heal)')) and
           'grounded ? BenefactorIds.Canvas : BenefactorIds.Forge' in scripts['GameInput'] and
           not re.search(r'\b(?:Heal|Refill|ResetAt|ReserveSupport)\s*\(', bot),
           'both district routes use the charged two-point heal through the grounded H chord')
    verify('Player.Health.Current * 2 <= Player.Health.Maximum && target.StrikeIn <= SafeSwingLead' in bot and
           'if (ReserveWaveParry(target)) return;' in bot and 'if (ReserveWaveParry(target)) return;' in scripts['ClockTraversal'],
           'wounded wave combat reserves the parry window for sword and setup targets')
    clock = read('Story/clock.json')
    rooms = {r['id']: r for r in story['rooms'] + clock['rooms']}
    arrival = next(r for r in rooms.values() if rows[-1]['resumePoint'] in {f['id'] for f in r['floors']})
    route = [arrival['id'], 'hub_lift', 'clock_01_entry', 'clock_02_pendulum', 'clock_09_shortcut']
    gates = []
    for src, dest in zip(route, route[1:]):
        exits = rooms[src]['exits'] + [d for d in clock['links'] if d['source'] == src]
        gates.append(next(d['gate'] for d in exits if d['room'] == dest))
    origin = next(f for f in arrival['floors'] if f['id'] == rows[-1]['resumePoint'])
    hub_door = next(d for d in arrival['exits'] if d['room'] == route[1])
    old_exit, fixed_exit = [return_door(arrival, origin, hub_door, physics, hop) for hop in (False, True)]
    probe = compact(scripts['ClockDistrictProbe'])
    verify(gates == ['depart', 'depart', 'clock_entry_complete', 'synergy'] and
           'Has("investigation_complete") && Record.Get(Room.skill).acquired && Record.Get(SkillRules.SilverStitch).acquired' in scripts['RepresentativeRoomLayout'] and
           'if (room == "velvet_05_return" && bot.Record.Get(SkillRules.SilverStitch).acquired) { Door(bot, keys, "hub_lift");' in probe and
           'result.silverBoutWinFlag = area.ActiveRow.completionFlag;' in probe and
           '!bot.Record.storyFlags.Contains(result.silverBoutWinFlag)' in probe and
           'result.contacts.Count == Route.Length' in probe,
           'post-win return uses the obstacle-aware door and requires the live victory flag at contact 09')
    verify(not old_exit['reached'] and fixed_exit['reached'] and
           hub_door['approach']['x'] <= fixed_exit['x'] <= hub_door['approach']['x'] + hub_door['approach']['width'] and
           'return positioned && Area.OnReturnFloor && door.InReach(Player.Body.position);' in scripts['ClockTraversal'],
           'single-tap jump clears the return waterway plank en route to the hub door')
    return dict(before=old, after=fixed, lastPhase=finals, oldPlank=old_plank,
                oldPlankWithoutRetreat=no_retreat, standardBout=main, postWinRooms=route, postWinGates=gates,
                waterwayExitBefore=old_exit, waterwayExitAfter=fixed_exit)
