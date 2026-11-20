using UnityEngine;

namespace RewindNotary
{
    // Wave targets use overlap attacks and do not push the player. Resolve only their feet against the terrain.
    internal sealed class WaveFooting2D
    {
        private const float Skin = .012f;
        private const float StepHeight = .34f;
        private const int GroundMask = 1 << LevelBuilder.GroundLayer;
        private readonly Vector2 spawn;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[8];
        private ContactFilter2D terrain;
        private Collider2D support;
        private Vector2 localFoot;
        private float fallSpeed;

        internal WaveFooting2D(Vector2 spawn)
        {
            this.spawn = spawn;
            HomeX = spawn.x;
            terrain.SetLayerMask(GroundMask);
            terrain.useTriggers = false;
        }

        internal float HomeX { get; private set; }
        internal bool Grounded => support != null;
        internal void Launch() { support = null; fallSpeed = -8f; }

        internal Vector2 FollowGround(Vector2 feet, float dt)
        {
            if (support != null && support.enabled && support.gameObject.activeInHierarchy)
            {
                Vector2 carried = support.transform.TransformPoint(localFoot);
                HomeX += carried.x - feet.x;
                feet = carried;
            }

            fallSpeed += 28f * dt;
            if (fallSpeed < 0f)
            {
                float rise = -fallSpeed * dt;
                if (Physics2D.Raycast(feet + Vector2.up * 1.7f, Vector2.up, rise, GroundMask).collider == null)
                    return feet + Vector2.up * rise;
                fallSpeed = 0f;
            }
            if (FindFloor(feet, StepHeight + fallSpeed * dt, out var floor))
                return Plant(feet.x, floor);

            support = null;
            feet.y -= fallSpeed * dt;
            if (feet.y >= spawn.y - 14f) return feet;
            fallSpeed = 0f;
            HomeX = spawn.x;
            return spawn;
        }

        internal bool TryStep(Vector2 feet, float nextX, out Vector2 next)
        {
            next = feet;
            if (!ProbeStep(feet, nextX, out var floor)) return false;
            next = Plant(nextX, floor);
            return true;
        }

        internal float WalkableDistance(Vector2 feet, int direction, float maxDistance)
        {
            float distance = 0f;
            while (distance < maxDistance)
            {
                float step = Mathf.Min(.24f, maxDistance - distance);
                float nextX = feet.x + direction * step;
                if (!ProbeStep(feet, nextX, out var floor)) break;
                feet = new Vector2(nextX, floor.point.y + Skin);
                distance += step;
            }
            return distance;
        }

        private bool ProbeStep(Vector2 feet, float nextX, out RaycastHit2D floor)
        {
            floor = default;
            float direction = Mathf.Sign(nextX - feet.x);
            if (Physics2D.Raycast(feet + Vector2.up * .7f, Vector2.right * direction,
                    Mathf.Abs(nextX - feet.x) + .24f, GroundMask).collider != null) return false;

            // Check the leading foot as well as the center so the actor turns before a drop.
            return FindFloor(new Vector2(nextX + direction * .19f, feet.y), StepHeight, out _) &&
                FindFloor(new Vector2(nextX, feet.y), StepHeight, out floor);
        }

        private bool FindFloor(Vector2 feet, float below, out RaycastHit2D floor)
        {
            int count = Physics2D.Raycast(feet + Vector2.up * StepHeight, Vector2.down, terrain, hits, below + StepHeight);
            floor = default;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                // A ray beginning inside a wall reports a synthetic normal opposite the ray.
                if (hit.fraction > 0f && hit.normal.y >= .58f &&
                    (floor.collider == null || hit.distance < floor.distance)) floor = hit;
            }
            return floor.collider != null;
        }

        private Vector2 Plant(float x, RaycastHit2D floor)
        {
            var feet = new Vector2(x, floor.point.y + Skin);
            support = floor.collider;
            localFoot = support.transform.InverseTransformPoint(feet);
            fallSpeed = 0f;
            return feet;
        }
    }
}
