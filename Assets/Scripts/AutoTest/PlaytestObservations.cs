using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class GameShell
    {
        internal int MenuIndex => selected;
        internal bool EmptyNewSlot => purpose == SlotPurpose.NewGame && selected < records.Length &&
            records[selected] == null && slotErrors[selected] == null;
        internal CoreRecord MenuRecord => selectedRecord;
        internal string MenuMessage => message;
    }

    public sealed partial class RepresentativeArea
    {
        internal CombatMotor2D Rea => player;
        internal GameObject ProbeLayout => layout;
        private Collider2D ProbeCollider(StorySurface s) =>
            surfaces.TryGetValue(s.id, out var box) ? box : fixedFloors.Find(f => f.name == s.id);
        internal Bounds ProbeSurfaceBounds(StorySurface s) => ProbeCollider(s).bounds;
        internal bool StandingOnGeneratedFloor(StorySurface s)
        {
            if (surfaces.ContainsKey(s.id)) return false;
            var box = ProbeCollider(s);
            var actor = player.GetComponent<Collider2D>();
            return player.Grounded && box.isActiveAndEnabled && box.IsTouching(actor) &&
                Mathf.Abs(actor.bounds.min.y - box.bounds.max.y) <= .12f;
        }

        internal string ClockSupportDescription()
        {
            if (player == null || !player.Grounded) return "-";
            var actor = player.GetComponent<Collider2D>();
            foreach (var box in surfaces.Values)
            {
                var b = box.bounds;
                if (box.IsTouching(actor) && Mathf.Abs(actor.bounds.min.y - b.max.y) <= .12f)
                    return $"{box.name}[{b.min.x:F2},{b.max.x:F2}]@{b.max.y:F2}";
            }
            return "-";
        }

        internal StorySurface[] ProbeTerrain()
        {
            var faces = new List<StorySurface>(Room.terrain ?? System.Array.Empty<StorySurface>());
            foreach (var box in fixedFloors)
            {
                if (!box.isActiveAndEnabled || box.attachedRigidbody != null || surfaces.ContainsKey(box.name)) continue;
                var b = box.bounds;
                // Low story planks overlay the walking lane and Walk handles their lip.
                if (faces.Exists(s => FixedSurfaceRoute.IsFloor(s) && b.min.x >= s.rect.x &&
                    b.max.x <= s.rect.x + s.rect.width && b.max.y >= s.rect.y + s.rect.height &&
                    b.max.y - (s.rect.y + s.rect.height) < .5f)) continue;
                faces.Add(new StorySurface { id = box.name, wallJump = box.name == "east_wall" || box.name == "west_wall",
                    rect = new StoryRect { x = b.min.x, y = b.min.y, width = b.size.x, height = b.size.y } });
            }
            return faces.ToArray();
        }
        internal bool RidingSurface(StorySurface surface) => surface.Moving &&
            surfaces[surface.id].IsTouching(player.GetComponent<Collider2D>());
        internal CombatMotor2D ClockOpponent => opponent;
        internal WaveTarget2D ClockTargetOnTier => roomTargets.Find(t => !t.State.Cleared &&
            Mathf.Abs(t.transform.position.y - player.Body.position.y) < .2f);
        internal WaveTarget2D UnclearedClockTarget => roomTargets.Find(t => !t.State.Cleared);
        internal WaveTarget2D DistrictWaveTarget => Room == null ? null :
            Room.stage == "clock" ? UnclearedClockTarget : RouteTarget;
        // Inactive targets still belong to the wave, including queued velvet entry enemies.
        internal int WaveTargetCount => roomTargets.Count + entryWave.Count;
        internal int ClearedWaveTargets
        {
            get
            {
                int cleared = 0;
                foreach (var t in roomTargets) if (t.State.Cleared) cleared++;
                foreach (var t in entryWave) if (t.State.Cleared) cleared++;
                return cleared;
            }
        }
        internal CombatMotor2D BoutOpponent => mikhailBout == null ? null : opponent;
        internal BenefactorTrialOpponent Bout => mikhailBout;
        internal string PendingAction => ReadyForAction ? ActionToken : null;
        internal bool OnReturnFloor => Safe && OnFixedFloor();
        internal StoryDoor ForwardDoor
        {
            get
            {
                string next = NextDoorId();
                foreach (var door in Room.exits)
                    if (door.room == next && Gate(door)) return door;
                return null;
            }
        }

        internal WaveTarget2D RouteTarget
        {
            get
            {
                if (NeedsEntryWave && entryMember < entryWave.Count) return entryWave[entryMember];
                if (HasBridge && !Has(BridgeFlag)) return latches.Find(t => !t.State.Cleared);
                if (GatheringSilver) return roomTargets.Find(t => !t.State.Cleared && t.State.NeedsSetup);
                return null;
            }
        }
    }
}
