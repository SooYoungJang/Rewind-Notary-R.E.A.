using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [Serializable] public sealed class OperaContact
    {
        public string room, floor, targetBand;
        public float feetX, feetY;
        public double segmentSeconds, districtSeconds, outsideOperaSeconds, roomEntryToContactSeconds, fromEntrySeconds;
    }

    [Serializable] public sealed class TerrainWidth
    {
        public string room, surface;
        public float authored, measured, left, bottom, height;

        internal static void Measure(RepresentativeArea area, List<TerrainWidth> widths)
        {
            string room = area.Room.id;
            Physics2D.SyncTransforms();
            foreach (var s in area.Room.terrain)
            {
                if (area.Room.stage == "gallery") area.VerifyGalleryTile(s);
                var actual = area.ProbeSurfaceBounds(s);
                float measured = actual.size.x;
                widths.Add(new TerrainWidth { room = room, surface = s.id, authored = s.rect.width, measured = measured,
                    left = actual.min.x, bottom = actual.min.y, height = actual.size.y });
                if (!s.Moving && s.rampRise == 0 && (Mathf.Abs(actual.min.x - s.rect.x) > .005f ||
                    Mathf.Abs(actual.min.y - s.rect.y) > .005f || Mathf.Abs(actual.size.y - s.rect.height) > .005f))
                    throw new InvalidOperationException(room + "/" + s.id + " fixed collider rectangle mismatch");
                if (Mathf.Abs(measured - s.rect.width) > .005f)
                    throw new InvalidOperationException(room + "/" + s.id + " collider width " + measured + " expected " + s.rect.width);
            }
        }
    }

    [Serializable] public sealed class OperaDistrictResult
    {
        public StepResult run = new StepResult { id = "OperaDistrict", expected = "Slot 4: ten contacts, shaded chandelier, first-return waltz and investigated drop" };
        public List<OperaContact> contacts = new List<OperaContact>();
        public List<TerrainWidth> widths = new List<TerrainWidth>();
        public List<string> stationTargets = new List<string>(), longRooms = new List<string>(), dropRooms = new List<string>();
        public string firstWaltzTarget, stageTwoRoom, masteryRoom;
        public int slot, combatUses;
        public string[] previousSlotsBefore, previousSlotsAfter;
        public bool previousSlotsUnchanged, shadedCrossing, closedBeforeInvestigation, dropLanding, slotReadback;
        public double longSeconds, shortcutSeconds;
    }

    internal sealed class OperaDistrictProbe
    {
        private const string Entry = "opera_01_entry", Hall = "opera_02_auditorium", Chandelier = "opera_03_chandelier",
            Roof = "opera_04_rooftop_west", Wings = "opera_05_wings", Arena = "opera_06_arena",
            Upper = "opera_07_return_roof", Lower = "opera_08_return_foyer", Stairs = "opera_10_sanctuary_stairs", Seam = "opera_09_shortcut";
        private static readonly string[] Route = { Entry, Hall, Chandelier, Roof, Wings, Arena, Upper, Lower, Stairs, Seam };
        private static readonly string[] Comparison = { Wings, Roof, Chandelier, Hall, Chandelier, Roof, Wings, Hall };
        private readonly OperaDistrictResult result;
        private readonly Action publish;
        private readonly HashSet<string> uses = new HashSet<string>(), stations = new HashSet<string>(), surveyed = new HashSet<string>();
        private int station, closedFrame = -1, leg = -1;
        private string previousRoom;
        private bool previousOpera, rodeInShade;
        private double started, lastContact, lastSample, roomEntered, outside, longStart, dropStart;

        internal OperaDistrictProbe(OperaDistrictResult result, Action publish) { this.result = result; this.publish = publish; }

        internal void ObserveUse(ProgressionEvent e, string room)
        {
            if ((room != Upper && room != Lower) || e.kind != "valid_use" || e.skillId != BenefactorIds.Waltz) return;
            if (!uses.Add(e.useId)) throw new InvalidOperationException("Duplicate waltz activation " + e.useId);
            if (uses.Count == 1) result.firstWaltzTarget = e.targetId;
            if (e.after == 30) result.stageTwoRoom = room;
            if (e.after == 80) result.masteryRoom = room;
            if (e.targetId.StartsWith(room + ":station_", StringComparison.Ordinal))
            {
                if (!stations.Add(e.targetId)) throw new InvalidOperationException("Repeated waltz station " + e.targetId);
                result.stationTargets.Add(e.targetId);
            }
            else if (e.targetId.EndsWith(":posture", StringComparison.Ordinal)) result.combatUses++;
            else throw new InvalidOperationException("Unexpected waltz credit " + e.targetId);
        }

        private void Observe(FirstDepartureBot bot, double now)
        {
            var area = bot.Area;
            string room = area?.Room.id;
            bool opera = area?.Room.stage == "opera";
            if (started == 0 && room == Entry) started = lastContact = lastSample = now;
            if (started > 0 && !previousOpera) outside += now - lastSample;
            lastSample = now;
            previousOpera = opera;
            if (room == Chandelier)
            {
                var pendant = area.Room.Surface("pendant");
                if (area.ChandelierShaded && area.RidingSurface(pendant) &&
                    Mathf.Abs(area.Rea.Body.position.x - (pendant.rect.x + pendant.rect.width / 2)) < .7f) rodeInShade = true;
                if (rodeInShade && area.StandingOn("east_gallery")) result.shadedCrossing = true;
            }
            if (room == previousRoom) return;
            if (closedFrame >= 0 && !result.closedBeforeInvestigation && room != Wings)
                throw new InvalidOperationException("Uninvestigated drop admitted input");
            if (previousRoom == Upper && (room != Lower || station < 42))
                throw new InvalidOperationException("Left the upper route before its 42 stations");
            if (previousRoom == Lower && (result.masteryRoom != Lower || !bot.Record.storyFlags.Contains("first_transfer_seen")))
                throw new InvalidOperationException("Left the foyer before first mastery");
            if (room == Upper || room == Lower)
            {
                if (surveyed.Contains(room)) throw new InvalidOperationException("Repeated first-return visit");
                station = 0;
            }
            if (opera && surveyed.Add(room))
            {
                TerrainWidth.Measure(area, result.widths);
            }
            if (leg >= 0 && leg + 1 < Comparison.Length && room == Comparison[leg + 1])
            {
                leg++;
                if (leg <= 3) result.longRooms.Add(room);
                if (leg == 7) result.dropRooms.Add(room);
            }
            roomEntered = now;
            previousRoom = room;
        }

        private bool Contact(FirstDepartureBot bot, double now)
        {
            var area = bot.Area;
            int i = result.contacts.Count;
            if (i >= Route.Length || area.Room.id != Route[i]) return false;
            var f = Array.Find(area.Room.floors, item => item.contactSave);
            var feet = area.Rea.Body.position;
            if (!area.StandingOn(f.surface) || Mathf.Abs(feet.x - f.x) > .8f || !bot.Session.LastSaveSucceeded ||
                !bot.Record.storyFlags.Contains("scene_floor:opera:" + f.id))
            {
                if (area.ActiveRow == null || area.PendingAction != null)
                { bot.OperaPosition(f.surface, f.x); return true; }
                return false;
            }
            double segment = now - lastContact, inside = segment - outside;
            result.contacts.Add(new OperaContact { room = area.Room.id, floor = f.id, feetX = feet.x, feetY = feet.y,
                segmentSeconds = segment, districtSeconds = inside, outsideOperaSeconds = outside, fromEntrySeconds = now - started,
                roomEntryToContactSeconds = now - roomEntered, targetBand = inside < 180 ? "below-target" : inside > 300 ? "above-target" : "within-target" });
            lastContact = now; outside = 0; publish(); return false;
        }

        private static bool Door(FirstDepartureBot bot, BotKeyboard keys, string dest)
        {
            var d = Array.Find(bot.Area.Room.exits, e => e.room == dest);
            if (d == null || !bot.OperaDoor(d)) return false;
            keys.Tap(KeyCode.F);
            return GameInput.InvestigateDown;
        }

        private bool WorkStation(FirstDepartureBot bot, BotKeyboard keys)
        {
            var room = bot.Area.Room;
            if (station >= room.stations) return false;
            var mark = room.stationMarks[station];
            if (stations.Contains(room.id + ":station_" + station)) { station++; return true; }
            if (bot.OperaPosition(mark.surface, mark.x) && bot.Area.Rea.Grounded)
                keys.Cast(0, 1);
            return true;
        }

        private bool Compare(FirstDepartureBot bot, BotKeyboard keys, double now)
        {
            var area = bot.Area;
            if (leg < 0 && area.Room.id != Wings) return false;
            if (leg == 7 && result.dropLanding) return false;
            // Both watches start on the hatch and stop on the same front-of-house landing.
            if (leg < 0 || leg == 6 && dropStart == 0)
            {
                var head = area.Room.Floor("opera_drop_head");
                if (!bot.OperaPosition(head.surface, head.x) || !area.StandingOn(head.surface)) return true;
                if (leg < 0) { leg = 0; longStart = now; result.longRooms.Add(Wings); }
                else { dropStart = now; result.dropRooms.Add(Wings); }
            }
            if (leg == 3 && result.longSeconds == 0 || leg == 7)
            {
                var foot = area.Room.Floor("opera_drop_landing");
                if (!bot.OperaPosition(foot.surface, foot.x) || !area.StandingOn(foot.surface)) return true;
                if (leg == 3) result.longSeconds = now - longStart;
                else
                {
                    result.shortcutSeconds = now - dropStart;
                    result.dropLanding = bot.Session.LastSaveSucceeded && bot.Record.storyFlags.Contains("scene_floor:opera:" + foot.id);
                    if (!result.dropLanding || result.shortcutSeconds <= 0 || result.shortcutSeconds >= result.longSeconds)
                        throw new InvalidOperationException("Drop did not shorten the route between the measured landings");
                    publish(); return true;
                }
            }
            Door(bot, keys, Comparison[leg + 1]); return true;
        }

        internal bool Drive(FirstDepartureBot bot, BotKeyboard keys, double now)
        {
            var page = bot.Shell.Page;
            if (result.slot == 0)
            {
                if (bot.Session?.ActiveSlot == 4 && bot.Record.slotId == "slot-4" && !bot.Record.testData)
                {
                    result.slot = 4;
                    result.previousSlotsBefore = AutoTestRuntime.ReadPreviousSlotHashes();
                }
                else
                {
                    if (page == ShellPage.Playing || page == ShellPage.StoryDialogue) keys.Tap(KeyCode.Escape);
                    else if (page == ShellPage.Pause) bot.Select(3);
                    else if (page == ShellPage.Main) bot.Select(1);
                    else if (page == ShellPage.Slots) bot.Select(3);
                    return false;
                }
            }
            if (bot.Session != null && bot.Session.ActiveSlot != 4) throw new InvalidOperationException("Opera lost slot 4");
            Observe(bot, now);
            var area = bot.Area;
            if (page == ShellPage.Stages) { bot.Select(Array.FindIndex(StageCatalog.All, s => s.Id == "velvet")); return false; }
            if (page != ShellPage.Playing || area == null || !GameInput.CanPlay) { bot.Drive(PlaytestStep.Velvet); return false; }
            if (area.Rea.Health.Dead) throw new InvalidOperationException("Opera traversal interrupted by death");
            if (bot.TryDistrictHeal()) return false;
            string room = area.Room.id, action = area.PendingAction;
            bool investigated = bot.Record.storyFlags.Contains("investigation_complete");
            if (area.Room.stage != "opera")
            {
                if (area.ActiveRow == null && (room == "hub_lift" && (!investigated || bot.Record.Get(SkillRules.SilverStitch).acquired) ||
                    room == "velvet_05_return" && bot.Record.Get(SkillRules.SilverStitch).acquired))
                    Door(bot, keys, room == "hub_lift" ? Entry : "hub_lift");
                else bot.Drive(PlaytestStep.Velvet);
                return false;
            }
            if (area.BoutOpponent != null)
            {
                if (area.OnOperaLift(out _)) bot.OperaPosition("stage_floor", area.BoutOpponent.Body.position.x);
                else bot.FightBoss();
                return false;
            }
            if (Contact(bot, now)) return false;
            if (room == Seam && result.contacts.Count == Route.Length)
            {
                if (result.firstWaltzTarget != Upper + ":station_0" || result.stageTwoRoom != Upper || result.masteryRoom != Lower ||
                    uses.Count < 80 || !result.shadedCrossing || !result.closedBeforeInvestigation || !result.dropLanding ||
                    !bot.Record.Get(SkillRules.SilverStitch).acquired || !investigated || surveyed.Count != Route.Length ||
                    string.Join("/", result.longRooms) != string.Join("/", Wings, Roof, Chandelier, Hall) ||
                    string.Join("/", result.dropRooms) != Wings + "/" + Hall)
                    throw new InvalidOperationException("Missing opera contact, crossing, mastery or drop evidence");
                return true;
            }
            if (action == "use:return_call" || area.ActiveRow?.room == "*") { bot.Drive(PlaytestStep.SafeReturn); return false; }
            if (area.ActiveRow != null && action == null) return false;
            if ((room == Upper || room == Lower) && !bot.Record.firstTransferPending)
            {
                if (action == "cross:floor")
                {
                    var first = area.Room.stationMarks[0];
                    bot.OperaPosition(first.surface, first.x + 2.1f);
                    return false;
                }
                if (WorkStation(bot, keys)) return false;
            }
            if (action != null)
            {
                var point = area.Room.Point(action.Substring(action.IndexOf(':') + 1));
                if (point != null && (action.StartsWith("use:") || action.StartsWith("ride:") || action.StartsWith("walk:")))
                {
                    if (bot.OperaPosition(point.surface, point.x) && action.StartsWith("use:")) keys.Tap(KeyCode.F);
                    return false;
                }
            }
            // While a stopwatch runs the destination is the comparison landing. Otherwise
            // follow the outstanding roster target, including one outside its activation zone.
            bool timingTrip = action == null && (longStart > 0 && result.longSeconds == 0 || dropStart > 0 && !result.dropLanding);
            var target = timingTrip ? null : area.DistrictWaveTarget;
            if (target != null) { bot.OperaFight(target); return false; }
            if (action != null)
            {
                if (action.StartsWith("advance:")) Door(bot, keys, area.ActiveRow.alternateRoom);
                else if (action == "clear:wave")
                {
                    if (room == Roof) bot.OperaPosition("gutter", 36);
                    else bot.OperaDoor(area.Room.exits[1]);
                }
                return false;
            }
            if (room == Wings && !investigated && !result.closedBeforeInvestigation)
            {
                if (closedFrame < 0) { if (Door(bot, keys, Hall)) closedFrame = Time.frameCount; }
                else if (Time.frameCount > closedFrame + 1)
                {
                    var d = Array.Find(area.Room.exits, e => e.room == Hall);
                    result.closedBeforeInvestigation = d.InReach(area.Rea.Body.position) && !investigated;
                    if (!result.closedBeforeInvestigation) throw new InvalidOperationException("Missed closed hatch input");
                    publish();
                }
                return false;
            }
            if (room == Stairs) { Door(bot, keys, "hub_lift"); return false; }
            if (investigated && bot.Record.Get(SkillRules.SilverStitch).acquired)
            {
                if (Compare(bot, keys, now)) return false;
                if (room == Hall && result.dropLanding) { Door(bot, keys, Seam); return false; }
            }
            int idx = Array.IndexOf(Route, room);
            if (idx >= 0 && idx < 5) Door(bot, keys, Route[idx + 1]);
            return false;
        }
    }
}
