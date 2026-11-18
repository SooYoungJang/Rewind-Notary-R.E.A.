using UnityEngine;

namespace RewindNotary
{
    public static class BenefactorIds
    {
        public const string Pendulum = "pendulum_rewind", Waltz = "curtain_call_waltz";
        public const string Forge = "resonance_forge", Canvas = "canvas_carve";
        public static readonly string[] All = { SkillRules.SilverStitch, Pendulum, Waltz, Forge, Canvas };
    }
    public static class SkillGeometry
    {
        public const float MarkSpacing = 0.25f, MarkHeight = 2.2f, MarkSize = 0.25f;
        public const float GuardWidth = 0.6f, GuardHeight = 1.2f;
        public const float Height = 1f, HitHeight = 2f, Skin = 0.04f, ReturnGroundProbe = 0.4f;
        public const float PaintGroundOffset = 0.18f, PaintLead = 0.35f;
        public const float PaintThickness = 0.14f, PaintBelowFeet = 0.25f, WallOffset = 1.4f;
        public const float EchoVisualSeconds = 0.3f;
        public const float BurstInterval = 0.4f;
        public const float EffectSeconds = 0.18f, SupportTolerance = 0.4f, EchoDamageScale = 0.5f;
        public const float HazardSpeed = 1.5f, HazardSpan = 1f, ProjectileSpeed = 4f;
        public const float ProjectileInterval = 2.5f, ProjectileLifetime = 5f, ProjectileRadius = 0.15f;
        public const float PracticeStart = 26f, LampRadius = 0.4f, WallInputThreshold = 0.5f;
        public static readonly Vector2 PracticeLampPoint = new Vector2(31, 2.8f);
        public static readonly Vector2 PracticeGuardPoint = new Vector2(30, GuardHeight * 0.5f);
        public static readonly Vector2 PracticeHazardPoint = new Vector2(32, 1);
        public static readonly Vector2 PracticeEmitterPoint = new Vector2(34, 1.2f);
        public const int ProjectileDamage = 1;
        public static Bounds Area(Vector2 center, float radius) => new Bounds(center, new Vector3(radius * 2, radius * 2, 2));
        public static Bounds Forward(CombatMotor2D motor, float reach) => new Bounds(
            motor.transform.position + new Vector3(motor.Facing * reach * 0.5f, Height, 0), new Vector3(reach, HitHeight, 2));
    }
    // Each executor owns one context across all of its delayed effects and re-inputs.
    public abstract class BenefactorSkill : MonoBehaviour
    {
        protected CombatMotor2D motor, enemy;
        protected SkillSession session;
        protected UseContext use;
        protected SkillStageDefinition profile;
        protected float readyAt;
        public abstract string SkillId { get; }
        public virtual bool BlocksActions => false;
        public bool Active => use != null && !use.Closed;
        public virtual void Initialize(SkillSession skills, CombatMotor2D target)
        { session = skills; enemy = target; motor = GetComponent<CombatMotor2D>(); }
        protected bool CanAct => GameInput.CanPlay && !motor.Health.Dead && !motor.Stunned && !motor.Attacking
            && (motor.Defense == null || !motor.Defense.BlocksActions);
        protected bool Begin()
        {
            if (!CanAct || Time.time < readyAt || Active) return false;
            use = session.Progression.BeginUse(SkillId, session.PreviewStage);
            if (use == null) return false;
            profile = session.Progression.Catalog.Get(SkillId).Profile(use.Stage);
            readyAt = Time.time + profile.cooldown;
            motor.ClearComboForDefense();
            return true;
        }
        protected void Credit(string target, bool changed, EffectTargetKind kind = EffectTargetKind.TerrainMechanism, int damage = 0)
        { session.Progression.Apply(use, new CombatEffectResult(kind, target, changed, damage)); }
        protected bool Damage(Bounds area, int amount, bool scaleOutput = true)
        {
            if (!Active || amount <= 0) return false;
            if (scaleOutput) amount = FinaleCombat.Scale(session, amount);
            WaveCombatTargets.Skill(session, use, area);
            bool changed = false;
            if (!enemy.Health.Dead && area.Intersects(enemy.GetComponent<Collider2D>().bounds))
            {
                bool hit = enemy.Health.Hit(amount, transform.position);
                Credit(enemy.name, hit, EffectTargetKind.Enemy, hit ? amount : 0); changed |= hit;
            }
            foreach (var target in DestructibleTarget2D.All)
                if (area.Intersects(target.Bounds))
                {
                    bool hit = target.Hit(amount);
                    Credit(target.TargetId, hit, EffectTargetKind.Destructible, hit ? amount : 0); changed |= hit;
                }
            if (changed) AudioManager.Play(AudioManager.Sfx.Hit);
            return changed;
        }
        protected void Stroke(Vector2 from, Vector2 to, Color color)
        { SkillStroke.Show(from, to, color, SkillGeometry.EffectSeconds); }
        protected virtual void Finish() { session?.ReportTechniqueCompleted(use); session?.Progression.EndUse(use); use = null; }
        public abstract bool Activate();
        public virtual bool DriveMovement() => false;
        public virtual void Interrupt() { session?.Progression.EndUse(use); use = null; }
        public virtual void ResetAttempt() { Interrupt(); readyAt = 0; }
        protected virtual void OnDisable() { Interrupt(); }
    }
}
