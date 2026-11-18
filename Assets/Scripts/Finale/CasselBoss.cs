using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [RequireComponent(typeof(CombatMotor2D))]
    public sealed class CasselBoss : MonoBehaviour
    {
        private CombatMotor2D motor, player;
        private SkillSession session;
        private CasselDefinition data;
        private CasselPattern pattern;
        private float timer, guard, parryUntil, exposureUntil, staggerUntil, motionUntil, backstepUntil;
        private int cursor, beat, core, matchedSteps;
        private bool telegraph, mirrorOpen, mirrorCompleted, techniqueCompleted;
        private string armedSkill, matchingUse;
        private struct GuardEcho { public float at, power; }
        private readonly Queue<GuardEcho> guardEchoes = new Queue<GuardEcho>();
        private FinalBattleAttempt State => session.Finale.State;
        internal CasselSceneSequence Scenes { get; private set; }
        private bool clashWindow;
        private bool BasicMastered => session.Progression.Record.Get(SkillRules.BasicSword).stage == 3;
        private void ObserveMastery(ProgressionEvent e)
        {
            if (State.active && e.skillId == SkillRules.BasicSword && e.kind == "valid_use" && e.before == 399 && e.after == 400)
            {
                Scenes.Signal("awaken");
                if (Phase == 3) Scenes.Signal("clashReady");
            }
        }
        private void ObserveHealth(Vector2 source)
        {
            if (Phase == 1 && motor.Health.Current <= 150) Scenes?.Signal("hp150");
        }
        public int Phase => State.phase;
        public bool Finished => !State.active;
        public bool CanChooseReturn => State.active && Phase == 2 && MirrorOpen && !player.Health.Dead;
        public bool MirrorOpen => mirrorOpen && (State.returnCount == 0 || Time.time < exposureUntil);
        public string ArmedSkill => armedSkill;
        public string Status => Loc.Get("finale.boss_status", Phase, State.returnCount, State.ReleasedPercent, State.OutputMultiplier.ToString("0.0", Loc.Culture), (Finished ? Loc.Get("finale.result", (FinaleEndingEvaluator.Evaluate(State) == FinaleEnding.True ? Loc.Get("finale.ending.true")
                : FinaleEndingEvaluator.Evaluate(State) == FinaleEnding.Basic ? Loc.Get("finale.ending.basic") : Loc.Get("finale.ending.defeat")))
            : Phase == 1 ? Loc.Get("finale.original_sword", (pattern?.Label ?? Loc.Get("finale.approaching")))
            : Phase == 2 ? (MirrorOpen ? Loc.Get("finale.mirror_open") : Loc.Get("finale.mirror_closed"))
                + (armedSkill == null ? "" : Loc.Get("finale.selected_finisher", session.Progression.Catalog.Get(armedSkill).DisplayName))
            : Loc.Get("finale.core_hint", core, data.coreIntegrity)));
        public void Initialize(CombatMotor2D target, SkillSession skills)
        {
            motor = GetComponent<CombatMotor2D>(); player = target; session = skills; data = CasselDefinition.Load();
            motor.Health.Initialize(data.health, 0.12f);
            session.BasicParried += Parried;
            session.TechniqueCompleted += TechniqueCompleted;
            session.Finale.Returned += Returned;
            player.SwordContact += SwordContact;
            player.Health.Died += PlayerDefeated;
            if (!skills.Progression.Record.testData)
            {
                Scenes = new CasselSceneSequence(skills);
                skills.Progression.Event += ObserveMastery;
                motor.Health.Damaged += ObserveHealth;
            }
            ResetBattle();
        }
        private void PlayerDefeated()
        {
            pattern = null; telegraph = false; motionUntil = 0; guardEchoes.Clear();
            session.Finale.Defeat();
        }
        public void ResetBattle()
        {
            bool defeated = State.outcome == "defeated";
            session.Finale.BeginAttempt();
            clashWindow = false;
            Scenes?.Begin(defeated);
            pattern = null; cursor = beat = matchedSteps = 0; timer = 1.1f;
            guard = parryUntil = exposureUntil = staggerUntil = motionUntil = backstepUntil = 0;
            matchingUse = armedSkill = null; mirrorOpen = telegraph = mirrorCompleted = techniqueCompleted = false;
            guardEchoes.Clear(); core = data.coreIntegrity;
            motor.Health.MinimumHealth = data.mirrorHealth;
            motor.ResetAt(new Vector2(data.bossX, 0.15f)); motor.Face(-1);
            player.ResetAt(new Vector2(data.playerX, 0.15f));
            AudioManager.SetMusic(AudioManager.Music.Boss);
        }
        public bool ArmReturn(string skillId)
        {
            var skill = session.Progression.Record.Get(skillId);
            if (!CanChooseReturn || !State.CanUse(skillId) || skillId == SkillRules.BasicSword
                || skill == null || !skill.acquired || skill.stage != 3 || session.PreviewStage != 0) return false;
            Scenes?.Signal("armed");
            armedSkill = skillId; mirrorCompleted = techniqueCompleted = false;
            // The selected original is committed before either side's matching completion is accepted.
            pattern = System.Array.Find(data.patterns, p => p.skillId == skillId);
            motor.Face(player.Body.position.x - motor.Body.position.x);
            beat = 0; telegraph = true; timer = pattern.windup; motionUntil = 0;
            SkillStroke.Show(motor.transform.position + Vector3.up,
                motor.transform.position + new Vector3(motor.Facing * pattern.reach, 1, 0), Color.cyan, pattern.windup);
            return true;
        }
        private void TechniqueCompleted(UseContext use)
        {
            if (use == null || use.SkillId != armedSkill || !CanChooseReturn || session.PreviewStage != 0
                || Vector2.Distance(player.Body.position, motor.Body.position) > data.matchDistance) return;
            // Natural completion of a real effective stage-3 activation; interruption/preview never reaches here.
            techniqueCompleted = true; TryReserve();
        }
        private void TryReserve()
        {
            if (techniqueCompleted && mirrorCompleted && armedSkill != null && CanChooseReturn
                && Vector2.Distance(player.Body.position, motor.Body.position) <= data.matchDistance)
                session.Finale.Reserve(armedSkill);
        }
        private void Returned(string id)
        {
            if (Scenes != null)
            {
                Scenes.Returned(id, State.returnCount);
                if (Phase == 3)
                {
                    Scenes.Signal("phase3");
                    if (BasicMastered) Scenes.Signal("clashReady");
                }
            }
            // Fade first so executor interruption cannot immediately remove its surfaces.
            if (id == SkillRules.SilverStitch)
                foreach (var surface in StitchSurface2D.All) surface.BeginReturnFade();
            if (id == BenefactorIds.Canvas)
                foreach (var surface in PaintSurface2D.All.ToArray()) surface.BeginReturnFade();
            player.BenefactorSkills.Revoke(id);
            motor.GetComponent<StitchBinding>().Break();
            SkillProjectile2D.ClearAll(); guardEchoes.Clear();
            armedSkill = null; mirrorOpen = mirrorCompleted = techniqueCompleted = false; pattern = null; telegraph = false; beat = 0;
            // The new closed guard requires a parry after this return.
            parryUntil = float.NegativeInfinity;
            guard = 0; timer = 1f; motionUntil = 0;
            // A visible backstep; no teleport and no requirement for the returned mobility skill.
            motor.Face(player.Body.position.x - motor.Body.position.x);
            backstepUntil = Time.time + 0.25f;
            motor.Body.linearVelocity = new Vector2(-motor.Facing * 5f, 0);
            SkillStroke.Show(motor.transform.position + Vector3.up * 2,
                player.transform.position + Vector3.up, new Color(0.2f, 1f, 0.9f), 0.5f);
            AudioManager.Play(AudioManager.Sfx.Parry);
        }
        private void Parried()
        {
            if (!State.active) return;
            if (State.returnCount == 1 && !MirrorOpen) Scenes?.Signal("parry1");
            clashWindow = Phase == 3 && BasicMastered;
            var profile = data.Profile(State.returnCount);
            parryUntil = Time.time + data.parryOpening + profile.parryBonus;
            staggerUntil = Time.time + 0.35f + profile.parryBonus;
            pattern = null; telegraph = false; motionUntil = 0; timer = 0.6f;
            matchingUse = null; matchedSteps = 0;
        }
        private void SwordContact(UseContext use, int step, bool last, int damage)
        {
            if (!State.active || use == null || use.Preview || player.Health.Dead) return;
            var profile = data.Profile(State.returnCount);
            if (Phase == 2 && !MirrorOpen && Time.time <= parryUntil)
            {
                AddGuard(damage * profile.guardPower);
                if (profile.echoDelay > 0)
                { guardEchoes.Enqueue(new GuardEcho { at = Time.time + profile.echoDelay, power = damage * profile.guardPower }); }
            }
            if (profile.staggerSeconds > 0 && step == 1
                && (Time.time <= player.Defense.State.DashReadyAt || Time.time - player.LastLandedAt <= 0.35f))
            {
                staggerUntil = Mathf.Max(staggerUntil, Time.time + profile.staggerSeconds);
                if (telegraph && beat == 0) { pattern = null; telegraph = false; motionUntil = 0; timer = 0.6f; }
            }
            if (Phase != 3) return;
            core = Mathf.Max(0, core - damage);
            int basic = session.Progression.Record.Get(SkillRules.BasicSword).stage;
            if (basic < 3 && core == 0) { Finish(false, true); return; }
            if (basic == 3 && core == 0 && !(last && matchedSteps == 3 && matchingUse == use.Id && Time.time <= parryUntil))
                Scenes?.Signal("clashMiss");
            if (Time.time > parryUntil || use.Stage != 3) { matchedSteps = 0; return; }
            if (step == 1) { matchingUse = use.Id; matchedSteps = 1; }
            else if (matchingUse == use.Id && step == matchedSteps + 1) matchedSteps++;
            else matchedSteps = 0;
            if (basic == 3 && last && matchedSteps == 4) Finish(true, false);
        }
        private void AddGuard(float power)
        {
            if (Phase != 2 || MirrorOpen) return;
            guard += power;
            if (guard < data.guardThreshold) return;
            Scenes?.Signal("guard" + State.returnCount);
            guard = 0; mirrorOpen = true; exposureUntil = Time.time + data.exposureSeconds;
            SkillStroke.Show(motor.transform.position, motor.transform.position + Vector3.up * 3, Color.cyan, 0.6f);
        }
        private void Finish(bool clash, bool destroyed)
        {
            if (!session.Finale.Settle(clash, destroyed)) return;
            pattern = null; motionUntil = 0; motor.Move(0); SkillProjectile2D.ClearAll();
            AudioManager.Play(AudioManager.Sfx.BossDefeat);
        }
        private void FixedUpdate()
        {
            if (!GameInput.GameplayEnabled || session == null || !State.active || player.Health.Dead) return;
            if (Scenes?.Modal != null) return;
            if (clashWindow && Time.time > parryUntil)
            {
                clashWindow = false;
                Scenes?.Signal("clashMiss");
            }
            if (motor.Temporal.Frozen || motor.Stunned || motor.GetComponent<StitchBinding>().Bound) { motor.Move(0); return; }
            motor.RefreshGround();
            if (Phase == 1 && motor.Health.Current <= data.mirrorHealth)
            {
                session.Finale.EnterMirrorPhase(); Scenes?.Signal("mirror"); motor.Health.MinimumHealth = 1;
                mirrorOpen = true; pattern = null; telegraph = false; timer = 1f; motionUntil = 0;
            }
            if (Phase == 2 && mirrorOpen && !MirrorOpen) { mirrorOpen = false; armedSkill = null; }
            while (guardEchoes.Count > 0 && guardEchoes.Peek().at <= Time.time)
            { var echo = guardEchoes.Dequeue(); AddGuard(echo.power); SkillStroke.Afterimage(player.Animation.GetComponent<SpriteRenderer>(), 0.12f); }
            if (Time.time < staggerUntil) { motor.Move(0); return; }
            var point = motor.Body.position;
            point.x = Mathf.Clamp(point.x, data.arenaMin + 1, data.arenaMax - 1);
            if (point.y < -2) point.y = 0.15f;
            motor.Body.position = point;
            if (Time.time < backstepUntil) { motor.Move(-motor.Facing * 5f); return; }
            if (Time.time < motionUntil && pattern != null) motor.Move(motor.Facing * pattern.dashSpeed);
            else motor.Move(0);
            timer -= Time.fixedDeltaTime;
            if (timer > 0) return;
            if (pattern == null)
            {
                var sequence = data.Sequence(Phase);
                for (int i = 0; i < sequence.Length; i++)
                {
                    var candidate = data.Pattern(sequence[cursor++ % sequence.Length]);
                    if (data.Enabled(candidate, State)) { pattern = candidate; break; }
                }
                if (pattern == null) return; // Definition validation guarantees the zero-form fallback.
                float distance = Mathf.Abs(player.Body.position.x - motor.Body.position.x);
                motor.Face(player.Body.position.x - motor.Body.position.x);
                if (distance > pattern.reach && pattern.shape != "bell" && pattern.shape != "petals")
                { motor.Move(motor.Facing * 4f); pattern = null; timer = 0; return; }
                beat = 0; telegraph = true; timer = pattern.windup;
                SkillStroke.Show(motor.transform.position + Vector3.up,
                    motor.transform.position + new Vector3(motor.Facing * pattern.reach, 1, 0), Color.yellow, pattern.windup);
                AudioManager.Play(AudioManager.Sfx.BossWarning); return;
            }
            if (telegraph)
            {
                var committed = pattern;
                Strike();
                if (pattern != committed || !State.active) return;
                beat++;
                int total = pattern.shape == "sword" && Phase == 2 ? data.Profile(State.returnCount).directBeats : pattern.beats;
                if (beat < total) timer = pattern.interval;
                else
                {
                    if (pattern.skillId == armedSkill) mirrorCompleted = true;
                    telegraph = false; timer = pattern.recovery;
                    TryReserve();
                }
                return;
            }
            pattern = null; timer = 0;
        }
        private void Strike()
        {
            motionUntil = Time.time + 0.16f;
            Vector2 at = motor.Body.position + Vector2.up;
            var area = new Bounds(at + Vector2.right * motor.Facing * pattern.reach * 0.5f,
                new Vector3(pattern.reach, pattern.height, 2));
            if (pattern.shape == "bell") area = new Bounds(new Vector3((data.arenaMin + data.arenaMax) * 0.5f, 0.25f), new Vector3(pattern.reach, pattern.height, 2));
            if (pattern.shape == "thread")
                area = new Bounds(new Vector3(beat == 0 ? data.arenaMin + 2 : data.arenaMax - 2, 1), new Vector3(3, pattern.height, 2));
            if (pattern.shape == "petals")
            {
                var go = new GameObject("Cassel reflected petal"); go.transform.SetParent(SkillStroke.WorldRoot, false);
                go.transform.position = at + Vector2.up * 2;
                var shot = go.AddComponent<SkillProjectile2D>(); shot.Initialize(player, motor);
                Vector2 direction = ((Vector2)player.transform.position + Vector2.up - (Vector2)go.transform.position).normalized;
                shot.ConfigureTrialShot(direction * 6f, pattern.damage, 3); return;
            }
            // All patterns share the real dash/parry/counter receiver. No player input reading or mastery-scaled density.
            SkillStroke.Show(area.min, area.max, pattern.shape == "sword" ? Color.white : Color.cyan, 0.18f);
            if (area.Intersects(player.GetComponent<Collider2D>().bounds)) player.Health.Hit(pattern.damage, motor.transform.position);
            AudioManager.Play(AudioManager.Sfx.BossAttack);
        }
        private void OnDestroy()
        {
            if (session != null)
            { session.BasicParried -= Parried; session.TechniqueCompleted -= TechniqueCompleted; session.Finale.Returned -= Returned; }
            if (Scenes != null)
            {
                session.Progression.Event -= ObserveMastery;
                motor.Health.Damaged -= ObserveHealth;
            }
            if (player != null) { player.SwordContact -= SwordContact; player.Health.Died -= PlayerDefeated; }
        }
    }
}
