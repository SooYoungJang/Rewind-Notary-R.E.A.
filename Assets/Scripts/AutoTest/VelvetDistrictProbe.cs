using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [Serializable] public sealed class VelvetContact
    {
        public string room, floor, from, targetBand;
        public double segmentSeconds, fromEntrySeconds, roomEntryToContactSeconds, outsideVelvetSeconds, districtSeconds;
        public float feetX, feetY;
    }

    [Serializable] public sealed class VelvetDistrictResult
    {
        public StepResult run = new StepResult { id = "VelvetDistrict", expected = "Six physical checkpoints and the investigated upper return" };
        public int targetMinSeconds = 180, targetMaxSeconds = 300;
        public List<VelvetContact> contacts = new List<VelvetContact>();
        public List<string> returnDoors = new List<string>();
        public bool closedOnFirstVisit, arrivedAtUpperBridge, arrivedAtArena;
        public string firstSilverTarget;
        public int returnUses, shortSeams, guardPreparations, upperSupports;
        public double shortcutSeconds, outboundViaGallerySeconds;
    }

    internal sealed class VelvetDistrictProbe
    {
        private const string Entry = "velvet_01_entry", Loom = "velvet_02_loom", Gallery = "velvet_03_gallery",
            Arena = "velvet_04_arena", Waterway = "velvet_05_return", Bridge = "velvet_06_shortcut";
        private static readonly string[] Rooms = { Entry, Loom, Gallery, Arena, Waterway, Bridge };
        private readonly VelvetDistrictResult result;
        private readonly HashSet<string> uses = new HashSet<string>();
        private readonly HashSet<string> seams = new HashSet<string>();
        private double start, last, outside, previous, roomEntered, upperDoorTime, lowerDoorTime;
        private bool wasOutside, inSecondSlot, dropping;
        private int closedInputFrame = -1;
        private string previousRoom;
        private readonly Action publish;

        internal VelvetDistrictProbe(VelvetDistrictResult output, Action save)
        { result = output; publish = save; }

        internal void ObserveUse(ProgressionEvent e, string room)
        {
            if (room != Waterway || e.skillId != SkillRules.SilverStitch || e.kind != "valid_use") return;
            if (uses.Add(e.useId))
            {
                if (uses.Count == 1) result.firstSilverTarget = e.targetId;
                result.returnUses = uses.Count;
                if (e.targetId.StartsWith(Waterway + ":seam_", StringComparison.Ordinal) && seams.Add(e.targetId))
                {
                    if (e.targetId.EndsWith(":support", StringComparison.Ordinal)) result.upperSupports++;
                    else result.shortSeams++;
                }
                else if (e.targetId.EndsWith(":posture", StringComparison.Ordinal)) result.guardPreparations++;
            }
        }

        private void Contact(FirstDepartureBot bot, double now)
        {
            var area = bot.Area;
            int i = result.contacts.Count;
            if (i >= Rooms.Length || area?.Room.id != Rooms[i] || !bot.Session.LastSaveSucceeded) return;
            var mark = Array.Find(area.Room.floors, f => f.contactSave);
            if (mark == null || !area.StandingOn(mark.surface) || Mathf.Abs(area.Rea.Body.position.x - mark.x) > .8f ||
                !bot.Record.storyFlags.Contains("scene_floor:velvet:" + mark.id)) return;
            double span = now - last;
            double inDistrict = span - outside;
            result.contacts.Add(new VelvetContact {
                room = area.Room.id, floor = mark.id, from = i == 0 ? Entry : result.contacts[i - 1].floor,
                feetX = area.Rea.Body.position.x, feetY = area.Rea.Body.position.y, roomEntryToContactSeconds = now - roomEntered,
                segmentSeconds = span, fromEntrySeconds = now - start, outsideVelvetSeconds = outside, districtSeconds = inDistrict,
                targetBand = inDistrict < result.targetMinSeconds ? "below-target" :
                    inDistrict > result.targetMaxSeconds ? "above-target" : "within-target"
            });
            last = now; outside = 0;
            publish();
        }

        internal bool Drive(FirstDepartureBot bot, BotKeyboard keys, double now)
        {
            var page = bot.Shell.Page;
            if (!inSecondSlot)
            {
                if (bot.Session?.ActiveSlot == 2) inSecondSlot = true;
                else
                {
                    if (page == ShellPage.Playing || page == ShellPage.StoryDialogue) keys.Tap(KeyCode.Escape);
                    else if (page == ShellPage.Pause) bot.Select(3);
                    else if (page == ShellPage.Main) bot.Select(1);
                    else if (page == ShellPage.Slots) bot.Select(1);
                    return false;
                }
            }
            if (bot.Session != null && bot.Session.ActiveSlot != 2)
                throw new InvalidOperationException("District probe left its independent slot");
            var area = bot.Area;
            string room = area?.Room.id;
            if (room != previousRoom) roomEntered = now;
            if (start == 0 && room == Entry) start = last = previous = now;
            if (start > 0)
            {
                if (wasOutside) outside += now - previous;
                wasOutside = area?.Room.stage != "velvet";
                previous = now;
                Contact(bot, now);
            }
            bool investigated = bot.Record?.storyFlags.Contains("investigation_complete") == true;
            if (room != previousRoom)
            {
                if (room != null && (room == Loom && investigated || result.returnDoors.Count > 0))
                    result.returnDoors.Add(room);
                if (room == Bridge)
                {
                    if (previousRoom != Loom || !investigated ||
                        !bot.Record.storyFlags.Contains("scene_floor:velvet:velvet_upper_safe"))
                        throw new InvalidOperationException("Wrong bridge arrival");
                }
                if (room == Arena && previousRoom == Gallery) result.outboundViaGallerySeconds = now - lowerDoorTime;
                if (room == Arena && previousRoom == Bridge)
                {
                    result.arrivedAtArena = bot.Record.storyFlags.Contains("scene_floor:velvet:mikhail_arena_west");
                    result.shortcutSeconds = now - upperDoorTime;
                }
                if (previousRoom == Waterway && !investigated && result.returnUses < 80)
                    throw new InvalidOperationException("Left the first waterway visit below 80 valid uses");
                previousRoom = room;
            }
            if (room == Bridge && !result.arrivedAtUpperBridge)
            {
                var ingress = area.Room.Floor("velvet_upper_safe");
                result.arrivedAtUpperBridge = area.StandingOn(ingress.surface) &&
                    Mathf.Abs(area.Rea.Body.position.x - ingress.x) <= .8f;
                if (!result.arrivedAtUpperBridge) return false;
            }
            if (result.arrivedAtArena)
            {
                if (!area.StandingOn("balcony")) return false;
                if (result.contacts.Count != 6 || !result.closedOnFirstVisit || !result.arrivedAtUpperBridge ||
                    upperDoorTime <= 0 || lowerDoorTime <= 0 ||
                    string.Join("/", result.returnDoors) != Loom + "/" + Bridge + "/" + Arena ||
                    result.firstSilverTarget != Waterway + ":seam_0" || result.returnUses < 80 ||
                    result.shortSeams < 16 || result.guardPreparations < 16 || result.upperSupports < 48)
                    throw new InvalidOperationException("Incomplete district route, seam or contact evidence");
                return true;
            }
            if (page == ShellPage.Stages)
            {
                bot.Select(Array.FindIndex(StageCatalog.All, s => s.Id == "velvet"));
                return false;
            }
            if (room == Loom && !investigated && closedInputFrame >= 0 && !result.closedOnFirstVisit &&
                Time.frameCount > closedInputFrame + 1)
            {
                result.closedOnFirstVisit = bot.Record.storyFlags.Contains("scene_floor:velvet:loom_balcony_floor");
                if (!result.closedOnFirstVisit) throw new InvalidOperationException("Outbound upper door accepted the input");
                publish();
            }
            if (room == Arena && result.contacts.Count == 3 && page == ShellPage.Playing)
                return false; // Let the saved ingress physically settle before leaving for the bout.
            if (page == ShellPage.Playing && area != null && !area.Rea.Health.Dead && area.ActiveRow == null)
            {
                if (room == Loom && area.RouteTarget == null)
                {
                    var upper = Array.Find(area.Room.exits, d => d.room == Bridge);
                    if (!investigated && !result.closedOnFirstVisit)
                    {
                        if (closedInputFrame < 0 && bot.ReachBalcony() && bot.MoveTo(upper.x) &&
                            area.OnReturnFloor && upper.InReach(area.Rea.Body.position) && result.contacts.Count == 2)
                        {
                            keys.Tap(KeyCode.F);
                            if (GameInput.InvestigateDown) closedInputFrame = Time.frameCount;
                        }
                        return false;
                    }
                    if (investigated)
                    {
                        if (bot.ReachBalcony() && bot.MoveTo(upper.x) && area.OnReturnFloor && upper.InReach(area.Rea.Body.position))
                        {
                            keys.Tap(KeyCode.F);
                            if (GameInput.InvestigateDown && upperDoorTime == 0) upperDoorTime = now;
                        }
                        return false;
                    }
                    if (!investigated && !dropping)
                    {
                        var deck = area.Room.Surface("balcony").rect;
                        bot.MoveTo(deck.x + deck.width + 1.4f);
                        dropping = area.OnReturnFloor && area.Rea.Body.position.y < .2f;
                        return false;
                    }
                    var lower = area.ForwardDoor;
                    if (lower != null && bot.MoveTo(lower.x) && area.OnReturnFloor && lower.InReach(area.Rea.Body.position))
                    {
                        keys.Tap(KeyCode.F);
                        if (GameInput.InvestigateDown && lowerDoorTime == 0) lowerDoorTime = now;
                    }
                    return false;
                }
                // The suspended bridge marker lies between arrival and the arena door.
                if (room == Bridge && result.contacts.Count == 5)
                { bot.MoveTo(Array.Find(area.Room.floors, f => f.contactSave).x); return false; }
            }
            bot.Drive(PlaytestStep.Velvet);
            return false;
        }
    }
}
