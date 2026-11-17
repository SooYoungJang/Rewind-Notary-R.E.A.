using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    // Opt-in local clock. Only enemies, practice hazards and hostile projectiles register.
    public sealed class TemporalActor2D : MonoBehaviour
    {
        public static readonly List<TemporalActor2D> All = new List<TemporalActor2D>();
        public bool Frozen { get; private set; }
        private float until, startedAt;
        private Rigidbody2D body;
        private CombatMotor2D motor;
        private Vector2 velocity;
        private bool simulated;
        private void Awake() { body = GetComponent<Rigidbody2D>(); motor = GetComponent<CombatMotor2D>(); }
        private void OnEnable() { All.Add(this); }
        public bool TryFreeze(float seconds)
        {
            if (!isActiveAndEnabled || Frozen || seconds <= 0 || (motor != null && (motor.Health.Dead || motor.Health.IsPlayer))) return false;
            if (TryGetComponent<SkillProjectile2D>(out var projectile) && !projectile.Hostile) return false;
            Frozen = true; startedAt = Time.time; until = Time.time + seconds;
            if (body != null) { velocity = body.linearVelocity; simulated = body.simulated; body.simulated = false; }
            return true;
        }
        private void Update() { if (GameInput.GameplayEnabled && Frozen && Time.time >= until) Release(); }
        public void Release()
        {
            if (!Frozen) return;
            Frozen = false;
            motor?.DelayCombatClock(Time.time - startedAt);
            if (body != null)
            {
                body.simulated = simulated;
                body.linearVelocity = motor != null && motor.Health.Dead ? Vector2.zero : velocity;
            }
        }
        public void RememberImpulse() { if (Frozen && body != null) velocity = body.linearVelocity; }
        private void OnDisable() { Release(); All.Remove(this); }
    }
}
