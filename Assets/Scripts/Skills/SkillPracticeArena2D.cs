using UnityEngine;

namespace RewindNotary
{
    // Explicit finite targets, rearmed only by an attempt reset; the emitter provides real hostile contacts.
    public sealed class SkillPracticeArena2D : MonoBehaviour
    {
        private CombatMotor2D player, enemy;
        private float nextShot;
        private int serial;
        public void Initialize(CombatMotor2D actor, CombatMotor2D target)
        {
            player = actor; enemy = target;
            var lamp = Child("practice_air_lamp", SkillGeometry.PracticeLampPoint); lamp.AddComponent<PracticeLamp2D>();
            var guard = Child("practice_cracked_guard", SkillGeometry.PracticeGuardPoint);
            guard.AddComponent<ResonanceTarget2D>().InitializeMechanism();
            var hazard = Child("practice_temporal_hazard", SkillGeometry.PracticeHazardPoint);
            hazard.AddComponent<PracticeTemporalHazard2D>().Initialize(player);
        }
        private GameObject Child(string id, Vector2 at)
        { var go = new GameObject(id); go.transform.SetParent(transform, false); go.transform.position = at; return go; }
        private void Update()
        {
            if (!GameInput.GameplayEnabled || player.Health.Dead || player.transform.position.x < SkillGeometry.PracticeStart) return;
            if (Time.time < nextShot) return;
            nextShot = Time.time + SkillGeometry.ProjectileInterval;
            var go = Child("practice_projectile:" + (++serial), SkillGeometry.PracticeEmitterPoint);
            go.AddComponent<SkillProjectile2D>().Initialize(player, enemy);
        }
        public void ResetAttempt() { nextShot = Time.time + SkillGeometry.ProjectileInterval; }
    }
}
