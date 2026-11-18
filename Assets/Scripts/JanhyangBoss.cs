using UnityEngine;

namespace RewindNotary
{
    [RequireComponent(typeof(CombatMotor2D))]
    public sealed class JanhyangBoss : MonoBehaviour
    {
        public bool Active { get; private set; }
        public bool Telegraphing => windup > 0f;
        public int Phase => phaseIndex + 1;
        public string FocusSkill => focus;
        public bool CounterOpening => counterSpent && cooldown > 0;
        internal int LastCounterPhase { get; private set; }
        public string PatternLabel => definition == null ? "" : definition.phases[phaseIndex].Name
            + " · " + (string.IsNullOrEmpty(focus) ? Loc.Get("echo.observing") : Loc.Get("echo.mimicking", skills.Progression.Catalog.Get(focus).DisplayName))
            + (CounterOpening ? Loc.Get("echo.opening_suffix") : Loc.Get("echo.switch_hint_suffix"));
        private CombatMotor2D motor, player;
        private SkillSession skills;
        private JanhyangDefinition definition;
        private JanhyangMemory memory;
        private JanhyangMimic mimic;
        private JanhyangMemory.Choice[] choices;
        private SkillStageDefinition shape;
        private int phaseIndex, patternIndex;
        private float windup, cooldown, jumpTimer;
        private Vector2 firstAim, aim;
        private string focus;
        private bool linking, counterSpent;
        private StitchBinding stitchBinding;
        private AudioManager.VoiceHandle warningVoice;
        private JanhyangPhase CurrentPhase => definition.phases[phaseIndex];
        private void Awake() { motor = GetComponent<CombatMotor2D>(); }
        public void Initialize(CombatMotor2D target)
        {
            player = target; skills = target.Skills;
            definition = JanhyangDefinition.Load(skills.Progression.Catalog);
            memory = new JanhyangMemory(definition);
            mimic = new JanhyangMimic(motor, player, definition);
            // Subscribed before the approach/practice fight: consumes exactly the JSONL source events.
            skills.Progression.Event += ObserveUse;
            stitchBinding = GetComponent<StitchBinding>();
            motor.Health.Damaged += OnDamaged;
            motor.Health.Died += OnDefeated;
            player.Health.Died += OnPlayerDefeated;
        }
        private void ObserveUse(ProgressionEvent result)
        {
            if (!memory.Observe(result, Time.time)) return;
            if (!JanhyangMemory.RotationCounter(result, focus, gameObject.name,
                Active && !motor.Health.Dead && !counterSpent && (Telegraphing || mimic.Active || cooldown > 0), mimic.DeliveringContact)) return;
            // Damaged fires BEFORE valid_use. Keep the advertised focus through hit recovery
            // so that this decision uses the old snapshot, never the event just appended above.
            CancelAction();
            counterSpent = true;
            LastCounterPhase = Phase;
            cooldown = Mathf.Max(cooldown, CurrentPhase.recovery + definition.counterRecovery);
        }
        public void Activate()
        {
            if (Active || motor.Health.Dead) return;
            Active = true; cooldown = definition.initialCooldown; jumpTimer = definition.initialJump;
            phaseIndex = definition.PhaseFor(motor.Health.Current, motor.Health.Maximum);
            AudioManager.SetMusic(AudioManager.Music.Boss);
        }
        public void ResetPattern()
        {
            CancelAction(); Active = false; phaseIndex = 0; focus = null; choices = null; counterSpent = false;
            LastCounterPhase = 0;
            cooldown = definition != null ? definition.initialCooldown : 0.7f;
            jumpTimer = definition != null ? definition.initialJump : 1.8f;
            // Recent effective uses survive retry; HP, plans and pending contacts do not.
        }
        private void CancelWarning() { windup = 0f; AudioManager.Stop(warningVoice); warningVoice = default; }
        private void CancelAction() { CancelWarning(); mimic?.Cancel(); linking = false; }
        private void OnDamaged(Vector2 source)
        {
            // Ordinary hits still apply the motor's unchanged HP/knockback/0.18s stun.
            // A committed imitation pauses through recoil, then resumes. Only rotation
            // (or phase/death/reset) discards its remaining contacts and linked techniques.
            if (!Telegraphing && !mimic.Active) cooldown = Mathf.Max(cooldown, CurrentPhase.recovery);
        }
        private void OnPlayerDefeated() { ResetPattern(); }
        private void OnDefeated()
        {
            CancelAction(); focus = null;
            AudioManager.Play(AudioManager.Sfx.BossDefeat);
            ImpactFeedback.BossDefeated();
            AudioManager.SetMusic(AudioManager.Music.Field);
        }
        private void StartTelegraph()
        {
            var choice = choices[CurrentPhase.pattern[patternIndex] % choices.Length];
            focus = choice.SkillId; counterSpent = false;
            // Preview changes shape only; rejected preview uses never enter the history or counter.
            int recordedStage = skills.PreviewStage != 0 ? skills.PreviewStage : choice.Stage;
            int stage = Mathf.Min(recordedStage, CurrentPhase.stageCap);
            shape = skills.Progression.Catalog.Get(focus).Profile(Mathf.Clamp(stage, 1, 3));
            firstAim = aim = player.Body.position;
            motor.Face(aim.x - motor.Body.position.x);
            windup = CurrentPhase.windup; motor.Move(0);
            mimic.Telegraph(focus, shape, aim);
            warningVoice = AudioManager.Play(AudioManager.Sfx.BossWarning);
        }
        private void FixedUpdate()
        {
            if (!GameInput.GameplayEnabled) return;
            if (motor.Health == null || motor.Health.Dead) return;
            if (motor.Temporal != null && motor.Temporal.Frozen) { mimic?.Suspend(); return; }
            motor.RefreshGround();
            if (!Active || player == null || player.Health.Dead) { motor.Move(0); return; }
            int nextPhase = definition.PhaseFor(motor.Health.Current, motor.Health.Maximum);
            if (nextPhase > phaseIndex)
            {
                CancelAction(); phaseIndex = nextPhase;
                cooldown = Mathf.Max(cooldown, definition.transitionRecovery);
            }
            // Delay the next joint action without pulling/teleporting the target or changing tuning.
            if (stitchBinding != null && stitchBinding.Bound) { mimic.Suspend(); motor.Move(0); return; }
            jumpTimer -= Time.fixedDeltaTime;
            if (motor.Stunned) { mimic.Suspend(); return; }
            if (motor.Attacking) { motor.Move(0); return; }
            Vector2 delta = player.transform.position - transform.position;
            if (windup > 0f)
            {
                motor.Move(0);
                // Tracking stops BEFORE contact; all phases expose a final 0.2s locked direction.
                if (windup > definition.aimLockSeconds)
                {
                    aim = Vector2.Lerp(firstAim, player.Body.position, CurrentPhase.accuracy);
                    int previousFacing = motor.Facing;
                    motor.Face(aim.x - motor.Body.position.x);
                    if (previousFacing != motor.Facing) mimic.Telegraph(focus, shape, aim);
                }
                windup -= Time.fixedDeltaTime;
                if (windup <= 0f)
                {
                    CancelWarning();
                    mimic.Begin(focus, shape);
                    AudioManager.Play(AudioManager.Sfx.BossAttack);
                }
                return;
            }
            if (mimic.Active)
            {
                if (mimic.Tick(Time.fixedDeltaTime))
                {
                    patternIndex++;
                    linking = patternIndex < CurrentPhase.pattern.Length;
                    cooldown = linking ? CurrentPhase.linkGap : CurrentPhase.recovery;
                }
                return;
            }
            if (cooldown > 0f)
            {
                cooldown -= Time.fixedDeltaTime; motor.Move(0);
                if (cooldown <= 0 && !linking) { focus = null; counterSpent = false; mimic.Cancel(); }
                return;
            }
            if (linking) { StartTelegraph(); return; }
            // Re-rank only between complete patterns. New habits cannot rewrite an existing tell.
            var observed = memory.Select(Time.time, CurrentPhase.targets);
            float reach = observed[0].SkillId == SkillRules.BasicSword ? 1.55f
                : skills.Progression.Catalog.Get(observed[0].SkillId).Profile(1).reach;
            motor.Face(delta.x);
            if (Mathf.Abs(delta.x) < reach && Mathf.Abs(delta.y) < 1.15f)
            {
                choices = observed; patternIndex = 0;
                StartTelegraph(); return;
            }
            float move = Mathf.Abs(delta.x) > 1.25f ? Mathf.Sign(delta.x) * definition.moveSpeed : 0f;
            if ((transform.position.x < definition.arenaMin && move < 0f) || (transform.position.x > definition.arenaMax && move > 0f)) move = 0f;
            motor.Move(move);
            if (motor.Grounded && jumpTimer <= 0f && (Mathf.Abs(delta.x) > 2f || delta.y > 0.7f))
            { motor.Jump(definition.jumpSpeed); jumpTimer = definition.jumpInterval; }
        }
        private void LateUpdate()
        {
            if (motor.Animation != null)
            {
                motor.Animation.SetTelegraph(Telegraphing && !motor.Health.Dead);
                if (!motor.Health.Dead) mimic?.Animate();
            }
        }
        private void OnDisable() { ResetPattern(); }
        private void OnDestroy()
        {
            CancelWarning(); mimic?.Dispose();
            if (skills != null && skills.Progression != null) skills.Progression.Event -= ObserveUse;
            if (motor != null && motor.Health != null)
            { motor.Health.Damaged -= OnDamaged; motor.Health.Died -= OnDefeated; }
            if (player != null && player.Health != null) player.Health.Died -= OnPlayerDefeated;
        }
    }
}
