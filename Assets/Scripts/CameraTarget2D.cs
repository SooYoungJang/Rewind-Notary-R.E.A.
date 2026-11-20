using UnityEngine;

namespace RewindNotary
{
    [DefaultExecutionOrder(-200)]
    public sealed class CameraTarget2D : MonoBehaviour
    {
        public Transform Player;
        internal RepresentativeArea Area;
        private CombatMotor2D motor;
        private float lookAheadX;
        private float peekY;
        private float peekHoldSeconds;
        private int peekDirection;

        private void LateUpdate()
        {
            if (Player == null) return;
            if (motor == null) motor = Player.GetComponent<CombatMotor2D>();

            float dt = Time.deltaTime;
            float targetLook = 0f;
            float targetPeek = 0f;

            if (motor != null && GameInput.CanPlay && AutoTestRuntime.Report == null)
            {
                float vx = motor.Body != null ? motor.Body.linearVelocity.x : 0f;
                if (Mathf.Abs(vx) > 0.6f) targetLook = motor.Facing * 1.35f;
                else targetLook = motor.Facing * 0.45f;

                float vAxis = GameInput.Vertical;
                if (motor.Grounded && Mathf.Abs(vx) < 0.25f && !motor.Attacking && Mathf.Abs(vAxis) > 0.6f)
                {
                    int dir = vAxis > 0 ? 1 : -1;
                    if (dir == peekDirection) peekHoldSeconds += dt;
                    else { peekDirection = dir; peekHoldSeconds = 0f; }
                    if (peekHoldSeconds >= 0.38f) targetPeek = dir * 2.1f;
                }
                else
                {
                    peekDirection = 0;
                    peekHoldSeconds = 0f;
                }
            }

            float smooth = dt > 0f ? 1f - Mathf.Exp(-dt * 6.5f) : 1f;
            lookAheadX = Mathf.Lerp(lookAheadX, targetLook, smooth);
            peekY = Mathf.Lerp(peekY, targetPeek, smooth);

            float rawX = Player.position.x + lookAheadX;
            float rawY = Player.position.y + peekY;
            var bounds = Area?.Room.cameraBounds;
            if (bounds != null && bounds.Defined)
                transform.position = new Vector3(
                    Mathf.Clamp(rawX, bounds.x, bounds.x + bounds.width),
                    Mathf.Clamp(rawY, bounds.y, bounds.y + bounds.height), 0f);
            else
                transform.position = new Vector3(
                    Mathf.Clamp(rawX, 0f, 53f),
                    Mathf.Clamp(rawY, 1.5f, 5.5f), 0f);
        }

        public void Snap()
        {
            lookAheadX = 0f;
            peekY = 0f;
            peekHoldSeconds = 0f;
            peekDirection = 0;
            if (Player == null) return;
            var bounds = Area?.Room.cameraBounds;
            if (bounds != null && bounds.Defined)
                transform.position = new Vector3(
                    Mathf.Clamp(Player.position.x, bounds.x, bounds.x + bounds.width),
                    Mathf.Clamp(Player.position.y, bounds.y, bounds.y + bounds.height), 0f);
            else
                transform.position = new Vector3(
                    Mathf.Clamp(Player.position.x, 0f, 53f),
                    Mathf.Clamp(Player.position.y, 1.5f, 5.5f), 0f);
        }
    }
}
