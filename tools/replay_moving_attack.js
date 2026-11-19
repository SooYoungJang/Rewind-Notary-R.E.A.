const fs = require("node:fs");
const path = require("node:path");

const project = path.resolve(__dirname, "..");
const read = name => fs.readFileSync(path.join(project, name), "utf8");
const motor = process.argv[2] ? fs.readFileSync(process.argv[2], "utf8") : read("Assets/Scripts/CombatMotor2D.cs");
const controller = read("Assets/Scripts/PlayerController2D.cs");
const runSpeed = Number(controller.match(/\bMoveSpeed = ([\d.]+)f/)[1]);
const dt = Number(read("ProjectSettings/TimeManager.asset").match(/Fixed Timestep: ([\d.]+)/)[1]);
const duration = Number(motor.match(/AttackSpec\[\] PlayerCombo\s*=\s*\{\s*new AttackSpec\(([\d.]+)f/)[1]);
const sword = JSON.parse(read("Assets/Resources/Skills/catalog.json")).skills.find(skill => skill.skillId === "basic_sword");

function method(source, name) {
    const signature = new RegExp("^        (?:public|private|internal) \\w+ " + name +
        "\\([^\\n]*\\)\\s*\\{([\\s\\S]*?)\\n        \\}", "m");
    return source.match(signature)[1]
        .replace(/\/\/[^\n]*/g, "")
        .replace(/\b(?:float|bool) (\w+)/g, "let $1")
        .replace(/(\d)f\b/g, "$1")
        .replace(/GetComponent<Collider2D>\(\)/g, "GetComponent()")
        .replace(/ \+ Vector2\.right \* carrierSpeed/g, ".onCarrier(carrierSpeed)");
}

const move = new Function("state", "speed", "with (state) {" + method(motor, "Move") + "}");

class Vector2 {
    constructor(x, y) { this.x = x; this.y = y; }
    onCarrier(vx) { return new Vector2(this.x + vx, this.y); }
}

function replay(axis, profile, isPlayer = true) {
    const state = {
        Health: {IsPlayer: isPlayer, Dead: false}, Grounded: true, Stunned: false,
        Temporal: null, Defense: null, BenefactorSkills: null, Attacking: true,
        chainProfile: profile, attackStartedAt: 0, Facing: 1, carrierSpeed: 0,
        Time: {time: 0, fixedDeltaTime: dt}, Body: {linearVelocity: new Vector2(axis(0) * runSpeed, 0)},
        Mathf: {Abs: Math.abs}, Vector2, Face(speed) { if (Math.abs(speed) >= .01) state.Facing = Math.sign(speed); }
    };
    const trace = [];
    for (let tick = 0; tick * dt < duration; tick++) {
        state.Time.time = tick * dt;
        move(state, axis(tick) * runSpeed);
        trace.push(state.Body.linearVelocity.x);
    }
    return trace;
}

const stage2 = sword.stages.find(profile => profile.stage === 2);
const forward = () => 1;
const traces = {
    noProfile: replay(forward, null),
    stage1: replay(forward, sword.stages.find(profile => profile.stage === 1)),
    stage2: replay(forward, stage2),
    reverse: replay(tick => tick * dt < duration / 2 ? 1 : -1, stage2),
    analog: replay(() => -.5, stage2),
    stationary: replay(() => 0, stage2),
    release: replay(tick => tick < 2 ? 1 : 0, stage2),
    nonPlayer: replay(forward, null, false)
};

const probe = read("Assets/Scripts/AutoTest/FeelProbe.cs");
const drive = new Function("state", "with (state) {" + method(probe, "Drive") + "}");
const stop = new Function("state", "with (state) {" + method(probe, "SampleStop") + "}");
const sample = new Function("state", "with (state) {" + method(probe, "SampleMovingAttack") + "}");
const physics = new Function("state", "return (function* () {with (state) {" +
    method(probe, "Start").replace("yield return ", "yield ") + "}})()");
const abort = new Function("state", "reason", "with (state) {" + method(probe, "Abort") + "}");
const atRunSpeed = new Function("vx", "PlayerController2D", "return " +
    probe.match(/bool AtRunSpeed\(float vx\) => ([^;]+);/)[1].replace(/(\d)f\b/g, "$1"));
const phaseNames = probe.match(/enum Phase \{ ([^}]+) \}/)[1].split(", ");
const Phase = Object.fromEntries(phaseNames.map(name => [name, name]));

function observe(speeds, lostContact = -1) {
    const reading = {status: "skipped", physicsTicks: 0, minSpeed: 0};
    const player = {Grounded: false, Health: {Dead: false}, Attacking: false, Body: {linearVelocity: {x: 0}},
        GetComponent: () => ({IsTouchingLayers: () => false})};
    const held = new Set();
    const state = {
        Phase, phase: Phase.AirStop, get Passed() { return this.phase === Phase.Done; }, GameInput: {CanPlay: true}, player,
        result: {groundStop: {status: "pass"}, airStop: {status: "running", physicsTicks: 0, seconds: 0},
            stationaryAttack: {status: "pass"}, movingAttack: reading},
        KeyCode: {D: "D", J: "J"}, keys: {Hold: key => held.add(key), Tap: key => held.add(key)},
        PlayerController2D: {MoveSpeed: runSpeed}, Mathf: {Abs: Math.abs, Min: Math.min}, LevelBuilder: {GroundLayer: 8},
        Time: {fixedDeltaTime: dt}, WaitForFixedUpdate: class {}, OnAttackStarted: null,
        SampleStop: () => stop(state), SampleMovingAttack: () => sample(state),
        AtRunSpeed: vx => atRunSpeed(vx, state.PlayerController2D),
        Abort: reason => abort(state, reason)
    };
    const postPhysics = physics(state);
    postPhysics.next();
    postPhysics.next();
    player.Grounded = true;
    drive(state);
    if (!held.has("D") || held.has("J")) throw Error("Expected run-up before the moving attack");
    held.clear();
    player.Body.linearVelocity.x = runSpeed;
    drive(state);
    player.Attacking = held.has("J") && held.has("D");
    for (const [tick, vx] of speeds.entries()) {
        if (!player.Attacking || state.phase !== Phase.MovingAttack) break;
        held.clear();
        drive(state);
        if (!held.has("D") || held.has("J")) throw Error("Expected sustained forward input and a single attack");
        player.Body.linearVelocity.x = vx;
        player.GetComponent = () => ({IsTouchingLayers: () => tick !== lostContact});
        postPhysics.next();
    }
    player.Attacking = false;
    if (state.phase === Phase.MovingAttack) drive(state);
    return reading;
}

const threshold = runSpeed * .8;
const readings = {
    forward: observe(traces.noProfile), threshold: observe([threshold]),
    lateDrop: observe([runSpeed, runSpeed, threshold - .01]), backward: observe([-runSpeed]),
    empty: observe([]), lostFloor: observe([runSpeed, runSpeed], 1)
};
console.log(JSON.stringify({runSpeed, dt, duration, stepSeconds: stage2.stepSeconds, stepSpeed: stage2.stepSpeed, traces, readings}));
