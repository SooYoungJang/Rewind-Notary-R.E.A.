"""Defense source contracts + clock-driven Python reference scenarios, NOT executed Unity/C#."""
import hashlib
from investigation_baseline import baseline_digest
import json
import math
import re
import numpy as np


def validate_dash_parry(check, project, scripts, body):
    names, scenarios = [], []

    def verify(ok, message):
        check(ok, "Dash/parry: " + message)
        names.append(message)

    def scenario(ok, message):
        verify(ok, "reference: " + message)
        scenarios.append(message)

    root = project.parents[1]
    protected = json.loads((root / "tools/dash-parry-protected.sha256.json").read_text())
    for path, expected in protected.items():
        from validate_remaining_skills import extended_baseline
        extension = extended_baseline(root, path, scripts, body)
        verify(extension if extension is not None else baseline_digest(root, path) == expected,
               ("authorized extension preserves original contracts: " if extension is not None else "baseline unchanged: ") + path)

    state, defense, tuning, motor, inputs, player, health, session = (scripts[n] for n in
        ["DefenseState", "PlayerDefense2D", "DefenseTuning", "CombatMotor2D", "GameInput",
         "PlayerController2D", "Health", "SkillSession"])
    values = {k: float(v) for k, v in re.findall(r"const float (\w+) = ([0-9.]+)f;", tuning)}
    expected_values = {
        "DashDistance": 3.2, "DashSeconds": .16, "DashInvulnerableSeconds": .10, "DashCooldown": .60,
        "ParryWindowSeconds": .12, "ParryRecoverySeconds": .24, "ParrySuccessGuardSeconds": .10,
        "CounterSeconds": .65, "CounterDamageMultiplier": 2, "ParryStopScale": .02, "ParryStopSeconds": .14,
        "TrailInterval": .04, "TrailSeconds": .12, "TrailLength": .65, "TrailHeight": .9,
        "ParryEffectSeconds": .18, "ParryEffectLength": .9,
    }
    for key, expected in expected_values.items():
        verify(values.get(key) == expected, f"adjustable baseline {key} = {expected}")
    # Fail cleanly if a required constant was deleted, instead of hiding the rest of the report.
    if not set(expected_values).issubset(values):
        return {"checks": len(names), "source_constants_missing": True, "unity_executed": False}
    v = values
    verify("DashSpeed = DashDistance / DashSeconds" in tuning
           and 0 < v["DashInvulnerableSeconds"] < v["DashSeconds"] < v["DashCooldown"],
           "dash speed derives from distance/time; vulnerable tail and cooldown remain")
    verify(0 < v["ParryStopScale"] < .03 and v["ParryStopSeconds"] > .11,
           "parry hit-stop is stronger/longer than existing sword and finisher")
    verify(v["CounterSeconds"] > .16 and v["CounterDamageMultiplier"] > 1,
           "counter lasts long enough to start the first ordinary sword strike")

    for name, keys in {"DashDown": ["LeftShift", "RightShift", "K", "JoystickButton4"],
                       "ParryDown": ["L", "JoystickButton5"]}.items():
        match = re.search(r"public static bool " + name + r" => ([\s\S]*?);", inputs)
        verify(match is not None and set(re.findall(r"GetKeyDown\(KeyCode\.(\w+)\)", match[1])) == set(keys),
               "dedicated key-down mapping " + name + ": " + "/".join(keys))
    verify(inputs.count("JoystickButton4") == 1 and inputs.count("JoystickButton5") == 1,
           "LB/RB do not overlap existing gameplay or menu buttons")
    verify("[DefaultExecutionOrder(-75)]" in defense and "[DefaultExecutionOrder(-50)]" in motor
           and "[DefaultExecutionOrder(-200)]" in scripts["GameShell"],
           "defense input precedes both attacker motors, after shell pause processing")
    update = body(defense, "private void Update()")
    verify("!GameInput.GameplayEnabled" in update and "if (GameInput.CanPlay)" in update
           and "if (GameInput.DashDown) TryDash(GameInput.Horizontal)" in update
           and "else if (GameInput.ParryDown) TryStartParry()" in update,
           "menu/same-frame gates, dash precedence, no held-button repetition")
    verify("if (isPlayer)" in body(motor, "public void Initialize(")
           and "Defense = gameObject.AddComponent<PlayerDefense2D>()" in motor
           and "Defense.Initialize(this)" in motor,
           "player alone owns initialized defense component")
    verify("!motor.Health.Dead" in defense and "!motor.Stunned && !motor.Attacking" in defense
           and body(defense, "public bool TryDash(").startswith("\n            if (!CanStart)")
           and "if (!CanStart) return false" in body(defense, "public bool TryStartParry()"),
           "dead/stunned/committed attacks reject both actions")
    verify("motor.RefreshGround()" in body(defense, "public bool TryDash(")
           and "motor.ClearComboForDefense()" in body(defense, "public bool TryDash(")
           and "motor.ClearComboForDefense()" in body(defense, "public bool TryStartParry()")
           and "CancelAttack()" in body(motor, "internal void ClearComboForDefense()"),
           "dash samples ground on activation; defense closes old combo grace/context")
    dash = body(state, "public bool TryDash(")
    verify(all(s in dash for s in ["Tick(now)", "BlocksActions || now < DashReadyAt || (!grounded && AirDashUsed)",
           "Phase = DefensePhase.Dash", "AirDashUsed |= !grounded", "now + DefenseTuning.DashSeconds",
           "now + DefenseTuning.DashInvulnerableSeconds", "now + DefenseTuning.DashCooldown"]),
           "dash admission, charge consumption and start-relative timers wired")
    tick = body(state, "public void Tick(")
    verify("Phase == DefensePhase.ParryWindow && now >= phaseUntil" in tick
           and "Phase = DefensePhase.ParryRecovery" in tick
           and "phaseUntil += DefenseTuning.ParryRecoverySeconds" in tick
           and "Phase == DefensePhase.Dash || Phase == DefensePhase.ParryRecovery" in tick
           and "Phase = DefensePhase.Ready" in tick,
           "expiry uses >= and recovery anchors to window end, including long frames")
    parry = body(state, "public bool TryParry(")
    verify(parry.index("Tick(now)") < parry.index("Phase != DefensePhase.ParryWindow")
           and "Phase = DefensePhase.Ready" in parry
           and "counterUntil = now + DefenseTuning.CounterSeconds" in parry
           and "guardUntil = now + DefenseTuning.ParrySuccessGuardSeconds" in parry,
           "half-open parry window checked at contact, then one counter and brief follow-up guard")
    verify("now < counterUntil" in state and "now < dodgeUntil" in state and "now < guardUntil" in state
           and "Phase == DefensePhase.Dash && now < dodgeUntil" in state,
           "counter, dodge and guard ends are exclusive")
    verify("Defense?.State.Land()" in body(motor, "if (Grounded && !wasGrounded)")
           and "if (!Grounded) Defense?.State.ObserveAirborne()" in motor
           and "AirDashUsed = false" in body(state, "public void Land()")
           and "if (Phase == DefensePhase.Dash) AirDashUsed = true" in body(state, "public void ObserveAirborne()"),
           "real ground transition refunds air charge; crossing ledge while dashing consumes it")
    verify("Defense != null && !wasGrounded" in body(motor, "public void RefreshGround()")
           and "Defense.State.Phase == DefensePhase.Dash || Defense.State.AirDashUsed" in motor
           and all(token in body(motor, "public void RefreshGround()") for token in (
               "var floor = groundHits[i]", "if ((needsLandingContact || floor.TryGetComponent<AuthoredSurface>",
               "Mathf.Abs(Body.position.y - floor.bounds.max.y) > .09f", "!floor.IsTouching(capsule))) continue;")),
           "air dash and spent air charge need real ground contact, not just the near-floor probe")
    advance = body(defense, "private void Advance()")
    verify(advance.index("motor.RefreshGround()") < advance.index("State.Tick(Time.time)")
           and "if (State.Phase == DefensePhase.Dash) motor.RefreshGround()" in advance,
           "final dash physics step samples ledge/landing before timed dash exit")
    verify("AirDashUsed" not in re.sub(r"//[^\n]*", "", body(state, "public void Interrupt()"))
           and "DashReadyAt" not in body(state, "public void Land()")
           and "DashReadyAt = float.NegativeInfinity" in body(state, "public void Reset()"),
           "damage retains air charge/cooldown; landing retains cooldown; retry resets both")
    verify("savedGravity = motor.Body.gravityScale" in defense and "savedVelocity = motor.Body.linearVelocity" in defense
           and "motor.Body.gravityScale = 0f" in defense
           and "new Vector2(State.DashDirection * DefenseTuning.DashSpeed, 0f)" in defense
           and "motor.Body.gravityScale = savedGravity" in defense
           and "PlayerController2D.MoveSpeed)" in defense and "motor.Grounded ? 0f : savedVelocity.y" in defense,
           "horizontal dash suppresses gravity and restores prior vertical velocity/gravity")
    verify(all(s not in defense for s in ["transform.position =", "Body.position =", "isTrigger =", "IgnoreCollision", "simulated ="]),
           "dash does not teleport, disable body or bypass wall collision")
    verify("Defense.DriveMovement()" in body(motor, "public void Move(")
           and "Defense.BlocksActions" in body(motor, "public void Jump(")
           and "Defense.BlocksActions" in body(motor, "public bool Attack()")
           and "motor.Defense.BlocksActions" in body(scripts["SilverStitchSkill"], "public bool Activate()")
           and "motor.Defense.BlocksActions" in body(player, "private void FixedUpdate()"),
           "dash/window/failure recovery block movement, jump, sword and silver activations")
    verify("jumpPressed = float.NegativeInfinity; cutJump = false; return" in body(player, "private void Update()"),
           "defense clears buffered jump and release cuts before they can leak into dash")
    for signature, expected in [("private void ReceiveHit(", "Defense?.Interrupt()"),
                                ("private void Die()", "Defense?.ResetDefense()"),
                                ("public void ResetAt(", "Defense?.ResetDefense()"),
                                ("private void OnDisable()", "Defense?.ResetDefense()")]:
        verify(expected in body(motor, signature), "defense interruption/reset path: " + signature)
    verify("ResetDefense()" in body(defense, "private void OnDisable()")
           and "RestoreDashPhysics()" in body(defense, "public void Interrupt()")
           and "RestoreDashPhysics()" in body(defense, "public void ResetDefense()"),
           "disable/hit/retry restore gravity and clear temporary state")
    hit = body(health, "public bool Hit(")
    verify(hit.index("Dead || Time.time < InvulnerableUntil") < hit.index("defense.TryDefend(source)")
           < hit.index("Current = Mathf.Max") < hit.index("Damaged?.Invoke(source)")
           and "IsPlayer && TryGetComponent<PlayerDefense2D>" in hit,
           "ordinary immunity/death first; defense intercepts damage, knockback and death events")
    defend = body(defense, "public bool TryDefend(")
    verify(defend.index("Advance()") < defend.index("State.Evading(Time.time) || State.SuccessGuard(Time.time)")
           < defend.index("State.TryParry(Time.time)") < defend.index("RecordParrySuccess()"),
           "evade and post-success guard never count as parries; success fires only once")
    verify("ImpactFeedback.ParrySuccess()" in defend and "AudioManager.Sfx.Parry" in defend
           and "HitStop(DefenseTuning.ParryStopScale, DefenseTuning.ParryStopSeconds)" in scripts["ImpactFeedback"]
           and "Time.timeScale =" not in defense,
           "dedicated sound and enhanced hit-stop use existing managers/time restoration")
    verify("SkillStroke.Show(at, at - Vector3.right" in update and "SkillStroke.Afterimage" not in defense
           and "new Color(1f, 0.68f, 0.25f, 0.55f)" in defense
           and "go.transform.SetParent(WorldRoot, false)" in scripts["SkillStroke"],
           "amber dash strips differ from blue sword sprite afterimages and follow world cleanup")
    begin = body(motor, "private void BeginAttack(")
    strike = body(motor, "private void DeliverStrike()")
    verify("Defense.State.ConsumeCounter(Time.time) : 1f" in begin
           and "strikeDamage = Mathf.CeilToInt(damage * activeAttack.DamageMultiplier * counter)" in begin
           and motor.count("ConsumeCounter(") == 1
           and "counterUntil = float.NegativeInfinity" in body(state, "public float ConsumeCounter("),
           "one actual sword start snapshots/consumes counter, with ceiling integer damage")
    verify("destructible.Hit(strikeDamage), strikeDamage" in strike
           and "opponent.Hit(strikeDamage, transform.position, activeAttack.Knockback)" in strike
           and "opponent.gameObject.name, true, strikeDamage" in strike,
           "counter damage reaches enemy/destructible and existing same-chain use log consistently")
    verify(all(token not in state + defense for token in ["BeginUse(", "Progression.Apply(", "validUseCount", "Changed?.Invoke"]),
           "basic defense has no mastery activation, credit or save mutation")
    log = body(session, "public void RecordParrySuccess()")
    verify('kind = "parry_success", skillId = "", useId = ""' in log and "ParrySuccessCount++" in log
           and "Progression == null || closed" in log and "testData = Progression.Record.testData" in log
           and '"defense_only;preview=" + PreviewStage' in log and "Save()" not in log
           and "parrySuccessCount = ParrySuccessCount" in session,
           "separate session counter in uses JSONL, preview/slot context retained, no progression save")
    verify("ParrySuccessCount =" not in body(session, "public void SetPreview(")
           and "ParrySuccessCount" not in scripts["CoreRecord"],
           "parry telemetry survives attempt/preview switches but is not permanent skill progress")
    audio_names = re.search(r"Names\s*=\s*\{([^}]+)\}", scripts["AudioManager"])
    audio_enum = re.search(r"enum Sfx\s*\{([^}]+)\}", scripts["AudioManager"])
    verify(audio_names is not None and audio_enum is not None
           and re.findall(r'"([a-z-]+)"', audio_names[1]).index("parry")
           == [x.strip() for x in audio_enum[1].split(",")].index("Parry") == 9,
           "Parry appended at SFX slot 9 without moving existing enum/resource slots")
    combo = re.search(r"PlayerCombo\s*=\s*\{([^}]+)\}", motor)
    actual_combo = [[float(x.strip().removesuffix("f")) for x in row.split(",")]
                    for row in re.findall(r"new AttackSpec\(([^)]+)\)", combo[1])] if combo else []
    verify(actual_combo == [[.40, .16, .08, .90, 1.70, 1.55, 1, 1], [.36, .14, .07, 1, 1.90, 1.45, 2, 1],
                            [.44, .22, .10, 1.10, 2.10, 1.75, 3, 1], [.56, .28, .56, 1.20, 2.40, 1.90, 4, 1.7]],
           "all four original attack profiles exactly preserved")

    # Independent reference model for action admission, time boundaries and contact outcomes.
    # Numeric tuning comes from C# above; source contracts bind transitions to the production implementation.
    # This is intentionally reported as a Python reference model, not a C# test runner.
    f32 = np.float32

    class Model:
        def __init__(self):
            self.phase, self.end, self.cooldown = "ready", -math.inf, -math.inf
            self.air, self.direction = False, 1
            self.dodge, self.counter, self.guard = -math.inf, -math.inf, -math.inf
            self.successes, self.skill_uses = 0, 120

        def tick(self, now):
            now = f32(now)
            if self.phase == "window" and now >= self.end:
                self.phase, self.end = "recovery", f32(self.end + f32(v["ParryRecoverySeconds"]))
            if self.phase in ("dash", "recovery") and now >= self.end:
                self.phase = "ready"

        def dash(self, now, ground=True, direction=1):
            now = f32(now)
            self.tick(now)
            if self.phase != "ready" or now < self.cooldown or (not ground and self.air):
                return False
            self.phase, self.direction = "dash", -1 if direction < 0 else 1
            self.air |= not ground
            self.end = f32(now + f32(v["DashSeconds"]))
            self.dodge, self.cooldown = f32(now + f32(v["DashInvulnerableSeconds"])), f32(now + f32(v["DashCooldown"]))
            return True

        def parry(self, now):
            now = f32(now)
            self.tick(now)
            if self.phase != "ready" or now < self.guard:
                return False
            self.phase, self.end = "window", f32(now + f32(v["ParryWindowSeconds"]))
            return True

        def hit(self, now, immune=False, dead=False):
            now = f32(now)
            if immune or dead:
                return "ignored"
            self.tick(now)
            if (self.phase == "dash" and now < self.dodge) or now < self.guard:
                return "guarded"
            if self.phase != "window":
                return "damaged"
            self.phase = "ready"
            self.counter, self.guard = f32(now + f32(v["CounterSeconds"])), f32(now + f32(v["ParrySuccessGuardSeconds"]))
            self.successes += 1
            return "parried"

        def consume(self, now):
            now = f32(now)
            result = v["CounterDamageMultiplier"] if now < self.counter else 1
            self.counter = -math.inf
            return result

        def interrupt(self):
            self.phase, self.end = "ready", -math.inf
            self.dodge = self.guard = self.counter = -math.inf

        def land(self):
            self.air = False

        def airborne(self):
            if self.phase == "dash":
                self.air = True

        def reset(self):
            self.interrupt()
            self.air, self.cooldown = False, -math.inf

    before = lambda t: float(np.nextafter(f32(t), f32(-math.inf)))
    after = lambda t: float(np.nextafter(f32(t), f32(math.inf)))
    m = Model()
    scenario(m.dash(0) and m.phase == "dash" and not m.air, "ready -> ground dash without spending air charge")
    scenario(not m.dash(.01) and not m.parry(.01), "dash rejects overlapping dash/parry")
    for now, expected in [(0, "guarded"), (before(v["DashInvulnerableSeconds"]), "guarded"),
                          (v["DashInvulnerableSeconds"], "damaged"), (before(v["DashSeconds"]), "damaged")]:
        scenario(m.hit(now) == expected, f"dash contact at {now!r}: {expected}")
    m.tick(v["DashSeconds"])
    scenario(m.phase == "ready" and not m.dash(before(v["DashCooldown"])), "dash ends exactly; cooldown still blocks")
    scenario(m.dash(v["DashCooldown"]), "ground dash accepted at exact cooldown end")
    m = Model()
    scenario(m.dash(0, False, -1) and m.air and m.direction == -1, "air dash consumes one charge and locks left direction")
    scenario(not m.dash(5, False), "elapsed cooldown does not refund airborne charge")
    m.land()
    scenario(m.dash(5, False), "landing refunds air charge for next flight")
    m = Model()
    m.dash(0, False)
    m.land()
    scenario(not m.dash(v["DashSeconds"]), "landing does not bypass ground dash cooldown")
    m = Model()
    m.dash(0)
    m.airborne()
    scenario(m.air and not m.dash(1, False), "ground dash off ledge consumes flight charge")
    m = Model()
    m.airborne()
    scenario(m.dash(0, False), "ordinary walking/falling off ledge retains one air dash")
    m.interrupt()
    scenario(m.phase == "ready" and m.air and not m.dash(1, False), "damage clears action but cannot refund air dash")
    m.reset()
    scenario(m.dash(1, False), "death/retry reset restores dash availability")

    window = float(f32(v["ParryWindowSeconds"]))
    recovery_end = float(f32(f32(window) + f32(v["ParryRecoverySeconds"])))
    for now, expected in [(0, "parried"), (before(window), "parried"), (window, "damaged"), (after(window), "damaged")]:
        m = Model()
        m.parry(0)
        scenario(m.hit(now) == expected, f"parry contact at {now!r}: {expected}")
    m = Model()
    scenario(m.hit(0) == "damaged", "contact before activation cannot be parried retroactively")
    m.parry(0)
    scenario(not m.parry(.01) and not m.dash(.01), "window rejects repeated/overlapping defense")
    m.tick(window)
    scenario(m.phase == "recovery" and not m.parry(window) and not m.dash(window), "whiff -> locked failure recovery at exact window end")
    m.tick(before(recovery_end))
    scenario(m.phase == "recovery", "recovery remains locked immediately before its end")
    scenario(m.parry(recovery_end), "failure recovery ends inclusively for next input")
    m = Model()
    m.parry(0)
    m.tick(10)
    scenario(m.phase == "ready", "long frame crosses window and recovery without extending either")
    m = Model()
    m.parry(0)
    m.tick(.05)
    m.tick(.05)
    scenario(m.phase == "window" and m.hit(.05) == "parried", "paused game clock preserves remaining window")
    scenario(m.hit(.05) == "guarded" and m.successes == 1, "same-frame extra contact cannot double-count success")
    guard_end = float(f32(f32(.05) + f32(v["ParrySuccessGuardSeconds"])))
    scenario(m.hit(before(guard_end)) == "guarded" and m.hit(guard_end) == "damaged", "success guard end is exclusive")
    scenario(m.skill_uses == 120, "parry success leaves reference mastery count unchanged")
    scenario(m.consume(before(.05 + v["CounterSeconds"])) == v["CounterDamageMultiplier"]
             and m.consume(.05 + v["CounterSeconds"]) == 1, "next actual swing alone consumes counter, even on whiff")
    for when in [v["CounterSeconds"], after(v["CounterSeconds"])]:
        m = Model()
        m.parry(0)
        m.hit(0)
        scenario(m.consume(when) == 1, f"counter expired at {when!r}")
    for immune, dead in [(True, False), (False, True)]:
        m = Model()
        m.parry(0)
        scenario(m.hit(.01, immune, dead) == "ignored" and m.successes == 0,
                 f"ordinary immunity/death cannot farm parry count: immune={immune}, dead={dead}")
    m = Model()
    m.parry(0)
    m.hit(0)
    m.interrupt()
    scenario(m.consume(.01) == 1 and m.hit(.01) == "damaged", "damage interruption clears pending counter and guard")
    m.reset()
    scenario(m.successes == 1 and m.skill_uses == 120, "attempt reset keeps session-only success telemetry and mastery")

    return {"checks": len(names), "reference_scenarios": len(scenarios), "scenario_names": scenarios,
            "tuning": values, "dash_speed": v["DashDistance"] / v["DashSeconds"],
            "input_mapping": {"dash": "LeftShift/RightShift/K/Xbox LB (JoystickButton4)",
                              "parry": "L/Xbox RB (JoystickButton5)"},
            "validation_level": "C# source contracts, immutable baseline hashes and Python float32 clock/contact reference model",
            "unity_executed": False, "csharp_state_executed": False, "physical_gamepad_tested": False}
