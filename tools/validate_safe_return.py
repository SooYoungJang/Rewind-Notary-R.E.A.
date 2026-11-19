import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess

from validate_bell_reentry import sword_parameters
from validate_fixed_surface_route import link
from validate_level_step1 import number
from validate_level_step5 import physics_from_project
from validate_walk_contact import velvet_deck


def return_edges(deck, physics):
    edges = {}
    for face in deck:
        if face.wall:
            continue
        links = []
        for target in deck:
            step = link(deck, face, target, physics)
            if step:
                launch, land, direction = step
                links.append((abs(target.top - face.top),
                              dict(to=target.name, launch=launch, land=land, direction=direction)))
        if len(links) == 3:
            for a, b in ((0, 1), (0, 2), (1, 2)):
                if links[a][0] > links[b][0]:
                    links[a], links[b] = links[b], links[a]
        elif len(links) <= 16:
            links.sort(key=lambda item: item[0])
        else:
            raise ValueError(f'{face.name} requires the large-partition Mono sort')
        edges[face.name] = [edge for _, edge in links]
    return edges


def replay_safe_return(project, scripts):
    physics = physics_from_project(project, scripts)
    doc = json.loads((project / 'Assets/Resources/Story/representative.json').read_text())
    catalog = json.loads((project / 'Assets/Resources/Skills/catalog.json').read_text())
    rooms = {}
    for room in doc['rooms']:
        if room['stage'] != 'velvet':
            continue
        deck = velvet_deck(room, scripts['RepresentativeRoomLayout'], True)
        room['faces'] = [vars(f) for f in deck]
        room['edges'] = return_edges(deck, physics)
        rooms[room['id']] = room
    fixture = json.loads((project / 'tools/run28-safe-return.json').read_text())
    _, _, _, width, height = sword_parameters(project, scripts, fixture['swordUses'])
    combo = scripts['CombatMotor2D'].split('AttackSpec[] PlayerCombo')[1].split('};')[0]
    swings = [[float(n.strip().rstrip('f')) for n in spec.split(',')]
              for spec in re.findall(r'new AttackSpec\(([^)]+)\)', combo)]
    names = ['FirstDepartureBot', 'VelvetTraversal', 'ClockTraversal', 'WaveTargetState', 'PlaytestObservations',
             'FirstDeparture', 'StitchSurface2D', 'CombatMotor2D', 'VelvetDistrictProbe', 'RepresentativeArea',
             'RepresentativeRoomLayout']
    rows = [row for row in doc['rows'] if row['sceneId'] == 'first_transfer']
    resume = {floor['id']: r['id'] for r in doc['rooms'] for floor in r['floors']}
    waves = json.loads((project / 'Assets/Resources/Waves/test-stage.json').read_text())
    release = next(row for row in doc['rows'] if row['completionFlag'] == 'mikhail_i0_complete')
    plank = re.search(r'Room\.SurfaceTop\(p\.surface\) \+ \(plank \? ([\d.]+)f : [^)]+\), '
                      r'plank \? ([\d.]+)f : [^,]+, plank \? ([\d.]+)f :', scripts['RepresentativeRoomLayout'])
    data = dict(rooms=rooms, transferRows=rows, resume=resume, entryWave=waves['zones'][0]['waves'][0], boutExit=release,
                plank=list(map(float, plank.groups())), gravityScale=number(scripts['CombatMotor2D'], 'Body.gravityScale'),
                dashDistance=number(scripts['DefenseTuning'], 'DashDistance'),
                stepJumpHold=float(re.search(r'jumpUntil = Time.time \+ ([.\d]+)f', scripts['FirstDepartureBot'])[1]),
                district=json.loads((project / 'tools/run30-velvet-probe.json').read_text()),
                kinds={a['id']: a for a in waves['archetypes']},
                parry={k: number(scripts['DefenseTuning'], k) for k in
                       ('ParryWindowSeconds', 'ParryRecoverySeconds', 'ParrySuccessGuardSeconds')},
                immunity=float(re.search(r'CreateActor\("Rea",[^\n]+?, \d+, ([.\d]+)f', scripts['GameBootstrap'])[1]),
                physics=physics, sources={k: scripts[k] for k in names}, fixture=fixture,
                loom=json.loads((project / 'tools/run29-safe-return.json').read_text()),
                silver=next(s for s in catalog['skills'] if s['skillId'] == 'silver_stitch'),
                swordSkill=next(s for s in catalog['skills'] if s['skillId'] == 'basic_sword'),
                initialSwordUses=int(re.search(r'InitialSwordUses = (\d+)', scripts['SkillDefinition'])[1]),
                grace=number(scripts['CombatMotor2D'], 'ComboGraceSeconds'),
                airDrag=number(scripts['CombatMotor2D'], 'AirDrag'),
                jumpBuffer=number(scripts['PlayerController2D'], 'JumpBuffer'),
                coyote=number(scripts['PlayerController2D'], 'CoyoteTime'),
                swings=swings, targetSize=[width, height],
                districtIdle=int(re.search(r'\bDistrictIdleLimit = (\d+)', scripts['AutoTestDriver'])[1]),
                districtDeadline=int(re.search(r'now - entered > (\d+)\)\s+\{ Stop\("District probe', scripts['AutoTestDriver'])[1]),
                idle=int(re.search(r'now - lastProgress > (\d+)', scripts['AutoTestDriver'])[1]))
    output = subprocess.run(['node', str(project / 'tools/replay_safe_return.js')], input=json.dumps(data),
                            check=True, stdout=subprocess.PIPE, text=True)
    return json.loads(output.stdout)


def validate_safe_return(check, project, scripts):
    result = replay_safe_return(project, scripts)
    water = 'velvet_05_return'
    for key in ('recovery', 'manual', 'campaign'):
        run = result[key]
        guard = [e['action'] for e in run['combat'] if e['target'] == water + ':notary_guard_1']
        surfaces = {landing['face'] for landing in run['waterLandings']}
        check(guard == ['setup', 'open', 'cut'] and run['waterSave'] and run['waterExit'] == 'velvet_01_entry' and
              {'tailrace', 'counterfloat', 'wheel_gallery', 'balcony'} <= surfaces and run['end']['hp'] > 0,
              'QA28 / ' + key + ' clears the active guard, saves on the balcony and leaves through the return door')
    manual = result['manual']
    check(bool(manual.get('latchHit')) and manual['totals'] == [['velvet_05_return', 16]],
          'QA28 / independent movement, Q, J and F reach the reunion latch and complete the waterway at 16')
    run = result['allSeams']
    story = json.loads((project / 'Assets/Resources/Story/representative.json').read_text())
    expected = Counter()
    for room in story['rooms']:
        if not room.get('seamRuns'):
            continue
        for i in range(room['stitchAnchors']):
            effect = '' if i < room['singleSeams'] else ':support'
            expected[room['id'] + ':seam_' + str(i) + effect] = 1
        if room['singleSeams']:
            index = 0
            for group in room['roster']:
                if group['id'] == 'notary_guard':
                    for j in range(group['count']):
                        expected[f"{room['id']}:notary_guard_{index+j}:posture"] = group['setupHits'] * group['segments']
                index += group['count']
    receipts = Counter(e['target'] for e in run['credits'])
    check(receipts == expected and len({e['use'] for e in run['credits']}) == len(run['credits']) == 80 and
          {e['target'] + ':posture' for e in run['combat'] if e['action'] == 'cut'} ==
          {target for target in expected if target.endswith(':posture')},
          'QA28 / separate silver casts credit every planned seam, support and guard preparation, with all eight cuts')
    check(run['totals'] == [['velvet_05_return', 16], ['velvet_01_entry', 40], ['velvet_02_loom', 52],
                            ['velvet_03_gallery', 64], ['velvet_04_arena', 80]] and run['safeCall'] and
          run.get('destination') == ['hub_workroom', 'workroom_safe_floor'] and
          len(run['credits']) == 80 and run['credits'][29]['target'].startswith('velvet_01_entry:') and
          run['credits'][-1]['target'] == 'velvet_04_arena:seam_15:support',
          'QA28 / ordinary inputs yield 80 distinct activations on the five-room first return and F on fixed ground')
    loom = 'velvet_02_loom'
    gallery = 'velvet_03_gallery'
    arena = 'velvet_04_arena'
    itinerary = [water, 'velvet_01_entry', loom, gallery, arena]
    guard_roster = {}
    for room in story['rooms']:
        if room['id'] in itinerary:
            index = 0
            for group in room['roster']:
                for _ in range(group['count']):
                    if group['id'] == 'notary_guard':
                        guard_roster[f"{room['id']}:notary_guard_{index}"] = (group['segments'], group.get('setupHits', 1))
                    index += 1

    def cleared_on_route(run, route):
        receipts = Counter(e['target'] for e in run['credits'])
        for target, (count, setup_hits) in guard_roster.items():
            if target.split(':')[0] in route:
                fight = [e for e in run['combat'] if e['target'] == target]
                if (receipts[target + ':posture'] != count * setup_hits or
                        [e['action'] for e in fight] != (['setup'] * (setup_hits - 1) + ['open', 'cut']) * count or
                        [e['remaining'] for e in fight if e['action'] == 'cut'] != list(reversed(range(count)))):
                    return False
        return ([d['room'] for d in run['departures']] == route[:-1] and
                [d['to'] for d in run['departures']] == route[1:] and
                all(remaining == 0 for d in run['departures'] for _, remaining in d['guards']) and
                run['safeCall'] and run.get('transferFlags') == ['first_transfer_seen', 'returned_for_mid_invest'] and
                run.get('destination') == ['hub_workroom', 'workroom_safe_floor'])

    for key in ('loom', 'loomCrossing', 'manualLoom'):
        run = result[key]
        target = run['start']['target']
        duel = [e for e in run['combat'] if e['target'] == target]
        check(run['start']['uses'] == 52 and run['start']['remaining'] == 4 and
              run['start']['fixedFace'] is None and run['start']['support'] == loom + ':seam_11' and
              run.get('bank', {}).get('face') == 'reed_0' and
              [e['action'] for e in duel] == ['open', 'cut'] * 4 and
              all(e['support'] == 'floor' for e in duel if e['action'] == 'cut') and
              cleared_on_route(run, itinerary[2:]) and run['end']['uses'] == 80,
              'QA29 / ' + key + ' leaves the captured loom span, clears guard 2 and finishes the return')
    for key in ('campaign', 'campaign60', 'campaignOffset'):
        run = result[key]
        check(cleared_on_route(run, itinerary) and run['end']['uses'] == 80 and
              len(run['credits']) == len({e['use'] for e in run['credits']}) == 80 and
              any(e['target'] == loom + ':seam_11:support' for e in run['credits']) and
              run['credits'][-1]['target'].startswith(arena + ':') and
              [r for r, _ in run['totals']] == itinerary,
              'QA29 / ' + key + ' crosses the last loom span, clears every guard before each door and reaches 80 in the arena')
    held, still = (result[k] for k in ('heldAttack', 'stillAttack'))
    check(held['swingTrace'][-1]['support'] == 'reed_1' and
          all(s['vx'] == held['swingTrace'][0]['vx'] > 0 for s in held['swingTrace']) and
          still['end']['support'] == still['start']['support'] and all(s['vx'] == 0 for s in still['swingTrace']) and
          not held['combat'] and not still['combat'],
          'QA29 / held and released direction during the same swing at the captured position')
    for key in ('afterInvestigation', 'manualAfterInvestigation'):
        route = result[key]
        check(route['start']['remaining'] == 7 and route['start']['active'] == 0 and route['start']['target'] is None and
              route['start']['support'] == 'silt_bed' and route['start']['uses'] == 80 and
              route.get('destination') == ['velvet_01_entry', 'velvet_entry_floor'] and
              route['departures'][0]['support'] == 'outfall_quay' and route['end']['uses'] == 80 and
              route['end']['activations'] == 0,
              'QA30 / ' + key + ' leaves the captured silt bed and takes the entry door on fixed ground')
    low, high = result['afterInvestigation'], result['unreachableLip']
    check(low['start'] == high['start'] and high['lip']['top'] > low['lip']['top'] and
          low['lip']['contacts'] > 0 and any(x < low['start']['feet'][0] for x in low['lip']['takeoffs']) and
          high['lip']['contacts'] > 0 and not high['lip']['takeoffs'],
          'QA30 / the blocked launch gets a run-up for a reachable plank and no jump for a lip above the apex')
    standing = result['cornerJump']
    check(standing['lip']['headContacts'] > 0 and standing['lip']['peakFeet'] < 0 and
          standing['stop'] == 'first landing' and standing['end']['support'] == 'silt_bed',
          'QA30 / jumping at the captured lip hits the lower corner of the plank and returns to the silt bed')
    district = result['district']
    probe = district['probe']
    entry, bridge = 'velvet_01_entry', 'velvet_06_shortcut'
    check(district['stop'] == 'district complete' and probe['closedOnFirstVisit'] and
          probe['arrivedAtUpperBridge'] and probe['arrivedAtArena'] and probe['returnDoors'] == [loom, bridge, arena] and
          [c['room'] for c in probe['contacts']] == [entry, loom, gallery, arena, water, bridge] and
          district['rooms'] == [entry, loom, gallery, arena, water, entry, loom, gallery, arena, 'hub_workroom',
                                arena, water, entry, loom, bridge, arena] and district['end']['support'] == 'balcony',
          'QA30 / the slot-2 probe earns all six floor contacts, opens the investigated upper return and lands in the arena')
    check(probe['silverRooms'] == itinerary and probe['firstSilverTarget'] == water + ':seam_0' and
          len({e['use'] for e in district['credits']}) == len(district['credits']) == probe['returnUses'] == 80 and
          (probe['shortSeams'], probe['guardPreparations'], probe['upperSupports']) == (16, 16, 48),
          'QA30 / 80 distinct slot-2 casts include the 16 short seams, 16 guard preparations and 48 upper supports')
    return result


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--bot-dir', type=Path, help='Replay the bot sources from a saved AutoTest directory')
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[1]
    if not (project / 'Assets/Scripts').is_dir():
        project = project / 'unity/RewindNotary'
    scripts = {p.stem: p.read_text() for p in (project / 'Assets/Scripts').rglob('*.cs')}
    if args.bot_dir:
        for name in ('FirstDepartureBot', 'ClockTraversal', 'VelvetTraversal', 'PlaytestObservations', 'VelvetDistrictProbe'):
            scripts[name] = (args.bot_dir / (name + '.cs')).read_text()
    failed = []
    metrics = validate_safe_return(lambda ok, label: None if ok else failed.append(label), project, scripts)
    print(json.dumps(dict(errors=failed, replay=metrics), indent=2))
    raise SystemExit(bool(failed))
