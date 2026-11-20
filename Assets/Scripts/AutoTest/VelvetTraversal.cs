using System;
using UnityEngine;

namespace RewindNotary
{
    internal sealed partial class FirstDepartureBot
    {
        private string stairRoom;
        private int nextTread;
        private bool jumping;
        private int seamIndex;
        private string lastSewn, sewingRoom;
        private bool rodeFloat, leavingFloat;


        internal bool MoveTo(float x, float tolerance = .14f)
        {
            float dx = x - Player.Body.position.x;
            if (Mathf.Abs(dx) <= tolerance) return true;
            keys.Hold(dx < 0 ? KeyCode.LeftArrow : KeyCode.RightArrow);
            return false;
        }

        // Follow the leading edge of each tread, including flights running to the west.
        internal bool ReachBalcony()
        {
            var room = Area.Room;
            if (Area.StandingOn("balcony"))
            {
                stairRoom = null;
                return true;
            }
            var first = room.Surface("tread_1").rect;
            var second = room.Surface("tread_2").rect;
            int direction = second.x > first.x ? 1 : -1;
            if (stairRoom != room.id)
            {
                stairRoom = room.id;
                nextTread = 0;
                jumping = false;
            }
            if (Area.StandingOn("tread_2") && nextTread <= 2)
            { nextTread = 3; jumping = false; return false; }
            if (Area.StandingOn("tread_1") && nextTread <= 1)
            { nextTread = 2; jumping = false; return false; }
            if (Player.Grounded && Player.Body.position.y < .65f && nextTread > 1)
            { nextTread = 0; jumping = false; }
            if (nextTread == 0)
            {
                float start = direction > 0 ? first.x - 1.5f : first.x + first.width + 1.5f;
                if (Walk(start, .18f) && Player.Grounded) nextTread = 1;
                return false;
            }
            var target = nextTread == 1 ? first : nextTread == 2 ? second : room.Surface("balcony").rect;
            float inset = Mathf.Min(1.35f, target.width * .5f);
            MoveTo(direction > 0 ? target.x + inset : target.x + target.width - inset);
            if (Player.Grounded || jumping) { jumping = true; keys.Hold(KeyCode.Space); }
            return false;
        }

        internal void ObserveSilver(ProgressionEvent e)
        {
            if (e.kind == "valid_use" && e.skillId == SkillRules.SilverStitch)
                lastSewn = e.targetId;
        }

        private bool SewReturnRoute(bool collectUpperSupports)
        {
            var room = Area.Room;
            if (room.id != sewingRoom)
            {
                sewingRoom = room.id;
                seamIndex = 0;
                lastSewn = null;
                rodeFloat = leavingFloat = false;
            }
            var target = Area.RouteTarget;
            bool water = room.id == "velvet_05_return";
            bool upperRoute = collectUpperSupports && room.singleSeams == 0;
            if (seamIndex == room.stitchAnchors || room.id == "velvet_03_gallery" && !upperRoute)
            {
                if (!ReturnBankReady(room)) return true;
                if (target != null && !upperRoute) return false;
                if (water && !Has("scene_floor:velvet:waterway_splice_floor"))
                {
                    var mark = Array.Find(room.floors, f => f.contactSave);
                    ClockPosition(mark.surface, mark.x);
                    return true;
                }
                TakeReturnDoor();
                return true;
            }
            return SewReturnSeam(room, target, water);
        }

        private bool ReturnBankReady(StoryRoom room)
        {
            if (seamIndex <= room.singleSeams) return true;
            int last = seamIndex - 1;
            string id = room.id + ":seam_" + last;
            var span = StitchSurface2D.All.Find(s => s.TargetId == id);
            if (span.Available) return true;
            var crossing = room.SeamRun(last, out int offset);
            float bank = crossing.x + crossing.stepX * offset + Mathf.Sign(crossing.stepX) * 1.85f;
            MoveTo(bank, .10f);
            return false;
        }

        private bool SewReturnSeam(StoryRoom room, WaveTarget2D target, bool water)
        {
            var run = room.SeamRun(seamIndex, out int offset);
            float x = run.x + offset * run.stepX;
            if (target != null && (water && target.transform.position.x < x - 2.2f ||
                room.singleSeams > 0 && seamIndex >= room.singleSeams)) return false;
            if (water && seamIndex == room.singleSeams - 1 && !rodeFloat)
            {
                if (leavingFloat && Area.StandingOn("wheel_gallery")) rodeFloat = true;
                else if (leavingFloat)
                {
                    MoveTo(x);
                    keys.Hold(KeyCode.Space);
                }
                else if (RideCounterweight("counterfloat", "lock_bed", Area.Room.SurfaceTop("wheel_gallery") - 1.2f))
                {
                    leavingFloat = true;
                    keys.Hold(KeyCode.Space);
                }
                return true;
            }
            string id = room.id + ":seam_" + seamIndex;
            int direction = run.stepX > 0f ? 1 : -1;
            bool support = lastSewn == id + ":support";
            if (lastSewn == id || support)
            {
                if (support)
                {
                    float land = x + direction * 1.85f;
                    if (!TryFaceAt(land, run.y - .24f, out var lip)) return true;
                    if (!OnSurface(lip)) { MoveTo(land, .10f); return true; }
                }
                seamIndex++;
                return true;
            }
            var seam = StitchSurface2D.All.Find(s => s.TargetId == id);
            if (seam == null) throw new InvalidOperationException("Missing return seam " + id);
            if (seam.Installed)
            {
                // Move onto this installation. Stage two needs the actual supporting collider.
                Walk(x, .15f);
                return true;
            }
            // Approach from the fixed cloth, clear of the previous installation's retirement area.
            const float maxCastLead = 1.85f;
            float stance = x - direction * 1.70f;
            if (!TryFaceAt(stance, run.y - .24f, out var face)) return true;
            if (!OnSurface(face))
            {
                ClockPosition(face.id, stance);
                return true;
            }
            float lead = (x - Player.Body.position.x) * direction;
            if (lead >= maxCastLead)
                Walk(x, 0f);
            else if (lead <= seam.Bounds.extents.x || Player.Facing != direction)
                // Back out before turning. The turn itself moves Rea one or more physics ticks.
                keys.Hold(direction > 0 ? KeyCode.LeftArrow : KeyCode.RightArrow);
            else if (Player.Grounded)
                keys.Cast();
            return true;
        }
        private void TakeReturnDoor()
        {
            var door = Area.ForwardDoor;
            if (door != null && TryFaceAt(door.x, door.approach.y + door.approach.height / 2, out var exit) &&
                ClockPosition(exit.id, door.x) && Area.OnReturnFloor && door.InReach(Player.Body.position)) keys.Tap(GameInput.InteractKey);
        }
        internal bool RideCounterweight(string carrierId, string bank, float dismountHeight)
        {
            var carrier = Area.Room.Surface(carrierId);
            float x = carrier.rect.x + carrier.rect.width / 2;
            var deck = Area.ProbeSurfaceBounds(carrier);
            if (Area.RidingSurface(carrier) && Player.Grounded && Mathf.Abs(Player.Body.position.y - deck.max.y) <= .12f)
            {
                MoveTo(x);
                return deck.max.y >= dismountHeight;
            }
            ClockPosition(bank, x);
            return false;
        }
    }
}
