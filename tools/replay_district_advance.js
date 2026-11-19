const fs = require("node:fs");
const input = JSON.parse(fs.readFileSync(0, "utf8"));

function block(source, signature) {
    const start = source.indexOf(signature);
    if (start < 0) throw Error("Missing replay entry " + signature);
    const open = source.indexOf("{", start);
    let depth = 1;
    for (let i = open + 1; i < source.length; i++) {
        if (source[i] === "{") depth++;
        if (source[i] === "}" && --depth === 0) return source.slice(open + 1, i);
    }
    throw Error("Unclosed replay entry " + signature);
}

function decision(source) {
    const body = source.replace(/\/\/[^\n]*/g, "")
        .replace(/foreach \(var (\w+) in ([^)]+)\)/g, "for (const $1 of $2)")
        .replace(/\b(?:float|bool|int|StorySurface) (\w+)/g, "let $1")
        .replace(/(\d*\.\d+|\d+)f\b/g, "$1")
        .replace(/out (?:var )?\w+/g, "out")
        .replace(/approachingLaunch: /g, "")
        .replace(/\.GetComponent<(\w+)>\(\)/g, '.GetComponent("$1")')
        .replace(/ is WaveTarget2D target/g, " != null")
        .replace(/\.StartsWith\(/g, ".startsWith(")
        .replace(/Array\.Find\(/g, "find(")
        .replace(/Array\.FindIndex\(/g, "findIndex(");
    return new Function("env", "with (env) {" + body + "}");
}

const src = input.sources;
const fixedFaceAt = decision(block(src.ClockTraversal, "StorySurface FixedFaceAt("));
const clockPosition = decision(block(src.ClockTraversal, "bool ClockPosition("));
const gate = decision(block(src.RepresentativeRoomLayout, "bool Gate("));
const advance = decision(block(src.DistrictRoomLayout, 'if (token == "clear:wave" ||'));
const drivers = new Map();
for (const stage of input.stages) {
    const name = stage[0].toUpperCase() + stage.slice(1);
    const probe = src[name + "DistrictProbe"];
    const dispatch = probe.match(/if \(action\.StartsWith\("advance:"[^;]+;/);
    const traversal = decision(block(src[name + "Traversal"], "bool " + name + "Door("));
    drivers.set(stage, {name, dispatch: decision(dispatch?.[0] ?? ""), door: decision(block(probe, "bool Door(")), traversal});
}

function frame(sample, feet, fixed, paints, position, manual = false, grounded = fixed) {
    const {room, row, flags} = sample;
    const driver = drivers.get(room.stage);
    const actions = row.requiredPlayerAction;
    const area = {Room: room, ActiveRow: row, OnReturnFloor: fixed, DistrictWaveTarget: null};
    const player = {Grounded: grounded, Body: {position: feet}, GetComponent: () => ({bounds: {extents: {x: input.radius}}})};
    let selected = null, plan = null, travel = null, receipt = null, f = manual;
    const env = {
        Area: area, Player: player, area, Room: room, ActiveRow: row, player,
        Mathf: {Max: Math.max, Abs: Math.abs}, SkillGeometry: {SupportTolerance: input.supportTolerance},
        FixedSurfaceRoute: {AimTolerance: input.aimTolerance}, PaintSurface2D: {All: paints},
        out: null, face: null, dest: row.alternateRoom, destination: row.alternateRoom,
        action: actions[0], token: actions[0], StringComparison: {Ordinal: undefined},
        Window: "gallery_10_sanctuary_window", Terrace: "gallery_09_shortcut", Table: "bell_10_sanctuary_table",
        surveyed: {Contains: () => false}, find: (items, predicate) => items.find(predicate),
        Has: id => flags.includes(id), string: {IsNullOrEmpty: value => !value},
        Safe: grounded && sample.safe !== false, RoofDoorOpen: sample.roofOpen,
        Record: {Get: id => ({acquired: sample.skills.includes(id)})}, SkillRules: {SilverStitch: "silver_stitch"},
        roomTargets: {TrueForAll: predicate => sample.targets.every(t => predicate({State: {Cleared: t}}))},
        get InspectPressed() { return f; }, KeyCode: {F: "F"}, keys: {Tap: key => { f = key === "F"; }},
        GameInput: {get InvestigateDown() { return f; }},
        Put: flag => { receipt = flag; }, script: {Room: id => id},
        Travel: (room, floor) => { travel = {room, floor}; }
    };
    for (const door of room.exits) {
        const r = door.approach;
        door.InReach = p => r.x <= p.x && p.x <= r.x + r.width && r.y <= p.y && p.y <= r.y + r.height;
        r.Defined = r.width > 0 && r.height > 0;
    }
    room.Surface = id => room.terrain.find(s => s.id === id);
    env.terrain = room.terrain;
    env.ReadTerrain = () => {};
    env.FixedSurfaceRoute.IsFloor = s => !s.wallJump && !s.rampRise && !s.travelX && !s.travelY;
    env.TryFaceAt = (x, y) => {
        env.face = fixedFaceAt({...env, x, y});
        return !!env.face;
    };
    area.ProbeSurfaceBounds = s => bounds(s.rect);
    env.ClockPosition = env.OperaPosition = (face, x, tolerance = input.doorTolerance) => {
        plan = {face, x, tolerance};
        return position(plan, env);
    };
    env.Gate = door => gate({...env, door});
    env.bot = {Area: area, [driver.name + "Door"]: door => {
        selected = door.room;
        env.door = door;
        return driver.traversal(env);
    }};
    env.Door = (bot, keys, dest) => driver.door({...env, bot, keys, dest, destination: dest});
    if (!manual) driver.dispatch(env);
    advance(env);
    return {selected, plan, f, receipt, travel};
}

function bounds(r) {
    return {min: {x: r.x, y: r.y}, max: {x: r.x + r.width, y: r.y + r.height}};
}

function paintReplay(sample) {
    let {x, y} = sample.feet;
    const {brush, floor} = sample;
    let occupied = sample.paint !== "none", support = occupied ? "paint" : floor.id;
    let vx = 0, vy = 0, first, dry = support === floor.id ? 0 : null, released = null;
    const {speed, gravity, dt, brake, radius} = input;
    let dryFeet = dry === 0 ? [x, y] : null;
    for (let tick = 0; tick <= Math.ceil(input.idle / dt); tick++) {
        const fixed = support === floor.id;
        const paints = occupied ? [{Wall: false, Live: tick * dt < sample.life, Bounds: brush}] : [];
        let axis = 0;
        const lookups = [];
        let nextCalls = 0;
        const observation = frame(sample, {x, y}, fixed, paints, (plan, env) => {
            env.Area.StandingOnRamp = env.Area.RidingSurface = () => false;
            const step = {};
            const route = {Next(from, goal, next) {
                nextCalls++;
                const firstStep = input.graphs[sample.layout][env.terrain[from].id];
                if (!firstStep || env.terrain[goal].id !== floor.id) return false;
                Object.assign(next, firstStep);
                return true;
            }};
            return clockPosition({...env, face: plan.face, x: plan.x, tolerance: plan.tolerance,
                Time: {time: tick * dt}, jumpUntil: 0, jumpingFrom: null, stepFrom: -1, stepGoal: -1,
                route, step, out: step, ClearStep() {},
                FailRoute(reason) { throw Error(reason); },
                OnSurface: s => fixed && s.id === support,
                findIndex(items, predicate) {
                    const index = items.findIndex(predicate);
                    lookups.push(items[index]?.id ?? null);
                    return index;
                },
                Walk(aim, tolerance) {
                    if (Math.abs(aim - x) <= tolerance) return true;
                    axis = Math.sign(aim - x);
                    return false;
                }
            });
        }, sample.manual, support !== null);
        if (!first) first = {...observation, axis, fixedFaceLookups: lookups, nextCalls};
        if (sample.manual || observation.travel || tick === Math.ceil(input.idle / dt)) {
            return {first, dryTick: dry, dryFeet, paintRemovedTick: released, tick, feet: [x, y], support, ...observation};
        }
        vx = support ? axis * speed : Math.sign(vx) * Math.max(0, Math.abs(vx) - brake * dt);
        x += vx * dt;
        const previous = y;
        vy -= gravity * dt;
        y += vy * dt;
        support = null;
        const land = (left, right, top, name) => {
            if (x + radius > left && x - radius < right && vy <= 0 && previous >= top && y <= top) {
                y = top; vy = 0; support = name;
            }
        };
        if (occupied) land(brush.min.x, brush.max.x, brush.max.y, "paint");
        land(floor.rect.x, floor.rect.x + floor.rect.width, sample.top, floor.id);
        if (support === floor.id && dry === null) {
            dry = tick + 1;
            dryFeet = [x, y];
        }
        const slop = input.supportTolerance;
        if (occupied && (tick + 1) * dt >= sample.life &&
            (x < brush.min.x - slop || x > brush.max.x + slop || Math.abs(y - brush.max.y) > slop)) {
            occupied = false;
            released = tick + 1;
        }
    }
}

const result = {};
for (const [name, sample] of Object.entries(input.cases)) {
    if (sample.floor) result[name] = paintReplay(sample);
    else {
        result[name] = frame(sample, sample.feet, sample.fixed, [], plan =>
            plan.face === sample.support && Math.abs(plan.x - sample.feet.x) <= plan.tolerance);
    }
}
process.stdout.write(JSON.stringify(result));
