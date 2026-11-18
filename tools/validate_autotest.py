"""Static contracts of the live-input harness. Retain all original gameplay checks."""
import hashlib
import json
import re
from pathlib import Path

from story_link_baseline import AUTOTEST_EDITS, FEEL_EDITS, LEVEL_EDITS, prior_level3_source, reverse_exact


def validate_story_point_strike(verify, project, scripts, story, body):
    row = next((r for r in story['rows'] if r['textKey'] == 'story.invest.11_exp3_theo'), {})
    verify(row.get('room') == 'hub_workroom' and row.get('entryCondition') == ['mid_invest_10_read']
           and row.get('requiredPlayerAction') == ['use:reflection_plate', 'strike:reflection_plate', 'use:reflection_plate']
           and row.get('completionFlag') == 'exp_step3_complete', 'exp3 uses, strikes and detaches the same plate in order')
    area = scripts['RepresentativeArea']
    on_strike = body(area, 'private void OnStrike(')
    verify('player.BasicStrikeDelivered += OnStrike;' in area and
           'if (Health.IsPlayer) BasicStrikeDelivered?.Invoke(blade, strikeDamage);' in scripts['CombatMotor2D'] and
           all(token in on_strike for token in ('if (!ReadyForAction || !ActionToken.StartsWith("strike:",',
               'Room.Point(ActionToken.Substring(7))', 'blade.Contains(new Vector3(target.x, 1f, 0))) FinishAction();')),
           'delivered basic blade advances the pending strike on contact with the authored point')
    finish = ' '.join(body(area, 'private void FinishAction()').split())
    verify('if (ActionToken == "use:reflection_plate" && actionIndex > 0) session.Progression.CalibrationDormantSkill = null;' in finish
           and 'if (actionIndex + 1 < ActiveRow.requiredPlayerAction.Length) actionIndex++; else Complete();' in finish
           and 'Put(ActiveRow.completionFlag);' in body(area, 'private bool Complete()'),
           'last plate inspection restores the dormant skill and commits the row flag')

    bot = scripts['FirstDepartureBot']
    drive = body(bot, 'internal void Drive(')
    verify(all(token in drive for token in (
        'string action = Area.PendingAction;', 'Area.Room.Point(action.Substring(split + 1))',
        'if (action.StartsWith("strike:", StringComparison.Ordinal)) StrikeStoryPoint(point.x);',
        'if (action.StartsWith("use:", StringComparison.Ordinal) && Walk(point.x, .38f)) keys.Tap(KeyCode.F);')),
        'authored point drives story sword positioning and both inspection edges')

    strike = body(bot, 'private void StrikeStoryPoint(')
    verify(all(token in strike for token in ('if (Player.Attacking) return;', 'float dx = x - Player.Body.position.x;',
           'float ahead = dx * Player.Facing;')) and body(strike, 'if (ahead >= minRange && ahead <= maxRange)').strip()
           == 'if (Player.Grounded) keys.Tap(KeyCode.J);' and strike.count('Tap(') == 1,
           'story point swing requires grounded recovery, facing and both distance bounds')
    verify('keys.Hold(dx < 0 ? KeyCode.D : KeyCode.A);' in body(strike, 'else if (Mathf.Abs(dx) < maxRange)')
           and strike.strip().endswith('else Walk(x, 0f);'), 'retreat on either side then approach to turn toward the point')

    limits = re.search(r'const float minRange = ([.\d]+)f, maxRange = ([.\d]+)f;', strike)
    motor = scripts['CombatMotor2D']
    combo = body(motor, 'private static readonly AttackSpec[] PlayerCombo =')
    boxes = [tuple(float(v.strip().rstrip('f')) for v in spec.split(',')[3:5])
             for spec in re.findall(r'new AttackSpec\(([^)]+)\)', combo)]
    skills = json.loads((project / 'Assets/Resources/Skills/catalog.json').read_text())['skills']
    sword = next(s for s in skills if s['skillId'] == 'basic_sword')
    lunge = max(stage['stepSpeed'] * stage['stepSeconds'] for stage in sword['stages'])
    lo, hi = map(float, limits.groups()) if limits else (0, 0)
    verify(limits and len(boxes) == 4 and 0 < lo < hi and
           all(reach - width / 2 < lo - lunge and hi < reach + width / 2 for reach, width in boxes),
           'entire story strike window hits with every basic swing, from zero to maximum forward step')

    # Flat workroom walking reference. Include the run-3 offset, exact overlap and either facing.
    # Five 20 ms physics ticks per input sample cover the project's 100 ms maximum timestep.
    def reaches_window(dx, facing, travel):
        for _ in range(60):
            if lo <= dx * facing <= hi:
                return True
            toward = -1 if dx < 0 else 1
            facing = -toward if abs(dx) < hi else toward
            dx -= travel * facing
        return False

    for ticks in (1, 2, 5):
        verify(limits and all(reaches_window(dx, facing, 6.5 * .02 * ticks)
               for dx in (0, .1600094, -.1600094, .38, -.38, 4, -4) for facing in (-1, 1)),
               f'workroom approach reaches a forward swing from both sides at {ticks} physics tick(s) per sample')


def validate_log_classification(verify, runtime, report, driver, body):
    startup = 'UnityEditor.Search.SearchInit.IndexationOnStartup'
    table = body(runtime, 'SearchStartupFrames = new HashSet<string>(StringComparer.Ordinal)', braced=True)
    signatures = set(re.findall(r'^\s*"([^"]+)",?\s*$', table, re.M))
    if 'SearchStartup,' in table and f'const string SearchStartup = "{startup}";' in runtime:
        signatures.add(startup)
    verify(startup in signatures, 'explicit ordinal frame allowlist with Search startup signature')

    classifier = body(runtime, 'internal static bool IsEditorSearchStartupNoise(string stack)', braced=True)
    verify(all(token in classifier for token in (
        'if (string.IsNullOrWhiteSpace(stack)) return false;', 'bool sawStartup = false;',
        "foreach (string line in stack.Split('\\n'))", 'string frame = line.Trim();',
        'if (frame.Length == 0) continue;', 'Match match = ManagedFrame.Match(frame);',
        'if (!match.Success) return false;', 'string signature = match.Groups[1].Value;',
        'string arguments = match.Groups[2].Value;', 'if (arguments.Length != 0) signature += "(" + arguments + ")";',
        'if (!SearchStartupFrames.Contains(signature)) return false;', 'if (signature == SearchStartup) sawStartup = true;'))
        and classifier.strip().endswith('return sawStartup;') and classifier.count('return ') == 4,
        'pure stack classifier rejects each unrecognized frame before accepting startup')

    # Replay the C# frame expression and signature data, including the exact PC callback stack.
    declaration = re.search(r'ManagedFrame = new Regex\(\s*@"([^"]+)"\);', runtime)
    try:
        pattern = re.compile(declaration[1]) if declaration else None
    except re.error:
        pattern = None
    verify(pattern and pattern.groups == 2 and pattern.pattern.startswith('^') and pattern.pattern.endswith('$'),
           'frame parser covers the entire line and extracts method and arguments')

    def is_noise(stack):
        if not stack or not pattern or pattern.groups != 2:
            return False
        saw_startup = False
        for line in stack.split('\n'):
            if not line.strip():
                continue
            match = pattern.search(line.strip())
            if not match:
                return False
            method, args = match.groups()
            signature = f'{method}({args})' if args else method
            if signature not in signatures:
                return False
            if signature == startup:
                saw_startup = True
        return saw_startup

    pc = (Path(__file__).parent / 'fixtures/autotest-search-6000.6.9f1.stack').read_text()
    brief = '\r\n'.join('  at ' + frame for frame in (
        'System.Collections.Generic.List`1.get_Item', 'UnityEditor.Search.SearchDatabase+<EnumerateAll>d__78.MoveNext()',
        'System.Linq.Enumerable.TryGetFirst', 'UnityEditor.Search.SearchDatabase.GetDefaultSearchDatabase()',
        startup + '()', 'UnityEditor.EditorApplication.Internal_CallDelayFunctions()'))
    verify(is_noise(pc), '6000.6.9f1 PC Search stack is a warning')
    verify(is_noise(brief), 'abbreviated at-prefixed Search stack with CRLF is a warning')
    for label, stack in (
        ('missing stack', None), ('blank stack', ' \r\n\t'),
        ('no startup callback', '\n'.join(line for line in pc.splitlines() if startup not in line)),
        ('game frame before Search', 'RewindNotary.GameShell.Update ()\n' + pc),
        ('harness frame between Search frames', brief.replace('  at ' + startup, '  at AutoTestRunner.Play()\n  at ' + startup)),
        ('game frame after Search', pc + 'RewindNotary.GameBootstrap.Update () (at Assets/Scripts/GameBootstrap.cs:1)'),
        ('other editor subsystem', pc + 'UnityEditor.SceneView.OnGUI()'),
        ('unregistered Search method', pc + startup + 'Extra()'),
        ('project source on a listed method', pc + startup + ' () (at Assets/Editor/Search.cs:10)'),
        ('unparseable frame', pc + '<unknown frame>')):
        verify(not is_noise(stack), label + ' remains an error')

    on_log = ' '.join(body(runtime, 'private static void OnLog(').split())
    routing = ('if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception) { '
               'if (Application.isEditor && IsEditorSearchStartupNoise(stack)) Report.Warning(type.ToString(), message, stack); '
               'else Report.Error(type.ToString(), message, stack); }')
    verify(on_log.endswith(routing) and 'OnLog(entry.message, entry.stack, entry.type);' in runtime,
           'editor-only warning routing and fatal fallback shared by both log callbacks')
    verify('schemaVersion = 2,' in report and all(
        f'public List<RunError> {field} = new List<RunError>();' in report for field in ('errors', 'warnings'))
        and re.search(r'void Warning\([^)]*\)\s*=> warnings\.Add\(LogEntry\(type, message, stack\)\);', report)
        and re.search(r'void Error\([^)]*\)\s*=> errors\.Add\(LogEntry\(type, message, stack\)\);', report)
        and 'utc = DateTime.UtcNow.ToString("O"), type = type, message = message, stack = stack' in report,
        'v2 errors and warnings preserve the same original log fields')
    verify('if (report.errors.Count != 0) { Stop("Unity or harness error"); return; }' in driver
           and 'Report.errors.Count == 0 ? 0 : 1' in runtime and 'warnings' not in driver + runtime,
           'only errors affect the active step and final exit code')


def validate_autotest(check, project, scripts, body):
    outcomes = []

    def verify(ok, label):
        check(bool(ok), 'Autotest: ' + label)
        outcomes.append(bool(ok))

    bot, driver, runtime, report, device, keys = (scripts[name] for name in
        ('FirstDepartureBot', 'AutoTestDriver', 'AutoTestRuntime', 'AutoTestReport', 'GameInputDevice', 'BotKeyboard'))
    shell = scripts['GameShell']
    runner = (project / 'Assets/Editor/AutoTestRunner.cs').read_text()
    story = json.loads((project / 'Assets/Resources/Story/representative.json').read_text())
    validate_story_point_strike(verify, project, scripts, story, body)
    for path, change in AUTOTEST_EDITS.items():
        source = prior_level3_source(path, (project.parents[1] / path).read_text())
        source = reverse_exact(LEVEL_EDITS.get(path), source)
        source = reverse_exact(FEEL_EDITS.get(path), source)
        prior = reverse_exact(change, source)
        verify(hashlib.sha256(prior.encode()).hexdigest() == change['original'] and prior != source
               and reverse_exact(change, source + '// unrelated edit') == source + '// unrelated edit',
               'exact audited patch and unrelated mutation rejection ' + path)

    mapping = scripts['GameInput']
    alias = 'using Input = RewindNotary.GameInputDevice;\n'
    mapping_path = 'unity/RewindNotary/Assets/Scripts/Shell/GameInput.cs'
    historical_mapping = reverse_exact(FEEL_EDITS[mapping_path], mapping)
    verify(alias in mapping and hashlib.sha256(historical_mapping.replace(alias, '').encode()).hexdigest()
           == AUTOTEST_EDITS[mapping_path]['original'], 'every legacy mapping byte preserved')
    for method, arg in [('GetKey', 'key'), ('GetKeyDown', 'key'), ('GetKeyUp', 'key'), ('GetAxisRaw', 'axis'),
                        ('GetMouseButton', 'button'), ('GetMouseButtonDown', 'button')]:
        verify(f'Source?.{method}({arg}) ?? Input.{method}({arg})' in device, 'physical fallback ' + method)
    verify('current.Contains(key) && !previous.Contains(key)' in keys and
           '!current.Contains(key) && previous.Contains(key)' in keys and 'previous = current;' in keys and
           'current.Clear();' in keys, 'non-consuming down/hold/up edges shared by all readers')
    verify('DefaultExecutionOrder(-1000)' in driver and 'DefaultExecutionOrder(-200)' in shell and
           'DefaultExecutionOrder(-90)' in scripts['DirectionalSkillInput'], 'bot precedes shell, direction sampling and combat')
    verify(all('KeyCode.' + key in bot + keys for key in ('A', 'D', 'Space', 'J', 'Q', 'L', 'K', 'F', 'Return', 'DownArrow', 'UpArrow', 'R')),
           'scenario actions pass through keyboard including menu, cast and retry')
    verify('keys.Cast()' in bot and all(t in keys for t in ('Cast(int horizontal = 0, int vertical = 0)',
           'horizontal < 0 ? KeyCode.A : KeyCode.D', 'vertical < 0 ? KeyCode.DownArrow : KeyCode.UpArrow',
           'Tap(KeyCode.Q)')), 'direction and cast delivered together to original press-time resolver')
    actions = {'use', 'walk', 'strike', 'confirm', 'win'}
    route = [row for row in story['rows'] if row['sceneId'] in {'intro_attic', 'intro_board', 'intro_forge',
             'velvet_entry', 'mikhail_bout_i0', 'mikhail_reunion_i1', 'first_transfer', 'mid_invest', 'luka_lift'}]
    verify({token.partition(':')[0] for row in route for token in row['requiredPlayerAction']} == actions and
           all(x in bot for x in ('"use:', 'Walk(point.x', '"strike:', 'ShellPage.StoryDialogue', 'FightBoss()')),
           'all authored first-departure action types have physical input paths')
    stages = re.search(r'enum PlaytestStep \{([^}]+)\}', bot)[1].strip().split(', ')
    limits = [int(x) for x in re.search(r'int\[\] Limits = \{([^}]+)\}', bot)[1].split(',')]
    verify(len(limits) == len(stages) == 9 and all(0 < limit <= 600 for limit in limits) and
           set(re.findall(r'case PlaytestStep\.(\w+):', body(bot, 'internal bool Passed('))) == set(stages),
           'all nine stages have an independent assertion and finite real-time deadline')
    for flag in ('mint_candy_placed', 'contract_copy_received', 'sixth_bowl_stamped', 'mikhail_defeated',
                 'mikhail_i0_complete', 'returned_for_mid_invest', 'exp_step4_complete', 'luka_boarded', 'luka_lift_complete'):
        verify(any(row['completionFlag'] == flag for row in route) and flag in bot, 'assert shipped completion ' + flag)
    verify(all(t in bot for t in ('sawSafeCall && silver.validUseCount >= 80', 'Area.OnReturnFloor',
           'Record.completedEncounters.Contains("story:mikhail_bout")', 'room == "clock_01_entry"', 'Session.ActiveSlot == 1')),
           'verify physical safe-floor return, encounter receipt and same-slot district entry')
    verify(not re.search(r'\b(?:ResetAt|SetStoryFlag|GrantPracticeSkills|SeedBoundary|BeginUse|CompleteEncounter|'
                         r'Activate|ConfirmDialogue|SelectStage|SelectSlot|Travel|Hit|Apply)\s*\(', bot)
           and 'transform.position =' not in bot, 'bot drives inputs without state or world mutation')
    verify(all(t in driver for t in ('nextProbe = now + .25', 'if (now - lastProgress > 18)', 'now - entered >',
           'EndStep("fail", reason)', 'step.endState = bot.Snapshot()', 'Capture("failure")', 'StartDrain(false)')),
           'bounded stall and absolute timeout publish failure evidence and skip dependents')
    verify('audit.verifiedFinalSlot = audit.observedSlotWrite && SavedArrival(slot)' in runtime and
           all(t in body(runtime, 'private static bool SavedArrival(') for t in
               ('File.ReadAllText(path)', 'saved.schemaVersion == CoreRecord.SchemaVersion', 'saved.lastStageId == "clock"',
                'silver.validUseCount >= 80', 'saved.slotId == "slot-1"', 'saved.storyFlags.Contains("luka_lift_complete")')),
           'successful run requires reading back the final v4 arrival from the temporary slot')
    verify('RuntimeInitializeLoadType.BeforeSplashScreen' in runtime and
           'Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest")' in runtime and
           'if (!requested) return;' in runtime and 'if (!requested || sandbox == null) return;' in runtime,
           'opt-in before scene and fail-closed bootstrap')
    physical_refs = {name for name, src in scripts.items() if 'Application.persistentDataPath' in src}
    verify(physical_refs == {'AutoTestRuntime'} and 'var root = AutoTestRuntime.SaveRoot;' in shell and
           'new SettingsStore(root)' in shell and 'Path.Combine(root, "SkillSystem")' in shell and
           'MigrateLegacy(directory, catalog)' in shell and 'sandbox ?? throw' in runtime,
           'settings, migration, slots and usage logs share one isolated root')
    verify(all(t in runtime for t in ('Guid.NewGuid()', 'Path.GetTempPath()', 'Inside(temp, real)',
           'audit.afterSha256 = Fingerprint', 'audit.beforeSha256 == audit.afterSha256', 'audit.observedSlotWrite',
           'Directory.Delete(sandbox, true)')) and 'bot.Game.StopStage()' in driver,
           'fresh temp root, real save content verification and teardown after final save')
    verify(all(t in runtime for t in ('Application.logMessageReceived += OnLog', 'LogType.Error', 'LogType.Exception',
           'LogType.Assert', 'Report.errors.Count == 0', 'Application.Quit(code)')), 'Unity errors retained and affect exit status')
    validate_log_classification(verify, runtime, report, driver, body)
    verify(all(t in driver for t in ('Capture("start")', 'Capture("boss-start")', 'Capture("boss-end-"',
           'ScreenCapture.CaptureScreenshot', 'CompletePng(', 'drainStarted < 10')) and
           'com.unity.modules.screencapture' in (project / 'Packages/manifest.json').read_text(),
           'checkpoint PNGs requested, linked and checked before bounded shutdown')
    verify(all(t in driver for t in ('health.Damaged +=', 'health.Died +=', 'Progression.UseBegan +=',
           'e.kind == "valid_use"', 'report.bossAttempts.Add', 'EndBout("retry")')),
           'accepted activations, effective uses, damage, deaths and timed attempts are observed')
    verify('SceneManager.sceneLoaded += SceneLoaded' in runtime and
           'AutoTestRuntime.LoadEnd("room", Room.id, loadStarted)' in scripts['RepresentativeArea'] and
           'AutoTestRuntime.LoadEnd("stage", record.lastStageId, loadStarted)' in scripts['GameBootstrap'] and
           all(t in report for t in ('Math.Ceiling(frames * .01)', 'frameMs.Sort()', 'i >= frames - tail',
           'meanMs = total / frames', 'slowest1PercentMeanMs = slow / tail', '1000 / slowest1PercentMeanMs')),
           'actual scene/room loading and frame-time slowest percentile tail')
    verify(all(t in runner for t in ('public static void Run()', 'AutoTestRunner.Play -autotest', 'WaitForExit(45',
           'EditorApplication.EnterPlaymode()', 'OpenScene("Assets/Scenes/Main.unity")', 'EditorApplication.Exit(code)'))
           and 'worker.ExitCode == 0 && result.exitCode == 0' in runner,
           'batch parent owns -quit until rendered Play Mode worker and durable result complete')
    verify('options = BuildOptions.None' in (project / 'Assets/Editor/StandaloneBuild.cs').read_text()
           and 'result.autotestArguments = new[] { "-autotest", "-autotestProjectPath", project }' in
           (project / 'Assets/Editor/StandaloneBuild.cs').read_text()
           and '#if UNITY_EDITOR' not in driver and '-autotestProjectPath' in runtime,
           'regular standalone build contains same command-line scenario')
    return dict(checks=len(outcomes), passed=sum(outcomes), steps=stages, deadlines_seconds=limits, unity_executed=False)
