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
        private float airborneUntil;
        private float launchGravity;
        private float holdAtApex;

        internal WaveFooting2D(Vector2 spawn)
        {
            this.spawn = spawn;
            HomeX = spawn.x;
            terrain.SetLayerMask(GroundMask);
            terrain.useTriggers = false;
        }

        internal float HomeX { get; private set; }
        internal bool Grounded => support != null;
        internal bool Hanging => Time.time < airborneUntil;
        internal void Launch(float speed, float gravity)
        {
            support = null;
            fallSpeed = -speed;
            launchGravity = gravity;
            airborneUntil = holdAtApex = 0f;
        }

        internal void HoldAir(float seconds)
        {
            if (fallSpeed < 0f)
            {
                holdAtApex = seconds;
                return;
            }
            support = null;
            fallSpeed = 0f;
            airborneUntil = Time.time + seconds;
        }

        internal Vector2 FollowGround(Vector2 feet, float dt)
        {
            if (Hanging) return feet;
            if (support != null && support.enabled && support.gameObject.activeInHierarchy)
            {
                Vector2 carried = support.transform.TransformPoint(localFoot);
                HomeX += carried.x - feet.x;
                feet = carried;
            }

            fallSpeed += (launchGravity > 0f ? launchGravity : 28f) * dt;
            if (fallSpeed < 0f)
            {
                float rise = -fallSpeed * dt;
                float clearance = UpwardClearance(feet + Vector2.up * 1.7f, rise);
                feet.y += clearance;
                if (clearance >= rise) return feet;
                fallSpeed = 0f;
            }
            if (holdAtApex > 0f)
            {
                HoldAir(holdAtApex);
                holdAtApex = 0f;
                return feet;
            }
            float floorTolerance = launchGravity > 0f ? Skin : StepHeight;
            if (FindFloor(feet, floorTolerance + fallSpeed * dt, out var floor, floorTolerance))
                return Plant(feet.x, floor);

            support = null;
            feet.y -= fallSpeed * dt;
            if (feet.y >= spawn.y - 14f) return feet;
            fallSpeed = launchGravity = 0f;
            HomeX = spawn.x;
            return spawn;
        }

        private float UpwardClearance(Vector2 head, float rise)
        {
            int count = Physics2D.Raycast(head, Vector2.up, terrain, hits, rise);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.fraction <= 0f || hit.normal.y > -.58f) continue;
                if (hit.collider.usedByEffector &&
                    hit.collider.TryGetComponent<PlatformEffector2D>(out var platform) && platform.useOneWay) continue;
                rise = Mathf.Min(rise, Mathf.Max(0f, hit.distance - Skin));
            }
            return rise;
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

        private bool FindFloor(Vector2 feet, float below, out RaycastHit2D floor, float above = StepHeight)
        {
            int count = Physics2D.Raycast(feet + Vector2.up * above, Vector2.down, terrain, hits, below + above);
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
            fallSpeed = launchGravity = 0f;
            return feet;
        }
    }
}
