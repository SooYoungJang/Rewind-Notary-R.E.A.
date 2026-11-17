using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class CurtainCallWaltzSkill : BenefactorSkill
    {
        public override string SkillId => BenefactorIds.Waltz;
        public override bool BlocksActions => spinning || plunging;
        private bool spinning, plunging, stepReady, airborneSpent;
        private float spinEnd, endAt;
        private readonly HashSet<string> contacts = new HashSet<string>();
        public override bool Activate()
        {
            if (!CanAct || BlocksActions) return false;
            if (Active)
            {
                if (use.Stage < 2 || !stepReady || motor.Grounded) return false;
                stepReady = false; Spin(); return true;
            }
            if (!motor.Grounded && airborneSpent) return false;
            if (!Begin()) return false;
            contacts.Clear(); stepReady = false; airborneSpent = true;
            endAt = Time.time + profile.lifetime; Spin(); return true;
        }
        private void Spin()
        {
            spinning = true; spinEnd = Time.time + profile.duration;
            motor.Jump(profile.jumpSpeed);
            Stroke(motor.Body.position, motor.Body.position + Vector2.up * profile.reach, Color.cyan);
        }
        private void Contact(string id, bool changed, EffectTargetKind kind)
        {
            Credit(id, changed, kind);
            if (changed && use.Stage >= 2 && contacts.Add(id)) stepReady = true;
        }
        private void Update()
        {
            if (!GameInput.GameplayEnabled) return;
            if (motor.Grounded && !spinning && !plunging) airborneSpent = false;
            if (!Active) return;
            if (Time.time >= endAt) { Interrupt(); return; }
            if (spinning)
            {
                Bounds area = SkillGeometry.Area(transform.position + Vector3.up, profile.reach);
                if (!contacts.Contains(enemy.name) && !enemy.Health.Dead && area.Intersects(enemy.GetComponent<Collider2D>().bounds))
                {
                    bool hit = enemy.Health.Hit(profile.damage, transform.position);
                    Contact(enemy.name, hit, EffectTargetKind.Enemy);
                    if (hit) contacts.Add(enemy.name);
                }
                foreach (var shot in SkillProjectile2D.All.ToArray())
                    if (area.Intersects(shot.Bounds)) Contact(shot.name, shot.Deflect(use.Stage == 3), EffectTargetKind.TerrainMechanism);
                foreach (var lamp in PracticeLamp2D.All)
                    if (area.Intersects(lamp.Bounds)) Contact(lamp.name, lamp.Trigger(), EffectTargetKind.TerrainMechanism);
                foreach (var target in DestructibleTarget2D.All)
                    if (!contacts.Contains(target.TargetId) && area.Intersects(target.Bounds))
                    {
                        bool hit = target.Hit(profile.damage); Credit(target.TargetId, hit, EffectTargetKind.Destructible);
                        if (hit) contacts.Add(target.TargetId);
                    }
                if (Time.time >= spinEnd)
                {
                    spinning = false;
                    if (use.Stage == 3) plunging = true;
                }
            }
            else if (motor.Grounded)
            {
                if (plunging)
                {
                    Damage(SkillGeometry.Area(transform.position, profile.burstRadius), profile.burstDamage);
                    Stroke(motor.Body.position, motor.Body.position + Vector2.up * profile.burstRadius, Color.cyan);
                }
                plunging = false; airborneSpent = false; Finish();
            }
        }
        public override bool DriveMovement()
        {
            if (!BlocksActions) return false;
            motor.Body.linearVelocity = new Vector2(0, plunging ? -profile.plungeSpeed : motor.Body.linearVelocity.y);
            return true;
        }
        public override void Interrupt() { spinning = plunging = stepReady = false; base.Interrupt(); }
        public override void ResetAttempt() { base.ResetAttempt(); airborneSpent = false; contacts.Clear(); }
    }
}
