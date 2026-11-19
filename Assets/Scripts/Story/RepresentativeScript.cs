using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [Serializable] public sealed class StoryRect
    {
        public float x, y, width, height;
        public bool Defined => width > 0 && height > 0;
        public bool Contains(Vector2 feet) => feet.x >= x && feet.x <= x + width &&
            feet.y >= y && feet.y <= y + height;
    }
    [Serializable] public sealed class StorySurface
    {
        public string id;
        public StoryRect rect;
        public bool wallJump, oneWay, bellBeat;
        public int bellLatch, latchCuts;
        public int RequiredCuts => Math.Max(bellLatch, latchCuts);
        // Nonzero travel uses a kinematic body. Static marks never bind to it.
        public float travelX, travelY, period, rampRise;
        public bool Moving => travelX != 0 || travelY != 0;
    }
    [Serializable] public sealed class StoryMark
    {
        public string id, surface, landingSurface;
        public int direction;
        public float x;
        public bool platform, contactSave, projectile;
    }
    [Serializable] public sealed class StoryPlacement
    {
        public string id;
        public float x, y;
        public StoryRect active;
    }
    [Serializable] public sealed class StoryDoor
    {
        public string source, room, gate, requiresFlag, destinationFloor;
        public float x;
        public StoryRect approach;
        public bool waltzCue;
        public bool InReach(Vector2 feet) => approach != null && approach.Defined
            ? approach.Contains(feet) : Mathf.Abs(feet.x - x) <= 1.25f;
    }
    [Serializable] public sealed class StoryRoster
    {
        public string id, setupSkill;
        public string[] placementIds;
        public int count, segments, setupHits;
        public bool skillOnly;
    }
    [Serializable] public sealed class StorySeamRun
    {
        public int count;
        public float x, y, stepX;
    }
    [Serializable] public sealed class BellWaveDefinition
    {
        public string surface, refuge;
        public float atX, windup, speed, height;
    }
    [Serializable] public sealed class ViewingRoute
    {
        public string id;
        public string[] surfaces;
    }
    [Serializable] public sealed class StoryRoom
    {
        public string id, stage, skill, benefactor, crossing, seamId;
        public bool projectileStations;
        public StoryMark[] floors, points, stationMarks;
        public float SurfaceTop(string key)
        {
            var face = Surface(key);
            return face == null ? 0f : face.rect.y + face.rect.height;
        }
        public string backgroundSet;
        public StoryRect cameraBounds;
        public float fallLine;
        public bool hasFallLine;
        public float FallY => hasFallLine ? fallLine : -7f;
        public StorySurface[] terrain;
        public BellWaveDefinition[] bellWaves;
        public ViewingRoute[] viewingRoutes;
        public StoryPlacement[] placements;
        public StorySeamRun[] seamRuns;
        public float eastBoundary;
        public StorySurface Surface(string key) => string.IsNullOrEmpty(key) || terrain == null
            ? null : Array.Find(terrain, s => s.id == key);
        public StoryPlacement Placement(string key) => placements == null ? null : Array.Find(placements, p => p.id == key);
        public StoryDoor[] exits;
        public StoryRoster[] roster;
        public int stitchAnchors, singleSeams, releaseKnots, stations, boutHealth, interludeHealth;
        public string Label => Loc.Get("area.room." + id);
        public StoryMark Point(string key) => Array.Find(points, p => p.id == key);
        public StoryMark Floor(string key) => Array.Find(floors, p => p.id == key);
    }
    [Serializable] public sealed class StoryRow
    {
        public string sceneId, stepId, room, alternateRoom, speakerId, textKey, completionFlag, resumePoint, effect;
        public string receipt, presentation;
        public string Receipt => string.IsNullOrEmpty(receipt) ? completionFlag : receipt;
        public string[] entryCondition, requiredPlayerAction;
        public string ReadFlag => "read:" + textKey;
        public bool DialogueOnly => requiredPlayerAction[0] == "confirm";
        public bool SpansWaves => requiredPlayerAction[0].StartsWith("advance:", StringComparison.Ordinal);
    }
    [Serializable] public sealed class RepresentativeScript
    {
        private static readonly string[] Districts = { "clock", "opera", "bell", "gallery" };
        public StoryRoom[] rooms, roomAdditions;
        public StoryRow[] rows;
        public StoryDoor[] links;
        private StoryRow[] interrupts, finaleRows;
        public StoryRoom Room(string id) => Array.Find(rooms, r => r.id == id);
        public static bool IsDistrict(string stage) => stage == "velvet" || Array.IndexOf(Districts, stage) >= 0;
        public static bool Handles(string stage) => stage == "hub" || stage == "janhyang" || IsDistrict(stage);
        private static RepresentativeScript Read(string name) =>
            JsonUtility.FromJson<RepresentativeScript>(Resources.Load<TextAsset>("Story/" + name).text);

        public static RepresentativeScript Load()
        {
            var script = Read("representative");
            var rooms = new List<StoryRoom>(script.rooms);
            var rows = new List<StoryRow>(script.rows);
            foreach (string district in Districts)
            {
                var data = Read(district);
                rooms.AddRange(data.rooms);
                rows.AddRange(data.rows);
                foreach (var door in data.links)
                {
                    var hub = script.Room(door.source);
                    Array.Resize(ref hub.exits, hub.exits.Length + 1);
                    hub.exits[hub.exits.Length - 1] = door;
                }
            }
            var finale = Read("finale");
            script.finaleRows = finale.rows;
            rooms.AddRange(finale.rooms);
            rows.AddRange(finale.rows);
            foreach (var addition in finale.roomAdditions)
            {
                var hub = script.Room(addition.id);
                var points = new List<StoryMark>(hub.points);
                points.AddRange(addition.points);
                hub.points = points.ToArray();
                var floors = new List<StoryMark>(hub.floors);
                floors.AddRange(addition.floors);
                hub.floors = floors.ToArray();
            }
            foreach (var door in finale.links)
            {
                var hub = script.Room(door.source);
                Array.Resize(ref hub.exits, hub.exits.Length + 1);
                hub.exits[hub.exits.Length - 1] = door;
            }
            script.rooms = rooms.ToArray();
            script.rows = rows.ToArray();
            script.interrupts = rows.FindAll(r => r.room == "*").ToArray();
            return script;
        }

        internal static bool CourtEntry(CoreRecord record)
        {
            return record != null && record.storyFlags.Contains("janhyang_open")
                && record.storyFlags.Contains(InvestigationService.MiddleInvestigationFlag)
                && record.investigation.readStageIds.Contains(InvestigationCatalog.Load().finalStageId)
                && Array.TrueForAll(BenefactorIds.All, id => record.Get(id).acquired)
                && record.ledger.FindAll(e => e.transferredToRea && e.ownerId == "rea").Count == 5;
        }

        public static string FirstSkill(CoreRecord record)
        {
            const string prefix = "first_mastery:";
            var flag = record.storyFlags.Find(f => f.StartsWith(prefix, StringComparison.Ordinal));
            return flag?.Substring(prefix.Length) ?? record.ledger.Find(e => e.transferredToRea)?.skillId;
        }

        private static bool Eligible(StoryRow row, InvestigationService investigation, bool safe, bool half)
        {
            var record = investigation.Record;
            bool Meets(string term)
            {
                if (term == "courtEntry") return CourtEntry(record);
                if (term == "lowSword") return record.Get(SkillRules.BasicSword).stage < 3;
                if (term == "trainingAvailable") return record.storyFlags.Contains("ending_basic") || record.storyFlags.Contains("court_lift_ready");
                if (term.StartsWith("settlement:", StringComparison.Ordinal))
                    return term == "settlement:" + FinaleEndingEvaluator.Evaluate(record.finalBattleAttempt);
                if (term == "safe") return safe;
                if (term == "transferred") return record.firstTransferPending && safe;
                if (term == "half") return half;
                if (term == "finalReading") return investigation.FinalReadingComplete;
                if (term.StartsWith("first:", StringComparison.Ordinal)) return FirstSkill(record) == term.Substring(6);
                if (term.StartsWith("clue:", StringComparison.Ordinal)) return investigation.HasClue(term.Substring(5));
                if (term.StartsWith("owned:", StringComparison.Ordinal)) return record.Get(term.Substring(6)).acquired;
                if (term.StartsWith("stage2:", StringComparison.Ordinal)) return record.Get(term.Substring(7)).stage >= 2;
                if (term.StartsWith("stage3:", StringComparison.Ordinal)) return record.Get(term.Substring(7)).stage >= 3;
                if (term[0] == '!') return !record.storyFlags.Contains(term.Substring(1));
                return record.storyFlags.Contains(term);
            }
            return string.IsNullOrEmpty(row.presentation) && !record.storyFlags.Contains(row.Receipt) && Array.TrueForAll(row.entryCondition, Meets);
        }

        // Older v4 builds used the representative crack for every first skill. Preserve that
        // acknowledgement and let the newly available healer cut precede any pending call.
        public bool RestoreTransferReceipt(CoreRecord record)
        {
            if (!record.firstTransferPending || !record.storyFlags.Contains("first_transfer_seen")) return false;
            string skill = FirstSkill(record);
            var transfer = Array.Find(interrupts, r => Array.IndexOf(r.entryCondition, "stage3:" + skill) >= 0);
            if (transfer == null || record.storyFlags.Contains(transfer.completionFlag)) return false;
            var stage2 = Array.Find(interrupts, r => r.sceneId == transfer.sceneId && Array.IndexOf(r.entryCondition, "stage2:" + skill) >= 0);
            if (!record.storyFlags.Contains(stage2.completionFlag)) record.storyFlags.Add(stage2.completionFlag);
            record.storyFlags.Add(transfer.completionFlag);
            return true;
        }

        public StoryRow Interrupt(InvestigationService investigation, bool safe)
        {
            if (!safe) return null;
            var record = investigation.Record;
            bool investigated = record.storyFlags.Contains("investigation_complete");
            if (!investigated && record.storyFlags.Contains("returned_for_mid_invest")) return null;
            bool firstReturn = record.firstTransferPending && !investigated;
            string first = firstReturn ? FirstSkill(record) : null;
            var district = firstReturn ? Array.Find(rooms, r => r.skill == first) : null;
            string firstMastery = district == null ? null : district.stage + "_mastery";
            if (firstReturn && district != null)
            {
                var local = Array.Find(interrupts, r => r.sceneId == firstMastery && Eligible(r, investigation, true, false));
                if (local != null) return local;
            }
            foreach (var row in interrupts)
            {
                if (row.sceneId == "first_transfer" && first != SkillRules.SilverStitch && row.stepId == "01_trigger") continue;
                if (firstReturn && row.sceneId != "first_transfer" && row.sceneId != firstMastery) continue;
                if (Eligible(row, investigation, true, false)) return row;
            }
            return null;
        }

        public StoryRow Next(StoryRoom room, InvestigationService investigation, bool half)
        {
            return Array.Find(rows, r =>
                (r.room == room.id || (r.alternateRoom == room.id && (!r.SpansWaves ||
                    investigation.Record.storyFlags.Contains("crossed:" + r.completionFlag))))
                && Eligible(r, investigation, false, half));
        }

        internal StoryRow NextFinale(StoryRoom room, InvestigationService investigation)
        {
            if (room.stage != "hub" && room.stage != "janhyang") return null;
            var branch = investigation.Record.storyFlags.Find(f => f.StartsWith("endingScene:", StringComparison.Ordinal));
            return Array.Find(finaleRows, r => r.room == room.id &&
                (branch == null || Array.IndexOf(r.entryCondition, branch) >= 0) &&
                Eligible(r, investigation, false, false));
        }
    }
}
