using System;
using System.IO;
using UnityEngine;

namespace RewindNotary
{
    public enum ShellPage { Main, Slots, Stages, Playing, Pause, Settings, DeleteConfirm, QuitConfirm, Skills, Investigation, CoreReading, ReturnSelection, ReturnConfirm, CourtLift, StoryDialogue, StorySaving }
    public enum SlotPurpose { Continue, NewGame, StageSelect }

    // Runs before hit-stop and combat so pause/input transitions have a single owner.
    [DefaultExecutionOrder(-200)]
    public sealed partial class GameShell : MonoBehaviour
    {
        public ShellPage Page { get; private set; } = ShellPage.Main;
        public SettingsStore Settings { get; private set; }
        public bool Playing => Page == ShellPage.Playing;
        public static bool DebugAvailable => Debug.isDebugBuild || Application.isEditor;
        private GameBootstrap game;
        private SkillCatalog catalog;
        private string directory, message, legacyError;
        private readonly CoreRecord[] records = new CoreRecord[AutoTestRuntime.SlotCount];
        private readonly string[] slotErrors = new string[AutoTestRuntime.SlotCount];
        private readonly SkillSaveStore[] stores = new SkillSaveStore[AutoTestRuntime.SlotCount];
        private CoreRecord selectedRecord;
        private SkillSaveStore selectedStore;
        private SlotPurpose purpose;
        private ShellPage settingsReturn, quitReturn;
        private int selected, deleteSlot;
        private Vector2 previousAxis;
        private float repeatAt;
        private Font font;
        private GUIStyle titleStyle, textStyle, buttonStyle, selectedStyle;
        public void Initialize(GameBootstrap bootstrap, Font uiFont)
        {
            Loc.SetLanguage(Loc.DefaultLanguage(Application.systemLanguage));
            game = bootstrap; font = uiFont; catalog = SkillCatalog.Load();
            var root = AutoTestRuntime.SaveRoot;
            directory = Path.Combine(root, "SkillSystem");
            Settings = new SettingsStore(root); Settings.Load(); Settings.Apply();
            try { message = SkillSaveStore.MigrateLegacy(directory, catalog); }
            catch (Exception e) { legacyError = Loc.Get("save.migration_failed", e.Message); message = legacyError; }
            RefreshSlots(); Go(ShellPage.Main);
        }
        private SkillSaveStore Store(int slot) => stores[slot - 1] ?? (stores[slot - 1] = new SkillSaveStore(directory, catalog, slot));
        private void RefreshSlots()
        {
            for (int i = 0; i < records.Length; i++)
            {
                records[i] = null; slotErrors[i] = null;
                try { var store = Store(i + 1); records[i] = store.Load(); slotErrors[i] = store.LastError ?? store.RecoveryNotice; }
                catch (Exception e) { slotErrors[i] = e.Message; }
            }
            if (legacyError != null) slotErrors[0] = legacyError;
        }
        private void Go(ShellPage page)
        {
            Page = page; selected = 0; previousAxis = Vector2.zero; repeatAt = Time.unscaledTime + 0.3f;
            if (page == ShellPage.Main && Array.TrueForAll(records, r => r == null)
                && Array.TrueForAll(slotErrors, e => e == null)) selected = 1;
            GameInput.SetGameplay(page == ShellPage.Playing);
        }
        private void Update()
        {
            if (game == null || Settings == null) return;
            if (Playing && game.ActiveSkills != null)
            {
                string stage = game.ActiveSkills.Progression.Record.lastStageId;
                if ((game.Area != null && stage == "cassel") || (game.ActiveCassel != null && stage == "janhyang"))
                {
                    if (game.ActiveSkills.LastSaveSucceeded)
                    {
                        selectedRecord = game.ActiveSkills.Progression.Record;
                        game.StopStage();
                        SelectStage(StageCatalog.Find(stage));
                        return;
                    }
                }
                else if (game.ActiveCassel?.Scenes?.Modal != null && game.ActiveSkills.LastSaveSucceeded)
                {
                    OpenStoryDialogue();
                    return;
                }
            }
            if (Playing && game.ActiveSkills?.Progression.Record.lastStageId == EndingCheckpoint.Lift)
            {
                selectedRecord = game.ActiveSkills.Progression.Record;
                game.SuspendInput(); Time.timeScale = 0f; Go(ShellPage.CourtLift);
            }
            if (Page == ShellPage.CourtLift && game.ActiveSkills != null)
            {
                if (!game.ActiveSkills.LastSaveSucceeded) { message = game.ActiveSkills.StorageError; return; }
                game.StopStage(); Time.timeScale = 1f; message = null;
                return; // The finishing input cannot also select a lift action.
            }
            if (Playing && (game.Area != null || game.ActiveCassel != null) && !game.ActiveSkills.LastSaveSucceeded)
            {
                game.SuspendInput(); ImpactFeedback.ResetEffects(); Time.timeScale = 0f;
                Go(ShellPage.StorySaving); return;
            }
            if (Page == ShellPage.StorySaving)
            {
                if (game.ActiveSkills.LastSaveSucceeded) Resume();
                else if (GameInput.PauseDown || GameInput.CancelDown) Go(ShellPage.Pause);
                else if (GameInput.SubmitDown) game.ActiveSkills.Save();
                return;
            }
            if (Page == ShellPage.StoryDialogue && (GameInput.PauseDown || GameInput.CancelDown))
            { Go(ShellPage.Pause); return; }
            if (Page == ShellPage.StoryDialogue && openingScene != null)
            {
                if (!game.ActiveSkills.LastSaveSucceeded) { Go(ShellPage.StorySaving); return; }
                if (openingScene.Advance(Time.unscaledDeltaTime, GameInput.SubmitDown) &&
                    game.Area.ConfirmDialogue(false)) Resume();
                return;
            }
            if (Playing)
            {
                if (GameInput.PauseDown) Pause();
                else if (GameInput.InvestigateDown && game.ActiveCassel?.CanChooseReturn == true) OpenReturnSelection();
                return;
            }
            if (GameInput.CancelDown || ((Page == ShellPage.Pause || Page == ShellPage.Skills || Page == ShellPage.Investigation || Page == ShellPage.CoreReading) && GameInput.PauseDown)) { Back(); return; }
            Vector2 raw = GameInput.MenuAxis;
            var axis = new Vector2(Mathf.Abs(raw.x) >= 0.5f ? Mathf.Sign(raw.x) : 0,
                Mathf.Abs(raw.y) >= 0.5f ? Mathf.Sign(raw.y) : 0);
            if (axis != Vector2.zero && (axis != previousAxis || Time.unscaledTime >= repeatAt))
            {
                if (axis.y != 0) selected = (selected - (int)axis.y + RowCount) % RowCount;
                else if (Page == ShellPage.Settings) AdjustSetting(selected, (int)axis.x);
                else if (Page == ShellPage.Investigation) clueScroll.y = Mathf.Max(0, clueScroll.y + axis.x * 85);
                else if (Page == ShellPage.StoryDialogue || Page == ShellPage.CourtLift) dialogueScroll.y = Mathf.Max(0, dialogueScroll.y + axis.x * 85);
                else if (Page == ShellPage.CoreReading) readingScroll.y = Mathf.Max(0, readingScroll.y + axis.x * 85);
                repeatAt = Time.unscaledTime + (axis != previousAxis ? 0.35f : 0.12f);
            }
            previousAxis = axis;
            if (Page == ShellPage.Slots && selected < records.Length && GameInput.DeleteDown) { RequestDelete(selected + 1); return; }
            if (GameInput.SubmitDown) Activate(selected);
        }
        private int RowCount => Page == ShellPage.StoryDialogue ? (CanSkipLine ? 2 : 1) : Page == ShellPage.CourtLift ? LiftLabels().Length : Page == ShellPage.Main ? 5 : Page == ShellPage.Slots ? records.Length + 1
            : Page == ShellPage.Stages ? StageCatalog.All.Length + (EndingCheckpoint.Marker(selectedRecord) == null ? 1 : 2) : Page == ShellPage.Pause ? 5
            : Page == ShellPage.Investigation || Page == ShellPage.CoreReading ? InvestigationRows
            : Page == ShellPage.ReturnSelection ? BenefactorIds.All.Length + 1
            : Page == ShellPage.Skills ? 2 : Page == ShellPage.Settings ? 6 : 2;
        public void Pause()
        {
            if (!Playing) return;
            game.FlushProgress(); game.SuspendInput();
            ImpactFeedback.ResetEffects(); Time.timeScale = 0f;
            Go(ShellPage.Pause);
        }
        private void Resume()
        {
            if ((game.Area != null && game.Area.DialogueOpen) || game.ActiveCassel?.Scenes?.Modal != null)
            { dialogueScroll = Vector2.zero; Go(ShellPage.StoryDialogue); return; }
            game.SuspendInput(); Time.timeScale = 1f; Go(ShellPage.Playing);
        }
        private void Back()
        {
            switch (Page)
            {
                case ShellPage.CourtLift: RefreshSlots(); Go(ShellPage.Slots); break;
                case ShellPage.ReturnSelection: Resume(); break;
                case ShellPage.ReturnConfirm: Go(ShellPage.ReturnSelection); break;
                case ShellPage.Pause: Resume(); break;
                case ShellPage.Skills: Go(ShellPage.Pause); break;
                case ShellPage.Investigation:
                case ShellPage.CoreReading: CloseInvestigation(); break;
                case ShellPage.Settings: Settings.Save(); Go(settingsReturn); break;
                case ShellPage.Stages: RefreshSlots(); Go(ShellPage.Slots); break;
                case ShellPage.DeleteConfirm: RefreshSlots(); Go(ShellPage.Slots); break;
                case ShellPage.QuitConfirm: Go(quitReturn); break;
                case ShellPage.Slots: Go(ShellPage.Main); break;
            }
        }
        private void Activate(int index)
        {
            message = null;
            switch (Page)
            {
                case ShellPage.ReturnSelection:
                case ShellPage.ReturnConfirm: ActivateReturn(index); break;
                case ShellPage.StorySaving: game.ActiveSkills.Save(); break;
                case ShellPage.StoryDialogue:
                    if (game.Area != null ? game.Area.ConfirmDialogue(index == 1) :
                        game.ActiveCassel.Scenes.Confirm()) Resume();
                    break;
                case ShellPage.CourtLift: ActivateLift(index); break;
                case ShellPage.Main:
                    if (index < 3)
                    {
                        purpose = index == 0 ? SlotPurpose.Continue : index == 1 ? SlotPurpose.NewGame : SlotPurpose.StageSelect;
                        RefreshSlots(); Go(ShellPage.Slots);
                    }
                    else if (index == 3) OpenSettings(); else RequestQuit();
                    break;
                case ShellPage.Slots:
                    if (index == records.Length) Back(); else SelectSlot(index + 1);
                    break;
                case ShellPage.Stages:
                    if (index == StageCatalog.All.Length) Back();
                    else if (index > StageCatalog.All.Length) ResumePreviousRecord();
                    else SelectStage(StageCatalog.All[index]);
                    break;
                case ShellPage.Pause:
                    if (index == 0) Resume();
                    else if (index == 1) Go(ShellPage.Skills);
                    else if (index == 2) OpenSettings();
                    else if (index == 3) ReturnMain(); else RequestQuit();
                    break;
                case ShellPage.Skills:
                    if (index == 0) OpenInvestigation(); else Back();
                    break;
                case ShellPage.Investigation:
                case ShellPage.CoreReading:
                    ActivateInvestigation(index);
                    break;
                case ShellPage.Settings:
                    if (index == 5) Back(); else AdjustSetting(index, 1);
                    break;
                case ShellPage.DeleteConfirm:
                    if (index == 0) Back(); else DeleteSelected();
                    break;
                case ShellPage.QuitConfirm:
                    if (index == 0) Back(); else Quit();
                    break;
            }
        }
        private void SelectSlot(int slot)
        {
            try
            {
                if (slot == 1 && legacyError != null) { message = legacyError; return; }
                selectedStore = Store(slot); selectedRecord = selectedStore.Load();
                string recovery = selectedStore.LastError;
                if (purpose == SlotPurpose.NewGame && selectedRecord != null)
                { message = Loc.Get("slot.occupied_hint"); return; }
                if (selectedRecord == null)
                {
                    if (purpose == SlotPurpose.Continue) { message = Loc.Get("slot.empty_hint"); return; }
                    selectedRecord = purpose == SlotPurpose.NewGame ? selectedStore.Create("hub") : selectedStore.Create();
                }
                selectedStore.ImportEnding(selectedRecord);
                if (purpose == SlotPurpose.Continue && (selectedRecord.lastStageId == EndingCheckpoint.Lift || selectedRecord.lastStageId == "cassel"))
                {
                    var ending = FinaleEndingEvaluator.Evaluate(selectedRecord.finalBattleAttempt);
                    if (!selectedRecord.testData && ending != FinaleEnding.None &&
                        !selectedRecord.storyFlags.Contains(ending == FinaleEnding.True ? "true_ending_complete" : "basic_ending_complete"))
                        EndingCheckpoint.StartScenes(selectedRecord);
                }
                if (purpose == SlotPurpose.Continue && selectedRecord.lastStageId == EndingCheckpoint.Lift)
                { Go(ShellPage.CourtLift); message = recovery; return; }
                if (purpose == SlotPurpose.StageSelect) { Go(ShellPage.Stages); message = recovery; }
                else SelectStage(StageCatalog.Find(selectedRecord.lastStageId));
            }
            catch (Exception e) { message = Loc.Get("slot.open_failed", e.Message); RefreshSlots(); }
        }
        private void SelectStage(StageEntry stage)
        {
            // Stage selection also passes through the first departure. Continue inside a district keeps its checkpoint.
            if (Page == ShellPage.Stages && RepresentativeScript.IsDistrict(stage?.Id) && selectedRecord.lastStageId != stage.Id
                && selectedRecord.storyFlags.Contains(InvestigationService.MiddleInvestigationFlag)
                && !selectedRecord.storyFlags.Contains("luka_lift_complete"))
                stage = StageCatalog.Find("hub");
            if (stage == null || !stage.Unlocked(selectedRecord) || !stage.Implemented)
            {
                if (Page != ShellPage.Stages) Go(ShellPage.Stages);
                message = stage == null ? Loc.Get("stage.saved_unavailable")
                    : stage.Name + (stage.Unlocked(selectedRecord) ? Loc.Get("stage.unavailable_suffix") : stage.Id.StartsWith("trial_") ? Loc.Get("stage.skill_required_suffix") : Loc.Get("stage.story_locked_suffix"));
                return;
            }
            try
            {
                if (stage.Id == "echo_duel")
                {
                    // An optional match uses the slot's loadout without replacing its campaign checkpoint.
                    var duel = JsonUtility.FromJson<CoreRecord>(JsonUtility.ToJson(selectedRecord));
                    duel.lastStageId = stage.Id;
                    game.StartStage(selectedStore, duel);
                    Time.timeScale = 1f; Go(ShellPage.Playing);
                    return;
                }
                string priorStage = selectedRecord.lastStageId;
                selectedRecord.lastStageId = stage.Id;
                // Room completion and lift choices already saved this destination before releasing the previous world.
                bool checkpointSaved = priorStage == stage.Id && (Playing || Page == ShellPage.CourtLift);
                if (!checkpointSaved && !selectedStore.Save(selectedRecord))
                { selectedRecord.lastStageId = priorStage; message = selectedStore.LastError; return; }
                game.StartStage(selectedStore, selectedRecord);
                Time.timeScale = 1f; Go(ShellPage.Playing);
            }
            catch (Exception e) { game.AbortStage(); Time.timeScale = 1f; Go(ShellPage.Slots); message = Loc.Get("stage.start_failed", e.Message); }
        }
        internal void FinishDeparture()
        {
            if (!Playing || !game.ActiveSkills.LastSaveSucceeded) return;
            selectedRecord = game.ActiveSkills.Progression.Record;
            ShowStageChoices("clock");
        }
        internal void FinishEchoDuel() => ShowStageChoices("echo_duel");
        private void ShowStageChoices(string initialStage)
        {
            game.StopStage();
            purpose = SlotPurpose.StageSelect;
            Go(ShellPage.Stages);
            selected = Array.FindIndex(StageCatalog.All, s => s.Id == initialStage);
        }
        private void RequestDelete(int slot)
        {
            deleteSlot = slot; Go(ShellPage.DeleteConfirm);
        }
        private void DeleteSelected()
        {
            try
            {
                Store(deleteSlot).Delete();
                if (deleteSlot == 1) legacyError = null;
                selectedRecord = null; selectedStore = null;
                RefreshSlots(); Go(ShellPage.Slots); message = Loc.Get("slot.deleted", deleteSlot);
            }
            catch (Exception e) { message = Loc.Get("slot.delete_check", e.Message); RefreshSlots(); }
        }
        private void OpenSettings() { settingsReturn = Page; Go(ShellPage.Settings); }
        private void AdjustSetting(int index, int direction)
        {
            var value = Settings.Value;
            if (index == 0) value.bgmVolume = Mathf.Clamp01(value.bgmVolume + direction * 0.1f);
            else if (index == 1) value.sfxVolume = Mathf.Clamp01(value.sfxVolume + direction * 0.1f);
            else if (index == 2) value.screenShake = !value.screenShake;
            else if (index == 3 && DebugAvailable)
            {
                SetStageOverride((value.stageOverride + direction + 4) % 4); return;
            }
            else if (index == 4)
            {
                value.language = Loc.NextLanguage(value.language, direction);
                message = null;
            }
            else return;
            Settings.Apply(); Settings.Save();
        }
        public void SetStageOverride(int stage)
        {
            if (!DebugAvailable) return;
            Settings.Value.stageOverride = stage;
            game.ApplyStageOverride(stage);
            Settings.Save();
        }
        private void ReturnMain()
        {
            if (!game.FlushProgress()) { message = Loc.Get("save.retry_hint"); return; }
            var farewell = game.Area == null ? game.ActiveSkills?.Progression.Trials.Active?.dialogue.returnKey : null;
            game.StopStage(); Time.timeScale = 1f; RefreshSlots(); Go(ShellPage.Main);
            if (farewell != null) message = Loc.Get(farewell);
        }
        private void RequestQuit() { quitReturn = Page; Go(ShellPage.QuitConfirm); }
        private void Quit()
        {
            if (!game.FlushProgress()) { message = Loc.Get("save.quit_blocked"); return; }
            Settings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private void OnApplicationFocus(bool focused) { if (!focused) PausePresentation(); }
        private void OnApplicationPause(bool paused) { if (paused) PausePresentation(); }
        private void PausePresentation()
        {
            if (Page == ShellPage.StoryDialogue && openingScene != null) Go(ShellPage.Pause);
            else if (Playing) Pause();
        }
        private void OnDestroy() { GameInput.SetGameplay(false); Time.timeScale = 1f; }

        private string SlotLabel(int index)
        {
            var record = records[index];
            string prefix = Loc.Get("slot.prefix", (index + 1));
            if (record == null) return prefix + (slotErrors[index] == null ? Loc.Get("slot.empty") : Loc.Get("slot.unreadable"));
            var sword = record.Get(SkillRules.BasicSword);
            string when = DateTime.TryParse(record.savedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var utc)
                ? utc.ToLocalTime().ToString(Loc.Get("save.date_format"), Loc.Culture) : Loc.Get("save.unknown_time");
            string owned = string.Join(", ", record.skills.FindAll(s => s.acquired).ConvertAll(s => catalog.Get(s.skillId).DisplayName));
            string summary = Loc.Get("slot.summary", prefix, when, record.skills.FindAll(s => s.acquired).Count, owned, sword.validUseCount, sword.stage);
            if (record.storyFlags.Contains(EndingCheckpoint.Basic)) summary += " · " + Loc.Get("finale.ending.basic");
            if (record.storyFlags.Contains(EndingCheckpoint.True)) summary += " · " + Loc.Get("finale.ending.true");
            return summary;
        }
        private void OnGUI()
        {
            if (Settings == null || Playing) return;
            var previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2f,
                (Screen.height - 720 * scale) / 2f, 0), Quaternion.identity, Vector3.one * scale);
            if (Page == ShellPage.StoryDialogue && openingScene != null)
            {
                openingScene.Draw(font);
                GUI.matrix = previous;
                return;
            }
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                textStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true };
                buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 20, alignment = TextAnchor.MiddleLeft, wordWrap = true, padding = new RectOffset(20, 12, 5, 5) };
                selectedStyle = new GUIStyle(buttonStyle); selectedStyle.normal.textColor = new Color(0.45f, 1f, 0.9f);
            }
            GUI.color = new Color(0.045f, 0.065f, 0.1f, 0.97f);
            GUI.DrawTexture(new Rect(-2000, -2000, 6000, 5000), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(new Rect(90, 24, 1100, 62), Loc.Get("game.menu_title"), titleStyle);
            GUI.Label(new Rect(200, 95, 900, 36), Heading(), textStyle);
            ShellPage renderedPage = Page;
            if (Page == ShellPage.Slots)
            {
                int pitch = records.Length > 4 ? 490 / records.Length : 122;
                for (int i = 0; i < records.Length; i++)
                {
                    Row(i, SlotLabel(i), new Rect(200, 148 + i * pitch, 760, pitch - 12));
                    if (Page != renderedPage) break;
                    if (GUI.Button(new Rect(978, 175 + i * pitch, 100, 52), Loc.Get("ui.delete"), buttonStyle)) RequestDelete(i + 1);
                }
                if (Page == renderedPage) Row(records.Length, Loc.Get("ui.back"), new Rect(200, 154 + records.Length * pitch, 880, 46));
            }
            else if (Page == ShellPage.StorySaving)
            {
                GUI.Label(new Rect(170, 195, 940, 150), Loc.Get("save.retrying_memory") + "\n" + game.ActiveSkills.StorageError, textStyle);
                Row(0, Loc.Get("ui.save"), new Rect(240, 470, 800, 50));
            }
            else if (Page == ShellPage.StoryDialogue) DrawStoryDialogue();
            else if (Page == ShellPage.CourtLift) DrawCourtLift();
            else if (Page == ShellPage.Skills)
            {
                DrawSkills();
                Row(0, Loc.Get("ui.investigation_progress", Investigation.ProgressLabel), new Rect(60, 620, 760, 46));
                if (Page == renderedPage) Row(1, Loc.Get("ui.back_to_pause"), new Rect(840, 620, 380, 46));
            }
            else if (Page == ShellPage.Investigation || Page == ShellPage.CoreReading) DrawInvestigation();
            else if (Page == ShellPage.ReturnSelection || Page == ShellPage.ReturnConfirm) DrawReturn();
            else if (Page == ShellPage.Stages)
            {
                int rows = Mathf.CeilToInt(StageCatalog.All.Length / 2f);
                int pitch = Mathf.Min(45, 450 / rows);
                for (int i = 0; i < StageCatalog.All.Length; i++)
                {
                    var stage = StageCatalog.All[i];
                    Row(i, stage.Name + "\n[" + stage.Status(selectedRecord) + "]", new Rect(50 + (i / rows) * 600, 136 + (i % rows) * pitch, 580, pitch - 2));
                    if (Page != renderedPage) break;
                }
                if (Page == renderedPage)
                {
                    bool hasPrevious = EndingCheckpoint.Marker(selectedRecord) != null;
                    Row(StageCatalog.All.Length, Loc.Get("ui.back"), new Rect(160, 590, hasPrevious ? 350 : 960, 40));
                    if (Page == renderedPage && hasPrevious)
                        Row(StageCatalog.All.Length + 1, Loc.Get("story.resume_previous"), new Rect(530, 590, 590, 40));
                }
            }
            else
            {
                string[] labels = Labels();
                for (int i = 0; i < labels.Length; i++)
                {
                    Row(i, labels[i], new Rect(300, 170 + i * 67, 680, 55));
                    if (Page != renderedPage) break;
                }
                if (Page == ShellPage.Settings)
                {
                    GUI.Label(new Rect(210, 565, 880, 64), Loc.Get("settings.adjust_hint"), new GUIStyle(textStyle) { fontSize = 16 });
                }
            }
            string status = (Page == ShellPage.Investigation || Page == ShellPage.CoreReading)
                ? game.ActiveSkills.StorageError : message ?? (Page == ShellPage.Settings ? Settings.Error
                : Page == ShellPage.Slots && selected < records.Length ? slotErrors[selected] : null);
            if (!string.IsNullOrEmpty(status))
            {
                bool investigating = Page == ShellPage.Investigation || Page == ShellPage.CoreReading;
                GUI.Label(new Rect(160, investigating ? 590 : Page == ShellPage.Stages ? 634 : Page == ShellPage.Settings ? 632 : 599, 960, investigating ? 28 : Page == ShellPage.Stages || Page == ShellPage.Settings ? 38 : 67),
                    investigating ? (game.ActiveSkills.LastSaveSucceeded ? Loc.Get("save.log_path_hint") : Loc.Get("save.retrying_memory")) : status, textStyle);
            }
            GUI.Label(new Rect(160, 675, 1060, 32), Page == ShellPage.Investigation || Page == ShellPage.CoreReading
                ? Loc.Get("input.reading_menu")
                : Loc.Get("input.shell_menu"), textStyle);
            GUI.matrix = previous;
        }
        private Vector2 dialogueScroll;
        private OpeningScene openingScene;
        internal void OpenStoryDialogue()
        {
            game.SuspendInput();
            Time.timeScale = 0f;
            dialogueScroll = Vector2.zero;
            openingScene = OpeningScene.For(DialogueRow);
            Go(ShellPage.StoryDialogue);
        }
        private StoryRow DialogueRow => game.Area != null ? game.Area.ActiveRow : game.ActiveCassel.Scenes.Modal;
        private bool CanSkipLine => game.Area != null ? game.Area.CanSkip : game.ActiveSkills.Progression.Record.storyFlags.Contains(DialogueRow.ReadFlag);
        private void DrawStoryDialogue()
        {
            var row = DialogueRow;
            GUI.Label(new Rect(150, 150, 980, 44), Loc.Get("area.speaker." + row.speakerId), titleStyle);
            string line = Loc.Get(row.textKey);
            float height = textStyle.CalcHeight(new GUIContent(line), 940) + 16;
            dialogueScroll = GUI.BeginScrollView(new Rect(150, 218, 980, 310), dialogueScroll, new Rect(0, 0, 950, height));
            GUI.Label(new Rect(4, 0, 940, height), line, textStyle);
            GUI.EndScrollView();
            Row(0, Loc.Get("area.continue"), new Rect(180, 555, 480, 48));
            if (Page == ShellPage.StoryDialogue && CanSkipLine)
                Row(1, Loc.Get("area.skip"), new Rect(680, 555, 420, 48));
            if (!game.ActiveSkills.LastSaveSucceeded)
                GUI.Label(new Rect(180, 613, 920, 50), game.ActiveSkills.StorageError, textStyle);
        }
        private void DrawSkills()
        {
            var session = game.ActiveSkills;
            if (session == null) { GUI.Label(new Rect(80, 150, 1120, 50), Loc.Get("skills.no_active_record"), textStyle); return; }
            GUI.Label(new Rect(60, 128, 1160, 30), SkillStatusView.Mode(session), textStyle);
            var rows = SkillStatusView.Read(session);
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i]; float x = 60 + i % 2 * 590, y = 166 + i / 2 * 148;
                GUI.color = row.Owned ? new Color(0.1f, 0.19f, 0.23f) : new Color(0.12f, 0.13f, 0.16f);
                GUI.DrawTexture(new Rect(x, y, 570, 144), Texture2D.whiteTexture);
                GUI.color = row.Owned ? new Color(0.65f, 1f, 0.91f) : new Color(0.7f, 0.73f, 0.76f);
                GUI.Label(new Rect(x + 12, y + 6, 546, 26), row.Name + (row.Owned ? "" : Loc.Get("ui.locked_tag")), textStyle);
                GUI.color = Color.white;
                GUI.Label(new Rect(x + 12, y + 32, 546, 26), row.StageText, textStyle);
                GUI.Label(new Rect(x + 12, y + 58, 546, 26), row.ProgressText, textStyle);
                GUI.Label(new Rect(x + 12, y + 84, 546, 30), row.FormText, new GUIStyle(textStyle) { fontSize = 15 });
                var trialInfo = BenefactorTrialCatalog.Load().ForSkill(row.SkillId);
                if (trialInfo != null) GUI.Label(new Rect(x + 12, y + 116, 546, 26),
                    BenefactorTrialProgress.Status(session.Progression.Record, trialInfo), new GUIStyle(textStyle) { fontSize = 15 });
            }
        }
        private void Row(int index, string label, Rect rect)
        {
            // GUI controls are mouse-only; all keyboard/gamepad dispatch happens once in Update.
            // Clearing focus avoids IMGUI also submitting Space on an old focused button.
            GUI.FocusControl(null);
            var style = selected == index ? selectedStyle : buttonStyle;
            if (Page == ShellPage.Stages) style = new GUIStyle(style) { fontSize = 15, padding = new RectOffset(10, 6, 1, 1) };
            style = new GUIStyle(style);
            while (style.fontSize > 12 && style.CalcHeight(new GUIContent(label), rect.width - 40) > rect.height - 10)
                style.fontSize--;
            if (GUI.Button(rect, (selected == index ? "▶ " : "   ") + label, style))
            {
                selected = index;
                if (Page == ShellPage.Settings && index < 5) AdjustSetting(index, Event.current.mousePosition.x < rect.center.x ? -1 : 1);
                else Activate(index);
            }
        }
        private string SlotHeading => Loc.Get(purpose == SlotPurpose.NewGame ? "heading.new_game" :
            purpose == SlotPurpose.Continue ? "heading.continue" : "heading.stage_slot");

        private string Heading()
        {
            if (Page == ShellPage.Slots && records.Length > 3) return SlotHeading;
            switch (Page)
            {
                case ShellPage.ReturnSelection: return Loc.Get("heading.return_selection");
                case ShellPage.ReturnConfirm: return Loc.Get("heading.return_confirm");
                case ShellPage.CourtLift: return AtBasicSettlement ? Loc.Get("finale.ending.basic") + " · " + Loc.Get("story.court_lift") : Loc.Get("story.court_lift");
                case ShellPage.StorySaving:
                case ShellPage.StoryDialogue: return game.Area != null ? game.Area.Room.Label : Loc.Get("area.room.court_04_tribunal");
                case ShellPage.Main: return Loc.Get("heading.main");
                case ShellPage.Slots: return Loc.Get("heading.three_slots", SlotHeading);
                case ShellPage.Stages: return Loc.Get("heading.stages", selectedStore.Slot);
                case ShellPage.Pause: return Loc.Get("heading.pause");
                case ShellPage.Skills: return Loc.Get("heading.skills");
                case ShellPage.Investigation: return "";
                case ShellPage.CoreReading: return Loc.Get("heading.core");
                case ShellPage.Settings: return Loc.Get("heading.settings");
                case ShellPage.DeleteConfirm: return Loc.Get("slot.delete_confirm", deleteSlot);
                default: return Loc.Get("ui.quit_confirm");
            }
        }
        private string[] Labels()
        {
            if (Page == ShellPage.CourtLift) return LiftLabels();
            if (Page == ShellPage.Main) return new[] { Loc.Get("ui.continue"), Loc.Get("ui.new_game"), Loc.Get("ui.stage_select"), Loc.Get("ui.settings"), Loc.Get("ui.quit") };
            if (Page == ShellPage.Pause) return new[] { Loc.Get("ui.resume"), Loc.Get("ui.skills"), Loc.Get("ui.settings"), Loc.Get("ui.main_menu"), Loc.Get("ui.quit") };
            if (Page == ShellPage.DeleteConfirm) return new[] { Loc.Get("ui.cancel"), Loc.Get("ui.delete_record") };
            if (Page == ShellPage.QuitConfirm) return new[] { Loc.Get("ui.cancel"), Loc.Get("ui.save_quit") };
            var value = Settings.Value;
            return new[] { Loc.Get("settings.music_volume", Mathf.RoundToInt(value.bgmVolume * 100)),
                Loc.Get("settings.effects_volume", Mathf.RoundToInt(value.sfxVolume * 100)),
                Loc.Get("settings.screen_shake", (value.screenShake ? Loc.Get("ui.on") : Loc.Get("ui.off"))),
                Loc.Get("settings.stage_override", (!DebugAvailable ? Loc.Get("settings.development_only") : value.stageOverride == 0 ? Loc.Get("settings.actual_stage") : Loc.Get("settings.preview_stage", value.stageOverride))),
                Loc.Get("settings.language", Loc.Get("language." + value.language)),
                Loc.Get("ui.back") };
        }
    }
}
