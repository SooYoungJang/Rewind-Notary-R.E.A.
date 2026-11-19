using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace RewindNotary
{
    public enum SaveLoadStatus { NotLoaded, Empty, Loaded, RecoveredBackup, RecoveryFailed, UnsupportedVersion }
    // One instance owns one normal slot and its isolated debug file. Never load debug automatically.
    public sealed class SkillSaveStore
    {
        [Serializable] private sealed class SaveHeader { public int schemaVersion; public string rulesVersion; public InvestigationState investigation; public FinalBattleAttempt finalBattleAttempt; }
        private readonly string directory;
        private readonly SkillCatalog catalog;
        internal string DirectoryPath => directory;
        public int Slot { get; }
        public string SlotId => "slot-" + Slot;
        public string LastError { get; private set; }
        public bool WriteBlocked { get; private set; }
        public SaveLoadStatus LoadStatus { get; private set; }
        public string RecoveryNotice { get; private set; }
        public bool RequiresUserAttention => LoadStatus == SaveLoadStatus.RecoveredBackup
            || LoadStatus == SaveLoadStatus.RecoveryFailed || LoadStatus == SaveLoadStatus.UnsupportedVersion;
        private string Tombstone => Path.Combine(directory, SlotId + ".deleted");
        private string MigrationMarker => Path.Combine(directory, "legacy-imported");
        public SkillSaveStore(string directory, SkillCatalog catalog, int slot = 1)
        {
            if (slot < 1 || slot > AutoTestRuntime.SlotCount) throw new ArgumentOutOfRangeException(nameof(slot));
            this.directory = directory; this.catalog = catalog; Slot = slot;
        }
        private string PathFor(bool debug) => Path.Combine(directory, SlotId + (debug ? "-debug.json" : ".json"));
        public bool Exists => !File.Exists(Tombstone) && (File.Exists(PathFor(false)) || File.Exists(PathFor(false) + ".bak"));
        public CoreRecord Load()
        {
            LastError = null; WriteBlocked = false;
            if (!Exists) { LoadStatus = SaveLoadStatus.Empty; RecoveryNotice = null; return null; }
            var record = LoadFrom(PathFor(false), false);
            if (LoadStatus != SaveLoadStatus.RecoveredBackup) { LoadStatus = SaveLoadStatus.Loaded; RecoveryNotice = null; }
            return record;
        }
        private CoreRecord LoadFrom(string path, bool legacy)
        {
            if (File.Exists(path))
            {
                try
                {
                    return Read(path, legacy);
                }
                catch (UnsupportedSaveVersionException e)
                {
                    // Probe the backup, but never roll a newer save back automatically.
                    string backup;
                    try { Read(path + ".bak", legacy); backup = Loc.Get("save.backup_readable"); }
                    catch (Exception) { backup = Loc.Get("save.backup_unreadable"); }
                    WriteBlocked = true; LoadStatus = SaveLoadStatus.UnsupportedVersion;
                    RecoveryNotice = LastError = Loc.Get("save.use_compatible_version", e.Message, backup);
                    throw new InvalidDataException(LastError, e);
                }
                catch (Exception e) { LastError = e.Message; }
            }
            try
            {
                var recovered = Read(path + ".bak", legacy);
                if (!legacy)
                {
                    string temp = path + ".tmp";
                    WriteDurable(temp, ReadText(path + ".bak"));
                    Read(temp);
                    // Atomic recovery preserves the damaged primary; .bak remains the last good save.
                    if (File.Exists(path)) File.Replace(temp, path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                    else File.Move(temp, path);
                }
                LoadStatus = SaveLoadStatus.RecoveredBackup;
                RecoveryNotice = LastError = Loc.Get("save.backup_restored");
                return recovered;
            }
            catch (Exception e)
            {
                WriteBlocked = true;
                LoadStatus = SaveLoadStatus.RecoveryFailed;
                RecoveryNotice = LastError = Loc.Get("save.recovery_failed", LastError, e.Message);
                throw new InvalidDataException(LastError, e);
            }
        }
        private CoreRecord Read(string path, bool legacy = false)
        {
            string json = ReadText(path);
            var fields = SaveJsonIntegrity.Validate(json);
            var header = JsonUtility.FromJson<SaveHeader>(json);
            if (header == null || header.schemaVersion < 1 || header.schemaVersion > CoreRecord.SchemaVersion
                || header.rulesVersion != SkillRules.Version)
                throw new InvalidDataException(Loc.Get("save.invalid_version"));
            var record = JsonUtility.FromJson<CoreRecord>(json);
            if (record == null) throw new InvalidDataException(Loc.Get("save.empty_file"));
            if (header.schemaVersion >= 3 && header.investigation == null)
                throw new InvalidDataException(Loc.Get("save.missing_investigation"));
            if (header.schemaVersion < 3)
            {
                record.schemaVersion = CoreRecord.SchemaVersion;
                if (header.investigation == null || record.investigation == null) record.investigation = new InvestigationState();
            }
            if (header.schemaVersion >= 4 && header.finalBattleAttempt == null)
                throw new InvalidDataException(Loc.Get("save.missing_finale"));
            if (header.schemaVersion < 4)
            {
                if (header.finalBattleAttempt == null) record.finalBattleAttempt = new FinalBattleAttempt();
                record.schemaVersion = CoreRecord.SchemaVersion;
            }
            bool debug = !legacy && Path.GetFileName(path).StartsWith(SlotId + "-debug.json", StringComparison.Ordinal);
            if (header.schemaVersion == 1)
            {
                if (record.testData != debug || record.slotId != (debug ? "debug" : "main"))
                    throw new InvalidDataException(Loc.Get("save.legacy_slot_mismatch"));
                record.schemaVersion = CoreRecord.SchemaVersion;
                record.slotId = debug ? "debug" : SlotId;
                if (!fields.ContainsKey("storyFlags")) record.storyFlags = new System.Collections.Generic.List<string> { "hub_open" };
                if (!fields.ContainsKey("lastStageId")) record.lastStageId = "prototype";
            }
            if (record.testData != debug || record.slotId != (debug ? "debug" : SlotId))
                throw new InvalidDataException(Loc.Get("save.slot_mismatch"));
            record.Validate(catalog);
            return record;
        }
        public CoreRecord Create() => Create("prototype");
        public CoreRecord Create(string stage)
        {
            if (Exists) throw new InvalidOperationException(Loc.Get("save.slot_in_use"));
            if (File.Exists(Tombstone)) { RemoveSlotFiles(); File.Delete(Tombstone); }
            WriteBlocked = false; LoadStatus = SaveLoadStatus.NotLoaded; RecoveryNotice = null;
            var record = CoreRecord.NewGame(catalog); record.slotId = SlotId; record.lastStageId = stage;
            if (!Save(record)) throw new IOException(LastError);
            return record;
        }
        public bool Save(CoreRecord record) => Write(record, true);
        // Publish an inactive bank first. The primary selects it only in the subsequent atomic save.
        // A failed write leaves the previously published bank reachable, including on process restart.
        internal bool Save(CoreRecord record, CoreRecord previous)
        {
            if (previous == null) return Save(record);
            try
            {
                if (WriteBlocked || File.Exists(Tombstone)) throw new IOException(RecoveryNotice ?? Loc.Get("save.slot_blocked"));
                if (record.testData || record.slotId != SlotId || previous.slotId != SlotId
                    || previous.testData || previous.lastStageId != EndingCheckpoint.Lift)
                    throw new InvalidDataException(Loc.Get("save.slot_mismatch"));
                string marker = EndingCheckpoint.Marker(record);
                if (marker == null || EndingCheckpoint.Marker(previous) != marker)
                    throw new InvalidDataException(Loc.Get("save.missing_finale"));
                // Check the current primary (including newer-version blocking) before touching any bank.
                var current = LoadFrom(PathFor(false), false);
                string published = EndingCheckpoint.Marker(current);
                if (published != null && EndingCheckpoint.IsBankA(published) == EndingCheckpoint.IsBankA(marker))
                    throw new InvalidDataException(Loc.Get("save.slot_blocked"));
                string path = PreviousPath(marker), temp = path + ".tmp";
                if (File.Exists(path))
                {
                    try { Read(path); }
                    catch (UnsupportedSaveVersionException) { throw; }
                    catch (InvalidDataException) { } // A damaged inactive bank can be replaced.
                    catch (InvalidOperationException) { }
                }
                WriteDurable(temp, JsonUtility.ToJson(previous, true));
                ReadPrevious(temp, marker);
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return Save(record);
            }
            catch (Exception e) { LastError = Loc.Get("save.failed_preserved", e.Message); return false; }
        }
        private string PreviousPath(string marker) => PathFor(false) +
            (EndingCheckpoint.IsBankA(marker) ? ".previous-a" : ".previous-b");
        private CoreRecord ReadPrevious(string path, string marker)
        {
            var previous = Read(path);
            if (previous.lastStageId != EndingCheckpoint.Lift || previous.finalBattleAttempt.outcome != "ready"
                || EndingCheckpoint.Marker(previous) != marker
                || FinaleEndingEvaluator.Evaluate(previous.lastFinalBattleAttempt) != FinaleEnding.Basic
                || !marker.EndsWith(":" + previous.lastFinalBattleAttempt.attemptId, StringComparison.Ordinal)
                || !StageCatalog.Find("cassel").Unlocked(previous))
                throw new InvalidDataException(Loc.Get("save.missing_finale"));
            return previous;
        }
        public CoreRecord RestorePrevious()
        {
            var current = Load();
            string marker = current == null ? null : EndingCheckpoint.Marker(current);
            if (marker == null || !current.storyFlags.Contains(EndingCheckpoint.Basic))
                throw new InvalidDataException(Loc.Get("save.missing_finale"));
            var previous = ReadPrevious(PreviousPath(marker), marker);
            EndingCheckpoint.Resume(current, previous);
            if (!Save(current)) throw new IOException(LastError);
            return current;
        }
        // Old v4 settlements have no entry snapshot. Recover their available progress on first use.
        internal void ImportEnding(CoreRecord record)
        {
            int flagsBefore = record.storyFlags.Count;
            EndingCheckpoint.Remember(record, record.lastFinalBattleAttempt);
            EndingCheckpoint.Remember(record, record.finalBattleAttempt);
            if (record.lastStageId != "cassel" || EndingCheckpoint.Marker(record) != null
                || FinaleEndingEvaluator.Evaluate(record.finalBattleAttempt) != FinaleEnding.Basic
                || !StageCatalog.Find("cassel").Unlocked(record))
            {
                if (record.storyFlags.Count != flagsBefore && !Save(record)) throw new IOException(LastError);
                return;
            }
            var previous = EndingCheckpoint.BeforeBattle(record);
            EndingCheckpoint.Publish(record, previous);
            if (!Save(record, previous)) throw new IOException(LastError);
        }
        private bool Write(CoreRecord record, bool stamp)
        {
            if (WriteBlocked || File.Exists(Tombstone))
            { LastError = RecoveryNotice ?? Loc.Get("save.slot_blocked"); return false; }
            long revision = record?.revision ?? 0;
            string savedUtc = record?.savedUtc;
            try
            {
                if (record == null) throw new InvalidDataException(Loc.Get("save.no_record"));
                string path = PathFor(record.testData), temp = path + ".tmp";
                record.Validate(catalog);
                if (!record.testData && record.slotId != SlotId) throw new InvalidDataException(Loc.Get("save.wrong_slot"));
                // Never rotate a damaged/unsupported primary over the only good backup.
                if (File.Exists(path) || File.Exists(path + ".bak")) LoadFrom(path, false);
                if (stamp) { record.revision = checked(record.revision + 1); record.savedUtc = DateTime.UtcNow.ToString("O"); }
                Directory.CreateDirectory(directory);
                WriteDurable(temp, JsonUtility.ToJson(record, true));
                Read(temp);
                if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
                else File.Move(temp, path);
                LastError = null; return true;
            }
            catch (Exception e)
            {
                if (record != null) { record.revision = revision; record.savedUtc = savedUtc; }
                LastError = Loc.Get("save.failed_preserved", e.Message); Debug.LogWarning(LastError); return false;
            }
        }
        private static string ReadText(string path) => File.ReadAllText(path, new UTF8Encoding(false, true));
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
            RemoveSlotFiles(); WriteBlocked = false; LastError = null; RecoveryNotice = null; LoadStatus = SaveLoadStatus.Empty;
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
            return Loc.Get("save.legacy_migrated", (recovery == null ? "" : " " + recovery));
        }
    }
}
