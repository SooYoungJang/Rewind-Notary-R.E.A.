using System;
using System.IO;
using UnityEngine;

namespace RewindNotary
{
    public enum ShellPage { Main, Slots, Stages, Playing, Pause, Settings, DeleteConfirm, QuitConfirm }
    public enum SlotPurpose { Continue, NewGame, StageSelect }

    // Runs before hit-stop and combat so pause/input transitions have a single owner.
    [DefaultExecutionOrder(-200)]
    public sealed class GameShell : MonoBehaviour
    {
        public ShellPage Page { get; private set; } = ShellPage.Main;
        public SettingsStore Settings { get; private set; }
        public bool Playing => Page == ShellPage.Playing;
        public static bool DebugAvailable => Debug.isDebugBuild || Application.isEditor;
        private GameBootstrap game;
        private SkillCatalog catalog;
        private string directory, message, legacyError;
        private readonly CoreRecord[] records = new CoreRecord[3];
        private readonly string[] slotErrors = new string[3];
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
            game = bootstrap; font = uiFont; catalog = SkillCatalog.Load();
            directory = Path.Combine(Application.persistentDataPath, "SkillSystem");
            Settings = new SettingsStore(Application.persistentDataPath); Settings.Load(); Settings.Apply();
            try { message = SkillSaveStore.MigrateLegacy(directory, catalog); }
            catch (Exception e) { legacyError = "기존 기록 이전 실패: " + e.Message; message = legacyError; }
            RefreshSlots(); Go(ShellPage.Main);
        }
        private SkillSaveStore Store(int slot) => new SkillSaveStore(directory, catalog, slot);
        private void RefreshSlots()
        {
            for (int i = 0; i < 3; i++)
            {
                records[i] = null; slotErrors[i] = null;
                try { var store = Store(i + 1); records[i] = store.Load(); slotErrors[i] = store.LastError; }
                catch (Exception e) { slotErrors[i] = e.Message; }
            }
            if (legacyError != null) slotErrors[0] = legacyError;
        }
        private void Go(ShellPage page)
        {
            Page = page; selected = 0; previousAxis = Vector2.zero; repeatAt = Time.unscaledTime + 0.3f;
            GameInput.SetGameplay(page == ShellPage.Playing);
        }
        private void Update()
        {
            if (game == null || Settings == null) return;
            if (Playing) { if (GameInput.PauseDown) Pause(); return; }
            if (GameInput.CancelDown || (Page == ShellPage.Pause && GameInput.PauseDown)) { Back(); return; }
            Vector2 raw = GameInput.MenuAxis;
            var axis = new Vector2(Mathf.Abs(raw.x) >= 0.5f ? Mathf.Sign(raw.x) : 0,
                Mathf.Abs(raw.y) >= 0.5f ? Mathf.Sign(raw.y) : 0);
            if (axis != Vector2.zero && (axis != previousAxis || Time.unscaledTime >= repeatAt))
            {
                if (axis.y != 0) selected = (selected - (int)axis.y + RowCount) % RowCount;
                else if (Page == ShellPage.Settings) AdjustSetting(selected, (int)axis.x);
                repeatAt = Time.unscaledTime + (axis != previousAxis ? 0.35f : 0.12f);
            }
            previousAxis = axis;
            if (Page == ShellPage.Slots && selected < 3 && GameInput.DeleteDown) { RequestDelete(selected + 1); return; }
            if (GameInput.SubmitDown) Activate(selected);
        }
        private int RowCount => Page == ShellPage.Main ? 5 : Page == ShellPage.Slots ? 4
            : Page == ShellPage.Stages ? StageCatalog.All.Length + 1 : Page == ShellPage.Pause ? 4
            : Page == ShellPage.Settings ? 5 : 2;
        public void Pause()
        {
            if (!Playing) return;
            game.FlushProgress(); game.SuspendInput();
            ImpactFeedback.ResetEffects(); Time.timeScale = 0f;
            Go(ShellPage.Pause);
        }
        private void Resume()
        {
            game.SuspendInput(); Time.timeScale = 1f; Go(ShellPage.Playing);
        }
        private void Back()
        {
            switch (Page)
            {
                case ShellPage.Pause: Resume(); break;
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
                case ShellPage.Main:
                    if (index < 3)
                    {
                        purpose = index == 0 ? SlotPurpose.Continue : index == 1 ? SlotPurpose.NewGame : SlotPurpose.StageSelect;
                        RefreshSlots(); Go(ShellPage.Slots);
                    }
                    else if (index == 3) OpenSettings(); else RequestQuit();
                    break;
                case ShellPage.Slots:
                    if (index == 3) Back(); else SelectSlot(index + 1);
                    break;
                case ShellPage.Stages:
                    if (index == StageCatalog.All.Length) Back(); else SelectStage(StageCatalog.All[index]);
                    break;
                case ShellPage.Pause:
                    if (index == 0) Resume();
                    else if (index == 1) OpenSettings();
                    else if (index == 2) ReturnMain(); else RequestQuit();
                    break;
                case ShellPage.Settings:
                    if (index == 4) Back(); else AdjustSetting(index, 1);
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
                { message = "이미 기록이 있는 슬롯입니다. 빈 슬롯을 고르거나 삭제 버튼을 사용해 주세요."; return; }
                if (selectedRecord == null)
                {
                    if (purpose == SlotPurpose.Continue) { message = "비어 있는 슬롯입니다. 새 게임으로 기록을 만들어 주세요."; return; }
                    selectedRecord = selectedStore.Create();
                }
                if (purpose == SlotPurpose.StageSelect) { Go(ShellPage.Stages); message = recovery; }
                else SelectStage(StageCatalog.Find(selectedRecord.lastStageId));
            }
            catch (Exception e) { message = "슬롯을 열지 못했습니다: " + e.Message; RefreshSlots(); }
        }
        private void SelectStage(StageEntry stage)
        {
            if (stage == null || !stage.Unlocked(selectedRecord) || !stage.Implemented)
            {
                if (Page != ShellPage.Stages) Go(ShellPage.Stages);
                message = stage == null ? "저장된 스테이지는 아직 준비 중입니다. 테스트 스테이지를 선택해 주세요."
                    : stage.Name + (stage.Unlocked(selectedRecord) ? ": 준비 중입니다." : ": 스토리 진행으로 해제되는 잠금 항목입니다. 현재 준비 중입니다.");
                return;
            }
            try
            {
                selectedRecord.lastStageId = stage.Id;
                if (!selectedStore.Save(selectedRecord)) { message = selectedStore.LastError; return; }
                game.StartStage(selectedStore, selectedRecord);
                Time.timeScale = 1f; Go(ShellPage.Playing);
            }
            catch (Exception e) { game.AbortStage(); Time.timeScale = 1f; Go(ShellPage.Slots); message = "스테이지 시작 실패: " + e.Message; }
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
                RefreshSlots(); Go(ShellPage.Slots); message = "슬롯 " + deleteSlot + " 기록을 삭제했습니다.";
            }
            catch (Exception e) { message = "삭제 처리 확인이 필요합니다: " + e.Message; RefreshSlots(); }
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
            if (!game.FlushProgress()) { message = "저장에 실패했습니다. 설정·저장 경로를 확인한 뒤 다시 시도해 주세요."; return; }
            game.StopStage(); Time.timeScale = 1f; RefreshSlots(); Go(ShellPage.Main);
        }
        private void RequestQuit() { quitReturn = Page; Go(ShellPage.QuitConfirm); }
        private void Quit()
        {
            if (!game.FlushProgress()) { message = "저장에 실패하여 종료를 보류했습니다. 취소 후 다시 시도해 주세요."; return; }
            Settings.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        private void OnApplicationFocus(bool focused) { if (!focused && Playing) Pause(); }
        private void OnApplicationPause(bool paused) { if (paused && Playing) Pause(); }
        private void OnDestroy() { GameInput.SetGameplay(false); Time.timeScale = 1f; }

        private string SlotLabel(int index)
        {
            var record = records[index];
            string prefix = "슬롯 " + (index + 1) + " · ";
            if (record == null) return prefix + (slotErrors[index] == null ? "빈 기록" : "불러오기 불가 · 원본 보존");
            var sword = record.Get(SkillRules.BasicSword);
            string when = DateTime.TryParse(record.savedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var utc)
                ? utc.ToLocalTime().ToString("yyyy년 MM월 dd일 HH:mm:ss") : "저장 시각 미상";
            string owned = string.Join(", ", record.skills.FindAll(s => s.acquired).ConvertAll(s => catalog.Get(s.skillId).displayName));
            return prefix + when + "\n보유 스킬 " + record.skills.FindAll(s => s.acquired).Count + "개 · " + owned
                + "\n기본 검술 " + sword.validUseCount + "회 · " + sword.stage + "단계";
        }
        private void OnGUI()
        {
            if (Settings == null || Playing) return;
            var previous = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2f,
                (Screen.height - 720 * scale) / 2f, 0), Quaternion.identity, Vector3.one * scale);
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                textStyle = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true };
                buttonStyle = new GUIStyle(GUI.skin.button) { font = font, fontSize = 20, alignment = TextAnchor.MiddleLeft, wordWrap = true, padding = new RectOffset(20, 12, 5, 5) };
                selectedStyle = new GUIStyle(buttonStyle); selectedStyle.normal.textColor = new Color(0.45f, 1f, 0.9f);
            }
            GUI.color = new Color(0.045f, 0.065f, 0.1f, 0.97f);
            GUI.DrawTexture(new Rect(-2000, -2000, 6000, 5000), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(new Rect(90, 24, 1100, 62), "《리와인드 공증록: 레아》", titleStyle);
            GUI.Label(new Rect(200, 95, 900, 36), Heading(), textStyle);
            ShellPage renderedPage = Page;
            if (Page == ShellPage.Slots)
            {
                for (int i = 0; i < 3; i++)
                {
                    Row(i, SlotLabel(i), new Rect(200, 148 + i * 122, 760, 110));
                    if (Page != renderedPage) break;
                    if (GUI.Button(new Rect(978, 175 + i * 122, 100, 52), "삭제", buttonStyle)) RequestDelete(i + 1);
                }
                if (Page == renderedPage) Row(3, "뒤로", new Rect(200, 520, 880, 46));
            }
            else if (Page == ShellPage.Stages)
            {
                for (int i = 0; i < StageCatalog.All.Length; i++)
                {
                    var stage = StageCatalog.All[i];
                    Row(i, stage.Name + "  [" + stage.Status(selectedRecord) + "]", new Rect(160, 141 + i * 44, 960, 40));
                    if (Page != renderedPage) break;
                }
                if (Page == renderedPage) Row(StageCatalog.All.Length, "뒤로", new Rect(160, 542, 960, 40));
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
                    GUI.Label(new Rect(210, 525, 880, 52), "좌우로 조절 · 단계 미리보기는 보유 기술에만 적용하며 성장 집계에서 제외됩니다.\n실제 단계로 돌아가도 이번 플레이의 검증 저장 격리는 유지됩니다.", textStyle);
                }
            }
            string status = message ?? (Page == ShellPage.Settings ? Settings.Error
                : Page == ShellPage.Slots && selected < 3 ? slotErrors[selected] : null);
            if (!string.IsNullOrEmpty(status)) GUI.Label(new Rect(160, 599, 960, 67), status, textStyle);
            GUI.Label(new Rect(160, 675, 1060, 32), "방향키 / 왼쪽 스틱 / 십자키: 선택   Enter / A: 확인   Esc / B: 뒤로   슬롯 삭제: Delete / X", textStyle);
            GUI.matrix = previous;
        }
        private void Row(int index, string label, Rect rect)
        {
            // GUI controls are mouse-only; all keyboard/gamepad dispatch happens once in Update.
            // Clearing focus avoids IMGUI also submitting Space on an old focused button.
            GUI.FocusControl(null);
            if (GUI.Button(rect, (selected == index ? "▶ " : "   ") + label, selected == index ? selectedStyle : buttonStyle))
            {
                selected = index;
                if (Page == ShellPage.Settings && index < 4) AdjustSetting(index, Event.current.mousePosition.x < rect.center.x ? -1 : 1);
                else Activate(index);
            }
        }
        private string Heading()
        {
            switch (Page)
            {
                case ShellPage.Main: return "기록을 이어갈 곳을 선택하세요";
                case ShellPage.Slots: return (purpose == SlotPurpose.NewGame ? "새 게임 · 빈 슬롯 선택" : purpose == SlotPurpose.Continue ? "계속하기 · 기록 선택" : "스테이지 선택 · 사용할 기록 선택") + " (3개 슬롯)";
                case ShellPage.Stages: return "스테이지 선택 · 슬롯 " + selectedStore.Slot + " · 현재 테스트 스테이지만 플레이할 수 있습니다";
                case ShellPage.Pause: return "일시정지";
                case ShellPage.Settings: return "설정 · 변경 즉시 적용 및 저장";
                case ShellPage.DeleteConfirm: return "슬롯 " + deleteSlot + "을 삭제할까요? 진행과 검증 기록·백업이 삭제됩니다.";
                default: return "저장 후 게임을 종료할까요?";
            }
        }
        private string[] Labels()
        {
            if (Page == ShellPage.Main) return new[] { "계속하기", "새 게임", "스테이지 선택", "설정", "종료" };
            if (Page == ShellPage.Pause) return new[] { "계속하기", "설정", "메인 메뉴로", "종료" };
            if (Page == ShellPage.DeleteConfirm) return new[] { "취소", "기록 삭제" };
            if (Page == ShellPage.QuitConfirm) return new[] { "취소", "저장 후 종료" };
            var value = Settings.Value;
            return new[] { "배경음악 볼륨    ◀ " + Mathf.RoundToInt(value.bgmVolume * 100) + "% ▶",
                "효과음 볼륨    ◀ " + Mathf.RoundToInt(value.sfxVolume * 100) + "% ▶",
                "화면 흔들림    " + (value.screenShake ? "켜짐" : "꺼짐"),
                "단계 오버라이드 (테스트용)    " + (!DebugAvailable ? "개발 빌드에서 사용 가능" : value.stageOverride == 0 ? "실제 단계" : value.stageOverride + "단계 미리보기"),
                "뒤로" };
        }
    }
}
