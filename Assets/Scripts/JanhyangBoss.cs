using UnityEngine;

namespace RewindNotary
{
    [RequireComponent(typeof(CombatMotor2D))]
    public sealed class JanhyangBoss : MonoBehaviour
    {
        public bool Active { get; private set; }
        public bool Telegraphing => windup > 0f;
        private CombatMotor2D motor, player;
        private float windup, cooldown, jumpTimer;
        private StitchBinding stitchBinding;
        private AudioManager.VoiceHandle warningVoice;
        private void Awake() { motor = GetComponent<CombatMotor2D>(); }
        public void Initialize(CombatMotor2D target)
        {
            player = target;
            stitchBinding = GetComponent<StitchBinding>();
            motor.Health.Damaged += OnDamaged;
            motor.Health.Died += OnDefeated;
            player.Health.Died += CancelWarning;
        }
        public void Activate()
        {
            if (Active || motor.Health.Dead) return;
            Active = true; cooldown = 0.7f; jumpTimer = 1.8f;
            AudioManager.SetMusic(AudioManager.Music.Boss);
        }
        public void ResetPattern()
        {
            CancelWarning(); Active = false; cooldown = 0.7f; jumpTimer = 1.8f;
        }
        private void CancelWarning() { windup = 0f; AudioManager.Stop(warningVoice); warningVoice = default; }
        private void OnDamaged(Vector2 source) { CancelWarning(); }
        private void OnDefeated()
        {
            CancelWarning();
            AudioManager.Play(AudioManager.Sfx.BossDefeat);
            ImpactFeedback.BossDefeated();
            AudioManager.SetMusic(AudioManager.Music.Field);
        }
        private void FixedUpdate()
        {
            if (!GameInput.GameplayEnabled) return;
            if (motor.Health == null || motor.Health.Dead) return;
            if (motor.Temporal != null && motor.Temporal.Frozen) return;
            motor.RefreshGround();
            if (!Active || player == null || player.Health.Dead) { motor.Move(0); return; }
            // Delay the next joint action without pulling/teleporting the target or changing tuning.
            if (stitchBinding != null && stitchBinding.Bound) { motor.Move(0); return; }
            cooldown -= Time.fixedDeltaTime;
            jumpTimer -= Time.fixedDeltaTime;
            if (motor.Stunned) { CancelWarning(); return; }
            if (motor.Attacking) { motor.Move(0); return; }
            Vector2 delta = player.transform.position - transform.position;
            if (windup > 0f)
            {
                motor.Move(0);
                windup -= Time.fixedDeltaTime;
                if (windup <= 0f)
                {
                    CancelWarning();
                    if (motor.Attack()) AudioManager.Play(AudioManager.Sfx.BossAttack);
                    cooldown = 0.95f;
                }
                return;
            }
            motor.Face(delta.x);
            if (Mathf.Abs(delta.x) < 1.55f && Mathf.Abs(delta.y) < 1.15f && cooldown <= 0f)
            {
                windup = 0.45f; motor.Move(0);
                warningVoice = AudioManager.Play(AudioManager.Sfx.BossWarning);
                return;
            }
            float move = Mathf.Abs(delta.x) > 1.25f ? Mathf.Sign(delta.x) * 3.4f : 0f;
            if ((transform.position.x < 35.5f && move < 0f) || (transform.position.x > 58f && move > 0f)) move = 0f;
            motor.Move(move);
            if (motor.Grounded && jumpTimer <= 0f && (Mathf.Abs(delta.x) > 2f || delta.y > 0.7f))
            { motor.Jump(12.2f); jumpTimer = 2.4f; }
        }
        private void LateUpdate()
        {
            if (motor.Animation != null)
                motor.Animation.SetTelegraph(Telegraphing && !motor.Health.Dead);
        }
        private void OnDestroy()
        {
            CancelWarning();
            if (motor != null && motor.Health != null)
            { motor.Health.Damaged -= OnDamaged; motor.Health.Died -= OnDefeated; }
            if (player != null && player.Health != null) player.Health.Died -= CancelWarning;
        }
    }
}
