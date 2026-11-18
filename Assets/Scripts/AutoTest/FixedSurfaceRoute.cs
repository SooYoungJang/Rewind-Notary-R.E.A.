using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    // A directed edge is one ordinary keyboard manoeuvre between two fixed supports.
    internal sealed class FixedSurfaceRoute
    {
        internal readonly struct Step
        {
            internal readonly int To, WalkOff;
            internal readonly float LaunchX, LandX;
            internal Step(int to, float launch, float land, int walkOff = 0)
            { To = to; LaunchX = launch; LandX = land; WalkOff = walkOff; }
        }

        internal const float AimTolerance = .14f;
        private readonly StorySurface[] surfaces;
        private readonly Bounds[] bounds;
        private readonly List<Step>[] links;
        private readonly float radius, height, gravity, dt;
        private readonly float west = float.NegativeInfinity, east = float.PositiveInfinity;
        internal static bool IsFloor(StorySurface s) => !s.Moving && !s.wallJump && s.rampRise == 0;

        internal FixedSurfaceRoute(StorySurface[] terrain, Func<StorySurface, Bounds> getBounds,
            Vector2 bodySize, float gravity, float dt)
        {
            surfaces = terrain;
            bounds = Array.ConvertAll(terrain, s => getBounds(s));
            radius = bodySize.x / 2;
            height = bodySize.y;
            this.gravity = gravity;
            this.dt = dt;
            // BuildRoom also supplies solid end walls. A tread extending behind a wall has no exit there.
            for (int i = 0; i < terrain.Length; i++)
            {
                if (terrain[i].id == "west_wall") west = bounds[i].max.x + radius;
                if (terrain[i].id == "east_wall") east = bounds[i].min.x - radius;
            }
            links = new List<Step>[terrain.Length];
            for (int from = 0; from < terrain.Length; from++)
            {
                links[from] = new List<Step>();
                if (!IsFloor(terrain[from])) continue;
                for (int to = 0; to < terrain.Length; to++)
                    if (to != from && IsFloor(terrain[to]) && TryLink(from, to, out var step))
                        links[from].Add(step);
                // Prefer the smaller height change on equally short paths (e.g. descending galleries).
                float top = bounds[from].max.y;
                links[from].Sort((a, b) => Mathf.Abs(bounds[a.To].max.y - top)
                    .CompareTo(Mathf.Abs(bounds[b.To].max.y - top)));
            }
        }

        internal bool Next(int from, int goal, out Step first)
        {
            var visited = new bool[links.Length];
            var queue = new Queue<(int node, Step first)>();
            visited[from] = true;
            queue.Enqueue((from, default));
            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                foreach (var link in links[path.node])
                {
                    if (visited[link.To]) continue;
                    first = path.node == from ? link : path.first;
                    if (link.To == goal) return true;
                    visited[link.To] = true;
                    queue.Enqueue((link.To, first));
                }
            }
            first = default;
            return false;
        }

        private bool TryLink(int from, int to, out Step step)
        {
            var a = bounds[from];
            var b = bounds[to];
            float inset = radius + AimTolerance;
            float landingLeft = Mathf.Max(west, b.min.x + inset);
            float landingRight = Mathf.Min(east, b.max.x - inset);
            float left = Mathf.Max(a.min.x + inset, landingLeft);
            float right = Mathf.Min(a.max.x - inset, landingRight);
            step = default;
            if (a.size.x <= 2 * inset || landingLeft >= landingRight) return false;

            // A standing jump through a one-way tread needs an unobstructed column.
            if (b.max.y > a.max.y && surfaces[to].oneWay && left <= right)
            {
                foreach (float x in new[] { (left + right) / 2, left, right })
                {
                    step = new Step(to, x, x);
                    if (LandsOn(from, step)) return true;
                }
            }
            for (int dir = -1; dir <= 1; dir += 2)
            {
                float lip = dir < 0 ? a.min.x : a.max.x;
                float land = Mathf.Clamp(lip + dir * (1f + inset), landingLeft, landingRight);
                if ((land - lip) * dir <= inset) continue;
                if (b.max.y < a.max.y)
                {
                    step = new Step(to, lip, land, dir);
                    if (LandsOn(from, step)) return true;
                }
                // A separated ledge can also be reached by steering a full jump from the lip.
                step = new Step(to, lip - dir * inset, land);
                if (LandsOn(from, step)) return true;
            }
            return false;
        }

        // Use the controller's fixed tick, full jump, air steering and released-stick brake.
        // Reject a wall/underside collision or a different first landing along the approach.
        private bool LandsOn(int from, Step step)
        {
            if (step.LaunchX < west || step.LaunchX > east) return false;
            bool drop = step.WalkOff != 0;
            float x = step.LaunchX + step.WalkOff * radius;
            float y = bounds[from].max.y;
            float vx = step.WalkOff * PlayerController2D.MoveSpeed;
            float vy = drop ? 0 : PlayerController2D.JumpSpeed;
            do
            {
                float dx = step.LandX - x;
                float axis = Mathf.Abs(dx) <= AimTolerance ? 0 : Mathf.Sign(dx);
                vx = Mathf.MoveTowards(vx, axis * PlayerController2D.MoveSpeed,
                    (axis == 0 ? CombatMotor2D.AirBrake : CombatMotor2D.AirAcceleration) * dt);
                x += vx * dt;
                if (x < west || x > east) return false;
                float prevY = y;
                vy = Mathf.Max(vy, -18f) - gravity * dt;
                y += vy * dt;
                int landed = -1;
                for (int i = 0; i < bounds.Length; i++)
                {
                    if (surfaces[i].Moving || surfaces[i].rampRise != 0) continue;
                    var box = bounds[i];
                    if (x + radius <= box.min.x || x - radius >= box.max.x) continue;
                    if (vy < 0 && prevY >= box.max.y && y <= box.max.y)
                    {
                        if (landed < 0 || box.max.y > bounds[landed].max.y) landed = i;
                    }
                    else if (!surfaces[i].oneWay && y < box.max.y && y + height > box.min.y) return false;
                }
                if (landed >= 0)
                    return landed == step.To && x > bounds[landed].min.x + AimTolerance &&
                        x < bounds[landed].max.x - AimTolerance;
            } while (vy > 0 || y >= bounds[step.To].max.y);
            return false;
        }
    }
}
