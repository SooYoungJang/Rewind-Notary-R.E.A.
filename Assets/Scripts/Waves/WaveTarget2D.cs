using UnityEngine;

namespace RewindNotary
{
    public sealed partial class WaveTarget2D : MonoBehaviour
    {
        public string TargetId => name;
        public WaveTargetState State { get; private set; }
        public EffectTargetKind Kind => definition.mechanism ? EffectTargetKind.TerrainMechanism : EffectTargetKind.Enemy;
        // Includes the feet so a stage-1 canvas floor can actually contact the target.
        public Bounds Bounds => new Bounds(transform.position + Vector3.up * 0.9f, new Vector3(0.8f, 1.8f, 2));
        public string Hint => Loc.Get("wave.target_hint", definition.Label, State.Remaining, (State.Ready(Time.time) ? Loc.Get("wave.cut_with_sword") : State.NeedsSetup ? Loc.Get("wave.skill_then_sword", setupLabel) : Loc.Get("wave.defense_then_sword")));
        private WaveArchetype definition;
        private string movementArchetype;
        private WaveFooting2D footing;
        private int patrolDirection = 1;
        internal int Facing { get; private set; } = 1;
        internal bool Walking { get; private set; }
        internal bool FeetSupported => footing == null || footing.Grounded;
        private CombatMotor2D player;
        private string setupNameKey;
        private string setupLabel => string.IsNullOrEmpty(setupNameKey) ? "" : Loc.Get(setupNameKey);
        private SpriteRenderer visual;
        private float immobilizedUntil;
        public bool CanReceiveSupport => Kind == EffectTargetKind.Enemy && !State.Cleared && Time.time >= immobilizedUntil;
        public bool ReceiveSupport(BenefactorSupportDefinition support)
        {
            if (!CanReceiveSupport) return false;
            FinishAttack(Time.time);
            if (support.effect == "strike")
            {
                State.SupportHit(support.damage);
                if (State.Cleared) visual.enabled = false;
            }
            else immobilizedUntil = Time.time + support.seconds;
            nextAttack = Mathf.Max(Time.time, immobilizedUntil) + definition.attackInterval;
            return true;
        }
        public void Initialize(WaveArchetype kind, WaveSpawn spawn, CombatMotor2D actor, Sprite sprite, int setupHits = 1, bool skillOnly = false)
        {
            definition = kind; player = actor; State = new WaveTargetState(kind, spawn, setupHits, skillOnly);
            movementArchetype = spawn.archetypeId ?? kind.id;
            if (!kind.mechanism) footing = new WaveFooting2D(transform.position);
            setupNameKey = string.IsNullOrEmpty(spawn.setupSkill) ? "" : player.Skills.Progression.Catalog.Get(spawn.setupSkill).displayName;
            var picture = new GameObject("Target silhouette"); picture.transform.SetParent(transform, false);
            float scale = Mathf.Min(1.6f / sprite.bounds.size.x, 1.8f / sprite.bounds.size.y);
            picture.transform.localScale = Vector3.one * scale;
            picture.transform.localPosition = Vector3.up * (sprite.bounds.extents.y * scale);
            visual = picture.AddComponent<SpriteRenderer>(); visual.sprite = sprite; visual.sortingOrder = 8;
            attackIndex = Mathf.FloorToInt(Mathf.Abs(transform.position.x) * .65f);
            nextAttack = Time.time + definition.attackInterval;
        }
        public bool Setup(UseContext use)
        {
            if (!State.Setup(use)) return false;
            if (State.Cleared) visual.enabled = false;
            FinishAttack(Time.time);
            SkillStroke.Show(Bounds.min, Bounds.max, Color.cyan, 0.2f);
            return true;
        }
        public void RearmSwitch()
        {
            if (State.RearmSwitch()) visual.enabled = true;
        }
        public bool Cut(UseContext use, bool echo = false)
        {
            if (!State.Cut(use, Time.time, echo)) return false;
            FinishAttack(Time.time);
            if (State.Cleared) visual.enabled = false;
            return true;
        }
        private void FixedUpdate()
        {
            Walking = false;
            if (!GameInput.GameplayEnabled || footing == null || State.Cleared) return;
            Vector2 feet = footing.FollowGround(transform.position, Time.fixedDeltaTime);
            transform.position = feet;
            if (!footing.Grounded || player.Health.Dead || Time.time < immobilizedUntil ||
                (State.NeedsSetup && State.Ready(Time.time))) return;
            if (Charging)
            {
                StepCharge(feet, Time.time, Time.fixedDeltaTime);
                return;
            }
            if (attackPhase == AttackPhase.Windup || Time.time < recoverUntil) return;

            Vector2 offset = player.Body.position - feet;
            bool nearby = Mathf.Abs(offset.y) <= 1.25f && Mathf.Abs(offset.x) <= 5f && !TerrainBetween(player.Body.position);
            if (nearby && Mathf.Abs(offset.x) > .05f) Facing = offset.x > 0f ? 1 : -1;

            float goal, speed, leash;
            switch (movementArchetype)
            {
                case "notary_guard":
                    leash = 1.85f;
                    goal = nearby ? player.Body.position.x - Facing * 1.45f : footing.HomeX + patrolDirection * leash;
                    speed = nearby ? 1.65f : .8f;
                    if (nearby && Mathf.Abs(offset.x) < 1.45f) return;
                    break;
                case "shell_sentry":
                    if (!nearby || Mathf.Abs(offset.x) <= 1.7f) return;
                    leash = 1.3f;
                    goal = player.Body.position.x - Facing * 1.7f;
                    speed = .9f;
                    break;
                default: return;
            }
            goal = Mathf.Clamp(goal, footing.HomeX - leash, footing.HomeX + leash);
            float nextX = Mathf.MoveTowards(feet.x, goal, speed * Time.fixedDeltaTime);
            if (Mathf.Abs(nextX - feet.x) <= .001f || !footing.TryStep(feet, nextX, out var next))
            {
                if (!nearby) patrolDirection *= -1;
                return;
            }
            if (!nearby) Facing = nextX > feet.x ? 1 : -1;
            Walking = true;
            transform.position = next;
        }

        internal void ReactToStarter(int starter, float originX)
        {
            if (footing == null || State.Cleared || !footing.Grounded || State.NeedsSetup) return;
            if (starter == QuickSkills.Rising)
            {
                footing.Launch();
                return;
            }
            if (starter != QuickSkills.Sweep) return;
            Vector2 feet = transform.position;
            int direction = feet.x >= originX ? 1 : -1;
            float distance = footing.WalkableDistance(feet, direction, .85f);
            if (distance > 0f && footing.TryStep(feet, feet.x + direction * distance, out var next))
                transform.position = next;
        }

        private bool TerrainBetween(Vector2 feet) => Physics2D.Linecast(
            (Vector2)transform.position + Vector2.up * .67f, feet + Vector2.up * .67f,
            1 << LevelBuilder.GroundLayer).collider != null;

        private void Update()
        {
            if (!GameInput.GameplayEnabled || Time.deltaTime <= 0f || State == null || player.Health.Dead || State.Cleared) return;
            visual.color = attackPhase == AttackPhase.Windup ? Color.yellow : State.Ready(Time.time) ? Color.cyan
                : definition.gate == "armor" ? Color.gray : new Color(0.8f, 0.35f, 0.4f);
            if (Time.time < immobilizedUntil || footing == null || (State.NeedsSetup && State.Ready(Time.time))) return;
            if (!footing.Grounded)
            {
                if (attackPhase != AttackPhase.Idle) FinishAttack(Time.time);
                return;
            }
            TickAttack(Time.time);
        }
        private void OnEnable() { WaveCombatTargets.All.Add(this); }
        private void OnDisable()
        {
            attackPhase = AttackPhase.Idle;
            Walking = false;
            LastStrikeAt = float.NegativeInfinity;
            WaveCombatTargets.All.Remove(this);
        }
    }
}
