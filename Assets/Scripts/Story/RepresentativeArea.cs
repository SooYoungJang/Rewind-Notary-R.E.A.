using System;
using UnityEngine;

namespace RewindNotary
{
    [DefaultExecutionOrder(-150)]
    public sealed partial class RepresentativeArea : MonoBehaviour
    {
        private RepresentativeScript script;
        private CombatMotor2D player, opponent;
        private GameShell shell;
        private SkillSession session;
        private CoreRecord Record => session.Progression.Record;
        public StoryRoom Room { get; private set; }
        public StoryRow ActiveRow { get; private set; }
        public bool DialogueOpen { get; private set; }
        public bool CanSkip => ActiveRow != null && Has(ActiveRow.ReadFlag);
        private bool worldAction, committed, relocate;

        private int actionIndex, suspendedIndex;
        private StoryRow suspendedRow;
        private bool suspendedWorldAction;
        private string Benefactor => string.IsNullOrEmpty(Room.benefactor) ? "mikhail" : Room.benefactor;
        private float attackAt;
        private bool Has(string flag) => Record.storyFlags.Contains(flag);
        private void Put(string flag) { if (!Has(flag)) Record.storyFlags.Add(flag); }
        private bool InspectPressed => GameInput.InvestigateDown && !GameInput.RetryDown && !GameInput.JumpDown
            && !GameInput.AttackDown && !GameInput.CastDown && !GameInput.DashDown && !GameInput.ParryDown;
        private bool Safe => player.Grounded && !player.Health.Dead && !player.Attacking && !player.Stunned
            && !player.Defense.BlocksActions && !player.BenefactorSkills.BlocksActions;

        public void Initialize(SkillSession skills, GameShell owner, CombatMotor2D actor, CombatMotor2D enemy)
        {
            session = skills; shell = owner; player = actor; opponent = enemy;
            script = RepresentativeScript.Load();
            if (script.RestoreTransferReceipt(Record)) session.Save();
            InitArt();
            player.GetComponent<CanvasCarveSkill>().StrokeDrawn += ColorGalleryStroke;
            player.BasicStrikeDelivered += OnStrike;
            session.Progression.Event += OnDistrictEffect;
            Restore();
        }
        private string FloorPrefix => "scene_floor:" + Record.lastStageId + ":";
        private void Checkpoint(StoryRoom room, string floor)
        {
            Record.lastStageId = room.stage;
            string prefix = FloorPrefix;
            Record.storyFlags.RemoveAll(f => f.StartsWith(prefix, StringComparison.Ordinal));
            Put(prefix + floor);
        }

        public void Restore()
        {
            double loadStarted = AutoTestRuntime.LoadStart();
            string marker = Record.storyFlags.Find(f => f.StartsWith(FloorPrefix, StringComparison.Ordinal));
            string savedFloor = marker?.Substring(FloorPrefix.Length);
            Room = Array.Find(script.rooms, r => r.stage == Record.lastStageId && r.Floor(savedFloor) != null)
                ?? Array.Find(script.rooms, r => r.stage == Record.lastStageId);
            StoryMark floor = Room.Floor(savedFloor) ?? Room.floors[0];
            ActiveRow = null; worldAction = committed = DialogueOpen = false; actionIndex = 0;
            session.Progression.CalibrationDormantSkill = Room.id == "hub_workroom" && Has("exp_step2_complete") && !Has("exp_step3_complete")
                ? RepresentativeScript.FirstSkill(Record) : null;
            suspendedRow = null;
            DistrictTrial = string.IsNullOrEmpty(Room.skill) ? null : BenefactorTrialCatalog.Load().ForSkill(Room.skill);
            PrepareCourtRoom();
            StopBout();
            StopFieldTrial();
            session.Progression.Trials.Configure(InFieldTrial ? DistrictTrial.stageId : Room.stage, InFieldTrial, resume: InFieldTrial);
            BuildRoom();
            player.ResetAt(new Vector2(floor.x, LandingY(floor.id)));
            UpdateActivationZones();
            player.GetComponent<PlayerController2D>().ClearInput();
            session.EncounterId = InFieldTrial ? DistrictTrial.stageId : Room.id;
            AutoTestRuntime.LoadEnd("room", Room.id, loadStarted);
        }
        private void Travel(StoryRoom destination, string floorId = null)
        {
            // Enter on a fixed floor. A row with a different destination supplies its own floor.
            relocate = committed = true;
            Checkpoint(destination, destination.Floor(floorId)?.id ?? destination.floors[0].id);
            session.Save();
        }
        private bool Effect()
        {
            if (ActiveRow.effect.StartsWith("healed:", StringComparison.Ordinal))
            {
                if (RepresentativeScript.FirstSkill(Record) == ActiveRow.effect.Substring(7) && !Has("investigation_complete"))
                    Put("first_transfer_seen");
                return true;
            }
            switch (ActiveRow.effect)
            {
                case "": break;
                case "copy": return session.Story.CompleteMartaAtticHandoff();
                case "keepsake": return session.Story.CompleteLukaKeepsakeGuide(Benefactor);
                case "ledger": session.Investigation.RecordStoryClue("route_ledger", "story:mid_ledger"); break;
                case "waveform": session.Investigation.RecordStoryClue("route_waveform", "story:mid_waveform"); break;
                case "defeat": session.Progression.CompleteEncounter("story:" + Benefactor + "_bout", string.IsNullOrEmpty(Room.skill) ? SkillRules.SilverStitch : Room.skill); break;
                case "duelHelp": return session.Story.CompleteDuelHelp(Benefactor);
                case "shelterHelp": return session.Story.CompleteShelterHelp(Benefactor);
                case "recharge": return session.Progression.Trials.ConfirmStoryRecharge(DistrictTrial);
                case "endTrial": Record.storyFlags.Remove("trial_active:" + Benefactor); break;
                case "middle": return session.Investigation.CompleteMiddleInvestigation();
                case "openCassel": Put("cassel_open"); break;
                case "liftMenu": break; // The logical destination is committed with the row.
                case "openDistricts": Put("districts_open"); break;
                case "departCourt": Put("janhyang_open"); break;
                default: throw new InvalidOperationException("Unknown story effect: " + ActiveRow.effect);
            }
            return true;
        }
        private bool Complete()
        {
            if (ActiveRow == null || committed) return false;
            string floor = ActiveRow.resumePoint;
            if (floor == "current_room_safe_floor") floor = NearestFloor().id;
            // A reunion played on the return route keeps its local reinforced floor.
            if (ActiveRow.alternateRoom == Room.id && !ActiveRow.SpansWaves) floor = "velvet_return_mid";
            var destination = Array.Find(script.rooms, r => r.Floor(floor) != null);
            if (!session.CommitScene(Effect, () =>
            {
                Put(ActiveRow.ReadFlag);
                Put(ActiveRow.completionFlag);
                Put(ActiveRow.Receipt);
                Checkpoint(destination, floor);
                if (ActiveRow.effect == "liftMenu")
                {
                    Record.storyFlags.Remove("endingScene:True");
                    Record.storyFlags.Remove("endingScene:Basic");
                    Record.lastStageId = EndingCheckpoint.Lift;
                }
            })) return false;
            relocate = destination != Room;
            committed = true;
            return true;
        }
        private StoryMark NearestFloor()
        {
            var nearest = Room.floors[0];
            bool authored = Room.terrain != null && Room.terrain.Length > 0;
            foreach (var f in Room.floors)
            {
                float score = authored ? FloorDistance(f) : Distance(f.x);
                if (score < (authored ? FloorDistance(nearest) : Distance(nearest.x))) nearest = f;
            }
            return nearest;
        }
        public bool ConfirmDialogue(bool skip)
        {
            if (!DialogueOpen || (skip && !CanSkip) || !session.LastSaveSucceeded) return false;
            if (ActiveRow.DialogueOnly)
            {
                if (!Complete()) return false;
            }
            else
            {
                if (!CanSkip) { Put(ActiveRow.ReadFlag); session.Save(); }
                signalAt = Time.time;
                worldAction = true;
            }
            DialogueOpen = false;
            return true;
        }
        private void FinishAction()
        {
            if (ActionToken == "use:calibration_mount" && ActiveRow.completionFlag == "exp_step2_complete")
                session.Progression.CalibrationDormantSkill = RepresentativeScript.FirstSkill(Record);
            if (ActionToken == "use:reflection_plate" && actionIndex > 0)
                session.Progression.CalibrationDormantSkill = null;
            if (actionIndex + 1 < ActiveRow.requiredPlayerAction.Length) actionIndex++;
            else Complete();
        }
        private string ActionToken => ActiveRow.requiredPlayerAction[actionIndex];
        private bool ReadyForAction => ActiveRow != null && worldAction && !committed && session.LastSaveSucceeded
            && shell.Playing && GameInput.CanPlay && !player.Health.Dead;
        private float Distance(float x) => Mathf.Abs(player.transform.position.x - x);
        private void OnStrike(Bounds blade, int damage)
        {
            if (Room.stage == "gallery") RevealGalleryOutline(blade);
            if (!ReadyForAction || !ActionToken.StartsWith("strike:", StringComparison.Ordinal)) return;
            var target = Room.Point(ActionToken.Substring(7));
            if (target != null && blade.Contains(new Vector3(target.x, 1f, 0))) FinishAction();
        }
        private void Update()
        {
            if (!shell.Playing || !GameInput.CanPlay || player.Health.Dead || !session.LastSaveSucceeded) return;
            if (committed)
            {
                if (Record.lastStageId == EndingCheckpoint.Lift || Record.lastStageId == "cassel") return;
                if (ActiveRow?.completionFlag == "luka_lift_complete")
                { shell.FinishDeparture(); return; }
                if (relocate) { Restore(); return; }
                ActiveRow = null; committed = worldAction = false; actionIndex = 0;
                RefreshFixtures();
            }
            bool safeFloor = Safe && OnFixedFloor();
            if (!safeFloor) touchingCheckpoint = null;
            else if ((ActiveRow == null || ((Room.stage == "clock" || Room.stage == "opera" || Room.stage == "bell" || Room.stage == "gallery") && worldAction && !opponent.gameObject.activeSelf))
                && !SaveContactFloor()) return;
            var finaleRow = ActiveRow == null ? script.NextFinale(Room, session.Investigation) : null;
            var cut = Room.stage == "janhyang" || InEnding || finaleRow != null || ActiveRow?.sceneId.StartsWith("post_", StringComparison.Ordinal) == true
                ? null : script.Interrupt(session.Investigation, safeFloor);
            if (cut != null && ActiveRow != cut)
            {
                if (ActiveRow != null && ActiveRow.room != "*")
                {
                    suspendedRow = ActiveRow; suspendedIndex = actionIndex; suspendedWorldAction = worldAction;
                }
                ActiveRow = cut; worldAction = false; actionIndex = 0;
                DialogueOpen = true; shell.OpenStoryDialogue(); return;
            }
            if (ActiveRow == null && suspendedRow != null)
            {
                ActiveRow = suspendedRow; suspendedRow = null;
                worldAction = suspendedWorldAction; actionIndex = suspendedIndex;
            }
            if (ActiveRow == null)
            {
                bool half = opponent.gameObject.activeSelf && opponent.Health.Current <= opponent.Health.Maximum / 2;
                ActiveRow = finaleRow ?? script.Next(Room, session.Investigation, half);
                if (ActiveRow != null)
                {
                    if (ActiveRow.SpansWaves && ActiveRow.alternateRoom == Room.id) actionIndex = 1;
                    if (ActiveRow.completionFlag == "mikhail_i1_clash_halted") StopBout();
                    DialogueOpen = true;
                    shell.OpenStoryDialogue();
                    return;
                }
            }
            if (FightNeeded()) { DriveBout(); return; }
            if (ActiveRow != null)
            {
                if (!ReadyForAction) return;
                if (CourtAction() || DistrictAction()) return;
                if (!Safe) return;
                string token = ActionToken;
                if (token == "use:return_call") { if (OnFixedFloor() && InspectPressed) FinishAction(); return; }
                int split = token.IndexOf(':');
                if (split < 0) return;
                var target = Room.Point(token.Substring(split + 1));
                if (target == null || Vector2.Distance(player.Body.position, new Vector2(target.x, Room.SurfaceTop(target.surface) + .3f)) > 1.25f) return;
                if (token.StartsWith("walk:", StringComparison.Ordinal))
                {
                    if (OnPlank(target.id)) FinishAction();
                }
                else if (token.StartsWith("use:", StringComparison.Ordinal) && InspectPressed) FinishAction();
                return;
            }
            if (Safe && InspectPressed)
            {
                if (Room.id == "hub_lift" && Has("luka_lift_complete") && Distance(Room.Point("luka_freight_platform").x) <= 1.25f)
                { shell.FinishDeparture(); return; }
                if (TryTrialBell() || TryForgeReturn()) return;
                if (NearCore()) shell.OpenStoryCore();
                else
                {
                    var exit = NearestDoor();
                    if (exit != null) Travel(Departure(exit.room), exit.destinationFloor);
                }
            }
        }
        private bool NearCore()
        {
            var terminal = Room.Point("core_terminal");
            return terminal != null && Distance(terminal.x) <= 1.25f;
        }
        private bool FightNeeded()
        {
            if (committed || !session.LastSaveSucceeded) return false;
            if (ActiveRow != null) return worldAction && (ActionToken.StartsWith("win:", StringComparison.Ordinal) || ActionToken.StartsWith("interlude:", StringComparison.Ordinal));
            return Room.id == "velvet_04_arena" && Has("mikhail_i1_01_read") && !Has("mikhail_i1_clash_halted");
        }
        private void DriveBout()
        {
            if (!opponent.gameObject.activeSelf)
            {
                bool belfry = Room.id == "bell_06_arena";
                if (belfry && !StandingOn("refuge")) return;
                bool gallery = Room.id == "gallery_06_arena";
                if (gallery && !StandingOn("sun_deck")) return;
                opponent.gameObject.SetActive(true);
                bool finale = string.IsNullOrEmpty(Room.benefactor) ? Has("mikhail_retreat_plank_down") :
                    ActiveRow.sceneId == Benefactor + "_bout_i1" && ActionToken.StartsWith("win:", StringComparison.Ordinal);
                opponent.name = Benefactor;
                opponent.Health.Initialize(finale ? Room.interludeHealth : Room.boutHealth, .35f);
                opponent.Health.MinimumHealth = ActiveRow == null || ActionToken.StartsWith("interlude:", StringComparison.Ordinal)
                    ? Room.interludeHealth : 0;
                bool mikhail = Room.id == "velvet_04_arena";
                float spawnX = mikhail ? Mathf.Clamp(player.Body.position.x + 5f, 3f, 38f) : 17f;
                float spawnTop = gallery ? Room.SurfaceTop("sun_deck") : belfry ? Room.SurfaceTop("refuge") : Room.Surface("stage_floor") != null ? Room.SurfaceTop("stage_floor") : Room.SurfaceTop("drum_floor");
                opponent.ResetAt(new Vector2(spawnX, spawnTop + .1f));
                if (mikhail || belfry || gallery)
                {
                    mikhailBout = opponent.gameObject.AddComponent<BenefactorTrialOpponent>();
                    mikhailBout.StartStoryBout(player, belfry || gallery ? DistrictTrial : mikhailTrial, Room.interludeHealth,
                        gallery ? surfaces["sun_deck"].bounds : belfry ? surfaces["refuge"].bounds : (Bounds?)null);
                    AudioManager.SetMusic(AudioManager.Music.Boss);
                }
                attackAt = Time.time + .8f;
            }
            if (ActiveRow != null && ActionToken.StartsWith("interlude:", StringComparison.Ordinal)
                && opponent.Health.Current <= Room.interludeHealth)
            { if (Complete()) StopBout(); return; }
            if (opponent.Health.Dead) { if (Complete()) StopBout(); return; }
            if (mikhailBout != null) return;
            if (opponent.Stunned || opponent.Temporal.Frozen || opponent.GetComponent<StitchBinding>().Bound) return;
            opponent.RefreshGround();
            float dx = player.transform.position.x - opponent.transform.position.x;
            opponent.Face(dx);
            opponent.Move(Mathf.Abs(dx) > 1.2f ? Mathf.Sign(dx) * 2.8f : 0);
            if (Mathf.Abs(dx) < 1.5f && Time.time >= attackAt)
            { opponent.Attack(); attackAt = Time.time + 1.35f; }
        }
        internal void FlushVictory()
        {
            if (!committed && worldAction && ActiveRow != null && ActionToken.StartsWith("win:", StringComparison.Ordinal)
                && opponent.gameObject.activeSelf && opponent.Health.Dead && Complete()) StopBout();
        }
        private BenefactorTrialOpponent mikhailBout;
        private void StopBout()
        {
            if (mikhailBout != null)
            {
                if (opponent.Health.Dead)
                {
                    AudioManager.Play(AudioManager.Sfx.BossDefeat);
                    ImpactFeedback.BossDefeated();
                }
                if (Benefactor == "irina") SkillProjectile2D.ClearAll();
                AudioManager.SetMusic(AudioManager.Music.Field);
                mikhailBout.enabled = false;
                Destroy(mikhailBout);
                mikhailBout = null;
                opponent.Animation.Tint(Color.white);
            }
            opponent.gameObject.SetActive(false);
            opponent.Health.MinimumHealth = 0;
            opponent.Health.Initialize(0, 0);
        }
        private void OnDestroy()
        {
            if (player != null)
            {
                player.BasicStrikeDelivered -= OnStrike;
                var canvas = player.GetComponent<CanvasCarveSkill>();
                if (canvas != null) canvas.StrokeDrawn -= ColorGalleryStroke;
            }
            if (session != null) session.Progression.Event -= OnDistrictEffect;
            foreach (var sprite in rosterArt.Values) Destroy(sprite);
            if (square != null) Destroy(square);
            if (brass != null) Destroy(brass);
            if (weave != null) { Destroy(weave.texture); Destroy(weave); }
        }
    }
}
