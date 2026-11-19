const fs = require('node:fs');
const {createProbe} = require('./replay_velvet_probe');
const input = JSON.parse(fs.readFileSync(0, 'utf8'));
const {rooms, sources, silver, fixture} = input;
const [jump, gravity, dt, speed, accel, brake, radius, bodyHeight, aimTolerance] = input.physics;

function body(source, signature) {
    const start = source.indexOf(signature);
    if (start < 0) throw Error('Missing replay method ' + signature);
    const open = source.indexOf('{', start);
    let depth = 1;
    for (let i = open + 1; i < source.length; i++) {
        if (source[i] === '{') depth++;
        if (source[i] === '}' && --depth === 0) return source.slice(open + 1, i);
    }
    throw Error('Unclosed method ' + signature);
}

function compile(source, args = '') {
    const js = source.replace(/\/\/[^\n]*/g, '')
        .replace(/foreach \(var (\w+) in ([^)]+)\)/g, 'for (const $1 of $2)')
        .replace(/\b(?:const float|float|int|bool|string|var|StorySurface) (\w+)/g, 'let $1')
        .replace(/(\d*\.?\d+)f\b/g, '$1').replace(/approachingLaunch: /g, '')
        .replace(/out (?:let )?(\w+)/g, '"$1"')
        .replace(/Array\.FindIndex\((\w+), /g, '$1.findIndex(')
        .replace(/Array\.Find\(([\w.]+), /g, '$1.find(')
        .replace(/\.Exists\(/g, '.some(').replace(/\.Find\(/g, '.find(').replace(/\.(Length|Count)\b/g, '.length')
        .replace(/\.GetComponent<CapsuleCollider2D>\(\)/g, '.collider')
        .replace(/new InvalidOperationException/g, 'new Error')
        .replace(/Player.Body.position \+ Vector2.up \* ([.\d]+)/g, 'raised($1)')
        .replace(/Vector2.right \* (\w+(?:\.\w+)?)/g, '$1');
    return new Function('env', '...params', 'with (env) {return ((' + args + ') => {' + js + '})(...params)}');
}

const methods = {};
function method(file, signature, args) {
    const name = signature.slice(signature.lastIndexOf(' ') + 1, -1);
    methods[name] = compile(body(sources[file], signature), args);
}
method('VelvetTraversal', 'bool SewReturnRoute(', 'collectUpperSupports = false');
if (sources.VelvetTraversal.includes('bool SewReturnSeam(')) {
    method('VelvetTraversal', 'bool SewReturnSeam(', 'room, target, water');
    method('VelvetTraversal', 'bool ReturnBankReady(', 'room');
    method('VelvetTraversal', 'void TakeReturnDoor(');
}
method('VelvetTraversal', 'bool RideCounterweight(', 'carrierId, bank, dismountHeight');
method('VelvetTraversal', 'bool MoveTo(', 'x, tolerance = .14');
method('VelvetTraversal', 'bool ReachBalcony(');
method('VelvetTraversal', 'void ObserveSilver(', 'e');
method('ClockTraversal', 'bool ClockPosition(', 'face, x, tolerance = .24');
method('ClockTraversal', 'bool ClockDoor(', 'door');
method('ClockTraversal', 'StorySurface FixedFaceAt(', 'x, y');
method('ClockTraversal', 'bool ReachedCombatSurface(', 'support, targetX');
method('ClockTraversal', 'void ClearStep(');
method('FirstDepartureBot', 'bool Walk(', 'to, tolerance, approachingLaunch = false');
method('FirstDepartureBot', 'bool BlockedAtStep(', 'facing');
if (sources.FirstDepartureBot.includes('void ApproachRaisedLip('))
    method('FirstDepartureBot', 'void ApproachRaisedLip(', 'lip, body, facing, gravityStep');
if (sources.FirstDepartureBot.includes('void JumpOverStep('))
    method('FirstDepartureBot', 'void JumpOverStep(', 'facing, approachingLaunch = false');
method('FirstDepartureBot', 'bool Aim(', 'x');
method('FirstDepartureBot', 'void FightTarget(', 'target');
method('FirstDepartureBot', 'bool ReserveWaveParry(', 'target');
method('FirstDepartureBot', 'void SeekFloor(');
const drive = body(sources.FirstDepartureBot, 'void Drive(');
const gather = compile(drive.slice(drive.indexOf('if (Area.Room.stage == "velvet"'), drive.indexOf('var door = Area.ForwardDoor')),
    'collectUpperSupports = false');
const driveRoom = compile(drive.slice(drive.indexOf('if (Area.Room.stage == "velvet"')), 'step, collectUpperSupports = false');
const transfer = compile(body(drive, 'if (action == "use:return_call")'));
const routeTarget = compile(body(body(sources.PlaytestObservations, 'WaveTarget2D RouteTarget'), 'get'));
const nextDoor = compile(body(sources.FirstDeparture, 'string NextDoorId()'));
const nearestFloor = compile(body(sources.RepresentativeArea, 'StoryMark NearestFloor()'));
const landingY = compile(body(sources.RepresentativeRoomLayout, 'float LandingY('), 'floor');
const retire = compile(body(sources.StitchSurface2D, 'void Retire()'));
const ready = compile('return ' + sources.WaveTargetState.match(/bool Ready\(float now\) => ([^;]+)/)[1], 'now');
const setup = compile(body(sources.WaveTargetState, 'bool Setup('), 'use');
const cut = compile(body(sources.WaveTargetState, 'bool Cut('), 'use, now');
const onFloor = compile(body(sources.StitchSurface2D, 'bool PlayerOnFloor()')
    .replace('Vector3 delta = player.transform.position - transform.position;', 'let delta = separation;'));
const move = compile(body(sources.CombatMotor2D, 'void Move(')
    .replace(' + Vector2.right * carrierSpeed', '.onCarrier(carrierSpeed)'), 'speed');
class Vector2 {
    constructor(x, y) { this.x = x; this.y = y; }
    onCarrier(speed) { return new Vector2(this.x + speed, this.y); }
}
const controls = ['A', 'D', 'Space', 'F', 'J', 'K', 'L', 'Q', 'Return'];
const KeyCode = Object.fromEntries(controls.map(k => [k, k]));
const SkillRules = {SilverStitch: 'silver_stitch', BasicSword: 'basic_sword'};
const round = n => Math.round(n * 1000) / 1000;
const inside = (r, p) => r.x <= p.x && p.x <= r.x + r.width && r.y <= p.y && p.y <= r.y + r.height;

function firstHeadCorner(faces, feet, velocity) {
    if (velocity.y <= 0) return null;
    const speedSquared = velocity.x * velocity.x + velocity.y * velocity.y;
    let first = null;
    for (const face of faces) {
        if (face.one_way) continue;
        const y = feet.y + bodyHeight - radius - face.bottom;
        const x = feet.x - Math.max(face.left, Math.min(face.right, feet.x));
        if (y >= 0 || x === 0) continue;
        const approach = x * velocity.x + y * velocity.y;
        if (approach >= 0) continue;
        const discriminant = approach * approach - speedSquared * (x * x + y * y - radius * radius);
        if (discriminant < 0) continue;
        const time = Math.max(0, (-approach - Math.sqrt(discriminant)) / speedSquared);
        const atX = x + velocity.x * time, atY = y + velocity.y * time;
        if (time > dt || first && time >= first.time || atX * x <= 0 || atY >= 0) continue;
        const distance = Math.hypot(atX, atY);
        first = {face, time, normalX: atX / distance, normalY: atY / distance};
    }
    return first;
}

function simulate(mode, frameDt = dt, firstProbe = 0) {
    const district = mode === 'district';
    const highLip = mode === 'unreachableLip';
    const cornerJump = mode === 'cornerJump';
    const afterInvestigation = highLip || cornerJump || mode === 'afterInvestigation' || mode === 'manualAfterInvestigation';
    const waterStart = mode === 'recovery' || mode === 'manual';
    const swingProbe = mode === 'heldAttack' || mode === 'stillAttack';
    const loomStart = mode === 'loom' || mode === 'loomCrossing' || mode === 'manualLoom' || swingProbe;
    const start = afterInvestigation ? input.district : loomStart ? input.loom : fixture;
    const saved = loomStart || waterStart || afterInvestigation;
    const pos = {x: 0, y: 0}, vel = {x: 0, y: 0};
    const result = {threatContacts: [], waterLandings: [], combat: [], credits: [], totals: [], departures: [], waterSave: false, waterExit: null, safeCall: false};
    let parryUntil = 0, defenseUntil = 0, guardUntil = 0, stunUntil = 0, immuneUntil = 0;
    let room, deck, support, seams, guards, carrier, plank, tick = 0, uses = saved ? start.silverUses : 0;
    let swordUses = input.initialSwordUses + (saved && start.swordUses != null ? start.swordUses : input.loom.swordUsesAtDeparture);
    let chainProfile, swordUse, comboStep = 0, swing = input.swings[0];
    let down = new Set(), previous = new Set(), nextCast = 0, swingAt = -Infinity, delivered = true, serial = 0;
    let contacts = new Set(), sides = [];
    let fixedTick = 0, jumpAt = -Infinity, lastGrounded = -Infinity, cutJump = false;
    const now = () => tick * frameDt;
    const player = {Body: {position: pos, linearVelocity: vel, gravityScale: input.gravityScale}, Facing: 1,
        Health: {get Dead() { return this.Current <= 0; }, Current: start.hp, Maximum: start.hp},
        get Stunned() { return now() < stunUntil; },
        get Grounded() { return !!support; }, get Attacking() { return now() < swingAt + swing[0]; }};
    player.collider = {get bounds() { return {center: {x: pos.x, y: pos.y + bodyHeight / 2},
            min: {x: pos.x - radius, y: pos.y}, max: {x: pos.x + radius, y: pos.y + bodyHeight},
            size: {x: radius * 2, y: bodyHeight}, extents: {x: radius, y: bodyHeight / 2}}; },
        GetContacts(list) { list.splice(0, list.length, ...sides); }};
    const flags = new Set(district ? ['intro_complete', 'velvet_entry_complete'] : start.flags);
    const gathering = () => flags.has('mikhail_i0_complete') && uses < silver.stage3At;
    let entryWave = [], entryMember = 0;
    const keys = {Hold: k => down.add(k), Tap: k => { if (!previous.has(k)) down.add(k); },
        Cast: () => keys.Tap('Q'), GetKeyDown: k => down.has(k) && !previous.has(k)};
    const gate = d => (!d.gate || flags.has(d.gate)) && !(d.room === 'velvet_02_loom' && env.NeedsEntryWave);
    const area = {Rea: player, ActiveRow: null, get Room() { return room; }, get RouteTarget() { return routeTarget(env); },
        get ForwardDoor() { return room.exits.find(d => d.room === nextDoor(env) && gate(d)); },
        get OnReturnFloor() { return support && !support.temporary && support !== carrier && !player.Attacking && !player.Stunned && now() >= defenseUntil; },
        StandingOn: id => support?.name === id, RidingSurface: s => carrier && support === carrier && carrier.name === s.id,
        StandingOnRamp: () => false, ProbeSurfaceBounds: s => ({max: {y: s.id === carrier?.name ? carrier.top : s.top}})};
    const env = {KeyCode, SkillRules, Player: player, Area: area, keys,
        Time: {get time() { return now(); }, get fixedTime() { return fixedTick * dt; }, get frameCount() { return tick; }, fixedDeltaTime: dt},
        Mathf: {Abs: Math.abs, Sign: Math.sign, Min: Math.min, FloorToInt: Math.floor},
        FixedSurfaceRoute: {AimTolerance: aimTolerance, IsFloor: s => !s.wall && !s.travelY,
            Step: class { constructor(to, launch, land, direction = 0) {
                Object.assign(this, {To: to, LaunchX: launch, LandX: land, WalkOff: direction});
            }}}, PlayerController2D: {JumpSpeed: jump, MoveSpeed: speed},
        LevelBuilder: {GroundLayer: 1}, Physics2D: {Raycast: ray, gravity: {y: -gravity / input.gravityScale}},
        raised: h => ({x: pos.x, y: pos.y + h}),
        OnSurface: s => player.Grounded && contacts.has(s) && Math.abs(pos.y - s.top) <= .12,
        ReadTerrain() {}, walkContacts: [], blockedBy: null, blockedFrame: -1, blockedDirection: 0, blockedX: 0, blockedSince: 0,
        BlockedWalkSeconds: Number(sources.FirstDepartureBot.match(/BlockedWalkSeconds = ([.\d]+)f/)[1]),
        BlockedWalkSlop: Number(sources.FirstDepartureBot.match(/BlockedWalkSlop = ([.\d]+)f/)[1]),
        Has: flag => flags.has(flag), SafeSwingLead: .65,
        get NeedsEntryWave() { return district && room.id === 'velvet_01_entry' && !flags.has('velvet_entry_wave_cleared'); },
        get entryMember() { return entryMember; }, get entryWave() { return entryWave; },
        get HasBridge() { return district && ['velvet_02_loom', 'velvet_03_gallery'].includes(room.id); },
        get BridgeFlag() { return room.id === 'velvet_02_loom' ? 'loom_bridge' : 'gallery_bridge'; },
        get latches() { return guards.filter(g => g.kind.id === 'sword_latch'); },
        get GatheringSilver() { return gathering(); }, DefenseTuning: {DashDistance: input.dashDistance},
        FloorDistance: f => Math.hypot(f.x - pos.x, landingY(env, f.id) - pos.y), Distance: x => Math.abs(x - pos.x),
        InEnding: false, get Room() { return room; }, get roomTargets() { return guards; },
        Record: {Get: () => ({acquired: !district || flags.has('mikhail_i0_complete')}), get storyFlags() { return [...flags]; },
            get firstTransferPending() { return uses >= silver.stage3At; }},
        get StitchSurface2D() { return {All: seams}; }, get terrain() { return deck; },
        jumpingFrom: null, jumpUntil: -1, jumpStarted: -1, stepFrom: -1, stepGoal: -1, step: null,
        sewingRoom: null, lastSewn: null, seamIndex: 0, rodeFloat: false, leavingFloat: false, sawSafeCall: false,
        stairRoom: null, nextTread: 0, jumping: false,
        TryFaceAt(x, y, dest) {
            env[dest] = env.FixedFaceAt(x, y);
            if (!env[dest]) throw Error('No support at ' + [room.id, x, y]);
            return true;
        },
        FailRoute(reason) { throw Error(reason); },
        route: {Next(from, goal) {
            const queue = [[deck[from].id, null]], seen = new Set([deck[from].id]);
            for (const [id, first] of queue) {
                for (const edge of room.edges[id]) {
                    if (seen.has(edge.to)) continue;
                    const step = first ?? new env.FixedSurfaceRoute.Step(deck.findIndex(f => f.id === edge.to),
                        edge.launch, edge.land, edge.direction);
                    if (edge.to === deck[goal].id) { env.step = step; return true; }
                    seen.add(edge.to);
                    queue.push([edge.to, step]);
                }
            }
            return false;
        }}};
    for (const [name, fn] of Object.entries(methods)) env[name] = (...args) => fn(env, ...args);
    const bot = {Shell: {Page: 'Playing'}, Session: {ActiveSlot: Number(input.district.slot.split('-')[1]), LastSaveSucceeded: true}, Area: area,
        Record: env.Record, MoveTo: env.MoveTo, ReachBalcony: env.ReachBalcony, TryDistrictHeal: () => false,
        Drive: (...args) => driveRoom(env, ...args)};
    const probe = district ? createProbe(sources.VelvetDistrictProbe, body, compile, bot, keys, env.Time) : null;
    const capturedProbe = afterInvestigation && !cornerJump && mode !== 'manualAfterInvestigation'
        ? createProbe(sources.VelvetDistrictProbe, body, compile, bot, keys, env.Time) : null;
    if (probe) result.probe = probe.result;
    let districtProgress = 0, lastContacts = 0, lastCleared = 0, lastWaveRoom;
    const motor = {Vector2, Health: {IsPlayer: true, get Dead() { return player.Health.Dead; }},
        get Grounded() { return player.Grounded; }, get Stunned() { return player.Stunned; },
        get Facing() { return player.Facing; }, get Attacking() { return player.Attacking; },
        get attackStartedAt() { return swingAt; }, get chainProfile() { return chainProfile; },
        Face(v) { if (Math.abs(v) >= .01) player.Facing = Math.sign(v); },
        Body: {get linearVelocity() { return vel; }, set linearVelocity(v) { Object.assign(vel, v); }},
        Temporal: null, Defense: null, BenefactorSkills: null, carrierSpeed: 0,
        Time: {get time() { return now(); }, fixedDeltaTime: dt},
        AirDrag: input.airDrag, AirBrake: brake, AirAcceleration: accel,
        Mathf: {Abs: Math.abs, MoveTowards: (v, goal, by) => v + Math.sign(goal-v) * Math.min(by, Math.abs(goal-v))}};

    function waveTarget(id, kind, position, spawn) {
        const state = {archetype: kind, Remaining: spawn.segments, prepared: false, hits: 0, setupHits: spawn.setupHits ?? 1,
            NeedsSetup: kind.gate === 'skill' || Boolean(spawn.setupSkill), setupSkill: spawn.setupSkill || SkillRules.SilverStitch,
            lastSetup: null, LastSword: null, skillOnly: Boolean(spawn.skillOnly),
            float: {NegativeInfinity: -Infinity}, SkillRules, CrossLoopFinish: false, openUntil: -Infinity,
            get Cleared() { return this.Remaining === 0; }, Ready(t) { return ready(state, t); }};
        return {id, kind, transform: {position}, State: state, nextAttack: now() + kind.attackInterval, strikeAt: Infinity,
            get StrikeIn() { return this.strikeAt - now(); }};
    }

    function enter(id, floorId) {
        room = rooms[id];
        deck = room.faces.map(f => ({...f, id: f.name, oneWay: f.one_way,
            rect: {x: f.left, y: f.bottom, width: f.right - f.left, height: f.top - f.bottom}}));
        env.ClearStep();
        const floor = room.floors.find(f => f.id === floorId);
        pos.x = floor.x;
        support = floor.surface ? deck.find(f => f.id === floor.surface) : env.FixedFaceAt(pos.x, 0);
        pos.y = support.top;
        contacts = new Set([support]);
        sides = [];
        vel.x = vel.y = 0;
        if (swordUse) swordUse.Closed = true;
        swingAt = -Infinity;
        delivered = true;
        jumpAt = lastGrounded = -Infinity;
        cutJump = false;
        previous = new Set();
        parryUntil = defenseUntil = guardUntil = stunUntil = 0;
        player.Health.Current = player.Health.Maximum;
        immuneUntil = now() + .8;
        carrier = room.terrain.find(s => s.travelY);
        if (carrier) {
            const r = carrier.rect;
            carrier = {...carrier, name: carrier.id, left: r.x, right: r.x + r.width, top: r.y + r.height,
                bottom: r.y, base: r.y + r.height, entered: now(), one_way: true};
        }
        const mark = room.points.find(p => p.id === 'mikhail_reunion_plank');
        const [centerY, width, height] = input.plank;
        plank = mark && {name: mark.id, left: mark.x - width / 2, right: mark.x + width / 2,
            bottom: centerY - height / 2, top: centerY + height / 2};
        if (highLip && plank) plank.top = start.y + jump * jump / (2 * gravity) + radius;
        room.Surface = id => room.terrain.find(f => f.id === id);
        room.Floor = id => room.floors.find(f => f.id === id);
        room.Point = id => room.points.find(p => p.id === id);
        if (district) {
            result.rooms ??= [];
            result.rooms.push(id);
            for (const flag of flags) if (flag.startsWith('scene_floor:velvet:')) flags.delete(flag);
            flags.add('scene_floor:velvet:' + floor.id);
        }
        room.SurfaceTop = id => deck.find(f => f.id === id).top;
        for (const door of room.exits) {
            door.approach.Defined = door.approach.width > 0 && door.approach.height > 0;
            door.InReach = p => inside(door.approach, p);
        }
        seams = (room.seamRuns ?? []).flatMap(run => Array.from({length: run.count}, (_, i) => ({run, offset: i,
            x: run.x + run.stepX * i, y: run.y}))).map((s, i) => {
            const id = room.id + ':seam_' + i;
            const half = i < room.singleSeams ? .45 : 1.1;
            return {...s, floor: {enabled: false}, releasedBridge: false, TargetId: id, Bounds: {extents: {x: half}}, Installed: false, retiring: false,
                get Available() { return !this.Installed && !this.retiring; },
                name: id, left: s.x - half, right: s.x + half, top: s.y + .07, bottom: s.y - .07, temporary: true,
                progression: {EndUse(use) { use.Closed = true; }}, RestoreVisual() {},
                PlayerOnFloor() { return onFloor({player, separation: {x: pos.x - s.x, y: pos.y - s.y}, span: 2 * half, Mathf: env.Mathf}); }};
        });
        room.SeamRun = (i, dest) => { env[dest] = seams[i].offset; return seams[i].run; };
        guards = [];
        let index = 0;
        for (const group of room.roster) {
            for (const placement of group.placementIds) {
                const p = room.placements.find(p => p.id === placement);
                let kind = input.kinds[group.id];
                if (district && (group.silverReturnOnly && !gathering() ||
                    kind.mechanism && env.HasBridge && flags.has(env.BridgeFlag))) { index++; continue; }
                if (district && kind.gate === 'skill' && !group.setupSkill && !env.Record.Get().acquired)
                    kind = input.kinds.shell_sentry;
                if (district || !kind.mechanism) guards.push(waveTarget(room.id + ':' + group.id + '_' + index, kind, p, group));
                index++;
            }
        }
        entryWave = [];
        entryMember = 0;
        if (env.NeedsEntryWave) {
            for (const spawn of input.entryWave.spawns) {
                for (let i = 0; i < spawn.count; i++) {
                    const index = entryWave.length;
                    entryWave.push(waveTarget(room.id + ':' + input.entryWave.id + ':' + index,
                        input.kinds[spawn.archetypeId], {x: 8 + spawn.x + index * 2, y: .05}, spawn));
                }
            }
        }
    }

    function geometry() {
        const fixed = [...deck];
        if (plank) fixed.push(plank);
        if (carrier) fixed.push(carrier);
        return fixed.concat(seams.filter(s => s.floor.enabled));
    }
    function ray(origin, dir, length) {
        return geometry().find(f => {
            const gap = dir > 0 ? f.left - origin.x : origin.x - f.right;
            return gap >= 0 && gap <= length && f.bottom <= origin.y && origin.y <= f.top;
        });
    }
    function credit(use, target) {
        if (use.counted || use.Closed) return;
        use.counted = true;
        uses++;
        result.credits.push({tick, use: use.id, target});
        districtProgress = now();
        env.ObserveSilver({kind: 'valid_use', skillId: SkillRules.SilverStitch, targetId: target});
        probe?.credit({kind: 'valid_use', skillId: SkillRules.SilverStitch, targetId: target, useId: use.id, after: uses}, room.id);
    }
    function hit(target, center, y, width, height) {
        const p = target.transform.position;
        return (!p.active || inside(p.active, pos)) && Math.abs(p.x - center) <= (width + input.targetSize[0]) / 2 &&
            Math.abs(p.y + .9 - y) <= (height + input.targetSize[1]) / 2;
    }
    function effects() {
        const face = player.Facing;
        const actors = env.NeedsEntryWave && entryMember < entryWave.length ? [...guards, entryWave[entryMember]] : guards;
        const defend = input.parry;
        if (keys.GetKeyDown('L') && !player.Attacking && !player.Stunned && now() >= defenseUntil && now() >= guardUntil) {
            parryUntil = now() + defend.ParryWindowSeconds;
            defenseUntil = parryUntil + defend.ParryRecoverySeconds;
        }
        const canAct = !player.Stunned && now() >= defenseUntil;
        if (canAct && keys.GetKeyDown('Q') && now() >= nextCast && !player.Attacking) {
            const stage = uses < silver.stage2At ? 1 : 2;
            const profile = silver.stages[stage - 1];
            nextCast = now() + profile.cooldown;
            const use = {id: ++serial, SkillId: SkillRules.SilverStitch, Closed: false};
            for (const g of actors) {
                if (hit(g, pos.x + face * profile.reach / 2, pos.y + 1, profile.reach, 2) && setup(g.State, use)) {
                    g.strikeAt = Infinity;
                    g.nextAttack = now() + g.kind.attackInterval;
                    credit(use, g.id + ':posture');
                    result.combat.push({tick, target: g.id, action: g.State.prepared ? 'open' : 'setup', x: round(pos.x)});
                }
            }
            const candidates = seams.filter(s => {
                const lead = (s.x - pos.x) * face;
                return !s.Installed && !s.retiring && lead >= -s.Bounds.extents.x && lead <= profile.reach + s.Bounds.extents.x &&
                    s.y - .15 <= pos.y + 2 && s.y + 1.45 >= pos.y && Math.hypot(s.x - pos.x, s.y - pos.y) <= profile.reach;
            }).sort((a, b) => Math.hypot(a.x - pos.x, a.y - pos.y) - Math.hypot(b.x - pos.x, b.y - pos.y));
            const s = candidates[0];
            if (seams.filter(s => !s.Available).length < profile.installationLimit && s && 2 * s.Bounds.extents.x <= profile.maxSpan) {
                Object.assign(s, {Installed: true, stage, use, until: now() + profile.lifetime});
                s.floor.enabled = true;
                if (stage === 1) credit(use, s.TargetId);
            } else use.Closed = true;
        }
        if (swordUse && !player.Attacking && (comboStep === chainProfile.comboLimit || now() > swingAt + swing[0] + input.grace))
            swordUse.Closed = true;
        if (canAct && keys.GetKeyDown('J') && !player.Attacking) {
            swingAt = now(); delivered = false;
            if (!swordUse || swordUse.Closed) {
                const skill = input.swordSkill;
                chainProfile = skill.stages[Number(swordUses >= skill.stage2At) + Number(swordUses >= skill.stage3At)];
                swordUse = {SkillId: SkillRules.BasicSword, Closed: false};
                comboStep = 0;
            }
            swing = input.swings[comboStep++];
        }
        if (!delivered && now() - swingAt >= swing[1]) {
            delivered = true;
            const latch = room.points.find(p => p.id === 'reunion_latch');
            if (mode === 'manual' && latch && Math.abs(latch.x - pos.x - face * swing[3]) <= swing[4] / 2 &&
                Math.abs(pos.y) <= swing[5] / 2) result.latchHit = {tick, feet: [round(pos.x), round(pos.y)]};
            const wave = comboStep === chainProfile.comboLimit ? chainProfile.waveReach : 0;
            for (const g of actors) {
                if (hit(g, pos.x + face * (swing[3] + wave / 2), pos.y + 1, swing[4] + wave, swing[5]) && cut(g.State, swordUse, now())) {
                    if (!swordUse.counted) { swordUses++; swordUse.counted = true; districtProgress = now(); }
                    result.combat.push({tick, target: g.id, action: 'cut', remaining: g.State.Remaining,
                        x: round(pos.x), y: round(pos.y), support: support?.name});
                }
            }
        }
        for (const g of actors) {
            const p = g.transform.position;
            if (g.kind.mechanism || p.active && !inside(p.active, pos) || g.State.Cleared || g.State.prepared) {
                g.strikeAt = Infinity; continue;
            }
            const threat = Math.abs(p.x - pos.x) <= g.kind.attackReach + radius &&
                Math.abs(p.y + 1 - pos.y - bodyHeight / 2) <= 1.1 + bodyHeight / 2;
            if (threat && !Number.isFinite(g.strikeAt) && now() >= g.nextAttack) g.strikeAt = now() + g.kind.telegraphSeconds;
            if (now() >= g.strikeAt) {
                if (threat && now() >= immuneUntil && now() >= guardUntil) {
                    if (now() < parryUntil) {
                        defenseUntil = parryUntil = now();
                        guardUntil = now() + defend.ParrySuccessGuardSeconds;
                        player.Facing = Math.sign(p.x - pos.x) || player.Facing;
                        if (g.kind.gate === 'defense') g.State.openUntil = now() + g.kind.openingSeconds;
                        result.threatContacts.push({tick, target: g.id, outcome: 'parry'});
                    } else {
                        player.Health.Current -= g.kind.attackDamage;
                        result.threatContacts.push({tick, target: g.id, outcome: 'hit', hp: player.Health.Current});
                        stunUntil = now() + .18;
                        immuneUntil = now() + input.immunity;
                        nextCast = defenseUntil = 0;
                        if (swordUse) swordUse.Closed = true;
                        swingAt = -Infinity;
                        delivered = true;
                        for (const s of seams) if (s.Installed) s.use.Closed = true;
                        vel.x = pos.x >= p.x ? 5 : -5;
                        vel.y = 4;
                        support = null;
                    }
                }
                g.strikeAt = Infinity; g.nextAttack = now() + g.kind.attackInterval;
            }
        }
        for (const s of seams) {
            if (s.retiring && !s.PlayerOnFloor()) { s.retiring = false; s.floor.enabled = false; }
            if (!s.Installed) continue;
            if (now() >= s.until) retire(s);
            else if (s.stage >= 2 && player.Grounded && contacts.has(s) && s.PlayerOnFloor()) credit(s.use, s.TargetId + ':support');
        }
    }

    function physics() {
        const axis = +down.has('D') - +down.has('A');
        if (carrier) {
            const old = carrier.top;
            carrier.top = carrier.base + carrier.travelY * (.5 - .5 * Math.cos(((fixedTick + 1) * dt - carrier.entered) * 2 * Math.PI / carrier.period));
            carrier.bottom = carrier.top - carrier.rect.height;
            if (support === carrier) pos.y += carrier.top - old;
        }
        if (player.Grounded) lastGrounded = now();
        if (now() - lastGrounded <= input.coyote && now() - jumpAt <= input.jumpBuffer && !player.Stunned && now() >= defenseUntil) {
            support = null; vel.y = jump;
            lastGrounded = jumpAt = -Infinity;
        }
        if (now() < defenseUntil) vel.x = 0;
        else move(motor, axis * speed);
        if (cutJump && vel.y > 0) vel.y *= .5;
        cutJump = false;
        const faces = geometry();
        contacts = new Set();
        sides = [];
        vel.y = Math.max(vel.y, -18) - gravity * dt;
        let moveTime = dt;
        const contact = firstHeadCorner(faces, pos, vel);
        if (contact) {
            pos.x += vel.x * contact.time; pos.y += vel.y * contact.time;
            const normalSpeed = vel.x * contact.normalX + vel.y * contact.normalY;
            vel.x -= contact.normalX * normalSpeed; vel.y -= contact.normalY * normalSpeed;
            moveTime -= contact.time;
            if (afterInvestigation && contact.face === plank) result.lip.headContacts++;
        }
        let nx = pos.x + vel.x * moveTime;
        for (const f of faces) {
            if (f.one_way || f === support || f.bottom >= pos.y + bodyHeight || f.top <= pos.y + .001) continue;
            const capGap = Math.max(0, f.bottom - (pos.y + bodyHeight - radius), pos.y + radius - f.top);
            const reach = Math.sqrt(radius * radius - capGap * capGap);
            if (vel.x > 0 && pos.x <= f.left - reach + .001 && nx > f.left - reach) { nx = f.left - reach; contacts.add(f); }
            else if (vel.x < 0 && pos.x >= f.right + reach - .001 && nx < f.right + reach) { nx = f.right + reach; contacts.add(f); }
            else continue;
            f.gameObject = {layer: env.LevelBuilder.GroundLayer};
            f.bounds = {min: {x: f.left, y: f.bottom}, max: {x: f.right, y: f.top},
                center: {x: (f.left + f.right) / 2, y: (f.top + f.bottom) / 2}};
            sides.push({enabled: true, collider: player.collider, otherCollider: f,
                normal: {x: -Math.sign(vel.x) * reach / radius}, point: {x: vel.x > 0 ? f.left : f.right}});
        }
        if (Math.abs(nx - pos.x - vel.x * moveTime) > .00001) vel.x = 0;
        pos.x = nx;
        const ny = pos.y + vel.y * moveTime;
        const overlap = f => pos.x + radius > f.left && pos.x - radius < f.right;
        const land = faces.filter(f => overlap(f) && vel.y < 0 && ny <= f.top && (pos.y + .00001 >= f.top ||
            f === carrier && pos.y >= f.top - .065 && (!support || support.top <= f.top)));
        support = land.sort((a, b) => b.top - a.top)[0];
        if (support) { pos.y = support.top; vel.y = 0; contacts.add(support); }
        else {
            const ceiling = faces.find(f => !f.one_way && overlap(f) && vel.y > 0 &&
                pos.y + bodyHeight <= f.bottom && ny + bodyHeight >= f.bottom);
            pos.y = ceiling ? ceiling.bottom - bodyHeight : ny;
            if (ceiling) { vel.y = 0; contacts.add(ceiling); }
        }
    }

    const initial = district ? rooms.velvet_01_entry : rooms[start.room];
    enter(initial.id, initial.floors[0].id);
    if (afterInvestigation) {
        Object.assign(pos, {x: start.x, y: start.y});
        support = env.FixedFaceAt(pos.x, pos.y);
        contacts = new Set([support]);
        if (Boolean(support) !== start.grounded || Boolean(area.OnReturnFloor) !== start.safeFloor ||
            guards.length !== start.remainingTargets) throw Error('QA30 fixed ground or roster differs from the capture');
        if (capturedProbe) {
            capturedProbe.result.contacts.length = start.contacts;
            capturedProbe.result.returnUses = uses;
        }
        result.start = {feet: [pos.x, pos.y], support: support?.id, uses, remaining: guards.length,
            active: guards.filter(g => inside(g.transform.position.active, pos)).length,
            target: area.RouteTarget?.id ?? null, door: area.ForwardDoor?.room};
        result.lip = {top: plank.top, contacts: 0, headContacts: 0, takeoffs: [], peakFeet: pos.y};
    }
    if (loomStart) {
        Object.assign(pos, {x: start.x, y: start.y});
        support = seams.find(s => pos.x >= s.left && pos.x <= s.right && Math.abs(pos.y - s.top) < .01);
        support.floor.enabled = support.retiring = true;
        contacts = new Set([support]);
        env.sewingRoom = room.id;
        env.seamIndex = seams.length - Number(mode === 'loomCrossing');
        env.ObserveSilver({kind: 'valid_use', skillId: SkillRules.SilverStitch, targetId: support.TargetId + ':support'});
        if (area.RouteTarget?.id !== start.target || area.RouteTarget.State.Remaining !== start.targetSegments ||
            player.Grounded !== start.grounded || Boolean(area.OnReturnFloor) !== start.safeFloor)
            throw Error('QA29 ground contact or target differs from the capture');
        if (mode === 'loom') {
            const bankX = support.x + Math.sign(support.run.stepX) * 1.85;
            const from = env.FixedFaceAt(bankX, support.y - .24);
            const door = area.ForwardDoor;
            const goal = env.FixedFaceAt(door.x, door.approach.y + door.approach.height / 2);
            env.stepFrom = deck.indexOf(from);
            env.stepGoal = deck.indexOf(goal);
            if (!env.route.Next(env.stepFrom, env.stepGoal)) throw Error('No descending route from the loom bank');
            player.Facing = env.step.WalkOff;
            vel.x = player.Facing * speed;
        }
        result.start = {feet: [pos.x, pos.y], uses, hp: player.Health.Current, support: support.name,
            seamTop: support.top, fixedFace: env.FixedFaceAt(pos.x, pos.y)?.id ?? null,
            target: area.RouteTarget.id, remaining: area.RouteTarget.State.Remaining};
        if (swingProbe) result.swingTrace = [];
    }
    if (waterStart) {
        Object.assign(pos, {x: fixture.x, y: fixture.y});
        support = env.FixedFaceAt(pos.x, pos.y);
        contacts = new Set([support]);
        const target = guards.findIndex(g => g.id === fixture.target);
        guards[target].State.Remaining = fixture.targetSegments;
        if (!fixture.grounded || !fixture.safeFloor || !area.OnReturnFloor)
            throw Error('The QA28 recovery requires the reported fixed ground contact');
        for (const g of guards.slice(0, target)) g.State.Remaining = 0;
        const done = guards.slice(0, target).reduce((n, g) => n + g.State.setupHits, 0);
        env.sewingRoom = room.id;
        env.seamIndex = uses - done;
        const obstacle = seams[env.seamIndex - 1];
        obstacle.Installed = true;
        obstacle.use = {Closed: false};
        retire(obstacle);
        result.obstacle = {id: obstacle.TargetId, left: obstacle.left, right: obstacle.right,
            bottom: round(obstacle.bottom), top: round(obstacle.top), actorRight: round(pos.x + radius)};
        env.lastSewn = obstacle.TargetId;
    }
    let instruction = 0, began = 0, firstCornerContact, cornerJumpAt;
    function steerTo(x) {
        if (Math.abs(pos.x - x) <= .12) return true;
        keys.Hold(pos.x < x ? 'D' : 'A');
        return false;
    }
    function walkTo(x, floor) {
        return () => steerTo(x) && support?.name === floor;
    }
    function jumpTo(x, floor) {
        return () => {
            if (tick === began || !support) keys.Hold('Space');
            const there = steerTo(x);
            return tick > began && there && support?.name === floor;
        };
    }
    function sew(index) {
        return () => {
            if (player.Facing * (seams[index].x - pos.x) < 0) keys.Hold(seams[index].x > pos.x ? 'D' : 'A');
            else if (!seams[index].Installed) keys.Cast();
            return seams[index].Installed;
        };
    }
    function waitForThread(index) { return () => seams[index].Available; }
    function fight(index) {
        return () => {
            const state = guards[index].State;
            if (state.Cleared) return !player.Attacking;
            const dx = guards[index].transform.position.x - pos.x;
            if (player.Facing * dx < 0) keys.Hold(dx > 0 ? 'D' : 'A');
            else if (!player.Attacking) {
                if (state.prepared) keys.Tap('J');
                else keys.Cast();
            }
            return false;
        };
    }
    const manualRoute = [
        () => { keys.Hold('A'); return player.Facing === -1; },
        () => { if (!player.Attacking) keys.Tap('J'); return player.Attacking; },
        () => !player.Attacking,
        jumpTo(28.1, 'tailrace'), () => { keys.Hold('D'); return player.Facing === 1; },
        sew(4), waitForThread(4), walkTo(31.8, 'tailrace'), jumpTo(34.4, 'outfall_quay'),
        sew(5), waitForThread(5), walkTo(39.4, 'outfall_quay'), sew(6), waitForThread(6),
        walkTo(43.9, 'outfall_quay'), fight(1),
        () => steerTo(49.95) && support === carrier && carrier.top >= .95,
        jumpTo(51.55, 'wheel_gallery'), sew(7), waitForThread(7),
        walkTo(58.4, 'dry_dock'), fight(2), jumpTo(59.1, 'tread_1'), jumpTo(61.6, 'tread_2'),
        jumpTo(65, 'balcony'), fight(3), walkTo(64, 'balcony'), walkTo(46.3, 'lock_bed'),
        jumpTo(43.5, 'outfall_quay'), walkTo(38, 'outfall_quay'), () => { keys.Tap('F'); return false; }
    ];
    const useDoor = () => { keys.Tap('F'); return keys.GetKeyDown('F'); };
    const manualAfterInvestigation = [
        walkTo(15.4, 'silt_bed'), jumpTo(18.2, 'mikhail_reunion_plank'), walkTo(25.4, 'inspection_dock'),
        walkTo(30.1, 'tailrace'), jumpTo(34, 'outfall_quay'), walkTo(38, 'outfall_quay'), useDoor
    ];
    const manualFromLoom = [
        walkTo(8.15, 'reed_0'), walkTo(-.6, 'floor'),
        walkTo(8.4, 'floor'), fight(0), walkTo(2, 'floor'), fight(1), walkTo(3.1, 'floor'), fight(2),
        walkTo(37, 'east'), useDoor,
        walkTo(4, 'floor'), walkTo(9.1, 'floor'), fight(0), walkTo(4, 'floor'), fight(1),
        walkTo(13.6, 'floor'), fight(2), walkTo(18.8, 'floor'),
        jumpTo(21, 'tread_1'), jumpTo(24.2, 'tread_2'), jumpTo(27.6, 'balcony'), walkTo(37, 'balcony'), useDoor,
        jumpTo(4.7, 'west_gallery_0'), sew(0), jumpTo(6.4, 'velvet_04_arena:seam_0'),
        walkTo(8.3, 'west_gallery_1'), walkTo(18.3, 'west_gallery_1'), sew(1), jumpTo(20, 'velvet_04_arena:seam_1'),
        walkTo(21.9, 'west_gallery_2'), walkTo(32.3, 'west_gallery_2'), sew(2), jumpTo(34, 'velvet_04_arena:seam_2'),
        walkTo(35.9, 'west_gallery_3'), jumpTo(39.7, 'pennants_turn'), jumpTo(39.8, 'pennants_6'),
        walkTo(38.3, 'pennants_6'), sew(3), jumpTo(36.6, 'velvet_04_arena:seam_3'),
        walkTo(34.7, 'pennants_5')
    ];
    let lastProgress = 0, sample = {...pos}, lastState = '', nextProbe = firstProbe;
    let lastSpanAxis = 0;
    for (; now() <= (probe ? input.districtDeadline : 600); tick++) {
        if (now() >= nextProbe) {
            const state = [room.id, uses, area.RouteTarget?.id, area.RouteTarget?.State.Remaining].join('/');
            if (state !== lastState || Math.hypot(pos.x - sample.x, pos.y - sample.y) >= .5) {
                sample = {...pos}; lastProgress = now(); lastState = state;
            }
            nextProbe = now() + .25;
            if (now() - lastProgress > input.idle) { result.stop = 'no progress'; break; }
            if (probe || capturedProbe) {
                const cleared = [...guards, ...entryWave].filter(g => g.State.Cleared).length;
                const contacts = (probe || capturedProbe).result.contacts.length;
                if (contacts > lastContacts || lastWaveRoom === room.id && cleared > lastCleared) districtProgress = now();
                lastContacts = contacts; lastCleared = cleared; lastWaveRoom = room.id;
                if (now() - districtProgress >= input.districtIdle) { result.stop = 'district idle'; break; }
            }
        }
        let callRow;
        previous = down; down = new Set();
        if (now() < env.jumpUntil) keys.Hold('Space');
        if (swingProbe) {
            if (tick === 0) keys.Tap('J');
            if (mode === 'heldAttack') keys.Hold('D');
        }
        else if (cornerJump) {
            const step = room.edges.silt_bed.find(e => e.to === 'inspection_dock');
            if (sides.some(c => c.otherCollider === plank)) firstCornerContact ??= now();
            if (firstCornerContact != null && now() - firstCornerContact >= env.BlockedWalkSeconds) cornerJumpAt ??= now();
            if (cornerJumpAt != null && now() - cornerJumpAt < input.stepJumpHold) keys.Hold('Space');
            if (Math.abs(step.land - pos.x) > aimTolerance) keys.Hold(step.land > pos.x ? 'D' : 'A');
        }
        else if (mode === 'manualAfterInvestigation') {
            if (manualAfterInvestigation[instruction]()) { instruction++; began = tick + 1; }
        }
        else if (capturedProbe) capturedProbe.drive(now() + frameDt);
        else if (mode === 'manual') {
            if (manualRoute[instruction]()) { instruction++; began = tick + 1; }
        }
        else if (mode === 'manualLoom' && instruction < manualFromLoom.length) {
            if (manualFromLoom[instruction]()) { instruction++; began = tick + 1; }
        }
        else if (uses >= silver.stage3At && !flags.has('returned_for_mid_invest')) {
            const row = input.transferRows.find(r => !flags.has(r.completionFlag) && r.entryCondition.every(flag =>
                flag === 'transferred' || (flag.startsWith('!') ? !flags.has(flag.slice(1)) : flags.has(flag))));
            if (!area.OnReturnFloor) env.SeekFloor();
            else if (row.requiredPlayerAction.includes('confirm')) {
                keys.Tap('Return');
                if (keys.GetKeyDown('Return')) {
                    flags.add(row.completionFlag);
                    if (probe && row.resumePoint === 'current_room_safe_floor')
                        result.returnCheckpoint = [room.id, nearestFloor(env).id];
                }
            }
            else if (row.requiredPlayerAction.includes('use:return_call')) {
                callRow = row;
                if (mode === 'manualLoom') keys.Tap('F');
                else transfer(env);
            }
        }
        else if (probe) {
            if (probe.drive(now() + frameDt)) { result.stop = 'district complete'; break; }
        }
        else if (mode === 'allSeams' && !room.singleSeams && env.SewReturnSeam && room.roster.length) {
            if (env.sewingRoom !== room.id) {
                env.sewingRoom = room.id;
                env.seamIndex = 0;
                env.lastSewn = null;
            }
            if (env.seamIndex < room.stitchAnchors) env.SewReturnSeam(room, area.RouteTarget, false);
            else if (env.ReturnBankReady(room)) env.TakeReturnDoor();
        }
        else gather(env);
        if (tick === 0) result.firstKeys = [...down];
        if (afterInvestigation && player.Grounded && sides.some(c => c.otherCollider === plank && Math.abs(c.normal.x) >= .9)) {
            result.lip.contacts++;
        }
        if (afterInvestigation && support?.name === 'silt_bed' && keys.GetKeyDown('Space'))
            result.lip.takeoffs.push(round(pos.x));
        effects();
        const previousSupport = support?.name;
        if (room.id === input.loom.room && uses === input.loom.silverUses && support === seams[seams.length - 1]) {
            const trace = result.loomMotion ??= {minX: pos.x, maxX: pos.x, left: 0, right: 0, idle: 0, swings: 0, jumps: 0, sides: 0, turns: 0, firstTurns: []};
            const axis = +down.has('D') - +down.has('A');
            trace.minX = Math.min(trace.minX, pos.x);
            trace.maxX = Math.max(trace.maxX, pos.x);
            trace[axis < 0 ? 'left' : axis > 0 ? 'right' : 'idle']++;
            if (player.Attacking) trace.swings++;
            if (down.has('Space')) trace.jumps++;
            if (sides.length) trace.sides++;
            if (axis && lastSpanAxis !== axis) {
                if (lastSpanAxis) trace.turns++;
                if (trace.firstTurns.length < 4)
                    trace.firstTurns.push({tick, x: round(pos.x), axis, from: deck[env.stepFrom]?.id, step: env.step});
                lastSpanAxis = axis;
            }
        }
        if (keys.GetKeyDown('Q') || now() < defenseUntil) jumpAt = -Infinity;
        else if (keys.GetKeyDown('Space')) { jumpAt = now(); cutJump = false; }
        if (!down.has('Space') && previous.has('Space')) cutJump = true;
        while ((fixedTick + 1) * dt <= (tick + 1) * frameDt + 1e-9) {
            physics();
            fixedTick++;
        }
        if (afterInvestigation) {
            result.lip.peakFeet = Math.max(result.lip.peakFeet, pos.y);
            if (cornerJumpAt != null && now() > cornerJumpAt && player.Grounded) { result.stop = 'first landing'; break; }
        }
        if (swingProbe) {
            result.swingTrace.push({tick, x: round(pos.x), y: round(pos.y), vx: round(vel.x), support: support?.name});
            if ((tick + 1) * dt >= swing[0]) { result.stop = 'swing complete'; break; }
        }
        if (loomStart && !result.bank && support && area.OnReturnFloor)
            result.bank = {tick, face: support.name, feet: [round(pos.x), round(pos.y)]};
        if (room.id === fixture.room) {
            if (previousSupport !== support?.name && support)
                result.waterLandings.push({tick, face: support.name, x: round(pos.x), y: round(pos.y)});
            const mark = room.floors.find(f => f.contactSave);
            if (area.OnReturnFloor && support.id === mark.surface && Math.abs(pos.x - mark.x) <= .8) {
                flags.add('scene_floor:velvet:' + mark.id);
                if (!result.waterSave) result.waterSave = {tick, x: round(pos.x), y: round(pos.y), floor: mark.id};
            }
        }
        if (probe) {
            if (env.NeedsEntryWave && entryWave[entryMember].State.Cleared && entryWave[entryMember].State.LastSword?.Closed) {
                if (++entryMember === entryWave.length) flags.add('velvet_entry_wave_cleared');
            }
            if (env.HasBridge && env.latches.length > 0 && env.latches.every(g => g.State.Cleared)) flags.add(env.BridgeFlag);
            const mark = room.floors.find(f => f.contactSave);
            if (area.StandingOn(mark.surface) && Math.abs(pos.x - mark.x) <= .8)
                flags.add('scene_floor:velvet:' + mark.id);
            if (room.id === 'velvet_04_arena' && probe.result.contacts.length === 4 && !flags.has('mikhail_i0_complete')) {
                flags.add('mikhail_defeated'); flags.add(input.boutExit.completionFlag);
                enter(input.resume[input.boutExit.resumePoint], input.boutExit.resumePoint);
                continue;
            }
        }
        if (keys.GetKeyDown('F') && area.OnReturnFloor) {
            if (callRow) {
                flags.add(callRow.completionFlag);
                result.safeCall = mode === 'manualLoom' || env.sawSafeCall;
                result.transferFlags = input.transferRows.map(r => r.completionFlag).filter(f => flags.has(f));
                result.totals.push([room.id, uses]);
                result.destination = [input.resume[callRow.resumePoint], callRow.resumePoint];
                if (probe) {
                    result.rooms.push(result.destination[0]);
                    for (const flag of input.district.flags) flags.add(flag);
                    enter(...result.returnCheckpoint);
                    continue;
                } else { result.stop = 'first return'; break; }
            }
            const door = room.exits.filter(d => gate(d) && d.InReach(pos))
                .sort((a, b) => Math.abs(a.x - pos.x) - Math.abs(b.x - pos.x))[0];
            if (door) {
                result.departures.push({room: room.id, to: door.room, support: support.name,
                    feet: [round(pos.x), round(pos.y)], guards: guards.filter(g => g.State.NeedsSetup).map(g => [g.id, g.State.Remaining])});
                result.totals.push([room.id, uses]);
                if (room.id === fixture.room) result.waterExit = door.room;
                if (waterStart || afterInvestigation) {
                    result.destination = [door.room, door.destinationFloor];
                    result.stop = 'waterway exit';
                    break;
                }
                enter(door.room, door.destinationFloor);
            }
        }
        if (player.Health.Dead || pos.y < room.fallLine) { result.stop = player.Health.Dead ? 'death' : 'fall'; break; }
    }
    result.stop ??= 'step deadline';
    result.end = {tick, fixedTick, frameSeconds: frameDt, room: room.id, uses, hp: player.Health.Current, feet: [round(pos.x), round(pos.y)], support: support?.name,
        target: area.RouteTarget?.id, segments: area.RouteTarget?.State.Remaining, seam: env.seamIndex, keys: [...down],
        activations: serial, idleSeconds: round(now() - lastProgress), step: env.step, lastSewn: env.lastSewn,
        manualInstruction: mode === 'manualLoom' ? instruction : undefined};
    return result;
}

const modes = ['recovery', 'manual', 'campaign', 'loom', 'loomCrossing', 'manualLoom', 'allSeams', 'heldAttack', 'stillAttack',
    'afterInvestigation', 'manualAfterInvestigation', 'cornerJump', 'unreachableLip', 'district'];
const runs = Object.fromEntries(modes.map(k => [k, simulate(k)]));
runs.campaign60 = simulate('campaign', 1 / 60);
runs.campaignOffset = simulate('campaign', dt, dt);
process.stdout.write(JSON.stringify(runs));
