"""Source contracts, numeric boundary cases and one-change mutation tests for input feel."""
import hashlib
import json
import math
import operator
import re

from localization_followup import AUDIT, approved_table, prior_help_source
from story_link_baseline import FEEL_EDITS, LEVEL_EDITS, reverse_exact


def number(source, name):
    value = re.search(r'\b' + name + r' = ([\d.]+)f', source)
    return float(value[1]) if value else math.nan


def ordered(source, *pieces):
    start = 0
    for piece in pieces:
        at = source.find(piece, start)
        if at == -1:
            return False
        start = at + len(piece)
    return True


def release_trace(speed, drag, dt):
    if not (speed > 0 and drag > 0 and dt > 0):
        return {}
    ticks = math.ceil(speed / (drag * dt))
    speeds = [max(0., speed - drag * dt * n) for n in range(1, ticks + 1)]
    first = next(n for n, vx in enumerate(speeds, 1) if vx <= .5)
    return dict(ticks_to_half=first, seconds_to_half=round(first * dt, 4),
                speed_at_half=round(speeds[first - 1], 4), seconds_to_zero=round(ticks * dt, 4),
                distance_to_zero=round(sum(speeds) * dt, 4))


def comparison(source, variable):
    match = re.fullmatch(re.escape(variable) + r' (<=|>=|==|<|>) ([\d.]+)f?;', source)
    if not match:
        return lambda _: False
    op = {'<': operator.lt, '<=': operator.le, '>': operator.gt, '>=': operator.ge, '==': operator.eq}[match[1]]
    return lambda value: op(value, float(match[2]))


def contracts(scripts, body, dt):
    motor, player, mapping, shell, probe, driver, report, runtime, keys = (scripts[n] for n in
        ('CombatMotor2D', 'PlayerController2D', 'GameInput', 'GameShell', 'FeelProbe', 'AutoTestDriver',
         'AutoTestReport', 'AutoTestRuntime', 'BotKeyboard'))
    move = body(motor, 'public void Move(')
    air = body(move, 'if (Health.IsPlayer && !Grounded)')
    update = body(player, 'private void Update()')
    capture = 'if (!castDown && GameInput.AttackDown) attackPressed = Time.time;'
    dispatch = 'if (!castDown && !pendingCast.HasValue && Time.time - attackPressed <= AttackBuffer && motor.Attack())'
    drain = body(update, dispatch)
    attack = body(motor, 'public bool Attack()')
    suspend = body(player, 'public void SuspendInput()')
    steer = number(motor, 'AirAcceleration')
    coast = number(motor, 'AirDrag')
    brake = number(motor, 'AirBrake')
    speed = number(player, 'MoveSpeed')
    window = number(player, 'AttackBuffer')
    dead = number(mapping, 'PadDeadZone')
    combo = body(motor, 'private static readonly AttackSpec[] PlayerCombo =')
    opens = [float(values.split(',')[2].strip().rstrip('f')) for values in re.findall(r'new AttackSpec\(([^)]+)\)', combo)]
    drive = body(probe, 'internal bool Drive(')
    stop = body(probe, 'private void SampleStop()')
    late = body(probe, 'private void LateUpdate()')
    tick = body(driver, 'private void Update()')
    limits = re.search(r'bool inTime = ground \? (.+?) : (.+?);', stop)
    speeds = re.search(r'bool stopped = ground \? (.+?) : (.+?);', stop)
    results = {}
    results['ground movement'] = ('new Vector2(Attacking ? attackMove : speed, Body.linearVelocity.y)' in move
        and 'axis = GameInput.Horizontal;' in update
        and 'motor.Move(axis * MoveSpeed);' in body(player, 'private void FixedUpdate()'))
    results['air steering and released attack drag'] = (steer == 45 and coast == 6 and brake == 28
        and 'float releaseDrag = Attacking ? AirDrag : AirBrake;' in air
        and 'Mathf.Abs(speed) > 0.01f ? AirAcceleration : releaseDrag;' in air
        and 'Mathf.MoveTowards(Body.linearVelocity.x, speed, acceleration * Time.fixedDeltaTime)' in air)
    normal = release_trace(speed, brake, dt)
    results['air release from full speed'] = (speed == 6.5 and dt == .02 and normal.get('seconds_to_half', 1) <= .35
        and normal.get('ticks_to_half') == 11 and release_trace(speed, coast, dt).get('seconds_to_half', 0) > .35)
    results['capture before defense and skill gates'] = ordered(update, capture,
        'if (motor.Defense != null && motor.Defense.BlocksActions)', 'cutJump = false; return;',
        'if (motor.BenefactorSkills != null && motor.BenefactorSkills.BlocksActions)', 'cutJump = false; return;', dispatch)
    results['blocked retries retain the original press time'] = (capture in update and dispatch in update
        and 'attackPressed =' not in update.partition(capture)[2].partition(dispatch)[0] and update.count('attackPressed =') == 3)
    results['accept once including combo reservations'] = (window == .13 and drain.split() ==
        'attackPressed = float.NegativeInfinity; AttackInputAccepted?.Invoke();'.split()
        and update.count('motor.Attack()') == 1 and 'if (Health.Dead || Stunned) return false;' in attack
        and ordered(attack, 'elapsed < activeAttack.BufferOpen', 'return false;', 'queuedAttack = true;', 'return true;')
        and 'AttackStarted?.Invoke();' in body(motor, 'private void BeginAttack('))
    # Single edge, then only retries. Sample the authored gate times and both sides of expiry.
    def first_accept(press, gate, samples):
        return next((t for t in samples if t - press <= window and t >= gate), None)
    results['recovery at and after expiry'] = (first_accept(0, .13, (0, .12, .13, .14)) == .13
        and first_accept(0, .131, (0, .12, .131, .14)) is None)
    results['early tap reaches each chainable window'] = (len(opens) == 4 and all(.02 < gate and
        first_accept(.02, gate, (.02, gate - .001, gate)) == gate for gate in opens[:-1]))
    results['focus clears input reservations'] = ('if (!focused) ClearInput();' in body(player, 'private void OnApplicationFocus(')
        and 'SuspendInput();' in body(player, 'public void ClearInput()') and 'SuspendInput();' in body(player, 'private void OnDisable()')
        and all(x in suspend for x in ('axis = 0f;', 'pendingCast = null;', 'cutJump = false;',
            'attackPressed = jumpPressed = lastGrounded = float.NegativeInfinity;')))
    pad = body(mapping, 'private static float PadAxis(')
    results['both pad axes preserve values outside the dead zone'] = (dead == .25 and
        all(f'return keyboard != 0 ? keyboard : PadAxis("Pad{axis}", "PadDpad{axis}");' in
            body(mapping, 'public static float ' + axis) for axis in ('Horizontal', 'Vertical'))
        and 'Stronger(Input.GetAxisRaw(stick), Input.GetAxisRaw(dpad))' in pad
        and 'return Mathf.Abs(value) <= PadDeadZone ? 0f : value;' in pad)
    skill = number(mapping, 'SkillDirectionThreshold')
    results['menu and cast axis boundaries'] = (skill == .5 and 'Mathf.Abs(raw.x) >= 0.5f' in shell
        and 'Mathf.Abs(raw.y) >= 0.5f' in shell and 'direction.y > SkillDirectionThreshold' in mapping
        and all((abs(0 if abs(v) <= dead else v) > skill) == (abs(v) > .5)
                for sign in (-1, 1) for v in [sign*x for x in (.24, .25, .2501, .4999, .5, .5001, 1)]))
    bindings = re.search(r'public static bool AttackDown => ([^;]+);', mapping)
    results['HUD matches physical attack bindings'] = bool(bindings and re.findall(r'GetKeyDown\(KeyCode\.(\w+)\)|GetMouseButtonDown\((\d+)\)',
        bindings[1]) == [('J', ''), ('Z', ''), ('', '0'), ('JoystickButton2', '')])
    results['bot owns release frames'] = ('current.Clear();' in body(keys, 'internal void NextFrame()')
        and ordered(tick, 'keyboard.NextFrame();', '!feel.Drive(bot, keyboard, report.feel)', 'bot.Drive((PlaytestStep)index);')
        and 'return true;' in drive and 'case Phase.GroundStop:' not in drive and 'case Phase.AirStop:' not in drive)
    results['ground setup requires sustained real speed'] = ('runTicks >= 4 && player.Grounded && AtRunSpeed(speed)' in drive
        and 'result.groundStop.releaseSpeed = speed;' in drive and 'else keys.Hold(KeyCode.D);' in drive
        and 'runTicks = player.Grounded && AtRunSpeed(player.Body.linearVelocity.x) ? runTicks + 1 : 0;' in probe)
    results['jump from full speed then release all keys'] = (ordered(drive, 'case Phase.AirRun:', 'keys.Hold(KeyCode.D);',
        'player.Grounded && AtRunSpeed(speed)', 'keys.Hold(KeyCode.Space);', 'phase = Phase.Jump;', 'case Phase.Jump:',
        '!player.Grounded && !player.Attacking && player.Body.linearVelocity.y > 0f && AtRunSpeed(speed)',
        'result.airStop.releaseSpeed = speed;', 'phase = Phase.AirStop;'))
    results['post-physics ground and air observations'] = (ordered(probe, 'new WaitForFixedUpdate()', 'yield return afterPhysics;',
        'phase == Phase.GroundStop || phase == Phase.AirStop) SampleStop();') and all(x in stop for x in
        ('sample.physicsTicks++;', 'sample.seconds += Time.fixedDeltaTime;', 'sample.speed = Mathf.Abs(player.Body.linearVelocity.x);',
         'player.Grounded != ground || touchedFloor != ground || player.Attacking', 'IsTouchingLayers(1 << LevelBuilder.GroundLayer)')))
    if limits and speeds:
        for idx, name, time_var, time_cases, speed_cases in (
                (1, 'ground', 'sample.physicsTicks', ((1, True), (3, True), (4, False)), ((.0999, True), (.1, False))),
                (2, 'air', 'sample.seconds', ((.34, True), (.35, True), (.36, False)), ((.5, True), (.501, False)))):
            time_ok = comparison(limits[idx]+';', time_var)
            speed_ok = comparison(speeds[idx]+';', 'sample.speed')
            results[name + ' stop boundaries'] = all(time_ok(v) == ok for v, ok in time_cases) and all(speed_ok(v) == ok for v, ok in speed_cases)
    else:
        results['ground stop boundaries'] = results['air stop boundaries'] = False
    results['late stop fails even if speed later meets threshold'] = (ordered(stop, 'if (inTime && stopped)', 'sample.status = "pass";',
        'else if (ground ? sample.physicsTicks >= 3 : sample.seconds >= .35f)', 'Abort('))
    tap = drive.partition('case Phase.Tap:')[2].partition('break;\n                case')[0]
    results['neutral single J observes state and event'] = ('player.Attacking || Mathf.Abs(speed) >= .1f' in tap
        and re.findall(r'keys\.(?:Tap|Hold)\((.*?)\)', tap) == ['KeyCode.J'] and 'tapFrame = Time.frameCount;' in tap
        and 'player.AttackStarted += OnAttackStarted;' in tap and 'result.stationaryAttack.attackStartedCount++;' in probe
        and all(x in late for x in ('Time.frameCount - tapFrame', 'sample.attacking = player.Attacking;',
            'sample.framesSinceTap <= 2 && sample.attacking && sample.attackStartedCount == 1',
            'else if (sample.framesSinceTap >= 2) Abort(', 'player.AttackStarted -= OnAttackStarted;')))
    results['issued and accepted taps counted independently'] = (ordered(tick, 'bot.Drive((PlaytestStep)index);',
        'if (keyboard.GetKeyDown(KeyCode.J)) report.attackInputs.issued++;') and
        all(x in driver for x in ('controller.AttackInputAccepted += AttackAccepted;', 'controller.AttackInputAccepted -= AttackAccepted;',
            'void AttackAccepted() { report.attackInputs.accepted++; }')) and
        ordered(body(report, 'public void Write('), 'attackInputs.unaccepted = attackInputs.issued - attackInputs.accepted;', 'JsonUtility.ToJson(this, true)'))
    results['failed or missing checks fail the run'] = ('Stop(report.feel.failure)' in tick and '&& !feel.Passed)' in tick
        and 'StartDrain(false);' in body(driver, 'private void Stop(') and
        'groundStop.status == "pass" && airStop.status == "pass" && stationaryAttack.status == "pass"' in report
        and ordered(runtime, 'completed && !Report.feel.Passed) Report.Error', 'Report.errors.Count == 0 ? 0 : 1',
                    'Report.exitCode = code;', 'Report.Write(directory);') and 'now - lastProgress > 18' in tick)
    return results


# Each mutant must turn the named passing production contract red.
MUTATIONS = (
    ('CombatMotor2D', 'Attacking ? AirDrag : AirBrake', 'Attacking ? AirBrake : AirDrag', 'air steering and released attack drag'),
    ('CombatMotor2D', 'AirBrake = 28f', 'AirBrake = 6f', 'air release from full speed'),
    ('CombatMotor2D', 'Attacking ? attackMove : speed', 'Attacking ? attackMove : Body.linearVelocity.x', 'ground movement'),
    ('PlayerController2D', 'AttackBuffer = 0.13f', 'AttackBuffer = 0.12f', 'recovery at and after expiry'),
    ('PlayerController2D', 'AttackBuffer = 0.13f', 'AttackBuffer = 0.04f', 'early tap reaches each chainable window'),
    ('PlayerController2D', '!castDown && GameInput.AttackDown', '!castDown && !motor.Defense.BlocksActions && GameInput.AttackDown', 'capture before defense and skill gates'),
    ('PlayerController2D', 'if (motor.Defense.State.Phase != DefensePhase.Dash)', 'attackPressed = float.NegativeInfinity; if (motor.Defense.State.Phase != DefensePhase.Dash)', 'blocked retries retain the original press time'),
    ('PlayerController2D', '{ pendingCast = null; jumpPressed =', '{ attackPressed = float.NegativeInfinity; pendingCast = null; jumpPressed =', 'blocked retries retain the original press time'),
    ('PlayerController2D', '&& motor.Attack())', '&& axis != 0f && motor.Attack())', 'capture before defense and skill gates'),
    ('PlayerController2D', 'attackPressed = float.NegativeInfinity;\n                AttackInputAccepted', 'attackPressed = Time.time;\n                AttackInputAccepted', 'accept once including combo reservations'),
    ('PlayerController2D', 'if (!focused) ClearInput()', 'if (focused) ClearInput()', 'focus clears input reservations'),
    ('GameInput', 'PadDeadZone = 0.25f', 'PadDeadZone = 0.5f', 'both pad axes preserve values outside the dead zone'),
    ('GameInput', 'PadAxis("PadVertical", "PadDpadVertical")', '0f', 'both pad axes preserve values outside the dead zone'),
    ('GameInput', 'SkillDirectionThreshold = 0.5f', 'SkillDirectionThreshold = 0.25f', 'menu and cast axis boundaries'),
    ('GameInput', 'Input.GetKeyDown(KeyCode.Z)', 'Input.GetKeyDown(KeyCode.X)', 'HUD matches physical attack bindings'),
    ('BotKeyboard', 'current.Clear();', 'previous.Clear();', 'bot owns release frames'),
    ('FeelProbe', 'runTicks >= 4', 'runTicks >= 0', 'ground setup requires sustained real speed'),
    ('FeelProbe', 'result.airStop.releaseSpeed = speed;', 'result.airStop.releaseSpeed = 0f;', 'jump from full speed then release all keys'),
    ('FeelProbe', 'new WaitForFixedUpdate()', 'new WaitForEndOfFrame()', 'post-physics ground and air observations'),
    ('FeelProbe', 'touchedFloor != ground || ', '', 'post-physics ground and air observations'),
    ('FeelProbe', 'sample.physicsTicks <= 3', 'sample.physicsTicks <= 4', 'ground stop boundaries'),
    ('FeelProbe', 'sample.speed < .1f', 'sample.speed <= .1f', 'ground stop boundaries'),
    ('FeelProbe', 'sample.seconds <= .35f', 'sample.seconds <= .36f', 'air stop boundaries'),
    ('FeelProbe', 'sample.speed <= .5f', 'sample.speed < .5f', 'air stop boundaries'),
    ('FeelProbe', 'if (inTime && stopped)', 'if (stopped)', 'late stop fails even if speed later meets threshold'),
    ('FeelProbe', 'keys.Tap(KeyCode.J);', 'keys.Hold(KeyCode.D); keys.Tap(KeyCode.J);', 'neutral single J observes state and event'),
    ('FeelProbe', 'sample.framesSinceTap <= 2', 'sample.framesSinceTap <= 3', 'neutral single J observes state and event'),
    ('FeelProbe', 'sample.attacking && sample.attackStartedCount == 1', 'sample.attacking', 'neutral single J observes state and event'),
    ('AutoTestDriver', 'controller.AttackInputAccepted += AttackAccepted;', 'health.GetComponent<CombatMotor2D>().AttackStarted += AttackAccepted;', 'issued and accepted taps counted independently'),
    ('AutoTestReport', 'attackInputs.issued - attackInputs.accepted', '0', 'issued and accepted taps counted independently'),
    ('AutoTestRuntime', 'completed && !Report.feel.Passed', 'false', 'failed or missing checks fail the run'),
    ('AutoTestDriver', 'now - lastProgress > 18', 'now - lastProgress > 25', 'failed or missing checks fail the run'),
)


def validate_feel_pass(check, project, scripts, body):
    dt = float(re.search(r'Fixed Timestep: ([\d.]+)', (project/'ProjectSettings/TimeManager.asset').read_text())[1])
    base = contracts(scripts, body, dt)
    for label, ok in base.items():
        check(ok, 'Feel: ' + label)
    mutations = {}
    for name, old, new, label in MUTATIONS:
        source = scripts[name]
        mutant = dict(scripts, **{name: source.replace(old, new, 1)})
        detected = base[label] and source.count(old) == 1 and not contracts(mutant, body, dt)[label]
        mutations[f'{name} / {old} -> {new}'] = detected
    for path, audit in FEEL_EDITS.items():
        source = reverse_exact(LEVEL_EDITS.get(path), (project.parents[1]/path).read_text())
        previous = reverse_exact(audit, source)
        check(previous != source and hashlib.sha256(previous.encode()).hexdigest() == audit['original'],
              'Feel: exact historical source ' + path)
        changed = source + '// unrelated change'
        mutations[path + ' extra edit'] = reverse_exact(audit, changed) == changed
    for path in sorted((project/'Assets/Resources/Localization').glob('*.json')):
        lang, source = path.stem, path.read_text()
        def approved_help(text):
            entries = {e['key']: e['value'] for e in json.loads(text)['entries']}
            return entries.get('area.help') == approved_table(lang)['area.help'] and (
                hashlib.sha256(prior_help_source(lang, text).encode()).hexdigest() == AUDIT[lang]['updated_sha256'])
        ok = approved_help(source)
        check(ok, 'Feel: exact HUD-only edit ' + lang)
        original = prior_help_source(lang, source)
        mutations[lang + ' obsolete help'] = ok and not approved_help(original)
        changed = re.sub(r'("key": "story\.intro\.attic\.01",\s*"value": ")', r'\g<1>!', source, count=1)
        mutations[lang + ' unrelated story edit'] = ok and changed != source and not approved_help(changed)
    for label, detected in mutations.items():
        check(detected, 'Feel mutation detected: ' + label)
    motor = scripts['CombatMotor2D']
    return dict(contracts=len(base), mutations=len(mutations), detected=sum(mutations.values()),
                fixed_dt=dt, release={key: release_trace(number(scripts['PlayerController2D'], 'MoveSpeed'), number(motor, key), dt)
                                      for key in ('AirDrag', 'AirBrake')}, unity_executed=False)
