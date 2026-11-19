using UnityEngine;

namespace RewindNotary
{
    internal sealed partial class FirstDepartureBot
    {
        internal bool GalleryDoor(StoryDoor door)
        {
            if (Area.Room.stage != "gallery") return ClockDoor(door);
            var reach = door.approach;
            return TryFaceAt(door.x, reach.y + reach.height / 2, out var face) &&
                ClockPosition(face.id, door.x) && Area.OnReturnFloor && door.InReach(Player.Body.position);
        }

        private bool GalleryAim(float x, int facing)
        {
            float dx = x - Player.Body.position.x;
            if (Player.Facing != facing && dx * facing < .35f) x -= .7f * facing;
            return MoveTo(x, .10f) && Player.Facing == facing && Mathf.Abs(Player.Body.linearVelocity.x) < .2f;
        }

        internal void PaintGap(StoryMark mark, string station)
        {
            var stroke = PaintSurface2D.All.Find(p => p.StationId == station && p.Live);
            if (stroke != null)
            {
                if (Area.StandingOn(mark.surface)) keys.Hold(KeyCode.Space);
                if (!Player.Grounded) keys.Hold(KeyCode.Space);
                MoveTo(mark.x, .10f);
                return;
            }
            float launch = mark.x - mark.direction * 1.55f;
            if (!Area.StandingOn(mark.surface))
            {
                if (Area.Room.seamId != null && Mathf.Abs(Player.Body.position.x - launch) < 3)
                    MoveTo(launch, .10f);
                else ClockPosition(mark.surface, launch);
                return;
            }
            if (GalleryAim(launch, mark.direction) && !Player.GetComponent<CanvasCarveSkill>().Active)
                keys.Cast(0, -1);
        }

        internal bool LeavePaint(StoryMark mark)
        {
            float x = mark.x + mark.direction * 2.25f;
            return MoveTo(x) && Area.StandingOn(mark.landingSurface);
        }

        internal void SewGallerySeam()
        {
            var room = Area.Room;
            var sewn = StitchSurface2D.All.Find(s => s.TargetId == room.seamId + "_0" && s.Installed);
            if (sewn != null)
            {
                if (Area.StandingOn("terrace_0_0") || !Player.Grounded) keys.Hold(KeyCode.Space);
                MoveTo(sewn.transform.position.x);
                return;
            }
            var seam = room.seamRuns[0];
            float launch = seam.x - 1.45f;
            if (!Area.StandingOn("terrace_0_0")) { ClockPosition("terrace_0_0", launch); return; }
            if (GalleryAim(launch, 1)) keys.Cast();
        }

        internal void GalleryFight(WaveTarget2D target)
        {
            var at = target.transform.position;
            if (!TryFaceAt(at.x, at.y, out var face)) return;
            bool setup = target.State.NeedsSetup && !target.State.Ready(Time.time);
            // Floor-height strokes can remain underfoot for three seconds after a setup.
            bool onStroke = PaintSurface2D.All.Exists(p => !p.Wall && p.GetComponent<Collider2D>().IsTouching(Player.GetComponent<Collider2D>()) &&
                Mathf.Abs(p.Bounds.max.y - at.y) < .5f && Player.Body.position.x >= face.rect.x && Player.Body.position.x <= face.rect.x + face.rect.width);
            if (!onStroke && !ReachedCombatSurface(face, at.x)) return;
            if (!setup) { FightTarget(target); return; }
            if (ReserveWaveParry(target)) return;
            int direction = at.x - face.rect.x >= face.rect.x + face.rect.width - at.x ? 1 : -1;
            if (PaintSurface2D.All.Exists(p => !p.Wall && Mathf.Abs(p.Bounds.max.y - at.y) < .5f &&
                p.Bounds.min.x < at.x && p.Bounds.max.x > at.x))
            {
                // A retired brush stays physical while an occupant is within support tolerance.
                // Step off its near edge before asking for another activation on this guard.
                MoveTo(at.x - direction * 2.4f);
                return;
            }
            if (GalleryAim(at.x - direction * 1.45f, direction) && Player.Grounded && !Player.GetComponent<CanvasCarveSkill>().Active)
                keys.Cast(0, -1);
        }
    }
}
