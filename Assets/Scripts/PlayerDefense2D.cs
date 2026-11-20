using UnityEngine;

namespace RewindNotary
{
    // Shell (-200), feedback (-100), defense input (-75), strikes (-50), attack input (0).
    [DefaultExecutionOrder(-75)]
    public sealed class PlayerDefense2D : MonoBehaviour
    {
        public DefenseState State { get; } = new DefenseState();
        public bool BlocksActions => State.BlocksActions;
        private CombatMotor2D motor;
        private bool dashPhysics;
        private float savedGravity, nextTrail;
        private Vector2 savedVelocity, dashHeading, dashOrigin;
        private Vector2 DashVelocity => new Vector2(dashHeading.x,
            motor.Grounded ? Mathf.Max(0f, dashHeading.y) : dashHeading.y) * DefenseTuning.DashSpeed;

        public void Initialize(CombatMotor2D owner) { motor = owner; }
        private void Update()
        {
            if (!GameInput.GameplayEnabled || motor == null || motor.Health.Dead) return;
            Advance();
            if (GameInput.CanPlay && GameInput.DashDown) TryDash(GameInput.DashDirection);
            if (State.Phase == DefensePhase.Dash && Time.time >= nextTrail)
            {
                Vector3 at = transform.position + Vector3.up * DefenseTuning.TrailHeight;
                Vector3 heading = DashVelocity.normalized;
                SkillStroke.Show(at, at - heading * DefenseTuning.TrailLength,
                    new Color(1f, 0.68f, 0.25f, 0.55f), DefenseTuning.TrailSeconds);
                nextTrail = Time.time + DefenseTuning.TrailInterval;
            }
        }
        private void Advance()
        {
            // Sample the final physics step before releasing Dash; ledge exit must spend the charge.
            if (State.Phase == DefensePhase.Dash) motor.RefreshGround();
            State.Tick(Time.time);
            if (dashPhysics && State.Phase != DefensePhase.Dash) RestoreDashPhysics();
        }
        private bool CanStart => GameInput.CanPlay && motor != null && !motor.Health.Dead
            && !motor.Stunned && !motor.Attacking && (motor.BenefactorSkills == null || !motor.BenefactorSkills.BlocksActions);
        public bool TryDash(Vector2 direction)
        {
            if (!CanStart) return false;
            Advance();
            motor.RefreshGround();
            bool hasHorizontal = Mathf.Abs(direction.x) > .01f;
            int facing = hasHorizontal ? (direction.x < 0f ? -1 : 1) : motor.Facing;
            float vertical = hasHorizontal && Mathf.Abs(direction.y) > .01f ? Mathf.Sign(direction.y) : 0f;
            if (motor.Grounded && vertical < 0f) vertical = 0f;
            if (!State.TryDash(Time.time, motor.Grounded, facing)) return false;
            dashHeading = new Vector2(State.DashDirection, vertical).normalized;
            dashOrigin = motor.Body.position;
            motor.ClearComboForDefense();
            motor.Face(facing);
            savedGravity = motor.Body.gravityScale;
            savedVelocity = motor.Body.linearVelocity;
            dashPhysics = true;
            motor.Body.gravityScale = 0f;
            motor.Body.linearVelocity = DashVelocity;
            nextTrail = Time.time;
            return true;
        }
        // Called by the normal player FixedUpdate through Move; keeps ordinary air tuning intact.
        public bool DriveMovement()
        {
            Advance();
            if (!BlocksActions) return false;
            motor.Body.linearVelocity = DashVelocity;
            return true;
        }
        public bool TryEvade()
        {
            if (!isActiveAndEnabled || !GameInput.GameplayEnabled || motor.Health.Dead) return false;
            Advance();
            return State.Evading(Time.time);
        }

        internal bool Evaded(Bounds threat, Bounds body)
        {
            if (!TryEvade()) return false;
            var start = new Bounds(body.center + (Vector3)(dashOrigin - motor.Body.position), body.size);
            body.Encapsulate(start);
            return threat.Intersects(body);
        }
        private void RestoreDashPhysics()
        {
            if (!dashPhysics || motor == null) return;
            motor.Body.gravityScale = savedGravity;
            motor.Body.linearVelocity = new Vector2(
                Mathf.Clamp(savedVelocity.x, -PlayerController2D.MoveSpeed, PlayerController2D.MoveSpeed),
                motor.Grounded ? 0f : savedVelocity.y);
            dashPhysics = false;
        }
        public void Interrupt() { State.Interrupt(); RestoreDashPhysics(); }
        public void ResetDefense() { State.Reset(); RestoreDashPhysics(); }
        private void OnDisable() { ResetDefense(); }
    }
}
