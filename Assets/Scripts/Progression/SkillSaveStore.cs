using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace RewindNotary
{
    // One instance owns one normal slot and its isolated debug file. Never load debug automatically.
    public sealed class SkillSaveStore
    {
        [Serializable] private sealed class SaveHeader { public int schemaVersion; public string rulesVersion; }
        private readonly string directory;
        private readonly SkillCatalog catalog;
        public int Slot { get; }
        public string SlotId => "slot-" + Slot;
        public string LastError { get; private set; }
        public bool WriteBlocked { get; private set; }
        private string Tombstone => Path.Combine(directory, SlotId + ".deleted");
        private string MigrationMarker => Path.Combine(directory, "legacy-imported");
        public SkillSaveStore(string directory, SkillCatalog catalog, int slot = 1)
        {
            if (slot < 1 || slot > 3) throw new ArgumentOutOfRangeException(nameof(slot));
            this.directory = directory; this.catalog = catalog; Slot = slot;
        }
        private string PathFor(bool debug) => Path.Combine(directory, SlotId + (debug ? "-debug.json" : ".json"));
        public bool Exists => !File.Exists(Tombstone) && (File.Exists(PathFor(false)) || File.Exists(PathFor(false) + ".bak"));
        public CoreRecord Load()
        {
            LastError = null; WriteBlocked = false;
            return Exists ? LoadFrom(PathFor(false), false) : null;
        }
        private CoreRecord LoadFrom(string path, bool legacy)
        {
            if (File.Exists(path))
            {
                try
                {
                    var header = JsonUtility.FromJson<SaveHeader>(File.ReadAllText(path));
                    // Future rules/schema must not be silently replaced by an older backup.
                    if (header != null && (header.schemaVersion > CoreRecord.SchemaVersion
                        || (!string.IsNullOrEmpty(header.rulesVersion) && header.rulesVersion != SkillRules.Version)))
                    { WriteBlocked = true; throw new InvalidDataException("지원하지 않는 저장 버전입니다. 원본을 보존했습니다."); }
                    return Read(path, legacy);
                }
                catch (Exception e) { LastError = e.Message; if (WriteBlocked) throw; }
            }
            try
            {
                var recovered = Read(path + ".bak", legacy);
                if (!legacy)
                {
                    if (File.Exists(path)) File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.Ticks, false);
                    File.Copy(path + ".bak", path, true);
                }
                LastError = "이전 정상 백업을 복구했습니다. 기존 원본은 보존했습니다.";
                return recovered;
            }
            catch (Exception e)
            {
                WriteBlocked = true;
                throw new InvalidDataException("저장을 복구할 수 없습니다. 원본 보존: " + LastError + " / " + e.Message);
            }
        }
        private CoreRecord Read(string path, bool legacy = false)
        {
            string json = File.ReadAllText(path);
            var header = JsonUtility.FromJson<SaveHeader>(json);
            if (header == null || header.schemaVersion < 1 || header.schemaVersion > CoreRecord.SchemaVersion
                || header.rulesVersion != SkillRules.Version)
                throw new InvalidDataException("저장 버전 또는 규칙 버전이 올바르지 않습니다.");
            var record = JsonUtility.FromJson<CoreRecord>(json);
            if (record == null) throw new InvalidDataException("빈 저장 파일입니다.");
            bool debug = !legacy && Path.GetFileName(path).StartsWith(SlotId + "-debug.json", StringComparison.Ordinal);
            if (header.schemaVersion == 1)
            {
                if (record.testData != debug || record.slotId != (debug ? "debug" : "main"))
                    throw new InvalidDataException("이전 저장 슬롯이 일치하지 않습니다.");
                record.schemaVersion = CoreRecord.SchemaVersion;
                record.slotId = debug ? "debug" : SlotId;
                record.storyFlags = new System.Collections.Generic.List<string> { "hub_open" };
                record.lastStageId = "prototype";
            }
            if (record.testData != debug || record.slotId != (debug ? "debug" : SlotId))
                throw new InvalidDataException("저장 슬롯이 일치하지 않습니다.");
            record.Validate(catalog);
            return record;
        }
        public CoreRecord Create()
        {
            if (Exists) throw new InvalidOperationException("사용 중인 슬롯입니다. 먼저 삭제해 주세요.");
            if (File.Exists(Tombstone)) { RemoveSlotFiles(); File.Delete(Tombstone); }
            WriteBlocked = false;
            var record = CoreRecord.NewGame(catalog); record.slotId = SlotId;
            if (!Save(record)) throw new IOException(LastError);
            return record;
        }
        public bool Save(CoreRecord record) => Write(record, true);
        private bool Write(CoreRecord record, bool stamp)
        {
            if (WriteBlocked || File.Exists(Tombstone)) return false;
            string path = PathFor(record.testData), temp = path + ".tmp";
            try
            {
                record.Validate(catalog);
                if (!record.testData && record.slotId != SlotId) throw new InvalidDataException("다른 슬롯의 기록입니다.");
                if (stamp) { record.revision++; record.savedUtc = DateTime.UtcNow.ToString("O"); }
                Directory.CreateDirectory(directory);
                WriteDurable(temp, JsonUtility.ToJson(record, true));
                Read(temp);
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
                else File.Move(temp, path);
                LastError = null; return true;
            }
            catch (Exception e) { LastError = "저장 실패 (정상본 보존): " + e.Message; Debug.LogWarning(LastError); return false; }
        }
        private static void WriteDurable(string path, string value)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
        }
        public void Delete()
        {
            Directory.CreateDirectory(directory);
            // Commit deletion before cleanup: a crash/partial cleanup cannot resurrect a backup or legacy save.
            if (Slot == 1) WriteDurable(MigrationMarker, "slot-1 retired");
            WriteDurable(Tombstone, "deleted");
            RemoveSlotFiles(); WriteBlocked = false; LastError = null;
        }
        private void RemoveSlotFiles()
        {
            foreach (bool debug in new[] { false, true })
            {
                string path = PathFor(debug);
                if (File.Exists(path)) File.Delete(path);
                foreach (string artifact in Directory.GetFiles(directory, Path.GetFileName(path) + ".*")) File.Delete(artifact);
            }
        }
        public static string MigrateLegacy(string directory, SkillCatalog catalog)
        {
            var target = new SkillSaveStore(directory, catalog, 1);
            string legacy = Path.Combine(directory, "skill-progress.json");
            if (File.Exists(target.MigrationMarker) || File.Exists(target.Tombstone) || target.Exists
                || (!File.Exists(legacy) && !File.Exists(legacy + ".bak"))) return null;
            var record = target.LoadFrom(legacy, true);
            string recovery = target.LastError;
            // Preserve every v1 field, including timestamp/revision, except the required version and slot identity.
            if (!target.Write(record, false)) throw new IOException(target.LastError);
            WriteDurable(target.MigrationMarker, "migrated v1 to slot-1");
            return "기존 기록을 슬롯 1로 이전했습니다. 기존 파일은 보존했습니다." + (recovery == null ? "" : " " + recovery);
        }
    }
}
