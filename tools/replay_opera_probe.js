const fs = require("node:fs");
const input = JSON.parse(fs.readFileSync(0, "utf8"));

function method(source, signature, args, scope) {
    const start = source.indexOf(signature);
    if (start < 0) throw Error("Missing method " + signature);
    const open = source.indexOf("{", start);
    let end = open + 1, depth = 1;
    while (depth && end < source.length) {
        if (source[end] === "{") depth++;
        if (source[end++] === "}") depth--;
    }
    const code = source.slice(open + 1, end - 1)
                     .replace(/\/\/[^\n]*/g, "")
                     .replace(/new OperaContact\s*\{([^}]+)\}/g,
                         (_, fields) => "{" + fields.replace(/(\w+)\s*=(?!=)/g, "$1:") + "}")
                     .replace(/\b(?:bool|string|int|double|float|var) (?=\w+\s*=)/g, "let ")
                     .replace(/(\d*\.?\d+)f\b/g, "$1")
                     .replace(/out _/g, "null")
                     .replace(/, StringComparison\.Ordinal/g, "")
                     .replace(/\.GetComponent<(\w+)>\(\)/g, '.GetComponent("$1")')
                     .replace(/\.StartsWith\(/g, ".startsWith(")
                     .replace(/\.EndsWith\(/g, ".endsWith(")
                     .replace(/\.IndexOf\(/g, ".indexOf(")
                     .replace(/\.Substring\(/g, ".substring(")
                     .replace(/\.Length\b/g, ".length")
                     .replace(/Array\.Find\(([^,]+), /g, "$1.find(")
                     .replace(/Array\.FindIndex\(([^,]+), /g, "$1.findIndex(")
                     .replace(/Array\.indexOf\(([^,]+), /g, "$1.indexOf(")
                     .replace(/new InvalidOperationException\(/g, "new Error(");
    return new Function("scope", "return function(" + args + ") { with (scope) {" + code + "} }")(scope);
}

function collection() {
    const values = [];
    values.Contains = item => values.includes(item);
    values.Add = item => {
        if (values.includes(item)) return false;
        values.push(item);
        return true;
    };
    Object.defineProperty(values, "Count", {get : () => values.length});
    return values;
}

function replay() {
    const source = input.scripts.OperaDistrictProbe;
    const rooms = Object.fromEntries([...input.opera.rooms, ...input.externalRooms ].map(r => [r.id, r]));
    const scope = {
        Mathf : {Abs : Math.abs},
        KeyCode : {F : "F"},
        ShellPage : {Playing : "Playing", StoryDialogue : "StoryDialogue"},
        SkillRules : {SilverStitch : "silver_stitch"},
        BenefactorIds : {Waltz : "curtain_call_waltz"},
        PlaytestStep : {},
        GameInput : {CanPlay : true, InvestigateDown : false},
        Time : {frameCount : 0},
        string : {Join : (separator, ...values) => values.flat().join(separator)},
        publish() {},
        started : 0,
        lastContact : 0,
        lastSample : 0,
        roomEntered : 0,
        outside : 0,
        previousOpera : false,
        previousRoom : null,
        rodeInShade : false,
        station : 0,
        leg : -1,
        closedFrame : -1,
        longStart : 0,
        dropStart : 0,
        surveyed : collection(),
        stations : collection(),
        uses : collection(),
        TerrainWidth : {
            Measure(area, widths) {
                widths.push(...area.Room.terrain);
            }
        }
    };
    for (const match of source.matchAll(/\b(\w+) = "(opera_\d+_[\w]+)"/g))
        scope[match[1]] = match[2];
    for (const key of ["Route", "Comparison"]) {
        const names = source.match(new RegExp(key + " = \\{ ([^}]+) \\}"))[1];
        scope[key] = names.split(", ").map(name => scope[name]);
    }
    for (const [name, args] of Object.entries({
             Drive : "bot,keys,now",
             Contact : "bot,now,playing",
             Observe : "bot,now",
             Compare : "bot,keys,now",
             Door : "bot,keys,dest",
             WorkStation : "bot,keys",
             ObserveUse : "e,room"
         })) {
        scope[name] = method(source, " " + name + "(", args, scope);
    }
    const result = scope.result = {
        slot : 4,
        contacts : collection(),
        widths : [],
        stationTargets : collection(),
        longRooms : collection(),
        dropRooms : collection(),
        longSeconds : 0,
        shortcutSeconds : 0,
        combatUses : 0
    };
    const flags = collection(), owned = new Set();
    const visits = [], actions = [], seams = [], gates = [], kills = [];
    const actor = {
        Body : {position : {x : 0, y : 0}, linearVelocity : {x : 0}},
        Facing : 1,
        Grounded : true,
        Health : {Dead : false}
    };
    let support, row, index, targets, now = 1, nextDoor, shot, count = 0, installed, mounted = false;
    let scene = "Playing", receipt, motion = 0, transfer = false;
    const savedFloor = new Map();
    const record = {storyFlags : flags, Get : id => ({acquired : owned.has(id)}), firstTransferPending : false};
    const area = {
        Rea : actor,
        OnReturnFloor : true,
        get ActiveRow() {
            return row;
        },
        get PendingAction() {
            return scene === "Playing" && row ? row.requiredPlayerAction[index] : null;
        },
        get DistrictWaveTarget() {
            return targets[0];
        },
        get BoutOpponent() {
            return this.PendingAction === "win:valeria" ? {} : null;
        },
        get ChandelierShaded() {
            return mounted;
        },
        RidingSurface : () => mounted,
        StandingOn : id => id === support,
        OnOperaLift : () => false
    };
    for (const room of Object.values(rooms)) {
        room.Surface = id => room.terrain?.find(s => s.id === id);
        room.Point = id => room.points?.find(p => p.id === id);
        room.Floor = id => room.floors.find(f => f.id === id);
        for (const d of room.exits) {
            d.InReach = pos => {
                const r = d.approach;
                return pos.x >= r.x && pos.x <= r.x + r.width && pos.y >= r.y && pos.y <= r.y + r.height;
            };
        }
    }
    function land(face, x) {
        const r = area.Room.Surface(face).rect;
        if (x < r.x - .01 || x > r.x + r.width + .01) throw Error("Off " + face + " at " + x);
        support = face;
        actor.Body.position = {x, y : r.y + r.height};
        motion++;
    }
    function enter(id, floorId) {
        area.Room = rooms[id];
        visits.push(id);
        row = null;
        nextDoor = null;
        installed = null;
        mounted = false;
        const floor = area.Room.Floor(floorId) || area.Room.floors[0];
        if (area.Room.stage === "opera")
            land(floor.surface, floor.x);
        else {
            support = floor.surface;
            actor.Body.position = {x : floor.x, y : 0};
        }
        receipt = savedFloor.get(area.Room.stage);
        save(floor.id);
        targets = (area.Room.roster || [])
                      .flatMap(g => g.placementIds.map(
                                   id => ({...area.Room.placements.find(p => p.id === id), group : g})));
    }
    function save(floor) {
        const prefix = "scene_floor:" + area.Room.stage + ":";
        if (receipt) {
            const i = flags.indexOf(prefix + receipt);
            if (i >= 0) flags.splice(i, 1);
        }
        receipt = floor;
        savedFloor.set(area.Room.stage, floor);
        flags.Add(prefix + floor);
    }
    function position(face, x) {
        if (support === face) {
            if (Math.abs(x - actor.Body.position.x) < .10) return true;
            now += Math.abs(x - actor.Body.position.x) / input.speed;
            land(face, x);
            return false;
        }
        const graph = input.paths[area.Room.id];
        if (mounted) {
            mounted = false;
            const dest = face.startsWith("west") ? "west_gallery" : "east_gallery";
            land(dest, dest === "west_gallery" ? 16.25 : 31.25);
            return false;
        }
        const step = graph[support]?.[face];
        if (step) {
            now += step.seconds;
            land(step.face, step.x);
            return false;
        }
        if (area.Room.id === scope.Chandelier && support.startsWith("west") !== face.startsWith("west")) {
            const east = !face.startsWith("west");
            now += input.span[east ? "east" : "west"];
            mounted = true;
            const r = area.Room.Surface("pendant").rect;
            land("pendant", r.x + r.width / 2);
            return false;
        }
        throw Error("Unreachable " + area.Room.id + " " + support + " -> " + face);
    }
    function fixedFace(x, y) {
        return area.Room.terrain
            .filter(
                s => !s.travelX && !s.travelY && !s.rampRise && x >= s.rect.x && x <= s.rect.x + s.rect.width)
            .sort((a, b) => Math.abs(a.rect.y + a.rect.height - y) - Math.abs(b.rect.y + b.rect.height - y))[0];
    }
    function advance() {
        if (++index < row.requiredPlayerAction.length) return;
        flags.Add(row.completionFlag);
        if (row.effect === "defeat") owned.add("curtain_call_waltz");
        const dest = Object.values(rooms).find(r => r.Floor(row.resumePoint));
        const floor = row.resumePoint;
        row = null;
        if (dest && dest !== area.Room) enter(dest.id, floor);
    }
    function validUse(target) {
        scope.ObserveUse({
            kind : "valid_use",
            skillId : "curtain_call_waltz",
            targetId : target,
            useId : "cast-" + (++count),
            after : count
        },
            area.Room.id);
        if (count === input.mastery && area.Room.id === scope.Lower) transfer = true;
    }
    const bot = {
        Area : area,
        Record : record,
        Session : {ActiveSlot : 4, LastSaveSucceeded : true},
        Shell : {
            get Page() {
                return scene;
            }
        },
        TryDistrictHeal : () => false,
        OperaPosition : position,
        ClockPosition : position,
        FightBoss() {
            advance();
        },
        Drive() {
            if (scene === "StoryDialogue") {
                scene = "Playing";
                if (row.requiredPlayerAction[index] === "confirm") advance();
                return;
            }
            const room = area.Room;
            if (room.stage === "opera") return;
            const checkpoints = {
                hub_attic : [ "sixth_bowl_stamped", "hub_forge" ],
                hub_forge : [ "intro_complete", "hub_lift" ],
                hub_workroom : [ "investigation_complete", "hub_sanctuary" ],
                hub_sanctuary : [ null, scope.Stairs ],
                hub_lift : [ null, "velvet_01_entry" ],
                velvet_01_entry : [ "velvet_entry_complete", "velvet_02_loom" ],
                velvet_02_loom : [ null, "velvet_06_shortcut" ],
                velvet_06_shortcut : [ null, "velvet_04_arena" ],
                velvet_04_arena : [ "mikhail_defeated", "velvet_05_return" ]
            };
            const [flag, dest] = checkpoints[room.id];
            if (flag) flags.Add(flag);
            if (room.id === "velvet_04_arena") owned.add("silver_stitch");
            nextDoor = room.exits.find(d => d.room === dest);
            if (!nextDoor) throw Error("Missing story link " + room.id + " -> " + dest);
            keys.Tap("F");
        },
        OperaFight(t) {
            const f = fixedFace(t.x, t.y);
            if (!position(f.id, t.x)) return;
            kills.push(area.Room.id + ":" + t.id);
            targets.shift();
            if ([ scope.Upper, scope.Lower ].includes(area.Room.id) &&
                t.group.setupSkill === "curtain_call_waltz")
                for (let i = 0; i < t.group.segments * t.group.setupHits; i++)
                    validUse(t.id + ":posture");
        },
        OperaDoor(d) {
            if (area.Room.stage !== "opera") {
                nextDoor = d;
                return true;
            }
            const f = fixedFace(d.x, d.approach.y + d.approach.height / 2);
            const reached = position(f.id, d.x) && d.InReach(actor.Body.position);
            if (reached) nextDoor = d;
            return reached;
        }
    };
    const keys = {
        Tap(k) {
            if (k === "F") scope.GameInput.InvestigateDown = true;
        },
        Cast(h = 0, v = 0) {
            shot = v === 1 ? "waltz" : "silver";
        }
    };
    const aimScope = {
        Player : actor,
        Area : area,
        Mathf : scope.Mathf,
        keys,
        ClockPosition : position,
        MoveTo(x, tol) {
            const dx = x - actor.Body.position.x;
            if (Math.abs(dx) <= tol) {
                actor.Body.linearVelocity.x = 0;
                return true;
            }
            actor.Facing = Math.sign(dx);
            land(
                support, actor.Body.position.x + actor.Facing * Math.min(Math.abs(dx), input.speed * input.dt));
            actor.Body.linearVelocity.x = actor.Facing * input.speed;
            return false;
        }
    };
    aimScope.AimCastFrom = method(input.scripts.FirstDepartureBot, " AimCastFrom(", "x,facing", aimScope);
    bot.SewStationSeam = method(input.scripts.FirstDepartureBot, " SewStationSeam(", "mark", aimScope);
    function eligible(value) {
        if (value.startsWith("!")) return !flags.Contains(value.slice(1));
        if (value.startsWith("owned:")) return owned.has(value.slice(6));
        return flags.Contains(value);
    }
    enter("hub_attic");
    let lastState = "", idle = 0;
    for (let frame = 0; frame < 60000; frame++) {
        now += input.dt;
        scope.Time.frameCount++;
        scope.GameInput.InvestigateDown = false;
        nextDoor = null;
        shot = null;
        const room = area.Room;
        if (!row) {
            row = input.opera.rows.find(r => r.room === room.id && !r.textKey.startsWith("trial.") &&
                                             !flags.Contains(r.completionFlag) &&
                                             r.entryCondition.every(eligible));
            if (row) {
                index = 0;
                scene = "StoryDialogue";
            }
        }
        const contact = room.floors.find(
            f => f.contactSave && f.surface === support && Math.abs(f.x - actor.Body.position.x) <= .8);
        if (contact && receipt !== contact.id) save(contact.id);
        const action = area.PendingAction;
        if (scope.Drive(bot, keys, now))
            return {complete: true, visits, actions, seams, gates, kills, result};
        if (transfer) {
            flags.Add("first_transfer_seen");
            enter("hub_workroom");
            transfer = false;
            continue;
        }
        if (action && room === area.Room && row) {
            if (action.startsWith("use:") && scope.GameInput.InvestigateDown)
                advance();
            else if (action.startsWith("ride:")) {
                const p = room.Point(action.slice(5));
                if (p && support === p.surface && Math.abs(actor.Body.position.x - p.x) < .25) advance();
            } else if (action.startsWith("latch:") && !targets.length)
                advance();
            else if (action === "clear:wave" && !targets.length &&
                     (row.requiredPlayerAction[0].startsWith("advance:") || actor.Body.position.x >= 35))
                advance();
        }
        if (room === area.Room && shot === "silver" && action === "sew:seam") {
            const at = actor.Body.position, seam = room.seamRuns[0], dx = seam.x - at.x;
            if (dx * actor.Facing >= 0 && Math.hypot(dx, seam.y - at.y) <= input.silver.reach &&
                at.y <= seam.y + input.anchorTop && at.y + 2 >= seam.y + input.anchorBottom &&
                room.singleSeams > 0 && .9 <= input.silver.maxSpan) {
                installed = 0;
                seams.push({id : room.seamId + "_0", support, x : at.x, y : at.y});
                actions.push(action);
                advance();
            }
        }
        if (room === area.Room && shot === "waltz") {
            const i = room.stationMarks?.findIndex(
                m => m.surface === support && Math.abs(m.x - actor.Body.position.x) <= .24);
            if (i >= 0 && (room.id !== scope.Seam || i === installed)) {
                const target = room.id + ":station_" + i;
                if (!scope.stations.Contains(target)) validUse(target);
                if (action?.startsWith("station:")) {
                    if (room.id === scope.Seam) actions.push(action);
                    advance();
                }
            }
        }
        if (room === area.Room && action === "cross:floor") {
            const mark = room.stationMarks[Math.max(0, installed ?? 0)];
            if (mark.surface === support && actor.Body.position.x > mark.x + 1.5) {
                if (room.id === scope.Seam) actions.push(action);
                advance();
            }
        }
        if (nextDoor && scope.GameInput.InvestigateDown) {
            const allowed = !nextDoor.gate || (nextDoor.gate === "depart" ? flags.Contains("intro_complete")
                                                  : nextDoor.gate === "synergy"
                                                      ? owned.has(room.skill) && owned.has("silver_stitch") &&
                                                            flags.Contains("investigation_complete")
                                                      : flags.Contains(nextDoor.gate));
            gates.push([ room.id, nextDoor.room, allowed ]);
            if (allowed) {
                const d = nextDoor, spanning = action?.startsWith("advance:") && !targets.length ? row : null;
                enter(d.room, d.destinationFloor);
                if (spanning) {
                    row = spanning;
                    index = 1;
                    if (room.id === scope.Seam) actions.push(action);
                }
            }
        }
        const state = [
            area.Room.id, row?.textKey, index, support, motion, targets.length, scene, scope.station,
            flags.length, result.contacts.length
        ].join("|");
        if (state === lastState)
            idle += input.dt;
        else
            idle = 0;
        if (idle > 18)
            return {
                complete : false,
                room : area.Room.id,
                action : area.PendingAction,
                support,
                visits,
                actions,
                seams,
                gates,
                result
            };
        lastState = state;
    }
    throw Error("Unfinished opera replay in " + area.Room.id + " " + area.PendingAction);
}

function shortcut(stage) {
    const room = input.shortcuts[stage], mark = room.stationMarks[0], seam = room.seamRuns[0];
    const stageName = stage === "bell" ? "Bell" : "Gallery", slot = stage === "bell" ? 5 : 6;
    const source = input.scripts[stageName + "DistrictProbe"], flags = collection(), receipts = [];
    const actor = {
        Facing : 1,
        Grounded : true,
        Health : {Dead : false},
        Body : {position : {x : 0, y : 0}, linearVelocity : {x : 0}}
    };
    const fixture = {
        TargetId : room.seamId + "_0",
        Installed : false,
        SupportingPlayer : false,
        transform : {position : seam}
    };
    const stitch = {All : {Find : predicate => [fixture].find(predicate)}};
    const area = {Room : room, Rea : actor, ActiveRow : {room : room.id}, PendingAction : "sew:seam"};
    const scope = {
        ShellPage : {Playing : "Playing"},
        GameInput : {CanPlay : true},
        SkillRules : {SilverStitch : "silver_stitch"},
        result : {slot, contacts : collection()},
        Route : input.opera.rooms,
        Observe() {},
        Contact : () => false,
        Compare : () => false
    };
    for (const match of source.matchAll(/\b(\w+) = "((?:bell|gallery)_\d+_[\w]+)"/g))
        scope[match[1]] = match[2];
    flags.Add("investigation_complete");
    let support = room.floors[0].surface, vx = 0, vy = 0, jump = false, axis = 0, neutral = false,
        installationWithoutSupport = false;
    const dt = input.dt, at = actor.Body.position;
    const fixedTop = id => {
        const r = room.terrain.find(s => s.id === id).rect;
        return r.y + r.height;
    };
    at.x = room.floors[0].x;
    at.y = fixedTop(support);
    area.StandingOn = id => actor.Grounded && support === id;
    const keys = {
        Hold : key => {
            if (key === "Space") jump = true;
        },
        Cast : () => { neutral = true; }
    };
    const bot = {
        Area : area,
        Shell : {Page : "Playing"},
        Session : {ActiveSlot : slot},
        TryDistrictHeal : () => false,
        Record : {storyFlags : flags, Get : () => ({acquired : true})}
    };
    const walk = (x, tolerance = .14) => {
        if (Math.abs(x - at.x) <= tolerance) return true;
        axis = Math.sign(x - at.x);
        return false;
    };
    const move = (face, x) => area.StandingOn(face) && walk(x);
    const aim = {
        Area : area,
        Player : actor,
        keys,
        ClockPosition : move,
        MoveTo : walk,
        Mathf : {Abs : Math.abs},
        KeyCode : {Space : "Space"},
        StitchSurface2D : stitch
    };
    aim.AimCastFrom = method(input.scripts.FirstDepartureBot, " AimCastFrom(", "x,facing", aim);
    bot.SewStationSeam = method(input.scripts.FirstDepartureBot, " SewStationSeam(", "mark", aim);
    bot.SewGallerySeam = method(input.scripts.GalleryTraversal, " SewGallerySeam(", "", aim);
    const game = {
        Room : room,
        StitchSurface2D : stitch,
        BenefactorIds : {Pendulum : "pendulum_rewind"},
        stationIndex : -1,
        sewn : null,
        stationHit : false,
        FinishAction() {
            receipts.push(area.PendingAction);
            area.PendingAction = "station:" + room.skill;
        }
    };
    const sew = method(input.scripts.DistrictRoomLayout, 'if (token == "sew:seam")', "", game);
    const fire = method(source, " Drive(", "bot,keys,now", scope);
    let stationInput = null, crossing = null;
    bot.BellStrike = (surface, x, direction) => { stationInput = {surface, x, direction}; };
    bot.PaintGap = (m, id) => { stationInput = {surface : m.surface, x : m.x, id}; };
    bot.ClockPosition = (surface, x) => { crossing = {surface, x}; };
    bot.LeavePaint = m => { crossing = {surface : m.landingSurface, x : m.x + 2.25 * m.direction}; };
    for (let tick = 0; tick * dt <= 18; tick++) {
        neutral = false;
        jump = false;
        axis = 0;
        fire(bot, keys, tick * dt);
        if (crossing) break;
        if (stationInput) {
            if (stationInput.surface !== mark.surface || stationInput.x !== mark.x || game.sewn !== fixture)
                break;
            receipts.push(area.PendingAction);
            area.PendingAction = "cross:floor";
        }
        if (axis) actor.Facing = axis;
        vx = axis * input.speed;
        actor.Body.linearVelocity.x = vx;
        if (jump && actor.Grounded) {
            vy = input.jump;
            actor.Grounded = false;
            support = null;
        }
        const previous = at.y;
        at.x += vx * dt;
        vy -= input.gravity * dt;
        at.y += vy * dt;
        const surfaces =
            room.terrain.map(s => ({id : s.id, x : s.rect.x, width : s.rect.width, top : fixedTop(s.id)}));
        if (fixture.Installed)
            surfaces.push({id : fixture.TargetId, x : seam.x - .45, width : .9, top : seam.y + .07});
        const floor =
            surfaces
                .filter(s => vy <= 0 && at.x + input.radius > s.x && at.x - input.radius < s.x + s.width &&
                             previous >= s.top && at.y <= s.top)
                .sort((a, b) => b.top - a.top)[0];
        if (floor) {
            at.y = floor.top;
            vy = 0;
            actor.Grounded = true;
            support = floor.id;
        }
        if (neutral && (seam.x - at.x) * actor.Facing >= 0 &&
            Math.hypot(seam.x - at.x, seam.y - at.y) <= input.silver.reach &&
            at.y <= seam.y + input.anchorTop && at.y + 2 >= seam.y + input.anchorBottom)
            fixture.Installed = true;
        fixture.SupportingPlayer = actor.Grounded && support === fixture.TargetId;
        if (area.PendingAction === "sew:seam") {
            sew();
            if (fixture.Installed && !fixture.SupportingPlayer && area.PendingAction === "sew:seam")
                installationWithoutSupport = true;
        }
        if (!stationInput && area.PendingAction !== "sew:seam" && !fixture.Installed) break;
    }
    return {receipts, stationInput, crossing, installationWithoutSupport, anchor : game.sewn?.TargetId};
}

const output = replay();
output.shortcuts = Object.fromEntries([ "bell", "gallery" ].map(stage => [stage, shortcut(stage)]));
process.stdout.write(JSON.stringify(output));
