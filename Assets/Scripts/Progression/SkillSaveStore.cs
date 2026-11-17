using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace RewindNotary
{
    public sealed class SkillSaveStore
    {
        [Serializable]
        private sealed class SaveHeader
        {
            public int schemaVersion;
            public string rulesVersion;
        }
        private readonly string directory;
        private readonly SkillCatalog catalog;
        public string LastError { get; private set; }
        public bool WriteBlocked { get; private set; }
        public SkillSaveStore(string directory, SkillCatalog catalog) { this.directory = directory; this.catalog = catalog; }
        private string PathFor(bool debug) => Path.Combine(directory, debug ? "skill-progress-debug.json" : "skill-progress.json");
        public CoreRecord Load()
        {
            string path = PathFor(false);
            if (!File.Exists(path) && !File.Exists(path + ".bak")) return CoreRecord.NewGame(catalog);
            // A future version must never be silently replaced with an older backup.
            if (File.Exists(path))
            {
                try
                {
                    var header = JsonUtility.FromJson<SaveHeader>(File.ReadAllText(path));
                    if (header != null && (header.schemaVersion > CoreRecord.SchemaVersion
                        || (!string.IsNullOrEmpty(header.rulesVersion) && header.rulesVersion != SkillRules.Version)))
                    { WriteBlocked = true; throw new InvalidDataException("지원하지 않는 저장 버전입니다. 원본을 보존했습니다."); }
                    return Read(path);
                }
                catch (Exception e)
                {
                    LastError = e.Message;
                    if (WriteBlocked) throw;
                }
            }
            try
            {
                var recovered = Read(path + ".bak");
                if (File.Exists(path)) File.Copy(path, path + ".corrupt-" + DateTime.UtcNow.Ticks, false);
                // Restore only after validation; keep the good backup intact on the next save.
                File.Copy(path + ".bak", path, true);
                LastError = "손상된 저장을 보존하고 이전 정상 백업을 복구했습니다.";
                return recovered;
            }
            catch (Exception e)
            {
                WriteBlocked = true;
                throw new InvalidDataException("저장을 복구할 수 없습니다. 원본 보존: " + LastError + " / " + e.Message);
            }
        }
        private CoreRecord Read(string path)
        {
            string json = File.ReadAllText(path);
            var header = JsonUtility.FromJson<SaveHeader>(json);
            if (header == null || header.schemaVersion != CoreRecord.SchemaVersion || header.rulesVersion != SkillRules.Version)
                throw new InvalidDataException("Missing or unsupported save/rules version");
            var record = JsonUtility.FromJson<CoreRecord>(json);
            if (record == null) throw new InvalidDataException("Empty save");
            record.Validate(catalog);
            if (record.testData != Path.GetFileName(path).StartsWith("skill-progress-debug.json", StringComparison.Ordinal))
                throw new InvalidDataException("Save slot mismatch");
            return record;
        }
        public bool Save(CoreRecord record)
        {
            if (WriteBlocked) return false;
            string path = PathFor(record.testData), temp = path + ".tmp";
            try
            {
                record.Validate(catalog);
                record.revision++; record.savedUtc = DateTime.UtcNow.ToString("O");
                Directory.CreateDirectory(directory);
                // Synchronous, one writer, temp file on the same filesystem; no stale async snapshots.
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(record, true));
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                Read(temp);
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
                else File.Move(temp, path);
                LastError = null;
                return true;
            }
            catch (Exception e)
            {
                LastError = "저장 실패 (정상본 보존): " + e.Message;
                Debug.LogWarning(LastError);
                return false;
            }
        }
    }
}
