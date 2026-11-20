using UnityEngine;

namespace RewindNotary
{
    // Feet are the transform origin; the physics body extends upwards.
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(Health))]
    [DefaultExecutionOrder(-50)] // Finish/advance attacks before this frame's player input.
    public sealed partial class CombatMotor2D : MonoBehaviour
    {
        internal const float ReaVisualScale = .76f;
        public Rigidbody2D Body { get; private set; }
        public Health Health { get; private set; }
        public SpriteAnimator Animation { get; private set; }
        public int Facing { get; private set; } = 1;
        public bool Grounded { get; private set; }
        private float carrierSpeed;
        public bool Attacking { get; private set; }
        public float LastLandedAt { get; private set; } = float.NegativeInfinity;
        public bool Stunned => Time.time < stunUntil;
        public int ComboStep => comboStep;
        public int AttackDirectionY { get; private set; }
        public SkillSession Skills { get; set; }
        public PlayerDefense2D Defense { get; private set; }
        public BenefactorSkillRunner BenefactorSkills { get; set; }
        public TemporalActor2D Temporal { get; set; }
        public event System.Action<Bounds, int> BasicStrikeDelivered;
        public event System.Action<UseContext, int, bool, int> SwordContact;
        public event System.Action Landed;
        public event System.Action AttackStarted;
        private float attackStartedAt, comboExpiresAt, stunUntil, landUntil, lastHitAt;
        private bool strikeDelivered, groundSampled, queuedAttack;
        private int comboStep, attackStep, airStrikesUsed;
        private bool aerialSwing, holdingAir;
        private float airGravity;
        private int activeStarter = -1;
        private Health opponent;
        private int damage, strikeDamage;
        private UseContext chainUse, activeUse;
        private SkillStageDefinition chainProfile;
        internal const float RisingLaunchSpeed = 14.2f, JuggleSeconds = .53f;
        private static int ComboLimit => PlayerCombo.Length;
        public const float AttackDuration = 0.32f;
        public const float ComboGraceSeconds = 0.18f;
        public const float AirAcceleration = 45f, AirDrag = 6f;
        public const float AirBrake = 28f;

        // All times are scaled game seconds. A single key-down can reserve only the next step.
        private readonly struct AttackSpec
        {
            public readonly float Duration, StrikeTime, BufferOpen, Reach, Width, Height, Knockback;
            public readonly int DamageMultiplier;
            public AttackSpec(float duration, float strikeTime, float bufferOpen, float reach,
                float width, float height, int damageMultiplier, float knockback)
            {
                Duration = duration; StrikeTime = strikeTime; BufferOpen = bufferOpen;
                Reach = reach; Width = width; Height = height;
                DamageMultiplier = damageMultiplier; Knockback = knockback;
            }
        }
        private static readonly AttackSpec[] PlayerCombo =
        {
            new AttackSpec(0.40f, 0.16f, 0.08f, 0.90f, 1.70f, 1.55f, 1, 1f),
            new AttackSpec(0.36f, 0.14f, 0.07f, 1.00f, 1.90f, 1.45f, 2, 1f),
            new AttackSpec(0.44f, 0.22f, 0.10f, 1.10f, 2.10f, 1.75f, 3, 1f),
            new AttackSpec(0.56f, 0.28f, 0.14f, 1.20f, 2.40f, 1.90f, 4, 1.7f),
        };
        private static readonly AttackSpec BossAttack =
            new AttackSpec(AttackDuration, 0.16f, AttackDuration, 0.9f, 1.7f, 1.55f, 1, 1f);
        private static readonly (AttackSpec swing, float centerY, int art)[] StarterAttacks =
        {
            (new AttackSpec(.34f, .14f, 0f, 1.00f, 1.90f, 1.10f, 2, .8f), 1.05f, 3),
            (new AttackSpec(.41f, .14f, 0f, .72f, 1.66f, 2.38f, 2, .55f), 1.46f, 2),
            (new AttackSpec(.49f, .21f, 0f, 0f, 3.65f, 1.45f, 2, 1.55f), .95f, 4),
            (new AttackSpec(.48f, .20f, 0f, 0f, 0f, .95f, 2, .65f), 1.08f, 4)
        };
        private AttackSpec activeAttack;
        private CapsuleCollider2D capsule;
        private readonly Collider2D[] groundHits = new Collider2D[4];

        public void Initialize(string sheet, int hp, float immunity, int hitDamage, Color tint, bool isPlayer = false)
        {
            Body = GetComponent<Rigidbody2D>();
            Body.gravityScale = 3f;
            Body.freezeRotation = true;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            capsule = GetComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.62f, isPlayer ? 1.66f : 1.9f);
            capsule.offset = Vector2.up * (capsule.size.y * .5f);
            capsule.sharedMaterial = new PhysicsMaterial2D("No wall friction") { friction = 0f, bounciness = 0f };
            Health = GetComponent<Health>();
            Health.Initialize(hp, immunity, isPlayer);
            Health.Damaged += ReceiveHit;
            Health.Died += Die;
            damage = hitDamage;
            var visual = new GameObject("Sprite");
            visual.transform.SetParent(transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 10;
            Animation = visual.AddComponent<SpriteAnimator>();
            Animation.Initialize(sheet, isPlayer ? ReaVisualScale : 1f);
            Animation.Tint(tint);
            if (isPlayer)
            {
                Defense = gameObject.AddComponent<PlayerDefense2D>();
                Defense.Initialize(this);
            }
        }
        public void SetOpponent(Health target) { opponent = target; }
        public void Face(float direction)
        {
            if (Mathf.Abs(direction) < 0.01f) return;
            Facing = direction < 0 ? -1 : 1;
            Animation.Face(Facing);
        }
        public void RefreshGround()
        {
            var filter = new ContactFilter2D();
            filter.SetLayerMask(1 << LevelBuilder.GroundLayer);
            filter.useTriggers = false;
            bool wasGrounded = Grounded;
            bool needsLandingContact = Defense != null && !wasGrounded &&
                (Defense.State.Phase == DefensePhase.Dash || Defense.State.AirDashUsed);
            int hits = Physics2D.OverlapBox(
                Body.position + new Vector2(0, 0.025f), new Vector2(0.48f, 0.12f),
                0f, filter, groundHits);
            Grounded = false;
            carrierSpeed = 0;
            for (int i = 0; i < hits; i++)
            {
                var floor = groundHits[i];
                if (floor.TryGetComponent<AuthoredSurface>(out var surface) && surface.RampNormal != Vector2.zero)
                {
                    if (!surface.SupportsRamp(this, capsule, floor)) continue;
                    Grounded = true;
                    break;
                }
                var carrier = floor.attachedRigidbody == null ? null : floor.GetComponent<StoryMovingSurface>();
                if (Body.linearVelocity.y > (carrier == null ? 0f : carrier.StepVelocity.y) + .1f) continue;
                // Touching a wall while close to a different floor must not recharge a dash.
                if ((needsLandingContact || floor.TryGetComponent<AuthoredSurface>(out _)) &&
                    (Mathf.Abs(Body.position.y - floor.bounds.max.y) > .09f ||
                     !floor.IsTouching(capsule))) continue;
                if (carrier != null) carrierSpeed = carrier.StepVelocity.x;
                Grounded = true;
                break;
            }
            if (Grounded && !wasGrounded)
            {
                airStrikesUsed = 0;
                ReleaseAirSwing();
                Defense?.State.Land();
                landUntil = Time.time + 0.09f;
                LastLandedAt = Time.time;
                // Finish the current swing, but never carry an aerial chain/buffer onto the floor.
                if (groundSampled && Health.IsPlayer) ResetCombo();
                if (groundSampled && !Health.Dead) Landed?.Invoke();
            }
            if (!Grounded) Defense?.State.ObserveAirborne();
            groundSampled = true;
        }
        public void Move(float speed)
        {
            if (Health.Dead || Stunned) return;
            if (Temporal != null && Temporal.Frozen) return;
            if (Defense != null && Defense.DriveMovement()) return;
            if (BenefactorSkills != null && BenefactorSkills.DriveMovement()) return;
            if (Attacking && activeStarter == QuickSkills.Thrust && !strikeDelivered)
            {
                Body.linearVelocity = new Vector2(starterFacing * 11f + carrierSpeed, Body.linearVelocity.y);
                return;
            }
            if (Health.IsPlayer && !Grounded)
            {
                // Steering stays responsive during a swing. Only a released stick uses drag.
                float releaseDrag = Attacking ? AirDrag : AirBrake;
                float acceleration = Mathf.Abs(speed) > 0.01f ? AirAcceleration : releaseDrag;
                float horizontal = Mathf.MoveTowards(Body.linearVelocity.x, speed, acceleration * Time.fixedDeltaTime);
                Body.linearVelocity = new Vector2(horizontal, Body.linearVelocity.y);
                if (!Attacking || activeStarter < 0) Face(speed);
                return;
            }
            float attackMove = speed;
            if (!Health.IsPlayer || Mathf.Abs(speed) <= 0.01f)
                attackMove = chainProfile != null && Time.time - attackStartedAt < chainProfile.stepSeconds
                    ? Facing * chainProfile.stepSpeed : 0f;
            Body.linearVelocity = new Vector2(Attacking ? attackMove : speed, Body.linearVelocity.y) + Vector2.right * carrierSpeed;
            if (!Attacking || (Health.IsPlayer && activeStarter < 0)) Face(speed);
        }
        public void Jump(float velocity)
        {
            if (Health.Dead || Stunned) return;
            if (Defense != null && Defense.BlocksActions) return;
            Body.linearVelocity = new Vector2(Body.linearVelocity.x, velocity);
            Grounded = false;
            landUntil = 0f;
        }
        private bool CanSwing => !Health.Dead && !Stunned && Temporal?.Frozen != true &&
            Defense?.BlocksActions != true && BenefactorSkills?.BlocksActions != true;

        public bool Attack()
        {
            if (!CanSwing) return false;
            if (Attacking)
            {
                if (activeStarter == QuickSkills.Rising && !Grounded && strikeDelivered &&
                    airStrikesUsed < ComboLimit && Time.time - attackStartedAt >= activeAttack.StrikeTime + .10f)
                {
                    CancelAttack();
                    return BeginNextPlayerSwing();
                }
                if (!Health.IsPlayer || comboStep == 0 || queuedAttack ||
                    (!Grounded && airStrikesUsed >= ComboLimit)) return false;
                float elapsed = Time.time - attackStartedAt;
                if (elapsed < activeAttack.BufferOpen || elapsed >= activeAttack.Duration) return false;
                queuedAttack = true;
                return true; // Accepted input, not a swing/audio event yet.
            }
            if (!Health.IsPlayer) { BeginAttack(0); return true; }
            return BeginNextPlayerSwing();
        }

        internal bool UseStarterSkill(int index, int direction = 0, bool command = false)
        {
            if (!CanSwing) return false;
            if (Attacking && (!strikeDelivered || (activeStarter >= 0
                ? Time.time - attackStartedAt < activeAttack.StrikeTime + .085f
                : !command || !swingConnected))) return false;
            if (Attacking) CancelAttack();
            ResetCombo();
            activeUse = chainUse = Skills.Progression.BeginUse(SkillRules.BasicSword, Skills.PreviewStage);
            chainProfile = null;
            activeStarter = index;
            aerialSwing = false;
            float steering = direction != 0 ? direction : GameInput.Horizontal;
            starterFacing = steering == 0f ? Facing : (int)Mathf.Sign(steering);
            Face(starterFacing);
            attackStep = 0;
            AttackDirectionY = 0;
            var starter = StarterAttacks[index];
            StartSwing(starter.swing, starter.art, false);
            return true;
        }

        private bool BeginNextPlayerSwing(bool fromBuffer = false)
        {
            bool airborne = !Grounded;
            if (airborne && airStrikesUsed >= ComboLimit) return false;
            if (comboStep >= ComboLimit || (comboStep > 0 && aerialSwing != airborne) ||
                (!fromBuffer && Time.time > comboExpiresAt))
                ResetCombo();
            BeginAttack(airborne ? ++airStrikesUsed : comboStep + 1);
            return true;
        }

        private void BeginAttack(int step)
        {
            if (step > 0 && comboStep == 0 && Skills != null)
            {
                chainUse = Skills.Progression.BeginUse(SkillRules.BasicSword, Skills.PreviewStage);
                chainProfile = Skills.Progression.Catalog.Get(SkillRules.BasicSword).Profile(chainUse.Stage);
            }
            activeUse = chainUse;
            attackStep = comboStep = step;
            activeStarter = -1;
            aerialSwing = step > 0 && !Grounded;
            AttackDirectionY = 0;
            if (Health.IsPlayer && step > 0 && AutoTestRuntime.Report == null)
            {
                float vy = GameInput.Vertical;
                if (!Grounded && vy < -0.5f) AttackDirectionY = -1;
                else if (vy > 0.5f) AttackDirectionY = 1;
            }
            if (aerialSwing && AttackDirectionY >= 0) HoldAirSwing();
            else ReleaseAirSwing();
            bool moving = Mathf.Abs(Body.linearVelocity.x - carrierSpeed) >= PlayerController2D.MoveSpeed * .5f;
            StartSwing(step == 0 ? BossAttack : PlayerCombo[step - 1], step, moving);
        }

        private void StartSwing(AttackSpec swing, int art, bool moving)
        {
            activeAttack = swing;
            // Consume once at an actual swing start, including a later whiff/interruption.
            float counter = Defense != null ? Defense.State.ConsumeCounter(Time.time) : 1f;
            strikeDamage = Mathf.CeilToInt(damage * activeAttack.DamageMultiplier * counter);
            strikeDamage = FinaleCombat.Scale(Skills, strikeDamage);
            attackStartedAt = Time.time;
            Attacking = true;
            strikeDelivered = false;
            swingConnected = false;
            queuedAttack = false;
            AimPlayerAttack();
            Animation.BeginAttack(art, Grounded, moving, activeStarter);
            AttackStarted?.Invoke();
        }

        private void AimPlayerAttack()
        {
            if (activeStarter < 0 && Health.IsPlayer && GameInput.CanPlay && TryGetComponent<PlayerController2D>(out var controller))
                Face(controller.AttackSteering);
        }
        private void ResetCombo()
        {
            // Landing closes the chain, but the in-flight swing still owns its context until recovery.
            if (!Attacking) Skills?.Progression.EndUse(chainUse ?? activeUse);
            chainUse = null;
            comboStep = 0;
            queuedAttack = false;
            comboExpiresAt = float.NegativeInfinity;
        }
        private void CancelAttack()
        {
            ReleaseAirSwing();
            Skills?.Progression.EndUse(activeUse);
            activeUse = null;
            Attacking = false;
            ResetCombo();
        }
        internal void ClearComboForDefense() { CancelAttack(); }

        private void HoldAirSwing()
        {
            if (!holdingAir)
            {
                airGravity = Body.gravityScale;
                Body.gravityScale = 0f;
                holdingAir = true;
            }
            Body.linearVelocity = new Vector2(Body.linearVelocity.x, 0f);
        }

        private void ReleaseAirSwing()
        {
            if (!holdingAir) return;
            Body.gravityScale = airGravity;
            holdingAir = false;
        }
        private void DeliverStrike()
        {
            // Mark even a whiff/immune target as delivered: one contact query per swing.
            strikeDelivered = true;
            if (activeStarter == QuickSkills.BladeWave)
            {
                ReleaseStarterWave();
                return;
            }
            float centerY = activeStarter < 0 ? 1f : StarterAttacks[activeStarter].centerY;
            Bounds blade = AttackDirectionY == -1
                ? new Bounds(transform.position + new Vector3(0f, -0.35f, 0f), new Vector3(1.95f, 1.85f, 2f))
                : AttackDirectionY == 1
                    ? new Bounds(transform.position + new Vector3(0f, 2.15f, 0f), new Vector3(1.95f, 1.85f, 2f))
                    : new Bounds(transform.position + new Vector3(Facing * activeAttack.Reach, centerY, 0),
                        new Vector3(activeAttack.Width, activeAttack.Height, 2f));
            // The short wave extends only the last strike, with the SAME activation and damage query.
            if (Health.IsPlayer && AttackDirectionY == 0 && chainProfile != null && attackStep == ComboLimit && chainProfile.waveReach > 0f)
            {
                blade.center += new Vector3(Facing * chainProfile.waveReach * 0.5f, 0, 0);
                blade.size += new Vector3(chainProfile.waveReach, 0, 0);
                SkillStroke.Show(transform.position + new Vector3(Facing, 1, 0),
                    transform.position + new Vector3(Facing * (activeAttack.Reach + activeAttack.Width * 0.5f + chainProfile.waveReach), 1, 0),
                    new Color(0.2f, 0.85f, 1f), 0.16f);
            }
            if (Health.IsPlayer && AttackDirectionY == 0 && attackStep == ComboLimit && Skills?.Finale.State.active == true)
            {
                float reach = CasselDefinition.Load().Profile(Skills.Finale.State.returnCount).waveReach;
                blade.center += new Vector3(Facing * reach * 0.5f, 0, 0);
                blade.size += new Vector3(reach, 0, 0);
            }
            if (Health.IsPlayer && chainProfile != null && chainProfile.afterimageSeconds > 0f)
                SkillStroke.Afterimage(Animation.GetComponent<SpriteRenderer>(), chainProfile.afterimageSeconds);
            swingConnected = ApplySwordBounds(blade, activeUse, strikeDamage, activeAttack.Knockback, attackStep, activeStarter);
            if (activeStarter >= 0) ShowStarterStrike(blade, swingConnected);
            if (activeStarter == QuickSkills.Rising && Grounded) Jump(RisingLaunchSpeed);
            if (Health.IsPlayer && AttackDirectionY == -1 && swingConnected)
            {
                Body.linearVelocity = new Vector2(Body.linearVelocity.x, 10.8f);
                Defense?.State.Land();
                AtmospherePostEffect.PulseImpact(0.38f);
                CombatFloatingText2D.SpawnTag(transform.position + Vector3.up * 1.3f, Loc.Get("combat.pogo"), NotaryUiTheme.GoldGlow, 20);
            }
        }

        private bool ApplySwordBounds(Bounds blade, UseContext use, int amount, float knockback, int step, int starter, bool echo = false)
        {
            bool contact = false;
            bool pogo = starter < 0 && AttackDirectionY == -1;
            if (Skills != null)
            {
                if (pogo)
                {
                    foreach (var wt in WaveCombatTargets.All)
                        if (wt != null && wt.State != null && !wt.State.Cleared && blade.Intersects(wt.Bounds))
                            contact = true;
                }
                bool juggle = starter < 0 && aerialSwing && !Grounded && AttackDirectionY >= 0;
                contact |= WaveCombatTargets.Sword(Skills, use, blade, step, starter, echo, juggle);
                foreach (var destructible in DestructibleTarget2D.All)
                    if (blade.Intersects(destructible.Bounds))
                    {
                        bool hitOk = destructible.Hit(amount);
                        contact |= hitOk || pogo;
                        if (hitOk) CombatFloatingText2D.SpawnDamage(destructible.Bounds.center + Vector3.up * 0.4f, amount, false, false);
                        Skills.Progression.Apply(use, new CombatEffectResult(EffectTargetKind.Destructible,
                            destructible.TargetId, hitOk, amount));
                    }
                foreach (var surface in StitchSurface2D.All)
                    if (blade.Intersects(surface.Bounds))
                    {
                        bool cut = surface.Cut(false);
                        contact |= cut || pogo;
                        Skills.Progression.Apply(use, new CombatEffectResult(EffectTargetKind.TerrainMechanism,
                            surface.TargetId, cut));
                    }
            }
            if (Health.IsPlayer) BasicStrikeDelivered?.Invoke(blade, amount);
            if (opponent != null && opponent.gameObject.activeInHierarchy && !opponent.Dead)
            {
                var target = opponent.GetComponent<Collider2D>();
                if (target != null && blade.Intersects(target.bounds)
                    && (Health.IsPlayer
                        ? opponent.HitBySword(amount, starter == QuickSkills.BladeWave ? blade.center : transform.position,
                            step > 0 && step < ComboLimit ? knockback * .45f : knockback)
                        : opponent.Hit(amount, transform.position, knockback)))
                {
                    contact = true;
                    Skills?.Progression.Apply(use, new CombatEffectResult(EffectTargetKind.Enemy,
                        opponent.gameObject.name, true, amount));
                    if (Health.IsPlayer) SwordContact?.Invoke(use, step, step == ComboLimit, amount);
                    AudioManager.Play(AudioManager.Sfx.Hit);
                    if (!opponent.IsPlayer && starter < 0)
                    {
                        if (step == PlayerCombo.Length) ImpactFeedback.ComboFinisherHit();
                        else ImpactFeedback.SwordHit();
                    }
                }
            }
            return contact;
        }
        private void Update()
        {
            if (!GameInput.GameplayEnabled) return;
            if (Health == null) return;
            if (Temporal != null && Temporal.Frozen) return;
            Animation.SetCombatVisual(Time.time < Health.InvulnerableUntil, Health.Dead);
            if (Health.Dead) return;
            if (Attacking)
            {
                float elapsed = Time.time - attackStartedAt;
                AimPlayerAttack();
                Animation.PlayAttack(elapsed, activeAttack.StrikeTime, activeAttack.Duration);
                if (!strikeDelivered && elapsed >= activeAttack.StrikeTime) DeliverStrike();
                if (elapsed >= activeAttack.Duration)
                {
                    Attacking = false;
                    if (queuedAttack && comboStep > 0 && BeginNextPlayerSwing(fromBuffer: true)) return;
                    ReleaseAirSwing();
                    if (comboStep > 0 && comboStep < ComboLimit)
                    {
                        comboExpiresAt = attackStartedAt + activeAttack.Duration + ComboGraceSeconds;
                    }
                    else ResetCombo();
                }
            }
            if (Attacking) return;
            if (comboStep > 0 && Time.time > comboExpiresAt) ResetCombo();
            if (!Grounded) Animation.Play(Body.linearVelocity.y > 0.1f ? SpriteAnimator.Pose.Jump : SpriteAnimator.Pose.Fall);
            else if (Time.time < landUntil) Animation.Play(SpriteAnimator.Pose.Land);
            else Animation.Play(Mathf.Abs(Body.linearVelocity.x) > 0.1f ? SpriteAnimator.Pose.Run : SpriteAnimator.Pose.Idle);
        }
        private void ReceiveHit(Vector2 source)
        {
            Defense?.Interrupt();
            BenefactorSkills?.Interrupt();
            Animation.FlashHit();
            CancelAttack();
            lastHitAt = Time.time;
            stunUntil = Time.time + 0.18f;
            float away = transform.position.x >= source.x ? 1f : -1f;
            Body.linearVelocity = new Vector2(away * 5f, 4f) * Health.KnockbackMultiplier;
            Temporal?.RememberImpulse();
        }
        private void Die()
        {
            CancelStarterWaves();
            BenefactorSkills?.Interrupt();
            Temporal?.Release();
            Defense?.ResetDefense();
            CancelAttack();
            Body.linearVelocity = Vector2.zero;
            Animation.Play(SpriteAnimator.Pose.Land, true);
            Animation.SetCombatVisual(false, true);
        }
        public void DelayCombatClock(float seconds)
        {
            attackStartedAt += seconds; comboExpiresAt += seconds; landUntil += seconds;
            stunUntil += Mathf.Min(seconds, Time.time - lastHitAt);
        }
        public void ResetAt(Vector2 feet)
        {
            CancelStarterWaves();
            BenefactorSkills?.ResetAttempt();
            Temporal?.Release();
            Defense?.ResetDefense();
            transform.position = feet;
            Body.position = feet;
            Body.linearVelocity = Vector2.zero;
            CancelAttack();
            stunUntil = landUntil = 0f; LastLandedAt = float.NegativeInfinity;
            Grounded = false;
            airStrikesUsed = 0;
            groundSampled = false;
            Health.Refill();
            Animation.ResetVisual();
            Animation.Play(SpriteAnimator.Pose.Idle, true);
        }
        private void OnDisable() { BenefactorSkills?.Interrupt(); Temporal?.Release(); Defense?.ResetDefense(); CancelAttack(); }
        private void OnDestroy()
        {
            CancelStarterWaves();
            if (Health != null) { Health.Damaged -= ReceiveHit; Health.Died -= Die; }
            var collider = GetComponent<CapsuleCollider2D>();
            if (collider != null && collider.sharedMaterial != null) Destroy(collider.sharedMaterial);
        }
    }
}
