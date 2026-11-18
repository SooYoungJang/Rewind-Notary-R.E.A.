using System;
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
        internal static readonly int[] Limits = { 45, 100, 240, 180, 45, 600, 180, 150, 45 };
        private readonly BotKeyboard keys;
        internal GameBootstrap Game { get; private set; }
        internal GameShell Shell { get; private set; }
        internal RepresentativeArea Area => Game == null ? null : Game.Area;
        internal SkillSession Session => Game == null ? null : Game.ActiveSkills;
        internal CoreRecord Record => Session?.Progression.Record ?? Shell?.MenuRecord;
        private CombatMotor2D Player => Area.Rea;
        private bool sawEmpty, sawSafeCall;
        private float jumpUntil;
        private readonly int clockIndex = Array.FindIndex(StageCatalog.All, s => s.Id == "clock");

        internal FirstDepartureBot(BotKeyboard keyboard) { keys = keyboard; }
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

        internal void Drive(PlaytestStep step)
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
                case ShellPage.Pause:
                    Select(0);
                    return;
                case ShellPage.Playing: break;
                default: return;
            }
            if (Area == null || !GameInput.CanPlay) return;
            if (Player.Health.Dead) { keys.Tap(KeyCode.R); return; }
            if (Time.time < jumpUntil) keys.Hold(KeyCode.Space);
            if (Area.Bout != null)
            {
                FightBoss();
                return;
            }
            string action = Area.PendingAction;
            if (action == "use:return_call")
            {
                if (Area.OnReturnFloor)
                {
                    keys.Tap(KeyCode.F);
                    if (keys.GetKeyDown(KeyCode.F)) sawSafeCall = true;
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
                if (action.StartsWith("strike:", StringComparison.Ordinal)) StrikeStoryPoint(point.x);
                else if (action.StartsWith("walk:", StringComparison.Ordinal)) Walk(point.x, .38f);
                else if (action.StartsWith("use:", StringComparison.Ordinal) && Walk(point.x, .38f)) keys.Tap(KeyCode.F);
                return;
            }
            if (Record.firstTransferPending && !Has("returned_for_mid_invest"))
            {
                if (!Area.OnReturnFloor) SeekFloor();
                return;
            }
            if (Area.Room.seamRuns != null && Area.Room.seamRuns.Length > 0 &&
                Record.Get(SkillRules.SilverStitch).acquired && !Record.firstTransferPending && SewReturnRoute()) return;
            var target = Area.RouteTarget;
            if (target != null) { FightTarget(target); return; }
            var door = Area.ForwardDoor;
            if (door == null) return;
            if (door.approach != null && door.approach.y > 1 && !ReachBalcony()) return;
            if (Walk(door.x, .45f) && door.InReach(Player.Body.position)) keys.Tap(KeyCode.F);
            else if (Area.Room.id == "velvet_01_entry" && Player.Grounded &&
                door.x - Player.Body.position.x > DefenseTuning.DashDistance + 1f) keys.Tap(KeyCode.K);
        }

        internal void Select(int index)
        {
            if (Shell.MenuIndex == index) keys.Tap(KeyCode.Return);
            else keys.Tap(Shell.MenuIndex < index ? KeyCode.DownArrow : KeyCode.UpArrow);
        }

        private bool Walk(float to, float tolerance)
        {
            float dx = to - Player.Body.position.x;
            if (Mathf.Abs(dx) <= tolerance) return true;
            int facing = dx < 0 ? -1 : 1;
            keys.Hold(facing < 0 ? KeyCode.A : KeyCode.D);
            // A low solid plank can also lie between Rea and a door, without a walk row.
            var feet = Player.Body.position + Vector2.up * .18f;
            if (Player.Grounded && Physics2D.Raycast(feet, Vector2.right * facing, .95f, 1 << LevelBuilder.GroundLayer))
            {
                jumpUntil = Time.time + .24f;
                keys.Hold(KeyCode.Space);
            }
            return false;
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
                keys.Hold(dx < 0 ? KeyCode.A : KeyCode.D);
                return false;
            }
            return true;
        }
        private void Strike(float x)
        {
            if (Aim(x) && !Player.Attacking) keys.Tap(KeyCode.J);
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
                if (Player.Grounded) keys.Tap(KeyCode.J);
            }
            else if (Mathf.Abs(dx) < maxRange)
            {
                // Back out far enough to turn by walking toward the point again.
                keys.Hold(dx < 0 ? KeyCode.D : KeyCode.A);
            }
            else Walk(x, 0f);
        }
        private void FightTarget(WaveTarget2D target)
        {
            if (target.State.Cleared) return;
            bool aimed = Aim(target.transform.position.x);
            if (target.StrikeIn <= .085f)
            {
                keys.Tap(KeyCode.L);
                return;
            }
            if (!aimed || Player.Attacking || target.State.LastSword?.Closed == false) return;
            if (target.State.Ready(Time.time)) keys.Tap(KeyCode.J);
            else if (target.State.NeedsSetup) keys.Cast();
        }
        private void FightBoss()
        {
            if (Area.Room.id == "velvet_04_arena" && Player.Body.position.y > 1)
            { MoveTo(0); return; }
            float strikeIn = Area.Bout.StrikeIn;
            if (strikeIn <= .09f)
            {
                keys.Tap(KeyCode.L);
                return;
            }
            float x = Area.BoutOpponent.Body.position.x;
            if (strikeIn <= .38f) { Aim(x); return; }
            Strike(x);
        }

        internal WorldState Snapshot()
        {
            var r = Record;
            var t = Area?.RouteTarget;
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
