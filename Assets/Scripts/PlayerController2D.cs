using UnityEngine;

namespace RewindNotary
{
    [RequireComponent(typeof(CombatMotor2D))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        public const float CoyoteTime = 0.1f;
        public const float JumpBuffer = 0.1f;
        public const float MoveSpeed = 6.5f;
        public const float JumpSpeed = 12f;
        private CombatMotor2D motor;
        public SilverStitchSkill Silver { get; set; }
        private float axis, lastGrounded = float.NegativeInfinity, jumpPressed = float.NegativeInfinity;
        private bool cutJump;
        private void Awake()
        {
            motor = GetComponent<CombatMotor2D>();
            motor.Landed += OnLanded;
            motor.AttackStarted += OnAttackStarted;
        }
        private void OnLanded() { AudioManager.Play(AudioManager.Sfx.Land); }
        private void OnAttackStarted() { AudioManager.Play(AudioManager.Sfx.Swing); }
        private void Update()
        {
            if (!GameInput.CanPlay || motor.Health == null || motor.Health.Dead) return;
            axis = GameInput.Horizontal;
            if (GameInput.JumpDown) jumpPressed = Time.time;
            if (!GameInput.JumpHeld && GameInput.JumpUp) cutJump = true;
            if (GameInput.AttackDown) motor.Attack();
            if (GameInput.SilverDown) Silver?.Activate();
        }
        private void FixedUpdate()
        {
            if (!GameInput.CanPlay || motor.Health == null || motor.Health.Dead) return;
            motor.RefreshGround();
            if (motor.Grounded) lastGrounded = Time.time;
            if (Time.time - jumpPressed <= JumpBuffer && Time.time - lastGrounded <= CoyoteTime && !motor.Stunned)
            {
                motor.Jump(JumpSpeed);
                AudioManager.Play(AudioManager.Sfx.Jump);
                jumpPressed = lastGrounded = float.NegativeInfinity;
            }
            // Jump first so a simultaneous jump/attack receives air control immediately.
            motor.Move(axis * MoveSpeed);
            if (cutJump)
            {
                if (motor.Body.linearVelocity.y > 0f)
                    motor.Body.linearVelocity = new Vector2(motor.Body.linearVelocity.x, motor.Body.linearVelocity.y * 0.5f);
                cutJump = false;
            }
            if (motor.Body.linearVelocity.y < -18f)
                motor.Body.linearVelocity = new Vector2(motor.Body.linearVelocity.x, -18f);
        }
        public void SuspendInput()
        {
            axis = 0f; cutJump = false;
            jumpPressed = lastGrounded = float.NegativeInfinity;
        }
        public void ClearInput()
        {
            axis = 0f; cutJump = false;
            Silver?.ResetAttempt();
            jumpPressed = lastGrounded = float.NegativeInfinity;
        }
        private void OnDestroy()
        {
            if (motor == null) return;
            motor.Landed -= OnLanded;
            motor.AttackStarted -= OnAttackStarted;
        }
    }
}
