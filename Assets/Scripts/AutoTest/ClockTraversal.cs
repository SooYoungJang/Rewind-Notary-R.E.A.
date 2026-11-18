using System;
using UnityEngine;

namespace RewindNotary
{
    internal sealed partial class FirstDepartureBot
    {
        private string jumpingFrom;
        private float jumpStarted;
        private GameObject plannedLayout;
        private StorySurface[] terrain;
        private FixedSurfaceRoute route;
        private FixedSurfaceRoute.Step step;
        private int stepFrom = -1, stepGoal = -1;

        internal WaveTarget2D ClockTarget => Area.UnclearedClockTarget;

        private bool FailRoute(string reason)
        {
            stop(reason);
            return false;
        }

        private bool OnSurface(StorySurface s) => Area.StandingOn(s.id) || Area.StandingOnGeneratedFloor(s);

        private void ClearStep()
        {
            stepFrom = stepGoal = -1;
            jumpingFrom = null;
        }

        private void ReadTerrain()
        {
            if (plannedLayout == Area.ProbeLayout) return;
            ClearStep();
            terrain = Area.ProbeTerrain();
            route = null;
            plannedLayout = Area.ProbeLayout;
        }

        internal bool ClockPosition(string face, float x, float tolerance = .24f)
        {
            if (Time.time < jumpUntil) keys.Hold(KeyCode.Space);
            var room = Area.Room;
            ReadTerrain();
            int goal = Array.FindIndex(terrain, s => s.id == face);
            if (goal < 0) return FailRoute("Missing fixed face " + face + " in " + room.id);
            if (route == null)
            {
                // A pendulum dash temporarily disables gravity.
                float gravity = -Physics2D.gravity.y * Player.Body.gravityScale;
                if (gravity <= 0) return false;
                route = new FixedSurfaceRoute(terrain, Area.ProbeSurfaceBounds,
                    Player.GetComponent<CapsuleCollider2D>().bounds.size, gravity, Time.fixedDeltaTime);
            }

            var feet = Player.Body.position;
            int here = Array.FindIndex(terrain, s => FixedSurfaceRoute.IsFloor(s) && OnSurface(s));
            if (here < 0 && !Player.Grounded)
            {
                if (jumpingFrom != null) keys.Hold(KeyCode.Space);
                if (stepFrom >= 0) MoveTo(step.LandX, FixedSurfaceRoute.AimTolerance);
                return false;
            }
            if (here < 0)
            {
                var ramp = Array.Find(terrain, s => s.rampRise != 0 && Area.StandingOnRamp(s));
                if (ramp != null)
                {
                    ClearStep();
                    var r = ramp.rect;
                    bool up = room.SurfaceTop(face) > feet.y;
                    MoveTo(up == (ramp.rampRise > 0) ? r.x + r.width + .6f : r.x - .6f);
                    return false;
                }
                var carrier = Array.Find(terrain, Area.RidingSurface);
                if (carrier != null)
                {
                    // Both optional clock carriers run above a continuous fixed floor. Keep walking
                    // off its live lip even when a goal stands directly above or below the rider.
                    ClearStep();
                    var deck = Area.ProbeSurfaceBounds(carrier);
                    var landing = Area.ProbeSurfaceBounds(terrain[goal]);
                    bool left = landing.center.x < deck.center.x;
                    keys.Hold(left ? KeyCode.A : KeyCode.D);
                    return false;
                }
                // Low fixed story planks (including the timed return plank) overlay a walkable floor.
                here = Array.FindIndex(terrain, s => FixedSurfaceRoute.IsFloor(s) &&
                    feet.x >= s.rect.x && feet.x <= s.rect.x + s.rect.width &&
                    feet.y >= s.rect.y + s.rect.height - .1f && feet.y <= s.rect.y + s.rect.height + .5f);
                if (here < 0)
                {
                    if (stepFrom >= 0)
                    {
                        if (jumpingFrom != null) keys.Hold(KeyCode.Space);
                        if (step.WalkOff != 0) keys.Hold(step.WalkOff < 0 ? KeyCode.A : KeyCode.D);
                        else MoveTo(step.LandX);
                    }
                    return false;
                }
            }
            if (jumpingFrom != null && (jumpingFrom != terrain[here].id || Time.time - jumpStarted > 1))
            { ClearStep(); return false; } // Release Space before another takeoff.
            if (here == goal) { ClearStep(); return Walk(x, tolerance); }

            if (stepFrom != here || stepGoal != goal)
            {
                if (!route.Next(here, goal, out step))
                    return FailRoute("No fixed route in " + room.id + " from " + terrain[here].id + " to " + face);
                stepFrom = here;
                stepGoal = goal;
            }
            if (step.WalkOff != 0)
            {
                // A walk-off ends when support changes, even if the nominal landing x is underfoot.
                keys.Hold(step.WalkOff < 0 ? KeyCode.A : KeyCode.D);
            }
            else if (jumpingFrom != null)
            {
                keys.Hold(KeyCode.Space);
                MoveTo(step.LandX, FixedSurfaceRoute.AimTolerance);
            }
            else if (Walk(step.LaunchX, FixedSurfaceRoute.AimTolerance) && Mathf.Abs(Player.Body.linearVelocity.x) < .2f)
            {
                jumpingFrom = terrain[here].id;
                jumpStarted = Time.time;
                keys.Hold(KeyCode.Space);
                MoveTo(step.LandX, FixedSurfaceRoute.AimTolerance);
            }
            return false;
        }

        private StorySurface FixedFaceAt(float x, float y)
        {
            ReadTerrain();
            StorySurface nearest = null;
            float distance = .5f;

            foreach (var s in terrain)
            {
                if (!FixedSurfaceRoute.IsFloor(s) || x < s.rect.x || x > s.rect.x + s.rect.width) continue;
                float dy = Mathf.Abs(s.rect.y + s.rect.height - y);
                if (dy < distance) { nearest = s; distance = dy; }
            }
            return nearest;
        }

        private bool TryFaceAt(float x, float y, out StorySurface face)
        {
            face = FixedFaceAt(x, y);
            if (face == null) return FailRoute("Unsupported position " + new Vector2(x, y) + " in " + Area.Room.id);
            return true;
        }

        private bool ReachedCombatSurface(StorySurface support, float targetX)
        {
            // Hand the keyboard to combat only after the requested support actually catches the actor.
            if (jumpingFrom != null || !OnSurface(support))
            {
                ClockPosition(support.id, targetX);
                return false;
            }
            ClearStep();
            return true;
        }

        internal void ClockFight(WaveTarget2D target)
        {
            var at = target.transform.position;
            if (!TryFaceAt(at.x, at.y, out var face)) return;
            if (!ReachedCombatSurface(face, at.x)) return;
            if (target.State.NeedsSetup && !target.State.Ready(Time.time))
            {
                if (ReserveWaveParry(target)) return;
                // Hit a finite joint from outside its body and recall before the sword cut.
                var ledge = face.rect;
                int side = at.x - ledge.x < 3 ? -1 : ledge.x + ledge.width - at.x < 3 ? 1 :
                    at.x < Player.Body.position.x ? -1 : 1;
                ClockCast(at.x, side);
            }
            else FightTarget(target);
        }
        private float nextClockCast;
        internal void ObserveClockCast(UseContext use)
        {
            if (use.SkillId == BenefactorIds.Pendulum)
                nextClockCast = Time.time + Session.Progression.Catalog.Get(use.SkillId).Profile(use.Stage).cooldown;
        }
        internal void ClockCast(float x, int direction)
        {
            var rewind = Player.GetComponent<PendulumRewindSkill>();
            if (rewind.Active)
            {
                if (!rewind.BlocksActions) keys.Cast(-Player.Facing);
                return;
            }
            float lead = (x - Player.Body.position.x) * direction;
            if (lead > 2.8f) MoveTo(x);
            else if (lead < .85f || Player.Facing != direction) keys.Hold(direction > 0 ? KeyCode.A : KeyCode.D);
            else if (Player.Grounded && Time.time >= nextClockCast)
            {
                keys.Cast(-Player.Facing);
                if (GameInput.CastDown) nextClockCast = Time.time + .22f;
            }
        }

        internal bool ClockDoor(StoryDoor door)
        {
            var box = door.approach;
            bool positioned;
            if (box == null || !box.Defined || Area.Room.Surface("drum_floor") == null)
                positioned = Walk(door.x, .35f);
            else
                positioned = TryFaceAt(door.x, box.y + box.height * .5f, out var face) && ClockPosition(face.id, door.x);
            return positioned && Area.OnReturnFloor && door.InReach(Player.Body.position);
        }

    }
}
