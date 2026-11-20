using UnityEngine;

namespace RewindNotary
{
    public sealed class WaveTarget2D : MonoBehaviour
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
        private float nextAttack, strikeAt, lastDodge = float.NegativeInfinity;
        internal float StrikeIn => winding ? strikeAt - Time.time : float.PositiveInfinity;
        internal float LastStrikeAt { get; private set; } = float.NegativeInfinity;
        private bool winding;
        private float immobilizedUntil;
        public bool CanReceiveSupport => Kind == EffectTargetKind.Enemy && !State.Cleared && Time.time >= immobilizedUntil;
        public bool ReceiveSupport(BenefactorSupportDefinition support)
        {
            if (!CanReceiveSupport) return false;
            winding = false;
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
            nextAttack = Time.time + definition.attackInterval;
        }
        public bool Setup(UseContext use)
        {
            if (!State.Setup(use)) return false;
            if (State.Cleared) visual.enabled = false;
            winding = false; nextAttack = Time.time + definition.attackInterval;
            SkillStroke.Show(Bounds.min, Bounds.max, Color.cyan, 0.2f);
            return true;
        }
        public void RearmSwitch()
        {
            if (State.RearmSwitch()) visual.enabled = true;
        }
        public bool Cut(UseContext use)
        {
            if (!State.Cut(use, Time.time)) return false;
            winding = false; nextAttack = Time.time + definition.attackInterval;
            if (State.Cleared) visual.enabled = false;
            return true;
        }
        private Bounds Threat
        {
            get
            {
                float height = movementArchetype == "shell_sentry" ? 1.15f : 2.2f;
                float back = .4f;
                return new Bounds(transform.position + new Vector3(Facing * (definition.attackReach - back) * .5f, height * .5f, 0f),
                    new Vector3(definition.attackReach + back, height, 2f));
            }
        }

        private void FixedUpdate()
        {
            Walking = false;
            if (!GameInput.GameplayEnabled || footing == null || State.Cleared) return;
            Vector2 feet = footing.FollowGround(transform.position, Time.fixedDeltaTime);
            transform.position = feet;
            if (!footing.Grounded || winding || player.Health.Dead || Time.time < immobilizedUntil ||
                (State.NeedsSetup && State.Ready(Time.time))) return;

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

        private bool TerrainBetween(Vector2 feet) => Physics2D.Linecast(
            (Vector2)transform.position + Vector2.up * .67f, feet + Vector2.up * .67f,
            1 << LevelBuilder.GroundLayer).collider != null;

        private void Update()
        {
            if (!GameInput.GameplayEnabled || State == null || player.Health.Dead || State.Cleared) return;
            visual.color = winding ? Color.yellow : State.Ready(Time.time) ? Color.cyan
                : definition.gate == "armor" ? Color.gray : new Color(0.8f, 0.35f, 0.4f);
            if (Time.time < immobilizedUntil || footing == null || (State.NeedsSetup && State.Ready(Time.time))) return;
            if (!footing.Grounded) { winding = false; return; }
            var playerBounds = player.GetComponent<Collider2D>().bounds;
            if (!winding)
            {
                if (Time.time < nextAttack ||
                    Mathf.Abs(player.Body.position.x - transform.position.x) > definition.attackReach + .35f) return;
                Facing = player.Body.position.x >= transform.position.x ? 1 : -1;
                if (!Threat.Intersects(playerBounds) || TerrainBetween(player.Body.position)) return;
                winding = true; strikeAt = Time.time + definition.telegraphSeconds;
                lastDodge = float.NegativeInfinity;
                SkillStroke.Show(Threat.min, Threat.max, Color.yellow, definition.telegraphSeconds);
            }
            // A dash counts only when it actually traverses this imminent attack's danger area.
            if (Threat.Intersects(playerBounds) && player.Defense.State.Evading(Time.time)
                && strikeAt - Time.time <= DefenseTuning.DashInvulnerableSeconds) lastDodge = Time.time;
            if (Time.time < strikeAt) return;
            winding = false; nextAttack = Time.time + definition.attackInterval;
            LastStrikeAt = Time.time;
            bool contact = Threat.Intersects(playerBounds) && !TerrainBetween(player.Body.position);
            int parries = player.Skills.ParrySuccessCount;
            bool evading = contact && player.Defense.State.Evading(Time.time);
            bool hurt = contact && player.Health.Hit(definition.attackDamage, transform.position);
            bool parried = player.Skills.ParrySuccessCount > parries;
            bool dodged = !hurt && (evading || (!contact && Time.time - lastDodge <= DefenseTuning.DashInvulnerableSeconds));
            if (parried || dodged)
            {
                if (State.OpenDefense(Time.time))
                    player.Skills.Progression.EmitSimple("wave_opening", "", TargetId + (parried ? ":parry" : ":dash"));
            }
        }
        private void OnEnable() { WaveCombatTargets.All.Add(this); }
        private void OnDisable()
        {
            winding = false;
            Walking = false;
            LastStrikeAt = float.NegativeInfinity;
            WaveCombatTargets.All.Remove(this);
        }
    }
}
