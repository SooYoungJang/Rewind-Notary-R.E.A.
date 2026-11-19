import hashlib
import json
import re
from pathlib import Path

from PIL import Image


DISTRICTS = ('velvet', 'clock', 'opera', 'bell', 'gallery')
ROSTER = ('sword_latch', 'notary_guard', 'shell_sentry', 'reflection_statue')
EFFECTS = ('silver-stitch', 'pendulum-rewind', 'curtain-call-waltz', 'resonance-forge',
           'canvas-carve', 'basic-sword-mastery')


def validate_art_integration(check, project, scripts):
    def verify(ok, message):
        check(bool(ok), 'Art integration: ' + message)

    def source(name):
        return ' '.join(re.sub(r'//[^\n]*|/\*[\s\S]*?\*/', '', scripts[name]).split())

    root = project / 'Assets/Resources/GameArt'
    documents = [json.loads(path.read_text(encoding='utf-8'))
                 for path in sorted((project / 'Assets/Resources/Story').glob('*.json'))]
    rooms = [room for document in documents for room in document['rooms']]
    by_id = {room['id']: room for room in rooms}
    base_exits = sum(len(room['exits']) for room in rooms)
    links = [link for document in documents for link in document.get('links', [])]
    for link in links:
        verify(link['source'] in by_id and link['room'] in by_id, 'district or finale link ' + link['room'])
        if link['source'] in by_id:
            by_id[link['source']]['exits'].append(link)
    resources = {f'enemy-{enemy.replace("_", "-")}-{district}' for district in DISTRICTS for enemy in ROSTER}
    resources.update(f'door-{district}-{state}' for district in (*DISTRICTS, 'hub') for state in ('locked', 'open'))
    resources.update(f'{kind}-{district}' for kind in ('tile', 'station') for district in DISTRICTS)
    resources.update(f'fx-{effect}-additive' for effect in EFFECTS)
    resources.update(('notary-desk-props', 'seam-marker', 'paint-support-marker', 'stitch-platform-installed',
                      'stitch-platform-uninstalled', 'stitch-platform-uninstalled-v2'))
    valid = set()
    fingerprints = set()
    for name in sorted(resources):
        tile, effect = name.startswith('tile-'), name.startswith('fx-')
        path = root / ('backgrounds' if tile else 'sprites') / (name + '.png')
        meta = path.with_suffix('.png.meta')
        ok = path.is_file() and meta.is_file()
        if ok:
            importer = meta.read_text(encoding='utf-8')
            with Image.open(path) as image:
                image.load()
                opaque = tile or effect
                alpha = image.convert('RGBA').getchannel('A')
                histogram = alpha.histogram()
                ok = (image.format == 'PNG' and max(image.size) <= (1024 if opaque else 512)
                      and (not tile or image.size == (1024, 1024))
                      and any(lo < hi for lo, hi in image.convert('RGB').getextrema())
                      and (histogram[255] == image.width * image.height if opaque else
                           histogram[0] > 0 and histogram[255] > 0 and sum(histogram[1:255]) == 0)
                      and re.search(r'^guid: [a-f0-9]{32}$', importer, re.M)
                      and all(item in importer for item in ('TextureImporter:', 'nPOTScale: 0', 'spriteMode: 0',
                                                            'textureType: 0', 'enableMipMap: 0')))
                fingerprint = hashlib.sha256(image.tobytes()).digest()
                verify(fingerprint not in fingerprints, name + ' has distinct artwork')
                fingerprints.add(fingerprint)
        verify(ok, name + ' resolves to imported artwork at display resolution')
        if ok:
            valid.add(name)

    art = source('RoomArt')
    themes = {}
    for cases, result in re.findall(r'((?:case "\w+":\s*)+)return (stage|"\w+");', art):
        for stage in re.findall(r'case "(\w+)"', cases):
            themes[stage] = stage if result == 'stage' else result.strip('"')
    verify(all(themes.get(s) == s for s in DISTRICTS) and
           all(themes.get(s) in DISTRICTS for s in ('hub', 'janhyang', 'cassel')), 'every playable stage has artwork')
    verify(all(key in art for key in ('Picture("enemy-" + id.Replace(\'_\', \'-\') + "-" + District(stage))',
           'Picture("station-" + District(stage))', 'Load("GameArt/backgrounds/tile-" + District(stage)',
           'Load("GameArt/sprites/" + name', 'Resources.Load<Texture2D>(path)', 'Sprite.Create(art,',
           'if (art == null) throw', 'view.sprite = sprite;')), 'game resource names resolve to full texture sprites')
    court_themes = re.search(r'string theme = ([^?]+)\? "hub" : District\(stage\);', art)
    hub_doors = set(re.findall(r'stage == "(\w+)"', court_themes[1])) if court_themes else set()
    verify(hub_doors == {'hub', 'janhyang', 'cassel'} and
           'Picture("door-" + theme + (open ? "-open" : "-locked"))' in art and
           'canPass ? open : locked' in art and 'RoomArt.Door(stage, false)' in art and
           'RoomArt.Door(stage, true)' in art, 'each gate selects separate open and locked pictures')

    layout, district = source('RepresentativeRoomLayout'), source('DistrictRoomLayout')
    roster = layout.split('foreach (var group in Room.roster)')[-1].split('private void DrawWaterway')[0]
    verify(re.search(r'\}\s*target.Initialize\(kind, spawn, player, RoomArt.Enemy\(Room.stage, group.id\)', roster)
           and 'roomTargets.Add(target)' in roster and 'visual.sprite = sprite;' in source('WaveTarget2D'),
           'every roster spawn receives its authored identity even without placement or in the final hall')
    verify('RoomArt.Enemy(Room.stage, spawn.archetypeId)' in source('FirstDeparture') and
           'RoomArt.Enemy(Room.stage, "reflection_statue")' in source('FinaleRoomLayout') and
           'RoomArt.Enemy("velvet", spawn.archetypeId)' in source('WaveStageDirector'),
           'entry reinforcements, crucible training and practice waves use roster art')
    loop = district.split('for (int i = 0; i < Room.stations; i++)')[-1].split('if (Room.Point')[0]
    mark = loop.find('RoomArt.Station(Room.stage)')
    verify(0 <= mark < loop.find('if (') and
           'PlaceMarker(id + "_marker"' in loop[:mark], 'station marker precedes gallery gaps, emitters and mechanisms')
    verify('Room.stage == "velvet" ? RoomArt.Station(Room.stage)' in layout and
           'PlaceMarker("seam_marker_" + i' in layout, 'velvet sewing stations accompany the actual anchors')
    verify('foreach (var e in Room.exits)' in layout and 'doorViews.Add(e, new RoomDoorView(' in layout and
           'doorViews[e].Show(Gate(e))' in layout, 'story doors use the live gate result')
    arena = source('CasselArena')
    verify('Room("court_04_tribunal")' in arena and 'foreach (var exit in room.exits)' in arena and
           'doors.Add(exit, new RoomDoorView(door, room.stage))' in arena and
           'FinaleEndingEvaluator.Evaluate(session.Finale.State)' in arena and 'entry.Key.gate == "settled" && settled' in arena,
           'the separate Cassel arena builds its authored exits and displays settlement')
    seam = source('StitchSurface2D')
    verify('RoomArt.Picture("stitch-platform-installed")' in seam and
           'RoomArt.Picture("stitch-platform-uninstalled-v2")' in seam and 'taut = Installed || retiring || releasedBridge' in seam
           and 'visual.sprite = taut ? installedArt : looseArt' in seam and
           'SkillStroke.Show' not in seam.partition('bool Install(')[2].partition('internal bool SupportingPlayer')[0],
           'installed, retiring and loose sewing surfaces use the two pictures without installation strokes')
    verify('visual.sprite = RoomArt.Tile(Room.stage, plated)' in layout and
           'else visual.sprite = RoomArt.Tile(Room.stage, false)' in layout and 'RoomArt.Tile("cassel", false)' in arena,
           'authored, hub and final floors have district tile art')

    for name, code in scripts.items():
        for line in code.splitlines():
            if 'Texture2D.whiteTexture' not in line or 'GUI.DrawTexture(' in line:
                continue
            effects = {'SkillStroke': 'effect.sprite', 'SkillEvolutionBurst': 'effect.sprite',
                       'SkillProjectile2D': 'sprite', 'BellWaves': 'waveInk'}
            verify(name in effects and (effects[name] + ' = Sprite.Create(Texture2D.whiteTexture') in line,
                   'white texture belongs to an explicit effect in ' + name)

    totals = dict(rooms=len(rooms), authoredExits=base_exits, linkedExits=len(links),
                  exits=0, enemies=0, stations=0, anchors=0, velvetStations=0)
    for room in rooms:
        stage, rid = room['stage'], room['id']
        theme = themes.get(stage)
        verify('tile-' + str(theme) in valid, rid + ' / terrain')
        for index, door in enumerate(room['exits']):
            door_theme = 'hub' if stage in hub_doors else theme
            verify(all(f'door-{door_theme}-{state}' in valid for state in ('locked', 'open')),
                   f'{rid} / exit {index} to {door["room"]}')
            totals['exits'] += 1
        for group in room['roster']:
            for index in range(group['count']):
                verify(group['id'] in ROSTER and f'enemy-{group["id"].replace("_", "-")}-{theme}' in valid,
                       f'{rid} / {group["id"]} {index}')
                totals['enemies'] += 1
        for index in range(room.get('stations', 0)):
            verify(f'station-{theme}' in valid, f'{rid} / station {index}')
            totals['stations'] += 1
        for index in range(room.get('stitchAnchors', 0)):
            verify({'stitch-platform-uninstalled-v2', 'stitch-platform-installed',
                    'station-velvet' if stage == 'velvet' else 'seam-marker'} <= valid, f'{rid} / stitch {index}')
            totals['anchors'] += 1
            totals['velvetStations'] += stage == 'velvet'
    return totals


if __name__ == '__main__':
    project = Path(__file__).resolve().parents[1]
    scripts = {path.stem: path.read_text(encoding='utf-8') for path in (project / 'Assets/Scripts').rglob('*.cs')}
    passed, errors = [], []
    totals = validate_art_integration(lambda ok, msg: (passed if ok else errors).append(msg), project, scripts)
    print(json.dumps(dict(passed=len(passed), errors=errors, **totals), ensure_ascii=False))
    raise SystemExit(bool(errors))
