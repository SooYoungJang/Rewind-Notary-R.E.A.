using System;
using System.Collections.Generic;

namespace RewindNotary
{
    [Serializable]
    public sealed class SkillProgress
    {
        public string skillId, acquiredEncounterId;
        public bool acquired;
        public int validUseCount, stage; // stage is a checked cache; count + catalog are authoritative.
    }
    [Serializable]
    public sealed class NotaryEntry
    {
        public string doomId, skillId, ownerId, transferEventId;
        public bool transferredToRea;
    }
    [Serializable]
    public sealed class CoreRecord
    {
        public const int SchemaVersion = 2;
        public int schemaVersion = SchemaVersion;
        public string rulesVersion = SkillRules.Version;
        public string slotId = "slot-1", savedUtc;
        public long revision;
        public bool testData, basicMasteryReached, firstTransferPending;
        public List<SkillProgress> skills = new List<SkillProgress>();
        public List<NotaryEntry> ledger = new List<NotaryEntry>();
        public List<string> completedEncounters = new List<string>();
        public List<string> storyFlags = new List<string> { "hub_open" };
        public string lastStageId = "prototype";
        public SkillProgress Get(string id) => skills.Find(s => s.skillId == id);
        public static CoreRecord NewGame(SkillCatalog catalog)
        {
            var record = new CoreRecord();
            foreach (var definition in catalog.skills)
            {
                bool basic = definition.skillId == SkillRules.BasicSword;
                record.skills.Add(new SkillProgress { skillId = definition.skillId, acquired = basic,
                    validUseCount = basic ? SkillRules.InitialSwordUses : 0, stage = 1,
                    acquiredEncounterId = basic ? "record_01" : "" });
                if (!basic) record.ledger.Add(new NotaryEntry { doomId = definition.doomId,
                    skillId = definition.skillId, ownerId = definition.sourceId, transferEventId = "" });
            }
            return record;
        }
        public void Validate(SkillCatalog catalog)
        {
            if (schemaVersion != SchemaVersion || rulesVersion != SkillRules.Version)
                throw new InvalidOperationException("Unsupported save/rules version; original file retained");
            if (skills == null || skills.Count != 6 || ledger == null || ledger.Count != 5 || completedEncounters == null)
                throw new InvalidOperationException("Incomplete core record");
            if (testData != (slotId == "debug") || (!testData && slotId != "slot-1" && slotId != "slot-2" && slotId != "slot-3"))
                throw new InvalidOperationException("Invalid save slot");
            if (storyFlags == null || string.IsNullOrEmpty(lastStageId))
                throw new InvalidOperationException("Incomplete story state");
            var flags = new HashSet<string>();
            foreach (string flag in storyFlags)
                if (string.IsNullOrWhiteSpace(flag) || !flags.Add(flag))
                    throw new InvalidOperationException("Invalid story flag");
            var ids = new HashSet<string>();
            foreach (var progress in skills)
            {
                if (progress == null) throw new InvalidOperationException("Null skill progress");
                var definition = catalog.Get(progress.skillId);
                if (!ids.Add(progress.skillId) || progress.validUseCount < 0 || progress.validUseCount == int.MaxValue
                    || (!progress.acquired && (progress.validUseCount != 0 || !string.IsNullOrEmpty(progress.acquiredEncounterId)))
                    || (progress.acquired && string.IsNullOrEmpty(progress.acquiredEncounterId)))
                    throw new InvalidOperationException("Invalid skill progress");
                progress.stage = definition.Stage(progress.validUseCount);
            }
            if (!Get(SkillRules.BasicSword).acquired || basicMasteryReached != (Get(SkillRules.BasicSword).stage == 3))
                throw new InvalidOperationException("Invalid basic mastery flag");
            ids.Clear();
            foreach (var entry in ledger)
            {
                if (entry == null) throw new InvalidOperationException("Null notary entry");
                var definition = catalog.Get(entry.skillId);
                bool mastered = Get(entry.skillId).acquired && Get(entry.skillId).stage == 3;
                if (string.IsNullOrEmpty(definition.doomId) || definition.doomId != entry.doomId || !ids.Add(entry.doomId)
                    || entry.transferredToRea != mastered || entry.ownerId != (mastered ? "rea" : definition.sourceId)
                    || (mastered ? entry.transferEventId != "mastery:" + entry.skillId : !string.IsNullOrEmpty(entry.transferEventId)))
                    throw new InvalidOperationException("Invalid notary ledger");
            }
            bool anyTransferred = ledger.Exists(e => e.transferredToRea);
            if (firstTransferPending != anyTransferred) throw new InvalidOperationException("Invalid pending transfer hook");
            ids.Clear();
            foreach (string encounter in completedEncounters)
                if (string.IsNullOrEmpty(encounter) || !ids.Add(encounter)) throw new InvalidOperationException("Invalid encounter history");
        }
    }
}
