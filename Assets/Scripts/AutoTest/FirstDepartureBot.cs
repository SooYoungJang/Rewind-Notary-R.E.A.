using System;
using System.Collections.Generic;
using UnityEngine;

namespace RewindNotary
{
    internal enum PlaytestStep { NewSlot, Attic, Velvet, Mikhail, Silver, SafeReturn, Investigation, Lift, DistrictTwo }

    internal sealed partial class FirstDepartureBot
    {
        internal static readonly string[] Expectations = {
            "Empty slot 1 starts hub_attic with 120 basic sword uses",
            "Candy placed, contract copy received and sixth bowl stamped",
            "Entry wave cleared, both bridges open and velvet_04_arena reached",
            "Live Mikhail bout won and mikhail_defeated committed",
            "Silver Stitch acquired from story:mikhail_bout and velvet_05_return reached",
            "80 valid silver uses, first transfer on fixed ground and hub_workroom reached",
            "I=1 with both route clues and all calibration steps",
            "Luka signalled and boarded, saved lift_arrival_exit, clock selected",
            "Same slot enters clock_01_entry with investigation and lift receipts"
        };
        internal static readonly int[] Limits = { 45, 165, 240, 180, 45, 600, 180, 150, 45 };
        private readonly BotKeyboard keys;
        private readonly Action<string> stop;
        internal GameBootstrap Game { get; private set; }
        internal GameShell Shell { get; private set; }
        internal RepresentativeArea Area => Game == null ? null : Game.Area;
        internal SkillSession Session => Game == null ? null : Game.ActiveSkills;
        internal CoreRecord Record => Session?.Progression.Record ?? Shell?.MenuRecord;
        private CombatMotor2D Player => Area.Rea;
        private bool sawEmpty, sawSafeCall;
        private bool talkedToMarta;
        private float arrivalTestAt = -1f, martaApproachedAt = -1f;
        private float jumpUntil;
        private readonly List<ContactPoint2D> walkContacts = new List<ContactPoint2D>();
        private const float BlockedWalkSeconds = .2f, BlockedWalkSlop = .01f;
        private Collider2D blockedBy;
        private int blockedFrame = -1, blockedDirection;
        private float blockedX, blockedSince;
        private readonly int clockIndex = Array.FindIndex(StageCatalog.All, s => s.Id == "clock");

        internal FirstDepartureBot(BotKeyboard keyboard, Action<string> stop)
        {
            keys = keyboard;
            this.stop = stop;
        }
        internal void Locate()
        {
            if (Game == null) Game = UnityEngine.Object.FindFirstObjectByType<GameBootstrap>();
            if (Game != null && Shell == null) Shell = Game.GetComponent<GameShell>();
        }
        private bool Has(string flag) => Record.storyFlags.Contains(flag);
        internal bool Passed(PlaytestStep step, bool bossWon)
        {
            if (Record == null || Record.testData || Record.slotId != "slot-1" || Session?.LastSaveSucceeded == false) return false;
            string room = Area?.Room.id;
            var silver = Record.Get(SkillRules.SilverStitch);
            switch (step)
            {
                case PlaytestStep.NewSlot: return sawEmpty && room == "hub_attic" && Session.ActiveSlot == 1 &&
                    arrivalTestAt >= 0f && Time.unscaledTime >= arrivalTestAt + .35f && Shell.Playing &&
                    Record.Get(SkillRules.BasicSword).validUseCount == SkillRules.InitialSwordUses && !silver.acquired;
                case PlaytestStep.Attic: return room == "hub_attic" && Has("mint_candy_placed") &&
                    Has("contract_copy_received") && Record.investigation.clueIds.Contains("contract_copy") && Has("sixth_bowl_stamped");
                case PlaytestStep.Velvet: return room == "velvet_04_arena" && Has("intro_complete") && Has("velvet_entry_wave_cleared") &&
                    Has("loom_bridge") && Has("gallery_bridge");
                case PlaytestStep.Mikhail: return bossWon && Has("mikhail_defeated");
                case PlaytestStep.Silver: return silver.acquired && silver.acquiredEncounterId == "story:mikhail_bout" &&
                    Record.completedEncounters.Contains("story:mikhail_bout") && Has("mikhail_i0_complete") && room == "velvet_05_return";
                case PlaytestStep.SafeReturn: return sawSafeCall && silver.validUseCount >= 80 && silver.stage == 3 &&
                    Record.firstTransferPending && Has("first_transfer_seen") && Has("returned_for_mid_invest") && room == "hub_workroom";
                case PlaytestStep.Investigation: return room == "hub_workroom" && Has(InvestigationService.MiddleInvestigationFlag) &&
                    Has("exp_step1_complete") && Has("exp_step2_complete") && Has("exp_step3_complete") && Has("exp_step4_complete") &&
                    Record.investigation.clueIds.Contains("route_ledger") && Record.investigation.clueIds.Contains("route_waveform");
                case PlaytestStep.Lift: return Has("mikhail_support_first_granted") && Has("luka_signalled") && Has("luka_boarded") && Has("luka_lift_complete") &&
                    Has("scene_floor:hub:lift_arrival_exit") && Shell.Page == ShellPage.Stages && Shell.MenuIndex == clockIndex;
                case PlaytestStep.DistrictTwo: return Record.lastStageId == "clock" && room == "clock_01_entry" &&
                    Has("investigation_complete") && Has("luka_lift_complete") && Session.ActiveSlot == 1;
                default: return false;
            }
        }

        internal void Drive(PlaytestStep step, bool collectUpperSupports = false)
        {
            if (Shell == null) return;
            switch (Shell.Page)
            {
                case ShellPage.Main:
                    if (step == PlaytestStep.NewSlot) Select(1);
                    return;
                case ShellPage.Slots:
                    if (step == PlaytestStep.NewSlot && Shell.MenuIndex == 0 && Shell.EmptyNewSlot)
                    { sawEmpty = true; keys.Tap(KeyCode.Return); }
                    return;
                case ShellPage.Stages:
                    if (step == PlaytestStep.DistrictTwo && Shell.MenuIndex == clockIndex) keys.Tap(KeyCode.Return);
                    return;
                case ShellPage.StoryDialogue:
                    if (Area?.ActiveRow?.completionFlag == "intro_attic_01_read" && !talkedToMarta)
                    {
                        stop("The opening dialogue started before Rea interacted with Marta");
                        return;
                    }
                    Select(0);
                    return;
                case ShellPage.Pause:
                    Select(0);
                    return;
                case ShellPage.Playing: break;
                default: return;
            }
            if (Area == null || !GameInput.CanPlay) return;
            if (Player.Health.Dead) { keys.Tap(KeyCode.Backspace); return; }
            if (step == PlaytestStep.NewSlot)
            {
                if (arrivalTestAt < 0f && Player.Grounded)
                {
                    arrivalTestAt = Time.unscaledTime;
                    keys.Tap(GameInput.InteractKey);
                }
                return;
            }
            if (Time.time < jumpUntil) keys.Hold(KeyCode.Space);
            if (Area.BoutOpponent != null)
            {
                FightBoss();
                return;
            }
            string action = Area.PendingAction;
            if (action == "use:return_call")
            {
                if (Area.OnReturnFloor)
                {
                    keys.Tap(GameInput.InteractKey);
                    if (keys.GetKeyDown(GameInput.InteractKey)) sawSafeCall = true;
                }
                else SeekFloor();
                return;
            }
            if (Area.ActiveRow != null)
            {
                if (action == null) return;
                int split = action.IndexOf(':');
                if (split < 0) return;
                var point = Area.Room.Point(action.Substring(split + 1));
                if (point == null) return;
                if (Area.Room.id == "velvet_05_return" && !Area.StandingOn("inspection_dock") &&
                    (Mathf.Abs(Player.Body.position.y) > .5f || Mathf.Abs(Player.Body.position.x - point.x) > 2f))
                {
                    ClockPosition("inspection_dock", point.x);
                    return;
                }
                if (action.StartsWith("strike:", StringComparison.Ordinal)) StrikeStoryPoint(point.x);
                else if (action.StartsWith("walk:", StringComparison.Ordinal)) Walk(point.x, .38f);
                else if (action.StartsWith("use:", StringComparison.Ordinal) && Walk(point.x, .38f)) keys.Tap(GameInput.InteractKey);
                return;
            }
            if (Area.AtticDialogueTarget is StoryMark dialogueTarget)
            {
                if (!Walk(dialogueTarget.x, .38f)) return;
                if (dialogueTarget.id == "marta")
                {
                    if (martaApproachedAt < 0f) martaApproachedAt = Time.unscaledTime;
                    if (Time.unscaledTime < martaApproachedAt + .4f) return;
                    keys.Tap(GameInput.InteractKey);
                    talkedToMarta |= keys.GetKeyDown(GameInput.InteractKey);
                }
                else keys.Tap(GameInput.InteractKey);
                return;
            }
            if (Record.firstTransferPending && !Has("returned_for_mid_invest"))
            {
                if (!Area.OnReturnFloor) SeekFloor();
                return;
            }
            if (Area.Room.stage == "velvet" && Has("mikhail_i0_complete") && Area.Room.seamRuns?.Length > 0 &&
                Record.Get(SkillRules.SilverStitch).acquired && !Record.firstTransferPending && SewReturnRoute(collectUpperSupports)) return;
            var target = Area.RouteTarget;
            if (target != null)
            {
                var at = target.transform.position;
                var face = FixedFaceAt(at.x, at.y);
                if (face == null || ReachedCombatSurface(face, at.x)) FightTarget(target);
                return;
            }
            if (Area.Room.id == "velvet_04_arena" && Has("investigation_complete"))
            {
                TakeReturnDoor();
                return;
            }
            var door = Area.ForwardDoor;
            if (door == null) return;
            if (Area.Room.stage == "clock" || Area.Room.id == "velvet_05_return")
            {
                if (ClockDoor(door)) keys.Tap(GameInput.InteractKey);
                return;
            }
            if (Area.Room.stage == "velvet" && door.approach?.Defined == true && door.approach.y > 1 && !ReachBalcony()) return;
            if (Walk(door.x, .45f) && door.InReach(Player.Body.position)) keys.Tap(GameInput.InteractKey);
            else if (Area.Room.id == "velvet_01_entry" && Player.Grounded &&
                door.x - Player.Body.position.x > DefenseTuning.DashDistance + 1f) keys.Tap(KeyCode.LeftShift);
        }

        internal void Select(int index)
        {
            if (Shell.MenuIndex == index) keys.Tap(KeyCode.Return);
            else keys.Tap(Shell.MenuIndex < index ? KeyCode.DownArrow : KeyCode.UpArrow);
        }

        private bool Walk(float to, float tolerance, bool approachingLaunch = false)
        {
            float dx = to - Player.Body.position.x;
            if (Mathf.Abs(dx) <= tolerance) return true;
            int facing = dx < 0 ? -1 : 1;
            keys.Hold(facing < 0 ? KeyCode.LeftArrow : KeyCode.RightArrow);
            JumpOverStep(facing, approachingLaunch);
            return false;
        }

        private void JumpOverStep(int facing, bool approachingLaunch = false)
        {
            // A low solid plank can also lie between Rea and a door, without a walk row.
            var feet = Player.Body.position + Vector2.up * .18f;
            if (Player.Grounded && (Physics2D.Raycast(feet, Vector2.right * facing, .95f, 1 << LevelBuilder.GroundLayer)
                || (approachingLaunch && BlockedAtStep(facing))))
            {
                jumpUntil = Time.time + .24f;
                keys.Hold(KeyCode.Space);
            }
        }

        private bool BlockedAtStep(int facing)
        {
            // A solid deck can stop a walk to the next launch above the low plank ray.
            var actor = Player.GetComponent<CapsuleCollider2D>();
            var body = actor.bounds;
            float gravityStep = -Physics2D.gravity.y * Player.Body.gravityScale * Time.fixedDeltaTime;
            if (gravityStep <= 0f) return false;
            float jumpSpeed = PlayerController2D.JumpSpeed;
            int risingTicks = Mathf.FloorToInt(jumpSpeed / gravityStep);
            float highestFeet = body.min.y + risingTicks * Time.fixedDeltaTime *
                (jumpSpeed - (risingTicks + 1) * gravityStep * .5f);
            actor.GetContacts(walkContacts);
            foreach (var hit in walkContacts)
            {
                var other = hit.collider == actor ? hit.otherCollider : hit.collider;
                if (hit.enabled && other != null && other.gameObject.layer == LevelBuilder.GroundLayer &&
                    Mathf.Abs(hit.normal.x) >= .9f && (hit.point.x - body.center.x) * facing > 0 &&
                    other.bounds.max.y <= highestFeet)
                {
                    float x = Player.Body.position.x;
                    if (other != blockedBy || facing != blockedDirection || blockedFrame != Time.frameCount - 1 ||
                        Mathf.Abs(x - blockedX) > BlockedWalkSlop)
                    {
                        blockedBy = other;
                        blockedDirection = facing;
                        blockedX = x;
                        blockedSince = Time.fixedTime;
                    }
                    blockedFrame = Time.frameCount;
                    if (other.bounds.max.y <= body.max.y)
                        return Time.fixedTime - blockedSince >= BlockedWalkSeconds;
                    if (Time.fixedTime - blockedSince >= BlockedWalkSeconds && step.LaunchX == step.LandX)
                        ApproachRaisedLip(other.bounds, body, facing, gravityStep);
                    return false;
                }
            }
            return false;
        }

        private void ApproachRaisedLip(Bounds lip, Bounds body, int facing, float gravityStep)
        {
            var from = terrain[stepFrom].rect;
            var deck = terrain[step.To];
            float deckTop = deck.rect.y + deck.rect.height;
            if (!deck.oneWay || lip.max.y < deckTop || lip.max.y > deckTop + .5f ||
                lip.center.x < deck.rect.x || lip.center.x > deck.rect.x + deck.rect.width) return;

            float y = body.min.y;
            float speed = PlayerController2D.JumpSpeed;
            int ticks = 0;
            while (y < lip.max.y && speed > gravityStep)
            {
                speed -= gravityStep;
                y += speed * Time.fixedDeltaTime;
                ticks++;
            }
            float edge = facing > 0 ? lip.min.x : lip.max.x;
            float launch = edge - facing * (body.extents.x + ticks * Time.fixedDeltaTime * PlayerController2D.MoveSpeed);
            if (launch < from.x + body.extents.x || launch > from.x + from.width - body.extents.x) return;
            step = new FixedSurfaceRoute.Step(step.To, launch, lip.center.x);
        }

        private void SeekFloor()
        {
            // Leave a temporary stitched platform. Fixed room floors span the west side.
            if (Player.Grounded && !Player.Attacking && !Player.Stunned) Walk(2f, .3f);
        }

        private bool Aim(float x)
        {
            float dx = x - Player.Body.position.x;
            float range = Mathf.Abs(dx);
            if (range > 1.15f) { Walk(x, 1.15f); return false; }
            if (range > .06f && dx * Player.Facing <= 0)
            {
                keys.Hold(dx < 0 ? KeyCode.LeftArrow : KeyCode.RightArrow);
                return false;
            }
            return true;
        }
        internal void SewStationSeam(StoryMark mark)
        {
            if (!Area.StandingOn(mark.surface)) { ClockPosition(mark.surface, mark.x); return; }
            if (AimCastFrom(mark.x, Area.Room.seamRuns[0].x < mark.x ? -1 : 1))
                keys.Cast();
        }
        private bool AimCastFrom(float x, int facing)
        {
            float dx = x - Player.Body.position.x;
            if (Player.Facing != facing && dx * facing < .35f) x -= .7f * facing;
            return MoveTo(x, .10f) && Player.Facing == facing && Mathf.Abs(Player.Body.linearVelocity.x) < .2f;
        }
        private void StrikeStoryPoint(float x)
        {
            if (Player.Attacking) return;
            // OnStrike tests a single point at contact time, after the sword's forward step.
            const float minRange = .65f, maxRange = 1.4f;
            float dx = x - Player.Body.position.x;
            float ahead = dx * Player.Facing;
            if (ahead >= minRange && ahead <= maxRange)
            {
                if (Player.Grounded) keys.Tap(GameInput.AttackKey);
            }
            else if (Mathf.Abs(dx) < maxRange)
            {
                // Back out far enough to turn by walking toward the point again.
                keys.Hold(dx < 0 ? KeyCode.RightArrow : KeyCode.LeftArrow);
            }
            else Walk(x, 0f);
        }
        // Longest sword recovery (.56s) plus the lead needed for a dash.
        private const float SafeSwingLead = .65f;

        private bool ReserveWaveDodge(WaveTarget2D target)
        {
            if (target.StrikeIn <= .085f)
            {
                DodgeToward(target.transform.position.x);
                return true;
            }
            return Player.Health.Current * 2 <= Player.Health.Maximum && target.StrikeIn <= SafeSwingLead;
        }

        private void DodgeToward(float x)
        {
            bool right = x >= Player.Body.position.x;
            keys.Release(right ? KeyCode.LeftArrow : KeyCode.RightArrow);
            keys.Hold(right ? KeyCode.RightArrow : KeyCode.LeftArrow);
            keys.Tap(GameInput.DashKey);
        }

        private void FightTarget(WaveTarget2D target)
        {
            if (target.State.Cleared) return;
            bool aimed = Aim(target.transform.position.x);
            if (ReserveWaveDodge(target)) return;
            if (!aimed || Player.Attacking || target.State.LastSword?.Closed == false) return;
            if (target.State.Ready(Time.time)) keys.Tap(GameInput.AttackKey);
            else if (target.State.NeedsSetup) keys.Cast();
        }
        // Down + support selects the grounded healing assist through the ordinary input map.
        internal bool TryDistrictHeal()
        {
            if (!Player.Grounded || Player.Health.Dead || Player.Health.Current == Player.Health.Maximum ||
                !Session.Progression.Trials.OrdinaryCombat || Session.PreviewStage != 0) return false;
            var skill = GameInput.ResolveSkill(Vector2.down, Player.Facing, Player.Grounded);
            var assist = BenefactorTrialCatalog.Load().ForSkill(skill);
            if (assist?.support.effect != "heal" || !BenefactorTrialProgress.Has(Record, assist, "charged") ||
                Player.Health.Maximum - Player.Health.Current < assist.support.heal) return false;
            keys.Hold(KeyCode.DownArrow);
            keys.Tap(KeyCode.H);
            return true;
        }

        internal void FightBoss()
        {
            var foe = Area.BoutOpponent;
            float strikeIn = Area.Bout?.StrikeIn ?? float.PositiveInfinity;
            // An incoming attack can reach us on a low plank or in mid-jump.
            if (strikeIn <= .09f)
            {
                DodgeToward(foe.Body.position.x);
                return;
            }
            var at = foe.Body.position;
            var feet = Player.Body.position;
            if (stepFrom >= 0 || (Player.Grounded && Mathf.Abs(feet.y - at.y) > 1.25f))
            {
                var support = FixedFaceAt(at.x, at.y);
                if (support != null && !ReachedCombatSurface(support, at.x)) return;
            }
            float dx = at.x - feet.x;
            const float swordRange = 1.75f; // Tip of the first swing, measured from Rea's feet.
            if (Mathf.Abs(dx) > swordRange) { Walk(at.x, swordRange); return; }
            if (Mathf.Abs(dx) > .06f && dx * Player.Facing < 0)
            {
                keys.Hold(dx < 0 ? KeyCode.LeftArrow : KeyCode.RightArrow);
                return;
            }
            // Let a hop clear the lip and settle before committing the next swing.
            // Preserve enough of the telegraph to finish the swing and evade the next strike.
            if (Player.Grounded && Mathf.Abs(feet.y - at.y) <= 1.25f && strikeIn > SafeSwingLead &&
                !Player.Attacking && !Player.Stunned && !Player.Defense.BlocksActions)
                keys.Tap(GameInput.AttackKey);
        }

        internal WorldState Snapshot()
        {
            var r = Record;
            var t = Area?.DistrictWaveTarget;
            var p = Area?.Rea;
            return new WorldState {
                page = Shell == null ? "boot" : Shell.Page.ToString(), menuIndex = Shell?.MenuIndex ?? -1, message = Shell?.MenuMessage,
                slot = r?.slotId, stage = r?.lastStageId, room = Area?.Room.id, row = Area?.ActiveRow?.textKey,
                action = Area?.PendingAction, target = t?.TargetId, targetSegments = t?.State.Remaining ?? -1,
                x = p?.Body.position.x ?? 0, y = p?.Body.position.y ?? 0, grounded = p?.Grounded ?? false,
                hp = p?.Health.Current ?? -1, bossHp = Area?.BoutOpponent?.Health.Current ?? -1, safeFloor = Area?.OnReturnFloor ?? false,
                silverOwned = r?.Get(SkillRules.SilverStitch)?.acquired ?? false, silverUses = r?.Get(SkillRules.SilverStitch)?.validUseCount ?? 0,
                flags = r == null ? null : new System.Collections.Generic.List<string>(r.storyFlags),
                encounters = r == null ? null : new System.Collections.Generic.List<string>(r.completedEncounters), saveError = Session?.StorageError
            };
        }
    }
}
