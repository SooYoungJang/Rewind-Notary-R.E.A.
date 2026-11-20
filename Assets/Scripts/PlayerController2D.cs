using UnityEngine;

namespace RewindNotary
{
    [RequireComponent(typeof(CombatMotor2D))]
    public sealed class PlayerController2D : MonoBehaviour
    {
        public const float CoyoteTime = 0.1f;
        public const float JumpBuffer = 0.1f;
        public const float AttackBuffer = 0.13f;
        public const float MoveSpeed = 6.5f;
        public const float SprintSpeed = 8.7f;
        public const float JumpSpeed = 12f;
        private CombatMotor2D motor;
        public SilverStitchSkill Silver { get; set; }
        private float axis, lastGrounded = float.NegativeInfinity, jumpPressed = float.NegativeInfinity;
        private bool cutJump;
        private Collider2D actorCollider;
        private readonly ContactPoint2D[] wallContacts = new ContactPoint2D[12];
        internal const float WallSlideSpeed = 2.2f, WallKickX = 7f, WallKickY = 13f, KickControlTime = .14f;
        private float kickUntil;
        private int kickDirection;
        private Collider2D kickedWall;
        internal bool Sliding { get; private set; }
        internal int WallJumps { get; private set; }
        internal float AttackSteering => Time.time < kickUntil ? kickDirection : GameInput.Horizontal;
        private float attackPressed = float.NegativeInfinity;
        private bool attackHoldBlocked;
        internal event System.Action AttackInputAccepted;
        private SkillCastRequest? pendingCast;
        private readonly StarterCommandInput starterCommand = new StarterCommandInput();
        private const float StarterBuffer = .34f;
        private (int index, int facing, bool command, float expires)? pendingStarter;
        private void Awake()
        {
            motor = GetComponent<CombatMotor2D>();
            actorCollider = GetComponent<Collider2D>();
            motor.Landed += OnLanded;
            motor.AttackStarted += OnAttackStarted;
        }
        private void OnLanded() { AudioManager.Play(AudioManager.Sfx.Land); }
        private void OnAttackStarted() { AudioManager.Play(AudioManager.Sfx.Swing); }
        private void Update()
        {
            bool attackHeld = GameInput.AttackHeld;
            if (!GameInput.CanPlay || motor.Health == null || motor.Health.Dead)
            {
                attackPressed = float.NegativeInfinity;
                attackHoldBlocked = attackHeld;
                pendingCast = null;
                pendingStarter = null;
                starterCommand.Reset();
                return;
            }
            if (!attackHeld) attackHoldBlocked = false;
            axis = GameInput.Horizontal;
            var runner = motor.BenefactorSkills;
            var input = runner?.InputSnapshot;
            bool castDown = input != null && input.CastDown;
            bool commandDown = false;
            if (motor.Grounded && !castDown && !GameInput.SprintHeld && !motor.Stunned)
            {
                starterCommand.Press(GameInput.CommandArrowDown, Time.unscaledTime);
                if (GameInput.KeyboardAttackDown)
                {
                    int starter = starterCommand.Consume(Time.unscaledTime, out int facing);
                    if (starter >= 0)
                    {
                        QueueStarter(starter, facing, command: true);
                        commandDown = true;
                    }
                }
            }
            else starterCommand.Reset();
            // A cast chord owns its attack modifier. Other taps survive action recovery.
            if (!castDown && !commandDown && GameInput.AttackDown) attackPressed = Time.time;
            if (motor.Stunned) { pendingCast = null; pendingStarter = null; }
            if (castDown)
            {
                attackHoldBlocked = attackHeld;
                attackPressed = float.NegativeInfinity;
                jumpPressed = float.NegativeInfinity;
                // One request only: a later press replaces it, even if the requested skill is unlearned.
                pendingCast = null;
                pendingStarter = null;
                int starter = QuickSkills.StarterIndex(input.Request.SkillId);
                if (starter >= 0 && !motor.Stunned) QueueStarter(starter, 0, command: false);
                else if (runner.CanRequest(input.Request) && !motor.Stunned) pendingCast = input.Request;
            }
            if (motor.Defense != null && motor.Defense.BlocksActions)
            {
                // Dash keeps the press-time ID/modifier until recovery; parry never reserves a cast.
                if (motor.Defense.State.Phase != DefensePhase.Dash) { pendingCast = null; pendingStarter = null; }
                jumpPressed = float.NegativeInfinity; cutJump = false; return;
            }
            if (motor.BenefactorSkills != null && motor.BenefactorSkills.BlocksActions)
            { pendingCast = null; pendingStarter = null; jumpPressed = float.NegativeInfinity; cutJump = false; return; }

            if (pendingStarter.HasValue)
            {
                var starter = pendingStarter.Value;
                if (Time.time > starter.expires) pendingStarter = null;
                else if (motor.UseStarterSkill(starter.index, starter.facing, starter.command))
                {
                    pendingStarter = null;
                    cutJump = false;
                    return;
                }
            }
            if (!castDown && !pendingCast.HasValue && !pendingStarter.HasValue && GameInput.JumpDown)
            {
                // A release queued since the previous physics tick belongs to the previous press.
                jumpPressed = Time.time;
                cutJump = false;
            }
            if (!GameInput.JumpHeld && GameInput.JumpUp) cutJump = true;
            bool attackBuffered = Time.time - attackPressed <= AttackBuffer;
            bool repeatAttack = attackHeld && !attackHoldBlocked && !motor.Attacking;
            if (!castDown && !pendingCast.HasValue && !pendingStarter.HasValue &&
                (attackBuffered || repeatAttack) && motor.Attack())
            {
                attackPressed = float.NegativeInfinity;
                if (attackBuffered) AttackInputAccepted?.Invoke();
            }
            if (pendingCast.HasValue)
            {
                var request = pendingCast.Value; pendingCast = null;
                runner.Activate(request);
                // Skill leaps do not inherit a pending ordinary jump release/coyote reservation.
                if (motor.BenefactorSkills != null && motor.BenefactorSkills.BlocksActions)
                { jumpPressed = lastGrounded = float.NegativeInfinity; cutJump = false; }
            }
        }
        private void QueueStarter(int index, int facing, bool command)
        {
            if (facing == 0) facing = axis == 0f ? motor.Facing : (int)Mathf.Sign(axis);
            pendingStarter = (index, facing, command, Time.time + StarterBuffer);
            pendingCast = null;
            attackHoldBlocked = GameInput.AttackHeld;
            attackPressed = jumpPressed = float.NegativeInfinity;
        }
        private int WallSide(out Collider2D wall)
        {
            wall = null;
            if (motor.Grounded || motor.Stunned || motor.Attacking || motor.Skills == null ||
                !motor.Skills.Progression.Record.storyFlags.Contains("intro_complete") ||
                motor.Defense.BlocksActions || motor.BenefactorSkills.BlocksActions) return 0;
            int count = actorCollider.GetContacts(wallContacts);
            for (int i = 0; i < count; i++)
            {
                var hit = wallContacts[i];
                var other = hit.collider == actorCollider ? hit.otherCollider : hit.collider;
                if (other == null || other.attachedRigidbody != null || Mathf.Abs(hit.normal.x) < .9f ||
                    !other.TryGetComponent<AuthoredSurface>(out var surface) || !surface.Kickable) continue;
                int side = other.bounds.center.x > motor.Body.position.x ? 1 : -1;
                if (axis * side <= .25f) continue;
                wall = other;
                return side;
            }
            return 0;
        }
        private void FixedUpdate()
        {
            if (!GameInput.CanPlay || motor.Health == null || motor.Health.Dead) return;
            motor.RefreshGround();
            if (motor.Grounded) lastGrounded = Time.time;
            // Releasing direction or suspending an action can hide a wall without breaking contact.
            if (motor.Grounded || (kickedWall != null && !actorCollider.IsTouching(kickedWall))) kickedWall = null;
            int side = WallSide(out var wall);
            Sliding = side != 0 && motor.Body.linearVelocity.y <= 0f;
            if (motor.Stunned || motor.Grounded || motor.Defense.BlocksActions || motor.BenefactorSkills.BlocksActions)
                kickUntil = 0f;
            if (Time.time - jumpPressed <= JumpBuffer && Time.time - lastGrounded <= CoyoteTime && !motor.Stunned
                && (motor.Defense == null || !motor.Defense.BlocksActions)
                && (motor.BenefactorSkills == null || !motor.BenefactorSkills.BlocksActions))
            {
                motor.Jump(JumpSpeed);
                AudioManager.Play(AudioManager.Sfx.Jump);
                jumpPressed = lastGrounded = float.NegativeInfinity;
            }
            else if (wall != null && wall != kickedWall && Time.time - jumpPressed <= JumpBuffer)
            {
                kickedWall = wall;
                kickDirection = -side;
                kickUntil = Time.time + KickControlTime;
                motor.Jump(WallKickY);
                motor.Body.linearVelocity = new Vector2(kickDirection * WallKickX, WallKickY);
                motor.Face(kickDirection);
                Sliding = false;
                WallJumps++;
                AudioManager.Play(AudioManager.Sfx.Jump);
                jumpPressed = lastGrounded = float.NegativeInfinity;
            }
            motor.Animation.SetWallPose(Sliding ? side : 0);
            if (Sliding)
            {
                float gravityStep = Physics2D.gravity.y * motor.Body.gravityScale * Time.fixedDeltaTime;
                float beforeGravity = Mathf.Min(0f, -WallSlideSpeed - gravityStep);
                if (motor.Body.linearVelocity.y < beforeGravity)
                    motor.Body.linearVelocity = new Vector2(motor.Body.linearVelocity.x, beforeGravity);
            }
            // Jump first so a simultaneous jump/attack receives air control immediately.
            if (Time.time < kickUntil) motor.Move(kickDirection * WallKickX);
            else motor.Move(axis * (motor.Grounded && GameInput.SprintHeld ? SprintSpeed : MoveSpeed));
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
            pendingStarter = null;
            starterCommand.Reset();
            kickUntil = 0f; Sliding = false;
            motor?.Animation?.SetWallPose(0);
            axis = 0f; cutJump = false;
            attackPressed = jumpPressed = lastGrounded = float.NegativeInfinity;
            attackHoldBlocked = GameInput.AttackHeld;
        }
        public void ClearInput()
        {
            SuspendInput();
            Silver?.ResetAttempt();
            motor.BenefactorSkills?.ResetAttempt();
        }
        private void OnApplicationFocus(bool focused) { if (!focused) ClearInput(); }
        private void OnDisable() { SuspendInput(); }
        private void OnDestroy()
        {
            if (motor == null) return;
            motor.Landed -= OnLanded;
            motor.AttackStarted -= OnAttackStarted;
        }
    }
}
