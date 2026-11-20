using UnityEngine;

namespace RewindNotary
{
    internal sealed partial class FirstDepartureBot
    {
        private GameObject crossingRoom;
        private int crossingDirection;
        private bool boarding;

        // The fixed route ends at each bank. Use it to reach the launch lip, then walk the carrier.
        internal bool OperaPosition(string face, float x)
        {
            if (Area.OnOperaLift(out var lift))
            {
                if (Area.PendingAction?.StartsWith("ride:") == true) return true;
                // The next stair can start on the opposite side from the final destination.
                float toward = x;
                if (plannedLayout == Area.ProbeLayout && stepFrom >= 0 && terrain[stepGoal].id == face)
                    toward = jumpingFrom == null && step.WalkOff == 0 ? step.LaunchX : step.LandX;
                MoveTo(toward < lift.center.x ? lift.min.x - 1f : lift.max.x + 1f);
                return false;
            }
            if (crossingRoom != Area.ProbeLayout)
            {
                crossingRoom = Area.ProbeLayout;
                crossingDirection = 0;
            }
            if (Area.Room.Surface("pendant") != null)
            {
                bool west = face == "west_bank" || face == "west_gallery";
                int side = Area.StandingOn("west_bank") || Area.StandingOn("west_gallery") ? -1 :
                    Area.StandingOn("east_gallery") || Area.StandingOn("lantern_step") || Area.StandingOn("fly_grid") ? 1 : 0;
                int destination = west ? -1 : 1;
                if (side == destination) crossingDirection = 0;
                else if (crossingDirection != destination && side == -destination)
                {
                    crossingDirection = destination;
                    boarding = true;
                }
                if (crossingDirection != 0)
                {
                    bool eastbound = crossingDirection > 0;
                    if (boarding)
                    {
                        if (!ClockPosition(eastbound ? "west_gallery" : "east_gallery", eastbound ? 16.25f : 31.25f) ||
                            !Area.ChandelierShaded) return false;
                        boarding = false;
                    }
                    keys.Hold(eastbound ? KeyCode.RightArrow : KeyCode.LeftArrow);
                    return false;
                }
            }
            return ClockPosition(face, x);
        }

        internal bool OperaDoor(StoryDoor door)
        {
            if (Area.Room.stage != "opera") return ClockDoor(door);
            var box = door.approach;
            return TryFaceAt(door.x, box.y + box.height / 2, out var face) &&
                OperaPosition(face.id, door.x) && Area.OnReturnFloor && door.InReach(Player.Body.position);
        }

        internal void OperaFight(WaveTarget2D target)
        {
            var at = target.transform.position;
            if (!TryFaceAt(at.x, at.y, out var face)) return;
            if (!OnSurface(face))
            {
                OperaPosition(face.id, at.x);
                return;
            }
            ClearStep();
            if (ReserveWaveDodge(target)) return;
            if (target.State.NeedsSetup && !target.State.Ready(Time.time))
            {
                if (Aim(at.x) && Player.Grounded) keys.Cast(0, 1);
            }
            else FightTarget(target);
        }
    }
}
