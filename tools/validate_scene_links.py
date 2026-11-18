"""Check the shipped scene/room data against the delivery, then walk it as a slot."""
import json
from collections import Counter

from scene_link_source import AREA, delivery, rows, transfer_keys
from localization_followup import approved_table, with_cast_hint


def validate_scene_links(check, project, scripts, body):
    names = []
    def verify(ok, name):
        names.append(name)
        check(bool(ok), 'Representative area: ' + name)

    resources = project / 'Assets/Resources'
    data = json.loads((resources / 'Story/representative.json').read_text())
    rooms = {r['id']: r for r in data['rooms']}
    floors = {f['id']: r['id'] for r in rooms.values() for f in r['floors']}
    sequence = data['rows']
    texts = delivery()
    tables = {p.stem: {e['key']: e['value'] for e in json.loads(p.read_text())['entries']}
              for p in (resources / 'Localization').glob('*.json')}
    keys = transfer_keys(data)
    verify(len(texts) == 119 and Counter(k.split('.')[0] for k in texts) ==
           {'story': 53, 'trial': 15, 'investigation': 51}, '119 delivered keys by source and purpose')
    authored = {k: with_cast_hint('ko', k, '“' + v + '”' if k in {
        'trial.gabriel.enter', 'trial.gabriel.verified', 'trial.gabriel.return'} else v) for k, v in texts.items()}
    verify(all(tables['ko'].get(k) == v for k, v in authored.items()), 'Korean lines match the authored scenes and current cast hints')
    for language, table in tables.items():
        approved = approved_table(language)
        verify(all(table.get(k) == approved[k] for k in keys), language + ': all delivered text and labels match the reviewed locale')
        verify(all(table[k[:-4] + 'body'] == table[k] for k in texts if k.endswith('.text')),
               language + ': all nine clue body aliases preserve imported text')
    master = tables['ko']
    verify(master['skills.forge.form3'] == '기본 패링 + 공중 아래 종격 · 광역 반격 충격파'
           and 'Loc.Get("skills.forge.form3")' in scripts['SkillStatusView'], 'approved forge form in the skill status UI')
    clauses = [r.split('\t') for r in master['investigation.record.original_and_copy.body'].split('\f')]
    verify(len(clauses) == 6 and all(len(row) == 3 for row in clauses)
           and [r[0].split('\n')[0] for r in clauses] == ['조항 구분', '문서 번호 및 발령자', '제1조 (목적)',
               '제2조 (집행 기한)', '제3조 (핵심 차이)', '물리 감정 주석']
           and '영구 동결 격리' in clauses[4][1] and '특별 사면장' in clauses[4][2],
           'all six authored labels and both full Article 3 clauses in original/copy order')
    stills = master['investigation.record.record_01_failure.body'].split('\f')
    verify(len(stills) == 6 and all(s.startswith('스틸 #' + str(i)) for i, s in enumerate(stills)) and
           '120회' in stills[3] and '06:00' in stills[4], 'six complete ordered visual descriptions and silent captions')
    verify('~~[레아 생존]~~' in master['investigation.record.discarded_draft.body'], 'discarded draft with deleted survival field')
    investigation = json.loads((resources / 'Investigation/catalog.json').read_text())
    clue_keys = {'investigation.clue.' + c['clueId'] + '.body' for c in investigation['clues']}
    record_keys = {'investigation.record.' + e['id'] + '.body' for stage in investigation['stages'] for e in stage['entries']}
    verify(len(clue_keys) == 9 and len(record_keys) == 11 and (clue_keys | record_keys) <= master.keys(),
           'every live clue and core entry resolves its body key')
    shell, panels = scripts['InvestigationShell'], scripts['ReadingPanels']
    draw = body(shell, 'private void DrawInvestigation(')
    clues = body(panels, 'private float ClueBlocks(')
    entries = body(panels, 'private float ReadingBlocks(')
    verify('ClueBlocks(investigation, topicId, small, true)' in draw and 'ReadingBlocks(stage, unlocked, true)' in draw
           and 'InvestigationText.Get(clue.BodyKey' in body(clues, 'if (owned)')
           and 0 <= entries.find('if (!unlocked) return') < entries.find('InvestigationText.Get(entry.BodyKey'),
           'live scroll views draw collected clue bodies and unlocked record bodies')
    compare = body(panels, 'private float ComparisonBody(')
    verify('ComparisonBody(y, body, draw)' in entries and 'fields[0], heading, draw' in compare
           and 'Screen.width < 1280' in compare and 'stacked ? left : y' in compare
           and 'changed ? originalChange' in compare and 'changed ? copyChange' in compare,
           'comparison panel renders clause headings, changed passages and stacked pairs on small screens')
    controls = body(shell, 'private void ActivateInvestigation(')
    advance_still = body(controls, 'if (index == 1 && stillIndex < 5)')
    verify('DrawStill(y, stillIndex)' in entries and 'frames[Mathf.Min(stillIndex' in entries
           and 0 <= advance_still.find('stillIndex++;') < advance_still.find('return;')
           and 0 <= controls.find(advance_still) < controls.find('TryConfirmEntry')
           and 'Contains("failure_still:" + n)' in controls and 'game.ActiveSkills.Save()' in advance_still,
           'failure record advances caption by caption, restores its frontier and confirms only after frame 5')
    verify('DraftBody(y, body, draw)' in entries
           and [e['id'] for e in next(s for s in investigation['stages'] if s['id'] == 'stored_records')['entries']]
           == ['record_01_failure', 'discarded_draft'], 'the deleted draft is a separate final confirmation after the stills')
    mapped = [(r['sceneId'], r['stepId'], r['speakerId'], r['textKey'], r['completionFlag']) for r in sequence]
    verify(mapped == [(r[0], r[1], r[3], r[4], r[6]) for r in rows()] and len(set(mapped)) == 53,
           '53 row identities, speakers, keys, completion receipts and author order match both source tables')
    verify(len(rooms) == 11 and all('| **`' + room + '`** |' in AREA.read_text() for room in rooms),
           'eleven authored rooms')
    verify(len(sequence) == 53 and len({(r['sceneId'], r['stepId']) for r in sequence}) == 53
           and len({r['sceneId'] for r in sequence}) == 11, 'unique scene/step IDs across eleven groups')
    verify(len(floors) == sum(len(r['floors']) for r in rooms.values()), 'unambiguous fixed-floor checkpoint IDs')
    for room in rooms.values():
        verify(room['floors'] and all(-3 < p['x'] < room.get('eastBoundary', 46) for p in room['points'] + room['floors'] + room['exits'])
               and all(e['room'] in rooms for e in room['exits']), 'bounded placement and connected destinations: ' + room['id'])
    archetypes = {a['id'] for a in json.loads((resources / 'Waves/test-stage.json').read_text())['archetypes']}
    verify(all(g['id'] in archetypes and g['count'] > 0 for r in rooms.values() for g in r['roster']),
           'all representative room rosters resolve against the runtime wave catalog')
    for row in sequence:
        valid = row['resumePoint'] in floors or row['resumePoint'] == 'current_room_safe_floor'
        for room_id in [row['room'], row['alternateRoom']]:
            if not room_id or room_id == '*':
                continue
            marks = {p['id'] for p in rooms[room_id]['points']}
            for action in row['requiredPlayerAction']:
                if action in ('confirm', 'win:mikhail', 'use:return_call'):
                    continue
                verb, target = action.split(':')
                valid &= verb in ('use', 'strike', 'walk') and target in marks
        verify(valid, 'checkpoint and action bindings: ' + row['textKey'])
    skills = json.loads((resources / 'Skills/catalog.json').read_text())['skills']
    stitch = next(s for s in skills if s['skillId'] == 'silver_stitch')
    verify(stitch['stage3At'] == 80 and rooms['velvet_05_return']['singleSeams'] >= 6
           and '.9f : 2.2f' in scripts['RepresentativeRoomLayout'] and .9 <= stitch['stages'][0]['maxSpan'],
           'post-bout route has stage-one reachable anchors below the actual span limit')

    coverage = set()
    class Route:
        def __init__(self, skill):
            self.first = skill
            self.room = 'hub_attic'
            self.flags = set()
            self.transferred = self.half = self.final = False
            self.safe = True
            self.floor = 'attic_safe_floor'
            self.clues = set()
            self.support = 0

        def meets(self, term):
            if term.startswith('!'):
                return term[1:] not in self.flags
            if term.startswith('first:'):
                return term[6:] == self.first
            if term.startswith('clue:'):
                return term[5:] in self.clues
            if term in ('transferred', 'finalReading', 'half'):
                return {'transferred': self.transferred and self.safe, 'finalReading': self.final, 'half': self.half}[term]
            return term in self.flags

        def next(self):
            return next((r for r in sequence if self.room in (r['room'], r['alternateRoom']) or r['room'] == '*'
                         if r['completionFlag'] not in self.flags and all(map(self.meets, r['entryCondition']))), None)

        def play(self):
            while True:
                r = self.next()
                if not r:
                    break
                coverage.add(r['textKey'])
                self.flags.add(r['completionFlag'])
                clue = {'copy': 'contract_copy', 'keepsake': 'imprint_mikhail', 'ledger': 'route_ledger', 'waveform': 'route_waveform'}.get(r['effect'])
                if clue:
                    self.clues.add(clue)
                if r['effect'] in ('duelHelp', 'shelterHelp'):
                    self.support += 1
                if r['resumePoint'] != 'current_room_safe_floor':
                    self.floor = 'velvet_return_mid' if self.room == r['alternateRoom'] else r['resumePoint']
                # Discard the live room and resume from the just-committed fixed floor after every row.
                self.room = floors[self.floor]

        def walk(self, destination):
            door = next(e for e in rooms[self.room]['exits'] if e['room'] == destination)
            gate = door['gate']
            valid = not gate or gate in self.flags
            if gate == 'depart':
                valid = 'intro_complete' in self.flags and ('investigation_complete' not in self.flags or 'luka_lift_complete' in self.flags)
            if destination == 'hub_lift' and {'investigation_complete', 'mikhail_i0_complete'} <= self.flags:
                valid &= 'mikhail_support_first_granted' in self.flags
            if not valid:
                raise ValueError('locked door: ' + self.room + '/' + destination)
            self.room = destination
            self.floor = rooms[destination]['floors'][0]['id']
            self.play()

        def clear_latches(self):
            self.flags.update(('loom_bridge', 'gallery_bridge'))

    def scenario(skill, pre_bout):
        route = Route(skill)
        route.play()
        route.walk('hub_forge')
        if not pre_bout:  # First transfer sourced from another district / older slot.
            route.transferred = True
            route.play()
        else:
            route.walk('hub_lift')
            route.walk('velvet_01_entry')
            route.walk('velvet_02_loom')
            route.clear_latches()
            route.walk('velvet_03_gallery')
            route.walk('velvet_04_arena')
            assert route.room == 'velvet_05_return' and route.support == 0
            route.transferred = True
            route.play()
        assert route.room == 'hub_workroom' and 'investigation_complete' in route.flags
        assert {'contract_copy', 'route_ledger', 'route_waveform'} <= route.clues
        if pre_bout:
            route.walk('hub_sanctuary')
            assert route.support == 1
        route.walk('hub_lift')
        assert {'luka_lift_approached','luka_signalled','luka_boarded','luka_lift_complete'} <= route.flags
        if not pre_bout:
            route.walk('velvet_01_entry')
            route.walk('velvet_02_loom')
            route.clear_latches()
            route.walk('velvet_06_shortcut')
            route.walk('velvet_04_arena')
            assert not route.next() and 'mikhail_i1_clash_halted' not in route.flags
            route.half = True
            route.play()
            assert route.room == 'velvet_05_return' and route.support == 1
        route.play()
        assert route.support == 1

    for s in skills:
        if s['skillId'] == 'basic_sword':
            continue
        for pre_bout in (True, False):
            name = s['skillId'] + (' / I=0 duel + shelter' if pre_bout else ' / I=1 latch duel')
            try:
                scenario(s['skillId'], pre_bout)
            except (AssertionError, ValueError, KeyError, StopIteration) as error:
                verify(False, 'save/reload route: ' + name + ' ' + str(error))
            else:
                verify(True, 'save/reload route: ' + name)
    reunion = Route('silver_stitch')
    reunion.room = 'velvet_05_return'
    reunion.flags.update(('mikhail_defeated', 'mikhail_i0_complete', 'investigation_complete'))
    reunion.play()
    verify(reunion.room == 'velvet_05_return' and reunion.floor == 'velvet_return_mid'
           and 'mikhail_support_first_granted' in reunion.flags and reunion.support == 1,
           'alternate reunion saves the local reinforced plank and grants one support')
    first = Route('silver_stitch')
    first.room = 'velvet_05_return'
    first.flags.add('intro_complete')
    first.transferred = True
    first.safe = False
    waits = first.next() is None
    first.safe = True
    first_row = first.next()
    verify(waits and first_row and first_row['completionFlag'] == 'first_transfer_seen'
           and 'Safe && OnFixedFloor()' in scripts['RepresentativeArea']
           and 'record.firstTransferPending && safe' in scripts['RepresentativeScript'],
           'first transfer waits for finished actions and fixed ground')
    late = Route('silver_stitch')
    late.room = 'hub_workroom'
    verify(late.next() is None, 'late dialogue withheld before final explicit confirmation')
    late.final = True
    late.play()
    verify('court_departure_ready' in late.flags and coverage == set(r['textKey'] for r in sequence),
           'all 53 rows including four post-terminal lines exercised across routes')
    area = scripts['RepresentativeArea']
    bootstrap, menu = scripts['GameBootstrap'], scripts['GameShell']
    verify('selectedStore.Create("hub")' in menu and 'RepresentativeScript.Handles(record.lastStageId)' in bootstrap
           and 'Area.Initialize(skills, shell, player, boss)' in bootstrap and 'shell.OpenStoryDialogue()' in area
           and 'game.Area.ConfirmDialogue(index == 1)' in menu
           and 'Room.Floor(savedFloor)' in area and 'player.ResetAt(new Vector2(floor.x, LandingY(floor.id)))' in area,
           'new-slot, continue, dialogue input and fixed-floor restoration are wired into the playable stage')
    verify('session.CommitScene(Effect' in area and 'Checkpoint(destination, floor)' in area
           and 'if (relocate) { Restore(); return; }' in area and 'if (Progression == null || closed || sceneTransaction) return' in scripts['SkillSession'],
           'side effects, row receipt, and destination floor committed via one deferred save; transition waits for durability')
    verify('skip && !CanSkip' in area and 'ActiveRow.DialogueOnly' in area and 'worldAction = true' in area
           and 'player.BasicStrikeDelivered += OnStrike' in area and 'OnPlank(target.id)' in area, 'dialogue skip, blade contact and platform contact are separate')
    verify('RecordStoryClue("route_ledger"' in area and 'RecordStoryClue("route_waveform"' in area and
           sum(r['effect'] == 'middle' and r['stepId'] == '13_rea_decision' for r in sequence) == 1, 'workbench receipt and I=1 handoff')
    return dict(checks=len(names), rows=len(sequence), rooms=len(rooms), delivered_keys=len(texts),
                transfer_keys=len(keys), save_reload_routes=10, covered_rows=len(coverage), stills=len(stills))
