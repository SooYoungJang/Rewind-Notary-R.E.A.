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
