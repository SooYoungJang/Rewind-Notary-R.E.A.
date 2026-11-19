using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    public sealed partial class RepresentativeArea
    {
        private readonly List<WaveTarget2D> roomTargets = new List<WaveTarget2D>();
        private BenefactorTrialOpponent fieldTrial;
        private readonly List<GameObject> stations = new List<GameObject>();
        private int stationIndex;
        private StitchSurface2D sewn;
        private bool stationHit;
        private string switchUse;
        private float signalAt;
        private BenefactorTrialDefinition DistrictTrial { get; set; }
        private bool InFieldTrial => Room.boutHealth > 0 && Has("trial_active:" + Benefactor);
        private bool BellReady
        {
            get
            {
                var d = DistrictTrial;
                return d != null && Room.Point(d.id + "_trial_bell") != null && Has(d.id + "_support_first_granted") &&
                    !BenefactorTrialProgress.Has(Record, d, "charged") &&
                    BenefactorTrialProgress.Count(Record, d, "field", d.normalUsesToRenew) == d.normalUsesToRenew;
            }
        }

        private bool TryTrialBell()
        {
            if (!BellReady) return false;
            var bell = Room.Point(Benefactor + "_trial_bell");
            if (Distance(bell.x) > 1.25f || Room.stage == "opera" && !StandingOn(bell.surface)) return false;
            var arena = Array.Find(script.rooms, r => r.stage == Room.stage && r.boutHealth > 0);
            if (!session.CommitScene(() =>
            {
                foreach (var row in script.rows)
                    if (row.sceneId == Benefactor + "_trial") Record.storyFlags.Remove(row.completionFlag);
                Put("trial_active:" + Benefactor);
                session.Progression.Trials.Configure(DistrictTrial.stageId, true);
                return true;
            }, () => Checkpoint(arena, arena.floors[0].id))) return false;
            relocate = committed = true;
            return true;
        }

        private void StopFieldTrial()
        {
            if (fieldTrial == null) return;
            fieldTrial.enabled = false;
            Destroy(fieldTrial);
            fieldTrial = null;
            SkillProjectile2D.ClearAll();
        }

        private StoryRoom Departure(string destination)
        {
            // Visit outstanding pre-investigation reunions on the walk from the sanctuary to the lift.
            if (destination == "hub_lift" && Has("investigation_complete") && !Has("luka_lift_complete"))
                foreach (var room in script.rooms)
                    if (room.id.Contains("_10_sanctuary_") && Has(room.benefactor + "_defeated") &&
                        !Has(room.benefactor + "_i1_01_read") && !Has(room.benefactor + "_support_first_granted"))
                        return Room.id == "hub_workroom" || Room.id == "hub_forge" ? script.Room("hub_sanctuary") : room;
            return script.Room(destination);
        }

        private float StationX(int i) => Room.stationMarks == null || Room.stationMarks.Length == 0 ? 7 + 7 * i : Room.stationMarks[i].x;
        private float StationTop(int i) => Room.stationMarks == null || Room.stationMarks.Length == 0 ? 0f : Room.SurfaceTop(Room.stationMarks[i].surface);
        private void BuildPaintLedges(Color stone)
        {
            float left = -4;
            for (int i = 0; i < Room.stations; i++)
            {
                float end = StationX(i) - .7f;
                Block("ledge_" + i, (end + left) / 2, -1, end - left, 2, stone, true);
                left = end + 1.4f;
            }
            Block("last_ledge", (left + 46) / 2, -1, 46 - left, 2, stone, true);
        }

        private sealed class Emitter
        {
            public Vector2 origin;
            public string id;
            public SkillProjectile2D shot;
            public float next;
            public bool spent, overhead;
        }
        private readonly List<Emitter> emitters = new List<Emitter>();
        private Rigidbody2D movingSpan, raisedLift;
        private float liftBase;
        private const float LiftHeight = 1.4f;
        private bool LiftRaised => Room.id == "opera_06_arena" ? Has("valeria_safe_landed") : Has("valeria_reunion_landed");

        private void BuildCrossing(Color stone)
        {
            Block("west_bank", 11, -1, 30, 2, stone, true);
            Block("east_bank", 39, -1, 14, 2, stone, true);
            if (Room.crossing == "steps" || Room.crossing == "arch")
            {
                Block("lower_step", 26, .15f, 2.4f, .3f, stone, true);
                Block("upper_step", 29, .55f, 2.4f, .3f, stone, true);
                Block("landing_step", 32, .25f, 2.4f, .3f, stone, true);
                return;
            }
            var span = Block("span", 29, .18f, Room.crossing == "swing" ? 2.6f : 6.4f, .32f, stone, true);
            if (Room.crossing == "chandelier") return;
            // Dynamic spans never count as safe floors for a first-transfer checkpoint.
            fixedFloors.Remove(span.GetComponent<BoxCollider2D>());
            movingSpan = span.AddComponent<Rigidbody2D>();
            movingSpan.bodyType = RigidbodyType2D.Kinematic;
        }

        private void FixedUpdate()
        {
            if (Room == null || !GameInput.GameplayEnabled) return;
            UpdateOperaShutter();
            if (movingSpan != null)
            {
                if (Room.crossing == "swing")
                    movingSpan.MovePosition(new Vector2(29 + 2 * Mathf.Sin(Time.fixedTime * Mathf.PI * .8f), .18f));
                else movingSpan.GetComponent<BoxCollider2D>().enabled = Room.crossing == "latches"
                    ? latches.TrueForAll(t => t.State.Cleared) : Mathf.Repeat(Time.fixedTime, 4f) < 3f;
            }
            if (raisedLift != null && worldAction && ActiveRow != null && ActionToken.StartsWith("ride:", StringComparison.Ordinal))
            {
                bool aboard = OnPlank(ActionToken.Substring(5));
                float y = Mathf.MoveTowards(raisedLift.position.y, liftBase + (aboard ? LiftHeight : 0), 1.2f * Time.fixedDeltaTime);
                raisedLift.MovePosition(new Vector2(raisedLift.position.x, y));
            }
        }

        private void DrawHandSignal()
        {
            if (Benefactor != "gabriel" || !opponent.gameObject.activeSelf ||
                !(Has("gabriel_signals_agreed") || fieldTrial != null)) return;
            var trial = DistrictTrial;
            string signal = fieldTrial != null ? fieldTrial.PatternLabel :
                trial.patterns[Time.time + .6f >= attackAt ? 0 : 1].Label;
            var screen = Camera.main.WorldToScreenPoint(opponent.transform.position + Vector3.up * 2.7f);
            var position = GUI.matrix.inverse.MultiplyPoint3x4(new Vector3(screen.x, Screen.height - screen.y, 0));
            GUI.Box(new Rect(position.x - 160, position.y - 14, 320, 30), signal);
        }

        private void BuildDistrictFixtures(Color stone)
        {
            switchUse = null; stationIndex = -1; sewn = null; stations.Clear();
            if (DistrictTrial == null) return;
            foreach (string id in new[] { "luka_opera_lift", "valeria_reunion_lift" })
                if (fixtures.TryGetValue(id, out var lift))
                {
                    raisedLift = lift.AddComponent<Rigidbody2D>();
                    raisedLift.bodyType = RigidbodyType2D.Kinematic;
                    liftBase = raisedLift.position.y;
                    if (LiftRaised) raisedLift.position += Vector2.up * LiftHeight;
                    break;
                }
            for (int i = 0; i < Room.stations; i++)
            {
                float x = StationX(i);
                string id = Room.id + ":station_" + i;
                if (Room.skill == BenefactorIds.Canvas) continue; // Each gap supports actual ground-cast paint.
                if (Room.projectileStations && (Room.stationMarks == null || Room.stationMarks.Length == 0 || Room.stationMarks[i].projectile))
                {
                    bool overhead = Room.stage == "opera";
                    float y = StationTop(i) + (overhead ? 3.45f : 1.3f);
                    emitters.Add(new Emitter { origin = new Vector2(x, y), id = id, overhead = overhead });
                    if (overhead) OperaLampHousing(id, x, y + .22f);
                }
                else
                {
                    var station = Place(id, x, StationTop(i) + (Room.skill == BenefactorIds.Waltz ? 2.4f : Room.skill == BenefactorIds.Forge ? .55f : .05f));
                    stations.Add(station);
                    if (Room.skill == BenefactorIds.Waltz)
                    {
                        var lamp = station.AddComponent<PracticeLamp2D>();
                        if (Room.stage == "opera") lamp.SetPicture(OperaSprite("opera-lamp", 1.4f));
                    }
                    else if (Room.skill == BenefactorIds.Forge) station.AddComponent<ResonanceTarget2D>().InitializeMechanism();
                    else
                    {
                        var target = station.AddComponent<WaveTarget2D>();
                        target.Initialize(waves.Archetype("sword_latch"),
                            new WaveSpawn { setupSkill = Room.skill, segments = 1 }, player, square, 1, true);
                        if (Room.stage == "clock") target.SetPicture(RosterPicture("clock_switch"));
                    }
                }
            }
            if (Room.Point("julius_return_platform") != null)
            {
                var shutter = Block("julius_timed_shutter", 28, Room.SurfaceTop("drum_floor") + 3.4f,
                    .45f, 2.4f, stone, true);
                shutter.AddComponent<AuthoredSurface>();
                fixedFloors.Remove(shutter.GetComponent<BoxCollider2D>());
                fixtures["shutter"] = shutter;
            }
        }

        private void UpdateDistrictFixtures()
        {
            if (Room == null || !shell.Playing) return;
            // The linked lamp/bell becomes usable when its neighbouring seam is stitched.
            if (Room.stitchAnchors > 0 && stations.Count > 0 && Room.skill != BenefactorIds.Pendulum)
                for (int i = 0; i < stations.Count; i++)
                {
                    bool open = string.IsNullOrEmpty(Room.seamId) || Has(Room.stage + "_synergy_complete") ||
                        (sewn != null && sewn.TargetId == Room.seamId + "_" + i) ||
                        (ActiveRow != null && ActiveRow.stepId == "02_elite_clear");
                    if (stations[i].activeSelf != open) stations[i].SetActive(open);
                }
            foreach (var emitter in emitters)
            {
                if (emitter.spent || emitter.shot != null || Distance(emitter.origin.x) > 5f || Time.time < emitter.next) continue;
                if (emitter.overhead && (Distance(emitter.origin.x) > 1.15f ||
                    Mathf.Abs(player.Body.position.y - (emitter.origin.y - 3.45f)) > .65f)) continue;
                var go = Place(emitter.id, emitter.origin.x, emitter.origin.y);
                emitter.shot = go.AddComponent<SkillProjectile2D>();
                emitter.shot.Initialize(player, opponent);
                float direction = player.Body.position.x < emitter.origin.x ? -1 : 1;
                emitter.shot.ConfigureTrialShot(emitter.overhead ? Vector2.down * 2 : Vector2.right * direction * 3, 1, 2);
                emitter.next = Time.time + 2.5f;
            }
            if (!worldAction || ActiveRow == null) return;
            if (ActionToken.StartsWith("timed:", StringComparison.Ordinal))
            {
                bool open = ReturnWindowOpen;
                fixtures[ActionToken.Substring(6)].GetComponentInChildren<SpriteRenderer>().color = open ? Color.cyan : Color.gray;
                if (fixtures.TryGetValue("shutter", out var shutter))
                    shutter.transform.position = new Vector2(28, Room.SurfaceTop("drum_floor") + (open ? 3.4f : 1.2f));
            }
        }
        private bool ReturnWindowOpen => Mathf.Repeat(Time.time - signalAt, 6f) < 4f;

        private void OnDistrictEffect(ProgressionEvent e)
        {
            if (Room == null || Room.stations == 0 || e.skillId != Room.skill || e.targetId == null) return;
            // A later contact in the same activation can operate a switch after an earlier enemy earned the use.
            if (e.kind != "valid_use" && !(e.kind == "use_rejected" && e.reason == "same_activation")) return;
            string prefix = Room.id + ":station_";
            if (e.targetId.StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(e.targetId.Substring(prefix.Length).Split(':')[0], out int index))
            {
                if (roofCue != null && index == Room.stations - 1) roofLampLit = true;
                stationIndex = index;
                switchUse = e.useId; stationHit = true;
                var emitter = emitters.Find(item => item.id == e.targetId);
                if (emitter != null) emitter.spent = true;
            }
            if (Room.skill == BenefactorIds.Canvas && e.targetId.EndsWith(":support", StringComparison.Ordinal))
                for (int i = 0; i < Room.stations; i++)
                    if (Distance(StationX(i)) < 2f && (string.IsNullOrEmpty(Room.seamId) ||
                        (sewn != null && sewn.TargetId == Room.seamId + "_" + i)))
                    { stationIndex = i; stationHit = true; }
        }

        private bool DistrictAction()
        {
            if (DistrictTrial == null) return false;
            string token = ActionToken;
            if (token == "verify:" + Benefactor)
            {
                var d = DistrictTrial;
                if (BenefactorTrialProgress.Count(Record, d, "success", d.requiredSuccesses) == d.requiredSuccesses)
                { if (Complete()) { StopBout(); StopFieldTrial(); } }
                else if (fieldTrial == null)
                {
                    opponent.gameObject.SetActive(true); opponent.name = d.opponentId;
                    fieldTrial = opponent.gameObject.AddComponent<BenefactorTrialOpponent>();
                    fieldTrial.Initialize(player, session, d);
                    player.ResetAt(new Vector2(d.playerX, .1f));
                }
                return true;
            }
            if (token.StartsWith("latch:", StringComparison.Ordinal))
            {
                if (latches.Count > 0 && latches.TrueForAll(t => t.State.Cleared)) FinishAction();
                return true;
            }
            if (token == "clear:wave" || token.StartsWith("advance:", StringComparison.Ordinal))
            {
                if (!Safe || !roomTargets.TrueForAll(t => t.State.Cleared)) return true;
                if (token == "clear:wave")
                {
                    if (player.Body.position.x >= 35 || ActiveRow.SpansWaves) FinishAction();
                }
                else
                {
                    var door = Array.Find(Room.exits, e => e.room == ActiveRow.alternateRoom);
                    if (door != null && Gate(door) && door.InReach(player.Body.position) && InspectPressed)
                    {
                        Put("crossed:" + ActiveRow.completionFlag);
                        Travel(script.Room(door.room), door.destinationFloor);
                    }
                }
                return true;
            }
            if (token.StartsWith("station:", StringComparison.Ordinal))
            {
                bool timedReturn = Room.skill == BenefactorIds.Pendulum && string.IsNullOrEmpty(Room.seamId);
                if (stationHit && (!timedReturn || player.GetComponent<PendulumRewindSkill>().LastReturnUse == switchUse))
                {
                    if (Room.skill == BenefactorIds.Forge && sewn != null) sewn.Cut(false);
                    FinishAction();
                }
                else if (timedReturn && stationIndex >= 0)
                    stations[stationIndex].GetComponent<WaveTarget2D>().RearmSwitch();
                return true;
            }
            if (token == "sew:seam")
            {
                sewn = StitchSurface2D.All.Find(s => s.TargetId.StartsWith(Room.seamId + "_", StringComparison.Ordinal) && s.Installed &&
                    (Room.skill != BenefactorIds.Pendulum || s.TargetId == Room.seamId + "_" + stationIndex));
                if (sewn != null) { stationHit = false; FinishAction(); }
                return true;
            }
            if (token == "cross:floor")
            {
                int i = Mathf.Max(0, stationIndex);
                bool correctTier = Room.stationMarks == null || Room.stationMarks.Length == 0 || StandingOn(Room.stationMarks[i].surface);
                if (correctTier && Safe && OnFixedFloor() && player.Body.position.x > StationX(i) + 1.5f) FinishAction();
                return true;
            }
            if (token.StartsWith("ride:", StringComparison.Ordinal))
            {
                if (raisedLift != null && raisedLift.position.y >= liftBase + LiftHeight - .01f && OnPlank(token.Substring(5)))
                    FinishAction();
                return true;
            }
            if (token.StartsWith("timed:", StringComparison.Ordinal))
            {
                if (ReturnWindowOpen && Safe && OnPlank(token.Substring(6))) FinishAction();
                return true;
            }
            return false;
        }

        private bool DistrictHint(string[] action, out string hint)
        {
            hint = "";
            if (DistrictTrial == null) return false;
            var point = Room.Point(action[1]);
            if (point != null)
            {
                string key = action[0] == "latch" ? "area.strike" : point.platform ? "area.walk" : "area.travel";
                hint = Loc.Get(key, Loc.Get("area.target." + point.id)) + Arrow(point.x); return true;
            }
            switch (action[0])
            {
                case "win": case "interlude": hint = Loc.Get("area.bout", DistrictTrial.DisplayName); return true;
                case "verify": hint = DistrictTrial.ObjectiveLabel + "  " + session.Progression.Trials.SessionStatus; return true;
                case "advance": case "clear":
                    var target = roomTargets.Find(t => !t.State.Cleared);
                    hint = target != null ? target.Hint + Arrow(target.transform.position.x) :
                        action[0] == "advance" ? Loc.Get("area.travel", script.Room(action[1]).Label) + Arrow(Array.Find(Room.exits, e => e.room == action[1]).x) :
                        Loc.Get("area.walk", Room.Label) + Arrow(36);
                    return true;
                case "station": case "sew":
                    hint = session.Progression.Catalog.Get(action[0] == "sew" ? SkillRules.SilverStitch : Room.skill).DisplayName + Arrow(sewn != null ? sewn.transform.position.x + 1.1f : StationX(Mathf.Max(0, stationIndex)));
                    return true;
                case "cross": hint = Loc.Get("area.walk", Room.Label) + Arrow(StationX(Mathf.Max(0, stationIndex)) + 2); return true;
                default: return false;
            }
        }
    }
}
