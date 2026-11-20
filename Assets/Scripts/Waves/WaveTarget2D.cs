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
        private Bounds Threat => new Bounds(transform.position + Vector3.up,
            new Vector3(definition.attackReach * 2, 2.2f, 2));
        private void Update()
        {
            if (!GameInput.GameplayEnabled || State == null || player.Health.Dead || State.Cleared) return;
            visual.color = winding ? Color.yellow : State.Ready(Time.time) ? Color.cyan
                : definition.gate == "armor" ? Color.gray : new Color(0.8f, 0.35f, 0.4f);
            if (Time.time < immobilizedUntil || definition.mechanism || (State.NeedsSetup && State.Ready(Time.time))) return;
            var playerBounds = player.GetComponent<Collider2D>().bounds;
            if (!winding)
            {
                if (Time.time < nextAttack || !Threat.Intersects(playerBounds)) return;
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
            bool contact = Threat.Intersects(playerBounds);
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
            LastStrikeAt = float.NegativeInfinity;
            WaveCombatTargets.All.Remove(this);
        }
    }
}
