"""Undo exact story-link edits only for historical hash comparisons."""
import hashlib
import json
from pathlib import Path
from localization_followup import prior_translation_source

ROOT = Path(__file__).resolve().parents[1]
EXTENSIONS = json.loads(Path(__file__).with_name('story-link-extensions.json').read_text())


SCENE_EDITS = json.loads(Path(__file__).with_name('scene-link-extensions.json').read_text())


AREA_EDITS = json.loads(Path(__file__).with_name('area-link-extensions.json').read_text())


FINALE_EDITS = json.loads(Path(__file__).with_name('finale-link-extensions.json').read_text())


AUTOTEST_EDITS = json.loads(Path(__file__).with_name('autotest-extensions.json').read_text())
FEEL_EDITS = json.loads(Path(__file__).with_name('feel-pass-extensions.json').read_text())


LEVEL_EDITS = json.loads(Path(__file__).with_name('level-step1-extensions.json').read_text())


FLOW_EDITS = json.loads(Path(__file__).with_name('flow-assembly-extensions.json').read_text())


def reverse_exact(item, source):
    if not item or hashlib.sha256(source.encode()).hexdigest() != item['updated']:
        return source
    lines = source.splitlines(keepends=True)
    for start, end, old in reversed(item['edits']):
        lines[start:end] = old
    previous = ''.join(lines)
    return previous if hashlib.sha256(previous.encode()).hexdigest() == item['original'] else source


STEP3_EDITS = json.loads(Path(__file__).with_name('level-step3-extensions.json').read_text())


STEP4_EDITS = json.loads(Path(__file__).with_name('level-step4-extensions.json').read_text())


STEP5_EDITS = json.loads(Path(__file__).with_name('level-step5-extensions.json').read_text())


STEP6_EDITS = json.loads(Path(__file__).with_name('level-step6-extensions.json').read_text())


PASS2_EDITS = json.loads(Path(__file__).with_name('level-design-pass2-extensions.json').read_text())
FEEL_FIX2_EDITS = json.loads(Path(__file__).with_name('feel-fix2-extensions.json').read_text())
ART_EDITS = json.loads(Path(__file__).with_name('art-integration-extensions.json').read_text(encoding='utf-8'))
ART_FIX_EDITS = json.loads(Path(__file__).with_name('art-fix-extensions.json').read_text(encoding='utf-8'))


def prior_pass2_source(path, source):
    path = Path(path).as_posix()
    source = reverse_exact(ART_FIX_EDITS.get(path), source)
    source = reverse_exact(ART_EDITS.get(path), source)
    source = reverse_exact(FEEL_FIX2_EDITS.get(str(path)), source)
    return reverse_exact(PASS2_EDITS.get(str(path)), source)


def prior_level6_source(path, source):
    return reverse_exact(STEP6_EDITS.get(str(path)), prior_pass2_source(path, source))


def prior_level5_source(path, source):
    return reverse_exact(STEP5_EDITS.get(str(path)), prior_level6_source(path, source))


def prior_level4_source(path, source):
    return reverse_exact(STEP4_EDITS.get(str(path)), prior_level5_source(path, source))


def prior_level3_source(path, source):
    project = Path('unity/RewindNotary')
    path = Path(path)
    if path.is_relative_to(project):
        path = path.relative_to(project)
    return reverse_exact(STEP3_EDITS.get(path.as_posix()), prior_level4_source(path, source))


def prior_story_source(path, source):
    path = Path(path)
    if path.is_absolute():
        try:
            path = path.relative_to(ROOT)
        except ValueError:
            return source
    if path.parent == Path('unity/RewindNotary/Assets/Resources/Localization') and str(path) in FINALE_EDITS:
        source = prior_translation_source(path.stem, source, FINALE_EDITS[str(path)]['updated'])
    source = prior_level3_source(path, source)
    source = reverse_exact(LEVEL_EDITS.get(str(path)), source)
    source = reverse_exact(FEEL_EDITS.get(str(path)), source)
    source = reverse_exact(AUTOTEST_EDITS.get(str(path)), source)
    source = reverse_exact(FLOW_EDITS.get(str(path)), source)
    source = reverse_exact(FINALE_EDITS.get(str(path)), source)
    source = reverse_exact(AREA_EDITS.get(str(path)), source)
    source = reverse_exact(SCENE_EDITS.get(str(path)), source)
    item = EXTENSIONS.get(str(path))
    if not item:
        return source
    lines = source.splitlines(keepends=True)
    for start, before, after in reversed(item['edits']):
        if lines[start:start + len(after)] != after:
            return source
        lines[start:start + len(after)] = before
    old = ''.join(lines)
    return old if hashlib.sha256(old.encode()).hexdigest() == item['original_sha256'] else source


def prior_document_bytes(path):
    raw = path.read_bytes()
    if path.suffix == '.md':
        return prior_story_source(path, raw.decode('utf-8')).encode('utf-8')
    return raw
