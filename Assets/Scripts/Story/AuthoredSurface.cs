using UnityEngine;

namespace RewindNotary
{
    public sealed class AuthoredSurface : MonoBehaviour
    {
        internal bool Kickable { get; set; }
        internal Vector2 RampNormal { get; set; }

        internal bool SupportsRamp(CombatMotor2D motor, CapsuleCollider2D actor, Collider2D floor)
        {
            if (RampNormal == Vector2.zero || Vector2.Dot(motor.Body.linearVelocity, RampNormal) > .1f ||
                !floor.IsTouching(actor)) return false;
            // The capsule's lower cap is a circle. Only its tangent contact with the top counts.
            float radius = actor.size.x / 2;
            var center = motor.Body.position + Vector2.up * radius;
            var lip = (Vector2)transform.position + RampNormal * (((BoxCollider2D)floor).size.y / 2);
            return Mathf.Abs(Vector2.Dot(center - lip, RampNormal) - radius) < .055f;
        }
    }
}
