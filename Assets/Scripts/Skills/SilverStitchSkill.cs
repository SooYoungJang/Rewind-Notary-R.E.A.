using UnityEngine;

namespace RewindNotary
{
    public sealed class SilverStitchSkill : MonoBehaviour
    {
        private CombatMotor2D motor, enemy;
        private StitchBinding binding;
        private SkillSession session;
        private float readyAt;
        public void Initialize(SkillSession skills, CombatMotor2D target)
        {
            session = skills; enemy = target; motor = GetComponent<CombatMotor2D>();
            binding = enemy.GetComponent<StitchBinding>();
            motor.Health.Damaged += OnInterrupted;
            motor.Health.Died += ResetAttempt;
        }
        public bool Activate()
        {
            if (motor.Health.Dead || motor.Stunned || motor.Attacking || Time.time < readyAt) return false;
            if (motor.Defense != null && motor.Defense.BlocksActions) return false;
            if (motor.BenefactorSkills != null && motor.BenefactorSkills.BlocksActions) return false;
            var use = session.Progression.BeginUse(SkillRules.SilverStitch, session.PreviewStage);
            if (use == null) return false;
            var profile = session.Progression.Catalog.Get(SkillRules.SilverStitch).Profile(use.Stage);
            readyAt = Time.time + profile.cooldown;
            var blade = new Bounds(transform.position + new Vector3(motor.Facing * profile.reach * 0.5f, 1, 0),
                new Vector3(profile.reach, 2, 2));
            bool cut = false, installed = false;
            if (use.Stage == 3)
            {
                foreach (var surface in StitchSurface2D.All)
                {
                    if (!surface.Installed || !blade.Intersects(surface.Bounds)) continue;
                    // Damage applies only in the region connected to the installed thread.
                    if (!enemy.Health.Dead && surface.Bounds.Intersects(enemy.GetComponent<Collider2D>().bounds))
                    {
                        bool hit = enemy.Health.Hit(profile.cutDamage, transform.position);
                        session.Progression.Apply(use, new CombatEffectResult(EffectTargetKind.Enemy, enemy.name, hit, hit ? profile.cutDamage : 0));
                        if (hit) { AudioManager.Play(AudioManager.Sfx.Hit); ImpactFeedback.SwordHit(); }
                    }
                    bool changed = surface.Cut(true);
                    session.Progression.Apply(use, new CombatEffectResult(EffectTargetKind.TerrainMechanism, surface.TargetId, changed));
                    cut |= changed;
                }
                if (!enemy.Health.Dead && binding.Bound && blade.Intersects(enemy.GetComponent<Collider2D>().bounds))
                {
                    binding.Break(); cut = true;
                    bool hit = enemy.Health.Hit(profile.cutDamage, transform.position);
                    session.Progression.Apply(use, new CombatEffectResult(EffectTargetKind.Enemy, enemy.name + ":cut", true, hit ? profile.cutDamage : 0));
                    if (hit) { AudioManager.Play(AudioManager.Sfx.Hit); ImpactFeedback.SwordHit(); }
                }
            }
            if (!cut)
            {
                // Stage 1 binds a nearby joint. Later stages create a cross region on fixed anchors.
                if (use.Stage == 1 && !enemy.Health.Dead && blade.Intersects(enemy.GetComponent<Collider2D>().bounds))
                    session.Progression.Apply(use, new CombatEffectResult(EffectTargetKind.Enemy,
                        enemy.name + ":joint", binding.TryBind(profile.bindSeconds)));
                int count = 0;
                foreach (var surface in StitchSurface2D.All) if (surface.Installed || !surface.Available) count++;
                if (count < profile.installationLimit)
                {
                    StitchSurface2D closest = null;
                    float nearest = float.PositiveInfinity;
                    foreach (var surface in StitchSurface2D.All)
                    {
                        if (!surface.Available || !blade.Intersects(surface.Bounds)) continue;
                        float distance = Vector2.Distance(transform.position, surface.transform.position);
                        if (distance <= profile.reach && distance < nearest)
                        { closest = surface; nearest = distance; }
                    }
                    if (closest != null) installed = closest.Install(session.Progression, use, profile);
                }
            }
            SkillStroke.Show(transform.position + Vector3.up,
                transform.position + new Vector3(motor.Facing * profile.reach, 1, 0), Color.white, 0.12f);
            // Installed regions retain the originating context until a real support/slow/cut/expiry.
            if (!installed) session.Progression.EndUse(use);
            return true;
        }
        private void OnInterrupted(Vector2 source) { ResetAttempt(); }
        public void ResetAttempt()
        {
            readyAt = 0;
            // Invalidate delayed credit on interruption; installed floors retire safely below.
            foreach (var surface in StitchSurface2D.All) surface.CancelCredit();
        }
        private void OnDisable() { ResetAttempt(); }
        private void OnDestroy()
        {
            if (motor == null || motor.Health == null) return;
            motor.Health.Damaged -= OnInterrupted; motor.Health.Died -= ResetAttempt;
        }
    }
}
