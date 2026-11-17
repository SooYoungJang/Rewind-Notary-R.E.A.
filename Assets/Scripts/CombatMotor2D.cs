using UnityEngine;

namespace RewindNotary
{
    // Feet are the transform origin; the physics body extends upwards.
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(Health))]
    [DefaultExecutionOrder(-50)] // Finish/advance attacks before this frame's player input.
    public sealed class CombatMotor2D : MonoBehaviour
    {
        public Rigidbody2D Body { get; private set; }
        public Health Health { get; private set; }
        public SpriteAnimator Animation { get; private set; }
        public int Facing { get; private set; } = 1;
        public bool Grounded { get; private set; }
        public bool Attacking { get; private set; }
        public bool Stunned => Time.time < stunUntil;
        public int ComboStep => comboStep;
        public event System.Action Landed;
        public event System.Action AttackStarted;
        private float attackStartedAt, comboExpiresAt, stunUntil, landUntil;
        private bool strikeDelivered, groundSampled, queuedAttack;
        private int comboStep, attackStep;
        private Health opponent;
        private int damage;
        public const float AttackDuration = 0.32f;
        public const float ComboGraceSeconds = 0.18f;
        public const float AirAcceleration = 45f, AirDrag = 6f;

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
            new AttackSpec(0.56f, 0.28f, 0.56f, 1.20f, 2.40f, 1.90f, 4, 1.7f),
        };
        private static readonly AttackSpec BossAttack =
            new AttackSpec(AttackDuration, 0.16f, AttackDuration, 0.9f, 1.7f, 1.55f, 1, 1f);
        private AttackSpec activeAttack;
        private readonly Collider2D[] groundHits = new Collider2D[4];

        public void Initialize(string sheet, int hp, float immunity, int hitDamage, Color tint, bool isPlayer = false)
        {
            Body = GetComponent<Rigidbody2D>();
            Body.gravityScale = 3f;
            Body.freezeRotation = true;
            Body.interpolation = RigidbodyInterpolation2D.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var capsule = GetComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.62f, 1.9f);
            capsule.offset = new Vector2(0f, 0.95f);
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
            Animation.Initialize(sheet);
            Animation.Tint(tint);
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
            Grounded = Body.linearVelocity.y <= 0.1f && Physics2D.OverlapBox(
                (Vector2)transform.position + new Vector2(0, 0.025f), new Vector2(0.48f, 0.12f),
                0f, filter, groundHits) > 0;
            if (Grounded && !wasGrounded)
            {
                landUntil = Time.time + 0.09f;
                // Finish the current swing, but never carry an aerial chain/buffer onto the floor.
                if (groundSampled && Health.IsPlayer) ResetCombo();
                if (groundSampled && !Health.Dead) Landed?.Invoke();
            }
            groundSampled = true;
        }
        public void Move(float speed)
        {
            if (Health.Dead || Stunned) return;
            if (Health.IsPlayer && !Grounded)
            {
                // Attacking uses exactly the same air control as jumping/falling.
                // Reversals accelerate through zero; releasing input coasts with gentle drag.
                float acceleration = Mathf.Abs(speed) > 0.01f ? AirAcceleration : AirDrag;
                float horizontal = Mathf.MoveTowards(Body.linearVelocity.x, speed, acceleration * Time.fixedDeltaTime);
                Body.linearVelocity = new Vector2(horizontal, Body.linearVelocity.y);
                Face(speed);
                return;
            }
            Body.linearVelocity = new Vector2(Attacking ? 0f : speed, Body.linearVelocity.y);
            if (!Attacking) Face(speed);
        }
        public void Jump(float velocity)
        {
            if (Health.Dead || Stunned) return;
            Body.linearVelocity = new Vector2(Body.linearVelocity.x, velocity);
            Grounded = false;
            landUntil = 0f;
        }
        public bool Attack()
        {
            if (Health.Dead || Stunned) return false;
            if (Attacking)
            {
                // The final step and a chain reset by landing cannot reserve another swing.
                if (!Health.IsPlayer || comboStep == 0 || comboStep >= PlayerCombo.Length || queuedAttack) return false;
                float elapsed = Time.time - attackStartedAt;
                if (elapsed < activeAttack.BufferOpen || elapsed >= activeAttack.Duration) return false;
                queuedAttack = true;
                return true; // Accepted input, not a swing/audio event yet.
            }
            if (!Health.IsPlayer) { BeginAttack(0); return true; }
            if (Time.time > comboExpiresAt) ResetCombo();
            BeginAttack(comboStep + 1);
            return true;
        }
        private void BeginAttack(int step)
        {
            attackStep = comboStep = step;
            activeAttack = step == 0 ? BossAttack : PlayerCombo[step - 1];
            attackStartedAt = Time.time;
            Attacking = true;
            strikeDelivered = false;
            queuedAttack = false;
            Animation.PlayAttack(0f, activeAttack.StrikeTime, activeAttack.Duration);
            AttackStarted?.Invoke();
        }
        private void ResetCombo()
        {
            comboStep = 0;
            queuedAttack = false;
            comboExpiresAt = float.NegativeInfinity;
        }
        private void CancelAttack()
        {
            Attacking = false;
            ResetCombo();
        }
        private void DeliverStrike()
        {
            // Mark even a whiff/immune target as delivered: one contact query per swing.
            strikeDelivered = true;
            if (opponent == null || opponent.Dead) return;
            Bounds blade = new Bounds(transform.position + new Vector3(Facing * activeAttack.Reach, 1f, 0),
                new Vector3(activeAttack.Width, activeAttack.Height, 2f));
            var target = opponent.GetComponent<Collider2D>();
            if (target != null && blade.Intersects(target.bounds)
                && opponent.Hit(damage * activeAttack.DamageMultiplier, transform.position, activeAttack.Knockback))
            {
                AudioManager.Play(AudioManager.Sfx.Hit);
                if (!opponent.IsPlayer)
                {
                    if (attackStep == PlayerCombo.Length) ImpactFeedback.ComboFinisherHit();
                    else ImpactFeedback.SwordHit();
                }
            }
        }
        private void Update()
        {
            if (Health == null) return;
            Animation.SetCombatVisual(Time.time < Health.InvulnerableUntil, Health.Dead);
            if (Health.Dead) return;
            if (Attacking)
            {
                float elapsed = Time.time - attackStartedAt;
                Animation.PlayAttack(elapsed, activeAttack.StrikeTime, activeAttack.Duration);
                if (!strikeDelivered && elapsed >= activeAttack.StrikeTime) DeliverStrike();
                if (elapsed >= activeAttack.Duration)
                {
                    Attacking = false;
                    if (queuedAttack && comboStep > 0 && comboStep < PlayerCombo.Length)
                        BeginAttack(comboStep + 1);
                    else if (comboStep > 0 && comboStep < PlayerCombo.Length)
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
            Animation.FlashHit();
            CancelAttack();
            stunUntil = Time.time + 0.18f;
            float away = transform.position.x >= source.x ? 1f : -1f;
            Body.linearVelocity = new Vector2(away * 5f, 4f) * Health.KnockbackMultiplier;
        }
        private void Die()
        {
            CancelAttack();
            Body.linearVelocity = Vector2.zero;
            Animation.Play(SpriteAnimator.Pose.Land, true);
            Animation.SetCombatVisual(false, true);
        }
        public void ResetAt(Vector2 feet)
        {
            transform.position = feet;
            Body.position = feet;
            Body.linearVelocity = Vector2.zero;
            CancelAttack();
            stunUntil = landUntil = 0f;
            Grounded = false;
            groundSampled = false;
            Health.Refill();
            Animation.ResetVisual();
            Animation.Play(SpriteAnimator.Pose.Idle, true);
        }
        private void OnDisable() { CancelAttack(); }
        private void OnDestroy()
        {
            if (Health != null) { Health.Damaged -= ReceiveHit; Health.Died -= Die; }
            var collider = GetComponent<CapsuleCollider2D>();
            if (collider != null && collider.sharedMaterial != null) Destroy(collider.sharedMaterial);
        }
    }
}
