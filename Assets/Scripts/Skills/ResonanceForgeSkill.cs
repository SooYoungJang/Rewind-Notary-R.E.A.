using UnityEngine;

namespace RewindNotary
{
    public sealed class ResonanceForgeSkill : BenefactorSkill
    {
        public override string SkillId => BenefactorIds.Forge;
        public override bool BlocksActions => Active;
        private bool counter, plunge;
        private float endAt;
        public override bool Activate()
        {
            if (!Begin()) return false;
            counter = use.Stage == 3 && motor.Grounded; plunge = use.Stage == 3 && !motor.Grounded;
            endAt = Time.time + (counter ? profile.counterWindow : plunge ? profile.lifetime : profile.duration);
            if (!counter && !plunge) Strike(SkillGeometry.Forward(motor, profile.reach), false);
            Stroke(motor.Body.position, motor.Body.position + Vector2.up * profile.reach, Color.yellow);
            return true;
        }
        private void Strike(Bounds area, bool bell)
        {
            WaveCombatTargets.Skill(session, use, area);
            foreach (var target in ResonanceTarget2D.All)
                if (target.Guarded && area.Intersects(target.Bounds)) Credit(target.name + ":guard_break", target.BreakGuard());
            var marks = enemy.GetComponent<ResonanceTarget2D>();
            if (!enemy.Health.Dead && area.Intersects(enemy.GetComponent<Collider2D>().bounds))
            {
                bool chord = use.Stage >= 2 && marks.LiveMarks == profile.markLimit - 1;
                int damage = FinaleCombat.Scale(session, bell || chord ? profile.burstDamage : profile.damage);
                bool hit = enemy.Health.Hit(damage, transform.position);
                Credit(enemy.name, hit, EffectTargetKind.Enemy, hit ? damage : 0);
                bool exploded = hit && use.Stage >= 2 && marks.AddMark(profile.markLimit, profile.markSeconds);
                if (hit && use.Stage >= 2)
                    for (int i = 0; i < marks.Marks; i++)
                        SkillStroke.Show(enemy.transform.position + new Vector3(i * SkillGeometry.MarkSpacing, SkillGeometry.MarkHeight, 0),
                            enemy.transform.position + new Vector3(i * SkillGeometry.MarkSpacing, SkillGeometry.MarkHeight + SkillGeometry.MarkSize, 0),
                            Color.yellow, SkillGeometry.EffectSeconds);
                if (exploded)
                {
                    // The third contact itself carries explosion damage, so hit immunity cannot eat it.
                    Damage(SkillGeometry.Area(enemy.transform.position + Vector3.up, profile.burstRadius), profile.burstDamage);
                    Stroke((Vector2)enemy.transform.position + Vector2.left * profile.burstRadius,
                        (Vector2)enemy.transform.position + Vector2.right * profile.burstRadius, Color.yellow);
                }
            }
            foreach (var target in DestructibleTarget2D.All)
                if (area.Intersects(target.Bounds)) Credit(target.TargetId, target.Hit(FinaleCombat.Scale(session, bell ? profile.burstDamage : profile.damage)), EffectTargetKind.Destructible);
        }
        // Health calls this AFTER the unchanged basic dash/parry path. It never records a basic parry.
        public bool TryCounter(Vector2 source)
        {
            if (!GameInput.GameplayEnabled || !Active || !counter || Time.time >= endAt) return false;
            counter = false; motor.Face(source.x - transform.position.x);
            Credit("forge_counter", true);
            Strike(SkillGeometry.Area(transform.position + Vector3.up, profile.burstRadius), true);
            Stroke(motor.Body.position + Vector2.left * profile.burstRadius,
                motor.Body.position + Vector2.right * profile.burstRadius, Color.yellow);
            Finish(); return true;
        }
        private void Update()
        {
            if (!GameInput.GameplayEnabled || !Active) return;
            if (plunge && motor.Grounded)
            { plunge = false; Strike(SkillGeometry.Area(transform.position, profile.burstRadius), true); Finish(); }
            else if (Time.time >= endAt) Interrupt();
        }
        public override bool DriveMovement()
        {
            if (!Active) return false;
            motor.Body.linearVelocity = new Vector2(0, plunge ? -profile.plungeSpeed : motor.Body.linearVelocity.y);
            return true;
        }
        public override void Interrupt() { counter = plunge = false; base.Interrupt(); }
    }
}
