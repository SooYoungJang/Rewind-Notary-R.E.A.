using System;
using UnityEngine;

namespace RewindNotary
{
    public sealed class Health : MonoBehaviour
    {
        public int Maximum { get; private set; }
        public int Current { get; private set; }
        public bool Dead => Current <= 0;
        public bool IsPlayer { get; private set; }
        public event Action<Vector2> Damaged;
        public event Action Died;
        public float InvulnerableUntil { get; private set; }
        public float KnockbackMultiplier { get; private set; } = 1f;
        private float immunity;
        public void Initialize(int maximum, float hitImmunity, bool isPlayer = false)
        { Maximum = maximum; Current = maximum; immunity = hitImmunity; IsPlayer = isPlayer; }
        public bool Hit(int amount, Vector2 source, float knockbackMultiplier = 1f)
        {
            if (Dead || Time.time < InvulnerableUntil) return false;
            Current = Mathf.Max(0, Current - amount);
            InvulnerableUntil = Time.time + immunity;
            KnockbackMultiplier = knockbackMultiplier;
            Damaged?.Invoke(source);
            if (IsPlayer)
            {
                AudioManager.Play(Dead ? AudioManager.Sfx.PlayerDown : AudioManager.Sfx.Hurt);
                ImpactFeedback.PlayerHurt();
            }
            if (Dead) Died?.Invoke();
            return true;
        }
        public void Refill() { Current = Maximum; InvulnerableUntil = Time.time + 0.8f; }
    }
}
