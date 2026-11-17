using System;
using System.IO;
using UnityEngine;

namespace RewindNotary
{
    // Owns persistence/telemetry for this scene, independent of combat attempt resets.
    public sealed class SkillSession : MonoBehaviour
    {
        public SkillProgressionService Progression { get; private set; }
        public string Notice { get; private set; }
        public string StorageError => string.Join("\n", new[] { store?.LastError, logError }).Trim();
        public bool LastSaveSucceeded => !dirty;
        public int ActiveSlot => store.Slot;
        public int PreviewStage { get; private set; }
        public int ParrySuccessCount { get; private set; }
        public event Action<string, string> PresentationRequested;
        public event Action<string> SoundRequested;
        private SkillSaveStore store;
        private string logPath, logError;
        public string SaveDirectory { get; private set; }
        public string UseLogPath => logPath;
        private float noticeUntil, retryAt;
        private bool dirty, closed;
        private readonly string sessionId = Guid.NewGuid().ToString("N");
        [Serializable]
        private sealed class LogRow
        {
            public string utc, sessionId, rulesVersion, buildId, encounterId, slotId;
            public float gameSeconds, realtimeSeconds;
            public int parrySuccessCount;
            public ProgressionEvent result;
        }
        public void Initialize(SkillSaveStore selectedStore, CoreRecord record)
        {
            var catalog = SkillCatalog.Load();
            SaveDirectory = Path.Combine(Application.persistentDataPath, "SkillSystem");
            store = selectedStore;
            string recoveryNotice = store.LastError;
            logPath = Path.Combine(SaveDirectory, "uses-" + sessionId + ".jsonl");
            Progression = new SkillProgressionService(catalog, record);
            Progression.Changed += Save;
            Progression.Event += OnEvent;
            Progression.EmitSimple("session_start", "", "loaded_or_new_120");
            Save();
            Debug.Log("[Skill] 저장: " + SaveDirectory + " / 슬롯: " + store.Slot + " / 사용 로그: " + logPath);
            if (!string.IsNullOrEmpty(recoveryNotice)) Show(recoveryNotice, "save_recovered", "");
        }
        public void Save()
        {
            if (Progression == null || closed) return;
            dirty = !store.Save(Progression.Record);
            retryAt = Time.unscaledTime + 5f;
        }
        private void Update()
        {
            if (Time.unscaledTime > noticeUntil) Notice = null;
            if (dirty && Time.unscaledTime >= retryAt) Save();
        }
        private void OnEvent(ProgressionEvent result)
        {
            var row = new LogRow { utc = DateTime.UtcNow.ToString("O"), sessionId = sessionId, slotId = store.SlotId,
                rulesVersion = SkillRules.Version, buildId = Application.version,
                encounterId = EncounterRewardBridge.PrototypeEncounter,
                gameSeconds = Time.time, realtimeSeconds = Time.unscaledTime, result = result,
                parrySuccessCount = ParrySuccessCount };
            try
            {
                Directory.CreateDirectory(SaveDirectory);
                File.AppendAllText(logPath, JsonUtility.ToJson(row) + Environment.NewLine); logError = null;
            }
            catch (Exception e) { logError = "사용 로그 기록 실패: " + e.Message; Debug.LogWarning(logError); }
            string name = string.IsNullOrEmpty(result.skillId) ? "" : Progression.Catalog.Get(result.skillId).displayName;
            switch (result.kind)
            {
                case "acquired": Show(name + " 획득 — Q / 패드 Y로 사용", "acquired", result.skillId); break;
                case "stage_up": Show(name + " " + result.stage + "단계 — 동작 체화", "stage_up", result.skillId); break;
                case "doom_transfer": Show("극의 완전 재현 · 제1종언: 심장 파열 → 레아", "doom_transfer", result.skillId); break;
                case "doom_restored": Show("종언 원장 복구 · 제1종언: 심장 파열 → 레아", "doom_restored", result.skillId); break;
                case "basic_mastery": Show("진명 개방: 카셀 제0식 · 원형 동작 확인 (종언 추가 없음)", "basic_mastery", result.skillId); break;
                case "debug_seed": Show("검증 전용 저장 · 경계 " + result.reason, "debug", ""); break;
            }
        }
        public void RecordParrySuccess()
        {
            if (Progression == null || closed) return;
            ParrySuccessCount++;
            // Session telemetry only. No BeginUse/Apply, Changed event, save schema or mastery mutation.
            OnEvent(new ProgressionEvent { kind = "parry_success", skillId = "", useId = "",
                reason = "defense_only;preview=" + PreviewStage, testData = Progression.Record.testData });
        }
        private void Show(string message, string cue, string skillId)
        {
            Notice = message; noticeUntil = Time.unscaledTime + 3.5f;
            Debug.Log("[Skill] " + message);
            // Full narrative/audio assets can subscribe here; never take ownership of Time.timeScale.
            PresentationRequested?.Invoke(cue, skillId);
            SoundRequested?.Invoke(cue);
        }
        public void SetPreview(int stage)
        {
            if (stage < 0 || stage > 3) throw new ArgumentOutOfRangeException(nameof(stage));
            if (stage != 0) Progression.EnableDebugRecord();
            PreviewStage = stage;
            Progression.EmitSimple("debug_preview", "", "stage=" + stage);
        }
        public void PresentRestoredState()
        {
            // The durable flag is a narrative hand-off latch; restoration does not transfer twice.
            if (Progression.Record.firstTransferPending)
                foreach (var entry in Progression.Record.ledger)
                    if (entry.transferredToRea) Progression.EmitSimple("doom_restored", entry.skillId, entry.transferEventId);
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
