using UnityEngine;

namespace RewindNotary
{
    public sealed class StitchBinding : MonoBehaviour
    {
        private CombatMotor2D motor;
        private float until;
        public bool Bound => motor != null && !motor.Health.Dead && Time.time < until;
        private void Awake()
        {
            motor = GetComponent<CombatMotor2D>();
            motor.AttackStarted += Break;
        }
        public bool TryBind(float seconds)
        {
            // Existing binding cannot be refreshed/farmed. An attack already committed breaks the thread.
            if (Bound || motor.Health.Dead || motor.Attacking) return false;
            until = Time.time + seconds;
            SkillStroke.Show(transform.position + new Vector3(-0.35f, 0.8f),
                transform.position + new Vector3(0.35f, 1.4f), Color.white, seconds);
            return true;
        }
        public void Break() { until = 0f; }
        private void OnDisable() { Break(); }
        private void OnDestroy() { if (motor != null) motor.AttackStarted -= Break; }
    }
}
