"""Replay authored terrain and calibrate the model against the QA 24 Unity audit."""
import gzip
import json
import math
import re
from collections import Counter
from pathlib import Path
import shutil
import subprocess
import tempfile

from PIL import Image
import yaml


def aligned(a, b):
    # The playtest compares XY size and center as vectors, not each axis separately.
    return math.dist(a, b) <= .005


def world_rect(center, size, direction):
    c, s = map(abs, direction)
    w, h = size
    width, height = c * w + s * h, s * w + c * h
    return (center[0] - width / 2, center[1] - height / 2, width, height)


def same_rect(a, b):
    centers = [(r[0] + r[2] / 2, r[1] + r[3] / 2) for r in (a, b)]
    return (aligned(a[2:], b[2:]) and aligned(*centers) and
            all(abs(a[i] - b[i]) <= .005 for i in (0, 1)))


def tile_units(project, scripts, verify):
    art = project / 'Assets/Resources/GameArt/backgrounds'
    layout = scripts['RepresentativeRoomLayout']
    importer = (project / 'Assets/Editor/PrototypeProjectSetup.cs').read_text()
    verify(all(line in importer for line in ('assetPath.StartsWith("Assets/Resources/GameArt/")',
           'importer.textureType = TextureImporterType.Default;', 'importer.npotScale = TextureImporterNPOTScale.None;')),
           'terrain PNGs use the GameArt default texture importer without NPOT scaling')
    import_limit = int(re.search(r'importer\.maxTextureSize = (\d+);', importer)[1])
    terrain = scripts['StoryTerrain']
    units = {'hub': (1., 1.), 'velvet': (1., 1.)}
    for stage, resource in (('clock', 'brass-plate'), ('opera', 'opera-inlay'), ('gallery', 'gallery-gesso')):
        pitch = (re.search(r'plate\.width / ([.\d]+)f', terrain) if stage == 'clock' else
                 re.search(r'OperaSprite\("' + re.escape(resource) + r'", ([.\d]+)f\)', layout))
        asset = art / (resource + '.png')
        meta = yaml.safe_load(asset.with_suffix('.png.meta').read_text())['TextureImporter']
        with Image.open(asset) as im:
            width, height = im.size
        # Resources.Load<Texture2D> uses the full imported image. Sprite.Create supplies
        # its own PPU, so the inactive Sprite importer PPU does not determine native units.
        verify(meta['textureType'] == meta['spriteMode'] == meta['nPOTScale'] == 0 and
               max(width, height) <= min(import_limit, meta['maxTextureSize']) and
               all(not p['overridden'] for p in meta['platformSettings']),
               resource + ' imports at source dimensions for runtime Sprite.Create')
        ppu = width / float(pitch[1])
        units[stage] = (width / ppu, height / ppu)
    units['bell'] = units['clock']
    return units


def child_scale(formula, width, height, drawn):
    # Read the components of the actual child scale assignment. An unfitted child starts at one.
    if formula is None:
        return (1., 1.)
    values = {'width': width, 'height': height, 'drawn.x': drawn[0], 'drawn.y': drawn[1], '1': 1.}
    def component(expr):
        terms = expr.strip().split('/')
        if len(terms) == 1:
            return values[terms[0].strip()]
        a, b = terms
        return values[a.strip()] / values[b.strip()]
    return tuple(component(axis) for axis in formula)


def measured_rect(tile, component):
    c, s = tile[component + 'Center'], tile[component + 'Size']
    return c['x'] - s['x']/2, c['y'] - s['y']/2, s['x'], s['y']


def agrees_with_reading(tile, local_size, scale, units, drawn, collision, plated):
    def xy(field):
        return tile[field]['x'], tile[field]['y']

    box_scale = (1., 1.) if plated else scale
    return (aligned(units, xy('spriteUnits')) and aligned(scale, xy('childScale')) and
            aligned(box_scale, xy('boxScale')) and
            aligned(local_size, xy('localBox')) and aligned(local_size, xy('localTile')) and
            aligned(local_size, xy('localDrawn')) and tile['directChild'] == plated and
            not tile['autoTiling'] and same_rect(collision, measured_rect(tile, 'expected')) and
            same_rect(collision, measured_rect(tile, 'box')) and same_rect(drawn, measured_rect(tile, 'drawn')) and
            bool(tile['error']) == (not same_rect(drawn, collision)))


def validate_terrain_bounds(verify, project, scripts):
    layout = re.sub(r'//[^\n]*', '', scripts['RepresentativeRoomLayout'])
    block = ' '.join(layout.split('private GameObject Block(')[1].split('return go;')[0].split())
    fit = re.search(r'if \(plated && solid\) \{ var drawn = visual\.bounds\.size; '
                    r'(?:if \(!\(drawn\.x > 0 && drawn\.y > 0\)\) throw [^;]+; )?'
                    r'art\.transform\.localScale = new Vector3\(([^,]+), ([^,]+), 1\); \}', block)
    formula = fit.groups() if fit else None
    draw_mode = 'visual.drawMode = SpriteDrawMode.Tiled;'
    tile_size = 'visual.size = new Vector2(width, height);'
    sprite = re.search(r'visual\.sprite = Room\.stage == "gallery" \? OperaSprite\("gallery-gesso", [\d.]+f\) : '
                       r'Room\.stage == "opera" \? OperaSprite\("opera-inlay", [\d.]+f\) : '
                       r'plated \? BrassSprite\(\) : WeaveSprite\(\);', block)
    # Constrain the inputs to the numeric replay. Unexpected changes fail closed and the
    # editor preflight independently instantiates the production hierarchy.
    # The QA24 source fitted the child after configuring the renderer. The production
    # path initializes the final sprite directly in Tiled mode before setting its size.
    verify(sprite and ('if (woven || plated) { ' + ' '.join(
               (sprite[0], draw_mode, tile_size) if fit else (draw_mode, sprite[0], tile_size))) in block and
           (fit or ('else visual.sprite = square;' in block and block.count('visual.sprite = ') == 2)) and
           block.count('visual.size') == 1 and block.count('visual.drawMode') == 1 and
           'var visual = art.AddComponent<SpriteRenderer>();' in block and
           'if (!woven && !plated) go.transform.localScale = new Vector3(width, height, 1);' in block and
           'if (plated) box.autoTiling = false; if (woven || plated) box.size = new Vector2(width, height);' in block and
           block.count('.localScale') == 1 + int(formula is not None) and
           not any(op in block for op in ('box.offset', '.localPosition', '.localRotation', '.position =', '.rotation =',
                                          'localBounds', '.bounds =')),
           'tile replay uses the production draw mode, size, child transform and independent box')
    terrain = ' '.join(scripts['StoryTerrain'].split())
    build = terrain.split('private void BuildSurfaces()')[1].split('internal bool StandingOn(')[0]
    opera = ' '.join(scripts['OperaFixtures'].split())
    verify('new Rect(0, 0, 2, 2), Vector2.one * .5f, 2)' in layout and
           'Resources.Load<Texture2D>("GameArt/backgrounds/brass-plate")' in terrain and
           'Resources.Load<Texture2D>("GameArt/backgrounds/" + resource)' in opera and
           'plate.height), Vector2.one * .5f, plate.width /' in terrain and
           'size, size), Vector2.one * .5f, size, 0, SpriteMeshType.FullRect)' in terrain and
           'art.height), Vector2.one * .5f, art.width / width, 0, SpriteMeshType.FullRect)' in opera and
           'r.x + r.width / 2, r.y + r.height / 2, r.width, r.height' in terrain and
           'box.size = new Vector2(tangent.magnitude, r.height);' in terrain and
           'go.GetComponentInChildren<SpriteRenderer>().size = box.size;' in build and
           'box.offset' not in build and build.count('.transform.') == 2 and
           'go.transform.position = new Vector2(r.x, r.y + r.height) + tangent / 2 - normal * (r.height / 2);' in build,
           'full textures, centered pivots, tile PPU and rotated ramp sizes drive the replay')
    runner = (project / 'Assets/Editor/AutoTestRunner.cs').read_text()
    audit = scripts['AuthoredTerrainAudit']
    verify('RepresentativeArea.AuditAuthoredTerrain(' in runner and 'area.BuildSurfaces();' in audit and
           'Physics2D.SyncTransforms();' in audit and 'VerifyGalleryTile(s);' in audit and
           'reading.drawnSize = tile.bounds.size;' in audit and 'reading.boxSize = box.bounds.size;' in audit and
           '(a - b).sqrMagnitude <= .000025f' in audit and
           scripts['PlaytestObservations'].count('sqrMagnitude > .000025f') >= 3,
           'editor preflight reads live XY bounds and calls the slot-six verifier')

    units = tile_units(project, scripts, verify)
    with gzip.open(project / 'tools/run24-terrain-bounds.json.gz', 'rt', encoding='utf-8-sig') as f:
        qa24 = json.load(f)
    readings = {(t['room'], t['surface']): t for t in qa24['tiles']}
    verify(len(readings) == len(qa24['tiles']) == 435, 'QA24 has 435 unique room and surface readings')
    seen = Counter()
    before, after = Counter(), Counter()
    rooms = {}
    for path in sorted((project / 'Assets/Resources/Story').glob('*.json')):
        for room in json.loads(path.read_text()).get('rooms', []):
            stage = room['stage']
            for face in room.get('terrain', []):
                r = face['rect']
                w, h = r['width'], r['height']
                rise = face.get('rampRise', 0)
                length = math.hypot(w, rise)
                c, s = w / length, rise / length
                center = (r['x'] + (w + s*h) / 2, r['y'] + h + (rise - c*h) / 2)
                size = (length, h)
                collision = world_rect(center, size, (c, s))
                plated = stage not in ('hub', 'velvet')
                pitch = units[stage]
                # QA24's assigned scale reveals the bounds read inside Block, before
                # ramp rotation. At audit time localBounds is the requested tiled size.
                initial = (w * pitch[0], h * pitch[1])
                local = (1., 1.) if stage == 'hub' else size
                if plated:
                    old_scale = (w / initial[0], h / initial[1])
                    scale = child_scale(formula, w, h, initial)
                else:
                    scale = old_scale = (w, h) if stage == 'hub' else (1., 1.)
                previous = world_rect(center, (local[0]*old_scale[0], local[1]*old_scale[1]), (c, s))
                drawn = world_rect(center, (local[0]*scale[0], local[1]*scale[1]), (c, s))
                key = room['id'], face['id']
                if key in readings:
                    seen[key] += 1
                    verify(agrees_with_reading(readings[key], local, old_scale, pitch, previous, collision, plated),
                           'QA24 measured sprite, scale, local and world rectangles and verdict: ' + '/'.join(key))
                    before[stage] += int(not same_rect(previous, collision))
                ok = same_rect(drawn, collision)
                verify(ok, room['id'] + '/' + face['id'] + ' rendered surface agrees with collision in world XY')
                after[stage] += int(not ok)
                rooms.setdefault(room['id'], {})[face['id']] = [
                    [round(v, 6) for v in rect] for rect in (previous, drawn, collision)]

    verify(seen == Counter(readings.keys()), 'every archived QA24 surface was replayed exactly once')
    verify(sum(before.values()) == 357, 'QA24 replay reproduces 357 failed surfaces')
    # Historical screenshot witnesses for QA23. Native pitch explains the old screen
    # pixels; it is not a multiplier on the local tiled bounds read by the QA24 audit.
    # Keep raster rounding (one pixel) separate from the 0.005-unit geometry tolerance.
    for stage, thickness, pixels in (('gallery', .38, 11), ('opera', .35, 8), ('bell', .35, 36),
                                     ('clock', .35, 37), ('velvet', .44, 29)):
        verify(abs(thickness * units[stage][1] * 720/11 - pixels) <= 1,
               'run23 ' + stage + ' visible tile thickness reproduced to one screen pixel')
    # Frame 041: centers of the marks at x=15 and 28 lie at pixels 493.5 and 1343.5.
    # Use these to locate the three visible platform ends on the independently calibrated X axis.
    pixels_per_unit = 850 / 13
    # Use the historical room positions for the screenshot witness.
    for face, center, width, side, pixel in (('paper', 7, 14, 1, 702), ('folio', 17.5, 13, 1, 1337),
                                             ('studio_door', 38.5, 13, -1, 1350)):
        edge = center + side * width * units['gallery'][0] / 2
        verify(abs(493.5 + (edge - 15)*pixels_per_unit - pixel) <= 1,
               'run23 gallery_01_entry/' + face + ' observed screen edge reproduced to one pixel')
    verify(not same_rect((-4.2, -.272333, 22.4, .164667), (0., -.38, 14., .38)) and
           not aligned((.004, .004), (0., 0.)) and not aligned((.0051, 0.), (0., 0.)) and
           aligned((.005, 0.), (0., 0.)), 'reject original paper and combined XY drift at the probe threshold')
    return dict(rectangles='At spawn, [x,y,width,height]. QA24 render replay, current render model, authored collision.',
                qa24UnityVersion=qa24['unityVersion'], nativeTileUnits=units,
                previousMismatches=before, currentMismatches=after, rooms=rooms)


def measure_editor_tiles(editor, project, model, check):
    # Import into an isolated project so the static run can also be used beside an open editor.
    with tempfile.TemporaryDirectory(prefix='notary-terrain-') as tmp:
        root = Path(tmp)
        for name in ('Assets', 'Packages', 'ProjectSettings'):
            shutil.copytree(project / name, root / 'Project' / name)
        output = root / 'terrain-bounds.json'
        run = subprocess.run([str(editor), '-batchmode', '-quit', '-projectPath', str(root / 'Project'),
                              '-executeMethod', 'AutoTestRunner.VerifyTerrain', '-terrainBoundsPath', str(output),
                              '-logFile', str(root / 'editor.log')])
        if not output.is_file():
            log = root / 'editor.log'
            detail = log.read_text(errors='replace')[-2000:] if log.is_file() else ''
            raise RuntimeError(f'Unity exited {run.returncode} before writing terrain bounds. {detail}')
        readings = json.loads(output.read_text(encoding='utf-8-sig'))
        check(run.returncode == 0, 'Unity terrain audit completed without errors')
    expected = {(room, face): rects for room, faces in model['rooms'].items() for face, rects in faces.items()}
    ids = Counter((tile['room'], tile['surface']) for tile in readings['tiles'])
    check(ids == Counter(expected.keys()), 'Unity measured every authored surface exactly once')
    for tile in readings['tiles']:
        key = tile['room'], tile['surface']
        box, drawn = measured_rect(tile, 'box'), measured_rect(tile, 'drawn')
        check(key in expected and not tile.get('error') and same_rect(box, expected[key][2]) and
              same_rect(drawn, expected[key][1]) and same_rect(drawn, box),
              'Unity tile bounds: ' + '/'.join(key))
    return readings
