using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class RepresentativeArea
    {
        private readonly List<WaveTarget2D> entryWave = new List<WaveTarget2D>();
        private int entryMember;
        private const string EntryCleared = "velvet_entry_wave_cleared";

        // Existing slots already beyond this door retain their open route.
        private bool NeedsEntryWave => Room.id == "velvet_01_entry" && Has("velvet_entry_complete")
            && !Has(EntryCleared) && !Has("loom_bridge") && !Has("mikhail_defeated") && !Has("investigation_complete");

        private void UpdateEntryWave()
        {
            if (!NeedsEntryWave || !session.LastSaveSucceeded || player.Health.Dead || committed) return;
            if (entryWave.Count == 0)
            {
                var wave = waves.zones[0].waves[0];
                foreach (var spawn in wave.spawns)
                    for (int i = 0; i < spawn.count; i++)
                    {
                        var go = Place(Room.id + ":" + wave.id + ":" + entryWave.Count,
                            8f + spawn.x + entryWave.Count * 2, .05f);
                        var target = go.AddComponent<WaveTarget2D>();
                        target.Initialize(waves.Archetype(spawn.archetypeId), spawn, player, square);
                        go.SetActive(entryWave.Count == 0);
                        entryWave.Add(target);
                    }
            }
            var current = entryWave[entryMember];
            if (!current.State.Cleared || !current.State.EffectsClosed) return;
            current.gameObject.SetActive(false);
            if (++entryMember < entryWave.Count) entryWave[entryMember].gameObject.SetActive(true);
            else
            {
                Put(EntryCleared);
                session.Save();
                RefreshFixtures();
            }
        }

        private bool GatheringSilver => Has("mikhail_i0_complete") && !Record.firstTransferPending;
        private string NextDoorId()
        {
            if (Has("luka_lift_complete") || InEnding || Has("janhyang_open")) return null;
            switch (Room.id)
            {
                case "hub_attic": return "hub_forge";
                case "hub_workroom":
                case "hub_forge":
                    return Has("investigation_complete") && Has("mikhail_i0_complete") && !Has("mikhail_support_first_granted")
                        ? "hub_sanctuary" : "hub_lift";
                case "hub_sanctuary": return "hub_lift";
                case "hub_lift": return "velvet_01_entry";
                case "velvet_01_entry": return "velvet_02_loom";
                case "velvet_02_loom": return "velvet_03_gallery";
                case "velvet_03_gallery": return GatheringSilver ? "velvet_05_return" : "velvet_04_arena";
                case "velvet_04_arena": return "velvet_05_return";
                case "velvet_05_return": return "velvet_01_entry";
                default: return null;
            }
        }
        private string FirstDepartureHint()
        {
            if (NeedsEntryWave && entryMember < entryWave.Count)
            {
                var target = entryWave[entryMember];
                return Loc.Get("wave.progress", StageCatalog.Find("velvet").Name, 1, 3) + "\n" +
                    target.Hint + Arrow(target.transform.position.x);
            }
            if (Room.id == "hub_lift" && Has("luka_lift_complete") && Distance(Room.Point("luka_freight_platform").x) <= 1.25f)
                return Loc.Get("area.travel", Loc.Get("ui.stage_select"));
            if (HasBridge && !Has(BridgeFlag))
            {
                var latch = latches.Find(t => !t.State.Cleared);
                if (latch != null)
                    return Loc.Get("wave.progress", StageCatalog.Find("velvet").Name, Room.id == "velvet_02_loom" ? 2 : 3, 3)
                        + "\n" + Loc.Get("area.latch") + Arrow(latch.transform.position.x);
            }
            if (GatheringSilver && Room.stage == "velvet")
            {
                var guard = roomTargets.Find(t => !t.State.Cleared && t.State.NeedsSetup);
                if (guard != null)
                    return guard.Hint + Arrow(guard.transform.position.x) + "\n" +
                        Loc.Get("input.skill_mapping_row", Loc.Get("input.direction_neutral"), session.Progression.Catalog.Get(SkillRules.SilverStitch).DisplayName);
            }
            string id = NextDoorId();
            var door = Array.Find(Room.exits, e => e.room == id && Gate(e));
            return door == null ? null : Loc.Get("area.travel", Departure(door.room).Label) + Arrow(door.x);
        }
    }
}
