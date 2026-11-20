using System;
using UnityEngine;

namespace RewindNotary
{
    public sealed class Health : MonoBehaviour
    {
        public static float AssistImmunityMultiplier = 1f;
        public int Maximum { get; private set; }
        public int Current { get; private set; }
        public bool Dead => Current <= 0;
        public bool IsPlayer { get; private set; }
        public event Action<Vector2> Damaged;
        public event Action Died;
        public float InvulnerableUntil { get; private set; }
        public float KnockbackMultiplier { get; private set; } = 1f;
        public int MinimumHealth { get; set; }
        private float immunity;
        public void Initialize(int maximum, float hitImmunity, bool isPlayer = false)
        { Maximum = maximum; Current = maximum; immunity = hitImmunity; IsPlayer = isPlayer; }
        public bool Hit(int amount, Vector2 source, float knockbackMultiplier = 1f)
            => ApplyHit(amount, source, knockbackMultiplier, sword: false);

        internal bool HitBySword(int amount, Vector2 source, float knockbackMultiplier)
            => ApplyHit(amount, source, knockbackMultiplier, sword: true);

        private bool ApplyHit(int amount, Vector2 source, float knockbackMultiplier, bool sword)
        {
            if (Dead || Time.time < InvulnerableUntil) return false;
            // Avoidance/parry changes neither HP nor hit immunity and emits no damage/skill event.
            if (IsPlayer && TryGetComponent<PlayerDefense2D>(out var defense) && defense.TryDefend(source)) return false;
            if (IsPlayer && TryGetComponent<ResonanceForgeSkill>(out var forge) && forge.TryCounter(source)) return false;
            Current = Mathf.Max(MinimumHealth, Current - amount);
            float recovery = IsPlayer ? immunity * AssistImmunityMultiplier : sword ? Mathf.Min(immunity, .075f) : immunity;
            InvulnerableUntil = Time.time + recovery;
            KnockbackMultiplier = knockbackMultiplier;
            CombatFloatingText2D.SpawnDamage(transform.position + Vector3.up * 1.55f, amount, amount >= 3 && !IsPlayer, IsPlayer);
            Damaged?.Invoke(source);
            if (IsPlayer)
            {
                AudioManager.Play(Dead ? AudioManager.Sfx.PlayerDown : AudioManager.Sfx.Hurt);
                ImpactFeedback.PlayerHurt();
            }
            if (Dead) Died?.Invoke();
            return true;
        }
        public bool Heal(int amount)
        {
            if (Dead || amount <= 0 || Current >= Maximum) return false;
            int before = Current;
            Current = Mathf.Min(Maximum, Current + amount);
            int gained = Current - before;
            if (gained > 0) CombatFloatingText2D.SpawnTag(transform.position + Vector3.up * 1.75f, "+" + gained, NotaryUiTheme.TealAccent, 21);
            return true;
        }
        public void Refill() { Current = Maximum; InvulnerableUntil = Time.time + 0.8f; }
    }
}
