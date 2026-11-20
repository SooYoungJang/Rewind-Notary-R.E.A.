using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class RepresentativeArea
    {
        private readonly Dictionary<StoryDoor, RoomDoorView> doorViews = new Dictionary<StoryDoor, RoomDoorView>();
        private WaveCatalog waves;
        private BenefactorTrialDefinition mikhailTrial;
        private GameObject layout;
        private readonly List<Collider2D> fixedFloors = new List<Collider2D>();
        private readonly List<WaveTarget2D> latches = new List<WaveTarget2D>();
        private readonly Dictionary<string, GameObject> fixtures = new Dictionary<string, GameObject>();
        private void InitArt()
        {
            waves = WaveCatalog.Load();
            mikhailTrial = BenefactorTrialCatalog.Load().ForSkill(SkillRules.SilverStitch);
        }
        private GameObject Place(string id, float x, float y)
        {
            var go = new GameObject(id);
            go.transform.SetParent(layout.transform, false);
            go.transform.position = new Vector2(x, y);
            return go;
        }
        private GameObject Block(string id, float x, float y, float width, float height, Color color, bool solid = false)
        {
            var go = Place(id, x, y);
            bool woven = Room.stage == "velvet" && Room.terrain != null && Room.terrain.Length > 0;
            bool plated = (Room.stage == "clock" || Room.stage == "opera" || Room.stage == "bell" || Room.stage == "gallery") && Room.terrain != null;
            var art = go;
            if (plated && solid)
            {
                art = new GameObject("Plate") { layer = LevelBuilder.GroundLayer };
                art.transform.SetParent(go.transform, false);
            }
            var visual = art.AddComponent<SpriteRenderer>();
            visual.color = color;
            if (woven || plated)
            {
                visual.drawMode = SpriteDrawMode.Tiled;
                visual.sprite = RoomArt.Tile(Room.stage, plated);
                // Tiled size is already in local units. The sprite pitch only sets the repeat interval.
                visual.size = new Vector2(width, height);
                if (plated && solid && (visual.sprite == null || !(width > 0 && height > 0)))
                    throw new System.InvalidOperationException(Room.id + "/" + id + " has empty tile bounds");
            }
            else visual.sprite = RoomArt.Tile(Room.stage, false);
            if (!woven && !plated) go.transform.localScale = new Vector3(width, height, 1);
            if (solid)
            {
                go.layer = LevelBuilder.GroundLayer;
                var box = go.AddComponent<BoxCollider2D>();
                if (plated) box.autoTiling = false;
                if (woven || plated) box.size = new Vector2(width, height);
                fixedFloors.Add(box);
            }
            return go;
        }
        private void PlaceMarker(string id, float x, float floorY, Sprite sprite, float height)
        {
            var view = Place(id, x, floorY + height * .5f).AddComponent<SpriteRenderer>();
            view.sprite = sprite;
            view.transform.localScale = Vector3.one * (height / sprite.bounds.size.y);
            view.sortingOrder = 2;
        }

        private bool OnFixedFloor()
        {
            var actor = player.GetComponent<Collider2D>();
            return fixedFloors.Exists(f => f.isActiveAndEnabled && f.attachedRigidbody == null &&
                actor.bounds.min.y >= f.bounds.max.y - .1f && f.IsTouching(actor));
        }
        private bool OnPlank(string id) => fixtures[id].GetComponent<Collider2D>().IsTouching(player.GetComponent<Collider2D>());
        private float LandingY(string floor)
        {
            var authored = Room.Surface(Room.Floor(floor)?.surface);
            if (authored != null && (floor == "opera_reunion_lift_floor" || floor == "luka_opera_lift"))
                return Room.SurfaceTop(authored.id) + .38f + (LiftRaised ? LiftHeight : 0);
            if (authored != null && !authored.Moving)
                return authored.rect.y + authored.rect.height + .05f + (Room.Point(floor)?.platform == true ? .32f : 0f);
            var point = Room.Point(floor);
            if (floor == "opera_reunion_lift_floor" || floor == "luka_opera_lift")
                return LiftRaised ? 1.95f : .55f;
            if (point != null && point.platform) return .55f;
            return floor == "mikhail_escape_plank" || floor == "lift_platform_floor" ||
                floor == "sanctuary_bridge_floor" || floor == "velvet_return_mid" ? .55f : .1f;
        }
        private string BridgeFlag => Room.id == "velvet_02_loom" ? "loom_bridge" : "gallery_bridge";
        private bool HasBridge => Room.id == "velvet_02_loom" || Room.id == "velvet_03_gallery";
        private void BuildRoom()
        {
            if (layout != null) { layout.SetActive(false); Destroy(layout); }
            layout = new GameObject(Room.id); layout.transform.SetParent(transform, false);
            entryWave.Clear(); entryMember = 0;
            returnSeams.Clear();
            movingSpan = null; raisedLift = null; emitters.Clear();
            fixedFloors.Clear(); fixtures.Clear(); latches.Clear(); roomTargets.Clear(); stationHit = false;
            var stone = Room.stage == "hub" ? new Color(.36f,.35f,.31f) : new Color(.37f,.22f,.30f);
            bool echoArena = Room.id == "court_02_echo_arena";
            if (echoArena) Block("arena_floor", 47, -1, 30, 2, stone, true);
            else if (Room.Surface("drum_floor") != null)
            {
                // The formal renewal trial uses its original, low arena coordinates.
                if (InFieldTrial) Block("trial_floor", 29, -1, 66, 2, stone, true);
                var crown = Room.cameraBounds.y + Room.cameraBounds.height;
                var foot = Room.SurfaceTop("drum_floor");
                var spindle = Block("winding_arbor", 22, (foot + crown) / 2, 1.3f, crown - foot,
                    new Color(.57f,.65f,.62f)).GetComponent<SpriteRenderer>();
                spindle.sortingOrder = -4;
            }
            else if (Room.stage == "bell" && Room.terrain != null)
            {
                if (InFieldTrial) Block("trial_floor", 52.5f, Room.SurfaceTop("refuge") - 1, 15, 2, stone, true);
            }
            else if ((Room.stage == "opera" || Room.stage == "gallery") && Room.terrain != null)
            {
                if (InFieldTrial) Block("trial_floor", 29, -1, 66, 2, stone, true);
            }
            else if (!string.IsNullOrEmpty(Room.crossing)) BuildCrossing(stone);
            else if (Room.stations > 0 && Room.skill == BenefactorIds.Canvas) BuildPaintLedges(stone);
            else if (Room.Surface("intake_quay") != null)
                DrawWaterway();
            else if (Room.eastBoundary > 0)
            {
                var floor = Block("floor", (Room.eastBoundary - 4) / 2, -1, Room.eastBoundary + 4, 2, stone, true);
                if (InAttic) floor.GetComponent<SpriteRenderer>().enabled = false;
            }
            else Block("floor", HasBridge ? 14 : InFieldTrial ? 29 : 21, -1, HasBridge ? 36 : InFieldTrial ? 66 : 50, 2, stone, true);
            if (HasBridge)
            {
                Block("east", 40, -1, 12, 2, stone, true);
                fixtures["bridge"] = Block("latch_bridge", 33, -.14f, 2.2f, .28f, Color.gray, true);
            }
            bool tall = Room.stage == "clock" || Room.stage == "opera" || Room.stage == "bell" || Room.stage == "gallery" ||
                Room.stage == "velvet" && Room.cameraBounds.y + Room.cameraBounds.height >= 10;
            float wallHeight = tall ? Room.cameraBounds.y + Room.cameraBounds.height + 5 : 10;
            float wallY = tall ? wallHeight / 2 - 1 : 3;
            var westWall = Block("west_wall", echoArena ? 32 : InAttic ? 0.2f : -4, wallY, .4f, wallHeight, stone, true);
            var eastWall = Block("east_wall", echoArena ? 62 : InFieldTrial ? 61 : Room.eastBoundary > 0 ? Room.eastBoundary : 46,
                wallY, .4f, wallHeight, stone, true);
            if (InAttic)
            {
                westWall.GetComponent<SpriteRenderer>().enabled = false;
                eastWall.GetComponent<SpriteRenderer>().enabled = false;
            }
            BuildSurfaces();
            BuildOperaFixtures();
            BuildBellWaves();
            BuildGallery();
            foreach (var f in Room.floors)
            {
                if (!f.contactSave) continue;
                float y = Room.Surface(f.surface) == null ? .025f : LandingY(f.id) - .025f;
                Block(f.id + "_mark", f.x, y, 1.6f, .05f, new Color(1f,.77f,.32f));
            }
            doorViews.Clear();
            foreach (var e in Room.exits)
            {
                bool bounded = e.approach != null && e.approach.Defined;
                float x = bounded ? e.approach.x + e.approach.width * .5f : e.x;
                float y = bounded ? e.approach.y + 1.6f : 1.3f;
                if (e.destinationFloor == "gallery_frame_landing") DrawGalleryPassage(x, y);
                if (e.destinationFloor == "opera_drop_landing" || e.destinationFloor == "bell_drop_landing") DrawDropHatch(x, y);
                if (e.requiresFlag == "clock_04_cleared")
                {
                    var brassFrame = new Color(.88f,.78f,.54f);
                    Block("cage_lintel", x, y + 1.45f, 2.8f, .24f, brassFrame);
                    Block("cage_left", x - 1.23f, y, .16f, 2.95f, brassFrame);
                    Block("cage_right", x + 1.23f, y, .16f, 2.95f, brassFrame);
                    Block("cage_cable", x, y + 3.5f, .12f, 4, brassFrame);
                }
                doorViews.Add(e, new RoomDoorView(Place(e.room, x, y), Room.stage));
            }
            if (InAttic) BuildAtticFurniture();
            else BuildRoomPoints();
            BuildForgeMerchant();
            if (Room.Point("resonant_crucible") != null)
                for (int i = 0; i < 5; i++) Place("crucible_" + i, 10 + i, 1).AddComponent<DestructibleTarget2D>();

            for (int i = 0; i < Room.stitchAnchors; i++)
            {
                float x = string.IsNullOrEmpty(Room.seamId) ? 5 + 25f * i / Mathf.Max(1, Room.stitchAnchors - 1) : StationX(i) - 1.1f;
                float y = .24f;
                if (Room.seamRuns != null && Room.seamRuns.Length > 0)
                {
                    var run = Room.SeamRun(i, out int inRun);
                    x = run.x + inRun * run.stepX;
                    y = run.y;
                }
                Place("seam_" + i, x, y).AddComponent<StitchSurface2D>()
                    .Initialize((Room.seamId ?? Room.id + ":seam") + "_" + i, i < Room.singleSeams ? .9f : 2.2f, i >= Room.stitchAnchors - Room.releaseKnots, player, opponent);
                var marker = Room.stage == "velvet" ? RoomArt.Station(Room.stage) : RoomArt.Picture("seam-marker");
                PlaceMarker("seam_marker_" + i, x - (i < Room.singleSeams ? .45f : 1.1f), y, marker, .68f);
            }
            if (Room.id == "velvet_06_shortcut")
            {
                Place("upper_air_lamp", 8, 2.8f).AddComponent<PracticeLamp2D>();
                Place("upper_cracked_bell", 17, 1).AddComponent<ResonanceTarget2D>().InitializeMechanism();
                Place("upper_pendulum", 23, 1).AddComponent<PracticeTemporalHazard2D>().Initialize(player);
                Block("upper_paint_ledge", 27, 1.35f, 3.6f, .28f, stone, true);
            }
            BuildDistrictFixtures(stone);
            int index = 0;
            foreach (var group in Room.roster)
                for (int i = 0; i < group.count; i++, index++)
                {
                    if (group.silverReturnOnly && !GatheringSilver) continue;
                    var kind = waves.Archetype(group.id);
                    bool latch = group.id == "sword_latch";
                    if (latch && HasBridge && Has(BridgeFlag)) continue;
                    float x = string.IsNullOrEmpty(Room.benefactor) ? 6f + index * 2.25f : 10f + index * 4f;
                    if (latch && Room.Point(Room.stage + "_entry_latch") is StoryMark entry) x = entry.x;
                    var point = group.placementIds == null || group.placementIds.Length == 0 ? null : Room.Placement(group.placementIds[i]);
                    if (group.placementIds != null && group.placementIds.Length > 0 && point == null)
                        throw new System.InvalidOperationException("Unknown roster placement in " + Room.id);
                    var go = Place(Room.id + ":" + group.id + "_" + index, point == null ? x : point.x, point == null ? .05f : point.y);
                    var target = go.AddComponent<WaveTarget2D>();
                    var spawn = new WaveSpawn { archetypeId = group.id, segments = group.segments,
                        setupSkill = !string.IsNullOrEmpty(group.setupSkill) ? group.setupSkill : kind.gate == "skill" ? SkillRules.SilverStitch : null };
                    // Before learning stitch, the armour seam is exposed to ordinary sword hits.
                    if (kind.gate == "skill" && string.IsNullOrEmpty(group.setupSkill) &&
                        (!string.IsNullOrEmpty(Room.benefactor) || !Record.Get(SkillRules.SilverStitch).acquired))
                    { kind = waves.Archetype("shell_sentry"); spawn.setupSkill = null; }
                    target.Initialize(kind, spawn, player, RoomArt.Enemy(Room.stage, group.id), group.setupHits, group.skillOnly);
                    roomTargets.Add(target);
                    if (point?.active != null && point.active.Defined) activationZones.Add((go, point.active));
                    if (latch) latches.Add(target);
                }
            RefreshFixtures();
        }

        private void BuildRoomPoints()
        {
            foreach (var p in Room.points)
            {
                bool plank = p.platform || p.id == "mikhail_escape_plank" || p.id == "luka_freight_platform" || p.id == "mikhail_reunion_plank";
                if (p.id == "core_terminal" || p.id == "marta_ledger" || p.id == "basic_ending_ledger")
                {
                    var desk = Place(p.id, p.x, Room.SurfaceTop(p.surface) + .65f);
                    var view = desk.AddComponent<SpriteRenderer>();
                    view.sprite = RoomArt.Picture("notary-desk-props");
                    view.transform.localScale = Vector3.one * (1.3f / view.sprite.bounds.size.y);
                    fixtures[p.id] = desk;
                }
                else fixtures[p.id] = Block(p.id, p.x, Room.SurfaceTop(p.surface) + (plank ? .16f : .65f), plank ? 3.4f : .68f, plank ? .32f : 1.3f,
                    plank ? new Color(.68f,.43f,.24f) : new Color(.92f,.70f,.24f), plank);
            }
        }
        private void DrawWaterway()
        {
            var iron = new Color(.28f, .47f, .49f);
            var brass = new Color(.87f, .66f, .35f);
            Block("gauge_body", 50, 2.4f, 1.05f, 8.4f, iron).GetComponent<SpriteRenderer>().sortingOrder = -3;
            for (int i = 0; i < 8; i++)
                Block("gauge_mark_" + i, 50, -1f + i, i % 2 == 0 ? .9f : .5f, .09f, brass).GetComponent<SpriteRenderer>().sortingOrder = -2;
            Block("sluice_lintel", 52, 6.6f, 6, .5f, brass).GetComponent<SpriteRenderer>().sortingOrder = -2;
            foreach (var name in new[] { "silt_bed", "tailrace", "lock_bed" })
            {
                var bed = Room.Surface(name).rect;
                Block(name + "_water", bed.x + bed.width / 2, bed.y + bed.height + .38f,
                    bed.width, .76f, new Color(.17f, .58f, .67f, .7f)).GetComponent<SpriteRenderer>().sortingOrder = -1;
            }
        }

        private void RefreshFixtures()
        {
            if (InAttic) RefreshAtticProps();
            if (fixtures.TryGetValue("mikhail_escape_plank", out var plank)) plank.SetActive(Has("mikhail_retreat_plank_down"));
            if (fixtures.TryGetValue("luka_freight_platform", out var lift))
                lift.transform.position = new Vector2(lift.transform.position.x, Has("investigation_complete") && !Has("luka_signalled") ? 3.5f : .16f);
            if (fixtures.TryGetValue("echo_seal_barrier", out var seal)) seal.SetActive(!Has("janhyang_order_withdrawn"));
            if (fixtures.TryGetValue("echo_side_knot", out var knot)) knot.SetActive(!Has("echo_phase2_shifted"));
            if (HasBridge) fixtures["bridge"].SetActive(Has(BridgeFlag));
            if (framedLandscape != null) framedLandscape.enabled = Has("investigation_complete");
            RefreshDoors();
        }
        private void RefreshDoors()
        {
            foreach (var e in Room.exits)
            {
                bool canPass = Gate(e);
                doorViews[e].Show(canPass, !InAttic || canPass);
            }
        }
        private bool Gate(StoryDoor door)
        {
            if (door.waltzCue && !RoofDoorOpen) return false;
            if (!string.IsNullOrEmpty(door.requiresFlag) && !Has(door.requiresFlag)) return false;
            if (door.room == "velvet_02_loom" && NeedsEntryWave) return false;
            if (door.gate == "courtEntry") return RepresentativeScript.CourtEntry(Record);
            if (door.gate == "settled") return FinaleEndingEvaluator.Evaluate(Record.finalBattleAttempt) != FinaleEnding.None;
            if (door.room == "court_04_tribunal" && !StageCatalog.Find("cassel").Unlocked(Record)) return false;
            if (door.gate == "synergy") return Has("investigation_complete") && Record.Get(Room.skill).acquired && Record.Get(SkillRules.SilverStitch).acquired;
            if (door.gate == "depart") return Has("intro_complete") && (!Has("investigation_complete") || Has("luka_lift_complete"));
            // The first post-investigation departure passes the repaired plank for the pre-investigation victory.
            if (door.room == "hub_lift" && Has("investigation_complete") && Has("mikhail_i0_complete") && !Has("mikhail_support_first_granted")) return false;
            return string.IsNullOrEmpty(door.gate) || Has(door.gate);
        }
        private StoryDoor NearestDoor()
        {
            StoryDoor nearest = null;
            foreach (var e in Room.exits)
                if (Gate(e) && e.InReach(player.Body.position) && (nearest == null || Distance(e.x) < Distance(nearest.x))) nearest = e;
            return nearest;
        }
        private void LateUpdate()
        {
            UpdateForgeMerchant();
            if (shell.Playing) UpdateEntryWave();
            if (shell.Playing) { UpdateActivationZones(); RefreshDoors(); }
            UpdateDistrictFixtures();
            FlushVictory();
            if (!shell.Playing || !HasBridge || Has(BridgeFlag) || !session.LastSaveSucceeded) return;
            if (latches.Count > 0 && latches.TrueForAll(l => l.State.Cleared))
            { Put(BridgeFlag); session.Save(); RefreshFixtures(); }
        }
        internal void FillHudSnapshot(ref GameHudCanvas.HudSnapshot snap)
        {
            snap.LocationTitle = InAtticTutorial ? Loc.Get("tutorial.attic_title") : Room.Label;
            snap.RoomKey = Room.id;
            snap.OpeningTutorial = InAtticTutorial;
            snap.StatusLine = DistrictTrial == null
                ? Loc.Get("area.uses", Record.Get(SkillRules.SilverStitch).validUseCount)
                : Loc.Get("hud.skill_progress", session.Progression.Catalog.Get(Room.skill).DisplayName,
                    Record.Get(Room.skill).validUseCount, Record.Get(Room.skill).stage, "", "");
            if (Room.id == "court_03_lift" && Record.Get(SkillRules.BasicSword).stage < 3)
                snap.SubStatusLine = Loc.Get("stage.finale_survival_locked");
            else if (BenefactorTrialProgress.Has(Record, DistrictTrial ?? mikhailTrial, "unlocked"))
                snap.SubStatusLine = BenefactorTrialProgress.Status(Record, DistrictTrial ?? mikhailTrial);
            else
            {
                var defense = player.Defense.State;
                snap.SubStatusLine = defense.BlocksActions ? Loc.Get("defense.dashing") : Loc.Get("defense.ready");
            }
            snap.NoticeText = session.Notice;
            if (storyBout != null)
            {
                snap.BossVisible = true;
                snap.BossName = DistrictTrial != null ? DistrictTrial.DisplayName : Loc.Get("area.target.mikhail");
                snap.BossPhase = storyBout.PatternLabel;
                snap.BossHp = opponent.Health.Current;
                snap.BossMaxHp = opponent.Health.Maximum;
            }
            else if (courtEcho != null && ActiveRow != null && worldAction && ActionToken.StartsWith("echoHP"))
            {
                snap.BossVisible = true;
                snap.BossName = Loc.Get("area.speaker.rea_echo");
                snap.BossPhase = Loc.Get("hud.echo_phase", courtEcho.Phase, courtEcho.PatternLabel);
                snap.BossHp = opponent.Health.Current;
                snap.BossMaxHp = opponent.Health.Maximum;
            }
            snap.ObjectiveHint = BuildStoryHint();
            snap.ControlsText = InAtticTutorial ? GameInput.FormatOpeningControlsPrompt() :
                GameInput.FormatControlsPrompt(storyBout != null || NeedsEntryWave);
        }
        private string BuildStoryHint()
        {
            if (!session.LastSaveSucceeded) return session.StorageError;
            if (ActiveRow != null && worldAction)
            {
                if (InAttic && AtticActionHint() is string tutorial) return tutorial;
                string[] action = ActionToken.Split(':');
                string key = action[0] == "walk" ? "area.walk" : action[0] == "strike" ? "area.strike"
                    : action[0] == "win" ? "area.bout" : "area.travel";
                var p = Room.Point(action[1]);
                if (action[0] == "echoHP") return Loc.Get("hud.echo_phase", courtEcho.Phase, courtEcho.PatternLabel) + "   " + opponent.Health.Current + " / 12";
                if (action[0] == "train") return Loc.Get("area.target.resonant_crucible") + "   " + Record.Get(SkillRules.BasicSword).validUseCount + " / 400";
                if (action[0] == "clearDoor") return Loc.Get("area.travel", script.Room(action[1]).Label) + Arrow(Room.exits[1].x);
                if (DistrictHint(action, out string districtHint)) return districtHint;
                return Loc.Get(key, Loc.Get("area.target." + action[1])) + (p != null ? Arrow(p.x) : "");
            }
            if (AtticBoardApproach is StoryMark board)
                return Loc.Get("area.travel", Loc.Get("area.target." + board.id)) + Arrow(board.x);
            if (FightNeeded()) return Loc.Get("area.bout", DistrictTrial != null ? DistrictTrial.DisplayName : Loc.Get("area.target.mikhail"));
            if (CanTalkToEthan) return EthanPrompt();
            if (FirstDepartureHint() is string next) return next;
            string hint = "";
            if (BellReady) hint += Loc.Get("area.travel", Loc.Get("area.target." + Benefactor + "_trial_bell")) + Arrow(Room.Point(Benefactor + "_trial_bell").x) + "    ";
            if (Room.id == "hub_lift" && Has("luka_signalled")) hint += Loc.Get("area.signal") + "    ";
            if (Room.Point("core_terminal") != null && !NearCore())
                hint += Loc.Get("area.target.core_terminal") + Arrow(Room.Point("core_terminal").x) + "    ";
            if (NearCore()) hint = Loc.Get("area.travel", Loc.Get("area.target.core_terminal")) + "    ";
            if (ForgeReturn is StoryMark passage)
                hint += Loc.Get("area.travel", Loc.Get("area.target.forge_to_court_lift")) + Arrow(passage.x) + "    ";
            foreach (var e in Room.exits)
                if (Gate(e)) hint += Loc.Get("area.travel", Departure(e.room).Label) + DoorArrow(e) + "    ";
            if (HasBridge && !Has(BridgeFlag)) hint += Loc.Get("area.latch");
            if (Room.id == "hub_attic" && Has("sixth_bowl_stamped")) hint += Loc.Get("area.sixth");
            return hint.Trim();
        }
        public void DrawHUD(GUIStyle title, GUIStyle label)
        {
            var style = new GUIStyle(label) { wordWrap = true };
            GUI.Box(new Rect(20, 18, 806, 110), "");
            GUI.Label(new Rect(34, 24, 780, 35), Room.Label, title);
            GUI.Label(new Rect(34, 63, 770, 60), Loc.Get("hud.rea_health", player.Health.Current, player.Health.Maximum) + "   " +
                (DistrictTrial == null ? Loc.Get("area.uses", Record.Get(SkillRules.SilverStitch).validUseCount) :
                    Loc.Get("hud.skill_progress", session.Progression.Catalog.Get(Room.skill).DisplayName, Record.Get(Room.skill).validUseCount, Record.Get(Room.skill).stage, "", "")) + "   " + session.Notice, style);
            if (Room.id == "court_03_lift" && Record.Get(SkillRules.BasicSword).stage < 3)
                GUI.Label(new Rect(34, 92, 950, 34), Loc.Get("stage.finale_survival_locked"), style);
            else if (BenefactorTrialProgress.Has(Record, DistrictTrial ?? mikhailTrial, "unlocked"))
                GUI.Label(new Rect(34, 92, 950, 34), BenefactorTrialProgress.Status(Record, DistrictTrial ?? mikhailTrial), style);
            if (storyBout != null)
            {
                GUI.Box(new Rect(215, 138, 850, 78), "");
                GUI.Label(new Rect(235, 144, 810, 36), Loc.Get("area.bout", DistrictTrial != null ? DistrictTrial.DisplayName : Loc.Get("area.target.mikhail")) + "   " +
                    opponent.Health.Current + " / " + opponent.Health.Maximum + "   " + storyBout.PatternLabel, style);
                GameBootstrap.Bar(new Rect(235, 190, 810, 12), opponent.Health, new Color(.90f, .48f, .40f));
            }
            DrawHandSignal();
            string hint = "";
            if (!session.LastSaveSucceeded) hint = session.StorageError;
            else if (ActiveRow != null && worldAction)
            {
                string[] action = ActionToken.Split(':');
                string key = action[0] == "walk" ? "area.walk" : action[0] == "strike" ? "area.strike"
                    : action[0] == "win" ? "area.bout" : "area.travel";
                var p = Room.Point(action[1]);
                if (action[0] == "echoHP") hint = Loc.Get("hud.echo_phase", courtEcho.Phase, courtEcho.PatternLabel)
                    + "   " + opponent.Health.Current + " / 12";
                else if (action[0] == "train") hint = Loc.Get("area.target.resonant_crucible") + "   " + Record.Get(SkillRules.BasicSword).validUseCount + " / 400";
                else if (action[0] == "clearDoor") hint = Loc.Get("area.travel", script.Room(action[1]).Label) + Arrow(Room.exits[1].x);
                else if (DistrictHint(action, out string districtHint)) hint = districtHint;
                else hint = Loc.Get(key, Loc.Get("area.target." + action[1])) + (p != null ? Arrow(p.x) : "");
            }
            else if (FightNeeded()) hint = Loc.Get("area.bout", DistrictTrial != null ? DistrictTrial.DisplayName : Loc.Get("area.target.mikhail"));
            else if (FirstDepartureHint() is string next) hint = next;
            else
            {
                if (BellReady) hint += Loc.Get("area.travel", Loc.Get("area.target." + Benefactor + "_trial_bell")) + Arrow(Room.Point(Benefactor + "_trial_bell").x) + "    ";
                if (Room.id == "hub_lift" && Has("luka_signalled")) hint += Loc.Get("area.signal") + "    ";
                if (Room.Point("core_terminal") != null && !NearCore())
                    hint += Loc.Get("area.target.core_terminal") + Arrow(Room.Point("core_terminal").x) + "    ";
                if (NearCore()) hint = Loc.Get("area.travel", Loc.Get("area.target.core_terminal")) + "    ";
                if (ForgeReturn is StoryMark passage)
                    hint += Loc.Get("area.travel", Loc.Get("area.target.forge_to_court_lift")) + Arrow(passage.x) + "    ";
                foreach (var e in Room.exits)
                    if (Gate(e)) hint += Loc.Get("area.travel", Departure(e.room).Label) + DoorArrow(e) + "    ";
                if (HasBridge && !Has(BridgeFlag)) hint += Loc.Get("area.latch");
                if (Room.id == "hub_attic" && Has("sixth_bowl_stamped")) hint += Loc.Get("area.sixth");
            }
            if (storyBout != null || NeedsEntryWave) hint += "\n" + Loc.Get("input.gameplay");
            GUI.Box(new Rect(20, 534, 1240, 168), "");
            GUI.Label(new Rect(35, 545, 1200, 100), hint, style);
            GUI.Label(new Rect(35, 658, 1200, 36), Loc.Get("area.help"), style);
        }
        private string DoorArrow(StoryDoor door)
        {
            var r = door.approach;
            if (r == null || !r.Defined) return Arrow(door.x);
            float x = r.x + r.width / 2;
            if (player.Body.position.x < r.x || player.Body.position.x > r.x + r.width) return Arrow(x);
            return door.InReach(player.Body.position) ? " ◆" : player.Body.position.y < r.y ? " ▲" : " ▼";
        }
        private string Arrow(float x) => Distance(x) <= 1.25f ? " ◆" :
            (x < player.transform.position.x ? " ◀ " : " ▶ ") + Mathf.CeilToInt(Distance(x));
    }
}
