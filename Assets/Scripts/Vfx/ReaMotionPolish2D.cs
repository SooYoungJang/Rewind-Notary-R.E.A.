using UnityEngine;

namespace RewindNotary
{
    public sealed class ReaMotionPolish2D : MonoBehaviour
    {
        private CombatMotor2D motor;
        private ReaGrowthVisual2D growth;
        private Transform visual;
        private Vector2 springScale = Vector2.one;
        private bool wasGrounded = true;
        private float nextDashGhost;

        public void Initialize(CombatMotor2D owner, ReaGrowthVisual2D growthVisual)
        {
            motor = owner;
            growth = growthVisual;
            visual = owner.Animation.transform;
            motor.Landed += OnLanded;
            motor.AttackStarted += OnAttackStarted;
        }

        private void OnLanded()
        {
            springScale = new Vector2(1.22f, 0.80f);
        }

        private void OnAttackStarted()
        {
            int step = Mathf.Clamp(motor.ComboStep, 1, 4);
            int tier = growth != null ? growth.CurrentTier : 1;
            float size = (step == 1 ? 1.95f : step == 2 ? 2.25f : step == 3 ? 2.65f : 3.25f) * (1f + (tier - 1) * 0.10f);
            int dirY = motor.AttackDirectionY;
            float angle = dirY == -1 ? -90f : dirY == 1 ? 90f : (step == 1 ? 0f : step == 2 ? 18f : step == 3 ? -15f : 6f) * motor.Facing;
            Color tint = step == 4 || tier >= 3
                ? new Color(1.0f, 0.92f, 0.60f, 0.95f)
                : step == 3 ? new Color(1.0f, 0.86f, 0.56f, 0.90f)
                : step == 2 ? new Color(0.70f, 0.94f, 1.0f, 0.90f)
                : new Color(0.92f, 0.97f, 1.0f, 0.86f);
            Vector3 at = dirY == -1
                ? transform.position + new Vector3(0f, -0.15f, 0f)
                : dirY == 1
                    ? transform.position + new Vector3(0f, 2.05f, 0f)
                    : transform.position + new Vector3(motor.Facing * (0.92f + step * 0.08f), 1.02f, 0f);
            ActorVisualPolish2D.SpawnRotatedSlash("fx-basic-sword-mastery-additive", at, motor.Facing, size, 0.22f + step * 0.03f, angle, tint);
            springScale = step == 4 ? new Vector2(1.20f, 0.86f) : new Vector2(1.10f, 0.92f);
            if (step == 4)
            {
                if (motor.Animation.Renderer != null) SkillStroke.Afterimage(motor.Animation.Renderer, 0.24f);
                AtmospherePostEffect.PulseImpact(0.50f);
            }
        }

        private void LateUpdate()
        {
            if (motor == null || visual == null) return;
            float dt = Time.deltaTime;

            if (wasGrounded && !motor.Grounded && motor.Body.linearVelocity.y > 2.5f)
                springScale = new Vector2(0.84f, 1.18f);
            wasGrounded = motor.Grounded;

            var phase = motor.Defense != null ? motor.Defense.State.Phase : DefensePhase.Ready;
            if (phase == DefensePhase.Dash)
            {
                springScale = Vector2.Lerp(springScale, new Vector2(1.26f, 0.84f), dt * 22f);
                if (Time.time >= nextDashGhost && motor.Animation.Renderer != null)
                {
                    SkillStroke.Afterimage(motor.Animation.Renderer, 0.20f);
                    nextDashGhost = Time.time + 0.045f;
                }
            }
            else
            {
                springScale = Vector2.Lerp(springScale, Vector2.one, 1f - Mathf.Exp(-dt * 14f));
            }

            visual.localScale = new Vector3(springScale.x, springScale.y, 1f);
        }

        private void OnDestroy()
        {
            if (motor != null)
            {
                motor.Landed -= OnLanded;
                motor.AttackStarted -= OnAttackStarted;
            }
        }
    }
}
