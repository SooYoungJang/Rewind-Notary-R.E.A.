using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed class PendulumRewindSkill : BenefactorSkill
    {
        public override string SkillId => BenefactorIds.Pendulum;
        public override bool BlocksActions => dashing;
        private bool dashing, returned;
        private Vector2 origin, savedVelocity, previous;
        private float dashEnd, returnEnd, releaseAt, nextBurst, gravity;
        private int direction, burstIndex;
        private readonly List<TemporalActor2D> stopped = new List<TemporalActor2D>();
        private readonly List<Bounds> traces = new List<Bounds>();
        public override bool Activate()
        {
            if (Active)
            {
                if (!CanAct || dashing || returned || Time.time >= returnEnd) return false;
                if (!SafeReturn()) return false;
                Vector2 departure = motor.Body.position;
                if (use.Stage >= 2) SkillStroke.Afterimage(motor.Animation.GetComponent<SpriteRenderer>(), SkillGeometry.EchoVisualSeconds);
                motor.Body.position = origin; transform.position = origin;
                motor.Body.linearVelocity = Vector2.zero; returned = true;
                Bounds crossing = Path(origin, departure);
                traces.Add(crossing);
                if (use.Stage >= 2) Damage(crossing, profile.burstDamage);
                Stroke(departure + Vector2.up, origin + Vector2.up, Color.yellow);
                // Return alone has no credit: it must cause a real crossed hit or local stop.
                if (use.Stage < 3) Finish();
                return true;
            }
            if (!Begin()) return false;
            origin = previous = motor.Body.position; direction = motor.Facing;
            savedVelocity = motor.Body.linearVelocity; gravity = motor.Body.gravityScale;
            dashing = true; returned = false; burstIndex = 0; traces.Clear(); stopped.Clear();
            dashEnd = Time.time + profile.duration; returnEnd = Time.time + profile.returnWindow;
            releaseAt = Time.time + profile.freezeSeconds;
            motor.Body.gravityScale = 0;
            if (use.Stage == 3)
                foreach (var actor in TemporalActor2D.All)
                    if (Vector2.Distance(actor.transform.position, origin) <= profile.burstRadius && actor.TryFreeze(profile.freezeSeconds))
                    { stopped.Add(actor); Credit(actor.name + ":time_stop", true); }
            Stroke(origin, origin + Vector2.up * SkillGeometry.HitHeight, Color.yellow);
            DriveMovement();
            return true;
        }
        private static Bounds Path(Vector2 a, Vector2 b) => new Bounds((a + b) * 0.5f + Vector2.up * SkillGeometry.Height,
            new Vector3(Mathf.Abs(a.x - b.x) + SkillGeometry.Height, Mathf.Abs(a.y - b.y) + SkillGeometry.HitHeight, 2));
        public override bool DriveMovement()
        {
            if (!dashing) return false;
            if (Time.time >= dashEnd) { EndDash(); return false; }
            var area = Path(previous, motor.Body.position);
            Damage(area, profile.damage); previous = motor.Body.position;
            motor.Body.linearVelocity = new Vector2(direction * profile.reach / profile.duration, 0);
            return true;
        }
        private void EndDash()
        {
            if (!dashing) return;
            Damage(Path(previous, motor.Body.position), profile.damage);
            traces.Add(Path(origin, motor.Body.position));
            dashing = false; motor.Body.gravityScale = gravity;
            motor.Body.linearVelocity = savedVelocity;
        }
        private bool SafeReturn()
        {
            var capsule = GetComponent<CapsuleCollider2D>();
            int mask = 1 << LevelBuilder.GroundLayer;
            Vector2 delta = origin - motor.Body.position;
            // Check the complete swept body and the destination's supporting terrain, not just a point.
            if (delta.sqrMagnitude > SkillGeometry.Skin * SkillGeometry.Skin)
            {
                var hit = Physics2D.CapsuleCast(motor.Body.position + capsule.offset, capsule.size - Vector2.one * SkillGeometry.Skin,
                    CapsuleDirection2D.Vertical, 0, delta.normalized, delta.magnitude, mask);
                if (hit.collider != null) return false;
            }
            if (Physics2D.OverlapCapsule(origin + capsule.offset, capsule.size - Vector2.one * SkillGeometry.Skin,
                CapsuleDirection2D.Vertical, 0, mask) != null) return false;
            return Physics2D.BoxCast(origin + Vector2.up * SkillGeometry.Skin,
                new Vector2(capsule.size.x, SkillGeometry.Skin), 0, Vector2.down, SkillGeometry.ReturnGroundProbe, mask).collider != null;
        }
        private void Update()
        {
            if (!GameInput.GameplayEnabled || !Active) return;
            if (dashing && Time.time >= dashEnd) EndDash();
            if (use.Stage < 3) { if (Time.time >= returnEnd) Finish(); return; }
            if (Time.time < releaseAt) return;
            foreach (var actor in stopped) if (actor != null) actor.Release();
            stopped.Clear();
            if (Time.time < nextBurst) return;
            if (burstIndex < traces.Count)
            {
                Bounds trace = traces[burstIndex++]; Damage(trace, profile.burstDamage);
                Stroke(trace.min, trace.max, Color.yellow);
                nextBurst = Time.time + SkillGeometry.BurstInterval;
            }
            else Finish();
        }
        public override void Interrupt()
        {
            // Cancellation releases physics without producing another strike or explosion.
            if (dashing) { dashing = false; motor.Body.gravityScale = gravity; motor.Body.linearVelocity = savedVelocity; }
            foreach (var actor in stopped) if (actor != null) actor.Release();
            stopped.Clear(); traces.Clear(); nextBurst = 0; base.Interrupt();
        }
    }
}
