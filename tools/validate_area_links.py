"""Cross-check the playable district graph and walk its row/checkpoint transactions without Unity."""
import json
import re
from collections import Counter
from pathlib import Path

from story_link_baseline import prior_level4_source, prior_level5_source, reverse_exact

from area_link_source import DISTRICTS, delivery, handoff, room_table, scene_rows
from localization_followup import approved_table, with_cast_hint


def validate_area_links(check, project, scripts):
    checks = 0

    def verify(ok, message):
        nonlocal checks
        checks += 1
        check(bool(ok), 'Districts: ' + message)

    res = project / 'Assets/Resources'
    read = lambda path: json.loads((res / path).read_text())
    base = read('Story/representative.json')
    districts = [read(f'Story/{stage}.json') for stage in DISTRICTS]
    rooms = {r['id']: r for d in [base, *districts] for r in d['rooms']}
    all_rows = [r for d in [base, *districts] for r in d['rows']]
    floors = {f['id']: r['id'] for r in rooms.values() for f in r['floors']}
    exits = {r['id']: list(r['exits']) for r in rooms.values()}
    for d in districts:
        for link in d['links']:
            exits[link['source']].append(link)
    verify(len(rooms) == 51 and len(floors) == sum(len(r['floors']) for r in rooms.values()),
           '51 distinct rooms and unambiguous fixed-floor checkpoints')
    source = list(scene_rows())
    fields = ('sceneId', 'stepId', 'speakerId', 'textKey', 'completionFlag')
    mapped = [tuple(r[k] for k in fields) for d in districts for r in d['rows']]
    verify(mapped == [r[:5] for r in source] and len(set(mapped)) == 168,
           'all 168 scene/step, speaker, localization and completion IDs match authored order')
    verify(len(set((r['sceneId'], r['stepId']) for r in all_rows)) == 221, 'combined 221 scene/step identities')
    text = delivery()
    verify(set(text) == {r[3] for r in source} and Counter(k.split('.')[0] for k in text) == {'story': 156, 'trial': 12},
           'complete dialogue blocks for every delivered key')
    old_keys = {r['textKey'] for r in base['rows']}
    trials = {d['id']: d for d in read('Trials/catalog.json')['trials']}
    old_trials = {key for d in trials.values() for key in d['dialogue'].values()}
    verify(not (old_keys & text.keys()) and len(old_trials & text.keys()) == 12, '12 shared practice keys serve field and standalone trials')
    korean = {e['key']: e['value'] for e in read('Localization/ko.json')['entries']}
    verify(all(korean.get(k) == with_cast_hint('ko', k, v) for k, v in text.items()),
           'Korean lines match the four authored districts and current cast hints')
    transfer = handoff(districts)
    for p in sorted((res / 'Localization').glob('*.json')):
        entries = json.loads(p.read_text())['entries']
        table = {e['key']: e['value'] for e in entries}
        approved = approved_table(p.stem)
        verify(len(table) == len(entries) and all(table.get(k) == approved[k] for k in transfer),
               p.stem + ' exact full text, unique keys, room, fixture and speaker labels')
    archetypes = {g['id'] for g in read('Waves/test-stage.json')['archetypes']}
    source_rooms = list(room_table())
    verify([r['id'] for d in districts for r in d['rooms']] ==
           [re.search(r'`(\w+)`', t[0])[1] for t in source_rooms], 'all 40 authored room IDs in map order')
    for table_row in source_rooms:
        room = rooms[re.search(r'`(\w+)`', table_row[0])[1]]
        doors = exits[room['id']]
        listed = set(re.findall(r'`([a-z]\w+)`', table_row[2])) & rooms.keys()
        verify(listed <= {e['room'] for e in doors} and all(e['room'] in rooms for e in doors) and
               set(re.findall(r'`([a-z]\w+)`', table_row[3])) <= {f['id'] for f in room['floors']} and
               all(-3 < m['x'] < room.get('eastBoundary', 46) for m in [*doors, *room['floors'], *room['points']]) and
               all(g['id'] in archetypes and min(g['count'], g['segments']) > 0 for g in room['roster']),
               'authored doors, safe floors and finite combat roster ' + room['id'])

    # Test the same state vocabulary the row evaluator accepts. Conditions the model cannot interpret fail closed.
    coverage = set()
    routes = 0
    for d in districts:
        sequence, layout = d['rows'], d['rooms']
        a, b, sk = (layout[0][k] for k in ('stage', 'benefactor', 'skill'))
        verify(len(sequence) == 42 and len(layout) == 10 and all(r['stage'] == a for r in layout),
               a + ' 42 rows, ten district-owned rooms including the reunion')
        by_key = {r['textKey']: r for r in sequence}
        physical = {'use', 'walk', 'ride', 'timed', 'latch'}
        for row in sequence:
            room = rooms.get(row['room'])
            ok = row['resumePoint'] in floors if room else row['room'] == '*' and row['resumePoint'] == 'current_room_safe_floor'
            for act in row['requiredPlayerAction']:
                verb, _, target = act.partition(':')
                if verb in physical:
                    point = next((p for p in room['points'] if p['id'] == target), {})
                    ok &= bool(point) and (verb not in ('ride', 'walk', 'timed') or point.get('platform') is True)
                elif verb in ('win', 'interlude', 'verify'):
                    ok &= target == b and room['boutHealth'] > 0
                elif verb == 'advance':
                    ok &= target == row['alternateRoom'] and any(e['room'] == target for e in room['exits'])
                elif verb == 'clear':
                    ok &= bool((rooms[row['alternateRoom']] if row['alternateRoom'] else room)['roster'])
                elif verb in ('station', 'cross', 'sew'):
                    ok &= room['stations'] > 0 and (verb != 'sew' or room['stitchAnchors'] > 0)
                else:
                    ok &= act == 'confirm'
            verify(ok, row['textKey'] + ' input target, encounter and durable floor')
        authored = [r for r in source if r[3] in by_key]
        for src in authored:
            floor_ids = re.findall(r'`(\w+)`', src[7])
            row = by_key[src[3]]
            # Authored 03 describes the eventual workroom return in parentheses. The
            # healer stays on this floor until the separately acknowledged Luka call.
            if row['effect'] == 'healed:' + sk:
                assert floor_ids == ['hub_workroom']
                floor_ids = []
            verify((not floor_ids or floors.get(row['resumePoint']) == floor_ids[0]) and
                   (len(floor_ids) < 2 or row['resumePoint'] == floor_ids[-1]) and
                   (row['requiredPlayerAction'] == ['confirm']) == src[6].strip().startswith('대사 확인'),
                   src[3] + ' authored checkpoint destination and dialogue/world input distinction')

        def tour(post):
            # Reload from the committed floor at each step, including the intermediate multi-room leg.
            flags = {'intro_complete', 'districts_open'}
            skills = {'silver_stitch': 3} if post else {}
            if post:
                flags.update(('investigation_complete', 'luka_lift_complete'))
            room = 'hub_lift'
            grants = []
            seen = []

            def meets(term):
                if term.startswith('!'):
                    return term[1:] not in flags
                if term.startswith('owned:'):
                    return term[6:] in skills
                if term.startswith(('stage2:', 'stage3:')):
                    return skills.get(term[7:], 0) >= int(term[5])
                return term == 'safe' or term in flags

            def walk(dest):
                nonlocal room
                door = next(e for e in exits[room] if e['room'] == dest)
                gate = door['gate']
                if gate == 'depart':
                    assert 'intro_complete' in flags and ('investigation_complete' not in flags or 'luka_lift_complete' in flags)
                elif gate == 'synergy':
                    assert 'investigation_complete' in flags and {'silver_stitch', sk} <= skills.keys()
                else:
                    assert not gate or gate in flags
                room = dest

            def play():
                nonlocal room
                for _ in range(50):
                    row = next((r for r in sequence if (r['room'] in ('*', room) or
                                (r['alternateRoom'] == room and 'crossed:' + r['completionFlag'] in flags))
                                and r['completionFlag'] not in flags and all(map(meets, r['entryCondition']))), None)
                    if row is None:
                        return
                    if row['requiredPlayerAction'][0].startswith('advance:') and room == row['room']:
                        flags.add('crossed:' + row['completionFlag'])
                        walk(row['alternateRoom'])
                        continue
                    flags.add(row['completionFlag'])
                    coverage.add(row['textKey'])
                    seen.append(row['textKey'])
                    effect = row['effect']
                    if effect == 'defeat':
                        assert sk not in skills
                        skills[sk] = 1
                    elif effect in ('shelterHelp', 'duelHelp', 'recharge'):
                        grants.append(effect)
                    elif effect == 'endTrial':
                        flags.remove('trial_active:' + b)
                    if row['resumePoint'] != 'current_room_safe_floor':
                        room = floors[row['resumePoint']]
                raise AssertionError('row cycle')

            ids = [r['id'] for r in layout]
            for dest in ids[:6]:
                walk(dest)
                play()
            # The final duel line enters 07. Cross 07+08 and return to 01.
            assert room == ids[0] and sk in skills and a + '_return_complete' in flags
            if not post:
                skills[sk] = 3
                play()
                assert seen[-3:] == [f'story.{a}.mastery.0{n}' for n in (1, 2, 3)] and not grants
                flags.update(('investigation_complete', 'first_transfer_seen', 'returned_for_mid_invest'))
                room = 'hub_sanctuary'
                walk(ids[9]); play()
                assert grants == ['shelterHelp']
                skills['silver_stitch'] = 1
                flags.add('luka_lift_complete')
                walk('hub_lift'); walk(ids[0])
            else:
                assert grants == ['duelHelp']
                skills[sk] = 3
                play()
            walk(ids[1]); walk(ids[8]); play()
            assert room == ids[8] and a + '_synergy_complete' in flags
            walk(ids[4])
            for i in range(2):
                for r in sequence:
                    if r['sceneId'] == b + '_trial':
                        flags.discard(r['completionFlag'])
                flags.add('trial_active:' + b)
                walk(ids[5]); play()
                assert room == ids[4] and grants.count('recharge') == i + 1
            assert len(grants) == 3 and sk in skills and b + '_healed_seen' in flags

        for post in (False, True):
            try:
                tour(post)
            except (AssertionError, StopIteration, KeyError) as error:
                verify(False, f'{a} post={post} disconnected route: {error}')
            else:
                routes += 1
                verify(True, f'{a} post={post} pass, resume, reunion/duel, both wave rooms and two trial cycles')

    verify(coverage == set(text), 'branch walks exercise all 168 delivered rows')
    # Run the interrupt queue together with the representative investigation. Simulate an
    # app restart by rebuilding the next row from receipts and the committed room every time.
    interrupts = [r for r in all_rows if r['room'] == '*']
    for district in districts:
        stage, skill = (district['rooms'][0][k] for k in ('stage', 'skill'))
        flags = {'intro_complete', 'districts_open'}
        levels = {skill: 2}
        room = district['rooms'][6]['id']
        order = []

        def eligible(row):
            for term in row['entryCondition']:
                prefix, _, value = term.partition(':')
                if prefix in ('stage2', 'stage3'):
                    if levels.get(value, 0) < int(prefix[-1]):
                        return False
                elif prefix == 'first':
                    if value != skill:
                        return False
                elif term.startswith('!'):
                    if term[1:] in flags:
                        return False
                elif term not in flags and term not in ('safe', 'transferred', 'clue:contract_copy'):
                    return False
            return row['completionFlag'] not in flags

        def next_interrupt(safe):
            if not safe or ('returned_for_mid_invest' in flags and 'investigation_complete' not in flags):
                return None
            for r in interrupts:
                if r['sceneId'] == 'first_transfer' and r['stepId'] == '01_trigger':
                    continue
                if 'returned_for_mid_invest' not in flags and r['sceneId'] not in ('first_transfer', stage + '_mastery'):
                    continue
                if eligible(r):
                    return r
            return None

        def finish(row):
            destination = floors.get(row['resumePoint'], room)
            flags.add(row['completionFlag'])
            if row['effect'] == 'healed:' + skill and 'investigation_complete' not in flags:
                flags.add('first_transfer_seen')
            order.append(row['textKey'])
            return destination

        verify(next_interrupt(False) is None, stage + ' 30 waits for fixed ground')
        room = finish(next_interrupt(True))
        levels[skill] = 3
        verify(next_interrupt(False) is None, stage + ' 80 waits through an unfinished world action')
        for _ in range(3):
            room = finish(next_interrupt(True))
        expected_call = next(r for r in base['rows'] if r['stepId'] == '02_luka_call')
        verify(room == 'hub_workroom' and order == [f'story.{stage}.mastery.0{n}' for n in (1, 2, 3)] +
               [expected_call['textKey']] and expected_call['requiredPlayerAction'] == ['use:return_call'],
               stage + ' field transfer, healed cut and explicit Luka call, one crack')
        levels.update({d['rooms'][0]['skill']: 3 for d in districts})
        verify(next_interrupt(True) is None, stage + ' additional pending mastery waits until the joint investigation is complete')
        while True:
            row = next((r for r in base['rows'] if r['sceneId'] == 'mid_invest' and r['room'] == room and eligible(r)), None)
            if not row:
                break
            room = finish(row)
        symptom = [r for r in base['rows'] if r['sceneId'] == 'mid_invest' and r['stepId'].startswith('02')]
        verify('investigation_complete' in flags and [r['entryCondition'][-1] for r in symptom if r['textKey'] in order] == ['first:' + skill],
               stage + ' exactly the originating symptom and final investigation receipt')
        before = len(order)
        while next_interrupt(True):
            room = finish(next_interrupt(True))
        verify(all(r['textKey'] in order for r in interrupts if r['sceneId'].endswith('_mastery')) and
               room == 'hub_workroom' and not any(k.startswith('story.transfer') for k in order[before:]) and
               len(order) == len(set(order)), stage + ' later masteries and reload do not recall or replay')

    reunion_rooms = [r for r in rooms.values() if '_10_sanctuary_' in r['id']]
    verify([r['stage'] for r in reunion_rooms] == list(DISTRICTS) and
           all({'hub_lift', 'hub_sanctuary'} <= {e['room'] for e in exits[r['id']]} and
               any(e['room'] == r['id'] and e['gate'] == 'investigation_complete' for e in exits['hub_sanctuary'])
               for r in reunion_rooms), 'all-predefeated route has four ordered reunions between sanctuary and lift')
    supply = {}
    cumulative = read('Waves/test-stage.json')['initialSwordUses']

    def sword(r, first_visit=False):
        return r.get('boutHealth', 0) + sum(g['count'] * g['segments'] for g in r['roster'] if not g.get('skillOnly') and not (first_visit and g.get('silverReturnOnly')))

    def technique(r):
        return r.get('stations', 0) + sum(g['count'] * g['segments'] * g.get('setupHits', 1) for g in r['roster'] if g.get('setupSkill'))

    velvet = [r for r in base['rooms'] if r['stage'] == 'velvet']
    first_path = cumulative + sum(sword(r, first_visit=True) for r in velvet[:3])
    cumulative += sum(map(sword, velvet))
    verify((first_path, cumulative) == (162, 196), 'representative finite room supplies +42/+14/+20 from 120')
    clock_path = 'Assets/Resources/Story/clock.json'
    clock_patch = json.loads(Path(__file__).with_name('level-step3-extensions.json').read_text())[clock_path]
    original_clock = json.loads(reverse_exact(clock_patch, (project / clock_path).read_text()))
    for d, expected in zip(districts, [(24, 18, 272), (23, 18, 348), (24, 19, 423), (25, 19, 498)]):
        rs = d['rooms']
        a = rs[0]['stage']
        cuts = list(map(sword, rs))
        returns, elite = sum(map(technique, rs[6:8])), sum(map(technique, rs[8:]))
        previous_returns = sum(map(technique, original_clock['rooms'][6:8])) if a == 'clock' else returns
        if a == 'gallery':
            path = 'Assets/Resources/Story/gallery.json'
            previous = json.loads(prior_level5_source(path, (project / path).read_text()))
            previous_returns = sum(map(technique, previous['rooms'][6:8]))
            verify(returns == 96, 'live gallery returns provide 80 distinct paint stations and 16 finite preparations')
        if a == 'bell':
            path = 'Assets/Resources/Story/bell.json'
            previous = json.loads(prior_level5_source(path, (project / path).read_text()))
            previous_returns = sum(map(technique, previous['rooms'][6:8]))
            verify(returns == 100, 'live bell returns provide 84 distinct stations and 16 finite preparations')
        if a == 'opera':
            path = 'Assets/Resources/Story/opera.json'
            prior_opera = json.loads(prior_level4_source(path, (project / path).read_text()))
            previous_returns = sum(map(technique, prior_opera['rooms'][6:8]))
            verify(returns == 100, 'live opera returns provide 84 distinct stations and 16 finite preparations')
        if a == 'clock':
            verify(returns == 100, 'live clock returns provide 84 first switches and 16 finite preparations')
        after_boss = cumulative + sum(cuts[:6])
        cumulative += sum(cuts)
        verify(cuts[:6] == [1, 11, 13, 7, 0, 14] and 120 + sum(cuts[:5]) == 152 and
               (previous_returns, elite, cumulative) == expected and cuts[6:9] == [8, 8, 11],
               f'{a} room-spawn projection {previous_returns}+{elite} skill opportunities; sword cumulative {cumulative}')
        if a == 'opera':
            verify(after_boss == 318 and after_boss + cuts[6] + 4 == 330 and cuts[7] >= 4,
                   '330 on fourth lower-return cut after opera boss and upper return')
        if a == 'bell':
            verify(after_boss + cuts[6] == 402, '400 reached on upper bell return, 402 at exit')
        supply[a] = dict(sword_by_room=cuts, sword_total=cumulative, return_uses=returns, elite_uses=elite)

    loader, area, layout, actions = (scripts[n] for n in ('RepresentativeScript', 'RepresentativeArea', 'RepresentativeRoomLayout', 'DistrictRoomLayout'))
    verify(all(f'"{a}"' in loader and f'new StageEntry("{a}", "stage.{a}.name", "districts_open", true)' in scripts['StageCatalog']
               for a in DISTRICTS) and 'foreach (string district in Districts)' in loader and 'rows.AddRange(data.rows)' in loader
           and 'var hub = script.Room(door.source)' in loader, 'normal loader merges authored rows and adds hub exits')
    verify('script.Interrupt(session.Investigation, safeFloor)' in area and 'Safe && OnFixedFloor()' in area
           and 'row.stepId == "01_trigger"' in loader and 'first != SkillRules.SilverStitch' in loader
           and 'if (!safe) return null' in loader and 'Put("first_transfer_seen")' in area and 'firstMastery' in loader and 'if (!investigated && record.storyFlags.Contains("returned_for_mid_invest")) return null;' in loader,
           'safe-floor interruption prioritizes first owner, consumes the shared crack receipt on healing')
    verify('Put("crossed:" + ActiveRow.completionFlag)' in actions and 'Travel(script.Room(door.room), door.destinationFloor)' in actions and 'door.InReach(player.Body.position)' in actions
           and 'ActiveRow.alternateRoom == Room.id) actionIndex = 1' in area and 'roomTargets.TrueForAll' in actions,
           'wave handoffs retain intermediate progress; both live rooms require cleared targets')
    verify(all(k in actions for k in ('OnPlank(', 'ReturnWindowOpen', 'LastReturnUse', 'raisedLift.MovePosition', 'DrawHandSignal',
                                     'StitchSurface2D.All.Find', 'OnDistrictEffect', 'TryTrialBell', 'd.requiredSuccesses'))
           and 'target.Initialize(kind, spawn, player, square, group.setupHits, group.skillOnly)' in layout
           and 'segments = group.segments' in layout and 'opponent.Health.Initialize(finale ? Room.interludeHealth : Room.boutHealth' in area,
           'physical contact, qualified skill callbacks, declared segments and bout health consumed in room runtime')
    verify('ConfirmStoryRecharge(DistrictTrial)' in area and 'Configure(InFieldTrial ? DistrictTrial.stageId : Room.stage, InFieldTrial, resume: InFieldTrial)' in area
           and 'Record.storyFlags.Remove(row.completionFlag)' in actions and 'CommitScene(Effect' in area,
           'practice uses actual three-success trial proof and re-arms only the next renewal cycle')
    verify('RestoreTransferReceipt(Record)' in area and 'record.storyFlags.Contains("first_transfer_seen")' in loader
           and 'record.storyFlags.Contains(transfer.completionFlag)' in loader and 'record.storyFlags.Add(transfer.completionFlag)' in loader
           and 'r.sceneId == firstMastery && Eligible' in loader, 'legacy v4 shared-crack receipt suppresses duplicate local crack and permits healing before recall')
    verify('if (resume) return;' in scripts['BenefactorTrialProgress'] and 'if (Has(service.Record, d, "rewarded")) return true;' in scripts['BenefactorTrialProgress']
           and 'Trials.Configure(DistrictTrial.stageId, true)' in actions and 'Checkpoint(arena, arena.floors[0].id)' in actions,
           'trial renewal consumes field credit in the bell transaction; resume and already paid verification preserve balance')
    return dict(checks=checks, rooms=40, rows=len(mapped), covered_rows=len(coverage), branch_routes=routes,
                dialogue_keys=len(text), shared_trial_keys=12, original_new_dialogue_keys=156,
                dialogue_handoff_cells=len(text)*8, handoff_keys=len(transfer), handoff_cells=len(transfer)*8,
                initial_sword=120, first_path=first_path, supply=supply)
