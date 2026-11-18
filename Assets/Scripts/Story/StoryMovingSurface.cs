using UnityEngine;

namespace RewindNotary
{
    [DefaultExecutionOrder(-210)]
    public sealed class StoryMovingSurface : MonoBehaviour
    {
        private Rigidbody2D body;
        private Vector2 origin, travel;
        private float period, elapsed;
        internal Vector2 StepVelocity { get; private set; }
        internal void Initialize(StorySurface surface)
        {
            if (surface.period <= 0f) throw new System.ArgumentException("Moving surface requires a positive period: " + surface.id);
            body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            origin = transform.position;
            travel = new Vector2(surface.travelX, surface.travelY);
            period = surface.period;
        }
        private void FixedUpdate()
        {
            elapsed += Time.fixedDeltaTime;
            var next = origin + travel * (.5f - .5f * Mathf.Cos(elapsed * (2f * Mathf.PI / period)));
            StepVelocity = (next - body.position) / Time.fixedDeltaTime;
            body.MovePosition(next);
        }
    }
}
