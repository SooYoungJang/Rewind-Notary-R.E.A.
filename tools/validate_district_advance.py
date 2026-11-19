from copy import deepcopy
import json
from pathlib import Path
import re
import subprocess

from story_link_baseline import prior_pass2_source
from validate_bell_reentry import replay, sword_parameters
from validate_fixed_surface_route import fixed_edges, link, paths_from, room_faces
from validate_level_step1 import contains, number
from validate_level_step5 import first_landing, physics_from_project
from validate_opera_wave import support_at


def door_steps(deck, edges, goal, physics):
    steps = {}
    for source in edges:
        parents = paths_from(edges, source)
        if source == goal or goal not in parents:
            continue
        child = goal
        while parents[child] != source:
            child = parents[child]
        launch, land, direction = link(deck, source, child, physics)
        steps[source.name] = dict(To=child.name, LaunchX=launch, LandX=land, WalkOff=direction)
    return steps


def validate_district_advance(verify, project, scripts, physics):
    directory = project / 'tools'
    qa = json.loads((directory / 'run27-gallery-advance.json').read_text())
    state = qa['endState']
    documents = {p.stem: json.loads(p.read_text()) for p in (project / 'Assets/Resources/Story').glob('*.json')}
    rooms = {r['id']: r for doc in documents.values() for r in doc['rooms']}
    cases, routes, stages = {}, {}, set()
    graphs = {}
    sword = sword_parameters(project, scripts, 0)
    for doc in documents.values():
        for row in doc['rows']:
            actions = row['requiredPlayerAction']
            if not actions[0].startswith('advance:'):
                continue
            room = rooms[row['room']]
            stages.add(room['stage'])
            name = row['textKey']
            destination = actions[0].split(':', 1)[1]
            door = next(d for d in room['exits'] if d['room'] == destination)
            last_id = room['roster'][-1]['placementIds'][-1]
            target = next(p for p in room['placements'] if p['id'] == last_id)
            deck = room_faces(room)
            edges = fixed_edges(deck, physics)
            start = support_at(edges, target['x'], target['y'])
            goal = support_at(edges, door['x'], door['approach']['y'] + door['approach']['height']/2)
            if room['id'] == state['room']:
                graphs['pass2'] = door_steps(deck, edges, goal, physics)
            origin = dict(x=target['x'], y=start.top, support=start.name)
            path = replay(deck, edges, physics, origin, None, 0, door, False, sword)
            flags = [*row['entryCondition'], room['benefactor'] + '_defeated']
            skills = [room['skill']]
            if door['gate'] == 'synergy':
                flags.append('investigation_complete')
                skills.append('silver_stitch')
            sample = dict(room=room, row=row, flags=flags, skills=skills,
                          targets=[True for group in room['roster'] for _ in group['placementIds']],
                          roofOpen=True, fixed=True)
            cases[name + '/start'] = dict(sample, feet=dict(x=origin['x'], y=start.top), support=start.name)
            cases[name + '/end'] = dict(sample, feet=dict(zip(('x', 'y'), path['feet'])), support=path['supports'][-1])
            arrival = next(f for f in rooms[destination]['floors'] if f['id'] == door['destinationFloor'])
            routes[name] = dict(start=start.name, lastTarget=last_id, path=path, room=destination, floor=arrival['id'],
                                targetFace=goal.name)

    path = 'Assets/Resources/Story/gallery.json'
    old = json.loads(prior_pass2_source(path, (project / path).read_text()))
    before = next(r for r in old['rooms'] if r['id'] == state['room'])
    room = rooms[state['room']]
    row = next(r for r in documents['gallery']['rows'] if r['textKey'] == state['row'])
    door = next(d for d in room['exits'] if d['room'] == row['alternateRoom'])
    floor = next(s for s in room['terrain'] if s['id'] == qa['doorFloor']['surface'])
    top = floor['rect']['y'] + floor['rect']['height']
    latch = next(p for p in room['placements'] if p['id'] == routes[state['row']]['lastTarget'])
    profile = next(s for s in json.loads((project / 'Assets/Resources/Skills/catalog.json').read_text())['skills']
                   if s['skillId'] == room['skill'])['stages'][1]
    geometry = scripts['BenefactorSkill']
    offset = number(geometry, 'PaintGroundOffset')
    lead = number(geometry, 'PaintLead')
    thickness = number(geometry, 'PaintThickness')
    left = floor['rect']['x']
    direction = 1 if latch['x'] - left >= left + floor['rect']['width'] - latch['x'] else -1
    fight = scripts['GalleryTraversal'].split('void GalleryFight(')[1]
    aim_call = re.search(r'(\w+)\(at\.x - direction \* ([.\d]+)f, direction\)', fight)
    aim = float(aim_call[2])
    cast = latch['x'] - direction * aim
    ends = sorted((cast + direction * lead, cast + direction * (lead + profile['reach'])))
    station = qa['lastStation']
    last_mark = room['stationMarks'][-1]
    launch_floor = next(f for f in room_faces(room) if f.name == last_mark['surface'])
    grounded_offset = station['top'] - launch_floor.top - offset - thickness/2
    brush_top = top + grounded_offset + offset + thickness/2
    first = dict(cases[state['row'] + '/start'], room=room, floor=floor, top=top, layout='pass2',
                 feet={k: state[k] for k in ('x', 'y')}, flags=state['flags'], skills=[room['skill']],
                 brush=dict(min=dict(x=ends[0], y=brush_top - thickness),
                            max=dict(x=ends[1], y=brush_top)), life=0)
    cases['qa27'] = first
    old_deck = room_faces(before)
    graphs['run27'] = door_steps(old_deck, fixed_edges(old_deck, physics),
                                 next(f for f in old_deck if f.name == floor['id']), physics)
    cases['prePass2'] = dict(first, room=before, layout='run27')
    cases['fresh'] = dict(first, life=profile['lifetime'])
    signature = 'bool ' + aim_call[1] + '('
    aim_body = next(source.split(signature)[1] for source in scripts.values() if signature in source)
    aim_tolerance = float(re.search(r'MoveTo\(x, ([.\d]+)f\)', aim_body)[1])
    for name, shift in (('castLeft', -aim_tolerance), ('castRight', aim_tolerance)):
        cases[name] = deepcopy(first)
        for edge in cases[name]['brush'].values():
            edge['x'] += shift
    cases['dry'] = dict(first, feet=dict(x=door['x'], y=top), paint='none')
    cases['human'] = dict(first, manual=True)
    for key, changes in (('last target alive', {'targets': [True, True, True, False]}),
                         ('missing gate flag', {'flags': [f for f in state['flags'] if f != door['gate']]}),
                         ('outside approach', {'feet': dict(x=left + .5, y=top), 'paint': 'none'}),
                         ('during attack', {'safe': False})):
        cases[key] = dict(first, manual=True, **changes)

    dt = physics[2]
    source_names = {'RepresentativeRoomLayout', 'DistrictRoomLayout'}
    for stage in stages:
        source_names.update([stage.capitalize() + 'Traversal', stage.capitalize() + 'DistrictProbe'])
    idle = int(re.search(r'now - lastProgress > (\d+)', scripts['AutoTestDriver'])[1])
    edge = json.loads((directory / 'run31-district-seams.json').read_text(encoding='utf-8'))['gallery']
    window = rooms[edge['room']]
    deck = room_faces(window)
    edge['step'] = door_steps(deck, fixed_edges(deck, physics), next(f for f in deck if f.name == edge['to']), physics)[edge['from']]
    edge['goalX'] = next(d['x'] for d in window['exits'] if d['room'] == edge['door'])
    edge['room'] = window
    inputs = dict(sources={k: scripts[k] for k in sorted(source_names)}, stages=sorted(stages), cases=cases, graphs=graphs,
                  dt=dt, speed=physics[3], gravity=physics[1], brake=physics[5], radius=physics[6], aimTolerance=physics[8],
                  doorTolerance=number(scripts['ClockTraversal'], 'tolerance'),
                  supportTolerance=number(geometry, 'SupportTolerance'), idle=idle, windowEdge=edge)
    output = subprocess.run(['node', str(directory / 'replay_district_advance.js')], input=json.dumps(inputs),
                            text=True, stdout=subprocess.PIPE, check=True)
    frames = json.loads(output.stdout)
    release = frames['windowEdge']
    verify(release['released'] and release['firstDirection'] == -1 and release['resumedDirection'] == 1 and
           release['clearX'] < edge['brush']['min']['x'] - inputs['supportTolerance'],
           'QA31 / leave the retired stroke at the sash edge, then resume the hub door route on dry floor')
    for name, result in routes.items():
        start, end = frames[name + '/start'], frames[name + '/end']
        path = result['path']
        verify(start['selected'] == result['room'] and start['plan'] and start['plan']['face'] == result['targetFace'] and
               path['inspectTick'] is not None and end['plan'] == start['plan'] and
               end['travel'] == {'room': result['room'], 'floor': result['floor']},
               name + ' / after the final wave placement, plan the next door and inspect on its fixed floor')
    actual = qa['doorFloor']
    verify(state['grounded'] and not state['safeFloor'] and state['action'] == row['requiredPlayerAction'][0] and
           row['requiredPlayerAction'][1] == 'clear:wave' and door['gate'] in state['flags'] and
           state['target'] == '' and state['targetSegments'] == -1 and 'remainingTargets=0' in qa['reason'] and
           row['completionFlag'] not in state['flags'] and 'crossed:' + row['completionFlag'] not in state['flags'] and
           qa['stationUses'] == len(room['stationMarks']) and qa['combatUses'] == 8 and
           contains(door['approach'], state['x'], state['y']) and
           abs(top - actual['bottom'] - actual['height']) <= .005 and
           abs(left - actual['left']) <= .005 and abs(floor['rect']['width'] - actual['measured']) <= .005 and
           before['exits'] == room['exits'] and latch in before['placements'] and floor in before['terrain'],
           'run 27 / measured exit deck, wave receipts and door approach on both layouts')
    verify(ends[0] <= state['x'] <= ends[1] and
           abs(state['y'] - first['brush']['max']['y']) <= .005 and
           abs(station['width'] - profile['reach']) <= .005 and
           abs(last_mark['x'] - state['x']) > profile['reach'] + inputs['supportTolerance'] and
           station['target'] == room['id'] + ':station_' + str(len(room['stationMarks']) - 1) + ':support',
           'run 27 / the setup stroke supports the recorded feet and the last station stroke cannot')
    arrival = dict(room=door['room'], floor=door['destinationFloor'])
    destination = rooms[arrival['room']]
    spawn = next(f for f in destination['floors'] if f['id'] == arrival['floor'])
    contact = next(f for f in destination['floors'] if f.get('contactSave'))
    deck = room_faces(destination)
    spawn_face = next(f for f in deck if f.name == spawn['surface'])
    landed = first_landing(deck, spawn['x'], spawn_face.top + .05, destination['fallLine'], physics)
    verify(landed == contact['surface'] and abs(spawn['x'] - contact['x']) <= .8,
           'run 27 / 08 arrival falls onto the next physical save contact')
    for name in ('qa27', 'prePass2', 'fresh', 'castLeft', 'castRight'):
        result = frames[name]
        verify(result['first']['selected'] == door['room'] and result['first']['axis'] != 0 and
               result['dryTick'] is not None and result['dryTick'] <= result['tick'] < idle/dt and
               result['support'] == floor['id'] and result['receipt'] == 'crossed:' + row['completionFlag'] and
               result['travel'] == arrival, name + ' / walk off the physical paint, land and advance by inspect')
    verify(frames['dry']['tick'] == 0 and frames['dry']['travel'] == arrival,
           'run 27 / a dry approach takes inspect immediately')
    verify(frames['human']['tick'] == 0 and frames['human']['travel'] == arrival,
           'run 27 / ordinary inspect from the recorded paint-supported feet reaches 08')
    for name in ('last target alive', 'missing gate flag', 'outside approach', 'during attack'):
        verify(frames[name]['travel'] is None and frames[name]['receipt'] is None,
               'run 27 / inspect leaves the row pending with ' + name)
    return dict(advanceRows=routes, windowEdge=release, run27={k: frames[k] for k in ('qa27', 'prePass2', 'fresh', 'castLeft', 'castRight', 'dry', 'human')},
                reconstructedPaint=first['brush'], arrivalContact=dict(surface=landed, id=contact['id'], x=spawn['x']),
                fixedDeltaTime=dt)


if __name__ == '__main__':
    import argparse

    parser = argparse.ArgumentParser()
    parser.add_argument('--gallery-traversal', type=Path)
    args = parser.parse_args()
    project = Path('unity/RewindNotary')
    scripts = {p.stem: p.read_text() for p in (project / 'Assets/Scripts').rglob('*.cs')}
    if args.gallery_traversal:
        scripts['GalleryTraversal'] = args.gallery_traversal.read_text()
    passed, failed = [], []
    results = validate_district_advance(lambda ok, label: (passed if ok else failed).append(label),
                                        project, scripts, physics_from_project(project, scripts))
    print(json.dumps(dict(passed=len(passed), failed=failed, results=results), indent=2))
    raise SystemExit(bool(failed))
