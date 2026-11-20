using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    [Serializable] public sealed class GalleryContact
    {
        public string room, floor, targetBand;
        public float feetX, feetY;
        public double segmentSeconds, districtSeconds, outsideGallerySeconds, roomEntryToContactSeconds, fromEntrySeconds;
    }

    [Serializable] public sealed class PaintWitness
    {
        public string target, activation;
        public float feetX, feetY, width, top;
    }

    [Serializable] public sealed class GalleryDistrictResult
    {
        public StepResult run = new StepResult { id = "GalleryDistrict", expected = "Slot 6, ten saved landings, upper viewing route, first-return canvas mastery and framed shortcut" };
        public List<GalleryContact> contacts = new List<GalleryContact>();
        public List<TerrainWidth> widths = new List<TerrainWidth>();
        public List<string> stationTargets = new List<string>(), longRooms = new List<string>(), shortcutRooms = new List<string>();
        public List<string> branchContacts = new List<string>();
        public List<PaintWitness> paintContacts = new List<PaintWitness>();
        public string viewingRoute;
        public bool firstStrokeExpiredOnPermanentFloor, eliteCleared, portraitBalconyReached;
        public int slot, combatUses, priorTerrainCount, combinedTerrainCount;
        public string firstPaintTarget, stageTwoRoom, masteryRoom;
        public string[] previousSlotsBefore, previousSlotsAfter;
        public bool closedBeforeInvestigation, shortcutLanded, previousSlotsUnchanged, slotReadback;
        public double longSeconds, shortcutSeconds;
    }

    internal sealed class GalleryDistrictProbe
    {
        private const string Entry = "gallery_01_entry", White = "gallery_02_white_hall", Bridge = "gallery_03_easel_bridge",
            Garden = "gallery_04_greenhouse_path", Foyer = "gallery_05_frame_foyer", Arena = "gallery_06_arena",
            Atelier = "gallery_07_return_atelier", Vault = "gallery_08_return_gallery", Window = "gallery_10_sanctuary_window", Terrace = "gallery_09_shortcut";
        private static readonly string[] Route = { Entry, White, Bridge, Garden, Foyer, Arena, Atelier, Vault, Window, Terrace };
        private static readonly string[] Trip = { Foyer, Garden, Bridge, White, Bridge, Garden, Foyer, White };
        private readonly GalleryDistrictResult result;
        private readonly Action publish;
        private readonly HashSet<string> uses = new HashSet<string>(), stations = new HashSet<string>(), surveyed = new HashSet<string>();
        private int station, closedFrame = -1, leg = -1;
        private string previousRoom;
        private PaintSurface2D firstStroke;
        private int branchKicks;
        private bool wasInside;
        private double start, lastContact, sampled, entered, outside, longStart, shortcutStart;

        internal GalleryDistrictProbe(GalleryDistrictResult result, Action publish)
        { this.result = result; this.publish = publish; }

        internal void ObserveUse(ProgressionEvent e, RepresentativeArea area)
        {
            string room = area?.Room?.id;
            if (e.kind != "valid_use" || e.skillId != BenefactorIds.Canvas) return;
            if (room == Terrace && e.targetId.StartsWith(room + ":station_", StringComparison.Ordinal) && e.targetId.EndsWith(":support", StringComparison.Ordinal))
                MeasurePaint(e, area);
            if (room != Atelier && room != Vault) return;
            if (!uses.Add(e.useId)) throw new InvalidOperationException("Duplicate paint activation " + e.useId);
            if (uses.Count == 1) result.firstPaintTarget = e.targetId;
            if (e.after == 30) result.stageTwoRoom = room;
            if (e.after == 80) result.masteryRoom = room;
            if (e.targetId.StartsWith(room + ":station_", StringComparison.Ordinal) && e.targetId.EndsWith(":support", StringComparison.Ordinal))
            {
                if (e.targetId != room + ":station_" + station + ":support") throw new InvalidOperationException("Out-of-order painted support " + e.targetId);
                if (!stations.Add(e.targetId)) throw new InvalidOperationException("Repeated paint station " + e.targetId);
                var paint = MeasurePaint(e, area);
                if (uses.Count == 1) firstStroke = paint;
                result.stationTargets.Add(e.targetId);
            }
            else if (e.targetId.EndsWith(":posture", StringComparison.Ordinal)) result.combatUses++;
            else throw new InvalidOperationException("Unexpected paint credit " + e.targetId);
        }

        private PaintSurface2D MeasurePaint(ProgressionEvent e, RepresentativeArea area)
        {
            var paint = PaintSurface2D.All.Find(p => p.Live && p.StationId + ":support" == e.targetId);
            var actor = area.Rea.GetComponent<Collider2D>();
            var m = Array.Find(area.Room.stationMarks, mark => Mathf.Abs(mark.x - area.Rea.Body.position.x) <= .70f &&
                Mathf.Abs(area.Rea.Body.position.y - area.Room.SurfaceTop(mark.surface) - .25f) < .16f);
            if (m == null || paint == null || !paint.GetComponent<Collider2D>().IsTouching(actor) || area.OnReturnFloor ||
                Mathf.Abs(paint.Bounds.size.x - area.Rea.GetComponent<CanvasCarveSkill>().StrokeWidth) > .005f ||
                Mathf.Abs(paint.Bounds.size.y - SkillGeometry.PaintThickness) > .005f)
                throw new InvalidOperationException("Missing physical painted support " + e.targetId);
            result.paintContacts.Add(new PaintWitness { target = e.targetId, activation = e.useId,
                feetX = area.Rea.Body.position.x, feetY = area.Rea.Body.position.y, width = paint.Bounds.size.x, top = paint.Bounds.max.y });
            return paint;
        }

        private void Observe(FirstDepartureBot bot, double now)
        {
            var area = bot.Area;
            string room = area?.Room?.id;
            bool inside = area?.Room?.stage == "gallery";
            if (start == 0 && room == Entry) start = lastContact = sampled = now;
            if (start > 0 && !wasInside) outside += now - sampled;
            sampled = now;
            wasInside = inside;
            if (room == previousRoom) return;
            if (room == White && !surveyed.Contains(White)) branchKicks = area.Rea.GetComponent<PlayerController2D>().WallJumps;
            if (closedFrame >= 0 && !result.closedBeforeInvestigation && room != Foyer)
                throw new InvalidOperationException("Closed framed passage admitted inspect input");
            if (previousRoom == Atelier && (room != Vault || station != 40))
                throw new InvalidOperationException("Incomplete first atelier crossing");
            if (previousRoom == Vault && (result.masteryRoom != Vault || !bot.Record.storyFlags.Contains("first_transfer_seen")))
                throw new InvalidOperationException("First archive visit ended before mastery");
            if (room == Atelier || room == Vault)
            {
                if (surveyed.Contains(room)) throw new InvalidOperationException("Repeated first-return room");
                station = 0;
            }
            if (inside && surveyed.Add(room)) TerrainWidth.Measure(area, result.widths);
            if (leg >= 0 && leg + 1 < Trip.Length && room == Trip[leg + 1])
            {
                leg++;
                if (leg <= 3) result.longRooms.Add(room);
                if (leg == 7) result.shortcutRooms.Add(room);
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
            if (area.LastContactFloor != floor.id || !area.StandingOn(floor.surface) || Mathf.Abs(feet.x - floor.x) > .8f || !bot.Session.LastSaveSucceeded ||
                !bot.Record.storyFlags.Contains("scene_floor:gallery:" + floor.id))
            {
                if (area.ActiveRow != null && area.PendingAction == null) return false;
                bot.ClockPosition(floor.surface, floor.x);
                return true;
            }
            double interval = now - lastContact, inside = interval - outside;
            result.contacts.Add(new GalleryContact { room = area.Room.id, floor = floor.id, feetX = feet.x, feetY = feet.y,
                segmentSeconds = interval, districtSeconds = inside, outsideGallerySeconds = outside, roomEntryToContactSeconds = now - entered,
                fromEntrySeconds = now - start, targetBand = inside < 180 ? "below-target" : inside > 300 ? "above-target" : "within-target" });
            lastContact = now; outside = 0; publish();
            return false;
        }

        private bool Door(FirstDepartureBot bot, BotKeyboard keys, string destination)
        {
            var area = bot.Area;
            var room = area.Room;
            // After the sanctuary visit the story rows are complete, but reloading a room rebuilds its wave.
            if (room.stage == "gallery" && surveyed.Contains(Window) && room.id != Terrace && area.DistrictWaveTarget is WaveTarget2D target)
            {
                bot.GalleryFight(target);
                return false;
            }
            var door = Array.Find(room.exits, d => d.room == destination);
            if (door == null || !bot.GalleryDoor(door)) return false;
            keys.Tap(GameInput.InteractKey);
            return GameInput.InvestigateDown;
        }

        private bool Compare(FirstDepartureBot bot, BotKeyboard keys, double now)
        {
            var area = bot.Area;
            if (result.shortcutLanded || leg < 0 && area.Room.id != Foyer) return false;
            if (leg < 0 || leg == 6 && shortcutStart == 0)
            {
                var head = area.Room.Floor("gallery_frame_head");
                if (!bot.ClockPosition(head.surface, head.x) || !area.StandingOn(head.surface)) return true;
                if (leg < 0) { leg = 0; longStart = now; result.longRooms.Add(Foyer); }
                else { shortcutStart = now; result.shortcutRooms.Add(Foyer); }
            }
            if (leg == 3 && result.longSeconds == 0 || leg == 7)
            {
                var foot = area.Room.Floor("gallery_frame_landing");
                if (!bot.ClockPosition(foot.surface, foot.x) || !area.StandingOn(foot.surface)) return true;
                if (leg == 3) result.longSeconds = now - longStart;
                else
                {
                    result.shortcutSeconds = now - shortcutStart;
                    result.shortcutLanded = bot.Session.LastSaveSucceeded && bot.Record.storyFlags.Contains("scene_floor:gallery:" + foot.id);
                    if (!result.shortcutLanded || result.shortcutSeconds <= 0 || result.shortcutSeconds >= result.longSeconds)
                        throw new InvalidOperationException("Framed passage did not shorten the trip between the two landings");
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
                if (bot.Session?.ActiveSlot == 6 && bot.Record.slotId == "slot-6" && !bot.Record.testData) result.slot = 6;
                else
                {
                    if (page == ShellPage.Playing || page == ShellPage.StoryDialogue) keys.Tap(KeyCode.Escape);
                    else if (page == ShellPage.Pause) bot.Select(3);
                    else if (page == ShellPage.Main) bot.Select(1);
                    else if (page == ShellPage.Slots)
                    {
                        if (result.previousSlotsBefore == null) result.previousSlotsBefore = AutoTestRuntime.ReadPreviousSlotHashes(5);
                        bot.Select(5);
                    }
                    return false;
                }
            }
            if (bot.Session != null && bot.Session.ActiveSlot != 6) throw new InvalidOperationException("Gallery traversal lost slot 6");
            Observe(bot, now);
            var area = bot.Area;
            if (page == ShellPage.Stages) { bot.Select(Array.FindIndex(StageCatalog.All, s => s.Id == "velvet")); return false; }
            if (page != ShellPage.Playing || area == null || !GameInput.CanPlay) { bot.Drive(PlaytestStep.Velvet); return false; }
            if (area.Rea.Health.Dead) throw new InvalidOperationException("Gallery traversal interrupted by death");
            if (bot.TryDistrictHeal()) return false;
            string room = area.Room.id, action = area.PendingAction;
            bool investigated = bot.Record.storyFlags.Contains("investigation_complete");
            bool silver = bot.Record.Get(SkillRules.SilverStitch).acquired;
            if (area.Room.stage != "gallery")
            {
                if (area.ActiveRow == null && (room == "hub_lift" && (!investigated || silver) || room == "velvet_05_return" && silver))
                    Door(bot, keys, room == "hub_lift" ? Entry : "hub_lift");
                else bot.Drive(PlaytestStep.Velvet);
                return false;
            }
            if (area.BoutOpponent != null) { bot.FightBoss(); return false; }
            if (room != Terrace && Contact(bot, now)) return false;
            if (action == "use:return_call" || area.ActiveRow?.room == "*") { bot.Drive(PlaytestStep.SafeReturn); return false; }
            if (area.ActiveRow != null && action == null) return false;
            if (room == White && result.viewingRoute == null)
            {
                var vestibuleLatch = area.DistrictWaveTarget;
                if (vestibuleLatch != null && vestibuleLatch.transform.position.x < 10)
                { bot.GalleryFight(vestibuleLatch); return false; }
                var path = Array.Find(area.Room.viewingRoutes, r => r.id == "upper_frames");
                var lower = Array.Find(area.Room.viewingRoutes, r => r.id == "lower_sculptures");
                for (int i = 1; i < lower.surfaces.Length - 1; i++)
                    if (area.StandingOn(lower.surfaces[i])) throw new InvalidOperationException("Left the upper viewing route");
                string id = path.surfaces[result.branchContacts.Count];
                var deck = area.Room.Surface(id).rect;
                if (bot.ClockPosition(id, deck.x + deck.width / 2) && area.StandingOn(id))
                {
                    result.branchContacts.Add(id);
                    if (result.branchContacts.Count == path.surfaces.Length)
                    {
                        if (branchKicks != area.Rea.GetComponent<PlayerController2D>().WallJumps)
                            throw new InvalidOperationException("Viewing route used a wall kick");
                        result.viewingRoute = path.id;
                        Debug.Log("AUTOTEST gallery viewingRoute=" + path.id + " contacts=" + string.Join("/", result.branchContacts));
                        publish();
                    }
                }
                return false;
            }
            if (room == Atelier || room == Vault)
            {
                var marks = area.Room.stationMarks;
                if (room == Atelier && station == 1 && !result.firstStrokeExpiredOnPermanentFloor)
                {
                    if (area.StandingOn(marks[0].landingSurface) && firstStroke == null)
                    {
                        result.firstStrokeExpiredOnPermanentFloor = area.OnReturnFloor;
                        if (!result.firstStrokeExpiredOnPermanentFloor) throw new InvalidOperationException("Missing permanent landing after paint expired");
                    }
                    return false;
                }
                if (station > 0 && (station == marks.Length || marks[station].surface != marks[station - 1].landingSurface))
                {
                    var guard = area.GalleryTurnTarget(marks[station - 1].landingSurface);
                    if (guard != null) { bot.GalleryFight(guard); return false; }
                }
                if (station < marks.Length)
                {
                    var m = marks[station];
                    string id = room + ":station_" + station;
                    if (stations.Contains(id + ":support"))
                    {
                        if (bot.LeavePaint(m)) station++;
                        return false;
                    }
                    if (!bot.Record.firstTransferPending)
                    {
                        bot.PaintGap(m, id);
                        return false;
                    }
                }
            }
            if (room == Terrace)
            {
                if (area.StandingOn("portrait_balcony")) result.portraitBalconyReached = true;
                var first = area.Room.stationMarks[0];
                if (action == "sew:seam") { bot.SewGallerySeam(); return false; }
                if (action == "station:canvas_carve") { bot.PaintGap(first, room + ":station_0"); return false; }
                if (action == "cross:floor") { bot.LeavePaint(first); return false; }
                if (area.DistrictWaveTarget != null) { bot.GalleryFight(area.DistrictWaveTarget); return false; }
                if (Contact(bot, now)) return false;
                result.priorTerrainCount = AutoTestRuntime.Report.operaDistrict.widths.Count + AutoTestRuntime.Report.bellDistrict.widths.Count;
                result.combinedTerrainCount = result.priorTerrainCount + result.widths.Count;
                result.eliteCleared = area.ClearedWaveTargets == area.WaveTargetCount && area.WaveTargetCount > 0;
                if (result.priorTerrainCount != 142 || result.contacts.Count != Route.Length || result.firstPaintTarget != Atelier + ":station_0:support" ||
                    result.stageTwoRoom != Atelier || result.masteryRoom != Vault || uses.Count < 80 || surveyed.Count != Route.Length ||
                    !result.firstStrokeExpiredOnPermanentFloor || result.viewingRoute == null || !result.portraitBalconyReached || !result.eliteCleared ||
                    !bot.Record.storyFlags.Contains("gallery_synergy_crossed") || !investigated || !silver ||
                    !result.closedBeforeInvestigation || !result.shortcutLanded || string.Join("/", result.longRooms) != string.Join("/", Foyer, Garden, Bridge, White) ||
                    string.Join("/", result.shortcutRooms) != Foyer + "/" + White)
                    throw new InvalidOperationException("Missing gallery route, paint or shortcut evidence");
                return true;
            }
            if (action != null && action.StartsWith("use:", StringComparison.Ordinal))
            {
                var p = area.Room.Point(action.Substring(4));
                if (p != null && bot.ClockPosition(p.surface, p.x)) keys.Tap(GameInput.InteractKey);
                return false;
            }
            if (room == Arena && (action == "win:irina" || action == "interlude:irina"))
            { bot.ClockPosition("sun_deck", 19); return false; }
            if (investigated && silver && action == null && Compare(bot, keys, now)) return false;
            if (area.DistrictWaveTarget != null && (action != null || room == Window))
            { bot.GalleryFight(area.DistrictWaveTarget); return false; }
            if (action != null)
            {
                if (action.StartsWith("advance:", StringComparison.Ordinal)) Door(bot, keys, area.ActiveRow.alternateRoom);
                else if (action == "clear:wave") bot.GalleryDoor(area.Room.exits[1]);
                return false;
            }
            if (room == Foyer && !investigated && !result.closedBeforeInvestigation)
            {
                if (closedFrame < 0) { if (Door(bot, keys, White)) closedFrame = Time.frameCount; }
                else if (Time.frameCount > closedFrame + 1)
                {
                    var door = Array.Find(area.Room.exits, d => d.room == White);
                    result.closedBeforeInvestigation = door.InReach(area.Rea.Body.position) && !investigated;
                    if (!result.closedBeforeInvestigation) throw new InvalidOperationException("Missed closed framed passage input");
                    publish();
                }
                return false;
            }
            if (room == Window) Door(bot, keys, "hub_lift");
            else if (room == White && result.shortcutLanded) Door(bot, keys, Terrace);
            else
            {
                int i = Array.IndexOf(Route, room);
                if (i >= 0 && i < 5) Door(bot, keys, Route[i + 1]);
            }
            return false;
        }
    }
}
