import json
import math

from story_link_baseline import prior_pass2_source
from validate_fixed_surface_route import Face, fixed_edges, link, paths_from, room_faces, simulate_landing, steer
from validate_level_step1 import number
from validate_level_step5 import physics_from_project
from validate_walk_contact import velvet_deck


def longest_platform_run(room):
    rectangles = [s['rect'] for s in room.get('terrain', []) if not s.get('wallJump') and not s.get('rampRise')]
    lanes = []
    for rect in sorted(rectangles, key=lambda r: r['y']+r['height']):
        top = rect['y'] + rect['height']
        if not lanes or top-lanes[-1][0] > .005:
            lanes.append((top, []))
        lanes[-1][1].append(rect)
    maximum = 0
    for _, lane in lanes:
        previous = None
        count = 0
        for rect in sorted(lane, key=lambda r: r['x']):
            if previous and all(abs(rect[k]-previous[k]) <= .005 for k in ('width','height')):
                count += 1
            else:
                previous = rect
                count = 1
            maximum = max(maximum, count)
    return maximum


def route_modes(room, deck, physics):
    edges = fixed_edges(deck, physics)
    floor = room['floors'][0]
    start = next(f for f in edges if f.name == floor.get('surface') or
                 not floor.get('surface') and f.left <= floor['x'] <= f.right and abs(f.top)<.005)
    reachable = paths_from(edges, start)
    modes = set()
    for a in reachable:
        for b in edges[a]:
            step = link(deck, a, b, physics)
            if step[2] and a.top - b.top >= 1:
                modes.add('drop')
            elif not step[2]:
                modes.add('jump')
    for s in room['terrain']:
        r = s['rect']
        x, y = r['x'] + r['width']/2, r['y'] + r['height']
        if s.get('period', 0) > 0 and s.get('travelY',0) >= 2 and any(
            f.left < x < f.right and y <= f.top <= y+s['travelY']-1.2 for f in reachable):
            modes.add('ride')
        if s.get('wallJump') and not s.get('oneWay') and any(
            min(abs(f.right-r['x']),abs(f.left-r['x']-r['width'])) < 3 and
            r['y']-.2 <= f.top <= y-1 for f in reachable):
            modes.add('wall')
    if room['stage']=='velvet':
        index = 0
        for run in room.get('seamRuns', []):
            for i in range(run['count']):
                x = run['x'] + i*run['stepX']
                banks = [f for f in reachable if abs(f.top+.24-run['y'])<.005]
                if index >= room['singleSeams'] and not any(f.left<=x<=f.right for f in banks) and all(
                    any(f.left<=x+offset<=f.right for f in banks) for offset in (-1.1,1.1)):
                    modes.add('temporary support')
                index += 1
    skill = room.get('skill')
    if room.get('stationMarks'):
        if skill == 'pendulum_rewind':
            modes.add('pendulum')
        if skill == 'curtain_call_waltz':
            modes.add('waltz')
        if skill == 'canvas_carve' or room.get('seamId'):
            modes.add('temporary support')
    return sorted(modes)


def waterway_shortcuts(room, deck, physics, controls):
    fixed = {f.name:f for f in deck}
    bank, gallery = fixed['lock_bed'], fixed['wheel_gallery']
    carrier = next(s for s in room['terrain'] if s['id']=='counterfloat')
    rect = carrier['rect']
    dt = physics[2]
    low = rect['y'] + rect['height']
    samples = [low + carrier['travelY']*(.5-.5*math.cos(t*dt*2*math.pi/carrier['period']))
               for t in range(1,1+round(carrier['period']/dt))]
    takeoff = next((y for y in samples if y>=gallery.top-1.2),None)
    ride = None
    center = rect['x']+rect['width']/2
    if low <= bank.top and bank.left<center<bank.right and takeoff is not None:
        float_deck = Face(carrier['id'],rect['x'],rect['x']+rect['width'],takeoff-rect['height'],takeoff)
        hit = simulate_landing([*deck,float_deck],float_deck,gallery,center,room['seamRuns'][-1]['x'],0,physics)
        if hit:
            ride = dict(surface=gallery.name,x=round(hit[0],3),ticks=hit[1],low=round(low,3),high=round(max(samples),3))

    pier, cap = fixed['sluice_pier'], fixed['sluice_cap']
    x, y = pier.left-physics[6], fixed['balcony'].top
    jump, gravity = physics[:2]
    vx, vy = 0, jump
    slide = number(controls,'WallSlideSpeed')
    slide_ticks = 0
    unlock = None
    for tick in range(math.ceil(3/dt)):
        if unlock is None and vy<0 and pier.wall and y<pier.top and y+physics[7]>pier.bottom:
            slide_ticks += 1
            vy = max(vy,-slide+gravity*dt)
            if slide_ticks==3:
                vx, vy = -number(controls,'WallKickX'),number(controls,'WallKickY')
                unlock = tick + math.ceil(number(controls,'KickControlTime')/dt)
        if unlock is not None and tick>=unlock:
            vx = steer(vx,(cap.left+cap.right)/2-x,physics)
        x += vx*dt
        previous = y
        vy -= gravity*dt
        y += vy*dt
        caught = [f for f in deck if f.left<x<f.right and y<=f.top<=previous]
        if caught:
            landed = max(caught,key=lambda f:f.top)
            kick = dict(surface=cap.name,x=round(x,3),slideTicks=slide_ticks) if unlock is not None and landed==cap else None
            return dict(floatToGallery=ride,wallToCap=kick)
    return dict(floatToGallery=ride,wallToCap=None)


def validate_level_design(check, project, scripts):
    physics = physics_from_project(project, scripts)
    layout = scripts['RepresentativeRoomLayout']
    table = {}
    for path in sorted((project/'Assets/Resources/Story').glob('*.json')):
        raw = path.read_text()
        original = json.loads(prior_pass2_source(path.relative_to(project), raw))
        previous = {r['id']: r for r in original['rooms']}
        for room in json.loads(raw)['rooms']:
            rid = room['id']
            run = longest_platform_run(room)
            check(run <= 6, f'Level design: {rid} maximum consecutive equal platforms = {run} (limit 6)')
            if rid != 'velvet_05_return' and not (room['stage'] in ('clock','opera','bell','gallery') and
                    (rid.split('_')[1] in ('07','08') or room.get('stations'))):
                continue
            metrics = {}
            for key,r in (('before',previous[rid]),('after',room)):
                deck = velvet_deck(r, layout, True) if r['stage']=='velvet' else room_faces(r)
                metrics[key] = dict(run=longest_platform_run(r),stations=r.get('stations',0),modes=route_modes(r,deck,physics))
            check(len(metrics['after']['modes']) >= 3, 'Level design: three traversal modes in '+rid)
            check(room.get('stations',0)==len(room.get('stationMarks',[])), 'Level design: count placed stations in '+rid)
            if rid == 'velvet_05_return':
                shortcuts = waterway_shortcuts(room,deck,physics,scripts['PlayerController2D'])
                for name, arrival in shortcuts.items():
                    check(bool(arrival),'Level design: waterway landing '+name)
                metrics['shortcuts'] = shortcuts
            table[rid] = metrics
    return table
