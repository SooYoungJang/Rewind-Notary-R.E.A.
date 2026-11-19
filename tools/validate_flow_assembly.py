"""Walk the first departure through shipped rows/doors and v4 disk checkpoints. No Unity physics."""
import json
import re
from tempfile import TemporaryDirectory

from save_integrity_reference import Store, empty_attempt


def validate_flow_assembly(check, project, scripts, body):
    checks = 0

    def verify(condition, message):
        nonlocal checks
        checks += 1
        check(bool(condition), 'Flow assembly: ' + message)

    res = project / 'Assets/Resources'
    read = lambda path: json.loads((res / path).read_text())
    story = read('Story/representative.json')
    rooms = {r['id']: r for r in story['rooms']}
    floors = {f['id']: r for r in rooms.values() for f in r['floors']}
    waves = read('Waves/test-stage.json')
    skills = read('Skills/catalog.json')
    silver = next(d for d in skills['skills'] if d['skillId'] == 'silver_stitch')
    shell, boot, area, layout, flow, session, opponent = (scripts[k] for k in
        ('GameShell', 'GameBootstrap', 'RepresentativeArea', 'RepresentativeRoomLayout',
         'FirstDeparture', 'SkillSession', 'BenefactorTrialOpponent'))

    # Extract the guide's actual destinations, including grouped and conditional cases.
    routes = {}
    for cases, expr in re.findall(r'((?:case "\w+":\s*)+)return ([^;]+);', body(flow, 'private string NextDoorId()')):
        for room in re.findall(r'case "(\w+)"', cases):
            routes[room] = expr
    for room, expr in routes.items():
        dests = re.findall(r'"(\w+)"', expr.split('?')[-1])
        verify(all(any(d['room'] == dest for d in rooms[room]['exits']) for dest in dests),
               'guided destinations are real doors in ' + room)

    source_meta = (project / 'Assets/Scripts/GameBootstrap.cs.meta').read_text()
    guid = re.search(r'guid: (\w+)', source_meta)[1]
    verify(guid in (project / 'Assets/Scenes/Main.unity').read_text() and
           'shell.Initialize(this, hudFont)' in body(boot, 'private void Start()')
           and 'selectedStore.Create("hub")' in shell, 'Main scene to shell to new hub slot')
    stages = re.findall(r'new StageEntry\("([^"]+)"', scripts['StageCatalog'])
    verify(len(stages) == len(set(stages)) == 21 and 'new StageEntry("echo_duel", "encounter.rea_echo", "", true)' in scripts['StageCatalog'],
           'distinct release duel and gated story court entries')
    rows = (len(stages) + 1) // 2
    pitch = min(45, 450 // rows)
    verify('int pitch = Mathf.Min(45, 450 / rows)' in shell and '136 + (i % rows) * pitch, 580, pitch - 2' in shell
           and 136 + rows * pitch - 2 < 590, 'last of 21 buttons clears Back and previous-record action')

    def wired(source, *tokens):
        return all(t in source for t in tokens)

    verify(wired(body(flow, 'private void UpdateEntryWave()'), 'waves.zones[0].waves[0]', 'spawn.count',
                 'target.Initialize(waves.Archetype(spawn.archetypeId), spawn', 'current.State.EffectsClosed',
                 'Put(EntryCleared)', 'session.Save()') and 'if (shell.Playing) UpdateEntryWave()' in layout,
           'real catalog spawns, activation closure and durable entry clear')
    verify(wired(flow, 'Has("velvet_entry_complete")', '!Has(EntryCleared)', '!Has("loom_bridge")',
                 '!Has("mikhail_defeated")', '!Has("investigation_complete")')
           and 'door.room == "velvet_02_loom" && NeedsEntryWave' in body(layout, 'private bool Gate('),
           'first wave gates forward exit while keeping previously opened passages')
    verify(wired(layout, 'entryWave.Clear(); entryMember = 0;', 'FirstDepartureHint()', 'e.room == nextDoor ? Color.yellow'),
           'room rebuild resets wave cursor and HUD and door identify next action')
    verify(wired(area, 'Room.id == "velvet_04_arena"', 'mikhailBout.StartStoryBout', 'AudioManager.Music.Boss',
                 'if (mikhailBout != null) return', 'AudioManager.Sfx.BossDefeat')
           and 'GameBootstrap.Bar(' in layout, 'story duel is visible, owns one attack driver and ends with boss cue')
    patterns = read('Trials/catalog.json')['trials'][0]['patterns']
    verify([p['action'] for p in patterns] == ['thread_sweep', 'thread_cross']
           and wired(opponent, 'phase = 1; remaining = Pattern.duration', 'Threat().Intersects', 'player.Health.Hit(Pattern.damage',
                     'storyHalfHealth > 0 && motor.Health.Current <= storyHalfHealth', 'pressing ? .65f', 'pressing ? 1.5f')
           and 'Verified' not in body(opponent, 'internal void StartStoryBout('), 'two telegraphed patterns, half-HP pressure and no trial reward subscription')
    verify(wired(area, 'bool safeFloor = Safe && OnFixedFloor()', 'script.Interrupt(session.Investigation, safeFloor)',
                 'if (OnFixedFloor() && InspectPressed) FinishAction()') and 'roomTargets.Find(t => !t.State.Cleared && t.State.NeedsSetup)' in flow,
           'mastery return waits for fixed ground at interruption and at call input')
    verify(wired(area, 'ActiveRow?.completionFlag == "luka_lift_complete"', 'shell.FinishDeparture()', '!session.LastSaveSucceeded')
           and wired(body(shell, 'internal void FinishDeparture()'), '!game.ActiveSkills.LastSaveSucceeded',
                     'selectedRecord = game.ActiveSkills.Progression.Record', 'ShowStageChoices("clock")')
           and wired(body(shell, 'private void ShowStageChoices('), 'game.StopStage()', 'Go(ShellPage.Stages)'),
           'durable boarding handoff opens the current slot selector')
    launch = body(shell, 'if (stage.Id == "echo_duel")')
    verify(wired(launch, 'JsonUtility.FromJson<CoreRecord>(JsonUtility.ToJson(selectedRecord))', 'duel.lastStageId = stage.Id',
                 'game.StartStage(selectedStore, duel)', 'return;') and 'selectedStore.Save' not in launch
           and wired(session, 'standaloneDuel = record.lastStageId == "echo_duel"', 'if (standaloneDuel) return;')
           and session.index('if (standaloneDuel) return;') < session.index('dirty = !store.Save('),
           'optional duel cannot replace campaign, debug, or ending-bank bytes')
    build = body(boot, 'private void Build(')
    duel_start = body(build, 'if (echoDuel)', braced=True)
    verify(wired(boot, 'if (!echoDuel) reward.Initialize', 'if (fromStart) checkpoint = echoDuel', 'shell.FinishEchoDuel()')
           and wired(duel_start, 'player.ResetAt(LevelBuilder.ArenaStart)', 'bossAI.Activate()')
           and 'GrantPracticeSkills' not in launch and 'KeyCode.F7' in boot,
           'standalone arena starts and retries at boss, no campaign reward or practice grant, F7 retained')

    thresholds = (silver['stage2At'], silver['stage3At'])
    room_wave = waves['zones'][0]['waves'][0]
    kinds = {a['id']: a for a in waves['archetypes']}
    verify(all(kinds[s['archetypeId']]['gate'] != 'skill' and not s['setupSkill'] and s['segments'] > 0
               for s in room_wave['spawns']), 'pre-Mikhail wave is clearable with new-game sword and defense')
    milestones = ['mint_candy_placed', 'sixth_bowl_stamped', 'blade_notch_checked', 'velvet_entry_complete',
                  'velvet_entry_wave_cleared', 'loom_bridge', 'gallery_bridge', 'mikhail_defeated', 'mikhail_i0_complete',
                  'first_transfer_seen', 'returned_for_mid_invest', 'investigation_complete', 'mikhail_support_first_granted',
                  'luka_signalled', 'luka_boarded', 'luka_lift_complete']
    seen, visits, uses_at_returns = [], [], []
    with TemporaryDirectory() as td:
        store = Store(td, 2, skills, read('Investigation/catalog.json'))
        ids = [d['skillId'] for d in skills['skills'] if d['doomId']]
        record = dict(schemaVersion=4, rulesVersion=skills['rulesVersion'], slotId='slot-2', savedUtc='', revision=0,
                      testData=False, basicMasteryReached=False, firstTransferPending=False, lastStageId='hub',
                      storyFlags=['hub_open'], completedEncounters=[], investigation=dict(clueIds=[], readStageIds=[], confirmedEntryIds=[]),
                      skills=[], ledger=[], finalBattleAttempt=empty_attempt(ids), lastFinalBattleAttempt=None)
        for d in skills['skills']:
            basic = d['skillId'] == 'basic_sword'
            record['skills'].append(dict(skillId=d['skillId'], stage=1, acquired=basic,
                                         acquiredEncounterId='record_01' if basic else '', validUseCount=120 if basic else 0))
            if not basic:
                record['ledger'].append(dict(skillId=d['skillId'], doomId=d['doomId'], ownerId=d['sourceId'],
                                             transferredToRea=False, transferEventId=''))

        def put(flag):
            if flag not in record['storyFlags']:
                record['storyFlags'].append(flag)
                if flag in milestones:
                    seen.append(flag)

        def save_floor(floor):
            nonlocal record
            stage = floors[floor]['stage']
            prefix = 'scene_floor:' + stage + ':'
            record['lastStageId'] = stage
            record['storyFlags'] = [f for f in record['storyFlags'] if not f.startswith(prefix)]
            put(prefix + floor)
            if not store.save(record):
                raise ValueError('v4 checkpoint rejected: ' + floor)
            record = store.load()
            return floors[next(f[len(prefix):] for f in record['storyFlags'] if f.startswith(prefix))]

        def meets(term):
            if term.startswith('!'): return term[1:] not in record['storyFlags']
            if term.startswith('first:'): return 'first_mastery:' + term[6:] in record['storyFlags']
            if term.startswith('clue:'): return term[5:] in record['investigation']['clueIds']
            if term == 'transferred': return record['firstTransferPending']
            return term in record['storyFlags']

        try:
            floor = 'attic_safe_floor'
            room = save_floor(floor)
            for _ in range(120):
                flags = record['storyFlags']
                stitch = next(s for s in record['skills'] if s['skillId'] == 'silver_stitch')
                if 'luka_lift_complete' in flags: break
                row = next((r for r in story['rows'] if r['room'] in ('*', room['id'])
                            and r['completionFlag'] not in flags and all(map(meets, r['entryCondition']))), None)
                if row:
                    effect = row['effect']
                    if effect in ('copy', 'keepsake', 'ledger', 'waveform'):
                        record['investigation']['clueIds'].append(dict(copy='contract_copy', keepsake='imprint_mikhail',
                                                                        ledger='route_ledger', waveform='route_waveform')[effect])
                    if effect == 'defeat':
                        if row['requiredPlayerAction'] != ['win:mikhail']: raise ValueError('wrong reward trigger')
                        stitch.update(acquired=True, acquiredEncounterId='story:mikhail_bout')
                        record['completedEncounters'].append(stitch['acquiredEncounterId'])
                    if effect == 'openDistricts': put('districts_open')
                    put(row['completionFlag'])
                    if row['resumePoint'] != 'current_room_safe_floor': floor = row['resumePoint']
                    room = save_floor(floor)
                    continue

                if room['id'] == 'velvet_01_entry' and 'velvet_entry_wave_cleared' not in flags:
                    put('velvet_entry_wave_cleared')
                if room['id'] in ('velvet_02_loom', 'velvet_03_gallery'):
                    if not any(g['id'] == 'sword_latch' for g in room['roster']): raise ValueError('missing bridge latch')
                    put('loom_bridge' if room['id'] == 'velvet_02_loom' else 'gallery_bridge')
                gathering = 'mikhail_i0_complete' in flags and not record['firstTransferPending']
                if gathering:
                    supply = room['singleSeams']
                    if supply:
                        supply += sum(g['count'] * g['segments'] * g.get('setupHits', 1)
                                      for g in room['roster'] if kinds[g['id']]['gate'] == 'skill')
                    if stitch['validUseCount'] + supply >= thresholds[0]:
                        supply += room['stitchAnchors'] - room['singleSeams']
                    for _ in range(supply):
                        stitch['validUseCount'] += 1
                        count = stitch['validUseCount']
                        stitch['stage'] = 1 + sum(count >= t for t in thresholds)
                        if count == thresholds[1]:
                            entry = next(e for e in record['ledger'] if e['skillId'] == 'silver_stitch')
                            entry.update(ownerId='rea', transferredToRea=True, transferEventId='mastery:silver_stitch')
                            record['firstTransferPending'] = True
                            put('first_mastery:silver_stitch')
                            break
                    uses_at_returns.append((room['id'],stitch['validUseCount']))
                    if record['firstTransferPending']:
                        room = save_floor(floor)
                        continue

                expr = routes[room['id']]
                if '?' in expr:
                    condition, choices = expr.split('?')
                    selected = (gathering if condition.strip() == 'GatheringSilver' else
                                all(meets(('!' if neg else '') + flag)
                                    for neg, flag in re.findall(r'(!?)Has\("(\w+)"\)', condition)))
                    expr = choices.split(':')[0 if selected else 1]
                target = expr.strip().strip('"')
                door = next(e for e in room['exits'] if e['room'] == target)
                gate = door['gate']
                if gate == 'depart': gate = 'intro_complete' if 'investigation_complete' not in flags else 'luka_lift_complete'
                if gate and gate not in flags: raise ValueError('locked ' + target)
                visits.append(target)
                floor = rooms[target]['floors'][0]['id']
                room = save_floor(floor)
            verify(seen == milestones and room['id'] == 'hub_lift' and floor == 'lift_arrival_exit',
                   'ordered new-game path on a v4 slot reloaded at every row and doorway')
            verify(uses_at_returns == [('velvet_05_return',16),('velvet_01_entry',40),('velvet_02_loom',52),
                                       ('velvet_03_gallery',64),('velvet_04_arena',80)] and stitch['validUseCount'] == 80 and thresholds == (30, 80)
                   and record['completedEncounters'].count('story:mikhail_bout') == 1,
                   'five distinct return rooms reach first mastery through single effects and one story reward')
        except (KeyError, StopIteration, ValueError, IndexError) as error:
            verify(False, 'new-game path blocked: ' + repr(error))

    return dict(checks=checks, milestones=seen, doors_walked=visits, uses_after_return_room=uses_at_returns,
                unity_executed=False, csharp_compiled=False)
