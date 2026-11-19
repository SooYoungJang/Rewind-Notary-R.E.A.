import argparse
import json
import math
import re
import subprocess
from pathlib import Path

from validate_fixed_surface_route import fixed_edges, link, paths_from, room_faces, simulate_landing


def surface_steps(room, physics):
    deck = room_faces(room)
    edges = fixed_edges(deck, physics)
    result = {}
    for start in edges:
        parents = paths_from(edges, start)
        first_steps = {}
        for end in parents:
            if start == end:
                continue
            next_face = end
            while parents[next_face] != start:
                next_face = parents[next_face]
            if next_face not in first_steps:
                launch, aim, direction = link(deck, start, next_face, physics)
                x, ticks = simulate_landing(deck, start, next_face, launch, aim, direction, physics)
                first_steps[next_face] = dict(face=next_face.name, x=x, seconds=ticks * physics[2])
            result.setdefault(start.name, {})[end.name] = first_steps[next_face]
    return result


def validate_district_seams(verify, project, scripts, physics):
    from validate_level_step4 import simulate_span

    resources = project / 'Assets/Resources'
    districts = {stage: json.loads((resources / 'Story' / (stage + '.json')).read_text(encoding='utf-8'))
                 for stage in ('opera', 'bell', 'gallery', 'clock')}
    catalog = json.loads((resources / 'Skills/catalog.json').read_text(encoding='utf-8'))['skills']
    skills = {s['skillId']: s for s in catalog}
    silver = skills['silver_stitch']['stages'][0]
    bounds = re.search(r'Bounds => new Bounds\(transform.position \+ Vector3.up \* ([\d.]+)f, new Vector3\(span, ([\d.]+)f', scripts['StitchSurface2D'])
    offset, height = map(float, bounds.groups())
    bottom, top = offset - height / 2, offset + height / 2
    audit = {}
    for stage, doc in districts.items():
        rows = [row for row in doc['rows'] if 'sew:seam' in row['requiredPlayerAction']]
        anchors = []
        for row in rows:
            room = next(r for r in doc['rooms'] if r['id'] == row['room'])
            faces = {f.name: f for f in room_faces(room)}
            for run in room['seamRuns']:
                for i in range(run['count']):
                    index = len(anchors)
                    sx, sy = run['x'] + i * run['stepX'], run['y']
                    mark = room['stationMarks'][index]
                    if stage == 'gallery':
                        x = sx - 1.45
                        floor = next(f for f in faces.values() if f.left <= x <= f.right and abs(f.top - sy) < .5)
                    else:
                        x, floor = mark['x'], faces[mark['surface']]
                    reachable = (floor.left <= x <= floor.right and math.hypot(x - sx, floor.top - sy) <= silver['reach']
                                 and floor.top <= sy + top and floor.top + 2 >= sy + bottom)
                    landing = faces[mark.get('landingSurface', mark['surface'])]
                    cross_x = mark['x'] + mark.get('direction', 1) * 2.1
                    anchors.append(dict(id=room['seamId'] + '_' + str(index), x=sx, y=sy, castFrom=floor.name,
                                        launchX=x, launchY=floor.top, linkedSurface=mark['surface'],
                                        landing=landing.name, reachable=reachable,
                                        crossReachable=landing.left <= cross_x <= landing.right))
        audit[stage] = anchors
        verify(bool(anchors) and all(a['reachable'] and a['crossReachable'] for a in anchors),
               'QA31 / ' + stage + ' seam runs have a cast position and a crossing on the linked fixed tier')

    opera = districts['opera']
    chandelier = next(r for r in opera['rooms'] if r['id'] == 'opera_03_chandelier')
    paths = {r['id']: surface_steps(r, physics) for r in opera['rooms']}
    span = {side: simulate_span(chandelier, physics[2], physics[3], 0, side == 'east')[0]
            for side in ('east', 'west')}
    shortcuts = {stage: next(r for r in districts[stage]['rooms'] if r.get('seamId')) for stage in ('bell', 'gallery')}
    representative = json.loads((resources / 'Story/representative.json').read_text(encoding='utf-8'))
    external_rooms = [{k: r[k] for k in ('id', 'stage', 'floors', 'exits')} for r in representative['rooms']]
    for door in opera['links']:
        next(r for r in external_rooms if r['id'] == door['source'])['exits'].append(door)
    payload = dict(opera=opera, shortcuts=shortcuts, jump=physics[0], gravity=physics[1], radius=physics[6],
                   externalRooms=external_rooms,
                   scripts={k: scripts[k] for k in ('OperaDistrictProbe', 'FirstDepartureBot', 'BellDistrictProbe',
                                                    'GalleryDistrictProbe', 'GalleryTraversal', 'DistrictRoomLayout')},
                   paths=paths, span=span, dt=physics[2], speed=physics[3], anchorBottom=bottom, anchorTop=top,
                   silver=silver, mastery=skills['curtain_call_waltz']['stage3At'])
    tool = Path(__file__).with_name('replay_opera_probe.js')
    proc = subprocess.run(['node', str(tool)], input=json.dumps(payload), capture_output=True, text=True, encoding='utf-8')
    if proc.returncode:
        raise RuntimeError(proc.stderr.strip())
    replay = json.loads(proc.stdout)
    for stage, room in shortcuts.items():
        run = replay['shortcuts'][stage]
        mark = room['stationMarks'][0]
        cross = run['crossing']
        verify(run.get('anchor') == room['seamId'] + '_0' and run['receipts'] == ['sew:seam', 'station:' + room['skill']] and
               cross and cross['surface'] == mark.get('landingSurface', mark['surface']) and
               (cross['x'] - mark['x']) * mark.get('direction', 1) > 1.5 and
               (stage != 'gallery' or run['installationWithoutSupport']),
               'QA31 / ' + stage + ' probe earns the seam receipt and requests its linked station and crossing')
    result = replay['result']
    contacts = [c['room'] for c in result['contacts']]
    expected = [r['id'] for r in opera['rooms']]
    expected[-2:] = reversed(expected[-2:])
    verify(replay['complete'] and contacts == expected and result['shadedCrossing'] and result['dropLanding'] and
           result['closedBeforeInvestigation'] and 0 < result['shortcutSeconds'] < result['longSeconds'],
           'QA31 / the opera probe saves ten ordered landings including the arena and finishes both timed routes')
    verify(result.get('stageTwoRoom') == expected[6] and result.get('masteryRoom') == expected[7] and
           result.get('firstWaltzTarget') == expected[6] + ':station_0',
           'QA31 / first-return station input earns the waltz thresholds in the roof and foyer')
    short = next(r for r in opera['rooms'] if r['id'] == expected[-1])
    wave = {short['id'] + ':' + target for group in short['roster'] for target in group['placementIds']}
    verify(wave.issubset(replay.get('kills', [])) and [s['id'] for s in replay['seams']] == [short['seamId'] + '_0'] and
           replay['actions'] == ['sew:seam', 'station:curtain_call_waltz', 'cross:floor', 'advance:' + expected[-2]] and
           replay['visits'][-3:] == [expected[-1], expected[-2], expected[-1]],
           'QA31 / the linked lamp and fixed crossing open the sanctuary trip before the final shortcut save')
    failure = json.loads(Path(__file__).with_name('run31-district-seams.json').read_text(encoding='utf-8'))['opera']
    verify(short['id'] == failure['room'] and all(failure['y'] > a['y'] + top for a in audit['opera']) and
           any(s['id'] == failure['support'] and abs(s['rect']['y'] + s['rect']['height'] - failure['y']) < .01
               and s['rect']['x'] <= failure['x'] <= s['rect']['x'] + s['rect']['width'] for s in short['terrain']),
           'QA31 / from the recorded access step even the nearest seam lies below the silver blade')
    return dict(anchors=audit, contacts=contacts, visits=replay['visits'], actions=replay['actions'],
                cast=replay['seams'], gates=replay['gates'], complete=replay['complete'], shortcuts=replay['shortcuts'])


if __name__ == '__main__':
    from validate_level_step5 import physics_from_project
    parser = argparse.ArgumentParser()
    parser.add_argument('--bot-dir', type=Path)
    args = parser.parse_args()
    project = Path(__file__).resolve().parents[1]
    if not (project / 'Assets').is_dir():
        project = project / 'unity/RewindNotary'
    scripts = {p.stem: p.read_text(encoding='utf-8') for p in (project / 'Assets/Scripts').rglob('*.cs')}
    if args.bot_dir:
        for probe in ('OperaDistrictProbe', 'BellDistrictProbe', 'GalleryDistrictProbe'):
            scripts[probe] = (args.bot_dir / (probe + '.cs')).read_text(encoding='utf-8')
    passed, errors = [], []
    def check(ok, label):
        (passed if ok else errors).append(label)
    metrics = validate_district_seams(check, project, scripts, physics_from_project(project, scripts))
    print(json.dumps(dict(passed=len(passed), errors=errors, replay=metrics), indent=2))
    raise SystemExit(bool(errors))
