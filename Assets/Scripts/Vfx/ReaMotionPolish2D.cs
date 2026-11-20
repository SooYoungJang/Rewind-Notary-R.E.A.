using UnityEngine;

namespace RewindNotary
{
    public sealed class ReaMotionPolish2D : MonoBehaviour
    {
        private CombatMotor2D motor;
        private Transform visual;
        private Vector2 springScale = Vector2.one;
        private bool wasGrounded = true;
        private float nextDashGhost;

        public void Initialize(CombatMotor2D owner)
        {
            motor = owner;
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
            springScale = motor.ComboStep == 4 ? new Vector2(1.12f, .94f) : new Vector2(1.06f, .96f);
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
