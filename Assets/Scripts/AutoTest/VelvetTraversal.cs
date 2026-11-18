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
        private string lastSewn;

        internal bool MoveTo(float x, float tolerance = .14f)
        {
            float dx = x - Player.Body.position.x;
            if (Mathf.Abs(dx) <= tolerance) return true;
            keys.Hold(dx < 0 ? KeyCode.A : KeyCode.D);
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

        private bool SewReturnRoute()
        {
            var room = Area.Room;
            bool upper = seamIndex >= room.singleSeams;
            if (upper && Area.RouteTarget != null) return false; // Bind, bind, then cut each guard.
            if (seamIndex >= room.stitchAnchors)
                throw new InvalidOperationException("Return waterway exhausted before mastery");
            if (upper && (Player.Body.position.y < room.Surface("balcony").rect.y - .1f || stairRoom == room.id))
            {
                if (!ReachBalcony()) return true;
            }
            var run = room.seamRuns[upper ? 1 : 0];
            float x = run.x + (upper ? seamIndex - room.singleSeams : seamIndex) * run.stepX;
            string id = room.id + ":seam_" + seamIndex;
            if (lastSewn == id || lastSewn == id + ":support")
            { seamIndex++; return true; }
            var seam = StitchSurface2D.All.Find(s => s.TargetId == id);
            if (seam == null) throw new InvalidOperationException("Missing return seam " + id);
            if (seam.Installed)
            {
                // Move onto this installation. Stage two needs the actual supporting collider.
                Walk(x, .15f);
                return true;
            }
            float side = upper ? 1 : -1;
            if (!Walk(x + side * 1.70f, .12f)) return true;
            if ((x - Player.Body.position.x) * Player.Facing < 0)
            { keys.Hold(upper ? KeyCode.A : KeyCode.D); return true; }
            if (Player.Grounded) keys.Cast();
            return true;
        }
    }
}
