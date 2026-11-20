using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RewindNotary
{
    // Owns persistence/telemetry for this scene, independent of combat attempt resets.
    public sealed class SkillSession : MonoBehaviour
    {
        public SkillProgressionService Progression { get; private set; }
        public FinalBattleService Finale { get; private set; }
        public event Action<UseContext> TechniqueCompleted;
        public void ReportTechniqueCompleted(UseContext use)
        { if (use != null && use.Counted && !use.Closed && !use.Preview && use.Stage == 3) TechniqueCompleted?.Invoke(use); }
        public InvestigationService Investigation { get; private set; }
        public StorySceneLinks Story { get; private set; }
        private Func<string> notice;
        private readonly Queue<StoryRow> storyNotices = new Queue<StoryRow>();
        private List<StoryRow> returnNotice;
        private float storyNoticeLeft, returnNoticeLeft;
        public string Notice => returnNotice != null ? string.Join("\n\n", returnNotice.ConvertAll(StoryText))
            : storyNotices.Count > 0 ? StoryText(storyNotices.Peek()) : notice?.Invoke();
        internal static string StoryText(StoryRow row) => Loc.Get("area.speaker." + row.speakerId) + " · " + Loc.Get(row.textKey);
        internal void EnqueueStoryNotice(StoryRow row)
        {
            storyNotices.Enqueue(row);
            if (storyNotices.Count == 1) StartStoryNotice();
        }
        internal void ShowReturnNotice(List<StoryRow> lines)
        {
            if (lines.Count == 0) return;
            returnNotice = lines;
            returnNoticeLeft = Mathf.Clamp(Notice.Length / 25f, 3.5f, 22f);
        }
        internal void ClearStoryNotices()
        {
            storyNotices.Clear();
            returnNotice = null;
        }
        private void StartStoryNotice()
        {
            storyNoticeLeft = Mathf.Clamp(StoryText(storyNotices.Peek()).Length / 25f, 3.5f, 14f);
        }
        public string StorageError => string.Join("\n", new[] { store?.LastError, logError }).Trim();
        public bool LastSaveSucceeded => !dirty;
        internal bool AcceptsStoryActions => !closed && Progression != null;
        public int ActiveSlot => store.Slot;
        public int PreviewStage { get; private set; }
        public string EncounterId { get; set; } = EncounterRewardBridge.PrototypeEncounter;
        public event Action<string, string> PresentationRequested;
        public event Action<string> SoundRequested;
        private SkillSaveStore store;
        private string logPath, logError;
        public string SaveDirectory { get; private set; }
        public string UseLogPath => logPath;
        private float noticeUntil, retryAt;
        private bool dirty, closed, standaloneDuel;
        private readonly string sessionId = Guid.NewGuid().ToString("N");
        [Serializable]
        private sealed class LogRow
        {
            public string utc, sessionId, rulesVersion, buildId, encounterId, slotId;
            public float gameSeconds, realtimeSeconds;
            public ProgressionEvent result;
        }
        public void Initialize(SkillSaveStore selectedStore, CoreRecord record)
        {
            var catalog = SkillCatalog.Load();
            SaveDirectory = selectedStore.DirectoryPath;
            store = selectedStore;
            string recoveryNotice = store.RecoveryNotice ?? store.LastError;
            logPath = Path.Combine(SaveDirectory, "uses-" + sessionId + ".jsonl");
            standaloneDuel = record.lastStageId == "echo_duel";
            Progression = new SkillProgressionService(catalog, record);
            if (record.lastStageId != "cassel") record.finalBattleAttempt.Stop("interrupted");
            Finale = new FinalBattleService(Progression, Save, storesPrevious: true);
            Investigation = new InvestigationService(Progression, InvestigationCatalog.Load(), Save);
            Story = new StorySceneLinks(this);
            Progression.Changed += Save;
            Progression.Event += OnEvent;
            Progression.EmitSimple("session_start", "", "loaded_or_new_120");
            Save();
            Debug.Log("[Skill] 저장: " + SaveDirectory + " / 슬롯: " + store.Slot + " / 사용 로그: " + logPath);
            if (!string.IsNullOrEmpty(recoveryNotice)) Show(() => recoveryNotice, "save_recovered", "");
        }
        private bool sceneTransaction;
        // Existing clue/skill/support callbacks request saves. Flush once after the row and floor join them.
        internal bool CommitScene(Func<bool> effect, Action checkpoint)
        {
            if (!LastSaveSucceeded || !AcceptsStoryActions) return false;
            sceneTransaction = true;
            try
            {
                if (!effect()) return false;
                checkpoint();
            }
            finally { sceneTransaction = false; }
            Save();
            return true; // In-memory transaction accepted. The world waits for LastSaveSucceeded.
        }
        public void Save()
        {
            if (Progression == null || closed || sceneTransaction) return;
            if (standaloneDuel) return; // The stage selector owns the untouched campaign record.
            dirty = !store.Save(Progression.Record, Finale.PendingPrevious);
            if (!dirty) Finale.Saved();
            retryAt = Time.unscaledTime + 5f;
        }
        private void Update()
        {
            if (returnNotice != null)
            {
                returnNoticeLeft -= Time.deltaTime;
                if (returnNoticeLeft <= 0) returnNotice = null;
            }
            else if (storyNotices.Count > 0 && (storyNoticeLeft -= Time.deltaTime) <= 0)
            {
                storyNotices.Dequeue();
                if (storyNotices.Count > 0) StartStoryNotice();
            }
            if (Time.unscaledTime > noticeUntil) notice = null;
            if (dirty && Time.unscaledTime >= retryAt) Save();
        }
        private void OnEvent(ProgressionEvent result)
        {
            var row = new LogRow { utc = DateTime.UtcNow.ToString("O"), sessionId = sessionId, slotId = store.SlotId,
                rulesVersion = SkillRules.Version, buildId = Application.version,
                encounterId = EncounterId,
                gameSeconds = Time.time, realtimeSeconds = Time.unscaledTime, result = result };
            try
            {
                Directory.CreateDirectory(SaveDirectory);
                File.AppendAllText(logPath, JsonUtility.ToJson(row) + Environment.NewLine); logError = null;
            }
            catch (Exception e) { logError = Loc.Get("save.usage_log_failed", e.Message); Debug.LogWarning(logError); }
            Func<string> name = () => string.IsNullOrEmpty(result.skillId) ? "" : Progression.Catalog.Get(result.skillId).DisplayName;
            switch (result.kind)
            {
                case "acquired": Show(() => Loc.Get("skill.acquired_notice", name()), "acquired", result.skillId); break;
                case "stage_up": Show(() => Loc.Get("skill.stage_up_notice", name(), result.stage), "stage_up", result.skillId); break;
                case "doom_transfer":
                    if (RepresentativeScript.Handles(Progression.Record.lastStageId) && result.skillId != SkillRules.SilverStitch) break;
                    Show(() => Loc.Get("skill.doom_transfer_notice", DoomLabel(result.skillId)), "doom_transfer", result.skillId); break;
                case "doom_restored": Show(() => Loc.Get("skill.doom_restored_notice", DoomLabel(result.skillId)), "doom_restored", result.skillId); break;
                case "basic_mastery": Show(() => Loc.Get("skill.true_name_notice"), "basic_mastery", result.skillId); break;
                case "debug_seed": Show(() => Loc.Get("skill.debug_boundary_notice", result.reason), "debug", ""); break;
            }
        }
        private string DoomLabel(string skillId)
        {
            switch (Progression.Catalog.Get(skillId).doomId)
            {
                case "heart_rupture": return Loc.Get("doom.heart_rupture");
                case "rapid_aging": return Loc.Get("doom.rapid_aging");
                case "leg_punishment": return Loc.Get("doom.leg_punishment");
                case "eternal_silence": return Loc.Get("doom.eternal_silence");
                case "blindness": return Loc.Get("doom.blindness");
                default: return "";
            }
        }
        public void ShowUnlearnedSkill(string skillId)
        {
            // Presentation only: no use, telemetry, progression/save mutation or evolution cue.
            notice = () => Loc.Get("input.skill_unlearned", Progression.Catalog.Get(skillId).DisplayName);
            noticeUntil = Time.unscaledTime + 1.5f;
        }
        private void Show(Func<string> message, string cue, string skillId)
        {
            notice = message; noticeUntil = Time.unscaledTime + 3.5f;
            Debug.Log("[Skill] " + message());
            // Full narrative/audio assets can subscribe here; never take ownership of Time.timeScale.
            PresentationRequested?.Invoke(cue, skillId);
            SoundRequested?.Invoke(cue);
        }
        public void SetPreview(int stage)
        {
            if (stage < 0 || stage > 3) throw new ArgumentOutOfRangeException(nameof(stage));
            if (stage != 0) Progression.EnableDebugRecord();
            if (stage != 0) Progression.GrantPracticeSkills();
            PreviewStage = stage;
            Progression.EmitSimple("debug_preview", "", "stage=" + stage);
        }
        public void PresentRestoredState()
        {
            // The durable flag is a narrative hand-off latch; restoration does not transfer twice.
            if (Progression.Record.firstTransferPending)
                foreach (var entry in Progression.Record.ledger)
                    if (entry.transferredToRea && (!RepresentativeScript.Handles(Progression.Record.lastStageId) || entry.skillId == SkillRules.SilverStitch))
                        Progression.EmitSimple("doom_restored", entry.skillId, entry.transferEventId);
        }
        private void OnApplicationPause(bool paused) { if (paused) Save(); }
        private void OnApplicationQuit() { Save(); }
        private void OnDestroy() { Close(); }
        public void Close(bool save = true)
        {
            if (Progression == null || closed) return;
            if (save) Save(); Progression.Changed -= Save; Progression.Event -= OnEvent;
            closed = true; dirty = false;
        }
    }
}
