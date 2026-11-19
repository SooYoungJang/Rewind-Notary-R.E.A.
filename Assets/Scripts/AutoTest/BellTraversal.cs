using UnityEngine;

namespace RewindNotary
{
    internal sealed partial class FirstDepartureBot
    {
        private float nextForge;
        internal void ObserveBellCast(UseContext use)
        {
            if (use.SkillId == BenefactorIds.Forge)
                nextForge = Time.time + Session.Progression.Catalog.Get(use.SkillId).Profile(use.Stage).cooldown;
        }

        internal void BellRefuge(BellWaveDefinition wave)
        {
            if (!Area.BellWaveActive) { ClockPosition(wave.surface, wave.atX); return; }
            if (Area.StandingOn(wave.refuge)) { MoveTo(wave.atX, .16f); return; }
            if (Area.StandingOn(wave.surface))
            {
                if (Walk(wave.atX, .16f)) keys.Hold(KeyCode.Space);
            }
            else if (!Player.Grounded)
            {
                keys.Hold(KeyCode.Space);
                MoveTo(wave.atX, .16f);
            }
            else ClockPosition(wave.refuge, wave.atX);
        }

        internal bool BellDoor(StoryDoor door)
        {
            if (Area.Room.stage != "bell") return ClockDoor(door);
            var reach = door.approach;
            return TryFaceAt(door.x, reach.y + reach.height / 2, out var face) &&
                ClockPosition(face.id, door.x) && Area.OnReturnFloor && door.InReach(Player.Body.position);
        }

        internal void BellStrike(string surface, float x, int direction)
        {
            float launch = x - 1.1f * direction;
            var feet = Player.Body.position;
            float dy = feet.y - Area.Room.SurfaceTop(surface);
            if (!Player.Grounded && dy > .05f && dy < 1f && Mathf.Abs(feet.x - launch) < .3f && Player.Facing == direction)
            {
                if (!Player.BenefactorSkills.BlocksActions && Time.time >= nextForge) keys.Cast(0, -1);
                return;
            }
            if (!Area.StandingOn(surface)) { ClockPosition(surface, launch); return; }
            if (Player.Stunned || Player.Attacking || Player.Defense.BlocksActions || Player.BenefactorSkills.BlocksActions ||
                Time.time < nextForge) return;
            // Leave space to approach the plate facing it. A raycast walk would jump at the sealed slab.
            float aim = Player.Facing != direction && (launch - feet.x) * direction < .35f ? launch - .55f * direction : launch;
            if (MoveTo(aim, .16f) && Player.Facing == direction) keys.Tap(KeyCode.Space);
        }

        internal void BellFight(WaveTarget2D target)
        {
            var at = target.transform.position;
            if (Area.BellPlatePending(at) || !TryFaceAt(at.x, at.y, out var face)) return;
            if (target.State.NeedsSetup && !target.State.Ready(Time.time))
            {
                if (ReserveWaveParry(target)) return;
                int direction = at.x - face.rect.x < 1.8f ? -1 : 1;
                BellStrike(face.id, at.x, direction);
            }
            else if (ReachedCombatSurface(face, at.x)) FightTarget(target);
        }
    }
}
