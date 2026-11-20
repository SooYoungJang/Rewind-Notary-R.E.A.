using UnityEngine;

namespace RewindNotary
{
    internal sealed partial class FirstDepartureBot
    {
        private bool BackOffRetiredPaint()
        {
            var feet = Player.Body.position;
            foreach (var paint in PaintSurface2D.All)
            {
                var brush = paint.Bounds;
                if (paint.Live || paint.Wall || feet.y >= brush.min.y ||
                    brush.max.y - feet.y > SkillGeometry.SupportTolerance) continue;
                int direction = feet.x < brush.min.x ? -1 : feet.x > brush.max.x ? 1 : 0;
                float edge = direction < 0 ? brush.min.x : brush.max.x;
                if (direction == 0 || Mathf.Abs(feet.x - edge) > SkillGeometry.SupportTolerance) continue;

                var floor = FixedFaceAt(feet.x, feet.y);
                float clearX = edge + direction * (SkillGeometry.SupportTolerance + FixedSurfaceRoute.AimTolerance);
                if (floor == null || clearX < floor.rect.x || clearX > floor.rect.x + floor.rect.width) continue;
                keys.Hold(direction < 0 ? KeyCode.LeftArrow : KeyCode.RightArrow);
                return true;
            }
            return false;
        }

        internal bool GalleryDoor(StoryDoor door)
        {
            if (Area.Room.stage != "gallery") return ClockDoor(door);
            var reach = door.approach;
            if (!TryFaceAt(door.x, reach.y + reach.height / 2, out var face)) return false;
            var floor = Area.ProbeSurfaceBounds(face);
            float radius = Player.GetComponent<CapsuleCollider2D>().bounds.extents.x;
            float clearance = Mathf.Max(radius, SkillGeometry.SupportTolerance) + FixedSurfaceRoute.AimTolerance;
            foreach (var paint in PaintSurface2D.All)
            {
                var brush = paint.Bounds;
                if (paint.Wall || brush.min.y <= floor.max.y || brush.max.y > reach.y + reach.height ||
                    reach.x + reach.width + radius < brush.min.x || reach.x - radius > brush.max.x) continue;

                float left = brush.min.x - floor.min.x;
                float right = floor.max.x - brush.max.x;
                if (Mathf.Max(left, right) <= 2 * clearance) continue;
                float dryX = left > right ? (floor.min.x + brush.min.x) / 2 : (brush.max.x + floor.max.x) / 2;
                ClockPosition(face.id, dryX, FixedSurfaceRoute.AimTolerance);
                return false;
            }
            return ClockPosition(face.id, door.x) && Area.OnReturnFloor && door.InReach(Player.Body.position);
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
            if (AimCastFrom(launch, mark.direction) && !Player.GetComponent<CanvasCarveSkill>().Active)
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
            if (AimCastFrom(launch, 1)) keys.Cast();
        }

        private double? pendingBridgeTick;

        internal void GalleryFight(WaveTarget2D target)
        {
            // A sword cut changes the wave in Update. FixedUpdate publishes the solid bridge.
            if (Area.GalleryBridgeOpening())
            {
                pendingBridgeTick ??= Time.fixedTimeAsDouble;
                if (pendingBridgeTick == Time.fixedTimeAsDouble) return;
            }
            else pendingBridgeTick = null;

            var at = target.transform.position;
            if (!TryFaceAt(at.x, at.y, out var face)) return;
            bool setup = target.State.NeedsSetup && !target.State.Ready(Time.time);
            // Floor-height strokes can remain underfoot for three seconds after a setup.
            bool onStroke = PaintSurface2D.All.Exists(p => !p.Wall && p.GetComponent<Collider2D>().IsTouching(Player.GetComponent<Collider2D>()) &&
                Mathf.Abs(p.Bounds.max.y - at.y) < .5f && Player.Body.position.x >= face.rect.x && Player.Body.position.x <= face.rect.x + face.rect.width);
            if (!onStroke && !ReachedCombatSurface(face, at.x)) return;
            if (!setup) { FightTarget(target); return; }
            if (ReserveWaveDodge(target)) return;
            int direction = at.x - face.rect.x >= face.rect.x + face.rect.width - at.x ? 1 : -1;
            if (PaintSurface2D.All.Exists(p => !p.Wall && Mathf.Abs(p.Bounds.max.y - at.y) < .5f &&
                p.Bounds.min.x < at.x && p.Bounds.max.x > at.x))
            {
                // A retired brush stays physical while an occupant is within support tolerance.
                // Step off its near edge before asking for another activation on this guard.
                MoveTo(at.x - direction * 2.4f);
                return;
            }
            if (AimCastFrom(at.x - direction * 1.45f, direction) && Player.Grounded && !Player.GetComponent<CanvasCarveSkill>().Active)
                keys.Cast(0, -1);
        }
    }
}
