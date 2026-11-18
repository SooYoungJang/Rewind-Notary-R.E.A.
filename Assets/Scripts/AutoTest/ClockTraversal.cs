using System;
using UnityEngine;

namespace RewindNotary
{
    internal sealed partial class FirstDepartureBot
    {
        private string jumpingFrom;
        private float jumpStarted;
        internal WaveTarget2D ClockTarget => Area.UnclearedClockTarget;
        internal bool ClockPosition(string face, float x, float tolerance = .24f)
        {
            if (Time.time < jumpUntil) keys.Hold(KeyCode.Space);
            var room = Area.Room;
            int goal = Array.FindIndex(room.terrain, s => s.id == face);
            if (goal < 0) throw new InvalidOperationException("Missing clock face " + face);
            var feet = Player.Body.position;
            int here = Array.FindIndex(room.terrain, s => !s.Moving && s.rampRise == 0 && Area.StandingOn(s.id));
            if (here < 0)
            {
                var ramp = Array.Find(room.terrain, s => s.rampRise != 0 && Area.StandingOnRamp(s));
                if (ramp != null)
                {
                    var r = ramp.rect;
                    bool up = room.SurfaceTop(face) > feet.y;
                    MoveTo(up == (ramp.rampRise > 0) ? r.x + r.width + .6f : r.x - .6f);
                    return false;
                }
                // Low story planks share the drum floor's walking route.
                float aboveBase = feet.y - room.SurfaceTop("drum_floor");
                if (Player.Grounded && aboveBase >= -.1f && aboveBase < .5f) here = 0;
                else
                {
                    if (jumpingFrom != null) keys.Hold(KeyCode.Space);
                    else if (Player.Grounded) MoveTo(x); // Step off an optional moving carrier.
                    return false;
                }
            }
            if (jumpingFrom != null && (jumpingFrom != room.terrain[here].id || Time.time - jumpStarted > 1))
            { jumpingFrom = null; return false; } // release for the next press
            if (here == goal) return Walk(x, tolerance);
            var current = room.terrain[here].rect;
            var next = room.terrain[here + Math.Sign(goal - here)].rect;
            if (here > goal)
            {
                // Step off on the side where the next lower gallery continues.
                bool leftOpen = next.x < current.x;
                bool rightOpen = next.x + next.width > current.x + current.width;
                bool left = leftOpen && (!rightOpen || x < current.x + current.width / 2);
                float edge = left ? current.x - 1f : current.x + current.width + 1f;
                MoveTo(edge);
                return false;
            }
            if (here == 0)
            {
                var ramp = Array.Find(room.terrain, s => s.rampRise > 0 && feet.x <= s.rect.x + .5f &&
                    s.rect.y + s.rect.height + s.rampRise <= room.SurfaceTop(face) + .05f);
                if (ramp != null) { Walk(ramp.rect.x + ramp.rect.width + .4f, .24f); return false; }
            }
            float overlap = (Mathf.Max(current.x, next.x) + Mathf.Min(current.x + current.width, next.x + next.width)) / 2;
            if (Walk(overlap, .24f))
            {
                if (jumpingFrom == null) { jumpingFrom = room.terrain[here].id; jumpStarted = Time.time; }
                keys.Hold(KeyCode.Space);
            }
            return false;
        }

        internal string FaceAt(float x, float y)
        {
            var room = Area.Room;
            var face = Array.Find(room.terrain, s => !s.Moving && !s.wallJump && s.rampRise == 0 &&
                x >= s.rect.x && x <= s.rect.x + s.rect.width && Mathf.Abs(room.SurfaceTop(s.id) - y) < .5f);
            if (face == null) throw new InvalidOperationException("Unsupported clock position " + new Vector2(x, y));
            return face.id;
        }

        internal void ClockFight(WaveTarget2D target)
        {
            var at = target.transform.position;
            var face = FaceAt(at.x, at.y);
            if (!AtClockHeight(face)) { ClockPosition(face, at.x); return; }
            if (target.State.NeedsSetup && !target.State.Ready(Time.time))
            {
                if (target.StrikeIn <= .085f) { keys.Tap(KeyCode.L); return; }
                // Hit a finite joint from outside its body and recall before the sword cut.
                var ledge = Area.Room.Surface(face).rect;
                int side = at.x - ledge.x < 3 ? -1 : ledge.x + ledge.width - at.x < 3 ? 1 :
                    at.x < Player.Body.position.x ? -1 : 1;
                ClockCast(at.x, side);
            }
            else FightTarget(target);
        }

        private bool AtClockHeight(string face) => Mathf.Abs(Player.Body.position.y - Area.Room.SurfaceTop(face)) <= .45f;
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
            if (box == null) return Walk(door.x, .35f) && Area.OnReturnFloor;
            return ClockPosition(FaceAt(door.x, box.y + box.height * .5f), door.x) &&
                Area.OnReturnFloor && door.InReach(Player.Body.position);
        }

        internal void ClockBout()
        {
            if (!AtClockHeight("drum_floor")) { ClockPosition("drum_floor", 17); return; }
            Strike(Area.ClockOpponent.Body.position.x);
        }
    }
}
