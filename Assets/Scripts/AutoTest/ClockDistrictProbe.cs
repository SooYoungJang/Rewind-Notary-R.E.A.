using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [Serializable] public sealed class ClockContact
    {
        public string room, floor, targetBand;
        public float feetX, feetY;
        public double segmentSeconds, districtSeconds, outsideClockSeconds, roomEntryToContactSeconds, fromEntrySeconds;
    }

    [Serializable] public sealed class ClockDistrictResult
    {
        public StepResult run = new StepResult { id = "ClockDistrict", expected = "Slot 3 first ascent, pendulum mastery and two investigated lift rides" };
        public List<ClockContact> contacts = new List<ClockContact>();
        public List<string> liftRooms = new List<string>();
        public string firstPendulumTarget, stageTwoRoom, masteryRoom;
        public string silverBoutWinFlag;
        public int silverBoutEntryHP, silverBoutOpponentHP;
        public int stationUses, combatUses, slot;
        public bool slotReadback, synergyDoorUsed, closedBeforeInvestigation, closedAfterEscapementClear, immediateReturn, upperLanding, lowerLanding;
        public double outboundSeconds, liftSeconds, returnLiftSeconds;
    }

    internal sealed class ClockDistrictProbe
    {
        private const string Entry = "clock_01_entry", Hall = "clock_02_pendulum", Bridge = "clock_03_counterflow",
            Escape = "clock_04_escapement", Vestibule = "clock_05_vestibule", Arena = "clock_06_arena",
            Upper = "clock_07_return_upper", Lower = "clock_08_return_lower", Rail = "clock_10_sanctuary_rail", Chain = "clock_09_shortcut";
        private static readonly string[] Route = { Entry, Hall, Bridge, Escape, Vestibule, Arena, Upper, Lower, Rail, Chain };
        private readonly ClockDistrictResult result;
        private readonly Action publish;
        private readonly HashSet<string> uses = new HashSet<string>();
        private readonly HashSet<string> stations = new HashSet<string>();
        private int station, closedFrame = -1, clearedFrame = -1;
        private string lastRoom, firstUse;
        private double entryTime, roomEntered, lastContact, lastFrame, inDistrict, longStart, liftStart, descentStart;
        private bool onClock;
        internal ClockDistrictProbe(ClockDistrictResult output, Action save) { result = output; publish = save; }

        internal void ObserveUse(ProgressionEvent e, string room)
        {
            if ((room != Upper && room != Lower) || e.kind != "valid_use" || e.skillId != BenefactorIds.Pendulum) return;
            if (!uses.Add(e.useId)) throw new InvalidOperationException("Duplicate pendulum activation credit");
            if (e.after == 30) result.stageTwoRoom = room;
            if (e.after == 80) result.masteryRoom = room;
            if (uses.Count == 1) { result.firstPendulumTarget = e.targetId; firstUse = e.useId; }
            if (e.targetId.StartsWith(room + ":station_", StringComparison.Ordinal))
            {
                if (!stations.Add(e.targetId)) throw new InvalidOperationException("Repeated return switch " + e.targetId);
                result.stationUses++;
            }
            else if (e.targetId.StartsWith(room + ":notary_guard_", StringComparison.Ordinal) ||
                e.targetId.StartsWith(room + ":sword_latch_", StringComparison.Ordinal)) result.combatUses++;
            else throw new InvalidOperationException("Unexpected pendulum credit " + e.targetId);
        }

        private void ObserveRoom(FirstDepartureBot bot, string room, double now)
        {
            if (entryTime == 0 && room == Entry) entryTime = lastContact = now;
            if (onClock) inDistrict += now - lastFrame;
            lastFrame = now;
            onClock = bot.Area?.Room.stage == "clock";
            if (room == lastRoom) return;
            roomEntered = now;
            if (closedFrame >= 0 && !result.closedBeforeInvestigation && room != Entry ||
                clearedFrame >= 0 && !result.closedAfterEscapementClear && room != Escape)
                throw new InvalidOperationException("A closed lift accepted the inspect input");
            if (room == Chain) result.synergyDoorUsed = lastRoom == Hall && result.lowerLanding;
            if (room == Upper || room == Lower)
            {
                if (CountStations(room) > 0) throw new InvalidOperationException("Re-entered first-visit switch run");
                station = 0;
            }
            if (lastRoom == Upper && (room != Lower || CountStations(Upper) < 42))
                throw new InvalidOperationException("Upper return was left before its first-visit switches were used");
            if (lastRoom == Lower && !bot.Record.storyFlags.Contains("first_transfer_seen"))
                throw new InvalidOperationException("Left lower return before first mastery");

            if (room == Escape && lastRoom == Entry && liftStart > 0)
            { result.liftRooms.Add(room); result.liftSeconds = now - liftStart; }
            if (room == Entry && lastRoom == Escape && descentStart > 0)
            { result.liftRooms.Add(room); result.returnLiftSeconds = now - descentStart; }
            lastRoom = room;
        }

        private int CountStations(string room)
        {
            int count = 0;
            foreach (var target in stations) if (target.StartsWith(room + ":", StringComparison.Ordinal)) count++;
            return count;
        }

        private bool NeedContact(FirstDepartureBot bot, double now)
        {
            int i = result.contacts.Count;
            var area = bot.Area;
            if (i == Route.Length || area.Room.id != Route[i]) return false;
            var f = Array.Find(area.Room.floors, floor => floor.contactSave);
            var feet = area.Rea.Body.position;
            if (area.StandingOn(f.surface) && Mathf.Abs(feet.x - f.x) <= .8f && bot.Session.LastSaveSucceeded &&
                bot.Record.storyFlags.Contains("scene_floor:clock:" + f.id))
            {
                result.contacts.Add(new ClockContact { room = area.Room.id, floor = f.id, feetX = feet.x, feetY = feet.y,
                    roomEntryToContactSeconds = now - roomEntered, outsideClockSeconds = Math.Max(0, now - lastContact - inDistrict),
                    fromEntrySeconds = now - entryTime, segmentSeconds = now - lastContact, districtSeconds = inDistrict,
                    targetBand = inDistrict < 180 ? "below-target" : inDistrict > 300 ? "above-target" : "within-target" });
                lastContact = now; inDistrict = 0; publish();
                return false;
            }
            return true;
        }

        private static bool Landed(FirstDepartureBot bot, string floor)
        {
            var area = bot.Area;
            var f = area.Room.Floor(floor);
            return f != null && area.StandingOn(f.surface) && Mathf.Abs(area.Rea.Body.position.x - f.x) <= .8f &&
                bot.Session.LastSaveSucceeded && bot.Record.storyFlags.Contains("scene_floor:clock:" + floor);
        }

        private bool ReturnSwitch(FirstDepartureBot bot, BotKeyboard keys)
        {
            var area = bot.Area;
            if (station >= area.Room.stations) return false;
            string id = area.Room.id + ":station_" + station + ":posture";
            var mark = area.Room.stationMarks[station];
            if (stations.Contains(id))
            {
                if (area.Room.id == Upper && station == 0 && !result.immediateReturn)
                {
                    var rewind = area.Rea.GetComponent<PendulumRewindSkill>();
                    if (rewind.LastReturnUse != firstUse)
                    {
                        if (!rewind.Active) throw new InvalidOperationException("First switch missed its timed recall");
                        if (!rewind.BlocksActions) keys.Cast(-area.Rea.Facing);
                        return true;
                    }
                    result.immediateReturn = true;
                }
                station++;
                return true;
            }
            int direction = station + 1 < area.Room.stations && area.Room.stationMarks[station + 1].surface == mark.surface
                ? Math.Sign(area.Room.stationMarks[station + 1].x - mark.x) : Math.Sign(mark.x - area.Room.stationMarks[station - 1].x);
            if (!area.StandingOn(mark.surface))
            {
                var guard = area.ClockTargetOnTier;
                if (guard != null) bot.ClockFight(guard);
                else bot.ClockPosition(mark.surface, mark.x - 2 * direction);
            }
            else bot.ClockCast(mark.x, direction);
            return true;
        }

        internal bool Drive(FirstDepartureBot bot, BotKeyboard keys, double now)
        {
            var page = bot.Shell.Page;
            if (result.slot == 0)
            {
                if (bot.Session?.ActiveSlot == 3 && bot.Record.slotId == "slot-3" && !bot.Record.testData) result.slot = 3;
                else
                {
                    if (page == ShellPage.Playing || page == ShellPage.StoryDialogue) keys.Tap(KeyCode.Escape);
                    else if (page == ShellPage.Pause) bot.Select(3);
                    else if (page == ShellPage.Main) bot.Select(1);
                    else if (page == ShellPage.Slots) bot.Select(2);
                    return false;
                }
            }
            if (bot.Session != null && bot.Session.ActiveSlot != 3) throw new InvalidOperationException("Clock slot changed");
            var area = bot.Area;
            string room = area?.Room.id;
            ObserveRoom(bot, room, now);
            bool contact = onClock && NeedContact(bot, now);
            if (page == ShellPage.Stages) { bot.Select(Array.FindIndex(StageCatalog.All, s => s.Id == "clock")); return false; }
            if (page != ShellPage.Playing || area == null || !GameInput.CanPlay)
            { bot.Drive(PlaytestStep.Velvet); return false; }
            if (area.Rea.Health.Dead) throw new InvalidOperationException("Clock first visit interrupted by death");
            if (!onClock && area.BoutOpponent != null && area.ActiveRow?.effect == "defeat" &&
                area.PendingAction?.StartsWith("win:", StringComparison.Ordinal) == true && string.IsNullOrEmpty(result.silverBoutWinFlag))
            {
                result.silverBoutWinFlag = area.ActiveRow.completionFlag;
                result.silverBoutEntryHP = area.Rea.Health.Current;
                result.silverBoutOpponentHP = area.BoutOpponent.Health.Current;
                publish();
            }
            if (bot.TryDistrictHeal()) return false;
            bool investigated = bot.Record.storyFlags.Contains("investigation_complete");
            if (!onClock)
            {
                if (room == "hub_lift" && !investigated && area.ActiveRow == null)
                { Door(bot, keys, Entry); return false; }
                if (result.lowerLanding && area.ActiveRow == null)
                {
                    if (room == "hub_lift")
                    {
                        Door(bot, keys, bot.Record.Get(SkillRules.SilverStitch).acquired ? Entry : "velvet_01_entry");
                        return false;
                    }
                    if (room == "velvet_05_return" && bot.Record.Get(SkillRules.SilverStitch).acquired)
                    {
                        Door(bot, keys, "hub_lift");
                        return false;
                    }
                }
                bot.Drive(PlaytestStep.Velvet);
                return false;
            }
            string action = area.PendingAction;
            if (contact && (area.ActiveRow == null || room == Arena || room == Upper || room == Lower || room == Rail || room == Chain))
            {
                var f = Array.Find(area.Room.floors, m => m.contactSave);
                bot.ClockPosition(f.surface, f.x);
                return false;
            }
            if (result.lowerLanding && room == Chain && result.contacts.Count == Route.Length)
            {
                if (!result.synergyDoorUsed || !bot.Record.Get(SkillRules.SilverStitch).acquired || !investigated ||
                    string.IsNullOrEmpty(result.silverBoutWinFlag) || !bot.Record.storyFlags.Contains(result.silverBoutWinFlag) ||
                    result.stageTwoRoom != Upper || result.masteryRoom != Lower)
                    throw new InvalidOperationException("Missing synergy entrance, silver bout victory or first-visit mastery");
                return true;
            }
            if (action == "use:return_call" || area.ActiveRow?.room == "*")
            { bot.Drive(PlaytestStep.SafeReturn); return false; }
            if (area.BoutOpponent != null) { bot.FightBoss(); return false; }
            if (area.ActiveRow != null && action == null) return false;
            if ((room == Upper || room == Lower) && !bot.Record.firstTransferPending)
            {
                if (action == "cross:floor")
                { bot.ClockPosition(area.Room.stationMarks[0].surface, area.Room.stationMarks[0].x + 2.1f); return false; }
                if (ReturnSwitch(bot, keys)) return false;
            }
            if (action != null)
            {
                var point = area.Room.Point(action.Substring(action.IndexOf(':') + 1));
                if (action.StartsWith("use:", StringComparison.Ordinal) && point != null)
                { if (bot.ClockPosition(point.surface, point.x)) keys.Tap(KeyCode.C); }
                else if (action.StartsWith("timed:", StringComparison.Ordinal) && point != null)
                    bot.ClockPosition(point.surface, point.x);
                else if (bot.ClockTarget != null) bot.ClockFight(bot.ClockTarget);
                else if (action == "clear:wave") bot.ClockPosition("drum_floor", 36);
                else if (action.StartsWith("advance:", StringComparison.Ordinal)) Door(bot, keys, area.ActiveRow.alternateRoom);
                return false;
            }
            if (room == Entry && !investigated && !result.closedBeforeInvestigation)
            {
                if (closedFrame >= 0 && Time.frameCount > closedFrame + 1)
                {
                    result.closedBeforeInvestigation = !bot.Record.storyFlags.Contains("clock_04_cleared");
                    if (!result.closedBeforeInvestigation) throw new InvalidOperationException("Late closed-lift observation");
                    publish();
                }
                else if (closedFrame < 0 && Door(bot, keys, Escape)) { closedFrame = Time.frameCount; longStart = now; }
                return false;
            }
            if (room == Escape && !investigated && !result.closedAfterEscapementClear)
            {
                if (clearedFrame < 0)
                {
                    if (Door(bot, keys, Entry)) { clearedFrame = Time.frameCount; result.outboundSeconds = now - longStart; }
                }
                else if (Time.frameCount > clearedFrame + 1)
                {
                    var cage = Array.Find(area.Room.exits, d => d.room == Entry);
                    result.closedAfterEscapementClear = cage.InReach(area.Rea.Body.position) &&
                        bot.Record.storyFlags.Contains("clock_04_cleared");
                    if (!result.closedAfterEscapementClear) throw new InvalidOperationException("Missing pre-investigation lift check");
                    publish();
                }
                return false;
            }
            if (room == Entry && investigated)
            {
                if (result.lowerLanding)
                {
                    Door(bot, keys, bot.Record.Get(SkillRules.SilverStitch).acquired ? Hall : "hub_lift");
                    return false;
                }
                if (descentStart > 0)
                {
                    if (!Landed(bot, "clock_lift_foot")) return false;
                    result.lowerLanding = true;
                    if (result.contacts.Count != Route.Length - 1 || !result.closedBeforeInvestigation || !result.immediateReturn ||
                        !result.closedAfterEscapementClear || !result.upperLanding || string.Join("/", result.liftRooms) != Entry + "/" + Escape + "/" + Entry ||
                        result.firstPendulumTarget != Upper + ":station_0:posture" || uses.Count < 80 ||
                        bot.Record.Get(BenefactorIds.Pendulum).stage < 3 || result.liftSeconds <= 0 || result.returnLiftSeconds <= 0 || result.outboundSeconds <= result.liftSeconds)
                        throw new InvalidOperationException("Incomplete clock contact, mastery or lift evidence");
                    publish();
                    return false;
                }
                if (Door(bot, keys, Escape) && liftStart == 0) { liftStart = now; result.liftRooms.Add(Entry); }
                return false;
            }
            if (room == Escape && liftStart > 0)
            {
                if (!result.upperLanding)
                { result.upperLanding = Landed(bot, "clock_lift_cage"); return false; }
                if (Door(bot, keys, Entry) && descentStart == 0) descentStart = now;
                return false;
            }
            if (room == Hall && result.lowerLanding) { Door(bot, keys, Chain); return false; }
            if (room == Rail)
            {
                Door(bot, keys, bot.Record.storyFlags.Contains("luka_lift_complete") ? Entry : "hub_lift");
                return false;
            }
            int index = Array.IndexOf(Route, room);
            if (index >= 0 && index < 5) Door(bot, keys, Route[index + 1]);
            return false;
        }

        private static bool Door(FirstDepartureBot bot, BotKeyboard keys, string dest)
        {
            var door = Array.Find(bot.Area.Room.exits, d => d.room == dest);
            if (door == null || !bot.ClockDoor(door)) return false;
            keys.Tap(KeyCode.C);
            return GameInput.InvestigateDown;
        }
    }
}
