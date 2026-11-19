import argparse
from collections import Counter
import json
from pathlib import Path
import re
import subprocess

from validate_bell_reentry import sword_parameters
from validate_fixed_surface_route import fixed_edges, link
from validate_level_step1 import number
from validate_level_step5 import physics_from_project
from validate_walk_contact import velvet_deck


def replay_safe_return(project, scripts):
    physics = physics_from_project(project, scripts)
    doc = json.loads((project / 'Assets/Resources/Story/representative.json').read_text())
    catalog = json.loads((project / 'Assets/Resources/Skills/catalog.json').read_text())
    rooms = {}
    for room in doc['rooms']:
        if not room.get('seamRuns'):
            continue
        deck = velvet_deck(room, scripts['RepresentativeRoomLayout'], True)
        edges = fixed_edges(deck, physics)
        room['faces'] = [vars(f) for f in deck]
        room['edges'] = {a.name: [dict(to=b.name, launch=x, land=y, direction=d)
                                  for b in bs for x, y, d in [link(deck, a, b, physics)]]
                         for a, bs in edges.items()}
        rooms[room['id']] = room
    fixture = json.loads((project / 'tools/run28-safe-return.json').read_text())
    _, swing, profile, width, height = sword_parameters(project, scripts, fixture['swordUses'])
    names = ['FirstDepartureBot', 'VelvetTraversal', 'ClockTraversal', 'WaveTargetState', 'PlaytestObservations',
             'FirstDeparture', 'StitchSurface2D']
    rows = [row for row in doc['rows'] if row['sceneId'] == 'first_transfer']
    resume = {floor['id']: r['id'] for r in doc['rooms'] for floor in r['floors']}
    waves = json.loads((project / 'Assets/Resources/Waves/test-stage.json').read_text())
    data = dict(rooms=rooms, transferRows=rows, resume=resume,
                kinds={a['id']: a for a in waves['archetypes']},
                parry={k: number(scripts['DefenseTuning'], k) for k in
                       ('ParryWindowSeconds', 'ParryRecoverySeconds', 'ParrySuccessGuardSeconds')},
                immunity=float(re.search(r'CreateActor\("Rea",[^\n]+?, \d+, ([.\d]+)f', scripts['GameBootstrap'])[1]),
                physics=physics, sources={k: scripts[k] for k in names}, fixture=fixture,
                silver=next(s for s in catalog['skills'] if s['skillId'] == 'silver_stitch'),
                swing=swing, sword=profile, targetSize=[width, height],
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
    run = result['campaign']
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
        for name in ('FirstDepartureBot', 'ClockTraversal', 'VelvetTraversal', 'PlaytestObservations'):
            scripts[name] = (args.bot_dir / (name + '.cs')).read_text()
    failed = []
    metrics = validate_safe_return(lambda ok, label: None if ok else failed.append(label), project, scripts)
    print(json.dumps(dict(errors=failed, replay=metrics), indent=2))
    raise SystemExit(bool(failed))
