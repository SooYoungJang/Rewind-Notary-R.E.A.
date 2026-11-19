"""Gallery room geometry, both viewing routes, physical brush supports and first-return supply."""
from collections import Counter
from copy import deepcopy
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageOps, ImageStat

from story_link_baseline import prior_level6_source
from validate_district_advance import validate_district_advance
from validate_fixed_surface_route import Face, fixed_edges, link, paths_from, room_faces, simulate_landing
from validate_gallery_bridge import validate_gallery_bridge
from validate_level_step1 import contains, number
from validate_level_step4 import fixed_walk, shortest_return
from validate_level_step5 import first_landing, physics_from_project
from validate_terrain_bounds import validate_terrain_bounds


def camera_view(room, layers, art):
    """Settled 16:9 view at the contact mark, with the same pivot, tint and mirror parity as ParallaxBackground."""
    mark = next(m for m in room['floors'] if m.get('contactSave'))
    f = next(f for f in room_faces(room) if f.name == mark['surface'])
    c = room['cameraBounds']
    cx = max(c['x'], min(c['x'] + c['width'], mark['x']))
    cy = max(c['y'], min(c['y'] + c['height'], f.top)) + 1.2
    view = Image.new('RGBA', (480, 270))
    scale = view.height / 11
    for layer in layers:
        im = Image.open(art / (layer['resource'] + '.png')).convert('RGBA')
        uv = layer['crop']
        im = im.crop((round(im.width*uv['x']), round(im.height*(1-uv['y']-uv['height'])),
                      round(im.width*(uv['x']+uv['width'])), round(im.height*(1-uv['y']))))
        h = layer['height']
        w = h * im.width / im.height
        tint = bytes.fromhex(layer['tint'][1:])
        im = ImageChops.multiply(im, Image.new('RGBA', im.size, (*tint,255)))
        dx = cx*(1-layer['factor']) + layer['offset']
        pitch = w * layer['spacing']
        center = int((cx-dx)//pitch)
        y = round((cy+5.5-layer['bottom']-cy*layer['vertical']-h)*scale)
        for tile in range(center-2,center+3):
            mid = view.width/2 + (dx+tile*pitch-cx)*scale
            x, right = round(mid - w*scale/2), round(mid + w*scale/2)
            strip = im.resize((right-x,round(h*scale)),Image.Resampling.LANCZOS)
            view.alpha_composite(ImageOps.mirror(strip) if tile%2 else strip,(x,y))
    return view


def background_sheet(project):
    art = project/'Assets/Resources/GameArt/backgrounds'
    rooms = json.loads((project/'Assets/Resources/Story/gallery.json').read_text())['rooms']
    layers = {s['id']:s['layers'] for s in json.loads((art/'room-sets.json').read_text())['sets']}
    out = Image.new('RGB',(960, 5*298),'#292722')
    draw = ImageDraw.Draw(out)
    for i,room in enumerate(rooms[:8]+[rooms[9],rooms[8]]):
        x,y=(i%2)*480,(i//2)*298
        out.paste(camera_view(room,layers[room['backgroundSet']],art),(x,y))
        draw.text((x+10,y+277),room['id'],fill='#f6edda')
    out.save(project/'LEVEL-STEP6-BACKGROUNDS.jpg',quality=91)


def paint_replay(room, mark, profile, physics, geometry):
    """Make a fresh ground stroke, jump onto its box, walk off, remove the box and repeat the restore drop."""
    lead, offset, thickness = geometry
    deck = room_faces(room)
    by_name = {f.name:f for f in deck}
    launch, arrival = by_name[mark['surface']], by_name[mark['landingSurface']]
    x, direction, reach = mark['x'], mark['direction'], profile['reach']
    if direction not in (-1,1) or abs(launch.top-arrival.top)>.001: return False
    for error in (-.1,0,.1):
        lx = x - direction*1.55 + error
        if not launch.left+physics[6] <= lx <= launch.right-physics[6]: return False
        left,right=sorted((lx+direction*lead,lx+direction*(lead+reach)))
        top=launch.top+offset+thickness/2
        stroke=Face('live-stroke',left,right,top-thickness,top)
        if any(f.left < right and f.right > left and f.bottom < top and f.top > stroke.bottom for f in deck):
            return False
        if any(contains(p['active'],lx,launch.top+.01) and p['x']+.4>=left and p['x']-.4<=right and
               p['y']<=top and p['y']+1.8>=stroke.bottom for p in room['placements']): return False
        hit=simulate_landing([*deck,stroke],launch,stroke,lx,x,0,physics)
        if not hit or abs(hit[0]-x)>.7 or hit[1]*physics[2]>=profile['lifetime']: return False
        if any(f.left-physics[6] < hit[0] < f.right+physics[6] and abs(f.top-launch.top)<.001 for f in deck): return False
        # Both banks are reachable without the skill. Walk off the far end of the brush onto the receiving lip.
        edge = stroke.right if direction==1 else stroke.left
        if not arrival.left < edge + direction*physics[6] < arrival.right: return False
        end=x+direction*2.25
        if first_landing(deck,end,top+.05,room['fallLine'],physics)!=arrival.name: return False
        if first_landing(deck,lx,launch.top+.05,room['fallLine'],physics)!=launch.name: return False
    return True


def branch_replay(room, path, physics):
    deck=room_faces(room)
    fixed={f.name:f for f in deck}
    ids=path['surfaces']
    contacts=[]
    x=room['floors'][0]['x']
    elapsed=0.
    for source,dest in zip(ids,ids[1:]):
        a,b=fixed[source],fixed[dest]
        step=link(deck,a,b,physics)
        if not step:return None
        lip,aim,walk_off=step
        hit=simulate_landing(deck,a,b,lip,aim,walk_off,physics,x if walk_off else None)
        if not hit:return None
        if not walk_off:elapsed+=abs(x-lip)/physics[3]
        x,ticks=hit
        elapsed+=ticks*physics[2]
        contacts.append(dict(surface=dest,x=round(x,3),y=b.top))
    door=room['exits'][1]
    if not contains(door['approach'],door['x'],fixed[ids[-1]].top+.05):return None
    return dict(start=ids[0],landings=contacts,door=door['room'],movementSeconds=round(elapsed+abs(x-door['x'])/physics[3],2))


def shortcut(rooms,physics):
    times=[]
    for i in (4,3,2,1):
        r=rooms[i]
        start='gallery_frame_head' if i==4 else next(d['destinationFloor'] for d in rooms[i+1]['exits'] if d['room']==r['id'])
        m=next(m for m in r['floors'] if m['id']==start)
        graph=fixed_edges(room_faces(r),physics)
        faces={f.name:f for f in graph}
        if i==1:
            end=next(m for m in r['floors'] if m['id']=='gallery_frame_landing')
            dest,x=faces[end['surface']],end['x']
        else:
            d=next(d for d in r['exits'] if d['room']==rooms[i-1]['id'])
            x=d['x']
            dest,=(f for f in graph if f.left<x<f.right and contains(d['approach'],x,f.top+.05))
        times.append(fixed_walk(graph,physics,faces[m['surface']],m['x'],dest,x))
    return dict(before=shortest_return(rooms,False),after=shortest_return(rooms,True),
                ordinaryRoomSeconds=[round(t,2) for t in times],ordinaryMovementSeconds=round(sum(times),2))


def validate_level_step6(check,project,scripts):
    def verify(ok,label):check(bool(ok),'Level step 6: '+label)
    path='Assets/Resources/Story/gallery.json'
    raw=(project/path).read_text()
    old=prior_level6_source(path,raw)
    rooms=json.loads(raw)['rooms']
    original=json.loads(old)['rooms']
    rows=raw.split('\n  "rows":')[1]
    verify(raw!=old and rows==old.split('\n  "rows":')[1] and len(json.loads(raw)['rows'])==42,
           'all 42 story rows and hub links byte identical')
    physics=physics_from_project(project,scripts)
    geom=[number(scripts['BenefactorSkill'],k) for k in ('PaintLead','PaintGroundOffset','PaintThickness')]
    skill=next(s for s in json.loads((project/'Assets/Resources/Skills/catalog.json').read_text())['skills'] if s['skillId']=='canvas_carve')
    verify((skill['stage2At'],skill['stage3At'])==(30,80),'canvas stages at 30 and 80')
    art=project/'Assets/Resources/GameArt/backgrounds'
    sets={s['id']:s['layers'] for s in json.loads((art/'room-sets.json').read_text())['sets']}
    targets={r['id']:r for r in rooms}
    route_metrics={}
    saturation={}
    backgrounds=set()
    formations=set()
    station_order=[]
    dynamic_count=0
    for r,previous in zip(rooms,original,strict=True):
        rid=r['id'];deck=room_faces(r);edges=fixed_edges(deck,physics);faces={f.name:f for f in edges}
        cam=r['cameraBounds']
        verify(r['hasFallLine'] and r['fallLine']<min(f.top for f in edges)-4 and
               all(contains(cam,(f.left+f.right)/2,f.top+1.9) for f in edges) and
               first_landing(deck,-3.4,max(f.top for f in edges)+2,r['fallLine'],physics) is None,rid+' camera and exposed fall column')
        for f in edges:
            verify(len(paths_from(edges,f))==len(edges),rid+' ordinary jump and walk from '+f.name)
        route_metrics[rid]=dict(authored=len(faces),directedPairs=sum(len(paths_from(edges,f))-1 for f in edges))
        stable={s['id']:faces[s['id']] for s in r['terrain'] if not s.get('latchCuts')}
        floor_ids={f['id'] for f in r['floors']}
        verify({f['id'] for f in previous['floors']}<=floor_ids and sum(bool(f.get('contactSave')) for f in r['floors'])==1,rid+' preserves old restore IDs and has a physical checkpoint')
        for m in r['floors']+r['points']:
            f=stable[m['surface']];x=m['x']
            verify(f.left+physics[6]<=x<=f.right-physics[6] and first_landing(deck,x,f.top+.05,r['fallLine'],physics)==f.name,rid+' permanent restore or interaction '+m['id'])
        for d in r['exits']:
            hosts=[f for f in stable.values() if f.left<d['x']<f.right and contains(d['approach'],d['x'],f.top+.05)]
            verify(len(hosts)==1 and (d['room'] not in targets or d.get('destinationFloor') in
                   {f['id'] for f in targets[d['room']]['floors']}),rid+' exact door and destination '+d['room'])
        placement_ids=[p['id'] for p in r['placements']]
        verify(Counter(placement_ids)==Counter(k for g in r['roster'] for k in g['placementIds']) and all(len(g['placementIds'])==g['count'] for g in r['roster']),rid+' explicit roster placement')
        for p in r['placements']:
            verify(any(f.left+.31 < p['x'] < f.right-.31 and abs(f.top+.05-p['y'])<.001 for f in edges)
                   and contains(p['active'],p['x'],p['y']),rid+' supported combat '+p['id'])
        form=tuple((g['id'],g['count'],g['segments'],g.get('setupSkill'),g.get('skillOnly',False)) for g in r['roster'])
        if form:
            verify(form not in formations,rid+' distinct encounter composition');formations.add(form)
        layers=sets[r['backgroundSet']]
        sig=hashlib.sha256((art/(layers[0]['resource']+'.png')).read_bytes()).hexdigest()
        verify(len(layers)==3 and rid==r['backgroundSet'] and sig not in backgrounds and
               layers[0]['factor']<layers[1]['factor']<layers[2]['factor'],rid+' three painted depth layers')
        backgrounds.add(sig)
        view=camera_view(r,layers,art)
        saturation[rid]=round(ImageStat.Stat(view.convert('HSV').getchannel('S')).mean[0]/255,4)
        verify(view.getchannel('A').getextrema()==(255,255),rid+' complete contact-camera coverage')
        marks=r.get('stationMarks',[])
        verify(r.get('stations',0)==len(marks) and len({(m['x'],m['surface']) for m in marks})==len(marks),rid+' distinct authored gaps')
        for j,m in enumerate(marks):
            for profile in skill['stages']:
                verify(paint_replay(r,m,profile,physics,geom),f"{rid} / fresh stroke, landing and reset {m['id']} stage {profile['stage']}")
            if r in rooms[6:8]:station_order.append(dict(room=rid,station=j+1,x=m['x'],floor=faces[m['surface']].top))
        dynamic_count+=len(marks)
    sat=[saturation[r['id']] for r in rooms[:8]+[rooms[9],rooms[8]]]
    verify(sat[0]<.03 and all(a<b for a,b in zip(sat,sat[1:])), 'ten contact camera composites progress from white paper to full colour')
    white=rooms[1];paths=white['viewingRoutes'];branches={}
    for route in paths:
        replay=branch_replay(white,route,physics)
        verify(replay and all(p['x']<=12 or p['x']>=32 for p in white['placements']) and not any(g.get('setupSkill') for g in white['roster']),route['id']+' exclusive waypoints, shared combat and openable exit')
        branches[route['id']]=replay
    upper,lower=(route['surfaces'] for route in paths)
    verify(upper[0]==lower[0]=='vestibule' and upper[-1]==lower[-1]=='landing' and not set(upper[1:-1]) & set(lower[1:-1]),'two disjoint viewing routes reunite on the same permanent landing')
    # Remove one intermediate support without moving any others. Each named route must fail its own jump.
    for route in paths:
        broken=deepcopy(white)
        next(f for f in broken['terrain'] if f['id']==route['surfaces'][2])['rect']['y']+=12
        verify(branch_replay(broken,route,physics) is None,route['id']+' rejects an unreachable interior ledge')
    closed=rooms[2];span=next(s for s in closed['terrain'] if s.get('latchCuts'))
    g=fixed_edges([f for f in room_faces(closed) if f.name!=span['id']],physics)
    west=next(f for f in g if f.name=='easel_bank')
    reachable=paths_from(edges=g,start=west)
    latches=closed['roster'][0]['placementIds']
    verify(span['latchCuts']==len(latches)==2 and all(any(f.left<p['x']<f.right and abs(f.top+.05-p['y'])<.001 for f in reachable)
           for p in closed['placements'] if p['id'] in latches),'both wire latches reachable while folding canvas is closed')
    elite=rooms[8];active=[(g['id'],p) for g in elite['roster'] for p in elite['placements'] if p['id'] in g['placementIds']]
    verify(any(a=='notary_guard' and b=='reflection_statue' and abs(p['y']-q['y'])<.1 and contains(p['active'],q['x'],q['y'])
               for a,p in active for b,q in active),'terrace armoured guard and reflection share a combat zone')
    seam=elite['seamRuns'][0]
    terrace={f.name:f for f in room_faces(elite)}
    verify(elite['singleSeams']==seam['count']==len(elite['stationMarks'])==4 and
           's.SupportingPlayer' in scripts['DistrictRoomLayout'] and 'floor.IsTouching' in scripts['StitchSurface2D'],
           'four silver seams precede the paint gaps and the action requires actual sewn support')
    for i,m in enumerate(elite['stationMarks']):
        bank,lip=terrace[f'terrace_0_{i}'],terrace[m['surface']]
        x=seam['x']+seam['stepX']*i
        verify(0 <= x-.45-bank.right < physics[6] and 0 <= lip.left-x-.45 < physics[6] and
               abs(seam['y']-bank.top) < .1 and bank.top==lip.top and x<lip.left<m['x'],
               'terrace seam, tongue and open paint gap '+str(i+1))
    def supply(r):
        return r.get('stations',0)+sum(g['count']*g['segments']*g.get('setupHits',1) for g in r['roster'] if g.get('setupSkill'))
    verify([supply(r) for r in original[6:8]]==[13,12] and [supply(r) for r in rooms[6:8]]==[48,48] and len(station_order)==80,
           'first-visit supply 25 to 96 with 80 independent activations on empty spans alone')
    first=rooms[6]['floors'][0]
    verify(first['surface']==rooms[6]['stationMarks'][0]['surface'] and abs(first['x']+1.55-station_order[0]['x'])<.001,
           'ground cast at either duel outcome arrival reaches station one')
    # Replay the station/guard order used by the keyboard probe. Count each legal setup on
    # a different activation, and each gap only after the support test above passed.
    route_uses=[]
    for r in rooms[6:8]:
        faces={f.name:f for f in room_faces(r)}
        groups={key:g for g in r['roster'] for key in g['placementIds']}
        visited=set()
        marks=r['stationMarks']
        for j,m in enumerate(marks):
            route_uses.append(dict(room=r['id'],station=j+1))
            if j+1<len(marks) and marks[j+1]['surface']==m['landingSurface']: continue
            f=faces[m['landingSurface']]
            for actor in r['placements']:
                if not(f.left<actor['x']<f.right and abs(actor['y']-f.top)<.1): continue
                group=groups[actor['id']]
                direction=1 if actor['x']-f.left>=f.right-actor['x'] else -1
                launch=actor['x']-1.45*direction
                retreat=actor['x']-2.4*direction
                reach=skill['stages'][0]['reach']
                far=launch+direction*(geom[0]+reach)
                verify(actor['id'] not in visited and f.left+physics[6]<=retreat<=f.right-physics[6] and
                       contains(actor['active'],launch,f.top+.05) and (far-actor['x'])*direction>0 and
                       actor['y']<f.top+geom[1] and group['setupSkill']=='canvas_carve',
                       r['id']+' / grounded paint setup, release and sword '+actor['id'])
                for segment in range(group['segments']):
                    route_uses.append(dict(room=r['id'],target=actor['id'],segment=segment+1))
                visited.add(actor['id'])
        verify(len(visited)==len(r['placements']),r['id']+' every return guard met on its own landing')
    verify(len(route_uses)==96 and route_uses[29].get('station')==24 and route_uses[79].get('station')==26,
           'ordered return with finite guard setups reaches stages at atelier 24 and archive 26')
    # A single stroke at the first gap catches the drop. Removing it leaves open air at
    # the very same x and leaves the physical restore marker on its original surface.
    r=rooms[6];m=r['stationMarks'][0];deck=room_faces(r)
    floor=next(f for f in deck if f.name==first['surface'])
    top=floor.top+geom[1]+geom[2]/2
    stroke=Face('wet',m['x']-1.2,m['x']+1.2,top-geom[2],top)
    def drop_at(x,added):
        return first_landing([*deck,*added],x,top+.05,r['fallLine'],physics)
    reset_witness=dict(wetGap=drop_at(m['x'],[stroke]),resetGap=drop_at(m['x'],[]),
                       wetCheckpoint=drop_at(first['x'],[stroke]),resetCheckpoint=drop_at(first['x'],[]))
    verify(reset_witness==dict(wetGap='wet',resetGap=None,wetCheckpoint=floor.name,resetCheckpoint=floor.name),
           'same gap and restore point dropped before and after removing the paint collider')
    trip=shortcut(rooms,physics)
    d=next(d for d in rooms[4]['exits'] if d['room']==white['id'])
    head=next(m for m in rooms[4]['floors'] if m['id']=='gallery_frame_head')
    foot=next(m for m in white['floors'] if m['id']==d['destinationFloor'])
    head_y=next(f.top for f in room_faces(rooms[4]) if f.name==head['surface'])
    trip['directMovementSeconds']=abs(d['x']-head['x'])/physics[3]
    verify(contains(d['approach'],head['x'],head_y+.05) and foot['surface']=='landing' and trip['directMovementSeconds']==0,
           'same marked start and fixed arrival of the direct door')

    verify(d['gate']=='investigation_complete' and d['destinationFloor']=='gallery_frame_landing' and
           trip['before']==[r['id'] for r in rooms[4:0:-1]] and trip['after']==[rooms[4]['id'],white['id']] and trip['ordinaryMovementSeconds']>0,
           'framed passage shrinks the same return from three doors to one')
    layout,paint,canvas=(scripts[k] for k in ('RepresentativeRoomLayout','PaintSurface2D','CanvasCarveSkill'))
    verify('art.transform.SetParent(go.transform, false)' in layout and 'box.autoTiling = false' in layout and
           'box.size = new Vector2(width, height)' in layout and 'OperaSprite("gallery-gesso", 1.6f)' in layout,'gallery terrain collider owns authored size independently of tile pitch')
    verify('art.transform.SetParent(transform, false)' in paint and 'visual = art.AddComponent<SpriteRenderer>()' in paint and
           'floor.autoTiling = false' in paint and 'floor.size = size' in paint and 'OperaSprite("gallery-oil", 2.4f)' in scripts['GalleryPaint'],
           'separate paint collision and bristle art')
    reset=canvas.split('public override void ResetAttempt()')[1].split('private void OnDestroy')[0]
    verify('foreach (var paint in PaintSurface2D.All.ToArray())' in reset and 'paint.Remove();' in reset and 'surfaces' not in reset and
           'fixedFloors' not in paint and 'BoxCollider2D' not in scripts['GalleryPaint'] and 'floor.IsTouching(player.GetComponent<Collider2D>())' in paint,
           'reset removes only brush objects and static marks cannot award support credit')
    probe,driver,runtime=(scripts[k] for k in ('GalleryDistrictProbe','AutoTestDriver','AutoTestRuntime'))
    verify(all(s in probe for s in ('Entry, White, Bridge, Garden, Foyer, Arena, Atelier, Vault, Window, Terrace','area.StandingOn(floor.surface)',
           'Mathf.Abs(feet.x - floor.x) > .8f','scene_floor:gallery:', 'TerrainWidth.Measure(area, result.widths)', 'station != 40',
           'e.after == 30','e.after == 80','!uses.Add(e.useId)','!stations.Add(e.targetId)','IsTouching(actor)','firstStroke == null',
           'result.shortcutSeconds >= result.longSeconds','result.branchContacts.Add(id)','result.viewingRoute = path.id','!result.eliteCleared')),
           'slot six records feet, both clocks, real paint contacts, actual branch and mixed encounter')
    verify(all(s in runtime for s in ('ReadPreviousSlotHashes(5)','save.slotId == "slot-6"', 'if (!gallery.previousSlotsUnchanged)',
           'if (!gallery.slotReadback)','"scene_floor:gallery:gallery_rest_09"','requested && sandbox != null ? 6 : 3')) and
           'bot.Select(5)' in probe and 'gallery != null && e.kind == "attempt_reset"' in driver and 'now - entered > 1500) { Stop("Gallery district deadline")' in driver,
           'five isolated files, sixth slot disk receipt and unchanged failure deadlines')
    base=sum(len(r['terrain']) for area in ('opera','bell') for r in json.loads((project/f'Assets/Resources/Story/{area}.json').read_text())['rooms'])
    count=sum(a['authored'] for a in route_metrics.values())
    verify(base==142 and count==sum(len(r['terrain']) for r in rooms) and dynamic_count==84,'142 opera and bell surfaces plus all authored gallery rectangles, separately counted dynamic placements')
    return dict(rowsAndLinksSha256=hashlib.sha256(rows.encode()).hexdigest(),rooms=route_metrics,branches=branches,
                contactCameraSaturation=saturation,firstReturnStations=station_order,stationOnlyThresholds=[station_order[29],station_order[79]],
                firstReturnSupply=[supply(r) for r in rooms[6:8]],resetWitness=reset_witness,
                probeOrderThresholds=[route_uses[29],route_uses[79]],shortcut=trip,priorTerrain=base,galleryTerrain=count,
                combinedTerrain=base+count,paintGaps=dynamic_count,unityMeasured=False,
                districtAdvance=validate_district_advance(verify,project,scripts,physics),
                run25Bridge=validate_gallery_bridge(verify,project,closed,scripts,physics),
                terrainBounds=validate_terrain_bounds(verify,project,scripts))


if __name__ == '__main__':
    import argparse
    parser=argparse.ArgumentParser()
    parser.add_argument('--background-sheet',type=Path,required=True,metavar='UNITY_PROJECT')
    background_sheet(parser.parse_args().background_sheet)
