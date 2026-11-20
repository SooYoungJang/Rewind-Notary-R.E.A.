using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [Serializable] public sealed class BellContact
    {
        public string room, floor, targetBand;
        public float feetX, feetY;
        public double segmentSeconds, districtSeconds, outsideBellSeconds, roomEntryToContactSeconds, fromEntrySeconds;
    }

    [Serializable] public sealed class BellDistrictResult
    {
        public StepResult run = new StepResult { id = "BellDistrict", expected = "Slot 5, ten saved landings, first-return forge mastery and inspected descent" };
        public List<BellContact> contacts = new List<BellContact>();
        public List<string> counterweightRides = new List<string>();
        public List<TerrainWidth> widths = new List<TerrainWidth>();
        public List<string> stationTargets = new List<string>(), longRooms = new List<string>(), dropRooms = new List<string>();
        public List<string> waveRooms = new List<string>();
        public List<float> refugeSeconds = new List<float>(), waveArrivalSeconds = new List<float>();
        public int slot, combatUses;
        public string firstForgeTarget, stageTwoRoom, masteryRoom;
        public string[] previousSlotsBefore, previousSlotsAfter;
        public bool closedBeforeInvestigation, dropLanding, previousSlotsUnchanged, slotReadback;
        public double longSeconds, shortcutSeconds;
    }

    internal sealed class BellDistrictProbe
    {
        private const string Entry = "bell_01_entry", Tunnels = "bell_02_tunnels", Hall = "bell_03_acoustic_hall",
            Shaft = "bell_04_belfry_shaft", Landing = "bell_05_silence_landing", Arena = "bell_06_arena",
            Chime = "bell_07_return_chime", Under = "bell_08_return_undercroft", Table = "bell_10_sanctuary_table", Seam = "bell_09_shortcut";
        private static readonly string[] Route = { Entry, Tunnels, Hall, Shaft, Landing, Arena, Chime, Under, Table, Seam };
        private static readonly string[] Trip = { Landing, Shaft, Hall, Tunnels, Hall, Shaft, Landing, Tunnels };
        private readonly BellDistrictResult result;
        private readonly Action publish;
        private readonly HashSet<string> uses = new HashSet<string>(), stations = new HashSet<string>(), surveyed = new HashSet<string>();
        private int station, closedFrame = -1, leg = -1;
        private string previousRoom;
        private bool wasInside;
        private double start, lastContact, sampled, entered, outside, longStart, shortcutStart;

        internal BellDistrictProbe(BellDistrictResult result, Action publish)
        { this.result = result; this.publish = publish; }

        internal void ObserveUse(ProgressionEvent e, string room)
        {
            if ((room != Chime && room != Under) || e.kind != "valid_use" || e.skillId != BenefactorIds.Forge) return;
            if (!uses.Add(e.useId)) throw new InvalidOperationException("Duplicate forge activation " + e.useId);
            if (uses.Count == 1) result.firstForgeTarget = e.targetId;
            if (e.after == 30) result.stageTwoRoom = room;
            if (e.after == 80) result.masteryRoom = room;
            if (e.targetId.StartsWith(room + ":station_", StringComparison.Ordinal) && e.targetId.EndsWith(":guard_break", StringComparison.Ordinal))
            {
                if (e.targetId != room + ":station_" + station + ":guard_break") throw new InvalidOperationException("Out-of-order forge plate " + e.targetId);
                if (!stations.Add(e.targetId)) throw new InvalidOperationException("Repeated forge station " + e.targetId);
                result.stationTargets.Add(e.targetId);
            }
            else if (e.targetId.EndsWith(":posture", StringComparison.Ordinal)) result.combatUses++;
            else throw new InvalidOperationException("Unexpected forge credit " + e.targetId);
        }

        private void Observe(FirstDepartureBot bot, double now)
        {
            var area = bot.Area;
            string room = area?.Room.id;
            bool inside = area?.Room.stage == "bell";
            if (start == 0 && room == Entry) start = lastContact = sampled = now;
            if (start > 0 && !wasInside) outside += now - sampled;
            sampled = now;
            wasInside = inside;
            if (room == previousRoom) return;
            if (closedFrame >= 0 && !result.closedBeforeInvestigation && room != Landing)
                throw new InvalidOperationException("Closed descent admitted inspect input");
            if (previousRoom == Chime && (room != Under || station != 42))
                throw new InvalidOperationException("Incomplete first chime descent");
            if (previousRoom == Under && (result.masteryRoom != Under || !bot.Record.storyFlags.Contains("first_transfer_seen")))
                throw new InvalidOperationException("First undercroft visit ended before mastery");
            if (room == Chime || room == Under)
            {
                if (surveyed.Contains(room)) throw new InvalidOperationException("Repeated first-return room");
                station = 0;
            }
            if (inside && surveyed.Add(room)) TerrainWidth.Measure(area, result.widths);
            if (leg >= 0 && leg + 1 < Trip.Length && room == Trip[leg + 1])
            {
                leg++;
                if (leg <= 3) result.longRooms.Add(room);
                if (leg == 7) result.dropRooms.Add(room);
            }
            entered = now;
            previousRoom = room;
        }

        private bool Contact(FirstDepartureBot bot, double now)
        {
            var area = bot.Area;
            int i = result.contacts.Count;
            if (i == Route.Length || area.Room.id != Route[i]) return false;
            var floor = Array.Find(area.Room.floors, f => f.contactSave);
            var feet = area.Rea.Body.position;
            if (!area.StandingOn(floor.surface) || Mathf.Abs(feet.x - floor.x) > .8f || !bot.Session.LastSaveSucceeded ||
                !bot.Record.storyFlags.Contains("scene_floor:bell:" + floor.id))
            {
                if (area.ActiveRow != null && area.PendingAction == null) return false;
                bot.ClockPosition(floor.surface, floor.x);
                return true;
            }
            double interval = now - lastContact, inside = interval - outside;
            result.contacts.Add(new BellContact { room = area.Room.id, floor = floor.id, feetX = feet.x, feetY = feet.y,
                segmentSeconds = interval, districtSeconds = inside, outsideBellSeconds = outside, roomEntryToContactSeconds = now - entered,
                fromEntrySeconds = now - start, targetBand = inside < 180 ? "below-target" : inside > 300 ? "above-target" : "within-target" });
            lastContact = now; outside = 0; publish();
            return false;
        }

        private bool Door(FirstDepartureBot bot, BotKeyboard keys, string destination)
        {
            var area = bot.Area;
            var room = area.Room;
            // After the sanctuary visit the story rows are complete, but reloading a room rebuilds its wave.
            if (room.stage == "bell" && surveyed.Contains(Table) && area.DistrictWaveTarget is WaveTarget2D target)
            {
                bot.BellFight(target);
                return false;
            }
            var door = Array.Find(room.exits, d => d.room == destination);
            if (door == null || !bot.BellDoor(door)) return false;
            keys.Tap(GameInput.InteractKey);
            return GameInput.InvestigateDown;
        }

        private bool Compare(FirstDepartureBot bot, BotKeyboard keys, double now)
        {
            var area = bot.Area;
            if (result.dropLanding || leg < 0 && area.Room.id != Landing) return false;
            if (leg < 0 || leg == 6 && shortcutStart == 0)
            {
                var head = area.Room.Floor("bell_drop_head");
                if (!bot.ClockPosition(head.surface, head.x) || !area.StandingOn(head.surface)) return true;
                if (leg < 0) { leg = 0; longStart = now; result.longRooms.Add(Landing); }
                else { shortcutStart = now; result.dropRooms.Add(Landing); }
            }
            if (leg == 3 && result.longSeconds == 0 || leg == 7)
            {
                var foot = area.Room.Floor("bell_drop_landing");
                if (!bot.ClockPosition(foot.surface, foot.x) || !area.StandingOn(foot.surface)) return true;
                if (leg == 3) result.longSeconds = now - longStart;
                else
                {
                    result.shortcutSeconds = now - shortcutStart;
                    result.dropLanding = bot.Session.LastSaveSucceeded && bot.Record.storyFlags.Contains("scene_floor:bell:" + foot.id);
                    if (!result.dropLanding || result.shortcutSeconds <= 0 || result.shortcutSeconds >= result.longSeconds)
                        throw new InvalidOperationException("Hatch did not shorten the trip between the two landings");
                    publish(); return true;
                }
            }
            Door(bot, keys, Trip[leg + 1]);
            return true;
        }

        internal bool Drive(FirstDepartureBot bot, BotKeyboard keys, double now)
        {
            var page = bot.Shell.Page;
            if (result.slot == 0)
            {
                if (bot.Session?.ActiveSlot == 5 && bot.Record.slotId == "slot-5" && !bot.Record.testData) result.slot = 5;
                else
                {
                    if (page == ShellPage.Playing || page == ShellPage.StoryDialogue) keys.Tap(KeyCode.Escape);
                    else if (page == ShellPage.Pause) bot.Select(3);
                    else if (page == ShellPage.Main) bot.Select(1);
                    else if (page == ShellPage.Slots)
                    {
                        if (result.previousSlotsBefore == null) result.previousSlotsBefore = AutoTestRuntime.ReadPreviousSlotHashes(4);
                        bot.Select(4);
                    }
                    return false;
                }
            }
            if (bot.Session != null && bot.Session.ActiveSlot != 5) throw new InvalidOperationException("Bell traversal lost slot 5");
            Observe(bot, now);
            var area = bot.Area;
            if (page == ShellPage.Stages) { bot.Select(Array.FindIndex(StageCatalog.All, s => s.Id == "velvet")); return false; }
            if (page != ShellPage.Playing || area == null || !GameInput.CanPlay) { bot.Drive(PlaytestStep.Velvet); return false; }
            if (area.Rea.Health.Dead) throw new InvalidOperationException("Bell traversal interrupted by death");
            if (bot.TryDistrictHeal()) return false;
            string room = area.Room.id, action = area.PendingAction;
            bool investigated = bot.Record.storyFlags.Contains("investigation_complete");
            bool silver = bot.Record.Get(SkillRules.SilverStitch).acquired;
            if (area.Room.stage != "bell")
            {
                if (area.ActiveRow == null && (room == "hub_lift" && (!investigated || silver) || room == "velvet_05_return" && silver))
                    Door(bot, keys, room == "hub_lift" ? Entry : "hub_lift");
                else bot.Drive(PlaytestStep.Velvet);
                return false;
            }
            if (area.BoutOpponent != null) { bot.FightBoss(); return false; }
            if ((room == Shaft || room == Landing || room == Arena) && !result.waveRooms.Contains(room))
            {
                var wave = area.Room.bellWaves[0];
                if (area.FirstWavePassedAt >= 0)
                {
                    result.waveRooms.Add(room);
                    result.refugeSeconds.Add(area.FirstWaveSafeAt);
                    result.waveArrivalSeconds.Add(area.FirstWavePassedAt);
                    publish();
                }
                else
                {
                    bot.BellRefuge(wave);
                    return false;
                }
            }
            if ((room != Seam || bot.Record.storyFlags.Contains("bell_synergy_complete")) && Contact(bot, now)) return false;
            if (room == Seam && result.contacts.Count == Route.Length)
            {
                if (result.firstForgeTarget != Chime + ":station_0:guard_break" || result.stageTwoRoom != Chime ||
                    result.masteryRoom != Under || uses.Count < 80 || result.counterweightRides.Count != 2 || result.waveRooms.Count != 3 || surveyed.Count != Route.Length || !investigated || !silver ||
                    !result.closedBeforeInvestigation || !result.dropLanding || string.Join("/", result.longRooms) != string.Join("/", Landing, Shaft, Hall, Tunnels) ||
                    string.Join("/", result.dropRooms) != Landing + "/" + Tunnels)
                    throw new InvalidOperationException("Missing bell route, forge or descent evidence");
                return true;
            }
            if (action == "use:return_call" || area.ActiveRow?.room == "*") { bot.Drive(PlaytestStep.SafeReturn); return false; }
            if (area.ActiveRow != null && action == null) return false;
            if (room == Chime || room == Under)
            {
                var marks = area.Room.stationMarks;
                if (action == "cross:floor") { bot.ClockPosition(marks[0].surface, marks[0].x + 2.3f); return false; }
                if (!bot.Record.firstTransferPending && station < marks.Length)
                {
                    if (station > 0 && station % 7 == 0)
                    {
                        var guard = area.BellTargetOnTurn(station / 7 - 1);
                        if (guard != null) { bot.BellFight(guard); return false; }
                    }
                    if (station == 8 && !result.counterweightRides.Contains(room))
                    {
                        if (bot.RideCounterweight("counterweight_cradle", marks[7].surface, area.Room.SurfaceTop(marks[7].surface) + 1.2f)) result.counterweightRides.Add(room);
                        return false;
                    }
                    var m = marks[station];
                    if (stations.Contains(room + ":station_" + station + ":guard_break")) station++;
                    else bot.BellStrike(m.surface, m.x, station / 7 % 2 == 0 ? 1 : -1);
                    return false;
                }
            }
            if (action != null && action.StartsWith("use:", StringComparison.Ordinal))
            {
                var p = area.Room.Point(action.Substring(4));
                if (p != null && bot.ClockPosition(p.surface, p.x)) keys.Tap(GameInput.InteractKey);
                return false;
            }
            if (room == Arena && (action == "win:gabriel" || action == "interlude:gabriel"))
            { bot.ClockPosition("refuge", 23); return false; }
            if (investigated && silver && action == null && Compare(bot, keys, now)) return false;
            if (area.DistrictWaveTarget != null && (action != null || room == Table))
            { bot.BellFight(area.DistrictWaveTarget); return false; }
            if (action != null)
            {
                if (room == Seam && action == "sew:seam") bot.SewStationSeam(area.Room.stationMarks[0]);
                else if (room == Seam && action == "station:resonance_forge")
                {
                    var mark = area.Room.stationMarks[0];
                    bot.BellStrike(mark.surface, mark.x, -1);
                }
                else if (room == Seam && action == "cross:floor")
                {
                    var mark = area.Room.stationMarks[0];
                    bot.ClockPosition(mark.surface, mark.x + 2.1f);
                }
                else if (action.StartsWith("advance:", StringComparison.Ordinal)) Door(bot, keys, area.ActiveRow.alternateRoom);
                else if (action == "clear:wave") bot.BellDoor(area.Room.exits[1]);
                return false;
            }
            if (room == Landing && !investigated && !result.closedBeforeInvestigation)
            {
                if (closedFrame < 0) { if (Door(bot, keys, Tunnels)) closedFrame = Time.frameCount; }
                else if (Time.frameCount > closedFrame + 1)
                {
                    var door = Array.Find(area.Room.exits, d => d.room == Tunnels);
                    result.closedBeforeInvestigation = door.InReach(area.Rea.Body.position) && !investigated;
                    if (!result.closedBeforeInvestigation) throw new InvalidOperationException("Missed closed descent input");
                    publish();
                }
                return false;
            }
            if (room == Table) Door(bot, keys, "hub_lift");
            else if (room == Tunnels && result.dropLanding) Door(bot, keys, Seam);
            else
            {
                int i = Array.IndexOf(Route, room);
                if (i >= 0 && i < 5) Door(bot, keys, Route[i + 1]);
            }
            return false;
        }
    }
}
