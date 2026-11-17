using UnityEngine;

namespace RewindNotary
{
    // Feet are the transform origin; the physics body extends upwards.
    [RequireComponent(typeof(Rigidbody2D), typeof(CapsuleCollider2D), typeof(Health))]
    public sealed class CombatMotor2D : MonoBehaviour
    {
        public Rigidbody2D Body { get; private set; }
        public Health Health { get; private set; }
        public SpriteAnimator Animation { get; private set; }
        public int Facing { get; private set; } = 1;
        public bool Grounded { get; private set; }
        public bool Attacking => attackRemaining > 0f;
        public bool Stunned => Time.time < stunUntil;
        public event System.Action Landed;
        private float attackRemaining, stunUntil, landUntil;
        private bool strikeDelivered, groundSampled;
        private Health opponent;
        private int damage;
        public const float AttackDuration = 0.32f;
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
                if (groundSampled && !Health.Dead) Landed?.Invoke();
            }
            groundSampled = true;
        }
        public void Move(float speed)
        {
            if (Health.Dead || Stunned) return;
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
            if (Health.Dead || Attacking || Stunned) return false;
            attackRemaining = AttackDuration;
            strikeDelivered = false;
            Animation.Play(SpriteAnimator.Pose.Attack, true);
            return true;
        }
        private void Update()
        {
            if (Health == null) return;
            Animation.SetCombatVisual(Time.time < Health.InvulnerableUntil, Health.Dead);
            if (Health.Dead) return;
            if (Attacking)
            {
                attackRemaining -= Time.deltaTime;
                // One hit at the third (slash) frame, never every render frame.
                if (!strikeDelivered && attackRemaining <= 0.16f)
                {
                    strikeDelivered = true;
                    if (opponent != null && !opponent.Dead)
                    {
                        Bounds blade = new Bounds(transform.position + new Vector3(Facing * 0.9f, 1f, 0),
                            new Vector3(1.7f, 1.55f, 2f));
                        var target = opponent.GetComponent<Collider2D>();
                        if (target != null && blade.Intersects(target.bounds) && opponent.Hit(damage, transform.position))
                        {
                            AudioManager.Play(AudioManager.Sfx.Hit);
                            if (!opponent.IsPlayer) ImpactFeedback.SwordHit();
                        }
                    }
                }
            }
            if (Attacking) Animation.Play(SpriteAnimator.Pose.Attack);
            else if (!Grounded) Animation.Play(Body.linearVelocity.y > 0.1f ? SpriteAnimator.Pose.Jump : SpriteAnimator.Pose.Fall);
            else if (Time.time < landUntil) Animation.Play(SpriteAnimator.Pose.Land);
            else Animation.Play(Mathf.Abs(Body.linearVelocity.x) > 0.1f ? SpriteAnimator.Pose.Run : SpriteAnimator.Pose.Idle);
        }
        private void ReceiveHit(Vector2 source)
        {
            Animation.FlashHit();
            attackRemaining = 0;
            stunUntil = Time.time + 0.18f;
            float away = transform.position.x >= source.x ? 1f : -1f;
            Body.linearVelocity = new Vector2(away * 5f, 4f);
        }
        private void Die()
        {
            attackRemaining = 0;
            Body.linearVelocity = Vector2.zero;
            Animation.Play(SpriteAnimator.Pose.Land, true);
            Animation.SetCombatVisual(false, true);
        }
        public void ResetAt(Vector2 feet)
        {
            transform.position = feet;
            Body.position = feet;
            Body.linearVelocity = Vector2.zero;
            attackRemaining = stunUntil = landUntil = 0f;
            Grounded = false;
            groundSampled = false;
            Health.Refill();
            Animation.ResetVisual();
            Animation.Play(SpriteAnimator.Pose.Idle, true);
        }
        private void OnDestroy()
        {
            if (Health != null) { Health.Damaged -= ReceiveHit; Health.Died -= Die; }
            var collider = GetComponent<CapsuleCollider2D>();
            if (collider != null && collider.sharedMaterial != null) Destroy(collider.sharedMaterial);
        }
    }
}
