'use strict';

function createProbe(source, body, compile, bot, keys, time) {
    const names = Object.fromEntries([...source.matchAll(/\b(\w+) = "(velvet_\d+_[a-z]+)"/g)]
        .map(([, name, id]) => [name, id]));
    const rooms = source.match(/\bRooms = {([^}]+)}/)[1].split(',').map(name => names[name.trim()]);
    const result = {contacts: [], silverRooms: [], returnDoors: [], targetMinSeconds: 180, targetMaxSeconds: 300,
        returnUses: 0, guardPreparations: 0, shortSeams: 0, upperSupports: 0, outboundViaGallerySeconds: 0,
        shortcutSeconds: 0, arrivedAtUpperBridge: false, arrivedAtArena: false, closedOnFirstVisit: false};
    const env = {...names, result, Rooms: rooms, Time: time, Mathf: {Abs: Math.abs},
        ShellPage: {Playing: 'Playing', Stages: 'Stages'}, PlaytestStep: {Velvet: 'Velvet'},
        GameInput: {CanPlay: true, get InvestigateDown() { return keys.GetKeyDown('F'); }},
        KeyCode: {F: 'F'}, SkillRules: {SilverStitch: 'silver_stitch'}, start: 0, last: 0, outside: 0,
        previous: 0, roomEntered: 0, upperDoorTime: 0, lowerDoorTime: 0, wasOutside: false, inSecondSlot: false,
        closedInputFrame: -1, dropping: false, previousRoom: null, uses: new Set(), seams: new Set(), publish() {}};
    function compileProbe(signature, args) {
        const method = body(source, signature)
            .replace(/new VelvetContact\s*{([\s\S]*?)}/g, (_, fields) =>
                '({' + fields.replace(/(\w+)\s*=(?!=)/g, '$1:') + '})')
            .replace(/Array\.IndexOf\((\w+), (\w+)\)/g, '$1.indexOf($2)')
            .replace(/string\.Join\("(.*?)", (.*?)\)/g, '$2.join("$1")')
            .replace(/\.StartsWith\(([^,]*), StringComparison\.Ordinal\)/g, '.startsWith($1)')
            .replace(/\.EndsWith\(([^,]*), StringComparison\.Ordinal\)/g, '.endsWith($1)')
            .replace(/\bdouble (\w+)/g, 'let $1').replace(/\.Count/g, '.length').replace(/collectUpperSupports: /g, '')
            .replace(/(uses|seams)\.Add\(([^)]+)\)/g, 'newEntry($1, $2)')
            .replace(/uses\.length/g, 'uses.size')
            .replace(/\.Add\(/g, '.push(').replace(/\.Contains\(/g, '.includes(');
        return compile(method, args);
    }
    env.newEntry = (set, id) => {
        if (set.has(id)) return false;
        set.add(id);
        return true;
    };
    const contact = compileProbe('void Contact(', 'bot, now');
    const drive = compileProbe('bool Drive(', 'bot, keys, now');
    const observe = compileProbe('void ObserveUse(', 'e, room');
    env.Contact = (...args) => contact(env, ...args);
    return {result, drive: now => drive(env, bot, keys, now), credit(e, room) { observe(env, e, room); }};
}

module.exports = {createProbe};
