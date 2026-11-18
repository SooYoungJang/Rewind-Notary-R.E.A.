using UnityEngine;

namespace RewindNotary
{
    public sealed partial class GameShell
    {
        private string returnSkill;
        private bool AtBasicSettlement => FinaleEndingEvaluator.Evaluate(selectedRecord.finalBattleAttempt) == FinaleEnding.Basic;
        private StoryRow[] liftRows;
        private StoryRow LiftLine()
        {
            if (selectedRecord == null || !selectedRecord.storyFlags.Contains("resume_at_lift")) return null;
            if (liftRows == null) liftRows = System.Array.FindAll(RepresentativeScript.Load().rows, r => r.presentation == "lift");
            foreach (var row in liftRows)
            {
                if (selectedRecord.storyFlags.Contains(row.completionFlag)) continue;
                if (System.Array.TrueForAll(row.entryCondition, term => term == "lowSword"
                    ? selectedRecord.Get(SkillRules.BasicSword).stage < 3 : selectedRecord.storyFlags.Contains(term))) return row;
            }
            return null;
        }

        private string[] LiftLabels()
        {
            if (LiftLine()?.DialogueOnly == true) return new[] { Loc.Get("area.continue") };
            var choices = new System.Collections.Generic.List<string>();
            if (AtBasicSettlement) choices.Add(Loc.Get("story.resume_previous"));
            choices.Add(Loc.Get("stage.cassel.name"));
            choices.Add(Loc.Get("area.room.hub_forge"));
            if (selectedRecord.storyFlags.Contains("true_ending_complete")) choices.Add(Loc.Get("area.room.hub_attic"));
            choices.Add(Loc.Get("ui.stage_select"));
            return choices.ToArray();
        }

        private void DrawCourtLift()
        {
            var line = LiftLine();
            if (line != null)
            {
                string body = SkillSession.StoryText(line);
                float height = textStyle.CalcHeight(new GUIContent(body), 940) + 16;
                dialogueScroll = GUI.BeginScrollView(new Rect(150, 142, 980, 210), dialogueScroll, new Rect(0, 0, 950, height));
                GUI.Label(new Rect(4, 0, 940, height), body, textStyle);
                GUI.EndScrollView();
            }
            var labels = LiftLabels();
            for (int i = 0; i < labels.Length; i++)
            {
                Row(i, labels[i], new Rect(250, (line == null ? 164 : 368) + i * 50, 780, 46));
                if (Page != ShellPage.CourtLift || line != LiftLine()) break;
            }
        }

        private void ActivateLift(int index)
        {
            if (game.ActiveSkills != null) return;
            var line = LiftLine();
            if (line?.DialogueOnly == true)
            {
                SaveLiftChoice(line, null, null);
                return;
            }
            if (AtBasicSettlement)
            {
                if (index == 0) { ResumePreviousRecord(); return; }
                index--;
            }
            if (index == 0)
            {
                if (selectedRecord.storyFlags.Contains("court_lift_ready") && !selectedRecord.storyFlags.Contains("entered_cassel_tribunal"))
                {
                    if (SaveLiftChoice(line, "janhyang", "court_lift_floor")) SelectStage(StageCatalog.Find("janhyang"));
                }
                else if (SaveLiftChoice(line, "cassel", "cassel_arena_floor")) SelectStage(StageCatalog.Find("cassel"));
                return;
            }
            if (index == 1)
            {
                if (AtBasicSettlement)
                {
                    try { selectedRecord = selectedStore.RestorePrevious(); }
                    catch (System.Exception e) { message = Loc.Get("slot.open_failed", e.Message); return; }
                }
                if (SaveLiftChoice(line, "hub", "forge_anvil_floor")) SelectStage(StageCatalog.Find("hub"));
            }
            else if (index == 2 && selectedRecord.storyFlags.Contains("true_ending_complete"))
            {
                if (SaveLiftChoice(line, "hub", "attic_table_floor")) SelectStage(StageCatalog.Find("hub"));
            }
            else Go(ShellPage.Stages);
        }

        private bool SaveLiftChoice(StoryRow row, string stage, string floor)
        {
            // Mutate a copy so a failed menu save leaves the same option available.
            var next = JsonUtility.FromJson<CoreRecord>(JsonUtility.ToJson(selectedRecord));
            if (row != null)
            {
                if (!next.storyFlags.Contains(row.ReadFlag)) next.storyFlags.Add(row.ReadFlag);
                if (!next.storyFlags.Contains(row.completionFlag)) next.storyFlags.Add(row.completionFlag);
            }
            if (stage != null)
            {
                next.storyFlags.Remove("resume_at_lift");
                EndingCheckpoint.SetFloor(next, stage, floor);
            }
            if (!selectedStore.Save(next)) { message = selectedStore.LastError; return false; }
            selectedRecord = next;
            selected = 0;
            dialogueScroll = Vector2.zero;
            return true;
        }
        private void ResumePreviousRecord()
        {
            try { selectedRecord = selectedStore.RestorePrevious(); Go(ShellPage.CourtLift); }
            catch (System.Exception e) { message = Loc.Get("slot.open_failed", e.Message); }
        }
        private void OpenReturnSelection()
        {
            if (!Playing || game.ActiveCassel?.CanChooseReturn != true) return;
            game.ActiveCassel.Scenes?.Signal("menuOpen");
            Pause(); returnSkill = null; Go(ShellPage.ReturnSelection);
        }
        private void ActivateReturn(int index)
        {
            if (game.ActiveCassel == null) { Resume(); return; }
            if (Page == ShellPage.ReturnSelection)
            {
                if (index == BenefactorIds.All.Length) { Resume(); return; }
                string id = BenefactorIds.All[index];
                if (!game.ActiveSkills.Finale.State.CanUse(id)) { message = Loc.Get("finale.already_pledged"); return; }
                returnSkill = id; game.ActiveCassel.Scenes?.Signal("confirmMenu"); Go(ShellPage.ReturnConfirm); // cancel is selected first
            }
            else if (index == 0) Go(ShellPage.ReturnSelection);
            else if (game.ActiveCassel.ArmReturn(returnSkill)) Resume();
            else message = Loc.Get("finale.return_requirements");
        }
        private void DrawReturn()
        {
            var state = game.ActiveSkills.Finale.State;
            if (Page == ShellPage.ReturnSelection)
            {
                GUI.Label(new Rect(200, 140, 880, 70), Loc.Get("finale.return_instructions", state.returnCount, state.ReleasedPercent), textStyle);
                for (int i = 0; i < BenefactorIds.All.Length; i++)
                {
                    var id = BenefactorIds.All[i];
                    Row(i, catalog.Get(id).DisplayName + (state.CanUse(id) ? Loc.Get("finale.available_suffix") : Loc.Get("finale.pledged_suffix")), new Rect(200, 226 + 53 * i, 880, 48));
                    if (Page != ShellPage.ReturnSelection) return;
                }
                Row(5, Loc.Get("finale.back_to_battle"), new Rect(200, 502, 880, 48));
            }
            else
            {
                string details = Loc.Get("finale.confirm_return_body", catalog.Get(returnSkill).DisplayName, ((state.returnCount + 1) * 20));
                var row = game.ActiveCassel.Scenes?.ConfirmMenu;
                if (row != null) details = SkillSession.StoryText(row) + "\n\n" + details;
                GUI.Label(new Rect(164, 146, 952, 240), details, textStyle);
                Row(0, Loc.Get("finale.cancel_return"), new Rect(200, 390, 880, 56));
                if (Page == ShellPage.ReturnConfirm) Row(1, Loc.Get("finale.arm_return"), new Rect(200, 458, 880, 56));
            }
        }
        public void EnterCasselPractice(bool lowBasic = false)
        {
            if (!DebugAvailable || game.ActiveSkills == null) return;
            if (!game.FlushProgress()) { message = Loc.Get("finale.debug_save_failed"); return; }
            var session = game.ActiveSkills;
            session.Progression.SeedBoundary(true);
            if (lowBasic)
            {
                var basic = session.Progression.Record.Get(SkillRules.BasicSword);
                basic.validUseCount = SkillRules.InitialSwordUses; basic.stage = 1;
            }
            session.Progression.Trials.Configure("cassel");
            foreach (string id in BenefactorIds.All)
            {
                var use = session.Progression.BeginUse(id);
                session.Progression.Apply(use, new CombatEffectResult(EffectTargetKind.TerrainMechanism, "cassel_debug_fixture", true));
                session.Progression.EndUse(use);
            }
            // F8 fixture uses isolated debug data. It grants no I/L/echo victory or story unlock flags.
            selectedRecord = session.Progression.Record;
            selectedRecord.lastStageId = "cassel";
            session.Save();
            if (!session.LastSaveSucceeded) { message = session.StorageError; return; }
            game.StopStage(); game.StartStage(selectedStore, selectedRecord);
            Time.timeScale = 1f; Go(ShellPage.Playing);
        }
    }
}
