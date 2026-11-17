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
            if (motor.Health == null || motor.Health.Dead) return;
            axis = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f)
                - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                jumpPressed = Time.time;
            bool held = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            if (!held && (Input.GetKeyUp(KeyCode.Space) || Input.GetKeyUp(KeyCode.W) || Input.GetKeyUp(KeyCode.UpArrow)))
                cutJump = true;
            if (Input.GetKeyDown(KeyCode.J) || Input.GetKeyDown(KeyCode.Z) || Input.GetMouseButtonDown(0))
                motor.Attack();
        }
        private void FixedUpdate()
        {
            if (motor.Health == null || motor.Health.Dead) return;
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
        public void ClearInput()
        {
            axis = 0f; cutJump = false;
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
