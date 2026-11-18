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
        private SkillCastRequest? pendingCast;
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
            if (!GameInput.CanPlay || motor.Health == null || motor.Health.Dead)
            { pendingCast = null; return; }
            axis = GameInput.Horizontal;
            var runner = motor.BenefactorSkills;
            var input = runner?.InputSnapshot;
            bool castDown = input != null && input.CastDown;
            if (motor.Stunned) pendingCast = null;
            if (castDown)
            {
                // One request only: a later press replaces it, even if the requested skill is unlearned.
                pendingCast = null;
                if (runner.CanRequest(input.Request) && !motor.Stunned) pendingCast = input.Request;
            }
            if (motor.Defense != null && motor.Defense.BlocksActions)
            {
                // Dash keeps the press-time ID/modifier until recovery; parry never reserves a cast.
                if (motor.Defense.State.Phase != DefensePhase.Dash) pendingCast = null;
                jumpPressed = float.NegativeInfinity; cutJump = false; return;
            }
            if (motor.BenefactorSkills != null && motor.BenefactorSkills.BlocksActions)
            { pendingCast = null; jumpPressed = float.NegativeInfinity; cutJump = false; return; }
            if (!castDown && !pendingCast.HasValue && GameInput.JumpDown) jumpPressed = Time.time;
            if (!GameInput.JumpHeld && GameInput.JumpUp) cutJump = true;
            // A simultaneous attack hold modifies canvas rather than starting an ordinary swing.
            if (!castDown && !pendingCast.HasValue && GameInput.AttackDown) motor.Attack();
            if (pendingCast.HasValue)
            {
                var request = pendingCast.Value; pendingCast = null;
                runner.Activate(request);
                // Skill leaps do not inherit a pending ordinary jump release/coyote reservation.
                if (motor.BenefactorSkills != null && motor.BenefactorSkills.BlocksActions)
                { jumpPressed = lastGrounded = float.NegativeInfinity; cutJump = false; }
            }
        }
        private void FixedUpdate()
        {
            if (!GameInput.CanPlay || motor.Health == null || motor.Health.Dead) return;
            motor.RefreshGround();
            if (motor.Grounded) lastGrounded = Time.time;
            if (Time.time - jumpPressed <= JumpBuffer && Time.time - lastGrounded <= CoyoteTime && !motor.Stunned
                && (motor.Defense == null || !motor.Defense.BlocksActions)
                && (motor.BenefactorSkills == null || !motor.BenefactorSkills.BlocksActions))
            {
                motor.Jump(JumpSpeed);
                AudioManager.Play(AudioManager.Sfx.Jump);
                jumpPressed = lastGrounded = float.NegativeInfinity;
            }
            // Jump first so a simultaneous jump/attack receives air control immediately.
            motor.Move(axis * MoveSpeed);
            if (motor.BenefactorSkills != null && motor.BenefactorSkills.BlocksActions) cutJump = false;
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
            pendingCast = null;
            axis = 0f; cutJump = false;
            jumpPressed = lastGrounded = float.NegativeInfinity;
        }
        public void ClearInput()
        {
            pendingCast = null;
            axis = 0f; cutJump = false;
            Silver?.ResetAttempt();
            motor.BenefactorSkills?.ResetAttempt();
            jumpPressed = lastGrounded = float.NegativeInfinity;
        }
        private void OnDisable() { pendingCast = null; }
        private void OnDestroy()
        {
            if (motor == null) return;
            motor.Landed -= OnLanded;
            motor.AttackStarted -= OnAttackStarted;
        }
    }
}
