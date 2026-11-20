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
        private Vector2 savedVelocity, dashHeading;
        private Vector2 DashVelocity => new Vector2(dashHeading.x,
            motor.Grounded ? Mathf.Max(0f, dashHeading.y) : dashHeading.y) * DefenseTuning.DashSpeed;

        public void Initialize(CombatMotor2D owner) { motor = owner; }
        private void Update()
        {
            if (!GameInput.GameplayEnabled || motor == null || motor.Health.Dead) return;
            Advance();
            if (GameInput.CanPlay)
            {
                // Simultaneous defense buttons resolve to dash. No held-button auto-repeat.
                if (GameInput.DashDown) TryDash(GameInput.DashDirection);
                else if (GameInput.ParryDown) TryStartParry();
            }
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
        public bool TryStartParry()
        {
            if (!CanStart) return false;
            Advance();
            if (!State.TryStartParry(Time.time)) return false;
            motor.ClearComboForDefense();
            motor.Body.linearVelocity = new Vector2(0f, motor.Body.linearVelocity.y);
            ShowParry(new Color(1f, 0.85f, 0.35f), DefenseTuning.ParryWindowSeconds);
            return true;
        }
        // Called by the normal player FixedUpdate through Move; keeps ordinary air tuning intact.
        public bool DriveMovement()
        {
            Advance();
            if (State.Phase == DefensePhase.Dash)
            {
                motor.Body.linearVelocity = DashVelocity;
                return true;
            }
            if (!BlocksActions) return false;
            motor.Body.linearVelocity = new Vector2(0f, motor.Body.linearVelocity.y);
            return true;
        }
        public bool TryDefend(Vector2 source)
        {
            if (!isActiveAndEnabled || !GameInput.GameplayEnabled || motor.Health.Dead) return false;
            Advance();
            if (State.Evading(Time.time) || State.SuccessGuard(Time.time)) return true;
            if (!State.TryParry(Time.time)) return false;
            motor.Face(source.x - transform.position.x);
            ImpactFeedback.ParrySuccess();
            AudioManager.Play(AudioManager.Sfx.Parry);
            ShowParry(Color.white, DefenseTuning.ParryEffectSeconds);
            AtmospherePostEffect.PulseImpact(0.65f);
            GameHudCanvas.PulseParryFlash();
            CombatFloatingText2D.SpawnTag(transform.position + Vector3.up * 2.0f, Loc.Get("combat.parry"), NotaryUiTheme.TealAccent, 25);
            motor.Skills?.RecordParrySuccess();
            return true;
        }
        private void ShowParry(Color color, float seconds)
        {
            Vector3 center = transform.position + Vector3.up;
            Vector3 half = new Vector3(DefenseTuning.ParryEffectLength * 0.5f, DefenseTuning.ParryEffectLength * 0.5f, 0f);
            SkillStroke.Show(center - half, center + half, color, seconds);
            half.x = -half.x;
            SkillStroke.Show(center - half, center + half, color, seconds);
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
